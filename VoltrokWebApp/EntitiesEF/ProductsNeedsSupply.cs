using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class ProductsNeedsSupply
{
    public Guid ProductsNeedsSuppliesId { get; set; }

    public Guid ProductsId { get; set; }

    public Guid NeedsId { get; set; }

    public decimal Quantity { get; set; }

    public virtual Need Needs { get; set; } = null!;

    public virtual Product Products { get; set; } = null!;
}
