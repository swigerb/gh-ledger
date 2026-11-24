using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The export's cost lenses, cache bars, and context-growth charts, and how they read without JavaScript.
public sealed partial class ExportChartTests
{
    private const string Report = "copilot-ledger-report.html";

    [Fact]
    public async Task CH1_TheBilledLensStacksGitHubAndByokSpendWithAKey()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        Assert.Equal(
            [
                "claude-opus-5.5|seg-github 440|$0.51",
                "gpt-6.1-sol|seg-byok 272.83|$0.32",
                "mai-code-2|seg-github 131.22|$0.15",
                "qwen3.8-27b||$0.00",
                "gpt-unknown|unpriced 12|Unpriced",
            ],
            Bars(page, "cost-billed-model"));
        Assert.All(
            ExportFiles.ById(page, "lens-billed").Descendants("rect"),
            rect => Assert.Contains((string)rect.Attribute("class")!, new[] { "seg-github", "seg-byok", "unpriced" }));
        Assert.Equal("url(#hatch)", (string)ExportFiles.ById(page, "cost-billed-model").Descendants("rect").Single(rect => (string?)rect.Attribute("class") == "unpriced").Attribute("fill")!);
        XElement key = ExportFiles.ById(page, "legend-billed");
        Assert.Equal(
            ["swatch seg-github|GitHub AI credits", "swatch seg-byok|Foundry BYOK", "swatch|Unpriced requests"],
            key.Elements("li").Select(item => $"{(string)item.Elements().First().Attribute("class")!}|{item.Value}"));
        Assert.Equal(("unpriced", "url(#hatch)"), ((string)key.Descendants("rect").Single().Attribute("class")!, (string)key.Descendants("rect").Single().Attribute("fill")!));

