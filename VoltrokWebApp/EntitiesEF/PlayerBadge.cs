using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerBadge
{
    public Guid PlayerBadgesId { get; set; }

    public Guid PlayersId { get; set; }

    public Guid BadgeTypesId { get; set; }

    public DateTime AwardedAt { get; set; }

    public virtual BadgeType BadgeTypes { get; set; } = null!;

    public virtual Player Players { get; set; } = null!;
}
