namespace CopilotLedger.Storage;

public sealed record ImportRecordResult
{
    public required IReadOnlyList<ImportResolution> Resolutions { get; init; }   // one per entry, in order
    public required int ToolLinksRecorded { get; init; }
}
