using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class BadgeType
{
    public Guid BadgeTypesId { get; set; }

    public string Code { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string ColorHex { get; set; } = null!;

    public virtual ICollection<PlayerBadge> PlayerBadges { get; set; } = new List<PlayerBadge>();
}
