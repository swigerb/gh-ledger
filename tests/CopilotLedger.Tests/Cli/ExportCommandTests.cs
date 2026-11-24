using System.Xml.Linq;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// gh ledger export: its options, files, standard output, header, summary cards, and coverage notes.
public sealed class ExportCommandTests
{
    private const string Report = "copilot-ledger-report.html";

    [Theory]
    [InlineData("--format|pdf", "--format must be html.")]
    [InlineData("--out|", "--out can't be blank.")]
    [InlineData("--out|report.txt", "--out must end with .html or .htm.")]
    [InlineData("--repo|x|--all-repos", "Use either --repo or --all-repos, not both.")]
    [InlineData("--pr|0", "--pr must be a positive integer.")]
    [InlineData("--conversation| ", "--conversation can't be blank.")]
    [InlineData("--since|0d", "Invalid --since value 0d. Use today, all, <N>d (1-3650), or yyyy-MM-dd.")]
    [InlineData("--time-zone|Mars/Base", "Invalid --time-zone value Mars/Base. Use an IANA time zone ID, such as America/New_York.")]
    [InlineData("--generated-at|2026-11-21T21:30:00", "Invalid --generated-at value 2026-11-21T21:30:00. Use a UTC time such as 2026-11-21T21:30:00Z.")]
    [InlineData("--generated-at|2026-11-21T21:30:00-05:00", "Invalid --generated-at value 2026-11-21T21:30:00-05:00. Use a UTC time such as 2026-11-21T21:30:00Z.")]
    [InlineData("--max-timeline-spans|100001", "--max-timeline-spans must be from 0 to 100000.")]
    [InlineData("--max-timeline-spans|-1", "--max-timeline-spans must be from 0 to 100000.")]
    public async Task EC1_InvalidOptionsWriteNothing(string args, string message)
    {
        using TempDirectory temp = new();
        CliHarness cli = Harness(temp.Path);

        CliRun run = await cli.RunAsync(["export", .. args.Split('|')]);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message + "\n", run.Error);
        Assert.False(File.Exists(Path.Combine(temp.Path, Report)));
        Assert.False(File.Exists(cli.Context.DatabasePath));
    }

    [Fact]
    public async Task EC2_AnExistingReportIsReplacedOnlyWithForce()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        string path = Path.Combine(temp.Path, Report);
        File.WriteAllText(path, "old");

        CliRun refused = await cli.RunAsync("export", "--all-repos");
        CliRun replaced = await cli.RunAsync("export", "--all-repos", "--force");

        Assert.Equal((1, "copilot-ledger-report.html already exists. Use --force to replace it.\n"), (refused.ExitCode, refused.Error));
        Assert.Equal((0, string.Empty), (replaced.ExitCode, replaced.Error));
        Assert.StartsWith("<!DOCTYPE html>\n", File.ReadAllText(path));
    }

    [Fact]
    public async Task EC3_ADirectoryOrAMissingFolderIsAnError()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        Directory.CreateDirectory(Path.Combine(temp.Path, "out.html"));

        CliRun directory = await cli.RunAsync("export", "--all-repos", "--out", "out.html", "--force");
        CliRun missing = await cli.RunAsync("export", "--all-repos", "--out", "missing/report.html");

        Assert.Equal((1, "out.html is a directory.\n"), (directory.ExitCode, directory.Error));
        Assert.Equal(1, missing.ExitCode);
        Assert.StartsWith("Could not write missing/report.html: ", missing.Error);
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task EC4_TheDefaultScopeIsTheCurrentRepository()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        string repo = GitFixture.Create(temp.Path);
        CliHarness cli = Harness(temp.Path);

        CliRun inside = await cli.RunInAsync(repo, "export");
        CliRun outside = await cli.RunAsync("export");
        CliRun all = await cli.RunInAsync(repo, "export", "--all-repos", "--force");

        Assert.Equal(
            (0, "Wrote copilot-ledger-report.html: 5 requests in swigerb/copilot-ledger (last 30 days).\n", string.Empty),
            (inside.ExitCode, inside.Out, inside.Error));
        Assert.Equal("Wrote copilot-ledger-report.html: 8 requests in all repositories (last 30 days).\n", outside.Out);
        Assert.Equal("Wrote copilot-ledger-report.html: 8 requests in all repositories (last 30 days).\n", all.Out);
        Assert.True(File.Exists(Path.Combine(repo, Report)));
        Assert.True(File.Exists(Path.Combine(temp.Path, Report)));
    }

    [Fact]
    public async Task EC5_SinceIsEvaluatedAtGeneratedAtInTheExportTimeZone()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        CliRun run = await cli.RunAsync(
            "export", "--all-repos", "--since", "today", "--generated-at", "2026-11-21T03:00:00Z", "--time-zone", "Test/New_York");

        Assert.Equal("Wrote copilot-ledger-report.html: 2 requests in all repositories (today).\n", run.Out);
        XDocument page = ExportFiles.Load(Path.Combine(temp.Path, Report));
        Assert.Equal("2026-11-20 00:00 to 2026-11-20 22:00", ExportFiles.Meta(page, "Window"));
        Assert.Equal("Test/New_York", ExportFiles.Meta(page, "Time zone"));
        Assert.Equal("2026-11-20 22:00 by gh-ledger 0.4.1", ExportFiles.Meta(page, "Generated"));
    }

    [Fact]
    public async Task EC6_TheCardsShowTheReportsTotals()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(("8", "8", "1 unpriced"), Card(page, "requests"));
        Assert.Equal(("65.95", "65.95", "$0.66"), Card(page, "credits"));
        Assert.Equal(("$0.32", "0.315", "billed by Microsoft Foundry"), Card(page, "byok"));
        Assert.Equal(("~$0.51*", "0.508", "a comparison, not billed"), Card(page, "api"));
        Assert.Equal(("$0.17", "0.1725", "cloud spend avoided"), Card(page, "local"));
        Assert.Equal(("40.0%", "0.4", "1 of 8 reported"), Card(page, "cache"));
        Assert.Equal(("6", "6", "prompts sent"), Card(page, "chains"));
    }

    [Fact]
    public async Task EC7_UnpricedAndUnreportedNeverShowAsZero()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(
            Path.Combine(temp.Path, "data", "ledger.db"),
            new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        await store.RecordAsync([Entry(ExportSeeds.GeneratedAt.AddHours(-1))]);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(("Unpriced", "Unpriced", "a comparison, not billed"), Card(page, "api"));
        Assert.Equal(("n/a", "n/a", "0 of 1 reported"), Card(page, "cache"));
        Assert.DoesNotContain("$0.00", ExportFiles.ById(page, "card-api").Value);
        Assert.DoesNotContain("0.0%", ExportFiles.ById(page, "card-cache").Value);
    }

    [Fact]
    public async Task EC8_CoverageStatesRequestsAndToolCallsSeparately()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        XDocument fixture = await ExportAsync(cli, temp.Path, "--all-repos");
        await store.RecordAsync([Entry(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero))]);
        XDocument older = await ExportAsync(cli, temp.Path, "--all-repos", "--force");
        await store.RecordImportedAsync([], Checkpoint(ambiguous: 2));
        XDocument deferred = await ExportAsync(cli, temp.Path, "--all-repos", "--force");

        Assert.Equal(
            ("Requests: complete.", "Tool calls: complete for this window."),
            (ExportFiles.ById(fixture, "coverage-requests").Value, ExportFiles.ById(fixture, "coverage-tool-calls").Value));
        Assert.Equal(
            [
                "8 requests: 7 recorded live, 1 imported from Copilot session logs.",
                "1 imported request was left out because a live span recorded the same call.",
                "1 request has no price, so billed totals leave it out.",
                "6 of 8 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.",
                "1 priced span reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.",
                "7 requests reported no cache counters; cache hit ratios leave them out.",
            ],
            Notes(fixture));
        Assert.Equal(
            ("Requests: complete.", "Tool calls: partial; see the notes."),
            (ExportFiles.ById(older, "coverage-requests").Value, ExportFiles.ById(older, "coverage-tool-calls").Value));
        Assert.Contains(
            "Tool calls are kept for 7 days, so the 1 request before 2026-11-14 16:30 may be missing its tool calls.",
            Notes(older));
        Assert.Equal("Requests: may be incomplete; see the notes.", ExportFiles.ById(deferred, "coverage-requests").Value);
        Assert.Contains(
            "Across the whole ledger, gh ledger import deferred 2 requests whose sessions have live spans it couldn't match; some may belong in this report.",
            Notes(deferred));
    }

    [Fact]
    public async Task EC9_TheDocumentIsSelfContainedWellFormedAndLfOnly()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        await cli.RunAsync("export", "--all-repos");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        XDocument page = ExportFiles.Parse(html);
        Assert.StartsWith("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\" />\n", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "Content-Security-Policy"));
        Assert.Contains(
            "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:\" />",
            html);
        Assert.Equal("CopilotLedger report: all repositories (last 30 days)", page.Root!.Element("head")!.Element("title")!.Value);
        Assert.Equal("section", ExportFiles.ById(page, "summary").Name.LocalName);
        Assert.Equal("section", ExportFiles.ById(page, "coverage").Name.LocalName);
        Assert.DoesNotContain(page.Descendants(), element => (string?)element.Attribute("id") == "audit-findings");
    }

    [Fact]
    public async Task EC10_LocalPathsAreShownOnlyWhenIncluded()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        string root = ExportSeeds.LocalRoot(temp.Path);

        CliRun included = await cli.RunAsync("export", "--repo", root, "--include-local-paths");
        XDocument page = ExportFiles.Load(Path.Combine(temp.Path, Report));
        CliRun hidden = await cli.RunAsync("export", "--repo", root, "--force");
        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));

        Assert.Equal("Wrote copilot-ledger-report.html: 1 request in tools (last 30 days).\n", included.Out);
        Assert.Equal(included.Out, hidden.Out);
        Assert.Equal(root, ExportFiles.Meta(page, "Repository"));
        Assert.Equal("tools · last 30 days", page.Descendants("p").Single(p => (string?)p.Attribute("class") == "scope").Value);
        Assert.DoesNotContain("private-7c1d", html);
        Assert.DoesNotContain(ExportFiles.Parse(html).Descendants("dt"), dt => dt.Value == "Repository");
    }

    [Fact]
    public async Task EC11_RequestsThatLiveSpansReplacedWithoutAMatchMayBeMissing()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await ExportSeeds.SupersedeUnmatchedAsync(store);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(
            ("Requests: may be incomplete; see the notes.", "Tool calls: complete for this window."),
            (ExportFiles.ById(page, "coverage-requests").Value, ExportFiles.ById(page, "coverage-tool-calls").Value));
        Assert.Equal(
            [
                "9 requests: 8 recorded live, 1 imported from Copilot session logs.",
                "1 imported request was left out because a live span recorded the same call.",
                "1 imported request was left out because live spans of its session replaced it without matching it; they may not cover it.",
                "1 request has no price, so billed totals leave it out.",
                "7 of 9 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.",
                "1 priced span reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.",
                "8 requests reported no cache counters; cache hit ratios leave them out.",
            ],
            Notes(page));
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
    {
        FakeCliPlatform platform = new();
        platform.TimeZones["Test/New_York"] = TestZones.NewYork;
        return new CliHarness(root, platform, new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));
    }

    // Runs export in root, which must succeed, and loads the report.
    private static async Task<XDocument> ExportAsync(CliHarness cli, string root, params string[] args)
    {
        CliRun run = await cli.RunAsync(["export", .. args]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return ExportFiles.Load(Path.Combine(root, Report));
    }

    private static (string Value, string Title, string Note) Card(XDocument page, string id)
    {
        XElement card = ExportFiles.ById(page, "card-" + id);
        XElement value = card.Elements("span").Single(span => (string?)span.Attribute("class") == "card-value");
        XElement note = card.Elements("span").Single(span => (string?)span.Attribute("class") == "card-note");
        return (value.Value, (string)value.Attribute("title")!, note.Value);
    }

    private static IReadOnlyList<string> Notes(XDocument page) =>
        ExportFiles.ById(page, "coverage-notes").Elements("li").Select(item => item.Value).ToArray();

    // A mai-code-2 request without cache counters at start, which has no API-equivalent price.
    private static LedgerEntry Entry(DateTimeOffset start)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: start,
            requestModel: "mai-code-2",
            conversationId: "conv-x",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a",
            SpanId = "9a00000000000001",
        };
        return new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null);
    }

    private static ImportSessionCheckpoint Checkpoint(long ambiguous) => new()
    {
        Source = ImportSourceKind.Copilot,
        LogId = "session-log-1",
        LogSizeBytes = 10,
        LogLastWriteTime = ExportSeeds.GeneratedAt,
        LogSha256 = new string('a', 64),
        Complete = true,
        Recorded = 0,
        Duplicates = 0,
        OverlapSkipped = 0,
        AmbiguousOverlap = ambiguous,
    };
}
