using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerMilitary
{
    public Guid PlayerMilitaryLogId { get; set; }

    public Guid PlayersId { get; set; }

    public int Level { get; set; }

    public int Quantity { get; set; }

    public virtual Player Players { get; set; } = null!;
}
