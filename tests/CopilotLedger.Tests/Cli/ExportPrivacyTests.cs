using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Ingestion;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Cli;

// What the export may contain: no content, no local paths unless asked, encoded text, and the same bytes for the same inputs.
public sealed class ExportPrivacyTests
{
    private const string Report = "copilot-ledger-report.html";
    private const string Bundle = "copilot-ledger-report.zip";
    private const string Conversation = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private static readonly string[] Canaries =
    [
        "CANARY-SESSION-5e2b", "CANARY-PROMPT-5e2b", "CANARY-RESPONSE-5e2b", "CANARY-SYSTEM-5e2b", "CANARY-ARGS-5e2b",
        "CANARY-RESULT-5e2b", "CANARY-PATH-5e2b", "CANARY-ERRMSG-5e2b",
    ];

    // A chat request whose span carries content attributes, and a tool call with arguments and a result.
    private const string Spans = """
        {
          "resourceSpans": [
            {
              "resource": { "attributes": [ { "key": "service.name", "value": { "stringValue": "github-copilot" } } ] },
              "scopeSpans": [
                {
                  "scope": { "name": "github.copilot" },
                  "spans": [
                    {
                      "traceId": "5b8efff798038103d269b633813fc60c",
                      "spanId": "eee19b7ec3c1b174",
                      "parentSpanId": "eee19b7ec3c1b173",
                      "name": "chat mai-code-2",
                      "startTimeUnixNano": "1795199604707000000",
                      "endTimeUnixNano": "1795199606429000000",
                      "attributes": [
                        { "key": "gen_ai.operation.name", "value": { "stringValue": "chat" } },
                        { "key": "gen_ai.provider.name", "value": { "stringValue": "github" } },
                        { "key": "gen_ai.request.model", "value": { "stringValue": "mai-code-2" } },
                        { "key": "gen_ai.conversation.id", "value": { "stringValue": "0f8fad5b-d9cb-469f-a165-70867728950e" } },
                        { "key": "gen_ai.usage.input_tokens", "value": { "intValue": "1000" } },
                        { "key": "gen_ai.usage.output_tokens", "value": { "intValue": "100" } },
                        { "key": "gen_ai.input.messages", "value": { "stringValue": "CANARY-PROMPT-5e2b" } },
                        { "key": "gen_ai.output.messages", "value": { "stringValue": "CANARY-RESPONSE-5e2b" } },
                        { "key": "gen_ai.system_instructions", "value": { "stringValue": "CANARY-SYSTEM-5e2b" } }
                      ]
                    },
                    {
                      "traceId": "5b8efff798038103d269b633813fc60c",
                      "spanId": "eee19b7ec3c1b175",
                      "parentSpanId": "eee19b7ec3c1b173",
                      "name": "execute_tool bash",
                      "startTimeUnixNano": "1795199606500000000",
                      "endTimeUnixNano": "1795199607000000000",
                      "attributes": [
                        { "key": "gen_ai.operation.name", "value": { "stringValue": "execute_tool" } },
                        { "key": "gen_ai.conversation.id", "value": { "stringValue": "0f8fad5b-d9cb-469f-a165-70867728950e" } },
                        { "key": "gen_ai.tool.name", "value": { "stringValue": "bash" } },
                        { "key": "gen_ai.tool.call.arguments", "value": { "stringValue": "CANARY-ARGS-5e2b" } },
                        { "key": "gen_ai.tool.call.result", "value": { "stringValue": "CANARY-RESULT-5e2b" } }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task EP1_NoContentOrLocalPathReachesTheExport()
    {
        using TempDirectory temp = new();
        string repo = Path.Combine(temp.Path, "CANARY-PATH-5e2b", "repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git", "refs", "heads"));
        File.WriteAllText(Path.Combine(repo, ".git", "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(Path.Combine(repo, ".git", "refs", "heads", "main"), GitFixture.Sha1 + "\n");
        string session = Path.Combine(temp.Path, "copilot-home", "session-state", Conversation);
        Directory.CreateDirectory(session);
        File.WriteAllText(Path.Combine(session, "workspace.yaml"), $"cwd: {repo}\n");
        File.WriteAllText(Path.Combine(session, "events.jsonl"), "{\"type\":\"user.message\",\"data\":{\"content\":\"CANARY-SESSION-5e2b\"}}\n");
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        LedgerIngestor ingestor = new(
            store,
            JsonRateCardCalculator.LoadEmbedded(),
            new FileSystemGitContextResolver(ceilingDirectory: temp.Path),
            new CopilotSessionWorkspaceLocator(Path.Combine(temp.Path, "copilot-home")));
        IngestResult ingested = await ingestor.IngestAsync(OtlpTraceDecoder.DecodeJson(Encoding.UTF8.GetBytes(Spans)));

        // IngestFailureLog keeps only a SqliteException's message, so this one is how a canary can reach daemon.log.
        new IngestFailureLog(line => File.AppendAllText(Path.Combine(temp.Path, "data", "daemon.log"), line + "\n"), TimeProvider.System)
            .Report(new SqliteException("CANARY-ERRMSG-5e2b", 1));
        Assert.Equal(
            "Warning: could not record spans: SqliteException: CANARY-ERRMSG-5e2b\n",
            File.ReadAllText(Path.Combine(temp.Path, "data", "daemon.log")));

        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal(1, ingested.RecordedSpans);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        Assert.StartsWith("Wrote copilot-ledger-report.html: 1 request in all repositories (last 30 days).\n", run.Out);
        using JsonDocument spans = JsonDocument.Parse(Entry(temp.Path, "telemetry/spans.json"));
        Assert.Equal("repo", Assert.Single(spans.RootElement.GetProperty("spans").EnumerateArray()).GetProperty("repository").GetString());
        string[] texts =
        [
            run.Out,
            run.Error,
            File.ReadAllText(Path.Combine(temp.Path, Report)),
            .. ExportFiles.ReadZip(Path.Combine(temp.Path, Bundle)).Select(entry => Encoding.UTF8.GetString(entry.Bytes)),
        ];
        Assert.Equal(9, texts.Length);
        Assert.All(Canaries, canary => Assert.All(texts, text => Assert.DoesNotContain(canary, text, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task EP2_LedgerTextIsEncodedEverywhere()
    {
        const string Branch = "<script>alert(1)</script>";
        const string Agent = "\"></svg><img src=x onerror=alert(1)>";
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: ExportSeeds.GeneratedAt.AddHours(-1),
            requestModel: "claude-opus-5.5",
            agentName: Agent,
            conversationId: "conv-xss",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c",
            SpanId = "3c00000000000001",
        };
        await store.RecordAsync(
        [
            new LedgerEntry(
                span,
                JsonRateCardCalculator.LoadEmbedded().Calculate(span),
                GitFixture.Feature(temp.Path) with { BranchName = Branch }),
        ]);

        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--branch", Branch, "--bundle");

        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        Assert.DoesNotContain(Branch, html, StringComparison.Ordinal);
        Assert.DoesNotContain(Agent, html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&quot;&gt;&lt;/svg&gt;&lt;img src=x onerror=alert(1)&gt;", html, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(html, "</script>").Count);
        Assert.Contains("\\u003Cscript\\u003Ealert(1)\\u003C/script\\u003E", html, StringComparison.Ordinal);
        using JsonDocument data = ExportFiles.LedgerData(html);
        Assert.Equal(Branch, data.RootElement.GetProperty("filter").GetProperty("branch").GetString());
        Assert.NotNull(ExportFiles.Parse(html).Root);
        foreach (byte[] bytes in ExportFiles.ReadZip(Path.Combine(temp.Path, Bundle))
                     .Where(entry => entry.Name.EndsWith(".json", StringComparison.Ordinal))
                     .Select(entry => entry.Bytes))
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
        }

        using JsonDocument summary = JsonDocument.Parse(
            ExportFiles.ReadZip(Path.Combine(temp.Path, Bundle)).Single(entry => entry.Name == "telemetry/summary.json").Bytes);
        Assert.Equal(Agent, summary.RootElement.GetProperty("byAgent")[0].GetProperty("agent").GetString());
        Assert.Equal(Branch, summary.RootElement.GetProperty("byBranch")[0].GetProperty("branch").GetString());
    }

    [Fact]
    public async Task EP3_LocalPathsAppearOnlyWhenIncluded()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        CliRun hidden = await cli.RunAsync("export", "--all-repos", "--bundle");
        string[] hiddenTexts = Texts(temp.Path);
        CliRun included = await cli.RunAsync("export", "--all-repos", "--bundle", "--include-local-paths", "--force");
        string[] includedTexts = Texts(temp.Path);

        Assert.Equal((0, 0), (hidden.ExitCode, included.ExitCode));
        Assert.Equal(hidden.Out, included.Out);
        Assert.All(hiddenTexts, text => Assert.DoesNotContain("private-7c1d", text, StringComparison.Ordinal));
        Assert.All(hiddenTexts, text => Assert.DoesNotContain("\"repositoryRoot\"", text, StringComparison.Ordinal));
        Assert.Contains("\"localPathsIncluded\": false", hiddenTexts[1], StringComparison.Ordinal);
        Assert.Contains("\"localPathsIncluded\": true", includedTexts[1], StringComparison.Ordinal);
        Assert.Contains("private-7c1d", includedTexts[0], StringComparison.Ordinal);
        using JsonDocument spans = JsonDocument.Parse(Entry(temp.Path, "telemetry/spans.json"));
        JsonElement d1 = spans.RootElement.GetProperty("spans").EnumerateArray().Single(span => span.GetProperty("spanId").GetString() == "d100000000000001");
        Assert.Equal(ExportSeeds.LocalRoot(temp.Path), d1.GetProperty("repositoryRoot").GetString());
        Assert.Equal(ExportSeeds.LocalRoot(temp.Path), d1.GetProperty("repository").GetString());
        using JsonDocument summary = JsonDocument.Parse(Entry(temp.Path, "telemetry/summary.json"));
        Assert.Contains(
            summary.RootElement.GetProperty("byBranch").EnumerateArray(),
            row => row.GetProperty("repository").GetString() == ExportSeeds.LocalRoot(temp.Path));
    }

    [Fact]
    public async Task EP4_TheSameInputsWriteTheSameBytesAndAReplayReproducesThem()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        ManualTimeProvider clock = new(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5);
        CliHarness cli = new(temp.Path, new FakeCliPlatform(), clock);

        await cli.RunAsync("export", "--all-repos", "--bundle");
        (byte[] Html, byte[] Zip) first = Outputs(temp.Path);
        await cli.RunAsync("export", "--all-repos", "--bundle", "--force");
        (byte[] Html, byte[] Zip) second = Outputs(temp.Path);
        using JsonDocument manifest = JsonDocument.Parse(Entry(temp.Path, "manifest.json"));
        string generatedAt = manifest.RootElement.GetProperty("generatedAtUtc").GetString()!;
        clock.Advance(TimeSpan.FromHours(1));
        NormalizedGenAiSpan later = TestSpans.Chat(
            startTime: ExportSeeds.GeneratedAt.AddMinutes(30),
            requestModel: "claude-opus-5.5",
            conversationId: "conv-later",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d",
            SpanId = "4d00000000000001",
        };
        await store.RecordAsync([new LedgerEntry(later, JsonRateCardCalculator.LoadEmbedded().Calculate(later), null)]);
        CliRun replay = await cli.RunAsync("export", "--all-repos", "--bundle", "--force", "--generated-at", generatedAt);
        (byte[] Html, byte[] Zip) replayed = Outputs(temp.Path);

        Assert.Equal("2026-11-21T21:30:00.0000000+00:00", generatedAt);
        Assert.Equal(0, replay.ExitCode);
        Assert.Equal(first.Html, second.Html);
        Assert.Equal(first.Zip, second.Zip);
        Assert.Equal(first.Html, replayed.Html);
        Assert.Equal(first.Zip, replayed.Zip);
    }

    [Fact]
    public async Task EP5_TheCultureDoesntChangeAByte()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        (byte[] Html, byte[] Zip) invariant;
        (byte[] Html, byte[] Zip) german;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            await cli.RunAsync("export", "--all-repos", "--bundle");
            invariant = Outputs(temp.Path);
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            await cli.RunAsync("export", "--all-repos", "--bundle", "--force");
            german = Outputs(temp.Path);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }

        Assert.Equal(invariant.Html, german.Html);
        Assert.Equal(invariant.Zip, german.Zip);
    }

    [Fact]
    public void EP6_TheRepositoryAddsNoPackagesOrOtherTooling()
    {
        string root = RepositoryRoot();
        string[] projects =
        [
            .. FilesUnder(Path.Combine(root, "src")).Concat(FilesUnder(Path.Combine(root, "tests")))
                .Where(file => file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)),
        ];
        string[] packages =
        [
            .. projects
                .SelectMany(project => Regex.Matches(File.ReadAllText(project), "PackageReference Include=\"([^\"]+)\"").Select(match => match.Groups[1].Value))
                .Distinct()
                .Order(StringComparer.Ordinal),
        ];
        string[] files =
        [
            .. new[] { "src", "tests", "docs", ".github" }.SelectMany(folder => FilesUnder(Path.Combine(root, folder))),
            .. Directory.GetFiles(root),
        ];

        Assert.Equal(6, projects.Length);
        Assert.Equal(
            [
                "Microsoft.AspNetCore.TestHost", "Microsoft.Data.Sqlite", "Microsoft.NET.Test.Sdk", "ModelContextProtocol",
                "System.CommandLine", "coverlet.collector", "xunit", "xunit.runner.visualstudio",
            ],
            packages);
        Assert.DoesNotContain(files, file =>
            Path.GetFileName(file) is "package.json" or "requirements.txt" || file.EndsWith(".py", StringComparison.OrdinalIgnoreCase));
    }

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

    private static byte[] Entry(string root, string name) =>
        ExportFiles.ReadZip(Path.Combine(root, Bundle)).Single(entry => entry.Name == name).Bytes;

    private static (byte[] Html, byte[] Zip) Outputs(string root) =>
        (File.ReadAllBytes(Path.Combine(root, Report)), File.ReadAllBytes(Path.Combine(root, Bundle)));

    // The report, then each bundle entry, as text.
    private static string[] Texts(string root) =>
    [
        File.ReadAllText(Path.Combine(root, Report)),
        .. ExportFiles.ReadZip(Path.Combine(root, Bundle)).Select(entry => Encoding.UTF8.GetString(entry.Bytes)),
    ];

    // The files under folder, recursively, skipping bin and obj folders.
    private static IEnumerable<string> FilesUnder(string folder) =>
        !Directory.Exists(folder)
            ? []
            : Directory.GetFiles(folder).Concat(Directory.GetDirectories(folder)
                .Where(directory => Path.GetFileName(directory) is not ("bin" or "obj"))
                .SelectMany(FilesUnder));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CopilotLedger.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
