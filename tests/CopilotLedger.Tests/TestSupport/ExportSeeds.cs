using System.Globalization;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.TestSupport;

// The ledger of gh ledger export's tests. Times are UTC; GeneratedAt is 16:30 at ManualTimeProvider.UtcMinus5.
// A1, A2  trace A, conv-a, swigerb/copilot-ledger feature/ledger, pull request 42: claude-opus-5.5 at 11-21 14:00 with
//         40,000 reported cache reads, then an explore subagent's mai-code-2 at 14:01 without cache counters.
// B1      trace B, conv-b, feature/ledger: Foundry BYOK gpt-6.1-sol at 15:00, priced with RateFixtures.ByokTestRatesJson.
// C1, C2  trace C, conv-c, main: a local qwen3.8-27b request at 16:00 and an unpriced gpt-unknown one at 16:05.
// D1      trace D, conv-d: claude-opus-5.5 at 17:00 in LocalRoot(root), a repository without a remote, on main.
// I1      an imported Copilot record (conv-i, 11-20 12:00) that reported its one tool call, bash, which exited 0.
// I2, J1  an imported record (conv-j, 11-20 12:00) that reported its one tool call, bash, which exited 1, superseded by J1, its
//         live twin (trace J).
// SupersedeUnmatchedAsync adds U: an imported record that a live span of its session replaces without matching it.
// Links: execute_tool T1 and T2 (trace A, bash, exit 0 at 14:00:30, exit 1 at 14:00:40), T3 (trace B, view, status 2 at
// 15:00:10), T4 (trace E, which has no request, at 15:30), and T5, A's invoke_agent link. Breaker trips: credit_velocity on
// A at 14:02 (open), retry_churn on C at 16:06 (reset), credit_velocity on E (open). Budgets: 50 AI credits on
// swigerb/copilot-ledger feature/ledger and 10 on contoso/api main.
public static class ExportSeeds
{
    public const string A = "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1";
    public const string B = "b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1";
    public const string C = "c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1";
    public const string D = "d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1";
    public const string E = "e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1";
    public const string J = "f1f1f1f1f1f1f1f1f1f1f1f1f1f1f1f1";
    public const string U = "7a7a7a7a7a7a7a7a7a7a7a7a7a7a7a7a";
    public const string ServiceRequestId = "22222222-2222-4222-8222-222222222222";

    public static readonly DateTimeOffset GeneratedAt = new(2026, 11, 21, 21, 30, 0, TimeSpan.Zero);

    // D1's repository.
    public static string LocalRoot(string root) => Path.Combine(root, "private-7c1d", "tools");

    // A ledger at <root>/data/ledger.db, where CliHarness(root) looks, whose clock stays at GeneratedAt; seeded.
    public static async Task<SqliteLedgerStore> CreateAsync(string root)
    {
        SqliteLedgerStore store = new(
            Path.Combine(root, "data", "ledger.db"),
            new ManualTimeProvider(GeneratedAt));
        await store.InitializeAsync();
        await SeedAsync(store, root);
        return store;
    }

    public static async Task SeedAsync(SqliteLedgerStore store, string root)
    {
        JsonRateCardCalculator embedded = JsonRateCardCalculator.LoadEmbedded();
        JsonRateCardCalculator byok = JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson);
        GitWorkspaceContext feature = GitFixture.Feature(root);
        GitWorkspaceContext main = feature with { BranchName = "main" };
        GitWorkspaceContext local = new(LocalRoot(root), Path.Combine(LocalRoot(root), ".git"), "main", GitFixture.Sha2, null, null);

        ImportedLedgerEntry i1 = ImportEntries.Copilot("k1", "conv-i", null);
        ImportedLedgerEntry i2 = ImportEntries.Copilot("k2", "conv-j", ServiceRequestId);
        await store.RecordImportedAsync(
        [
            i1 with { ToolCallCount = 1, ToolLinks = [ImportEntries.ToolLink(i1, "call-1", 0)] },
            i2 with { ToolCallCount = 1, ToolLinks = [ImportEntries.ToolLink(i2, "call-2", 1)] },
        ],
            null);

        LedgerEntry j1 = ImportEntries.Live("conv-j", ServiceRequestId);
        (NormalizedGenAiSpan Span, JsonRateCardCalculator Rates, GitWorkspaceContext Git)[] spans =
        [
            (Chat(A, 1, "14:00:00", "claude-opus-5.5", "conv-a", 100_000, 10_000, cacheRead: 40_000), embedded, feature with { PullRequestNumber = 42 }),
            (Chat(A, 2, "14:01:00", "mai-code-2", "conv-a", 200_000, 20_000, agent: "explore"), embedded, feature with { PullRequestNumber = 42 }),
            (Chat(B, 1, "15:00:00", "gpt-6.1-sol", "conv-b", 100_000, 10_000, server: "contoso.openai.azure.com"), byok, feature),
            (Chat(C, 1, "16:00:00", "qwen3.8-27b", "conv-c", 300_000, 9_000, server: "127.0.0.1"), embedded, main),
            (Chat(C, 2, "16:05:00", "gpt-unknown", "conv-c", 1_000, 100), embedded, main),
            (Chat(D, 1, "17:00:00", "claude-opus-5.5", "conv-d", 10_000, 1_000), embedded, local),
        ];
        await store.RecordAsync(
        [
            .. spans.Select(item => new LedgerEntry(item.Span, item.Rates.Calculate(item.Span), item.Git)),
            j1 with { Span = j1.Span with { TraceId = J, SpanId = SpanId(J, 1) } },
        ]);

