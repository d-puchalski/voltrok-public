using Microsoft.EntityFrameworkCore;

using VoltrokEF;
using VoltrokUtils.Models;

namespace VoltrokServices.Services.Badges;

public sealed class PlayerBadgeService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<List<BadgeTypeCodeEnum>> GetBadgesForPlayersAsync(IEnumerable<Guid> playerIds)
    {
        var ids = playerIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.PlayerBadges
            .AsNoTracking()
            .Where(vb => ids.Contains(vb.PlayersId))
            .Select(vb => Enum.Parse<BadgeTypeCodeEnum>(vb.BadgeCode))
            .ToListAsync();
    }

    public async Task RefreshBadgesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var weekStart = now.AddDays(-7);
        var monthStart = now.AddMonths(-1);
        var yearStart = now.AddYears(-1);

        var grantedBadges = new List<PlayerBadge>();

        var battleReports = db.PlayerBattleReports.AsNoTracking();

        await GrantSingleWinnerAsync(battleReports.Where(r => r.CreatedAt >= weekStart), BadgeTypeCodeEnum.LootWeek, attacker: true);
        await GrantSingleWinnerAsync(battleReports.Where(r => r.CreatedAt >= monthStart), BadgeTypeCodeEnum.LootMonth, attacker: true);
        await GrantSingleWinnerAsync(battleReports.Where(r => r.CreatedAt >= yearStart), BadgeTypeCodeEnum.LootYear, attacker: true);

        await GrantSingleWinnerAsync(battleReports.Where(r => r.CreatedAt >= weekStart), BadgeTypeCodeEnum.DefenseWeek, attacker: false, defenderWinsOnly: true);
        await GrantSingleWinnerAsync(battleReports.Where(r => r.CreatedAt >= monthStart), BadgeTypeCodeEnum.DefenseMonth, attacker: false, defenderWinsOnly: true);
        await GrantSingleWinnerAsync(battleReports.Where(r => r.CreatedAt >= yearStart), BadgeTypeCodeEnum.DefenseYear, attacker: false, defenderWinsOnly: true);

        var richest = await db.Players.AsNoTracking()
            .OrderByDescending(v => v.Money)
            .Take(10)
            .Select(v => v.PlayersId)
            .ToListAsync(cancellationToken);

        grantedBadges.AddRange(richest.Select(playerId => new PlayerBadge
        {
            PlayerBadgesId = Guid.NewGuid(),
            PlayersId = playerId,
            BadgeCode = nameof(BadgeTypeCodeEnum.RichTop10),
            AwardedAt = now
        }));

        await GrantBestGrowthAsync(DateOnly.FromDateTime(weekStart), BadgeTypeCodeEnum.GrowthWeek);
        await GrantBestGrowthAsync(DateOnly.FromDateTime(monthStart), BadgeTypeCodeEnum.GrowthMonth);
        await GrantBestGrowthAsync(DateOnly.FromDateTime(yearStart), BadgeTypeCodeEnum.GrowthYear);

        var countryPresidents = await db.Countries
            .AsNoTracking()
            .Where(country => country.PresidentPlayersId.HasValue)
            .Select(country => country.PresidentPlayersId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        grantedBadges.AddRange(countryPresidents.Select(playerId => new PlayerBadge
        {
            PlayerBadgesId = Guid.NewGuid(),
            PlayersId = playerId,
            BadgeCode = nameof(BadgeTypeCodeEnum.CountryPresident),
            AwardedAt = now
        }));

        var premiumPlayerIds = await db.Players
            .AsNoTracking()
            .Where(player => player.BattlePassValidTill > DateTime.UtcNow)
            .Select(player => player.PlayersId)
            .ToListAsync(cancellationToken);

        grantedBadges.AddRange(premiumPlayerIds
            .Select(playerId => new PlayerBadge
            {
                PlayerBadgesId = Guid.NewGuid(),
                PlayersId = playerId,
                BadgeCode = nameof(BadgeTypeCodeEnum.Premium),
                AwardedAt = now
            }));


        var executionStrategy = db.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.PlayerBadges.ExecuteDeleteAsync(cancellationToken);

            if (grantedBadges.Count > 0)
            {
                await db.PlayerBadges.AddRangeAsync(grantedBadges, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });
        return;

        async Task GrantBestGrowthAsync(DateOnly periodStart, BadgeTypeCodeEnum codeEnum)
        {
            var growthRows = await db.PlayerDailyStats
                .AsNoTracking()
                .Where(stat => stat.StatsDate >= periodStart)
                .GroupBy(stat => stat.PlayersId)
                .Select(group => new
                {
                    PlayerId = group.Key,
                    Growth = group.Max(item => item.Money) - group.Min(item => item.Money)
                })
                .OrderByDescending(row => row.Growth)
                .FirstOrDefaultAsync(cancellationToken);

            if (growthRows == null || growthRows.Growth <= 0)
            {
                return;
            }

            grantedBadges.Add(new PlayerBadge
            {
                PlayerBadgesId = Guid.NewGuid(),
                PlayersId = growthRows.PlayerId,
                BadgeCode = codeEnum.ToString(),
                AwardedAt = now
            });
        }

        async Task GrantSingleWinnerAsync(IQueryable<PlayerBattleReport> query, BadgeTypeCodeEnum codeEnum, bool attacker, bool defenderWinsOnly = false)
        {
            if (defenderWinsOnly)
            {
                query = query.Where(report => report.Result == "defender_won");
            }

            var grouped = await query
                .GroupBy(report => attacker ? report.PlayerFromId : report.PlayerToId)
                .Select(group => new
                {
                    PlayerId = group.Key,
                    Score = attacker
                        ? group.Sum(item => item.LootMoney)
                        : group.Sum(item => (decimal)item.DefenderPower)
                })
                .OrderByDescending(row => row.Score)
                .FirstOrDefaultAsync(cancellationToken);

            if (grouped?.PlayerId is null || grouped.Score <= 0)
            {
                return;
            }

            grantedBadges.Add(new PlayerBadge
            {
                PlayerBadgesId = Guid.NewGuid(),
                PlayersId = grouped.PlayerId,
                BadgeCode = codeEnum.ToString(),
                AwardedAt = now
            });
        }
    }
}

public sealed record PlayerBadgeView(string Code, string ColorHex);
