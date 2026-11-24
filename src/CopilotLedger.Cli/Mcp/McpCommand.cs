using System.CommandLine;
using System.CommandLine.Parsing;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Core.Git;
using CopilotLedger.Mcp;

namespace CopilotLedger.Cli.Mcp;

internal static class McpCommand
{
    public static Command Create(CliContext context)
    {
        Command command = new(
            "mcp",
            "Run the gh-ledger MCP server on standard input and output.");
        command.SetAction(
            (ParseResult _, CancellationToken cancellationToken) =>
                RunAsync(context, cancellationToken));
        return command;
    }

    private static async Task<int> RunAsync(
        CliContext context,
        CancellationToken cancellationToken)
    {
        await LedgerMcpServer.RunAsync(
            new LedgerMcpContext
            {
                DatabasePath = context.DatabasePath,
                WorkingDirectory = context.WorkingDirectory,
                GetEnvironmentVariable = context.Platform.GetEnvironmentVariable,
                GitResolver = new FileSystemGitContextResolver(),
                TimeProvider = context.TimeProvider,
                Version = GhLedgerCli.Version,
                LoadAuditPricing = () => RateCardFiles.Load(RateCardFiles.LoadBase(context), RateCardFiles.OverlayPath(context)),
            },
            context.StandardInput ?? Console.OpenStandardInput(),
            context.StandardOutput ?? Console.OpenStandardOutput(),
            cancellationToken);
        return 0;
    }
}
