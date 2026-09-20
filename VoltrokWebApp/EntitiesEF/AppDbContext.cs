using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Voltrok.EF;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BackgroundWorkerLastRun> BackgroundWorkerLastRuns { get; set; }

    public virtual DbSet<BadgeType> BadgeTypes { get; set; }

    public virtual DbSet<ChatCountryMessage> ChatCountryMessages { get; set; }

    public virtual DbSet<ChatGlobalMessage> ChatGlobalMessages { get; set; }

    public virtual DbSet<ChatPrivateMessage> ChatPrivateMessages { get; set; }

    public virtual DbSet<ChatRegionMessage> ChatRegionMessages { get; set; }

    public virtual DbSet<Country> Countries { get; set; }

    public virtual DbSet<CountryWar> CountryWars { get; set; }

    public virtual DbSet<MilitaryUnit> MilitaryUnits { get; set; }

    public virtual DbSet<Need> Needs { get; set; }

    public virtual DbSet<Player> Players { get; set; }

    public virtual DbSet<PlayerBadge> PlayerBadges { get; set; }

    public virtual DbSet<PlayerBattleReport> PlayerBattleReports { get; set; }

    public virtual DbSet<PlayerBattleReportProduct> PlayerBattleReportProducts { get; set; }

    public virtual DbSet<PlayerDailyStat> PlayerDailyStats { get; set; }

    public virtual DbSet<PlayerInventory> PlayerInventories { get; set; }

    public virtual DbSet<PlayerMilitary> PlayerMilitaries { get; set; }

    public virtual DbSet<PlayerMilitaryOrder> PlayerMilitaryOrders { get; set; }

    public virtual DbSet<PlayerMilitaryTransport> PlayerMilitaryTransports { get; set; }

    public virtual DbSet<PlayerMilitaryTransportUnit> PlayerMilitaryTransportUnits { get; set; }

    public virtual DbSet<PlayerMilitaryUnit> PlayerMilitaryUnits { get; set; }

    public virtual DbSet<PlayerNeed> PlayerNeeds { get; set; }

    public virtual DbSet<PlayerNotification> PlayerNotifications { get; set; }

    public virtual DbSet<PlayerProductionOrder> PlayerProductionOrders { get; set; }

    public virtual DbSet<PlayerProductionProduct> PlayerProductionProducts { get; set; }

    public virtual DbSet<PlayerSiege> PlayerSieges { get; set; }

    public virtual DbSet<PlayerTransport> PlayerTransports { get; set; }

    public virtual DbSet<PlayerTransportProduct> PlayerTransportProducts { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductsNeedsSupply> ProductsNeedsSupplies { get; set; }

    public virtual DbSet<Region> Regions { get; set; }

    public virtual DbSet<RegionWar> RegionWars { get; set; }

    public virtual DbSet<Trade> Trades { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BackgroundWorkerLastRun>(entity =>
        {
            entity.HasKey(e => e.RunType).HasName("background_worker_last_runs_pkey");

            entity.ToTable("background_worker_last_runs");

            entity.Property(e => e.RunType)
                .HasMaxLength(128)
                .HasColumnName("run_type");
            entity.Property(e => e.LastExecutedAtUtc)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_executed_at_utc");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<BadgeType>(entity =>
        {
            entity.HasKey(e => e.BadgeTypesId).HasName("badge_types_pkey");

            entity.ToTable("badge_types");

            entity.HasIndex(e => e.Code, "badge_types_uq_badge_types_code").IsUnique();

            entity.Property(e => e.BadgeTypesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("badge_types_id");
            entity.Property(e => e.Code)
                .HasMaxLength(80)
                .HasColumnName("code");
            entity.Property(e => e.ColorHex)
                .HasMaxLength(7)
                .HasColumnName("color_hex");
            entity.Property(e => e.Label)
                .HasMaxLength(120)
                .HasColumnName("label");
        });

        modelBuilder.Entity<ChatCountryMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("chat_country_messages_pkey");

            entity.ToTable("chat_country_messages");

            entity.HasIndex(e => e.CountriesId, "chat_country_messages_idx_countries_id");

            entity.HasIndex(e => e.PlayersId, "chat_country_messages_idx_players_id");

            entity.HasIndex(e => e.SentAt, "chat_country_messages_idx_sent_at");

            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("message_id");
            entity.Property(e => e.CountriesId).HasColumnName("countries_id");
            entity.Property(e => e.MessageText).HasColumnName("message_text");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("sent_at");

            entity.HasOne(d => d.Countries).WithMany(p => p.ChatCountryMessages)
                .HasForeignKey(d => d.CountriesId)
                .HasConstraintName("chat_country_messages_countries_id_fkey");

            entity.HasOne(d => d.Players).WithMany(p => p.ChatCountryMessages)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("chat_country_messages_players_id_fkey");
        });

        modelBuilder.Entity<ChatGlobalMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("chat_global_messages_pkey");

            entity.ToTable("chat_global_messages");

            entity.HasIndex(e => e.PlayersId, "chat_global_messages_idx_players_id");

            entity.HasIndex(e => e.SentAt, "chat_global_messages_idx_sent_at");

            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("message_id");
            entity.Property(e => e.MessageText).HasColumnName("message_text");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("sent_at");

            entity.HasOne(d => d.Players).WithMany(p => p.ChatGlobalMessages)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("chat_global_messages_players_id_fkey");
        });

        modelBuilder.Entity<ChatPrivateMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("chat_private_messages_pkey");

            entity.ToTable("chat_private_messages");

            entity.HasIndex(e => new { e.SenderPlayersId, e.ReceiverPlayersId, e.SentAt }, "chat_private_messages_idx_conversation");

            entity.HasIndex(e => e.ReceiverPlayersId, "chat_private_messages_idx_receiver");

            entity.HasIndex(e => e.SenderPlayersId, "chat_private_messages_idx_sender");

            entity.HasIndex(e => new { e.ReceiverPlayersId, e.IsRead }, "chat_private_messages_idx_unread");

            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("message_id");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.MessageText).HasColumnName("message_text");
            entity.Property(e => e.ReceiverPlayersId).HasColumnName("receiver_players_id");
            entity.Property(e => e.SenderPlayersId).HasColumnName("sender_players_id");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("sent_at");

            entity.HasOne(d => d.ReceiverPlayers).WithMany(p => p.ChatPrivateMessageReceiverPlayers)
                .HasForeignKey(d => d.ReceiverPlayersId)
                .HasConstraintName("chat_private_messages_receiver_players_id_fkey");

            entity.HasOne(d => d.SenderPlayers).WithMany(p => p.ChatPrivateMessageSenderPlayers)
                .HasForeignKey(d => d.SenderPlayersId)
                .HasConstraintName("chat_private_messages_sender_players_id_fkey");
        });

        modelBuilder.Entity<ChatRegionMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("chat_region_messages_pkey");

            entity.ToTable("chat_region_messages");

            entity.HasIndex(e => e.PlayersId, "chat_region_messages_idx_players_id");

            entity.HasIndex(e => e.RegionsId, "chat_region_messages_idx_regions_id");

            entity.HasIndex(e => e.SentAt, "chat_region_messages_idx_sent_at");

            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("message_id");
            entity.Property(e => e.MessageText).HasColumnName("message_text");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.RegionsId).HasColumnName("regions_id");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("sent_at");

            entity.HasOne(d => d.Players).WithMany(p => p.ChatRegionMessages)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("chat_region_messages_players_id_fkey");

            entity.HasOne(d => d.Regions).WithMany(p => p.ChatRegionMessages)
                .HasForeignKey(d => d.RegionsId)
                .HasConstraintName("chat_region_messages_regions_id_fkey");
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.HasKey(e => e.CountriesId).HasName("countries_pkey");

            entity.ToTable("countries");

            entity.HasIndex(e => e.Name, "countries_idx_name");

            entity.HasIndex(e => e.PresidentPlayersId, "countries_idx_president");

            entity.HasIndex(e => e.Name, "countries_name_key").IsUnique();

            entity.Property(e => e.CountriesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("countries_id");
            entity.Property(e => e.ColorHex)
                .HasMaxLength(7)
                .HasDefaultValueSql("'#3498db'::character varying")
                .HasColumnName("color_hex");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.ChipsTier)
                .HasMaxLength(8)
                .HasColumnName("chips_tier");
            entity.Property(e => e.Name)
                .HasMaxLength(250)
                .HasColumnName("name");
            entity.Property(e => e.OilTier)
                .HasMaxLength(8)
                .HasColumnName("oil_tier");
            entity.Property(e => e.PresidentPlayersId).HasColumnName("president_players_id");
            entity.Property(e => e.UraniumTier)
                .HasMaxLength(8)
                .HasColumnName("uranium_tier");

            entity.HasOne(d => d.PresidentPlayers).WithMany(p => p.Countries)
                .HasForeignKey(d => d.PresidentPlayersId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("countries_president_players_id_fkey");
        });

        modelBuilder.Entity<CountryWar>(entity =>
        {
            entity.HasKey(e => e.CountryWarsId).HasName("country_wars_pkey");

            entity.ToTable("country_wars");

            entity.HasIndex(e => new { e.AttackerCountriesId, e.DefenderCountriesId, e.Status }, "country_wars_idx_country_wars_pair_status");

            entity.Property(e => e.CountryWarsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("country_wars_id");
            entity.Property(e => e.AttackerCountriesId).HasColumnName("attacker_countries_id");
            entity.Property(e => e.CancelPenaltyPercent)
                .HasPrecision(5, 2)
                .HasDefaultValue(2.00m)
                .HasColumnName("cancel_penalty_percent");
            entity.Property(e => e.CaptureThresholdPercent)
                .HasPrecision(5, 2)
                .HasDefaultValue(75.00m)
                .HasColumnName("capture_threshold_percent");
            entity.Property(e => e.ConqueredFromCountriesId).HasColumnName("conquered_from_countries_id");
            entity.Property(e => e.DeclaredByPlayersId).HasColumnName("declared_by_players_id");
            entity.Property(e => e.DefenderCountriesId).HasColumnName("defender_countries_id");
            entity.Property(e => e.EndReason)
                .HasMaxLength(64)
                .HasColumnName("end_reason");
            entity.Property(e => e.EndedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ended_at");
            entity.Property(e => e.StartedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .HasDefaultValueSql("'active'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.AttackerCountries).WithMany(p => p.CountryWarAttackerCountries)
                .HasForeignKey(d => d.AttackerCountriesId)
                .HasConstraintName("country_wars_attacker_countries_id_fkey");

            entity.HasOne(d => d.ConqueredFromCountries).WithMany(p => p.CountryWarConqueredFromCountries)
                .HasForeignKey(d => d.ConqueredFromCountriesId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("country_wars_conquered_from_countries_id_fkey");

            entity.HasOne(d => d.DeclaredByPlayers).WithMany(p => p.CountryWars)
                .HasForeignKey(d => d.DeclaredByPlayersId)
                .HasConstraintName("country_wars_declared_by_players_id_fkey");

            entity.HasOne(d => d.DefenderCountries).WithMany(p => p.CountryWarDefenderCountries)
                .HasForeignKey(d => d.DefenderCountriesId)
                .HasConstraintName("country_wars_defender_countries_id_fkey");
        });

        modelBuilder.Entity<MilitaryUnit>(entity =>
        {
            entity.HasKey(e => e.MilitaryUnitsId).HasName("military_units_pkey");

            entity.ToTable("military_units");

            entity.HasIndex(e => e.Code, "military_units_idx_military_unit_code");

            entity.HasIndex(e => e.Code, "military_units_unique_military_unit_code").IsUnique();

            entity.Property(e => e.MilitaryUnitsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("military_units_id");
            entity.Property(e => e.AttackPoints).HasColumnName("attack_points");
            entity.Property(e => e.Code)
                .HasMaxLength(250)
                .HasColumnName("code");
            entity.Property(e => e.DefensePoints).HasColumnName("defense_points");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Icon)
                .HasMaxLength(120)
                .HasColumnName("icon");
            entity.Property(e => e.ProductionCost)
                .HasPrecision(18, 2)
                .HasColumnName("production_cost");
            entity.Property(e => e.TimeSeconds).HasColumnName("time_seconds");
            entity.Property(e => e.TravelSpeedKmh)
                .HasPrecision(8, 2)
                .HasColumnName("travel_speed_kmh");
        });

        modelBuilder.Entity<Need>(entity =>
        {
            entity.HasKey(e => e.NeedsId).HasName("needs_pkey");

            entity.ToTable("needs");

            entity.HasIndex(e => e.Code, "needs_code_key").IsUnique();

            entity.HasIndex(e => e.Code, "needs_idx_code");

            entity.Property(e => e.NeedsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("needs_id");
            entity.Property(e => e.Code)
                .HasMaxLength(250)
                .HasColumnName("code");
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.PlayersId).HasName("players_pkey");

            entity.ToTable("players");

            entity.HasIndex(e => e.IsNpc, "players_idx_is_npc");

            entity.HasIndex(e => new { e.LocationX, e.LocationY }, "players_idx_location");

            entity.HasIndex(e => e.Name, "players_idx_name");

            entity.HasIndex(e => e.UsersId, "players_idx_users_id");

            entity.HasIndex(e => e.UsersId, "players_unique_user_id").IsUnique();

            entity.Property(e => e.PlayersId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("players_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.IsCountryCouncilMember).HasColumnName("is_country_council_member");
            entity.Property(e => e.IsNpc).HasColumnName("is_npc");
            entity.Property(e => e.LocationX)
                .HasPrecision(18, 8)
                .HasColumnName("location_x");
            entity.Property(e => e.LocationY)
                .HasPrecision(18, 8)
                .HasColumnName("location_y");
            entity.Property(e => e.Money)
                .HasPrecision(18, 2)
                .HasColumnName("money");
            entity.Property(e => e.Oil)
                .HasPrecision(18, 2)
                .HasColumnName("oil");
            entity.Property(e => e.OilExtractionLevel).HasColumnName("oil_extraction_level");
            entity.Property(e => e.Name)
                .HasMaxLength(250)
                .HasColumnName("name");
            entity.Property(e => e.PlayerAvatar)
                .HasMaxLength(500)
                .HasColumnName("player_avatar");
            entity.Property(e => e.ShieldValidTill).HasColumnName("shield_valid_till");
            entity.Property(e => e.Score)
                .HasPrecision(18, 2)
                .HasColumnName("score");
            entity.Property(e => e.Uranium)
                .HasPrecision(18, 2)
                .HasColumnName("uranium");
            entity.Property(e => e.Chips)
                .HasPrecision(18, 2)
                .HasColumnName("chips");
            entity.Property(e => e.UraniumExtractionLevel).HasColumnName("uranium_extraction_level");
            entity.Property(e => e.MilitaryTechLevel).HasColumnName("military_tech_level");
            entity.Property(e => e.UsersId).HasColumnName("users_id");

            entity.HasOne(d => d.Users).WithOne(p => p.Player)
                .HasForeignKey<Player>(d => d.UsersId)
                .HasConstraintName("players_users_id_fkey");
        });

        modelBuilder.Entity<PlayerBadge>(entity =>
        {
            entity.HasKey(e => e.PlayerBadgesId).HasName("player_badges_pkey");

            entity.ToTable("player_badges");

            entity.HasIndex(e => e.BadgeTypesId, "player_badges_idx_player_badges_badge_type");

            entity.HasIndex(e => e.PlayersId, "player_badges_idx_player_badges_player");

            entity.HasIndex(e => new { e.PlayersId, e.BadgeTypesId }, "player_badges_uq_player_badges_player_badge").IsUnique();

            entity.Property(e => e.PlayerBadgesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_badges_id");
            entity.Property(e => e.AwardedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("awarded_at");
            entity.Property(e => e.BadgeTypesId).HasColumnName("badge_types_id");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");

            entity.HasOne(d => d.BadgeTypes).WithMany(p => p.PlayerBadges)
                .HasForeignKey(d => d.BadgeTypesId)
                .HasConstraintName("player_badges_badge_types_id_fkey");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerBadges)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_badges_players_id_fkey");
        });

        modelBuilder.Entity<PlayerBattleReport>(entity =>
        {
            entity.HasKey(e => e.PlayerBattleReportsId).HasName("player_battle_reports_pkey");

            entity.ToTable("player_battle_reports");

            entity.HasIndex(e => e.AttackerPlayersId, "player_battle_reports_idx_battle_attacker");

            entity.HasIndex(e => e.DefenderPlayersId, "player_battle_reports_idx_battle_defender");

            entity.HasIndex(e => e.Result, "player_battle_reports_idx_battle_result");

            entity.Property(e => e.PlayerBattleReportsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_battle_reports_id");
            entity.Property(e => e.AttackerLosses).HasColumnName("attacker_losses");
            entity.Property(e => e.AttackerPlayersId).HasColumnName("attacker_players_id");
            entity.Property(e => e.AttackerPower).HasColumnName("attacker_power");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DefenderLosses).HasColumnName("defender_losses");
            entity.Property(e => e.DefenderPlayersId).HasColumnName("defender_players_id");
            entity.Property(e => e.DefenderPower).HasColumnName("defender_power");
            entity.Property(e => e.EndedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ended_at");
            entity.Property(e => e.LootMoney)
                .HasPrecision(18, 2)
                .HasColumnName("loot_money");
            entity.Property(e => e.Result)
                .HasMaxLength(250)
                .HasColumnName("result");
            entity.Property(e => e.StartedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");

            entity.HasOne(d => d.AttackerPlayers).WithMany(p => p.PlayerBattleReportAttackerPlayers)
                .HasForeignKey(d => d.AttackerPlayersId)
                .HasConstraintName("player_battle_reports_attacker_players_id_fkey");

            entity.HasOne(d => d.DefenderPlayers).WithMany(p => p.PlayerBattleReportDefenderPlayers)
                .HasForeignKey(d => d.DefenderPlayersId)
                .HasConstraintName("player_battle_reports_defender_players_id_fkey");
        });

        modelBuilder.Entity<PlayerBattleReportProduct>(entity =>
        {
            entity.HasKey(e => e.PlayerBattleReportProductsId).HasName("player_battle_report_products_pkey");

            entity.ToTable("player_battle_report_products");

            entity.HasIndex(e => e.ProductsId, "player_battle_report_products_idx_battle_products_id");

            entity.HasIndex(e => e.PlayerBattleReportsId, "player_battle_report_products_idx_battle_report_id");

            entity.Property(e => e.PlayerBattleReportProductsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_battle_report_products_id");
            entity.Property(e => e.PlayerBattleReportsId).HasColumnName("player_battle_reports_id");
            entity.Property(e => e.ProductsId).HasColumnName("products_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.PlayerBattleReports).WithMany(p => p.PlayerBattleReportProducts)
                .HasForeignKey(d => d.PlayerBattleReportsId)
                .HasConstraintName("player_battle_report_products_player_battle_reports_id_fkey");

            entity.HasOne(d => d.Products).WithMany(p => p.PlayerBattleReportProducts)
                .HasForeignKey(d => d.ProductsId)
                .HasConstraintName("player_battle_report_products_products_id_fkey");
        });

        modelBuilder.Entity<PlayerDailyStat>(entity =>
        {
            entity.HasKey(e => e.PlayerDailyStatsId).HasName("player_daily_stats_pkey");

            entity.ToTable("player_daily_stats");

            entity.HasIndex(e => new { e.PlayersId, e.StatsDate }, "player_daily_stats_idx_player_daily_stats_player_date");

            entity.HasIndex(e => new { e.PlayersId, e.StatsDate }, "player_daily_stats_uq_player_daily_stats_player_date").IsUnique();

            entity.Property(e => e.PlayerDailyStatsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_daily_stats_id");
            entity.Property(e => e.ArmyTotal).HasColumnName("army_total");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Money)
                .HasPrecision(18, 2)
                .HasColumnName("money");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.ResourcesTotal).HasColumnName("resources_total");
            entity.Property(e => e.Score)
                .HasPrecision(18, 2)
                .HasColumnName("score");
            entity.Property(e => e.StatsDate).HasColumnName("stats_date");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerDailyStats)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_daily_stats_players_id_fkey");
        });

        modelBuilder.Entity<PlayerInventory>(entity =>
        {
            entity.HasKey(e => e.PlayerInventoriesId).HasName("player_inventories_pkey");

            entity.ToTable("player_inventories");

            entity.HasIndex(e => e.PlayersId, "player_inventories_idx_players_id");

            entity.HasIndex(e => e.ProductsId, "player_inventories_idx_products_id");

            entity.HasIndex(e => new { e.PlayersId, e.ProductsId }, "player_inventories_unique_player_product").IsUnique();

            entity.Property(e => e.PlayerInventoriesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_inventories_id");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.ProductsId).HasColumnName("products_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerInventories)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_inventories_players_id_fkey");

            entity.HasOne(d => d.Products).WithMany(p => p.PlayerInventories)
                .HasForeignKey(d => d.ProductsId)
                .HasConstraintName("player_inventories_products_id_fkey");
        });

        modelBuilder.Entity<PlayerMilitary>(entity =>
        {
            entity.HasKey(e => e.PlayerMilitaryLogId).HasName("player_military_pkey");

            entity.ToTable("player_military");

            entity.HasIndex(e => e.PlayersId, "player_military_idx_players_id");

            entity.Property(e => e.PlayerMilitaryLogId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_military_log_id");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerMilitaries)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_military_players_id_fkey");
        });

        modelBuilder.Entity<PlayerMilitaryOrder>(entity =>
        {
            entity.HasKey(e => e.PlayerMilitaryOrdersId).HasName("player_military_orders_pkey");

            entity.ToTable("player_military_orders");

            entity.HasIndex(e => e.PlayersId, "player_military_orders_idx_player_military_orders_player");

            entity.HasIndex(e => e.MilitaryUnitsId, "player_military_orders_idx_player_military_orders_unit");

            entity.Property(e => e.PlayerMilitaryOrdersId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_military_orders_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.MilitaryUnitsId).HasColumnName("military_units_id");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.ProducedQuantity).HasColumnName("produced_quantity");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.MilitaryUnits).WithMany(p => p.PlayerMilitaryOrders)
                .HasForeignKey(d => d.MilitaryUnitsId)
                .HasConstraintName("player_military_orders_military_units_id_fkey");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerMilitaryOrders)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_military_orders_players_id_fkey");
        });

        modelBuilder.Entity<PlayerMilitaryTransport>(entity =>
        {
            entity.HasKey(e => e.PlayerMilitaryTransportsId).HasName("player_military_transports_pkey");

            entity.ToTable("player_military_transports");

            entity.HasIndex(e => e.PlayerFromId, "player_military_transports_idx_military_player_from_id");

            entity.HasIndex(e => e.PlayerToId, "player_military_transports_idx_military_player_to_id");

            entity.HasIndex(e => e.Status, "player_military_transports_idx_military_status");

            entity.Property(e => e.PlayerMilitaryTransportsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_military_transports_id");
            entity.Property(e => e.AutoReturnAfterBattle).HasColumnName("auto_return_after_battle");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.EndTime)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("end_time");
            entity.Property(e => e.MissionType)
                .HasMaxLength(250)
                .HasColumnName("mission_type");
            entity.Property(e => e.PlayerFromId).HasColumnName("player_from_id");
            entity.Property(e => e.PlayerToId).HasColumnName("player_to_id");
            entity.Property(e => e.StartTime)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasMaxLength(250)
                .HasColumnName("status");

            entity.HasOne(d => d.PlayerFrom).WithMany(p => p.PlayerMilitaryTransportPlayerFroms)
                .HasForeignKey(d => d.PlayerFromId)
                .HasConstraintName("player_military_transports_player_from_id_fkey");

            entity.HasOne(d => d.PlayerTo).WithMany(p => p.PlayerMilitaryTransportPlayerTos)
                .HasForeignKey(d => d.PlayerToId)
                .HasConstraintName("player_military_transports_player_to_id_fkey");
        });

        modelBuilder.Entity<PlayerMilitaryTransportUnit>(entity =>
        {
            entity.HasKey(e => e.PlayerMilitaryTransportUnitsId).HasName("player_military_transport_units_pkey");

            entity.ToTable("player_military_transport_units");

            entity.HasIndex(e => e.PlayerMilitaryTransportsId, "player_military_transport_units_idx_military_transport_id");

            entity.Property(e => e.PlayerMilitaryTransportUnitsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_military_transport_units_id");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.PlayerMilitaryTransportsId).HasColumnName("player_military_transports_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.PlayerMilitaryTransports).WithMany(p => p.PlayerMilitaryTransportUnits)
                .HasForeignKey(d => d.PlayerMilitaryTransportsId)
                .HasConstraintName("player_military_transport_uni_player_military_transports_i_fkey");
        });

        modelBuilder.Entity<PlayerMilitaryUnit>(entity =>
        {
            entity.HasKey(e => e.PlayerMilitaryUnitsId).HasName("player_military_units_pkey");

            entity.ToTable("player_military_units");

            entity.HasIndex(e => e.PlayersId, "player_military_units_idx_player_military_units_player");

            entity.HasIndex(e => e.MilitaryUnitsId, "player_military_units_idx_player_military_units_unit");

            entity.HasIndex(e => new { e.PlayersId, e.MilitaryUnitsId }, "player_military_units_unique_player_military_unit").IsUnique();

            entity.Property(e => e.PlayerMilitaryUnitsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_military_units_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.MilitaryUnitsId).HasColumnName("military_units_id");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");

            entity.HasOne(d => d.MilitaryUnits).WithMany(p => p.PlayerMilitaryUnits)
                .HasForeignKey(d => d.MilitaryUnitsId)
                .HasConstraintName("player_military_units_military_units_id_fkey");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerMilitaryUnits)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_military_units_players_id_fkey");
        });

        modelBuilder.Entity<PlayerNeed>(entity =>
        {
            entity.HasKey(e => e.PlayerNeedsId).HasName("player_needs_pkey");

            entity.ToTable("player_needs");

            entity.HasIndex(e => e.NeedsId, "player_needs_idx_needs_id");

            entity.HasIndex(e => e.PlayersId, "player_needs_idx_players_id");

            entity.HasIndex(e => new { e.PlayersId, e.NeedsId }, "player_needs_unique_players_need").IsUnique();

            entity.Property(e => e.PlayerNeedsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_needs_id");
            entity.Property(e => e.NeedsId).HasColumnName("needs_id");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.Value)
                .HasPrecision(18, 2)
                .HasColumnName("value");

            entity.HasOne(d => d.Needs).WithMany(p => p.PlayerNeeds)
                .HasForeignKey(d => d.NeedsId)
                .HasConstraintName("player_needs_needs_id_fkey");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerNeeds)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_needs_players_id_fkey");
        });

        modelBuilder.Entity<PlayerNotification>(entity =>
        {
            entity.HasKey(e => e.PlayerNotificationsId).HasName("player_notifications_pkey");

            entity.ToTable("player_notifications");

            entity.HasIndex(e => new { e.PlayersId, e.CreatedAt }, "player_notifications_idx_notifications_player_created");

            entity.HasIndex(e => new { e.PlayersId, e.IsPopupDelivered }, "player_notifications_idx_notifications_popup");

            entity.HasIndex(e => new { e.PlayersId, e.IsRead }, "player_notifications_idx_notifications_unread");

            entity.Property(e => e.PlayerNotificationsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_notifications_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DataJson).HasColumnName("data_json");
            entity.Property(e => e.IsPopupDelivered).HasColumnName("is_popup_delivered");
            entity.Property(e => e.IsRead).HasColumnName("is_read");
            entity.Property(e => e.Message)
                .HasMaxLength(1024)
                .HasColumnName("message");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.PopupDeliveredAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("popup_delivered_at");
            entity.Property(e => e.ReadAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("read_at");
            entity.Property(e => e.Title)
                .HasMaxLength(250)
                .HasColumnName("title");
            entity.Property(e => e.Type)
                .HasMaxLength(64)
                .HasColumnName("type");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerNotifications)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_notifications_players_id_fkey");
        });

        modelBuilder.Entity<PlayerProductionOrder>(entity =>
        {
            entity.HasKey(e => e.PlayerProductionOrdersId).HasName("player_production_orders_pkey");

            entity.ToTable("player_production_orders");

            entity.HasIndex(e => e.PlayersId, "player_production_orders_idx_players_id");

            entity.HasIndex(e => e.ProductsId, "player_production_orders_idx_products_id");

            entity.Property(e => e.PlayerProductionOrdersId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_production_orders_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.ProducedQuantity).HasColumnName("produced_quantity");
            entity.Property(e => e.ProductsId).HasColumnName("products_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerProductionOrders)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_production_orders_players_id_fkey");

            entity.HasOne(d => d.Products).WithMany(p => p.PlayerProductionOrders)
                .HasForeignKey(d => d.ProductsId)
                .HasConstraintName("player_production_orders_products_id_fkey");
        });

        modelBuilder.Entity<PlayerProductionProduct>(entity =>
        {
            entity.HasKey(e => e.PlayerProductionProductsId).HasName("player_production_products_pkey");

            entity.ToTable("player_production_products");

            entity.HasIndex(e => e.PlayersId, "player_production_products_idx_players_id");

            entity.HasIndex(e => e.ProductsId, "player_production_products_idx_products_id");

            entity.HasIndex(e => new { e.PlayersId, e.ProductsId }, "player_production_products_unique_player_product").IsUnique();

            entity.Property(e => e.PlayerProductionProductsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_production_products_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.ProductsId).HasColumnName("products_id");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerProductionProducts)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_production_products_players_id_fkey");

            entity.HasOne(d => d.Products).WithMany(p => p.PlayerProductionProducts)
                .HasForeignKey(d => d.ProductsId)
                .HasConstraintName("player_production_products_products_id_fkey");
        });

        modelBuilder.Entity<PlayerSiege>(entity =>
        {
            entity.HasKey(e => e.PlayerSiegesId).HasName("player_sieges_pkey");

            entity.ToTable("player_sieges");

            entity.HasIndex(e => e.AttackerPlayersId, "player_sieges_idx_siege_attacker");

            entity.HasIndex(e => e.PlayersId, "player_sieges_idx_siege_player");

            entity.HasIndex(e => e.Status, "player_sieges_idx_siege_status");

            entity.Property(e => e.PlayerSiegesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_sieges_id");
            entity.Property(e => e.AttackerPlayersId).HasColumnName("attacker_players_id");
            entity.Property(e => e.EndedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ended_at");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.StartedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(250)
                .HasColumnName("status");

            entity.HasOne(d => d.AttackerPlayers).WithMany(p => p.PlayerSiegeAttackerPlayers)
                .HasForeignKey(d => d.AttackerPlayersId)
                .HasConstraintName("player_sieges_attacker_players_id_fkey");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerSiegePlayers)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_sieges_players_id_fkey");
        });

        modelBuilder.Entity<PlayerTransport>(entity =>
        {
            entity.HasKey(e => e.PlayerTransportsId).HasName("player_transports_pkey");

            entity.ToTable("player_transports");

            entity.HasIndex(e => e.PlayerFromId, "player_transports_idx_player_from_id");

            entity.HasIndex(e => e.PlayerToId, "player_transports_idx_player_to_id");

            entity.HasIndex(e => e.Status, "player_transports_idx_status");

            entity.Property(e => e.PlayerTransportsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_transports_id");
            entity.Property(e => e.EndTime)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("end_time");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.PlayerFromId).HasColumnName("player_from_id");
            entity.Property(e => e.PlayerToId).HasColumnName("player_to_id");
            entity.Property(e => e.StartTime)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasMaxLength(250)
                .HasColumnName("status");

            entity.HasOne(d => d.PlayerFrom).WithMany(p => p.PlayerTransportPlayerFroms)
                .HasForeignKey(d => d.PlayerFromId)
                .HasConstraintName("player_transports_player_from_id_fkey");

            entity.HasOne(d => d.PlayerTo).WithMany(p => p.PlayerTransportPlayerTos)
                .HasForeignKey(d => d.PlayerToId)
                .HasConstraintName("player_transports_player_to_id_fkey");
        });

        modelBuilder.Entity<PlayerTransportProduct>(entity =>
        {
            entity.HasKey(e => e.PlayerTransportProductsId).HasName("player_transport_products_pkey");

            entity.ToTable("player_transport_products");

            entity.HasIndex(e => e.PlayerTransportsId, "player_transport_products_idx_player_transports_id");

            entity.HasIndex(e => e.ProductsId, "player_transport_products_idx_products_id");

            entity.Property(e => e.PlayerTransportProductsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_transport_products_id");
            entity.Property(e => e.PlayerTransportsId).HasColumnName("player_transports_id");
            entity.Property(e => e.ProductsId).HasColumnName("products_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.PlayerTransports).WithMany(p => p.PlayerTransportProducts)
                .HasForeignKey(d => d.PlayerTransportsId)
                .HasConstraintName("player_transport_products_player_transports_id_fkey");

            entity.HasOne(d => d.Products).WithMany(p => p.PlayerTransportProducts)
                .HasForeignKey(d => d.ProductsId)
                .HasConstraintName("player_transport_products_products_id_fkey");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductsId).HasName("products_pkey");

            entity.ToTable("products");

            entity.HasIndex(e => e.Code, "products_idx_code");

            entity.Property(e => e.ProductsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("products_id");
            entity.Property(e => e.Code)
                .HasMaxLength(250)
                .HasColumnName("code");
            entity.Property(e => e.ProductionCost)
                .HasPrecision(18, 2)
                .HasColumnName("production_cost");
            entity.Property(e => e.TimeSeconds).HasColumnName("time_seconds");
        });

        modelBuilder.Entity<ProductsNeedsSupply>(entity =>
        {
            entity.HasKey(e => e.ProductsNeedsSuppliesId).HasName("products_needs_supplies_pkey");

            entity.ToTable("products_needs_supplies");

            entity.HasIndex(e => e.NeedsId, "products_needs_supplies_idx_needs_id");

            entity.HasIndex(e => e.ProductsId, "products_needs_supplies_idx_products_id");

            entity.HasIndex(e => new { e.ProductsId, e.NeedsId }, "products_needs_supplies_unique_product_need").IsUnique();

            entity.Property(e => e.ProductsNeedsSuppliesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("products_needs_supplies_id");
            entity.Property(e => e.NeedsId).HasColumnName("needs_id");
            entity.Property(e => e.ProductsId).HasColumnName("products_id");
            entity.Property(e => e.Quantity)
                .HasPrecision(18, 2)
                .HasColumnName("quantity");

            entity.HasOne(d => d.Needs).WithMany(p => p.ProductsNeedsSupplies)
                .HasForeignKey(d => d.NeedsId)
                .HasConstraintName("products_needs_supplies_needs_id_fkey");

            entity.HasOne(d => d.Products).WithMany(p => p.ProductsNeedsSupplies)
                .HasForeignKey(d => d.ProductsId)
                .HasConstraintName("products_needs_supplies_products_id_fkey");
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.HasKey(e => e.RegionsId).HasName("regions_pkey");

            entity.ToTable("regions");

            entity.HasIndex(e => e.CountriesId, "regions_idx_countries_id");

            entity.HasIndex(e => e.PresidentPlayersId, "regions_idx_president");

            entity.HasIndex(e => e.Name, "regions_name_key").IsUnique();

            entity.Property(e => e.RegionsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("regions_id");
            entity.Property(e => e.AdmCode1)
                .HasMaxLength(10)
                .HasColumnName("adm_code_1");
            entity.Property(e => e.Area)
                .HasPrecision(18, 2)
                .HasDefaultValue(0.00m)
                .HasColumnName("area");
            entity.Property(e => e.Color)
                .HasMaxLength(7)
                .HasDefaultValueSql("'#ffffff'::character varying")
                .HasColumnName("color");
            entity.Property(e => e.CountriesId).HasColumnName("countries_id");
            entity.Property(e => e.Flag).HasColumnName("flag");
            entity.Property(e => e.IsoCode2)
                .HasMaxLength(10)
                .HasColumnName("iso_code_2");
            entity.Property(e => e.Name)
                .HasMaxLength(250)
                .HasColumnName("name");
            entity.Property(e => e.PresidentPlayersId).HasColumnName("president_players_id");
        });

        modelBuilder.Entity<RegionWar>(entity =>
        {
            entity.HasKey(e => e.RegionWarsId).HasName("region_wars_pkey");

            entity.ToTable("region_wars");

            entity.HasIndex(e => new { e.AttackerRegionsId, e.DefenderRegionsId, e.Status }, "region_wars_idx_region_wars_pair_status");

            entity.Property(e => e.RegionWarsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("region_wars_id");
            entity.Property(e => e.AttackerRegionsId).HasColumnName("attacker_regions_id");
            entity.Property(e => e.CancelPenaltyPercent)
                .HasPrecision(5, 2)
                .HasDefaultValue(2.00m)
                .HasColumnName("cancel_penalty_percent");
            entity.Property(e => e.CaptureThresholdPercent)
                .HasPrecision(5, 2)
                .HasDefaultValue(75.00m)
                .HasColumnName("capture_threshold_percent");
            entity.Property(e => e.ConqueredFromRegionsId).HasColumnName("conquered_from_regions_id");
            entity.Property(e => e.DeclaredByPlayersId).HasColumnName("declared_by_players_id");
            entity.Property(e => e.DefenderRegionsId).HasColumnName("defender_regions_id");
            entity.Property(e => e.EndReason)
                .HasMaxLength(64)
                .HasColumnName("end_reason");
            entity.Property(e => e.EndedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ended_at");
            entity.Property(e => e.StartedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .HasDefaultValueSql("'active'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.AttackerRegions).WithMany(p => p.RegionWarAttackerRegions)
                .HasForeignKey(d => d.AttackerRegionsId)
                .HasConstraintName("region_wars_attacker_regions_id_fkey");

            entity.HasOne(d => d.ConqueredFromRegions).WithMany(p => p.RegionWarConqueredFromRegions)
                .HasForeignKey(d => d.ConqueredFromRegionsId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("region_wars_conquered_from_regions_id_fkey");

            entity.HasOne(d => d.DeclaredByPlayers).WithMany(p => p.RegionWars)
                .HasForeignKey(d => d.DeclaredByPlayersId)
                .HasConstraintName("region_wars_declared_by_players_id_fkey");

            entity.HasOne(d => d.DefenderRegions).WithMany(p => p.RegionWarDefenderRegions)
                .HasForeignKey(d => d.DefenderRegionsId)
                .HasConstraintName("region_wars_defender_regions_id_fkey");
        });

        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasKey(e => e.TradesId).HasName("trades_pkey");

            entity.ToTable("trades");

            entity.HasIndex(e => e.ProductsId, "trades_idx_products_id");

            entity.HasIndex(e => e.Status, "trades_idx_status");

            entity.Property(e => e.TradesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("trades_id");
            entity.Property(e => e.CountryTaxPaid)
                .HasPrecision(18, 2)
                .HasDefaultValue(0.00m)
                .HasColumnName("country_tax_paid");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.FromPlayersId).HasColumnName("from_players_id");
            entity.Property(e => e.PricePerUnit)
                .HasPrecision(18, 2)
                .HasColumnName("price_per_unit");
            entity.Property(e => e.ProductsId).HasColumnName("products_id");
            entity.Property(e => e.ResourceCode)
                .HasMaxLength(16)
                .HasColumnName("resource_code");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.FactionTaxPaid)
                .HasPrecision(18, 2)
                .HasDefaultValue(0.00m)
                .HasColumnName("faction_tax_paid");
            entity.Property(e => e.Status)
                .HasMaxLength(250)
                .HasColumnName("status");
            entity.Property(e => e.ToPlayersId).HasColumnName("to_players_id");
            entity.Property(e => e.TotalPrice)
                .HasPrecision(18, 2)
                .HasColumnName("total_price");

            entity.HasOne(d => d.FromPlayers).WithMany(p => p.TradeFromPlayers)
                .HasForeignKey(d => d.FromPlayersId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("trades_from_players_id_fkey");

            entity.HasOne(d => d.Products).WithMany(p => p.Trades)
                .HasForeignKey(d => d.ProductsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("trades_products_id_fkey");

            entity.HasOne(d => d.ToPlayers).WithMany(p => p.TradeToPlayers)
                .HasForeignKey(d => d.ToPlayersId)
                .HasConstraintName("trades_to_players_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UsersId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Email, "users_idx_email");

            entity.HasIndex(e => e.IsActive, "users_idx_is_active");

            entity.HasIndex(e => e.LastLogin, "users_idx_last_login");

            entity.Property(e => e.UsersId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("users_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(250)
                .HasColumnName("email");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LastLogin)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_login");
            entity.Property(e => e.Password)
                .HasMaxLength(250)
                .HasColumnName("password");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
