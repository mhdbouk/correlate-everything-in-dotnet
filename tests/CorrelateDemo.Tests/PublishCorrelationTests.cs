using Correlate;
using Correlate.DependencyInjection;
using CorrelateDemo.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CorrelateDemo.Tests;

public class PublishCorrelationTests
{
    [Fact]
    public async Task Should_stamp_envelope_with_current_correlation_id()
    {
        var services = new ServiceCollection().AddCorrelate().BuildServiceProvider();
        var accessor = services.GetRequiredService<ICorrelationContextAccessor>();
        var manager = services.GetRequiredService<IAsyncCorrelationManager>();

        string? capturedCorrelationId = null;
        await manager.CorrelateAsync("abc-123", () =>
        {
            var message = CorrelatedMessageFactory.Create(new OrderPlaced(Guid.NewGuid()), accessor);
            capturedCorrelationId = message.CorrelationId;
            return Task.CompletedTask;
        });

        capturedCorrelationId.ShouldBe("abc-123");
    }

    [Fact]
    public void Should_generate_a_correlation_id_when_no_ambient_context()
    {
        var services = new ServiceCollection().AddCorrelate().BuildServiceProvider();
        var accessor = services.GetRequiredService<ICorrelationContextAccessor>();

        var message = CorrelatedMessageFactory.Create(new OrderPlaced(Guid.NewGuid()), accessor);

        message.CorrelationId.ShouldNotBeNullOrEmpty();
        Guid.TryParse(message.CorrelationId, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_set_subject_to_message_type_name()
    {
        var services = new ServiceCollection().AddCorrelate().BuildServiceProvider();
        var accessor = services.GetRequiredService<ICorrelationContextAccessor>();
        var manager = services.GetRequiredService<IAsyncCorrelationManager>();

        string? subject = null;
        await manager.CorrelateAsync("any", () =>
        {
            var message = CorrelatedMessageFactory.Create(new OrderPlaced(Guid.NewGuid()), accessor);
            subject = message.Subject;
            return Task.CompletedTask;
        });

        subject.ShouldBe(nameof(OrderPlaced));
    }
}
