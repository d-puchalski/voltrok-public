using VoltrokUtils.Enums;

namespace VoltrokUtils.Engines;

public static class BuildingUnitEngine
{
    public static readonly List<BuildingUnit> BuildingUnitsAllData =
    [
        new(Code: CountryBuildingTypes.Refinery,
            StartingProductionEfficiency: 1.00m,
            LevelUpProductionEfficiencyMultiplayer: 0.05m,  
            UpgradeCost: 800,                               
            UpgradeCostMultiplayer: 0.15m),                 

        new(Code: CountryBuildingTypes.PowerStation,
            StartingProductionEfficiency: 0.90m,
            LevelUpProductionEfficiencyMultiplayer: 0.05m,  
            UpgradeCost: 900,                               
            UpgradeCostMultiplayer: 0.15m),                 

        new(Code: CountryBuildingTypes.Factory,
            StartingProductionEfficiency: 0.80m,
            LevelUpProductionEfficiencyMultiplayer: 0.05m,  
            UpgradeCost: 1000,                              
            UpgradeCostMultiplayer: 0.15m),                 
        
        new(Code: CountryBuildingTypes.Bank,
            StartingProductionEfficiency: 0.80m,
            LevelUpProductionEfficiencyMultiplayer: 0.05m,   
            UpgradeCost: 1000,
            UpgradeCostMultiplayer: 0.5m),
    ];

    public static BuildingUnit? FindByCode(string? buildingCode)
    {
        var normalized = NormalizeCode(buildingCode);
        return normalized is null
            ? null
            : BuildingUnitsAllData.FirstOrDefault(unit => string.Equals(ToStorageCode(unit.Code), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string ToStorageCode(CountryBuildingTypes code)
        => code switch
        {
            CountryBuildingTypes.Refinery => "refinery",
            CountryBuildingTypes.PowerStation => "power_station",
            CountryBuildingTypes.Factory => "factory",
            CountryBuildingTypes.Bank => "bank",
            _ => code.ToString().ToLowerInvariant()
        };

    public static string? NormalizeCode(string? buildingCode)
    {
        if (string.IsNullOrWhiteSpace(buildingCode))
        {
            return null;
        }

        var normalized = buildingCode.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "refinery" => "refinery",
            "powerstation" => "power_station",
            "power_station" => "power_station",
            "factory" => "factory",
            "bank" => "bank",
            _ => null
        };
    }

    public static decimal GetProductionEfficiency(string? buildingCode, int level)
        => FindByCode(buildingCode)?.GetProductionEfficiency(level) ?? 0m;

    public static decimal GetUpgradeCost(string? buildingCode, int targetLevel)
        => FindByCode(buildingCode)?.GetUpgradeCost(targetLevel) ?? 0m;

}


public record BuildingUnit(
    CountryBuildingTypes Code,
    decimal StartingProductionEfficiency,
    decimal LevelUpProductionEfficiencyMultiplayer,
    decimal UpgradeCost,
    decimal UpgradeCostMultiplayer
)
{
    public decimal GetProductionEfficiency(int level)
    {
        if (level <= 0)
        {
            return 0m;
        }

        var extraLevels = Math.Max(0, level - 1);
        return StartingProductionEfficiency + (extraLevels * LevelUpProductionEfficiencyMultiplayer);
    }

    public decimal GetUpgradeCost(int targetLevel)
    {
        var safeTargetLevel = Math.Max(1, targetLevel);
        var growth = 1m + ((safeTargetLevel - 1) * UpgradeCostMultiplayer);
        return Math.Ceiling(UpgradeCost * growth);
    }
}
