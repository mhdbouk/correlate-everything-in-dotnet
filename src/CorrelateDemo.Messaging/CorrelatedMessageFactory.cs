using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Correlate;

namespace CorrelateDemo.Messaging;

public static class CorrelatedMessageFactory
{
    public static ServiceBusMessage Create<T>
    (
        T payload,
        ICorrelationContextAccessor accessor,
        JsonSerializerOptions? serializerOptions = null
    )
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(accessor);

        var body = JsonSerializer.Serialize(payload, serializerOptions);

        return new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            Subject = typeof(T).Name,
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = accessor.CorrelationContext?.CorrelationId ?? Guid.NewGuid().ToString()
        };
    }
}
