using System.Globalization;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.TestSupport;

// Four prompt chains on 2026-11-21 (UTC) for turn and context-bloat tests, with their span links back-filled.
// B1 (conversation conv-b, swigerb/copilot-ledger feature/ledger, pull request 42): the default agent's claude-opus-5.5
// requests c1, c2, and c3, and an explore subagent's mai-code-2 requests s1 and s2, interleaved with them; s2 starts at the
// same millisecond as c2. o1's parent was never recorded. B2 (conv-b, pull request 42): one request after B1. B3 (conv-c,
// feature/ledger): two growing requests. B0 (no conversation or Git): one request without span links.
public static class TurnSeeds
{
    public const string B0 = "b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0";
    public const string B1 = "b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1";
    public const string B2 = "b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2";
    public const string B3 = "b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3";

    public static async Task SeedAsync(SqliteLedgerStore store, string root)
    {
        JsonRateCardCalculator rates = JsonRateCardCalculator.LoadEmbedded();
        GitWorkspaceContext pullRequest = GitFixture.Feature(root) with { PullRequestNumber = 42 };
        GitWorkspaceContext feature = GitFixture.Feature(root);
        (NormalizedGenAiSpan Span, GitWorkspaceContext? Git)[] spans =
        [
            (Chat(B1, "c100000000000001", "a100000000000001", "10:00:00", "claude-opus-5.5", 10_000, cacheWrite: 9_000), pullRequest),
            (Chat(B1, "5100000000000001", "a100000000000002", "10:00:05", "mai-code-2", 3_000), pullRequest),
            (Chat(B1, "5100000000000002", "a100000000000002", "10:00:10", "mai-code-2", 5_000, cacheRead: 2_000), pullRequest),
            (Chat(B1, "c100000000000002", "a100000000000001", "10:00:10", "claude-opus-5.5", 12_000, cacheRead: 9_000, cacheWrite: 2_500), pullRequest),
            (Chat(B1, "c100000000000003", "a100000000000001", "10:00:20", "claude-opus-5.5", 11_000, cacheRead: 11_000), pullRequest),
            (Chat(B1, "0100000000000001", "f100000000000009", "10:00:30", "claude-opus-5.5", 20_000), pullRequest),
            (Chat(B2, "d200000000000001", "a200000000000001", "10:05:00", "claude-opus-5.5", 15_000, cacheRead: 10_000), pullRequest),
            (Chat(B3, "e300000000000001", "a300000000000001", "11:00:00", "claude-opus-5.5", 20_000, cacheRead: 5_000, conversation: "conv-c"), feature),
            (Chat(B3, "e300000000000002", "a300000000000001", "11:01:00", "claude-opus-5.5", 30_000, cacheRead: 5_000, conversation: "conv-c"), feature),
            (Chat(B0, "f000000000000001", null, "12:00:00", "mai-code-2", 5_000, conversation: null) with
            {
                CacheReadState = CacheCounterState.Reported,
            }, null),
        ];
        await store.RecordAsync(
            spans.Select(item => new LedgerEntry(item.Span, rates.Calculate(item.Span), item.Git)).ToArray());
        await store.RecordSpanLinksAsync(
        [
            Link(B1, "a100000000000001", null, "invoke_agent", "10:00:00"),
            Link(B1, "e100000000000001", "a100000000000001", "execute_tool", "10:00:04"),
            Link(B1, "a100000000000002", "e100000000000001", "invoke_agent", "10:00:04", "explore"),
            .. spans.Where(item => item.Span.ParentSpanId is not null).Select(item => Link(
                item.Span.TraceId,
                item.Span.SpanId,
                item.Span.ParentSpanId,
                "chat",
                item.Span.StartTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                conversation: item.Span.ConversationId)),
            Link(B2, "a200000000000001", null, "invoke_agent", "10:05:00"),
            Link(B3, "a300000000000001", null, "invoke_agent", "11:00:00", conversation: "conv-c"),
        ]);
        await store.BackfillFromAgentSpansAsync([B0, B1, B2, B3]);
    }

    private static NormalizedGenAiSpan Chat(
        string trace,
        string span,
        string? parent,
        string time,
        string model,
        long input,
        long cacheRead = 0,
        long cacheWrite = 0,
        string? conversation = "conv-b") =>
        TestSpans.Chat(
            parentSpanId: parent,
            startTime: At(time),
            requestModel: model,
            conversationId: conversation,
            inputTokens: input,
            outputTokens: model == "mai-code-2" ? 100 : 500,
            cacheReadInputTokens: cacheRead,
            cacheWriteInputTokens: cacheWrite) with
        {
            TraceId = trace,
            SpanId = span,
        };

    private static GenAiSpanLink Link(
        string trace,
        string span,
        string? parent,
        string operation,
        string time,
        string? agent = null,
        string? conversation = "conv-b") => new()
    {
        TraceId = trace,
        SpanId = span,
        ParentSpanId = parent,
        OperationName = operation,
        AgentName = agent,
        ConversationId = conversation,
        StartTime = At(time),
    };

    private static DateTimeOffset At(string time) =>
        DateTimeOffset.Parse($"2026-11-21T{time}Z", CultureInfo.InvariantCulture);
}
