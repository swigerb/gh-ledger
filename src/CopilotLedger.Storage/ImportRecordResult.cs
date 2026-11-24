namespace CopilotLedger.Storage;

public sealed record ImportRecordResult
{
    public required IReadOnlyList<ImportResolution> Resolutions { get; init; }   // one per entry, in order
    public required int ToolLinksRecorded { get; init; }
    public int Repriced { get; init; }
    public int LiveRepriced { get; init; }
    public int ToolLinksRelinked { get; init; }
    public int ToolLinksRemoved { get; init; }
}
