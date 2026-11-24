using System.Globalization;
using System.Security.Cryptography;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;

// The session logs that Copilot CLI and the GitHub Copilot app write, <home>/session-state/<session ID>/events.jsonl, as
// Copilot 1.0.103 to 1.0.114 write them. Only the allowlisted metadata of known events is read.
public sealed class CopilotSessionLogSource : ISessionLogSource
{
    public static readonly ProducerVersion MinimumVersion = new(1, 0, 103);
    public static readonly ProducerVersion MaximumVersion = new(1, 0, 114);

    // The events whose allowlisted metadata is read. Every other type is skipped after its "type".
    private static readonly string[] Types =
    [
        "session.start",
        "session.resume",
        "session.usage_record",
        "session.compaction_complete",
        "subagent.started",
        "tool.execution_start",
        "tool.execution_complete",
        "journal.link",
    ];

    private static readonly ImportEventKind[] Kinds =
    [
        ImportEventKind.SessionStart,
        ImportEventKind.SessionResume,
        ImportEventKind.UsageRecord,
        ImportEventKind.CompactionComplete,
        ImportEventKind.SubagentStarted,
        ImportEventKind.ToolExecutionStart,
        ImportEventKind.ToolExecutionComplete,
        ImportEventKind.JournalLink,
    ];

    private static readonly PathTree StartPaths = new(
        ("data.sessionId", 128),
        ("data.producer", 64),
        ("data.version", 0),
        ("data.copilotVersion", ProducerVersion.MaxLength),
        ("data.context.cwd", 4096),
        ("data.context.gitRoot", 4096),
        ("data.context.branch", 256),
        ("data.context.headCommit", 64),
        ("data.context.repository", 512));

    private static readonly PathTree ResumePaths = new(
        ("data.context.cwd", 4096),
        ("data.context.gitRoot", 4096),
        ("data.context.branch", 256),
        ("data.context.headCommit", 64),
        ("data.context.repository", 512));

    private static readonly PathTree UsagePaths = new(
        ("timestamp", 64),
        ("agentId", 2048),
        ("data.usage.model", 256),
        ("data.usage.responseModel", 256),
        ("data.usage.inputTokens", 0),
        ("data.usage.outputTokens", 0),
        ("data.usage.cacheReadTokens", 0),
        ("data.usage.cacheWriteTokens", 0),
        ("data.usage.cacheDetailsReported", 0),
        ("data.usage.isByok", 0),
        ("data.usage.provider.kind", 64),
        ("data.usage.providerType", 64),
        ("data.usage.serviceRequestId", 64),
        ("data.usage.accounting.usageId", 2048),
        ("data.usage.parentToolCallId", 2048),
        ("data.usage.duration", 0),
        ("data.usage.numToolCalls", 0),
        ("data.usage.initiator", 64));

    private static readonly PathTree CompactionPaths = new(
        ("timestamp", 64),
        ("agentId", 2048),
        ("data.success", 0),
        ("data.serviceRequestId", 64),
        ("data.compactionTokensUsed.requestCount", 0),
        ("data.compactionTokensUsed.inputTokens", 0),
        ("data.compactionTokensUsed.outputTokens", 0),
        ("data.compactionTokensUsed.cacheReadTokens", 0),
        ("data.compactionTokensUsed.cacheWriteTokens", 0),
        ("data.compactionTokensUsed.model", 256),
        ("data.compactionTokensUsed.duration", 0),
        ("data.compactionTokensUsed.provider.kind", 64));

    private static readonly PathTree SubagentPaths = new(
        ("agentId", 2048),
        ("data.agentName", 256),
        ("data.toolCallId", 2048));

    private static readonly PathTree ToolStartPaths = new(
        ("timestamp", 64),
        ("agentId", 2048),
        ("data.toolCallId", 2048),
        ("data.toolName", 256),
        ("data.parentToolCallId", 2048));

    private static readonly PathTree ToolCompletePaths = new(
        ("data.toolCallId", 2048),
        ("data.success", 0),
        ("data.shellExecution.exitCode", 0));

    public ImportSourceKind Kind => ImportSourceKind.Copilot;

