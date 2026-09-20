using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class ChatCountryMessage
{
    public Guid MessageId { get; set; }

    public Guid CountriesId { get; set; }

    public Guid PlayersId { get; set; }

    public string MessageText { get; set; } = null!;

    public DateTime? SentAt { get; set; }

    public virtual Country Countries { get; set; } = null!;

    public virtual Player Players { get; set; } = null!;
}
