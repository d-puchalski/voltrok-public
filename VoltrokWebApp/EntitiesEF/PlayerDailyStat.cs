using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerDailyStat
{
    public Guid PlayerDailyStatsId { get; set; }

    public Guid PlayersId { get; set; }

    public DateOnly StatsDate { get; set; }

    public decimal Money { get; set; }

    public decimal Score { get; set; }

    public long ResourcesTotal { get; set; }

    public int ArmyTotal { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Player Players { get; set; } = null!;
}