    // Each <home>/session-state/<session ID>/events.jsonl whose directory name is a valid session ID. A session folder that
    // can't be entered or listed, or a session-state that exists but isn't a folder that can be listed, is an unreadable entry;
    // a session folder without events.jsonl has no log.
    public SessionLogDiscovery Discover(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        string root = Path.Combine(home, "session-state");
        List<SessionLogLocation> logs = [];
        long unreadable = 0;
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(root))
            {
                string sessionId = Path.GetFileName(directory);
                if (!CopilotSessionIds.IsValid(sessionId))
                {
                    continue;
                }

                // FileInfo.Exists is false for a file that can't be examined, too, so it can't tell a folder without a log from
                // one this user can't enter. Length throws FileNotFoundException only when there's no events.jsonl.
                try
                {
                    FileInfo events = new(Path.Combine(directory, "events.jsonl"));
                    logs.Add(new SessionLogLocation
                    {
                        Source = ImportSourceKind.Copilot,
                        LogId = sessionId,
                        Path = events.FullName,
                        SizeBytes = events.Length,
                        LastWriteTime = new DateTimeOffset(events.LastWriteTimeUtc, TimeSpan.Zero),
                        IsActive = Directory.EnumerateFiles(directory, "inuse.*.lock").Any(),
                    });
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    // No events.jsonl, or the session folder is gone: no log.
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    unreadable++;
                }
            }
        }
        catch (DirectoryNotFoundException) when (!File.Exists(root))
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            unreadable++;
        }

        return new SessionLogDiscovery
        {
            Logs = [.. logs.OrderBy(log => log.LogId, StringComparer.Ordinal)],
            UnreadableEntries = unreadable,
        };
    }

    public Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (location.Source != ImportSourceKind.Copilot)
        {
            throw new ArgumentException("The location isn't a Copilot session log.", nameof(location));
        }

        return Task.Run(() => Read(location, cancellationToken), cancellationToken);
    }

    private static SessionLogReadResult Read(SessionLogLocation location, CancellationToken cancellationToken)
    {
        Session session = new(location);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            using FileStream stream = new(
                location.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                0,
                FileOptions.SequentialScan);
            JsonlLineReader.Read(
                stream,
                location.SizeBytes,
                ImportLimits.MaxLineBytes,
                session.OnLine,
                session.OnOversized,
                session.OnUnterminated,
                hash,
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SessionLogs.NotRead(location, SessionLogStatus.Unreadable, ImportEventKind.Unknown, null, 0, 0);
        }

        return session.Finish(Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    // One log's reading state.
    private sealed class Session(SessionLogLocation location)
    {
        private readonly ImportDiagnosticBag _diagnostics = new(ImportSourceKind.Copilot, location.LogId);
        private readonly List<ImportedUsageRecord> _records = [];
        private readonly List<ImportedToolCall> _toolCalls = [];
        private (SessionLogStatus Status, ImportEventKind Kind, string? Version, long Line, long Offset)? _stop;
        private bool _started;
        private bool _journal;
        private string? _version;
        private ImportedGitContext? _context;
        private string? _turnKey;
        private long _parsed;
        private long _unsupported;
        private long _malformed;
        private long _skipped;

        // Sub-agents: their names by envelope agentId and by the tool call that started them, and their agentIds by that call.
        private readonly Dictionary<string, string> _agentNameByAgentId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _agentNameByToolCallId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _agentIdByToolCallId = new(StringComparer.Ordinal);

        // The latest usage record of each agent (context key), which owns the tool calls that follow it.
        private readonly Dictionary<string, string> _latestRecordByContext = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingTool> _pendingTools = new(StringComparer.Ordinal);

        internal bool OnLine(ReadOnlySpan<byte> line, long number, long offset)
        {
            int? type = JsonFields.ReadType(line, Types);
            if (type is null)
            {
                Malformed(ImportDiagnosticCode.MalformedLine, ImportEventKind.Unknown, null, number, offset);
                return true;
            }

            ImportEventKind kind = type < 0 ? ImportEventKind.Unknown : Kinds[type.Value];
            if (!_started)
            {
                return kind == ImportEventKind.SessionStart
                    ? Start(JsonFields.Read(line, StartPaths), number, offset)
                    : Stop(SessionLogStatus.MissingSessionStart, kind, null, number, offset);
            }

            switch (kind)
            {
                case ImportEventKind.SessionResume:
                    Resume(JsonFields.Read(line, ResumePaths), number, offset);
                    break;
                case ImportEventKind.UsageRecord:
                    Usage(JsonFields.Read(line, UsagePaths), number, offset);
                    break;
                case ImportEventKind.CompactionComplete:
                    Compaction(JsonFields.Read(line, CompactionPaths), number, offset);
                    break;
                case ImportEventKind.SubagentStarted:
                    SubagentStarted(JsonFields.Read(line, SubagentPaths));
                    break;
                case ImportEventKind.ToolExecutionStart:
                    ToolStart(JsonFields.Read(line, ToolStartPaths));
                    break;
                case ImportEventKind.ToolExecutionComplete:
                    ToolComplete(JsonFields.Read(line, ToolCompletePaths));
                    break;
                case ImportEventKind.JournalLink:
                    if (!_journal)
                    {
                        _journal = true;
                        _diagnostics.Add(ImportDiagnosticCode.JournalPredecessor, kind, null, number, offset);
                    }

                    break;
                default:
                    _skipped++;
                    break;
            }

            return true;
        }

        internal void OnOversized(long number, long offset) =>
            Malformed(ImportDiagnosticCode.OversizedLine, ImportEventKind.Unknown, null, number, offset);

        internal void OnUnterminated(long number, long offset) =>
            Malformed(ImportDiagnosticCode.UnterminatedLine, ImportEventKind.Unknown, null, number, offset);

        // contentSha256: the SHA-256 of the bytes that were read, which a Read result keeps.
        internal SessionLogReadResult Finish(string contentSha256)
        {
            if (_stop is { } stop)
            {
                return SessionLogs.NotRead(location, stop.Status, stop.Kind, stop.Version, stop.Line, stop.Offset);
            }

            if (!_started)
            {
                return SessionLogs.NotRead(
                    location,
                    SessionLogStatus.MissingSessionStart,
                    ImportEventKind.Unknown,
                    null,
                    0,
                    0);
            }

            return new SessionLogReadResult
            {
                Status = SessionLogStatus.Read,
                ProducerVersion = _version,
                Records = _records,
                ToolCalls = _toolCalls,
                Diagnostics = _diagnostics.ToList(),
                RecordsParsed = _parsed,
                UnsupportedUsageShapes = _unsupported,
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
                ContentSha256 = contentSha256,
            };
        }

        // The gate: this session's own session.start, from copilot-agent schema version 1 and Copilot 1.0.103 to 1.0.114.
        // Reading stops at a session.start that fails it, so an older session's later lines are never read.
        private bool Start(JsonFields fields, long number, long offset)
        {
            if (fields.Duplicate is not null)
            {
                Malformed(ImportDiagnosticCode.MalformedLine, ImportEventKind.SessionStart, fields.Duplicate, number, offset);
                return true;
            }

            string? sessionId = fields.String("data.sessionId");
            if (!CopilotSessionIds.IsValid(sessionId) || !string.Equals(sessionId, location.LogId, StringComparison.Ordinal))
            {
                return Stop(SessionLogStatus.SessionIdMismatch, ImportEventKind.SessionStart, null, number, offset);
            }

            if (fields.String("data.producer") != "copilot-agent" || fields.Integer("data.version") != 1)
            {
                return Stop(SessionLogStatus.UnsupportedProducer, ImportEventKind.SessionStart, null, number, offset);
            }

            string? text = fields.String("data.copilotVersion");
            if (!ProducerVersion.TryParse(text, out ProducerVersion? version))
            {
                return Stop(SessionLogStatus.UnsupportedProducerVersion, ImportEventKind.SessionStart, null, number, offset);
            }

            if (version.CompareTo(MaximumVersion) > 0)
            {
                return Stop(SessionLogStatus.UnsupportedProducerVersion, ImportEventKind.SessionStart, text, number, offset);
            }

            if (version.CompareTo(MinimumVersion) < 0)
            {
                return Stop(SessionLogStatus.AggregateOnly, ImportEventKind.SessionStart, text, number, offset);
            }

            _started = true;
            _version = text;
            _context = Context(fields);
            return true;
        }

        // A resumed session's context replaces the one before for the records that follow.
        private void Resume(JsonFields fields, long number, long offset)
        {
            if (fields.Duplicate is not null)
            {
                Malformed(ImportDiagnosticCode.MalformedLine, ImportEventKind.SessionResume, fields.Duplicate, number, offset);
            }
            else if (fields.IsObject("data.context"))
            {
                _context = Context(fields);
            }
        }

        // A session.usage_record: one model call. The first field that fails validation names the unsupported shape.
        private void Usage(JsonFields fields, long number, long offset)
        {
            const ImportEventKind kind = ImportEventKind.UsageRecord;
            _parsed++;
            if (fields.Duplicate is not null)
            {
                Unsupported(kind, fields.Duplicate, number, offset);
                return;
            }

            if (Time(fields) is not DateTimeOffset end)
            {
                Unsupported(kind, "timestamp", number, offset);
                return;
            }

            if (!fields.IsObject("data.usage"))
            {
                Unsupported(kind, "data.usage", number, offset);
                return;
            }

            string? usageId = fields.String("data.usage.accounting.usageId");
            string? model = fields.String("data.usage.model");
            string? responseModel = fields.String("data.usage.responseModel");
            string? tokens = Tokens(fields, "data.usage.", out long input, out long output, out long cacheRead, out long cacheWrite);
            bool? cacheReported = fields.Boolean("data.usage.cacheDetailsReported");
            bool? byok = fields.Boolean("data.usage.isByok");
            string? serviceRequestId = UsageIdentity.NormalizeServiceRequestId(fields.String("data.usage.serviceRequestId"));
            long duration = Duration(fields.Integer("data.usage.duration")) ?? 0;
            string? failed =
                usageId is null ? "data.usage.accounting.usageId"
                : model is null ? "data.usage.model"
                : responseModel is null && fields.IsPresent("data.usage.responseModel") ? "data.usage.responseModel"
                : tokens
                ?? (cacheReported is null ? "data.usage.cacheDetailsReported"
                : byok is null ? "data.usage.isByok"
                : byok == false && fields.String("data.usage.provider.kind") != "copilot" ? "data.usage.provider.kind"
                : byok == false && serviceRequestId is null ? "data.usage.serviceRequestId"
                : StartsBeforeTime(end, duration) ? "data.usage.duration"
                : null);
            if (failed is not null)
            {
                Unsupported(kind, failed, number, offset);
                return;
            }

            (string contextKey, string? agentName) = Attribute(
                fields.String("agentId"),
                fields.String("data.usage.parentToolCallId"),
                kind,
                number,
                offset);
            if (contextKey.Length == 0 && fields.String("data.usage.initiator") == "user")
            {
                _turnKey = usageId;
            }

            _records.Add(new ImportedUsageRecord
            {
                Source = ImportSourceKind.Copilot,
                Kind = ImportedRecordKind.Usage,
                SessionId = location.LogId,
                RecordKey = usageId!,
                TurnKey = _turnKey,
                ServiceRequestId = byok == true ? null : serviceRequestId,
                StartTime = end.AddMilliseconds(-duration),
                EndTime = end,
                RequestModel = model!,
                ResponseModel = responseModel,
                ProviderName = byok == true
                    ? fields.String("data.usage.providerType")?.ToLowerInvariant()
                    : UsageIdentity.GitHubProviderName,
                BillingSource = byok == true ? BillingSource.Provider : BillingSource.Endpoint,
                AgentName = agentName,
                InputTokens = input,
                OutputTokens = output,
                CacheReadInputTokens = cacheRead,
                CacheWriteInputTokens = cacheWrite,
                CacheReadState = cacheReported == true || cacheRead > 0 ? CacheCounterState.Reported : CacheCounterState.Absent,
                CacheWriteState = cacheReported == true || cacheWrite > 0 ? CacheCounterState.Reported : CacheCounterState.Absent,
                ToolCallCount = Count(fields.Integer("data.usage.numToolCalls")),
                Git = _context,
                LineNumber = number,
            });
            _latestRecordByContext[contextKey] = usageId!;
        }

        // A session.compaction_complete with compactionTokensUsed: the one billed call that summarized the conversation.
        // Without compactionTokensUsed, it reports no usage and is a skipped event.
        private void Compaction(JsonFields fields, long number, long offset)
        {
            const ImportEventKind kind = ImportEventKind.CompactionComplete;
            if (fields.Duplicate is null && !fields.IsPresent("data.compactionTokensUsed"))
            {
                _skipped++;
                return;
            }

            _parsed++;
            string? serviceRequestId = UsageIdentity.NormalizeServiceRequestId(fields.String("data.serviceRequestId"));
            string? model = fields.String("data.compactionTokensUsed.model");
            DateTimeOffset? end = Time(fields);
            string? failed =
                fields.Duplicate
                ?? (end is null ? "timestamp"
                : fields.Boolean("data.success") != true ? "data.success"
                : !fields.IsObject("data.compactionTokensUsed") ? "data.compactionTokensUsed"
                : fields.Integer("data.compactionTokensUsed.requestCount") != 1 ? "data.compactionTokensUsed.requestCount"
                : serviceRequestId is null ? "data.serviceRequestId"
                : model is null ? "data.compactionTokensUsed.model"
                : null);
            long input = 0;
            long output = 0;
            long cacheRead = 0;
            long cacheWrite = 0;
            long duration = Duration(fields.Integer("data.compactionTokensUsed.duration")) ?? 0;
            failed ??= Tokens(fields, "data.compactionTokensUsed.", out input, out output, out cacheRead, out cacheWrite)
                ?? (fields.String("data.compactionTokensUsed.provider.kind") != "copilot"
                    ? "data.compactionTokensUsed.provider.kind"
                    : StartsBeforeTime(end!.Value, duration) ? "data.compactionTokensUsed.duration"
                    : null);
            if (failed is not null)
            {
                Unsupported(kind, failed, number, offset);
                return;
            }

            (_, string? agentName) = Attribute(fields.String("agentId"), null, kind, number, offset);
            _records.Add(new ImportedUsageRecord
            {
                Source = ImportSourceKind.Copilot,
                Kind = ImportedRecordKind.Compaction,
                SessionId = location.LogId,
                RecordKey = serviceRequestId!,
                TurnKey = _turnKey,
                ServiceRequestId = serviceRequestId,
                StartTime = end!.Value.AddMilliseconds(-duration),
                EndTime = end.Value,
                RequestModel = model!,
                ProviderName = UsageIdentity.GitHubProviderName,
                BillingSource = BillingSource.Endpoint,
                AgentName = agentName,
                InputTokens = input,
                OutputTokens = output,
                CacheReadInputTokens = cacheRead,
                CacheWriteInputTokens = cacheWrite,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Reported,
                Git = _context,
                LineNumber = number,
            });
        }

        private void SubagentStarted(JsonFields fields)
        {
            string? agentId = fields.String("agentId");
            string? agentName = fields.String("data.agentName");
            string? toolCallId = fields.String("data.toolCallId");
            if (fields.Duplicate is null && agentId is not null && agentName is not null && toolCallId is not null)
            {
                _agentNameByAgentId[agentId] = agentName;
                _agentNameByToolCallId[toolCallId] = agentName;
                _agentIdByToolCallId[toolCallId] = agentId;
            }
        }

        // A tool call belongs to the latest usage record of its agent, whose response asked for it.
        private void ToolStart(JsonFields fields)
        {
            string? toolCallId = fields.String("data.toolCallId");
            string? toolName = fields.String("data.toolName");
            if (fields.Duplicate is not null || toolCallId is null || toolName is null || Time(fields) is not DateTimeOffset start)
            {
                return;
            }

            string? agentId = fields.String("agentId");
            string? parentToolCallId = fields.String("data.parentToolCallId");
            string contextKey = ContextKey(agentId, parentToolCallId);
            _pendingTools[toolCallId] = new PendingTool(
                _latestRecordByContext.GetValueOrDefault(contextKey),
                toolName,
                AgentName(agentId, parentToolCallId),
                start);
        }

        private void ToolComplete(JsonFields fields)
        {
            string? toolCallId = fields.String("data.toolCallId");
            if (fields.Duplicate is not null ||
                toolCallId is null ||
                fields.Boolean("data.success") is not bool success ||
                !_pendingTools.Remove(toolCallId, out PendingTool? pending) ||
                pending.Owner is null)
            {
                return;
            }

            _toolCalls.Add(new ImportedToolCall
            {
                SessionId = location.LogId,
                OwnerRecordKey = pending.Owner,
                ToolCallId = toolCallId,
                ToolName = pending.ToolName,
                AgentName = pending.AgentName,
                StartTime = pending.StartTime,
                Success = success,
                ExitCode = fields.Integer("data.shellExecution.exitCode"),
            });
        }

        // The context key and agent name of a sub-agent's event: by envelope agentId, else (Copilot 1.0.103's records) by the
        // tool call that started the sub-agent. The main agent has key "" and no name, as its live spans have none.
        private (string ContextKey, string? AgentName) Attribute(
            string? agentId,
            string? parentToolCallId,
            ImportEventKind kind,
            long number,
            long offset)
        {
            string contextKey = ContextKey(agentId, parentToolCallId);
            string? agentName = AgentName(agentId, parentToolCallId);
            if (contextKey.Length > 0 && agentName is null)
            {
                _diagnostics.Add(ImportDiagnosticCode.UnknownSubagent, kind, null, number, offset);
            }

            return (contextKey, agentName);
        }

        private string ContextKey(string? agentId, string? parentToolCallId) =>
            agentId is not null ? "agent:" + agentId
            : parentToolCallId is null ? ""
            : _agentIdByToolCallId.TryGetValue(parentToolCallId, out string? id) ? "agent:" + id
            : "tool:" + parentToolCallId;

        private string? AgentName(string? agentId, string? parentToolCallId) =>
            (agentId is not null ? _agentNameByAgentId.GetValueOrDefault(agentId) : null)
            ?? (parentToolCallId is not null ? _agentNameByToolCallId.GetValueOrDefault(parentToolCallId) : null);

        private bool Stop(SessionLogStatus status, ImportEventKind kind, string? version, long number, long offset)
        {
            _stop = (status, kind, version, number, offset);
            return false;
        }

        private void Malformed(ImportDiagnosticCode code, ImportEventKind kind, string? field, long number, long offset)
        {
            _malformed++;
            _diagnostics.Add(code, kind, field, number, offset);
        }

        private void Unsupported(ImportEventKind kind, string field, long number, long offset)
        {
            _unsupported++;
            _diagnostics.Add(ImportDiagnosticCode.UnsupportedUsageShape, kind, field, number, offset);
        }

        // The four token counts under prefix, or the first invalid field: each from 0 to ImportLimits.MaxTokenCount, and
        // cache reads plus cache writes no more than the input, which includes them.
        private static string? Tokens(
            JsonFields fields,
            string prefix,
            out long input,
            out long output,
            out long cacheRead,
            out long cacheWrite)
        {
            input = Count(fields.Integer(prefix + "inputTokens")) ?? -1;
            output = Count(fields.Integer(prefix + "outputTokens")) ?? -1;
            cacheRead = Count(fields.Integer(prefix + "cacheReadTokens")) ?? -1;
            cacheWrite = Count(fields.Integer(prefix + "cacheWriteTokens")) ?? -1;
            return input < 0 ? prefix + "inputTokens"
                : output < 0 ? prefix + "outputTokens"
                : cacheRead < 0 ? prefix + "cacheReadTokens"
                : cacheWrite < 0 ? prefix + "cacheWriteTokens"
                : cacheRead + cacheWrite > input ? prefix + "inputTokens"
                : null;
        }

        private static long? Count(long? value) => value is >= 0 and <= ImportLimits.MaxTokenCount ? value : null;

        private static long? Duration(long? value) => value is >= 0 and <= ImportLimits.MaxDurationMilliseconds ? value : null;

        // A call that ended at end and lasted duration milliseconds would have started before the first instant.
        private static bool StartsBeforeTime(DateTimeOffset end, long duration) =>
            end - DateTimeOffset.MinValue < TimeSpan.FromMilliseconds(duration);

        // The envelope timestamp, in UTC.
        private static DateTimeOffset? Time(JsonFields fields) =>
            DateTimeOffset.TryParse(
                fields.String("timestamp"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset time)
                ? time
                : null;

        private static ImportedGitContext Context(JsonFields fields) => new()
        {
            WorkingDirectory = SessionLogs.FullPath(fields.String("data.context.cwd")),
            RepositoryRoot = SessionLogs.FullPath(fields.String("data.context.gitRoot")),
            RepositorySlug = SessionLogs.RepositorySlug(fields.String("data.context.repository")),
            Branch = fields.String("data.context.branch"),
            HeadCommitSha = SessionLogs.CommitSha(fields.String("data.context.headCommit")),
        };

        private sealed record PendingTool(string? Owner, string ToolName, string? AgentName, DateTimeOffset StartTime);
    }
}
