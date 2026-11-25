using System.Globalization;
using System.Text.Json.Nodes;

namespace CopilotLedger.Tests.TestSupport;

// A synthetic Copilot session log (events.jsonl) with the event shapes Copilot 1.0.103 to 1.0.115 write, as the sanitized
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

    public string LastId => Guid(_sequence);

    // The time of the last event; each event is one second after the one before.
    public DateTimeOffset Time => _time;

    public IReadOnlyList<string> Lines => _lines;

    // {"type": type, "data": data, "agentId": agentId (when given), "id": …, "timestamp": …, "parentId": …}, one second
    // after the event before, or at the given time.
    public CopilotSessionLogs Event(
        string type,
        JsonObject data,
        string? agentId = null,
        DateTimeOffset? at = null,
        string? parentId = null)
    {
        _time = at ?? (_lines.Count == 0 ? _time : _time.AddSeconds(1));
        JsonObject line = new() { ["type"] = type, ["data"] = data };
        if (agentId is not null)
        {
            line["agentId"] = agentId;
        }

        line["id"] = Guid(++_sequence);
        line["timestamp"] = Stamp(_time);
        line["parentId"] = parentId ?? (_sequence == 1 ? null : Guid(_sequence - 1));
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

    public CopilotSessionLogs AssistantMessage(string apiCallId, string? parentId = null) =>
        Event(
            "assistant.message",
            new JsonObject
            {
                ["messageId"] = "<id>",
                ["apiCallId"] = apiCallId,
                ["content"] = Canary,
                ["reasoningText"] = Canary,
                ["reasoningOpaque"] = Canary,
                ["encryptedContent"] = Canary,
                ["toolRequests"] =
                    new JsonArray(
                        new JsonObject
                        {
                            ["toolCallId"] = "<id>",
                            ["name"] = Canary,
                            ["arguments"] = new JsonObject { ["command"] = Canary },
                        }),
                ["reasoningBlocks"] = new JsonArray(Canary),
            },
            parentId: parentId);

    // A session.start or session.resume context.
    public static JsonObject Context(
        string cwd,
        string? gitRoot = null,
        string? branch = null,
        string? repository = null,
        string? headCommit = null)
    {
        JsonObject context = new() { ["cwd"] = cwd };
        if (gitRoot is not null)
        {
            context["gitRoot"] = gitRoot;
            context["branch"] = branch;
            context["headCommit"] = headCommit;
            context["baseCommit"] = headCommit;
            context["repository"] = repository;
            context["repositoryHost"] = "github.com";
            context["hostType"] = "github";
        }

        return context;
    }

    public CopilotSessionLogs Resume(JsonObject? context) =>
        Event(
            "session.resume",
            context is null
                ? new JsonObject { ["resumeTime"] = Stamp(_time), ["eventCount"] = _lines.Count }
                : new JsonObject { ["resumeTime"] = Stamp(_time), ["eventCount"] = _lines.Count, ["context"] = context });

    // A session.usage_record with the shape Copilot 1.0.114 writes; with responseModel null, as 1.0.103 writes it. A BYOK
    // record has providerType and no provider or service request ID. change can alter data.usage before it's written.
    public CopilotSessionLogs Usage(
        string usageId,
        string? serviceRequestId,
        string model = "claude-haiku-4.5",
        string? responseModel = null,
        long input = 1_000,
        long output = 100,
        long cacheRead = 0,
        long cacheWrite = 0,
        bool cacheDetailsReported = true,
        string initiator = "user",
        long duration = 1_000,
        long numToolCalls = 0,
        string? agentId = null,
        string? parentToolCallId = null,
        string? providerType = null,
        long? totalNanoAiu = null,
        Action<JsonObject>? change = null)
    {
        JsonObject usage = new() { ["model"] = model };
        if (responseModel is not null)
        {
            usage["responseModel"] = responseModel;
        }

        if (providerType is null)
        {
            usage["provider"] = new JsonObject { ["id"] = "copilot", ["kind"] = "copilot", ["label"] = "GitHub Copilot" };
        }

        usage["modelDisplayName"] = model;
        usage["inputTokens"] = input;
        usage["outputTokens"] = output;
        usage["cacheReadTokens"] = cacheRead;
        usage["cacheWriteTokens"] = cacheWrite;
        usage["reasoningTokens"] = 0;
        usage["cost"] = JsonNode.Parse("0.33");
        usage["duration"] = duration;
        usage["initiator"] = initiator;
        usage["interactionType"] = "conversation-agent";
        usage["isByok"] = providerType is not null;
        usage["apiEndpoint"] = "/v1/messages";
        usage["apiCallId"] = usageId;
        if (providerType is null)
        {
            usage["providerCallId"] = "chatcmpl-" + Canary;
            usage["serviceRequestId"] = serviceRequestId;
        }
        else
        {
            usage["byokKind"] = "custom";
            usage["modelProvider"] = providerType;
            usage["providerType"] = providerType;
        }

        if (totalNanoAiu is not null)
        {
            usage["copilotUsage"] = new JsonObject
            {
                ["totalNanoAiu"] = totalNanoAiu.Value,
                ["tokenDetails"] =
                    new JsonArray(new JsonObject { ["tokenType"] = "input", ["tokenCount"] = input }),
            };
        }

        usage["accounting"] = new JsonObject
        {
            ["sourceSessionId"] = SessionId,
            ["sequence"] = _lines.Count,
            ["usageId"] = usageId,
        };
        if (parentToolCallId is not null)
        {
            usage["parentToolCallId"] = parentToolCallId;
        }

        usage["cacheDetailsReported"] = cacheDetailsReported;
        usage["numToolCalls"] = numToolCalls;
        usage["toolCounts"] = new JsonObject { [Canary] = numToolCalls };
        usage["finishReason"] = "tool_calls";
        change?.Invoke(usage);
        return Event("session.usage_record", new JsonObject { ["usage"] = usage }, agentId);
    }

    public CopilotSessionLogs Usage115(
        string usageId,
        string serviceRequestId,
        long? totalNanoAiu,
        string model = "claude-haiku-4.5",
        string? responseModel = null,
        long input = 1_000,
        long output = 100,
        long cacheRead = 0,
        long cacheWrite = 0,
        long duration = 1_000,
        string initiator = "user",
        string? agentId = null,
        Action<JsonObject>? change = null)
    {
        JsonObject usage = new()
        {
            ["model"] = model,
            ["provider"] = new JsonObject
            {
                ["id"] = "copilot",
                ["kind"] = "copilot",
                ["label"] = "GitHub Copilot",
            },
            ["modelDisplayName"] = model,
            ["inputTokens"] = input,
            ["outputTokens"] = output,
            ["cacheReadTokens"] = cacheRead,
            ["cacheWriteTokens"] = cacheWrite,
            ["reasoningTokens"] = 0,
            ["duration"] = duration,
            ["initiator"] = initiator,
            ["serviceRequestId"] = serviceRequestId,
            ["accounting"] =
                new JsonObject
                {
                    ["sourceSessionId"] = SessionId,
                    ["sequence"] = _lines.Count,
                    ["usageId"] = usageId,
                },
        };
        if (responseModel is not null)
        {
            usage["responseModel"] = responseModel;
        }

        if (totalNanoAiu is not null)
        {
            usage["copilotUsage"] = new JsonObject
            {
                ["totalNanoAiu"] = totalNanoAiu.Value,
                ["tokenDetails"] = new JsonArray(),
            };
        }

        change?.Invoke(usage);
        return Event(
            "session.usage_record",
            new JsonObject { ["usage"] = usage, ["apiCallIdFromUsageId"] = true },
            agentId);
    }

    // A session.compaction_complete with the shape Copilot 1.0.114 writes: its usage in compactionTokensUsed, and content
    // in summaryContent, checkpointPath, and the request IDs that match nothing. change can alter data before it's written.
    public CopilotSessionLogs Compaction(
        string? serviceRequestId,
        string model = "claude-haiku-4.5",
        long input = 112_223,
        long output = 18,
        long cacheRead = 18_967,
        long cacheWrite = 93_251,
        long duration = 891,
        string? agentId = null,
        bool tokensUsed = true,
        long? totalNanoAiu = 11_855_545_000,
        Action<JsonObject>? change = null)
    {
        JsonObject data = new()
        {
            ["success"] = true,
            ["outcome"] = "applied",
            ["compactionAttemptId"] = "c0000000-0000-4000-8000-000000000001",
            ["preCompactionMessagesLength"] = 34,
            ["summaryContent"] = Canary,
            ["trigger"] = "threshold",
            ["execution"] = "background",
            ["preCompactionTokens"] = 117_765,
            ["postCompactionTokens"] = 25_805,
            ["clientRequestId"] = Canary,
            ["attemptMetrics"] = new JsonObject { ["durationMs"] = 2_640, ["deadlinePolicy"] = Canary },
        };
        if (tokensUsed)
        {
            data["compactionTokensUsed"] = new JsonObject
            {
                ["requestCount"] = 1,
                ["aiCreditsStatus"] = "complete",
                ["provider"] = new JsonObject { ["id"] = "copilot", ["kind"] = "copilot", ["label"] = "GitHub Copilot" },
                ["modelDisplayName"] = Canary,
                ["inputTokens"] = input,
                ["outputTokens"] = output,
                ["cacheReadTokens"] = cacheRead,
                ["cacheWriteTokens"] = cacheWrite,
                ["duration"] = duration,
                ["model"] = model,
            };
            if (totalNanoAiu is not null)
            {
                data["compactionTokensUsed"]!["copilotUsage"] = new JsonObject
                {
                    ["totalNanoAiu"] = totalNanoAiu.Value,
                };
            }
        }

        data["requestId"] = Canary;
        data["serviceRequestId"] = serviceRequestId;
        data["checkpointNumber"] = 1;
        data["checkpointPath"] = Path.Combine(Canary, "checkpoint.md");
        change?.Invoke(data);
        return Event("session.compaction_complete", data, agentId);
    }

    public CopilotSessionLogs SubagentStarted(string agentId, string agentName, string toolCallId) =>
        Event(
            "subagent.started",
            new JsonObject
            {
                ["toolCallId"] = toolCallId,
                ["agentName"] = agentName,
                ["agentDisplayName"] = Canary,
                ["agentDescription"] = Canary,
                ["model"] = "gpt-6-luna",
                ["agentType"] = agentName,
            },
            agentId);

    public CopilotSessionLogs ToolStart(
        string toolCallId,
        string toolName,
        string? agentId = null,
        string? parentToolCallId = null,
        string? parentId = null,
        string? mcpServerName = null,
        string? mcpToolName = null)
    {
        JsonObject data = new()
        {
            ["toolCallId"] = toolCallId,
            ["toolName"] = toolName,
            ["arguments"] = new JsonObject { ["command"] = Canary },
            ["toolTitle"] = Canary,
        };
        if (parentToolCallId is not null)
        {
            data["parentToolCallId"] = parentToolCallId;
        }

        if (mcpServerName is not null)
        {
            data["mcpServerName"] = mcpServerName;
        }

        if (mcpToolName is not null)
        {
            data["mcpToolName"] = mcpToolName;
        }

        return Event("tool.execution_start", data, agentId, parentId: parentId);
    }

    public CopilotSessionLogs ToolComplete(
        string toolCallId,
        bool success,
        long? exitCode = null,
        string? agentId = null,
        string? parentId = null)
    {
        JsonObject data = new()
        {
            ["toolCallId"] = toolCallId,
            ["success"] = success,
            ["result"] = new JsonObject { ["content"] = Canary },
        };
        if (!success)
        {
            data["error"] = new JsonObject { ["code"] = "failure", ["message"] = Canary };
        }

        if (exitCode is long code)
        {
            data["shellExecution"] = new JsonObject { ["exitCode"] = code };
        }

        return Event("tool.execution_complete", data, agentId, parentId: parentId);
    }

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
