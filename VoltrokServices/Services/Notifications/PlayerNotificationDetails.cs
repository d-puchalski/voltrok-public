namespace VoltrokServices.Services.Notifications;

public sealed class PlayerNotificationDetails
{
    public string? NotificationKind { get; set; }
    public string? Channel { get; set; }

    public Guid? TradeId { get; set; }
    public Guid? SenderPlayerId { get; set; }
    public string? SenderPlayerName { get; set; }
    public Guid? RecipientPlayerId { get; set; }
    public string? RecipientPlayerName { get; set; }
    public string? CounterpartyName { get; set; }

    public Guid? AttackerPlayerId { get; set; }
    public Guid? DefenderPlayerId { get; set; }
    public string? AttackerPlayerName { get; set; }
    public string? DefenderPlayerName { get; set; }
    public string? WinnerPlayerName { get; set; }
    public string? LoserPlayerName { get; set; }

    public string? MissionType { get; set; }
    public string? Result { get; set; }
    public string? ResourceCode { get; set; }
    public string? Preview { get; set; }

    public int? AttackerLosses { get; set; }
    public int? DefenderLosses { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? LootMoney { get; set; }
    public decimal? LootOil { get; set; }
    public decimal? LootUranium { get; set; }
    public decimal? LootChips { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public DateTime? ArrivedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }

    public bool? IsOutgoing { get; set; }
    public bool? EndedByShield { get; set; }
    public string? LootSummaryText { get; set; }
}
