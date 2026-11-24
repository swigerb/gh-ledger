using System.Globalization;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.TestSupport;

// The ledger of gh ledger audit's tests. Times are UTC on 2026-11-21; GeneratedAt is 16:30 at ManualTimeProvider.UtcMinus5.
// Live requests have chat links under their agent's invoke_agent link, as Copilot CLI exports them.
// R  trace R, conv-r, swigerb/copilot-ledger feature/ledger, pull request 42. The default agent (invocation R0) asks
//    claude-opus-5.5 at 15:00:00 (20,000 input tokens, 16,000 read from the cache, 400 output) and calls task at 15:00:05,
//    whose explore sub-agent (R1, from 15:00:06) asks claude-haiku-4.5 five times from 15:00:10, 20 seconds apart (10,000
//    input, 8,000 read, 300 output: $0.0043 each) and calls bash five times from 15:00:20, 20 seconds apart, with exit codes
//    1, 1, 1, 1, and 0. The default agent then asks claude-opus-5.5 at 15:02:00 (22,000, 17,600, 300).
// P  trace P, conv-p, main: the default agent (P0) asks claude-opus-5.5 twelve short, tool-free questions from 14:00:00,
//    2 minutes apart (30,000 input, 24,000 read, 120 output: $0.0312 each).
// M  trace M, conv-m, feature/ledger, pull request 42: the default agent (M0) asks mai-code-2 six times from 18:00:00, a
//    minute apart, with 40,000 input tokens, both cache counters reported as 0, and 1,500 output.
// B  trace B, conv-b, feature/ledger, pull request 42: gpt-6.2-sol at 17:00:00 with 260,000 input tokens (200,000 read) and
//    1,000 output, without an invoke_agent link.
// I1, I2  imported Copilot records (conv-i, 11-20 12:00): the main agent's (numToolCalls 1, its bash call exited 0), and an
//    explore sub-agent's (numToolCalls 0).
// X1 an imported Codex record (conv-x, 11-20 12:00), billed by OpenAI, so unpriced.
// The findings: failed_tool_retry (R, high), premium_model_trivial_turn (P, warning), cache_miss_streak (M, warning), and
// context_bloat (B, info).
public static class AuditSeeds
{
    public const string R = "52525252525252525252525252525252";
    public const string P = "50505050505050505050505050505050";
    public const string M = "4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d";
    public const string B = "42424242424242424242424242424242";

    public static readonly DateTimeOffset GeneratedAt = new(2026, 11, 21, 21, 30, 0, TimeSpan.Zero);

    // A ledger at <root>/data/ledger.db, where CliHarness(root) looks, whose clock stays at GeneratedAt; seeded.
    public static async Task<SqliteLedgerStore> CreateAsync(string root)
    {
        SqliteLedgerStore store = new(Path.Combine(root, "data", "ledger.db"), new ManualTimeProvider(GeneratedAt));
        await store.InitializeAsync();
        await SeedAsync(store, root);
        return store;
    }

    public static async Task SeedAsync(SqliteLedgerStore store, string root)
    {
        GitWorkspaceContext feature = GitFixture.Feature(root) with { PullRequestNumber = 42 };
        GitWorkspaceContext main = GitFixture.Feature(root) with { BranchName = "main" };

        LiveTrace r = new(R, "conv-r", feature);
        string r0 = r.Agent(0, At("15:00:00"));
        r.Request(1, r0, At("15:00:00"), "claude-opus-5.5", 20_000, 16_000, 400);
        string task = r.Tool(0, r0, At("15:00:05"), "task", null, 1);
        string r1 = r.Agent(1, At("15:00:06"), task, "explore");
        for (int k = 0; k < 5; k++)
        {
            r.Request(2 + k, r1, At("15:00:10").AddSeconds(20 * k), "claude-haiku-4.5", 10_000, 8_000, 300, "explore");
            r.Tool(1 + k, r1, At("15:00:20").AddSeconds(20 * k), "bash", k < 4 ? 1 : 0, k < 4 ? 2 : 1, "explore");
        }

        r.Request(7, r0, At("15:02:00"), "claude-opus-5.5", 22_000, 17_600, 300);
        await r.RecordAsync(store);

        LiveTrace p = new(P, "conv-p", main);
        string p0 = p.Agent(0, At("14:00:00"));
        for (int k = 0; k < 12; k++)
        {
            p.Request(1 + k, p0, At("14:00:00").AddMinutes(2 * k), "claude-opus-5.5", 30_000, 24_000, 120);
        }

        await p.RecordAsync(store);

        LiveTrace m = new(M, "conv-m", feature);
        string m0 = m.Agent(0, At("18:00:00"));
        for (int k = 0; k < 6; k++)
        {
            m.Request(1 + k, m0, At("18:00:00").AddMinutes(k), "mai-code-2", 40_000, 0, 1_500, reportZeroCache: true);
        }

        await m.RecordAsync(store);

        LiveTrace b = new(B, "conv-b", feature);
        b.Request(1, null, At("17:00:00"), "gpt-6.2-sol", 260_000, 200_000, 1_000);
        await b.RecordAsync(store);

        ImportedLedgerEntry i1 = ImportEntries.Copilot("k1", "conv-i", null);
        ImportedLedgerEntry i2 = ImportEntries.Copilot("k2", "conv-i", null);
        await store.RecordImportedAsync(
        [
            i1 with { ToolCallCount = 1, ToolLinks = [ImportEntries.ToolLink(i1, "call-1", 0)] },
            i2 with { Span = i2.Span with { AgentName = "explore" }, ToolCallCount = 0 },
            Codex("x1", "conv-x"),
        ],
            null);
    }

