using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using CopilotLedger.Cli;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The export's tables, the breaker and budget cards, sorting keys, and stylesheet.
public sealed class ExportTableTests
{
    private const string Report = "copilot-ledger-report.html";

    [Fact]
    public async Task EH1_TheRollupTablesMatchTheReports()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        XDocument page = await ExportAsync(cli, temp.Path, "--all-repos");
        using JsonDocument branch = JsonDocument.Parse((await cli.RunAsync("branch", "--all-repos", "--json")).Out);
        using JsonDocument agents = JsonDocument.Parse((await cli.RunAsync("agents", "--json")).Out);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        Assert.Equal(
            [
                "swigerb/copilot-ledger|feature/ledger|3|0.598|0.315",
                "tools|main|1|0.06|0",
                "(none)|(none)|2|0.0015|0",
                "swigerb/copilot-ledger|main|2|0|0",
            ],
            Rows(page, "table-by-branch").Select(row => $"{row[0].Value}|{row[1].Value}|{row[2].Value}|{Title(row[5])}|{Title(row[6])}"));
        Assert.Equal(
            branch.RootElement.GetProperty("rows").EnumerateArray().Select(row =>
                $"{Shown(row.GetProperty("repository").GetString()!)}|{row.GetProperty("branch").GetString()}|{row.GetProperty("spans")}|{row.GetProperty("aiCreditsUsd").GetRawText()}|{row.GetProperty("foundryByokUsd").GetRawText()}"),
            Rows(page, "table-by-branch").Select(row => $"{row[0].Value}|{row[1].Value}|{row[2].Value}|{Title(row[5])}|{Title(row[6])}"));
        Assert.Equal(
            agents.RootElement.GetProperty("rows").EnumerateArray().Select(row =>
                $"{row.GetProperty("agent").GetString() ?? "(none)"}|{row.GetProperty("spans")}|{row.GetProperty("aiCreditsUsd").GetRawText()}|{row.GetProperty("localSavedUsd").GetRawText()}"),
            Rows(page, "table-by-agent").Select(row => $"{row[0].Value}|{row[1].Value}|{Title(row[4])}|{Title(row[7])}"));
        Assert.Equal(
            (await snapshot.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All)).Select(row =>
                $"{row.Model}|{row.SpanCount}|{Exact(row.AiCreditsUsd)}|{(row.ApiEquivalentUsd is decimal api ? Exact(api) : "Unpriced")}"),
            Rows(page, "table-by-model").Select(row => $"{row[0].Value}|{row[1].Value}|{Title(row[4])}|{Title(row[6])}"));
        Assert.Equal(
            [
                "GitHub AI credits|claude-opus-5.5|2|0.508",
                "Foundry BYOK|gpt-6.1-sol|1|0",
                "GitHub AI credits|mai-code-2|3|0.1515",
                "Local|qwen3.8-27b|1|0",
                "GitHub AI credits|gpt-unknown|1|0",
            ],
            Rows(page, "table-by-channel-model").Select(row => $"{row[0].Value}|{row[1].Value}|{row[2].Value}|{Title(row[5])}"));

        string Shown(string key) => key == ExportSeeds.LocalRoot(temp.Path) ? "tools" : key;
    }

    [Fact]
    public async Task EH2_PromptChainsAreTheTopChainsByBilledCost()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        XElement table = ExportFiles.ById(page, "table-prompt-chains");
        Assert.Equal("Top 6 of 6 prompt chains by billed cost", table.Element("caption")!.Value);
        Assert.Equal(
            [
                "1", "2026-11-21 09:00", "swigerb/copilot-ledger", "feature/ledger", "explore", "claude-opus-5.5, mai-code-2", "2",
                "300,000", "30,000", "59.80", "$0.60", "$0.00", "$0.45*", "$0.00", ExportSeeds.A,
            ],
            Rows(page, "table-prompt-chains")[0].Select(cell => cell.Value));
        Assert.Equal(
            [ExportSeeds.A, ExportSeeds.B, ExportSeeds.D, ImportEntries.Copilot("k1", "conv-i", null).Span.TraceId, ExportSeeds.J, ExportSeeds.C],
            Rows(page, "table-prompt-chains").Select(row => row[14].Value));
    }

    [Fact]
    public async Task EH3_CacheTablesShowNotAvailableAndPartialTotals()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        IReadOnlyList<string> opus = Row(page, "table-cache-model", "claude-opus-5.5");
        IReadOnlyList<string> mai = Row(page, "table-cache-model", "mai-code-2");
        Assert.Equal(["claude-opus-5.5", "2", "1", "110,000", "40,000*", "0*", "60,000*", "11,000", "40.0%"], opus);
        Assert.Equal(["mai-code-2", "3", "0", "202,000", "n/a", "n/a", "n/a", "20,200", "n/a"], mai);
        Assert.Equal(["(none)", "explore"], Rows(page, "table-cache-agent").Select(row => row[0].Value));
    }

    [Fact]
    public async Task EH4_BreakerTripsAndBudgetsHaveTablesAndCards()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(
            [
                "Retry churn|2026-11-21 11:06|Reset 2026-11-21 16:30|swigerb/copilot-ledger|main|-|-|5|" + ExportSeeds.C,
                "Credit velocity|2026-11-21 09:02|Open|swigerb/copilot-ledger|feature/ledger|$1.80|5 min|-|" + ExportSeeds.A,
            ],
            Rows(page, "table-breaker-trips").Select(row => string.Join('|', row.Select(cell => cell.Value))));
        Assert.Equal(
            ["swigerb/copilot-ledger|feature/ledger|50.00|91.30|Exceeded"],
            Rows(page, "table-budgets").Select(row => string.Join('|', row.Select(cell => cell.Value))));
        Assert.Equal(["91.3"], Rows(page, "table-budgets").Select(row => Title(row[3])));
        Assert.Equal("Circuit breaker 1 open 2 trips", ExportFiles.ById(page, "card-breaker").Value);
        Assert.Equal("Budgets 1 of 1 exceeded all-time spend", ExportFiles.ById(page, "card-budgets").Value);
    }

    [Fact]
    public async Task EH5_SortKeysAreRanksOfTheExactValues()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        foreach (XElement table in page.Descendants("table").Where(table => (string?)table.Attribute("class") == "data sortable"))
        {
            IReadOnlyList<IReadOnlyList<XElement>> rows = Rows(page, (string)table.Attribute("id")!);
            for (int column = 0; column < rows[0].Count; column++)
            {
                Assert.Equal(
                    Enumerable.Range(0, rows.Count),
                    rows.Select(row => int.Parse((string)row[column].Attribute("data-sort")!, NumberStyles.None, CultureInfo.InvariantCulture)).Order());
            }
        }

        IReadOnlyList<IReadOnlyList<XElement>> byModel = Rows(page, "table-by-model");
        Assert.Equal(
            ["0", "0", "0", "0.1515", "0.508"],
            byModel.OrderBy(row => Rank(row[4])).Select(row => Title(row[4])));
        Assert.Equal(
            ["Unpriced", "Unpriced", "Unpriced", "Unpriced", "0.508"],
            byModel.OrderBy(row => Rank(row[6])).Select(row => Title(row[6])));
        Assert.Equal(
            ["claude-opus-5.5", "gpt-6.1-sol", "gpt-unknown", "mai-code-2", "qwen3.8-27b"],
            byModel.OrderBy(row => Rank(row[0])).Select(row => row[0].Value));
    }

    [Fact]
    public async Task EH6_TheStyleElementHoldsTheStylesheetOnce()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");
        using Stream resource = typeof(GhLedgerCli).Assembly.GetManifestResourceStream("CopilotLedger.Cli.Export.Assets.report.css")!;
        using StreamReader reader = new(resource);
        string css = reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd('\n');

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));

        Assert.Contains("<style>\n" + css + "\n</style>\n", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<style"));
        Assert.True(css.Length > 1_000);
        Assert.DoesNotContain("</style", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("url(", css.Replace("url(#hatch)", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EH7_LocalRootsAreFolderNamesUnlessIncluded()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        string root = ExportSeeds.LocalRoot(temp.Path);

        XDocument hidden = await ExportAsync(cli, temp.Path, "--all-repos");
        string hiddenHtml = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        XDocument included = await ExportAsync(cli, temp.Path, "--all-repos", "--include-local-paths", "--force");

        Assert.Contains("tools", Rows(hidden, "table-by-branch").Select(row => row[0].Value));
        Assert.DoesNotContain("private-7c1d", hiddenHtml);
        Assert.Contains(root, Rows(included, "table-by-branch").Select(row => row[0].Value));
        Assert.Contains(root, Rows(included, "table-prompt-chains").Select(row => row[2].Value));
    }

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

    private static async Task<XDocument> ExportAsync(CliHarness cli, string root, params string[] args)
    {
        CliRun run = await cli.RunAsync(["export", .. args]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return ExportFiles.Load(Path.Combine(root, Report));
    }

    // The table's body rows, each as its cells.
    private static IReadOnlyList<IReadOnlyList<XElement>> Rows(XDocument page, string id) =>
        [.. ExportFiles.ById(page, id).Element("tbody")!.Elements("tr").Select(row => (IReadOnlyList<XElement>)[.. row.Elements("td")])];

    // The texts of the table's row whose first cell is key.
    private static IReadOnlyList<string> Row(XDocument page, string id, string key) =>
        [.. Rows(page, id).Single(row => row[0].Value == key).Select(cell => cell.Value)];

    private static string Title(XElement cell) => (string)cell.Attribute("title")!;

    private static int Rank(XElement cell) =>
        int.Parse((string)cell.Attribute("data-sort")!, NumberStyles.None, CultureInfo.InvariantCulture);

    private static string Exact(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);
}
