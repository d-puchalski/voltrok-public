using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerProductionOrder
{
    public Guid PlayerProductionOrdersId { get; set; }

    public Guid PlayersId { get; set; }

    public Guid ProductsId { get; set; }

    public int Quantity { get; set; }

    public int ProducedQuantity { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Player Players { get; set; } = null!;

    public virtual Product Products { get; set; } = null!;
}
