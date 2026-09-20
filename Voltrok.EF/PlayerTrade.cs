namespace VoltrokEF;

public partial class PlayerTrade
{
    public Guid PlayerTradesId { get; set; }

    public Guid FromPlayersId { get; set; }

    public Guid? ToPlayersId { get; set; }

    public string ResourceCode { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal PricePerUnit { get; set; }

    public decimal TotalPrice { get; set; }

    public string Status { get; set; } = null!;

    public decimal? CountryTaxPaid { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime TransportStartTime { get; set; }

    public DateTime TransportEndTime { get; set; }

    public virtual Player FromPlayers { get; set; } = null!;

    public virtual Player? ToPlayers { get; set; }
}
