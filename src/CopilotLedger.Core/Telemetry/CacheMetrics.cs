namespace CopilotLedger.Core.Telemetry;

public static class CacheMetrics
{
    // True when the span reported its prompt-cache usage, that is, it carried at least one cache counter. The OTel GenAI
    // conventions leave out a cache counter whose value is zero, so the other counter on such a span is a reported 0.
    // A span with neither counter is unknown: its provider may not report cache usage at all.
    public static bool IsReported(CacheCounterState cacheRead, CacheCounterState cacheWrite) =>
        cacheRead == CacheCounterState.Reported || cacheWrite == CacheCounterState.Reported;

    // cacheReadInputTokens / inputTokens, computed exactly in decimal; null when inputTokens is 0. Never rounds.
    public static decimal? HitRatio(long cacheReadInputTokens, long inputTokens) =>
        inputTokens == 0 ? null : (decimal)cacheReadInputTokens / inputTokens;
}
