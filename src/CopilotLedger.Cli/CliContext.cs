using CopilotLedger.Core;
using CopilotLedger.Core.Import;
using CopilotLedger.Otlp.Attribution;
using Microsoft.AspNetCore.Hosting;

namespace CopilotLedger.Cli;

public sealed record CliContext
{
    public required TextWriter Out { get; init; }
    public required TextWriter Error { get; init; }
    public required string DataDirectory { get; init; }
    public required string CopilotHome { get; init; }
    public required string CodexHome { get; init; }                                     // import --source codex
    public required string WorkingDirectory { get; init; }
    public required TimeProvider TimeProvider { get; init; }
    public required ICliPlatform Platform { get; init; }
    public Stream? StandardInput { get; init; }            // mcp: null uses Console.OpenStandardInput()
    public Stream? StandardOutput { get; init; }           // mcp: null uses Console.OpenStandardOutput()
    public Action<IWebHostBuilder>? ConfigureWebHost { get; init; }                     // tests: builder => builder.UseTestServer()
    public Func<HttpMessageHandler>? CreateHttpHandler { get; init; }                   // rates update; tests: a fake handler; null: RatesUpdate.CreateDefaultHandler()
    public Func<HttpMessageHandler>? CreateLocalLlmHandler { get; init; }               // audit --local-llm; tests: a fake handler; null: LocalLlmAdvisor.CreateDefaultHandler()
    public TimeSpan DaemonPollInterval { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan DaemonStartTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan DaemonStopTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public string DatabasePath => Path.Combine(DataDirectory, "ledger.db");
    public string RatesOverlayPath => Path.Combine(DataDirectory, "rates.json");

    // DataDirectory: COPILOT_LEDGER_HOME when set and not blank, else LedgerPaths.DataDirectory.
    // CopilotHome: CopilotSessionWorkspaceLocator.DefaultCopilotHome. CodexHome: CodexSessionLogSource.DefaultHome.
    // WorkingDirectory: Environment.CurrentDirectory. TimeProvider: TimeProvider.System. Platform: new SystemCliPlatform().
    public static CliContext CreateDefault(TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        string? overridePath = Environment.GetEnvironmentVariable("COPILOT_LEDGER_HOME");
        return new CliContext
        {
            Out = output,
            Error = error,
            DataDirectory = string.IsNullOrWhiteSpace(overridePath)
                ? LedgerPaths.DataDirectory
                : overridePath,
            CopilotHome = CopilotSessionWorkspaceLocator.DefaultCopilotHome,
            CodexHome = CodexSessionLogSource.DefaultHome,
            WorkingDirectory = Environment.CurrentDirectory,
            TimeProvider = TimeProvider.System,
            Platform = new SystemCliPlatform(),
        };
    }
}
