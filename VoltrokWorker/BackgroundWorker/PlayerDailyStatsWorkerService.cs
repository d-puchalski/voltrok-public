using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VoltrokEF;

namespace VoltrokWorker.BackgroundWorker;

public sealed class PlayerDailyStatsWorkerService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<PlayerDailyStatsWorkerService> logger) : BackgroundService
{
    private const string RunType = "player_daily_stats_snapshot";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                if (!await IsDailySnapshotAlreadyCompletedAsync(nowUtc, stoppingToken))
                {
                    await SaveDailySnapshotAsync(nowUtc, stoppingToken);
                    await MarkDailySnapshotCompletedAsync(nowUtc, stoppingToken);
                    logger.LogInformation("Player daily stats snapshot completed at {NowUtc}.", nowUtc);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PlayerDailyStatsWorkerService tick failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SaveDailySnapshotAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var statsDate = DateOnly.FromDateTime(nowUtc.Date);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var players = await db.Players
            .AsNoTracking()
            .Select(player => new
            {
                player.PlayersId,
                player.Money,
                player.Score,
                player.Oil,
                player.Uranium,
                player.Chips
            })
            .ToListAsync(cancellationToken);

        var armyTotals = await db.PlayerMilitaryUnits
            .AsNoTracking()
            .GroupBy(unit => unit.PlayersId)
            .Select(group => new
            {
                PlayerId = group.Key,
                ArmyTotal = group.Sum(unit => unit.Quantity)
            })
            .ToDictionaryAsync(row => row.PlayerId, row => row.ArmyTotal, cancellationToken);

        var existingStats = await db.PlayerDailyStats
            .Where(stat => stat.StatsDate == statsDate)
            .ToDictionaryAsync(stat => stat.PlayersId, cancellationToken);

        foreach (var player in players)
        {
            var armyTotal = armyTotals.GetValueOrDefault(player.PlayersId, 0);
            if (existingStats.TryGetValue(player.PlayersId, out var existingStat))
            {
                existingStat.Money = player.Money;
                existingStat.Score = player.Score;
                existingStat.Oil = player.Oil;
                existingStat.Uranium = player.Uranium;
                existingStat.Chips = player.Chips;
                existingStat.ArmyTotal = armyTotal;
                continue;
            }

            db.PlayerDailyStats.Add(new PlayerDailyStat
            {
                PlayersId = player.PlayersId,
                StatsDate = statsDate,
                Money = player.Money,
                Score = player.Score,
                Oil = player.Oil,
                Uranium = player.Uranium,
                Chips = player.Chips,
                ArmyTotal = armyTotal
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> IsDailySnapshotAlreadyCompletedAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var dayStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await db.BackgroundWorkerLastRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RunType == RunType, cancellationToken);

        return state is not null && ToUtc(state.LastExecutedAtUtc) >= dayStartUtc;
    }

    private async Task MarkDailySnapshotCompletedAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await db.BackgroundWorkerLastRuns
            .FirstOrDefaultAsync(x => x.RunType == RunType, cancellationToken);

        if (state is null)
        {
            db.BackgroundWorkerLastRuns.Add(new BackgroundWorkerLastRun
            {
                RunType = RunType,
                LastExecutedAtUtc = nowUtc
            });
        }
        else
        {
            state.LastExecutedAtUtc = nowUtc;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another worker instance may have updated the daily marker first.
        }
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
