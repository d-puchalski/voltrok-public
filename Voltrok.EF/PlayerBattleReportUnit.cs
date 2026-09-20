namespace VoltrokEF;

public partial class PlayerBattleReportUnit
{
    public Guid PlayerBattleReportUnitsId { get; set; }

    public Guid PlayerBattleReportsId { get; set; }

    public string Side { get; set; } = null!;

    public string MilitaryUnitsCode { get; set; } = null!;

    public int Level { get; set; }

    public int StartingQuantity { get; set; }

    public int RemainingQuantity { get; set; }

    public int LostQuantity { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual PlayerBattleReport PlayerBattleReports { get; set; } = null!;
}
