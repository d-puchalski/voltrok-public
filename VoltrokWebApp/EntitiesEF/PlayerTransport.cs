using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerTransport
{
    public Guid PlayerTransportsId { get; set; }

    public Guid PlayerFromId { get; set; }

    public Guid PlayerToId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string Status { get; set; } = null!;

    public int Level { get; set; }

    public virtual Player PlayerFrom { get; set; } = null!;

    public virtual Player PlayerTo { get; set; } = null!;

    public virtual ICollection<PlayerTransportProduct> PlayerTransportProducts { get; set; } = new List<PlayerTransportProduct>();
}
