using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Voltrok.EF;

public partial class Player
{
    public Guid PlayersId { get; set; }

    public Guid? CountriesId { get; set; }

    public string? FactionCode { get; set; }

    [NotMapped]
    public Guid RegionsId { get; set; }

    public Guid UsersId { get; set; }

    public string Name { get; set; } = null!;

    public decimal LocationX { get; set; }

    public decimal LocationY { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal Money { get; set; }

    public decimal Score { get; set; }

    public decimal Oil { get; set; }

    public decimal Uranium { get; set; }

    public decimal Chips { get; set; }

    public int OilExtractionLevel { get; set; }

    public int UraniumExtractionLevel { get; set; }

    public int MilitaryTechLevel { get; set; }

    public string? PlayerAvatar { get; set; }

    [NotMapped]
    public bool HasRelocatedRegion { get; set; }

    public DateTime? ShieldValidTill { get; set; }

    [NotMapped]
    public bool PremiumShieldEnabled => ShieldValidTill.HasValue && ShieldValidTill.Value > DateTime.UtcNow;

    public bool IsNpc { get; set; }

    public bool IsCountryCouncilMember { get; set; }

    public virtual ICollection<ChatCountryMessage> ChatCountryMessages { get; set; } = new List<ChatCountryMessage>();

    public virtual ICollection<ChatGlobalMessage> ChatGlobalMessages { get; set; } = new List<ChatGlobalMessage>();

    public virtual ICollection<ChatPrivateMessage> ChatPrivateMessageReceiverPlayers { get; set; } = new List<ChatPrivateMessage>();

    public virtual ICollection<ChatPrivateMessage> ChatPrivateMessageSenderPlayers { get; set; } = new List<ChatPrivateMessage>();

    [NotMapped]
    public virtual ICollection<ChatRegionMessage> ChatRegionMessages { get; set; } = new List<ChatRegionMessage>();

    public virtual ICollection<Country> Countries { get; set; } = new List<Country>();

    public virtual Country? CountriesNavigation { get; set; }

    [NotMapped]
    public virtual Country? CountryMembership
    {
        get => CountriesNavigation;
        set => CountriesNavigation = value;
    }

    public virtual CountryFaction? CountryFaction { get; set; }

    [NotMapped]
    public virtual CountryFaction? CountryFactionMembership
    {
        get => CountryFaction;
        set => CountryFaction = value;
    }

    public virtual ICollection<CountryWar> CountryWars { get; set; } = new List<CountryWar>();

    public virtual ICollection<PlayerBadge> PlayerBadges { get; set; } = new List<PlayerBadge>();

    public virtual ICollection<PlayerBattleReport> PlayerBattleReportAttackerPlayers { get; set; } = new List<PlayerBattleReport>();

    public virtual ICollection<PlayerBattleReport> PlayerBattleReportDefenderPlayers { get; set; } = new List<PlayerBattleReport>();

    public virtual ICollection<PlayerDailyStat> PlayerDailyStats { get; set; } = new List<PlayerDailyStat>();

    public virtual ICollection<PlayerInventory> PlayerInventories { get; set; } = new List<PlayerInventory>();

    public virtual ICollection<PlayerMilitary> PlayerMilitaries { get; set; } = new List<PlayerMilitary>();

    public virtual ICollection<PlayerMilitaryOrder> PlayerMilitaryOrders { get; set; } = new List<PlayerMilitaryOrder>();

    public virtual ICollection<PlayerMilitaryTransport> PlayerMilitaryTransportPlayerFroms { get; set; } = new List<PlayerMilitaryTransport>();

    public virtual ICollection<PlayerMilitaryTransport> PlayerMilitaryTransportPlayerTos { get; set; } = new List<PlayerMilitaryTransport>();

    public virtual ICollection<PlayerMilitaryUnit> PlayerMilitaryUnits { get; set; } = new List<PlayerMilitaryUnit>();

    public virtual ICollection<PlayerNeed> PlayerNeeds { get; set; } = new List<PlayerNeed>();

    public virtual ICollection<PlayerNotification> PlayerNotifications { get; set; } = new List<PlayerNotification>();

    public virtual ICollection<PlayerProductionOrder> PlayerProductionOrders { get; set; } = new List<PlayerProductionOrder>();

    public virtual ICollection<PlayerProductionProduct> PlayerProductionProducts { get; set; } = new List<PlayerProductionProduct>();

    public virtual ICollection<PlayerSiege> PlayerSiegeAttackerPlayers { get; set; } = new List<PlayerSiege>();

    public virtual ICollection<PlayerSiege> PlayerSiegePlayers { get; set; } = new List<PlayerSiege>();

    public virtual ICollection<PlayerTransport> PlayerTransportPlayerFroms { get; set; } = new List<PlayerTransport>();

    public virtual ICollection<PlayerTransport> PlayerTransportPlayerTos { get; set; } = new List<PlayerTransport>();

    [NotMapped]
    public virtual ICollection<RegionWar> RegionWars { get; set; } = new List<RegionWar>();

    [NotMapped]
    public virtual Region Regions { get; set; } = null!;

    public virtual ICollection<Trade> TradeFromPlayers { get; set; } = new List<Trade>();

    public virtual ICollection<Trade> TradeToPlayers { get; set; } = new List<Trade>();

    public virtual User Users { get; set; } = null!;
}
