namespace VoltrokEF;

public partial class PlayerSiegeUnit
{
    public Guid PlayerSiegeUnitsId { get; set; }

    public Guid PlayerSiegesId { get; set; }

    public string Side { get; set; } = null!;

    public string MilitaryUnitsCode { get; set; } = null!;

    public int Level { get; set; }

    public int StartingQuantity { get; set; }

    public int CurrentQuantity { get; set; }

    public int LostQuantity { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual PlayerSiege PlayerSieges { get; set; } = null!;
}
