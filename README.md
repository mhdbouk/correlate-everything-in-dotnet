# Correlate Everything in .NET

Working code for the blog post **["Correlate Everything in .NET"](https://mdbouk.com/correlate-everything-in-dotnet/)**.

It wires [Correlate](https://github.com/skwasjer/Correlate) into ASP.NET Core, propagates the correlation ID across an HTTP call and an Azure Service Bus message, and proves every step with tests.

## Run the tests

```bash
dotnet test
```

Ten passing tests. No broker required.

## Run it end to end

The repo ships a `docker-compose.yml` and `emulator/Config.json` that boot the official Azure Service Bus emulator with an `orders` queue pre-declared. On Apple Silicon, SQL Edge runs under Rosetta via `platform: linux/amd64`. No extra setup.

```bash
docker compose up -d
# wait for: "Emulator Service is Successfully Up!"
docker logs -f correlate-demo-sb-emulator
```

In three terminals:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/CorrelateDemo.ServiceB --urls http://localhost:5002
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/CorrelateDemo.ServiceA --urls http://localhost:5001
DOTNET_ENVIRONMENT=Development      dotnet run --project src/CorrelateDemo.Worker
```

Send a correlated request:

```bash
curl -i -X POST http://localhost:5001/orders/publish \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: ride-the-id" \
  -d '{ "orderId": "22222222-2222-2222-2222-222222222222" }'
```

Tear down: `docker compose down`.

## Proof

A single correlation ID supplied at ServiceA flows through every hop. Captured from a real run:

| Hop | Log line |
|---|---|
| Caller sets header | `X-Correlation-ID: ride-the-id-1778521287` |
| ServiceA receives | `[ride-the-id-1778521287] Service A handling order 11111111-...` |
| ServiceA outbound HTTP | `[ride-the-id-1778521287] Sending HTTP request GET http://localhost:5002/downstream` |
| ServiceB receives | `[ride-the-id-1778521287] Service B handled downstream call` |
| ServiceA publishes to bus | `[ride-the-id-1778521287] Service A published OrderPlaced 22222222-... as 2cafa8c9-...` |
| Worker consumes | `[ride-the-id-1778521287] Worker handled OrderPlaced 22222222-... under correlation ride-the-id-1778521287` |

The bracketed prefix is Serilog enrichment via `Enrich.FromLogContext()`. The trailing value in the Worker line is `ICorrelationContextAccessor.CorrelationContext.CorrelationId` read inside the handler, after `IAsyncCorrelationManager.CorrelateAsync(message.CorrelationId, ...)` restored the context from the envelope. Both resolve to the same string.

## Where things live

```
src/
  CorrelateDemo.Messaging/    Shared contract + ServiceBusMessage factory
  CorrelateDemo.ServiceA/     Inbound API. Calls ServiceB and publishes to Service Bus.
  CorrelateDemo.ServiceB/     Downstream API. Returns the correlation ID it observed.
  CorrelateDemo.Worker/       Azure Service Bus consumer with correlation restore.
tests/
  CorrelateDemo.Tests/        Integration and unit tests.
```

| Concern | File |
|---|---|
| Middleware (`AddCorrelate` + `UseCorrelate`) | `ServiceA/Program.cs`, `ServiceB/Program.cs` |
| Outbound HttpClient propagation (`CorrelateRequests`) | `ServiceA/Program.cs` |
| Stamp the ID on the bus envelope | `Messaging/CorrelatedMessageFactory.cs` |
| Restore the context on consume | `Worker/OrderPlacedProcessor.cs` |
| Serilog enrichment | All three hosts |

## License

MIT.
