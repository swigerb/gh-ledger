using CopilotLedger.Core.Import;

namespace CopilotLedger.Storage.Import;

public sealed record ImportReport
{
    public required bool DryRun { get; init; }
    public required IReadOnlyList<ImportSourceReport> Sources { get; init; }       // in ImportSourceKind order
    public required IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; }     // by source, then log, then line
    public bool Complete => Sources.All(source => source.Complete);
}

// What one source's import read and decided. RecordsParsed = RecordsOutOfScope + Recorded + Duplicates + OverlapSkipped +
// AmbiguousOverlap + UnsupportedUsageShapes + CostOutOfRange. The amounts cover the Recorded requests only.
public sealed record ImportSourceReport
{
    public required ImportSourceKind Source { get; init; }
    public required long SessionsScanned { get; init; }             // every log discovered
    public required long UnreadableStoreEntries { get; init; }      // folders and files of the store that couldn't be listed
    public required long SessionsSkippedUnchanged { get; init; }    // completely imported, and unchanged since
    public required long SessionsSkippedActive { get; init; }       // still open, without IncludeActive
    public required long SessionsSkippedOutOfScope { get; init; }   // last written before Since
    public required long SessionsUnsupported { get; init; }         // any Status other than Read
    public required long SessionsAggregateOnly { get; init; }       // of those, Copilot before 1.0.103
    public required long RecordsParsed { get; init; }
    public required long RecordsOutOfScope { get; init; }           // started before Since, or of another repository
    public required long Recorded { get; init; }
    public required long Duplicates { get; init; }
    public required long OverlapSkipped { get; init; }
    public required long AmbiguousOverlap { get; init; }
    public required long SupersededUnmatched { get; init; }         // imported rows in scope that unmatched live rows superseded
    public required long MalformedLines { get; init; }
    public required long UnsupportedUsageShapes { get; init; }
    public required long CostOutOfRange { get; init; }
    public required long SkippedEvents { get; init; }
    public required long JournalPredecessors { get; init; }
    public required long Unpriced { get; init; }                    // recorded with a status other than priced or local_offload
    public required long ToolLinksRecorded { get; init; }
    public required decimal AiCredits { get; init; }
    public required decimal AiCreditsUsd { get; init; }
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
    public required decimal? ApiEquivalentUsd { get; init; }        // null when requests were recorded and none has a price
    public required long ApiEquivalentUnpricedSpans { get; init; }
    public required long ApiEquivalentEstimatedSpans { get; init; } // priced without a reported cache counter

    public bool Complete =>
        UnreadableStoreEntries == 0 &&
        SessionsUnsupported == 0 &&
        MalformedLines == 0 &&
        UnsupportedUsageShapes == 0 &&
        AmbiguousOverlap == 0 &&
        SupersededUnmatched == 0 &&
        CostOutOfRange == 0 &&
        JournalPredecessors == 0;
}
