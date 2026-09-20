namespace VoltrokUtils.Models;

public sealed class DirectContactSummary
{
    public required Guid ContactPlayerId { get; init; }
    public required string ContactPlayerName { get; init; }
    public required Guid LastMessageId { get; init; }
    public required Guid LastSenderPlayerId { get; init; }
    public DateTime LastSentAt { get; init; }
}
