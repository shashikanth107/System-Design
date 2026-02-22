using DistributedTransactionPatterns.Models;

namespace DistributedTransactionPatterns.Services;

public sealed class OrchestrationSagaExecutor(
    ITransactionStore store,
    InventoryService inventoryService,
    PaymentService paymentService,
    ShippingService shippingService,
    ILogger<OrchestrationSagaExecutor> logger)
{
    public Guid Start(StartTransactionRequest request)
    {
        var transaction = store.Create(SagaPattern.Orchestration, request);
        _ = Task.Run(() => ExecuteAsync(transaction.TransactionId, request, CancellationToken.None));
        return transaction.TransactionId;
    }

    private async Task ExecuteAsync(Guid transactionId, StartTransactionRequest request, CancellationToken cancellationToken)
    {
        var compensations = new Stack<Func<Task>>();

        store.Update(transactionId, transaction =>
        {
            transaction.Status = TransactionStatus.InProgress;
            transaction.AddEvent("Orchestrator", "Saga started");
        });

        try
        {
            await ExecuteStepAsync(
                transactionId,
                stepName: "Inventory",
                action: ct => inventoryService.ReserveAsync(request, ct),
                compensation: ct => inventoryService.ReleaseAsync(request, ct),
                compensations,
                cancellationToken);

            await ExecuteStepAsync(
                transactionId,
                stepName: "Payment",
                action: ct => paymentService.ChargeAsync(request, ct),
                compensation: ct => paymentService.RefundAsync(request, ct),
                compensations,
                cancellationToken);

            await ExecuteStepAsync(
                transactionId,
                stepName: "Shipping",
                action: ct => shippingService.ScheduleAsync(request, ct),
                compensation: ct => shippingService.CancelAsync(request, ct),
                compensations,
                cancellationToken);

            store.Update(transactionId, transaction =>
            {
                transaction.Status = TransactionStatus.Completed;
                transaction.AddEvent("Orchestrator", "Saga completed successfully");
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Orchestration failed for transaction {TransactionId}", transactionId);
            await CompensateAsync(transactionId, compensations, cancellationToken);
        }
    }

    private async Task ExecuteStepAsync(
        Guid transactionId,
        string stepName,
        Func<CancellationToken, Task> action,
        Func<CancellationToken, Task> compensation,
        Stack<Func<Task>> compensations,
        CancellationToken cancellationToken)
    {
        store.Update(transactionId, transaction =>
        {
            transaction.SetStep(stepName, StepStatus.Running, "In progress");
            transaction.AddEvent(stepName, "Step started");
        });

        try
        {
            await action(cancellationToken);

            store.Update(transactionId, transaction =>
            {
                transaction.SetStep(stepName, StepStatus.Succeeded, "Completed");
                transaction.AddEvent(stepName, "Step succeeded");
            });

            compensations.Push(async () =>
            {
                await compensation(cancellationToken);
                store.Update(transactionId, transaction =>
                {
                    transaction.SetStep(stepName, StepStatus.Compensated, "Compensation completed");
                    transaction.AddEvent(stepName, "Compensation succeeded");
                });
            });
        }
        catch (Exception ex)
        {
            store.Update(transactionId, transaction =>
            {
                transaction.SetStep(stepName, StepStatus.Failed, ex.Message);
                transaction.Status = TransactionStatus.Failed;
                transaction.AddEvent(stepName, $"Step failed: {ex.Message}");
            });

            throw;
        }
    }

    private async Task CompensateAsync(Guid transactionId, Stack<Func<Task>> compensations, CancellationToken cancellationToken)
    {
        store.Update(transactionId, transaction =>
        {
            transaction.Status = TransactionStatus.Compensating;
            transaction.AddEvent("Orchestrator", "Compensation started");
        });

        while (compensations.Count > 0)
        {
            var compensation = compensations.Pop();
            try
            {
                await compensation();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Compensation failed for transaction {TransactionId}", transactionId);
                store.Update(transactionId, transaction =>
                {
                    transaction.Status = TransactionStatus.CompensationFailed;
                    transaction.AddEvent("Orchestrator", $"Compensation failed: {ex.Message}");
                });
                return;
            }
        }

        store.Update(transactionId, transaction =>
        {
            transaction.Status = TransactionStatus.Compensated;
            transaction.AddEvent("Orchestrator", "Compensation finished");
        });
    }
}
