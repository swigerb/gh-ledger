using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Storage.Import;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class McpToolNameImportTests
{
    private const string LedgerHash =
        "fe14010b4fe83303852f0467c919ef9a7ca089b91e96e3aad7d426dd87079297/eb20c46435d60871da0eb97ce23417c04835e272725c59a3a649c7ce8e521d97";

    private const string ExecJs =
        "10be6beef496b89dae799c37bd04e5eba2a6264a0a4d605f5661b50d7dc4d1ea/8453dc19fda259c463d7aaffb07becf26b7ce2cfc690a2b0a957e201653dc85a";

    private const string SessionId = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ML1_AnMcpToolStart_TeachesItsName()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId).Usage("u1", S1, numToolCalls: 1);
        Mcp(log, "t1").ToolComplete("t1", true).Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(
            [new LearnedToolName(ExecJs, "computer-use-exec_js")],
            result.LearnedToolNames);
        Assert.Equal("computer-use-exec_js", Assert.Single(result.ToolCalls).ToolName);
        CopilotRecordTests.AssertNoCanary(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ML2_OnlyThreeValidStringsTeachAName(int shape)
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new(SessionId);
        switch (shape)
        {
            case 0:
                log.Event(
                    "tool.execution_start",
                    new JsonObject
                    {
                        ["toolCallId"] = "t1",
                        ["mcpServerName"] = "computer-use",
                        ["mcpToolName"] = "exec_js",
                    });
                break;
            case 1:
                log.Event(
                    "tool.execution_start",
                    new JsonObject
                    {
                        ["toolCallId"] = "t1",
                        ["toolName"] = "computer-use-exec_js",
                        ["mcpServerName"] = "computer-use",
                    });
                break;
            case 2:
                log.Event(
                    "tool.execution_start",
                    new JsonObject
                    {
                        ["toolCallId"] = "t1",
                        ["toolName"] = "computer-use-exec_js",
                        ["mcpServerName"] = 7,
                        ["mcpToolName"] = "exec_js",
                    });
                break;
            case 3:
                log.Event(
                    "tool.execution_start",
                    new JsonObject
                    {
                        ["toolCallId"] = "t1",
                        ["toolName"] = "computer-use-exec_js",
                        ["mcpServerName"] = "computer-use",
                        ["mcpToolName"] = "",
                    });
                break;
            case 4:
                log.Event(
                    "tool.execution_start",
                    new JsonObject
                    {
                        ["toolCallId"] = "t1",
                        ["toolName"] = "computer-use-exec_js",
                        ["mcpServerName"] = new string('s', 257),
                        ["mcpToolName"] = "exec_js",
                    });
                break;
            default:
                log.Raw(
                    """{"type":"tool.execution_start","data":{"toolCallId":"t1","toolName":"computer-use-exec_js","mcpServerName":"computer-use","mcpServerName":"other","mcpToolName":"exec_js"},"id":"00000000-0000-4000-8000-0000000000ff","timestamp":"2026-11-20T12:00:01.000Z","parentId":null}""");
                break;
        }

        log.Write(temp.Path);
        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Empty(result.LearnedToolNames);
    }

    [Fact]
    public async Task ML3_EachNameIsLearnedOnce_InOrder()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new(SessionId);
        Mcp(log, "t1");
        Mcp(log, "t2");
        log.ToolStart(
                "t3",
                "ledger-get_branch_ledger",
                mcpServerName: "ledger",
                mcpToolName: "get_branch_ledger")
            .ToolStart(
                "t4",
                "computer-use-exec_js-v2",
                mcpServerName: "computer-use",
                mcpToolName: "exec_js")
            .ToolStart("t5", "powershell")
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal(
            [
                new LearnedToolName(ExecJs, "computer-use-exec_js"),
                new LearnedToolName(ExecJs, "computer-use-exec_js-v2"),
                new LearnedToolName(LedgerHash, "ledger-get_branch_ledger"),
            ],
            result.LearnedToolNames);
    }

    [Fact]
    public async Task ML4_AStartWithoutACallIdOrCompletion_StillTeachesItsName()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Event(
                "tool.execution_start",
                new JsonObject
                {
                    ["toolName"] = "computer-use-exec_js",
                    ["mcpServerName"] = "computer-use",
                    ["mcpToolName"] = "exec_js",
                })
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal(
            [new LearnedToolName(ExecJs, "computer-use-exec_js")],
            result.LearnedToolNames);
        Assert.Empty(result.ToolCalls);
    }

    [Theory]
    [InlineData("1.0.102", SessionLogStatus.AggregateOnly)]
    [InlineData("1.0.116", SessionLogStatus.UnsupportedProducerVersion)]
    public async Task ML5_ALogThatIsntRead_TeachesNothing(string version, SessionLogStatus status)
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new(SessionId, version);
        Mcp(log, "t1").Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal(status, result.Status);
        Assert.Empty(result.LearnedToolNames);
    }

    [Fact]
    public async Task ML6_ImportRecordsTheNamesOfLiveSessionsAndOldToolCalls()
    {
        using TempDirectory temp = new();
        string database = Database(temp);
        SqliteLedgerStore store = new(database);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("s-live", S1)]);
        CopilotSessionLogs live = new CopilotSessionLogs("s-live").Usage("u1", S1, numToolCalls: 1);
        Mcp(live, "t1").ToolComplete("t1", true).Write(temp.Path);
        new CopilotSessionLogs(
                "s-old",
                start: new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero))
            .Usage("u2", S2, numToolCalls: 1)
            .ToolStart(
                "t2",
                "ledger-get_branch_ledger",
                mcpServerName: "ledger",
                mcpToolName: "get_branch_ledger")
            .ToolComplete("t2", true)
            .Write(temp.Path);

        ImportSourceReport report = Copilot(await ImportAsync(database, temp.Path));

        Assert.Equal((1, 1, 0), (report.OverlapSkipped, report.Recorded, report.ToolLinksRecorded));
        Assert.Equal(
            [ExecJs + "|computer-use-exec_js", LedgerHash + "|ledger-get_branch_ledger"],
            await Names(database));
        Assert.Equal(0L, await LedgerSql.ScalarAsync(database, "SELECT COUNT(*) FROM span_links;"));
    }

    [Fact]
    public async Task ML7_AScopedImport_LearnsFromLogsOutOfScope()
    {
        using TempDirectory temp = new();
        string database = Database(temp);
        string api = Path.Combine(temp.Path, "api");
        CopilotSessionLogs log = new CopilotSessionLogs(
                "s-elsewhere",
                context: CopilotSessionLogs.Context(api, api, "main", "contoso/api", GitFixture.Sha1))
            .Usage("u1", S1);
        Mcp(log, "t1").Write(temp.Path);

        ImportSourceReport report = Copilot(await ImportAsync(
            database,
            temp.Path,
            request => request with
            {
                RepositoryRoot = Path.Combine(temp.Path, "repo"),
                RepositorySlug = "swigerb/copilot-ledger",
            }));

        Assert.Equal((1, 0), (report.RecordsOutOfScope, report.Recorded));
        Assert.Equal([ExecJs + "|computer-use-exec_js"], await Names(database));
    }

    [Fact]
    public async Task ML8_ADryRun_LearnsNothing()
    {
        using TempDirectory temp = new();
        string database = Database(temp);
        await new SqliteLedgerStore(database).InitializeAsync();
        Mcp(new CopilotSessionLogs("s-a"), "t1").Write(temp.Path);

        await ImportAsync(database, temp.Path, request => request with { DryRun = true });
        Assert.Empty(await Names(database));

        await ImportAsync(database, temp.Path);
        Assert.Equal([ExecJs + "|computer-use-exec_js"], await Names(database));
    }

    [Fact]
    public async Task ML9_TheSmallestNameWins_WhateverTheOrder()
    {
        using TempDirectory temp = new();
        string first = Path.Combine(temp.Path, "first");
        string second = Path.Combine(temp.Path, "second");
        string firstHome = Path.Combine(first, "home");
        string secondHome = Path.Combine(second, "home");
        string firstDatabase = Path.Combine(first, "ledger.db");
        string secondDatabase = Path.Combine(second, "ledger.db");
        WriteNames(firstHome, "computer-use-exec_js-v2", "computer-use-exec_js");
        WriteNames(secondHome, "computer-use-exec_js", "computer-use-exec_js-v2");

        await ImportAsync(firstDatabase, firstHome);
        await ImportAsync(secondDatabase, secondHome);

        Assert.Equal([ExecJs + "|computer-use-exec_js"], await Names(firstDatabase));
        Assert.Equal([ExecJs + "|computer-use-exec_js"], await Names(secondDatabase));
    }

    [Fact]
    public async Task ML10_ALogOfAnEarlierRevision_IsReadOnceMore()
    {
        using TempDirectory temp = new();
        string database = Database(temp);
        CopilotSessionLogs log = new CopilotSessionLogs("s-a").Usage("u1", S1, numToolCalls: 1);
        Mcp(log, "t1").ToolComplete("t1", true).Write(temp.Path);

        ImportSourceReport first = Copilot(await ImportAsync(database, temp.Path));
        Assert.Equal((1, 1), (first.Recorded, first.ToolLinksRecorded));

        await LedgerSql.ExecuteAsync(
            database,
            "DELETE FROM mcp_tool_names; UPDATE import_sessions SET import_revision = 1;");
        ImportSourceReport second = Copilot(await ImportAsync(database, temp.Path));

        Assert.Equal(
            (0, 1, 0),
            (second.SessionsSkippedUnchanged, second.Duplicates, second.Recorded));
        Assert.Equal([ExecJs + "|computer-use-exec_js"], await Names(database));
        SqliteLedgerStore store = new(database);
        await store.InitializeAsync();
        Assert.Equal(
            2,
            (await store.GetImportSessionAsync(ImportSourceKind.Copilot, "s-a"))!.ImportRevision);

        ImportSourceReport third = Copilot(await ImportAsync(database, temp.Path));
        Assert.Equal((1, 0), (third.SessionsSkippedUnchanged, third.Recorded));
    }

    private static CopilotSessionLogs Mcp(CopilotSessionLogs log, string id) =>
        log.ToolStart(
            id,
            "computer-use-exec_js",
            mcpServerName: "computer-use",
            mcpToolName: "exec_js");

    private static void WriteNames(string home, string first, string second)
    {
        new CopilotSessionLogs("s-a")
            .ToolStart("t1", first, mcpServerName: "computer-use", mcpToolName: "exec_js")
            .Write(home);
        new CopilotSessionLogs("s-b")
            .ToolStart("t2", second, mcpServerName: "computer-use", mcpToolName: "exec_js")
            .Write(home);
    }

    private static Task<ImportReport> ImportAsync(
        string database,
        string home,
        Func<ImportRequest, ImportRequest>? change = null)
    {
        ImportRequest request = new()
        {
            DatabasePath = database,
            Sources = [new ImportSourceRequest(new CopilotSessionLogSource(), home)],
        };
        return new LedgerImporter(JsonRateCardCalculator.LoadEmbedded(), new ManualTimeProvider(Now))
            .RunAsync(change is null ? request : change(request));
    }

    private static string Database(TempDirectory temp) => Path.Combine(temp.Path, "ledger.db");

    private static Task<IReadOnlyList<string>> Names(string database) =>
        LedgerSql.RowsAsync(
            database,
            "SELECT hashed_name || '|' || tool_name FROM mcp_tool_names ORDER BY hashed_name;");

    private static ImportSourceReport Copilot(ImportReport report) =>
        Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Copilot);
}
