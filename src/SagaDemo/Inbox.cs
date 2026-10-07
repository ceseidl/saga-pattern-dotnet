using System.Collections.Concurrent;

namespace SagaDemo;

using MessageKey = (string Consumer, Guid MessageId);

public sealed record SagaMessage(
    Guid MessageId, Guid SagaId, string Type);

public sealed class InboxStore
{
    private readonly ConcurrentDictionary<MessageKey, bool>
        _processed = new();

    // EN: TryAdd is atomic: only one caller wins the message key.
    // PT: TryAdd é atômico: só um chamador "ganha" a chave.
    public bool TryClaim(string consumer, Guid messageId) =>
        _processed.TryAdd((consumer, messageId), true);
}

public sealed class StockConsumer(
    InboxStore inbox, ReserveStockStep reserve)
{
    public async Task HandleAsync(
        SagaMessage message,
        OrderContext ctx,
        CancellationToken ct)
    {
        if (!inbox.TryClaim("estoque", message.MessageId))
            // EN: Duplicate: acknowledge and ignore it.
            // PT: Duplicada: confirma a mensagem e ignora.
            return;

        await reserve.ExecuteAsync(ctx, ct);
    }
}
