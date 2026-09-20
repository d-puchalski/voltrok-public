using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VoltrokEF;
using VoltrokServices.Services.Notifications;
using VoltrokUtils.Enums;
using VoltrokUtils.Models;

namespace VoltrokServices.Services.Chat;

public class ChatService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    NotificationService notificationService)
{
    private const string ChatCacheKey = "ChatMessages";
    private const int MaxMessages = 100;

    public async Task<List<DirectContactSummary>> GetDirectContactSummariesAsync(Guid playerId)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var rawContacts = await dbContext.ChatPrivateMessages
            .AsNoTracking()
            .Where(c => c.SenderPlayersId == playerId || c.ReceiverPlayersId == playerId)
            .Select(c => new
            {
                c.MessageId,
                c.SenderPlayersId,
                ContactId = c.SenderPlayersId == playerId ? c.ReceiverPlayersId : c.SenderPlayersId,
                ContactName = c.SenderPlayersId == playerId ? c.ReceiverPlayers.Name : c.SenderPlayers.Name,
                SentAt = c.SentAt ?? DateTime.MinValue
            })
            .OrderByDescending(c => c.SentAt)
            .ThenByDescending(c => c.MessageId)
            .ToListAsync();

        var summaries = new List<DirectContactSummary>();
        var seen = new HashSet<Guid>();

        foreach (var contact in rawContacts)
        {
            if (!seen.Add(contact.ContactId))
            {
                continue;
            }

            summaries.Add(new DirectContactSummary
            {
                ContactPlayerId = contact.ContactId,
                ContactPlayerName = contact.ContactName,
                LastMessageId = contact.MessageId,
                LastSenderPlayerId = contact.SenderPlayersId,
                LastSentAt = contact.SentAt
            });
        }

