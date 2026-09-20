namespace VoltrokEF;

public partial class ChatGlobalMessage
{
    public Guid MessageId { get; set; }

    public Guid PlayersId { get; set; }

    public string MessageText { get; set; } = null!;

    public DateTime? SentAt { get; set; }

    public virtual Player Players { get; set; } = null!;
}
