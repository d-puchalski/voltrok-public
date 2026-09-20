using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerMilitaryUnit
{
    public Guid PlayerMilitaryUnitsId { get; set; }

    public Guid PlayersId { get; set; }

    public Guid MilitaryUnitsId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual MilitaryUnit MilitaryUnits { get; set; } = null!;

    public virtual Player Players { get; set; } = null!;
}
