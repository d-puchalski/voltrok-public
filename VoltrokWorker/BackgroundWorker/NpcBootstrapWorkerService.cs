using Microsoft.Extensions.Hosting;
using Serilog;
using VoltrokServices.Services.Players;

namespace VoltrokWorker.BackgroundWorker;

public sealed class NpcBootstrapWorkerService(
    NpcBootstrapService npcBootstrapService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            Log.Information("NPC bootstrap started.");
            await npcBootstrapService.EnsureNpcPlayersAsync(stoppingToken);
            Log.Information("NPC bootstrap finished.");
        }
        catch (OperationCanceledException)
        {
            Log.Information("NPC bootstrap cancelled.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "NPC bootstrap failed.");
        }
    }
}
