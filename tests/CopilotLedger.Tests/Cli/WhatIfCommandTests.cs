using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Cli;

public sealed class WhatIfCommandTests
{
    private static ManualTimeProvider Time => new(
        DateTimeOffset.Parse("2026-11-21T15:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        ManualTimeProvider.UtcMinus5);

    [Fact]
    public async Task WF1_BranchShowsTheWhatIfAmountsAndLabel()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string path = Path.Combine(temp.Path, "negotiated.json");

        CliRun run = await harness.RunAsync("branch", "--all-repos", "--since", "all", "--pricing", "negotiated.json");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            $"""
            Branches in all repositories (all time)
            Pricing: what-if {path} (ratesVersion retail-fixture), for API-equivalent amounts only

            Repository              Branch          Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
            swigerb/copilot-ledger  main                1         0      286.00       $2.86     $0.00       Unpriced        $0.00
            swigerb/copilot-ledger  feature/ledger      2         0       56.00       $0.56     $0.00        ~$0.36*        $0.23
            contoso/api             main                1         0        0.00       $0.00     $0.32         ~$0.30        $0.00
            (none)                  (none)              1         1        0.00       $0.00     $0.00       Unpriced        $0.00

            3 of 5 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
            2 priced spans reported no cache counters, so all of their input is priced as uncached; ~ marks an estimate.

            """.ReplaceLineEndings("\n"),
            run.Out);
        Assert.Equal(string.Empty, run.Error);
    }

    // Only the API-equivalent fields change; recorded amounts, counts, and warnings don't, and nothing is written.
    [Theory]
    [InlineData("status")]
    [InlineData("branch")]
    [InlineData("agents")]
    [InlineData("top-prompts")]
    public async Task WF2_JsonChangesOnlyTheApiEquivalentFields(string command)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        harness.Platform.ProcessVariables["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc";
        string[] report = command == "status" ? ["status", "--json"] : [command, "--since", "all", "--json"];
        string[] before = await RowsAsync(temp.Path);

        CliRun recorded = await harness.RunAsync(report);
        CliRun whatIf = await harness.RunAsync([.. report, "--pricing", "negotiated.json"]);

        Assert.Equal(0, whatIf.ExitCode);
        JsonObject recordedJson = JsonNode.Parse(recorded.Out)!.AsObject();
        JsonObject whatIfJson = JsonNode.Parse(whatIf.Out)!.AsObject();
        Assert.Equal(
            $$"""{"kind":"whatIf","path":{{JsonSerializer.Serialize(Path.Combine(temp.Path, "negotiated.json"))}},"ratesVersion":"retail-fixture","apiRateCardSha256":"{{JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson).ApiRateCardSha256}}"}""",
            whatIfJson["pricingSource"]!.ToJsonString());
        Assert.Null(recordedJson["pricingSource"]);
        Assert.NotEqual(recorded.Out, whatIf.Out);
        Assert.Equal(WithoutApiEquivalent(recordedJson), WithoutApiEquivalent(whatIfJson));
        Assert.Equal(before, await RowsAsync(temp.Path));
    }

    public static TheoryData<string, string> InvalidFiles => new()
    {
        { "missing", "Invalid pricing file: <path> does not exist.\n" },
        { "not-json", "Invalid pricing file: <path>: line 1, column 2: invalid JSON\n" },
        { "schema-1", "Invalid pricing file: <path>: schemaVersion: must be 2, so that the file has API list prices\n" },
        { "schema-3", "Invalid pricing file: <path>: schemaVersion: must be 1 or 2\n" },
        { "overlay", "Invalid pricing file: <path>: ratesVersion: is required\n" },
    };

    // Every command that takes --pricing, and rates validate --pricing, refuse an unusable file with the same message.
    [Theory]
    [MemberData(nameof(InvalidFiles))]
    public async Task WF3_InvalidFilesFailWithTheSameMessage(string file, string error)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string path = Path.Combine(temp.Path, $"{file}.json");
        string? text = file switch
        {
            "not-json" => "{",
            "schema-1" => RateFixtures.ByokTestRatesJson,
            "schema-3" => RateFixtures.RetailTestRatesJson.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 3", StringComparison.Ordinal),
            "overlay" => """{ "schemaVersion": 2, "apiEquivalent": { "models": [] } }""",
            _ => null,
        };
        if (text is not null)
        {
            await File.WriteAllTextAsync(path, text);
        }

