namespace VoltrokEF;

public partial class PlayerMilitaryProduction
{
    public Guid PlayerMilitaryOrdersId { get; set; }

    public Guid PlayersId { get; set; }

    public string MilitaryUnitsCode { get; set; } = null!;

    public int Quantity { get; set; }

    public int Level { get; set; }

    public int ProducedQuantity { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Player Players { get; set; } = null!;
}
