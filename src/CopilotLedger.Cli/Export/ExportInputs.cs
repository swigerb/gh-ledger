using CopilotLedger.Core.Audit;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// The export's options, resolved before the ledger is read.
internal sealed record ExportInputs
{
    public required DateTimeOffset GeneratedAt { get; init; }   // UTC, whole milliseconds
    public required TimeZoneInfo TimeZone { get; init; }
    public string? RequestedTimeZone { get; init; }             // --time-zone as given; null for the machine's zone
    public required string Since { get; init; }                 // --since, trimmed
    public required LedgerPeriod Period { get; init; }          // --since at GeneratedAt in TimeZone
    public required LedgerFilter Filter { get; init; }          // From = Period.From, To = GeneratedAt, and the key filters
    public required bool AllRepositories { get; init; }         // no repository key: --all-repos, or outside a Git repository
    public required int MaxTimelineSpans { get; init; }
    public required bool IncludeLocalPaths { get; init; }
    public IAuditPricing? AuditPricing { get; init; }           // the rates in use for the audit findings; null when the overlay is invalid
}
