namespace VoltrokEF;

public partial class PlayerSiege
{
    public Guid PlayerSiegesId { get; set; }

    public Guid PlayerFromId { get; set; }

    public Guid PlayerToId { get; set; }

    public string Status { get; set; } = null!;

    public decimal LootMoney { get; set; }

    public decimal LootOil { get; set; }

    public decimal LootUranium { get; set; }

    public decimal LootChips { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public DateTime? LastTickAt { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public virtual Player PlayerFrom { get; set; } = null!;

    public virtual ICollection<PlayerSiegeUnit> PlayerSiegeUnits { get; set; } = new List<PlayerSiegeUnit>();

    public virtual Player PlayerTo { get; set; } = null!;
}
