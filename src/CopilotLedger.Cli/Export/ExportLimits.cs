namespace CopilotLedger.Cli.Export;

// gh ledger export's fixed limits and defaults.
internal static class ExportLimits
{
    public const int SchemaVersion = 1;                 // of manifest.json, every telemetry file, and ledger-data
    public const string DefaultOut = "copilot-ledger-report.html";
    public const int DefaultMaxTimelineSpans = 20_000;
    public const int MaxTimelineSpans = 100_000;
    public const int PromptChains = 100;
    public const int GrowthChains = 10;
    public const int GrowthTurns = 500;                 // points drawn per chain
    public const int ChartRows = 40;                    // bars per cost or cache chart
    public const int AuditFindings = 100;               // audit findings the report shows; the bundle has them all
    public const int PageSize = 1_000;                  // rows per query when streaming the bundle
}
