namespace VoltrokEF;

public partial class PlayerBadge
{
    public Guid PlayerBadgesId { get; set; }

    public Guid PlayersId { get; set; }

    public string BadgeCode { get; set; } = null!;

    public DateTime AwardedAt { get; set; }

    public virtual Player Players { get; set; } = null!;
}
