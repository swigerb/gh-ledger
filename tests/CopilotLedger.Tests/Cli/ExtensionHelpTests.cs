using System.CommandLine;
using System.Globalization;
using System.Text.RegularExpressions;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class ExtensionHelpTests
{
    public static TheoryData<string[], int, string> HelpCases => new()
    {
        { ["--help"], 0, "[command] [options]" },
        { ["import", "--help"], 0, "import [options]" },
        { ["budget", "set", "--help"], 0, "budget set <credits> [options]" },
        { ["daemon", "stop", "-h"], 0, "daemon stop [options]" },
        { ["budget", "set"], 1, "budget set <credits> [options]" },
        { ["daemon"], 1, "daemon [command] [options]" },
        { ["imprt"], 1, "[command] [options]" },
    };

    [Theory]
    [MemberData(nameof(HelpCases))]
    public async Task HL1_UnderTheExtension_OnlyTheUsageLineChanges(
        string[] args,
        int exitCode,
        string usage)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            using TempDirectory temp = new();
            string root = RootCommand.ExecutableName;

            CliRun built = await RunAsync(temp.Path, Built(), args);
            CliRun extension = await RunAsync(temp.Path, Extension(), args);

            Assert.Equal(exitCode, built.ExitCode);
            Assert.Equal(exitCode, extension.ExitCode);
            string usageLine = "  " + root + " " + usage;
            Assert.Single(built.Out.Split('\n'), line => line == usageLine);
            Assert.Equal(
                built.Out.Replace(
                    "\n" + usageLine + "\n",
                    "\n  gh ledger " + usage + "\n",
                    StringComparison.Ordinal),
                extension.Out);
            Assert.Equal(built.Error, extension.Error);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HL2_DaemonStopsHelp_IsTheDefaultExceptForTheName(bool extension)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            using TempDirectory temp = new();
            FakeCliPlatform platform = extension ? Extension() : Built();
            string name = extension ? "gh ledger" : RootCommand.ExecutableName;

            CliRun run = await RunAsync(temp.Path, platform, "daemon", "stop", "--help");

            Assert.Equal(0, run.ExitCode);
            Assert.Equal(string.Empty, run.Error);
            Assert.Equal(
                "Description:\n" +
                "  Stop the running daemon.\n" +
                "\n" +
                "Usage:\n" +
                "  " + name + " daemon stop [options]\n" +
                "\n" +
                "Options:\n" +
                "  -?, -h, --help  Show help and usage information\n" +
                "\n",
                run.Out);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    [Fact]
    public async Task HL3_UnderTheExtension_EveryCommandsUsageLineStartsWithGhLedger()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            using TempDirectory temp = new();
            Queue<string[]> paths = new();
            paths.Enqueue([]);
            int runs = 0;

            while (paths.TryDequeue(out string[]? path))
            {
                CliRun run = await RunAsync(
                    temp.Path,
                    Extension(),
                    [.. path, "--help"]);
                Assert.Equal(0, run.ExitCode);
                string[] lines = run.Out.Split('\n');
                int usage = Array.IndexOf(lines, "Usage:");
                Assert.True(usage >= 0);
                Assert.StartsWith(
                    string.Join(' ', ["  gh ledger", .. path]) + " ",
                    lines[usage + 1]);

                int commands = Array.IndexOf(lines, "Commands:");
                if (commands >= 0)
                {
                    for (int i = commands + 1; i < lines.Length && lines[i].Length != 0; i++)
                    {
                        Match match = Regex.Match(
                            lines[i],
                            @"^  (?<name>[a-z][a-z-]*)( |$)");
                        if (match.Success)
                        {
                            paths.Enqueue([.. path, match.Groups["name"].Value]);
                        }
                    }
                }

                runs++;
            }

            Assert.Equal(29, runs);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    private static FakeCliPlatform Extension() =>
        new()
        {
            IsWindows = true,
            ProcessPath = @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe",
            ExistingFiles =
            {
                @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\manifest.yml",
            },
        };

    private static FakeCliPlatform Built() => new();

    private static async Task<CliRun> RunAsync(
        string root,
        FakeCliPlatform platform,
        params string[] args)
    {
        await using CliHarness harness = new(root, platform);
        return await harness.RunAsync(args);
    }
}
