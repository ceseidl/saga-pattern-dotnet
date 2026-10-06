using System.Collections.Concurrent;

namespace SagaDemo;

// EN: Each step would be a call to another microservice (HTTP or message).
// PT: Cada passo seria uma chamada a outro microsserviço (HTTP ou mensagem).
// EN: Here they are simulated in memory; the idempotency key is the OrderId.
// PT: Aqui são simulados em memória; a chave de idempotência é o OrderId.
public sealed class ReserveStockStep : ISagaStep
{
    private readonly ConcurrentDictionary<Guid, bool> _reservations = new();
    public string Name => "ReservarEstoque";

    public Task ExecuteAsync(OrderContext ctx, CancellationToken ct)
    {
        // EN: Upsert: repeating does not duplicate.
        // PT: Upsert: repetir não duplica.
        _reservations[ctx.OrderId] = true;
        return Task.CompletedTask;
    }

    public Task CompensateAsync(OrderContext ctx, CancellationToken ct)
    {
        // EN: Removing what does not exist is a no-op.
        // PT: Remover o que não existe é no-op.
        _reservations.TryRemove(ctx.OrderId, out _);
        return Task.CompletedTask;
    }
}

public sealed class AuthorizePaymentStep(bool failOnExecute) : ISagaStep
{
    private readonly ConcurrentDictionary<Guid, decimal> _authorizations = new();
    public string Name => "AutorizarPagamento";

    public Task ExecuteAsync(OrderContext ctx, CancellationToken ct)
    {
        if (failOnExecute)
            throw new InvalidOperationException("Card declined / Cartão recusado");
        // EN: Does not charge the same order twice.
        // PT: Não cobra duas vezes o mesmo pedido.
        _authorizations.TryAdd(ctx.OrderId, ctx.Total);
        return Task.CompletedTask;
    }

    public Task CompensateAsync(OrderContext ctx, CancellationToken ct)
    {
        // EN: Cancel the authorization.
        // PT: Cancelar a autorização.
        _authorizations.TryRemove(ctx.OrderId, out _);
        return Task.CompletedTask;
    }
}

public sealed class CreateShipmentStep : ISagaStep
{
    public string Name => "CriarEntrega";
    public Task ExecuteAsync(OrderContext ctx, CancellationToken ct) => Task.CompletedTask;
    public Task CompensateAsync(OrderContext ctx, CancellationToken ct) => Task.CompletedTask;
}
