using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VoltrokServices.Services.Players;

namespace VoltrokWorker.BackgroundWorker;

public sealed class PlayerTransportSettlementWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PlayerTransportSettlementWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var playerTradeService = scope.ServiceProvider.GetRequiredService<PlayerTradeService>();
                await playerTradeService.ProcessCompletedTradesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PlayerTransportSettlementWorker tick failed.");
            }
        }
    }
}
