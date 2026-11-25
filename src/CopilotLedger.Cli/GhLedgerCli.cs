using System.CommandLine;
using System.CommandLine.Help;
using System.Reflection;
using CopilotLedger.Cli.Audit;
using CopilotLedger.Cli.Breaker;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Export;
using CopilotLedger.Cli.Import;
using CopilotLedger.Cli.Install;
using CopilotLedger.Cli.Mcp;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Cli.Receipts;

namespace CopilotLedger.Cli;

public static class GhLedgerCli
{
    // AssemblyInformationalVersionAttribute: "0.7.0"
    public static string Version { get; } =
        typeof(GhLedgerCli).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "0.7.0";

    // Builds a new RootCommand, parses args, and invokes it with
    // new InvocationConfiguration { Output = context.Out, Error = context.Error, ProcessTerminationTimeout = null }.
    public static Task<int> InvokeAsync(
        string[] args,
        CliContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);
        RootCommand root = new(
            "gh-ledger: a local AI-spend ledger for GitHub Copilot");
        root.Add(DaemonCommands.Create(context));
        root.Add(RatesCommands.Create(context));
        foreach (Command command in ReportCommands.Create(context))
        {
            root.Add(command);
        }

        foreach (Command command in InstallCommands.Create(context))
        {
            root.Add(command);
        }

        foreach (Command command in BreakerCommands.Create(context))
        {
            root.Add(command);
        }

        root.Add(ReceiptCommands.Create(context));
        root.Add(ExportCommands.Create(context));
        root.Add(AuditCommands.Create(context));
        root.Add(ImportCommands.Create(context));
        root.Add(McpCommand.Create(context));

        if (GhExtension.IsInstalled(context.Platform))
        {
            HelpOption help = root.Options.OfType<HelpOption>().Single();
            help.Action = new ExtensionHelpAction((HelpAction)help.Action!, root.Name);
        }

        return root.Parse(args).InvokeAsync(
            new InvocationConfiguration
            {
                Output = context.Out,
                Error = context.Error,
                ProcessTerminationTimeout = null,
            },
            cancellationToken);
    }
}
