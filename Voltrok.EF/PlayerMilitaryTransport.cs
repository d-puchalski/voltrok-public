namespace VoltrokEF;

public partial class PlayerMilitaryTransport
{
    public Guid PlayerMilitaryTransportsId { get; set; }

    public Guid PlayerFromId { get; set; }

    public Guid PlayerToId { get; set; }

    public string MissionType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool AutoReturnAfterBattle { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public virtual Player PlayerFrom { get; set; } = null!;

    public virtual ICollection<PlayerMilitaryTransportUnit> PlayerMilitaryTransportUnits { get; set; } = new List<PlayerMilitaryTransportUnit>();

    public virtual Player PlayerTo { get; set; } = null!;
}
