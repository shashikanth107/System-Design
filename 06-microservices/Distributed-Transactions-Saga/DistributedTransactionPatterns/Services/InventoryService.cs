using DistributedTransactionPatterns.Models;

namespace DistributedTransactionPatterns.Services;

public sealed class InventoryService
{
    public Task ReserveAsync(StartTransactionRequest request, CancellationToken cancellationToken)
        => RunStepAsync("Inventory reservation failed.", request.StepDelayMs, request.FailOnInventory, cancellationToken);

    public Task ReleaseAsync(StartTransactionRequest request, CancellationToken cancellationToken)
        => Task.Delay(Math.Max(request.StepDelayMs / 2, 200), cancellationToken);

    private static async Task RunStepAsync(string failureMessage, int delayMs, bool shouldFail, CancellationToken cancellationToken)
    {
        await Task.Delay(Math.Max(delayMs, 100), cancellationToken);
        if (shouldFail)
        {
            throw new InvalidOperationException(failureMessage);
        }
    }
}
