using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class RegionWar
{
    public Guid RegionWarsId { get; set; }

    public Guid AttackerRegionsId { get; set; }

    public Guid DefenderRegionsId { get; set; }

    public Guid DeclaredByPlayersId { get; set; }

    public string Status { get; set; } = null!;

    public decimal CancelPenaltyPercent { get; set; }

    public decimal CaptureThresholdPercent { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string? EndReason { get; set; }

    public Guid? ConqueredFromRegionsId { get; set; }

    public virtual Region AttackerRegions { get; set; } = null!;

    public virtual Region? ConqueredFromRegions { get; set; }

    public virtual Player DeclaredByPlayers { get; set; } = null!;

    public virtual Region DefenderRegions { get; set; } = null!;
}
