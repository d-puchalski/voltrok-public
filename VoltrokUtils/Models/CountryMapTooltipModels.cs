namespace VoltrokUtils.Models;

public sealed class CountryMapTooltipStats
{
    public Guid CountryId { get; init; }
    public string CountryName { get; init; } = string.Empty;
    public List<CountryMapWarInfo> ActiveWars { get; init; } = [];
}

public sealed class CountryMapWarInfo
{
    public Guid WarId { get; init; }
    public bool IsAttacker { get; init; }
    public string OpponentName { get; init; } = string.Empty;
    public string? OpponentFlag { get; init; }
    public DateTime StartedAtUtc { get; init; }
}