        // The channel colors, light and dark, are none of the model colors.
        string css = StyleSheet().Match(ExportFiles.ReadReport(Path.Combine(temp.Path, Report))).Groups[1].Value;
        string[] models = [.. ModelColor().Matches(css).Select(match => match.Groups[1].Value)];
        string[] channels = [.. ChannelColor().Matches(css).Select(match => match.Groups[1].Value)];
        Assert.Equal((8, 8), (models.Length, channels.Length));
        Assert.DoesNotContain(channels, channel => models.Contains(channel));
    }

    [Fact]
    public async Task CH2_EachLensHasItsOwnScale()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        Assert.Equal(
            [
                "claude-opus-5.5|seg-api 440|~$0.51",
                "gpt-6.1-sol|unpriced 12|Unpriced",
                "mai-code-2|unpriced 12|Unpriced",
                "qwen3.8-27b|unpriced 12|Unpriced",
                "gpt-unknown|unpriced 12|Unpriced",
            ],
            Bars(page, "cost-api-model"));
        Assert.Equal(["qwen3.8-27b|seg-local 440|$0.17"], Bars(page, "cost-local-model"));
        Assert.Equal(["(none)|seg-local 440|$0.17"], Bars(page, "cost-local-agent"));
        Assert.Contains("gpt-6.1-sol|seg-byok 272.83|$0.32", Bars(page, "cost-billed-model"));
        Assert.Equal(["API-equivalent USD", "No API-equivalent price"], ExportFiles.ById(page, "legend-api").Elements("li").Select(item => item.Value));
        Assert.Equal(["Cloud spend avoided"], ExportFiles.ById(page, "legend-local").Elements("li").Select(item => item.Value));
    }

    [Fact]
    public async Task CH3_UnpricedRequestsGetAHatchedMark()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        Assert.Equal(
            ["(none)|seg-github 271.9 seg-byok 168.1 unpriced 12|$0.82 + 1 unpriced", "explore|seg-github 80.05|$0.15"],
            Bars(page, "cost-billed-agent"));
        Assert.Equal(
            ["(none)|seg-api 440 unpriced 12|~$0.51*", "explore|unpriced 12|Unpriced"],
            Bars(page, "cost-api-agent"));
    }

    [Fact]
    public async Task CH4_CacheBarsShowNotAvailableInsteadOfZero()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        Assert.Equal(
            [
                "qwen3.8-27b|na 60|n/a (no cache counters)",
                "mai-code-2|na 60|n/a (no cache counters)",
                "claude-opus-5.5|seg-cache 176|40.0% · 1 of 2 reported",
                "gpt-6.1-sol|na 60|n/a (no cache counters)",
                "gpt-unknown|na 60|n/a (no cache counters)",
            ],
            Bars(page, "cache-model"));
        Assert.DoesNotMatch(@"(?<![0-9])0\.0%", ExportFiles.ById(page, "cache-model").Value);
        Assert.Equal(["Cache hit ratio", "No hit ratio (n/a)"], ExportFiles.ById(page, "legend-cache").Elements("li").Select(item => item.Value));
        Assert.Equal("na", (string)ExportFiles.ById(page, "legend-cache").Descendants("rect").Single().Attribute("class")!);
    }

    [Fact]
    public async Task CH5_ContextGrowthChartsOnlyChainsOfMoreThanOneRequest()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        XElement figure = ExportFiles.ById(page, "growth-figure");
        Assert.Equal(
            ["growth-1", "growth-6"],
            figure.Descendants("svg").Where(svg => (string?)svg.Attribute("class") == "growth").Select(svg => (string)svg.Attribute("id")!));
        XElement[] captions = [.. figure.Descendants("p").Where(paragraph => (string?)paragraph.Attribute("class") == "growth-caption")];
        Assert.Equal(
            ["#1 · 2026-11-21 09:00 · 2 requests · peak 200,000 input tokens", "#6 · 2026-11-21 11:00 · 2 requests · peak 300,000 input tokens"],
            captions.Select(caption => caption.Value));
        Assert.Equal([ExportSeeds.A, ExportSeeds.C], captions.Select(caption => (string)caption.Attribute("title")!));
        XElement first = ExportFiles.ById(page, "growth-1");
        Assert.Equal(
            ["(none) · claude-opus-5.5: 1 request", "explore · mai-code-2: 1 request"],
            first.Elements("g").Select(sequence => (string)sequence.Attribute("aria-label")!));
        Assert.Equal(
            ["48,70", "348,10"],
            first.Descendants("circle").Select(circle => $"{(string)circle.Attribute("cx")!},{(string)circle.Attribute("cy")!}"));
        Assert.Equal(
            "Request 2 · 09:01:00 · explore · mai-code-2 · 200,000 input tokens",
            first.Descendants("circle").Last().Element("title")!.Value);
        Assert.Equal(
            ["swatch s0|(none) · claude-opus-5.5", "swatch s1|explore · mai-code-2"],
            first.ElementsAfterSelf("ul").Single().Elements("li").Select(item => $"{(string)item.Element("span")!.Attribute("class")!}|{item.Value}"));
        Assert.Equal(2, ExportFiles.ById(page, "table-growth").Element("tbody")!.Elements("tr").Count());
    }

    [Fact]
    public async Task CH6_ThePageReadsWithoutJavaScript()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        Assert.All(
            page.Descendants("svg").Where(svg => (string?)svg.Attribute("aria-hidden") != "true"),
            svg =>
            {
                Assert.Equal("img", (string?)svg.Attribute("role"));
                Assert.Equal(["title", "desc"], svg.Elements().Take(2).Select(element => element.Name.LocalName));
            });
        Assert.All(
            page.Descendants("figure"),
            figure =>
            {
                Assert.Single(figure.Elements("figcaption"));
                Assert.NotEmpty(figure.Descendants("table"));
            });
        Assert.Equal(
            ["timeline-controls", "lens-toggle"],
            page.Descendants().Where(element => element.Attribute("hidden") is not null).Select(element => (string)element.Attribute("id")!));
        Assert.Equal(3, page.Descendants("figure").Count(figure => figure.Attribute("data-lens") is not null));
        XElement[] headings = [.. page.Descendants("h3").Where(heading => (string?)heading.Attribute("class") == "chart-heading")];
        Assert.Equal(["By model", "By agent", "By model", "By agent", "By model", "By agent", "By model", "By agent"], headings.Select(heading => heading.Value));
        Assert.Equal(
            ["cost-billed-model", "cost-billed-agent", "cost-api-model", "cost-api-agent", "cost-local-model", "cost-local-agent", "cache-model", "cache-agent"],
            headings.Select(heading => (string)heading.ElementsAfterSelf().First().Attribute("id")!));
    }

    [Fact]
    public async Task CH7_LabelsAreCutBetweenCharactersNotInsideThem()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        string agent = new string('a', 26) + "😀tail";
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: ExportSeeds.GeneratedAt.AddHours(-1),
            requestModel: "claude-opus-5.5",
            agentName: agent,
            conversationId: "1234567😀xyz",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e",
            SpanId = "5e00000000000001",
        };
        await store.RecordAsync([new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null)]);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        XElement bar = ExportFiles.ById(page, "cost-billed-agent").Elements("g").Single();
        Assert.Equal(new string('a', 26) + "…", bar.Element("text")!.Value);
        Assert.Equal(agent + ": $0.01", bar.Element("title")!.Value);
        XElement label = page.Descendants("svg").Single(svg => (string?)svg.Attribute("class") == "timeline-labels").Elements("text").Single();
        Assert.Equal(("1 · 11-21 15:30", "1234567😀xyz"), (label.Nodes().OfType<XText>().Single().Value, label.Element("title")!.Value));
        Assert.Contains("&#x1F600;", html);
        Assert.DoesNotContain("&#xFFFD;", html);
        Assert.DoesNotContain('\uFFFD', html);
    }

    [Fact]
    public async Task CH8_WithoutAChainOfTwoRequestsTheReportSaysSo()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: ExportSeeds.GeneratedAt.AddHours(-1),
            requestModel: "claude-opus-5.5",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "5f5f5f5f5f5f5f5f5f5f5f5f5f5f5f5f",
            SpanId = "5f00000000000001",
        };
        await store.RecordAsync([new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null)]);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        Assert.Equal(1, data.RootElement.GetProperty("coverage").GetProperty("spans").GetInt64());
        Assert.Equal("Context growth", ExportFiles.ById(page, "growth-heading").Value);
        Assert.Equal("No prompt chain has more than one request.", ExportFiles.ById(page, "growth-none").Value);
        Assert.DoesNotContain(page.Descendants(), element => (string?)element.Attribute("id") is "growth-figure" or "table-growth");
    }

    private static async Task<XDocument> ExportAsync(string root, params string[] args)
    {
        CliHarness cli = new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));
        CliRun run = await cli.RunAsync(["export", .. args]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return ExportFiles.Load(Path.Combine(root, Report));
    }

    // Each bar of the chart: "label|class width ...|value text".
    private static IReadOnlyList<string> Bars(XDocument page, string id) =>
    [
        .. ExportFiles.ById(page, id).Elements("g").Select(bar =>
            $"{bar.Elements("text").First().Value}|{string.Join(' ', bar.Elements("rect").Select(rect => $"{(string)rect.Attribute("class")!} {(string)rect.Attribute("width")!}"))}|{bar.Elements("text").Last().Value}"),
    ];

    [GeneratedRegex(@"<style>(.*?)</style>", RegexOptions.Singleline)]
    private static partial Regex StyleSheet();

    [GeneratedRegex(@"\.m[0-7] \{ fill: (#[0-9a-f]{6});")]
    private static partial Regex ModelColor();

    [GeneratedRegex(@"--(?:github|byok|ink|local): (#[0-9a-f]{6});")]
    private static partial Regex ChannelColor();
}
