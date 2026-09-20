namespace VoltrokUtils.Enums;

public enum PlayerMilitaryTransportMissionType
{
    Attack,
    Siege,
    Aid
}

public enum PlayerTransportStatus
{
    InProgress,
    InSiege,
    Cancelled,
    Returning,
    Completed
}

public enum CountryWarStatus
{
    Active,
    Cancelled,
    Ended
}

public enum CountryBuildingStatus
{
    Hold,
    Active
}

public enum CountryBuildingTypes
{
    Refinery,
    PowerStation,
    Factory,
    Bank
}

public enum MilitaryUnitsEnum
{
    Infantry,
    Garrison,
    Tanks,
    AntiTank,
    Fighters,
    AirDefense,
    AttackDrones,
    AntiDroneWarfare,
    CyberForces,
    CyberDefense
}

public enum PlayerSiegesStatus
{
    Active,
    Cancelled,
    Completed
}

public enum Side
{
    Attacker,
    Defender
}

public enum PlayerTradeStatus
{
    Pending,
    Completed,
    InTransit,
    Cancelled
}

public static class EnumValueExtensions
{
    public static string ToDbValue(this PlayerMilitaryTransportMissionType value) => value.ToString();
    public static string ToDbValue(this PlayerTransportStatus value) => value.ToString();
    public static string ToDbValue(this CountryWarStatus value) => value.ToString();
    public static string ToDbValue(this CountryBuildingStatus value) => value.ToString();
    public static string ToDbValue(this PlayerSiegesStatus value) => value.ToString();
    public static string ToDbValue(this Side value) => value.ToString();
    public static string ToDbValue(this PlayerTradeStatus value) => value.ToString();
}
