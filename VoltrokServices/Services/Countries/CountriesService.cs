using Microsoft.EntityFrameworkCore;

using VoltrokEF;
using VoltrokUtils.Engines;
using VoltrokUtils.Enums;
using VoltrokUtils.Models;

namespace VoltrokServices.Services.Countries;

public class CountriesService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<List<Country>> GetAllCountriesAsync() => await (await dbContextFactory.CreateDbContextAsync())
        .Countries.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

    public async Task<List<CountryWarDetailsModel>> GetCountryWarDetailsAsync(Guid countryId, int rangeHours)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var nowUtc = DateTime.UtcNow;
        const decimal attackerVictoryThresholdPercent = 80m;
        const decimal defenderCancelThresholdPercent = 50m;

        var wars = await db.CountryWars
            .AsNoTracking()
            .Where(war =>
                (war.CountryFromId == countryId || war.CountryToId == countryId)
                && war.Status == CountryWarStatus.Active.ToDbValue())
            .OrderByDescending(war => war.StartedAt)
            .ToListAsync();

        if (wars.Count == 0)
        {
            return [];
        }

        var relevantCountryIds = wars
            .SelectMany(war => new[] { war.CountryFromId, war.CountryToId })
            .Distinct()
            .ToList();

        var players = await db.Players
            .AsNoTracking()
            .Where(player => relevantCountryIds.Contains(player.CountriesId))
            .Select(player => new
            {
                player.PlayersId,
                player.CountriesId
            })
            .ToListAsync();

        var playerCountryById = players.ToDictionary(player => player.PlayersId, player => player.CountriesId);
        var playerIds = players.Select(player => player.PlayersId).ToList();

        var reports = await db.PlayerBattleReports
            .AsNoTracking()
            .Where(report => playerIds.Contains(report.PlayerFromId) && playerIds.Contains(report.PlayerToId))
            .ToListAsync();

        var transports = await db.PlayerMilitaryTransports
            .AsNoTracking()
            .Include(transport => transport.PlayerMilitaryTransportUnits)
            .Where(transport => playerIds.Contains(transport.PlayerFromId) && playerIds.Contains(transport.PlayerToId))
            .ToListAsync();

        var sieges = await db.PlayerSieges
            .AsNoTracking()
            .Where(siege => playerIds.Contains(siege.PlayerFromId) && playerIds.Contains(siege.PlayerToId))
            .ToListAsync();

        var activeSiegeCode = PlayerSiegesStatus.Active.ToDbValue();
        var activeSieges = sieges
            .Where(siege => string.Equals(siege.Status, activeSiegeCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return wars.Select(war =>
        {
            var attackerCountryId = war.CountryFromId;
            var defenderCountryId = war.CountryToId;
            var rangeStartUtc = rangeHours <= 0
                ? war.StartedAt
                : MaxUtc(war.StartedAt, nowUtc.AddHours(-rangeHours));
            var rangeEndUtc = war.EndedAt.HasValue && war.EndedAt.Value < nowUtc
                ? war.EndedAt.Value
                : nowUtc;

            bool IsWarPlayers(Guid fromPlayerId, Guid toPlayerId)
            {
                if (!playerCountryById.TryGetValue(fromPlayerId, out var fromCountryId) || !playerCountryById.TryGetValue(toPlayerId, out var toCountryId))
                {
                    return false;
                }

                return (fromCountryId == attackerCountryId && toCountryId == defenderCountryId)
                    || (fromCountryId == defenderCountryId && toCountryId == attackerCountryId);
            }

            var warReports = reports
                .Where(report => report.EndedAt >= war.StartedAt && IsWarPlayers(report.PlayerFromId, report.PlayerToId))
                .ToList();

            var reportsInRange = warReports
                .Where(report => report.EndedAt >= rangeStartUtc && report.EndedAt <= rangeEndUtc)
                .ToList();

            var defenderTotalPlayers = players.Count(player => player.CountriesId == defenderCountryId);
            var attackerTotalPlayers = players.Count(player => player.CountriesId == attackerCountryId);
            var attackerActiveSieges = activeSieges
                .Where(siege =>
                    playerCountryById.TryGetValue(siege.PlayerFromId, out var fromCountryId)
                    && fromCountryId == attackerCountryId
                    && playerCountryById.TryGetValue(siege.PlayerToId, out var toCountryId)
                    && toCountryId == defenderCountryId)
                .Select(siege => siege.PlayerToId)
                .Distinct()
                .Count();
            var defenderActiveSieges = activeSieges
                .Where(siege =>
                    playerCountryById.TryGetValue(siege.PlayerFromId, out var fromCountryId)
                    && fromCountryId == defenderCountryId
                    && playerCountryById.TryGetValue(siege.PlayerToId, out var toCountryId)
                    && toCountryId == attackerCountryId)
                .Select(siege => siege.PlayerToId)
                .Distinct()
                .Count();

            var attackerProgressPercent = defenderTotalPlayers > 0
                ? Math.Round((attackerActiveSieges * 100m) / defenderTotalPlayers, 1)
                : 0m;
            var defenderProgressPercent = attackerTotalPlayers > 0
                ? Math.Round((defenderActiveSieges * 100m) / attackerTotalPlayers, 1)
                : 0m;

            var warTransportsInRange = transports
                .Where(transport =>
                    transport.StartTime >= rangeStartUtc
                    && transport.StartTime <= rangeEndUtc
                    && IsWarPlayers(transport.PlayerFromId, transport.PlayerToId))
                .ToList();

            var attackMissionCode = PlayerMilitaryTransportMissionType.Attack.ToDbValue();
            var siegeMissionCode = PlayerMilitaryTransportMissionType.Siege.ToDbValue();
            var attackMissionsInRange = warTransportsInRange.Count(transport => string.Equals(transport.MissionType, attackMissionCode, StringComparison.OrdinalIgnoreCase));
            var siegeMissionsInRange = warTransportsInRange.Count(transport => string.Equals(transport.MissionType, siegeMissionCode, StringComparison.OrdinalIgnoreCase));

            var unitActivity = warTransportsInRange
                .SelectMany(transport => transport.PlayerMilitaryTransportUnits)
                .GroupBy(unit => unit.MilitaryUnitsCode, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var sample = group.First();
                    var definition = MilitaryUnitEngine.FindByCode(sample.MilitaryUnitsCode);
                    var quantity = group.Sum(unit => unit.Quantity);
                    var attackPower = definition == null
                        ? quantity
                        : (int)Math.Round(group.Sum(unit => unit.Quantity * definition.GetOffensivePoints(unit.Level)), MidpointRounding.AwayFromZero);
                    var defensePower = definition == null
                        ? quantity
                        : (int)Math.Round(group.Sum(unit => unit.Quantity * definition.GetDefensivePoints(unit.Level)), MidpointRounding.AwayFromZero);

                    return new CountryWarUnitActivityModel
                    {
                        UnitCode = sample.MilitaryUnitsCode,
                        Quantity = quantity,
                        AttackPower = attackPower,
                        DefensePower = defensePower
                    };
                })
                .OrderByDescending(unit => unit.Quantity)
                .Take(4)
                .ToList();

            var activeSiegeCount = sieges.Count(siege =>
                string.Equals(siege.Status, activeSiegeCode, StringComparison.OrdinalIgnoreCase)
                && IsWarPlayers(siege.PlayerFromId, siege.PlayerToId));

            return new CountryWarDetailsModel
            {
                WarId = war.CountryWarsId,
                CountryId = countryId,
                OpponentCountryId = war.CountryFromId == countryId ? war.CountryToId : war.CountryFromId,
                IsAttacker = war.CountryFromId == countryId,
                Status = war.Status,
                StartedAtUtc = war.StartedAt,
                EndedAtUtc = war.EndedAt,
                AttackerProgressPercent = attackerProgressPercent,
                CaptureThresholdPercent = attackerVictoryThresholdPercent,
                DefenderProgressPercent = defenderProgressPercent,
                DefenderCancelThresholdPercent = defenderCancelThresholdPercent,
                AttackerActiveSieges = attackerActiveSieges,
                DefenderActiveSieges = defenderActiveSieges,
                DefenderTotalPlayers = defenderTotalPlayers,
                AttackerTotalPlayers = attackerTotalPlayers,
                RangeStartUtc = rangeStartUtc,
                RangeEndUtc = rangeEndUtc,
                BattlesInRange = reportsInRange.Count,
                AttackerWinsInRange = reportsInRange.Count(report =>
                    string.Equals(report.Result, "attacker_win", StringComparison.OrdinalIgnoreCase)
                    && playerCountryById.TryGetValue(report.PlayerFromId, out var fromCountryId)
                    && fromCountryId == attackerCountryId),
                DefenderWinsInRange = reportsInRange.Count(report =>
                    string.Equals(report.Result, "attacker_win", StringComparison.OrdinalIgnoreCase)
                    && playerCountryById.TryGetValue(report.PlayerFromId, out var fromCountryId)
                    && fromCountryId == defenderCountryId),
                AttackerLossesInRange = reportsInRange.Sum(report => report.AttackerLosses),
                DefenderLossesInRange = reportsInRange.Sum(report => report.DefenderLosses),
                AttackMissionsInRange = attackMissionsInRange,
                SiegeMissionsInRange = siegeMissionsInRange,
                ActiveSieges = activeSiegeCount,
                UnitsSentInRange = warTransportsInRange.SelectMany(transport => transport.PlayerMilitaryTransportUnits).Sum(unit => unit.Quantity),
                AttackPowerSentInRange = unitActivity.Sum(unit => unit.AttackPower),
                DefensePowerSentInRange = unitActivity.Sum(unit => unit.DefensePower),
                TopUnitsInRange = unitActivity
            };
        }).ToList();
    }

    public async Task<CountryMapTooltipStats?> GetCountryMapTooltipStatsAsync(Guid countryId)
        => (await GetCountryMapTooltipStatsAsync()).FirstOrDefault(x => x.CountryId == countryId);

    public async Task<List<CountryMapTooltipStats>> GetCountryMapTooltipStatsAsync()
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var countries = await db.Countries.AsNoTracking().ToListAsync();
        var activeWars = await db.CountryWars.AsNoTracking()
            .Where(war => war.Status == CountryWarStatus.Active.ToDbValue())
            .Join(
                db.Countries.AsNoTracking(),
                war => war.CountryFromId,
                country => country.CountriesId,
                (war, countryFrom) => new
                {
                    War = war,
                    CountryFromName = countryFrom.Name,
                    CountryFromFlag = countryFrom.Flag,
                    CountryFromIsoCode2 = countryFrom.IsoCode2
                })
            .Join(
                db.Countries.AsNoTracking(),
                x => x.War.CountryToId,
                country => country.CountriesId,
                (x, countryTo) => new
                {
                    x.War,
                    x.CountryFromName,
                    x.CountryFromFlag,
                    x.CountryFromIsoCode2,
                    CountryToName = countryTo.Name,
                    CountryToFlag = countryTo.Flag,
                    CountryToIsoCode2 = countryTo.IsoCode2
                })
            .ToListAsync();

        return countries.Select(country =>
        {
            var wars = activeWars
                .Where(war => war.War.CountryFromId == country.CountriesId || war.War.CountryToId == country.CountriesId)
                .GroupBy(war => war.War.CountryWarsId)
                .Select(group => group.First())
                .Select(war => new CountryMapWarInfo
                {
                    WarId = war.War.CountryWarsId,
                    IsAttacker = war.War.CountryFromId == country.CountriesId,
                    OpponentName = war.War.CountryFromId == country.CountriesId ? war.CountryToName : war.CountryFromName,
                    OpponentFlag = war.War.CountryFromId == country.CountriesId
                        ? (war.CountryToFlag ?? war.CountryToIsoCode2)
                        : (war.CountryFromFlag ?? war.CountryFromIsoCode2),
                    StartedAtUtc = war.War.StartedAt
                })
                .OrderByDescending(war => war.StartedAtUtc)
                .ToList();

            return new CountryMapTooltipStats
            {
                CountryId = country.CountriesId,
                CountryName = country.Name,
                ActiveWars = wars
            };
        }).ToList();
    }

    private static DateTime MaxUtc(DateTime first, DateTime second)
        => first >= second ? first : second;
}
