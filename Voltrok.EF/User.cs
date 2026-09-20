using System.ComponentModel.DataAnnotations;

namespace VoltrokEF;

public partial class User
{
    public Guid UsersId { get; set; }

    [EmailAddress]
    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string AuthProvider { get; set; } = null!;

    public string? GoogleSubjectId { get; set; }

    public bool GoogleEmailVerified { get; set; }

    public DateTime? GoogleLinkedAt { get; set; }

    public string? StripeCustomerId { get; set; }

    public DateTime LastLogin { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Player? Player { get; set; }

    public virtual ICollection<PremiumPaymentEvent> PremiumPaymentEvents { get; set; } = new List<PremiumPaymentEvent>();

    public virtual ICollection<UserLoginHistory> UserLoginHistories { get; set; } = new List<UserLoginHistory>();
}
