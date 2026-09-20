namespace VoltrokUtils.Models;

public sealed class PlayerMapTooltipStats
{
    public Guid PlayerId { get; init; }
    public Guid CountryId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public bool IsNpc { get; init; }
    public bool PremiumShieldEnabled { get; init; }
    public bool IsCountryPresident { get; init; }
    public bool IsCountryCouncilMember { get; init; }
    public string? PlayerAvatar { get; init; }
    public string CountryName { get; init; } = string.Empty;
    public string? CountryFlag { get; init; }
    public string? CountryIsoCode2 { get; init; }
    public decimal Money { get; init; }
    public decimal Oil { get; init; }
    public decimal Uranium { get; init; }
    public decimal Chips { get; init; }
    public List<PlayerMapBadgeInfo> Badges { get; init; } = [];
}

public sealed class PlayerMapBadgeInfo
{
    public string Code { get; init; } = string.Empty;
    public DateTime AwardedAtUtc { get; init; }
}
