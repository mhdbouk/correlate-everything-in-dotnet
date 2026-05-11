namespace CorrelateDemo.ServiceB;

public sealed class EntryMarker { }

public sealed record DownstreamPayload(string CorrelationId);
