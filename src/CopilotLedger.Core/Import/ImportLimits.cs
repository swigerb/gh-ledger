namespace CopilotLedger.Core.Import;

public static class ImportLimits
{
    public const int MaxLineBytes = 8 * 1024 * 1024;      // a longer line is discarded unread
    public const long MaxTokenCount = 1_000_000_000;      // as GenAiSpanNormalizer.MaxTokenCount
    public const long MaxDurationMilliseconds = 86_400_000;
}
