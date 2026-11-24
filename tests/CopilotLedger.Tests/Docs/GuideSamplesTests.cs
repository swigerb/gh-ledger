using System.Globalization;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using CopilotLedger.Cli;
using CopilotLedger.Tests.TestSupport;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CopilotLedger.Tests.Docs;

// Checks that each sample in docs/guide.md is exactly what gh-ledger prints in the GuideWorld example.
public sealed partial class GuideSamplesTests
{
    // The samples that show a command's output, and the state each command runs in.
    private static readonly Dictionary<string, GuideState> Commands = new(StringComparer.Ordinal)
    {
        ["install"] = GuideState.Fresh,
        ["status-first"] = GuideState.FirstPrompt,
        ["import-dry-run"] = GuideState.Backfill,
        ["import"] = GuideState.Backfill,
        ["status"] = GuideState.Month,
        ["branch"] = GuideState.Month,
        ["top-prompts"] = GuideState.Month,
        ["agents"] = GuideState.Month,
        ["cache"] = GuideState.Month,
        ["cache-bloat"] = GuideState.Month,
        ["cache-turns"] = GuideState.Month,
        ["budget-set"] = GuideState.Month,
        ["budget-list"] = GuideState.Month,
        ["breaker-status"] = GuideState.Month,
        ["breaker-reset"] = GuideState.Month,
        ["export-pr"] = GuideState.Month,
        ["rates-validate"] = GuideState.Month,
        ["daemon-stop"] = GuideState.Month,
        ["uninstall"] = GuideState.Month,
    };

    // The samples that GS3 to GS7 check, and the overlay that the Month state uses.
    private static readonly string[] OtherSamples =
        ["daemon-start", "rates-update", "budget-alert", "receipt", "mcp", "rates-overlay"];

    private static readonly Lazy<IReadOnlyDictionary<string, GuideSample>> Samples = new(ReadSamples);

    public static TheoryData<string> CommandIds => new(Commands.Keys);

    private static string Overlay => Samples.Value["rates-overlay"].Text + "\n";

