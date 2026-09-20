using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

using VoltrokEF;

namespace VoltrokServices.Services.Notifications;

public class NotificationService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    private const string LocalizationPropertyName = "_localization";
    private const string TitleArgsPropertyName = "titleArgs";
    private const string MessageArgsPropertyName = "messageArgs";

    public async Task CreateAsync(Guid playerId, PlayerNotificationCode code, PlayerNotificationDetails details, DateTime? createdAt = null, CancellationToken cancellationToken = default)
    {
        var payload = BuildDataJson(code, details);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.PlayerNotifications.Add(BuildNotification(
            playerId,
            code.ToStorageValue(),
            payload,
            createdAt));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<PlayerNotification>> GetRecentNotificationsAsync(Guid playerId, int limit = 50, bool includeRead = true, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.PlayerNotifications
            .AsNoTracking()
            .Where(n => n.PlayersId == playerId);

        if (!includeRead)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.PlayerNotificationsId)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PlayerNotification>> ClaimPopupNotificationsAsync(Guid playerId, int limit = 5, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var toDeliver = await dbContext.PlayerNotifications
            .Where(n => n.PlayersId == playerId && !n.IsPopupDelivered)
            .OrderBy(n => n.CreatedAt)
            .ThenBy(n => n.PlayerNotificationsId)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);

        if (toDeliver.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        foreach (var notification in toDeliver)
        {
            notification.IsPopupDelivered = true;
            notification.PopupDeliveredAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return toDeliver
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.PlayerNotificationsId)
            .ToList();
    }

    public async Task MarkAsReadAsync(Guid playerId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var notification = await dbContext.PlayerNotifications
            .FirstOrDefaultAsync(
                n => n.PlayerNotificationsId == notificationId && n.PlayersId == playerId,
                cancellationToken);

        if (notification == null || notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildDataJson(PlayerNotificationCode code, PlayerNotificationDetails details)
    {
        var payload = new JsonObject();
        IEnumerable<string?>? titleArgs = null;
        IEnumerable<string?>? messageArgs = null;

        switch (code)
        {
            case PlayerNotificationCode.ChatMessageReceived:
                payload["channel"] = (details.Channel ?? "direct").ToLowerInvariant();
                payload["senderPlayerId"] = details.SenderPlayerId;
                payload["senderPlayerName"] = details.SenderPlayerName;
                messageArgs = [details.SenderPlayerName, details.Preview];
                break;

            case PlayerNotificationCode.IncomingTransport:
                payload["notificationKind"] = details.NotificationKind ?? "incoming_transport_arrived";
                payload["tradeId"] = details.TradeId;
                payload["senderPlayerId"] = details.SenderPlayerId;
                payload["senderPlayerName"] = details.SenderPlayerName;
                payload["recipientPlayerId"] = details.RecipientPlayerId;
                payload["recipientPlayerName"] = details.RecipientPlayerName;
                payload["resourceCode"] = details.ResourceCode;
                payload["quantity"] = details.Quantity;
                titleArgs = [details.SenderPlayerName, details.RecipientPlayerName];
                messageArgs = [details.SenderPlayerName, details.RecipientPlayerName, details.ResourceCode, FormatDecimal(details.Quantity)];
                break;

            case PlayerNotificationCode.IncomingAid:
                payload["notificationKind"] = details.NotificationKind ?? (details.IsOutgoing == true ? "outgoing_aid_arrived" : "incoming_aid_arrived");
                payload["missionType"] = details.MissionType ?? "aid";
                payload["attackerPlayerId"] = details.AttackerPlayerId;
                payload["defenderPlayerId"] = details.DefenderPlayerId;
                payload["arrivedAtUtc"] = details.ArrivedAtUtc;
                messageArgs = [details.CounterpartyName];
                break;

            case PlayerNotificationCode.SiegeStarted:
                payload["notificationKind"] = details.NotificationKind ?? "siege_started";
                payload["missionType"] = details.MissionType ?? "siege";
                payload["attackerPlayerId"] = details.AttackerPlayerId;
                payload["defenderPlayerId"] = details.DefenderPlayerId;
                payload["startedAtUtc"] = details.StartedAtUtc;
                break;

            case PlayerNotificationCode.BattleReportAttacker:
            case PlayerNotificationCode.BattleReportDefender:
                payload["notificationKind"] = details.NotificationKind;
                payload["missionType"] = details.MissionType;
                payload["result"] = details.Result;
                payload["attackerPlayerId"] = details.AttackerPlayerId;
                payload["defenderPlayerId"] = details.DefenderPlayerId;
                payload["attackerPlayerName"] = details.AttackerPlayerName;
                payload["defenderPlayerName"] = details.DefenderPlayerName;
                payload["winnerPlayerName"] = details.WinnerPlayerName;
                payload["loserPlayerName"] = details.LoserPlayerName;
                payload["attackerLosses"] = details.AttackerLosses;
                payload["defenderLosses"] = details.DefenderLosses;
                payload["lootMoney"] = details.LootMoney;
                payload["lootOil"] = details.LootOil;
                payload["lootUranium"] = details.LootUranium;
                payload["lootChips"] = details.LootChips;
                payload["endedAtUtc"] = details.EndedAtUtc;
                messageArgs = BuildBattleMessageArgs(code, details);
                break;

            case PlayerNotificationCode.SiegeFinished:
                payload["notificationKind"] = details.NotificationKind;
                payload["missionType"] = details.MissionType ?? "siege";
                payload["attackerPlayerId"] = details.AttackerPlayerId;
                payload["defenderPlayerId"] = details.DefenderPlayerId;
                payload["endedAtUtc"] = details.EndedAtUtc;
                payload["endedByShield"] = details.EndedByShield;
                messageArgs = [details.CounterpartyName];
                break;

            case PlayerNotificationCode.CountryWarEnded:
                payload["notificationKind"] = details.NotificationKind;
                break;

            case PlayerNotificationCode.IncomingAttack:
            case PlayerNotificationCode.IncomingSiege:
            case PlayerNotificationCode.CountryWarStarted:
                payload["notificationKind"] = details.NotificationKind ?? string.Empty;
                payload["attackerPlayerId"] = details.AttackerPlayerId;
                payload["defenderPlayerId"] = details.DefenderPlayerId;
                payload["startedAtUtc"] = details.StartedAtUtc;
                messageArgs = [details.CounterpartyName];
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported notification code.");
        }

        payload[LocalizationPropertyName] = new JsonObject
        {
            [TitleArgsPropertyName] = CreateArgsArray(titleArgs),
            [MessageArgsPropertyName] = CreateArgsArray(messageArgs)
        };

        return payload.ToJsonString();
    }

    private static IReadOnlyList<string> BuildBattleMessageArgs(PlayerNotificationCode code, PlayerNotificationDetails details)
    {
        var losses = code == PlayerNotificationCode.BattleReportAttacker
            ? details.AttackerLosses
            : details.DefenderLosses;

        var args = new List<string?>
        {
            details.AttackerPlayerName,
            details.DefenderPlayerName,
            details.LoserPlayerName,
            losses?.ToString()
        };

        var shouldIncludeLoot = code switch
        {
            PlayerNotificationCode.BattleReportAttacker => string.Equals(details.NotificationKind, "battle_report_attacker_victory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(details.NotificationKind, "siege_loot_gained", StringComparison.OrdinalIgnoreCase),
            PlayerNotificationCode.BattleReportDefender => string.Equals(details.NotificationKind, "battle_report_defender_defeat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(details.NotificationKind, "siege_loot_lost", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

        if (shouldIncludeLoot)
        {
            args.Add(details.LootSummaryText ?? BuildLootSummaryText(details));
        }

        return args.Select(a => a ?? string.Empty).ToList();
    }

    private static string BuildLootSummaryText(PlayerNotificationDetails details)
    {
        var parts = new List<string>();

        if ((details.LootMoney ?? 0) > 0) parts.Add($"Money {FormatDecimal(details.LootMoney)}");
        if ((details.LootOil ?? 0) > 0) parts.Add($"Oil {FormatDecimal(details.LootOil)}");
        if ((details.LootUranium ?? 0) > 0) parts.Add($"Uranium {FormatDecimal(details.LootUranium)}");
        if ((details.LootChips ?? 0) > 0) parts.Add($"Chips {FormatDecimal(details.LootChips)}");

        return parts.Count == 0 ? "None" : string.Join(", ", parts);
    }

    private static string FormatDecimal(decimal? value)
    {
        return (value ?? 0m).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static JsonArray CreateArgsArray(IEnumerable<string?>? args)
    {
        var values = new JsonArray();
        if (args == null)
        {
            return values;
        }

        foreach (var arg in args)
        {
            values.Add(arg ?? string.Empty);
        }

        return values;
    }

    private static PlayerNotification BuildNotification(Guid playerId, string type, string? dataJson = null, DateTime? createdAt = null)
    {
        var normalizedType = type.Trim();

        return new PlayerNotification
        {
            PlayerNotificationsId = Guid.NewGuid(),
            PlayersId = playerId,
            Type = normalizedType,
            DataJson = dataJson,
            IsRead = false,
            IsPopupDelivered = false,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }
}
