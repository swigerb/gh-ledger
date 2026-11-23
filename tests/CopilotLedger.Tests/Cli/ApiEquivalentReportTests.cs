using System.Globalization;
using System.Text.Json;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class ApiEquivalentReportTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-21T14:00:00Z", CultureInfo.InvariantCulture);

    private static ManualTimeProvider Time => new(
        DateTimeOffset.Parse("2026-11-21T15:00:00Z", CultureInfo.InvariantCulture),
        ManualTimeProvider.UtcMinus5);

    [Fact]
    public async Task AE1_BranchTableAndJsonShowEachRule()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path, Branches(big: false));

        CliRun text = await harness.RunAsync("branch", "--all-repos", "--since", "all");
        CliRun json = await harness.RunAsync("branch", "--all-repos", "--since", "all", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            """
            Branches in all repositories (all time)

            Repository   Branch    Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
            contoso/app  estimate      1         0        0.00       $0.00     $0.32         ~$0.30        $0.00
            contoso/app  partial       2         1        7.40       $0.07     $0.00         $0.22*        $0.00
            contoso/app  priced        1         0        7.40       $0.07     $0.00          $0.22        $0.00
            contoso/app  both          2         1        1.50       $0.02     $0.00        ~$0.05*        $0.00
            contoso/app  unpriced      1         1        0.00       $0.00     $0.00       Unpriced        $0.00

            3 of 7 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
            2 priced spans reported no cache counters, so all of their input is priced as uncached; ~ marks an estimate.

            """.ReplaceLineEndings("\n"),
            text.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement[] rows = document.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(
            ["0.3|0|1", "0.222|1|0", "0.222|0|0", "0.045|1|1", "null|1|0"],
            rows.Select(row => string.Join(
                '|',
                row.GetProperty("apiEquivalentUsd").ValueKind == JsonValueKind.Null
                    ? "null"
                    : row.GetProperty("apiEquivalentUsd").GetRawText(),
                row.GetProperty("apiEquivalentUnpricedSpans").GetInt64(),
                row.GetProperty("apiEquivalentEstimatedSpans").GetInt64())));
        Assert.Equal(
            ["localSavedUsd", "apiEquivalentUsd", "apiEquivalentUnpricedSpans", "apiEquivalentEstimatedSpans", "cacheReportedSpans"],
            rows[0].EnumerateObject().Select(property => property.Name).SkipWhile(name => name != "localSavedUsd").Take(5));
        Assert.Contains("\"apiEquivalentUsd\": null,", json.Out);
    }

    [Fact]
    public async Task AE2_TopPromptsShowTheColumnAndJsonFields()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path, Branches(big: false));

        CliRun text = await harness.RunAsync("top-prompts", "--since", "all", "--limit", "3");
        CliRun json = await harness.RunAsync("top-prompts", "--since", "all", "--limit", "3", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            """
            Top 3 prompts by cost in all repositories (all time)

            #  Started           Repository   Agents  Models                     Spans  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved  Trace
            1  2026-11-21 09:00  contoso/app  -       gpt-6.1-sol                    1        0.00       $0.00     $0.32         ~$0.30        $0.00  44444444444444444444444444444444
            2  2026-11-21 09:00  contoso/app  -       test-retail                    1        7.40       $0.07     $0.00          $0.22        $0.00  11111111111111111111111111111111
            3  2026-11-21 09:00  contoso/app  -       test-retail, test-unknown      2        7.40       $0.07     $0.00         $0.22*        $0.00  22222222222222222222222222222222

            1 of 4 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
            1 priced span reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.

            """.ReplaceLineEndings("\n"),
            text.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement partial = document.RootElement.GetProperty("rows")[2];
        Assert.Equal(
            ["localSavedUsd", "apiEquivalentUsd", "apiEquivalentUnpricedSpans", "apiEquivalentEstimatedSpans"],
            partial.EnumerateObject().Select(property => property.Name).TakeLast(4));
        Assert.Equal(0.222m, partial.GetProperty("apiEquivalentUsd").GetDecimal());
        Assert.Equal(1, partial.GetProperty("apiEquivalentUnpricedSpans").GetInt64());
    }

    // With every GitHub-billed request priced both ways, status shows their ratio; local requests compare their savings with
    // their list prices.
    [Fact]
    public async Task AE3_StatusComparesTheSameRequestsInEachView()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path, Lenses());

        CliRun text = await harness.RunAsync("status");
        CliRun json = await harness.RunAsync("status", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            """
            Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
            Today             2         0        7.40       $0.07     $0.00         ~$0.23        $0.01
            Last 7 days       2         0        7.40       $0.07     $0.00         ~$0.23        $0.01
            Last 30 days      2         0        7.40       $0.07     $0.00         ~$0.23        $0.01

            Period        GitHub USD  At API prices  GitHub/API  Local saved  Local at API prices
            Today              $0.07          $0.22       0.33x        $0.01               ~$0.01
            Last 7 days        $0.07          $0.22       0.33x        $0.01               ~$0.01
            Last 30 days       $0.07          $0.22       0.33x        $0.01               ~$0.01

            1 priced span in the last 30 days reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.

            Period        Cache hit  Cache reported
            Today             40.0%  1 of 2 spans
            Last 7 days       40.0%  1 of 2 spans
            Last 30 days      40.0%  1 of 2 spans

            """.ReplaceLineEndings("\n"),
            text.Out[(text.Out.IndexOf("\n\n", StringComparison.Ordinal) + 2)..]);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement period = document.RootElement.GetProperty("periods")[0];
        Assert.Equal(
            ["localSavedUsd", "apiEquivalentUsd", "apiEquivalentUnpricedSpans", "apiEquivalentEstimatedSpans", "lenses", "cache"],
            period.EnumerateObject().Select(property => property.Name).TakeLast(6));
        Assert.Equal(
            """
            {
                    "copilot": {
                      "spans": 1,
                      "unpricedSpans": 0,
                      "aiCredits": 7.4,
                      "aiCreditsUsd": 0.074,
                      "apiEquivalentUsd": 0.222,
                      "apiEquivalentUnpricedSpans": 0,
                      "apiEquivalentEstimatedSpans": 0,
                      "billedToApiRatio": 0.3333333333333333333333333333
                    },
                    "local": {
                      "spans": 1,
                      "localSavedUsd": 0.0075,
                      "apiEquivalentUsd": 0.0112,
                      "apiEquivalentUnpricedSpans": 0,
                      "apiEquivalentEstimatedSpans": 1
                    }
                  }
            """.ReplaceLineEndings("\n"),
            period.GetProperty("lenses").GetRawText().ReplaceLineEndings("\n"));
    }

    // The ratio needs every GitHub-billed request priced in both views.
    [Fact]
    public async Task AE4_StatusLeavesOutTheRatioWhenARequestHasNoPrice()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path, Branches(big: false));

        CliRun json = await harness.RunAsync("status", "--json");

        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement copilot = document.RootElement.GetProperty("periods")[0].GetProperty("lenses").GetProperty("copilot");
        Assert.Equal(3, copilot.GetProperty("spans").GetInt64());
        Assert.Equal(1, copilot.GetProperty("unpricedSpans").GetInt64());
        Assert.Equal(0.444m, copilot.GetProperty("apiEquivalentUsd").GetDecimal());
        Assert.Equal(1, copilot.GetProperty("apiEquivalentUnpricedSpans").GetInt64());
        Assert.Equal(JsonValueKind.Null, copilot.GetProperty("billedToApiRatio").ValueKind);
    }

    [Fact]
    public async Task AE5_OutputIsTheSameUnderGermanCulture()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path, Branches(big: true));
        string[][] commands =
        [
            ["status"],
            ["status", "--json"],
            ["branch", "--all-repos", "--since", "all"],
            ["branch", "--all-repos", "--since", "all", "--json"],
            ["agents", "--since", "all"],
            ["agents", "--since", "all", "--json"],
            ["top-prompts", "--since", "all"],
            ["top-prompts", "--since", "all", "--json"],
        ];
        List<string> invariant = [];
        List<string> german = [];
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            foreach (string[] command in commands)
            {
                invariant.Add((await harness.RunAsync(command)).Out);
            }

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            foreach (string[] command in commands)
            {
                german.Add((await harness.RunAsync(command)).Out);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }

        Assert.Equal(invariant, german);
        Assert.Contains("~$1,050.03*", german[2]);
        Assert.Contains("\"apiEquivalentUsd\": 1050.031845,", german[3]);
        Assert.Contains("\"billedToApiRatio\": null", german[1]);
    }

    // Branch, trace, and span of the AE1, AE2, AE4, and AE5 requests, priced with RateFixtures.RetailTestRatesJson. Each
    // request is its own trace, except that the two of "partial" share one. big makes one request cost over $1,000.
    private static IReadOnlyList<(string Branch, string Trace, NormalizedGenAiSpan Span)> Branches(bool big) =>
    [
        ("priced", "1", TestSpans.Chat(
            startTime: Start,
            requestModel: "test-retail",
            inputTokens: 100_000,
            cacheReadInputTokens: 40_000,
            outputTokens: 2_000)),
        ("partial", "2", TestSpans.Chat(
            startTime: Start,
            requestModel: "test-retail",
            inputTokens: 100_000,
            cacheReadInputTokens: 40_000,
            outputTokens: 2_000)),
        ("partial", "2", TestSpans.Chat(
            startTime: Start,
            requestModel: "test-unknown",
            inputTokens: 1_000,
            outputTokens: 100)),
        ("estimate", "4", TestSpans.Chat(
            startTime: Start,
            serverAddress: "contoso.openai.azure.com",
            requestModel: "gpt-6.1-sol",
            inputTokens: 100_000,
            outputTokens: 10_000)),
        ("unpriced", "5", TestSpans.Chat(
            startTime: Start.AddDays(-40),
            requestModel: "test-unknown",
            inputTokens: 1_000,
            outputTokens: 100)),
        ("both", "6", TestSpans.Chat(
            startTime: Start.AddDays(-40),
            requestModel: "test-retail",
            inputTokens: 10_000,
            outputTokens: big ? 70_000_123 : 1_000)),
        ("both", "7", TestSpans.Chat(
            startTime: Start.AddDays(-40),
            requestModel: "test-unknown",
            inputTokens: 1_000,
            outputTokens: 100)),
    ];

    // A GitHub request with cache reads, and a local one, both priced in every view.
    private static IReadOnlyList<(string Branch, string Trace, NormalizedGenAiSpan Span)> Lenses() =>
    [
        ("main", "1", TestSpans.Chat(
            startTime: Start,
            requestModel: "test-retail",
            inputTokens: 100_000,
            cacheReadInputTokens: 40_000,
            outputTokens: 2_000)),
        ("main", "2", TestSpans.Chat(
            startTime: Start,
            providerName: "ollama",
            requestModel: "qwen-local",
            inputTokens: 10_000,
            outputTokens: 1_000)),
    ];

    private static async Task<CliHarness> SeededHarnessAsync(
        string root,
        IReadOnlyList<(string Branch, string Trace, NormalizedGenAiSpan Span)> seed)
    {
        CliHarness harness = new(root, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(Path.Combine(root, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        JsonRateCardCalculator retail = JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);
        string repository = Path.Combine(root, "app");
        int index = 0;
        await store.RecordAsync(seed.Select(item =>
        {
            NormalizedGenAiSpan span = item.Span with
            {
                TraceId = new string(item.Trace[0], 32),
                SpanId = $"{++index:x16}",
            };
            GitWorkspaceContext git = new(
                repository,
                Path.Combine(repository, ".git"),
                item.Branch,
                "0123456789abcdef0123456789abcdef01234567",
                "https://github.com/contoso/app.git",
                "contoso/app");
            return new LedgerEntry(span, retail.Calculate(span), git);
        }));
        return harness;
    }
}
