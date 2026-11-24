namespace CopilotLedger.Storage;

public sealed record LedgerFilter
{
    public static LedgerFilter All { get; } = new();
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? Repository { get; init; }
    public string? Branch { get; init; }
    public int? PullRequestNumber { get; init; }   // spans recorded with this pull request number
    public string? Agent { get; init; }            // agent name, or (none) for spans without one
    public string? ConversationId { get; init; }   // spans of this conversation (gen_ai.conversation.id, a Copilot session)
}
