using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The export's timeline: rows, bars, tool ticks, night bands, the axis, truncation, and the ledger-data block.
public sealed partial class ExportTimelineTests
{
    private const string Report = "copilot-ledger-report.html";

    [Fact]
    public async Task TL1_TheTimelineDrawsEveryRequestAndToolCallBySession()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        XElement plot = ExportFiles.ById(page, "timeline-plot");
        Assert.Equal(8, Elements(plot, "rect", "span").Count());
        Assert.Equal(4, Elements(plot, "line", "tool").Count());
        Assert.Equal(2, Elements(plot, "line", "fail").Count());
        Assert.Single(Elements(plot, "rect", "night"));
        XElement[] labels = [.. page.Descendants("svg").Single(svg => (string?)svg.Attribute("class") == "timeline-labels").Elements("text")];
        Assert.Equal(
            ["1 · 11-20 07:00", "2 · 11-20 07:00", "3 · 11-21 09:00", "4 · 11-21 10:00", "5 · 11-21 11:00", "6 · 11-21 12:00"],
            labels.Select(text => text.Nodes().OfType<XText>().Single().Value));
        Assert.Equal(
            ["conv-i", "conv-j", "conv-a", "conv-b", "conv-c", "conv-d"],
            labels.Select(text => text.Element("title")!.Value));
        XElement opus = Elements(plot, "rect", "m0").First();
        Assert.Equal(("896.535", "34"), ((string)opus.Attribute("x")!, (string)opus.Attribute("y")!));
        Assert.Equal("09:00:00 · claude-opus-5.5 · (none) · 44.80 AI credits", opus.Element("title")!.Value);
        Assert.Equal("All 8 requests and 4 tool calls.", ExportFiles.ById(page, "timeline-note").Value);
    }

    [Fact]
    public async Task TL2_TheCapDrawsTheLatestRequestsAndToolCalls()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        const string Truncation =
            "The timeline draws the latest 2 of 8 requests and 2 of 4 tool calls; the totals and tables include them all.";

        XDocument full = await ExportAsync(cli, temp.Path, "--all-repos");
        CliRun run = await cli.RunAsync("export", "--all-repos", "--max-timeline-spans", "2", "--force");
        XDocument capped = ExportFiles.Load(Path.Combine(temp.Path, Report));

        Assert.Equal(
            $"Wrote copilot-ledger-report.html: 8 requests in all repositories (last 30 days).\n{Truncation}\n",
            run.Out);
        XElement plot = ExportFiles.ById(capped, "timeline-plot");
        Assert.Equal(
            ["11:05:00 · gpt-unknown · (none) · Unpriced (unknown_model)", "12:00:00 · claude-opus-5.5 · (none) · 6.00 AI credits"],
            Elements(plot, "rect", "span").Select(rect => rect.Element("title")!.Value));
        Assert.Equal(
            ["09:00:40 · bash · exit code 1", "10:00:10 · view · error"],
            plot.Descendants("line").Select(line => line.Element("title")!.Value));
        Assert.Equal(Truncation, ExportFiles.ById(capped, "timeline-note").Value);
        Assert.Equal(Truncation, ExportFiles.ById(capped, "coverage-notes").Elements("li").Last().Value);
        Assert.Equal(ExportFiles.ById(full, "summary").ToString(), ExportFiles.ById(capped, "summary").ToString());
    }

    [Fact]
    public async Task TL3_ACapOfZeroDrawsNothing()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--max-timeline-spans", "0");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        XDocument page = ExportFiles.Parse(html);
        using JsonDocument data = ExportFiles.LedgerData(html);
        Assert.EndsWith(
            "The timeline draws the latest 0 of 8 requests and 0 of 4 tool calls; the totals and tables include them all.\n",
            run.Out);
        Assert.Equal("No requests or tool calls to draw.", ExportFiles.ById(page, "timeline-note").Value);
        Assert.DoesNotContain(page.Descendants(), element => (string?)element.Attribute("id") is "timeline-plot" or "timeline-figure");
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("timeline").ValueKind);
        Assert.Equal(0, data.RootElement.GetProperty("timeZone").GetProperty("nightBandsUtc").GetArrayLength());
        Assert.True(data.RootElement.GetProperty("coverage").GetProperty("timeline").GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task TL4_NightBandsFollowDaylightSavingTime()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        await store.RecordAsync(
        [
            Entry("conv-dst", 1, new DateTimeOffset(2026, 3, 7, 20, 0, 0, TimeSpan.Zero)),
            Entry("conv-dst", 2, new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero)),
        ]);

        await ExportAsync(
            Harness(temp.Path),
            temp.Path,
            "--all-repos", "--since", "all", "--time-zone", "Test/New_York", "--generated-at", "2026-03-10T00:00:00Z");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        using JsonDocument data = ExportFiles.LedgerData(html);
        JsonElement zone = data.RootElement.GetProperty("timeZone");
        Assert.Equal(("Test/New_York", "Test/New_York"), (zone.GetProperty("requested").GetString(), zone.GetProperty("id").GetString()));
        Assert.Equal(
            [
                "2026-03-07T23:00:00.0000000+00:00|2026-03-08T10:00:00.0000000+00:00",
                "2026-03-08T22:00:00.0000000+00:00|2026-03-09T10:00:00.0000000+00:00",
            ],
            zone.GetProperty("nightBandsUtc").EnumerateArray().Select(band =>
                $"{band.GetProperty("fromUtc").GetString()}|{band.GetProperty("toUtc").GetString()}"));
        Assert.Equal(2, Elements(ExportFiles.ById(ExportFiles.Parse(html), "timeline-plot"), "rect", "night").Count());
    }

    [Fact]
    public async Task TL5_TheLedgerDataBlockHasTheFilterTimeZoneAndAxis()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        using JsonDocument data = ExportFiles.LedgerData(html);

        JsonElement root = data.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("2026-11-21T21:30:00.0000000+00:00", root.GetProperty("generatedAtUtc").GetString());
        Assert.Equal("30d", root.GetProperty("filter").GetProperty("since").GetString());
        Assert.Equal("2026-10-23T05:00:00.0000000+00:00", root.GetProperty("filter").GetProperty("fromUtc").GetString());
        Assert.Equal("2026-11-21T21:30:00.0000000+00:00", root.GetProperty("filter").GetProperty("toUtc").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("filter").GetProperty("repo").ValueKind);
        Assert.Equal("CopilotLedger-Test-0500", root.GetProperty("timeZone").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("timeZone").GetProperty("requested").ValueKind);
        Assert.Equal(1000, root.GetProperty("timeline").GetProperty("width").GetInt32());
        Assert.Equal("2026-11-20T12:00:00.0000000+00:00", root.GetProperty("timeline").GetProperty("fromUtc").GetString());
        Assert.Equal("2026-11-21T17:00:02.0000000+00:00", root.GetProperty("timeline").GetProperty("toUtc").GetString());
        Assert.Equal(
            ["api-equivalent-estimated", "api-equivalent-unpriced", "cache-unreported", "unpriced"],
            root.GetProperty("coverage").GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()));
        Assert.Equal(
            (true, 1, 0),
            (root.GetProperty("coverage").GetProperty("requestsComplete").GetBoolean(),
                root.GetProperty("coverage").GetProperty("supersededMatchedImportSpans").GetInt32(),
                root.GetProperty("coverage").GetProperty("supersededUnmatchedImportSpans").GetInt32()));
        Assert.True(root.GetProperty("coverage").GetProperty("toolHistory").GetProperty("complete").GetBoolean());
    }

    [Fact]
    public async Task TL10_LedgerDataFlagsImportsThatLiveSpansReplacedWithoutAMatch()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await ExportSeeds.SupersedeUnmatchedAsync(store);

        await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        JsonElement coverage = data.RootElement.GetProperty("coverage");
        Assert.Equal(
            (false, 1, 1),
            (coverage.GetProperty("requestsComplete").GetBoolean(),
                coverage.GetProperty("supersededMatchedImportSpans").GetInt32(),
                coverage.GetProperty("supersededUnmatchedImportSpans").GetInt32()));
        Assert.Equal(
            ["api-equivalent-estimated", "api-equivalent-unpriced", "cache-unreported", "import-superseded-unmatched", "unpriced"],
            coverage.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()));
    }

    [Fact]
    public async Task TL6_ToolCallsAreTicksWithoutDurations()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        XElement[] ticks = [.. ExportFiles.ById(page, "timeline-plot").Descendants("line")];
        Assert.Equal(4, ticks.Length);
        Assert.All(ticks, tick => Assert.Equal((string)tick.Attribute("x1")!, (string)tick.Attribute("x2")!));
        Assert.All(ticks, tick => Assert.Matches(ToolTitle(), tick.Element("title")!.Value));
    }

    [Fact]
    public async Task TL7_TheAxisShowsAtMostTwelveLabelsOfTheFinestLevel()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        using JsonDocument data = ExportFiles.LedgerData(html);

        JsonElement[] ticks = [.. data.RootElement.GetProperty("timeline").GetProperty("ticks").EnumerateArray()];
        Assert.Equal(30, ticks.Length);
        Assert.Equal([0, 1, 2], ticks.Select(tick => tick.GetProperty("level").GetInt32()).Distinct().Order());
        Assert.Equal(("0", 2, "07:00"), (ticks[0].GetProperty("x").GetRawText(), ticks[0].GetProperty("level").GetInt32(), ticks[0].GetProperty("label").GetString()));
        Assert.Equal(
            ["17.241%|12:00", "37.9303%|18:00", "58.6196%|2026-11-21", "79.3088%|06:00", "99.9981%|12:00"],
            ExportFiles.ById(ExportFiles.Parse(html), "timeline-axis-labels").Elements("span").Select(span =>
                $"{((string)span.Attribute("style")!)["left: ".Length..]}|{span.Value}"));
    }

    [Fact]
    public async Task TL8_MoreThanTwoHundredSessionsShareTheLastRow()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        DateTimeOffset start = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
        await store.RecordAsync(
        [
            .. Enumerable.Range(0, 201).Select(index =>
                Entry($"conv-{index.ToString("D3", CultureInfo.InvariantCulture)}", index, start.AddMinutes(index))),
        ]);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        string[] labels =
        [
            .. page.Descendants("svg").Single(svg => (string?)svg.Attribute("class") == "timeline-labels").Elements("text")
                .Select(text => text.Nodes().OfType<XText>().Single().Value),
        ];
        Assert.Equal(200, labels.Length);
        Assert.Equal(("1 · 11-21 05:00", "199 · 11-21 08:18", "200 · 2 other sessions"), (labels[0], labels[198], labels[199]));
        Assert.Equal("3200", (string)ExportFiles.ById(page, "timeline-plot").Attribute("height")!);
    }

    [Fact]
    public async Task TL9_RowLabelsStayDistinctWhenSessionIdsShareAPrefix()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        const string Prefix = "7d4e1c2a-9b8f-4e3d-a1b2-00000000000";
        DateTimeOffset start = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
        await store.RecordAsync(
        [
            Entry(Prefix + "1", 0, start),
            Entry(Prefix + "2", 1, start.AddSeconds(30)),
            Entry(Prefix + "3", 2, start.AddHours(1)),
        ]);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        XElement[] texts = [.. page.Descendants("svg").Single(svg => (string?)svg.Attribute("class") == "timeline-labels").Elements("text")];
        string[] labels = [.. texts.Select(text => text.Nodes().OfType<XText>().Single().Value)];
        Assert.Equal(["1 · 11-21 05:00", "2 · 11-21 05:00", "3 · 11-21 06:00"], labels);
        Assert.Equal(labels.Length, labels.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal([Prefix + "1", Prefix + "2", Prefix + "3"], texts.Select(text => text.Element("title")!.Value));
        Assert.Equal(
            ["1|" + Prefix + "1", "2|" + Prefix + "2", "3|" + Prefix + "3"],
            ExportFiles.ById(page, "table-timeline").Element("tbody")!.Elements("tr").Select(row =>
                string.Join('|', row.Elements("td").Take(2).Select(cell => cell.Value))));
    }

    private static CliHarness Harness(string root)
    {
        FakeCliPlatform platform = new();
        platform.TimeZones["Test/New_York"] = TestZones.NewYork;
        return new CliHarness(root, platform, new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));
    }

    private static async Task<XDocument> ExportAsync(CliHarness cli, string root, params string[] args)
    {
        CliRun run = await cli.RunAsync(["export", .. args]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return ExportFiles.Load(Path.Combine(root, Report));
    }

    // The elements named name under parent that have the class className.
    private static IEnumerable<XElement> Elements(XElement parent, string name, string className) =>
        parent.Descendants(name).Where(element =>
            ((string?)element.Attribute("class"))?.Split(' ').Contains(className, StringComparer.Ordinal) == true);

    // A mai-code-2 request in conversation at start.
    private static LedgerEntry Entry(string conversation, int index, DateTimeOffset start)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: start,
            requestModel: "mai-code-2",
            conversationId: conversation,
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "7e" + index.ToString("D30", CultureInfo.InvariantCulture),
            SpanId = "7e" + index.ToString("D14", CultureInfo.InvariantCulture),
        };
        return new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null);
    }

    [GeneratedRegex(@"\A[0-9]{2}:[0-9]{2}:[0-9]{2} · [^·]+ · (exit code -?[0-9]+|error|ok)\z")]
    private static partial Regex ToolTitle();
}
