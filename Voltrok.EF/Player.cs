namespace VoltrokEF;

public partial class Player
{
    public Guid PlayersId { get; set; }

    public Guid CountriesId { get; set; }

    public Guid UsersId { get; set; }

    public string Name { get; set; } = null!;

    public decimal LocationX { get; set; }

    public decimal LocationY { get; set; }

    public decimal Money { get; set; }

    public decimal Score { get; set; }

    public decimal Oil { get; set; }

    public decimal Uranium { get; set; }

    public decimal Chips { get; set; }

    public short OilTier { get; set; }

    public short UraniumTier { get; set; }

    public short ChipsTier { get; set; }

    public string? PlayerAvatar { get; set; }

    public bool HasRelocatedRegion { get; set; }

    public DateTime? BattlePassValidTill { get; set; }

    public DateTime? ShieldValidTill { get; set; }

    public bool PremiumShieldEnabled => ShieldValidTill.HasValue && ShieldValidTill.Value > DateTime.UtcNow;

    public DateTime? BattlePassTrialGrantedAt { get; set; }

    public bool IsNpc { get; set; }

    public bool IsCountryCouncilMember { get; set; }

    public virtual ICollection<ChatCountryMessage> ChatCountryMessages { get; set; } = new List<ChatCountryMessage>();

    public virtual ICollection<ChatGlobalMessage> ChatGlobalMessages { get; set; } = new List<ChatGlobalMessage>();

    public virtual ICollection<ChatPrivateMessage> ChatPrivateMessageReceiverPlayers { get; set; } = new List<ChatPrivateMessage>();

    public virtual ICollection<ChatPrivateMessage> ChatPrivateMessageSenderPlayers { get; set; } = new List<ChatPrivateMessage>();

    public virtual Country Countries { get; set; } = null!;

    public virtual ICollection<PlayerBadge> PlayerBadges { get; set; } = new List<PlayerBadge>();

    public virtual ICollection<PlayerBattleReport> PlayerBattleReportPlayerFroms { get; set; } = new List<PlayerBattleReport>();

    public virtual ICollection<PlayerBattleReport> PlayerBattleReportPlayerTos { get; set; } = new List<PlayerBattleReport>();

    public virtual ICollection<PlayerDailyStat> PlayerDailyStats { get; set; } = new List<PlayerDailyStat>();

    public virtual ICollection<PlayerMilitaryProduction> PlayerMilitaryProductions { get; set; } = new List<PlayerMilitaryProduction>();

    public virtual ICollection<PlayerMilitaryTransport> PlayerMilitaryTransportPlayerFroms { get; set; } = new List<PlayerMilitaryTransport>();

    public virtual ICollection<PlayerMilitaryTransport> PlayerMilitaryTransportPlayerTos { get; set; } = new List<PlayerMilitaryTransport>();

    public virtual ICollection<PlayerMilitaryUnit> PlayerMilitaryUnits { get; set; } = new List<PlayerMilitaryUnit>();

    public virtual ICollection<PlayerNotification> PlayerNotifications { get; set; } = new List<PlayerNotification>();

    public virtual ICollection<PlayerSiege> PlayerSiegePlayerFroms { get; set; } = new List<PlayerSiege>();

    public virtual ICollection<PlayerSiege> PlayerSiegePlayerTos { get; set; } = new List<PlayerSiege>();

    public virtual ICollection<PlayerTrade> PlayerTradeFromPlayers { get; set; } = new List<PlayerTrade>();

    public virtual ICollection<PlayerTrade> PlayerTradeToPlayers { get; set; } = new List<PlayerTrade>();

    public virtual User Users { get; set; } = null!;
}
