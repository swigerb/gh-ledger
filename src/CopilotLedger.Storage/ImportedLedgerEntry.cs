using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

// One imported model call, priced, with where it came from.
public sealed record ImportedLedgerEntry
{
    public required ImportSourceKind Source { get; init; }               // Copilot or Codex
    public required string RecordKeySha256 { get; init; }                // ImportIds.RecordKeySha256: 64 lowercase hex
    public required NormalizedGenAiSpan Span { get; init; }              // SessionImportMapper.ToSpan; ConversationId set
    public required CreditCalculationResult Calculation { get; init; }   // the current rates at Span.StartTime
    public ImportedGitContext? Git { get; init; }
    public long? ToolCallCount { get; init; }                            // the log's own count; never expanded into calls
    public IReadOnlyList<GenAiSpanLink> ToolLinks { get; init; } = [];   // the execute_tool links this call owns
}
