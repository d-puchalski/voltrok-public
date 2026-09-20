using Microsoft.EntityFrameworkCore;

using VoltrokEF;
using VoltrokServices.Services.Premium;
using VoltrokUtils.Engines;
using VoltrokUtils.Enums;
using VoltrokUtils.Models;

namespace VoltrokServices.Services.Military;

public class PlayerMilitaryTransportService(IDbContextFactory<AppDbContext> dbContextFactory, PremiumService premiumService)
{
    public async Task<IReadOnlyList<PlayerMilitaryTransport>> DispatchMissionAsync(
        Guid fromPlayerId,
        Guid toPlayerId,
        string missionType,
        IReadOnlyCollection<MilitaryDispatchSelection> selections)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var normalizedMission = NormalizeMissionType(missionType);
        var sourcePlayer = await db.Players.Include(player => player.PlayerMilitaryUnits).FirstOrDefaultAsync(player => player.PlayersId == fromPlayerId)
            ?? throw new InvalidOperationException("Source player not found.");
        var targetPlayer = await db.Players.FirstOrDefaultAsync(player => player.PlayersId == toPlayerId)
            ?? throw new InvalidOperationException("Target player not found.");

        if (fromPlayerId == toPlayerId)
        {
            throw new InvalidOperationException("Cannot dispatch units to the same player.");
        }

        if (IsAggressiveMission(normalizedMission)
            && await HasBlockingActiveSiegeAsync(toPlayerId, fromPlayerId))
        {
            throw new InvalidOperationException("Target player is already under siege.");
        }

        var normalizedSelections = NormalizeSelections(selections);
        if (normalizedSelections.Count == 0)
        {
            throw new InvalidOperationException("No units selected.");
        }

        foreach (var selection in normalizedSelections)
        {
            var available = sourcePlayer.PlayerMilitaryUnits
                .Where(unit => unit.Quantity > 0
                               && string.Equals(unit.MilitaryUnitsCode, selection.UnitCode, StringComparison.OrdinalIgnoreCase))
                .Sum(unit => unit.Quantity);

            if (available < selection.Quantity)
            {
                throw new InvalidOperationException($"Not enough {selection.UnitCode} units.");
            }
        }

        var speedMultiplier = await premiumService.GetMilitarySpeedMultiplierAsync(fromPlayerId);
        var plans = BuildDispatchPlans(sourcePlayer, targetPlayer, normalizedSelections, speedMultiplier);
        var totalOilCost = plans.Sum(plan => plan.OilCost);
        if (sourcePlayer.Oil < totalOilCost)
        {
            throw new InvalidOperationException("Not enough oil for this mission.");
        }

        sourcePlayer.Oil -= totalOilCost;

        var rosterByCode = sourcePlayer.PlayerMilitaryUnits
            .Where(unit => unit.Quantity > 0)
            .GroupBy(unit => unit.MilitaryUnitsCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(unit => unit.Level)
                    .ThenBy(unit => unit.CreatedAt ?? DateTime.UtcNow)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var createdTransports = new List<PlayerMilitaryTransport>();
        foreach (var plan in plans)
        {
            var transport = new PlayerMilitaryTransport
            {
                PlayerMilitaryTransportsId = Guid.NewGuid(),
                PlayerFromId = fromPlayerId,
                PlayerToId = toPlayerId,
                MissionType = normalizedMission,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddSeconds(plan.TravelTimeSeconds),
                Status = PlayerTransportStatus.InProgress.ToDbValue(),
                AutoReturnAfterBattle = normalizedMission != PlayerMilitaryTransportMissionType.Siege.ToDbValue(),
                CreatedAt = DateTime.UtcNow
            };

            db.PlayerMilitaryTransports.Add(transport);

            foreach (var selection in plan.Selections)
            {
                if (!rosterByCode.TryGetValue(selection.UnitCode, out var rosterEntries))
                {
                    throw new InvalidOperationException($"No roster entries found for {selection.UnitCode}.");
                }

                var remaining = selection.Quantity;
                foreach (var rosterEntry in rosterEntries.Where(entry => entry.Quantity > 0 && remaining > 0))
                {
                    var allocated = Math.Min(remaining, rosterEntry.Quantity);
                    if (allocated <= 0)
                    {
                        continue;
                    }

                    db.PlayerMilitaryTransportUnits.Add(new PlayerMilitaryTransportUnit
                    {
                        PlayerMilitaryTransportUnitsId = Guid.NewGuid(),
                        PlayerMilitaryTransportsId = transport.PlayerMilitaryTransportsId,
                        MilitaryUnitsCode = selection.UnitCode,
                        Level = rosterEntry.Level,
                        Quantity = allocated
                    });

                    rosterEntry.Quantity -= allocated;
                    remaining -= allocated;
                }

                if (remaining > 0)
                {
                    throw new InvalidOperationException($"Unable to allocate all selected units for {selection.UnitCode}.");
                }
            }

            createdTransports.Add(transport);
        }

        await db.SaveChangesAsync();
        return createdTransports;
    }

