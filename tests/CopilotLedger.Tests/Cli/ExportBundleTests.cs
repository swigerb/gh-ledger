using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The bundle: its entries, manifest, telemetry files, and how it's written.
public sealed class ExportBundleTests
{
    private const string Report = "copilot-ledger-report.html";
    private const string Bundle = "copilot-ledger-report.zip";

    private static readonly string[] Telemetry =
    [
        "report.html", "telemetry/audit-findings.json", "telemetry/prompt-chains.json", "telemetry/spans.json", "telemetry/summary.json",
        "telemetry/tool-calls.json",
    ];

    private static readonly HashSet<string> MoneyNames =
    [
        "aiCredits", "aiCreditsUsd", "foundryByokUsd", "localSavedUsd", "apiEquivalentUsd", "costUsd", "savedUsd", "windowCostUsd",
        "budgetAiCredits", "spentAiCredits",
    ];

    [Fact]
    public async Task EB1_TheZipHasSevenEntriesWithFixedDatesInOrdinalOrder()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        CliRun run = await ExportAsync(temp.Path, "--all-repos", "--bundle");

        Assert.Equal(
            "Wrote copilot-ledger-report.html: 8 requests in all repositories (last 30 days).\n" +
            "Wrote copilot-ledger-report.zip: report.html, manifest.json, and 5 telemetry files.\n",
            run.Out);
        Assert.Equal(["manifest.json", .. Telemetry], ExportFiles.ReadZip(Path.Combine(temp.Path, Bundle)).Select(entry => entry.Name));
        Assert.All(
            ExportFiles.LocalHeaders(Path.Combine(temp.Path, Bundle)),
            header => Assert.Equal((0x0000, 0x0021, 8), (header.Time, header.Date, header.Method)));
        Assert.Equal(File.ReadAllBytes(Path.Combine(temp.Path, Report)), Entry(temp.Path, "report.html"));
    }

    [Fact]
    public async Task EB2_TheManifestHashesEveryOtherEntry()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle");

        using JsonDocument manifest = Json(temp.Path, "manifest.json");
        JsonElement root = manifest.RootElement;
        Assert.Equal((1, "gh-ledger", "0.4.1"), (root.GetProperty("schemaVersion").GetInt32(), root.GetProperty("tool").GetString(), root.GetProperty("toolVersion").GetString()));
        Assert.Equal(
            Telemetry.Select(name => $"{name}|{Entry(temp.Path, name).Length.ToString(CultureInfo.InvariantCulture)}|{Convert.ToHexStringLower(SHA256.HashData(Entry(temp.Path, name)))}"),
            root.GetProperty("files").EnumerateArray().Select(file =>
                $"{file.GetProperty("path").GetString()}|{file.GetProperty("bytes").GetInt64().ToString(CultureInfo.InvariantCulture)}|{file.GetProperty("sha256").GetString()}"));
        Assert.All(Telemetry.Skip(1), name => Assert.EndsWith("}\n", Encoding.UTF8.GetString(Entry(temp.Path, name))));
    }

    [Fact]
    public async Task EB3_MoneyIsExactAndSpansAddUpToTheTotalsWhateverTheCap()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle", "--max-timeline-spans", "1");

        using JsonDocument spans = Json(temp.Path, "telemetry/spans.json");
        using JsonDocument summary = Json(temp.Path, "telemetry/summary.json");
        foreach (string name in Telemetry.Skip(1))
        {
            using JsonDocument document = Json(temp.Path, name);
            Assert.All(MoneyValues(document.RootElement), raw =>
                Assert.Equal(decimal.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture).ToString("0.############", CultureInfo.InvariantCulture), raw));
        }

        JsonElement[] records = [.. spans.RootElement.GetProperty("spans").EnumerateArray()];
        JsonElement totals = summary.RootElement.GetProperty("totals");
        Assert.Equal(8, records.Length);
        Assert.Equal(8, totals.GetProperty("spans").GetInt64());
        Assert.Equal(1, records.Count(span => span.GetProperty("pricingStatus").GetString() is not ("priced" or "local_offload")));
        Assert.Equal(0.6595m, Sum(records, "costUsd", "github_ai_credits"));
        Assert.Equal(totals.GetProperty("aiCreditsUsd").GetDecimal(), Sum(records, "costUsd", "github_ai_credits"));
        Assert.Equal(65.95m, Sum(records, "aiCredits", null));
        Assert.Equal(totals.GetProperty("aiCredits").GetDecimal(), Sum(records, "aiCredits", null));
        Assert.Equal(0.315m, Sum(records, "costUsd", "foundry_byok"));
        Assert.Equal(totals.GetProperty("foundryByokUsd").GetDecimal(), Sum(records, "costUsd", "foundry_byok"));
        Assert.Equal(0.1725m, Sum(records, "savedUsd", null));
        Assert.Equal(totals.GetProperty("localSavedUsd").GetDecimal(), Sum(records, "savedUsd", null));
        Assert.Equal(0.508m, Sum(records, "apiEquivalentUsd", null));
        Assert.Equal(totals.GetProperty("apiEquivalentUsd").GetDecimal(), Sum(records, "apiEquivalentUsd", null));
    }

    [Fact]
    public async Task EB4_ToolCallsHaveNoEndOrDuration()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle");

        using JsonDocument document = Json(temp.Path, "telemetry/tool-calls.json");
        JsonElement[] calls = [.. document.RootElement.GetProperty("toolCalls").EnumerateArray()];
        Assert.Equal(4, calls.Length);
        Assert.All(calls, call => Assert.Equal(
            ["traceId", "spanId", "conversationId", "agent", "toolName", "startTime", "exitCode", "status", "failed", "ingestSource"],
            call.EnumerateObject().Select(property => property.Name)));
        Assert.Equal(
            ["bash|0|unset|False|import:copilot", "bash|0|unset|False|otlp", "bash|1|unset|True|otlp", "view||error|True|otlp"],
            calls.Select(call =>
                $"{call.GetProperty("toolName").GetString()}|{call.GetProperty("exitCode")}|{call.GetProperty("status").GetString()}|{call.GetProperty("failed").GetBoolean()}|{call.GetProperty("ingestSource").GetString()}"));
    }

    [Fact]
    public async Task EB5_TheManifestHoldsTheReplayInputsAndProvenance()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle");

        using JsonDocument manifest = Json(temp.Path, "manifest.json");
        using JsonDocument summary = Json(temp.Path, "telemetry/summary.json");
        JsonElement root = manifest.RootElement;
        Assert.Contains(
            "\n  \"generatedAtUtc\": \"2026-11-21T21:30:00.0000000+00:00\",\n",
            Encoding.UTF8.GetString(Entry(temp.Path, "manifest.json")),
            StringComparison.Ordinal);
        Assert.Equal(
            [
                "schemaVersion", "tool", "toolVersion", "generatedAtUtc", "ratesVersions", "apiRateCards", "filter", "timeZone", "options",
                "privacy", "files", "coverage",
            ],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("2026-11-21T21:30:00.0000000+00:00", root.GetProperty("generatedAtUtc").GetString());
        Assert.Equal(["2026-11-23", "test-fixture"], root.GetProperty("ratesVersions").EnumerateArray().Select(version => version.GetString()));
        Assert.Equal(["2026-11-23", "test-fixture"], root.GetProperty("apiRateCards").EnumerateArray().Select(card => card.GetProperty("ratesVersion").GetString()));
        JsonElement filter = root.GetProperty("filter");
        Assert.Equal(
            ("30d", "last 30 days", true, "2026-10-23T05:00:00.0000000+00:00"),
            (filter.GetProperty("since").GetString(), filter.GetProperty("period").GetString(), filter.GetProperty("allRepositories").GetBoolean(), filter.GetProperty("fromUtc").GetString()));
        JsonElement band = Assert.Single(root.GetProperty("timeZone").GetProperty("nightBandsUtc").EnumerateArray());
        Assert.Equal(
            ("2026-11-20T23:00:00.0000000+00:00", "2026-11-21T11:00:00.0000000+00:00"),
            (band.GetProperty("fromUtc").GetString(), band.GetProperty("toUtc").GetString()));
        Assert.Equal("{\n    \"maxTimelineSpans\": 20000,\n    \"includeLocalPaths\": false\n  }", root.GetProperty("options").GetRawText());
        JsonElement privacy = root.GetProperty("privacy");
        Assert.True(privacy.GetProperty("contentFree").GetBoolean());
        Assert.False(privacy.GetProperty("localPathsIncluded").GetBoolean());
        Assert.Equal(
            ["prompts", "responses", "tool arguments", "tool results", "file contents", "remote URLs"],
            privacy.GetProperty("excluded").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(summary.RootElement.GetProperty("coverage").GetRawText(), root.GetProperty("coverage").GetRawText());
        Assert.True(root.GetProperty("coverage").GetProperty("requestsComplete").GetBoolean());
    }

    [Fact]
    public async Task EB6_AnExistingBundleIsReplacedOnlyWithForce()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        File.WriteAllText(Path.Combine(temp.Path, Bundle), "old");
        CliHarness cli = Harness(temp.Path);

        CliRun refused = await cli.RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal((1, "copilot-ledger-report.zip already exists. Use --force to replace it.\n"), (refused.ExitCode, refused.Error));
        Assert.False(File.Exists(Path.Combine(temp.Path, Report)));
        Assert.Equal("old", File.ReadAllText(Path.Combine(temp.Path, Bundle)));
        CliRun replaced = await cli.RunAsync("export", "--all-repos", "--bundle", "--force");
        Assert.Equal((0, string.Empty), (replaced.ExitCode, replaced.Error));
        Assert.Equal(7, ExportFiles.ReadZip(Path.Combine(temp.Path, Bundle)).Count);
    }

    [Fact]
    public async Task EB7_AFailedBundleLeavesNeitherFileNorTemporaryFiles()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        Directory.CreateDirectory(Path.Combine(temp.Path, $"{Bundle}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.tmp"));

        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal(1, run.ExitCode);
        Assert.StartsWith("Could not write copilot-ledger-report.zip: ", run.Error);
        Assert.False(File.Exists(Path.Combine(temp.Path, Report)));
        Assert.False(File.Exists(Path.Combine(temp.Path, Bundle)));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task EB8_PromptChainsAreTheTopPromptsRows()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle");
        using JsonDocument topPrompts = JsonDocument.Parse((await cli.RunAsync("top-prompts", "--limit", "100", "--json")).Out);

        using JsonDocument chains = Json(temp.Path, "telemetry/prompt-chains.json");
        Assert.Equal((100, 6), (chains.RootElement.GetProperty("limit").GetInt32(), chains.RootElement.GetProperty("total").GetInt32()));
        Assert.Equal(
            topPrompts.RootElement.GetProperty("rows").EnumerateArray().Select(row => Shown(Flat(row), temp.Path)),
            chains.RootElement.GetProperty("chains").EnumerateArray().Select(Flat));
    }

    [Fact]
    public async Task EB9_TheSummaryRowsAreTheReportsRows()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle");
        using JsonDocument branch = JsonDocument.Parse((await cli.RunAsync("branch", "--all-repos", "--json")).Out);
        using JsonDocument agents = JsonDocument.Parse((await cli.RunAsync("agents", "--json")).Out);
        using JsonDocument cache = JsonDocument.Parse((await cli.RunAsync("status", "--cache", "--json")).Out);

        using JsonDocument summary = Json(temp.Path, "telemetry/summary.json");
        JsonElement root = summary.RootElement;
        Assert.Equal(branch.RootElement.GetProperty("rows").EnumerateArray().Select(row => Shown(Flat(row), temp.Path)), root.GetProperty("byBranch").EnumerateArray().Select(Flat));
        Assert.Equal(agents.RootElement.GetProperty("rows").EnumerateArray().Select(Flat), root.GetProperty("byAgent").EnumerateArray().Select(Flat));
        Assert.Equal(cache.RootElement.GetProperty("rows").EnumerateArray().Select(Flat), root.GetProperty("cacheByModel").EnumerateArray().Select(Flat));
        Assert.Equal(5, root.GetProperty("byChannelModel").GetArrayLength());
        Assert.Equal(6, root.GetProperty("totals").GetProperty("promptChains").GetInt32());
    }

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

    private static async Task<CliRun> ExportAsync(string root, params string[] args)
    {
        CliRun run = await Harness(root).RunAsync(["export", .. args]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return run;
    }

    private static byte[] Entry(string root, string name) =>
        ExportFiles.ReadZip(Path.Combine(root, Bundle)).Single(entry => entry.Name == name).Bytes;

    private static JsonDocument Json(string root, string name) => JsonDocument.Parse(Entry(root, name));

    // The raw text of every number property with a money name, anywhere in element.
    private static IEnumerable<string> MoneyValues(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property =>
            MoneyNames.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.Number
                ? [property.Value.GetRawText()]
                : MoneyValues(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(MoneyValues),
        _ => [],
    };

    // The sum of a money field over the spans, of one billing channel when channel isn't null.
    private static decimal Sum(IEnumerable<JsonElement> spans, string name, string? channel) =>
        spans.Where(span => channel is null || span.GetProperty("billingChannel").GetString() == channel)
            .Where(span => span.GetProperty(name).ValueKind == JsonValueKind.Number)
            .Sum(span => span.GetProperty(name).GetDecimal());

    // An object's properties as "name=value" lines, values as raw JSON without layout, so files with different indentation
    // compare equal.
    private static string Flat(JsonElement row) =>
        string.Join('\n', row.EnumerateObject().Select(property => $"{property.Name}={JsonSerializer.Serialize(property.Value)}"));

    // The flat row with ExportSeeds' local root shown as its folder name, as the export shows it.
    private static string Shown(string flat, string root) =>
        flat.Replace(JsonSerializer.Serialize(ExportSeeds.LocalRoot(root)), "\"tools\"", StringComparison.Ordinal);
}
