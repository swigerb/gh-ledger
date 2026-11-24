using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage.Import;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class CopilotToolLinkTests
{
    private const string SessionId = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TL1_ToolCallsUseTheAssistantApiCallIdOwner()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = Tl1();
        log.Write(temp.Path);

        SessionLogReadResult read = await CopilotRecordTests.ReadAsync(temp.Path);
        Assert.Equal(["api-A", "api-A"], read.ToolCalls.Select(call => call.OwnerRecordKey));
        Assert.Equal(0, read.ToolCallsFallback);

        ImportReport report = await ImportAsync(temp, log);
        Assert.Equal((2, 0), (Copilot(report).ToolLinksRecorded, Copilot(report).ToolLinksFallback));
        Assert.Equal(
            ["1|1|1", "1|1|1"],
            await LedgerSql.RowsAsync(
                Database(temp),
                """
                SELECT l.owner_span_row_id, s.id, l.trace_id = s.trace_id
                FROM span_links l
                JOIN spans s ON s.id = l.owner_span_row_id
                ORDER BY l.tool_name;
                """));
        Assert.DoesNotContain(
            "2",
            await LedgerSql.RowsAsync(Database(temp), "SELECT owner_span_row_id FROM span_links;"));
    }

    [Fact]
    public async Task TL2_AnUnknownParentUsesTheFallbackOwner()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId)
            .AssistantMessage("api-A");
        string messageA = log.LastId;
        log.Usage("api-A", S1, numToolCalls: 1)
            .AssistantMessage("api-B")
            .Usage("api-B", S2)
            .ToolStart("t1", "bash", parentId: messageA + "-unknown")
            .ToolComplete("t1", true)
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal("api-B", Assert.Single(result.ToolCalls).OwnerRecordKey);
        Assert.Equal(1, result.ToolCallsFallback);
    }

    [Fact]
    public async Task TL3_AChainThroughAnUnknownEventUsesTheFallbackOwner()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId)
            .AssistantMessage("api-A");
        string message = log.LastId;
        log.Usage("msg-A", S1, numToolCalls: 1)
            .Event("hook.start", new JsonObject(), parentId: message);
        string hook = log.LastId;
        log.ToolStart("t1", "bash", parentId: hook)
            .ToolComplete("t1", true)
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal("msg-A", Assert.Single(result.ToolCalls).OwnerRecordKey);
        Assert.Equal(1, result.ToolCallsFallback);
    }

    [Fact]
    public async Task TL4_AnUnmatchedApiCallIdUsesTheFallbackOwner()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId)
            .AssistantMessage("api-missing");
        string message = log.LastId;
        log.Usage("msg-A", S1, numToolCalls: 1)
            .ToolStart("t1", "bash", parentId: message)
            .ToolComplete("t1", true)
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal("msg-A", Assert.Single(result.ToolCalls).OwnerRecordKey);
        Assert.Equal(1, result.ToolCallsFallback);
    }

    [Fact]
    public async Task TL5_AUsageIdIsTheApiCallIdFallback()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId)
            .AssistantMessage("api-A");
        string message = log.LastId;
        log.Usage(
                "api-A",
                S1,
                numToolCalls: 1,
                change: usage => usage.Remove("apiCallId"))
            .ToolStart("t1", "bash", parentId: message)
            .ToolComplete("t1", true)
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal("api-A", Assert.Single(result.ToolCalls).OwnerRecordKey);
        Assert.Equal(0, result.ToolCallsFallback);
    }

    [Fact]
    public async Task TL6_AParentCycleUsesTheFallbackAfterTheHopLimit()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("api-A", S1, numToolCalls: 1)
            .ToolStart("t1", "bash", parentId: "00000000-0000-4000-8000-000000000004")
            .ToolComplete("t1", true, parentId: "00000000-0000-4000-8000-000000000003")
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal("api-A", Assert.Single(result.ToolCalls).OwnerRecordKey);
        Assert.Equal(1, result.ToolCallsFallback);
    }

    [Fact]
    public async Task TL7_AUsageAfterTheToolStillOwnsItAtFinish()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId)
            .AssistantMessage("api-late");
        string message = log.LastId;
        log.ToolStart("t1", "bash", parentId: message)
            .ToolComplete("t1", true)
            .Usage("api-late", S1, numToolCalls: 1)
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal("api-late", Assert.Single(result.ToolCalls).OwnerRecordKey);
        Assert.Equal(0, result.ToolCallsFallback);
    }

    [Fact]
    public async Task TL8_ExistingToolCallsStillUseTheirAgentFallback()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg-A", S1, numToolCalls: 1)
            .ToolStart("t1", "bash")
            .ToolComplete("t1", true)
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal("msg-A", Assert.Single(result.ToolCalls).OwnerRecordKey);
        Assert.Equal(1, result.ToolCallsFallback);
    }

    [Fact]
    public async Task TL9_ADryRunReportsResolvedToolLinks()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = Tl1();
        log.Write(Path.Combine(temp.Path, "copilot"));
        ImportReport report = await ImportAsync(temp, request => request with { DryRun = true });
        ImportSourceReport copilot = Copilot(report);

        Assert.True(report.DryRun);
        Assert.Equal((2, 0), (copilot.ToolLinksRecorded, copilot.ToolLinksFallback));
    }

    private static CopilotSessionLogs Tl1()
    {
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId).AssistantMessage("api-A");
        string messageA = log.LastId;
        log.Usage("api-A", S1, numToolCalls: 2).AssistantMessage("api-B").Usage("api-B", S2);
        log.ToolStart("t1", "bash", parentId: messageA);
        string startA = log.LastId;
        return log.ToolStart("t2", "bash", parentId: startA)
            .ToolComplete("t1", true, parentId: startA)
            .ToolComplete("t2", true);
    }

    private static Task<ImportReport> ImportAsync(TempDirectory temp, CopilotSessionLogs log) =>
        ImportAsync(temp, write: log);

    private static async Task<ImportReport> ImportAsync(
        TempDirectory temp,
        Func<ImportRequest, ImportRequest>? change = null,
        CopilotSessionLogs? write = null)
    {
        write?.Write(Path.Combine(temp.Path, "copilot"));
        ImportRequest request = new()
        {
            DatabasePath = Database(temp),
            Sources = [new ImportSourceRequest(new CopilotSessionLogSource(), Path.Combine(temp.Path, "copilot"))],
        };
        return await new LedgerImporter(JsonRateCardCalculator.LoadEmbedded(), new ManualTimeProvider(Now))
            .RunAsync(change is null ? request : change(request));
    }

    private static string Database(TempDirectory temp) => Path.Combine(temp.Path, "ledger.db");

    private static ImportSourceReport Copilot(ImportReport report) =>
        Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Copilot);
}
