using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Tests.Import;

public sealed class SessionImportMapperTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";

    private static readonly ImportedUsageRecord Record = new()
    {
        Source = ImportSourceKind.Copilot,
        Kind = ImportedRecordKind.Usage,
        SessionId = "session-a",
        RecordKey = "msg_1",
        TurnKey = "msg_0",
        ServiceRequestId = S1,
        StartTime = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
        EndTime = new DateTimeOffset(2026, 11, 20, 12, 0, 3, TimeSpan.Zero),
        RequestModel = "claude-haiku-4.5",
        ResponseModel = "claude-haiku-4-5-20251001",
        ProviderName = "github",
        BillingSource = BillingSource.Endpoint,
        AgentName = "explore",
        InputTokens = 1_000,
        OutputTokens = 100,
        CacheReadInputTokens = 600,
        CacheWriteInputTokens = 200,
        CacheReadState = CacheCounterState.Reported,
        CacheWriteState = CacheCounterState.Reported,
        ToolCallCount = 2,
        Git = new ImportedGitContext { RepositorySlug = "contoso/webapp", Branch = "main" },
        LineNumber = 7,
    };

    [Fact]
    public void MP1_ARecordIsAChatSpanOfItsSession()
    {
        Assert.Equal(
            new NormalizedGenAiSpan
            {
                TraceId = ImportIds.TraceId(ImportSourceKind.Copilot, "session-a", "msg_0", ImportedRecordKind.Usage, "msg_1"),
                SpanId = ImportIds.SpanId(ImportSourceKind.Copilot, "session-a", ImportedRecordKind.Usage, "msg_1"),
                ParentSpanId = null,
                OperationName = "chat",
                StartTime = Record.StartTime,
                EndTime = Record.EndTime,
                ProviderName = "github",
                RequestModel = "claude-haiku-4.5",
                ResponseModel = "claude-haiku-4-5-20251001",
                AgentName = "explore",
                ConversationId = "session-a",
                ServerAddress = null,
                ServerPort = null,
                WorkingDirectory = null,
                InputTokens = 1_000,
                OutputTokens = 100,
                CacheReadInputTokens = 600,
                CacheWriteInputTokens = 200,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Reported,
                ServiceRequestId = S1,
                BillingSource = BillingSource.Endpoint,
            },
            SessionImportMapper.ToSpan(Record));
    }

    [Theory]
    [InlineData(true, null, 0)]
    [InlineData(false, 2L, 2)]
    public void MP2_AToolCallIsAnExecuteToolLinkInItsOwnersTrace(bool success, long? exitCode, int status)
    {
        ImportedToolCall call = new()
        {
            SessionId = "session-a",
            OwnerRecordKey = "msg_1",
            ToolCallId = "toolu_1",
            ToolName = "bash",
            AgentName = "explore",
            StartTime = Record.EndTime,
            Success = success,
            ExitCode = exitCode,
        };

        Assert.Equal(
            new GenAiSpanLink
            {
                TraceId = SessionImportMapper.ToSpan(Record).TraceId,
                SpanId = ImportIds.ToolSpanId(ImportSourceKind.Copilot, "session-a", "toolu_1"),
                ParentSpanId = null,
                OperationName = "execute_tool",
                AgentName = "explore",
                ConversationId = "session-a",
                StartTime = Record.EndTime,
                ToolName = "bash",
                ToolExitCode = exitCode,
                StatusCode = status,
            },
            SessionImportMapper.ToToolLink(call, Record));
    }
}
