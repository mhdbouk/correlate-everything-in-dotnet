using Azure.Messaging.ServiceBus;
using Correlate.DependencyInjection;
using CorrelateDemo.Worker;
using Serilog;

var hostBuilder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

hostBuilder.Logging.ClearProviders();
hostBuilder.Logging.AddSerilog(Log.Logger);

hostBuilder.Services.AddCorrelate();

hostBuilder.Services.AddSingleton(_ =>
{
    var connectionString = hostBuilder.Configuration["ServiceBus:ConnectionString"]
        ?? throw new InvalidOperationException(
            "ServiceBus:ConnectionString is required. Set it in appsettings.json, user secrets, or environment.");
    return new ServiceBusClient(connectionString);
});

hostBuilder.Services.AddHostedService<OrderPlacedProcessor>();

await hostBuilder.Build().RunAsync();
