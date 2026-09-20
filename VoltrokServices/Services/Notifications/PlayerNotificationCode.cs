using VoltrokUtils.Enums;

namespace VoltrokServices.Services.Notifications;

public enum PlayerNotificationCode
{
    IncomingAttack,
    IncomingSiege,
    IncomingAid,
    IncomingTransport,
    BattleReportAttacker,
    BattleReportDefender,
    SiegeStarted,
    SiegeFinished,
    CountryWarStarted,
    CountryWarEnded,
    ChatMessageReceived
}

public static class PlayerNotificationCodeExtensions
{
    public static string ToStorageValue(this PlayerNotificationCode code)
    {
        return code switch
        {
            PlayerNotificationCode.IncomingAttack => PlayerNotificationType.IncomingAttack,
            PlayerNotificationCode.IncomingSiege => PlayerNotificationType.IncomingSiege,
            PlayerNotificationCode.IncomingAid => PlayerNotificationType.IncomingAid,
            PlayerNotificationCode.IncomingTransport => PlayerNotificationType.IncomingTransport,
            PlayerNotificationCode.BattleReportAttacker => PlayerNotificationType.BattleReportAttacker,
            PlayerNotificationCode.BattleReportDefender => PlayerNotificationType.BattleReportDefender,
            PlayerNotificationCode.SiegeStarted => PlayerNotificationType.SiegeStarted,
            PlayerNotificationCode.SiegeFinished => PlayerNotificationType.SiegeFinished,
            PlayerNotificationCode.CountryWarStarted => PlayerNotificationType.CountryWarStarted,
            PlayerNotificationCode.CountryWarEnded => PlayerNotificationType.CountryWarEnded,
            PlayerNotificationCode.ChatMessageReceived => PlayerNotificationType.ChatMessageReceived,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported notification code.")
        };
    }
}
