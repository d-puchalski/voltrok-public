using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerNotification
{
    public Guid PlayerNotificationsId { get; set; }

    public Guid PlayersId { get; set; }

    public string Type { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? DataJson { get; set; }

    public bool IsRead { get; set; }

    public bool IsPopupDelivered { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime? PopupDeliveredAt { get; set; }

    public virtual Player Players { get; set; } = null!;
}
