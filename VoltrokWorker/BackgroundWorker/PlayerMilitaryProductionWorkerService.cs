using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

using VoltrokEF;
using VoltrokServices.Services.Premium;
using VoltrokUtils.Engines;
using VoltrokUtils.Enums;

namespace VoltrokWorker.BackgroundWorker;

public class PlayerMilitaryProductionWorkerService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration) : BackgroundService
{
    private int ProcessCooldownMinutes => Math.Max(1, configuration.GetValue("BackgroundWorker:PlayerMilitaryBreakMinutes", 1));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Player military production worker started. Interval: {ProcessCooldownMinutes}m.", ProcessCooldownMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while processing military recruitment queue.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(ProcessCooldownMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var activeOrders = await dbContext.PlayerMilitaryProductions
            .Where(order => order.Quantity > order.ProducedQuantity)
            .OrderBy(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

        if (activeOrders.Count == 0)
        {
            return;
        }

        var blockedPlayerIds = await dbContext.PlayerSieges
            .AsNoTracking()
            .Where(siege => siege.Status == PlayerSiegesStatus.Active.ToDbValue())
            .Select(siege => siege.PlayerToId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var blockedPlayerIdSet = blockedPlayerIds.ToHashSet();
        var playerIds = activeOrders.Select(order => order.PlayersId).Distinct().ToList();
        var now = DateTime.UtcNow;
        var premiumPlayerIds = await dbContext.Players
            .AsNoTracking()
            .Where(player => player.BattlePassValidTill > now && playerIds.Contains(player.PlayersId))
            .Select(player => player.PlayersId)
            .ToListAsync(cancellationToken);
        var premiumPlayerIdSet = premiumPlayerIds.ToHashSet();
        var existingUnits = await dbContext.PlayerMilitaryUnits
            .Where(unit => playerIds.Contains(unit.PlayersId))
            .ToListAsync(cancellationToken);

        var unitsByKey = existingUnits.ToDictionary(
            unit => (unit.PlayersId, Code: unit.MilitaryUnitsCode.ToLowerInvariant(), Level: Math.Max(1, unit.Level)));

        var militaryDefinitions = MilitaryUnitEngine.MilitaryUnitsAllData.ToDictionary(
            unit => unit.Code.ToString().ToLowerInvariant(),
            unit => unit);

        var hasChanges = false;

        foreach (var order in activeOrders)
        {
            if (blockedPlayerIdSet.Contains(order.PlayersId))
            {
                continue;
            }

            var code = order.MilitaryUnitsCode?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(code) || !militaryDefinitions.TryGetValue(code, out var unitDefinition))
            {
                continue;
            }

            var remaining = order.Quantity - order.ProducedQuantity;
            if (remaining <= 0)
            {
                continue;
            }

            var createdAt = NormalizeUtcTimestamp(order.CreatedAt, now);
            var elapsedSeconds = Math.Max(0, (now - createdAt).TotalSeconds);
            var productionSpeedMultiplier = premiumPlayerIdSet.Contains(order.PlayersId)
                ? 5m
                : 1m;
            var productionTimeSeconds = Math.Max(1, (int)Math.Ceiling(unitDefinition.ProductionTime / productionSpeedMultiplier));
            var producibleByTime = (int)Math.Floor(elapsedSeconds / productionTimeSeconds);
            var readyByTime = producibleByTime - order.ProducedQuantity;

            if (readyByTime <= 0)
            {
                continue;
            }

            var producedNow = Math.Min(remaining, readyByTime);


            order.ProducedQuantity += producedNow;

            var targetLevel = Math.Max(1, order.Level);
            var unitKey = (order.PlayersId, code, targetLevel);
            if (!unitsByKey.TryGetValue(unitKey, out var playerUnit))
            {
                playerUnit = new PlayerMilitaryUnit
                {
                    PlayerMilitaryUnitsId = Guid.NewGuid(),
                    PlayersId = order.PlayersId,
                    MilitaryUnitsCode = order.MilitaryUnitsCode,
                    Level = targetLevel,
                    Quantity = 0,
                    CreatedAt = now
                };

                await dbContext.PlayerMilitaryUnits.AddAsync(playerUnit, cancellationToken);
                unitsByKey[unitKey] = playerUnit;
            }

            playerUnit.Quantity += producedNow;
            hasChanges = true;
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
