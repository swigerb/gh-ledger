using System.Text;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class RatesCommandTests
{
    private const string Scaffold = """
        {
          "schemaVersion": 1,
          "notes": [
            "gh-ledger rates overlay. See docs/configuration.md in the gh-ledger repository.",
            "Prices are USD per 1,000,000 tokens, from 0 to 1,000,000, with at most 6 decimal places."
          ],
          "foundryByok": {
            "hostSuffixes": [],
            "models": []
          }
        }
        """;

    [Fact]
    public async Task RT1_InitIsIdempotentUnlessForced()
    {
        using var temp = new TempDirectory();
        await using var harness = new CliHarness(temp.Path);
        string path = Path.Combine(temp.Path, "data", "rates.json");

        CliRun first = await harness.RunAsync("rates", "init");
        Assert.Equal(0, first.ExitCode);
        Assert.Equal(
            $"Created {path}. Add your prices, then run: gh-ledger rates validate\n",
            first.Out);
        Assert.Equal(
            Encoding.UTF8.GetBytes(Scaffold.ReplaceLineEndings("\n") + "\n"),
            await FileRetry.ReadAllBytesAsync(path));
        string original = await FileRetry.ReadAllTextAsync(path);

        CliRun second = await harness.RunAsync("rates", "init");
        Assert.Equal(1, second.ExitCode);
        Assert.Equal(
            $"{path} already exists. Use --force to replace it.\n",
            second.Error);
        Assert.Equal(original, await FileRetry.ReadAllTextAsync(path));
        CliRun forced = await harness.RunAsync("rates", "init", "--force");
        Assert.Equal(0, forced.ExitCode);
        Assert.Equal(original, await FileRetry.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task RT2_ValidateUsesDefaultAndCreatedOverlay()
    {
        using var temp = new TempDirectory();
        await using var harness = new CliHarness(temp.Path);
        string path = Path.Combine(temp.Path, "data", "rates.json");

        CliRun missing = await harness.RunAsync("rates", "validate");
        Assert.Equal(0, missing.ExitCode);
        Assert.Equal(
            $"No rates overlay at {path}; using the built-in rates (2026-11-23).\n",
            missing.Out);
        await harness.RunAsync("rates", "init");
        CliRun created = await harness.RunAsync("rates", "validate");
        Assert.Equal(0, created.ExitCode);
        Assert.Equal(
            $"{path} is valid. Rates version: 2026-11-23+user.\n",
            created.Out);
    }

    public static TheoryData<string, string> InvalidFiles => new()
    {
        { """{ "schemaVersion": 3 }""", "schemaVersion: must be 1 or 2" },
        { "{ \"schemaVersion\": 1,\n  \"foundryByok\": { \"models\": [ }\n}\n", "line 2, column 32: invalid JSON" },
        { """{ "schemaVersion": 1, "foundryBYOK": { } }""", "foundryBYOK: is not a known property; did you mean foundryByok?" },
    };

    [Theory]
    [MemberData(nameof(InvalidFiles))]
    public async Task RT3_InvalidFileAndRelativePath_ReportExactErrors(
        string contents,
        string problem)
    {
        using var temp = new TempDirectory();
        await using var harness = new CliHarness(temp.Path);
        string path = Path.Combine(temp.Path, "bad.json");
        await File.WriteAllTextAsync(path, contents);
        CliRun run = await harness.RunAsync("rates", "validate", "--file", path);

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Out);
        Assert.Equal($"Invalid rates overlay: {path}: {problem}\n", run.Error);

    }

    [Fact]
    public async Task RT3_RelativeFileReportsResolvedPath()
    {
        using var temp = new TempDirectory();
        await using var harness = new CliHarness(temp.Path);
        string path = Path.Combine(temp.Path, "bad.json");
        await File.WriteAllTextAsync(path, """{ "schemaVersion": 3 }""");

        CliRun relative = await harness.RunInAsync(
            temp.Path,
            "rates",
            "validate",
            "--file",
            "bad.json");
        Assert.Equal(1, relative.ExitCode);
        Assert.Empty(relative.Out);
        Assert.Equal(
            $"Invalid rates overlay: {path}: schemaVersion: must be 1 or 2\n",
            relative.Error);
    }

    [Fact]
    public async Task RT3_MissingExplicitFile_ReportsItsPath()
    {
        using var temp = new TempDirectory();
        await using var harness = new CliHarness(temp.Path);
        string path = Path.Combine(temp.Path, "missing.json");
        CliRun run = await harness.RunAsync("rates", "validate", "--file", path);

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Out);
        Assert.Equal($"{path} does not exist.\n", run.Error);
    }
}
