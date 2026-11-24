using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Storage.Import;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// gh ledger export: its options, files, standard output, header, summary cards, and coverage notes.
public sealed class ExportCommandTests
{
    private const string Report = "copilot-ledger-report.html";
    private const string CountedSession = "c0c0c0c0-0000-4000-8000-000000000020";
    private const string UncountedSession = "c0c0c0c0-0000-4000-8000-000000000021";

    // A Copilot session that starts within the tool history of ExportSeeds' ledger: after its earliest link, 11-20 12:00.
    private static readonly DateTimeOffset RecentImport = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

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
        Assert.Equal((1, "Could not write missing/report.html: its folder doesn't exist.\n"), (missing.ExitCode, missing.Error));
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
        Assert.Equal("2026-11-20 22:00 by gh-ledger 0.6.0", ExportFiles.Meta(page, "Generated"));
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
        await store.RecordSpanLinksAsync([ToolCall("9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a", new DateTimeOffset(2026, 11, 10, 12, 0, 30, TimeSpan.Zero))]);
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
        Assert.Equal("gh-ledger report: all repositories (last 30 days)", page.Root!.Element("head")!.Element("title")!.Value);
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

    // Only Windows refuses to replace a read-only file or to move one that's open; on Linux, the folder's permissions decide.
    [Fact]
    public async Task EC12_AFailedReplaceLeavesBothFilesAsTheyWere()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        string report = Path.Combine(temp.Path, Report);
        string bundle = Path.ChangeExtension(report, ".zip");
        File.WriteAllText(report, "old report");
        File.WriteAllText(bundle, "old bundle");

        File.SetAttributes(report, FileAttributes.ReadOnly);
        CliRun readOnly = await cli.RunAsync("export", "--all-repos", "--bundle", "--force");
        File.SetAttributes(report, FileAttributes.Normal);
        CliRun open;
        using (new FileStream(bundle, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            open = await cli.RunAsync("export", "--all-repos", "--bundle", "--force");
        }

        Assert.Equal((1, "Could not write copilot-ledger-report.html: access is denied.\n"), (readOnly.ExitCode, readOnly.Error));
        Assert.Equal(
            (1, "Could not write copilot-ledger-report.zip: the file system reported error 0x80070020.\n"),
            (open.ExitCode, open.Error));
        Assert.Equal(("old report", "old bundle"), (File.ReadAllText(report), File.ReadAllText(bundle)));
        Assert.Equal([report, bundle], Directory.GetFiles(temp.Path).Order(StringComparer.Ordinal));
    }

