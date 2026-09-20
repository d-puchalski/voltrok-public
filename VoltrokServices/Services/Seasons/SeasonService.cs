using Microsoft.EntityFrameworkCore;

using VoltrokEF;

namespace VoltrokServices.Services.Seasons;

public sealed class SeasonService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public const int SeasonDurationDays = 14;
    public const string ActiveSeasonStatus = "active";
    public const string CompletedSeasonStatus = "completed";
    private const string RunType = "game_season_rollover";

    public async Task<GameSeason> GetCurrentSeasonAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await EnsureCurrentSeasonAsync(db, DateTime.UtcNow, cancellationToken);
    }

    public async Task<SeasonLeaderboardSnapshot> GetSeasonLeaderboardAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var currentSeason = await EnsureCurrentSeasonAsync(db, nowUtc, cancellationToken);

        var players = await db.Players
            .AsNoTracking()
            .Include(player => player.Countries)
            .OrderByDescending(player => player.Score)
            .ThenByDescending(player => player.Money)
            .ThenBy(player => player.Name)
            .Take(limit)
            .Select(player => new SeasonLeaderboardPlayerEntry(
                player.PlayersId,
                player.Name,
                player.Score,
                player.CountriesId,
                player.Countries.Name,
                player.Countries.Flag,
                player.Countries.IsoCode2,
                player.Money,
                player.Oil,
                player.Uranium,
                player.Chips))
            .ToListAsync(cancellationToken);

        var countryRows = await db.Players
            .AsNoTracking()
            .Where(player => player.CountriesId != Guid.Empty)
            .Select(player => new CountryLeaderboardSourceRow(
                player.CountriesId,
                player.Countries.Name,
                player.Countries.Flag,
                player.Countries.IsoCode2,
                player.Score,
                player.Money,
                player.Oil,
                player.Uranium,
                player.Chips))
            .ToListAsync(cancellationToken);

        var countries = BuildCountryLeaderboard(countryRows, limit);

        return new SeasonLeaderboardSnapshot(
            currentSeason.GameSeasonsId,
            currentSeason.SeasonNumber,
            currentSeason.StartsAtUtc,
            currentSeason.EndsAtUtc,
            currentSeason.Status,
            players,
            countries);
    }

    public async Task<SeasonLeaderboardSnapshot> GetSeasonLeaderboardAsync(Guid seasonId, int limit = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var season = await db.GameSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.GameSeasonsId == seasonId, cancellationToken)
            ?? throw new InvalidOperationException("Season not found.");

        var players = await db.GameSeasonLeaderboardEntries
            .AsNoTracking()
            .Include(entry => entry.Players)
                .ThenInclude(player => player.Countries)
            .Where(entry => entry.GameSeasonsId == seasonId && entry.EntryKind == "player")
            .OrderBy(entry => entry.Position)
            .Take(limit)
            .Select(entry => new SeasonLeaderboardPlayerEntry(
                entry.PlayersId ?? Guid.Empty,
                entry.EntryName,
                entry.SeasonScore,
                entry.CountriesId ?? Guid.Empty,
                entry.Players != null && entry.Players.Countries != null ? entry.Players.Countries.Name : string.Empty,
                entry.Players != null && entry.Players.Countries != null ? entry.Players.Countries.Flag : null,
                entry.Players != null && entry.Players.Countries != null ? entry.Players.Countries.IsoCode2 : null,
                entry.PlayerMoney,
                entry.PlayerOil,
                entry.PlayerUranium,
                entry.PlayerChips))
            .ToListAsync(cancellationToken);

        var countries = await db.GameSeasonLeaderboardEntries
            .AsNoTracking()
            .Include(entry => entry.Countries)
            .Where(entry => entry.GameSeasonsId == seasonId && entry.EntryKind == "country")
            .OrderBy(entry => entry.Position)
            .Take(limit)
            .Select(entry => new SeasonLeaderboardCountryEntry(
                entry.CountriesId ?? Guid.Empty,
                entry.EntryName,
                entry.SeasonScore,
                entry.Countries != null ? entry.Countries.Flag : null,
                entry.Countries != null ? entry.Countries.IsoCode2 : null,
                entry.CountryMoney,
                entry.CountryOil,
                entry.CountryUranium,
                entry.CountryChips))
            .ToListAsync(cancellationToken);

        return new SeasonLeaderboardSnapshot(
            season.GameSeasonsId,
            season.SeasonNumber,
            season.StartsAtUtc,
            season.EndsAtUtc,
            season.Status,
            players,
            countries);
    }

    public async Task<IReadOnlyList<SeasonArchiveItem>> GetCompletedSeasonsAsync(int limit = 12, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var seasons = await db.GameSeasons
            .AsNoTracking()
            .Where(season => season.Status == CompletedSeasonStatus)
            .OrderByDescending(season => season.SeasonNumber)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var items = new List<SeasonArchiveItem>(seasons.Count);
        foreach (var season in seasons)
        {
            items.Add(await BuildArchiveItemAsync(db, season, cancellationToken));
        }

        return items;
    }

    public async Task<SeasonArchiveItem?> GetLatestCompletedSeasonSummaryAsync(CancellationToken cancellationToken = default)
    {
        var seasons = await GetCompletedSeasonsAsync(1, cancellationToken);
        return seasons.FirstOrDefault();
    }

    public async Task<bool> RollExpiredSeasonAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var changed = false;

        while (true)
        {
            var activeSeason = await db.GameSeasons
                .OrderByDescending(season => season.SeasonNumber)
                .FirstOrDefaultAsync(season => season.Status == ActiveSeasonStatus, cancellationToken);

            if (activeSeason is null)
            {
                await EnsureCurrentSeasonAsync(db, nowUtc, cancellationToken);
                break;
            }

            if (activeSeason.EndsAtUtc > nowUtc)
            {
                break;
            }

            await CloseSeasonAsync(db, activeSeason, cancellationToken);
            await CreateNextSeasonAsync(db, activeSeason, cancellationToken);
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    private async Task<GameSeason> EnsureCurrentSeasonAsync(AppDbContext db, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var activeSeason = await db.GameSeasons
            .OrderByDescending(season => season.SeasonNumber)
            .FirstOrDefaultAsync(season => season.Status == ActiveSeasonStatus, cancellationToken);

        if (activeSeason is not null)
        {
            return activeSeason;
        }

        var latestSeason = await db.GameSeasons
            .OrderByDescending(season => season.SeasonNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSeason is null)
        {
            var firstSeason = new GameSeason
            {
                GameSeasonsId = Guid.NewGuid(),
                SeasonNumber = 1,
                Status = ActiveSeasonStatus,
                StartsAtUtc = nowUtc,
                EndsAtUtc = nowUtc.AddDays(SeasonDurationDays),
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            db.GameSeasons.Add(firstSeason);
            await db.SaveChangesAsync(cancellationToken);
            return firstSeason;
        }

        if (latestSeason.Status == CompletedSeasonStatus && latestSeason.EndsAtUtc <= nowUtc)
        {
            return await CreateNextSeasonAsync(db, latestSeason, cancellationToken);
        }

        if (latestSeason.Status == CompletedSeasonStatus && latestSeason.EndsAtUtc > nowUtc)
        {
            var currentSeason = new GameSeason
            {
                GameSeasonsId = Guid.NewGuid(),
                SeasonNumber = latestSeason.SeasonNumber + 1,
                Status = ActiveSeasonStatus,
                StartsAtUtc = nowUtc,
                EndsAtUtc = nowUtc.AddDays(SeasonDurationDays),
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            db.GameSeasons.Add(currentSeason);
            await db.SaveChangesAsync(cancellationToken);
            return currentSeason;
        }

        return latestSeason;
    }

    private async Task CloseSeasonAsync(AppDbContext db, GameSeason season, CancellationToken cancellationToken)
    {
        var topPlayers = await db.Players
            .AsNoTracking()
            .Include(player => player.Countries)
            .Where(player => player.Score > 0m)
            .OrderByDescending(player => player.Score)
            .ThenByDescending(player => player.Money)
            .ThenBy(player => player.Name)
            .Take(10)
            .Select(player => new SeasonLeaderboardPlayerEntry(
                player.PlayersId,
                player.Name,
                player.Score,
                player.CountriesId,
                player.Countries.Name,
                player.Countries.Flag,
                player.Countries.IsoCode2,
                player.Money,
                player.Oil,
                player.Uranium,
                player.Chips))
            .ToListAsync(cancellationToken);

        var topCountryRows = await db.Players
            .AsNoTracking()
            .Where(player => player.CountriesId != Guid.Empty && player.Score > 0m)
            .Select(player => new CountryLeaderboardSourceRow(
                player.CountriesId,
                player.Countries.Name,
                null,
                null,
                player.Score,
                player.Money,
                player.Oil,
                player.Uranium,
                player.Chips))
            .ToListAsync(cancellationToken);

        var topCountries = BuildCountryLeaderboard(topCountryRows, 10);

        season.Status = CompletedSeasonStatus;
        season.ClosedAtUtc = DateTime.UtcNow;
        season.UpdatedAt = DateTime.UtcNow;

        db.GameSeasonLeaderboardEntries.RemoveRange(
            db.GameSeasonLeaderboardEntries.Where(entry => entry.GameSeasonsId == season.GameSeasonsId));

        for (var i = 0; i < topPlayers.Count; i++)
        {
            var player = topPlayers[i];
            db.GameSeasonLeaderboardEntries.Add(new GameSeasonLeaderboardEntry
            {
                GameSeasonLeaderboardEntriesId = Guid.NewGuid(),
                GameSeasonsId = season.GameSeasonsId,
                EntryKind = "player",
                Position = i + 1,
                PlayersId = player.PlayerId,
                EntryName = player.PlayerName,
                SeasonScore = player.SeasonScore,
                PlayerMoney = player.PlayerMoney,
                PlayerOil = player.PlayerOil,
                PlayerUranium = player.PlayerUranium,
                PlayerChips = player.PlayerChips,
                CountryMoney = 0m,
                CountryOil = 0m,
                CountryUranium = 0m,
                CountryChips = 0m,
                CreatedAt = DateTime.UtcNow
            });
        }

        for (var i = 0; i < topCountries.Count; i++)
        {
            var country = topCountries[i];
            db.GameSeasonLeaderboardEntries.Add(new GameSeasonLeaderboardEntry
            {
                GameSeasonLeaderboardEntriesId = Guid.NewGuid(),
                GameSeasonsId = season.GameSeasonsId,
                EntryKind = "country",
                Position = i + 1,
                CountriesId = country.CountryId,
                EntryName = country.CountryName,
                SeasonScore = country.SeasonScore,
                PlayerMoney = 0m,
                PlayerOil = 0m,
                PlayerUranium = 0m,
                PlayerChips = 0m,
                CountryMoney = country.CountryMoney,
                CountryOil = country.CountryOil,
                CountryUranium = country.CountryUranium,
                CountryChips = country.CountryChips,
                CreatedAt = DateTime.UtcNow
            });
        }

        var playersToReset = await db.Players
            .Where(player => player.Score > 0m)
            .ToListAsync(cancellationToken);

        foreach (var player in playersToReset)
        {
            player.Score = 0m;
        }
    }

    private async Task<GameSeason> CreateNextSeasonAsync(AppDbContext db, GameSeason previousSeason, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var nextSeason = new GameSeason
        {
            GameSeasonsId = Guid.NewGuid(),
            SeasonNumber = previousSeason.SeasonNumber + 1,
            Status = ActiveSeasonStatus,
            StartsAtUtc = previousSeason.EndsAtUtc,
            EndsAtUtc = previousSeason.EndsAtUtc.AddDays(SeasonDurationDays),
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        db.GameSeasons.Add(nextSeason);
        await db.SaveChangesAsync(cancellationToken);
        return nextSeason;
    }

    private async Task<SeasonArchiveItem> BuildArchiveItemAsync(AppDbContext db, GameSeason season, CancellationToken cancellationToken)
    {
        var winnerPlayer = await db.GameSeasonLeaderboardEntries
            .AsNoTracking()
            .Include(entry => entry.Players)
                .ThenInclude(player => player.Countries)
            .Where(entry => entry.GameSeasonsId == season.GameSeasonsId && entry.EntryKind == "player")
            .OrderBy(entry => entry.Position)
            .Select(entry => new
            {
                PlayerName = entry.EntryName,
                CountryName = entry.Players != null && entry.Players.Countries != null ? entry.Players.Countries.Name : null,
                CountryFlag = entry.Players != null && entry.Players.Countries != null ? entry.Players.Countries.Flag : null,
                CountryIsoCode2 = entry.Players != null && entry.Players.Countries != null ? entry.Players.Countries.IsoCode2 : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        var winnerCountry = await db.GameSeasonLeaderboardEntries
            .AsNoTracking()
            .Include(entry => entry.Countries)
            .Where(entry => entry.GameSeasonsId == season.GameSeasonsId && entry.EntryKind == "country")
            .OrderBy(entry => entry.Position)
            .Select(entry => new
            {
                CountryName = entry.EntryName,
                CountryFlag = entry.Countries != null ? entry.Countries.Flag : null,
                CountryIsoCode2 = entry.Countries != null ? entry.Countries.IsoCode2 : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new SeasonArchiveItem(
            season.GameSeasonsId,
            season.SeasonNumber,
            season.StartsAtUtc,
            season.EndsAtUtc,
            season.ClosedAtUtc,
            winnerPlayer?.PlayerName,
            winnerPlayer?.CountryName,
            winnerPlayer?.CountryFlag,
            winnerPlayer?.CountryIsoCode2,
            winnerCountry?.CountryName,
            winnerCountry?.CountryFlag,
            winnerCountry?.CountryIsoCode2);
    }

    public sealed record SeasonLeaderboardSnapshot(
        Guid SeasonId,
        int SeasonNumber,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        string Status,
        IReadOnlyList<SeasonLeaderboardPlayerEntry> Players,
        IReadOnlyList<SeasonLeaderboardCountryEntry> Countries);

    public sealed record SeasonLeaderboardPlayerEntry(
        Guid PlayerId,
        string PlayerName,
        decimal SeasonScore,
        Guid CountryId,
        string CountryName,
        string? CountryFlag,
        string? CountryIsoCode2,
        decimal PlayerMoney,
        decimal PlayerOil,
        decimal PlayerUranium,
        decimal PlayerChips);

    public sealed record SeasonLeaderboardCountryEntry(
        Guid CountryId,
        string CountryName,
        decimal SeasonScore,
        string? CountryFlag,
        string? CountryIsoCode2,
        decimal CountryMoney,
        decimal CountryOil,
        decimal CountryUranium,
        decimal CountryChips);

    public sealed record SeasonArchiveItem(
        Guid SeasonId,
        int SeasonNumber,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        DateTime? ClosedAtUtc,
        string? WinnerPlayerName,
        string? WinnerPlayerCountryName,
        string? WinnerPlayerCountryFlag,
        string? WinnerPlayerCountryIsoCode2,
        string? WinnerCountryName,
        string? WinnerCountryFlag,
        string? WinnerCountryIsoCode2);

    private sealed record CountryLeaderboardSourceRow(
        Guid CountryId,
        string CountryName,
        string? Flag,
        string? IsoCode2,
        decimal Score,
        decimal Money,
        decimal Oil,
        decimal Uranium,
        decimal Chips);

    private static List<SeasonLeaderboardCountryEntry> BuildCountryLeaderboard(
        IEnumerable<CountryLeaderboardSourceRow> rows,
        int limit)
    {
        return rows
            .Where(row => row.CountryId != Guid.Empty)
            .GroupBy(row => new
            {
                row.CountryId,
                row.CountryName,
                row.Flag,
                row.IsoCode2
            })
            .Select(group => new SeasonLeaderboardCountryEntry(
                group.Key.CountryId,
                group.Key.CountryName,
                group.Sum(row => row.Score),
                group.Key.Flag,
                group.Key.IsoCode2,
                group.Sum(row => row.Money),
                group.Sum(row => row.Oil),
                group.Sum(row => row.Uranium),
                group.Sum(row => row.Chips)))
            .OrderByDescending(country => country.SeasonScore)
            .ThenBy(country => country.CountryName)
            .Take(limit)
            .ToList();
    }
}
