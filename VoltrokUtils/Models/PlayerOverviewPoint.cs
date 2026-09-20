namespace VoltrokUtils.Models;

public sealed record PlayerOverviewPoint(
    DateOnly StatsDate,
    decimal Money,
    decimal Score,
    decimal Oil,
    decimal Uranium,
    decimal Chips,
    int ArmyTotal);
