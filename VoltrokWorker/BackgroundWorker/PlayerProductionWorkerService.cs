using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using VoltrokEF;
using VoltrokServices.Utils;

namespace VoltrokWorker.BackgroundWorker;

public class PlayerProductionWorkerService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration) : BackgroundService
{
    private int ProcessCooldownMinutes => Math.Max(1, configuration.GetValue("BackgroundWorker:PlayerProductionBreakMinutes", 5));
    private const int MaxConcurrencyRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Player production worker started. Interval: {ProcessCooldownMinutes}m.", ProcessCooldownMinutes);
        var cycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            cycle++;
            var startedAt = DateTime.UtcNow;
            Log.Information("Player production worker cycle {Cycle} started.", cycle);

            try
            {
                await ProcessPlayerProductionAsync(stoppingToken);
                Log.Information(
                    "Player production worker cycle {Cycle} finished in {DurationMs} ms.",
                    cycle,
                    (DateTime.UtcNow - startedAt).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while processing player production");
            }

            try
            {
                Log.Information("Player production worker sleeping for {ProcessCooldownMinutes}m.", ProcessCooldownMinutes);
                await Task.Delay(TimeSpan.FromMinutes(ProcessCooldownMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Player production worker stopping due to cancellation.");
                break;
            }
        }
    }

    private async Task ProcessPlayerProductionAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var playerIds = await dbContext.Players
            .AsNoTracking()
            .Where(player =>
                player.PlayerProductionOrders.Any(order => order.Quantity > order.ProducedQuantity) &&
                !player.PlayerSiegePlayers.Any(siege => siege.Status == "active"))
            .Select(player => player.PlayersId)
            .ToListAsync(cancellationToken);

        foreach (var playerId in playerIds)
        {
            await ProcessPlayerProductionForPlayerAsync(playerId, cancellationToken);
        }
    }

    private async Task ProcessPlayerProductionForPlayerAsync(Guid playerId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            try
            {
                var player = await dbContext.Players
                    .Include(v => v.PlayerProductionOrders)
                    .ThenInclude(o => o.Products)
                    .Include(v => v.PlayerInventories)
                    .Include(v => v.PlayerNeeds)
                    .ThenInclude(n => n.Needs)
                    .Include(v => v.PlayerSiegePlayers)
                    .FirstOrDefaultAsync(v => v.PlayersId == playerId, cancellationToken);

                if (player == null)
                {
                    return;
                }

                var hasActiveDefensiveSiege = player.PlayerSiegePlayers.Any(s => s.Status == "active");
                if (hasActiveDefensiveSiege)
                {
                    return;
                }

                var needEffects = PlayerNeedEffectCalculator.Calculate(player.PlayerNeeds);
                var activeOrders = player.PlayerProductionOrders
                    .Where(order => order.Quantity > order.ProducedQuantity)
                    .OrderBy(order => order.CreatedAt)
                    .ToList();

                if (activeOrders.Count == 0)
                {
                    return;
                }

                var hasChanges = false;

                foreach (var order in activeOrders)
                {
                    var remaining = order.Quantity - order.ProducedQuantity;
                    if (remaining <= 0)
                    {
                        continue;
                    }

                    var product = order.Products;
                    if (product == null)
                    {
                        continue;
                    }

                    var timeSeconds = Math.Max(1, product.TimeSeconds);
                    var createdAt = order.CreatedAt?.ToUniversalTime() ?? DateTime.UtcNow;
                    var elapsedSeconds = Math.Max(0, (DateTime.UtcNow - createdAt).TotalSeconds);
                    var effectiveElapsedSeconds = elapsedSeconds * (double)needEffects.ProductionSpeedMultiplier;
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
                    var inventoryItem = player.PlayerInventories.FirstOrDefault(i => i.ProductsId == order.ProductsId);
                    if (inventoryItem == null)
                    {
                        inventoryItem = new PlayerInventory
                        {
                            PlayerInventoriesId = Guid.NewGuid(),
                            PlayersId = player.PlayersId,
                            ProductsId = order.ProductsId,
                            Quantity = producedNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        player.PlayerInventories.Add(inventoryItem);
                        await dbContext.PlayerInventories.AddAsync(inventoryItem, cancellationToken);
                    }
                    else
                    {
                        inventoryItem.Quantity += producedNow;
                        inventoryItem.UpdatedAt = DateTime.UtcNow;
                    }

                    hasChanges = true;
                }

                if (hasChanges)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);

                }

                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < MaxConcurrencyRetries && !cancellationToken.IsCancellationRequested)
            {
                var delayMs = 50 * attempt + Random.Shared.Next(25, 100);
                Log.Warning(
                    ex,
                    "Player production concurrency conflict for player {PlayerId} on attempt {Attempt}/{MaxAttempts}. Retrying with a fresh DbContext after {DelayMs}ms.",
                    playerId,
                    attempt,
                    MaxConcurrencyRetries,
                    delayMs);

                await Task.Delay(delayMs, cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            { 
                Log.Warning(
                    ex,
                    "Player production skipped for player {PlayerId} after {MaxAttempts} concurrency retries in this cycle.",
                    playerId,
                    MaxConcurrencyRetries);
                return;
            }
        }
    }
}
