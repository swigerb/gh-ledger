using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Import;

public sealed class CopilotCompactionShapeTests
{
    private const string SessionId = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string FailedSrid = "22222222-2222-4222-8222-222222222222";
    private const string AttemptId = "c0000000-0000-4000-8000-0000000000b1";
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CC1_BYOKAndFailedCompactionsHaveTheSevenClassifications()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        Cc1Log().Write(harness.Context.CopilotHome);

        CliRun first = await harness.RunAsync("import", "--all", "--json");
        CliRun second = await harness.RunAsync("import", "--all");

        Assert.Equal((0, 0), (first.ExitCode, second.ExitCode));
        using JsonDocument json = JsonDocument.Parse(first.Out);
        Assert.Equal("complete", json.RootElement.GetProperty("coverage").GetString());
        JsonElement source = json.RootElement.GetProperty("sources")[0];
        Assert.Equal(3, source.GetProperty("recordsParsed").GetInt32());
        Assert.Equal(3, source.GetProperty("recorded").GetInt32());
        Assert.Equal(1, source.GetProperty("skippedEvents").GetInt32());
        Assert.Equal(11.855545m, source.GetProperty("aiCredits").GetDecimal());
        Assert.Contains("Skipped, unchanged               1\n", second.Out);
        Assert.Equal(
            [
                "anthropic|other_provider|rate_card|0",
                "anthropic|other_provider|rate_card|0",
                "github|priced|reported|118555450000",
            ],
            await LedgerSql.RowsAsync(
                harness.Context.DatabasePath,
                """
                SELECT provider_name, pricing_status, charge_source, cost_usd_pico
                  FROM spans
                 ORDER BY start_time_unix_ms, provider_name;
                """));
    }

    [Fact]
    public async Task CC2_ASuccessfulGithubCompactionMissingInputTokensIsIncompleteAndRetried()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Compaction(
                FailedSrid,
                change: data => data["compactionTokensUsed"]!.AsObject().Remove("inputTokens"))
            .Write(harness.Context.CopilotHome);

        CliRun first = await harness.RunAsync("import", "--all");
        CliRun second = await harness.RunAsync("import", "--all");

        Assert.Equal((2, 2), (first.ExitCode, second.ExitCode));
        Assert.Contains("Unsupported usage shapes", first.Out);
        Assert.Contains("Coverage                  incomplete\n", first.Out);
        Assert.DoesNotContain("Skipped, unchanged               1", second.Out);
        Assert.Contains("unsupported data.compactionTokensUsed.inputTokens", second.Error);
    }

    [Fact]
    public async Task CC3_CompactionSuccessMustBeABoolean()
    {
        SessionLogReadResult result = await ReadAsync(
            log => log.Compaction(FailedSrid, change: data => data["success"] = "true"));

        AssertUnsupported(result, "data.success");
    }

    [Fact]
    public async Task CC4_AFailedCompactionWithAnIdButNoUsageIsUnsupported()
    {
        SessionLogReadResult result = await ReadAsync(
            log => log.Compaction(
                FailedSrid,
                change: data =>
                {
                    data["success"] = false;
                    data["compactionTokensUsed"]!.AsObject().Remove("copilotUsage");
                }));

        AssertUnsupported(result, "data.success");
    }

    [Fact]
    public async Task CC5_AFailedCompactionNeedsAValidReportedCharge()
    {
        SessionLogReadResult result = await ReadAsync(
            log => log.Compaction(
                FailedSrid,
                change: data =>
                {
                    data["success"] = false;
                    data["compactionTokensUsed"]!["copilotUsage"]!["totalNanoAiu"] = JsonNode.Parse("0.5");
                }));

        AssertUnsupported(result, "data.success");
    }

    [Fact]
    public async Task CC6_ASuccessfulCompactionWithoutAnIdButWithCopilotUsageIsUnsupported()
    {
        SessionLogReadResult result = await ReadAsync(log => log.Compaction(null));

        AssertUnsupported(result, "data.serviceRequestId");
    }

    [Fact]
    public async Task CC7_BYOKCompactionWithoutAnEarlierModelProviderHasANullProvider()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Compaction(null, change: data => MakeByok(data, AttemptId))
            .Write(harness.Context.CopilotHome);

        CliRun run = await harness.RunAsync("import", "--all");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            ["null|other_provider|unpriced"],
            await LedgerSql.RowsAsync(
                harness.Context.DatabasePath,
                "SELECT provider_name, pricing_status, billing_channel FROM spans;"));
    }

    [Fact]
    public async Task CC8_BYOKCompactionsAreAmbiguousBesideLiveSpans()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live(SessionId, "11111111-1111-4111-8111-111111111111")]);
        SqliteConnection.ClearAllPools();
        Cc1Log().Write(harness.Context.CopilotHome);

        CliRun run = await harness.RunAsync("import", "--all", "--json");

        Assert.Equal(2, run.ExitCode);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        JsonElement source = json.RootElement.GetProperty("sources")[0];
        Assert.Equal(2, source.GetProperty("ambiguousOverlap").GetInt32());
        Assert.Equal(1, source.GetProperty("recorded").GetInt32());
        Assert.Equal(
            ["github"],
            await LedgerSql.RowsAsync(
                harness.Context.DatabasePath,
                "SELECT provider_name FROM spans WHERE ingest_source = 'import:copilot';"));
    }

    [Fact]
    public async Task CC9_CompactionCanariesAndAttemptIdsNeverReachTheLedgerOrOutput()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        Cc1Log().Write(harness.Context.CopilotHome);

        CliRun text = await harness.RunAsync("import", "--all");
        CliRun json = await harness.RunAsync("import", "--all", "--json");
        CliRun reportText = await harness.RunAsync("status");
        CliRun reportJson = await harness.RunAsync("status", "--json");

        Assert.Equal((0, 0, 0, 0), (text.ExitCode, json.ExitCode, reportText.ExitCode, reportJson.ExitCode));
        string output = string.Concat(
            text.Out, text.Error, json.Out, json.Error, reportText.Out, reportText.Error, reportJson.Out, reportJson.Error);
        Assert.DoesNotContain(CopilotSessionLogs.Canary, output, StringComparison.Ordinal);
        Assert.DoesNotContain(AttemptId, output, StringComparison.Ordinal);
        SqliteConnection.ClearAllPools();
        byte[] canary = Encoding.UTF8.GetBytes(CopilotSessionLogs.Canary);
        byte[] attemptId = Encoding.UTF8.GetBytes(AttemptId);
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string path = harness.Context.DatabasePath + suffix;
            if (!File.Exists(path))
            {
                continue;
            }

            byte[] bytes = await File.ReadAllBytesAsync(path);
            Assert.True(bytes.AsSpan().IndexOf(canary) < 0, $"{suffix} contains the canary.");
            Assert.True(bytes.AsSpan().IndexOf(attemptId) < 0, $"{suffix} contains the raw attempt ID.");
        }
    }

    private static CopilotSessionLogs Cc1Log() =>
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Usage("usage-byok-1", null, providerType: "anthropic")
            .Compaction(null, change: data => MakeByok(data, AttemptId))
            .Compaction(
                FailedSrid,
                change: data =>
                {
                    data["success"] = false;
                    data["compactionAttemptId"] = "c0000000-0000-4000-8000-0000000000b2";
                })
            .Compaction(
                null,
                change: data =>
                {
                    data["success"] = false;
                    data["compactionAttemptId"] = "c0000000-0000-4000-8000-0000000000b3";
                    data["compactionTokensUsed"]!.AsObject().Remove("copilotUsage");
                });

    private static void MakeByok(JsonObject data, string attemptId)
    {
        data["success"] = true;
        data["compactionAttemptId"] = attemptId;
        JsonObject used = data["compactionTokensUsed"]!.AsObject();
        used.Remove("provider");
        used.Remove("copilotUsage");
    }

    private static async Task<SessionLogReadResult> ReadAsync(Action<CopilotSessionLogs> addCompaction)
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new(SessionId, "1.0.114");
        addCompaction(log);
        log.Write(temp.Path);
        return await CopilotRecordTests.ReadAsync(temp.Path);
    }

    private static void AssertUnsupported(SessionLogReadResult result, string field)
    {
        Assert.Empty(result.Records);
        Assert.Equal((1, 1), (result.RecordsParsed, result.UnsupportedUsageShapes));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            (ImportDiagnosticCode.UnsupportedUsageShape, ImportEventKind.CompactionComplete, field),
            (diagnostic.Code, diagnostic.Event, diagnostic.Field));
    }

    private static CliHarness Harness(TempDirectory temp) =>
        new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));
}
