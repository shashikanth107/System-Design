# Distributed Transaction Patterns in ASP.NET Core (Saga)

This project is a standalone ASP.NET Core 8 Web API sample that demonstrates how to design **distributed transactions** without a global 2PC coordinator.

It implements:
- **Saga Orchestration pattern** (central coordinator controls steps)
- **Saga Choreography pattern** (services react to events)
- **Long-running transaction behavior** (async step delays)
- **Multi-service transaction flow** (`Inventory -> Payment -> Shipping`)
- **Rollback via compensating transactions** on failure

## Project location

`/Users/shashi/Repos/Private/System-Design/06-microservices/Distributed-Transactions-Saga/DistributedTransactionPatterns`

## What this sample models

A checkout flow across multiple services:
1. Reserve inventory
2. Charge payment
3. Schedule shipping

If any step fails, compensation runs in reverse order for already-completed steps.

## Architecture

### 1) Orchestration Saga

A central `OrchestrationSagaExecutor`:
- Starts the transaction
- Invokes each service step in order
- Tracks state and timeline
- Triggers compensations when a step fails

Flow:
`Reserve Inventory -> Charge Payment -> Schedule Shipping`

Compensation order:
`Cancel Shipping -> Refund Payment -> Release Inventory`

### 2) Choreography Saga

A decentralized `ChoreographySagaExecutor` with internal event-driven flow:
- Publishes `OrderCreated`
- Inventory service handles it and publishes `InventoryReserved`
- Payment service handles next event and publishes `PaymentCharged`
- Shipping service handles next event and publishes `ShippingScheduled`
- On failure, publishes `SagaFailed` and runs compensations based on completed steps

## Core files

- `Program.cs`: API endpoints and DI setup
- `Models/DistributedTransaction.cs`: transaction state, step state, timeline event models
- `Models/StartTransactionRequest.cs`: request payload and failure simulation flags
- `Services/InMemoryTransactionStore.cs`: thread-safe in-memory transaction store
- `Services/OrchestrationSagaExecutor.cs`: orchestration coordinator + compensation stack
- `Services/ChoreographySagaExecutor.cs`: choreography event flow + distributed compensation
- `Services/InventoryService.cs`, `Services/PaymentService.cs`, `Services/ShippingService.cs`: simulated microservices

## API endpoints

### Start orchestration saga

`POST /api/saga/orchestration`

### Start choreography saga

`POST /api/saga/choreography`

### List all transactions

`GET /api/saga/transactions`

### Get transaction details

`GET /api/saga/transactions/{transactionId}`

Response contains:
- Overall transaction status
- Per-step statuses (`Pending`, `Running`, `Succeeded`, `Failed`, `Compensated`, `Skipped`)
- Timeline events for debugging and observability

## Request payload

```json
{
  "amount": 199.99,
  "productId": "LAPTOP-15",
  "quantity": 1,
  "stepDelayMs": 800,
  "failOnInventory": false,
  "failOnPayment": false,
  "failOnShipping": false
}
```

Failure flags help test compensation scenarios.

## Run locally

```bash
cd /Users/shashi/Repos/Private/System-Design/06-microservices/Distributed-Transactions-Saga/DistributedTransactionPatterns
dotnet restore
dotnet run
```

By default, API is available at:
- `http://localhost:5188` (may vary based on launch profile)

## Test examples

### 1) Happy path (orchestration)

```bash
curl -X POST http://localhost:5188/api/saga/orchestration \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 450,
    "productId": "PHONE-ULTRA",
    "quantity": 1,
    "stepDelayMs": 700,
    "failOnInventory": false,
    "failOnPayment": false,
    "failOnShipping": false
  }'
```

### 2) Force rollback (payment failure in orchestration)

```bash
curl -X POST http://localhost:5188/api/saga/orchestration \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 450,
    "productId": "PHONE-ULTRA",
    "quantity": 1,
    "stepDelayMs": 700,
    "failOnPayment": true
  }'
```

Expected behavior:
- Inventory succeeds first
- Payment fails
- Inventory compensation executes (release)
- Transaction ends as `Compensated`

### 3) Force rollback (shipping failure in choreography)

```bash
curl -X POST http://localhost:5188/api/saga/choreography \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 650,
    "productId": "TABLET-PRO",
    "quantity": 2,
    "stepDelayMs": 900,
    "failOnShipping": true
  }'
```

Expected behavior:
- Inventory reserved
- Payment charged
- Shipping fails
- Compensation runs (refund payment, release inventory)

## Long-running transaction handling design

This sample simulates long-running steps with async delay and returns `202 Accepted` immediately.
The transaction continues in background and can be polled by ID.

In real systems, extend with:
- Durable state store (SQL/NoSQL)
- Outbox pattern and idempotency keys
- Retry + backoff + dead-letter queues
- Timeout/escalation rules for stuck transactions
- Distributed tracing (`traceId`, `spanId`)

## Recommended production hardening

- Persist saga state in a durable database
- Add idempotent command handling for each step
- Use message brokers (Kafka, RabbitMQ, Azure Service Bus) for choreography
- Implement outbox/inbox for exactly-once processing semantics at service boundary
- Add observability: OpenTelemetry traces, metrics, structured logs
- Add retry policies with circuit breakers and rate limits

## Reference links

- Saga pattern (microservices.io): [https://microservices.io/patterns/data/saga.html](https://microservices.io/patterns/data/saga.html)
- Choreography vs Orchestration (microservices.io): [https://microservices.io/post/sagas/2019/08/15/developing-sagas-part-3.html](https://microservices.io/post/sagas/2019/08/15/developing-sagas-part-3.html)
- Transactional Outbox pattern: [https://microservices.io/patterns/data/transactional-outbox.html](https://microservices.io/patterns/data/transactional-outbox.html)
- Idempotent consumer pattern: [https://microservices.io/post/microservices/patterns/2020/10/16/idempotent-consumer.html](https://microservices.io/post/microservices/patterns/2020/10/16/idempotent-consumer.html)
- .NET minimal APIs: [https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)
- Distributed tracing in .NET: [https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing](https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing)

## Notes

- This is intentionally in-memory and educational.
- Restarting the app clears transactions.
- The same design can be moved to real services by replacing in-memory store and simulator classes with real clients + message transport.
