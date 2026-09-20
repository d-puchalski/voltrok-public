namespace VoltrokUtils.Models;

public enum BadgeTypeCodeEnum
{
    LootWeek,
    LootMonth,
    LootYear,
    DefenseWeek,
    DefenseMonth,
    DefenseYear,
    RichTop10,
    GrowthWeek,
    GrowthMonth,
    GrowthYear,
    CountryPresident,
    CountryCouncil,
    Premium
}


public sealed class BadgeType
{
    public BadgeTypeCodeEnum Code { get; init; } = default!;
    public string ColorHex { get; init; } = null!;

}
