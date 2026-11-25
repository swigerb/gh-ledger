using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Globalization;

namespace CopilotLedger.Cli.Install;

// --help, and the help after a parse error, of the GitHub CLI extension: System.CommandLine's help, whose usage line names
// the root command by the executable's name (rootName), with "gh ledger" in its place. Everything else is unchanged.
internal sealed class ExtensionHelpAction(HelpAction help, string rootName) : SynchronousCommandLineAction
{
    public override bool ClearsParseErrors => help.ClearsParseErrors;

    public override int Invoke(ParseResult parseResult)
    {
        InvocationConfiguration configuration = parseResult.InvocationConfiguration;
        TextWriter output = configuration.Output;
        StringWriter buffer = new(CultureInfo.InvariantCulture) { NewLine = output.NewLine };
        configuration.Output = buffer;
        int exitCode;
        try
        {
            exitCode = help.Invoke(parseResult);
        }
        finally
        {
            configuration.Output = output;
        }

        output.Write(Rewrite(buffer.ToString(), rootName, output.NewLine));
        return exitCode;
    }

    // help with "gh ledger" in place of rootName in its usage line: the first line after the first empty line that equals
    // "  " + rootName or starts with "  " + rootName + " ". The help's other lines, and its line breaks (newLine), are kept.
    internal static string Rewrite(string help, string rootName, string newLine)
    {
        string[] lines = help.Split(newLine);
        int blank = Array.IndexOf(lines, string.Empty);
        if (blank < 0)
        {
            return help;
        }

        string prefix = "  " + rootName;
        for (int i = blank + 1; i < lines.Length; i++)
        {
            if (lines[i].Equals(prefix, StringComparison.Ordinal)
                || lines[i].StartsWith(prefix + " ", StringComparison.Ordinal))
            {
                lines[i] = "  gh ledger" + lines[i][prefix.Length..];
                break;
            }
        }

        return string.Join(newLine, lines);
    }
}
