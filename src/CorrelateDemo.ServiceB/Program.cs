using Correlate;
using Correlate.AspNetCore;
using Correlate.DependencyInjection;
using CorrelateDemo.ServiceB;
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

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseCorrelate();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet
(
    "/downstream",
    (ICorrelationContextAccessor accessor, ILogger<Program> logger) =>
    {
        var id = accessor.CorrelationContext?.CorrelationId ?? "none";
        logger.LogInformation("Service B handled downstream call");
        return Results.Ok(new DownstreamPayload(id));
    }
);

await app.RunAsync();
