using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class MilitaryUnit
{
    public Guid MilitaryUnitsId { get; set; }

    public string Code { get; set; } = null!;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public int AttackPoints { get; set; }

    public int DefensePoints { get; set; }

    public decimal TravelSpeedKmh { get; set; }

    public int TimeSeconds { get; set; }

    public decimal ProductionCost { get; set; }

    public virtual ICollection<PlayerMilitaryOrder> PlayerMilitaryOrders { get; set; } = new List<PlayerMilitaryOrder>();

    public virtual ICollection<PlayerMilitaryUnit> PlayerMilitaryUnits { get; set; } = new List<PlayerMilitaryUnit>();
}
