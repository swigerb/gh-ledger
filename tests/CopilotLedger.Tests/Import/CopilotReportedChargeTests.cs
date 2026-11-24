using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Storage.Import;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class CopilotReportedChargeTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IRC1_AUsageReportedChargeReplacesTheRateCardCharge()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-a")
            .Usage("u1", S1, totalNanoAiu: 47_388_600)
            .Write(CopilotHome(temp));

        ImportReport result = await RunAsync(temp);

        Assert.Equal(
            ["473886000|reported|1500000000"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT cost_usd_pico, charge_source, list_cost_usd_pico FROM spans;"));
        ImportSourceReport report = Copilot(result);
        Assert.Equal((0.0473886m, 0L, true), (report.AiCredits, report.ReportedChargesRejected, report.Complete));
    }

    [Fact]
    public async Task IRC2_AUsageWithoutACopilotReportedChargeUsesTheRateCard()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-a").Usage("u1", S1).Write(CopilotHome(temp));

        ImportReport result = await RunAsync(temp);

        Assert.Equal(
            ["1500000000|rate_card"],
            await LedgerSql.RowsAsync(Database(temp), "SELECT cost_usd_pico, charge_source FROM spans;"));
        Assert.Equal(0, Copilot(result).ReportedChargesRejected);
    }

    [Fact]
    public async Task IRC3_MalformedReportedChargesAreRejectedWithoutLosingCoverage()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new("s-a");
        AddMalformed(log, "u1", 1, JsonNode.Parse("0.5"));
        AddMalformed(log, "u2", 2, JsonValue.Create(-1));
        AddMalformed(log, "u3", 3, JsonValue.Create("47388600"));
        AddMalformed(log, "u4", 4, JsonValue.Create(9_007_199_254_740_993));
        AddMalformed(log, "u5", 5, new JsonObject());
        log.Write(CopilotHome(temp));

        ImportReport result = await RunAsync(temp);

        Assert.Equal(
            ["rejected", "rejected", "rejected", "rejected", "rejected"],
            await LedgerSql.RowsAsync(Database(temp), "SELECT charge_source FROM spans ORDER BY id;"));
        Assert.All(
            await LedgerSql.RowsAsync(Database(temp), "SELECT cost_usd_pico FROM spans ORDER BY id;"),
            cost => Assert.Equal("1500000000", cost));
        ImportSourceReport report = Copilot(result);
        Assert.Equal(5, report.ReportedChargesRejected);
        Assert.True(report.Complete);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            (ImportSourceKind.Copilot, "s-a", ImportDiagnosticCode.ReportedChargeRejected, 5L),
            (diagnostic.Source, diagnostic.LogId, diagnostic.Code, diagnostic.Count));

        using TempDirectory cliTemp = new();
        await using CliHarness harness = new(cliTemp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));
        CopilotSessionLogs cliLog = new("s-cli");
        AddMalformed(cliLog, "u1", 1, JsonNode.Parse("0.5"));
        AddMalformed(cliLog, "u2", 2, JsonValue.Create(-1));
        AddMalformed(cliLog, "u3", 3, JsonValue.Create("47388600"));
        AddMalformed(cliLog, "u4", 4, JsonValue.Create(9_007_199_254_740_993));
        AddMalformed(cliLog, "u5", 5, new JsonObject());
        cliLog.Write(harness.Context.CopilotHome);

        CliRun cli = await harness.RunAsync("import", "--all");

        Assert.Equal(0, cli.ExitCode);
        Assert.Contains("5 reported charges couldn't be used; charged at the rate card\n", cli.Error);
    }

    [Fact]
    public async Task IRC4_ACompactionsDefaultReportedChargeIsBilled()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-a").Compaction(S1).Write(CopilotHome(temp));

        ImportReport result = await RunAsync(temp);

        Assert.Equal(
            ["118555450000|reported"],
            await LedgerSql.RowsAsync(Database(temp), "SELECT cost_usd_pico, charge_source FROM spans;"));
        Assert.Equal(11.855545m, Copilot(result).AiCredits);
    }

    [Fact]
    public async Task IRC5_AByokUsageRejectsAPositiveGitHubReportedCharge()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-a")
            .Usage("u1", null, providerType: "anthropic", totalNanoAiu: 5)
            .Write(CopilotHome(temp));

        ImportReport result = await RunAsync(temp);

        Assert.Equal(
            ["other_provider|rejected"],
            await LedgerSql.RowsAsync(Database(temp), "SELECT pricing_status, charge_source FROM spans;"));
        Assert.Equal(1, Copilot(result).ReportedChargesRejected);
    }

    [Fact]
    public async Task IRC7_DryRunCountsRejectedChargesWithoutWriting()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Database(temp));
        await store.InitializeAsync();
        CopilotSessionLogs log = new("s-a");
        AddMalformed(log, "u1", 1, JsonNode.Parse("0.5"));
        AddMalformed(log, "u2", 2, JsonValue.Create(-1));
        AddMalformed(log, "u3", 3, JsonValue.Create("47388600"));
        AddMalformed(log, "u4", 4, JsonValue.Create(9_007_199_254_740_993));
        AddMalformed(log, "u5", 5, new JsonObject());
        log.Write(CopilotHome(temp));

        ImportReport dry = await RunAsync(temp, request => request with { DryRun = true });

        ImportSourceReport report = Copilot(dry);
        Assert.Equal((5L, 5L, true), (report.Recorded, report.ReportedChargesRejected, report.Complete));
        Assert.Equal(ImportDiagnosticCode.ReportedChargeRejected, Assert.Single(dry.Diagnostics).Code);
        Assert.Equal(0L, await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"));
    }

    private static void AddMalformed(CopilotSessionLogs log, string usageId, int index, JsonNode? totalNanoAiu) =>
        log.Usage(
            usageId,
            $"22222222-2222-4222-8222-{index:000000000000}",
            change: usage =>
                usage["copilotUsage"] = new JsonObject { ["totalNanoAiu"] = totalNanoAiu?.DeepClone() });

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
            .RunAsync(change?.Invoke(request) ?? request);
    }
}
