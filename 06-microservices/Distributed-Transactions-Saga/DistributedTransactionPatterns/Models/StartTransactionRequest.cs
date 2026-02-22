namespace DistributedTransactionPatterns.Models;

public sealed class StartTransactionRequest
{
    public decimal Amount { get; init; } = 199.99m;
    public string ProductId { get; init; } = "LAPTOP-15";
    public int Quantity { get; init; } = 1;
    public int StepDelayMs { get; init; } = 800;
    public bool FailOnInventory { get; init; }
    public bool FailOnPayment { get; init; }
    public bool FailOnShipping { get; init; }
}
