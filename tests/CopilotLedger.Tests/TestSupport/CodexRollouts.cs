using System.Globalization;
using System.Text.Json.Nodes;

namespace CopilotLedger.Tests.TestSupport;

// A synthetic Codex rollout with the line shapes of Codex 0.153.0 to 0.171.0 (codex-rs at rust-v0.171.0) and made-up
// values. Content-bearing fields hold CopilotSessionLogs.Canary.
public sealed class CodexRollouts
{
    private readonly List<string> _lines = [];
    private DateTimeOffset _time;

    // Starts the rollout with session_meta.
    public CodexRollouts(
        string threadId,
        string? sessionId = null,
        string version = "0.171.0",
        string? modelProvider = "openai",
        string? agentRole = null,
        JsonObject? git = null,
        string? cwd = null,
        DateTimeOffset? start = null)
    {
        ThreadId = threadId;
        SessionId = sessionId ?? threadId;
        _time = start ?? new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        JsonObject payload = new()
        {
            ["session_id"] = SessionId,
            ["id"] = threadId,
            ["timestamp"] = Stamp(_time),
            ["cwd"] = cwd ?? CopilotSessionLogs.DefaultWorkingDirectory,
            ["originator"] = "codex_cli_rs",
            ["cli_version"] = version,
            ["source"] = "cli",
        };
        if (agentRole is not null)
        {
            payload["agent_role"] = agentRole;
        }

        payload["model_provider"] = modelProvider;
        payload["base_instructions"] = new JsonObject { ["text"] = CopilotSessionLogs.Canary };
        if (git is not null)
        {
            payload["git"] = git;
        }

        Line("session_meta", payload);
    }

    public string ThreadId { get; }

    public string SessionId { get; }

    // The time of the last line; each line is one second after the one before.
    public DateTimeOffset Time => _time;

    // {"timestamp": …, "type": type, "payload": payload}, one second after the line before.
    public CodexRollouts Line(string type, JsonObject payload)
    {
        _time = _lines.Count == 0 ? _time : _time.AddSeconds(1);
        _lines.Add(new JsonObject { ["timestamp"] = Stamp(_time), ["type"] = type, ["payload"] = payload }.ToJsonString());
        return this;
    }

    public CodexRollouts Raw(string line)
    {
        _lines.Add(line);
        return this;
    }

    public CodexRollouts TurnContext(string? turnId, string model)
    {
        JsonObject payload = new();
        if (turnId is not null)
        {
            payload["turn_id"] = turnId;
        }

        payload["cwd"] = CopilotSessionLogs.DefaultWorkingDirectory;
        payload["approval_policy"] = "never";
        payload["sandbox_policy"] = new JsonObject { ["type"] = "danger-full-access" };
        payload["model"] = model;
        payload["summary"] = "auto";
        payload["user_instructions"] = CopilotSessionLogs.Canary;
        return Line("turn_context", payload);
    }

    public CodexRollouts UserMessage() =>
        Line(
                "response_item",
                new JsonObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = new JsonArray(new JsonObject { ["type"] = "input_text", ["text"] = CopilotSessionLogs.Canary }),
                })
            .Line(
                "event_msg",
                new JsonObject { ["type"] = "user_message", ["message"] = CopilotSessionLogs.Canary, ["kind"] = "plain" });

    // An event_msg token_count with cumulative and last totals, which import never reads.
    public CodexRollouts TokenCount(long input, long cached, long output) =>
        Line(
            "event_msg",
            new JsonObject
            {
                ["type"] = "token_count",
                ["info"] = new JsonObject
                {
                    ["total_token_usage"] = Usage(input, cached, 0, output),
                    ["last_token_usage"] = Usage(input, cached, 0, output),
                    ["model_context_window"] = 272_000,
                },
                ["rate_limits"] = null,
            });

    // A token_usage_record of this thread (or threadId), for one completed response. change can alter the payload.
    public CodexRollouts Usage(
        string? responseId,
        string turnId,
        long input = 1_000,
        long cached = 0,
        long output = 100,
        long? cacheWrite = 0,
        string? rootTurnId = null,
        string? threadId = null,
        Action<JsonObject>? change = null)
    {
        JsonObject payload = new()
        {
            ["thread_id"] = threadId ?? ThreadId,
            ["turn_id"] = turnId,
            ["session_id"] = SessionId,
            ["root_turn_id"] = rootTurnId ?? turnId,
            ["response_id"] = responseId,
            ["usage"] = Usage(input, cached, cacheWrite, output),
            ["turn_token_usage"] = Usage(input * 10, cached, cacheWrite, output * 10),
            ["thread_token_usage"] = Usage(input * 100, cached, cacheWrite, output * 100),
        };
        change?.Invoke(payload);
        return Line("token_usage_record", payload);
    }

    // The rollout's bytes: each line followed by "\n".
    public string Text() => string.Concat(_lines.Select(line => line + "\n"));

    // Writes <codexHome>/sessions/2026/11/20/rollout-2026-11-20T12-00-00-<ThreadId>.jsonl, or the same name directly under
    // archived_sessions, with .zst appended when compressed, and returns its path.
    public string Write(string codexHome, bool archived = false, bool compressed = false, string? rolloutId = null)
    {
        string directory = archived
            ? Path.Combine(codexHome, "archived_sessions")
            : Path.Combine(codexHome, "sessions", "2026", "11", "20");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(
            directory,
            $"rollout-2026-11-20T12-00-00-{ThreadId}{(rolloutId is null ? "" : "_" + rolloutId)}.jsonl{(compressed ? ".zst" : "")}");
        File.WriteAllText(path, Text());
        FileRetry.ReadAllText(path);
        return path;
    }

    public static string Stamp(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static JsonObject Usage(long input, long cached, long? cacheWrite, long output)
    {
        JsonObject usage = new() { ["input_tokens"] = input, ["cached_input_tokens"] = cached };
        if (cacheWrite is long writes)
        {
            usage["cache_write_input_tokens"] = writes;
        }

        usage["output_tokens"] = output;
        usage["reasoning_output_tokens"] = 0;
        usage["total_tokens"] = input + output;
        return usage;
    }
}
