using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Voltrok.EF;

public partial class Country
{
    public Guid CountriesId { get; set; }

    public string Name { get; set; } = null!;

    public Guid? PresidentPlayersId { get; set; }

    public string? ColorHex { get; set; }

    public string OilTier { get; set; } = "low";

    public string UraniumTier { get; set; } = "low";

    public string ChipsTier { get; set; } = "low";

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<ChatCountryMessage> ChatCountryMessages { get; set; } = new List<ChatCountryMessage>();

    public virtual ICollection<CountryWar> CountryWarAttackerCountries { get; set; } = new List<CountryWar>();

    public virtual ICollection<CountryWar> CountryWarConqueredFromCountries { get; set; } = new List<CountryWar>();

    public virtual ICollection<CountryWar> CountryWarDefenderCountries { get; set; } = new List<CountryWar>();

    public virtual ICollection<CountryTradePolicy> CountryTradePolicySourceCountries { get; set; } = new List<CountryTradePolicy>();

    public virtual ICollection<CountryTradePolicy> CountryTradePolicyTargetCountries { get; set; } = new List<CountryTradePolicy>();

    [NotMapped]
    public virtual ICollection<Player> PlayerMembers { get; set; } = new List<Player>();

    public virtual Player? PresidentPlayers { get; set; }
}
