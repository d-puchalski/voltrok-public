namespace VoltrokWebApp.Components.Tabs;

public enum MilitarySubTab
{
    Recruitment,
    Reports
}

public enum RecruitUnitFilter
{
    All,
    Affordable,
    NotAffordable
}

public enum RecruitUnitSort
{
    CostAsc,
    CostDesc,
    TimeAsc,
    TimeDesc,
    AttackDesc,
    DefenseDesc,
    SpeedDesc,
    CodeAsc,
    CodeDesc
}

public readonly record struct CountryOption(Guid CountryId, string Name);
