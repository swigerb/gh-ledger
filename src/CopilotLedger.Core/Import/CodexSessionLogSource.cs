using System.Globalization;
using System.Security.Cryptography;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;

// The rollouts Codex 0.153.0 to 0.171.0 writes: <home>/sessions/YYYY/MM/DD/rollout-<time>-<thread ID>[_<rollout ID>].jsonl,
// and the same names under <home>/archived_sessions. Usage comes only from token_usage_record lines, one per completed
// response; token_count events, compacted snapshots, and turn and thread totals are never read.
public sealed class CodexSessionLogSource : ISessionLogSource
{
    public static readonly ProducerVersion MinimumVersion = new(0, 153, 0);
    public static readonly ProducerVersion MaximumVersion = new(0, 171, 0);

    // The lines whose allowlisted metadata is read. Every other type is skipped after its "type".
    private static readonly string[] Types = ["session_meta", "turn_context", "token_usage_record"];

    private static readonly ImportEventKind[] Kinds =
        [ImportEventKind.SessionMeta, ImportEventKind.TurnContext, ImportEventKind.TokenUsageRecord];

    private static readonly PathTree MetaPaths = new(
        ("payload.id", 64),
        ("payload.session_id", 64),
        ("payload.cli_version", ProducerVersion.MaxLength),
        ("payload.cwd", 4096),
        ("payload.model_provider", 64),
        ("payload.agent_role", 256),
        ("payload.git.commit_hash", 64),
        ("payload.git.branch", 256),
        ("payload.git.repository_url", 2048));

    private static readonly PathTree TurnPaths = new(("payload.turn_id", 2048), ("payload.model", 256));

    private static readonly PathTree UsagePaths = new(
        ("timestamp", 64),
        ("payload.thread_id", 64),
        ("payload.turn_id", 2048),
        ("payload.session_id", 64),
        ("payload.root_turn_id", 2048),
        ("payload.response_id", 2048),
        ("payload.usage.input_tokens", 0),
        ("payload.usage.cached_input_tokens", 0),
        ("payload.usage.cache_write_input_tokens", 0),
        ("payload.usage.output_tokens", 0));

