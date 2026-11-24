using System.Text.RegularExpressions;
using System.Xml.Linq;
using CopilotLedger.Cli;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The report is self-contained: no external references, and an inline script that only reads the page.
public sealed class ExportAssetTests
{
    private const string Report = "copilot-ledger-report.html";

    // The script may contain none of these, not even in a comment.
    private static readonly string[] Banned =
    [
        "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "eval", "Function(", "setTimeout", "setInterval",
        "fetch(", "XMLHttpRequest", "WebSocket", "EventSource", "sendBeacon", "import(", "localStorage", "sessionStorage",
        "document.cookie", "window.open", "postMessage", "location", "@import", "url(", "</script", "<!--",
    ];

    [Fact]
    public async Task EA1_ThePageReferencesNothingOutsideItself()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        string html = await ExportAsync(temp.Path);

        XDocument page = ExportFiles.Parse(html);
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xlink:href", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xmlns", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(page.Descendants().Attributes(), attribute => attribute.Name.LocalName == "src");
        Assert.All(
            page.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "href"),
            href => Assert.Matches("^#[a-z-]+$", href.Value));
        Assert.DoesNotContain(page.Descendants(), element =>
            element.Name.LocalName is "img" or "link" or "iframe" or "object" or "embed" or "base" or "form");
        foreach (string token in new[] { "@import", "fetch(", "XMLHttpRequest", "WebSocket" })
        {
            Assert.DoesNotContain(token, html, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("url(", html.Replace("url(#hatch)", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "Content-Security-Policy"));
    }

    [Fact]
    public async Task EA2_TheScriptIsTheAssetAndAvoidsUnsafeApis()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        using Stream resource = typeof(GhLedgerCli).Assembly.GetManifestResourceStream("CopilotLedger.Cli.Export.Assets.report.js")!;
        using StreamReader reader = new(resource);
        string script = reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd('\n');

        string html = await ExportAsync(temp.Path);

        Assert.EndsWith("</script>\n<script>\n" + script + "\n</script>\n</body>\n</html>\n", html);
        Assert.StartsWith("(function () {\n  'use strict';", script);
        Assert.Equal(2, Regex.Matches(html, "<script").Count);
        Assert.Equal(2, Regex.Matches(html, "</script>").Count);
        Assert.All(Banned, token => Assert.DoesNotContain(token, script, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EA3_TheElementsTheScriptUsesExistAndItsControlsStartHidden()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        string html = await ExportAsync(temp.Path);

        XDocument page = ExportFiles.Parse(html);
        string[] hooks =
        [
            "ledger-data", "timeline-plot", "timeline-axis", "timeline-axis-labels", "timeline-overview", "timeline-window",
            "timeline-viewport", "timeline-controls", "timeline-reset", "lens-toggle",
        ];
        Assert.All(hooks, id =>
        {
            Assert.Single(page.Descendants(), element => (string?)element.Attribute("id") == id);
            Assert.Contains($"'{id}'", html, StringComparison.Ordinal);
        });
        Assert.Contains(page.Descendants("table"), table => (string?)table.Attribute("class") == "data sortable");
        Assert.Equal("hidden", (string?)ExportFiles.ById(page, "timeline-controls").Attribute("hidden"));
        Assert.Equal("hidden", (string?)ExportFiles.ById(page, "lens-toggle").Attribute("hidden"));
        Assert.Equal(["billed", "api", "local"], page.Descendants("figure").Select(figure => (string?)figure.Attribute("data-lens")).OfType<string>());
    }

    [Fact]
    public async Task EA4_APlainScrollOverTheTimelineScrollsThePage()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        string html = await ExportAsync(temp.Path);

        XDocument page = ExportFiles.Parse(html);
        string wheel = html[html.IndexOf("addEventListener('wheel'", StringComparison.Ordinal)..];
        wheel = wheel[..wheel.IndexOf("}, { passive: false });", StringComparison.Ordinal)];
        int guard = wheel.IndexOf("if (!(event.ctrlKey || event.metaKey) || event.deltaY === 0) {", StringComparison.Ordinal);
        Assert.InRange(guard, 0, wheel.IndexOf("return;", StringComparison.Ordinal));
        Assert.True(wheel.IndexOf("return;", StringComparison.Ordinal) < wheel.IndexOf("event.preventDefault();", StringComparison.Ordinal));
        Assert.Contains("#timeline-plot { touch-action: pan-y; cursor: grab; }", html, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "touch-action: none"));
        Assert.StartsWith("Hold Ctrl or Command and scroll, or pinch, to zoom; drag sideways to pan;", ExportFiles.ById(page, "timeline-help").Value);
    }

    private static async Task<string> ExportAsync(string root)
    {
        CliHarness cli = new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));
        CliRun run = await cli.RunAsync("export", "--all-repos");
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return ExportFiles.ReadReport(Path.Combine(root, Report));
    }
}
