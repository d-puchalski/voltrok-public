namespace VoltrokEF;

public partial class PlayerBattleReport
{
    public Guid PlayerBattleReportsId { get; set; }

    public Guid PlayerFromId { get; set; }

    public Guid PlayerToId { get; set; }

    public string Result { get; set; } = null!;

    public int AttackerPower { get; set; }

    public int DefenderPower { get; set; }

    public int AttackerLosses { get; set; }

    public int DefenderLosses { get; set; }

    public decimal LootMoney { get; set; }

    public decimal LootOil { get; set; }

    public decimal LootUranium { get; set; }

    public decimal LootChips { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime EndedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public virtual ICollection<PlayerBattleReportUnit> PlayerBattleReportUnits { get; set; } = new List<PlayerBattleReportUnit>();

    public virtual Player PlayerFrom { get; set; } = null!;

    public virtual Player PlayerTo { get; set; } = null!;
}
