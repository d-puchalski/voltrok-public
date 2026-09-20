using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;

using VoltrokEF;
using VoltrokServices.Services.Badges;

namespace VoltrokWorker.BackgroundWorker;

public class BadgeRefreshWorkerService(IDbContextFactory<AppDbContext> dbContextFactory, PlayerBadgeService playerBadgeService) : BackgroundService
{
    private const string RunType = "badge_refresh";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                if (!await IsDailyRefreshAlreadyCompletedAsync(nowUtc, stoppingToken))
                {
                    await playerBadgeService.RefreshBadgesAsync(stoppingToken);
                    await MarkDailyRefreshCompletedAsync(nowUtc, stoppingToken);
                    Log.Information("Badge refresh completed at {NowUtc}.", nowUtc);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while refreshing player badges");
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

    private async Task<bool> IsDailyRefreshAlreadyCompletedAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var dayStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await dbContext.BackgroundWorkerLastRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RunType == RunType, cancellationToken);

        return state is not null && ToUtc(state.LastExecutedAtUtc) >= dayStartUtc;
    }

    private async Task MarkDailyRefreshCompletedAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await dbContext.BackgroundWorkerLastRuns
            .FirstOrDefaultAsync(x => x.RunType == RunType, cancellationToken);

        if (state is null)
        {
            dbContext.BackgroundWorkerLastRuns.Add(new BackgroundWorkerLastRun
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
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another instance may insert/update the marker at the same time.
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
