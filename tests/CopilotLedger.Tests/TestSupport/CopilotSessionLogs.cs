using System.Globalization;
using System.Text.Json.Nodes;

namespace CopilotLedger.Tests.TestSupport;

// A synthetic Copilot session log (events.jsonl) with the event shapes Copilot 1.0.103 to 1.0.114 write, as the sanitized
// captures in the design show them, and made-up values. Content-bearing fields hold Canary, which must never reach the
// ledger, a diagnostic, or any output.
public sealed class CopilotSessionLogs
{
    public const string Canary = "CANARY-5e1f0c-never-store-me";

    private readonly List<string> _lines = [];
    private DateTimeOffset _time;
    private int _sequence;

    // Starts the log with session.start, at start (default 2026-11-20 12:00:00 UTC).
    public CopilotSessionLogs(
        string sessionId,
        string version = "1.0.110",
        DateTimeOffset? start = null,
        JsonObject? context = null,
        string producer = "copilot-agent",
        int schemaVersion = 1)
    {
        SessionId = sessionId;
        _time = start ?? new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        Event(
            "session.start",
            new JsonObject
            {
                ["sessionId"] = sessionId,
                ["version"] = schemaVersion,
                ["producer"] = producer,
                ["copilotVersion"] = version,
                ["startTime"] = Stamp(_time),
                ["selectedModel"] = "claude-haiku-4.5",
                ["context"] = context ?? new JsonObject { ["cwd"] = DefaultWorkingDirectory },
                ["alreadyInUse"] = false,
            });
    }

    public static string DefaultWorkingDirectory { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "work"));

    public string SessionId { get; }

    // The time of the last event; each event is one second after the one before.
    public DateTimeOffset Time => _time;

    public IReadOnlyList<string> Lines => _lines;

    // {"type": type, "data": data, "agentId": agentId (when given), "id": …, "timestamp": …, "parentId": …}, one second
    // after the event before, or at the given time.
    public CopilotSessionLogs Event(string type, JsonObject data, string? agentId = null, DateTimeOffset? at = null)
    {
        _time = at ?? (_lines.Count == 0 ? _time : _time.AddSeconds(1));
        JsonObject line = new() { ["type"] = type, ["data"] = data };
        if (agentId is not null)
        {
            line["agentId"] = agentId;
        }

        line["id"] = Guid(++_sequence);
        line["timestamp"] = Stamp(_time);
        line["parentId"] = _sequence == 1 ? null : Guid(_sequence - 1);
        _lines.Add(line.ToJsonString());
        return this;
    }

    // A line as given, such as a malformed one.
    public CopilotSessionLogs Raw(string line)
    {
        _lines.Add(line);
        return this;
    }

    // A user message: content that's never read.
    public CopilotSessionLogs UserMessage() =>
        Event("user.message", new JsonObject { ["content"] = Canary, ["transformedContent"] = Canary });

    // The log's bytes: each line followed by "\n".
    public string Text() => string.Concat(_lines.Select(line => line + "\n"));

    // Writes <copilotHome>/session-state/<SessionId>/events.jsonl, with an inuse.4242.lock file when active, and returns the
    // events.jsonl path.
    public string Write(string copilotHome, bool active = false)
    {
        string directory = Path.Combine(copilotHome, "session-state", SessionId);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "events.jsonl");
        File.WriteAllText(path, Text());
        if (active)
        {
            File.WriteAllText(Path.Combine(directory, "inuse.4242.lock"), "4242");
        }

        FileRetry.ReadAllText(path);
        return path;
    }

    public static string Stamp(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    // A deterministic UUID for envelope IDs, which the parser never reads.
    private static string Guid(int sequence) =>
        string.Create(CultureInfo.InvariantCulture, $"00000000-0000-4000-8000-{sequence:x12}");
}
