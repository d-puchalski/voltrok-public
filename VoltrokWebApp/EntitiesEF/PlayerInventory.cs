using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerInventory
{
    public Guid PlayerInventoriesId { get; set; }

    public Guid PlayersId { get; set; }

    public Guid ProductsId { get; set; }

    public int Quantity { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Player Players { get; set; } = null!;

    public virtual Product Products { get; set; } = null!;
}
