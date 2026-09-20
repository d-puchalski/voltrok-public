using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerMilitaryOrder
{
    public Guid PlayerMilitaryOrdersId { get; set; }

    public Guid PlayersId { get; set; }

    public Guid MilitaryUnitsId { get; set; }

    public int Quantity { get; set; }

    public int ProducedQuantity { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual MilitaryUnit MilitaryUnits { get; set; } = null!;

    public virtual Player Players { get; set; } = null!;
}
