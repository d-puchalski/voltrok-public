using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class ChatRegionMessage
{
    public Guid MessageId { get; set; }

    public Guid RegionsId { get; set; }

    public Guid PlayersId { get; set; }

    public string MessageText { get; set; } = null!;

    public DateTime? SentAt { get; set; }

    public virtual Player Players { get; set; } = null!;

    public virtual Region Regions { get; set; } = null!;
}
