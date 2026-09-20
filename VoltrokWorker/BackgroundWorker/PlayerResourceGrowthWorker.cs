using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VoltrokEF;
using VoltrokUtils.Engines;
using VoltrokUtils.Enums;

namespace VoltrokWorker.BackgroundWorker;

public sealed class PlayerResourceGrowthWorker(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<PlayerResourceGrowthWorker> logger) : BackgroundService
{
    private static int ProcessCooldownMinutes => 1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PlayerResourceGrowthWorker started. Interval: {IntervalMinutes}m.", ProcessCooldownMinutes);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(ProcessCooldownMinutes));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessPlayerResourceGrowthAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PlayerResourceGrowthWorker tick failed.");
            }
        }
    }

    private async Task ProcessPlayerResourceGrowthAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var countries = await db.Countries
            .AsNoTracking()
            .Select(country => new
            {
                country.CountriesId,
                country.IsoCode2,
                country.OilTier,
                country.UraniumTier,
                country.ChipsTier
            })
            .ToListAsync(cancellationToken);

        var countryBuildingBonuses = await db.CountryBuildings
            .AsNoTracking()
                .Where(building => building.Status == CountryBuildingStatus.Active.ToDbValue())
            .GroupBy(building => new { building.CountriesId, building.BuildingCode })
            .Select(group => new
            {
                group.Key.CountriesId,
                group.Key.BuildingCode,
                TotalLevel = group.Sum(building => building.Level),
                BuildingCount = group.Count()
            })
            .ToListAsync(cancellationToken);

        var players = await db.Players
            .Where(player => player.CountriesId != Guid.Empty)
            .Select(player => new
            {
                Entity = player,
                player.Name
            })
            .ToListAsync(cancellationToken);

        var bonusLookup = countryBuildingBonuses
            .GroupBy(entry => entry.CountriesId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var refineryBonus = group
                        .Where(entry => IsBuildingType(entry.BuildingCode, CountryBuildingTypes.Refinery))
                        .Sum(entry => CalculateBuildingEfficiency(entry.BuildingCode, entry.TotalLevel, entry.BuildingCount));
                    var powerStationBonus = group
                        .Where(entry => IsBuildingType(entry.BuildingCode, CountryBuildingTypes.PowerStation))
                        .Sum(entry => CalculateBuildingEfficiency(entry.BuildingCode, entry.TotalLevel, entry.BuildingCount));
                    var factoryBonus = group
                        .Where(entry => IsBuildingType(entry.BuildingCode, CountryBuildingTypes.Factory))
                        .Sum(entry => CalculateBuildingEfficiency(entry.BuildingCode, entry.TotalLevel, entry.BuildingCount));
                    var bankBonus = group
                        .Where(entry => IsBuildingType(entry.BuildingCode, CountryBuildingTypes.Bank))
                        .Sum(entry => CalculateBuildingEfficiency(entry.BuildingCode, entry.TotalLevel, entry.BuildingCount));

                    return new CountryResourceBonusBreakdown(refineryBonus, powerStationBonus, factoryBonus, bankBonus);
                });

        var processedPlayers = 0;
        decimal totalOilAdded = 0m;
        decimal totalUraniumAdded = 0m;
        decimal totalChipsAdded = 0m;
        decimal totalMoneyAdded = 0m;

        foreach (var player in players)
        {
            var playerEntity = player.Entity;
            var country = countries.FirstOrDefault(entry => entry.CountriesId == playerEntity.CountriesId);
            if (country == null)
            {
                continue;
            }

            var bonuses = bonusLookup.GetValueOrDefault(playerEntity.CountriesId);
            var oilGain = country.OilTier + (bonuses?.OilBonus ?? 0m);
            var uraniumGain = country.UraniumTier + (bonuses?.UraniumBonus ?? 0m);
            var chipsGain = country.ChipsTier + (bonuses?.ChipsBonus ?? 0m);
            var moneyGain = bonuses?.MoneyBonus ?? 0m;
            var oilBefore = playerEntity.Oil;
            var uraniumBefore = playerEntity.Uranium;
            var chipsBefore = playerEntity.Chips;
            var moneyBefore = playerEntity.Money;

            if (oilGain > 0)
            {
                playerEntity.Oil += oilGain;
            }

            if (uraniumGain > 0)
            {
                playerEntity.Uranium += uraniumGain;
            }

            if (chipsGain > 0)
            {
                playerEntity.Chips += chipsGain;
            }

            if (moneyGain > 0)
            {
                playerEntity.Money += moneyGain;
            }

            processedPlayers++;
            totalOilAdded += Math.Max(0m, oilGain);
            totalUraniumAdded += Math.Max(0m, uraniumGain);
            totalChipsAdded += Math.Max(0m, chipsGain);
            totalMoneyAdded += Math.Max(0m, moneyGain);

            logger.LogInformation(
                "PlayerResourceGrowthWorker player={PlayerName} country={CountryIso2} oil {OilBefore}->{OilAfter} (+{OilGain}) [tier={OilTier}, buildings={OilBuildingBonus}] uranium {UraniumBefore}->{UraniumAfter} (+{UraniumGain}) [tier={UraniumTier}, buildings={UraniumBuildingBonus}] chips {ChipsBefore}->{ChipsAfter} (+{ChipsGain}) [tier={ChipsTier}, buildings={ChipsBuildingBonus}] money {MoneyBefore}->{MoneyAfter} (+{MoneyGain}) [buildings={MoneyBuildingBonus}]",
                player.Name,
                string.IsNullOrWhiteSpace(country.IsoCode2) ? "??" : country.IsoCode2.ToUpperInvariant(),
                oilBefore,
                playerEntity.Oil,
                oilGain,
                country.OilTier,
                bonuses?.OilBonus ?? 0m,
                uraniumBefore,
                playerEntity.Uranium,
                uraniumGain,
                country.UraniumTier,
                bonuses?.UraniumBonus ?? 0m,
                chipsBefore,
                playerEntity.Chips,
                chipsGain,
                country.ChipsTier,
                bonuses?.ChipsBonus ?? 0m,
                moneyBefore,
                playerEntity.Money,
                moneyGain,
                bonuses?.MoneyBonus ?? 0m);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PlayerResourceGrowthWorker tick completed. PlayersProcessed={PlayersProcessed}, OilAdded={OilAdded}, UraniumAdded={UraniumAdded}, ChipsAdded={ChipsAdded}, MoneyAdded={MoneyAdded}",
            processedPlayers,
            totalOilAdded,
            totalUraniumAdded,
            totalChipsAdded,
            totalMoneyAdded);
    }

    private static decimal CalculateBuildingEfficiency(string? buildingCode, int totalLevel, int buildingCount)
    {
        if (totalLevel <= 0 || buildingCount <= 0)
        {
            return 0m;
        }

        var definition = BuildingUnitEngine.BuildingUnitsAllData.FirstOrDefault(entry => IsBuildingType(buildingCode, entry.Code));
        if (definition is null)
        {
            return 0m;
        }

        var extraLevels = Math.Max(0, totalLevel - buildingCount);
        return (definition.StartingProductionEfficiency * buildingCount) + (definition.LevelUpProductionEfficiencyMultiplayer * extraLevels);
    }

    private static bool IsBuildingType(string? rawBuildingCode, CountryBuildingTypes buildingType)
    {
        var normalized = BuildingUnitEngine.NormalizeCode(rawBuildingCode);
        return string.Equals(
            normalized,
            BuildingUnitEngine.ToStorageCode(buildingType),
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CountryResourceBonusBreakdown(
        decimal OilBonus,
        decimal UraniumBonus,
        decimal ChipsBonus,
        decimal MoneyBonus);
}
