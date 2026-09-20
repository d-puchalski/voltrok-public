using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;

using VoltrokEF;
using VoltrokServices.Services.Countries;

namespace VoltrokWorker.BackgroundWorker;

public class ElectionWorkerService(IDbContextFactory<AppDbContext> dbContextFactory, CountryGovernanceService countryGovernanceService) : BackgroundService
{
    private const string RunType = "automatic_president_selection";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                if (!await IsDailyRefreshAlreadyCompletedAsync(nowUtc, stoppingToken))
                {
                    await RunAutomaticPresidentSelectionAsync(stoppingToken);
                    await MarkDailyRefreshCompletedAsync(nowUtc, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while processing region elections");
            }

            try
            {
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Region election worker stopping due to cancellation.");
                break;
            }
        }
    }

    private async Task RunAutomaticPresidentSelectionAsync(CancellationToken stoppingToken)
    {
        var countryChanges = await countryGovernanceService.RefreshAutomaticCountryPresidentsAsync(stoppingToken);
        if (countryChanges > 0)
        {
            Log.Information(
                "Automatic president selection updated {CountryChanges} countries.",
                countryChanges);
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
