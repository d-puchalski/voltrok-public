using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;
using Npgsql;
using Serilog;

using VoltrokEF;
using VoltrokUtils;

namespace VoltrokServices.Services.Auth;

[method: ActivatorUtilitiesConstructor]
public class AuthService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostEnvironment hostEnvironment,
    ProtectedLocalStorage? protectedLocalStorage = null)
{
    private const string MapFileName = "ne_10m_admin_0_countries.json";
    private const string TokenStorageKey = "auth_token";
    private const string UsersEmailConstraintName = "users_email_key";
    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    public AuthService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ProtectedLocalStorage protectedLocalStorage,
        IHostEnvironment hostEnvironment)
        : this(dbContextFactory, hostEnvironment, protectedLocalStorage)
    {
    }

    public async Task<bool> LoginAsync(User request)
    {
        if (protectedLocalStorage is null)
        {
            throw new InvalidOperationException("Protected local storage is not available in this host.");
        }

        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var normalizedEmail = request.Email?.Trim() ?? string.Empty;
        var user = await dbContext.Users
            .Include(q => q.Player)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null || !VerifyPassword(request.Password, user.Password))
        {
            await RecordLoginAttemptAsync(dbContext, normalizedEmail, null, false, "invalid_credentials");
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        if (user.IsActive == false)
        {
            await RecordLoginAttemptAsync(dbContext, normalizedEmail, user, false, "inactive_account");
            throw new UnauthorizedAccessException("Account is inactive");
        }

        // Update last login
        user.LastLogin = DateTime.UtcNow;
        dbContext.Users.Update(user);
        dbContext.UserLoginHistories.Add(new UserLoginHistory
        {
            UsersId = user.UsersId,
            IsSuccessful = true,
            AttemptedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        await protectedLocalStorage.SetAsync(TokenStorageKey, user.Player!.PlayersId);

        return true;
    }

    public async Task<bool> SignupAsync(User requestUser, Player requestPlayer, bool persistAuthToken = true)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var nowUtc = DateTime.UtcNow;
        var normalizedEmail = requestUser.Email?.Trim() ?? string.Empty;

        if (!EmailAddressValidator.IsValid(normalizedEmail))
        {
            throw new InvalidOperationException("Invalid email address");
        }

        // Check if user exists
        var existingUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (existingUser != null)
        {
            throw new InvalidOperationException("Email already exists");
        }

        var hashedPassword = HashPassword(requestUser.Password);
        var userId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var country = await dbContext.Countries.FirstOrDefaultAsync(c => c.CountriesId == requestPlayer.CountriesId);
        var cords = ResolveSignupCoordinates(country?.IsoCode2, null, country?.Name);
        var playerName = string.IsNullOrWhiteSpace(requestPlayer.Name)
            ? $"Player {playerId}"
            : requestPlayer.Name.Trim();

        dbContext.Users.Add(new User
        {
            UsersId = userId,
            Email = normalizedEmail,
            Password = hashedPassword,
            AuthProvider = "local",
            LastLogin = nowUtc,
            IsActive = true,
            CreatedAt = nowUtc
        });

        dbContext.Players.Add(new Player
        {
            PlayersId = playerId,
            CountriesId = requestPlayer.CountriesId,
            UsersId = userId,
            Name = playerName,
            LocationX = (decimal)cords.Longitude,
            LocationY = (decimal)cords.Latitude,
            Money = requestPlayer.IsNpc ? 80000 : 250000,
            Oil = 1000,
            Chips = 1000,
            Uranium = 1000,
            PlayerAvatar = requestPlayer.PlayerAvatar,
            IsNpc = requestPlayer.IsNpc,
            BattlePassValidTill = nowUtc.AddHours(48),
            BattlePassTrialGrantedAt = nowUtc
        });

        if (country is { CountriesId: var selectedCountryId, PresidentPlayersId: null })
        {
            var firstCountryPlayerId = await dbContext.Players
                .AsNoTracking()
                .Where(player => player.CountriesId == selectedCountryId && !player.IsNpc)
                .Select(player => (Guid?)player.PlayersId)
                .FirstOrDefaultAsync();

            country.PresidentPlayersId = firstCountryPlayerId ?? (requestPlayer.IsNpc ? null : playerId);
        }

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateEmailViolation(ex))
        {
            throw new InvalidOperationException("Email already exists", ex);
        }

        if (!persistAuthToken) return true;

        if (protectedLocalStorage is null)
        {
            throw new InvalidOperationException("Protected local storage is not available in this host.");
        }

        await protectedLocalStorage.SetAsync(TokenStorageKey, playerId);

        return true;
    }


    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private bool VerifyPassword(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }

    public async Task<string?> GetTokenAsync()
    {
        if (protectedLocalStorage is null)
        {
            return null;
        }

        try
        {
            var result = await protectedLocalStorage.GetAsync<string>(TokenStorageKey);
            return result.Success ? result.Value : null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to read protected auth token from browser storage.");
        }

        return null;
    }

    public async Task LogoutAsync()
    {
        if (protectedLocalStorage is null)
        {
            return;
        }

        await protectedLocalStorage.DeleteAsync(TokenStorageKey);
    }

    private (double Latitude, double Longitude) ResolveSignupCoordinates(string? isoCode2, string? admCode1, string? name)
    {
        var geoJsonPath = ResolveGeoJsonPath();
        if (!string.IsNullOrWhiteSpace(geoJsonPath))
        {
            try
            {
                return RandomCoordinateGenerator.GetRandomLandCoordinateForRegion(isoCode2, admCode1, name, geoJsonPath);
            }
            catch (IOException ex)
            {
                Log.Warning(ex, "GeoJSON map file could not be read while resolving signup coordinates. Falling back to random world coordinates.");
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Warning(ex, "GeoJSON map file was inaccessible while resolving signup coordinates. Falling back to random world coordinates.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to resolve signup coordinates for country {IsoCode2}/{CountryName}. Falling back to random world coordinates.", isoCode2, name);
            }
        }

        return RandomWorldCoordinate();
    }

    private string? ResolveGeoJsonPath()
    {
        var relativePath = Path.Combine("wwwroot", "assets", "maps", MapFileName);
        var directCandidates = new[]
        {
            Path.Combine(hostEnvironment.ContentRootPath, relativePath),
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), relativePath),
            Path.Combine(hostEnvironment.ContentRootPath, "VoltrokWebApp", relativePath),
            Path.Combine(AppContext.BaseDirectory, "VoltrokWebApp", relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), "VoltrokWebApp", relativePath)
        };

        foreach (var candidate in directCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        foreach (var start in new[] { hostEnvironment.ContentRootPath, AppContext.BaseDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = new DirectoryInfo(start);
            for (var depth = 0; depth < 8 && directory is not null; depth++, directory = directory.Parent)
            {
                var fromWebServerProject = Path.Combine(directory.FullName, "webServer", relativePath);
                if (File.Exists(fromWebServerProject))
                {
                    return fromWebServerProject;
                }

                var fromWebAppProject = Path.Combine(directory.FullName, "VoltrokWebApp", relativePath);
                if (File.Exists(fromWebAppProject))
                {
                    return fromWebAppProject;
                }
            }
        }

        return null;
    }

    private static (double Latitude, double Longitude) RandomWorldCoordinate()
    {
        var latitude = -60d + Random.Shared.NextDouble() * 145d;
        var longitude = -180d + Random.Shared.NextDouble() * 360d;
        return (latitude, longitude);
    }

    private static bool IsDuplicateEmailViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UsersEmailConstraintName
        };

    private static async Task RecordLoginAttemptAsync(
        AppDbContext dbContext,
        string email,
        User? user,
        bool isSuccessful,
        string? failureReason)
    {
        if (user is null)
        {
            return;
        }

        dbContext.UserLoginHistories.Add(new UserLoginHistory
        {
            UsersId = user.UsersId,
            IsSuccessful = isSuccessful,
            FailureReason = failureReason,
            AttemptedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

}
