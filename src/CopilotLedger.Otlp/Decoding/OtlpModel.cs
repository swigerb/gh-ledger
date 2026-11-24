namespace CopilotLedger.Otlp.Decoding;

public enum OtlpValueKind
{
    Empty,          // no value field
    String,
    Bool,
    Int,
    Double,         // recognized; only an exact non-negative integer value is kept (DoubleAsNanoAiu)
    Bytes,          // recognized; the value isn't kept
    Array,          // recognized; the value isn't kept
    KeyValueList,   // recognized; the value isn't kept
}

public sealed record OtlpAnyValue
{
    public static OtlpAnyValue Empty { get; } = new() { Kind = OtlpValueKind.Empty };
    public required OtlpValueKind Kind { get; init; }
    public string? StringValue { get; init; }   // set when Kind is String
    public bool BoolValue { get; init; }        // set when Kind is Bool
    public long IntValue { get; init; }         // set when Kind is Int
    public long? DoubleAsNanoAiu { get; init; } // set when Kind is Double and the value is an integer from 0 to 2^53 (NanoAiu)
}

public sealed record OtlpAttribute(string Key, OtlpAnyValue Value);

public sealed record OtlpSpan
{
    public string? TraceId { get; init; }                  // 32 lowercase hex chars; null when missing, the wrong length, not hex, or all zeros
    public string? SpanId { get; init; }                   // 16 lowercase hex chars; null when missing, the wrong length, not hex, or all zeros
    public string? ParentSpanId { get; init; }             // 16 lowercase hex chars; null when empty, missing, invalid, or all zeros
    public string Name { get; init; } = "";
    public ulong StartTimeUnixNano { get; init; }          // 0 when missing or invalid
    public ulong EndTimeUnixNano { get; init; }            // 0 when missing or invalid
    public int StatusCode { get; init; }                   // Status.code: 1 ok, 2 error; 0 when unset, missing, or any other value
    public IReadOnlyList<OtlpAttribute> Attributes { get; init; } = [];
}

public sealed record OtlpResourceSpans(
    IReadOnlyList<OtlpAttribute> ResourceAttributes,
    IReadOnlyList<OtlpSpan> Spans);                        // the spans of every ScopeSpans entry, in order

public sealed record OtlpTraceRequest(IReadOnlyList<OtlpResourceSpans> ResourceSpans)
{
    public int SpanCount => ResourceSpans.Sum(r => r.Spans.Count);
}
