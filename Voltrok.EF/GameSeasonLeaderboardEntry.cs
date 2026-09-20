namespace VoltrokEF;

public partial class GameSeasonLeaderboardEntry
{
    public Guid GameSeasonLeaderboardEntriesId { get; set; }

    public Guid GameSeasonsId { get; set; }

    public string EntryKind { get; set; } = null!;

    public int Position { get; set; }

    public Guid? PlayersId { get; set; }

    public Guid? CountriesId { get; set; }

    public string EntryName { get; set; } = null!;

    public decimal SeasonScore { get; set; }

    public decimal PlayerMoney { get; set; }

    public decimal PlayerOil { get; set; }

    public decimal PlayerUranium { get; set; }

    public decimal PlayerChips { get; set; }

    public decimal CountryMoney { get; set; }

    public decimal CountryOil { get; set; }

    public decimal CountryUranium { get; set; }

    public decimal CountryChips { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual GameSeason GameSeason { get; set; } = null!;

    public virtual Player? Players { get; set; }

    public virtual Country? Countries { get; set; }
}
