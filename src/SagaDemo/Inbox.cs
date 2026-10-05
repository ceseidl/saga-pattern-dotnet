using System.Collections.Concurrent;

namespace SagaDemo;

public sealed record SagaMessage(Guid MessageId, Guid SagaId, string Type);

public sealed class InboxStore
{
    private readonly ConcurrentDictionary<(string Consumer, Guid MessageId), bool> _processed = new();

    // TryAdd é atômico: só um chamador "ganha" a chave da mensagem
    public bool TryClaim(string consumer, Guid messageId) =>
        _processed.TryAdd((consumer, messageId), true);
}

public sealed class StockConsumer(InboxStore inbox, ReserveStockStep reserve)
{
    public async Task HandleAsync(SagaMessage message, OrderContext ctx, CancellationToken ct)
    {
        if (!inbox.TryClaim("estoque", message.MessageId))
            return; // duplicada: confirma a mensagem e ignora

        await reserve.ExecuteAsync(ctx, ct);
    }
}
