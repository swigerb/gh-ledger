namespace CopilotLedger.Core.Import;

// The session logs that Copilot CLI and the GitHub Copilot app write, <home>/session-state/<session ID>/events.jsonl, as
// Copilot 1.0.103 to 1.0.114 write them. Only the allowlisted metadata of known events is read.
public sealed class CopilotSessionLogSource : ISessionLogSource
{
    public static readonly ProducerVersion MinimumVersion = new(1, 0, 103);
    public static readonly ProducerVersion MaximumVersion = new(1, 0, 114);

    // The events whose allowlisted metadata is read. Every other type is skipped after its "type".
    private static readonly string[] Types = ["session.start", "journal.link"];

    private static readonly ImportEventKind[] Kinds = [ImportEventKind.SessionStart, ImportEventKind.JournalLink];

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

    public ImportSourceKind Kind => ImportSourceKind.Copilot;

    // Each <home>/session-state/<session ID>/events.jsonl whose directory name is a valid session ID.
    public IReadOnlyList<SessionLogLocation> Discover(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        List<SessionLogLocation> logs = [];
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(Path.Combine(home, "session-state")))
            {
                string sessionId = Path.GetFileName(directory);
                if (!CopilotSessionIds.IsValid(sessionId))
                {
                    continue;
                }

                try
                {
                    FileInfo events = new(Path.Combine(directory, "events.jsonl"));
                    if (!events.Exists)
                    {
                        continue;
                    }

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
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return [.. logs.OrderBy(log => log.LogId, StringComparer.Ordinal)];
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
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SessionLogs.NotRead(location, SessionLogStatus.Unreadable, ImportEventKind.Unknown, null, 0, 0);
        }

        return session.Finish();
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
                return kind == ImportEventKind.SessionStart
                    ? Start(JsonFields.Read(line, StartPaths), number, offset)
                    : Stop(SessionLogStatus.MissingSessionStart, kind, null, number, offset);
            }

            switch (kind)
            {
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

        internal SessionLogReadResult Finish()
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
                RecordsParsed = 0,
                UnsupportedUsageShapes = 0,
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
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
            return true;
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
    }
}