        await store.RecordSpanLinksAsync(
        [
            Tool(A, 1, "14:00:30", "conv-a", "bash", exitCode: 0),
            Tool(A, 2, "14:00:40", "conv-a", "bash", exitCode: 1),
            Tool(B, 3, "15:00:10", "conv-b", "view", status: 2),
            Tool(E, 4, "15:30:00", "conv-e", "bash", exitCode: 0),
            new GenAiSpanLink
            {
                TraceId = A,
                SpanId = "7100000000000005",
                OperationName = "invoke_agent",
                AgentName = "explore",
                ConversationId = "conv-a",
                StartTime = At("14:00:50"),
            },
        ]);

        await store.RecordBreakerTripAsync(Velocity(A, "conv-a", "14:02:00", feature.RepositoryRoot));
        long? churn = await store.RecordBreakerTripAsync(new NewBreakerTrip
        {
            Rule = BreakerRule.RetryChurn,
            TraceId = C,
            ConversationId = "conv-c",
            Repository = "swigerb/copilot-ledger",
            Branch = "main",
            RepositoryRoot = feature.RepositoryRoot,
            TrippedAt = At("16:06:00"),
            Detail = "5 failed tool calls in a row",
            FailureCount = 5,
        });
        await store.ResetBreakerTripsAsync(new BreakerTripFilter { Id = churn });
        await store.RecordBreakerTripAsync(Velocity(E, null, "15:31:00", feature.RepositoryRoot));
        await store.SetBranchBudgetAsync("swigerb/copilot-ledger", "feature/ledger", 50m);
        await store.SetBranchBudgetAsync("contoso/api", "main", 10m);
    }

    // Records an imported Copilot request (conv-u, 11-20 12:00) without a service request ID, then a live span of its session
    // without one (trace U, 11-21 12:00), which supersedes it without matching it (I3). Returns the imported request.
    public static async Task<ImportedLedgerEntry> SupersedeUnmatchedAsync(SqliteLedgerStore store)
    {
        ImportedLedgerEntry imported = ImportEntries.Copilot("k9", "conv-u", null);
        await store.RecordImportedAsync([imported], null);
        LedgerEntry live = ImportEntries.Live("conv-u", null, ImportEntries.Start.AddDays(1));
        await store.RecordAsync([live with { Span = live.Span with { TraceId = U, SpanId = SpanId(U, 1) } }]);
        return imported;
    }

    // The 16-character span ID of a trace's request or link: the trace's first two characters, zeros, and index.
    public static string SpanId(string trace, int index) =>
        trace[..2] + "000000000000" + index.ToString("D2", CultureInfo.InvariantCulture);

    private static NormalizedGenAiSpan Chat(
        string trace,
        int index,
        string time,
        string model,
        string conversation,
        long input,
        long output,
        long cacheRead = 0,
        string? agent = null,
        string? server = null) =>
        TestSpans.Chat(
            startTime: At(time),
            requestModel: model,
            agentName: agent,
            conversationId: conversation,
            serverAddress: server,
            inputTokens: input,
            outputTokens: output,
            cacheReadInputTokens: cacheRead) with
        {
            TraceId = trace,
            SpanId = SpanId(trace, index),
        };

    private static GenAiSpanLink Tool(
        string trace,
        int index,
        string time,
        string conversation,
        string tool,
        long? exitCode = null,
        int status = 0) => new()
    {
        TraceId = trace,
        SpanId = "71000000000000" + index.ToString("D2", CultureInfo.InvariantCulture),
        OperationName = "execute_tool",
        ConversationId = conversation,
        StartTime = At(time),
        ToolName = tool,
        ToolExitCode = exitCode,
        StatusCode = status,
    };

    private static NewBreakerTrip Velocity(string trace, string? conversation, string time, string root) => new()
    {
        Rule = BreakerRule.CreditVelocity,
        TraceId = trace,
        ConversationId = conversation,
        Repository = "swigerb/copilot-ledger",
        Branch = "feature/ledger",
        RepositoryRoot = root,
        TrippedAt = At(time),
        Detail = "180.00 AI credits in 5 minutes",
        WindowCostUsd = 1.8m,
        WindowMinutes = 5,
    };

    private static DateTimeOffset At(string time) =>
        DateTimeOffset.Parse($"2026-11-21T{time}Z", CultureInfo.InvariantCulture);
}
