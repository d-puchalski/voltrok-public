using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using VoltrokEF;

namespace VoltrokWorker.BackgroundWorker;

public class PlayerNeedsWorkerService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration) : BackgroundService
{
    private const decimal DefaultMinNeedDecayPerMinute = 0.008m;
    private const decimal DefaultMaxNeedDecayPerMinute = 0.04m;
    private const decimal AccessibilityCostNormalization = 50m;
    private const double NeedDecayCurvePower = 0.85d;

    private int ProcessCooldownMinutes => Math.Max(1, configuration.GetValue("BackgroundWorker:PlayerNeedsBreakMinutes", 1));

    private decimal NeedDecayBalanceMultiplier => Math.Max(
        0m,
        configuration.GetValue(
            "BackgroundWorker:PlayerNeedsBalanceMultiplier",
            configuration.GetValue("BackgroundWorker:PlayerNeedsDecayPerCycle", 1m)));

    private decimal MinNeedDecayPerMinute => Math.Max(
        0m,
        configuration.GetValue("BackgroundWorker:PlayerNeedsMinDecayPerMinute", DefaultMinNeedDecayPerMinute));

    private decimal MaxNeedDecayPerMinute => Math.Max(
        MinNeedDecayPerMinute,
        configuration.GetValue("BackgroundWorker:PlayerNeedsMaxDecayPerMinute", DefaultMaxNeedDecayPerMinute));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Player needs worker started. Interval: {ProcessCooldownMinutes}m.", ProcessCooldownMinutes);

        var cycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            cycle++;

            var startedAt = DateTime.UtcNow;
            Log.Information("Player needs worker cycle {Cycle} started.", cycle);

            await DumpNeedDepletionTimesToConsoleAsync(stoppingToken);

