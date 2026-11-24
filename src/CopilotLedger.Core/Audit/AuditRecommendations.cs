namespace CopilotLedger.Core.Audit;

// The fixed recommendation of each finding's RecommendationKey. docs/cli.md's table repeats them (AuditDocsTests).
public static class AuditRecommendations
{
    public const string ContextBloatZeroOutput = "context_bloat_zero_output";

    private static readonly (string Key, string Text)[] Sentences =
    [
        (AuditRuleIds.TurnVelocity,
            "Check whether the agent was looping: give it a narrower task or a clear stopping point, and let the circuit breaker's credit velocity rule stop runaway bursts."),
        (AuditRuleIds.ToolLoop,
            "Check why the agent called the same tool again and again with the same outcome, and give it what it needed up front or a clear stopping point."),
        (AuditRuleIds.FailedToolRetry,
            "Fix the cause of the failing command, such as a missing dependency or a wrong path, before the agent retries it, because every retry costs another model request."),
        (AuditRuleIds.ContextBloat,
            "Send less context with each request: attach smaller files and tool results, trim long instructions, or start a new session for a new task."),
        (ContextBloatZeroOutput,
            "Check why the request returned no output, such as a cancelled or failed request: its input was still sent and billed."),
        (AuditRuleIds.ContextGrowth,
            "Start a new session or compact the conversation when the context keeps growing, and keep tool results short."),
        (AuditRuleIds.CacheMissStreak,
            "Keep the start of each prompt stable, such as instructions, tool definitions, and earlier messages, so the prompt cache can serve it, and avoid switching models in the middle of a task."),
        (AuditRuleIds.PremiumModelTrivialTurn,
            "Consider a cheaper model or a local model for these short requests without tool calls; they're only candidates, because a short answer can still need a strong model."),
        (AuditRuleIds.VelocityNearBreaker,
            "Check whether this burst of spend was expected; if it wasn't, lower the circuit breaker's credit velocity with gh ledger breaker config so it trips next time."),
    ];

    // Every key: the rule IDs in AuditRuleIds.All order, with context_bloat_zero_output after context_bloat.
    public static IReadOnlyList<string> Keys { get; } = [.. Sentences.Select(sentence => sentence.Key)];

    // The key's fixed sentence. Throws ArgumentOutOfRangeException for any other key.
    public static string Text(string key)
    {
        foreach ((string candidate, string text) in Sentences)
        {
            if (string.Equals(candidate, key, StringComparison.Ordinal))
            {
                return text;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown recommendation key.");
    }
}
