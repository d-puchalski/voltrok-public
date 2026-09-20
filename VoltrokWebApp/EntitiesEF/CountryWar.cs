using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class CountryWar
{
    public Guid CountryWarsId { get; set; }

    public Guid AttackerCountriesId { get; set; }

    public Guid DefenderCountriesId { get; set; }

    public Guid DeclaredByPlayersId { get; set; }

    public string Status { get; set; } = null!;

    public decimal CancelPenaltyPercent { get; set; }

    public decimal CaptureThresholdPercent { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string? EndReason { get; set; }

    public Guid? ConqueredFromCountriesId { get; set; }

    public virtual Country AttackerCountries { get; set; } = null!;

    public virtual Country? ConqueredFromCountries { get; set; }

    public virtual Player DeclaredByPlayers { get; set; } = null!;

    public virtual Country DefenderCountries { get; set; } = null!;
}
