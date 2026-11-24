using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class CopilotRecordTests
{
    private const string SessionId = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static readonly CopilotSessionLogSource Source = new();

    public static TheoryData<int, string> Shapes => new()
    {
        { 0, "data.usage.accounting.usageId" },
        { 1, "data.usage.serviceRequestId" },
        { 2, "data.usage.inputTokens" },
        { 3, "data.usage.outputTokens" },
        { 4, "data.usage.inputTokens" },
        { 5, "data.usage.provider.kind" },
        { 6, "data.usage.model" },
        { 7, "data.usage.model" },
        { 8, "data.usage.isByok" },
    };

    [Fact]
    public async Task CU1_AMainAgentRecordMapsEveryField()
    {
        using TempDirectory temp = new();
        string root = Path.Combine(temp.Path, "webapp");
        string cwd = Path.Combine(root, "src");
        CopilotSessionLogs log = new CopilotSessionLogs(
                SessionId,
                "1.0.103",
                context: CopilotSessionLogs.Context(cwd, root + Path.DirectorySeparatorChar, "main", "contoso/webapp", Sha.ToUpperInvariant()))
            .UserMessage()
            .Usage(
                "msg_01SyntheticA",
                S1.ToUpperInvariant(),
                input: 19_484,
                output: 321,
                cacheWrite: 19_474,
                duration: 3_372,
                numToolCalls: 1);
        log.Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(1, result.RecordsParsed);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            new ImportedUsageRecord
            {
                Source = ImportSourceKind.Copilot,
                Kind = ImportedRecordKind.Usage,
                SessionId = SessionId,
                RecordKey = "msg_01SyntheticA",
                TurnKey = "msg_01SyntheticA",
                ServiceRequestId = S1,
                StartTime = log.Time.AddMilliseconds(-3_372),
                EndTime = log.Time,
                RequestModel = "claude-haiku-4.5",
                ResponseModel = null,
                ProviderName = "github",
                BillingSource = BillingSource.Endpoint,
                AgentName = null,
                InputTokens = 19_484,
                OutputTokens = 321,
                CacheReadInputTokens = 0,
                CacheWriteInputTokens = 19_474,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Reported,
                ToolCallCount = 1,
                Git = new ImportedGitContext
                {
                    WorkingDirectory = cwd,
                    RepositoryRoot = root,
                    RepositorySlug = "contoso/webapp",
                    Branch = "main",
                    HeadCommitSha = Sha,
                },
                LineNumber = 3,
            },
            Assert.Single(result.Records));
    }

    [Fact]
    public async Task CU2_TheResponseModelIsKeptAsLogged()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Usage("msg_01SyntheticA", S1, responseModel: "claude-haiku-4-5-20251001")
            .Write(temp.Path);

        ImportedUsageRecord record = Assert.Single((await ReadAsync(temp.Path)).Records);

        Assert.Equal("claude-haiku-4.5", record.RequestModel);
        Assert.Equal("claude-haiku-4-5-20251001", record.ResponseModel);
    }

    [Theory]
    [InlineData(0, CacheCounterState.Absent)]
    [InlineData(500, CacheCounterState.Reported)]
    public async Task CU3_UnreportedCacheDetailsAreAbsentUnlessACounterIsNonzero(long cacheRead, CacheCounterState state)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg_01SyntheticA", S1, cacheRead: cacheRead, cacheDetailsReported: false)
            .Write(temp.Path);

        ImportedUsageRecord record = Assert.Single((await ReadAsync(temp.Path)).Records);

        Assert.Equal(state, record.CacheReadState);
        Assert.Equal(CacheCounterState.Absent, record.CacheWriteState);
        Assert.Equal(cacheRead, record.CacheReadInputTokens);
        Assert.Equal(0, record.CacheWriteInputTokens);
    }

    [Theory]
    [InlineData("1.0.103")]
    [InlineData("1.0.114")]
    public async Task CU4_SubagentRecordsAreAttributedToTheirAgent(string version)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, version)
            .Usage("msg_main", S1)
            .ToolStart("toolu_task", "task")
            .SubagentStarted("agent-1", "explore", "toolu_task")
            .Usage(
                "msg_sub",
                S2,
                initiator: "agent",
                agentId: "agent-1",
                parentToolCallId: version == "1.0.103" ? "toolu_task" : null)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal([null, "explore"], result.Records.Select(record => record.AgentName));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task CU5_AnUnknownSubagentHasNoNameAndAnInformationalDiagnostic()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg_sub", S1, initiator: "agent", agentId: "agent-unknown")
            .Usage("msg_old", S2, initiator: "agent", parentToolCallId: "toolu_unknown")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal([null, null], result.Records.Select(record => record.AgentName));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.UnknownSubagent, diagnostic.Code);
        Assert.Equal(ImportEventKind.UsageRecord, diagnostic.Event);
        Assert.Equal(2, diagnostic.Count);
        Assert.Equal(2, diagnostic.FirstLine);
        Assert.Equal(0, result.UnsupportedUsageShapes);
    }

    [Fact]
    public async Task CU6_AResumeChangesTheContextOfLaterRecordsOnly()
    {
        using TempDirectory temp = new();
        string root = Path.Combine(temp.Path, "webapp");
        new CopilotSessionLogs(
                SessionId,
                context: CopilotSessionLogs.Context(root, root, "main", "contoso/webapp", Sha))
            .Usage("msg_1", S1)
            .Resume(null)
            .Usage("msg_2", S2)
            .Resume(CopilotSessionLogs.Context(root, root, "feature/search", "contoso/webapp", Sha))
            .Usage("msg_3", S3)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(["main", "main", "feature/search"], result.Records.Select(record => record.Git!.Branch));
        Assert.All(result.Records, record => Assert.Equal(root, record.Git!.RepositoryRoot));
    }

    [Fact]
    public async Task CU7_ByokRecordsAreBilledByTheirProvider()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg_byok", null, model: "gpt-6.1-sol", providerType: "OpenAI")
            .Write(temp.Path);

        ImportedUsageRecord record = Assert.Single((await ReadAsync(temp.Path)).Records);

        Assert.Equal(BillingSource.Provider, record.BillingSource);
        Assert.Equal("openai", record.ProviderName);
        Assert.Null(record.ServiceRequestId);
        Assert.Equal("gpt-6.1-sol", record.RequestModel);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task CU8_EachUnsupportedShapeNamesItsField(int caseNumber, string field)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg_ok", S1)
            .Usage(
                "msg_bad",
                S2,
                change: usage =>
                {
                    switch (caseNumber)
                    {
                        case 0:
                            usage["accounting"]!.AsObject().Remove("usageId");
                            break;
                        case 1:
                            usage.Remove("serviceRequestId");
                            break;
                        case 2:
                            usage["inputTokens"] = JsonNode.Parse("1.0");
                            break;
                        case 3:
                            usage["outputTokens"] = -1;
                            break;
                        case 4:
                            usage["cacheReadTokens"] = 1_001;
                            break;
                        case 5:
                            usage["provider"]!["kind"] = "other";
                            break;
                        case 6:
                            usage.Remove("model");
                            break;
                        case 7:
                            usage["model"] = new string('m', 257);
                            break;
                        default:
                            usage.Remove("isByok");
                            break;
                    }
                })
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(2, result.RecordsParsed);
        Assert.Equal(1, result.UnsupportedUsageShapes);
        Assert.Equal(["msg_ok"], result.Records.Select(record => record.RecordKey));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.UnsupportedUsageShape, diagnostic.Code);
        Assert.Equal(ImportEventKind.UsageRecord, diagnostic.Event);
        Assert.Equal(field, diagnostic.Field);
        Assert.Equal(3, diagnostic.FirstLine);
    }

    [Fact]
    public async Task CU9_UserPromptsStartTracesThatOtherRecordsJoin()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg_before", S1, initiator: "agent")
            .Usage("msg_user_1", S2)
            .SubagentStarted("agent-1", "explore", "toolu_task")
            .Usage("msg_sub", S3, initiator: "user", agentId: "agent-1")
            .Usage("msg_agent", "44444444-4444-4444-8444-444444444444", initiator: "agent")
            .Usage("msg_user_2", "55555555-5555-4555-8555-555555555555")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(
            [null, "msg_user_1", "msg_user_1", "msg_user_1", "msg_user_2"],
            result.Records.Select(record => record.TurnKey));
    }

    [Fact]
    public async Task CU10_ToolCallsBelongToTheirAgentsLatestRecord()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId)
            .ToolStart("toolu_0", "bash")
            .ToolComplete("toolu_0", true)
            .Usage("msg_main", S1)
            .ToolStart("toolu_1", "bash")
            .ToolComplete("toolu_1", false, exitCode: 2)
            .ToolStart("toolu_2", "view")
            .ToolComplete("toolu_9", true)
            .SubagentStarted("agent-1", "explore", "toolu_task")
            .Usage("msg_sub", S2, initiator: "agent", agentId: "agent-1")
            .ToolStart("toolu_4", "grep", agentId: "agent-1");
        DateTimeOffset grepStart = log.Time;
        log.ToolComplete("toolu_4", true, agentId: "agent-1").Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Collection(
            result.ToolCalls,
            call =>
            {
                Assert.Equal("msg_main", call.OwnerRecordKey);
                Assert.Equal("toolu_1", call.ToolCallId);
                Assert.Equal("bash", call.ToolName);
                Assert.Null(call.AgentName);
                Assert.False(call.Success);
                Assert.Equal(2, call.ExitCode);
            },
            call =>
            {
                Assert.Equal("msg_sub", call.OwnerRecordKey);
                Assert.Equal("toolu_4", call.ToolCallId);
                Assert.Equal("grep", call.ToolName);
                Assert.Equal("explore", call.AgentName);
                Assert.True(call.Success);
                Assert.Null(call.ExitCode);
                Assert.Equal(grepStart, call.StartTime);
                Assert.Equal(SessionId, call.SessionId);
            });
    }

    [Fact]
    public async Task CU11_SessionTotalsNeverBecomeRecords()
    {
        using TempDirectory temp = new();
        JsonObject totals = new()
        {
            ["agentMetrics"] = new JsonObject
            {
                ["main"] = new JsonObject { ["inputTokens"] = 5_000, ["model"] = "claude-haiku-4.5" },
            },
            ["codeChanges"] = new JsonObject { ["filesModified"] = new JsonArray(CopilotSessionLogs.Canary) },
        };
        new CopilotSessionLogs(SessionId)
            .Usage("msg_1", S1)
            .Event("session.usage_checkpoint", totals.DeepClone().AsObject())
            .Event("subagent.completed", new JsonObject { ["inputTokens"] = 5_000, ["agentName"] = "explore" })
            .Event("session.shutdown", totals.DeepClone().AsObject())
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(["msg_1"], result.Records.Select(record => record.RecordKey));
        Assert.Equal(3, result.SkippedEvents);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CU12_NoContentReachesTheResult()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .UserMessage()
            .Event("assistant.message", new JsonObject { ["content"] = CopilotSessionLogs.Canary })
            .Usage("msg_main", S1, numToolCalls: 1)
            .ToolStart("toolu_1", "bash")
            .ToolComplete("toolu_1", false, exitCode: 1)
            .SubagentStarted("agent-1", "explore", "toolu_task")
            .Usage("msg_sub", S2, initiator: "agent", agentId: "agent-1")
            .Usage("msg_byok", null, providerType: "anthropic")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(3, result.Records.Count);
        Assert.Single(result.ToolCalls);
        AssertNoCanary(result);
    }

    // A call that would have started before the first instant can't be recorded; it's an unsupported shape, not an error.
    [Fact]
    public async Task CU13_ARecordThatWouldStartBeforeTheFirstInstantIsUnsupported()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, start: DateTimeOffset.MinValue)
            .Usage("msg_1", S1, duration: 2_000)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal((1, 1), (result.RecordsParsed, result.UnsupportedUsageShapes));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            (ImportDiagnosticCode.UnsupportedUsageShape, "data.usage.duration"),
            (diagnostic.Code, diagnostic.Field));
    }

    // A credential in the recorded repository's query or fragment is dropped with them, and a slug with one is no slug.
    [Theory]
    [InlineData("https://user:" + CopilotSessionLogs.Canary + "@git.example/o/r.git?token=" + CopilotSessionLogs.Canary + "#" + CopilotSessionLogs.Canary, "o/r")]
    [InlineData("git@git.example:o/r.git?token=" + CopilotSessionLogs.Canary, "o/r")]
    [InlineData("o/r?token=" + CopilotSessionLogs.Canary, null)]
    public async Task CU14_AQueryOrFragmentOfTheRepositoryIsNeverKept(string repository, string? slug)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(
                SessionId,
                context: new JsonObject
                {
                    ["cwd"] = CopilotSessionLogs.DefaultWorkingDirectory,
                    ["gitRoot"] = CopilotSessionLogs.DefaultWorkingDirectory,
                    ["repository"] = repository,
                })
            .Usage("msg_1", S1)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(slug, Assert.Single(result.Records).Git!.RepositorySlug);
        AssertNoCanary(result);
    }

    internal static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);

    internal static void AssertNoCanary(SessionLogReadResult result) =>
        Assert.DoesNotContain(CopilotSessionLogs.Canary, JsonSerializer.Serialize(result), StringComparison.Ordinal);
}
