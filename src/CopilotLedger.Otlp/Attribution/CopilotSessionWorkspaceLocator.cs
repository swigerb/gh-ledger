using CopilotLedger.Core.Import;

namespace CopilotLedger.Otlp.Attribution;

public sealed class CopilotSessionWorkspaceLocator
{
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromSeconds(60);

    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _timeToLive;

    // COPILOT_HOME when it's set and not blank; otherwise Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot").
    public static string DefaultCopilotHome
    {
        get
        {
            string? configured = Environment.GetEnvironmentVariable("COPILOT_HOME");
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".copilot")
                : configured;
        }
    }

    // Throws ArgumentException when copilotHome is null or blank.
    public CopilotSessionWorkspaceLocator(
        string copilotHome,
        TimeProvider? timeProvider = null,
        TimeSpan? timeToLive = null)
    {
        if (string.IsNullOrWhiteSpace(copilotHome))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(copilotHome));
        }

        _timeToLive = timeToLive ?? DefaultTimeToLive;
        CopilotHome = copilotHome;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string CopilotHome { get; }

    // The session's working directory from <CopilotHome>/session-state/<conversationId>/workspace.yaml, or null.
    public string? GetWorkingDirectory(string? conversationId)
    {
        if (!CopilotSessionIds.IsValid(conversationId))
        {
            return null;
        }

        lock (_lock)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (_cache.TryGetValue(conversationId, out CacheEntry? entry) &&
                now < entry.ExpiresAt)
            {
                return entry.WorkingDirectory;
            }

            string? workingDirectory = ReadWorkingDirectory(conversationId);
            _cache[conversationId] = new CacheEntry(
                workingDirectory,
                now.Add(_timeToLive));
            return workingDirectory;
        }
    }

    private string? ReadWorkingDirectory(string conversationId)
    {
        string path = Path.Combine(
            CopilotHome,
            "session-state",
            conversationId,
            "workspace.yaml");
        try
        {
            foreach (string line in File.ReadLines(path))
            {
                if (!line.StartsWith("cwd:", StringComparison.Ordinal))
                {
                    continue;
                }

                string raw = line[4..].Trim();
                string value = Unquote(raw);
                return !string.IsNullOrEmpty(value) && Path.IsPathFullyQualified(value)
                    ? value
                    : null;
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    private static string Unquote(string raw)
    {
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            string value = raw[1..^1];
            string result = string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] == '\\' &&
                    index + 1 < value.Length &&
                    value[index + 1] is '\\' or '"')
                {
                    index++;
                }

                result += value[index];
            }

            return result;
        }

        if (raw.Length >= 2 && raw[0] == '\'' && raw[^1] == '\'')
        {
            return raw[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        int comment = raw.IndexOf(" #", StringComparison.Ordinal);
        return (comment >= 0 ? raw[..comment] : raw).TrimEnd();
    }

    private sealed record CacheEntry(
        string? WorkingDirectory,
        DateTimeOffset ExpiresAt);
}
