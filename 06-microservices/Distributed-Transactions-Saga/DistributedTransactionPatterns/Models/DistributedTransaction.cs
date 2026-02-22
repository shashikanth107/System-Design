namespace DistributedTransactionPatterns.Models;

public enum SagaPattern
{
    Orchestration,
    Choreography
}

public enum TransactionStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Compensating,
    Compensated,
    CompensationFailed
}

public enum StepStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Compensated,
    Skipped
}

public sealed class TransactionStep
{
    public string Name { get; init; } = string.Empty;
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TimelineEvent
{
    public DateTimeOffset AtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string Source { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class DistributedTransaction
{
    public Guid TransactionId { get; init; }
    public SagaPattern Pattern { get; init; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Amount { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<TransactionStep> Steps { get; init; } = [];
    public List<TimelineEvent> Timeline { get; init; } = [];

    // Internal lock object to keep updates consistent in this in-memory sample.
    public object SyncRoot { get; } = new();
}
