namespace SagaDemo;

// EN: Command: an order to ONE participant (orchestration).
// PT: Comando: uma ordem para UM participante (orquestração).
public sealed record ReserveStock(Guid OrderId, decimal Total);

// EN: Event: a fact that already happened; anyone may react
// to it (choreography).
// PT: Evento: um fato que já aconteceu; qualquer um pode reagir
// a ele (coreografia).
public sealed record StockReserved(Guid OrderId);

public static class StockHandler
{
    // EN: The participant receives the command and answers
    // with an event.
    // PT: O participante recebe o comando e responde
    // com um evento.
    public static StockReserved Handle(ReserveStock command) =>
        new(command.OrderId);
}
