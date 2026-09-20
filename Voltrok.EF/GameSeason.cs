namespace VoltrokEF;

public partial class GameSeason
{
    public Guid GameSeasonsId { get; set; }

    public int SeasonNumber { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartsAtUtc { get; set; }

    public DateTime EndsAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

}
