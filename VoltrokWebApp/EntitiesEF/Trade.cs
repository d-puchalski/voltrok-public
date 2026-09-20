using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class Trade
{
    public Guid TradesId { get; set; }

    public Guid FromPlayersId { get; set; }

    public Guid? ToPlayersId { get; set; }

    public string ResourceCode { get; set; } = "oil";

    public Guid ProductsId { get; set; }

    public int Quantity { get; set; }

    public decimal PricePerUnit { get; set; }

    public decimal TotalPrice { get; set; }

    public string Status { get; set; } = null!;

    public decimal? FactionTaxPaid { get; set; }

    public decimal? CountryTaxPaid { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Player FromPlayers { get; set; } = null!;

    public virtual Product Products { get; set; } = null!;

    public virtual Player? ToPlayers { get; set; }
}