    // CODEX_HOME when it's set and not empty, as Codex reads it; else <user profile>/.codex.
    public static string DefaultHome => ResolveHome(
        Environment.GetEnvironmentVariable("CODEX_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public ImportSourceKind Kind => ImportSourceKind.Codex;

    public static string ResolveHome(string? codexHome, string userProfile) =>
        string.IsNullOrEmpty(codexHome) ? Path.Combine(userProfile, ".codex") : codexHome;

    // Rollouts under <home>/sessions, and under <home>/archived_sessions with LogIds that start with "archived/". A
    // .jsonl.zst rollout is listed as compressed and never opened. A folder or rollout that exists but can't be listed or
    // examined is an unreadable entry; listing stops at the first folder that can't be listed.
    public SessionLogDiscovery Discover(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        List<SessionLogLocation> logs = [];
        long unreadable = AddRollouts(logs, Path.Combine(home, "sessions"), "") +
                          AddRollouts(logs, Path.Combine(home, "archived_sessions"), "archived/");
        return new SessionLogDiscovery
        {
            Logs = [.. logs.OrderBy(log => log.LogId, StringComparer.Ordinal)],
            UnreadableEntries = unreadable,
        };
    }

    public Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (location.Source != ImportSourceKind.Codex)
        {
            throw new ArgumentException("The location isn't a Codex rollout.", nameof(location));
        }

        return Task.Run(() => Read(location, cancellationToken), cancellationToken);
    }

    // Adds root's rollouts to logs and returns how many entries couldn't be listed or examined.
    private static long AddRollouts(List<SessionLogLocation> logs, string root, string prefix)
    {
        long unreadable = 0;
        try
        {
            foreach (string path in Directory.EnumerateFiles(root, "rollout-*", SearchOption.AllDirectories))
            {
                if (ParseName(Path.GetFileName(path)) is not (string id, bool compressed))
                {
                    continue;
                }

                try
                {
                    FileInfo file = new(path);
                    logs.Add(new SessionLogLocation
                    {
                        Source = ImportSourceKind.Codex,
                        LogId = prefix + id,
                        Path = file.FullName,
                        SizeBytes = file.Length,
                        LastWriteTime = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                        IsActive = false,
                        IsCompressed = compressed,
                    });
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

        return unreadable;
    }

    // rollout-YYYY-MM-DDTHH-MM-SS-<thread ID>[_<rollout ID>].jsonl, or .jsonl.zst: the IDs in lowercase, joined by "_".
    private static (string Id, bool Compressed)? ParseName(string name)
    {
        bool compressed = name.EndsWith(".jsonl.zst", StringComparison.Ordinal);
        if (!name.StartsWith("rollout-", StringComparison.Ordinal) ||
            (!compressed && !name.EndsWith(".jsonl", StringComparison.Ordinal)))
        {
            return null;
        }

        string core = name["rollout-".Length..^(compressed ? ".jsonl.zst".Length : ".jsonl".Length)];
        if (core.Length < 20 ||
            core[19] != '-' ||
            !DateTime.TryParseExact(core[..19], "yyyy-MM-dd'T'HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return null;
        }

        string[] ids = core[20..].Split('_');
        string? thread = SessionLogs.Uuid(ids[0]);
        string? rollout = ids.Length == 2 ? SessionLogs.Uuid(ids[1]) : null;
        if (thread is null || ids.Length > 2 || (ids.Length == 2 && rollout is null))
        {
            return null;
        }

        return (rollout is null ? thread : thread + "_" + rollout, compressed);
    }

    private static SessionLogReadResult Read(SessionLogLocation location, CancellationToken cancellationToken)
    {
        if (location.IsCompressed)
        {
            return SessionLogs.NotRead(location, SessionLogStatus.Compressed, ImportEventKind.Unknown, null, 0, 0);
        }

        Rollout rollout = new(location);
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
                rollout.OnLine,
                rollout.OnOversized,
                rollout.OnUnterminated,
                hash,
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SessionLogs.NotRead(location, SessionLogStatus.Unreadable, ImportEventKind.Unknown, null, 0, 0);
        }

        return rollout.Finish(Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    // One rollout's reading state.
    private sealed class Rollout(SessionLogLocation location)
    {
        private readonly ImportDiagnosticBag _diagnostics = new(ImportSourceKind.Codex, location.LogId);
        private readonly List<ImportedUsageRecord> _records = [];
        private readonly Dictionary<string, string> _modelByTurn = new(StringComparer.Ordinal);
        private (SessionLogStatus Status, ImportEventKind Kind, string? Version, long Line, long Offset)? _stop;
        private bool _started;
        private string? _version;
        private string? _threadId;
        private string? _sessionId;
        private string? _providerName;
        private string? _agentName;
        private ImportedGitContext? _git;
        private string? _latestModel;
        private long _parsed;
        private long _unsupported;
        private long _malformed;
        private long _skipped;

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
                return kind == ImportEventKind.SessionMeta
                    ? Meta(JsonFields.Read(line, MetaPaths), number, offset)
                    : Stop(SessionLogStatus.MissingSessionStart, kind, null, number, offset);
            }

            switch (kind)
            {
                case ImportEventKind.TurnContext:
                    Turn(JsonFields.Read(line, TurnPaths));
                    break;
                case ImportEventKind.TokenUsageRecord:
                    Usage(JsonFields.Read(line, UsagePaths), number, offset);
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
                ToolCalls = [],
                Diagnostics = _diagnostics.ToList(),
                RecordsParsed = _parsed,
                UnsupportedUsageShapes = _unsupported,
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
                ContentSha256 = contentSha256,
            };
        }

        // The first session_meta is the rollout's identity (Codex's own rule); a fork's later, inherited ones are skipped.
        private bool Meta(JsonFields fields, long number, long offset)
        {
            if (fields.Duplicate is not null)
            {
                Malformed(ImportDiagnosticCode.MalformedLine, ImportEventKind.SessionMeta, fields.Duplicate, number, offset);
                return true;
            }

            _threadId = SessionLogs.Uuid(fields.String("payload.id"));
            _sessionId = SessionLogs.Uuid(fields.String("payload.session_id"));
            if (_threadId is null || _sessionId is null)
            {
                return Stop(SessionLogStatus.UnsupportedProducer, ImportEventKind.SessionMeta, null, number, offset);
            }

            string? text = fields.String("payload.cli_version");
            if (!ProducerVersion.TryParse(text, out ProducerVersion? version))
            {
                return Stop(SessionLogStatus.UnsupportedProducerVersion, ImportEventKind.SessionMeta, null, number, offset);
            }

            if (version.CompareTo(MinimumVersion) < 0 || version.CompareTo(MaximumVersion) > 0)
            {
                return Stop(SessionLogStatus.UnsupportedProducerVersion, ImportEventKind.SessionMeta, text, number, offset);
            }

            string? provider = fields.String("payload.model_provider")?.ToLowerInvariant();
            _started = true;
            _version = text;
            _providerName = provider == "lmstudio" ? "lm_studio" : provider;
            _agentName = fields.String("payload.agent_role");
            _git = new ImportedGitContext
            {
                WorkingDirectory = SessionLogs.FullPath(fields.String("payload.cwd")),
                RepositorySlug = SessionLogs.RepositorySlug(fields.String("payload.git.repository_url")),
                Branch = fields.String("payload.git.branch"),
                HeadCommitSha = SessionLogs.CommitSha(fields.String("payload.git.commit_hash")),
            };
            return true;
        }

        private void Turn(JsonFields fields)
        {
            string? model = fields.String("payload.model");
            if (fields.Duplicate is not null || model is null)
            {
                return;
            }

            _latestModel = model;
            if (fields.String("payload.turn_id") is string turnId)
            {
                _modelByTurn[turnId] = model;
            }
        }

        // A token_usage_record: one completed response. A record of another thread, inherited by a fork, is skipped; a record
        // that gives an allowlisted field twice is unsupported before its thread is read, so it can't pass for inherited.
        private void Usage(JsonFields fields, long number, long offset)
        {
            const ImportEventKind kind = ImportEventKind.TokenUsageRecord;
            if (fields.Duplicate is not null)
            {
                _parsed++;
                _unsupported++;
                _diagnostics.Add(ImportDiagnosticCode.UnsupportedUsageShape, kind, fields.Duplicate, number, offset);
                return;
            }

            string? threadId = SessionLogs.Uuid(fields.String("payload.thread_id"));
            if (threadId is not null && threadId != _threadId)
            {
                _diagnostics.Add(ImportDiagnosticCode.InheritedRecord, kind, null, number, offset);
                return;
            }

            _parsed++;
            string? turnId = fields.String("payload.turn_id");
            string? responseId = fields.String("payload.response_id");
            DateTimeOffset? time = DateTimeOffset.TryParse(
                fields.String("timestamp"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed)
                ? parsed
                : null;
            long? input = Count(fields.Integer("payload.usage.input_tokens"));
            long? cached = Count(fields.Integer("payload.usage.cached_input_tokens"));
            long? output = Count(fields.Integer("payload.usage.output_tokens"));
            bool writes = fields.IsPresent("payload.usage.cache_write_input_tokens");
            long? written = writes ? Count(fields.Integer("payload.usage.cache_write_input_tokens")) : 0;
            string? model = (turnId is not null ? _modelByTurn.GetValueOrDefault(turnId) : null) ?? _latestModel;
            string? failed =
                time is null ? "timestamp"
                : threadId is null ? "payload.thread_id"
                : SessionLogs.Uuid(fields.String("payload.session_id")) != _sessionId ? "payload.session_id"
                : responseId is null ? "payload.response_id"
                : !fields.IsObject("payload.usage") ? "payload.usage"
                : input is null ? "payload.usage.input_tokens"
                : cached is null ? "payload.usage.cached_input_tokens"
                : output is null ? "payload.usage.output_tokens"
                : written is null ? "payload.usage.cache_write_input_tokens"
                : cached + written > input ? "payload.usage.input_tokens"
                : model is null ? "turn_context.model"
                : null;
            if (failed is not null)
            {
                _unsupported++;
                _diagnostics.Add(ImportDiagnosticCode.UnsupportedUsageShape, kind, failed, number, offset);
                return;
            }

            _records.Add(new ImportedUsageRecord
            {
                Source = ImportSourceKind.Codex,
                Kind = ImportedRecordKind.Usage,
                SessionId = _sessionId!,
                RecordKey = responseId!,
                TurnKey = fields.String("payload.root_turn_id") ?? turnId,
                StartTime = time!.Value,
                EndTime = time.Value,
                RequestModel = model!,
                ProviderName = _providerName,
                BillingSource = BillingSource.Provider,
                AgentName = _agentName,
                InputTokens = input!.Value,
                OutputTokens = output!.Value,
                CacheReadInputTokens = cached!.Value,
                CacheWriteInputTokens = written!.Value,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = writes ? CacheCounterState.Reported : CacheCounterState.Absent,
                Git = _git,
                LineNumber = number,
            });
        }

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

        private static long? Count(long? value) => value is >= 0 and <= ImportLimits.MaxTokenCount ? value : null;
    }
}
