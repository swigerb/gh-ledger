using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CopilotLedger.Cli;
using CopilotLedger.Core.Git;
using CopilotLedger.Mcp;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;

namespace CopilotLedger.Tests.Cli;

// gh ledger audit's privacy, determinism, and arithmetic: the local model's answer is never stored, the output doesn't
// depend on the culture or the run, and the audit code uses no binary floating point.
public sealed partial class AuditPrivacyTests
{
    private const string Url = "http://127.0.0.1:11434/v1";
    private const string Canary = "CANARY-ANSWER-9c41";

    [Fact]
    public async Task AV1_TheLocalModelsAnswer_IsShownAndNeverStored()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await AuditSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        CliRun advised = await cli.RunWithLocalLlmAsync(
            () => FakeLlmHandler.Returning(() => FakeLlmHandler.Answer($"Fix the bash failures ({Canary}).")),
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3");
        CliRun json = await cli.RunAsync("audit", "--json");
        CliRun export = await cli.RunAsync("export", "--all-repos", "--since", "7d", "--bundle");
        await using McpTestHarness mcp = await McpTestHarness.StartAsync(new LedgerMcpContext
        {
            DatabasePath = cli.Context.DatabasePath,
            WorkingDirectory = Path.Combine(temp.Path, "repo"),
            GetEnvironmentVariable = _ => null,
            GitResolver = new FileSystemGitContextResolver(ceilingDirectory: temp.Path),
            TimeProvider = new ManualTimeProvider(AuditSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5),
            Version = "9.9.9",
        });
        CallToolResult findings = await mcp.Client.CallToolAsync("get_audit_findings", new Dictionary<string, object?>());
        SqliteConnection.ClearAllPools();

        Assert.Contains($"Fix the bash failures ({Canary}).", advised.Out);
        Assert.DoesNotContain(Canary, advised.Error);
        Assert.DoesNotContain(Canary, json.Out);
        Assert.Equal(0, export.ExitCode);
        Assert.DoesNotContain(Canary, ExportFiles.ReadReport(Path.Combine(temp.Path, "copilot-ledger-report.html")));
        Assert.All(
            ExportFiles.ReadZip(Path.Combine(temp.Path, "copilot-ledger-report.zip")),
            entry => Assert.DoesNotContain(Canary, Encoding.UTF8.GetString(entry.Bytes)));
        Assert.DoesNotContain(Canary, Assert.IsType<TextContentBlock>(Assert.Single(findings.Content)).Text);
        byte[] canary = Encoding.UTF8.GetBytes(Canary);
        Assert.All(
            Directory.GetFiles(temp.Path, "*", SearchOption.AllDirectories),
            file => Assert.True(File.ReadAllBytes(file).AsSpan().IndexOf(canary) < 0, file));
    }

    [Fact]
    public async Task AV2_TheOutput_IsTheSameInEveryCulture()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (string name in new[] { "de-DE", "fr-FR", "" })
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

                CliRun table = await cli.RunAsync("audit");
                CliRun json = await cli.RunAsync("audit", "--json");

                Assert.Equal(AuditOutputs.Table + "\n", table.Out);
                Assert.Equal(AuditOutputs.Json + "\n", json.Out);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    public static TheoryData<string, bool> Snippets => new()
    {
        { "decimal cost = 1m; double approximate = (double)cost;", true },
        { "float ratio = 0.5f;", true },
        { "System.Double value = 0;", true },
        { "Single value = 0;", true },
        { "long ms = (long)span.TotalMilliseconds;", true },
        { "decimal x = element.GetDouble() is var d ? 0m : 0m;", true },
        { "string sql = \"SELECT CAST(cost_usd_pico AS REAL) FROM spans;\";", true },
        { "AuditFinding finding = findings.Single(item => item.RuleId == id);", false },
        { "var row = rows.SingleOrDefault();", false },
        { "// a double counts nothing; /* float */", false },
        { "string text = \"a double-check, a float, a single request\";", false },
        { "bool realTime = RealTime;", false },
    };

    [Theory]
    [MemberData(nameof(Snippets))]
    public void AV3_TheFloatingPointScan_FindsRealUsesOnly(string code, bool found)
    {
        Assert.Equal(found, FloatingPoint(code).Any());
    }

