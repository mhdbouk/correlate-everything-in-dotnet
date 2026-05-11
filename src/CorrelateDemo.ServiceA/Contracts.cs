namespace CorrelateDemo.ServiceA;

public sealed class EntryMarker { }

public sealed record OrderRequest(Guid OrderId);

public sealed record OrderResult(string ServiceACorrelationId, string ServiceBCorrelationId);

public sealed record OrderPublishResult(string CorrelationId, string MessageId);

internal sealed record DownstreamPayload(string CorrelationId);
