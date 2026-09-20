namespace VoltrokEF;

public partial class PremiumPaymentEvent
{
    public Guid PremiumPaymentEventsId { get; set; }

    public Guid UsersId { get; set; }

    public string StripeEventId { get; set; } = null!;

    public string EventType { get; set; } = null!;

    public string? StripeObjectId { get; set; }

    public string Status { get; set; } = null!;

    public string? ErrorMessage { get; set; }

    public string PayloadJson { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public virtual User Users { get; set; } = null!;
}