    // The 16-character span ID of a trace's request (c), agent (a), or tool call (t): the trace's first two characters, the
    // kind, and the index in 13 digits.
    public static string SpanId(string trace, char kind, int index) =>
        trace[..2] + kind + index.ToString("D13", CultureInfo.InvariantCulture);

    // An imported Codex request that OpenAI billed, so gh-ledger can't price it.
    public static ImportedLedgerEntry Codex(string key, string conversation)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: ImportEntries.Start,
            providerName: "openai",
            requestModel: "gpt-6.2-sol",
            conversationId: conversation,
            inputTokens: 2_000,
            outputTokens: 200) with
        {
            TraceId = ImportIds.TraceId(ImportSourceKind.Codex, conversation, null, ImportedRecordKind.Usage, key),
            SpanId = ImportIds.SpanId(ImportSourceKind.Codex, conversation, ImportedRecordKind.Usage, key),
            BillingSource = BillingSource.Provider,
        };
        return new ImportedLedgerEntry
        {
            Source = ImportSourceKind.Codex,
            RecordKeySha256 = ImportIds.RecordKeySha256(ImportSourceKind.Codex, ImportedRecordKind.Usage, key),
            Span = span,
            Calculation = ImportEntries.Calculator.Calculate(span),
        };
    }

    public static DateTimeOffset At(string time) =>
        DateTimeOffset.Parse($"2026-11-21T{time}Z", CultureInfo.InvariantCulture);

    // One live trace as Copilot CLI exports it, priced with the embedded rates: invoke_agent links, requests with their chat
    // links, and execute_tool links. RecordAsync records them and copies each agent's span ID onto its requests.
    public sealed class LiveTrace(string traceId, string conversation, GitWorkspaceContext? git)
    {
        private readonly List<LedgerEntry> _requests = [];
        private readonly List<GenAiSpanLink> _links = [];

        // An invoke_agent link (span ID kind a), under parent when it isn't null. Returns its span ID.
        public string Agent(int index, DateTimeOffset start, string? parent = null, string? name = null)
        {
            string spanId = SpanId(traceId, 'a', index);
            _links.Add(Link(spanId, parent, GenAiOperationNames.InvokeAgent, start, name));
            return spanId;
        }

        // A request (span ID kind c) ending 2 seconds after start, and its chat link under agent when agent isn't null.
        public void Request(
            int index,
            string? agent,
            DateTimeOffset start,
            string model,
            long input,
            long cacheRead,
            long output,
            string? agentName = null,
            bool reportZeroCache = false)
        {
            NormalizedGenAiSpan span = TestSpans.Chat(
                parentSpanId: agent,
                startTime: start,
                requestModel: model,
                responseModel: model,
                agentName: agentName,
                conversationId: conversation,
                inputTokens: input,
                outputTokens: output,
                cacheReadInputTokens: cacheRead,
                cacheReadState: reportZeroCache ? CacheCounterState.Reported : null,
                cacheWriteState: reportZeroCache ? CacheCounterState.Reported : null) with
            {
                TraceId = traceId,
                SpanId = SpanId(traceId, 'c', index),
            };
            _requests.Add(new LedgerEntry(span, ImportEntries.Calculator.Calculate(span), git));
            if (agent is not null)
            {
                _links.Add(Link(span.SpanId, agent, GenAiOperationNames.Chat, start, agentName));
            }
        }

        // An execute_tool link (span ID kind t) under parent. Returns its span ID.
        public string Tool(
            int index,
            string? parent,
            DateTimeOffset start,
            string tool,
            long? exitCode,
            int status,
            string? agentName = null)
        {
            string spanId = SpanId(traceId, 't', index);
            _links.Add(Link(spanId, parent, GenAiOperationNames.ExecuteTool, start, agentName) with
            {
                ToolName = tool,
                ToolExitCode = exitCode,
                StatusCode = status,
            });
            return spanId;
        }

        public async Task RecordAsync(SqliteLedgerStore store)
        {
            if (_requests.Count > 0)
            {
                await store.RecordAsync(_requests);
            }

            if (_links.Count > 0)
            {
                await store.RecordSpanLinksAsync(_links);
                await store.BackfillFromAgentSpansAsync([traceId]);
            }
        }

        private GenAiSpanLink Link(string spanId, string? parent, string operation, DateTimeOffset start, string? agentName) => new()
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parent,
            OperationName = operation,
            AgentName = agentName,
            ConversationId = conversation,
            StartTime = start,
        };
    }
}
