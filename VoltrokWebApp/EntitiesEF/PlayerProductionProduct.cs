using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerProductionProduct
{
    public Guid PlayerProductionProductsId { get; set; }

    public Guid PlayersId { get; set; }

    public Guid ProductsId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Player Players { get; set; } = null!;

    public virtual Product Products { get; set; } = null!;
}
