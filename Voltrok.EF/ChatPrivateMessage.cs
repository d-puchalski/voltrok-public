namespace VoltrokEF;

public partial class ChatPrivateMessage
{
    public Guid MessageId { get; set; }

    public Guid SenderPlayersId { get; set; }

    public Guid ReceiverPlayersId { get; set; }

    public string MessageText { get; set; } = null!;

    public bool? IsRead { get; set; }

    public DateTime? SentAt { get; set; }

    public virtual Player ReceiverPlayers { get; set; } = null!;

    public virtual Player SenderPlayers { get; set; } = null!;
}
