namespace VoltrokEF;

public partial class BackgroundWorkerLastRun
{
    public string RunType { get; set; } = null!;

    public DateTime LastExecutedAtUtc { get; set; }

    public DateTime UpdatedAt { get; set; }
}
