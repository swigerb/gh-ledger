namespace CopilotLedger.Storage;

// What the live/import authority rules decided for one imported record.
public enum ImportResolution
{
    Recorded,          // a new effective row
    Duplicate,         // this record, or an imported row with its identity, is already in the ledger
    OverlapSkipped,    // a live row with its identity is already in the ledger
    AmbiguousOverlap,  // deferred: the conversation's live rows can't be matched to it
}
