using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;

using VoltrokEF;
using VoltrokServices.Services.Players;
using VoltrokUtils.Engines;
using VoltrokUtils.Enums;
using VoltrokUtils.Models;

namespace VoltrokWorker.BackgroundWorker;

public class NpcWorldSimulationWorkerService(
    PlayerTradeService playerTradeService,
    IDbContextFactory<AppDbContext> dbContextFactory) : BackgroundService
{
    private readonly Dictionary<Guid, DateTime> _lastAttackByNpc = [];
    private readonly Dictionary<(Guid AttackerId, Guid TargetId), DateTime> _lastAttackByPair = [];
    private readonly Dictionary<string, DateTime> _lastOfferBySellerResource = [];

    private int IntervalSeconds => 5;
    private int MinMilitaryToAttack => 10;
    private int AttackIntervalMinutes => 1;
    private decimal TravelSpeedKmh => 360;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("NPC world simulation worker started. Interval: {IntervalSeconds}s.", IntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SimulateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while simulating NPC gameplay");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SimulateAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var npcs = await db.Players.AsNoTracking().Where(p => p.IsNpc).Select(p => new NpcPlayer(
            p.PlayersId, p.Name, p.Money, p.LocationX, p.LocationY, p.CountriesId, p.Oil, p.Uranium, p.Chips)).ToListAsync(cancellationToken);
        if (npcs.Count == 0) return;

        CleanupState(npcs.Select(x => x.Id).ToHashSet(), now);
        var incoming = await LoadIncomingAsync(db, cancellationToken);
        var trades = await LoadTradesAsync(db, cancellationToken);

        var buys = await BuyResourcesAsync(npcs, trades, incoming);
        var offers = await CreateOffersAsync(db, npcs, now, cancellationToken);
        var recruitment = await RecruitMilitaryAsync(db, npcs, cancellationToken);
        var attacks = await CreateAttacksAsync(db, npcs, now, cancellationToken);

        Log.Information("NPC tick {Now}: Npcs={NpcCount}, Buys={Buys}, Offers={Offers}, Recruitments={Recruitments}, Attacks={Attacks}.",
            now, npcs.Count, buys.Buys, offers.Offers, recruitment.Orders, attacks.Attacks);
    }

    private async Task<BuyReport> BuyResourcesAsync(
        IReadOnlyCollection<NpcPlayer> npcs,
        List<OpenTrade> trades,
        Dictionary<(Guid, ResourceUnitsEnum), int> incoming)
    {
        var tradeBuyBudget = ResolveTradeBuyBudget(npcs.Count, trades.Count);
        var buys = 0;
        var spent = 0m;
        foreach (var npc in npcs.OrderByDescending(n => TotalDeficit(n, incoming)))
        {
            if (buys >= tradeBuyBudget) break;
            var buyerMoney = npc.Money;
            var buyerBuys = 0;
            var skipped = new HashSet<Guid>();
            var buyCapacity = ResolveNpcBuyCapacity(npc, incoming);
            while (buys < tradeBuyBudget && buyerBuys < buyCapacity)
            {
                var deficits = Deficits(npc, incoming);
                if (deficits.Count == 0) break;
                var best = PickTrade(npc, buyerMoney, deficits, trades, skipped);
                if (best is null) break;
                try
                {
                    await playerTradeService.BuyTradeAsync(best.Id, npc.Id);
                    buys++;
                    buyerBuys++;
                    buyerMoney -= best.TotalPrice;
                    spent += best.TotalPrice;
                    trades.Remove(best);
                    var key = (npc.Id, best.Resource);
                    incoming[key] = incoming.GetValueOrDefault(key) + best.Quantity;
                }
                catch
                {
                    skipped.Add(best.Id);
                }
            }
        }

        return new BuyReport(buys, spent);
    }

    private async Task<OfferReport> CreateOffersAsync(AppDbContext db, IReadOnlyCollection<NpcPlayer> npcs, DateTime now, CancellationToken ct)
    {
        var npcIds = npcs.Select(x => x.Id).ToHashSet();
        var pending = await db.PlayerTrades.AsNoTracking()
            .Where(t => t.Status == PlayerTradeStatus.Pending.ToDbValue() && t.ToPlayersId == null && npcIds.Contains(t.FromPlayersId))
            .Select(t => new { t.FromPlayersId, t.ResourceCode }).ToListAsync(ct);
        var counts = pending.GroupBy(x => x.FromPlayersId).ToDictionary(g => g.Key, g => g.Count());
        var byResource = pending.GroupBy(x => x.FromPlayersId).ToDictionary(
            g => g.Key, g => g.Select(x => NormalizeResourceKey(x.ResourceCode)).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var offers = 0;
        foreach (var npc in npcs.OrderBy(_ => Random.Shared.Next()))
        {
            var current = counts.GetValueOrDefault(npc.Id);
            var offerCapacity = ResolveNpcOfferCapacity(npc);
            if (current >= offerCapacity) continue;
            foreach (var entry in ResourceEntries(npc).OrderByDescending(x => x.Quantity))
            {
                if (current >= offerCapacity) break;
                var qty = DecimalQty(entry.Quantity);
                var availableForTrade = Math.Max(0, qty - 1);
                if (availableForTrade <= 0) continue;
                var totalInventory = TotalResourceInventory(npc);
                var reserve = DesiredReserve(npc, entry.Resource);
                var surplus = qty - reserve;
                var key = NormalizeResourceKey(entry.Resource.ToString());
                if (byResource.TryGetValue(npc.Id, out var resources) && resources.Contains(key)) continue;
                var cooldown = $"{npc.Id:N}:{key}";
                if (_lastOfferBySellerResource.TryGetValue(cooldown, out var last) && now - last < TimeSpan.FromMinutes(30)) continue;
                var targetOfferQuantity = surplus > 0
                    ? surplus
                    : Math.Min(availableForTrade, GetFallbackOfferQuantity(totalInventory));
                var listQty = CalculateOfferQuantity(targetOfferQuantity, totalInventory, availableForTrade);
                if (listQty <= 0) continue;
                try
                {
                    await playerTradeService.CreateResourceTradeAsync(npc.Id, entry.Resource, listQty, AskPrice(entry.Resource, qty));
                    current++;
                    counts[npc.Id] = current;
                    if (!byResource.TryGetValue(npc.Id, out resources))
                    {
                        resources = [];
                        byResource[npc.Id] = resources;
                    }
                    resources.Add(key);
                    _lastOfferBySellerResource[cooldown] = now;
                    offers++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "NPC {NpcId} failed to create {Resource} trade offer. Qty={Quantity}, Price={PricePerUnit}.",
                        npc.Id, entry.Resource, listQty, AskPrice(entry.Resource, qty));
                }
            }
        }

        return new OfferReport(offers);
    }

    private async Task<AttackReport> CreateAttacksAsync(AppDbContext db, IReadOnlyCollection<NpcPlayer> npcs, DateTime now, CancellationToken ct)
    {
        var npcCountryIds = npcs.Where(x => x.CountryId.HasValue).Select(x => x.CountryId!.Value).Distinct().ToHashSet();
        if (npcCountryIds.Count == 0) return new AttackReport(0);

        var wars = await db.CountryWars.AsNoTracking()
            .Where(w => w.Status == CountryWarStatus.Active.ToDbValue() && (npcCountryIds.Contains(w.CountryFromId) || npcCountryIds.Contains(w.CountryToId)))
            .Select(w => new War(w.CountryFromId, w.CountryToId)).ToListAsync(ct);
        if (wars.Count == 0) return new AttackReport(0);

        var units = await db.PlayerMilitaryUnits.AsNoTracking().Where(x => x.Quantity > 0)
            .Select(x => new UnitRow(x.PlayersId, x.MilitaryUnitsCode, x.Level, x.Quantity)).ToListAsync(ct);
        var rosterByNpc = units.Where(x => npcs.Any(n => n.Id == x.PlayerId)).GroupBy(x => x.PlayerId)
            .ToDictionary(g => g.Key, g => BuildRoster(g.ToList()));
        var powerByPlayer = units.GroupBy(x => x.PlayerId).ToDictionary(g => g.Key, g => g.Sum(x => (decimal)(Math.Max(1, x.Level) * x.Quantity)));
        var outgoing = await db.PlayerMilitaryTransports.AsNoTracking()
            .Where(t => (t.Status == PlayerTransportStatus.InProgress.ToDbValue() || t.Status == PlayerTransportStatus.InSiege.ToDbValue()) && (t.EndTime > now || t.Status == PlayerTransportStatus.InSiege.ToDbValue()))
            .GroupBy(t => t.PlayerFromId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var targets = await db.Players.AsNoTracking().Select(p => new Target(p.PlayersId, p.LocationX, p.LocationY, p.CountriesId)).ToListAsync(ct);

        var attacks = 0;
        var attackInterval = TimeSpan.FromMinutes(AttackIntervalMinutes);
        foreach (var npc in npcs.OrderBy(_ => Random.Shared.Next()))
        {
            if (!npc.CountryId.HasValue || !rosterByNpc.TryGetValue(npc.Id, out var roster) || roster.TotalQty < MinMilitaryToAttack) continue;
            var outgoingCapacity = ResolveOutgoingMilitaryCapacity(roster);
            if (outgoing.GetValueOrDefault(npc.Id) >= outgoingCapacity) continue;
            if (_lastAttackByNpc.TryGetValue(npc.Id, out var lastAttack) && now - lastAttack < attackInterval) continue;

            var enemyCountryIds = wars.Where(w => w.From == npc.CountryId.Value || w.To == npc.CountryId.Value)
                .Select(w => w.From == npc.CountryId.Value ? w.To : w.From).ToHashSet();
            if (enemyCountryIds.Count == 0) continue;

            var best = PickTarget(npc, roster, targets.Where(t => t.PlayerId != npc.Id && t.CountryId.HasValue && enemyCountryIds.Contains(t.CountryId.Value))
                .Select(t => new TargetScore(t.PlayerId, t.X, t.Y, powerByPlayer.GetValueOrDefault(t.PlayerId))).ToList(), powerByPlayer.GetValueOrDefault(npc.Id), now);
            if (best is null) continue;

            var qty = Math.Max(1, Math.Min(roster.BestQty / 2, 70));
            var transport = new PlayerMilitaryTransport
            {
                PlayerMilitaryTransportsId = Guid.NewGuid(),
                PlayerFromId = npc.Id,
                PlayerToId = best.PlayerId,
                MissionType = PlayerMilitaryTransportMissionType.Attack.ToDbValue(),
                StartTime = now,
                EndTime = now.AddSeconds(TravelSeconds(npc.Y, npc.X, best.Y, best.X)),
                Status = PlayerTransportStatus.InProgress.ToDbValue(),
                AutoReturnAfterBattle = false
            };
            db.PlayerMilitaryTransports.Add(transport);
            db.PlayerMilitaryTransportUnits.Add(new PlayerMilitaryTransportUnit
            {
                PlayerMilitaryTransportUnitsId = Guid.NewGuid(),
                PlayerMilitaryTransportsId = transport.PlayerMilitaryTransportsId,
                MilitaryUnitsCode = roster.BestCode,
                Level = roster.BestLevel,
                Quantity = qty
            });
            await db.SaveChangesAsync(ct);

            _lastAttackByNpc[npc.Id] = now;
            _lastAttackByPair[(npc.Id, best.PlayerId)] = now;
            outgoing[npc.Id] = outgoing.GetValueOrDefault(npc.Id) + 1;
            attacks++;
        }

        return new AttackReport(attacks);
    }

    private async Task<RecruitmentReport> RecruitMilitaryAsync(AppDbContext db, IReadOnlyCollection<NpcPlayer> npcs, CancellationToken ct)
    {
        if (npcs.Count == 0)
        {
            return new RecruitmentReport(0);
        }

        var npcIds = npcs.Select(x => x.Id).ToHashSet();
        var players = await db.Players
            .Where(p => p.IsNpc && npcIds.Contains(p.PlayersId))
            .ToDictionaryAsync(p => p.PlayersId, ct);
        if (players.Count == 0)
        {
            return new RecruitmentReport(0);
        }

        var units = await db.PlayerMilitaryUnits.AsNoTracking()
            .Where(x => npcIds.Contains(x.PlayersId) && x.Quantity > 0)
            .Select(x => new UnitRow(x.PlayersId, x.MilitaryUnitsCode, x.Level, x.Quantity))
            .ToListAsync(ct);
        var queuedOrders = await db.PlayerMilitaryProductions.AsNoTracking()
            .Where(x => npcIds.Contains(x.PlayersId) && x.Quantity > x.ProducedQuantity)
            .Select(x => new QueuedRow(x.PlayersId, x.MilitaryUnitsCode, x.Quantity - x.ProducedQuantity))
            .ToListAsync(ct);
        var wars = await db.CountryWars.AsNoTracking()
            .Where(w => w.Status == CountryWarStatus.Active.ToDbValue())
            .Select(w => new War(w.CountryFromId, w.CountryToId))
            .ToListAsync(ct);

        var activeWarCountries = wars
            .SelectMany(w => new[] { w.From, w.To })
            .ToHashSet();
        var existingByNpc = units
            .GroupBy(x => x.PlayerId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var queuedByNpc = queuedOrders
            .GroupBy(x => x.PlayerId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var activeOrderCounts = queuedOrders
            .GroupBy(x => x.PlayerId)
            .ToDictionary(g => g.Key, g => g.Count());

        var recruitments = 0;
        foreach (var npc in npcs.OrderBy(x => activeWarCountries.Contains(x.CountryId ?? Guid.Empty) ? 0 : 1).ThenBy(_ => Random.Shared.Next()))
        {
            if (!players.TryGetValue(npc.Id, out var player))
            {
                continue;
            }

            var inWar = npc.CountryId.HasValue && activeWarCountries.Contains(npc.CountryId.Value);
            var maxActiveOrderSlots = ResolveActiveRecruitmentSlotCapacity(inWar);
            var remainingOrderSlots = maxActiveOrderSlots - activeOrderCounts.GetValueOrDefault(npc.Id);
            if (remainingOrderSlots <= 0)
            {
                continue;
            }

            var offensiveQty = GetUnitQuantity(existingByNpc, queuedByNpc, npc.Id, isDefensive: false);
            var defensiveQty = GetUnitQuantity(existingByNpc, queuedByNpc, npc.Id, isDefensive: true);
            var desiredOffensive = inWar ? Math.Max(MinMilitaryToAttack * 2, 24) : Math.Max(MinMilitaryToAttack, 12);
            var desiredDefensive = inWar ? Math.Max(18, MinMilitaryToAttack) : Math.Max(12, MinMilitaryToAttack / 2);

            var recruitmentNeed = Math.Max(0, desiredOffensive - offensiveQty) + Math.Max(0, desiredDefensive - defensiveQty);
            var recruitAttempts = Math.Min(remainingOrderSlots, ResolveRecruitmentAttempts(player, inWar, recruitmentNeed));
            for (var i = 0; i < recruitAttempts; i++)
            {
                var unit = PickRecruitmentUnit(player, offensiveQty, defensiveQty, desiredOffensive, desiredDefensive, inWar);
                if (unit is null)
                {
                    break;
                }

                var moneyReserve = inWar ? 120m : 180m;
                var availableMoney = Math.Max(0m, player.Money - moneyReserve);
                var costPerUnit = Math.Max(1m, unit.StartingCost);
                var maxAffordable = (int)Math.Floor(availableMoney / costPerUnit);
                if (maxAffordable <= 0)
                {
                    break;
                }

                var deficit = unit.IsDefensive
                    ? Math.Max(1, desiredDefensive - defensiveQty)
                    : Math.Max(1, desiredOffensive - offensiveQty);
                var quantity = Math.Max(1, Math.Min(deficit, Math.Min(maxAffordable, unit.IsDefensive ? 4 : 6)));
                var totalCost = costPerUnit * quantity;
                if (quantity <= 0 || totalCost > player.Money)
                {
                    break;
                }

                player.Money -= totalCost;
                db.PlayerMilitaryProductions.Add(new PlayerMilitaryProduction
                {
                    PlayerMilitaryOrdersId = Guid.NewGuid(),
                    PlayersId = player.PlayersId,
                    MilitaryUnitsCode = unit.Code.ToString(),
                    Quantity = quantity,
                    ProducedQuantity = 0,
                    Level = 1,
                    CreatedAt = DateTime.UtcNow
                });

                if (unit.IsDefensive)
                {
                    defensiveQty += quantity;
                }
                else
                {
                    offensiveQty += quantity;
                }

                recruitments++;
                activeOrderCounts[npc.Id] = activeOrderCounts.GetValueOrDefault(npc.Id) + 1;
                if (activeOrderCounts[npc.Id] >= maxActiveOrderSlots)
                {
                    break;
                }
            }
        }

        if (recruitments > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new RecruitmentReport(recruitments);
    }

    private void CleanupState(HashSet<Guid> activeNpcIds, DateTime now)
    {
        foreach (var id in _lastAttackByNpc.Keys.Where(id => !activeNpcIds.Contains(id)).ToList()) _lastAttackByNpc.Remove(id);
        foreach (var key in _lastOfferBySellerResource.Where(x => now - x.Value > TimeSpan.FromHours(4)).Select(x => x.Key).ToList()) _lastOfferBySellerResource.Remove(key);
        foreach (var key in _lastAttackByPair.Where(x => now - x.Value > TimeSpan.FromHours(6)).Select(x => x.Key).ToList()) _lastAttackByPair.Remove(key);
    }

    private async Task<List<OpenTrade>> LoadTradesAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.PlayerTrades.AsNoTracking()
            .Where(t => t.Status == PlayerTradeStatus.Pending.ToDbValue() && t.ToPlayersId == null)
            .OrderBy(t => t.PricePerUnit)
            .ThenBy(t => t.CreatedAt)
            .Take(4000)
            .Select(t => new { t.PlayerTradesId, t.FromPlayersId, t.ResourceCode, t.Quantity, t.PricePerUnit, t.TotalPrice, X = t.FromPlayers.LocationX, Y = t.FromPlayers.LocationY })
            .ToListAsync(ct);
        return rows.Where(x => TryParseResource(x.ResourceCode, out _)).Select(x => new OpenTrade(
            x.PlayerTradesId, x.FromPlayersId, ParseResource(x.ResourceCode), DecimalQty(x.Quantity), x.PricePerUnit, x.TotalPrice, x.X, x.Y))
            .Where(x => x.Quantity > 0).ToList();
    }

    private async Task<Dictionary<(Guid, ResourceUnitsEnum), int>> LoadIncomingAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.PlayerTrades.AsNoTracking()
            .Where(t => t.Status == PlayerTradeStatus.InTransit.ToDbValue() && t.ToPlayersId.HasValue)
            .Select(t => new { PlayerId = t.ToPlayersId, t.ResourceCode, t.Quantity })
            .ToListAsync(ct);
        return rows.Where(x => x.PlayerId.HasValue && TryParseResource(x.ResourceCode, out _))
            .GroupBy(x => (x.PlayerId!.Value, ParseResource(x.ResourceCode)))
            .ToDictionary(g => g.Key, g => g.Sum(x => DecimalQty(x.Quantity)));
    }

    private Dictionary<ResourceUnitsEnum, int> Deficits(NpcPlayer npc, IReadOnlyDictionary<(Guid, ResourceUnitsEnum), int> incoming)
    {
        var result = new Dictionary<ResourceUnitsEnum, int>();
        foreach (var resource in Enum.GetValues<ResourceUnitsEnum>())
        {
            var deficit = DesiredReserve(npc, resource) - (Qty(npc, resource) + incoming.GetValueOrDefault((npc.Id, resource)));
            if (deficit > 0) result[resource] = deficit;
        }
        return result;
    }

    private int TotalDeficit(NpcPlayer npc, IReadOnlyDictionary<(Guid, ResourceUnitsEnum), int> incoming) => Deficits(npc, incoming).Values.Sum();

    private OpenTrade? PickTrade(NpcPlayer npc, decimal money, IReadOnlyDictionary<ResourceUnitsEnum, int> deficits, IReadOnlyCollection<OpenTrade> trades, ISet<Guid> skipped)
    {
        OpenTrade? best = null;
        var bestScore = decimal.MinValue;
        foreach (var trade in trades)
        {
            if (skipped.Contains(trade.Id) || trade.SellerId == npc.Id || trade.TotalPrice > money) continue;
            if (!deficits.TryGetValue(trade.Resource, out var deficit) || deficit <= 0) continue;
            var distance = DistanceKm(npc.Y, npc.X, trade.Y, trade.X);
            var hours = Math.Max(1m / 60m, distance / Math.Max(1m, TravelSpeedKmh));
            var useful = Math.Min(deficit, trade.Quantity);
            var score = useful * 2.5m - trade.PricePerUnit * 1.35m - hours * 0.45m - distance * 0.015m;
            if (score > bestScore) { bestScore = score; best = trade; }
        }
        return best;
    }

    private TargetScore? PickTarget(NpcPlayer npc, Roster roster, IReadOnlyCollection<TargetScore> targets, decimal attackerPower, DateTime now)
    {
        TargetScore? best = null;
        var bestScore = decimal.MinValue;
        foreach (var target in targets)
        {
            var distance = DistanceKm(npc.Y, npc.X, target.Y, target.X);
            var relative = attackerPower <= 0m ? 0m : (attackerPower - target.Power) / Math.Max(1m, attackerPower);
            var penalty = _lastAttackByPair.TryGetValue((npc.Id, target.PlayerId), out var last) && now - last < TimeSpan.FromMinutes(Math.Max(10, AttackIntervalMinutes / 2)) ? 0.45m : 0m;
            var score = relative * 2.8m + Math.Min(1.5m, roster.TotalQty / 50m) - distance * 0.01m - penalty;
            if (score > bestScore) { bestScore = score; best = target; }
        }
        return bestScore > -1.25m ? best : null;
    }

    private IEnumerable<ResourceRow> ResourceEntries(NpcPlayer npc)
    {
        yield return new ResourceRow(ResourceUnitsEnum.Oil, npc.Oil);
        yield return new ResourceRow(ResourceUnitsEnum.Uranium, npc.Uranium);
        yield return new ResourceRow(ResourceUnitsEnum.Chips, npc.Chips);
    }

    private int DesiredReserve(NpcPlayer npc, ResourceUnitsEnum resource)
    {
        var preferenceRank = GetPreferenceRank(npc.Id, resource);
        var totalInventory = TotalResourceInventory(npc);

        if (totalInventory < 150)
        {
            return preferenceRank switch
            {
                0 => 34,
                1 => 24,
                _ => 14
            };
        }

        if (totalInventory < 300)
        {
            return preferenceRank switch
            {
                0 => 60,
                1 => 36,
                _ => 20
            };
        }

        return preferenceRank switch
        {
            0 => resource switch
            {
                ResourceUnitsEnum.Oil => 420,
                ResourceUnitsEnum.Uranium => 360,
                ResourceUnitsEnum.Chips => 390,
                _ => 300
            },
            1 => resource switch
            {
                ResourceUnitsEnum.Oil => 180,
                ResourceUnitsEnum.Uranium => 150,
                ResourceUnitsEnum.Chips => 170,
                _ => 120
            },
            _ => resource switch
            {
                ResourceUnitsEnum.Oil => 70,
                ResourceUnitsEnum.Uranium => 60,
                ResourceUnitsEnum.Chips => 65,
                _ => 45
            }
        };
    }

    private static int ResolveTradeBuyBudget(int npcCount, int openTradesCount)
        => Math.Max(8, Math.Min(openTradesCount, npcCount * 3));

    private int ResolveNpcBuyCapacity(NpcPlayer npc, IReadOnlyDictionary<(Guid, ResourceUnitsEnum), int> incoming)
    {
        var deficits = Deficits(npc, incoming);
        if (deficits.Count == 0)
        {
            return 0;
        }

        var totalDeficit = deficits.Values.Sum();
        var deficitDrivenCapacity = Math.Max(1, (int)Math.Ceiling(totalDeficit / 25m));
        var cashDrivenCapacity = npc.Money switch
        {
            < 100m => 1,
            < 250m => 2,
            < 600m => 3,
            _ => 4
        };

        return Math.Min(deficitDrivenCapacity, cashDrivenCapacity);
    }

    private static int ResolveNpcOfferCapacity(NpcPlayer npc)
    {
        var totalInventory = TotalResourceInventory(npc);
        return totalInventory switch
        {
            < 40 => 1,
            < 140 => 2,
            _ => 3
        };
    }

    private static int ResolveOutgoingMilitaryCapacity(Roster roster)
        => roster.TotalQty switch
        {
            < 40 => 1,
            < 120 => 2,
            _ => 3
        };

    private static int ResolveActiveRecruitmentSlotCapacity(bool inWar)
        => inWar ? 6 : 3;

    private static int ResolveRecruitmentAttempts(Player player, bool inWar, int recruitmentNeed)
    {
        if (recruitmentNeed <= 0)
        {
            return 0;
        }

        var needDriven = Math.Max(1, (int)Math.Ceiling(recruitmentNeed / (inWar ? 5m : 7m)));
        var moneyDriven = player.Money switch
        {
            < 150m => 1,
            < 400m => 2,
            < 900m => 3,
            _ => 4
        };

        return Math.Min(needDriven, moneyDriven);
    }

    private static int TotalResourceInventory(NpcPlayer npc)
        => DecimalQty(npc.Oil) + DecimalQty(npc.Uranium) + DecimalQty(npc.Chips);

    private static int GetFallbackOfferQuantity(int totalInventory)
        => totalInventory < 150 ? 1 : 2;

    private static int CalculateOfferQuantity(int desiredQuantity, int totalInventory, int availableForTrade)
    {
        if (availableForTrade <= 0 || desiredQuantity <= 0)
        {
            return 0;
        }

        if (totalInventory < 150)
        {
            var proposed = Math.Max(1, Math.Min(desiredQuantity, Math.Max(1, desiredQuantity / 2)));
            return Math.Max(1, Math.Min(availableForTrade, proposed));
        }

        var standard = Math.Max(2, Math.Min(desiredQuantity, Math.Max(2, desiredQuantity / 2)));
        return Math.Max(1, Math.Min(availableForTrade, standard));
    }

    private static int GetPreferenceRank(Guid npcId, ResourceUnitsEnum resource)
    {
        var bytes = npcId.ToByteArray();
        var ordered = new[]
        {
            ResourceUnitsEnum.Oil,
            ResourceUnitsEnum.Uranium,
            ResourceUnitsEnum.Chips
        };

        if ((bytes[1] & 1) == 1)
        {
            Array.Reverse(ordered);
        }

        var rotation = bytes[0] % ordered.Length;
        for (var i = 0; i < ordered.Length; i++)
        {
            if (ordered[(i + rotation) % ordered.Length] == resource)
            {
                return i;
            }
        }

        return ordered.Length - 1;
    }

    private static decimal AskPrice(ResourceUnitsEnum resource, int currentQty)
    {
        var basePrice = resource switch
        {
            ResourceUnitsEnum.Oil => 5.5m,
            ResourceUnitsEnum.Uranium => 11.0m,
            ResourceUnitsEnum.Chips => 8.0m,
            _ => 5m
        };
        var modifier = currentQty switch
        {
            < 20 => 1.35m,
            < 40 => 1.15m,
            > 120 => 0.82m,
            > 80 => 0.90m,
            _ => 1m
        };
        return Math.Round(basePrice * modifier, 2);
    }

    private static Roster BuildRoster(IReadOnlyCollection<UnitRow> units)
    {
        var best = units.OrderByDescending(x => x.Level).ThenByDescending(x => x.Quantity).First();
        return new Roster(best.Code, best.Level, best.Quantity, units.Sum(x => x.Quantity));
    }

    private static int GetUnitQuantity(
        IReadOnlyDictionary<Guid, List<UnitRow>> existingByNpc,
        IReadOnlyDictionary<Guid, List<QueuedRow>> queuedByNpc,
        Guid npcId,
        bool isDefensive)
    {
        var total = 0;

        if (existingByNpc.TryGetValue(npcId, out var existing))
        {
            total += existing
                .Where(x => TryResolveMilitaryUnit(x.Code, out var unit) && unit.IsDefensive == isDefensive)
                .Sum(x => Math.Max(0, x.Quantity));
        }

        if (queuedByNpc.TryGetValue(npcId, out var queued))
        {
            total += queued
                .Where(x => TryResolveMilitaryUnit(x.Code, out var unit) && unit.IsDefensive == isDefensive)
                .Sum(x => Math.Max(0, x.Quantity));
        }

        return total;
    }

    private static MilitaryUnit? PickRecruitmentUnit(
        Player player,
        int offensiveQty,
        int defensiveQty,
        int desiredOffensive,
        int desiredDefensive,
        bool inWar)
    {
        if (defensiveQty < desiredDefensive)
        {
            return ChooseAffordableUnit(player.Money, MilitaryUnitEngine.MilitaryUnitsAllData
                .Where(x => x.IsDefensive)
                .OrderBy(x => x.Cost)
                .ThenBy(x => x.ProductionTime));
        }

        if (offensiveQty < desiredOffensive)
        {
            var offensivePool = inWar
                ? MilitaryUnitEngine.MilitaryUnitsAllData.Where(x => !x.IsDefensive).OrderByDescending(x => x.OffensivePoints).ThenBy(x => x.Cost)
                : MilitaryUnitEngine.MilitaryUnitsAllData.Where(x => !x.IsDefensive).OrderBy(x => x.Cost).ThenByDescending(x => x.OffensivePoints);
            return ChooseAffordableUnit(player.Money, offensivePool);
        }

        if (player.Money >= 500m)
        {
            return ChooseAffordableUnit(player.Money, MilitaryUnitEngine.MilitaryUnitsAllData
                .OrderBy(x => x.IsDefensive)
                .ThenByDescending(x => x.OffensivePoints + x.DefensivePoints)
                .ThenBy(x => x.Cost));
        }

        return null;
    }

    private static MilitaryUnit? ChooseAffordableUnit(decimal money, IEnumerable<MilitaryUnit> candidates)
        => candidates.FirstOrDefault(x => x.StartingCost <= Math.Max(0m, money - 100m))
           ?? candidates.FirstOrDefault(x => x.StartingCost <= money);

    private static bool TryResolveMilitaryUnit(string? code, out MilitaryUnit unit)
    {
        unit = default!;
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();
        var match = MilitaryUnitEngine.MilitaryUnitsAllData.FirstOrDefault(x => string.Equals(x.Code.ToString(), normalized, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        unit = match;
        return true;
    }

    private int TravelSeconds(decimal fromLat, decimal fromLng, decimal toLat, decimal toLng)
        => Math.Max(60, (int)Math.Round((double)(DistanceKm(fromLat, fromLng, toLat, toLng) / Math.Max(1m, TravelSpeedKmh) * 3600m)));

    private static int Qty(NpcPlayer npc, ResourceUnitsEnum resource) => resource switch
    {
        ResourceUnitsEnum.Oil => DecimalQty(npc.Oil),
        ResourceUnitsEnum.Uranium => DecimalQty(npc.Uranium),
        ResourceUnitsEnum.Chips => DecimalQty(npc.Chips),
        _ => 0
    };

    private static int DecimalQty(decimal value) => value <= 0m ? 0 : (int)Math.Floor(value);
    private static string NormalizeResourceKey(string value) => value.Trim().ToLowerInvariant();
    private static bool TryParseResource(string? value, out ResourceUnitsEnum resource) => Enum.TryParse(value, true, out resource);
    private static ResourceUnitsEnum ParseResource(string value) => Enum.Parse<ResourceUnitsEnum>(value, true);

    private static decimal DistanceKm(decimal fromLat, decimal fromLng, decimal toLat, decimal toLng)
    {
        const double radiusKm = 6371.0;
        var fromLatRad = ToRadians((double)fromLat);
        var fromLngRad = ToRadians((double)fromLng);
        var toLatRad = ToRadians((double)toLat);
        var toLngRad = ToRadians((double)toLng);
        var deltaLat = toLatRad - fromLatRad;
        var deltaLng = toLngRad - fromLngRad;
        var a = Math.Pow(Math.Sin(deltaLat / 2), 2) + Math.Cos(fromLatRad) * Math.Cos(toLatRad) * Math.Pow(Math.Sin(deltaLng / 2), 2);
        return (decimal)(radiusKm * (2 * Math.Asin(Math.Min(1, Math.Sqrt(a)))));
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180d);

    private sealed record NpcPlayer(Guid Id, string Name, decimal Money, decimal X, decimal Y, Guid? CountryId, decimal Oil, decimal Uranium, decimal Chips);
    private sealed record ResourceRow(ResourceUnitsEnum Resource, decimal Quantity);
    private sealed record OpenTrade(Guid Id, Guid SellerId, ResourceUnitsEnum Resource, int Quantity, decimal PricePerUnit, decimal TotalPrice, decimal X, decimal Y);
    private sealed record War(Guid From, Guid To);
    private sealed record UnitRow(Guid PlayerId, string Code, int Level, int Quantity);
    private sealed record Target(Guid PlayerId, decimal X, decimal Y, Guid? CountryId);
    private sealed record TargetScore(Guid PlayerId, decimal X, decimal Y, decimal Power);
    private sealed record Roster(string BestCode, int BestLevel, int BestQty, int TotalQty);
    private sealed record QueuedRow(Guid PlayerId, string Code, int Quantity);
    private sealed record BuyReport(int Buys, decimal Spent);
    private sealed record OfferReport(int Offers);
    private sealed record RecruitmentReport(int Orders);
    private sealed record AttackReport(int Attacks);
}
