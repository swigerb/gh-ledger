namespace CopilotLedger.Otlp.Ingestion;

public sealed record IngestResult
{
    public required int ReceivedSpans { get; init; }
    public required int RejectedSpans { get; init; }     // normalizer rejections plus cost rejections
    public string? FirstRejectionReason { get; init; }   // null when RejectedSpans is 0
    public required int InferenceSpans { get; init; }   // entries passed to RecordAsync
    public required int RecordedSpans { get; init; }    // new ledger rows; duplicates excluded
    public int RejectedReportedCharges { get; init; }    // inference spans recorded at the rate card because their reported charge was rejected
}
