using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class CodexRolloutTests
{
    private const string Thread = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b";
    private const string Fork = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2c";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static readonly CodexSessionLogSource Source = new();

    public static TheoryData<int, string> Shapes => new()
    {
        { 0, "payload.response_id" },
        { 1, "payload.response_id" },
        { 2, "payload.usage.output_tokens" },
        { 3, "payload.usage.input_tokens" },
        { 4, "payload.usage.input_tokens" },
    };

    [Fact]
    public void CX1_DiscoveryListsRolloutsAndArchivedOnes()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread).Write(temp.Path);
        new CodexRollouts(Thread).Write(temp.Path, rolloutId: Fork.ToUpperInvariant());
        new CodexRollouts(Fork).Write(temp.Path, archived: true);
        new CodexRollouts(Fork).Write(temp.Path, compressed: true);
        string day = Path.Combine(temp.Path, "sessions", "2026", "11", "20");
        File.WriteAllText(Path.Combine(day, "notes.txt"), "x");
        File.WriteAllText(Path.Combine(day, "rollout-2026-11-20T12-00-00-not-a-thread.jsonl"), "x");
        File.WriteAllText(Path.Combine(day, $"rollout-2026-13-20T12-00-00-{Thread}.jsonl"), "x");

        SessionLogDiscovery discovery = Source.Discover(temp.Path);
        IReadOnlyList<SessionLogLocation> logs = discovery.Logs;

        Assert.Equal(
            [Thread, $"{Thread}_{Fork}", Fork, $"archived/{Fork}"],
            logs.Select(log => log.LogId));
        Assert.Equal([false, false, true, false], logs.Select(log => log.IsCompressed));
        Assert.All(logs, log => Assert.False(log.IsActive));
        Assert.Equal(0, discovery.UnreadableEntries);
        SessionLogDiscovery missing = Source.Discover(Path.Combine(temp.Path, "missing"));
        Assert.Empty(missing.Logs);
        Assert.Equal(0, missing.UnreadableEntries);
    }

    [Theory]
    [InlineData("0.153.0", SessionLogStatus.Read)]
    [InlineData("0.171.0", SessionLogStatus.Read)]
    [InlineData("0.152.0", SessionLogStatus.UnsupportedProducerVersion)]
    [InlineData("0.172.0", SessionLogStatus.UnsupportedProducerVersion)]
    [InlineData("0.173.0-alpha.1", SessionLogStatus.UnsupportedProducerVersion)]
    [InlineData(null, SessionLogStatus.MissingSessionStart)]
    public async Task CX2_TheGateReadsOnlyCodex0_153_0To0_171_0(string? version, SessionLogStatus status)
    {
        using TempDirectory temp = new();
        CodexRollouts rollout = new CodexRollouts(Thread, version: version ?? "0.171.0")
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1");
        if (version is null)
        {
            string path = rollout.Write(temp.Path);
            File.WriteAllLines(path, File.ReadAllLines(path).Skip(1));
        }
        else
        {
            rollout.Write(temp.Path);
        }

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(status, result.Status);
        Assert.Equal(status == SessionLogStatus.Read ? 1 : 0, result.Records.Count);
    }

    [Fact]
    public async Task CX3_ATokenUsageRecordMapsEveryField()
    {
        using TempDirectory temp = new();
        string cwd = Path.Combine(temp.Path, "api");
        CodexRollouts rollout = new CodexRollouts(
                Thread,
                sessionId: Fork,
                modelProvider: "OpenAI",
                agentRole: "explorer",
                git: new JsonObject
                {
                    ["commit_hash"] = Sha,
                    ["branch"] = "feature/x",
                    ["repository_url"] = "https://github.com/contoso/api.git",
                },
                cwd: cwd)
            .TurnContext("turn-2", "gpt-6.1-sol")
            .Usage("resp_2", "turn-2", input: 100, cached: 40, output: 7, cacheWrite: 60, rootTurnId: "turn-1");
        rollout.Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(1, result.RecordsParsed);
        Assert.Equal("0.171.0", result.ProducerVersion);
        Assert.Equal(
            new ImportedUsageRecord
            {
                Source = ImportSourceKind.Codex,
                Kind = ImportedRecordKind.Usage,
                SessionId = Fork,
                RecordKey = "resp_2",
                TurnKey = "turn-1",
                ServiceRequestId = null,
                StartTime = rollout.Time,
                EndTime = rollout.Time,
                RequestModel = "gpt-6.1-sol",
                ResponseModel = null,
                ProviderName = "openai",
                BillingSource = BillingSource.Provider,
                AgentName = "explorer",
                InputTokens = 100,
                OutputTokens = 7,
                CacheReadInputTokens = 40,
                CacheWriteInputTokens = 60,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Reported,
                ToolCallCount = null,
                Git = new ImportedGitContext
                {
                    WorkingDirectory = cwd,
                    RepositoryRoot = null,
                    RepositorySlug = "contoso/api",
                    Branch = "feature/x",
                    HeadCommitSha = Sha,
                },
                LineNumber = 3,
            },
            Assert.Single(result.Records));
    }

    [Fact]
    public async Task CX4_TheModelComesFromTheRecordsTurnElseTheLatestTurnContext()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .TurnContext("turn-1", "gpt-6.1-sol")
            .TurnContext(null, "gpt-6-luna")
            .Usage("resp_1", "turn-1")
            .Usage("resp_2", "turn-9", cacheWrite: null)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(["gpt-6.1-sol", "gpt-6-luna"], result.Records.Select(record => record.RequestModel));
        Assert.Equal(
            [CacheCounterState.Reported, CacheCounterState.Absent],
            result.Records.Select(record => record.CacheWriteState));
    }

    [Theory]
    [InlineData("ollama", "ollama")]
    [InlineData("lmstudio", "lm_studio")]
    [InlineData("openai", "openai")]
    [InlineData(null, null)]
    public async Task CX5_TheModelProviderNamesTheProvider(string? modelProvider, string? providerName)
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread, modelProvider: modelProvider)
            .TurnContext("turn-1", "qwen3.8-27b")
            .Usage("resp_1", "turn-1")
            .Write(temp.Path);

        ImportedUsageRecord record = Assert.Single((await ReadAsync(temp.Path)).Records);

        Assert.Equal(providerName, record.ProviderName);
        Assert.Equal(BillingSource.Provider, record.BillingSource);
    }

    [Fact]
    public async Task CX6_TokenCountsAndCompactedSnapshotsAddNothing()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .TurnContext("turn-1", "gpt-6.1-sol")
            .TokenCount(1_000, 0, 100)
            .Usage("resp_1", "turn-1")
            .TokenCount(1_000, 0, 100)
            .TokenCount(1_000, 0, 100)
            .TokenCount(0, 0, 0)
            .Line(
                "compacted",
                new JsonObject
                {
                    ["message"] = CopilotSessionLogs.Canary,
                    ["compaction_response_id"] = "resp_compact",
                    ["latest_token_usage_record"] = new JsonObject
                    {
                        ["thread_id"] = Thread,
                        ["response_id"] = "resp_1",
                        ["usage"] = new JsonObject { ["input_tokens"] = 1_000 },
                    },
                })
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(["resp_1"], result.Records.Select(record => record.RecordKey));
        Assert.Equal(1, result.RecordsParsed);
        Assert.Equal(5, result.SkippedEvents);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task CX7_UnsupportedRecordsNameTheirField(int caseNumber, string field)
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage(
                caseNumber == 0 ? null : caseNumber == 1 ? "" : "resp_1",
                "turn-1",
                output: caseNumber == 2 ? -1 : 100,
                cached: caseNumber == 3 ? 1_001 : 600,
                cacheWrite: caseNumber == 4 ? 401 : 0,
                change: payload =>
                {
                    if (caseNumber == 0)
                    {
                        payload.Remove("response_id");
                    }
                })
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal(1, result.UnsupportedUsageShapes);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.UnsupportedUsageShape, diagnostic.Code);
        Assert.Equal(ImportEventKind.TokenUsageRecord, diagnostic.Event);
        Assert.Equal(field, diagnostic.Field);
    }

    [Fact]
    public async Task CX8_ARecordOfAnotherThreadIsSkipped()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .Line("session_meta", new JsonObject { ["id"] = Fork, ["session_id"] = Fork, ["cli_version"] = "0.171.0" })
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_parent", "turn-1", threadId: Fork)
            .Usage("resp_own", "turn-1")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(["resp_own"], result.Records.Select(record => record.RecordKey));
        Assert.Equal(1, result.RecordsParsed);
        Assert.Equal(1, result.SkippedEvents);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.InheritedRecord, diagnostic.Code);
        Assert.Equal(4, diagnostic.FirstLine);
    }

    [Fact]
    public async Task CX9_CredentialsInTheRepositoryUrlAreNeverKept()
    {
        using TempDirectory temp = new();
        new CodexRollouts(
                Thread,
                git: new JsonObject { ["repository_url"] = "https://user:" + CopilotSessionLogs.Canary + "@github.com/o/r.git" })
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal("o/r", Assert.Single(result.Records).Git!.RepositorySlug);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CX10_ACompressedRolloutIsNeverOpened()
    {
        using TempDirectory temp = new();
        string path = new CodexRollouts(Thread).Write(temp.Path, compressed: true);
        SessionLogLocation location = Assert.Single(Source.Discover(temp.Path).Logs);
        File.Delete(path);

        SessionLogReadResult result = await Source.ReadAsync(location, CancellationToken.None);

        Assert.Equal(SessionLogStatus.Compressed, result.Status);
        Assert.Equal(ImportDiagnosticCode.CompressedLog, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("codex-home")]
    public void CX11_CodexHomeIsCodexHomeWhenSet(string? codexHome)
    {
        string profile = Path.Combine(Path.GetTempPath(), "profile");

        Assert.Equal(
            string.IsNullOrEmpty(codexHome) ? Path.Combine(profile, ".codex") : codexHome,
            CodexSessionLogSource.ResolveHome(codexHome, profile));
    }

    [Fact]
    public async Task CX12_NoContentReachesTheResult()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .UserMessage()
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Line("event_msg", new JsonObject { ["type"] = "agent_message", ["message"] = CopilotSessionLogs.Canary })
            .Line(
                "response_item",
                new JsonObject
                {
                    ["type"] = "function_call",
                    ["name"] = "shell",
                    ["arguments"] = CopilotSessionLogs.Canary,
                    ["call_id"] = CopilotSessionLogs.Canary,
                })
            .Usage("resp_1", "turn-1")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Single(result.Records);
        AssertNoCanary(result);
    }

    // A credential in a URL's query or fragment is dropped with them: the slug comes from the path alone.
    [Theory]
    [InlineData("https://git.example/o/r.git?token=" + CopilotSessionLogs.Canary)]
    [InlineData("https://git.example/o/r#" + CopilotSessionLogs.Canary)]
    public async Task CX13_AQueryOrFragmentOfTheRepositoryUrlIsNeverKept(string url)
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread, git: new JsonObject { ["repository_url"] = url })
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal("o/r", Assert.Single(result.Records).Git!.RepositorySlug);
        AssertNoCanary(result);
    }

    // A record that gives thread_id twice is an unsupported shape, whichever thread its first value names.
    [Fact]
    public async Task CX14_ARecordThatGivesAFieldTwiceIsUnsupportedNotInherited()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Raw(
                """{"timestamp":"2026-11-20T12:00:02.000Z","type":"token_usage_record","payload":{"thread_id":"FORK","thread_id":"THREAD","turn_id":"turn-1","session_id":"THREAD","root_turn_id":"turn-1","response_id":"resp_1","usage":{"input_tokens":1000,"cached_input_tokens":0,"output_tokens":100}}}"""
                    .Replace("FORK", Fork, StringComparison.Ordinal)
                    .Replace("THREAD", Thread, StringComparison.Ordinal))
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal((1, 1), (result.RecordsParsed, result.UnsupportedUsageShapes));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            (ImportDiagnosticCode.UnsupportedUsageShape, "payload.thread_id", 3L),
            (diagnostic.Code, diagnostic.Field, diagnostic.FirstLine));
    }

    // A rollout folder that can't be listed is counted, and the other folder's rollouts are still found.
    [Fact]
    public async Task CX15_ARolloutFolderThatCantBeListedIsCounted()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread).Write(temp.Path, archived: true);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "sessions"), "not a folder");

        SessionLogDiscovery discovery = Source.Discover(temp.Path);

        Assert.Equal([$"archived/{Thread}"], discovery.Logs.Select(log => log.LogId));
        Assert.Equal(1, discovery.UnreadableEntries);
    }

    // A rollout's SHA-256 covers the bytes it was read up to.
    [Fact]
    public async Task CX16_AReadRolloutHasTheHashOfItsBytes()
    {
        using TempDirectory temp = new();
        string path = new CodexRollouts(Thread).TurnContext("turn-1", "gpt-6.1-sol").Usage("resp_1", "turn-1").Write(temp.Path);
        SessionLogLocation location = Assert.Single(Source.Discover(temp.Path).Logs);
        byte[] bytes = await File.ReadAllBytesAsync(path);
        await File.AppendAllTextAsync(path, "{\"type\":\"event_msg\"}\n");

        SessionLogReadResult result = await Source.ReadAsync(location, CancellationToken.None);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), result.ContentSha256);
        Assert.Equal(result.ContentSha256, await SessionLogHash.ComputeAsync(location, CancellationToken.None));
    }

    // As CS16: a name or type that holds an escaped lone surrogate is none of the parser's, and the rollout is read on.
    [Fact]
    public async Task CX17_AnEscapedLoneSurrogateInANameOrATypeIsNeverOneOfTheParsers()
    {
        using TempDirectory temp = new();
        string path = new CodexRollouts(Thread)
            .Raw("{\"\\uDC00\\uDC00\":\"" + CopilotSessionLogs.Canary + "\",\"type\":\"event_msg\",\"payload\":{}}")
            .Raw("{\"type\":\"\\uDC00\\uDC00\\uDC00\",\"payload\":{\"message\":\"" + CopilotSessionLogs.Canary + "\"}}")
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1")
            .Write(temp.Path);
        await File.WriteAllTextAsync(
            path,
            (await File.ReadAllTextAsync(path)).Replace(
                "\"usage\":{\"input_tokens\":",
                "\"usage\":{\"\\uDC00\\uDC00\":1,\"input_tokens\":",
                StringComparison.Ordinal));

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(
            (1L, 0L, 0L, 2L),
            (result.RecordsParsed, result.UnsupportedUsageShapes, result.MalformedLines, result.SkippedEvents));
        Assert.Equal("resp_1", Assert.Single(result.Records).RecordKey);
        Assert.Empty(result.Diagnostics);
        AssertNoCanary(result);
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);

    private static void AssertNoCanary(SessionLogReadResult result) =>
        Assert.DoesNotContain(CopilotSessionLogs.Canary, JsonSerializer.Serialize(result), StringComparison.Ordinal);
}
