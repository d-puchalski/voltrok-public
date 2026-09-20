using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using VoltrokEF;
using VoltrokServices.Services.Notifications;
using VoltrokUtils;
using VoltrokUtils.Engines;
using VoltrokUtils.Enums;

namespace VoltrokWorker.BackgroundWorker;

public sealed class MilitaryCombatSettlementWorker(IDbContextFactory<AppDbContext> dbContextFactory, IConfiguration configuration, NotificationService notificationService) : BackgroundService
{
    private static readonly IReadOnlyDictionary<string, MilitaryUnit> CombatUnitDefinitions = MilitaryUnitEngine.MilitaryUnitsAllData.ToDictionary(unit => unit.Code.ToString());
    private const decimal AttackerWarVictorySiegeThresholdPercent = 80m;
    private const decimal DefenderWarCancelSiegeThresholdPercent = 50m;
    private const decimal ScoreReductionFactor = 20m;
    private const decimal AttackerLootScoreMultiplier = 5m / ScoreReductionFactor;
    private const decimal DefenderLootScorePenaltyMultiplier = 3m / ScoreReductionFactor;

    private int CombatIntervalSeconds => Math.Max(5, configuration.GetValue("BackgroundWorker:PlayerCombatBreakSeconds", 10));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Military combat settlement worker started. Interval: {CombatIntervalSeconds}s.", CombatIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCombatAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while settling military combat.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(CombatIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessCombatAsync(CancellationToken cancellationToken)
    {
        await ResolveRecoveredTransportsAsync(cancellationToken);
        await ResolveCompletedTransportsAsync(cancellationToken);
        await ResolveCompletedSiegesAsync(cancellationToken);
        await ResolveCountryWarsAsync(cancellationToken);
    }

    private async Task ResolveCountryWarsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var activeWars = await dbContext.CountryWars
            .Where(war => war.Status == CountryWarStatus.Active.ToDbValue())
            .ToListAsync(cancellationToken);

        if (activeWars.Count == 0)
        {
            return;
        }

