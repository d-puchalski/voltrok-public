namespace VoltrokEF;

public partial class CountryWar
{
    public Guid CountryWarsId { get; set; }

    public Guid CountryFromId { get; set; }

    public Guid CountryToId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public virtual Country CountryFrom { get; set; } = null!;

    public virtual Country CountryTo { get; set; } = null!;
}
