namespace VoltrokEF;

public partial class Country
{
    public Guid CountriesId { get; set; }

    public string Name { get; set; } = null!;

    public string? IsoCode2 { get; set; }

    public string? Flag { get; set; }

    public Guid? PresidentPlayersId { get; set; }

    public decimal TaxPercent { get; set; }

    public decimal Money { get; set; }

    public decimal Oil { get; set; }

    public decimal Uranium { get; set; }

    public decimal Chips { get; set; }

    public short OilTier { get; set; }

    public short UraniumTier { get; set; }

    public short ChipsTier { get; set; }

    public string? ColorHex { get; set; }

    public bool IsAllowToAttackInsideCountry { get; set; }

    public bool IsAllowToAttackWithoutWarDeclaration { get; set; }

    public virtual ICollection<ChatCountryMessage> ChatCountryMessages { get; set; } = new List<ChatCountryMessage>();

    public virtual ICollection<CountryBuilding> CountryBuildings { get; set; } = new List<CountryBuilding>();

    public virtual ICollection<CountryTradePolicy> CountryTradePolicySourceCountries { get; set; } = new List<CountryTradePolicy>();

    public virtual ICollection<CountryTradePolicy> CountryTradePolicyTargetCountries { get; set; } = new List<CountryTradePolicy>();

    public virtual ICollection<CountryWar> CountryWarCountryFroms { get; set; } = new List<CountryWar>();

    public virtual ICollection<CountryWar> CountryWarCountryTos { get; set; } = new List<CountryWar>();

    public virtual ICollection<Player> Players { get; set; } = new List<Player>();
}
