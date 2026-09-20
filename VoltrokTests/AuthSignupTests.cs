using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;
using Voltrok.EF;
using VoltrokServices.Services.Auth;

namespace VoltrokTests;

public class AuthSignupTests
{
    [Fact]
    public async Task SignupAsync_Creates_10000_Players()
    {
        var setup = CreateAuthService();
        var authService = setup.AuthService;
        var countryIds = await GetCountryIdsAsync(setup.DbContextFactory);
        var productIds = await GetProductIdsAsync(setup.DbContextFactory);

        for (var i = 2; i < 4; i++)
        {
            var credential = $"{i}@{i}.pl";
            var user = new User
            {
                Email = credential,
                Password = credential
            };

            var countryId = countryIds[Random.Shared.Next(countryIds.Count)];
            var player = new Player
            {
                Name = $"Player {i}",
                CountriesId = countryId,
                PlayerAvatar = $"/assets/icons/kings/{new Random().Next(1, 72)}.png"
            };

            var selectedProducts = productIds
                .OrderBy(_ => Guid.NewGuid())
                .Take(5)
                .ToList();

            var result = await authService.SignupAsync(user, player, selectedProducts);
            Assert.True(result);
        }
    }

    [Fact]
    public async Task SignupAsync_WhenEmailExists_ThrowsInvalidOperationException()
    {
        var setup = CreateAuthService();
        var authService = setup.AuthService;
        var countryIds = await GetCountryIdsAsync(setup.DbContextFactory);
        var productIds = await GetProductIdsAsync(setup.DbContextFactory);
        var email = $"duplicate-{Guid.NewGuid():N}@voltrok.local";

        var firstResult = await authService.SignupAsync(
            new User
            {
                Email = email,
                Password = "secret"
            },
            new Player
            {
                Name = "First Player",
                CountriesId = countryIds[0]
            },
            productIds);

        Assert.True(firstResult);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => authService.SignupAsync(
            new User
            {
                Email = email,
                Password = "secret"
            },
            new Player
            {
                Name = "Second Player",
                CountriesId = countryIds[0]
            },
            productIds));

        Assert.Equal("Email already exists", exception.Message);
    }

    private static async Task<List<Guid>> GetCountryIdsAsync(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var countryIds = await dbContext.Countries
            .Select(c => c.CountriesId)
            .ToListAsync();
        if (countryIds.Count == 0)
        {
            throw new InvalidOperationException("No countries available in the database.");
        }

        return countryIds;
    }

    private static async Task<List<Guid>> GetProductIdsAsync(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        if (!await dbContext.Countries.AnyAsync())
        {
            throw new InvalidOperationException("No countries available in the database.");
        }

        return
        [
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555")
        ];
    }

    private static (AuthService AuthService, IDbContextFactory<AppDbContext> DbContextFactory) CreateAuthService()
    {
        var contentRoot = GetContentRoot();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=map;Username=postgres;Password=postgres;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var dbContextFactory = new PooledDbContextFactory<AppDbContext>(options);
        var dataProtectionProvider = Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create("webServer.Tests");
        var jsRuntime = new NoopJsRuntime();
        var protectedLocalStorage = new ProtectedLocalStorage(jsRuntime, dataProtectionProvider);
        var hostEnvironment = new TestHostEnvironment(contentRoot);

        return (new AuthService(dbContextFactory, protectedLocalStorage, hostEnvironment), dbContextFactory);
    }

    private static string GetContentRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "webServer"));

    private sealed class NoopJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => new(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => new(default(TValue)!);
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "webServer.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
