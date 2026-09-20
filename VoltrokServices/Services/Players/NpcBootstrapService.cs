using Microsoft.EntityFrameworkCore;
using Serilog;

using VoltrokEF;
using VoltrokServices.Services.Auth;

namespace VoltrokServices.Services.Players;

public class NpcBootstrapService(AuthService authService, IDbContextFactory<AppDbContext> dbContextFactory)
{
    private static int NpcPerCountry => 3;
    private static int MaxNpcCount => 10000;

    public async Task EnsureNpcPlayersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var countries = await db.Countries
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        if (countries.Count == 0)
        {
            return;
        }

        var existingNpcCount = await db.Players
            .AsNoTracking()
            .CountAsync(p => p.IsNpc, cancellationToken);
        if (existingNpcCount >= MaxNpcCount)
        {
            return;
        }

        var existingNpcByCountry = await db.Players.AsNoTracking()
            .Where(p => p.IsNpc)
            .GroupBy(p => p.CountriesId)
            .Select(g => new { CountryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CountryId, x => x.Count, cancellationToken);

        var targetCount = Math.Min(MaxNpcCount, countries.Count * NpcPerCountry);
        var remainingSlots = Math.Max(0, targetCount - existingNpcCount);
        if (remainingSlots == 0)
        {
            return;
        }

        var creationPlan = new List<Guid>(remainingSlots);
        foreach (var country in countries)
        {
            if (creationPlan.Count >= remainingSlots)
            {
                break;
            }

            existingNpcByCountry.TryGetValue(country.CountriesId, out var currentCountryCount);
            var missingForCountry = Math.Max(0, NpcPerCountry - currentCountryCount);
            for (var i = 0; i < missingForCountry && creationPlan.Count < remainingSlots; i++)
            {
                creationPlan.Add(country.CountriesId);
            }
        }

        if (creationPlan.Count == 0)
        {
            return;
        }

        var created = 0;
        var duplicateSkips = 0;
        await Parallel.ForEachAsync(
            creationPlan,
            new ParallelOptions
            {
                CancellationToken = cancellationToken
            },
            async (countryId, token) =>
            {
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    token.ThrowIfCancellationRequested();

                    var suffix = Guid.NewGuid().ToString("N")[..12];
                    var email = $"npc_{suffix}@voltrok.local";
                    var playerName = $"npc_{suffix}";

                    try
                    {
                        await authService.SignupAsync(
                            new User
                            {
                                Email = email,
                                Password = suffix
                            },
                            new Player
                            {
                                CountriesId = countryId,
                                Name = playerName,
                                IsNpc = true,
                                PlayerAvatar = $"/assets/icons/kings/{Random.Shared.Next(72)}.png",
                            },
                            persistAuthToken: false);

                        Interlocked.Increment(ref created);
                        return;
                    }
                    catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Email already exists", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref duplicateSkips);
                    }
                }
            });

        if (created > 0)
        {
            Log.Information(
                "Ensured NPC players. Created {CreatedCount} new NPCs using parallel bootstrap ({Parallelism}).",
                created,
                duplicateSkips);
        }
    }
}
