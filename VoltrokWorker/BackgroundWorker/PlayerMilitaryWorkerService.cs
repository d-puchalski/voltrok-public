using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using VoltrokEF;
using VoltrokServices.Utils;

namespace VoltrokWorker.BackgroundWorker;

public class PlayerMilitaryWorkerService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration) : BackgroundService
{
    private int ProcessCooldownMinutes => Math.Max(1, configuration.GetValue("BackgroundWorker:PlayerMilitaryBreakMinutes", 1440));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Player military worker started. Interval: {ProcessCooldownMinutes}m.", ProcessCooldownMinutes);
        var cycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            cycle++;
            var startedAt = DateTime.UtcNow;
            Log.Information("Player military worker cycle {Cycle} started.", cycle);

            try
            {
                await ProcessPlayerMilitaryAsync(stoppingToken);
                Log.Information(
                    "Player military worker cycle {Cycle} finished in {DurationMs} ms.",
                    cycle,
                    (DateTime.UtcNow - startedAt).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while processing regional military recruitment");
            }

            try
            {
                Log.Information("Player military worker sleeping for {ProcessCooldownMinutes}m.", ProcessCooldownMinutes);
                await Task.Delay(TimeSpan.FromMinutes(ProcessCooldownMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Player military worker stopping due to cancellation.");
                break;
            }
        }
    }

    private async Task ProcessPlayerMilitaryAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var players = await dbContext.Players
            .Include(v => v.PlayerMilitaryOrders)
            .ThenInclude(o => o.MilitaryUnits)
            .Include(v => v.PlayerNeeds)
            .ThenInclude(n => n.Needs)
            .Include(v => v.PlayerSiegePlayers)
            .ToListAsync(cancellationToken);

        if (players.Count == 0)
        {
            return;
        }

        var playerIdSet = players
            .Select(v => v.PlayersId)
            .Distinct()
            .ToHashSet();

        // MySql.EntityFrameworkCore has issues mapping local Guid collections in SQL IN clauses.
        // Load once and filter in memory to keep compatibility across provider versions.
        var existingRoster = (await dbContext.PlayerMilitaries.ToListAsync(cancellationToken))
            .Where(entry => playerIdSet.Contains(entry.PlayersId))
            .ToList();

        var orderedMilitaryUnits = await dbContext.MilitaryUnits
            .AsNoTracking()
            .OrderBy(unit => unit.ProductionCost)
            .ThenBy(unit => unit.AttackPoints)
            .ThenBy(unit => unit.Code)
            .Select(unit => unit.MilitaryUnitsId)
            .ToListAsync(cancellationToken);

        var rosterLevelByUnitId = orderedMilitaryUnits
            .Select((unitId, index) => new { unitId, RosterLevel = index + 1 })
            .ToDictionary(item => item.unitId, item => item.RosterLevel);

        // Protect recruitment loop from historical duplicates in DB by normalizing roster in memory.
        var rosterByPlayerLevel = existingRoster
            .GroupBy(entry => (entry.PlayersId, entry.Level))
            .ToDictionary(group => group.Key, group =>
            {
                var first = group.First();
                if (group.Count() == 1)
                {
                    return first;
                }

                first.Quantity = group.Sum(entry => entry.Quantity);
                foreach (var duplicate in group.Skip(1))
                {
                    dbContext.PlayerMilitaries.Remove(duplicate);
                }

                return first;
            });

        var hasChanges = false;

        foreach (var player in players)
        {
            var hasActiveDefensiveSiege = player.PlayerSiegePlayers.Any(s => s.Status == "active");
            if (hasActiveDefensiveSiege)
            {
                continue;
            }

            var needEffects = PlayerNeedEffectCalculator.Calculate(player.PlayerNeeds);
            var activeOrders = player.PlayerMilitaryOrders
                .Where(order => order.Quantity > order.ProducedQuantity)
                .OrderBy(order => order.CreatedAt)
                .ToList();

            if (activeOrders.Count == 0)
            {
                continue;
            }

            foreach (var order in activeOrders)
            {
                var militaryUnit = order.MilitaryUnits;
                if (militaryUnit == null)
                {
                    continue;
                }

                if (!rosterLevelByUnitId.TryGetValue(militaryUnit.MilitaryUnitsId, out var rosterLevel))
                {
                    continue;
                }

                var remaining = order.Quantity - order.ProducedQuantity;
                if (remaining <= 0)
                {
                    continue;
                }

                var timeSeconds = Math.Max(1, militaryUnit.TimeSeconds);
                var now = DateTime.UtcNow;
                var createdAt = NormalizeUtcTimestamp(order.CreatedAt, now);
                var elapsedSeconds = Math.Max(0, (now - createdAt).TotalSeconds);
                var effectiveElapsedSeconds = elapsedSeconds * (double)needEffects.MilitaryRecruitmentSpeedMultiplier;
                var producibleByTime = (int)Math.Floor(effectiveElapsedSeconds / timeSeconds);
                var readyByTime = producibleByTime - order.ProducedQuantity;

                if (readyByTime <= 0)
                {
                    continue;
                }

                var producedNow = Math.Min(remaining, readyByTime);
                if (producedNow <= 0)
                {
                    continue;
                }

                order.ProducedQuantity += producedNow;
                var rosterKey = (player.PlayersId, rosterLevel);
                if (!rosterByPlayerLevel.TryGetValue(rosterKey, out var rosterEntry))
                {
                    rosterEntry = new PlayerMilitary
                    {
                        PlayerMilitaryLogId = Guid.NewGuid(),
                        PlayersId = player.PlayersId,
                        Level = rosterLevel,
                        Quantity = 0
                    };

                    await dbContext.PlayerMilitaries.AddAsync(rosterEntry, cancellationToken);
                    rosterByPlayerLevel[rosterKey] = rosterEntry;
                }

                rosterEntry.Quantity += producedNow;

                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static DateTime NormalizeUtcTimestamp(DateTime? value, DateTime fallbackUtc)
    {
        if (!value.HasValue)
        {
            return fallbackUtc;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
