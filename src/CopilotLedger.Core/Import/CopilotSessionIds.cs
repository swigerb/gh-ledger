using System.Diagnostics.CodeAnalysis;

namespace CopilotLedger.Core.Import;

public static class CopilotSessionIds
{
    // A Copilot session directory name: an ASCII letter or digit, then up to 127 ASCII letters, digits, '.', '_', or '-'.
    public static bool IsValid([NotNullWhen(true)] string? sessionId) =>
        sessionId is { Length: >= 1 and <= 128 } &&
        char.IsAsciiLetterOrDigit(sessionId[0]) &&
        sessionId.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
}
