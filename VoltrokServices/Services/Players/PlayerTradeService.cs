using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VoltrokEF;
using VoltrokServices.Services.Notifications;
using VoltrokServices.Services.Premium;
using VoltrokUtils.Enums;
using VoltrokUtils.Models;

namespace VoltrokServices.Services.Players;

public class PlayerTradeService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    PremiumService premiumService,
    NotificationService notificationService)
{

    public async Task<List<PlayerTrade>> GetActiveTransportsForMapAsync()
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        return await db.PlayerTrades.AsNoTracking()
            .Include(t => t.FromPlayers)
            .Include(t => t.ToPlayers)
            .Where(t => t.Status == PlayerTradeStatus.InTransit.ToDbValue() && t.ToPlayersId != null)
            .ToListAsync();
    }

    public async Task<List<PlayerSiege>> GetActiveSiegesForMapAsync()
    {
        return await (await dbContextFactory.CreateDbContextAsync()).PlayerSieges.AsNoTracking()
            .Include(s => s.PlayerTo)
            .Include(s => s.PlayerFrom)
            .Include(s => s.PlayerSiegeUnits)
            .Where(s => s.Status == PlayerSiegesStatus.Active.ToDbValue())
            .ToListAsync();
    }

    public async Task<List<CountryTradePolicy>> GetOutgoingTradePoliciesForPlayerCountryAsync(Guid playerId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var countryId = await db.Players.AsNoTracking().Where(p => p.PlayersId == playerId).Select(p => p.CountriesId).FirstOrDefaultAsync();
        return await db.CountryTradePolicies.AsNoTracking().Where(policy => policy.SourceCountriesId == countryId).ToListAsync();
    }

    public async Task<PagedResult<PlayerTrade>> GetTradeHistoryPagedAsync(Guid playerId, int page, int pageSize, string? status, string? sortBy, bool sortAscending)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var query = db.PlayerTrades.AsNoTracking()
            .Include(t => t.FromPlayers)
            .Include(t => t.ToPlayers)
            .Where(t => (t.FromPlayersId == playerId || t.ToPlayersId == playerId) && t.TotalPrice > 0m);

        if (!string.IsNullOrWhiteSpace(status) && status != "all") query = query.Where(t => t.Status == status);

        var total = await query.CountAsync();

        query = ApplyTradeSorting(query, sortBy, sortAscending);

        var items = await query
            .Skip(Math.Max(0, page - 1) * Math.Max(1, pageSize))
            .Take(Math.Max(1, pageSize))
            .ToListAsync();
        return new PagedResult<PlayerTrade>(items, total, Math.Max(1, page), Math.Max(1, pageSize));
    }

    public Task<PagedResult<PlayerTrade>> GetTradeListingsPagedAsync(Guid? playerId, int page, int pageSize, string? sortBy, bool sortAscending, Guid? sellerPlayerId = null)
        => GetTradeListingsPagedInternalAsync(playerId, page, pageSize, null, sortBy, sortAscending, sellerPlayerId, null);

    public Task<PagedResult<PlayerTrade>> GetTradeListingsPagedAsync(Guid? playerId, int page, int pageSize, string? sortBy, bool sortAscending, string? resourceCode, decimal? priceFrom, decimal? priceTo, decimal? deliveryHoursFrom, decimal? deliveryHoursTo)
        => GetTradeListingsPagedInternalAsync(playerId, page, pageSize, resourceCode, sortBy, sortAscending, null, null, priceFrom, priceTo, deliveryHoursFrom, deliveryHoursTo);

    public Task<PagedResult<PlayerTrade>> GetTradeListingsPagedAsync(Guid? playerId, int page, int pageSize, string? sortBy, bool sortAscending, string? resourceCode, Guid? sellerCountryId, decimal? priceFrom, decimal? priceTo, decimal? deliveryHoursFrom, decimal? deliveryHoursTo)
        => GetTradeListingsPagedInternalAsync(playerId, page, pageSize, resourceCode, sortBy, sortAscending, null, sellerCountryId, priceFrom, priceTo, deliveryHoursFrom, deliveryHoursTo);

    public async Task<List<Country>> GetTradeListingCountriesAsync(Guid? playerId, string? resourceCode)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var query = db.PlayerTrades
            .AsNoTracking()
            .Where(t => t.Status == PlayerTradeStatus.Pending.ToDbValue() && t.TotalPrice > 0m);

        if (playerId.HasValue)
        {
            query = query.Where(t => t.FromPlayersId != playerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(resourceCode))
        {
            query = query.Where(t => t.ResourceCode == resourceCode);
        }

        var countryIds = await query
            .Select(t => t.FromPlayers.CountriesId)
            .Where(countryId => countryId != Guid.Empty)
            .Distinct()
            .ToListAsync();

        if (countryIds.Count == 0)
        {
            return [];
        }

        return await db.Countries
            .AsNoTracking()
            .Where(country => countryIds.Contains(country.CountriesId))
            .OrderBy(country => country.Name)
            .ToListAsync();
    }

    public async Task BuyTradeAsync(Guid tradeId, Guid buyerPlayerId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var trade = await db.PlayerTrades.FirstOrDefaultAsync(t => t.PlayerTradesId == tradeId) ?? throw new InvalidOperationException("Trade not found.");
        var buyer = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == buyerPlayerId) ?? throw new InvalidOperationException("Buyer not found.");
        var seller = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == trade.FromPlayersId) ?? throw new InvalidOperationException("Seller not found.");
        if (trade.Status != PlayerTradeStatus.Pending.ToDbValue()) throw new InvalidOperationException("Trade is no longer available.");
        if (seller.PlayersId == buyerPlayerId) throw new InvalidOperationException("Cannot buy your own trade.");

        var resourceCode = Enum.Parse<ResourceUnitsEnum>(trade.ResourceCode);
        var now = DateTime.UtcNow;
        var travelTimeSeconds = await CalculateTransportTravelTimeSeconds(seller, buyer);
        var buyerCountry = await db.Countries.FirstOrDefaultAsync(c => c.CountriesId == buyer.CountriesId);
        var sellerCountry = await db.Countries.FirstOrDefaultAsync(c => c.CountriesId == seller.CountriesId);
        var tradePolicy = await db.CountryTradePolicies.FirstOrDefaultAsync(policy =>
            policy.SourceCountriesId == buyer.CountriesId
            && policy.TargetCountriesId == seller.CountriesId);

        if (tradePolicy?.IsEmbargo == true)
        {
            throw new InvalidOperationException("Trade is blocked by embargo.");
        }

        var tariffPaid = CalculateTradeTariffPaid(trade.TotalPrice, tradePolicy?.TariffPercent ?? 0m);
        var buyerTotalPrice = trade.TotalPrice + tariffPaid;
        if (buyer.Money < buyerTotalPrice) throw new InvalidOperationException("Not enough money.");

        var countryTaxPercent = sellerCountry?.TaxPercent ?? 0m;
        var countryTaxPaid = Math.Max(0m, Math.Round(trade.TotalPrice * (countryTaxPercent / 100m), 2, MidpointRounding.AwayFromZero));
        var sellerNetAmount = Math.Max(0m, trade.TotalPrice - countryTaxPaid);

        buyer.Money -= buyerTotalPrice;
        seller.Money += sellerNetAmount;
        if (buyerCountry != null && tariffPaid > 0m)
        {
            buyerCountry.Money += tariffPaid;
        }

        if (sellerCountry != null && countryTaxPaid > 0m)
        {
            sellerCountry.Money += countryTaxPaid;
        }

        trade.ToPlayersId = buyerPlayerId;
        trade.Status = PlayerTradeStatus.InTransit.ToDbValue();
        trade.CountryTaxPaid = countryTaxPaid;
        trade.TransportStartTime = now;
        trade.TransportEndTime = now.AddSeconds(travelTimeSeconds);

        await db.SaveChangesAsync();
    }

    public async Task CancelTradeAsync(Guid tradeId, Guid playerId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var trade = await db.PlayerTrades.FirstOrDefaultAsync(t => t.PlayerTradesId == tradeId && t.FromPlayersId == playerId) ?? throw new InvalidOperationException("Trade not found.");
        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == playerId) ?? throw new InvalidOperationException("Player not found.");
        SetPlayerResourceAmount(player, Enum.Parse<ResourceUnitsEnum>(trade.ResourceCode), GetPlayerResourceAmount(player, Enum.Parse<ResourceUnitsEnum>(trade.ResourceCode)) + trade.Quantity);
        trade.Status = PlayerTradeStatus.Cancelled.ToDbValue();
        await db.SaveChangesAsync();
    }

    public async Task<PlayerTrade> CreateResourceTradeAsync(Guid fromPlayerId, ResourceUnitsEnum resourceCode, int quantity, decimal pricePerUnit)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == fromPlayerId) ?? throw new InvalidOperationException("Player not found.");
        if (GetPlayerResourceAmount(player, resourceCode) < quantity) throw new InvalidOperationException("Insufficient resource balance.");
        SetPlayerResourceAmount(player, resourceCode, GetPlayerResourceAmount(player, resourceCode) - quantity);
        var trade = new PlayerTrade
        {
            PlayerTradesId = Guid.NewGuid(),
            FromPlayersId = fromPlayerId,
            ResourceCode = resourceCode.ToString(),
            Quantity = quantity,
            PricePerUnit = pricePerUnit,
            TotalPrice = quantity * pricePerUnit,
            Status = PlayerTradeStatus.Pending.ToDbValue(),
            CreatedAt = DateTime.UtcNow,
            TransportStartTime = DateTime.UtcNow,
            TransportEndTime = DateTime.UtcNow
        };
        db.PlayerTrades.Add(trade);
        await db.SaveChangesAsync();
        return trade;
    }

    private async Task<PagedResult<PlayerTrade>> GetTradeListingsPagedInternalAsync(Guid? playerId, int page, int pageSize, string? resourceCode, string? sortBy, bool sortAscending, Guid? sellerPlayerId, Guid? sellerCountryId, decimal? priceFrom = null, decimal? priceTo = null, decimal? deliveryHoursFrom = null, decimal? deliveryHoursTo = null)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var query = db.PlayerTrades
            .AsNoTracking()
            .Include(t => t.FromPlayers)
            .ThenInclude(player => player.Countries)
            .Where(t => t.Status == PlayerTradeStatus.Pending.ToDbValue() && t.TotalPrice > 0m);
        if (playerId.HasValue) query = query.Where(t => t.FromPlayersId != playerId.Value);
        if (sellerPlayerId.HasValue) query = query.Where(t => t.FromPlayersId == sellerPlayerId.Value);
        if (sellerCountryId.HasValue) query = query.Where(t => t.FromPlayers.CountriesId == sellerCountryId.Value);
        if (!string.IsNullOrWhiteSpace(resourceCode)) query = query.Where(t => t.ResourceCode == resourceCode);

        Player? buyerPlayer = null;
        var buyerTransportSpeedMultiplier = 1m;
        var transportSpeedKmh = Math.Max(1m, configuration.GetValue<decimal?>("MovementSpeeds:TransportKmh") ?? 60m);
        if (playerId.HasValue)
        {
            buyerPlayer = await db.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(player => player.PlayersId == playerId.Value);
            if (buyerPlayer != null)
            {
                buyerTransportSpeedMultiplier = await premiumService.GetTransportSpeedMultiplierAsync(buyerPlayer.PlayersId);
            }
        }

        var listings = await query.ToListAsync();
        var filteredListings = listings.AsEnumerable();

        if (playerId.HasValue && buyerPlayer != null)
        {
            filteredListings = filteredListings.Where(trade => trade.FromPlayers != null);
        }

        if (priceFrom.HasValue)
        {
            filteredListings = filteredListings.Where(trade => trade.PricePerUnit >= priceFrom.Value);
        }

        if (priceTo.HasValue)
        {
            filteredListings = filteredListings.Where(trade => trade.PricePerUnit <= priceTo.Value);
        }

        if (deliveryHoursFrom.HasValue)
        {
            var minDeliverySeconds = (int)Math.Round((double)(deliveryHoursFrom.Value * 3600m));
            filteredListings = filteredListings.Where(trade => CalculateTradeDeliverySeconds(trade, buyerPlayer, transportSpeedKmh, buyerTransportSpeedMultiplier) >= minDeliverySeconds);
        }

        if (deliveryHoursTo.HasValue)
        {
            var maxDeliverySeconds = (int)Math.Round((double)(deliveryHoursTo.Value * 3600m));
            filteredListings = filteredListings.Where(trade => CalculateTradeDeliverySeconds(trade, buyerPlayer, transportSpeedKmh, buyerTransportSpeedMultiplier) <= maxDeliverySeconds);
        }

        var total = await query.CountAsync();

        var sortedListings = ApplyTradeListingSorting(filteredListings, sortBy, sortAscending, buyerPlayer, transportSpeedKmh, buyerTransportSpeedMultiplier).ToList();
        total = sortedListings.Count;
        var items = sortedListings
            .Skip(Math.Max(0, page - 1) * Math.Max(1, pageSize))
            .Take(Math.Max(1, pageSize))
            .ToList();

        return new PagedResult<PlayerTrade>(items, total, Math.Max(1, page), Math.Max(1, pageSize));
    }

    public async Task<int> ProcessCompletedTradesAsync(CancellationToken cancellationToken = default)
    {
        var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var completedTransports = await db.PlayerTrades
            .Where(t => t.Status == PlayerTradeStatus.InTransit.ToDbValue()
                        && t.ToPlayersId != null
                        && t.TransportEndTime <= now)
            .ToListAsync(cancellationToken);

        if (completedTransports.Count == 0)
        {
            return 0;
        }

        var targetPlayerIds = completedTransports
            .Where(t => t.ToPlayersId.HasValue)
            .Select(t => t.ToPlayersId!.Value)
            .Distinct()
            .ToList();

        var sourcePlayerIds = completedTransports.Select(t => t.FromPlayersId).Distinct().ToList();

        var playersById = targetPlayerIds.Count == 0
            ? new Dictionary<Guid, Player>()
            : await db.Players.Where(p => targetPlayerIds.Contains(p.PlayersId)).ToDictionaryAsync(p => p.PlayersId, cancellationToken);

        var sourcePlayersById = sourcePlayerIds.Count == 0
            ? new Dictionary<Guid, Player>()
            : await db.Players.Where(p => sourcePlayerIds.Contains(p.PlayersId)).ToDictionaryAsync(p => p.PlayersId, cancellationToken);

        var notificationsToCreate = new List<(Guid PlayerId, PlayerNotificationCode Code, PlayerNotificationDetails Details)>();

        foreach (var transport in completedTransports)
        {
            if (!transport.ToPlayersId.HasValue || !Enum.TryParse<ResourceUnitsEnum>(transport.ResourceCode, true, out var resourceCode))
            {
                transport.Status = PlayerTradeStatus.Completed.ToDbValue();
                continue;
            }

            if (playersById.TryGetValue(transport.ToPlayersId.Value, out var recipient))
            {
                SetPlayerResourceAmount(recipient, resourceCode, GetPlayerResourceAmount(recipient, resourceCode) + transport.Quantity);

                sourcePlayersById.TryGetValue(transport.FromPlayersId, out var sender);
                var senderName = sender?.Name ?? "Unknown";
                var recipientName = recipient.Name ?? "Unknown";
                notificationsToCreate.Add((
                    recipient.PlayersId,
                    PlayerNotificationCode.IncomingTransport,
                    new PlayerNotificationDetails
                    {
                        NotificationKind = "incoming_transport_arrived",
                        TradeId = transport.PlayerTradesId,
                        SenderPlayerId = transport.FromPlayersId,
                        SenderPlayerName = senderName,
                        RecipientPlayerId = recipient.PlayersId,
                        RecipientPlayerName = recipientName,
                        ResourceCode = transport.ResourceCode,
                        Quantity = transport.Quantity
                    }));
            }

            transport.Status = PlayerTradeStatus.Completed.ToDbValue();
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var notification in notificationsToCreate)
        {
            await notificationService.CreateAsync(
                notification.PlayerId,
                notification.Code,
                notification.Details,
                now,
                cancellationToken);
        }

        return completedTransports.Count;
    }

    private static IOrderedQueryable<PlayerTrade> ApplyTradeSorting(IQueryable<PlayerTrade> query, string? sortBy, bool sortAscending)
    {
        var normalizedSort = sortBy?.Trim();

        return normalizedSort switch
        {
            "price" => sortAscending ? query.OrderBy(t => t.PricePerUnit).ThenByDescending(t => t.CreatedAt) : query.OrderByDescending(t => t.PricePerUnit).ThenByDescending(t => t.CreatedAt),
            "quantity" => sortAscending ? query.OrderBy(t => t.Quantity).ThenByDescending(t => t.CreatedAt) : query.OrderByDescending(t => t.Quantity).ThenByDescending(t => t.CreatedAt),
            "product" => sortAscending ? query.OrderBy(t => t.ResourceCode).ThenByDescending(t => t.CreatedAt) : query.OrderByDescending(t => t.ResourceCode).ThenByDescending(t => t.CreatedAt),
            "seller" => sortAscending ? query.OrderBy(t => t.FromPlayers.Name).ThenByDescending(t => t.CreatedAt) : query.OrderByDescending(t => t.FromPlayers.Name).ThenByDescending(t => t.CreatedAt),
            "status" => sortAscending ? query.OrderBy(t => t.Status).ThenByDescending(t => t.CreatedAt) : query.OrderByDescending(t => t.Status).ThenByDescending(t => t.CreatedAt),
            "created" or "createdat" => sortAscending ? query.OrderBy(t => t.CreatedAt).ThenBy(t => t.PlayerTradesId) : query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.PlayerTradesId),
            _ => sortAscending ? query.OrderBy(t => t.CreatedAt).ThenBy(t => t.PlayerTradesId) : query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.PlayerTradesId)
        };
    }

    private static IOrderedEnumerable<PlayerTrade> ApplyTradeListingSorting(IEnumerable<PlayerTrade> listings, string? sortBy, bool sortAscending, Player? buyerPlayer, decimal transportSpeedKmh, decimal buyerTransportSpeedMultiplier)
    {
        var normalizedSort = sortBy?.Trim();

        return normalizedSort switch
        {
            "type" => OrderListings(listings, trade => trade.ToPlayersId.HasValue ? 1 : 0, sortAscending),
            "product" => OrderListings(listings, trade => trade.ResourceCode, sortAscending),
            "quantity" => OrderListings(listings, trade => trade.Quantity, sortAscending),
            "price" => OrderListings(listings, trade => trade.PricePerUnit, sortAscending),
            "seller" => OrderListings(listings, trade => trade.FromPlayers?.Name ?? string.Empty, sortAscending),
            "country" => OrderListings(listings, trade => trade.FromPlayers?.Countries?.Name ?? string.Empty, sortAscending),
            "delivery" => OrderListings(listings, trade => CalculateTradeDeliverySeconds(trade, buyerPlayer, transportSpeedKmh, buyerTransportSpeedMultiplier), sortAscending),
            "created" or "createdat" => OrderListings(listings, trade => trade.CreatedAt ?? DateTime.MinValue, sortAscending),
            _ => OrderListings(listings, trade => trade.CreatedAt ?? DateTime.MinValue, sortAscending: false)
        };
    }

    private static IOrderedEnumerable<PlayerTrade> OrderListings<TKey>(IEnumerable<PlayerTrade> listings, Func<PlayerTrade, TKey> keySelector, bool sortAscending)
        => sortAscending
            ? listings.OrderBy(keySelector).ThenByDescending(trade => trade.CreatedAt ?? DateTime.MinValue)
            : listings.OrderByDescending(keySelector).ThenByDescending(trade => trade.CreatedAt ?? DateTime.MinValue);

    private static int CalculateTradeDeliverySeconds(PlayerTrade trade, Player? buyerPlayer, decimal transportSpeedKmh, decimal buyerTransportSpeedMultiplier)
    {
        if (trade.ToPlayersId.HasValue && trade.TransportEndTime > trade.TransportStartTime)
        {
            return Math.Max(0, (int)Math.Ceiling((trade.TransportEndTime - trade.TransportStartTime).TotalSeconds));
        }

        if (buyerPlayer == null || trade.FromPlayers == null)
        {
            return int.MaxValue;
        }

        var distanceKm = CalculateDistanceKm(trade.FromPlayers.LocationY, trade.FromPlayers.LocationX, buyerPlayer.LocationY, buyerPlayer.LocationX);
        return Math.Max(60, (int)Math.Round((double)(distanceKm / Math.Max(1m, transportSpeedKmh * buyerTransportSpeedMultiplier) * 3600m)));
    }

    public static decimal CalculateTradeTariffPaid(decimal totalPrice, decimal tariffPercent)
        => Math.Max(0m, Math.Round(totalPrice * (Math.Max(0m, tariffPercent) / 100m), 2, MidpointRounding.AwayFromZero));

    private static decimal GetPlayerResourceAmount(Player player, ResourceUnitsEnum resourceCode)
    {
        return resourceCode switch
        {
            ResourceUnitsEnum.Oil => player.Oil,
            ResourceUnitsEnum.Uranium => player.Uranium,
            ResourceUnitsEnum.Chips => player.Chips,
            _ => 0m
        };
    }

    private static void SetPlayerResourceAmount(Player player, ResourceUnitsEnum resourceCode, decimal value)
    {
        var safeValue = Math.Max(0m, value);
        switch (resourceCode)
        {
            case ResourceUnitsEnum.Oil: player.Oil = safeValue; break;
            case ResourceUnitsEnum.Uranium: player.Uranium = safeValue; break;
            case ResourceUnitsEnum.Chips: player.Chips = safeValue; break;
            default: throw new InvalidOperationException("Invalid resource code.");
        }
    }

    private async Task<int> CalculateTransportTravelTimeSeconds(Player fromPlayer, Player toPlayer)
    {
        var transportSpeedKmh = Math.Max(1m, configuration.GetValue<decimal?>("MovementSpeeds:TransportKmh") ?? 60m);
        var premiumMultiplier = await premiumService.GetTransportSpeedMultiplierAsync(toPlayer.PlayersId);
        var distanceKm = CalculateDistanceKm(fromPlayer.LocationY, fromPlayer.LocationX, toPlayer.LocationY, toPlayer.LocationX);
        return Math.Max(60, (int)Math.Round((double)(distanceKm / Math.Max(1m, transportSpeedKmh * premiumMultiplier) * 3600m)));
    }

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
