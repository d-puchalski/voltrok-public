using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerBattleReport
{
    public Guid PlayerBattleReportsId { get; set; }

    public Guid AttackerPlayersId { get; set; }

    public Guid DefenderPlayersId { get; set; }

    public string Result { get; set; } = null!;

    public int AttackerPower { get; set; }

    public int DefenderPower { get; set; }

    public int AttackerLosses { get; set; }

    public int DefenderLosses { get; set; }

    public decimal LootMoney { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime EndedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Player AttackerPlayers { get; set; } = null!;

    public virtual Player DefenderPlayers { get; set; } = null!;

    public virtual ICollection<PlayerBattleReportProduct> PlayerBattleReportProducts { get; set; } = new List<PlayerBattleReportProduct>();
}
