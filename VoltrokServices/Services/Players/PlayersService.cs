using Microsoft.EntityFrameworkCore;
using VoltrokEF;
using VoltrokUtils;
using VoltrokUtils.Engines;
using VoltrokUtils.Enums;
using VoltrokUtils.Models;

namespace VoltrokServices.Services.Players;

public class PlayersService(
    IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<List<Player>> GetIncomingAttackSourcesAsync(Guid playerId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        return await db.PlayerMilitaryTransports
            .AsNoTracking()
            .Where(t =>
                t.PlayerToId == playerId
                && t.EndTime > now
                && t.Status == PlayerTransportStatus.InProgress.ToDbValue()
                && t.MissionType == PlayerMilitaryTransportMissionType.Attack.ToDbValue())
            .Include(t => t.PlayerFrom)
            .GroupBy(t => new
            {
                t.PlayerFrom.PlayersId,
                t.PlayerFrom.Name,
                t.PlayerFrom.LocationX,
                t.PlayerFrom.LocationY
            })
            .OrderBy(group => group.Key.Name)
            .Select(group => new Player
            {
                PlayersId = group.Key.PlayersId,
                Name = group.Key.Name,
                LocationX = group.Key.LocationX,
                LocationY = group.Key.LocationY
            })
            .ToListAsync();
    }

    public async Task<(int IncomingAttackCount, int IncomingSiegeCount)> GetIncomingThreatCountsAsync(Guid playerId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        var incomingAttackCount = await db.PlayerMilitaryTransports
            .AsNoTracking()
            .CountAsync(t =>
                t.PlayerToId == playerId
                && t.EndTime > now
                && t.Status == PlayerTransportStatus.InProgress.ToDbValue()
                && t.MissionType == PlayerMilitaryTransportMissionType.Attack.ToDbValue());

        var incomingSiegeCount = await db.PlayerSieges
            .AsNoTracking()
            .CountAsync(s =>
                s.PlayerToId == playerId
                && s.EndedAt > now
                && s.Status == PlayerSiegesStatus.Active.ToDbValue());

        return (incomingAttackCount, incomingSiegeCount);
    }

    public async Task<List<Player>> GetAllPlayers()
    {
        return await dbContextFactory.CreateDbContextAsync()
            .Result.Players.AsNoTracking().Include(p => p.Countries)
            .Select(p => new Player
            {
                CountriesId = p.CountriesId,
                LocationX = p.LocationX,
                LocationY = p.LocationY,
                PlayersId = p.PlayersId,
                PlayerAvatar = p.PlayerAvatar,
                Name = p.Name,
                Countries = new Country
                {
                    CountriesId = p.Countries.CountriesId,
                    Name = p.Countries.Name,
                    IsoCode2 = p.Countries.IsoCode2,
                    Flag = p.Countries.Flag
                }
            }).ToListAsync();
    }

    public async Task<List<PlayerMapTooltipStats>> GetPlayerMapTooltipStatsAsync()
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var players = await db.Players.AsNoTracking().Include(p => p.Countries).ToListAsync();

        var badges = await db.PlayerBadges
            .AsNoTracking()
            .ToListAsync();

        return players.Select(player =>
        {
            return new PlayerMapTooltipStats
            {
                PlayerId = player.PlayersId,
                CountryId = player.CountriesId,
                PlayerName = player.Name,
                IsNpc = player.IsNpc,
                PremiumShieldEnabled = player.ShieldValidTill.HasValue && player.ShieldValidTill.Value > DateTime.UtcNow,
                IsCountryCouncilMember = player.IsCountryCouncilMember,
                IsCountryPresident = player.Countries.PresidentPlayersId == player.PlayersId,
                PlayerAvatar = player.PlayerAvatar,
                CountryName = player.Countries.Name,
                CountryFlag = player.Countries.Flag,
                CountryIsoCode2 = player.Countries.IsoCode2,
                Money = player.Money,
                Oil = player.Oil,
                Uranium = player.Uranium,
                Chips = player.Chips,
                Badges = badges.Where(b => b.PlayersId == player.PlayersId).Select(b => new PlayerMapBadgeInfo
                {
                    Code = b.BadgeCode,
                    AwardedAtUtc = b.AwardedAt
                }).ToList()
            };
        }).ToList();
    }

    public async Task<PlayerMapTooltipStats?> GetPlayerMapTooltipStatsAsync(Guid playerId)
        => (await GetPlayerMapTooltipStatsAsync()).FirstOrDefault(x => x.PlayerId == playerId);

    public async Task<Player?> GetPlayerForTab(Guid playerId, PlayerOverviewTabEnum tabEnum)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var query = db.Players
            .AsNoTracking()
            .Include(p => p.Countries)
            .Where(p => p.PlayersId == playerId);

        if (tabEnum == PlayerOverviewTabEnum.Military)
        {
            query = query
                .Include(p => p.PlayerMilitaryProductions)
                .Include(p => p.PlayerMilitaryUnits)
                .Include(p => p.PlayerMilitaryTransportPlayerFroms)
                    .ThenInclude(t => t.PlayerMilitaryTransportUnits)
                .Include(p => p.PlayerMilitaryTransportPlayerFroms)
                    .ThenInclude(t => t.PlayerTo)
                .Include(p => p.PlayerMilitaryTransportPlayerTos)
                    .ThenInclude(t => t.PlayerMilitaryTransportUnits)
                .Include(p => p.PlayerMilitaryTransportPlayerTos)
                    .ThenInclude(t => t.PlayerFrom)
                .Include(p => p.PlayerSiegePlayerTos)
                    .ThenInclude(s => s.PlayerFrom)
                .Include(p => p.PlayerSiegePlayerTos)
                    .ThenInclude(s => s.PlayerSiegeUnits)
                .Include(p => p.PlayerSiegePlayerFroms)
                    .ThenInclude(s => s.PlayerTo)
                .Include(p => p.PlayerSiegePlayerFroms)
                    .ThenInclude(s => s.PlayerSiegeUnits)
                .Include(p => p.PlayerBattleReportPlayerFroms)
                    .ThenInclude(r => r.PlayerTo)
                .Include(p => p.PlayerBattleReportPlayerFroms)
                    .ThenInclude(r => r.PlayerBattleReportUnits)
                .Include(p => p.PlayerBattleReportPlayerTos)
                    .ThenInclude(r => r.PlayerFrom)
                .Include(p => p.PlayerBattleReportPlayerTos)
                    .ThenInclude(r => r.PlayerBattleReportUnits);
        }

        if (tabEnum == PlayerOverviewTabEnum.Trade || tabEnum == PlayerOverviewTabEnum.Transports)
        {
            query = query
                .Include(p => p.PlayerTradeFromPlayers)
                .Include(p => p.PlayerTradeToPlayers);
        }

        var player = await query.FirstOrDefaultAsync();
        return player ?? null;
    }

    public async Task<PlayerSidePanelStats> GetPlayerSidePanelStatsAsync(Guid playerId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.PlayersId == playerId);
        if (player == null) return new PlayerSidePanelStats(0, 0, 0, 0);
        var pendingTrades = await db.PlayerTrades.AsNoTracking()
            .CountAsync(t => t.FromPlayersId == playerId && t.Status == PlayerTradeStatus.Pending.ToDbValue() && t.TotalPrice > 0m);
        var transports = await db.PlayerTrades.AsNoTracking()
            .CountAsync(t => t.FromPlayersId == playerId && t.ToPlayersId != null && t.Status == PlayerTradeStatus.InTransit.ToDbValue());
        var armyUnits = await db.PlayerMilitaryUnits.AsNoTracking()
            .Where(x => x.PlayersId == playerId)
            .GroupBy(x => x.MilitaryUnitsCode)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        var completedTrades = await db.PlayerTrades.AsNoTracking()
            .Where(t => (t.FromPlayersId == playerId || t.ToPlayersId == playerId) && t.Status == PlayerTradeStatus.Completed.ToDbValue() && t.TotalPrice > 0m)
            .ToListAsync();
        var resources = DecimalToInventoryQuantity(player.Oil + player.Uranium + player.Chips);
        var armyTotal = armyUnits.Sum(x => x.Count);
        var attackPower = armyUnits.Sum(x => x.Count * 1);
        var defensePower = armyUnits.Sum(x => x.Count * 1);
        var tradeTransactionsCount = completedTrades.Count;
        var tradeTransactionsValue = completedTrades.Sum(t => t.TotalPrice);
        return new PlayerSidePanelStats(resources, pendingTrades, transports, armyTotal, attackPower, defensePower, tradeTransactionsCount, tradeTransactionsValue);
    }

    public async Task<List<PlayerOverviewPoint>> GetPlayerOverviewPointsAsync(Guid playerId, int lookbackDays)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var from = DateTime.UtcNow.Date.AddDays(-Math.Max(1, lookbackDays));
        var fromDate = DateOnly.FromDateTime(from);
        var points = await db.PlayerDailyStats.AsNoTracking()
            .Where(x => x.PlayersId == playerId && x.StatsDate >= fromDate)
            .OrderBy(x => x.StatsDate)
            .Select(x => new PlayerOverviewPoint(x.StatsDate, x.Money, x.Score, x.Oil, x.Uranium, x.Chips, x.ArmyTotal))
            .ToListAsync();

        if (points.Count > 0)
        {
            return points;
        }

        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.PlayersId == playerId);
        if (player == null)
        {
            return [];
        }

        var armyTotal = await db.PlayerMilitaryUnits.AsNoTracking().CountAsync(x => x.PlayersId == playerId);
        return
        [
            new PlayerOverviewPoint(
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                player.Money,
                player.Score,
                player.Oil,
                player.Uranium,
                player.Chips,
                armyTotal)
        ];
    }

    public async Task<Player?> UpdatePlayerAvatar(Guid playerId, string? playerAvatar)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == playerId);
        if (player == null) return null;
        player.PlayerAvatar = playerAvatar;
        await db.SaveChangesAsync();
        return player;
    }

    public async Task<Player?> UpdatePlayerName(Guid playerId, string newName)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == playerId);
        if (player == null) return null;
        player.Name = newName.Trim();
        await db.SaveChangesAsync();
        return player;
    }

    public async Task<Player?> UpdatePlayerCountryOnce(Guid playerId, Guid newCountryId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == playerId);
        if (player == null || player.HasRelocatedRegion) return player;
        var country = await db.Countries.AsNoTracking().FirstOrDefaultAsync(c => c.CountriesId == newCountryId);
        var coords = ResolveCountryRelocationCoordinates(country?.IsoCode2, country?.Name);
        player.CountriesId = newCountryId;
        player.LocationX = (decimal)coords.Longitude;
        player.LocationY = (decimal)coords.Latitude;
        player.HasRelocatedRegion = true;
        await db.SaveChangesAsync();
        return player;
    }

    public async Task<Player?> SetPremiumShieldStatus(Guid playerId, bool enabled)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == playerId);
        if (player == null)
        {
            return null;
        }

        var nowUtc = DateTime.UtcNow;
        var premiumActive = player.BattlePassValidTill.HasValue && player.BattlePassValidTill.Value > nowUtc;
        if (!premiumActive)
        {
            throw new InvalidOperationException("Premium expired. You can no longer manage shield.");
        }

        if (enabled)
        {
            player.ShieldValidTill = player.BattlePassValidTill;
        }
        else
        {
            player.ShieldValidTill = null;
        }

        await db.SaveChangesAsync();
        return player;
    }

    public async Task<PlayerMilitaryProduction> CreateMilitaryProductionAsync(Guid playerId, MilitaryUnit militaryUnit, int quantity)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == playerId) ?? throw new InvalidOperationException("Player not found.");
        var unitCode = militaryUnit.Code.ToString();
        var currentUnitLevel = await db.PlayerMilitaryUnits
            .Where(x => x.PlayersId == playerId && x.MilitaryUnitsCode == unitCode)
            .MaxAsync(x => (int?)x.Level) ?? 1;
        var queuedUnitLevel = await db.PlayerMilitaryProductions
            .Where(x => x.PlayersId == playerId && x.MilitaryUnitsCode == unitCode && x.Quantity > x.ProducedQuantity)
            .MaxAsync(x => (int?)x.Level) ?? 1;
        var targetLevel = Math.Max(1, Math.Max(currentUnitLevel, queuedUnitLevel));
        var totalCost = Math.Max(0.01m, militaryUnit.GetCost(targetLevel)) * quantity;
        if (player.Money < totalCost) throw new InvalidOperationException("Not enough money.");
        player.Money -= totalCost;
        var order = new PlayerMilitaryProduction
        {
            PlayerMilitaryOrdersId = Guid.NewGuid(),
            PlayersId = playerId,
            MilitaryUnitsCode = unitCode,
            Quantity = quantity,
            Level = targetLevel,
            ProducedQuantity = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.PlayerMilitaryProductions.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    public async Task UpgradeMilitaryUnitAsync(Guid playerId, string unitCode)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var normalizedUnitCode = unitCode.Trim();
        var unitDefinition = MilitaryUnitEngine.FindByCode(normalizedUnitCode)
            ?? throw new InvalidOperationException("Military unit not found.");

        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == playerId)
            ?? throw new InvalidOperationException("Player not found.");

        var rosterEntries = await db.PlayerMilitaryUnits
            .Where(x => x.PlayersId == playerId && x.MilitaryUnitsCode == normalizedUnitCode)
            .OrderByDescending(x => x.Level)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();

        var queuedOrders = await db.PlayerMilitaryProductions
            .Where(x => x.PlayersId == playerId && x.MilitaryUnitsCode == normalizedUnitCode && x.Quantity > x.ProducedQuantity)
            .ToListAsync();

        if (rosterEntries.Count == 0 && queuedOrders.Count == 0)
        {
            throw new InvalidOperationException("Recruit this unit before upgrading it.");
        }

        var currentLevel = Math.Max(
            rosterEntries.Select(x => Math.Max(1, x.Level)).DefaultIfEmpty(1).Max(),
            queuedOrders.Select(x => Math.Max(1, x.Level)).DefaultIfEmpty(1).Max());
        var targetLevel = currentLevel + 1;
        var upgradeCost = unitDefinition.GetUpgradeCost(targetLevel);

        if (player.Chips < upgradeCost)
        {
            throw new InvalidOperationException("Not enough chips.");
        }

        player.Chips -= upgradeCost;

        foreach (var queuedOrder in queuedOrders)
        {
            queuedOrder.Level = targetLevel;
        }

        if (rosterEntries.Count > 0)
        {
            var primaryEntry = rosterEntries[0];
            primaryEntry.Level = targetLevel;
            primaryEntry.Quantity = rosterEntries.Sum(x => Math.Max(0, x.Quantity));
            primaryEntry.CreatedAt ??= DateTime.UtcNow;

            foreach (var extraEntry in rosterEntries.Skip(1))
            {
                db.PlayerMilitaryUnits.Remove(extraEntry);
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task CancelMilitaryOrderAsync(Guid playerId, Guid militaryOrderId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var order = await db.PlayerMilitaryProductions.FirstOrDefaultAsync(o => o.PlayerMilitaryOrdersId == militaryOrderId && o.PlayersId == playerId) ?? throw new InvalidOperationException("Military order not found.");
        db.PlayerMilitaryProductions.Remove(order);
        await db.SaveChangesAsync();
    }

    public async Task ArchiveBattleReportAsync(Guid playerId, Guid reportId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var report = await db.PlayerBattleReports
            .FirstOrDefaultAsync(r => r.PlayerBattleReportsId == reportId
                                      && (r.PlayerFromId == playerId || r.PlayerToId == playerId))
            ?? throw new InvalidOperationException("Battle report not found.");

        report.IsArchived = true;
        report.ArchivedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ArchiveSiegeReportAsync(Guid playerId, Guid siegeId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var siege = await db.PlayerSieges
            .FirstOrDefaultAsync(s => s.PlayerSiegesId == siegeId
                                      && (s.PlayerFromId == playerId || s.PlayerToId == playerId))
            ?? throw new InvalidOperationException("Siege report not found.");

        if (string.Equals(siege.Status, PlayerSiegesStatus.Active.ToDbValue(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Active siege cannot be archived.");
        }

        siege.IsArchived = true;
        siege.ArchivedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ArchiveAidReportAsync(Guid playerId, Guid transportId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var transport = await db.PlayerMilitaryTransports
            .FirstOrDefaultAsync(t => t.PlayerMilitaryTransportsId == transportId
                                      && string.Equals(t.MissionType, PlayerMilitaryTransportMissionType.Aid.ToDbValue(), StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(t.Status, PlayerTransportStatus.Completed.ToDbValue(), StringComparison.OrdinalIgnoreCase)
                                      && (t.PlayerFromId == playerId || t.PlayerToId == playerId))
            ?? throw new InvalidOperationException("Help report not found.");

        transport.IsArchived = true;
        transport.ArchivedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<List<Player>> GetMilitaryDestinationsAsync(Guid playerId, string missionType)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var sourceCountryId = await db.Players.Where(p => p.PlayersId == playerId).Select(p => p.CountriesId).FirstOrDefaultAsync();
        return await db.Players.AsNoTracking()
            .Include(p => p.Countries)
            .Where(p => p.PlayersId != playerId && p.CountriesId == sourceCountryId)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task RelocatePlayerToRegionAsync(Guid playerId, Guid newRegionId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == playerId) ?? throw new InvalidOperationException("Player not found.");
        player.CountriesId = newRegionId;
        await db.SaveChangesAsync();
    }

    private static (double Latitude, double Longitude) ResolveCountryRelocationCoordinates(string? isoCode2, string? countryName)
    {
        var geoJsonPath = ResolveCountriesGeoJsonPath();
        var admCode1 = (string?)null;
        var name = countryName;
        return RandomCoordinateGenerator.GetRandomLandCoordinateForRegion(
            isoCode2,
            admCode1,
            name,
            geoJsonPath ?? throw new InvalidOperationException("Countries GeoJSON file is unavailable."));
    }

    private static string? ResolveCountriesGeoJsonPath()
    {
        var relativePath = Path.Combine("wwwroot", "assets", "maps", "ne_10m_admin_0_countries.json");
        var baseDirectory = AppContext.BaseDirectory;
        var currentDirectory = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(baseDirectory, relativePath),
            Path.Combine(currentDirectory, relativePath),
            Path.Combine(baseDirectory, "VoltrokWebApp", relativePath),
            Path.Combine(currentDirectory, "VoltrokWebApp", relativePath),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "VoltrokWebApp", relativePath)),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "VoltrokWebApp", relativePath)),
            Path.GetFullPath(Path.Combine(currentDirectory, "..", "VoltrokWebApp", relativePath)),
            Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "VoltrokWebApp", relativePath))
        };

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }

    private static int DecimalToInventoryQuantity(decimal value)
        => value <= 0m ? 0 : (int)Math.Floor(value);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record PlayerSidePanelStats(
    int ResourcesTotal,
    int TradeCount,
    int TransportCount,
    int ArmyTotal,
    int ArmyAttackPower,
    int ArmyDefensePower,
    int TradeTransactionsCount,
    decimal TradeTransactionsValue)
{
    public PlayerSidePanelStats(int resourcesTotal, int tradeCount, int transportCount, int armyTotal)
        : this(resourcesTotal, tradeCount, transportCount, armyTotal, 0, 0, 0, 0m)
    {
    }
}
