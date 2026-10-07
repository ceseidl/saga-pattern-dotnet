namespace SagaDemo;

public sealed record OrderContext(
    Guid OrderId, string CustomerId, decimal Total);

public interface ISagaStep
{
    string Name { get; }

    Task ExecuteAsync(OrderContext ctx, CancellationToken ct);

    Task CompensateAsync(OrderContext ctx, CancellationToken ct);
}

public enum SagaOutcome
{
    Completed,
    Compensated,
    CompensationFailed
}
