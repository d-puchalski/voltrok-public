namespace VoltrokEF;

public partial class PlayerDailyStat
{
    public Guid PlayerDailyStatsId { get; set; }

    public Guid PlayersId { get; set; }

    public DateOnly StatsDate { get; set; }

    public decimal Money { get; set; }

    public decimal Score { get; set; }

    public decimal Oil { get; set; }

    public decimal Uranium { get; set; }

    public decimal Chips { get; set; }

    public int ArmyTotal { get; set; }

    public virtual Player Players { get; set; } = null!;
}
