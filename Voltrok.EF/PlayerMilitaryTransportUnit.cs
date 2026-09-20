namespace VoltrokEF;

public partial class PlayerMilitaryTransportUnit
{
    public Guid PlayerMilitaryTransportUnitsId { get; set; }

    public Guid PlayerMilitaryTransportsId { get; set; }

    public string MilitaryUnitsCode { get; set; } = null!;

    public int Level { get; set; }

    public int Quantity { get; set; }

    public virtual PlayerMilitaryTransport PlayerMilitaryTransports { get; set; } = null!;
}
