using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Correlate;
using CorrelateDemo.Messaging;

namespace CorrelateDemo.Worker;

public sealed class OrderPlacedProcessor : IHostedService, IAsyncDisposable
{
    private const string QueueName = "orders";

    private readonly ICorrelationContextAccessor _accessor;
    private readonly IAsyncCorrelationManager _correlationManager;
    private readonly ILogger<OrderPlacedProcessor> _logger;
    private readonly ServiceBusProcessor _processor;

    public OrderPlacedProcessor
    (
        ServiceBusClient client,
        IAsyncCorrelationManager correlationManager,
        ICorrelationContextAccessor accessor,
        ILogger<OrderPlacedProcessor> logger
    )
    {
        _correlationManager = correlationManager;
        _accessor = accessor;
        _logger = logger;
        _processor = client.CreateProcessor
        (
            QueueName,
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 1,
                AutoCompleteMessages = false
            }
        );
        _processor.ProcessMessageAsync += OnMessageReceivedAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting OrderPlaced processor on queue {QueueName}", QueueName);
        return _processor.StartProcessingAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping OrderPlaced processor");
        return _processor.StopProcessingAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _processor.DisposeAsync();

    private Task OnMessageReceivedAsync(ProcessMessageEventArgs args)
    {
        return _correlationManager.CorrelateAsync
        (
            args.Message.CorrelationId,
            async () => await HandleAsync(args)
        );
    }

    private async Task HandleAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<OrderPlaced>(args.Message.Body.ToString());
            var id = _accessor.CorrelationContext?.CorrelationId ?? "none";

            _logger.LogInformation(
                "Worker handled OrderPlaced {OrderId} under correlation {CorrelationId}",
                payload?.OrderId,
                id);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message {MessageId}", args.Message.MessageId);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "ServiceBus processor error on {EntityPath}", args.EntityPath);
        return Task.CompletedTask;
    }
}
