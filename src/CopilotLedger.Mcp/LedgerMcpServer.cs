using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CopilotLedger.Mcp;

public static class LedgerMcpServer
{
    public const string ServerName = "copilot-ledger";
    public const string Instructions =
        "gh-ledger reports the AI spend that Copilot recorded on this machine, by Git branch. " +
        "Tools use the current repository and branch unless you pass repository and branch. " +
        "When a result starts with WARNING, stop and tell the user before doing more work.";

    // ServerInfo = { Name = ServerName, Version = context.Version }, ServerInstructions = Instructions, and the two handlers.
    public static McpServerOptions CreateOptions(LedgerMcpContext context)
    {
        LedgerMcpTools tools = new(context);
        return new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = ServerName,
                Version = context.Version,
            },
            ServerInstructions = Instructions,
            Handlers = new McpServerHandlers
            {
                ListToolsHandler = (_, _) => ValueTask.FromResult(tools.List()),
                CallToolHandler = (request, cancellationToken) =>
                    tools.CallAsync(request.Params, cancellationToken),
            },
        };
    }

    // await using McpServer server = McpServer.Create(new StreamServerTransport(input, output, ServerName), CreateOptions(context));
    // then await server.RunAsync(cancellationToken). McpServer is IAsyncDisposable.
    // Returns when input ends or cancellationToken is cancelled. Writes nothing else to output.
    public static async Task RunAsync(
        LedgerMcpContext context,
        Stream input,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        await using McpServer server = McpServer.Create(
            new StreamServerTransport(input, output, ServerName),
            CreateOptions(context));
        await server.RunAsync(cancellationToken);
    }
}
