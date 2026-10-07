[English](README.md) | Português

[![CI](https://github.com/ceseidl/saga-pattern-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/ceseidl/saga-pattern-dotnet/actions/workflows/ci.yml) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

# Saga Pattern em .NET

> **Início rápido**

```bash
cd src/SagaDemo
dotnet run
```

Precisa só do SDK do .NET 10. Detalhes em [Como rodar](#como-rodar).

Exemplo didático do **Saga Pattern** com orquestração e compensação em C#/.NET 10, sem dependências externas. Acompanha o artigo *Saga Pattern em .NET: Transações Distribuídas entre Microsserviços*, da série **Arquitetura .NET**.

> Código de estudo. Os "serviços" são simulados em memória e o estado da saga **não é persistido**. Veja [Limitações](#limitações).

## O problema

No monólito, "criar pedido" cabe em uma transação: reservar estoque, cobrar e gerar a entrega, com `COMMIT` ou `ROLLBACK`. Ao dividir em microsserviços, cada serviço tem seu próprio banco e essa transação única deixa de existir. Se o estoque foi reservado e o pagamento foi recusado, alguém precisa desfazer a reserva.

O Saga Pattern resolve isso **sem transação distribuída**: quebra a operação em **transações locais** e, se uma falhar, executa **transações compensatórias** nos passos já concluídos.

## O que o exemplo mostra

- **Orquestração**: um orquestrador executa os passos em ordem e, na primeira falha, compensa os passos concluídos **em ordem inversa**.
- **Compensação com retentativa**: cada compensação tenta até 3 vezes. Se esgotar, o resultado é `CompensationFailed` (alerta e intervenção manual).
- **Passos idempotentes**: executar ou compensar duas vezes tem o mesmo efeito de uma (chave: `OrderId`).
- **Consumidor idempotente (Inbox)**: uma mensagem entregue duas vezes é processada uma só.

## Estrutura

```
src/SagaDemo/
├── Saga.cs      # contrato do passo (ISagaStep), contexto e resultado
├── OrderSagaOrchestrator.cs  # execução e compensação em ordem inversa
├── StepKinds.cs # passos compensáveis, pivô e retentáveis; checagem do plano
├── Messages.cs  # comando (ReserveStock) e evento (StockReserved)
├── Steps.cs     # ReservarEstoque, AutorizarPagamento, CriarEntrega
├── Inbox.cs     # InboxStore e StockConsumer (deduplicação por mensagem)
├── Program.cs   # fluxo feliz, fluxo de falha e mensagem duplicada
└── SagaDemo.csproj
```

## Como rodar

Requisito: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/ceseidl/saga-pattern-dotnet.git
cd saga-pattern-dotnet/src/SagaDemo
dotnet run
```

Saída resumida:

```
Plan / Plano: True
Pivot first / Pivô primeiro: False
Command / Comando: ReserveStock -> StockReserved
Happy path / Fluxo feliz -> Completed
... AutorizarPagamento failed; starting compensation / falhou; iniciando compensação
... compensated / compensado ReservarEstoque
Failure path / Fluxo de falha -> Compensated
Inbox: second delivery ignored / segunda entrega ignorada = True
```

As mensagens são bilíngues ("English / Português"). O projeto usa o SDK `Microsoft.NET.Sdk.Web` apenas para ter `ILogger` e injeção de dependência no *shared framework* sem baixar pacotes. Em um projeto de console puro, adicione `Microsoft.Extensions.Hosting`.

## Como funciona

1. O orquestrador percorre os passos e empilha cada um que **concluiu**.
2. Se um passo lança exceção, o orquestrador desempilha e chama `CompensateAsync` em cada passo concluído (o que falhou não entra na pilha).
3. Cada compensação é repetida até 3 vezes, com espera crescente.
4. O resultado é `Completed`, `Compensated` ou `CompensationFailed`.

```csharp
foreach (var step in steps)
{
    try
    {
        await step.ExecuteAsync(ctx, ct);
        completed.Push(step);                       // só o concluído entra na pilha
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        return await CompensateAsync(ctx, completed, ct);
    }
}
```

### Conceitos

- **Compensável**: pode ser desfeito por uma compensação.
- **Pivô**: o ponto sem volta. Depois dele, os passos seguintes precisam concluir.
- **Retentável**: vem depois do pivô, é idempotente e pode ser repetido até dar certo.
- **Coreografia vs orquestração**: sem controlador central (eventos entre serviços) ou com um orquestrador que dirige o fluxo. Este exemplo usa orquestração.

## Limitações

Este exemplo existe para mostrar a mecânica. Em produção, falta:

- **Persistir o estado da saga** a cada transição (hoje, se o processo cair no meio, o estado se perde). Bibliotecas como MassTransit, NServiceBus e Wolverine oferecem isso.
- **Transactional Outbox**: gravar o estado e a mensagem na mesma transação local e publicar por um processo separado.
- **Inbox em banco**: o `InboxStore` é em memória. Em produção, use uma tabela com restrição de unicidade, gravada na mesma transação do efeito de negócio.
- **Timeouts por passo**, **correlação** (logs e traces) e **alertas** para `CompensationFailed`.
- **Isolamento**: sagas concorrentes enxergam dados intermediários. Contramedidas: *semantic lock*, atualizações comutativas, reler valores, visão pessimista.
- Decidir explicitamente o que fazer com `OperationCanceledException` (aqui não dispara compensação).

## Quando usar e quando evitar

**Use** quando uma operação passa por vários serviços, cada um com seu banco, e existe uma compensação de negócio por passo.

**Evite** quando tudo roda em um serviço e um banco, o fluxo tem poucos passos, exige consistência forte e imediata, há ações que não podem ser desfeitas ou o sistema é um CRUD simples.

## Referências

- Microsoft Learn: [Saga distributed transactions pattern](https://learn.microsoft.com/azure/architecture/patterns/saga)
- Microsoft Learn: [Compensating Transaction pattern](https://learn.microsoft.com/azure/architecture/patterns/compensating-transaction)
- Microsoft Learn: [Idempotent Consumer pattern](https://learn.microsoft.com/azure/architecture/patterns/idempotent-consumer)
- Microsoft Learn: [Transactional Outbox pattern with Azure Cosmos DB](https://learn.microsoft.com/azure/architecture/databases/guide/transactional-out-box-cosmos)
- microservices.io: [Pattern: Saga](https://microservices.io/patterns/data/saga.html)
- MassTransit: [Saga State Machine](https://masstransit.io/documentation/patterns/saga/state-machine)

## Licença

[MIT](LICENSE). Autor: Carlos Eduardo Seidl.
