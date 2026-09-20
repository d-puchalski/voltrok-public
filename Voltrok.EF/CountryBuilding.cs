namespace VoltrokEF;

public partial class CountryBuilding
{
    public Guid CountryBuildingsId { get; set; }

    public Guid CountriesId { get; set; }

    public string BuildingCode { get; set; } = null!;

    public int Level { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public virtual Country Countries { get; set; } = null!;
}
