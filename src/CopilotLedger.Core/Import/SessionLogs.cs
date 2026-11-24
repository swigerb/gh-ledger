using CopilotLedger.Core.Git;

namespace CopilotLedger.Core.Import;

// What the session-log sources share.
internal static class SessionLogs
{
    // A log that isn't Read: its status diagnostic only, without records, tool calls, or counts.
    internal static SessionLogReadResult NotRead(
        SessionLogLocation location,
        SessionLogStatus status,
        ImportEventKind kind,
        string? version,
        long line,
        long byteOffset) =>
        new()
        {
            Status = status,
            ProducerVersion = version,
            Records = [],
            ToolCalls = [],
            Diagnostics =
            [
                new ImportDiagnostic
                {
                    Source = location.Source,
                    LogId = location.LogId,
                    Code = status switch
                    {
                        SessionLogStatus.MissingSessionStart => ImportDiagnosticCode.MissingSessionStart,
                        SessionLogStatus.SessionIdMismatch => ImportDiagnosticCode.SessionIdMismatch,
                        SessionLogStatus.UnsupportedProducer => ImportDiagnosticCode.UnsupportedProducer,
                        SessionLogStatus.UnsupportedProducerVersion => ImportDiagnosticCode.UnsupportedProducerVersion,
                        SessionLogStatus.AggregateOnly => ImportDiagnosticCode.AggregateOnlySession,
                        SessionLogStatus.Compressed => ImportDiagnosticCode.CompressedLog,
                        SessionLogStatus.Unreadable => ImportDiagnosticCode.UnreadableLog,
                        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
                    },
                    Event = kind,
                    Version = version,
                    Count = 1,
                    FirstLine = line,
                    FirstByteOffset = byteOffset,
                },
            ],
            RecordsParsed = 0,
            UnsupportedUsageShapes = 0,
            MalformedLines = 0,
            SkippedEvents = 0,
        };

    // An "owner/repo" slug from a recorded repository: a remote URL (credentials removed first) or an owner/repo slug; else null.
    internal static string? RepositorySlug(string? repository)
    {
        if (repository is null)
        {
            return null;
        }

        if (repository.Contains("://", StringComparison.Ordinal) || repository.Contains('@', StringComparison.Ordinal))
        {
            return GitRemoteUrl.GetRepositorySlug(GitRemoteUrl.Sanitize(repository));
        }

        string[] parts = repository.Split('/');
        return parts.Length == 2 && parts.All(part => part.Length is >= 1 and <= 100 &&
                                                      part.All(character => char.IsAsciiLetterOrDigit(character) ||
                                                                            character is '.' or '_' or '-'))
            ? repository
            : null;
    }

    // A commit SHA of 40 or 64 hex characters, lowercased; else null.
    internal static string? CommitSha(string? value) =>
        value is { Length: 40 or 64 } && value.All(char.IsAsciiHexDigit) ? value.ToLowerInvariant() : null;

    // A UUID of 36 hex digits and hyphens, in lowercase "D" form; else null.
    internal static string? Uuid(string? value) =>
        value is { Length: 36 } &&
        value.All(character => character == '-' || char.IsAsciiHexDigit(character)) &&
        Guid.TryParseExact(value, "D", out Guid id)
            ? id.ToString("D")
            : null;

    // A fully qualified path without trailing separators; else null.
    internal static string? FullPath(string? value) =>
        value is not null && Path.IsPathFullyQualified(value) ? Path.TrimEndingDirectorySeparator(value) : null;
}
