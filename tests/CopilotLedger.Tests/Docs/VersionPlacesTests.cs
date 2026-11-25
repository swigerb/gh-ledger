using System.Text.RegularExpressions;
using CopilotLedger.Cli;

namespace CopilotLedger.Tests.Docs;

// Step 1 of docs/releasing.md lists every place that writes the version. The places that no other test reads must show the
// project's <Version> too, so a release that misses one fails here, and every other line that names the version must be a
// mention that step 1 says to leave.
public sealed class VersionPlacesTests
{
    // The lines that may name the version: each one's file, and text that the line holds. First the places to change, with
    // {version} for the version, which VR1 and the tests that step 1 names check. Then the mentions to leave, with the
    // version they name written out, so that changing one fails VR3, and the words that step 1's list of them uses.
    private static readonly (string File, string Text, string? Step1)[] Places =
    [
        (".github/workflows/ci.yml", "--version | tr -d '\\r')\" = \"{version}\"", null),
        ("docs/cli.md", "  \"toolVersion\": \"{version}\",", null),
        ("docs/guide.md", "gh-ledger {version} (GitHub CLI extension)", null),
        ("docs/guide.md", " UTC by gh-ledger {version}", null),
        ("docs/guide.md", "  \"generator\": \"gh-ledger {version}\",", null),
        ("docs/releasing.md", "   git tag v{version}", null),
        ("docs/releasing.md", "   git push origin v{version}", null),
        ("src/CopilotLedger.Cli/CopilotLedger.Cli.csproj", "<Version>{version}</Version>", null),
        ("src/CopilotLedger.Cli/GhLedgerCli.cs", "// AssemblyInformationalVersionAttribute: \"{version}\"", null),
        ("src/CopilotLedger.Cli/GhLedgerCli.cs", "?? \"{version}\";", null),
        ("tests/CopilotLedger.Tests/Cli/DaemonCommandTests.cs", "Assert.Equal(\"{version}\", root.GetProperty(\"version\").GetString());", null),
        ("tests/CopilotLedger.Tests/Cli/ExportBundleTests.cs", "(1, \"gh-ledger\", \"{version}\")", null),
        ("tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs", "\"2026-11-20 22:00 by gh-ledger {version}\"", null),
        ("tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs", "gh-ledger {version}", null),
        ("docs/releasing.md", "such as `v0.5.0-rc.1`", "the prerelease tag"),
        ("tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs", "gh-ledger 0.5.0 builds the name in", "`GuideSamplesTests.HaikuOverlay`"),
    ];

    [Fact]
    public void VR1_TheListedPlacesShowTheProjectVersion() => AssertTheListedPlacesShow(GhLedgerCli.Version, Text);

    // docs/releasing.md allows a prerelease, whose <Version> and tag end in a suffix such as -rc.1. When every place shows a
    // prerelease of this version, VR1's checks pass too.
    [Fact]
    public void VR2_APrereleaseVersionPassesTheSameChecks()
    {
        string prerelease = GhLedgerCli.Version + "-rc.1";

        AssertTheListedPlacesShow(prerelease, parts => Text(parts).Replace(GhLedgerCli.Version, prerelease, StringComparison.Ordinal));
    }

    // Every line of the README, the docs, the workflows, src, and tests that names the version is one of Places, so a new
    // mention fails here until step 1 of docs/releasing.md and Places list it, and step 1's list of the versions to leave
    // names each mention to leave. This file writes those mentions out, so VR3 skips it.
    [Fact]
    public void VR3_EveryLineThatNamesTheVersionIsListed()
    {
        string version = GhLedgerCli.Version;
        Regex names = new($@"(?<![0-9.]){Regex.Escape(version)}(?![0-9]|\.[0-9])");
        string root = RepositoryFile();
        string[] files =
        [
            RepositoryFile("README.md"),
            .. Directory.GetFiles(RepositoryFile("docs"), "*.md"),
            .. Directory.GetFiles(RepositoryFile(".github", "workflows"), "*.yml"),
            .. FilesUnder(RepositoryFile("src")),
            .. FilesUnder(RepositoryFile("tests")),
        ];
        string[] unlisted =
        [
            .. files.Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
                .Where(file => file != "tests/CopilotLedger.Tests/Docs/VersionPlacesTests.cs")
                .Order(StringComparer.Ordinal)
                .SelectMany(file => File.ReadAllLines(Path.Combine(root, file))
                    .Select((line, index) => (Text: line, Number: index + 1))
                    .Where(line => names.IsMatch(line.Text) && !Places.Any(place => place.File == file
                        && line.Text.Contains(place.Text.Replace("{version}", version, StringComparison.Ordinal), StringComparison.Ordinal)))
                    .Select(line => $"{file}:{line.Number}")),
        ];
        string releasing = Text("docs", "releasing.md");
        int leave = releasing.IndexOf("that the docs and tests name on purpose:", StringComparison.Ordinal);

        Assert.Empty(unlisted);
        Assert.True(leave >= 0, "docs/releasing.md doesn't list the versions to leave.");
        Assert.All(
            Places.Where(place => place.Step1 is not null),
            place => Assert.Contains(place.Step1!, releasing[leave..releasing.IndexOf('\n', leave)], StringComparison.Ordinal));
    }

    // VR1's checks of version: it's a version, with or without a prerelease suffix, and the places that no other test reads
    // show it. text reads a repository file, given its path's parts.
    private static void AssertTheListedPlacesShow(string version, Func<string[], string> text)
    {
        string cli = text(["src", "CopilotLedger.Cli", "GhLedgerCli.cs"]);

        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", version);
        Assert.Contains($"    // AssemblyInformationalVersionAttribute: \"{version}\"\n", cli);
        Assert.Contains($"        ?? \"{version}\";\n", cli);
        Assert.Contains($"--version | tr -d '\\r')\" = \"{version}\"\n", text([".github", "workflows", "ci.yml"]));
        Assert.Contains($"  \"toolVersion\": \"{version}\",\n", text(["docs", "cli.md"]));
        Assert.Contains($"   git tag v{version}\n   git push origin v{version}\n", text(["docs", "releasing.md"]));
    }

    // A repository file's text with \n line breaks, whatever the checkout's.
    private static string Text(params string[] parts) =>
        File.ReadAllText(RepositoryFile(parts)).ReplaceLineEndings("\n");

    private static string RepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CopilotLedger.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }

    // The files under folder, recursively, skipping bin, obj, and TestResults folders.
    private static IEnumerable<string> FilesUnder(string folder) =>
        Directory.GetFiles(folder).Concat(Directory.GetDirectories(folder)
            .Where(directory => Path.GetFileName(directory) is not ("bin" or "obj" or "TestResults"))
            .SelectMany(FilesUnder));
}