        foreach (var war in activeWars)
        {
            var attackerPlayerIds = await dbContext.Players
                .Where(player => player.CountriesId == war.CountryFromId)
                .Select(player => player.PlayersId)
                .ToListAsync(cancellationToken);

            var defenderPlayerIds = await dbContext.Players
                .Where(player => player.CountriesId == war.CountryToId)
                .Select(player => player.PlayersId)
                .ToListAsync(cancellationToken);

            var attackerTotalPlayers = attackerPlayerIds.Count;
            var defenderTotalPlayers = defenderPlayerIds.Count;

            var activeSieges = await dbContext.PlayerSieges
                .Where(siege => siege.Status == PlayerSiegesStatus.Active.ToDbValue())
                .Select(siege => new
                {
                    siege.PlayerFromId,
                    siege.PlayerToId
                })
                .ToListAsync(cancellationToken);

            var attackerActiveSieges = activeSieges
                .Where(siege => attackerPlayerIds.Contains(siege.PlayerFromId) && defenderPlayerIds.Contains(siege.PlayerToId))
                .Select(siege => siege.PlayerToId)
                .Distinct()
                .Count();

            var defenderActiveSieges = activeSieges
                .Where(siege => defenderPlayerIds.Contains(siege.PlayerFromId) && attackerPlayerIds.Contains(siege.PlayerToId))
                .Select(siege => siege.PlayerToId)
                .Distinct()
                .Count();

            var attackerProgressPercent = defenderTotalPlayers > 0
                ? (attackerActiveSieges * 100m) / defenderTotalPlayers
                : 0m;
            var defenderProgressPercent = attackerTotalPlayers > 0
                ? (defenderActiveSieges * 100m) / attackerTotalPlayers
                : 0m;

            if (attackerProgressPercent >= AttackerWarVictorySiegeThresholdPercent)
            {
                await ConquerCountryWarAsync(dbContext, war, now, cancellationToken);
                continue;
            }

            if (defenderProgressPercent >= DefenderWarCancelSiegeThresholdPercent)
            {
                await CancelCountryWarAsDefendedAsync(dbContext, war, now, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveRecoveredTransportsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var transports = await dbContext.PlayerMilitaryTransports
            .Include(t => t.PlayerMilitaryTransportUnits)
            .Where(t => t.Status == PlayerTransportStatus.Cancelled.ToDbValue() || t.Status == PlayerTransportStatus.Returning.ToDbValue())
            .ToListAsync(cancellationToken);

        if (transports.Count == 0)
        {
            return;
        }

        foreach (var transport in transports)
        {
            await ReturnSurvivorsAsync(
                dbContext,
                transport.PlayerFromId,
                transport.PlayerMilitaryTransportUnits,
                cancellationToken);

            transport.Status = PlayerTransportStatus.Completed.ToDbValue();
            transport.EndTime = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveCompletedTransportsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var transports = await dbContext.PlayerMilitaryTransports
            .Include(t => t.PlayerMilitaryTransportUnits)
            .Where(t => t.Status == PlayerTransportStatus.InProgress.ToDbValue() && t.EndTime <= now)
            .ToListAsync(cancellationToken);

        if (transports.Count == 0)
        {
            return;
        }

        foreach (var transport in transports)
        {
            var missionType = transport.MissionType;
            switch (missionType)
            {
                case nameof(PlayerMilitaryTransportMissionType.Attack) or nameof(PlayerMilitaryTransportMissionType.Siege):
                    {
        var lootRate = missionType == PlayerMilitaryTransportMissionType.Siege.ToDbValue() ? CombatBalanceEngine.SiegeInitialLootRate : CombatBalanceEngine.StandardAttackLootRate;
                        await ResolveBattleAsync(dbContext, transport, now, lootRate, cancellationToken);
                        break;
                    }
                case nameof(PlayerMilitaryTransportMissionType.Aid):
                    await ResolveAidAsync(dbContext, transport, now, cancellationToken);
                    transport.Status = PlayerTransportStatus.Completed.ToDbValue();
                    break;
                default:
                    transport.Status = PlayerTransportStatus.Completed.ToDbValue();
                    break;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveCompletedSiegesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var sieges = await dbContext.PlayerSieges
            .Include(s => s.PlayerSiegeUnits)
            .Where(s => s.Status == PlayerSiegesStatus.Active.ToDbValue())
            .ToListAsync(cancellationToken);

        if (sieges.Count == 0)
        {
            return;
        }

        foreach (var siege in sieges)
        {
            var defender = await dbContext.Players.FirstAsync(p => p.PlayersId == siege.PlayerToId, cancellationToken);
            var attacker = await dbContext.Players.FirstAsync(p => p.PlayersId == siege.PlayerFromId, cancellationToken);
            var attackerCountry = await dbContext.Countries.FirstAsync(c => c.CountriesId == attacker.CountriesId, cancellationToken);

            var siegeTransports = await dbContext.PlayerMilitaryTransports
                .Include(t => t.PlayerMilitaryTransportUnits)
                .Where(t => t.PlayerFromId == siege.PlayerFromId
                            && t.PlayerToId == siege.PlayerToId
                            && t.MissionType == PlayerMilitaryTransportMissionType.Siege.ToDbValue()
                            && t.Status == PlayerTransportStatus.InSiege.ToDbValue())
                .ToListAsync(cancellationToken);

            if (siegeTransports.Count > 0)
            {
                await ProcessSiegeTicksAsync(
                    dbContext,
                    siege,
                    attacker,
                    attackerCountry,
                    defender,
                    siegeTransports,
                    now,
                    cancellationToken);
            }

            var hasAttackerUnits = siegeTransports.Any(t => t.PlayerMilitaryTransportUnits.Any(u => u.Quantity > 0));
            var shouldFinish = !siege.EndedAt.HasValue
                               || siege.EndedAt.Value <= now
                               || siegeTransports.Count == 0
                               || !hasAttackerUnits;
            if (!shouldFinish)
            {
                continue;
            }

            foreach (var transport in siegeTransports)
            {
                await ReturnSurvivorsAsync(dbContext, attacker.PlayersId, transport.PlayerMilitaryTransportUnits, cancellationToken);
                transport.Status = PlayerTransportStatus.Completed.ToDbValue();
                transport.EndTime = now;
            }

            siege.Status = PlayerSiegesStatus.Completed.ToDbValue();
            siege.EndedAt = now;

            await CreateSiegeFinishedNotificationsAsync(attacker, defender, false, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveBattleAsync(AppDbContext dbContext, PlayerMilitaryTransport transport, DateTime now, decimal lootRate, 
        CancellationToken cancellationToken)
    {
        var attacker = await dbContext.Players.FirstAsync(p => p.PlayersId == transport.PlayerFromId, cancellationToken);
        var defender = await dbContext.Players.FirstAsync(p => p.PlayersId == transport.PlayerToId, cancellationToken);
        var attackerCountry = await dbContext.Countries.FirstAsync(c => c.CountriesId == attacker.CountriesId, cancellationToken);

        var attackerUnits = transport.PlayerMilitaryTransportUnits
            .Where(u => u.Quantity > 0)
            .OrderByDescending(u => u.Level)
            .ThenBy(u => u.MilitaryUnitsCode)
            .ToList();

        var defenderUnits = await dbContext.PlayerMilitaryUnits
            .Where(u => u.PlayersId == defender.PlayersId && u.Quantity > 0)
            .OrderByDescending(u => u.Level)
            .ThenBy(u => u.MilitaryUnitsCode)
            .ToListAsync(cancellationToken);

        var attackerStartingSnapshot = SnapshotUnits(attackerUnits);
        var defenderStartingSnapshot = SnapshotUnits(defenderUnits);

        var useSiegePower = string.Equals(transport.MissionType, PlayerMilitaryTransportMissionType.Siege.ToDbValue(), StringComparison.OrdinalIgnoreCase);
        var simulationResult = BattleSimulationEngine.SimulateBattle(
            attackerUnits.Select(unit => new BattleSimulationUnitInput(unit.MilitaryUnitsCode, unit.Level, unit.Quantity)),
            defenderUnits.Select(unit => new BattleSimulationUnitInput(unit.MilitaryUnitsCode, unit.Level, unit.Quantity)),
            useSiegePower);

        ApplySimulationLosses(attackerUnits, simulationResult.AttackerKilledUnits);
        ApplySimulationLosses(defenderUnits, simulationResult.DefenderKilledUnits);

        var attackerPower = simulationResult.AttackerPower;
        var defenderPower = simulationResult.DefenderPower;
        var attackerLosses = simulationResult.Attacker.KilledTotalUnits;
        var defenderLosses = simulationResult.Defender.KilledTotalUnits;

        var attackerSurvivors = attackerUnits.Sum(u => u.Quantity);
        var attackerWon = simulationResult.AttackerWon;
        var lootSummary = ResourceLootSummary.Empty;
        if (attackerWon && attackerSurvivors > 0)
        {
            lootSummary = LootResources(attacker, attackerCountry, defender, lootRate);
        }

        var battleReport = new PlayerBattleReport
        {
            PlayerBattleReportsId = Guid.NewGuid(),
            PlayerFromId = attacker.PlayersId,
            PlayerToId = defender.PlayersId,
            Result = $"{transport.MissionType}_{(attackerWon ? "attacker_win" : "defender_win")}",
            AttackerPower = (int)Math.Round(attackerPower, MidpointRounding.AwayFromZero),
            DefenderPower = (int)Math.Round(defenderPower, MidpointRounding.AwayFromZero),
            AttackerLosses = attackerLosses,
            DefenderLosses = defenderLosses,
            LootMoney = lootSummary.Money,
            LootOil = lootSummary.Oil,
            LootUranium = lootSummary.Uranium,
            LootChips = lootSummary.Chips,
            StartedAt = transport.StartTime,
            EndedAt = now,
            CreatedAt = now
        };

        dbContext.PlayerBattleReports.Add(battleReport);
        dbContext.PlayerBattleReportUnits.AddRange(BuildBattleReportUnits(
            battleReport.PlayerBattleReportsId,
            attackerStartingSnapshot,
            SnapshotUnits(attackerUnits),
            defenderStartingSnapshot,
            SnapshotUnits(defenderUnits),
            now));

        var shouldStartSiege = string.Equals(transport.MissionType, PlayerMilitaryTransportMissionType.Siege.ToDbValue(), StringComparison.OrdinalIgnoreCase)
                               && attackerWon
                               && attackerSurvivors > 0;
        var shouldStationAttack = string.Equals(transport.MissionType, PlayerMilitaryTransportMissionType.Attack.ToDbValue(), StringComparison.OrdinalIgnoreCase)
                                  && attackerWon
                                  && attackerSurvivors > 0
                                  && !transport.AutoReturnAfterBattle;

        if (shouldStartSiege)
        {
            transport.Status = PlayerTransportStatus.InSiege.ToDbValue();
            transport.EndTime = now.Add(CombatBalanceEngine.MaxSiegeDuration);
            await StartSiegeAsync(dbContext, attacker, defender, attackerUnits, defenderUnits, lootSummary, now, cancellationToken);
        }
        else if (shouldStationAttack)
        {
            transport.Status = PlayerTransportStatus.InSiege.ToDbValue();
            transport.EndTime = now.Add(CombatBalanceEngine.MaxSiegeDuration);
        }
        else
        {
            if (transport.AutoReturnAfterBattle && attackerSurvivors > 0)
            {
                await ReturnSurvivorsAsync(dbContext, attacker.PlayersId, attackerUnits, cancellationToken);
            }
            else
            {
                foreach (var unit in attackerUnits.Where(u => u.Quantity > 0))
                {
                    unit.Quantity = 0;
                }
            }

            transport.Status = PlayerTransportStatus.Completed.ToDbValue();
            transport.EndTime = now;
        }

        await CreateBattleNotificationsAsync(
            attacker,
            defender,
            transport.MissionType,
            attackerWon ? "attacker_win" : "defender_win",
            attackerLosses,
            defenderLosses,
            lootSummary,
            cancellationToken);
    }

    private async Task ResolveAidAsync(AppDbContext dbContext, PlayerMilitaryTransport transport, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var unit in transport.PlayerMilitaryTransportUnits.Where(u => u.Quantity > 0))
        {
            var destinationEntry = await dbContext.PlayerMilitaryUnits
                .FirstOrDefaultAsync(entry =>
                        entry.PlayersId == transport.PlayerToId
                        && entry.MilitaryUnitsCode == unit.MilitaryUnitsCode
                        && entry.Level == unit.Level,
                    cancellationToken);

            if (destinationEntry == null)
            {
                dbContext.PlayerMilitaryUnits.Add(new PlayerMilitaryUnit
                {
                    PlayerMilitaryUnitsId = Guid.NewGuid(),
                    PlayersId = transport.PlayerToId,
                    MilitaryUnitsCode = unit.MilitaryUnitsCode,
                    Level = unit.Level,
                    Quantity = unit.Quantity,
                    CreatedAt = now
                });
            }
            else
            {
                destinationEntry.Quantity += unit.Quantity;
            }
        }

        var fromPlayer = await dbContext.Players.AsNoTracking().FirstOrDefaultAsync(p => p.PlayersId == transport.PlayerFromId, cancellationToken);
        var toPlayer = await dbContext.Players.AsNoTracking().FirstOrDefaultAsync(p => p.PlayersId == transport.PlayerToId, cancellationToken);
        if (toPlayer != null)
        {
            await notificationService.CreateAsync(
                toPlayer.PlayersId,
                PlayerNotificationCode.IncomingAid,
                new PlayerNotificationDetails
                {
                    NotificationKind = "incoming_aid_arrived",
                    MissionType = PlayerMilitaryTransportMissionType.Aid.ToDbValue(),
                    AttackerPlayerId = transport.PlayerFromId,
                    DefenderPlayerId = transport.PlayerToId,
                    ArrivedAtUtc = now,
                    CounterpartyName = fromPlayer?.Name ?? "Unknown",
                    IsOutgoing = false
                },
                now,
                cancellationToken);
        }

        if (fromPlayer != null)
        {
            await notificationService.CreateAsync(
                fromPlayer.PlayersId,
                PlayerNotificationCode.IncomingAid,
                new PlayerNotificationDetails
                {
                    NotificationKind = "outgoing_aid_arrived",
                    MissionType = PlayerMilitaryTransportMissionType.Aid.ToDbValue(),
                    AttackerPlayerId = transport.PlayerFromId,
                    DefenderPlayerId = transport.PlayerToId,
                    ArrivedAtUtc = now,
                    CounterpartyName = toPlayer?.Name ?? "Unknown",
                    IsOutgoing = true
                },
                now,
                cancellationToken);
        }
    }

    private async Task StartSiegeAsync(AppDbContext dbContext, Player attacker, Player defender, IReadOnlyCollection<PlayerMilitaryTransportUnit> attackerUnits,
        IReadOnlyCollection<PlayerMilitaryUnit> defenderUnits, ResourceLootSummary initialLootSummary, DateTime now, CancellationToken cancellationToken)
    {
        var startedNewSiege = false;
        var activeSiege = await dbContext.PlayerSieges
            .Include(s => s.PlayerSiegeUnits)
            .FirstOrDefaultAsync(
                s => s.PlayerFromId == attacker.PlayersId
                     && s.PlayerToId == defender.PlayersId
                     && s.Status == PlayerSiegesStatus.Active.ToDbValue(),
                cancellationToken);

        if (activeSiege == null)
        {
            startedNewSiege = true;
            activeSiege = new PlayerSiege
            {
                PlayerSiegesId = Guid.NewGuid(),
                PlayerFromId = attacker.PlayersId,
                PlayerToId = defender.PlayersId,
                Status = PlayerSiegesStatus.Active.ToDbValue(),
                LootMoney = 0m,
                LootOil = 0m,
                LootUranium = 0m,
                LootChips = 0m,
                StartedAt = now,
                EndedAt = now.Add(CombatBalanceEngine.MaxSiegeDuration),
                LastTickAt = now
            };
            dbContext.PlayerSieges.Add(activeSiege);

            AppendSiegeUnitsForSide(activeSiege, Side.Attacker.ToDbValue(), SnapshotUnits(attackerUnits), now);
            AppendSiegeUnitsForSide(activeSiege, Side.Defender.ToDbValue(), SnapshotUnits(defenderUnits), now);
        }
        else
        {
            activeSiege.EndedAt = now.Add(CombatBalanceEngine.MaxSiegeDuration);
            AppendSiegeUnitsForSide(activeSiege, Side.Attacker.ToDbValue(), SnapshotUnits(attackerUnits), now);
            UpdateSiegeUnitState(activeSiege, Side.Defender.ToDbValue(), SnapshotUnits(defenderUnits), now);
        }

        activeSiege.LootMoney += initialLootSummary.Money;
        activeSiege.LootOil += initialLootSummary.Oil;
        activeSiege.LootUranium += initialLootSummary.Uranium;
        activeSiege.LootChips += initialLootSummary.Chips;

        if (startedNewSiege)
        {
            await notificationService.CreateAsync(
                defender.PlayersId,
                PlayerNotificationCode.SiegeStarted,
                new PlayerNotificationDetails
                {
                    NotificationKind = "siege_started",
                    MissionType = PlayerMilitaryTransportMissionType.Siege.ToDbValue(),
                    AttackerPlayerId = attacker.PlayersId,
                    DefenderPlayerId = defender.PlayersId,
                    StartedAtUtc = now,
                    CounterpartyName = attacker.Name
                },
                now,
                cancellationToken);
        }
    }

    private async Task CreateBattleNotificationsAsync(Player attacker, Player defender, string missionType, string result, int attackerLosses,
        int defenderLosses, ResourceLootSummary lootSummary, CancellationToken cancellationToken)
    {
        var attackerWon = string.Equals(result, "attacker_win", StringComparison.OrdinalIgnoreCase);
        var battleEndedAtUtc = DateTime.UtcNow;
        var winnerName = attackerWon ? attacker.Name : defender.Name;
        var loserName = attackerWon ? defender.Name : attacker.Name;
        var lootSummaryText = BuildLootSummaryText(lootSummary);

        await notificationService.CreateAsync(
            attacker.PlayersId,
            PlayerNotificationCode.BattleReportAttacker,
            new PlayerNotificationDetails
            {
                NotificationKind = attackerWon ? "battle_report_attacker_victory" : "battle_report_attacker_defeat",
                MissionType = missionType,
                Result = result,
                AttackerPlayerId = attacker.PlayersId,
                DefenderPlayerId = defender.PlayersId,
                AttackerPlayerName = attacker.Name,
                DefenderPlayerName = defender.Name,
                WinnerPlayerName = winnerName,
                LoserPlayerName = loserName,
                AttackerLosses = attackerLosses,
                DefenderLosses = defenderLosses,
                LootMoney = lootSummary.Money,
                LootOil = lootSummary.Oil,
                LootUranium = lootSummary.Uranium,
                LootChips = lootSummary.Chips,
                LootSummaryText = lootSummaryText,
                EndedAtUtc = battleEndedAtUtc
            },
            cancellationToken: cancellationToken);

        await notificationService.CreateAsync(
            defender.PlayersId,
            PlayerNotificationCode.BattleReportDefender,
            new PlayerNotificationDetails
            {
                NotificationKind = attackerWon ? "battle_report_defender_defeat" : "battle_report_defender_victory",
                MissionType = missionType,
                Result = result,
                AttackerPlayerId = attacker.PlayersId,
                DefenderPlayerId = defender.PlayersId,
                AttackerPlayerName = attacker.Name,
                DefenderPlayerName = defender.Name,
                WinnerPlayerName = winnerName,
                LoserPlayerName = loserName,
                AttackerLosses = attackerLosses,
                DefenderLosses = defenderLosses,
                LootMoney = lootSummary.Money,
                LootOil = lootSummary.Oil,
                LootUranium = lootSummary.Uranium,
                LootChips = lootSummary.Chips,
                LootSummaryText = lootSummaryText,
                EndedAtUtc = battleEndedAtUtc
            },
            cancellationToken: cancellationToken);
    }

    private static string BuildLootSummaryText(ResourceLootSummary lootSummary)
    {
        var parts = new List<string>();

        if (lootSummary.Money > 0) parts.Add($"Money {lootSummary.Money:0.##}");
        if (lootSummary.Oil > 0) parts.Add($"Oil {lootSummary.Oil:0.##}");
        if (lootSummary.Uranium > 0) parts.Add($"Uranium {lootSummary.Uranium:0.##}");
        if (lootSummary.Chips > 0) parts.Add($"Chips {lootSummary.Chips:0.##}");

        return parts.Count == 0 ? "None" : string.Join(", ", parts);
    }

    private async Task ProcessSiegeTicksAsync(AppDbContext dbContext, PlayerSiege siege, Player attacker, Country? attackerCountry, Player defender,
        List<PlayerMilitaryTransport> siegeTransports, DateTime now, CancellationToken cancellationToken)
    {
        await EnsureSiegeTrackingAsync(dbContext, siege, defender, siegeTransports, now, cancellationToken);

        var lastTickAt = siege.LastTickAt ?? siege.StartedAt;
        var effectiveTickEnd = siege.EndedAt.HasValue && siege.EndedAt.Value < now
            ? siege.EndedAt.Value
            : now;
        var elapsed = effectiveTickEnd - lastTickAt;
        var tickCount = (int)Math.Floor(elapsed.TotalSeconds / CombatBalanceEngine.SiegeTickInterval.TotalSeconds);

        if (tickCount <= 0)
        {
            return;
        }

        var defenderUnits = await dbContext.PlayerMilitaryUnits
            .Where(u => u.PlayersId == defender.PlayersId && u.Quantity > 0)
            .OrderByDescending(u => u.Level)
            .ThenBy(u => u.MilitaryUnitsCode)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < tickCount; i++)
        {
            var attackerUnits = siegeTransports
                .SelectMany(t => t.PlayerMilitaryTransportUnits)
                .Where(u => u.Quantity > 0)
                .OrderByDescending(u => u.Level)
                .ThenBy(u => u.MilitaryUnitsCode)
                .ToList();

            if (attackerUnits.Count == 0)
            {
                break;
            }

                var lootSummary = LootResources(attacker, attackerCountry, defender, CombatBalanceEngine.SiegeHourlyLootRate);
            siege.LootMoney += lootSummary.Money;
            siege.LootOil += lootSummary.Oil;
            siege.LootUranium += lootSummary.Uranium;
            siege.LootChips += lootSummary.Chips;

            ApplySiegeAttrition(attackerUnits, defenderUnits);

            UpdateSiegeUnitState(siege, Side.Attacker.ToDbValue(), SnapshotUnits(attackerUnits), now);
            UpdateSiegeUnitState(siege, Side.Defender.ToDbValue(), SnapshotUnits(defenderUnits), now);
                siege.LastTickAt = lastTickAt.AddTicks(CombatBalanceEngine.SiegeTickInterval.Ticks * (i + 1L));
        }
    }

    private async Task EnsureSiegeTrackingAsync(AppDbContext dbContext, PlayerSiege siege, Player defender, 
        IReadOnlyCollection<PlayerMilitaryTransport> siegeTransports, DateTime now, CancellationToken cancellationToken)
    {
        if (siege.LastTickAt == null)
        {
            siege.LastTickAt = now;
        }

        if (siege.PlayerSiegeUnits.Any())
        {
            return;
        }

        var attackerStarting = SnapshotUnits(siegeTransports.SelectMany(t => t.PlayerMilitaryTransportUnits).Where(u => u.Quantity > 0));
        var defenderUnits = await dbContext.PlayerMilitaryUnits
            .Where(u => u.PlayersId == defender.PlayersId && u.Quantity > 0)
            .OrderByDescending(u => u.Level)
            .ThenBy(u => u.MilitaryUnitsCode)
            .ToListAsync(cancellationToken);

        AppendSiegeUnitsForSide(siege, Side.Attacker.ToDbValue(), attackerStarting, now);
        AppendSiegeUnitsForSide(siege, Side.Defender.ToDbValue(), SnapshotUnits(defenderUnits), now);
    }

    private static void AppendSiegeUnitsForSide(PlayerSiege siege, string side, IReadOnlyCollection<CombatUnitSnapshot> snapshots, DateTime now)
    {
        foreach (var snapshot in snapshots)
        {
            var existing = siege.PlayerSiegeUnits.FirstOrDefault(unit => unit.Side == side 
                                                                         && unit.MilitaryUnitsCode == snapshot.MilitaryUnitsCode 
                                                                         && unit.Level == snapshot.Level);

            if (existing == null)
            {
                siege.PlayerSiegeUnits.Add(new PlayerSiegeUnit
                {
                    PlayerSiegeUnitsId = Guid.NewGuid(),
                    PlayerSiegesId = siege.PlayerSiegesId,
                    Side = side,
                    MilitaryUnitsCode = snapshot.MilitaryUnitsCode,
                    Level = snapshot.Level,
                    StartingQuantity = snapshot.Quantity,
                    CurrentQuantity = snapshot.Quantity,
                    LostQuantity = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                existing.StartingQuantity += snapshot.Quantity;
                existing.CurrentQuantity += snapshot.Quantity;
                existing.LostQuantity = Math.Max(0, existing.StartingQuantity - existing.CurrentQuantity);
                existing.UpdatedAt = now;
            }
        }
    }

    private static void UpdateSiegeUnitState(PlayerSiege siege, string side, IReadOnlyCollection<CombatUnitSnapshot> remaining, DateTime now)
    {
        var remainingLookup = remaining.ToDictionary(
            unit => GetSnapshotKey(unit.MilitaryUnitsCode, unit.Level),
            unit => unit.Quantity,
            StringComparer.OrdinalIgnoreCase);

        foreach (var unit in siege.PlayerSiegeUnits.Where(unit => string.Equals(unit.Side, side, StringComparison.OrdinalIgnoreCase)))
        {
            unit.CurrentQuantity = remainingLookup.GetValueOrDefault(GetSnapshotKey(unit.MilitaryUnitsCode, unit.Level), 0);
            unit.LostQuantity = Math.Max(0, unit.StartingQuantity - unit.CurrentQuantity);
            unit.UpdatedAt = now;
        }
    }

    private async Task CreateSiegeFinishedNotificationsAsync(Player attacker, Player defender, bool endedByShield, DateTime now,
        CancellationToken cancellationToken)
    {
        await notificationService.CreateAsync(
            attacker.PlayersId,
            PlayerNotificationCode.SiegeFinished,
            new PlayerNotificationDetails
            {
                NotificationKind = endedByShield ? "siege_finished_shield" : "siege_finished_timeout",
                MissionType = PlayerMilitaryTransportMissionType.Siege.ToDbValue(),
                AttackerPlayerId = attacker.PlayersId,
                DefenderPlayerId = defender.PlayersId,
                EndedAtUtc = now,
                EndedByShield = endedByShield,
                CounterpartyName = defender.Name
            },
            now,
            cancellationToken);

        await notificationService.CreateAsync(
            defender.PlayersId,
            PlayerNotificationCode.SiegeFinished,
            new PlayerNotificationDetails
            {
                NotificationKind = endedByShield ? "siege_finished_shield" : "siege_finished_timeout",
                MissionType = PlayerMilitaryTransportMissionType.Siege.ToDbValue(),
                AttackerPlayerId = attacker.PlayersId,
                DefenderPlayerId = defender.PlayersId,
                EndedAtUtc = now,
                EndedByShield = endedByShield,
                CounterpartyName = attacker.Name
            },
            now,
            cancellationToken);
    }

    private async Task ConquerCountryWarAsync(AppDbContext dbContext, CountryWar war, DateTime now, CancellationToken cancellationToken)
    {
        if (!string.Equals(war.Status, CountryWarStatus.Active.ToDbValue(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var attackerPlayerIds = await dbContext.Players
            .Where(player => player.CountriesId == war.CountryFromId)
            .Select(player => player.PlayersId)
            .ToListAsync(cancellationToken);

        var defenderPlayerIds = await dbContext.Players
            .Where(player => player.CountriesId == war.CountryToId)
            .Select(player => player.PlayersId)
            .ToListAsync(cancellationToken);

        await CancelCountryWarMilitaryActionsAsync(dbContext, war.CountryFromId, war.CountryToId, now, cancellationToken);

        var defenderPlayers = await dbContext.Players
            .Where(player => player.CountriesId == war.CountryToId)
            .ToListAsync(cancellationToken);

        var attackerCountry = await dbContext.Countries
            .AsNoTracking()
            .FirstOrDefaultAsync(country => country.CountriesId == war.CountryFromId, cancellationToken);

        var relocationCoordinates = ResolveCountryRelocationCoordinates(attackerCountry?.IsoCode2, attackerCountry?.Name);

        foreach (var player in defenderPlayers)
        {
            player.CountriesId = war.CountryFromId;
            player.LocationX = (decimal)relocationCoordinates.Longitude;
            player.LocationY = (decimal)relocationCoordinates.Latitude;
        }

        war.Status = CountryWarStatus.Ended.ToDbValue();
        war.EndedAt = now;

        await CreateCountryWarEndedNotificationsAsync(
            attackerPlayerIds,
            defenderPlayerIds,
            "country_borders_changed",
            "country_borders_changed",
            now,
            cancellationToken);
    }

    private async Task CancelCountryWarAsDefendedAsync(AppDbContext dbContext, CountryWar war, DateTime now, CancellationToken cancellationToken)
    {
        if (!string.Equals(war.Status, CountryWarStatus.Active.ToDbValue(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var attackerPlayerIds = await dbContext.Players
            .Where(player => player.CountriesId == war.CountryFromId)
            .Select(player => player.PlayersId)
            .ToListAsync(cancellationToken);

        var defenderPlayerIds = await dbContext.Players
            .Where(player => player.CountriesId == war.CountryToId)
            .Select(player => player.PlayersId)
            .ToListAsync(cancellationToken);

        await CancelCountryWarMilitaryActionsAsync(dbContext, war.CountryFromId, war.CountryToId, now, cancellationToken);

        war.Status = CountryWarStatus.Cancelled.ToDbValue();
        war.EndedAt = now;

        await CreateCountryWarEndedNotificationsAsync(
            attackerPlayerIds,
            defenderPlayerIds,
            "country_war_peace_attacker",
            "country_war_peace_defender",
            now,
            cancellationToken);
    }

    private async Task CreateCountryWarEndedNotificationsAsync(
        IReadOnlyCollection<Guid> attackerPlayerIds,
        IReadOnlyCollection<Guid> defenderPlayerIds,
        string attackerNotificationKind,
        string defenderNotificationKind,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var playerId in attackerPlayerIds.Distinct())
        {
            await notificationService.CreateAsync(
                playerId,
                PlayerNotificationCode.CountryWarEnded,
                new PlayerNotificationDetails
                {
                    NotificationKind = attackerNotificationKind,
                    EndedAtUtc = now
                },
                now,
                cancellationToken);
        }

        foreach (var playerId in defenderPlayerIds.Distinct())
        {
            await notificationService.CreateAsync(
                playerId,
                PlayerNotificationCode.CountryWarEnded,
                new PlayerNotificationDetails
                {
                    NotificationKind = defenderNotificationKind,
                    EndedAtUtc = now
                },
                now,
                cancellationToken);
        }
    }

    private static async Task CancelCountryWarMilitaryActionsAsync(
        AppDbContext dbContext,
        Guid attackerCountryId,
        Guid defenderCountryId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attackerPlayerIds = await dbContext.Players
            .Where(player => player.CountriesId == attackerCountryId)
            .Select(player => player.PlayersId)
            .ToListAsync(cancellationToken);

        var defenderPlayerIds = await dbContext.Players
            .Where(player => player.CountriesId == defenderCountryId)
            .Select(player => player.PlayersId)
            .ToListAsync(cancellationToken);

        await CancelMilitaryActionsBetweenPlayersAsync(
            dbContext,
            attackerPlayerIds,
            defenderPlayerIds,
            now,
            cancellationToken);
    }

    private static async Task CancelMilitaryActionsBetweenPlayersAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> firstSidePlayerIds,
        IReadOnlyCollection<Guid> secondSidePlayerIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (firstSidePlayerIds.Count == 0 || secondSidePlayerIds.Count == 0)
        {
            return;
        }

        var transports = await dbContext.PlayerMilitaryTransports
            .Include(transport => transport.PlayerMilitaryTransportUnits)
            .Where(transport =>
                (transport.Status == PlayerTransportStatus.InProgress.ToDbValue() || transport.Status == PlayerTransportStatus.InSiege.ToDbValue())
                && (transport.MissionType == PlayerMilitaryTransportMissionType.Attack.ToDbValue()
                    || transport.MissionType == PlayerMilitaryTransportMissionType.Siege.ToDbValue()
                    || transport.MissionType == PlayerMilitaryTransportMissionType.Aid.ToDbValue())
                && ((firstSidePlayerIds.Contains(transport.PlayerFromId) && secondSidePlayerIds.Contains(transport.PlayerToId))
                    || (secondSidePlayerIds.Contains(transport.PlayerFromId) && firstSidePlayerIds.Contains(transport.PlayerToId))))
            .ToListAsync(cancellationToken);

        foreach (var transport in transports)
        {
            await ReturnSurvivorsAsync(dbContext, transport.PlayerFromId, transport.PlayerMilitaryTransportUnits, cancellationToken);
            transport.Status = PlayerTransportStatus.Cancelled.ToDbValue();
            transport.EndTime = now;
        }

        var activeSieges = await dbContext.PlayerSieges
            .Where(siege =>
                siege.Status == PlayerSiegesStatus.Active.ToDbValue()
                && ((firstSidePlayerIds.Contains(siege.PlayerFromId) && secondSidePlayerIds.Contains(siege.PlayerToId))
                    || (secondSidePlayerIds.Contains(siege.PlayerFromId) && firstSidePlayerIds.Contains(siege.PlayerToId))))
            .ToListAsync(cancellationToken);

        foreach (var siege in activeSieges)
        {
            siege.Status = PlayerSiegesStatus.Cancelled.ToDbValue();
            siege.EndedAt = now;
        }
    }

    private static async Task ReturnSurvivorsAsync(AppDbContext dbContext, Guid playerId, IEnumerable<PlayerMilitaryTransportUnit> units, 
        CancellationToken cancellationToken)
    {
        var roster = await dbContext.PlayerMilitaryUnits
            .Where(entry => entry.PlayersId == playerId)
            .ToListAsync(cancellationToken);

        foreach (var unit in units.Where(u => u.Quantity > 0))
        {
            var rosterEntry = roster.FirstOrDefault(r => r.MilitaryUnitsCode == unit.MilitaryUnitsCode && r.Level == unit.Level);
            if (rosterEntry == null)
            {
                rosterEntry = new PlayerMilitaryUnit
                {
                    PlayerMilitaryUnitsId = Guid.NewGuid(),
                    PlayersId = playerId,
                    MilitaryUnitsCode = unit.MilitaryUnitsCode,
                    Level = unit.Level,
                    Quantity = 0,
                    CreatedAt = DateTime.UtcNow
                };
                roster.Add(rosterEntry);
                dbContext.PlayerMilitaryUnits.Add(rosterEntry);
            }

            rosterEntry.Quantity += unit.Quantity;
            unit.Quantity = 0;
        }
    }

    private static (double Latitude, double Longitude) ResolveCountryRelocationCoordinates(string? isoCode2, string? countryName)
    {
        var geoJsonPath = ResolveCountriesGeoJsonPath();
        var admCode1 = (string?)null;
        var name = countryName;
        return RandomCoordinateGenerator.GetRandomLandCoordinateForRegion(
            isoCode2,
            admCode1,
            name,
            geoJsonPath ?? throw new InvalidOperationException("Countries GeoJSON file is unavailable."));
    }

    private static string? ResolveCountriesGeoJsonPath()
    {
        var relativePath = Path.Combine("wwwroot", "assets", "maps", "ne_10m_admin_0_countries.json");
        var baseDirectory = AppContext.BaseDirectory;
        var currentDirectory = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(baseDirectory, relativePath),
            Path.Combine(currentDirectory, relativePath),
            Path.Combine(baseDirectory, "VoltrokWebApp", relativePath),
            Path.Combine(currentDirectory, "VoltrokWebApp", relativePath),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "VoltrokWebApp", relativePath)),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "VoltrokWebApp", relativePath)),
            Path.GetFullPath(Path.Combine(currentDirectory, "..", "VoltrokWebApp", relativePath)),
            Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "VoltrokWebApp", relativePath))
        };

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }


    private static ResourceLootSummary LootResources(Player attacker, Country? attackerCountry, Player defender, decimal lootRate)
    {
        var lootMoney = Math.Min(defender.Money, Math.Round(defender.Money * lootRate, 2));
        var lootOil = Math.Min(defender.Oil, Math.Round(defender.Oil * lootRate, 2));
        var lootUranium = Math.Min(defender.Uranium, Math.Round(defender.Uranium * lootRate, 2));
        var lootChips = Math.Min(defender.Chips, Math.Round(defender.Chips * lootRate, 2));
        var countryTaxPercent = Math.Clamp(attackerCountry?.TaxPercent ?? 0m, 0m, 100m);

        var countryMoney = CalculateTaxAmount(lootMoney, countryTaxPercent);
        var countryOil = CalculateTaxAmount(lootOil, countryTaxPercent);
        var countryUranium = CalculateTaxAmount(lootUranium, countryTaxPercent);
        var countryChips = CalculateTaxAmount(lootChips, countryTaxPercent);

        var attackerMoney = Math.Max(0m, lootMoney - countryMoney);
        var attackerOil = Math.Max(0m, lootOil - countryOil);
        var attackerUranium = Math.Max(0m, lootUranium - countryUranium);
        var attackerChips = Math.Max(0m, lootChips - countryChips);

        if (lootMoney > 0)
        {
            defender.Money -= lootMoney;
            attacker.Money += attackerMoney;
            attacker.Score += lootMoney * AttackerLootScoreMultiplier;
            defender.Score = Math.Max(0m, defender.Score - (lootMoney * DefenderLootScorePenaltyMultiplier));
        }

        if (lootOil > 0)
        {
            defender.Oil -= lootOil;
            attacker.Oil += attackerOil;
        }

        if (lootUranium > 0)
        {
            defender.Uranium -= lootUranium;
            attacker.Uranium += attackerUranium;
        }

        if (lootChips > 0)
        {
            defender.Chips -= lootChips;
            attacker.Chips += attackerChips;
        }

        if (attackerCountry != null)
        {
            if (countryMoney > 0) attackerCountry.Money += countryMoney;
            if (countryOil > 0) attackerCountry.Oil += countryOil;
            if (countryUranium > 0) attackerCountry.Uranium += countryUranium;
            if (countryChips > 0) attackerCountry.Chips += countryChips;
        }

        return new ResourceLootSummary(lootMoney, lootOil, lootUranium, lootChips);
    }

    private static decimal CalculateTaxAmount(decimal grossAmount, decimal taxPercent)
        => CombatBalanceEngine.CalculateTaxAmount(grossAmount, taxPercent);

    private static decimal CalculateAttackerPower(IEnumerable<PlayerMilitaryTransportUnit> units, bool includeSiegePower = false)
        => MilitaryCombatPowerEngine.CalculateAttackerPower(
            units.Select(ToCombatStack).Where(stack => stack.HasValue).Select(stack => stack!.Value),
            includeSiegePower);

    private static decimal CalculateDefenderPower(IEnumerable<PlayerMilitaryUnit> defenderUnits, IEnumerable<PlayerMilitaryTransportUnit> attackerUnits)
        => MilitaryCombatPowerEngine.CalculateDefenderPower(
            defenderUnits.Select(ToCombatStack).Where(stack => stack.HasValue).Select(stack => stack!.Value),
            attackerUnits.Select(ToCombatStack).Where(stack => stack.HasValue).Select(stack => stack!.Value));

    private static string GetCombatFamily(PlayerMilitaryTransportUnit unit)
        => MapToCombatFamily(unit.MilitaryUnitsCode);

    private static string GetCombatFamily(PlayerMilitaryUnit unit)
        => MapToCombatFamily(unit.MilitaryUnitsCode);

    private static string MapToCombatFamily(string? unitCode)
    {
        if (string.IsNullOrWhiteSpace(unitCode))
        {
            return string.Empty;
        }

        return CombatBalanceEngine.NormalizeCombatFamily(unitCode);
    }

    private static MilitaryUnit ResolveCombatDefinition(PlayerMilitaryTransportUnit unit)
        => ResolveCombatDefinition(unit.MilitaryUnitsCode);

    private static MilitaryUnit ResolveCombatDefinition(PlayerMilitaryUnit unit)
        => ResolveCombatDefinition(unit.MilitaryUnitsCode);

    private static CombatUnitStack? ToCombatStack(PlayerMilitaryTransportUnit unit)
        => MilitaryCombatPowerEngine.CreateStack(unit.MilitaryUnitsCode, unit.Level, unit.Quantity);

    private static CombatUnitStack? ToCombatStack(PlayerMilitaryUnit unit)
        => MilitaryCombatPowerEngine.CreateStack(unit.MilitaryUnitsCode, unit.Level, unit.Quantity);

    private static MilitaryUnit ResolveCombatDefinition(string? unitCode)
    {
        if (!string.IsNullOrWhiteSpace(unitCode) && CombatUnitDefinitions.TryGetValue(unitCode.Trim(), out var definition))
        {
            return definition;
        }

        return MilitaryUnitEngine.MilitaryUnitsAllData.First();
    }

    private static int CalculateLosses(int totalUnits, decimal ownPower, decimal enemyPower, bool winner)
    {
        if (totalUnits <= 0)
        {
            return 0;
        }

        if (ownPower <= 0)
        {
            return totalUnits;
        }

        return CombatBalanceEngine.CalculateLosses(totalUnits, ownPower, enemyPower, winner);
    }

    private static void ApplySimulationLosses(List<PlayerMilitaryTransportUnit> units, IReadOnlyList<BattleSimulationUnitInput> killedUnits)
    {
        foreach (var killedUnit in killedUnits)
        {
            var remaining = killedUnit.Quantity;
            foreach (var unit in units
                         .Where(unit => unit.Level == killedUnit.Level
                                        && string.Equals(unit.MilitaryUnitsCode, killedUnit.UnitCode, StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(unit => unit.Level)
                         .ThenBy(unit => unit.MilitaryUnitsCode, StringComparer.OrdinalIgnoreCase))
            {
                if (remaining <= 0)
                {
                    break;
                }

                var loss = Math.Min(unit.Quantity, remaining);
                unit.Quantity -= loss;
                remaining -= loss;
            }
        }
    }

    private static void ApplySimulationLosses(List<PlayerMilitaryUnit> units, IReadOnlyList<BattleSimulationUnitInput> killedUnits)
    {
        foreach (var killedUnit in killedUnits)
        {
            var remaining = killedUnit.Quantity;
            foreach (var unit in units
                         .Where(unit => unit.Level == killedUnit.Level
                                        && string.Equals(unit.MilitaryUnitsCode, killedUnit.UnitCode, StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(unit => unit.Level)
                         .ThenBy(unit => unit.MilitaryUnitsCode, StringComparer.OrdinalIgnoreCase))
            {
                if (remaining <= 0)
                {
                    break;
                }

                var loss = Math.Min(unit.Quantity, remaining);
                unit.Quantity -= loss;
                remaining -= loss;
            }
        }
    }

    private static int ApplyLosses(List<PlayerMilitaryTransportUnit> units, int lossTarget)
    {
        var remaining = lossTarget;
        foreach (var unit in units.OrderByDescending(u => u.Level))
        {
            if (remaining <= 0)
            {
                break;
            }

            var loss = Math.Min(unit.Quantity, remaining);
            unit.Quantity -= loss;
            remaining -= loss;
        }

        return lossTarget - remaining;
    }

    private static int ApplyLosses(List<PlayerMilitaryUnit> units, int lossTarget)
    {
        var remaining = lossTarget;
        foreach (var unit in units.OrderByDescending(u => u.Level))
        {
            if (remaining <= 0)
            {
                break;
            }

            var loss = Math.Min(unit.Quantity, remaining);
            unit.Quantity -= loss;
            remaining -= loss;
        }

        return lossTarget - remaining;
    }

    private static void ApplySiegeAttrition(List<PlayerMilitaryTransportUnit> attackerUnits, List<PlayerMilitaryUnit> defenderUnits)
    {
        if (attackerUnits.Count == 0 || defenderUnits.Count == 0)
        {
            return;
        }

        var attackerPower = CalculateAttackerPower(attackerUnits);
        var defenderPower = CalculateDefenderPower(defenderUnits, attackerUnits);
        if (attackerPower <= 0 && defenderPower <= 0)
        {
            return;
        }

        var attackerLossTarget = CalculateSiegeLosses(
            attackerUnits.Sum(u => u.Quantity),
            defenderPower,
            attackerPower,
            CombatBalanceEngine.SiegeAttackerBaseLossRate,
            CombatBalanceEngine.SiegeAttackerSwingRate);
        var defenderLossTarget = CalculateSiegeLosses(
            defenderUnits.Sum(u => u.Quantity),
            attackerPower,
            defenderPower,
            CombatBalanceEngine.SiegeDefenderBaseLossRate,
            CombatBalanceEngine.SiegeDefenderSwingRate);

        ApplyLosses(attackerUnits, attackerLossTarget);
        ApplyLosses(defenderUnits, defenderLossTarget);
    }

    private static int CalculateSiegeLosses(int totalUnits, decimal enemyPower, decimal ownPower, decimal baseRate, decimal swingRate)
    {
        if (totalUnits <= 0)
        {
            return 0;
        }

        if (enemyPower <= 0)
        {
            return 0;
        }

        return CombatBalanceEngine.CalculateSiegeLosses(totalUnits, enemyPower, ownPower, baseRate, swingRate);
    }

    private static List<CombatUnitSnapshot> SnapshotUnits(IEnumerable<PlayerMilitaryTransportUnit> units)
        => units
            .Where(u => u.Quantity > 0)
            .GroupBy(u => GetSnapshotKey(u.MilitaryUnitsCode, u.Level), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                return new CombatUnitSnapshot(first.MilitaryUnitsCode.Trim(), first.Level, group.Sum(x => x.Quantity));
            })
            .OrderByDescending(x => x.Level)
            .ThenBy(x => x.MilitaryUnitsCode)
            .ToList();

    private static List<CombatUnitSnapshot> SnapshotUnits(IEnumerable<PlayerMilitaryUnit> units)
        => units
            .Where(u => u.Quantity > 0)
            .GroupBy(u => GetSnapshotKey(u.MilitaryUnitsCode, u.Level), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                return new CombatUnitSnapshot(first.MilitaryUnitsCode.Trim(), first.Level, group.Sum(x => x.Quantity));
            })
            .OrderByDescending(x => x.Level)
            .ThenBy(x => x.MilitaryUnitsCode)
            .ToList();

    private static IEnumerable<PlayerBattleReportUnit> BuildBattleReportUnits(Guid reportId, IReadOnlyCollection<CombatUnitSnapshot> attackerStarting,
        IReadOnlyCollection<CombatUnitSnapshot> attackerRemaining,
        IReadOnlyCollection<CombatUnitSnapshot> defenderStarting,
        IReadOnlyCollection<CombatUnitSnapshot> defenderRemaining,
        DateTime createdAt)
        => BuildBattleReportUnitsForSide(reportId, Side.Attacker.ToDbValue(), attackerStarting, attackerRemaining, createdAt)
            .Concat(BuildBattleReportUnitsForSide(reportId, Side.Defender.ToDbValue(), defenderStarting, defenderRemaining, createdAt));

    private static IEnumerable<PlayerBattleReportUnit> BuildBattleReportUnitsForSide(Guid reportId, string side, IReadOnlyCollection<CombatUnitSnapshot> starting,
        IReadOnlyCollection<CombatUnitSnapshot> remaining,
        DateTime createdAt)
    {
        var remainingLookup = remaining.ToDictionary(
            unit => GetSnapshotKey(unit.MilitaryUnitsCode, unit.Level),
            unit => unit.Quantity,
            StringComparer.OrdinalIgnoreCase);

        foreach (var unit in starting.OrderByDescending(x => x.Level).ThenBy(x => x.MilitaryUnitsCode))
        {
            var remainingQuantity = remainingLookup.GetValueOrDefault(GetSnapshotKey(unit.MilitaryUnitsCode, unit.Level), 0);
            yield return new PlayerBattleReportUnit
            {
                PlayerBattleReportUnitsId = Guid.NewGuid(),
                PlayerBattleReportsId = reportId,
                Side = side,
                MilitaryUnitsCode = unit.MilitaryUnitsCode,
                Level = unit.Level,
                StartingQuantity = unit.Quantity,
                RemainingQuantity = remainingQuantity,
                LostQuantity = Math.Max(0, unit.Quantity - remainingQuantity),
                CreatedAt = createdAt
            };
        }
    }

    private static string GetSnapshotKey(string? militaryUnitsCode, int level)
        => $"{(militaryUnitsCode ?? string.Empty).Trim()}::{level}";

    private readonly record struct ResourceLootSummary(decimal Money, decimal Oil, decimal Uranium, decimal Chips)
    {
        public static ResourceLootSummary Empty => new(0m, 0m, 0m, 0m);
    }

    private readonly record struct CombatUnitSnapshot(string MilitaryUnitsCode, int Level, int Quantity);
}
