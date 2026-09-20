using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerNeed
{
    public Guid PlayerNeedsId { get; set; }

    public Guid PlayersId { get; set; }

    public Guid NeedsId { get; set; }

    public decimal Value { get; set; }

    public virtual Need Needs { get; set; } = null!;

    public virtual Player Players { get; set; } = null!;
}
