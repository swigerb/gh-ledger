using System.Text.Json;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Ingestion;
using CopilotLedger.Otlp.Normalization;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using static CopilotLedger.Tests.TestSupport.OtlpRequests;

namespace CopilotLedger.Tests.Otlp;

public sealed class ReportedChargeIngestTests
{
    private const string TraceId = "5b8efff798038103d269b633813fc60c";

    [Fact]
    public async Task RI1_ReportedChargeIsRecordedWithItsListPrice()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        OtlpTraceRequest request = RequestWithCharge("""{"doubleValue":47388600}""");

        IngestResult result = await harness.Ingestor.IngestAsync(request);

        AssertResult(result, 1, 1, 0);
        long listPico = ListCostPico(harness, RequestWithoutCharge());
        Assert.Equal(
            (473_886_000L, listPico, "reported"),
            await SpanMoneyAsync(harness.DatabasePath));
    }

    [Fact]
    public async Task RI2_AbsentChargeUsesTheRateCardForBothAmounts()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        OtlpTraceRequest request = RequestWithoutCharge();
        long listPico = ListCostPico(harness, request);

        IngestResult result = await harness.Ingestor.IngestAsync(request);

        AssertResult(result, 1, 1, 0);
        Assert.Equal(
            (listPico, listPico, "rate_card"),
            await SpanMoneyAsync(harness.DatabasePath));
    }

    [Fact]
    public async Task RI3_InvokeAgentChargeIsNotAddedToTheTrace()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        OtlpTraceRequest request = Decode(
            new SpanJson(
                "0000000000000001",
                "invoke_agent",
                """{"doubleValue":11855545000}"""),
            new SpanJson(
                "0000000000000002",
                "chat",
                """{"doubleValue":47388600}"""));

        IngestResult result = await harness.Ingestor.IngestAsync(request);

        AssertResult(result, 2, 1, 0);
        Assert.Equal(
            (473_886_000L, "reported"),
            await AggregateMoneyAsync(harness.DatabasePath));
        LedgerRollupRow row = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Repository,
            LedgerFilter.All));
        Assert.Equal(0.000473886m, row.AiCreditsUsd);
    }

    [Fact]
    public async Task RI4_MalformedChargesAreRecordedAtTheRateCard()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        OtlpTraceRequest request = Decode(
            new SpanJson("0000000000000001", "chat", """{"doubleValue":0.5}"""),
            new SpanJson("0000000000000002", "chat", """{"doubleValue":-1}"""),
            new SpanJson("0000000000000003", "chat", """{"doubleValue":"NaN"}"""),
            new SpanJson("0000000000000004", "chat", """{"doubleValue":9007199254740993}"""));

        IngestResult result = await harness.Ingestor.IngestAsync(request);

        Assert.Equal(4, result.ReceivedSpans);
        Assert.Equal(0, result.RejectedSpans);
        Assert.Null(result.FirstRejectionReason);
        Assert.Equal(4, result.InferenceSpans);
        Assert.Equal(4, result.RecordedSpans);
        Assert.Equal(4, result.RejectedReportedCharges);
        long rateCardPico = ListCostPico(harness, RequestWithoutCharge());
        Assert.All(
            await SpanMoneyRowsAsync(harness.DatabasePath),
            row => Assert.Equal((rateCardPico, rateCardPico, "rejected"), row));
    }

    [Fact]
    public async Task RI5_ReceiptTotalsEqualTheBranchChargedAmount()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        await harness.Ingestor.IngestAsync(
            WithWorkingDirectory(RequestWithCharge("""{"doubleValue":47388600}"""), harness.Repo));
        await using CliHarness cli = new(temp.Path);

        CliRun receipt = await cli.RunInAsync(harness.Repo, "export-pr", "--json");
        CliRun branch = await cli.RunInAsync(
            harness.Repo,
            "branch",
            "--since",
            "all",
            "--json");

        Assert.Equal(0, receipt.ExitCode);
        Assert.Equal(0, branch.ExitCode);
        using JsonDocument receiptJson = JsonDocument.Parse(receipt.Out);
        using JsonDocument branchJson = JsonDocument.Parse(branch.Out);
        JsonElement totals = receiptJson.RootElement.GetProperty("totals");
        JsonElement row = Assert.Single(branchJson.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal(0.0473886m, row.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(0.000473886m, row.GetProperty("aiCreditsUsd").GetDecimal());
        Assert.Equal(row.GetProperty("aiCredits").GetDecimal(), totals.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(row.GetProperty("aiCreditsUsd").GetDecimal(), totals.GetProperty("aiCreditsUsd").GetDecimal());
    }

    [Fact]
    public async Task RI6_BudgetUsesTheChargedAmount()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        OtlpTraceRequest request = WithWorkingDirectory(
            RequestWithCharge("""{"doubleValue":47388600}"""),
            harness.Repo);
        await harness.Store.SetBranchBudgetAsync(
            "swigerb/copilot-ledger",
            "feature/ledger",
            0.05m);

        await harness.Ingestor.IngestAsync(request);
        BranchBudgetRow before = Assert.Single(
            await harness.Store.GetBranchBudgetsAsync(
                "swigerb/copilot-ledger",
                "feature/ledger"));
        Assert.Equal(0.0473886m, before.SpentAiCredits);
        Assert.False(before.Exceeded);

        await harness.Ingestor.IngestAsync(request with
        {
            ResourceSpans =
            [
                RequestWithoutCharge().ResourceSpans[0] with
                {
                    ResourceAttributes = request.ResourceSpans[0].ResourceAttributes,
                    Spans =
                    [
                        request.ResourceSpans[0].Spans[0] with
                        {
                            SpanId = "0000000000000002",
                        },
                    ],
                },
            ],
        });
        BranchBudgetRow after = Assert.Single(
            await harness.Store.GetBranchBudgetsAsync(
                "swigerb/copilot-ledger",
                "feature/ledger"));
        Assert.Equal(0.0947772m, after.SpentAiCredits);
        Assert.True(after.Exceeded);
    }

    [Fact]
    public async Task RI7_CreditVelocityUsesTheChargedAmount()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        OtlpTraceRequest request = WithWorkingDirectory(
            RequestWithCharge("""{"doubleValue":47388600}"""),
            harness.Repo);
        await harness.Ingestor.IngestAsync(request);
        TraceActivity activity = await harness.Store.GetTraceActivityAsync(TraceId);
        long listPico = ListCostPico(harness, RequestWithoutCharge());
        decimal chargedUsd = 0.000473886m;
        decimal listUsd = PicoUsd.ToUsd(listPico);
        Assert.InRange(chargedUsd, 0m, listUsd);
        decimal threshold = (chargedUsd + listUsd) / 2m;
        Assert.InRange(threshold, chargedUsd, listUsd);

        VelocityTrip? trip = BreakerRules.FindVelocityTrip(
            activity.Costs,
            threshold,
            TimeSpan.FromMinutes(5));

        Assert.Null(trip);
        Assert.Equal(chargedUsd, Assert.Single(activity.Costs).CostUsd);
    }

    private static OtlpTraceRequest RequestWithCharge(string valueJson) =>
        Decode(new SpanJson("0000000000000001", "chat", valueJson));

    private static OtlpTraceRequest RequestWithoutCharge() =>
        Decode(new SpanJson("0000000000000001", "chat", null));

    private static OtlpTraceRequest WithWorkingDirectory(OtlpTraceRequest request, string repo)
    {
        OtlpResourceSpans resource = request.ResourceSpans[0];
        return request with
        {
            ResourceSpans =
            [
                resource with
                {
                    ResourceAttributes =
                    [
                        Str("service.name", "github-copilot"),
                        Str("process.working_directory", repo),
                    ],
                },
            ],
        };
    }

    private static OtlpTraceRequest Decode(params SpanJson[] spans)
    {
        string renderedSpans = string.Join(
            ",",
            spans.Select((span, index) =>
            {
                string charge = span.ValueJson is null
                    ? ""
                    : ",{\"key\":\"github.copilot.nano_aiu\",\"value\":" +
                      span.ValueJson +
                      "}";
                return "{\"traceId\":\"" + TraceId
                    + "\",\"spanId\":\"" + span.SpanId
                    + "\",\"startTimeUnixNano\":\"" +
                    (1795199604707000000UL + (ulong)index)
                    + "\",\"endTimeUnixNano\":\"" +
                    (1795199606429000000UL + (ulong)index)
                    + "\",\"attributes\":["
                    + "{\"key\":\"gen_ai.operation.name\",\"value\":{\"stringValue\":\"" +
                    span.Operation + "\"}},"
                    + "{\"key\":\"gen_ai.request.model\",\"value\":{\"stringValue\":\"claude-haiku-4.5\"}},"
                    + "{\"key\":\"gen_ai.usage.input_tokens\",\"value\":{\"intValue\":\"1000\"}},"
                    + "{\"key\":\"gen_ai.usage.output_tokens\",\"value\":{\"intValue\":\"100\"}}"
                    + charge + "]}";
            }));
        string json =
            "{\"resourceSpans\":[{\"scopeSpans\":[{\"spans\":["
            + renderedSpans
            + "]}]}]}";
        return OtlpTraceDecoder.DecodeJson(OtlpFixtures.Utf8(json));
    }

    private static long ListCostPico(Harness harness, OtlpTraceRequest withoutCharge)
    {
        NormalizedGenAiSpan span = Assert.Single(
            GenAiSpanNormalizer.Normalize(withoutCharge).InferenceSpans);
        return PicoUsd.FromUsd(harness.Calculator.Calculate(span).UsdCost);
    }

    private static void AssertResult(
        IngestResult result,
        int received,
        int recorded,
        int rejectedReportedCharges)
    {
        Assert.Equal(received, result.ReceivedSpans);
        Assert.Equal(0, result.RejectedSpans);
        Assert.Null(result.FirstRejectionReason);
        Assert.Equal(recorded, result.InferenceSpans);
        Assert.Equal(recorded, result.RecordedSpans);
        Assert.Equal(rejectedReportedCharges, result.RejectedReportedCharges);
    }

    private static async Task<(long Cost, long List, string Source)> SpanMoneyAsync(
        string databasePath)
    {
        return Assert.Single(await SpanMoneyRowsAsync(databasePath));
    }

    private static async Task<IReadOnlyList<(long Cost, long List, string Source)>>
        SpanMoneyRowsAsync(string databasePath)
    {
        await using SqliteConnection connection = new($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT cost_usd_pico, list_cost_usd_pico, charge_source FROM spans ORDER BY span_id;";
        List<(long, long, string)> rows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2)));
        }

        return rows;
    }

    private static async Task<(long Cost, string Source)> AggregateMoneyAsync(string databasePath)
    {
        await using SqliteConnection connection = new($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT SUM(cost_usd_pico), MAX(charge_source), COUNT(*) FROM spans;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1L, reader.GetInt64(2));
        return (reader.GetInt64(0), reader.GetString(1));
    }

    private sealed record SpanJson(string SpanId, string Operation, string? ValueJson);

    private sealed class Harness
    {
        private Harness(
            string databasePath,
            SqliteLedgerStore store,
            LedgerIngestor ingestor,
            JsonRateCardCalculator calculator,
            string repo)
        {
            DatabasePath = databasePath;
            Store = store;
            Ingestor = ingestor;
            Calculator = calculator;
            Repo = repo;
        }

        public string DatabasePath { get; }
        public SqliteLedgerStore Store { get; }
        public LedgerIngestor Ingestor { get; }
        public JsonRateCardCalculator Calculator { get; }
        public string Repo { get; }

        public static async Task<Harness> CreateAsync(TempDirectory temp)
        {
            string databasePath = Path.Combine(temp.Path, "data", "ledger.db");
            SqliteLedgerStore store = new(databasePath);
            await store.InitializeAsync();
            string repo = GitFixture.Create(temp.Path);
            JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbedded();
            LedgerIngestor ingestor = new(
                store,
                calculator,
                new FileSystemGitContextResolver(ceilingDirectory: temp.Path),
                new CopilotSessionWorkspaceLocator(Path.Combine(temp.Path, "copilot-home")));
            return new Harness(databasePath, store, ingestor, calculator, repo);
        }
    }
}
