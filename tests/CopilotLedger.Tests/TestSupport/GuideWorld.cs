using System.Globalization;
using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Otlp.Breaker;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.TestSupport;

public enum GuideState
{
    Fresh,          // nothing installed and no ledger yet
    FirstPrompt,    // installed at 16:20, the daemon running, one prompt at 16:25
    Backfill,       // FirstPrompt, and Copilot's session logs of earlier sessions
    Month,          // installed, the daemon running since 08:02, a month of spend, one breaker trip, a budget, and an overlay
}

// The setup behind the samples in docs/guide.md: gh-ledger as the GitHub CLI extension on Windows, run in the
// contoso/webapp repository on branch feature/search at 16:30 on 2026-11-21, UTC-5. The daemon is a stand-in that
// writes daemon.json with process 4242, holds daemon.lock, and exits when daemon stop writes daemon.stop.
public sealed class GuideWorld : IAsyncDisposable
{
    public const string ExtensionPath = @"C:\Users\you\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe";
    public const string ShownDataDirectory = @"C:\Users\you\.copilot-ledger";
    public const string Slug = "contoso/webapp";
    public const string Commit = "4f9c2e1d8b7a6c5d4e3f2a1b0c9d8e7f6a5b4c3d";
    public const int DaemonProcessId = 4242;

    public static readonly DateTimeOffset Now = Utc("2026-11-21T21:30:00Z");

    private readonly ManualTimeProvider _clock;
    private readonly List<(CancellationTokenSource Stop, Task Run)> _daemons = [];

    private GuideWorld(string root, ManualTimeProvider clock)
    {
        _clock = clock;
        Repository = Path.Combine(root, "webapp");
        Platform = new FakeCliPlatform
        {
            IsWindows = true,
            ProcessPath = ExtensionPath,
        };
        Platform.ExistingFiles.Add(
            @"C:\Users\you\AppData\Local\GitHub CLI\extensions\gh-ledger\manifest.yml");
        Platform.RunningProcesses.Add(DaemonProcessId);
        Harness = new CliHarness(root, Platform, clock);
        Platform.StartDetachedHandler = (_, _, _) =>
        {
            StartDaemon();
            return DaemonProcessId;
        };
    }

    public CliHarness Harness { get; }
    public FakeCliPlatform Platform { get; }
    public string Repository { get; }
    public ManualTimeProvider Clock => _clock;

    // Creates the repository and the state; overlay is the rates overlay the Month state saves and prices with.
    public static async Task<GuideWorld> CreateAsync(string root, GuideState state, string overlay)
    {
        DateTimeOffset start = state switch
        {
            GuideState.Fresh => Now,
            GuideState.FirstPrompt or GuideState.Backfill => Utc("2026-11-21T21:20:00Z"),
            _ => Utc("2026-11-21T13:02:00Z"),
        };
        GuideWorld world = new(root, new ManualTimeProvider(start, ManualTimeProvider.UtcMinus5));
        world.CreateRepository();
        if (state != GuideState.Fresh)
        {
            CliRun install = await world.Harness.RunAsync("install");
            Assert.Equal(0, install.ExitCode);
        }

        world._clock.Advance(Now - start);
        if (state != GuideState.Fresh)
        {
            await world.SeedAsync(state, overlay);
        }

        return world;
    }

    // Runs "gh ledger <arguments>" in the repository.
    public Task<CliRun> RunAsync(string command)
    {
        Assert.StartsWith("gh ledger ", command);
        return Harness.RunInAsync(Repository, command["gh ledger ".Length..].Split(' '));
    }

