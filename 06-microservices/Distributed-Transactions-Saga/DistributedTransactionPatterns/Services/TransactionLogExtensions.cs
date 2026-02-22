using DistributedTransactionPatterns.Models;

namespace DistributedTransactionPatterns.Services;

public static class TransactionLogExtensions
{
    public static void AddEvent(this DistributedTransaction transaction, string source, string message)
    {
        transaction.Timeline.Add(new TimelineEvent
        {
            AtUtc = DateTimeOffset.UtcNow,
            Source = source,
            Message = message
        });
    }

    public static void SetStep(this DistributedTransaction transaction, string name, StepStatus status, string message)
    {
        var step = transaction.Steps.First(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        step.Status = status;
        step.Message = message;
        step.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
