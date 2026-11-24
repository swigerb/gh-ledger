using CopilotLedger.Cli;

namespace CopilotLedger.Tests.Docs;

// Step 1 of docs/releasing.md lists every place that writes the version. The places that no other test reads must show the
// project's <Version> too, so a release that misses one fails here.
public sealed class VersionPlacesTests
{
    [Fact]
    public void VR1_TheListedPlacesShowTheProjectVersion()
    {
        string version = GhLedgerCli.Version;
        string cli = Text("src", "CopilotLedger.Cli", "GhLedgerCli.cs");

        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
        Assert.Contains($"    // AssemblyInformationalVersionAttribute: \"{version}\"\n", cli);
        Assert.Contains($"        ?? \"{version}\";\n", cli);
        Assert.Contains($"--version | tr -d '\\r')\" = \"{version}\"\n", Text(".github", "workflows", "ci.yml"));
        Assert.Contains($"  \"toolVersion\": \"{version}\",\n", Text("docs", "cli.md"));
        Assert.Contains($"   git tag v{version}\n   git push origin v{version}\n", Text("docs", "releasing.md"));
    }

    // A repository file's text with \n line breaks, whatever the checkout's.
    private static string Text(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CopilotLedger.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory.FullName, .. parts])).ReplaceLineEndings("\n");
    }
}
