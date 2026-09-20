using VoltrokUtils.Enums;

namespace VoltrokUtils.Engines;

public static class MilitaryUnitEngine
{
    public static readonly List<MilitaryUnit> MilitaryUnitsAllData =
    [
        // LINE UNITS
        new(Code: MilitaryUnitsEnum.Infantry,
            StartingOffensivePoints: 10,
            StartingDefensivePoints: 10,
            StartingSiegePoints: 10,
            StartingDurability: 10,
            StartingSpeed: 50,
            StartingCost: 100,
            StartingProductionTime: new TimeSpan(0, 1, 0),
            LevelUpOffensivePointsMultiplayer: 0.07m,
            LevelUpDefensivePointsMultiplayer: 0.07m,
            LevelUpSiegePointsMultiplayer: 0.07m,
            LevelUpDurabilityMultiplayer: 0.06m,
            LevelUpSpeedMultiplayer: 0.03m,
            LevelUpCostMultiplayer: 0.06m,
            LevelUpProductionTimeMultiplayer: 0.02m,
            IsDefensive: false,
            DefensiveAgainst: null),

        new(Code: MilitaryUnitsEnum.Garrison,
            StartingOffensivePoints: 8,
            StartingDefensivePoints: 50,
            StartingSiegePoints: 0,
            StartingDurability: 50,
            StartingSpeed: 35,
            StartingCost: 450,
            StartingProductionTime: new TimeSpan(0, 2, 0),
            LevelUpOffensivePointsMultiplayer: 0.02m,
            LevelUpDefensivePointsMultiplayer: 0.09m,
            LevelUpSiegePointsMultiplayer: 0.01m,
            LevelUpDurabilityMultiplayer: 0.08m,
            LevelUpSpeedMultiplayer: 0.01m,
            LevelUpCostMultiplayer: 0.05m,
            LevelUpProductionTimeMultiplayer: 0.02m,
            IsDefensive: true,
            DefensiveAgainst: MilitaryUnitsEnum.Infantry),

        new(Code: MilitaryUnitsEnum.Tanks,
            StartingOffensivePoints: 100,
            StartingDefensivePoints: 110,
            StartingSiegePoints: 220,
            StartingDurability: 250,
            StartingSpeed: 80,
            StartingCost: 1300,
            StartingProductionTime: new TimeSpan(0, 5, 0),
            LevelUpOffensivePointsMultiplayer: 0.07m,
            LevelUpDefensivePointsMultiplayer: 0.07m,
            LevelUpSiegePointsMultiplayer: 0.08m,
            LevelUpDurabilityMultiplayer: 0.07m,
            LevelUpSpeedMultiplayer: 0.02m,
            LevelUpCostMultiplayer: 0.07m,
            LevelUpProductionTimeMultiplayer: 0.02m,
            IsDefensive: false,
            DefensiveAgainst: null),

        // HARD COUNTERS
        new(Code: MilitaryUnitsEnum.AntiTank,
            StartingOffensivePoints: 25,
            StartingDefensivePoints: 220,
            StartingSiegePoints: 0,
            StartingDurability: 210,
            StartingSpeed: 15,
            StartingCost: 1100,
            StartingProductionTime: new TimeSpan(0, 5, 0),
            LevelUpOffensivePointsMultiplayer: 0.03m,
            LevelUpDefensivePointsMultiplayer: 0.09m,
            LevelUpSiegePointsMultiplayer: 0.00m,
            LevelUpDurabilityMultiplayer: 0.08m,
            LevelUpSpeedMultiplayer: 0.01m,
            LevelUpCostMultiplayer: 0.06m,
            LevelUpProductionTimeMultiplayer: 0.02m,
            IsDefensive: true,
            DefensiveAgainst: MilitaryUnitsEnum.Tanks),

        new(Code: MilitaryUnitsEnum.Fighters,
            StartingOffensivePoints: 500,
            StartingDefensivePoints: 0,
            StartingSiegePoints: 0,
            StartingDurability: 300,
            StartingSpeed: 1100,
            StartingCost: 5000,
            StartingProductionTime: new TimeSpan(0, 15, 0),
            LevelUpOffensivePointsMultiplayer: 0.07m,
            LevelUpDefensivePointsMultiplayer: 0.00m,
            LevelUpSiegePointsMultiplayer: 0.00m,
            LevelUpDurabilityMultiplayer: 0.06m,
            LevelUpSpeedMultiplayer: 0.03m,
            LevelUpCostMultiplayer: 0.07m,
            LevelUpProductionTimeMultiplayer: 0.03m,
            IsDefensive: false,
            DefensiveAgainst: null),

        new(Code: MilitaryUnitsEnum.AirDefense,
            StartingOffensivePoints: 0,
            StartingDefensivePoints: 1600,
            StartingSiegePoints: 0,
            StartingDurability: 350,
            StartingSpeed: 10,
            StartingCost: 6800,
            StartingProductionTime: new TimeSpan(0, 15, 0),
            LevelUpOffensivePointsMultiplayer: 0.00m,
            LevelUpDefensivePointsMultiplayer: 0.09m,
            LevelUpSiegePointsMultiplayer: 0.00m,
            LevelUpDurabilityMultiplayer: 0.07m,
            LevelUpSpeedMultiplayer: 0.00m,
            LevelUpCostMultiplayer: 0.06m,
            LevelUpProductionTimeMultiplayer: 0.02m,
            IsDefensive: true,
            DefensiveAgainst: MilitaryUnitsEnum.Fighters),

        // PREMIUM AIR / DRONE LAYER
        new(Code: MilitaryUnitsEnum.AttackDrones,
            StartingOffensivePoints: 650,
            StartingDefensivePoints: 0,
            StartingSiegePoints: 0,
            StartingDurability: 280,
            StartingSpeed: 1800,
            StartingCost: 7500,
            StartingProductionTime: new TimeSpan(0, 24, 0),
            LevelUpOffensivePointsMultiplayer: 0.08m,
            LevelUpDefensivePointsMultiplayer: 0.00m,
            LevelUpSiegePointsMultiplayer: 0.00m,
            LevelUpDurabilityMultiplayer: 0.07m,
            LevelUpSpeedMultiplayer: 0.02m,
            LevelUpCostMultiplayer: 0.08m,
            LevelUpProductionTimeMultiplayer: 0.03m,
            IsDefensive: false,
            DefensiveAgainst: null),

        new(Code: MilitaryUnitsEnum.AntiDroneWarfare,
            StartingOffensivePoints: 0,
            StartingDefensivePoints: 1700,
            StartingSiegePoints: 0,
            StartingDurability: 340,
            StartingSpeed: 5,
            StartingCost: 8500,
            StartingProductionTime: new TimeSpan(0, 28, 0),
            LevelUpOffensivePointsMultiplayer: 0.00m,
            LevelUpDefensivePointsMultiplayer: 0.09m,
            LevelUpSiegePointsMultiplayer: 0.00m,
            LevelUpDurabilityMultiplayer: 0.07m,
            LevelUpSpeedMultiplayer: 0.00m,
            LevelUpCostMultiplayer: 0.06m,
            LevelUpProductionTimeMultiplayer: 0.02m,
            IsDefensive: true,
            DefensiveAgainst: MilitaryUnitsEnum.AttackDrones),

        // CYBER LAYER
        // OFF/COST: 350/5000 = 0.070 — niższe niż Fighters (0.10),
        // ale speed 8000 vs 1100 uzasadnia ~30% dopłatę w efektywności
        new(Code: MilitaryUnitsEnum.CyberForces,
            StartingOffensivePoints: 350,                          // ZMIANA: 100 → 350
            StartingDefensivePoints: 0,
            StartingSiegePoints: 0,
            StartingDurability: 180,
            StartingSpeed: 8000,
            StartingCost: 5000,
            StartingProductionTime: new TimeSpan(0, 7, 0),         // ZMIANA: 4 → 7 min
            LevelUpOffensivePointsMultiplayer: 0.09m,              // ZMIANA: 0.11 → 0.09
            LevelUpDefensivePointsMultiplayer: 0.00m,
            LevelUpSiegePointsMultiplayer: 0.00m,
            LevelUpDurabilityMultiplayer: 0.08m,
            LevelUpSpeedMultiplayer: 0.02m,
            LevelUpCostMultiplayer: 0.08m,
            LevelUpProductionTimeMultiplayer: 0.03m,
            IsDefensive: false,
            DefensiveAgainst: null),

        // DEF/OFF ratio: 500/350 = 1.43:1 bazowo, rośnie z poziomami (0.10 vs 0.09)
        new(Code: MilitaryUnitsEnum.CyberDefense,
            StartingOffensivePoints: 0,
            StartingDefensivePoints: 500,                          // ZMIANA: 130 → 500
            StartingSiegePoints: 0,
            StartingDurability: 220,
            StartingSpeed: 8000,
            StartingCost: 4000,
            StartingProductionTime: new TimeSpan(0, 3, 0),
            LevelUpOffensivePointsMultiplayer: 0.00m,
            LevelUpDefensivePointsMultiplayer: 0.10m,              // ZMIANA: 0.09 → 0.10
            LevelUpSiegePointsMultiplayer: 0.00m,
            LevelUpDurabilityMultiplayer: 0.08m,
            LevelUpSpeedMultiplayer: 0.01m,
            LevelUpCostMultiplayer: 0.06m,
            LevelUpProductionTimeMultiplayer: 0.03m,
            IsDefensive: true,
            DefensiveAgainst: MilitaryUnitsEnum.CyberForces),
];
    public static MilitaryUnit? FindByCode(string? unitCode)
    {
        if (string.IsNullOrWhiteSpace(unitCode))
        {
            return null;
        }

        return MilitaryUnitsAllData.FirstOrDefault(unit => string.Equals(unit.Code.ToString(), unitCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    internal static decimal ScaleValue(decimal baseValue, decimal perLevelMultiplier, int level)
    {
        var safeLevel = Math.Max(1, level);
        return Math.Round(baseValue * (1m + ((safeLevel - 1) * perLevelMultiplier)), 2, MidpointRounding.AwayFromZero);
    }

    internal static int ScaleDuration(TimeSpan baseDuration, decimal perLevelMultiplier, int level)
    {
        var scaled = ScaleValue((decimal)baseDuration.TotalSeconds, perLevelMultiplier, level);
        return Math.Max(1, (int)Math.Round(scaled, MidpointRounding.AwayFromZero));
    }

}


public record MilitaryUnit(
    MilitaryUnitsEnum Code,
    decimal StartingOffensivePoints,
    decimal StartingDefensivePoints,
    decimal StartingSiegePoints,
    decimal StartingDurability,
    decimal StartingSpeed,
    decimal StartingCost,
    TimeSpan StartingProductionTime,
    decimal LevelUpOffensivePointsMultiplayer,
    decimal LevelUpDefensivePointsMultiplayer,
    decimal LevelUpSiegePointsMultiplayer,
    decimal LevelUpDurabilityMultiplayer,
    decimal LevelUpSpeedMultiplayer,
    decimal LevelUpCostMultiplayer,
    decimal LevelUpProductionTimeMultiplayer,
    bool IsDefensive,
    MilitaryUnitsEnum? DefensiveAgainst)
{
    public decimal OffensivePoints => GetOffensivePoints(1);
    public decimal DefensivePoints => GetDefensivePoints(1);
    public decimal SiegePoints => GetSiegePoints(1);
    public decimal Durability => GetDurability(1);
    public decimal Speed => GetSpeed(1);
    public decimal Cost => GetCost(1);
    public int ProductionTime => GetProductionTime(1);

    public decimal GetOffensivePoints(int level)
        => MilitaryUnitEngine.ScaleValue(StartingOffensivePoints, LevelUpOffensivePointsMultiplayer, level);

    public decimal GetDefensivePoints(int level)
        => MilitaryUnitEngine.ScaleValue(StartingDefensivePoints, LevelUpDefensivePointsMultiplayer, level);

    public decimal GetSiegePoints(int level)
        => MilitaryUnitEngine.ScaleValue(StartingSiegePoints, LevelUpSiegePointsMultiplayer, level);

    public decimal GetDurability(int level)
        => MilitaryUnitEngine.ScaleValue(StartingDurability, LevelUpDurabilityMultiplayer, level);

    public decimal GetSpeed(int level)
        => MilitaryUnitEngine.ScaleValue(StartingSpeed, LevelUpSpeedMultiplayer, level);

    public decimal GetCost(int level)
        => MilitaryUnitEngine.ScaleValue(StartingCost, LevelUpCostMultiplayer, level);

    public int GetProductionTime(int level)
        => MilitaryUnitEngine.ScaleDuration(StartingProductionTime, LevelUpProductionTimeMultiplayer, level);

    public decimal GetUpgradeCost(int targetLevel)
        => GetCost(targetLevel);
}
