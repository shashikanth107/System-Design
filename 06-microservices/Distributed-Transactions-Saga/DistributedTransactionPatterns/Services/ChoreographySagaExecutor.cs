using DistributedTransactionPatterns.Models;

namespace DistributedTransactionPatterns.Services;

public sealed class ChoreographySagaExecutor(
    ITransactionStore store,
    InventoryService inventoryService,
    PaymentService paymentService,
    ShippingService shippingService,
    ILogger<ChoreographySagaExecutor> logger)
{
    private interface IChoreographyEvent;
    private sealed record OrderCreated : IChoreographyEvent;
    private sealed record InventoryReserved : IChoreographyEvent;
    private sealed record PaymentCharged : IChoreographyEvent;
    private sealed record ShippingScheduled : IChoreographyEvent;
    private sealed record SagaFailed(string Step, string Message) : IChoreographyEvent;

    public Guid Start(StartTransactionRequest request)
    {
        var transaction = store.Create(SagaPattern.Choreography, request);
        _ = Task.Run(() => ExecuteAsync(transaction.TransactionId, request, CancellationToken.None));
        return transaction.TransactionId;
    }

    private async Task ExecuteAsync(Guid transactionId, StartTransactionRequest request, CancellationToken cancellationToken)
    {
        var inventoryDone = false;
        var paymentDone = false;
        var shippingDone = false;

        var queue = new Queue<IChoreographyEvent>();
        queue.Enqueue(new OrderCreated());

        store.Update(transactionId, transaction =>
        {
            transaction.Status = TransactionStatus.InProgress;
            transaction.AddEvent("EventBus", "Published: OrderCreated");
        });

        while (queue.Count > 0)
        {
            var evt = queue.Dequeue();

            switch (evt)
            {
                case OrderCreated:
                    await HandleOrderCreatedAsync();
                    break;
                case InventoryReserved:
                    await HandleInventoryReservedAsync();
                    break;
                case PaymentCharged:
                    await HandlePaymentChargedAsync();
                    break;
                case ShippingScheduled:
                    store.Update(transactionId, transaction =>
                    {
                        transaction.Status = TransactionStatus.Completed;
                        transaction.AddEvent("EventBus", "Saga completed after ShippingScheduled");
                    });
                    break;
                case SagaFailed failed:
                    await HandleSagaFailedAsync(failed);
                    return;
            }
        }

        async Task HandleOrderCreatedAsync()
        {
            store.Update(transactionId, transaction =>
            {
                transaction.SetStep("Inventory", StepStatus.Running, "Listening to OrderCreated");
                transaction.AddEvent("Inventory", "Received event: OrderCreated");
            });

            try
            {
                await inventoryService.ReserveAsync(request, cancellationToken);
                inventoryDone = true;
                store.Update(transactionId, transaction =>
                {
                    transaction.SetStep("Inventory", StepStatus.Succeeded, "Inventory reserved");
                    transaction.AddEvent("EventBus", "Published: InventoryReserved");
                });
                queue.Enqueue(new InventoryReserved());
            }
            catch (Exception ex)
            {
                queue.Enqueue(new SagaFailed("Inventory", ex.Message));
            }
        }

        async Task HandleInventoryReservedAsync()
        {
            store.Update(transactionId, transaction =>
            {
                transaction.SetStep("Payment", StepStatus.Running, "Listening to InventoryReserved");
                transaction.AddEvent("Payment", "Received event: InventoryReserved");
            });

            try
            {
                await paymentService.ChargeAsync(request, cancellationToken);
                paymentDone = true;
                store.Update(transactionId, transaction =>
                {
                    transaction.SetStep("Payment", StepStatus.Succeeded, "Payment charged");
                    transaction.AddEvent("EventBus", "Published: PaymentCharged");
                });
                queue.Enqueue(new PaymentCharged());
            }
            catch (Exception ex)
            {
                queue.Enqueue(new SagaFailed("Payment", ex.Message));
            }
        }

        async Task HandlePaymentChargedAsync()
        {
            store.Update(transactionId, transaction =>
            {
                transaction.SetStep("Shipping", StepStatus.Running, "Listening to PaymentCharged");
                transaction.AddEvent("Shipping", "Received event: PaymentCharged");
            });

            try
            {
                await shippingService.ScheduleAsync(request, cancellationToken);
                shippingDone = true;
                store.Update(transactionId, transaction =>
                {
                    transaction.SetStep("Shipping", StepStatus.Succeeded, "Shipping scheduled");
                    transaction.AddEvent("EventBus", "Published: ShippingScheduled");
                });
                queue.Enqueue(new ShippingScheduled());
            }
            catch (Exception ex)
            {
                queue.Enqueue(new SagaFailed("Shipping", ex.Message));
            }
        }

        async Task HandleSagaFailedAsync(SagaFailed failed)
        {
            logger.LogWarning("Choreography failed for transaction {TransactionId} at {Step}", transactionId, failed.Step);
            store.Update(transactionId, transaction =>
            {
                transaction.Status = TransactionStatus.Failed;
                transaction.SetStep(failed.Step, StepStatus.Failed, failed.Message);
                transaction.AddEvent("EventBus", $"Published: SagaFailed ({failed.Step})");
            });

            store.Update(transactionId, transaction =>
            {
                transaction.Status = TransactionStatus.Compensating;
                transaction.AddEvent("Compensator", "Starting decentralized compensation flow");
                if (!shippingDone && !failed.Step.Equals("Shipping", StringComparison.OrdinalIgnoreCase))
                {
                    transaction.SetStep("Shipping", StepStatus.Skipped, "Skipped due to upstream failure");
                }
                if (!paymentDone && !failed.Step.Equals("Payment", StringComparison.OrdinalIgnoreCase))
                {
                    transaction.SetStep("Payment", StepStatus.Skipped, "Skipped due to upstream failure");
                }
                if (!inventoryDone && !failed.Step.Equals("Inventory", StringComparison.OrdinalIgnoreCase))
                {
                    transaction.SetStep("Inventory", StepStatus.Skipped, "Skipped due to upstream failure");
                }
            });

            try
            {
                if (shippingDone)
                {
                    await shippingService.CancelAsync(request, cancellationToken);
                    store.Update(transactionId, transaction =>
                    {
                        transaction.SetStep("Shipping", StepStatus.Compensated, "Shipping canceled");
                        transaction.AddEvent("Shipping", "Compensation event handled");
                    });
                }

                if (paymentDone)
                {
                    await paymentService.RefundAsync(request, cancellationToken);
                    store.Update(transactionId, transaction =>
                    {
                        transaction.SetStep("Payment", StepStatus.Compensated, "Payment refunded");
                        transaction.AddEvent("Payment", "Compensation event handled");
                    });
                }

                if (inventoryDone)
                {
                    await inventoryService.ReleaseAsync(request, cancellationToken);
                    store.Update(transactionId, transaction =>
                    {
                        transaction.SetStep("Inventory", StepStatus.Compensated, "Inventory released");
                        transaction.AddEvent("Inventory", "Compensation event handled");
                    });
                }

                store.Update(transactionId, transaction =>
                {
                    transaction.Status = TransactionStatus.Compensated;
                    transaction.AddEvent("Compensator", "Distributed compensation flow completed");
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Compensation failed for transaction {TransactionId}", transactionId);
                store.Update(transactionId, transaction =>
                {
                    transaction.Status = TransactionStatus.CompensationFailed;
                    transaction.AddEvent("Compensator", $"Compensation failure: {ex.Message}");
                });
            }
        }
    }
}
