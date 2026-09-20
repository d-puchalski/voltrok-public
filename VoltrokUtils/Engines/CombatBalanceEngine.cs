using VoltrokUtils.Enums;

namespace VoltrokUtils.Engines;

public static class CombatBalanceEngine
{
    public static readonly TimeSpan MaxSiegeDuration = TimeSpan.FromHours(48);
    public static readonly TimeSpan SiegeTickInterval = TimeSpan.FromHours(1);

    public const decimal StandardAttackLootRate = 0.10m;
    public const decimal SiegeInitialLootRate = 0.05m;
    public const decimal SiegeHourlyLootRate = 0.005m;

    public const decimal WinnerLossBaseRate = 0.05m;
    public const decimal WinnerLossSwingRate = 0.25m;
    public const decimal WinnerLossMaxRate = 0.30m;

    public const decimal LoserLossBaseRate = 0.35m;
    public const decimal LoserLossSwingRate = 0.45m;
    public const decimal LoserLossMaxRate = 0.95m;

    public const decimal SiegeAttackerBaseLossRate = 0.008m;
    public const decimal SiegeAttackerSwingRate = 0.05m;
    public const decimal SiegeDefenderBaseLossRate = 0.015m;
    public const decimal SiegeDefenderSwingRate = 0.08m;

    public const decimal CountryTaxDivisor = 100m;

    public const decimal TransportBaseFuelRate = 0.015m;
    public const decimal TransportCombatLoadDivisor = 900m;
    public const decimal TransportMobilityLoadDivisor = 1200m;
    public const decimal TransportMobilityLoadMinimum = 0.01m;

    public static decimal CalculateTaxAmount(decimal grossAmount, decimal taxPercent)
        => grossAmount <= 0m || taxPercent <= 0m
            ? 0m
            : Math.Max(0m, Math.Round(grossAmount * (taxPercent / CountryTaxDivisor), 2, MidpointRounding.AwayFromZero));

    public static int CalculateLosses(int totalUnits, decimal ownPower, decimal enemyPower, bool winner)
    {
        if (totalUnits <= 0)
        {
            return 0;
        }

        if (ownPower <= 0)
        {
            return totalUnits;
        }

        var relativeThreat = enemyPower <= 0m
            ? 0m
            : enemyPower / Math.Max(1m, ownPower);

        var lossRate = winner
            ? Math.Clamp(WinnerLossBaseRate + (WinnerLossSwingRate * relativeThreat), WinnerLossBaseRate, WinnerLossMaxRate)
            : Math.Clamp(LoserLossBaseRate + (LoserLossSwingRate * relativeThreat), LoserLossBaseRate, LoserLossMaxRate);

        return (int)Math.Clamp(Math.Round(totalUnits * lossRate, MidpointRounding.AwayFromZero), 0, totalUnits);
    }

    public static int CalculateSiegeLosses(int totalUnits, decimal enemyPower, decimal ownPower, decimal baseRate, decimal swingRate)
    {
        if (totalUnits <= 0 || enemyPower <= 0m)
        {
            return 0;
        }

        var pressure = enemyPower / Math.Max(1m, ownPower);
        var lossRate = Math.Clamp(baseRate + (pressure * swingRate), baseRate, baseRate + swingRate);
        var losses = (int)Math.Round(totalUnits * lossRate, MidpointRounding.AwayFromZero);
        return Math.Clamp(losses <= 0 ? 1 : losses, 0, totalUnits);
    }

    public static decimal CalculateTransportPerUnitFuel(decimal offensivePoints, decimal defensivePoints, decimal siegePoints, decimal speed)
    {
        var combatLoad = (offensivePoints + defensivePoints + siegePoints) / TransportCombatLoadDivisor;
        var mobilityLoad = Math.Max(TransportMobilityLoadMinimum, speed / TransportMobilityLoadDivisor);
        return TransportBaseFuelRate + combatLoad + mobilityLoad;
    }

    public static string NormalizeCombatFamily(string? unitCode)
    {
        if (string.IsNullOrWhiteSpace(unitCode))
        {
            return string.Empty;
        }

        return unitCode.Trim() switch
        {
            nameof(MilitaryUnitsEnum.Garrison) => nameof(MilitaryUnitsEnum.Infantry),
            nameof(MilitaryUnitsEnum.AntiTank) => nameof(MilitaryUnitsEnum.Tanks),
            nameof(MilitaryUnitsEnum.AirDefense) => nameof(MilitaryUnitsEnum.Fighters),
            nameof(MilitaryUnitsEnum.AntiDroneWarfare) => nameof(MilitaryUnitsEnum.AttackDrones),
            nameof(MilitaryUnitsEnum.CyberDefense) => nameof(MilitaryUnitsEnum.CyberForces),
            _ => unitCode.Trim()
        };
    }
}