    // Shows the test's data directory as C:\Users\you\.copilot-ledger.
    public string Display(string text)
    {
        string data = Harness.Context.DataDirectory;
        return text
            .Replace(data + Path.DirectorySeparatorChar, ShownDataDirectory + @"\", StringComparison.Ordinal)
            .Replace(data, ShownDataDirectory, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        foreach ((CancellationTokenSource stop, Task run) in _daemons)
        {
            await stop.CancelAsync();
            await run;
            stop.Dispose();
        }

        await Harness.DisposeAsync();
    }

    private static DateTimeOffset Utc(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private void CreateRepository()
    {
        string git = Path.Combine(Repository, ".git");
        string branch = Path.Combine(git, "refs", "heads", "feature", "search");
        Directory.CreateDirectory(Path.GetDirectoryName(branch)!);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/feature/search\n");
        File.WriteAllText(branch, Commit + "\n");
        File.WriteAllText(
            Path.Combine(git, "config"),
            "[remote \"origin\"]\n\turl = https://github.com/contoso/webapp.git\n");
        _ = FileRetry.ReadAllText(Path.Combine(git, "HEAD"));
        _ = FileRetry.ReadAllText(branch);
        _ = FileRetry.ReadAllText(Path.Combine(git, "config"));
    }

    // Each start holds its own lock until daemon stop writes daemon.stop or the world is disposed, then removes
    // daemon.json and daemon.stop and releases the lock, as the real daemon does.
    private void StartDaemon()
    {
        string data = Harness.Context.DataDirectory;
        FileStream daemonLock = new(
            Path.Combine(data, "daemon.lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        File.WriteAllText(
            Path.Combine(data, "daemon.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                processId = DaemonProcessId,
                startedAt = _clock.GetUtcNow(),
                endpoint = "http://localhost:4318",
                version = GhLedgerCli.Version,
                databasePath = Harness.Context.DatabasePath,
            }));
        _ = FileRetry.ReadAllText(Path.Combine(data, "daemon.json"));
        CancellationTokenSource stop = new();
        _daemons.Add((stop, RunDaemonAsync(data, daemonLock, stop.Token)));
    }

    private static async Task RunDaemonAsync(
        string data,
        FileStream daemonLock,
        CancellationToken cancellationToken)
    {
        string stopPath = Path.Combine(data, "daemon.stop");
        while (!cancellationToken.IsCancellationRequested && !File.Exists(stopPath))
        {
            await Task.Delay(25, CancellationToken.None);
        }

        try
        {
            FileRetry.Delete(Path.Combine(data, "daemon.json"));
            FileRetry.Delete(stopPath);
        }
        finally
        {
            await daemonLock.DisposeAsync();
        }
    }

    private async Task SeedAsync(GuideState state, string overlay)
    {
        SqliteLedgerStore store = new(Harness.Context.DatabasePath, _clock);
        await store.InitializeAsync();
        if (state is GuideState.FirstPrompt or GuideState.Backfill)
        {
            JsonRateCardCalculator builtIn = JsonRateCardCalculator.LoadEmbedded();
            NormalizedGenAiSpan first = Chat("1f", "c1", "2026-11-21T21:25:00Z", "claude-opus-5.5", null, null, 30_000, 1_500, 20_000);
            await store.RecordAsync([new LedgerEntry(first, builtIn.Calculate(first), Context("feature/search"))]);
            if (state == GuideState.Backfill)
            {
                WriteSessionLogs();
            }

            return;
        }

        string overlayPath = Path.Combine(Harness.Context.DataDirectory, "rates.json");
        await File.WriteAllTextAsync(overlayPath, overlay);
        _ = await FileRetry.ReadAllTextAsync(overlayPath);
        JsonRateCardCalculator rates = JsonRateCardCalculator.LoadEmbeddedWithOverlay(overlayPath);
        (NormalizedGenAiSpan Span, string Branch)[] spans =
        [
            (Chat("a1", "c1", "2026-11-21T14:05:00Z", "claude-opus-5.5", null, null, 400_000, 15_000, 250_000), "feature/search"),
            (Chat("a1", "c2", "2026-11-21T14:07:00Z", "mai-code-2", null, "explore", 300_000, 15_000), "feature/search"),
            (Chat("b2", "c1", "2026-11-21T16:20:00Z", "claude-opus-5.5", null, null, 357_500, 20_000, 150_000), "feature/search"),
            (Chat("b2", "c2", "2026-11-21T16:21:30Z", "claude-opus-5.5", null, null, 452_500, 20_000, 250_000), "feature/search"),
            (Chat("b2", "c3", "2026-11-21T16:23:00Z", "claude-opus-5.5", null, null, 547_500, 20_000, 350_000), "feature/search"),
            (Chat("b2", "c4", "2026-11-21T16:24:30Z", "claude-opus-5.5", null, null, 642_500, 20_000, 450_000), "feature/search"),
            (Chat("c3", "c1", "2026-11-21T19:10:00Z", "gpt-6.1-sol", "contoso.openai.azure.com", null, 200_000, 10_000), "feature/search"),
            (Chat("d4", "c1", "2026-11-21T20:40:00Z", "qwen3.8-27b", "127.0.0.1", null, 300_000, 9_000), "feature/search"),
            (Chat("e5", "c1", "2026-11-19T15:00:00Z", "claude-opus-5.5", null, "code-review", 200_000, 20_000), "fix/login-timeout"),
            (Chat("f6", "c1", "2026-11-18T18:00:00Z", "mai-code-2", null, null, 800_000, 40_000, 500_000), "main"),
            (Chat("a7", "c1", "2026-11-03T15:30:00Z", "claude-opus-5.5", null, "explore", 300_000, 12_000), "main"),
        ];
        await store.RecordAsync(
            spans.Select(item => new LedgerEntry(item.Span, rates.Calculate(item.Span), Context(item.Branch))).ToArray());

        // Each prompt's span tree: the default agent's invoke_agent span (span ID ending a0), a subagent's invoke_agent
        // span (e0) below it, and each request's chat span below its agent's. Links already recorded are kept.
        List<GenAiSpanLink> links = [];
        foreach (NormalizedGenAiSpan span in spans.Select(item => item.Span))
        {
            string root = span.SpanId[..14] + "a0";
            links.Add(Link(span, root, null, null, "invoke_agent"));
            if (span.AgentName is not null)
            {
                links.Add(Link(span, span.ParentSpanId!, root, span.AgentName, "invoke_agent"));
            }

            links.Add(Link(span, span.SpanId, span.ParentSpanId, span.AgentName, "chat"));
        }

        await store.RecordSpanLinksAsync(links);
        await store.BackfillFromAgentSpansAsync(spans.Select(item => item.Span.TraceId));
        await new CircuitBreakerMonitor(store, new RecordingAlertSink()).EvaluateAsync(
            spans.Select(item => item.Span.TraceId));
        foreach (BreakerTrip trip in await store.GetOpenBreakerTripsAsync(BreakerTripFilter.All))
        {
            Assert.True(await store.MarkBreakerTripNotifiedAsync(trip.Id));
        }

        await store.SetBranchBudgetAsync(Slug, "feature/search", 750m);
    }

    private GitWorkspaceContext Context(string branch) => new(
        Repository,
        Path.Combine(Repository, ".git"),
        branch,
        Commit,
        "https://github.com/contoso/webapp.git",
        Slug);

    // Copilot 1.0.110's logs, as Copilot CLI writes them under COPILOT_HOME: two sessions in this repository on 2026-11-20,
    // before CopilotLedger was installed, one with an explore subagent and one with a compaction; one in contoso/api; and the
    // session of the prompt at 16:25, which is still open.
    private void WriteSessionLogs()
    {
        string home = Harness.Context.CopilotHome;
        string api = Path.Combine(Path.GetDirectoryName(Repository)!, "api");
        new CopilotSessionLogs(
                "3f9b2c71-6d4e-4a8b-9c1d-2e5f7a9b0c11",
                start: Utc("2026-11-20T14:00:00Z"),
                context: CopilotSessionLogs.Context(Repository, Repository, "feature/search", Slug, Commit))
            .UserMessage()
            .Usage("msg_01A", "0b6f3a2e-9c41-4d7a-8e25-6b1f0c9d3a71", "claude-opus-5.5", input: 40_000, output: 2_000, cacheRead: 30_000)
            .SubagentStarted("agent-1", "explore", "toolu_01")
            .Usage(
                "msg_01B",
                "1c7a4b3f-0d52-4e8b-9f36-7c2a1d0e4b82",
                "claude-haiku-4.5",
                input: 20_000,
                output: 1_000,
                cacheRead: 10_000,
                initiator: "agent",
                agentId: "agent-1")
            .Usage(
                "msg_01C",
                "2d8b5c4a-1e63-4f9c-8a47-8d3b2e1f5c93",
                "claude-opus-5.5",
                input: 60_000,
                output: 3_000,
                cacheRead: 50_000,
                initiator: "agent")
            .Write(home);
        new CopilotSessionLogs(
                "8c2d4e6f-1a3b-4c5d-8e7f-9a0b1c2d3e4f",
                start: Utc("2026-11-20T18:00:00Z"),
                context: CopilotSessionLogs.Context(Repository, Repository, "feature/search", Slug, Commit))
            .UserMessage()
            .Usage("msg_02A", "3e9c6d5b-2f74-4a0d-9b58-9e4c3f2a6d04", "claude-opus-5.5", input: 50_000, output: 2_500, cacheRead: 40_000)
            .Compaction(
                "4fad7e6c-3a85-4b1e-8c69-af5d4a3b7e15",
                "claude-haiku-4.5",
                input: 80_000,
                output: 500,
                cacheRead: 20_000,
                cacheWrite: 50_000,
                duration: 900)
            .Write(home);
        new CopilotSessionLogs(
                "c41e7a9b-2d3f-4b5c-8d6e-7f8a9b0c1d2e",
                start: Utc("2026-11-19T15:00:00Z"),
                context: CopilotSessionLogs.Context(api, api, "main", "contoso/api", Commit))
            .UserMessage()
            .Usage("msg_03A", "5abe8f7d-4b96-4c2f-9d7a-b06e5b4c8f26", "claude-opus-5.5", input: 30_000, output: 1_000)
            .Write(home);
        new CopilotSessionLogs(
                "7d4e1c2a-5b3f-4e8d-9a6c-00000000001f",
                start: Utc("2026-11-21T21:24:00Z"),
                context: CopilotSessionLogs.Context(Repository, Repository, "feature/search", Slug, Commit))
            .UserMessage()
            .Usage("msg_04A", "6bcf908e-5ca7-4d3a-8e8b-c17f6c5d9037", "claude-opus-5.5", input: 30_000, output: 1_500, cacheRead: 20_000)
            .Write(home, active: true);
    }

    // trace and span are the last characters of 32- and 16-character IDs. GitHub-hosted models report cache counters, and
    // this example's Foundry BYOK and local endpoints (the spans with a serverAddress) don't. A request's parent is its
    // agent's invoke_agent span: the default agent's ends in a0, and a subagent's in e0.
    private static NormalizedGenAiSpan Chat(
        string trace,
        string span,
        string start,
        string model,
        string? serverAddress,
        string? agent,
        long inputTokens,
        long outputTokens,
        long cacheReadInputTokens = 0) =>
        TestSpans.Chat(
            parentSpanId: "5e8a1c3f7b9d" + trace.PadLeft(2, '0') + (agent is null ? "a0" : "e0"),
            startTime: Utc(start),
            requestModel: model,
            serverAddress: serverAddress,
            agentName: agent,
            conversationId: "7d4e1c2a-5b3f-4e8d-9a6c-" + trace.PadLeft(12, '0'),
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            cacheReadInputTokens: cacheReadInputTokens,
            cacheReadState: serverAddress is null ? CacheCounterState.Reported : CacheCounterState.Absent,
            cacheWriteState: serverAddress is null ? CacheCounterState.Reported : CacheCounterState.Absent) with
        {
            TraceId = "0f3c9a7e5d1b4f2a8c6e0d9b7a5c" + trace.PadLeft(4, '0'),
            SpanId = "5e8a1c3f7b9d" + trace.PadLeft(2, '0') + span,
        };

    private static GenAiSpanLink Link(
        NormalizedGenAiSpan chat,
        string spanId,
        string? parentSpanId,
        string? agent,
        string operation) => new()
    {
        TraceId = chat.TraceId,
        SpanId = spanId,
        ParentSpanId = parentSpanId,
        OperationName = operation,
        AgentName = agent,
        ConversationId = chat.ConversationId,
        StartTime = chat.StartTime,
    };
}
