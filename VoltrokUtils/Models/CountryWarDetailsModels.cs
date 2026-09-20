namespace VoltrokUtils.Models;

public sealed class CountryWarDetailsModel
{
    public Guid WarId { get; init; }
    public Guid CountryId { get; init; }
    public Guid OpponentCountryId { get; init; }
    public bool IsAttacker { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime StartedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }
    public decimal AttackerProgressPercent { get; init; }
    public decimal CaptureThresholdPercent { get; init; }
    public decimal DefenderProgressPercent { get; init; }
    public decimal DefenderCancelThresholdPercent { get; init; }
    public int AttackerActiveSieges { get; init; }
    public int DefenderActiveSieges { get; init; }
    public int DefenderTotalPlayers { get; init; }
    public int AttackerTotalPlayers { get; init; }
    public DateTime RangeStartUtc { get; init; }
    public DateTime RangeEndUtc { get; init; }
    public int BattlesInRange { get; init; }
    public int AttackerWinsInRange { get; init; }
    public int DefenderWinsInRange { get; init; }
    public int AttackerLossesInRange { get; init; }
    public int DefenderLossesInRange { get; init; }
    public int AttackMissionsInRange { get; init; }
    public int SiegeMissionsInRange { get; init; }
    public int ActiveSieges { get; init; }
    public int UnitsSentInRange { get; init; }
    public int AttackPowerSentInRange { get; init; }
    public int DefensePowerSentInRange { get; init; }
    public List<CountryWarUnitActivityModel> TopUnitsInRange { get; init; } = [];
}

public sealed class CountryWarUnitActivityModel
{
    public string UnitCode { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public int AttackPower { get; init; }
    public int DefensePower { get; init; }
}