            try
            {
                await ProcessPlayerNeedsAsync(stoppingToken);

                Log.Information(
                    "Player needs worker cycle {Cycle} finished in {DurationMs} ms.",
                    cycle,
                    (DateTime.UtcNow - startedAt).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while processing player needs");
            }

            try
            {
                Log.Information("Player needs worker sleeping for {ProcessCooldownMinutes}m.", ProcessCooldownMinutes);
                await Task.Delay(TimeSpan.FromMinutes(ProcessCooldownMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Player needs worker stopping due to cancellation.");
                break;
            }
        }
    }

    private async Task ProcessPlayerNeedsAsync(CancellationToken cancellationToken)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var players = await dbContext.Players
            .Include(v => v.PlayerNeeds)
            .ToListAsync(cancellationToken);

        if (players.Count == 0)
        {
            return;
        }

        var needs = await dbContext.Needs
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (needs.Count == 0)
        {
            return;
        }

        var needSupplies = await dbContext.ProductsNeedsSupplies
            .AsNoTracking()
            .Include(s => s.Products)
            .ToListAsync(cancellationToken);

        var needDecayPerCycleByNeedId = CalculateNeedDecayPerCycleByNeedId(needs, needSupplies);
        var fallbackNeedDecayPerCycle = GetFallbackNeedDecayPerCycle();
        var hasChanges = false;

        foreach (var player in players)
        {
            foreach (var need in needs)
            {
                if (!needDecayPerCycleByNeedId.TryGetValue(need.NeedsId, out var needDecayPerCycle))
                {
                    needDecayPerCycle = fallbackNeedDecayPerCycle;
                }

                var existing = player.PlayerNeeds.FirstOrDefault(n => n.NeedsId == need.NeedsId);

                if (existing == null)
                {
                    player.PlayerNeeds.Add(new PlayerNeed
                    {
                        PlayerNeedsId = Guid.NewGuid(),
                        PlayersId = player.PlayersId,
                        NeedsId = need.NeedsId,
                        Value = Math.Clamp(100m - needDecayPerCycle, 0m, 100m)
                    });
                    hasChanges = true;
                    continue;
                }

                var nextValue = Math.Clamp(existing.Value - needDecayPerCycle, 0m, 100m);

                if (Math.Abs(existing.Value - nextValue) < 0.01m)
                {
                    continue;
                }

                existing.Value = nextValue;
                hasChanges = true;
            }
        }

        if (!hasChanges)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Dictionary<Guid, decimal> CalculateNeedDecayPerCycleByNeedId(
        IReadOnlyCollection<Need> needs,
        IReadOnlyCollection<ProductsNeedsSupply> needSupplies)
    {
        var needDecayPerMinuteByNeedId = CalculateNeedDecayPerMinuteByNeedId(
            needs,
            needSupplies,
            MinNeedDecayPerMinute,
            MaxNeedDecayPerMinute);

        return needDecayPerMinuteByNeedId.ToDictionary(
            pair => pair.Key,
            pair => pair.Value * ProcessCooldownMinutes * NeedDecayBalanceMultiplier);
    }

    private decimal GetFallbackNeedDecayPerCycle()
    {
        var averageDecayPerMinute = (MinNeedDecayPerMinute + MaxNeedDecayPerMinute) / 2m;
        return averageDecayPerMinute * ProcessCooldownMinutes * NeedDecayBalanceMultiplier;
    }

    private static Dictionary<Guid, decimal> CalculateNeedDecayPerMinuteByNeedId(
        IReadOnlyCollection<Need> needs,
        IReadOnlyCollection<ProductsNeedsSupply> needSupplies,
        decimal minNeedDecayPerMinute,
        decimal maxNeedDecayPerMinute)
    {
        var accessibilityByNeedId = needSupplies
            .GroupBy(supply => supply.NeedsId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(CalculateNeedAccessibilityScore)
                    .DefaultIfEmpty(0m)
                    .Sum());

        var nonZeroAccessibility = accessibilityByNeedId.Values
            .Where(value => value > 0m)
            .ToList();

        var defaultNeedDecayPerMinute = (minNeedDecayPerMinute + maxNeedDecayPerMinute) / 2m;

        if (nonZeroAccessibility.Count == 0 || maxNeedDecayPerMinute <= minNeedDecayPerMinute)
        {
            return needs.ToDictionary(need => need.NeedsId, _ => defaultNeedDecayPerMinute);
        }

        var minAccessibility = nonZeroAccessibility.Min();
        var maxAccessibility = nonZeroAccessibility.Max();

        if (maxAccessibility <= minAccessibility)
        {
            return needs.ToDictionary(need => need.NeedsId, _ => defaultNeedDecayPerMinute);
        }

        return needs.ToDictionary(
            need => need.NeedsId,
            need =>
            {
                if (!accessibilityByNeedId.TryGetValue(need.NeedsId, out var accessibility) || accessibility <= 0m)
                {
                    return defaultNeedDecayPerMinute;
                }

                var normalizedAccessibility = (accessibility - minAccessibility) / (maxAccessibility - minAccessibility);
                var curvedAccessibility = (decimal)Math.Pow((double)normalizedAccessibility, NeedDecayCurvePower);

                return minNeedDecayPerMinute + (curvedAccessibility * (maxNeedDecayPerMinute - minNeedDecayPerMinute));
            });
    }

    private static decimal CalculateNeedAccessibilityScore(ProductsNeedsSupply supply)
    {
        var timeSeconds = Math.Max(1, supply.Products.TimeSeconds);
        var quantityPerHour = supply.Quantity * (3600m / timeSeconds);
        var productionCost = Math.Max(1m, supply.Products.ProductionCost);
        var costModifier = AccessibilityCostNormalization / (AccessibilityCostNormalization + productionCost);

        return quantityPerHour * costModifier;
    }

    public async Task<IReadOnlyList<string>> DumpNeedDepletionTimesToConsoleAsync(CancellationToken cancellationToken = default)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var needs = await dbContext.Needs
            .AsNoTracking()
            .OrderBy(n => n.Code)
            .ToListAsync(cancellationToken);

        if (needs.Count == 0)
        {
            const string noNeedsMessage = "No needs found in database.";
            Log.Warning(noNeedsMessage);
            return [noNeedsMessage];
        }

        var needSupplies = await dbContext.ProductsNeedsSupplies
            .AsNoTracking()
            .Include(s => s.Products)
            .ToListAsync(cancellationToken);

        var byNeedId = EstimateDepletionTimeFromFullToZeroByNeedIdForTests(
            needs,
            needSupplies,
            MinNeedDecayPerMinute,
            MaxNeedDecayPerMinute,
            ProcessCooldownMinutes,
            NeedDecayBalanceMultiplier);

        var lines = needs
            .Select(need =>
            {
                if (!byNeedId.TryGetValue(need.NeedsId, out var depletionTime) || depletionTime == TimeSpan.MaxValue)
                {
                    return $"{need.Code}: never (decay <= 0)";
                }

                return $"{need.Code}: {depletionTime}";
            })
            .ToList();

        Log.Information("=== Need depletion time dump (100 -> 0) ===");
        Log.Information(
            $"Config: break={ProcessCooldownMinutes}m, minDecayPerMinute={MinNeedDecayPerMinute}, maxDecayPerMinute={MaxNeedDecayPerMinute}, balanceMultiplier={NeedDecayBalanceMultiplier}");
        foreach (var line in lines)
        {
            Log.Information(line);
        }

        return lines;
    }

    public static IReadOnlyDictionary<Guid, TimeSpan> EstimateDepletionTimeFromFullToZeroByNeedIdForTests(
        IReadOnlyCollection<Need> needs,
        IReadOnlyCollection<ProductsNeedsSupply> needSupplies,
        decimal minNeedDecayPerMinute,
        decimal maxNeedDecayPerMinute,
        int processCooldownMinutes,
        decimal needDecayBalanceMultiplier)
    {
        var safeProcessCooldownMinutes = Math.Max(1, processCooldownMinutes);
        var safeMinNeedDecayPerMinute = Math.Max(0m, minNeedDecayPerMinute);
        var safeMaxNeedDecayPerMinute = Math.Max(safeMinNeedDecayPerMinute, maxNeedDecayPerMinute);
        var safeNeedDecayBalanceMultiplier = Math.Max(0m, needDecayBalanceMultiplier);

        var needDecayPerMinuteByNeedId = CalculateNeedDecayPerMinuteByNeedId(
            needs,
            needSupplies,
            safeMinNeedDecayPerMinute,
            safeMaxNeedDecayPerMinute);

        return needDecayPerMinuteByNeedId.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                var needDecayPerCycle = pair.Value * safeProcessCooldownMinutes * safeNeedDecayBalanceMultiplier;
                if (needDecayPerCycle <= 0m)
                {
                    return TimeSpan.MaxValue;
                }

                var cyclesToDeplete = (double)Math.Ceiling(100m / needDecayPerCycle);
                var totalMinutes = cyclesToDeplete * safeProcessCooldownMinutes;
                return TimeSpan.FromMinutes(totalMinutes);
            });
    }
}
