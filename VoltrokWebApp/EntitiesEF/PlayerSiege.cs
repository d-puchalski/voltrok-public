using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerSiege
{
    public Guid PlayerSiegesId { get; set; }

    public Guid PlayersId { get; set; }

    public Guid AttackerPlayersId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public virtual Player AttackerPlayers { get; set; } = null!;

    public virtual Player Players { get; set; } = null!;
}
