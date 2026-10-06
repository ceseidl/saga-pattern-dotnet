namespace SagaDemo;

public sealed record OrderContext(Guid OrderId, string CustomerId, decimal Total);

public interface ISagaStep
{
    string Name { get; }
    Task ExecuteAsync(OrderContext ctx, CancellationToken ct);
    Task CompensateAsync(OrderContext ctx, CancellationToken ct);
}

public enum SagaOutcome { Completed, Compensated, CompensationFailed }

public sealed class OrderSagaOrchestrator(
    IReadOnlyList<ISagaStep> steps,
    ILogger<OrderSagaOrchestrator> logger)
{
    private const int MaxCompensationAttempts = 3;

    public async Task<SagaOutcome> RunAsync(OrderContext ctx, CancellationToken ct = default)
    {
        var completed = new Stack<ISagaStep>();

        foreach (var step in steps)
        {
            try
            {
                logger.LogInformation("[{Order}] executing / executando {Step}", ctx.OrderId, step.Name);
                await step.ExecuteAsync(ctx, ct);
                completed.Push(step);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[{Order}] {Step} failed; starting compensation / falhou; iniciando compensação", ctx.OrderId, step.Name);
                return await CompensateAsync(ctx, completed, ct);
            }
        }

        return SagaOutcome.Completed;
    }

    private async Task<SagaOutcome> CompensateAsync(
        OrderContext ctx, Stack<ISagaStep> completed, CancellationToken ct)
    {
        var allOk = true;

        // EN: Reverse order: the last completed step is the first to be undone.
        // PT: Ordem inversa: o último passo concluído é o primeiro a ser desfeito.
        while (completed.TryPop(out var step))
        {
            var ok = false;
            for (var attempt = 1; attempt <= MaxCompensationAttempts && !ok; attempt++)
            {
                try
                {
                    // EN: Must be idempotent.
                    // PT: Precisa ser idempotente.
                    await step.CompensateAsync(ctx, ct);
                    ok = true;
                    logger.LogInformation("[{Order}] compensated / compensado {Step}", ctx.OrderId, step.Name);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "[{Order}] compensation of {Step} failed / compensação falhou ({Attempt}/{Max})",
                        ctx.OrderId, step.Name, attempt, MaxCompensationAttempts);
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
                }
            }

            if (!ok)
            {
                allOk = false;
                logger.LogError("[{Order}] compensation of {Step} exhausted retries: manual intervention / compensação esgotou tentativas: intervenção manual",
                    ctx.OrderId, step.Name);
            }
        }

        return allOk ? SagaOutcome.Compensated : SagaOutcome.CompensationFailed;
    }
}
