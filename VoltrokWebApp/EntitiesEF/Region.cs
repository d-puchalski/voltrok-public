using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class Region
{
    public Guid RegionsId { get; set; }

    public Guid? CountriesId { get; set; }

    public string Name { get; set; } = null!;

    public Guid? PresidentPlayersId { get; set; }

    public string? Flag { get; set; }

    public string? Color { get; set; }

    public string? IsoCode2 { get; set; }

    public string? AdmCode1 { get; set; }

    public decimal? Area { get; set; }

    public virtual ICollection<ChatRegionMessage> ChatRegionMessages { get; set; } = new List<ChatRegionMessage>();

    public virtual ICollection<Player> Players { get; set; } = new List<Player>();

    public virtual ICollection<RegionWar> RegionWarAttackerRegions { get; set; } = new List<RegionWar>();

    public virtual ICollection<RegionWar> RegionWarConqueredFromRegions { get; set; } = new List<RegionWar>();

    public virtual ICollection<RegionWar> RegionWarDefenderRegions { get; set; } = new List<RegionWar>();
}
