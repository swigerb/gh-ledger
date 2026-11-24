using System.Text.RegularExpressions;

namespace CopilotLedger.Tests.Docs;

public sealed class ReleaseWorkflowTests
{
    [Fact]
    public void RW1_EveryActionIsPinnedToACommit()
    {
        Regex pinned = new(@"^\s*(- )?uses: [A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40} # v\d+\.\d+\.\d+$");
        string[] uses =
        [
            .. Directory.GetFiles(RepositoryFile(".github", "workflows"), "*.yml")
                .SelectMany(File.ReadAllLines)
                .Where(line =>
                {
                    string trimmed = line.TrimStart();
                    return trimmed.StartsWith("uses:", StringComparison.Ordinal)
                        || trimmed.StartsWith("- uses:", StringComparison.Ordinal);
                }),
        ];

        Assert.True(uses.Length >= 9, "Expected at least 9 action uses across all workflows.");
        Assert.All(uses, line => Assert.Matches(pinned, line));
    }

    [Fact]
    public void RW2_TheReleaseJobAttestsTheBinariesAndRates_BeforeItReleases()
    {
        string[] lines = File.ReadAllLines(RepositoryFile(".github", "workflows", "release.yml"))
            .Select(line => line.Trim())
            .ToArray();
        const string attestation = "uses: actions/attest-build-provenance@4d101475d8b20a2381f78447822ac1eab6504dd8 # v4.2.2";
        int attest = Assert.Single(lines.Select((line, index) => (Line: line, Index: index)),
            item => item.Line == attestation).Index;
        int addRates = Array.IndexOf(lines, "- name: Add the rates and checksums");
        int release = Array.IndexOf(lines, "- name: Create the release");
        int subjects = Array.IndexOf(lines, "subject-path: |");

        Assert.True(attest > addRates && attest < release);
        Assert.Equal(
        [
            "dist/gh-ledger-windows-amd64.exe",
            "dist/gh-ledger-windows-arm64.exe",
            "dist/gh-ledger-linux-amd64",
            "dist/gh-ledger-linux-arm64",
            "dist/gh-ledger-darwin-amd64",
            "dist/gh-ledger-darwin-arm64",
            "dist/rates.json",
            "dist/rates.v2.json",
        ], lines[(subjects + 1)..(subjects + 9)]);
    }

    [Fact]
    public void RW3_OnlyTheTagReleaseJobMayAttest()
    {
        string[] workflowFiles = Directory.GetFiles(RepositoryFile(".github", "workflows"), "*.yml");
        string all = string.Join('\n', workflowFiles.SelectMany(File.ReadAllLines));
        string release = File.ReadAllText(RepositoryFile(".github", "workflows", "release.yml"))
            .ReplaceLineEndings("\n");
        int releaseJob = release.IndexOf("  release:\n", StringComparison.Ordinal);
        int permissions = release.IndexOf("    permissions:\n", releaseJob, StringComparison.Ordinal);
        string expectedPermissions = "    permissions:\n"
            + "      contents: write\n"
            + "      id-token: write\n"
            + "      attestations: write\n";

        Assert.Equal(1, Count(all, "id-token: write"));
        Assert.Equal(1, Count(all, "attestations: write"));
        Assert.True(releaseJob >= 0);
        Assert.StartsWith(expectedPermissions, release[permissions..], StringComparison.Ordinal);
        Assert.StartsWith("name: Release\n\non:\n  push:\n    tags: ['v*']\n  workflow_dispatch:\n\npermissions:\n  contents: read\n",
            release);
        Assert.Contains("    if: github.event_name == 'push' && github.ref_type == 'tag'", release,
            StringComparison.Ordinal);
        Assert.Contains("  workflow_dispatch:", release, StringComparison.Ordinal);
    }

    [Fact]
    public void RW4_TheDocsShowHowToVerify()
    {
        const string workflow = "--repo swigerb/gh-ledger --signer-workflow swigerb/gh-ledger/.github/workflows/release.yml";
        string cli = Text("docs", "cli.md");
        string releasing = Text("docs", "releasing.md");

        Assert.All([cli, releasing], text =>
        {
            Assert.Contains("gh attestation verify ", text, StringComparison.Ordinal);
            Assert.Contains(workflow, text, StringComparison.Ordinal);
            Assert.Contains("Authenticode", text, StringComparison.Ordinal);
        });
        Assert.Contains("## Verify a release", releasing, StringComparison.Ordinal);
        Assert.Contains("--source-ref refs/tags/<tag>", releasing, StringComparison.Ordinal);
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int start = 0;
        while ((start = text.IndexOf(value, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += value.Length;
        }

        return count;
    }

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
}
