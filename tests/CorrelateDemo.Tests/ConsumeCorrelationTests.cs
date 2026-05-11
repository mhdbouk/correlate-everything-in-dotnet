using Azure.Messaging.ServiceBus;
using Correlate;
using Correlate.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CorrelateDemo.Tests;

public class ConsumeCorrelationTests
{
    [Fact]
    public async Task Should_restore_correlation_context_from_message_envelope()
    {
        var services = new ServiceCollection().AddCorrelate().BuildServiceProvider();
        var manager = services.GetRequiredService<IAsyncCorrelationManager>();
        var accessor = services.GetRequiredService<ICorrelationContextAccessor>();

        var received = ServiceBusModelFactory.ServiceBusReceivedMessage(correlationId: "msg-xyz");

        string? observed = null;
        await manager.CorrelateAsync(received.CorrelationId, () =>
        {
            observed = accessor.CorrelationContext?.CorrelationId;
            return Task.CompletedTask;
        });

        observed.ShouldBe("msg-xyz");
    }

    [Fact]
    public async Task Should_generate_id_when_envelope_has_none()
    {
        var services = new ServiceCollection().AddCorrelate().BuildServiceProvider();
        var manager = services.GetRequiredService<IAsyncCorrelationManager>();
        var accessor = services.GetRequiredService<ICorrelationContextAccessor>();

        var received = ServiceBusModelFactory.ServiceBusReceivedMessage();

        string? observed = null;
        await manager.CorrelateAsync(received.CorrelationId, () =>
        {
            observed = accessor.CorrelationContext?.CorrelationId;
            return Task.CompletedTask;
        });

        observed.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_clear_context_after_delegate_completes()
    {
        var services = new ServiceCollection().AddCorrelate().BuildServiceProvider();
        var manager = services.GetRequiredService<IAsyncCorrelationManager>();
        var accessor = services.GetRequiredService<ICorrelationContextAccessor>();

        await manager.CorrelateAsync("scoped-id", () => Task.CompletedTask);

        accessor.CorrelationContext.ShouldBeNull();
    }
}
