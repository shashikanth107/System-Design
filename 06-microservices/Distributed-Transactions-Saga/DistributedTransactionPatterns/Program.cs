using DistributedTransactionPatterns.Models;
using DistributedTransactionPatterns.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITransactionStore, InMemoryTransactionStore>();
builder.Services.AddSingleton<InventoryService>();
builder.Services.AddSingleton<PaymentService>();
builder.Services.AddSingleton<ShippingService>();
builder.Services.AddSingleton<OrchestrationSagaExecutor>();
builder.Services.AddSingleton<ChoreographySagaExecutor>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    project = "DistributedTransactionPatterns",
    message = "Use /api/saga/orchestration or /api/saga/choreography to start a transaction and /api/saga/transactions/{id} to inspect status."
}));

app.MapPost("/api/saga/orchestration", (StartTransactionRequest request, OrchestrationSagaExecutor executor) =>
{
    var transactionId = executor.Start(request);
    return Results.Accepted($"/api/saga/transactions/{transactionId}", new
    {
        transactionId,
        pattern = SagaPattern.Orchestration.ToString(),
        statusEndpoint = $"/api/saga/transactions/{transactionId}"
    });
});

app.MapPost("/api/saga/choreography", (StartTransactionRequest request, ChoreographySagaExecutor executor) =>
{
    var transactionId = executor.Start(request);
    return Results.Accepted($"/api/saga/transactions/{transactionId}", new
    {
        transactionId,
        pattern = SagaPattern.Choreography.ToString(),
        statusEndpoint = $"/api/saga/transactions/{transactionId}"
    });
});

app.MapGet("/api/saga/transactions", (ITransactionStore store) =>
{
    var transactions = store.GetAll().Select(MapSummary);
    return Results.Ok(transactions);
});

app.MapGet("/api/saga/transactions/{transactionId:guid}", (Guid transactionId, ITransactionStore store) =>
{
    return store.TryGet(transactionId, out var transaction)
        ? Results.Ok(MapDetails(transaction!))
        : Results.NotFound(new { message = $"Transaction {transactionId} not found." });
});

app.Run();

static object MapSummary(DistributedTransaction transaction) => new
{
    transaction.TransactionId,
    Pattern = transaction.Pattern.ToString(),
    Status = transaction.Status.ToString(),
    transaction.ProductId,
    transaction.Quantity,
    transaction.Amount,
    transaction.CreatedAtUtc,
    transaction.UpdatedAtUtc
};

static object MapDetails(DistributedTransaction transaction) => new
{
    transaction.TransactionId,
    Pattern = transaction.Pattern.ToString(),
    Status = transaction.Status.ToString(),
    transaction.ProductId,
    transaction.Quantity,
    transaction.Amount,
    transaction.CreatedAtUtc,
    transaction.UpdatedAtUtc,
    Steps = transaction.Steps.Select(step => new
    {
        step.Name,
        Status = step.Status.ToString(),
        step.Message,
        step.UpdatedAtUtc
    }),
    Timeline = transaction.Timeline.Select(entry => new
    {
        entry.AtUtc,
        entry.Source,
        entry.Message
    })
};
