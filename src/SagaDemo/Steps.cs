using System.Collections.Concurrent;

namespace SagaDemo;

// Cada passo seria uma chamada a outro microsserviço (HTTP ou mensagem).
// Aqui são simulados em memória; a chave de idempotência é o OrderId.
public sealed class ReserveStockStep : ISagaStep
{
    private readonly ConcurrentDictionary<Guid, bool> _reservations = new();
    public string Name => "ReservarEstoque";

    public Task ExecuteAsync(OrderContext ctx, CancellationToken ct)
    {
        _reservations[ctx.OrderId] = true;   // upsert: repetir não duplica
        return Task.CompletedTask;
    }

    public Task CompensateAsync(OrderContext ctx, CancellationToken ct)
    {
        _reservations.TryRemove(ctx.OrderId, out _);   // remover o que não existe é no-op
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
            throw new InvalidOperationException("Cartão recusado");
        _authorizations.TryAdd(ctx.OrderId, ctx.Total);   // não cobra duas vezes o mesmo pedido
        return Task.CompletedTask;
    }

    public Task CompensateAsync(OrderContext ctx, CancellationToken ct)
    {
        _authorizations.TryRemove(ctx.OrderId, out _);   // cancelar a autorização
        return Task.CompletedTask;
    }
}

public sealed class CreateShipmentStep : ISagaStep
{
    public string Name => "CriarEntrega";
    public Task ExecuteAsync(OrderContext ctx, CancellationToken ct) => Task.CompletedTask;
    public Task CompensateAsync(OrderContext ctx, CancellationToken ct) => Task.CompletedTask;
}
