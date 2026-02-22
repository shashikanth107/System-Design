using System.Collections.Concurrent;
using DistributedTransactionPatterns.Models;

namespace DistributedTransactionPatterns.Services;

public sealed class InMemoryTransactionStore : ITransactionStore
{
    private readonly ConcurrentDictionary<Guid, DistributedTransaction> _transactions = new();

    public DistributedTransaction Create(SagaPattern pattern, StartTransactionRequest request)
    {
        var transaction = new DistributedTransaction
        {
            TransactionId = Guid.NewGuid(),
            Pattern = pattern,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            Amount = request.Amount,
            Status = TransactionStatus.Pending,
            Steps =
            [
                new TransactionStep { Name = "Inventory", Message = "Waiting" },
                new TransactionStep { Name = "Payment", Message = "Waiting" },
                new TransactionStep { Name = "Shipping", Message = "Waiting" }
            ],
            Timeline =
            [
                new TimelineEvent { Source = "API", Message = $"Transaction created for {pattern}" }
            ]
        };

        _transactions[transaction.TransactionId] = transaction;
        return transaction;
    }

    public bool TryGet(Guid transactionId, out DistributedTransaction? transaction)
        => _transactions.TryGetValue(transactionId, out transaction);

    public IReadOnlyCollection<DistributedTransaction> GetAll()
        => _transactions.Values
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToArray();

    public void Update(Guid transactionId, Action<DistributedTransaction> mutation)
    {
        if (!_transactions.TryGetValue(transactionId, out var transaction))
        {
            return;
        }

        lock (transaction.SyncRoot)
        {
            mutation(transaction);
            transaction.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}