        string[][] commands =
        [
            ["status", "--pricing", path],
            ["branch", "--pricing", path],
            ["agents", "--pricing", path],
            ["top-prompts", "--pricing", path],
            ["export-pr", "--pricing", path],
            ["rates", "validate", "--pricing", path],
        ];
        foreach (string[] command in commands)
        {
            CliRun run = await harness.RunInAsync(Path.Combine(temp.Path, "repo"), command);
            Assert.Equal(1, run.ExitCode);
            Assert.Equal(string.Empty, run.Out);
            Assert.Equal(error.Replace("<path>", path, StringComparison.Ordinal), run.Error);
        }
    }

    [Fact]
    public async Task WF4_RatesValidateAcceptsAPricingFile()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun valid = await harness.RunAsync("rates", "validate", "--pricing", "negotiated.json");
        CliRun both = await harness.RunAsync("rates", "validate", "--file", "x.json", "--pricing", "negotiated.json");
        CliRun cache = await harness.RunAsync("status", "--cache", "--pricing", "negotiated.json");

        Assert.Equal(0, valid.ExitCode);
        Assert.Equal(
            $"{Path.Combine(temp.Path, "negotiated.json")} is a valid pricing file. Rates version: retail-fixture.\n",
            valid.Out);
        Assert.Equal(1, both.ExitCode);
        Assert.Contains("Use either --file or --pricing, not both.", both.Error);
        Assert.Equal(1, cache.ExitCode);
        Assert.Contains("--pricing can't be used with --cache.", cache.Error);
    }

    // A receipt names the file, not its local path, in the Markdown and the JSON.
    [Fact]
    public async Task WF5_ReceiptsLabelWhatIfPrices()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");
        string path = Path.Combine(temp.Path, "negotiated.json");

        CliRun markdown = await harness.RunInAsync(repo, "export-pr", "--stdout", "--pricing", path);
        CliRun json = await harness.RunInAsync(repo, "export-pr", "--json", "--pricing", path);

        Assert.Equal(0, markdown.ExitCode);
        Assert.Contains(
            "\n- **API-equivalent (retail):** $0.36 at what-if prices from `negotiated.json`, ratesVersion `retail-fixture` • 1 turn unpriced • estimated: 1 turn reported no cache counters\n",
            markdown.Out);
        Assert.DoesNotContain(temp.Path, markdown.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement source = document.RootElement.GetProperty("pricingSource");
        Assert.Equal("negotiated.json", source.GetProperty("path").GetString());
        Assert.Equal(
            ["schemaVersion", "repository", "branch", "commit", "generatedAt", "generator", "pricingSource", "totals"],
            document.RootElement.EnumerateObject().Select(property => property.Name).Take(8));
        Assert.Equal(0.3584m, document.RootElement.GetProperty("totals").GetProperty("apiEquivalentUsd").GetDecimal());
        Assert.Equal(56m, document.RootElement.GetProperty("totals").GetProperty("aiCredits").GetDecimal());
    }

    // The JSON without its API-equivalent fields and pricingSource, at every depth.
    private static string WithoutApiEquivalent(JsonNode node)
    {
        JsonNode copy = node.DeepClone();
        Strip(copy);
        return copy.ToJsonString();

        static void Strip(JsonNode? item)
        {
            if (item is JsonObject value)
            {
                foreach (string name in new[] { "apiEquivalentUsd", "apiEquivalentUnpricedSpans", "apiEquivalentEstimatedSpans", "billedToApiRatio", "pricingSource" })
                {
                    value.Remove(name);
                }

                foreach (KeyValuePair<string, JsonNode?> property in value)
                {
                    Strip(property.Value);
                }
            }
            else if (item is JsonArray array)
            {
                foreach (JsonNode? element in array)
                {
                    Strip(element);
                }
            }
        }
    }

    // The LedgerSeeds ledger, recorded with the built-in rates, the GitFixture repository, and negotiated.json, a complete
    // rate card with RateFixtures.RetailTestRatesJson's fictional list prices.
    private static async Task<CliHarness> SeededHarnessAsync(string root)
    {
        GitFixture.Create(root);
        CliHarness harness = new(root, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(Path.Combine(root, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        await LedgerSeeds.SeedChainsAsync(store, Path.Combine(root, "repo"), Path.Combine(root, "api"));
        string negotiated = Path.Combine(root, "negotiated.json");
        await File.WriteAllTextAsync(negotiated, RateFixtures.RetailTestRatesJson);
        await FileRetry.ReadAllTextAsync(negotiated);
        return harness;
    }

    // Every row of spans, each as its values separated by '|'.
    private static async Task<string[]> RowsAsync(string root)
    {
        await using SqliteConnection connection = new($"Data Source={Path.Combine(root, "data", "ledger.db")}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM spans ORDER BY id;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        List<string> rows = [];
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join(
                '|',
                Enumerable.Range(0, reader.FieldCount)
                    .Select(index => reader.IsDBNull(index)
                        ? "null"
                        : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture))));
        }

        return [.. rows];
    }
}
