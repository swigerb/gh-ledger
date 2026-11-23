namespace CopilotLedger.Storage;

// The inference spans of one trace, or of one conversation, for GetTurnBreakdownAsync.
public sealed record TurnSelector
{
    private TurnSelector(string? traceId, string? conversationId)
    {
        TraceId = traceId;
        ConversationId = conversationId;
    }

    public string? TraceId { get; }          // 32 lowercase hexadecimal characters; null when ConversationId is set
    public string? ConversationId { get; }   // null when TraceId is set

    // Throws ArgumentNullException for null, and ArgumentException unless traceId is 32 hexadecimal characters.
    // Matches the lowercase trace ID that the ledger stores.
    public static TurnSelector ForTrace(string traceId)
    {
        ArgumentNullException.ThrowIfNull(traceId);
        if (traceId.Length != 32 || !traceId.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("A trace ID is 32 hexadecimal characters.", nameof(traceId));
        }

        return new TurnSelector(traceId.ToLowerInvariant(), null);
    }

    // Throws ArgumentNullException for null, and ArgumentException for a blank conversationId.
    public static TurnSelector ForConversation(string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        return new TurnSelector(null, conversationId);
    }
}
