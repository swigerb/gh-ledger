using System.Text.RegularExpressions;

namespace CopilotLedger.Tests.Docs;

// The product's name is gh-ledger. CopilotLedger stays only as an identifier that existing installs rely on: the namespaces
// and projects (CopilotLedger.<name>), the Windows sign-in item's Run value, and the app name that notify-send gives
// notification settings.
public sealed partial class ProductNameTests
{
    [Fact]
    public void PN1_TheSourceNamesCopilotLedgerOnlyAsAnIdentifier()
    {
        Assert.Empty(Mentions(FilesUnder(RepositoryFile("src")), line => line
            .Replace("\"CopilotLedger\"", string.Empty, StringComparison.Ordinal)
            .Replace("\\Run\\CopilotLedger\"", string.Empty, StringComparison.Ordinal)
            .Replace("\"--app-name=CopilotLedger\"", string.Empty, StringComparison.Ordinal)));
    }

    // "<file>:<line>" for each line that still names CopilotLedger, or Copilot Ledger, in any case, once removeAllowed and
    // Identifier have taken out what may stay.
    private static string[] Mentions(IEnumerable<string> files, Func<string, string> removeAllowed)
    {
        string root = RepositoryFile();
        return
        [
            .. files.Order(StringComparer.Ordinal).SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (Text: removeAllowed(Identifier().Replace(line, string.Empty)), Number: index + 1))
                .Where(line => line.Text.Contains("CopilotLedger", StringComparison.OrdinalIgnoreCase)
                    || line.Text.Contains("Copilot Ledger", StringComparison.OrdinalIgnoreCase))
                .Select(line => $"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{line.Number}")),
        ];
    }

    // The files under folder, recursively, skipping bin and obj folders.
    private static IEnumerable<string> FilesUnder(string folder) =>
        Directory.GetFiles(folder).Concat(Directory.GetDirectories(folder)
            .Where(directory => Path.GetFileName(directory) is not ("bin" or "obj"))
            .SelectMany(FilesUnder));

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

    // A namespace or project name: CopilotLedger followed by a dot and a letter, as in CopilotLedger.Cli.
    [GeneratedRegex(@"CopilotLedger(?=\.[A-Za-z])")]
    private static partial Regex Identifier();
}
