namespace CopilotLedger.Core.Telemetry;

// Whether a span carried one prompt-cache counter (gen_ai.usage.cache_read.input_tokens, or cache_write.input_tokens with
// its legacy cache_creation.input_tokens). The counter's value is stored either way; an absent counter is stored as 0.
public enum CacheCounterState
{
    Absent,          // the span didn't carry the attribute
    Reported,        // the span carried the attribute
    LegacyUnknown,   // the span was recorded before the ledger stored counter states
}
