namespace VoltrokServices.Services.Factions;

public sealed class FactionGovernanceSnapshot
{
    public Guid CountryId { get; init; }
    public string CountryName { get; init; } = string.Empty;
    public string FactionCode { get; init; } = string.Empty;
    public string FactionName { get; init; } = string.Empty;
    public Guid? PresidentPlayerId { get; init; }
    public string? PresidentPlayerName { get; init; }
    public bool IsViewerInFaction { get; init; }
    public bool IsViewerPresident { get; init; }
    public DateTime NextAutomaticPresidentSelectionAtUtc { get; init; }
    public decimal FactionTaxPercent { get; init; }
    public decimal CountryTaxPercent { get; init; }
    public decimal TreasuryMoney { get; init; }
    public decimal? LocationX { get; init; }
    public decimal? LocationY { get; init; }
    public int MemberCount { get; init; }
    public int InventoryStackCount { get; init; }
    public int InventoryQuantityTotal { get; init; }
    public int MilitaryStackCount { get; init; }
    public int MilitaryQuantityTotal { get; init; }
    public int MilitaryAttackPower { get; init; }
    public int MilitaryDefensePower { get; init; }
    public List<FactionMemberState> Members { get; init; } = [];
    public List<FactionInventoryState> Inventory { get; init; } = [];
    public List<FactionMilitaryState> Military { get; init; } = [];
}

public sealed class FactionMemberState
{
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public bool IsPresident { get; init; }
    public bool IsNpc { get; init; }
    public decimal Score { get; init; }
    public decimal Money { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class FactionInventoryState
{
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

public sealed class FactionMilitaryState
{
    public Guid MilitaryUnitId { get; init; }
    public string UnitCode { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public int AttackPowerPerUnit { get; init; }
    public int DefensePowerPerUnit { get; init; }
    public int TotalAttackPower { get; init; }
    public int TotalDefensePower { get; init; }
}
