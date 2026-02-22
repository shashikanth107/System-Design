using DistributedTransactionPatterns.Models;

namespace DistributedTransactionPatterns.Services;

public sealed class PaymentService
{
    public async Task ChargeAsync(StartTransactionRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(Math.Max(request.StepDelayMs + 400, 250), cancellationToken);
        if (request.FailOnPayment)
        {
            throw new InvalidOperationException("Payment authorization failed.");
        }
    }

    public Task RefundAsync(StartTransactionRequest request, CancellationToken cancellationToken)
        => Task.Delay(Math.Max(request.StepDelayMs / 2, 200), cancellationToken);
}
