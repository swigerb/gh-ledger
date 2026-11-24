using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Storage.Import;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Import;

public sealed class Copilot115Tests
{
    private const string SessionId = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task V115_1_The115ShapeRecordsChargesAndResolvesToolCalls()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));
        Log().Write(harness.Context.CopilotHome);

        CliRun run = await harness.RunAsync("import", "--all", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Error);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        JsonElement source = json.RootElement.GetProperty("sources")[0];
        Assert.Equal("complete", source.GetProperty("coverage").GetString());
        Assert.Equal(2, source.GetProperty("recorded").GetInt32());
        Assert.Equal(2, source.GetProperty("toolLinksRecorded").GetInt32());
        Assert.Equal(0, source.GetProperty("toolLinksFallback").GetInt32());
        Assert.Equal(0.059244145m, source.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(
            ["473886000|reported|null", "118555450|reported|null"],
            await LedgerSql.RowsAsync(
                harness.Context.DatabasePath,
                "SELECT cost_usd_pico, charge_source, imported_tool_call_count FROM spans ORDER BY id;"));
        Assert.Equal(
            ["1|1", "2|1"],
            await LedgerSql.RowsAsync(
                harness.Context.DatabasePath,
                "SELECT owner_span_row_id, COUNT(*) FROM span_links GROUP BY owner_span_row_id ORDER BY owner_span_row_id;"));
    }

    [Fact]
    public async Task V115_2_DryAndRealReportsAreIdentical()
    {
        using TempDirectory temp = new();
        Log().Write(CopilotHome(temp));

        ImportReport dry = await RunAsync(temp, request => request with { DryRun = true });
        ImportReport real = await RunAsync(temp);

        Assert.Equal(Copilot(dry), Copilot(real));
        Assert.Equal(dry.Sources, real.Sources);
        Assert.Equal(dry.Diagnostics, real.Diagnostics);
    }

    [Fact]
    public async Task V115_3_ALiveSpanSkipsOnlyItsMatching115Record()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Database(temp));
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live(SessionId, S1)]);
        SqliteConnection.ClearAllPools();
        Log().Write(CopilotHome(temp));

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal((1, 1, true), (report.Recorded, report.OverlapSkipped, report.Complete));
        Assert.Equal(
            ["otlp|effective", "import:copilot|effective"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT ingest_source, accounting_state FROM spans ORDER BY ingest_source DESC;"));
    }

    [Fact]
    public async Task V115_4_AMissingCopilotUsageFailsClosedAsIsByok()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.115")
            .Usage115("u1", S1, null)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal(1, result.UnsupportedUsageShapes);
        Assert.Equal("data.usage.isByok", Assert.Single(result.Diagnostics).Field);
    }

    [Fact]
    public async Task V115_5_AnotherProviderFailsClosedAsIsByok()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.115")
            .Usage115("u1", S1, 1, change: usage => usage["provider"]!["kind"] = "byok-custom")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal(1, result.UnsupportedUsageShapes);
        Assert.Equal("data.usage.isByok", Assert.Single(result.Diagnostics).Field);
    }

    [Fact]
    public async Task V115_6_The114CacheDetailsRuleIsUnchanged()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Usage("u1", S1, change: usage => usage.Remove("cacheDetailsReported"))
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal(1, result.UnsupportedUsageShapes);
        Assert.Equal("data.usage.cacheDetailsReported", Assert.Single(result.Diagnostics).Field);
    }

    [Theory]
    [InlineData("1.0.116")]
    [InlineData("1.0.116-0")]
    public async Task V115_7_LaterVersionsAreNotRead(string version)
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new(SessionId, version);
        log.Usage115("u1", S1, 1).Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.UnsupportedProducerVersion, result.Status);
        Assert.Empty(result.Records);
        Assert.Equal(0, result.RecordsParsed);

        using TempDirectory cliTemp = new();
        await using CliHarness harness = new(cliTemp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));
        log.Write(harness.Context.CopilotHome);

        CliRun run = await harness.RunAsync("import", "--all");

        Assert.Equal(2, run.ExitCode);
        Assert.Equal(
            $"Warning: Copilot session {SessionId}, line 1 (byte 0): Copilot {version} isn't supported (1.0.103 to 1.0.115); skipped\n",
            run.Error);
    }

    [Fact]
    public async Task V115_8_Unread115AndMessageFieldsKeepTheirCanariesPrivate()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs canaryLog = new(SessionId, "1.0.115");
        canaryLog.AssistantMessage("api-A");
        string message = canaryLog.LastId;
        canaryLog.Usage115(
                "api-A",
                S1,
                47_388_600,
                change: usage =>
                {
                    usage["modelDisplayName"] = CopilotSessionLogs.Canary;
                    usage["toolCounts"] = new JsonObject { [CopilotSessionLogs.Canary] = 1 };
                })
            .ToolStart("t1", "bash", parentId: message)
            .ToolComplete("t1", true)
            .Write(CopilotHome(temp));

        ImportReport report = await RunAsync(temp);
        SqliteConnection.ClearAllPools();

        Assert.DoesNotContain(
            CopilotSessionLogs.Canary,
            JsonSerializer.Serialize(report),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            CopilotSessionLogs.Canary,
            Encoding.UTF8.GetString(await FileRetry.ReadAllBytesAsync(Database(temp))),
            StringComparison.Ordinal);
    }

    private static CopilotSessionLogs Log()
    {
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId, "1.0.115").AssistantMessage("api-A");
        string messageA = log.LastId;
        log.Usage115("api-A", S1, 47_388_600)
            .AssistantMessage("api-B");
        string messageB = log.LastId;
        return log.Usage115("api-B", S2, 11_855_545)
            .ToolStart("t1", "bash", parentId: messageA)
            .ToolComplete("t1", true)
            .ToolStart("t2", "bash", parentId: messageB)
            .ToolComplete("t2", true);
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await new CopilotSessionLogSource().ReadAsync(
            Assert.Single(new CopilotSessionLogSource().Discover(home).Logs),
            CancellationToken.None);

    private static string CopilotHome(TempDirectory temp) => Path.Combine(temp.Path, "copilot");

    private static string Database(TempDirectory temp) => Path.Combine(temp.Path, "ledger.db");

    private static ImportSourceReport Copilot(ImportReport report) =>
        Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Copilot);

    private static Task<ImportReport> RunAsync(
        TempDirectory temp,
        Func<ImportRequest, ImportRequest>? change = null)
    {
        ImportRequest request = new()
        {
            DatabasePath = Database(temp),
            Sources = [new ImportSourceRequest(new CopilotSessionLogSource(), CopilotHome(temp))],
        };
        return new LedgerImporter(JsonRateCardCalculator.LoadEmbedded(), new ManualTimeProvider(Now))
            .RunAsync(change is null ? request : change(request));
    }
}
