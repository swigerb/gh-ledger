using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class CopilotCompactionTests
{
    private const string SessionId = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S9 = "99999999-9999-4999-8999-999999999999";

    public static TheoryData<int, string> Shapes => new()
    {
        { 1, "data.compactionTokensUsed.requestCount" },
        { 2, "data.compactionTokensUsed.requestCount" },
        { 3, "data.serviceRequestId" },
        { 4, "data.serviceRequestId" },
        { 5, "data.compactionTokensUsed.outputTokens" },
        { 6, "data.compactionTokensUsed.inputTokens" },
        { 7, "data.compactionTokensUsed.model" },
        { 8, "data.compactionTokensUsed.provider.kind" },
    };

    [Fact]
    public async Task CC1_ACompactionIsTheCallThatSummarized()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId, "1.0.114")
            .Usage("msg_user", S1)
            .Compaction(S9.ToUpperInvariant());
        log.Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal(2, result.RecordsParsed);
        ImportedUsageRecord record = result.Records[1];
        Assert.Equal(ImportedRecordKind.Compaction, record.Kind);
        Assert.Equal(S9, record.RecordKey);
        Assert.Equal(S9, record.ServiceRequestId);
        Assert.Equal("msg_user", record.TurnKey);
        Assert.Equal(log.Time.AddMilliseconds(-891), record.StartTime);
        Assert.Equal(log.Time, record.EndTime);
        Assert.Equal("claude-haiku-4.5", record.RequestModel);
        Assert.Null(record.ResponseModel);
        Assert.Equal("github", record.ProviderName);
        Assert.Equal(BillingSource.Endpoint, record.BillingSource);
        Assert.Null(record.AgentName);
        Assert.Equal(112_223, record.InputTokens);
        Assert.Equal(18, record.OutputTokens);
        Assert.Equal(18_967, record.CacheReadInputTokens);
        Assert.Equal(93_251, record.CacheWriteInputTokens);
        Assert.Equal(CacheCounterState.Reported, record.CacheReadState);
        Assert.Equal(CacheCounterState.Reported, record.CacheWriteState);
        Assert.Equal(0L, record.ToolCallCount);
        Assert.Equal(3, record.LineNumber);
    }

    [Fact]
    public async Task CC2_ASubagentsCompactionIsAttributedByItsAgentId()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .SubagentStarted("agent-1", "explore", "toolu_task")
            .Compaction(S9, agentId: "agent-1")
            .Write(temp.Path);

        ImportedUsageRecord record = Assert.Single((await CopilotRecordTests.ReadAsync(temp.Path)).Records);

        Assert.Equal("explore", record.AgentName);
        Assert.Null(record.TurnKey);
    }

    [Fact]
    public async Task CC3_ACompactionWithoutTokensUsedIsASkippedEvent()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114").Compaction(S9, tokensUsed: false).Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal(0, result.RecordsParsed);
        Assert.Equal(1, result.SkippedEvents);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task CC4_EachUnsupportedCompactionNamesItsField(int caseNumber, string field)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Compaction(
                caseNumber == 3 ? null : caseNumber == 4 ? "not-a-uuid" : S9,
                change: data =>
                {
                    JsonObject used = data["compactionTokensUsed"]!.AsObject();
                    switch (caseNumber)
                    {
                        case 1:
                            used["requestCount"] = 2;
                            break;
                        case 2:
                            used.Remove("requestCount");
                            break;
                        case 3:
                            data.Remove("serviceRequestId");
                            break;
                        case 5:
                            used["outputTokens"] = -1;
                            break;
                        case 6:
                            used["cacheWriteTokens"] = 100_000;
                            break;
                        case 7:
                            used.Remove("model");
                            break;
                        case 8:
                            used["provider"]!["kind"] = "byok";
                            break;
                    }
                })
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal(1, result.RecordsParsed);
        Assert.Equal(1, result.UnsupportedUsageShapes);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.UnsupportedUsageShape, diagnostic.Code);
        Assert.Equal(ImportEventKind.CompactionComplete, diagnostic.Event);
        Assert.Equal(field, diagnostic.Field);
    }

    [Fact]
    public async Task CC5_ACompactionDoesntTakeOverTheToolCallsOfTheRecordBefore()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Usage("msg_user", S1)
            .Compaction(S9)
            .ToolStart("toolu_1", "view")
            .ToolComplete("toolu_1", true)
            .Write(temp.Path);

        ImportedToolCall call = Assert.Single((await CopilotRecordTests.ReadAsync(temp.Path)).ToolCalls);

        Assert.Equal("msg_user", call.OwnerRecordKey);
    }

    [Fact]
    public async Task CC6_TheSummaryAndRequestIdsAreNeverRead()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Compaction(
                S9,
                change: data =>
                {
                    data["error"] = new JsonObject { ["message"] = CopilotSessionLogs.Canary };
                    data["failureReason"] = CopilotSessionLogs.Canary;
                    data["failureDiagnostics"] = new JsonArray(CopilotSessionLogs.Canary);
                    data["responsesReasoning"] = CopilotSessionLogs.Canary;
                })
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Single(result.Records);
        CopilotRecordTests.AssertNoCanary(result);
    }

    // A compaction that would have started before the first instant is an unsupported shape, not an error.
    [Fact]
    public async Task CC7_ACompactionThatWouldStartBeforeTheFirstInstantIsUnsupported()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114", start: DateTimeOffset.MinValue)
            .Compaction(S9, duration: 5_000)
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal((1, 1), (result.RecordsParsed, result.UnsupportedUsageShapes));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            (ImportDiagnosticCode.UnsupportedUsageShape, ImportEventKind.CompactionComplete, "data.compactionTokensUsed.duration"),
            (diagnostic.Code, diagnostic.Event, diagnostic.Field));
    }
}
