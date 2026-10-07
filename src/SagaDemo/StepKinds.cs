namespace SagaDemo;

// EN: Order matters: compensable, then the pivot, then retriable.
// PT: A ordem importa: compensáveis, o pivô, depois retentáveis.
public enum StepKind { Compensable, Pivot, Retriable }

public sealed record StepInfo(string Name, StepKind Kind);

public static class SagaPlan
{
    // EN: Valid = kinds never go backwards and there is one pivot
    // at most (after it, there is no way back).
    // PT: Válido = os tipos nunca voltam e há no máximo um pivô
    // (depois dele, não há volta).
    public static bool IsValid(IReadOnlyList<StepInfo> steps) =>
        steps.Count(s => s.Kind == StepKind.Pivot) <= 1
        && steps.Zip(steps.Skip(1))
            .All(p => p.First.Kind <= p.Second.Kind);
}