    [Fact]
    public void GS1_TheGuideHasExactlyTheCheckedSamples()
    {
        Assert.Equal(
            Commands.Keys.Concat(OtherSamples).Order(StringComparer.Ordinal),
            Samples.Value.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(CommandIds))]
    public async Task GS2_CommandSamplesShowTheirOutput(string id)
    {
        GuideSample sample = Samples.Value[id];
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, Commands[id], Overlay);
        CliRun run = await world.RunAsync(sample.Command!);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(sample.Text + "\n", world.Display(run.Out));
    }

    [Fact]
    public async Task GS3_DaemonStartSampleRestartsTheDaemon()
    {
        GuideSample sample = Samples.Value["daemon-start"];
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        Assert.Equal(0, (await world.RunAsync("gh ledger daemon stop")).ExitCode);
        CliRun run = await world.RunAsync(sample.Command!);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(sample.Text + "\n", world.Display(run.Out));
    }

    [Fact]
    public async Task GS4_RatesUpdateSampleDownloadsNewerRates()
    {
        GuideSample sample = Samples.Value["rates-update"];
        Assert.Equal("gh ledger rates update", sample.Command);
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        FakeHttpHandler handler = new(
            _ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-15"))));
        CliRun run = await world.Harness.RunWithHttpAsync(handler, "rates", "update");
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(sample.Text + "\n", world.Display(run.Out));
    }

    [Fact]
    public async Task GS5_BudgetAlertSampleIsTheDaemonsAlertLine()
    {
        GuideSample sample = Samples.Value["budget-alert"];
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        Assert.Equal(0, (await world.RunAsync("gh ledger daemon stop")).ExitCode);
        await world.Harness.StartInProcessDaemonAsync();
        using HttpClient client = world.Harness.CreateDaemonClient();
        using ByteArrayContent content = new(Encoding.UTF8.GetBytes(ChatTrace(world.Repository)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using HttpResponseMessage response = await client.PostAsync("/v1/traces", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await world.Harness.RunAsync("daemon", "stop")).ExitCode);
        Assert.Equal(0, (await world.Harness.InProcessDaemon!).ExitCode);
        string[] lines = world.Harness.DaemonOutput.Split('\n');
        Assert.Equal(4, lines.Length);

        // The daemon logs "listening" just after it writes daemon.json, so the alert can come before it.
        Assert.Single(
            lines[..2],
            line => line.StartsWith(
                $"2026-11-21T21:30:00.0000000+00:00 gh-ledger {GhLedgerCli.Version} daemon listening on http://localhost:4318 (pid ",
                StringComparison.Ordinal));
        Assert.Single(lines[..2], line => line == sample.Text);
        Assert.Equal("2026-11-21T21:30:00.0000000+00:00 gh-ledger daemon stopped", lines[2]);
        Assert.Equal(string.Empty, lines[3]);
    }

    [Fact]
    public async Task GS6_ReceiptSampleIsTheBranchReceipt()
    {
        GuideSample sample = Samples.Value["receipt"];
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        CliRun run = await world.RunAsync("gh ledger export-pr --stdout");
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(sample.Text + "\n", run.Out);
    }

    [Fact]
    public async Task GS7_McpSampleIsTheBranchLedgerTool()
    {
        GuideSample sample = Samples.Value["mcp"];
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        Pipe toServer = new();
        Pipe toClient = new();
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        Task<int> command = GhLedgerCli.InvokeAsync(
            ["mcp"],
            world.Harness.Context with
            {
                StandardInput = toServer.Reader.AsStream(),
                StandardOutput = toClient.Writer.AsStream(),
                WorkingDirectory = world.Repository,
                Out = output,
                Error = error,
            });
        await using McpClient client = await McpClient.CreateAsync(
            new StreamClientTransport(
                toServer.Writer.AsStream(),
                toClient.Reader.AsStream()),
            new McpClientOptions
            {
                ClientInfo = new Implementation
                {
                    Name = "guide-tests",
                    Version = "1.0",
                },
            });
        CallToolResult result = await client.CallToolAsync(
            "get_branch_ledger",
            new Dictionary<string, object?>());
        string text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        await client.DisposeAsync();
        await toServer.Writer.CompleteAsync();
        Assert.Equal(0, await command.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Empty(output.ToString());
        Assert.Empty(error.ToString());
        await toClient.Reader.CompleteAsync();
        int json = text.LastIndexOf('\n') + 1;
        Assert.Equal(sample.Text, text[..json] + Indent(text[json..]));
    }

    // A sample is "<!-- sample: <id> -->", then a shell block holding one gh ledger command and a text block holding
    // its output, or a single text or json block, or a quoted receipt, then "<!-- /sample -->".
    private static IReadOnlyDictionary<string, GuideSample> ReadSamples()
    {
        string guide = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "guide.md"))
            .ReplaceLineEndings("\n");
        Dictionary<string, GuideSample> samples = new(StringComparer.Ordinal);
        foreach (Match match in SampleRegex().Matches(guide))
        {
            string body = match.Groups["body"].Value;
            Match command = CommandSampleRegex().Match(body);
            Match block = BlockSampleRegex().Match(body);
            GuideSample sample = command.Success
                ? new GuideSample(command.Groups["command"].Value, command.Groups["text"].Value)
                : block.Success
                    ? new GuideSample(null, block.Groups["text"].Value)
                    : new GuideSample(null, Unquote(body));
            samples.Add(match.Groups["id"].Value, sample);
        }

        return samples;
    }

    // Removes the "> " or ">" that starts each line of a quoted sample.
    private static string Unquote(string body) =>
        string.Join(
            '\n',
            body.Split('\n').Select(line =>
                line == ">"
                    ? string.Empty
                    : line.StartsWith("> ", StringComparison.Ordinal)
                        ? line[2..]
                        : throw new InvalidDataException($"A quoted sample has an unquoted line: {line}")));

    // get_branch_ledger returns its JSON on one line; the guide shows it indented.
    private static string Indent(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(
            document.RootElement,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                NewLine = "\n",
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
    }

    // One chat span in the repository at 14:50 local time: 250,000 input tokens of claude-opus-5.5, 100.00 AI credits.
    private static string ChatTrace(string repository) => """
        {"resourceSpans":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"github-copilot"}},{"key":"process.working_directory","value":{"stringValue":REPOSITORY}}]},"scopeSpans":[{"spans":[{"traceId":"0f3c9a7e5d1b4f2a8c6e0d9b7a5c00f1","spanId":"5e8a1c3f7b9df1c1","name":"chat claude-opus-5.5","kind":3,"startTimeUnixNano":"1795290600000000000","endTimeUnixNano":"1795290600000000000","attributes":[{"key":"gen_ai.operation.name","value":{"stringValue":"chat"}},{"key":"gen_ai.request.model","value":{"stringValue":"claude-opus-5.5"}},{"key":"gen_ai.usage.input_tokens","value":{"intValue":"250000"}},{"key":"gen_ai.usage.output_tokens","value":{"intValue":"0"}}]}]}]}]}
        """.Replace("REPOSITORY", JsonSerializer.Serialize(repository), StringComparison.Ordinal);

    [GeneratedRegex(@"^<!-- sample: (?<id>[a-z-]+) -->\n(?<body>.*?)\n<!-- /sample -->$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex SampleRegex();

    [GeneratedRegex(@"\A```shell\n(?<command>gh ledger [^\n]+)\n```\n\n```text\n(?<text>.*)\n```\z", RegexOptions.Singleline)]
    private static partial Regex CommandSampleRegex();

    [GeneratedRegex(@"\A```(?:text|json)\n(?<text>.*)\n```\z", RegexOptions.Singleline)]
    private static partial Regex BlockSampleRegex();

    private sealed record GuideSample(string? Command, string Text);
}
