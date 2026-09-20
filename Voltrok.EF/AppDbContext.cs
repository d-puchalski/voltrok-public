using Microsoft.EntityFrameworkCore;

namespace VoltrokEF;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BackgroundWorkerLastRun> BackgroundWorkerLastRuns { get; set; }

    public virtual DbSet<ChatCountryMessage> ChatCountryMessages { get; set; }

    public virtual DbSet<ChatGlobalMessage> ChatGlobalMessages { get; set; }

    public virtual DbSet<ChatPrivateMessage> ChatPrivateMessages { get; set; }

    public virtual DbSet<Country> Countries { get; set; }

    public virtual DbSet<CountryBuilding> CountryBuildings { get; set; }

    public virtual DbSet<CountryTradePolicy> CountryTradePolicies { get; set; }

    public virtual DbSet<CountryWar> CountryWars { get; set; }

    public virtual DbSet<Player> Players { get; set; }

    public virtual DbSet<PlayerBadge> PlayerBadges { get; set; }

    public virtual DbSet<PlayerBattleReport> PlayerBattleReports { get; set; }

    public virtual DbSet<PlayerBattleReportUnit> PlayerBattleReportUnits { get; set; }

    public virtual DbSet<PlayerDailyStat> PlayerDailyStats { get; set; }

    public virtual DbSet<PlayerMilitaryProduction> PlayerMilitaryProductions { get; set; }

    public virtual DbSet<PlayerMilitaryTransport> PlayerMilitaryTransports { get; set; }

    public virtual DbSet<PlayerMilitaryTransportUnit> PlayerMilitaryTransportUnits { get; set; }

    public virtual DbSet<PlayerMilitaryUnit> PlayerMilitaryUnits { get; set; }

    public virtual DbSet<PlayerNotification> PlayerNotifications { get; set; }

    public virtual DbSet<PlayerSiege> PlayerSieges { get; set; }

    public virtual DbSet<PlayerSiegeUnit> PlayerSiegeUnits { get; set; }

    public virtual DbSet<PlayerTrade> PlayerTrades { get; set; }

    public virtual DbSet<GameSeason> GameSeasons { get; set; }

    public virtual DbSet<GameSeasonLeaderboardEntry> GameSeasonLeaderboardEntries { get; set; }

    public virtual DbSet<PremiumPaymentEvent> PremiumPaymentEvents { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserLoginHistory> UserLoginHistories { get; set; }

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

        modelBuilder.Entity<ChatCountryMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("chat_country_messages_pkey");

            entity.ToTable("chat_country_messages");

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

        modelBuilder.Entity<Country>(entity =>
        {
            entity.HasKey(e => e.CountriesId).HasName("countries_pkey");

            entity.ToTable("countries");

            entity.HasIndex(e => e.Name, "countries_name_key").IsUnique();

            entity.Property(e => e.CountriesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("countries_id");
            entity.Property(e => e.Chips)
                .HasPrecision(18, 2)
                .HasColumnName("chips");
            entity.Property(e => e.ChipsTier)
                .HasDefaultValue((short)1)
                .HasColumnName("chips_tier");
            entity.Property(e => e.ColorHex)
                .HasMaxLength(7)
                .HasDefaultValueSql("'#3498db'::character varying")
                .HasColumnName("color_hex");
            entity.Property(e => e.Flag)
                .HasMaxLength(10)
                .HasColumnName("flag");
            entity.Property(e => e.IsAllowToAttackInsideCountry).HasColumnName("is_allow_to_attack_inside_country");
            entity.Property(e => e.IsAllowToAttackWithoutWarDeclaration).HasColumnName("is_allow_to_attack_without_war_declaration");
            entity.Property(e => e.IsoCode2)
                .HasMaxLength(10)
                .HasColumnName("iso_code_2");
            entity.Property(e => e.Money)
                .HasPrecision(18, 2)
                .HasColumnName("money");
            entity.Property(e => e.Name)
                .HasMaxLength(250)
                .HasColumnName("name");
            entity.Property(e => e.Oil)
                .HasPrecision(18, 2)
                .HasColumnName("oil");
            entity.Property(e => e.OilTier)
                .HasDefaultValue((short)1)
                .HasColumnName("oil_tier");
            entity.Property(e => e.PresidentPlayersId).HasColumnName("president_players_id");
            entity.Property(e => e.TaxPercent)
                .HasPrecision(5, 2)
                .HasColumnName("tax_percent");
            entity.Property(e => e.Uranium)
                .HasPrecision(18, 2)
                .HasColumnName("uranium");
            entity.Property(e => e.UraniumTier)
                .HasDefaultValue((short)1)
                .HasColumnName("uranium_tier");
        });

        modelBuilder.Entity<CountryBuilding>(entity =>
        {
            entity.HasKey(e => e.CountryBuildingsId).HasName("country_buildings_pkey");

            entity.ToTable("country_buildings");

            entity.Property(e => e.CountryBuildingsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("country_buildings_id");
            entity.Property(e => e.BuildingCode)
                .HasMaxLength(250)
                .HasColumnName("building_code");
            entity.Property(e => e.CountriesId).HasColumnName("countries_id");
            entity.Property(e => e.EndedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ended_at");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .HasDefaultValueSql("'hold'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.Countries).WithMany(p => p.CountryBuildings)
                .HasForeignKey(d => d.CountriesId)
                .HasConstraintName("country_buildings_countries_id_fkey");
        });

        modelBuilder.Entity<CountryTradePolicy>(entity =>
        {
            entity.HasKey(e => e.CountryTradePoliciesId).HasName("country_trade_policies_pkey");

            entity.ToTable("country_trade_policies");

            entity.HasIndex(e => new { e.SourceCountriesId, e.TargetCountriesId }, "country_trade_policies_uq_pair").IsUnique();

            entity.Property(e => e.CountryTradePoliciesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("country_trade_policies_id");
            entity.Property(e => e.IsEmbargo).HasColumnName("is_embargo");
            entity.Property(e => e.SourceCountriesId).HasColumnName("source_countries_id");
            entity.Property(e => e.TargetCountriesId).HasColumnName("target_countries_id");
            entity.Property(e => e.TariffPercent)
                .HasPrecision(5, 2)
                .HasColumnName("tariff_percent");

            entity.HasOne(d => d.SourceCountries).WithMany(p => p.CountryTradePolicySourceCountries)
                .HasForeignKey(d => d.SourceCountriesId)
                .HasConstraintName("country_trade_policies_source_countries_id_fkey");

            entity.HasOne(d => d.TargetCountries).WithMany(p => p.CountryTradePolicyTargetCountries)
                .HasForeignKey(d => d.TargetCountriesId)
                .HasConstraintName("country_trade_policies_target_countries_id_fkey");
        });

        modelBuilder.Entity<CountryWar>(entity =>
        {
            entity.HasKey(e => e.CountryWarsId).HasName("country_wars_pkey");

            entity.ToTable("country_wars");

            entity.HasIndex(e => new { e.CountryFromId, e.CountryToId }, "country_wars_country_from_id_country_to_id_key").IsUnique();

            entity.Property(e => e.CountryWarsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("country_wars_id");
            entity.Property(e => e.CountryFromId).HasColumnName("country_from_id");
            entity.Property(e => e.CountryToId).HasColumnName("country_to_id");
            entity.Property(e => e.EndedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ended_at");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .HasDefaultValueSql("'active'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.CountryFrom).WithMany(p => p.CountryWarCountryFroms)
                .HasForeignKey(d => d.CountryFromId)
                .HasConstraintName("country_wars_country_from_id_fkey");

            entity.HasOne(d => d.CountryTo).WithMany(p => p.CountryWarCountryTos)
                .HasForeignKey(d => d.CountryToId)
                .HasConstraintName("country_wars_country_to_id_fkey");
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.PlayersId).HasName("players_pkey");

            entity.ToTable("players");

            entity.HasIndex(e => e.UsersId, "players_unique_user_id").IsUnique();

            entity.Property(e => e.PlayersId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("players_id");
            entity.Property(e => e.Chips)
                .HasPrecision(18, 2)
                .HasColumnName("chips");
            entity.Property(e => e.ChipsTier)
                .HasDefaultValue((short)1)
                .HasColumnName("chips_tier");
            entity.Property(e => e.CountriesId).HasColumnName("countries_id");
            entity.Property(e => e.HasRelocatedRegion).HasColumnName("has_relocated_region");
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
            entity.Property(e => e.Name)
                .HasMaxLength(250)
                .HasColumnName("name");
            entity.Property(e => e.Oil)
                .HasPrecision(18, 2)
                .HasColumnName("oil");
            entity.Property(e => e.OilTier)
                .HasDefaultValue((short)1)
                .HasColumnName("oil_tier");
            entity.Property(e => e.PlayerAvatar)
                .HasMaxLength(500)
                .HasColumnName("player_avatar");
            entity.Property(e => e.Score)
                .HasPrecision(18, 2)
                .HasColumnName("score");
            entity.Property(e => e.BattlePassValidTill)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("battle_pass_valid_till");
            entity.Property(e => e.ShieldValidTill)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("shield_valid_till");
            entity.Property(e => e.BattlePassTrialGrantedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("battle_pass_trial_granted_at");
            entity.Property(e => e.Uranium)
                .HasPrecision(18, 2)
                .HasColumnName("uranium");
            entity.Property(e => e.UraniumTier)
                .HasDefaultValue((short)1)
                .HasColumnName("uranium_tier");
            entity.Property(e => e.UsersId).HasColumnName("users_id");

            entity.HasOne(d => d.Countries).WithMany(p => p.Players)
                .HasForeignKey(d => d.CountriesId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("players_countries_id_fkey");

            entity.HasOne(d => d.Users).WithOne(p => p.Player)
                .HasForeignKey<Player>(d => d.UsersId)
                .HasConstraintName("players_users_id_fkey");
        });

        modelBuilder.Entity<PlayerBadge>(entity =>
        {
            entity.HasKey(e => e.PlayerBadgesId).HasName("player_badges_pkey");

            entity.ToTable("player_badges");

            entity.Property(e => e.PlayerBadgesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_badges_id");
            entity.Property(e => e.AwardedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("awarded_at");
            entity.Property(e => e.BadgeCode)
                .HasMaxLength(250)
                .HasColumnName("badge_code");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerBadges)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_badges_players_id_fkey");
        });

        modelBuilder.Entity<PlayerBattleReport>(entity =>
        {
            entity.HasKey(e => e.PlayerBattleReportsId).HasName("player_battle_reports_pkey");

            entity.ToTable("player_battle_reports");

            entity.Property(e => e.PlayerBattleReportsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_battle_reports_id");
            entity.Property(e => e.ArchivedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("archived_at");
            entity.Property(e => e.AttackerLosses).HasColumnName("attacker_losses");
            entity.Property(e => e.AttackerPower).HasColumnName("attacker_power");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DefenderLosses).HasColumnName("defender_losses");
            entity.Property(e => e.DefenderPower).HasColumnName("defender_power");
            entity.Property(e => e.EndedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ended_at");
            entity.Property(e => e.IsArchived).HasColumnName("is_archived");
            entity.Property(e => e.LootChips)
                .HasPrecision(18, 2)
                .HasColumnName("loot_chips");
            entity.Property(e => e.LootMoney)
                .HasPrecision(18, 2)
                .HasColumnName("loot_money");
            entity.Property(e => e.LootOil)
                .HasPrecision(18, 2)
                .HasColumnName("loot_oil");
            entity.Property(e => e.LootUranium)
                .HasPrecision(18, 2)
                .HasColumnName("loot_uranium");
            entity.Property(e => e.PlayerFromId).HasColumnName("player_from_id");
            entity.Property(e => e.PlayerToId).HasColumnName("player_to_id");
            entity.Property(e => e.Result)
                .HasMaxLength(250)
                .HasColumnName("result");
            entity.Property(e => e.StartedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");

            entity.HasOne(d => d.PlayerFrom).WithMany(p => p.PlayerBattleReportPlayerFroms)
                .HasForeignKey(d => d.PlayerFromId)
                .HasConstraintName("player_battle_reports_player_from_id_fkey");

            entity.HasOne(d => d.PlayerTo).WithMany(p => p.PlayerBattleReportPlayerTos)
                .HasForeignKey(d => d.PlayerToId)
                .HasConstraintName("player_battle_reports_player_to_id_fkey");
        });

        modelBuilder.Entity<PlayerBattleReportUnit>(entity =>
        {
            entity.HasKey(e => e.PlayerBattleReportUnitsId).HasName("player_battle_report_units_pkey");

            entity.ToTable("player_battle_report_units");

            entity.Property(e => e.PlayerBattleReportUnitsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_battle_report_units_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.LostQuantity).HasColumnName("lost_quantity");
            entity.Property(e => e.MilitaryUnitsCode)
                .HasMaxLength(250)
                .HasColumnName("military_units_code");
            entity.Property(e => e.PlayerBattleReportsId).HasColumnName("player_battle_reports_id");
            entity.Property(e => e.RemainingQuantity).HasColumnName("remaining_quantity");
            entity.Property(e => e.Side)
                .HasMaxLength(32)
                .HasColumnName("side");
            entity.Property(e => e.StartingQuantity).HasColumnName("starting_quantity");

            entity.HasOne(d => d.PlayerBattleReports).WithMany(p => p.PlayerBattleReportUnits)
                .HasForeignKey(d => d.PlayerBattleReportsId)
                .HasConstraintName("player_battle_report_units_player_battle_reports_id_fkey");
        });

        modelBuilder.Entity<PlayerDailyStat>(entity =>
        {
            entity.HasKey(e => e.PlayerDailyStatsId).HasName("player_daily_stats_pkey");

            entity.ToTable("player_daily_stats");

            entity.HasIndex(e => new { e.PlayersId, e.StatsDate }, "player_daily_stats_uq_player_daily_stats_player_date").IsUnique();

            entity.Property(e => e.PlayerDailyStatsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_daily_stats_id");
            entity.Property(e => e.ArmyTotal).HasColumnName("army_total");
            entity.Property(e => e.Chips)
                .HasPrecision(18, 2)
                .HasColumnName("chips");
            entity.Property(e => e.Money)
                .HasPrecision(18, 2)
                .HasColumnName("money");
            entity.Property(e => e.Oil)
                .HasPrecision(18, 2)
                .HasColumnName("oil");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.Score)
                .HasPrecision(18, 2)
                .HasColumnName("score");
            entity.Property(e => e.StatsDate).HasColumnName("stats_date");
            entity.Property(e => e.Uranium)
                .HasPrecision(18, 2)
                .HasColumnName("uranium");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerDailyStats)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_daily_stats_players_id_fkey");
        });

        modelBuilder.Entity<PlayerMilitaryProduction>(entity =>
        {
            entity.HasKey(e => e.PlayerMilitaryOrdersId).HasName("player_military_production_pkey");

            entity.ToTable("player_military_production");

            entity.Property(e => e.PlayerMilitaryOrdersId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_military_orders_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.MilitaryUnitsCode)
                .HasMaxLength(250)
                .HasColumnName("military_units_code");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.ProducedQuantity).HasColumnName("produced_quantity");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerMilitaryProductions)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_military_production_players_id_fkey");
        });

        modelBuilder.Entity<PlayerMilitaryTransport>(entity =>
        {
            entity.HasKey(e => e.PlayerMilitaryTransportsId).HasName("player_military_transports_pkey");

            entity.ToTable("player_military_transports");

            entity.Property(e => e.PlayerMilitaryTransportsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_military_transports_id");
            entity.Property(e => e.ArchivedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("archived_at");
            entity.Property(e => e.AutoReturnAfterBattle).HasColumnName("auto_return_after_battle");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.EndTime)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("end_time");
            entity.Property(e => e.IsArchived).HasColumnName("is_archived");
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

            entity.Property(e => e.PlayerMilitaryTransportUnitsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_military_transport_units_id");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.MilitaryUnitsCode)
                .HasMaxLength(250)
                .HasColumnName("military_units_code");
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

            entity.Property(e => e.PlayerMilitaryUnitsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_military_units_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.MilitaryUnitsCode)
                .HasMaxLength(250)
                .HasColumnName("military_units_code");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerMilitaryUnits)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_military_units_players_id_fkey");
        });

        modelBuilder.Entity<PlayerNotification>(entity =>
        {
            entity.HasKey(e => e.PlayerNotificationsId).HasName("player_notifications_pkey");

            entity.ToTable("player_notifications");

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
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.PopupDeliveredAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("popup_delivered_at");
            entity.Property(e => e.ReadAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("read_at");
            entity.Property(e => e.Type)
                .HasMaxLength(64)
                .HasColumnName("type");

            entity.HasOne(d => d.Players).WithMany(p => p.PlayerNotifications)
                .HasForeignKey(d => d.PlayersId)
                .HasConstraintName("player_notifications_players_id_fkey");
        });

        modelBuilder.Entity<PlayerSiege>(entity =>
        {
            entity.HasKey(e => e.PlayerSiegesId).HasName("player_sieges_pkey");

            entity.ToTable("player_sieges");

            entity.Property(e => e.PlayerSiegesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_sieges_id");
            entity.Property(e => e.ArchivedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("archived_at");
            entity.Property(e => e.EndedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ended_at");
            entity.Property(e => e.IsArchived).HasColumnName("is_archived");
            entity.Property(e => e.LastTickAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_tick_at");
            entity.Property(e => e.LootChips)
                .HasPrecision(18, 2)
                .HasColumnName("loot_chips");
            entity.Property(e => e.LootMoney)
                .HasPrecision(18, 2)
                .HasColumnName("loot_money");
            entity.Property(e => e.LootOil)
                .HasPrecision(18, 2)
                .HasColumnName("loot_oil");
            entity.Property(e => e.LootUranium)
                .HasPrecision(18, 2)
                .HasColumnName("loot_uranium");
            entity.Property(e => e.PlayerFromId).HasColumnName("player_from_id");
            entity.Property(e => e.PlayerToId).HasColumnName("player_to_id");
            entity.Property(e => e.StartedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(250)
                .HasColumnName("status");

            entity.HasOne(d => d.PlayerFrom).WithMany(p => p.PlayerSiegePlayerFroms)
                .HasForeignKey(d => d.PlayerFromId)
                .HasConstraintName("player_sieges_player_from_id_fkey");

            entity.HasOne(d => d.PlayerTo).WithMany(p => p.PlayerSiegePlayerTos)
                .HasForeignKey(d => d.PlayerToId)
                .HasConstraintName("player_sieges_player_to_id_fkey");
        });

        modelBuilder.Entity<PlayerSiegeUnit>(entity =>
        {
            entity.HasKey(e => e.PlayerSiegeUnitsId).HasName("player_siege_units_pkey");

            entity.ToTable("player_siege_units");

            entity.Property(e => e.PlayerSiegeUnitsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_siege_units_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CurrentQuantity).HasColumnName("current_quantity");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.LostQuantity).HasColumnName("lost_quantity");
            entity.Property(e => e.MilitaryUnitsCode)
                .HasMaxLength(250)
                .HasColumnName("military_units_code");
            entity.Property(e => e.PlayerSiegesId).HasColumnName("player_sieges_id");
            entity.Property(e => e.Side)
                .HasMaxLength(32)
                .HasColumnName("side");
            entity.Property(e => e.StartingQuantity).HasColumnName("starting_quantity");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.PlayerSieges).WithMany(p => p.PlayerSiegeUnits)
                .HasForeignKey(d => d.PlayerSiegesId)
                .HasConstraintName("player_siege_units_player_sieges_id_fkey");
        });

        modelBuilder.Entity<PlayerTrade>(entity =>
        {
            entity.HasKey(e => e.PlayerTradesId).HasName("player_trades_pkey");

            entity.ToTable("player_trades");

            entity.Property(e => e.PlayerTradesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("player_trades_id");
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
            entity.Property(e => e.Quantity)
                .HasPrecision(18, 2)
                .HasColumnName("quantity");
            entity.Property(e => e.ResourceCode)
                .HasMaxLength(16)
                .HasColumnName("resource_code");
            entity.Property(e => e.Status)
                .HasMaxLength(250)
                .HasColumnName("status");
            entity.Property(e => e.ToPlayersId).HasColumnName("to_players_id");
            entity.Property(e => e.TotalPrice)
                .HasPrecision(18, 2)
                .HasColumnName("total_price");
            entity.Property(e => e.TransportEndTime)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("transport_end_time");
            entity.Property(e => e.TransportStartTime)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("transport_start_time");

            entity.HasOne(d => d.FromPlayers).WithMany(p => p.PlayerTradeFromPlayers)
                .HasForeignKey(d => d.FromPlayersId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("player_trades_from_players_id_fkey");

            entity.HasOne(d => d.ToPlayers).WithMany(p => p.PlayerTradeToPlayers)
                .HasForeignKey(d => d.ToPlayersId)
                .HasConstraintName("player_trades_to_players_id_fkey");
        });

        modelBuilder.Entity<PremiumPaymentEvent>(entity =>
        {
            entity.HasKey(e => e.PremiumPaymentEventsId).HasName("premium_payment_events_pkey");

            entity.ToTable("premium_payment_events");

            entity.HasIndex(e => e.StripeEventId, "premium_payment_events_stripe_event_id_key").IsUnique();

            entity.Property(e => e.PremiumPaymentEventsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("premium_payment_events_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.EventType)
                .HasMaxLength(120)
                .HasColumnName("event_type");
            entity.Property(e => e.PayloadJson).HasColumnName("payload_json");
            entity.Property(e => e.ProcessedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("processed_at");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .HasColumnName("status");
            entity.Property(e => e.StripeEventId)
                .HasMaxLength(255)
                .HasColumnName("stripe_event_id");
            entity.Property(e => e.StripeObjectId)
                .HasMaxLength(255)
                .HasColumnName("stripe_object_id");
            entity.Property(e => e.UsersId).HasColumnName("users_id");

            entity.HasOne(d => d.Users).WithMany(p => p.PremiumPaymentEvents)
                .HasForeignKey(d => d.UsersId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("premium_payment_events_users_id_fkey");
        });

        modelBuilder.Entity<GameSeason>(entity =>
        {
            entity.HasKey(e => e.GameSeasonsId).HasName("game_seasons_pkey");

            entity.ToTable("game_seasons");

            entity.HasIndex(e => e.SeasonNumber, "game_seasons_season_number_key").IsUnique();

            entity.Property(e => e.GameSeasonsId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("game_seasons_id");
            entity.Property(e => e.ClosedAtUtc)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("closed_at_utc");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.EndsAtUtc)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ends_at_utc");
            entity.Property(e => e.SeasonNumber).HasColumnName("season_number");
            entity.Property(e => e.StartsAtUtc)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("starts_at_utc");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<GameSeasonLeaderboardEntry>(entity =>
        {
            entity.HasKey(e => e.GameSeasonLeaderboardEntriesId).HasName("game_season_leaderboard_entries_pkey");

            entity.ToTable("game_season_leaderboard_entries");

            entity.HasIndex(e => new { e.GameSeasonsId, e.EntryKind, e.Position }, "game_season_leaderboard_entries_uq_season_kind_position").IsUnique();
            entity.HasIndex(e => new { e.GameSeasonsId, e.EntryKind, e.PlayersId }, "game_season_leaderboard_entries_uq_season_kind_player").IsUnique();
            entity.HasIndex(e => new { e.GameSeasonsId, e.EntryKind, e.CountriesId }, "game_season_leaderboard_entries_uq_season_kind_country").IsUnique();

            entity.Property(e => e.GameSeasonLeaderboardEntriesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("game_season_leaderboard_entries_id");
            entity.Property(e => e.CountriesId).HasColumnName("countries_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.EntryKind)
                .HasMaxLength(32)
                .HasColumnName("entry_kind");
            entity.Property(e => e.EntryName)
                .HasMaxLength(250)
                .HasColumnName("entry_name");
            entity.Property(e => e.GameSeasonsId).HasColumnName("game_seasons_id");
            entity.Property(e => e.PlayersId).HasColumnName("players_id");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.PlayerMoney)
                .HasPrecision(18, 2)
                .HasColumnName("player_money");
            entity.Property(e => e.PlayerOil)
                .HasPrecision(18, 2)
                .HasColumnName("player_oil");
            entity.Property(e => e.PlayerUranium)
                .HasPrecision(18, 2)
                .HasColumnName("player_uranium");
            entity.Property(e => e.PlayerChips)
                .HasPrecision(18, 2)
                .HasColumnName("player_chips");
            entity.Property(e => e.CountryMoney)
                .HasPrecision(18, 2)
                .HasColumnName("country_money");
            entity.Property(e => e.CountryOil)
                .HasPrecision(18, 2)
                .HasColumnName("country_oil");
            entity.Property(e => e.CountryUranium)
                .HasPrecision(18, 2)
                .HasColumnName("country_uranium");
            entity.Property(e => e.CountryChips)
                .HasPrecision(18, 2)
                .HasColumnName("country_chips");

            entity.HasOne(d => d.GameSeason).WithMany()
                .HasForeignKey(d => d.GameSeasonsId)
                .HasConstraintName("game_season_leaderboard_entries_game_seasons_id_fkey");

            entity.HasOne(d => d.Countries).WithMany()
                .HasForeignKey(d => d.CountriesId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("game_season_leaderboard_entries_countries_id_fkey");

            entity.HasOne(d => d.Players).WithMany()
                .HasForeignKey(d => d.PlayersId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("game_season_leaderboard_entries_players_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UsersId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.GoogleSubjectId, "users_google_subject_id_key").IsUnique();

            entity.HasIndex(e => e.StripeCustomerId, "users_stripe_customer_id_key").IsUnique();

            entity.Property(e => e.UsersId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("users_id");
            entity.Property(e => e.AuthProvider)
                .HasMaxLength(32)
                .HasDefaultValueSql("'local'::character varying")
                .HasColumnName("auth_provider");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(250)
                .HasColumnName("email");
            entity.Property(e => e.GoogleEmailVerified).HasColumnName("google_email_verified");
            entity.Property(e => e.GoogleLinkedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("google_linked_at");
            entity.Property(e => e.GoogleSubjectId)
                .HasMaxLength(255)
                .HasColumnName("google_subject_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LastLogin)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_login");
            entity.Property(e => e.Password)
                .HasMaxLength(250)
                .HasColumnName("password");
            entity.Property(e => e.StripeCustomerId)
                .HasMaxLength(255)
                .HasColumnName("stripe_customer_id");
        });

        modelBuilder.Entity<UserLoginHistory>(entity =>
        {
            entity.HasKey(e => e.UserLoginHistoriesId).HasName("user_login_histories_pkey");

            entity.ToTable("user_login_histories");

            entity.Property(e => e.UserLoginHistoriesId)
                .HasDefaultValueSql("uuidv7()")
                .HasColumnName("user_login_histories_id");
            entity.Property(e => e.AttemptedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("attempted_at");
            entity.Property(e => e.FailureReason)
                .HasMaxLength(250)
                .HasColumnName("failure_reason");
            entity.Property(e => e.IsSuccessful).HasColumnName("is_successful");
            entity.Property(e => e.UsersId).HasColumnName("users_id");

            entity.HasOne(d => d.Users).WithMany(p => p.UserLoginHistories)
                .HasForeignKey(d => d.UsersId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("user_login_histories_users_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
