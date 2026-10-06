using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SagaDemo;

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Information));
var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<OrderSagaOrchestrator>>();

async Task<SagaOutcome> Run(bool paymentFails)
{
    ISagaStep[] steps =
    [
        new ReserveStockStep(),
        new AuthorizePaymentStep(failOnExecute: paymentFails),
        new CreateShipmentStep()
    ];
    var orchestrator = new OrderSagaOrchestrator(steps, logger);
    return await orchestrator.RunAsync(new OrderContext(Guid.NewGuid(), "cli-42", 199.90m));
}

Console.WriteLine($"Happy path / Fluxo feliz -> {await Run(paymentFails: false)}");
Console.WriteLine($"Failure path / Fluxo de falha -> {await Run(paymentFails: true)}");

var inbox = new InboxStore();
var consumer = new StockConsumer(inbox, new ReserveStockStep());
var msg = new SagaMessage(Guid.NewGuid(), Guid.NewGuid(), "ReservarEstoque");
var octx = new OrderContext(Guid.NewGuid(), "cli-42", 10m);
await consumer.HandleAsync(msg, octx, CancellationToken.None);
// EN: Redelivery of the same message.
// PT: Reentrega da mesma mensagem.
await consumer.HandleAsync(msg, octx, CancellationToken.None);
Console.WriteLine($"Inbox: second delivery ignored / segunda entrega ignorada = {!inbox.TryClaim("estoque", msg.MessageId)}");
