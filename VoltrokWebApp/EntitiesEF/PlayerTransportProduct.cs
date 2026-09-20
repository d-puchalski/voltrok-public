using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerTransportProduct
{
    public Guid PlayerTransportProductsId { get; set; }

    public Guid PlayerTransportsId { get; set; }

    public Guid ProductsId { get; set; }

    public int Quantity { get; set; }

    public virtual PlayerTransport PlayerTransports { get; set; } = null!;

    public virtual Product Products { get; set; } = null!;
}