    [Fact]
    public void AV3_TheAuditCode_UsesNoBinaryFloatingPoint()
    {
        string root = RepositoryRoot();
        string[] files =
        [
            .. Directory.GetFiles(Path.Combine(root, "src", "CopilotLedger.Core", "Audit"), "*.cs"),
            .. Directory.GetFiles(Path.Combine(root, "src", "CopilotLedger.Cli", "Audit"), "*.cs"),
            Path.Combine(root, "src", "CopilotLedger.Storage", "SqliteLedgerStore.Audit.cs"),
            Path.Combine(root, "src", "CopilotLedger.Core", "Pricing", "JsonRateCardCalculator.cs"),
        ];

        Assert.True(files.Length >= 30, $"{files.Length} files");
        Assert.All(files, file => Assert.Empty(FloatingPoint(File.ReadAllText(file))));
    }

    [Fact]
    public async Task AV4_TheJson_IsTheSameBytesOnEveryRun_AndHasNoCarriageReturn()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        string first = await RawAsync(cli.Context, "audit", "--json");
        string second = await RawAsync(cli.Context, "audit", "--json");

        Assert.Equal(first, second);
        Assert.DoesNotContain('\r', first);
        Assert.Equal(AuditOutputs.Json + "\n", first);
    }

    [Fact]
    public async Task AV5_AnAudit_WritesNothingToTheLedger()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        string database = cli.Context.DatabasePath;
        SqliteConnection.ClearAllPools();
        byte[] before = File.ReadAllBytes(database);
        long spans = (long)(await LedgerSql.ScalarAsync(database, "SELECT COUNT(*) FROM spans;"))!;
        SqliteConnection.ClearAllPools();

        CliRun run = await cli.RunWithLocalLlmAsync(
            () => FakeLlmHandler.Returning(() => FakeLlmHandler.Answer("ok")),
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3");
        await cli.RunAsync("audit", "--json");
        SqliteConnection.ClearAllPools();

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(spans, (long)(await LedgerSql.ScalarAsync(database, "SELECT COUNT(*) FROM spans;"))!);
        SqliteConnection.ClearAllPools();
        Assert.Equal(before, File.ReadAllBytes(database));
    }

    // The uses of binary floating point in C# code: the double, float, Double, and Single types (not LINQ's Single), the
    // members that return a double, and REAL in SQL strings. Comments and other string contents don't count.
    private static IEnumerable<string> FloatingPoint(string code)
    {
        List<string> strings = [];
        string bare = Literal().Replace(code, match =>
        {
            if (match.Value.StartsWith('"') || match.Value.StartsWith('$') || match.Value.StartsWith('@'))
            {
                strings.Add(match.Value);
                return "\"\"";
            }

            return match.Value.StartsWith('\'') ? "' '" : " ";
        });
        foreach (Match match in TypeOrMember().Matches(bare))
        {
            yield return match.Value;
        }

        foreach (string literal in strings)
        {
            foreach (Match match in SqlReal().Matches(literal))
            {
                yield return match.Value;
            }
        }
    }

    // Comments, then string and character literals (raw, verbatim, interpolated, and regular).
    [GeneratedRegex("""//[^\n]*|/\*.*?\*/|\$*"{3,}.*?"{3,}|[$@]{0,2}"(?:[^"\\\n]|\\.|"")*"|'(?:[^'\\\n]|\\.)'""", RegexOptions.Singleline)]
    private static partial Regex Literal();

    [GeneratedRegex(@"\b(?:double|float|Double)\b|(?<!\.)\bSingle\b(?!\s*\()|\b(?:GetDouble|TryGetDouble|ToDouble|TotalDays|TotalHours|TotalMinutes|TotalSeconds|TotalMilliseconds)\b")]
    private static partial Regex TypeOrMember();

    [GeneratedRegex(@"\bREAL\b", RegexOptions.IgnoreCase)]
    private static partial Regex SqlReal();

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), new ManualTimeProvider(AuditSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

    // The command's standard output as written: CliHarness would replace "\r\n".
    private static async Task<string> RawAsync(CliContext context, params string[] args)
    {
        StringWriter output = new(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        StringWriter error = new(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        Assert.Equal(0, await GhLedgerCli.InvokeAsync(args, context with { Out = output, Error = error }));
        Assert.Equal("", error.ToString());
        return output.ToString();
    }

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
