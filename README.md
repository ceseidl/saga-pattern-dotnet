English | [Português](README.pt-BR.md)

# Saga Pattern in .NET

A didactic example of the **Saga Pattern** with orchestration and compensation in C#/.NET 10, with no external dependencies. It accompanies the article *Saga Pattern em .NET: Transações Distribuídas entre Microsserviços* (Portuguese), from the **Arquitetura .NET** series.

> Study code. The "services" are simulated in memory and the saga state is **not persisted**. See [Limitations](#limitations).

## The problem

In a monolith, "create order" fits in a single transaction: reserve stock, charge the customer and create the shipment, with `COMMIT` or `ROLLBACK`. When you split into microservices, each service owns its database and that single transaction no longer exists. If stock was reserved and the payment was declined, someone has to undo the reservation.

The Saga Pattern solves this **without a distributed transaction**: it breaks the operation into **local transactions** and, if one fails, runs **compensating transactions** on the steps that already completed.

## What the example shows

- **Orchestration**: an orchestrator runs the steps in order and, on the first failure, compensates the completed steps **in reverse order**.
- **Compensation with retries**: each compensation is attempted up to 3 times. If it runs out of attempts, the result is `CompensationFailed` (alert and manual intervention).
- **Idempotent steps**: executing or compensating twice has the same effect as once (key: `OrderId`).
- **Idempotent consumer (Inbox)**: a message delivered twice is processed only once.

## Structure

```
src/SagaDemo/
├── Saga.cs      # step contract (ISagaStep), context and orchestrator
├── Steps.cs     # ReservarEstoque, AutorizarPagamento, CriarEntrega
├── Inbox.cs     # InboxStore and StockConsumer (per-message deduplication)
├── Program.cs   # happy path, failure path and duplicate message
└── SagaDemo.csproj
```

Identifiers and step names (`ReservarEstoque`, `AutorizarPagamento`, `CriarEntrega`) are kept in Portuguese, as in the article. In English: reserve stock, authorize payment, create shipment.

## How to run

Requirement: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/ceseidl/saga-pattern-dotnet.git
cd saga-pattern-dotnet/src/SagaDemo
dotnet run
```

Summarized output (messages are bilingual, "English / Português"):

```
Happy path / Fluxo feliz -> Completed
... AutorizarPagamento failed; starting compensation / falhou; iniciando compensação
... compensated / compensado ReservarEstoque
Failure path / Fluxo de falha -> Compensated
Inbox: second delivery ignored / segunda entrega ignorada = True
```

The project uses the `Microsoft.NET.Sdk.Web` SDK only to get `ILogger` and dependency injection from the shared framework without downloading packages. In a plain console project, add `Microsoft.Extensions.Hosting`.

## How it works

1. The orchestrator walks the steps and pushes each one that **completed** onto a stack.
2. If a step throws, the orchestrator pops the stack and calls `CompensateAsync` on each completed step (the one that failed is not on the stack).
3. Each compensation is retried up to 3 times, with increasing wait.
4. The result is `Completed`, `Compensated` or `CompensationFailed`.

```csharp
foreach (var step in steps)
{
    try
    {
        await step.ExecuteAsync(ctx, ct);
        completed.Push(step);                       // only the completed step goes on the stack
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        return await CompensateAsync(ctx, completed, ct);
    }
}
```

### Concepts

- **Compensable**: can be undone by a compensation.
- **Pivot**: the point of no return. After it, the following steps must complete.
- **Retriable**: comes after the pivot, is idempotent and can be repeated until it succeeds.
- **Choreography vs orchestration**: no central controller (events between services) versus an orchestrator that drives the flow. This example uses orchestration.

## Limitations

This example exists to show the mechanics. In production, it lacks:

- **Persisting the saga state** on every transition (today, if the process dies midway, the state is lost). Libraries such as MassTransit, NServiceBus and Wolverine provide this.
- **Transactional Outbox**: write the state and the message in the same local transaction and publish them from a separate process.
- **Database-backed Inbox**: `InboxStore` is in memory. In production, use a table with a uniqueness constraint, written in the same transaction as the business effect.
- **Per-step timeouts**, **correlation** (logs and traces) and **alerts** for `CompensationFailed`.
- **Isolation**: concurrent sagas see intermediate data. Countermeasures: *semantic lock*, commutative updates, re-reading values, pessimistic view.
- Explicitly deciding what to do with `OperationCanceledException` (here it does not trigger compensation).

## When to use and when to avoid

**Use** it when an operation spans several services, each with its own database, and there is a business compensation for each step.

**Avoid** it when everything runs in one service and one database, the flow has few steps, it requires strong and immediate consistency, there are actions that cannot be undone, or the system is a simple CRUD.

## References

- Microsoft Learn: [Saga distributed transactions pattern](https://learn.microsoft.com/azure/architecture/patterns/saga)
- Microsoft Learn: [Compensating Transaction pattern](https://learn.microsoft.com/azure/architecture/patterns/compensating-transaction)
- Microsoft Learn: [Idempotent Consumer pattern](https://learn.microsoft.com/azure/architecture/patterns/idempotent-consumer)
- Microsoft Learn: [Transactional Outbox pattern with Azure Cosmos DB](https://learn.microsoft.com/azure/architecture/databases/guide/transactional-out-box-cosmos)
- microservices.io: [Pattern: Saga](https://microservices.io/patterns/data/saga.html)
- MassTransit: [Saga State Machine](https://masstransit.io/documentation/patterns/saga/state-machine)

## License

[MIT](LICENSE). Author: Carlos Eduardo Seidl.
