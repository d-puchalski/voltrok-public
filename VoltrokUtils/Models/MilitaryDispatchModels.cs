namespace VoltrokUtils.Models;

public sealed record MilitaryDispatchSelection(string UnitCode, int Quantity);

public sealed record MilitaryDispatchPlan(
    IReadOnlyList<MilitaryDispatchSelection> Selections,
    int SpeedKmh,
    int TravelTimeSeconds,
    decimal DistanceKm,
    decimal OilCost);