    public async Task<List<PlayerMilitaryTransport>> GetActivePlayerMilitaryTransportsForMapAsync()
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var transports = await db.PlayerMilitaryTransports
            .AsNoTracking()
            .Include(t => t.PlayerFrom)
            .Include(t => t.PlayerTo)
            .Include(t => t.PlayerMilitaryTransportUnits)
            .Where(t => t.Status == PlayerTransportStatus.InProgress.ToDbValue())
            .ToListAsync();

        return transports;
    }

    public async Task<PlayerMilitaryTransport?> GetLatestOutgoingMissionAsync(Guid sourcePlayerId, Guid targetPlayerId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        return await db.PlayerMilitaryTransports
            .AsNoTracking()
            .Include(t => t.PlayerMilitaryTransportUnits)
            .Where(t => t.PlayerFromId == sourcePlayerId
                        && t.PlayerToId == targetPlayerId
                        && (t.MissionType == PlayerMilitaryTransportMissionType.Attack.ToDbValue() || t.MissionType == PlayerMilitaryTransportMissionType.Siege.ToDbValue())
                        && (t.Status == PlayerTransportStatus.InProgress.ToDbValue() || t.Status == PlayerTransportStatus.InSiege.ToDbValue()))
            .OrderByDescending(t => t.CreatedAt ?? t.StartTime)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasBlockingActiveSiegeAsync(Guid targetPlayerId, Guid? excludingAttackerPlayerId = null)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var activeSiegeCode = PlayerSiegesStatus.Active.ToDbValue().ToLowerInvariant();

        return await db.PlayerSieges
            .AsNoTracking()
            .AnyAsync(siege =>
                siege.PlayerToId == targetPlayerId
                && siege.Status != null
                && siege.Status.ToLower() == activeSiegeCode
                && (!excludingAttackerPlayerId.HasValue || siege.PlayerFromId != excludingAttackerPlayerId.Value));
    }

    public async Task CancelAttackAsync(Guid sourcePlayerId, Guid transportId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var transport = await db.PlayerMilitaryTransports.FirstOrDefaultAsync(t => t.PlayerMilitaryTransportsId == transportId && t.PlayerFromId == sourcePlayerId) ?? throw new InvalidOperationException("Transport not found.");
        transport.Status = PlayerTransportStatus.Cancelled.ToDbValue();
        await db.SaveChangesAsync();
    }

    public async Task SetAutoReturnAfterBattleAsync(Guid sourcePlayerId, Guid transportId, bool autoReturnAfterBattle)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var transport = await db.PlayerMilitaryTransports.FirstOrDefaultAsync(t => t.PlayerMilitaryTransportsId == transportId && t.PlayerFromId == sourcePlayerId) ?? throw new InvalidOperationException("Transport not found.");
        transport.AutoReturnAfterBattle = autoReturnAfterBattle;
        await db.SaveChangesAsync();
    }

    public async Task WithdrawStationedUnitsAsync(Guid sourcePlayerId, Guid transportId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var transport = await db.PlayerMilitaryTransports.FirstOrDefaultAsync(t => t.PlayerMilitaryTransportsId == transportId && t.PlayerFromId == sourcePlayerId) ?? throw new InvalidOperationException("Transport not found.");
        transport.Status = PlayerTransportStatus.Returning.ToDbValue();
        await db.SaveChangesAsync();
    }

    public IReadOnlyList<MilitaryDispatchPlan> BuildDispatchPlans(
        Player fromPlayer,
        Player toPlayer,
        IReadOnlyCollection<MilitaryDispatchSelection> selections,
        decimal speedMultiplier = 1m)
    {
        var normalizedSelections = NormalizeSelections(selections);
        if (normalizedSelections.Count == 0)
        {
            return [];
        }

        var distanceKm = CalculateDistanceKm(fromPlayer.LocationY, fromPlayer.LocationX, toPlayer.LocationY, toPlayer.LocationX);
        var mergeSpeed = normalizedSelections
            .Select(selection => ResolveUnit(selection.UnitCode))
            .Min(unit => Math.Max(1, (int)Math.Round(unit.StartingSpeed * Math.Max(1m, speedMultiplier), MidpointRounding.AwayFromZero)));
        var mergeTime = CalculateTravelTimeSeconds(distanceKm, mergeSpeed);
        var mergeOil = RoundOilCost(CalculateRawOilCost(distanceKm, normalizedSelections) * 0.92m);
        return [new MilitaryDispatchPlan(normalizedSelections, mergeSpeed, mergeTime, distanceKm, mergeOil)];
    }

    public decimal CalculateDistanceKm(Player fromPlayer, Player toPlayer)
        => CalculateDistanceKm(fromPlayer.LocationY, fromPlayer.LocationX, toPlayer.LocationY, toPlayer.LocationX);

    private static List<MilitaryDispatchSelection> NormalizeSelections(IEnumerable<MilitaryDispatchSelection> selections)
        => selections
            .Where(selection => !string.IsNullOrWhiteSpace(selection.UnitCode) && selection.Quantity > 0)
            .GroupBy(selection => selection.UnitCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new MilitaryDispatchSelection(group.Key, group.Sum(item => item.Quantity)))
            .OrderBy(selection => selection.UnitCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string NormalizeMissionType(string missionType)
    {
        return missionType is nameof(PlayerMilitaryTransportMissionType.Attack) or nameof(PlayerMilitaryTransportMissionType.Siege) or nameof(PlayerMilitaryTransportMissionType.Aid)
            ? missionType
            : throw new InvalidOperationException("Unsupported mission type.");
    }

    private static bool IsAggressiveMission(string missionType)
        => missionType is nameof(PlayerMilitaryTransportMissionType.Attack) or nameof(PlayerMilitaryTransportMissionType.Siege);

    private static MilitaryUnit ResolveUnit(string unitCode)
        => MilitaryUnitEngine.MilitaryUnitsAllData.FirstOrDefault(unit => string.Equals(unit.Code.ToString(), unitCode, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException($"Unknown military unit: {unitCode}");

    private static decimal CalculateRawOilCost(decimal distanceKm, IReadOnlyCollection<MilitaryDispatchSelection> selections)
        => selections.Sum(selection =>
        {
            var unit = ResolveUnit(selection.UnitCode);
            var perUnitFuel = CombatBalanceEngine.CalculateTransportPerUnitFuel(
                unit.StartingOffensivePoints,
                unit.StartingDefensivePoints,
                unit.StartingSiegePoints,
                unit.StartingSpeed);
            return selection.Quantity * distanceKm * perUnitFuel / 100m;
        });

    private static decimal RoundOilCost(decimal value) => Math.Ceiling(value * 100m) / 100m;

    private static int CalculateTravelTimeSeconds(decimal distanceKm, int speedKmh)
        => Math.Max(60, (int)Math.Ceiling((double)(distanceKm / Math.Max(1, speedKmh) * 3600m)));

    private static decimal CalculateDistanceKm(decimal fromLat, decimal fromLng, decimal toLat, decimal toLng)
    {
        const decimal earthRadiusKm = 6371m;
        var dLat = ToRadians(toLat - fromLat);
        var dLon = ToRadians(toLng - fromLng);
        var lat1 = ToRadians(fromLat);
        var lat2 = ToRadians(toLat);

        var sinDlat = Math.Sin((double)dLat / 2d);
        var sinDlon = Math.Sin((double)dLon / 2d);
        var a = sinDlat * sinDlat + Math.Cos((double)lat1) * Math.Cos((double)lat2) * sinDlon * sinDlon;
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return earthRadiusKm * (decimal)c;
    }

    private static decimal ToRadians(decimal degrees) => degrees * ((decimal)Math.PI / 180m);
}
