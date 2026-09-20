namespace VoltrokEF;

public partial class CountryTradePolicy
{
    public Guid CountryTradePoliciesId { get; set; }

    public Guid SourceCountriesId { get; set; }

    public Guid TargetCountriesId { get; set; }

    public bool IsEmbargo { get; set; }

    public decimal TariffPercent { get; set; }

    public virtual Country SourceCountries { get; set; } = null!;

    public virtual Country TargetCountries { get; set; } = null!;
}
