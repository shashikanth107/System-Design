using DistributedTransactionPatterns.Models;

namespace DistributedTransactionPatterns.Services;

public interface ITransactionStore
{
    DistributedTransaction Create(SagaPattern pattern, StartTransactionRequest request);
    bool TryGet(Guid transactionId, out DistributedTransaction? transaction);
    IReadOnlyCollection<DistributedTransaction> GetAll();
    void Update(Guid transactionId, Action<DistributedTransaction> mutation);
}
