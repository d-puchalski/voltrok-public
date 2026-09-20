using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class Need
{
    public Guid NeedsId { get; set; }

    public string Code { get; set; } = null!;

    public virtual ICollection<PlayerNeed> PlayerNeeds { get; set; } = new List<PlayerNeed>();

    public virtual ICollection<ProductsNeedsSupply> ProductsNeedsSupplies { get; set; } = new List<ProductsNeedsSupply>();
}