    // The daemon deleted the tool calls before 11-21, so the two requests of 11-20 may have lost theirs, though they're
    // within 7 days of the generation time.
    [Fact]
    public async Task EC13_ToolHistoryStartsNoEarlierThanTheEarliestLinkKept()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await store.RecordSpanLinksAsync([ToolCall(ExportSeeds.E, new DateTimeOffset(2026, 11, 21, 13, 0, 0, TimeSpan.Zero))]);
        await store.PruneSpanLinksAsync(new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero));

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos", "--generated-at", "2026-11-21T21:30:00Z");

        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        JsonElement history = data.RootElement.GetProperty("coverage").GetProperty("toolHistory");
        Assert.Equal(
            ("2026-11-21T13:00:00.0000000+00:00", 2, false),
            (history.GetProperty("retainedFromUtc").GetString(), history.GetProperty("spansBeforeRetention").GetInt32(), history.GetProperty("complete").GetBoolean()));
        Assert.Equal("Tool calls: partial; see the notes.", ExportFiles.ById(page, "coverage-tool-calls").Value);
        Assert.Contains(
            "The ledger's tool history starts at 2026-11-21 08:00, so the 2 requests before it may be missing their tool calls.",
            Notes(page));
    }

    // Even with --force, a read-only report or bundle stops the export before it writes anything, on every OS.
    [Theory]
    [InlineData("copilot-ledger-report.html")]
    [InlineData("copilot-ledger-report.zip")]
    public async Task EC14_AReadOnlyFileIsNotReplacedEvenWithForce(string readOnly)
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        string report = Path.Combine(temp.Path, Report);
        string bundle = Path.ChangeExtension(report, ".zip");
        File.WriteAllText(report, "old report");
        File.WriteAllText(bundle, "old bundle");

        File.SetAttributes(Path.Combine(temp.Path, readOnly), FileAttributes.ReadOnly);
        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--bundle", "--force");
        File.SetAttributes(Path.Combine(temp.Path, readOnly), FileAttributes.Normal);

        Assert.Equal((1, $"Could not write {readOnly}: access is denied.\n"), (run.ExitCode, run.Error));
        Assert.Equal(("old report", "old bundle"), (File.ReadAllText(report), File.ReadAllText(bundle)));
        Assert.Equal([report, bundle], Directory.GetFiles(temp.Path).Order(StringComparer.Ordinal));
    }

    // Without --force, a report or bundle that another program writes during the export is refused and kept: the fake platform
    // writes it after the checks, as the export writes its report's temporary file, so before the export decides whether to
    // move a bundle aside.
    [Theory]
    [InlineData("copilot-ledger-report.zip")]
    [InlineData("copilot-ledger-report.html")]
    public async Task EC15_AFileThatAppearsDuringTheExportIsKeptWithoutForce(string name)
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        string competing = Path.Combine(temp.Path, name);
        string reportTemp = $"{Path.Combine(temp.Path, Report)}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.tmp";
        FakeCliPlatform platform = new()
        {
            FileOperationHandler = (operation, path) =>
            {
                if (operation == "write" && path == reportTemp)
                {
                    File.WriteAllText(competing, "another program's file");
                }
            },
        };
        CliHarness cli = new(temp.Path, platform, new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

        CliRun run = await cli.RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal((1, $"{name} already exists. Use --force to replace it.\n"), (run.ExitCode, run.Error));
        Assert.Equal("another program's file"u8.ToArray(), File.ReadAllBytes(competing));
        Assert.Equal([competing], Directory.GetFiles(temp.Path));
    }

    // When the report can't be moved into place and the new bundle can't be deleted, standard error says that they don't
    // match: the fake platform fails both.
    [Fact]
    public async Task EC16_ABundleThatCantBeTakenBackIsReported()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        string bundle = Path.Combine(temp.Path, "copilot-ledger-report.zip");
        FakeCliPlatform platform = new()
        {
            FileOperationHandler = (operation, path) =>
            {
                if (operation == "move" && path == Path.Combine(temp.Path, Report))
                {
                    throw new UnauthorizedAccessException();
                }

                if (operation == "delete" && path == bundle)
                {
                    throw new IOException();
                }
            },
        };
        CliHarness cli = new(temp.Path, platform, new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

        CliRun run = await cli.RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal(
            (1, "Could not write copilot-ledger-report.html: access is denied.\ncopilot-ledger-report.zip was written, but copilot-ledger-report.html wasn't, so they don't match.\n"),
            (run.ExitCode, run.Error));
        Assert.Equal([bundle], Directory.GetFiles(temp.Path));
    }

    // An export that can't delete the bundle it replaced doesn't succeed: the fake platform fails the deletion.
    [Fact]
    public async Task EC17_AReplacedBundleThatCantBeDeletedIsReported()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        string bundle = Path.Combine(temp.Path, "copilot-ledger-report.zip");
        string backup = $"{bundle}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.bak";
        File.WriteAllText(bundle, "old bundle");
        FakeCliPlatform platform = new()
        {
            FileOperationHandler = (operation, path) =>
            {
                if (operation == "delete" && path == backup)
                {
                    throw new IOException();
                }
            },
        };
        CliHarness cli = new(temp.Path, platform, new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

        CliRun run = await cli.RunAsync("export", "--all-repos", "--bundle", "--force");

        Assert.Equal(
            (1, string.Empty, $"copilot-ledger-report.html and copilot-ledger-report.zip were written, but the replaced copilot-ledger-report.zip is kept as {Path.GetFileName(backup)}: it couldn't be deleted.\n"),
            (run.ExitCode, run.Out, run.Error));
        Assert.Equal("old bundle", File.ReadAllText(backup));
        Assert.Equal(
            [Path.Combine(temp.Path, Report), bundle, backup],
            Directory.GetFiles(temp.Path).Order(StringComparer.Ordinal));
    }

    // With every link deleted, the ledger keeps no tool history, so no request's tool calls count as kept, even at a past
    // --generated-at.
    [Fact]
    public async Task EC18_ALedgerWithoutLinksKeepsNoToolHistory()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await store.PruneSpanLinksAsync(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos", "--generated-at", "2026-11-21T21:30:00Z");

        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        JsonElement history = data.RootElement.GetProperty("coverage").GetProperty("toolHistory");
        Assert.Equal(
            ("2026-11-21T21:30:00.0000000+00:00", 8, false),
            (history.GetProperty("retainedFromUtc").GetString(), history.GetProperty("spansBeforeRetention").GetInt32(), history.GetProperty("complete").GetBoolean()));
        Assert.Contains("The ledger keeps no tool history, so the 8 requests may be missing their tool calls.", Notes(page));
    }

    // A Codex rollout records no tool calls, so a recent request imported from one leaves tool history incomplete, and the page,
    // the manifest, and the summary say so.
    [Fact]
    public async Task EC19_ACodexImportLeavesToolHistoryIncomplete()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await store.RecordImportedAsync([ImportEntries.Copilot("k7", "conv-codex", null) with { Source = ImportSourceKind.Codex }], null);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos", "--bundle");

        IReadOnlyList<(string Name, byte[] Bytes)> entries = ExportFiles.ReadZip(Path.Combine(temp.Path, "copilot-ledger-report.zip"));
        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        using JsonDocument manifest = JsonDocument.Parse(entries.Single(entry => entry.Name == "manifest.json").Bytes);
        using JsonDocument summary = JsonDocument.Parse(entries.Single(entry => entry.Name == "telemetry/summary.json").Bytes);
        JsonDocument[] documents = [data, manifest, summary];
        foreach (JsonElement coverage in documents.Select(document => document.RootElement.GetProperty("coverage")))
        {
            JsonElement history = coverage.GetProperty("toolHistory");
            Assert.Equal(
                (false, 1, 0, 0),
                (history.GetProperty("complete").GetBoolean(),
                 history.GetProperty("importedRequestsWithoutToolData").GetInt32(),
                 history.GetProperty("spansBeforeRetention").GetInt32(),
                 history.GetProperty("importedToolCallsWithoutDetail").GetInt32()));
            Assert.Contains(
                "imported-requests-without-tool-data",
                coverage.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()));
        }

        Assert.Equal("Tool calls: partial; see the notes.", ExportFiles.ById(page, "coverage-tool-calls").Value);
        Assert.Contains("1 imported request has no tool-call count in its session log, so it may be missing its tool calls.", Notes(page));
    }

    // Copilot imports through the real reader keep tool history complete: a usage record that reported 0 tool calls, and a
    // compaction, which makes none, both store a count of 0.
    [Fact]
    public async Task EC20_CopilotImportsKeepToolHistoryComplete()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        await ImportCopilotAsync(
            temp.Path,
            new CopilotSessionLogs(CountedSession, start: RecentImport)
                .Usage("msg_zero", "55555555-5555-4555-8555-555555555555", numToolCalls: 0)
                .Compaction("66666666-6666-4666-8666-666666666666"),
            recorded: 2);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(["0", "0"], await StoredToolCallCountsAsync(temp.Path, CountedSession));
        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        JsonElement history = data.RootElement.GetProperty("coverage").GetProperty("toolHistory");
        Assert.Equal((true, 0), (history.GetProperty("complete").GetBoolean(), history.GetProperty("importedRequestsWithoutToolData").GetInt32()));
        Assert.Equal("Tool calls: complete for this window.", ExportFiles.ById(page, "coverage-tool-calls").Value);
    }

    // A Copilot usage record whose numToolCalls is missing or invalid has no tool-call count, so it may be missing tool
    // calls: the real reader imports it without one, and the export counts it.
    [Theory]
    [InlineData("missing")]
    [InlineData("negative")]
    public async Task EC21_ACopilotRequestWithoutAToolCallCountLeavesToolHistoryIncomplete(string count)
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        Action<JsonObject> change = count == "missing"
            ? usage => usage.Remove("numToolCalls")
            : usage => usage["numToolCalls"] = -1;
        await ImportCopilotAsync(
            temp.Path,
            new CopilotSessionLogs(UncountedSession, start: RecentImport)
                .Usage("msg_unknown", "44444444-4444-4444-8444-444444444444", change: change),
            recorded: 1);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(["null"], await StoredToolCallCountsAsync(temp.Path, UncountedSession));
        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        JsonElement coverage = data.RootElement.GetProperty("coverage");
        JsonElement history = coverage.GetProperty("toolHistory");
        Assert.Equal(
            (false, 1, 0, 0),
            (history.GetProperty("complete").GetBoolean(),
             history.GetProperty("importedRequestsWithoutToolData").GetInt32(),
             history.GetProperty("spansBeforeRetention").GetInt32(),
             history.GetProperty("importedToolCallsWithoutDetail").GetInt32()));
        Assert.Contains(
            "imported-requests-without-tool-data",
            coverage.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()));
        Assert.Equal("Tool calls: partial; see the notes.", ExportFiles.ById(page, "coverage-tool-calls").Value);
        Assert.Contains("1 imported request has no tool-call count in its session log, so it may be missing its tool calls.", Notes(page));
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

    // Imports a Copilot session log through the real reader, as gh ledger import does at ExportSeeds.GeneratedAt, into the
    // ledger at <root>/data/ledger.db, which must record the given number of requests.
    private static async Task ImportCopilotAsync(string root, CopilotSessionLogs log, long recorded)
    {
        string home = Path.Combine(root, "copilot");
        log.Write(home);
        ImportReport report = await new LedgerImporter(
            JsonRateCardCalculator.LoadEmbedded(),
            new ManualTimeProvider(ExportSeeds.GeneratedAt)).RunAsync(new ImportRequest
            {
                DatabasePath = Path.Combine(root, "data", "ledger.db"),
                Sources = [new ImportSourceRequest(new CopilotSessionLogSource(), home)],
            });
        ImportSourceReport copilot = Assert.Single(report.Sources);
        Assert.Equal((true, recorded), (copilot.Complete, copilot.Recorded));
    }

    // The imported_tool_call_count of a session's requests, by start time; NULL as "null".
    private static Task<IReadOnlyList<string>> StoredToolCallCountsAsync(string root, string session) =>
        LedgerSql.RowsAsync(
            Path.Combine(root, "data", "ledger.db"),
            "SELECT imported_tool_call_count FROM spans WHERE conversation_id = @p0 ORDER BY start_time_unix_ms;",
            session);

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

    // A bash call in trace at start.
    private static GenAiSpanLink ToolCall(string trace, DateTimeOffset start) => new()
    {
        TraceId = trace,
        SpanId = trace[..2] + "00000000000099",
        OperationName = "execute_tool",
        ConversationId = "conv-x",
        StartTime = start,
        ToolName = "bash",
        ToolExitCode = 0,
    };

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