        return summaries;
    }

    public async Task<List<ChatGlobalMessage>> GetGlobalChatsAsync()
    {
        if (cache.TryGetValue(ChatCacheKey, out List<ChatGlobalMessage>? cached))
        {
            return cached;
        }

        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var messages = await dbContext.ChatGlobalMessages
            .AsNoTracking()
            .Include(c => c.Players)
            .OrderBy(c => c.SentAt)
            .Take(MaxMessages)
            .ToListAsync();

        cache.Set(ChatCacheKey, messages);
        return messages;
    }

    public async Task<List<ChatCountryMessage>> GetCountryChatsAsync(Guid senderPlayerId)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var countryId = await dbContext.Players
            .Where(v => v.PlayersId == senderPlayerId)
            .Select(v => v.CountriesId)
            .FirstOrDefaultAsync();

        var countryCacheKey = $"ChatMessages:Country:{countryId}";
        if (cache.TryGetValue(countryCacheKey, out List<ChatCountryMessage>? cachedCountry))
        {
            return cachedCountry;
        }

        var messages = await dbContext.ChatCountryMessages
            .AsNoTracking()
            .Include(c => c.Players)
            .Where(c => c.CountriesId == countryId)
            .OrderBy(c => c.SentAt)
            .Take(MaxMessages)
            .ToListAsync();

        cache.Set(countryCacheKey, messages);
        return messages;
    }

    public async Task<List<ChatPrivateMessage>> GetDirectChatsAsync(Guid senderPlayerId, Guid targetPlayerId)
    {
        var directCacheKey = $"ChatMessages:Direct:{GetDirectPairKey(senderPlayerId, targetPlayerId)}";
        if (cache.TryGetValue(directCacheKey, out List<ChatPrivateMessage>? cachedDirect))
        {
            return cachedDirect;
        }

        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var messages = await dbContext.ChatPrivateMessages
            .AsNoTracking()
            .Include(c => c.SenderPlayers)
            .Where(c =>
                (c.SenderPlayersId == senderPlayerId && c.ReceiverPlayersId == targetPlayerId) ||
                (c.SenderPlayersId == targetPlayerId && c.ReceiverPlayersId == senderPlayerId))
            .OrderBy(c => c.SentAt)
            .Take(MaxMessages)
            .ToListAsync();

        cache.Set(directCacheKey, messages);
        return messages;
    }

    public async Task<object?> SendChatAsync(string messageText, ChatChannel channel, Guid senderPlayerId, Guid? targetPlayerId)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return null;
        }

        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var senderPlayer = await dbContext.Players
            .FirstOrDefaultAsync(v => v.PlayersId == senderPlayerId);

        if (senderPlayer == null)
        {
            return null;
        }

        var messageId = Guid.NewGuid();
        var sentAt = DateTime.UtcNow;

        switch (channel)
        {
            case ChatChannel.General:
            {
                var chatMessage = new ChatGlobalMessage
                {
                    MessageId = messageId,
                    PlayersId = senderPlayer.PlayersId,
                    MessageText = messageText,
                    SentAt = sentAt
                };

                dbContext.ChatGlobalMessages.Add(chatMessage);
                await dbContext.SaveChangesAsync();

                var saved = await dbContext.ChatGlobalMessages
                    .AsNoTracking()
                    .Include(c => c.Players)
                    .FirstOrDefaultAsync(c => c.MessageId == messageId);

                if (saved != null)
                {
                    AppendCacheItem(ChatCacheKey, saved);
                }

                return saved;
            }
            case ChatChannel.Country:
            {
                var countryMessage = new ChatCountryMessage
                {
                    MessageId = messageId,
                    CountriesId = senderPlayer.CountriesId,
                    PlayersId = senderPlayer.PlayersId,
                    MessageText = messageText,
                    SentAt = sentAt
                };

                dbContext.ChatCountryMessages.Add(countryMessage);
                await dbContext.SaveChangesAsync();

                var saved = await dbContext.ChatCountryMessages
                    .AsNoTracking()
                    .Include(c => c.Players)
                    .FirstOrDefaultAsync(c => c.MessageId == messageId);

                var cacheKey = GetCacheKey(ChatChannel.Country, senderPlayerId, null, senderPlayer);
                if (cacheKey != null && saved != null)
                {
                    AppendCacheItem(cacheKey, saved);
                }

                return saved;
            }
            case ChatChannel.Direct:
            {
                if (!targetPlayerId.HasValue)
                {
                    return null;
                }

                var privateMessage = new ChatPrivateMessage
                {
                    MessageId = messageId,
                    SenderPlayersId = senderPlayer.PlayersId,
                    ReceiverPlayersId = targetPlayerId.Value,
                    MessageText = messageText,
                    SentAt = sentAt
                };

                dbContext.ChatPrivateMessages.Add(privateMessage);

                if (targetPlayerId.Value != senderPlayer.PlayersId)
                {
                    var preview = messageText.Length > 80
                        ? $"{messageText[..77]}..."
                        : messageText;

                    await notificationService.CreateAsync(
                        targetPlayerId.Value,
                        PlayerNotificationCode.ChatMessageReceived,
                        new PlayerNotificationDetails
                        {
                            Channel = nameof(ChatChannel.Direct).ToLowerInvariant(),
                            SenderPlayerId = senderPlayer.PlayersId,
                            SenderPlayerName = senderPlayer.Name,
                            Preview = preview
                        },
                        sentAt);
                }

                await dbContext.SaveChangesAsync();

                var saved = await dbContext.ChatPrivateMessages
                    .AsNoTracking()
                    .Include(c => c.SenderPlayers)
                    .FirstOrDefaultAsync(c => c.MessageId == messageId);

                var cacheKey = GetCacheKey(ChatChannel.Direct, senderPlayerId, targetPlayerId, senderPlayer);
                if (cacheKey != null && saved != null)
                {
                    AppendCacheItem(cacheKey, saved);
                }

                return saved;
            }
            default:
                return null;
        }
    }

    public async Task<string?> GetChannelKeyAsync(ChatChannel channel, Guid senderPlayerId, Guid? targetPlayerId = null)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var senderPlayer = await dbContext.Players
            .FirstOrDefaultAsync(v => v.PlayersId == senderPlayerId);

        if (senderPlayer == null)
        {
            return null;
        }

        return GetCacheKey(channel, senderPlayerId, targetPlayerId, senderPlayer);
    }

    private string? GetCacheKey(ChatChannel channel, Guid? senderPlayerId, Guid? targetPlayerId, Player? senderPlayer = null)
    {
        return channel switch
        {
            ChatChannel.General => ChatCacheKey, 
            ChatChannel.Country => senderPlayer?.CountriesId != null ? $"ChatMessages:Country:{senderPlayer.CountriesId}" : null, 
            ChatChannel.Direct when senderPlayerId.HasValue && targetPlayerId.HasValue => 
                $"ChatMessages:Direct:{GetDirectPairKey(senderPlayerId.Value, targetPlayerId.Value)}", 
            _ => null 
        };
    }

    private void AppendCacheItem<TMessage>(string cacheKey, TMessage message)
    {
        var list = cache.Get<List<TMessage>>(cacheKey) ?? [];
        list.Add(message);
        if (list.Count > MaxMessages)
        {
            list.RemoveAt(0);
        }

        cache.Set(cacheKey, list);
    }

    private static string GetDirectPairKey(Guid first, Guid second)
    {
        return string.CompareOrdinal(first.ToString(), second.ToString()) < 0
            ? $"{first}:{second}"
            : $"{second}:{first}";
    }
}
