namespace VoltrokEF;

public partial class PlayerMilitaryUnit
{
    public Guid PlayerMilitaryUnitsId { get; set; }

    public Guid PlayersId { get; set; }

    public string MilitaryUnitsCode { get; set; } = null!;

    public int Level { get; set; }

    public int Quantity { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Player Players { get; set; } = null!;
}
