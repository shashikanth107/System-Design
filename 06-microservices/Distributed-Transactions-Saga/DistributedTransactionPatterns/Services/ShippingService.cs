using DistributedTransactionPatterns.Models;

namespace DistributedTransactionPatterns.Services;

public sealed class ShippingService
{
    public async Task ScheduleAsync(StartTransactionRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(Math.Max(request.StepDelayMs + 250, 250), cancellationToken);
        if (request.FailOnShipping)
        {
            throw new InvalidOperationException("Shipping slot not available.");
        }
    }

    public Task CancelAsync(StartTransactionRequest request, CancellationToken cancellationToken)
        => Task.Delay(Math.Max(request.StepDelayMs / 2, 200), cancellationToken);
}
