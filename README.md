# Correlate Everything in .NET

A runnable companion to the blog post **["Correlate Everything in .NET"](https://mdbouk.com/correlate-everything-in-dotnet/)**.

It wires the [Correlate](https://github.com/skwasjer/Correlate) library into ASP.NET Core, propagates the correlation ID across an HTTP call and an Azure Service Bus message, and proves every step with tests.

## Layout

```
src/
  CorrelateDemo.Messaging/    Shared contract + ServiceBusMessage factory
  CorrelateDemo.ServiceA/     Inbound API. Calls ServiceB and publishes to Service Bus.
  CorrelateDemo.ServiceB/     Downstream API. Returns the correlation ID it observed.
  CorrelateDemo.Worker/       Azure Service Bus consumer with correlation restore.
tests/
  CorrelateDemo.Tests/        Integration and unit tests for every claim.
```

## What it demonstrates

| Concern | Where to look |
|---|---|
| Inbound middleware (`AddCorrelate` + `UseCorrelate`) | `ServiceA/Program.cs`, `ServiceB/Program.cs` |
| Reading the current ID from a service | `ICorrelationContextAccessor` injection in both APIs |
| Outbound HttpClient propagation (`CorrelateRequests`) | `ServiceA/Program.cs` |
| Stamping the ID on an Azure Service Bus envelope | `CorrelatedMessageFactory.Create` |
| Restoring the context on consume (`IAsyncCorrelationManager.CorrelateAsync`) | `OrderPlacedProcessor.OnMessageReceivedAsync` |
| Serilog enrichment via `Enrich.FromLogContext()` | All three hosts |

## Run the tests

```bash
dotnet test
```

Ten tests cover the full surface. No real Azure Service Bus is needed.

| Test class | What it proves |
|---|---|
| `InboundCorrelationTests` | The middleware echoes a supplied header back on the response, and generates one when missing. |
| `CrossServicePropagationTests` | A correlation ID set on ServiceA is observed by ServiceB after an HttpClient hop. |
| `PublishCorrelationTests` | The factory writes the current correlation ID into `ServiceBusMessage.CorrelationId`. |
| `ConsumeCorrelationTests` | `IAsyncCorrelationManager.CorrelateAsync(message.CorrelationId, ...)` restores the context, then clears it on exit. |

## Run the services end to end

### HTTP only (no broker)

The two web services need nothing external:

```bash
dotnet run --project src/CorrelateDemo.ServiceB
# in a second terminal
dotnet run --project src/CorrelateDemo.ServiceA
```

Then:

```bash
curl -i -X POST http://localhost:5001/orders \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: ride-the-id" \
  -d '{ "orderId": "11111111-1111-1111-1111-111111111111" }'
```

The response header echoes `ride-the-id`, the body returns it from both services, and every log line in both processes carries the same value.

### Full flow with the Azure Service Bus emulator

The repo ships a `docker-compose.yml` and an `emulator/Config.json` that boot the official Microsoft Service Bus emulator with an `orders` queue pre-declared. Apple Silicon users: SQL Edge runs under Rosetta via `platform: linux/amd64`, no extra setup.

```bash
docker compose up -d
# wait for the emulator log line "Emulator Service is Successfully Up!"
docker logs -f correlate-demo-sb-emulator
```

The Worker and ServiceA both read `ServiceBus:ConnectionString` from `appsettings.Development.json`, which is already wired to the emulator:

```
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true
```

In three terminals (or background shells):

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/CorrelateDemo.ServiceB --urls http://localhost:5002
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/CorrelateDemo.ServiceA --urls http://localhost:5001
DOTNET_ENVIRONMENT=Development      dotnet run --project src/CorrelateDemo.Worker
```

Publish a message through ServiceA:

```bash
curl -i -X POST http://localhost:5001/orders/publish \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: bus-trip" \
  -d '{ "orderId": "22222222-2222-2222-2222-222222222222" }'
```

You should see a line like this in the Worker output:

```
[20:41:28 INF] [bus-trip] Worker handled OrderPlaced 22222222-... under correlation bus-trip
```

Same `bus-trip` ID in ServiceA, ServiceB, and the Worker. Restored on consume from `ServiceBusMessage.CorrelationId`.

Tear down:

```bash
docker compose down
```

## Where this came from

The Azure Service Bus pattern in this demo (stamp `ServiceBusMessage.CorrelationId` on publish, wrap the consumer in `IAsyncCorrelationManager.CorrelateAsync(message.CorrelationId, ...)`) is taken from a production codebase I built. The intent here is to extract that pattern, strip it to the minimum that proves the point, and back every line with a test.

## License

MIT.
