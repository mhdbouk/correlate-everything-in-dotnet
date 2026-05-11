using System.Net.Http.Json;
using Azure.Messaging.ServiceBus;
using Correlate;
using Correlate.AspNetCore;
using Correlate.DependencyInjection;
using CorrelateDemo.Messaging;
using CorrelateDemo.ServiceA;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddCorrelate(options =>
{
    options.RequestHeaders = ["X-Correlation-ID"];
});

builder.Services
    .AddHttpClient
    (
        "downstream",
        client =>
        {
            var baseUrl = builder.Configuration["Downstream:BaseUrl"] ?? "http://localhost:5002";
            client.BaseAddress = new Uri(baseUrl);
        }
    )
    .CorrelateRequests("X-Correlation-ID");

builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration["ServiceBus:ConnectionString"];
    return string.IsNullOrWhiteSpace(connectionString)
        ? null!
        : new ServiceBusClient(connectionString);
});

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseCorrelate();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost
(
    "/orders",
    async
    (
        OrderRequest request,
        IHttpClientFactory clientFactory,
        ICorrelationContextAccessor accessor,
        ILogger<Program> logger,
        CancellationToken ct
    ) =>
    {
        var localId = accessor.CorrelationContext?.CorrelationId ?? "none";
        logger.LogInformation("Service A handling order {OrderId}", request.OrderId);

        var downstream = clientFactory.CreateClient("downstream");
        var downstreamResponse = await downstream.GetAsync("/downstream", ct);
        downstreamResponse.EnsureSuccessStatusCode();
        var downstreamBody = await downstreamResponse.Content.ReadFromJsonAsync<DownstreamPayload>(cancellationToken: ct);

        return Results.Ok(new OrderResult(localId, downstreamBody?.CorrelationId ?? "none"));
    }
);

app.MapPost
(
    "/orders/publish",
    async
    (
        OrderRequest request,
        ServiceBusClient? client,
        ICorrelationContextAccessor accessor,
        ILogger<Program> logger,
        CancellationToken ct
    ) =>
    {
        if (client is null)
        {
            return Results.Problem(
                title: "Azure Service Bus not configured",
                detail: "Set ServiceBus:ConnectionString in configuration to publish messages.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await using var sender = client.CreateSender("orders");
        var message = CorrelatedMessageFactory.Create(new OrderPlaced(request.OrderId), accessor);
        await sender.SendMessageAsync(message, ct);

        logger.LogInformation(
            "Service A published OrderPlaced {OrderId} as {MessageId}",
            request.OrderId,
            message.MessageId);

        return Results.Accepted(value: new OrderPublishResult(message.CorrelationId, message.MessageId));
    }
);

await app.RunAsync();
