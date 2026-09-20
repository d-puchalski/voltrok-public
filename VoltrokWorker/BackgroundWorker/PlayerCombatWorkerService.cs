using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using VoltrokEF;
using VoltrokServices.Models;
using VoltrokServices.Models.Enums;
using VoltrokServices.Services.Notifications;
using VoltrokServices.Services;

namespace VoltrokWorker.BackgroundWorker;

public class PlayerCombatWorkerService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    NotificationService notificationService) : BackgroundService
{
    private static readonly IReadOnlyDictionary<string, MilitaryUnit> CombatUnitDefinitions = StaticData.AllMilitaryUnits
        .ToDictionary(unit => unit.Code.ToString(), StringComparer.OrdinalIgnoreCase);
    private const decimal ScoreReductionFactor = 20m;
    private const decimal AttackerLootScoreMultiplier = 5m / ScoreReductionFactor;
    private const decimal DefenderLootScorePenaltyMultiplier = 3m / ScoreReductionFactor;

    private int CombatIntervalSeconds => Math.Max(5, configuration.GetValue("BackgroundWorker:PlayerCombatBreakSeconds", 10));
    private static readonly TimeSpan MaxSiegeDuration = CombatBalanceEngine.MaxSiegeDuration;
    private static readonly TimeSpan SiegeAttritionTick = CombatBalanceEngine.SiegeTickInterval;
    private const decimal SiegeHourlyAttritionRate = CombatBalanceEngine.SiegeAttackerBaseLossRate;
    private const decimal StandardAttackLootRate = CombatBalanceEngine.StandardAttackLootRate;
    private const decimal SiegeInitialLootRate = CombatBalanceEngine.SiegeInitialLootRate;
    private const decimal SiegeHourlyLootRate = CombatBalanceEngine.SiegeHourlyLootRate;
    private TimeSpan MinimumWarDuration => TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue<int?>("WarSettings:MinimumWarDurationMinutes") ?? 1));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Player combat worker started. Interval: {CombatIntervalSeconds}s.", CombatIntervalSeconds);
        var cycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            cycle++;
            var startedAt = DateTime.UtcNow;
            Log.Information("Player combat worker cycle {Cycle} started.", cycle);

            try
            {
                await ProcessCombatAsync(stoppingToken);
                Log.Information(
                    "Player combat worker cycle {Cycle} finished in {DurationMs} ms.",
                    cycle,
                    (DateTime.UtcNow - startedAt).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while processing combat and sieges");
            }

            try
            {
                Log.Information("Player combat worker sleeping for {CombatIntervalSeconds}s.", CombatIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(CombatIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Player combat worker stopping due to cancellation.");
                break;
            }
        }
    }

    private async Task ProcessCombatAsync(CancellationToken cancellationToken)
    {
        await ResolveCompletedTransportsAsync(cancellationToken);
        await ResolveCompletedSiegesAsync(cancellationToken);
        await ResolveDormantCountryWarsAsync(cancellationToken);
    }

    private async Task ResolveDormantCountryWarsAsync(CancellationToken cancellationToken)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var minStart = now - MinimumWarDuration;

        var activeWars = await dbContext.CountryWars
            .Where(w => w.Status == "active" && w.StartedAt <= minStart)
            .ToListAsync(cancellationToken);

        if (activeWars.Count == 0)
        {
            return;
        }

        var changed = false;
        foreach (var war in activeWars)
        {
            var defenderHasUnits = await (from pm in dbContext.PlayerMilitaries
                                          join p in dbContext.Players on pm.PlayersId equals p.PlayersId
                                          where p.CountriesId == war.DefenderCountriesId
                                                && pm.Quantity > 0
                                          select pm.PlayerMilitaryLogId)
                .AnyAsync(cancellationToken);

            if (defenderHasUnits)
            {
                continue;
            }

            await ConquerCountryWarAsync(dbContext, war, now, "no_defenders", cancellationToken);
            changed = true;

            var playersToNotify = await dbContext.Players
                .Where(v => v.CountriesId == war.AttackerCountriesId || v.CountriesId == war.DefenderCountriesId)
                .Select(v => v.PlayersId)
                .ToListAsync(cancellationToken);

            foreach (var playerId in playersToNotify)
            {
                await notificationService.CreateAsync(
                    playerId,
                    PlayerNotificationCode.CountryWarEnded,
                    new PlayerNotificationDetails
                    {
                        NotificationKind = "country_war_ended_no_defenders"
                    },
                    now, cancellationToken);
            }
        }

        if (!changed)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveDormantRegionWarsAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    private async Task ResolveCompletedTransportsAsync(CancellationToken cancellationToken)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var transports = await dbContext.PlayerMilitaryTransports
            .Include(t => t.PlayerMilitaryTransportUnits)
            .Where(t => t.Status == "in_progress" && t.EndTime <= now)
            .ToListAsync(cancellationToken);

        if (transports.Count == 0)
        {
            return;
        }

        foreach (var transport in transports)
        {
            var missionType = transport.MissionType?.ToLowerInvariant();
            if (missionType == "attack" || missionType == "siege")
            {
                var lootRate = missionType == "siege" ? SiegeInitialLootRate : StandardAttackLootRate;
                await ResolveBattleAsync(dbContext, transport, "battle", now, cancellationToken, lootRate);
            }
            else if (missionType == "aid")
            {
                await ResolveAidAsync(dbContext, transport, cancellationToken);
                await ResolveActiveSiegeAfterAidAsync(dbContext, transport, now, cancellationToken);
                transport.Status = "completed";
            }
            else
            {
                transport.Status = "completed";
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveCompletedSiegesAsync(CancellationToken cancellationToken)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var sieges = await dbContext.PlayerSieges
            .Where(s => s.Status == "active")
            .ToListAsync(cancellationToken);

        if (sieges.Count == 0)
        {
            return;
        }

        foreach (var siege in sieges)
        {
            var siegeTransports = await dbContext.PlayerMilitaryTransports
                .Include(t => t.PlayerMilitaryTransportUnits)
                .Where(t => t.Status == "in_siege" && t.PlayerToId == siege.PlayersId && t.PlayerFromId == siege.AttackerPlayersId)
                .ToListAsync(cancellationToken);

            var siegeTimedOut = siege.EndedAt != null && siege.EndedAt <= now;
            if (siegeTransports.Count > 0 && !siegeTimedOut)
            {
                var lootAnchor = siegeTransports
                    .OrderBy(t => t.StartTime)
                    .FirstOrDefault();

                if (lootAnchor != null)
                {
                    var lastLootTickAt = lootAnchor.CreatedAt?.ToUniversalTime() ?? lootAnchor.StartTime;
                    var lootTicks = (int)Math.Floor((now - lastLootTickAt).TotalHours / SiegeAttritionTick.TotalHours);
                    if (lootTicks > 0)
                    {
                        await ApplySiegeLootAsync(dbContext, siege.AttackerPlayersId, siege.PlayersId, lootTicks, cancellationToken);
                    }
                }
            }

            if (siegeTransports.Count > 0)
            {
                ApplySiegeAttrition(siegeTransports, now);
            }

            if (!defenderShieldEnabled && !siegeTimedOut)
            {
                continue;
            }

            if (siegeTransports.Count > 0)
            {
                foreach (var transport in siegeTransports)
                {
                    if (defenderShieldEnabled)
                    {
                        await ReturnSurvivorsAsync(dbContext, transport.PlayerFromId, transport.PlayerMilitaryTransportUnits.ToList(), cancellationToken);
                    }
                    else
                    {
                        foreach (var unit in transport.PlayerMilitaryTransportUnits.Where(u => u.Quantity > 0))
                        {
                            unit.Quantity = 0;
                        }
                    }

                    transport.Status = "completed";
                }
            }

            siege.Status = "cancelled";
            siege.EndedAt = now;

            var attackerPlayer = await dbContext.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.PlayersId == siege.AttackerPlayersId, cancellationToken);
            var defenderPlayer = await dbContext.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.PlayersId == siege.PlayersId, cancellationToken);

            var siegeFinishPayload = JsonSerializer.Serialize(new
            {
                notificationKind = defenderShieldEnabled
                    ? "siege_finished_attacker_shield"
                    : "siege_finished_attacker_timeout",
                missionType = "siege",
                attackerPlayerId = siege.AttackerPlayersId,
                defenderPlayerId = siege.PlayersId,
                endedAtUtc = now,
                endedByShield = defenderShieldEnabled
            });

            if (attackerPlayer != null && defenderPlayer != null)
            {
                await notificationService.CreateAsync(
                    attackerPlayer.PlayersId,
                    PlayerNotificationCode.SiegeFinished,
                    new PlayerNotificationDetails
                    {
                        NotificationKind = defenderShieldEnabled
                            ? "siege_finished_attacker_shield"
                            : "siege_finished_attacker_timeout",
                        MissionType = "siege",
                        AttackerPlayerId = siege.AttackerPlayersId,
                        DefenderPlayerId = siege.PlayersId,
                        CounterpartyName = defenderPlayer.Name,
                        EndedAtUtc = now,
                        EndedByShield = defenderShieldEnabled
                    },
                    now);

                var defenderSiegeFinishPayload = JsonSerializer.Serialize(new
                {
                    notificationKind = defenderShieldEnabled
                        ? "siege_finished_defender_shield"
                        : "siege_finished_defender_timeout",
                    missionType = "siege",
                    attackerPlayerId = siege.AttackerPlayersId,
                    defenderPlayerId = siege.PlayersId,
                    endedAtUtc = now,
                    endedByShield = defenderShieldEnabled
                });

                await notificationService.CreateAsync(
                    defenderPlayer.PlayersId,
                    PlayerNotificationCode.SiegeFinished,
                    new PlayerNotificationDetails
                    {
                        NotificationKind = defenderShieldEnabled
                            ? "siege_finished_defender_shield"
                            : "siege_finished_defender_timeout",
                        MissionType = "siege",
                        AttackerPlayerId = siege.AttackerPlayersId,
                        DefenderPlayerId = siege.PlayersId,
                        CounterpartyName = attackerPlayer.Name,
                        EndedAtUtc = now,
                        EndedByShield = defenderShieldEnabled
                    },
                    now);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<DateTime?> StartSiegeAsync(AppDbContext dbContext, PlayerMilitaryTransport transport, DateTime now, CancellationToken cancellationToken)
    {
        var existingSiege = await dbContext.PlayerSieges
            .FirstOrDefaultAsync(s => s.PlayersId == transport.PlayerToId && s.Status == "active", cancellationToken);

        if (existingSiege != null)
        {
            if (existingSiege.AttackerPlayersId != transport.PlayerFromId)
            {
                return null;
            }

            if (existingSiege.EndedAt.HasValue)
            {
                return existingSiege.EndedAt.Value;
            }

            existingSiege.EndedAt = now.Add(MaxSiegeDuration);
            return existingSiege.EndedAt.Value;
        }

        var endedAt = now.Add(MaxSiegeDuration);
        dbContext.PlayerSieges.Add(new PlayerSiege
        {
            PlayerSiegesId = Guid.NewGuid(),
            PlayersId = transport.PlayerToId!.Value,
            AttackerPlayersId = transport.PlayerFromId,
            Status = "active",
            StartedAt = now,
            EndedAt = endedAt
        });

        var siegeStartPayload = JsonSerializer.Serialize(new
        {
            notificationKind = "siege_started",
            missionType = "siege",
            attackerPlayerId = transport.PlayerFromId,
            defenderPlayerId = transport.PlayerToId,
            startedAtUtc = now
        });

        await notificationService.CreateAsync(
            transport.PlayerToId!.Value,
            PlayerNotificationCode.SiegeStarted,
            new PlayerNotificationDetails
            {
                NotificationKind = "siege_started",
                MissionType = "siege",
                AttackerPlayerId = transport.PlayerFromId,
                DefenderPlayerId = transport.PlayerToId!.Value,
                StartedAtUtc = now
            },
            now, cancellationToken);
        return endedAt;
    }

    private static async Task ResolveAidAsync(AppDbContext dbContext, PlayerMilitaryTransport transport, CancellationToken cancellationToken)
    {
        if (string.Equals(transport.DestinationType, "country", StringComparison.OrdinalIgnoreCase) &&
            transport.DestinationCountryId.HasValue)
        {
            foreach (var unit in transport.PlayerMilitaryTransportUnits.Where(u => u.Quantity > 0))
            {
                var military = await dbContext.CountryMilitaries
                    .FirstOrDefaultAsync(m =>
                        m.CountriesId == transport.DestinationCountryId.Value &&
                        m.MilitaryUnitsId == ResolveMilitaryUnitIdForRosterLevel(dbContext, unit.Level),
                        cancellationToken);

                var militaryUnitId = ResolveMilitaryUnitIdForRosterLevel(dbContext, unit.Level);
                if (militaryUnitId == Guid.Empty)
                {
                    continue;
                }

                if (military == null)
                {
                    military = new CountryMilitary
                    {
                        CountryMilitaryId = Guid.NewGuid(),
                        CountriesId = transport.DestinationCountryId.Value,
                        MilitaryUnitsId = militaryUnitId,
                        Quantity = 0,
                        UpdatedAt = DateTime.UtcNow
                    };
                    dbContext.CountryMilitaries.Add(military);
                }

                military.Quantity += unit.Quantity;
                military.UpdatedAt = DateTime.UtcNow;
                unit.Quantity = 0;
            }
            return;
        }

        if (!transport.PlayerToId.HasValue)
        {
            return;
        }

        var destinationPlayer = await dbContext.Players.FirstOrDefaultAsync(v => v.PlayersId == transport.PlayerToId.Value, cancellationToken);
        if (destinationPlayer == null)
        {
            return;
        }

        var destinationRoster = await dbContext.PlayerMilitaries
            .Where(entry => entry.PlayersId == destinationPlayer.PlayersId)
            .ToListAsync(cancellationToken);

        foreach (var unit in transport.PlayerMilitaryTransportUnits.Where(u => u.Quantity > 0))
        {
            var rosterEntry = destinationRoster.FirstOrDefault(r => r.Level == unit.Level);
            if (rosterEntry == null)
            {
                dbContext.PlayerMilitaries.Add(new PlayerMilitary
                {
                    PlayerMilitaryLogId = Guid.NewGuid(),
                    PlayersId = transport.PlayerToId!.Value,
                    Level = unit.Level,
                    Quantity = unit.Quantity
                });
            }
            else
            {
                rosterEntry.Quantity += unit.Quantity;
            }

            unit.Quantity = 0;
        }
    }

    private static Guid ResolveMilitaryUnitIdForRosterLevel(AppDbContext dbContext, int rosterLevel)
    {
        return dbContext.MilitaryUnits
            .AsNoTracking()
            .OrderBy(unit => unit.ProductionCost)
            .ThenBy(unit => unit.AttackPoints)
            .ThenBy(unit => unit.Code)
            .Select(unit => unit.MilitaryUnitsId)
            .Skip(Math.Max(0, rosterLevel - 1))
            .FirstOrDefault();
    }

    private Task ResolveBattleAsync(AppDbContext dbContext, PlayerMilitaryTransport transport, string resultPrefix, DateTime now, CancellationToken cancellationToken, decimal lootRate = StandardAttackLootRate)
    {
        return ResolveBattleAsync(dbContext, transport, transport.PlayerFromId, transport.PlayerToId!.Value, transport.PlayerMilitaryTransportUnits.ToList(), resultPrefix, now, cancellationToken, lootRate);
    }

    private async Task ResolveBattleAsync(
        AppDbContext dbContext,
        PlayerMilitaryTransport? sourceTransport,
        Guid attackerPlayerId,
        Guid defenderPlayerId,
        List<PlayerMilitaryTransportUnit> attackerUnits,
        string resultPrefix,
        DateTime now,
        CancellationToken cancellationToken,
        decimal lootRate = StandardAttackLootRate,
        bool returnAttackerSurvivors = true,
        bool applyLoot = true)
    {
        var attackerPlayer = await dbContext.Players.FirstOrDefaultAsync(v => v.PlayersId == attackerPlayerId, cancellationToken);
        var defenderPlayer = await dbContext.Players.FirstOrDefaultAsync(v => v.PlayersId == defenderPlayerId, cancellationToken);

        if (attackerPlayer == null || defenderPlayer == null)
        {
            return;
        }

        var defenderUnits = await dbContext.PlayerMilitaries
            .Where(entry => entry.PlayersId == defenderPlayerId)
            .ToListAsync(cancellationToken);

        var simulationResult = BattleSimulationEngine.SimulateBattle(
            attackerUnits.Select(unit => new BattleSimulationUnitInput(unit.MilitaryUnitsCode, unit.Level, unit.Quantity)),
            defenderUnits.Select(unit => new BattleSimulationUnitInput(unit.MilitaryUnitsCode, unit.Level, unit.Quantity)));

        ApplySimulationLosses(attackerUnits, simulationResult.AttackerKilledUnits, dbContext.PlayerMilitaryTransportUnits);
        ApplySimulationLosses(defenderUnits, simulationResult.DefenderKilledUnits, dbContext.PlayerMilitaries);

        var attackerPower = (int)Math.Round(simulationResult.AttackerPower, MidpointRounding.AwayFromZero);
        var defenderPower = (int)Math.Round(simulationResult.DefenderPower, MidpointRounding.AwayFromZero);
        var attackerWins = simulationResult.AttackerWon;
        var attackerLosses = simulationResult.Attacker.KilledTotalUnits;
        var defenderLosses = simulationResult.Defender.KilledTotalUnits;

        DateTime? siegeEndsAt = null;
        var keepOccupation = false;
        if (sourceTransport != null && attackerWins && !sourceTransport.AutoReturnAfterBattle)
        {
            siegeEndsAt = await StartSiegeAsync(dbContext, sourceTransport, now, cancellationToken);
            keepOccupation = siegeEndsAt.HasValue;
        }

        if (!keepOccupation && returnAttackerSurvivors)
        {
            await ReturnSurvivorsAsync(dbContext, attackerPlayerId, attackerUnits, cancellationToken);
        }

        if (sourceTransport != null)
        {
            sourceTransport.Status = keepOccupation ? "in_siege" : "completed";
            if (keepOccupation && siegeEndsAt.HasValue)
            {
                sourceTransport.StartTime = now;
                sourceTransport.EndTime = siegeEndsAt.Value;
                sourceTransport.CreatedAt = now;
            }
        }

        var lootMoney = 0m;
        var lootProducts = new List<PlayerBattleReportProduct>();

        if (attackerWins && applyLoot)
        {
            lootMoney = await LootResourcesAsync(dbContext, attackerPlayerId, defenderPlayerId, lootProducts, lootRate, cancellationToken);
            await TryResolveCountryWarConquestAsync(dbContext, attackerPlayerId, defenderPlayerId, now, cancellationToken);
        }

        var report = new PlayerBattleReport
        {
            PlayerBattleReportsId = Guid.NewGuid(),
            AttackerPlayersId = attackerPlayerId,
            DefenderPlayersId = defenderPlayerId,
            Result = attackerWins ? $"{resultPrefix}_attacker_win" : $"{resultPrefix}_defender_win",
            AttackerPower = attackerPower,
            DefenderPower = defenderPower,
            AttackerLosses = attackerLosses,
            DefenderLosses = defenderLosses,
            LootMoney = lootMoney,
            StartedAt = now,
            EndedAt = now,
            CreatedAt = now
        };

        dbContext.PlayerBattleReports.Add(report);

        foreach (var lootProduct in lootProducts)
        {
            lootProduct.PlayerBattleReportsId = report.PlayerBattleReportsId;
            dbContext.PlayerBattleReportProducts.Add(lootProduct);
        }

        var reportPayload = JsonSerializer.Serialize(new
        {
            notificationKind = attackerWins ? "battle_report_attacker_victory" : "battle_report_attacker_defeat",
            battleReportId = report.PlayerBattleReportsId,
            attackerPlayerId,
            defenderPlayerId,
            result = report.Result,
            attackerPlayerName = attackerPlayer.Name,
            defenderPlayerName = defenderPlayer.Name,
            attackerPower = report.AttackerPower,
            defenderPower = report.DefenderPower,
            attackerLosses = report.AttackerLosses,
            defenderLosses = report.DefenderLosses,
            lootMoney = report.LootMoney,
            endedAtUtc = report.EndedAt
        });

        var lootSummary = await BuildLootSummaryAsync(dbContext, lootMoney, lootProducts, cancellationToken);
        await notificationService.CreateAsync(
            attackerPlayerId,
            PlayerNotificationCode.BattleReportAttacker,
            new PlayerNotificationDetails
            {
                NotificationKind = attackerWins ? "battle_report_attacker_victory" : "battle_report_attacker_defeat",
                MissionType = resultPrefix,
                Result = report.Result,
                AttackerPlayerId = attackerPlayerId,
                DefenderPlayerId = defenderPlayerId,
                AttackerPlayerName = attackerPlayer.Name,
                DefenderPlayerName = defenderPlayer.Name,
                WinnerPlayerName = attackerWins ? attackerPlayer.Name : defenderPlayer.Name,
                LoserPlayerName = attackerWins ? defenderPlayer.Name : attackerPlayer.Name,
                AttackerLosses = report.AttackerLosses,
                DefenderLosses = report.DefenderLosses,
                LootMoney = report.LootMoney,
                LootSummaryText = lootSummary.gained,
                EndedAtUtc = report.EndedAt ?? now
            },
            now, cancellationToken);

        var defenderReportPayload = JsonSerializer.Serialize(new
        {
            notificationKind = attackerWins ? "battle_report_defender_defeat" : "battle_report_defender_victory",
            battleReportId = report.PlayerBattleReportsId,
            attackerPlayerId,
            defenderPlayerId,
            result = report.Result,
            attackerPlayerName = attackerPlayer.Name,
            defenderPlayerName = defenderPlayer.Name,
            attackerPower = report.AttackerPower,
            defenderPower = report.DefenderPower,
            attackerLosses = report.AttackerLosses,
            defenderLosses = report.DefenderLosses,
            lootMoney = report.LootMoney,
            endedAtUtc = report.EndedAt
        });

        await notificationService.CreateAsync(
            defenderPlayerId,
            PlayerNotificationCode.BattleReportDefender,
            new PlayerNotificationDetails
            {
                NotificationKind = attackerWins ? "battle_report_defender_defeat" : "battle_report_defender_victory",
                MissionType = resultPrefix,
                Result = report.Result,
                AttackerPlayerId = attackerPlayerId,
                DefenderPlayerId = defenderPlayerId,
                AttackerPlayerName = attackerPlayer.Name,
                DefenderPlayerName = defenderPlayer.Name,
                WinnerPlayerName = attackerWins ? attackerPlayer.Name : defenderPlayer.Name,
                LoserPlayerName = attackerWins ? defenderPlayer.Name : attackerPlayer.Name,
                AttackerLosses = report.AttackerLosses,
                DefenderLosses = report.DefenderLosses,
                LootMoney = report.LootMoney,
                LootSummaryText = lootSummary.lost,
                EndedAtUtc = report.EndedAt ?? now
            },
            now, cancellationToken);
    }

    private async Task ResolveActiveSiegeAfterAidAsync(
        AppDbContext dbContext,
        PlayerMilitaryTransport aidTransport,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var activeSiege = await dbContext.PlayerSieges
            .FirstOrDefaultAsync(s => s.PlayersId == aidTransport.PlayerToId && s.Status == "active", cancellationToken);
        if (activeSiege == null)
        {
            return;
        }

        var siegeTransports = await dbContext.PlayerMilitaryTransports
            .Include(t => t.PlayerMilitaryTransportUnits)
            .Where(t => t.Status == "in_siege"
                        && t.PlayerToId == activeSiege.PlayersId
                        && t.PlayerFromId == activeSiege.AttackerPlayersId)
            .ToListAsync(cancellationToken);

        if (siegeTransports.Count == 0)
        {
            return;
        }

        var siegeUnits = siegeTransports
            .SelectMany(t => t.PlayerMilitaryTransportUnits)
            .Where(u => u.Quantity > 0)
            .ToList();

        if (siegeUnits.Count == 0)
        {
            return;
        }

        await ResolveBattleAsync(
            dbContext,
            null,
            activeSiege.AttackerPlayersId,
            activeSiege.PlayersId,
            siegeUnits,
            "siege",
            now,
            cancellationToken,
            lootRate: 0m,
            returnAttackerSurvivors: false);

        var remainingAttackerUnits = siegeTransports
            .SelectMany(t => t.PlayerMilitaryTransportUnits)
            .Sum(u => Math.Max(0, u.Quantity));

        if (remainingAttackerUnits > 0)
        {
            return;
        }

        foreach (var transport in siegeTransports)
        {
            transport.Status = "completed";
        }

        activeSiege.Status = "cancelled";
        activeSiege.EndedAt = now;
    }

    private static void ApplySiegeAttrition(
        IEnumerable<PlayerMilitaryTransport> siegeTransports,
        DateTime now)
    {
        foreach (var transport in siegeTransports)
        {
            var lastTickAt = transport.CreatedAt?.ToUniversalTime() ?? transport.StartTime;
            if (now <= lastTickAt)
            {
                continue;
            }

            var elapsed = now - lastTickAt;
            var elapsedTicks = (int)Math.Floor(elapsed.TotalHours / SiegeAttritionTick.TotalHours);
            if (elapsedTicks <= 0)
            {
                continue;
            }

            for (var tick = 0; tick < elapsedTicks; tick++)
            {
                foreach (var unit in transport.PlayerMilitaryTransportUnits.Where(u => u.Quantity > 0))
                {
                    var loss = (int)Math.Floor(unit.Quantity * SiegeHourlyAttritionRate);
                    if (loss <= 0)
                    {
                        loss = 1;
                    }

                    unit.Quantity = Math.Max(0, unit.Quantity - loss);
                }
            }

            transport.CreatedAt = lastTickAt.AddHours(elapsedTicks);
        }
    }


    private async Task TryResolveCountryWarConquestAsync(
        AppDbContext dbContext,
        Guid attackerPlayerId,
        Guid defenderPlayerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attackerCountryId = await dbContext.Players
            .Where(v => v.PlayersId == attackerPlayerId)
            .Select(v => v.CountriesId)
            .FirstOrDefaultAsync(cancellationToken);

        var defenderCountryId = await dbContext.Players
            .Where(v => v.PlayersId == defenderPlayerId)
            .Select(v => v.CountriesId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!attackerCountryId.HasValue || !defenderCountryId.HasValue || attackerCountryId == defenderCountryId)
        {
            return;
        }

        var war = await dbContext.CountryWars.FirstOrDefaultAsync(w => w.Status == "active"
            && ((w.AttackerCountriesId == attackerCountryId.Value && w.DefenderCountriesId == defenderCountryId.Value)
                || (w.AttackerCountriesId == defenderCountryId.Value && w.DefenderCountriesId == attackerCountryId.Value)), cancellationToken);
        if (war == null)
        {
            return;
        }

        if (now - war.StartedAt < MinimumWarDuration)
        {
            return;
        }

        var warAttackerId = war.AttackerCountriesId;
        var warDefenderId = war.DefenderCountriesId;

        var defenderHasUnits = await (from pm in dbContext.PlayerMilitaries
                                      join p in dbContext.Players on pm.PlayersId equals p.PlayersId
                                      where p.CountriesId == warDefenderId
                                            && pm.Quantity > 0
                                      select pm.PlayerMilitaryLogId)
            .AnyAsync(cancellationToken);
        if (!defenderHasUnits)
        {
            await ConquerCountryWarAsync(dbContext, war, now, "no_defenders", cancellationToken);
            return;
        }

        var defenderTotalPlayers = await dbContext.Players.CountAsync(v => v.CountriesId == warDefenderId, cancellationToken);
        if (defenderTotalPlayers <= 0)
        {
            await ConquerCountryWarAsync(dbContext, war, now, "no_defenders", cancellationToken);
            return;
        }

        var controlledPlayers = await dbContext.PlayerBattleReports
            .CountAsync(r => r.Result.Contains("attacker_win")
                && r.EndedAt >= war.StartedAt
                && dbContext.Players.Where(v => v.PlayersId == r.AttackerPlayersId).Select(v => v.CountriesId).FirstOrDefault() == warAttackerId
                && dbContext.Players.Where(v => v.PlayersId == r.DefenderPlayersId).Select(v => v.CountriesId).FirstOrDefault() == warDefenderId, cancellationToken);
        var progress = (controlledPlayers * 100m) / defenderTotalPlayers;
        if (progress < war.CaptureThresholdPercent)
        {
            return;
        }

        await ConquerCountryWarAsync(dbContext, war, now, "captured_75_percent", cancellationToken);

        var playersToNotify = await dbContext.Players
            .Where(v => v.CountriesId == warAttackerId || v.CountriesId == warDefenderId)
            .Select(v => v.PlayersId)
            .ToListAsync(cancellationToken);

        foreach (var playerId in playersToNotify)
        {
            await notificationService.CreateAsync(
                playerId,
                PlayerNotificationCode.CountryWarEnded,
                new PlayerNotificationDetails
                {
                    NotificationKind = "country_borders_changed"
                },
                cancellationToken: cancellationToken);
        }
    }

    private static async Task ConquerCountryWarAsync(
        AppDbContext dbContext,
        CountryWar war,
        DateTime now,
        string endReason,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(war.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await CancelCountryWarMilitaryActionsAsync(
            dbContext,
            war.AttackerCountriesId,
            war.DefenderCountriesId,
            now,
            cancellationToken);

        var defenderPlayers = await dbContext.Players
            .Where(p => p.CountriesId == war.DefenderCountriesId)
            .ToListAsync(cancellationToken);

        foreach (var player in defenderPlayers)
        {
            player.CountriesId = war.AttackerCountriesId;
        }

        war.Status = "ended";
        war.EndReason = endReason;
        war.EndedAt = now;
        war.ConqueredFromCountriesId = war.DefenderCountriesId;
    }

    private static async Task CancelCountryWarMilitaryActionsAsync(
        AppDbContext dbContext,
        Guid attackerCountryId,
        Guid defenderCountryId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attackerPlayerIds = await dbContext.Players
            .Where(p => p.CountriesId == attackerCountryId)
            .Select(p => p.PlayersId)
            .ToListAsync(cancellationToken);

        var defenderPlayerIds = await dbContext.Players
            .Where(p => p.CountriesId == defenderCountryId)
            .Select(p => p.PlayersId)
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
            .Include(t => t.PlayerMilitaryTransportUnits)
            .Where(t => (t.Status == "in_progress" || t.Status == "in_siege")
                && (t.MissionType == "attack" || t.MissionType == "siege")
                && t.PlayerToId.HasValue
                && ((firstSidePlayerIds.Contains(t.PlayerFromId) && secondSidePlayerIds.Contains(t.PlayerToId.Value))
                    || (secondSidePlayerIds.Contains(t.PlayerFromId) && firstSidePlayerIds.Contains(t.PlayerToId.Value))))
            .ToListAsync(cancellationToken);

        foreach (var transport in transports)
        {
            await ReturnSurvivorsAsync(
                dbContext,
                transport.PlayerFromId,
                transport.PlayerMilitaryTransportUnits.ToList(),
                cancellationToken);

            transport.Status = "cancelled";
            transport.EndTime = now;
        }

        var activeSieges = await dbContext.PlayerSieges
            .Where(s => s.Status == "active"
                && ((firstSidePlayerIds.Contains(s.AttackerPlayersId) && secondSidePlayerIds.Contains(s.PlayersId))
                    || (secondSidePlayerIds.Contains(s.AttackerPlayersId) && firstSidePlayerIds.Contains(s.PlayersId))))
            .ToListAsync(cancellationToken);

        foreach (var siege in activeSieges)
        {
            siege.Status = "cancelled";
            siege.EndedAt = now;
        }
    }

    private async Task<decimal> LootResourcesAsync(
        AppDbContext dbContext,
        Guid attackerPlayerId,
        Guid defenderPlayerId,
        List<PlayerBattleReportProduct> lootProducts,
        decimal lootRate,
        CancellationToken cancellationToken)
    {
        var defender = await dbContext.Players.FirstOrDefaultAsync(v => v.PlayersId == defenderPlayerId, cancellationToken);
        var attacker = await dbContext.Players.FirstOrDefaultAsync(v => v.PlayersId == attackerPlayerId, cancellationToken);

        if (defender == null || attacker == null)
        {
            return 0m;
        }

        var lootMoney = Math.Min(defender.Money, Math.Round(defender.Money * lootRate, 2));
        if (lootMoney > 0)
        {
                defender.Money -= lootMoney;
                attacker.Money += lootMoney;
                attacker.Score += lootMoney * AttackerLootScoreMultiplier;
                defender.Score = Math.Max(0m, defender.Score - (lootMoney * DefenderLootScorePenaltyMultiplier));
            }

        var defenderInventory = await dbContext.PlayerInventories
            .Where(i => i.PlayersId == defenderPlayerId && i.Quantity > 0)
            .ToListAsync(cancellationToken);

        var attackerInventory = await dbContext.PlayerInventories
            .Where(i => i.PlayersId == attackerPlayerId)
            .ToListAsync(cancellationToken);

        foreach (var item in defenderInventory)
        {
            var lootQuantity = (int)Math.Floor(item.Quantity * lootRate);
            lootQuantity = Math.Min(item.Quantity, lootQuantity);
            if (lootQuantity <= 0)
            {
                continue;
            }

            item.Quantity -= lootQuantity;
            item.UpdatedAt = DateTime.UtcNow;
            if (item.Quantity <= 0)
            {
                dbContext.PlayerInventories.Remove(item);
            }

            var attackerItem = attackerInventory.FirstOrDefault(i => i.ProductsId == item.ProductsId);
            if (attackerItem == null)
            {
                attackerItem = new PlayerInventory
                {
                    PlayerInventoriesId = Guid.NewGuid(),
                    PlayersId = attackerPlayerId,
                    ProductsId = item.ProductsId,
                    Quantity = lootQuantity,
                    UpdatedAt = DateTime.UtcNow
                };
                attackerInventory.Add(attackerItem);
                dbContext.PlayerInventories.Add(attackerItem);
            }
            else
            {
                attackerItem.Quantity += lootQuantity;
                attackerItem.UpdatedAt = DateTime.UtcNow;
            }

            lootProducts.Add(new PlayerBattleReportProduct
            {
                PlayerBattleReportProductsId = Guid.NewGuid(),
                ProductsId = item.ProductsId,
                Quantity = lootQuantity
            });
        }

        return lootMoney;
    }

    private async Task ApplySiegeLootAsync(
        AppDbContext dbContext,
        Guid attackerPlayerId,
        Guid defenderPlayerId,
        int hourlyTicks,
        CancellationToken cancellationToken)
    {
        if (hourlyTicks <= 0)
        {
            return;
        }

        var defender = await dbContext.Players.FirstOrDefaultAsync(v => v.PlayersId == defenderPlayerId, cancellationToken);
        var attacker = await dbContext.Players.FirstOrDefaultAsync(v => v.PlayersId == attackerPlayerId, cancellationToken);
        if (defender == null || attacker == null)
        {
            return;
        }

        var defenderInventory = await dbContext.PlayerInventories
            .Where(i => i.PlayersId == defenderPlayerId && i.Quantity > 0)
            .ToListAsync(cancellationToken);

        var attackerInventory = await dbContext.PlayerInventories
            .Where(i => i.PlayersId == attackerPlayerId)
            .ToListAsync(cancellationToken);

        var totalMoneyLoot = 0m;
        var totalProductsLoot = new Dictionary<Guid, int>();

        for (var tick = 0; tick < hourlyTicks; tick++)
        {
            var moneyLoot = Math.Min(defender.Money, Math.Round(defender.Money * SiegeHourlyLootRate, 2));
            if (moneyLoot > 0)
            {
                defender.Money -= moneyLoot;
                attacker.Money += moneyLoot;
                attacker.Score += moneyLoot * AttackerLootScoreMultiplier;
                defender.Score = Math.Max(0m, defender.Score - (moneyLoot * DefenderLootScorePenaltyMultiplier));
                totalMoneyLoot += moneyLoot;
            }

            foreach (var item in defenderInventory.Where(i => i.Quantity > 0))
            {
                var lootQuantity = (int)Math.Floor(item.Quantity * SiegeHourlyLootRate);
                lootQuantity = Math.Min(item.Quantity, lootQuantity);
                if (lootQuantity <= 0)
                {
                    continue;
                }

                item.Quantity -= lootQuantity;
                item.UpdatedAt = DateTime.UtcNow;
                if (item.Quantity <= 0)
                {
                    dbContext.PlayerInventories.Remove(item);
                }

                var attackerItem = attackerInventory.FirstOrDefault(i => i.ProductsId == item.ProductsId);
                if (attackerItem == null)
                {
                    attackerItem = new PlayerInventory
                    {
                        PlayerInventoriesId = Guid.NewGuid(),
                        PlayersId = attackerPlayerId,
                        ProductsId = item.ProductsId,
                        Quantity = lootQuantity,
                        UpdatedAt = DateTime.UtcNow
                    };
                    attackerInventory.Add(attackerItem);
                    dbContext.PlayerInventories.Add(attackerItem);
                }
                else
                {
                    attackerItem.Quantity += lootQuantity;
                    attackerItem.UpdatedAt = DateTime.UtcNow;
                }

                if (totalProductsLoot.TryGetValue(item.ProductsId, out var existingLoot))
                {
                    totalProductsLoot[item.ProductsId] = existingLoot + lootQuantity;
                }
                else
                {
                    totalProductsLoot[item.ProductsId] = lootQuantity;
                }
            }
        }

        if (totalMoneyLoot <= 0m && totalProductsLoot.Count == 0)
        {
            return;
        }

        var lootProducts = totalProductsLoot
            .Where(pair => pair.Value > 0)
            .Select(pair => new PlayerBattleReportProduct
            {
                ProductsId = pair.Key,
                Quantity = pair.Value
            })
            .ToList();

        var lootSummary = await BuildLootSummaryAsync(dbContext, totalMoneyLoot, lootProducts, cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            notificationKind = "siege_loot_gained",
            missionType = "siege",
            attackerPlayerId,
            defenderPlayerId,
            lootMoney = totalMoneyLoot,
            lootProducts = totalProductsLoot
        });

        var siegeLootNotificationTime = DateTime.UtcNow;

        await notificationService.CreateAsync(
            attackerPlayerId,
            PlayerNotificationCode.BattleReportAttacker,
            new PlayerNotificationDetails
            {
                NotificationKind = "siege_loot_gained",
                MissionType = "siege",
                AttackerPlayerId = attackerPlayerId,
                DefenderPlayerId = defenderPlayerId,
                LootMoney = totalMoneyLoot,
                LootSummaryText = lootSummary.gained,
                EndedAtUtc = siegeLootNotificationTime
            },
            siegeLootNotificationTime, cancellationToken);

        var defenderPayload = JsonSerializer.Serialize(new
        {
            notificationKind = "siege_loot_lost",
            missionType = "siege",
            attackerPlayerId,
            defenderPlayerId,
            lootMoney = totalMoneyLoot,
            lootProducts = totalProductsLoot
        });

        await notificationService.CreateAsync(
            defenderPlayerId,
            PlayerNotificationCode.BattleReportDefender,
            new PlayerNotificationDetails
            {
                NotificationKind = "siege_loot_lost",
                MissionType = "siege",
                AttackerPlayerId = attackerPlayerId,
                DefenderPlayerId = defenderPlayerId,
                LootMoney = totalMoneyLoot,
                LootSummaryText = lootSummary.lost,
                EndedAtUtc = siegeLootNotificationTime
            },
            siegeLootNotificationTime, cancellationToken);
    }

    private async Task<(string gained, string lost)> BuildLootSummaryAsync(
        AppDbContext dbContext,
        decimal lootMoney,
        IEnumerable<PlayerBattleReportProduct> lootProducts,
        CancellationToken cancellationToken)
    {
        var products = lootProducts
            .Where(p => p.Quantity > 0)
            .GroupBy(p => p.ProductsId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(p => p.Quantity) })
            .ToList();
        var productIds = products
            .Select(p => p.ProductId)
            .Distinct()
            .ToList();

        var productNames = productIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await dbContext.Products
                .Select(p => new { p.ProductsId, p.Code })
                .ToListAsync(cancellationToken))
                .Where(p => productIds.Contains(p.ProductsId))
                .ToDictionary(p => p.ProductsId, p => string.IsNullOrWhiteSpace(p.Code) ? p.ProductsId.ToString() : p.Code);

        var productSummary = products.Count == 0
            ? "brak produktów"
            : string.Join(", ", products.Select(p =>
            {
                var name = productNames.TryGetValue(p.ProductId, out var code) ? code : p.ProductId.ToString();
                return $"{name} x{p.Quantity}";
            }));

        var moneySummary = $"{lootMoney:N2} monet";
        return ($"{moneySummary}; {productSummary}", $"{moneySummary}; {productSummary}");
    }

    private static int CalculateAttackerPower<TUnit>(IEnumerable<TUnit> units)
    {
        return units.Sum(unit => Math.Max(0, ResolveCombatDefinition(unit).OffensivePoints) * Math.Max(0, GetUnitQuantity(unit)));
    }

    private static int CalculateDefenderPower<TUnit>(IEnumerable<TUnit> units)
    {
        return units.Sum(unit => Math.Max(0, ResolveCombatDefinition(unit).DefensivePoints) * Math.Max(0, GetUnitQuantity(unit)));
    }

    private static int CalculateLosses(int totalUnits, int ownPower, int enemyPower, bool winner)
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

    private static int ApplyLosses<TUnit>(
        List<TUnit> units,
        int lossTarget,
        DbSet<TUnit> dbSet) where TUnit : class
    {
        var remaining = lossTarget;

        foreach (var unit in units.OrderByDescending(GetUnitLevel))
        {
            if (remaining <= 0)
            {
                break;
            }

            var quantity = GetUnitQuantity(unit);
            var loss = Math.Min(quantity, remaining);
            SetUnitQuantity(unit, quantity - loss);
            remaining -= loss;

            if (GetUnitQuantity(unit) <= 0)
            {
                dbSet.Remove(unit);
            }
        }

        return lossTarget - remaining;
    }

    private static void ApplySimulationLosses<TUnit>(
        List<TUnit> units,
        IReadOnlyList<BattleSimulationUnitInput> killedUnits,
        DbSet<TUnit> dbSet) where TUnit : class
    {
        foreach (var killedUnit in killedUnits)
        {
            var remaining = killedUnit.Quantity;
            foreach (var unit in units
                         .Where(unit => GetUnitLevel(unit) == killedUnit.Level
                                        && string.Equals(GetUnitCode(unit), killedUnit.UnitCode, StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(GetUnitLevel))
            {
                if (remaining <= 0)
                {
                    break;
                }

                var quantity = GetUnitQuantity(unit);
                var loss = Math.Min(quantity, remaining);
                SetUnitQuantity(unit, quantity - loss);
                remaining -= loss;

                if (GetUnitQuantity(unit) <= 0)
                {
                    dbSet.Remove(unit);
                }
            }
        }
    }

    private static int GetUnitLevel<TUnit>(TUnit unit)
    {
        return unit switch
        {
            PlayerMilitaryTransportUnit transportUnit => transportUnit.Level,
            PlayerMilitary playerUnit => playerUnit.Level,
            _ => 1
        };
    }

    private static int GetUnitQuantity<TUnit>(TUnit unit)
    {
        return unit switch
        {
            PlayerMilitaryTransportUnit transportUnit => transportUnit.Quantity,
            PlayerMilitary playerUnit => playerUnit.Quantity,
            _ => 0
        };
    }

    private static string GetUnitCode<TUnit>(TUnit unit)
    {
        return unit switch
        {
            PlayerMilitaryTransportUnit transportUnit => transportUnit.MilitaryUnitsCode,
            PlayerMilitary playerUnit => playerUnit.MilitaryUnitsCode,
            _ => string.Empty
        };
    }

    private static void SetUnitQuantity<TUnit>(TUnit unit, int quantity)
    {
        switch (unit)
        {
            case PlayerMilitaryTransportUnit transportUnit:
                transportUnit.Quantity = quantity;
                break;
            case PlayerMilitary playerUnit:
                playerUnit.Quantity = quantity;
                break;
        }
    }

    private static string GetCombatFamily<TUnit>(TUnit unit)
    {
        var code = GetUnitCode(unit);
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        return CombatBalanceEngine.NormalizeCombatFamily(code);
    }

    private static MilitaryUnit ResolveCombatDefinition<TUnit>(TUnit unit)
    {
        var code = GetUnitCode(unit);
        if (!string.IsNullOrWhiteSpace(code) &&
            CombatUnitDefinitions.TryGetValue(code.Trim(), out var definition))
        {
            return definition;
        }

        var fallbackDefinitions = StaticData.AllMilitaryUnits
            .OrderBy(definition => definition.ProductionCost)
            .ThenBy(definition => definition.OffensivePoints)
            .ThenBy(definition => definition.Code.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var level = Math.Max(1, GetUnitLevel(unit));
        return fallbackDefinitions[Math.Min(level - 1, fallbackDefinitions.Count - 1)];
    }

    private static string? GetUnitCode<TUnit>(TUnit unit)
    {
        return unit switch
        {
            PlayerMilitaryTransportUnit transportUnit => transportUnit.MilitaryUnitsCode,
            PlayerMilitary playerUnit => playerUnit.MilitaryUnitsCode,
            _ => null
        };
    }

    private static async Task ReturnSurvivorsAsync(
        AppDbContext dbContext,
        Guid attackerPlayerId,
        ICollection<PlayerMilitaryTransportUnit> attackerUnits,
        CancellationToken cancellationToken)
    {
        var attackerPlayer = await dbContext.Players.FirstOrDefaultAsync(v => v.PlayersId == attackerPlayerId, cancellationToken);
        if (attackerPlayer == null)
        {
            return;
        }

        var roster = await dbContext.PlayerMilitaries
            .Where(entry => entry.PlayersId == attackerPlayerId)
            .ToListAsync(cancellationToken);

        foreach (var unit in attackerUnits.Where(u => u.Quantity > 0))
        {
            var rosterEntry = roster.FirstOrDefault(r => r.Level == unit.Level);
            if (rosterEntry == null)
            {
                dbContext.PlayerMilitaries.Add(new PlayerMilitary
                {
                    PlayerMilitaryLogId = Guid.NewGuid(),
                    PlayersId = attackerPlayerId,
                    Level = unit.Level,
                    Quantity = unit.Quantity
                });
            }
            else
            {
                rosterEntry.Quantity += unit.Quantity;
            }

            unit.Quantity = 0;
        }
    }
}
