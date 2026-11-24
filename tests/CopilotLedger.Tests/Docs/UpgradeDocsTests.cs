namespace CopilotLedger.Tests.Docs;

// The upgrade steps in docs/guide.md and docs/cli.md copy the data directory after the daemon stops and before gh upgrades
// the extension: a new release can upgrade the ledger's format the first time it opens the ledger, and an earlier release
// can't open it after that.
public sealed class UpgradeDocsTests
{
    // The copy, in PowerShell and in a macOS or Linux shell: the whole data directory, so that a ledger.db-wal file comes
    // with the ledger, to a new folder in the home directory, outside the data directory.
    private static readonly string PowerShellCopy = """
        $data = if ($env:COPILOT_LEDGER_HOME) { $env:COPILOT_LEDGER_HOME } else { "$HOME\.copilot-ledger" }
        Copy-Item -LiteralPath $data -Destination "$HOME\copilot-ledger-backup-$(Get-Date -Format yyyyMMdd-HHmmss)" -Recurse
        """.ReplaceLineEndings("\n");

    private const string ShellCopy = """
        cp -R "${COPILOT_LEDGER_HOME:-$HOME/.copilot-ledger}" "$HOME/copilot-ledger-backup-$(date +%Y%m%d-%H%M%S)"
        """;

    // Each document's upgrade steps: the line they start with, and the line after them.
    public static TheoryData<string, string, string> UpgradeSteps => new()
    {
        { "guide.md", "## Upgrade or uninstall", "To remove gh-ledger, undo `install`:" },
        { "cli.md", "**Upgrading or removing the extension.**", "## The daemon" },
    };

    [Theory]
    [MemberData(nameof(UpgradeSteps))]
    public void UD1_TheUpgradeStepsCopyTheDataDirectoryBeforeTheUpgrade(string file, string start, string end)
    {
        string[] lines = Steps(file, start, end);
        (int Line, string Language, string Text)[] blocks = Blocks(lines);
        int[] order =
        [
            Array.FindIndex(blocks, block => block.Language == "shell" && block.Text.StartsWith("gh ledger daemon stop", StringComparison.Ordinal)),
            Array.FindIndex(blocks, block => block.Language == "powershell" && block.Text == PowerShellCopy),
            Array.FindIndex(blocks, block => block.Language == "shell" && block.Text == ShellCopy),
            Array.FindIndex(blocks, block => block.Language == "shell" && block.Text.StartsWith("gh extension upgrade ledger", StringComparison.Ordinal)),
        ];

        Assert.DoesNotContain(-1, order);
        Assert.Equal(order.Order(), order);
        Assert.Contains("can't be undone", string.Join('\n', lines[..blocks[order[1]].Line]), StringComparison.Ordinal);
    }

    [Fact]
    public void UD2_TheReferenceExplainsTheLedgerFormatBeforeItsUpgradeSteps()
    {
        string[] lines = Steps("cli.md", "**Upgrading or removing the extension.**", "## The daemon");
        int format = Array.FindIndex(lines, line => line.StartsWith("**Ledger format.**", StringComparison.Ordinal));
        int firstBlock = Array.FindIndex(lines, line => line.StartsWith("```", StringComparison.Ordinal));

        Assert.InRange(format, 0, firstBlock);
    }

    // The lines of docs/<file> from the first line that starts with start up to, not including, the next line that starts
    // with end.
    private static string[] Steps(string file, string start, string end)
    {
        string[] lines = File.ReadAllLines(RepositoryFile("docs", file));
        int first = Array.FindIndex(lines, line => line.StartsWith(start, StringComparison.Ordinal));
        int last = first < 0 ? -1 : Array.FindIndex(lines, first + 1, line => line.StartsWith(end, StringComparison.Ordinal));

        Assert.True(first >= 0 && last > first, $"docs/{file} has no {start} followed by {end}.");
        return lines[first..last];
    }

    // The fenced blocks in lines, in order: each one's opening line, its language, and its lines joined with \n.
    private static (int Line, string Language, string Text)[] Blocks(string[] lines)
    {
        List<(int Line, string Language, string Text)> blocks = [];
        for (int index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("```", StringComparison.Ordinal))
            {
                int close = Array.IndexOf(lines, "```", index + 1);
                Assert.True(close > index, $"The block at line {index} has no end.");
                blocks.Add((index, lines[index][3..], string.Join('\n', lines[(index + 1)..close])));
                index = close;
            }
        }

        return [.. blocks];
    }

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
