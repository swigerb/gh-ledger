namespace CopilotLedger.Storage;

// The last row of a page of export rows. The next page starts after it in (start time, trace ID, span ID) order.
public sealed record ExportCursor(long StartTimeUnixMs, string TraceId, string SpanId);
