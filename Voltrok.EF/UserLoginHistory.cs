namespace VoltrokEF;

public partial class UserLoginHistory
{
    public Guid UserLoginHistoriesId { get; set; }

    public Guid UsersId { get; set; }

    public bool IsSuccessful { get; set; }

    public string? FailureReason { get; set; }

    public DateTime AttemptedAt { get; set; }

    public virtual User Users { get; set; } = null!;
}
