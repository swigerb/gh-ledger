using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage.Import;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Cli.Import;

// gh ledger import: records past requests from local Copilot and Codex session logs (LedgerImporter) and reports, per
// source, what it read and decided. Exit code 0 when every source's coverage is complete, 2 when the import finished with
// some coverage incomplete, and 1 for invalid usage or a fatal error. Nothing from a log reaches the output except
// validated log IDs and versions, counts, and the parsers' own field names.
internal static class ImportCommands
{
    public const string ClaudeUnsupported =
        "Claude Code import isn't supported: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it.";

    private const int Incomplete = 2;
    private const int MaxWarnings = 20;

    private static readonly JsonWriterOptions JsonOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The table's rows after the header: a label and each source's count.
    private static readonly (string Label, Func<ImportSourceReport, long> Count)[] Rows =
    [
        ("Sessions scanned", report => report.SessionsScanned),
        ("Skipped, unchanged", report => report.SessionsSkippedUnchanged),
        ("Skipped, active", report => report.SessionsSkippedActive),
        ("Skipped, out of scope", report => report.SessionsSkippedOutOfScope),
        ("Unsupported sessions", report => report.SessionsUnsupported),
        ("Records parsed", report => report.RecordsParsed),
        ("Out of scope", report => report.RecordsOutOfScope),
        ("Recorded", report => report.Recorded),
        ("Duplicate", report => report.Duplicates),
        ("Overlap-skipped", report => report.OverlapSkipped),
        ("Ambiguous-overlap", report => report.AmbiguousOverlap),
        ("Malformed lines", report => report.MalformedLines),
        ("Unsupported usage shapes", report => report.UnsupportedUsageShapes),
        ("Cost out of range", report => report.CostOutOfRange),
        ("Unpriced", report => report.Unpriced),
    ];

    public static Command Create(CliContext context)
    {
        Command command = new("import", "Record past requests from local Copilot and Codex session logs.");
        Option<string?> repository = new("--repo")
        {
            Description = "Import only requests from the Git repository at this path.",
        };
        Option<bool> all = new("--all")
        {
            Description = "Import requests from every repository.",
        };
        Option<string[]> sources = new("--source")
        {
            Description = "Session logs to read: copilot (the default) or codex. Repeat it for both.",
        };
        Option<string> since = new("--since")
        {
            Description = "Period: today, all, Nd, or yyyy-MM-dd.",
            DefaultValueFactory = _ => "all",
        };
        since.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string>() ?? string.Empty;
            if (!Period.TryParse(value, context, out _))
            {
                result.AddError(
                    $"Invalid --since value {value}. Use today, all, <N>d (1-3650), or yyyy-MM-dd.");
            }
        });
        Option<bool> dryRun = new("--dry-run")
        {
            Description = "Show what would be recorded without changing the ledger.",
        };
        Option<bool> includeActive = new("--include-active")
        {
            Description = "Also read Copilot sessions that are still open.",
        };
        Option<bool> json = new("--json")
        {
            Description = "Write JSON instead of a table.",
        };
        command.Add(repository);
        command.Add(all);
        command.Add(sources);
        command.Add(since);
        command.Add(dryRun);
        command.Add(includeActive);
        command.Add(json);
        command.Validators.Add(result =>
        {
            string? path = result.GetValue(repository);
            bool everything = result.GetValue(all);
            if (path is null && !everything)
            {
                result.AddError("Use --repo <path> or --all.");
            }
            else if (path is not null && everything)
            {
                result.AddError("Use either --repo or --all, not both.");
            }
            else if (path is not null && string.IsNullOrWhiteSpace(path))
            {
                result.AddError("--repo can't be blank.");
            }

            string[] values = result.GetValue(sources) ?? [];
            if (values.Contains("claude"))
            {
                result.AddError(ClaudeUnsupported);
            }

            if (values.Any(value => value is not ("copilot" or "codex" or "claude")))
            {
                result.AddError("--source must be copilot or codex.");
            }
        });
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
            {
                string[] values = result.GetValue(sources) ?? [];
                List<ImportSourceKind> kinds = [];
                if (values.Length == 0 || values.Contains("copilot"))
                {
                    kinds.Add(ImportSourceKind.Copilot);
                }

                if (values.Contains("codex"))
                {
                    kinds.Add(ImportSourceKind.Codex);
                }

                return RunAsync(
                    context,
                    result.GetValue(repository),
                    kinds,
                    Period.TryParse(result.GetValue(since), context, out Period period)
                        ? period
                        : throw new InvalidOperationException(),
                    result.GetValue(dryRun),
                    result.GetValue(includeActive),
                    result.GetValue(json),
                    cancellationToken);
            });
        return command;
    }

    private static async Task<int> RunAsync(
        CliContext context,
        string? repositoryPath,
        IReadOnlyList<ImportSourceKind> kinds,
        Period period,
        bool dryRun,
        bool includeActive,
        bool json,
        CancellationToken cancellationToken)
    {
        GitWorkspaceContext? git = null;
        if (repositoryPath is not null)
        {
            string path = Path.GetFullPath(repositoryPath, context.WorkingDirectory);
            git = new FileSystemGitContextResolver().Resolve(path);
            if (git is null)
            {
                context.Error.WriteLine($"{path} isn't in a Git repository.");
                return 1;
            }
        }

        RatesBase rates = RateCardFiles.LoadBase(context);
        if (rates.Warning is not null)
        {
            context.Error.WriteLine($"Warning: {rates.Warning}");
        }

        JsonRateCardCalculator calculator;
        try
        {
            calculator = RateCardFiles.Load(rates, RateCardFiles.OverlayPath(context));
        }
        catch (InvalidDataException ex)
        {
            context.Error.WriteLine($"Invalid rates overlay: {ex.Message}");
            return 1;
        }

        // A daemon of another version records live spans by its own rules, so a request it records later might not
        // supersede an imported one.
        if (DaemonFiles.IsHeld(context) &&
            DaemonFiles.TryReadState(context)?.Version is string version &&
            version != GhLedgerCli.Version)
        {
            context.Error.WriteLine(
                $"Warning: the daemon runs gh-ledger {version}; restart it so live spans follow this version's import rules.");
        }

        ImportRequest request = new()
        {
            DatabasePath = context.DatabasePath,
            Sources =
            [
                .. kinds.Select(kind => kind == ImportSourceKind.Copilot
                    ? new ImportSourceRequest(new CopilotSessionLogSource(), context.CopilotHome)
                    : new ImportSourceRequest(new CodexSessionLogSource(), context.CodexHome)),
            ],
            RepositoryRoot = git?.RepositoryRoot,
            Since = period.From,
            DryRun = dryRun,
            IncludeActive = includeActive,
        };
        ImportReport report;
        try
        {
            report = await new LedgerImporter(calculator, context.TimeProvider).RunAsync(request, cancellationToken);
        }
        catch (SqliteException ex)
        {
            context.Error.WriteLine(
                dryRun
                    ? $"Dry run stopped: SQLite error {ex.SqliteErrorCode}."
                    : $"Import stopped: SQLite error {ex.SqliteErrorCode}. Requests recorded before it are kept; run the import again.");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            context.Error.WriteLine(ex.Message);
            return 1;
        }

        if (json)
        {
            WriteJson(context, report, period, git);
        }
        else
        {
            WriteText(context, report, period, git);
        }

        return report.Complete ? 0 : Incomplete;
    }

    private static void WriteText(CliContext context, ImportReport report, Period period, GitWorkspaceContext? git)
    {
        string scope = git is null ? string.Empty : $" for {git.RepositorySlug ?? git.RepositoryRoot}";
        context.Out.WriteLine(
            $"Import from local session logs{scope} ({period.Label}{(report.DryRun ? ", dry run" : string.Empty)})");
        context.Out.WriteLine();
        List<IReadOnlyList<string>> rows =
        [
            .. Rows.Select(row =>
                (IReadOnlyList<string>)[row.Label, .. report.Sources.Select(source => LedgerFormat.Count(row.Count(source)))]),
            ["Coverage", .. report.Sources.Select(source => Coverage(source.Complete))],
        ];
        TableWriter.Write(
            context.Out,
            [new TableColumn(string.Empty, false), .. report.Sources.Select(source => new TableColumn(Name(source.Source), true))],
            rows);
        context.Out.WriteLine();

        Totals totals = Totals.Of(report.Sources);
        if (totals.Recorded == 0)
        {
            context.Out.WriteLine(report.DryRun ? "There's nothing new to record." : "Nothing new to record.");
        }
        else
        {
            string api = LedgerFormat.ApiEquivalent(
                totals.ApiEquivalentUsd,
                totals.ApiEquivalentUnpricedSpans,
                totals.ApiEquivalentEstimatedSpans);
            context.Out.WriteLine(
                $"{(report.DryRun ? "Importing would record" : "Recorded")} {Plural(totals.Recorded, "request")}: {LedgerFormat.Credits(totals.AiCredits)} AI credits ({LedgerFormat.Usd(totals.AiCreditsUsd)}), API equivalent {api}.");
            if (totals.FoundryByokUsd != 0m || totals.LocalSavedUsd != 0m)
            {
                context.Out.WriteLine(
                    $"Foundry BYOK: {LedgerFormat.Usd(totals.FoundryByokUsd)}. Local saved: {LedgerFormat.Usd(totals.LocalSavedUsd)}.");
            }

            if (api.EndsWith('*'))
            {
                context.Out.WriteLine(
                    $"{LedgerFormat.Count(totals.ApiEquivalentUnpricedSpans)} of {Plural(totals.Recorded, "request")} had no API-equivalent price; * marks a partial amount.");
            }

            if (api.StartsWith('~'))
            {
                long estimated = totals.ApiEquivalentEstimatedSpans;
                context.Out.WriteLine(
                    $"{Plural(estimated, "priced request")} reported no cache counters, so all of {(estimated == 1 ? "its" : "their")} input is priced as uncached; ~ marks an estimate.");
            }
        }

        if (report.DryRun)
        {
            context.Out.WriteLine("Dry run: nothing was written.");
        }

        if (!report.Complete)
        {
            long old = report.Sources.Sum(source => source.SessionsAggregateOnly);
            context.Out.WriteLine(
                old > 0 && report.Sources.All(OnlyOldSessionsMissing)
                    ? $"Coverage is incomplete only because {Plural(old, "session")} {(old == 1 ? "predates" : "predate")} Copilot {CopilotSessionLogSource.MinimumVersion}; use --since to leave {(old == 1 ? "it" : "them")} out."
                    : "Coverage is incomplete; see the warnings.");
        }

        foreach (ImportDiagnostic diagnostic in report.Diagnostics.Take(MaxWarnings))
        {
            context.Error.WriteLine($"Warning: {Describe(diagnostic)}");
        }

        if (report.Diagnostics.Count > MaxWarnings)
        {
            int more = report.Diagnostics.Count - MaxWarnings;
            context.Error.WriteLine($"Warning: {Plural(more, "more warning")}; run with --json to see them all.");
        }
    }

    // Every gap in the source's coverage is a session from before Copilot 1.0.103.
    private static bool OnlyOldSessionsMissing(ImportSourceReport source) =>
        source.SessionsUnsupported == source.SessionsAggregateOnly &&
        source.MalformedLines == 0 &&
        source.UnsupportedUsageShapes == 0 &&
        source.AmbiguousOverlap == 0 &&
        source.CostOutOfRange == 0 &&
        source.JournalPredecessors == 0;

    private static void WriteJson(CliContext context, ImportReport report, Period period, GitWorkspaceContext? git)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteBoolean("dryRun", report.DryRun);
        writer.WriteString("period", period.Label);
        LedgerFormat.WriteTime(writer, "from", period.From);
        LedgerFormat.WriteNullable(writer, "repository", git is null ? null : git.RepositorySlug ?? git.RepositoryRoot);
        LedgerFormat.WriteNullable(writer, "repositoryRoot", git?.RepositoryRoot);
        writer.WriteString("coverage", Coverage(report.Complete));
        writer.WriteStartArray("sources");
        foreach (ImportSourceReport source in report.Sources)
        {
            writer.WriteStartObject();
            writer.WriteString("source", ImportIds.SourceName(source.Source));
            writer.WriteString("coverage", Coverage(source.Complete));
            writer.WriteNumber("sessionsScanned", source.SessionsScanned);
            writer.WriteNumber("sessionsSkippedUnchanged", source.SessionsSkippedUnchanged);
            writer.WriteNumber("sessionsSkippedActive", source.SessionsSkippedActive);
            writer.WriteNumber("sessionsSkippedOutOfScope", source.SessionsSkippedOutOfScope);
            writer.WriteNumber("sessionsUnsupported", source.SessionsUnsupported);
            writer.WriteNumber("sessionsAggregateOnly", source.SessionsAggregateOnly);
            writer.WriteNumber("recordsParsed", source.RecordsParsed);
            writer.WriteNumber("recordsOutOfScope", source.RecordsOutOfScope);
            writer.WriteNumber("recorded", source.Recorded);
            writer.WriteNumber("duplicate", source.Duplicates);
            writer.WriteNumber("overlapSkipped", source.OverlapSkipped);
            writer.WriteNumber("ambiguousOverlap", source.AmbiguousOverlap);
            writer.WriteNumber("malformedLines", source.MalformedLines);
            writer.WriteNumber("unsupportedUsageShapes", source.UnsupportedUsageShapes);
            writer.WriteNumber("costOutOfRange", source.CostOutOfRange);
            writer.WriteNumber("journalPredecessors", source.JournalPredecessors);
            writer.WriteNumber("skippedEvents", source.SkippedEvents);
            writer.WriteNumber("unpriced", source.Unpriced);
            writer.WriteNumber("toolLinksRecorded", source.ToolLinksRecorded);
            WriteAmounts(writer, Totals.Of([source]));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        Totals totals = Totals.Of(report.Sources);
        writer.WritePropertyName("totals");
        writer.WriteStartObject();
        writer.WriteNumber("recorded", totals.Recorded);
        WriteAmounts(writer, totals);
        writer.WriteEndObject();
        writer.WriteStartArray("diagnostics");
        foreach (ImportDiagnostic diagnostic in report.Diagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("source", ImportIds.SourceName(diagnostic.Source));
            writer.WriteString("log", diagnostic.LogId);
            writer.WriteString("code", JsonNamingPolicy.CamelCase.ConvertName(diagnostic.Code.ToString()));
            writer.WriteString("event", JsonNamingPolicy.CamelCase.ConvertName(diagnostic.Event.ToString()));
            LedgerFormat.WriteNullable(writer, "field", diagnostic.Field);
            LedgerFormat.WriteNullable(writer, "version", diagnostic.Version);
            writer.WriteNumber("count", diagnostic.Count);
            LedgerFormat.WriteNullable(writer, "line", diagnostic.FirstLine > 0 ? (long?)diagnostic.FirstLine : null);
            LedgerFormat.WriteNullable(
                writer,
                "byteOffset",
                diagnostic.FirstLine > 0 ? (long?)diagnostic.FirstByteOffset : null);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteAmounts(Utf8JsonWriter writer, Totals totals)
    {
        LedgerFormat.WriteMoney(writer, "aiCredits", totals.AiCredits);
        LedgerFormat.WriteMoney(writer, "aiCreditsUsd", totals.AiCreditsUsd);
        LedgerFormat.WriteMoney(writer, "foundryByokUsd", totals.FoundryByokUsd);
        LedgerFormat.WriteMoney(writer, "localSavedUsd", totals.LocalSavedUsd);
        LedgerFormat.WriteApiEquivalent(
            writer,
            totals.ApiEquivalentUsd,
            totals.ApiEquivalentUnpricedSpans,
            totals.ApiEquivalentEstimatedSpans);
    }

    // "<source> <session|rollout> <log ID>[, line <n> (byte <offset>)]: <message>"
    private static string Describe(ImportDiagnostic diagnostic)
    {
        string log = diagnostic.Source == ImportSourceKind.Codex ? "Codex rollout" : "Copilot session";
        string line = diagnostic.FirstLine > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $", line {diagnostic.FirstLine} (byte {diagnostic.FirstByteOffset})")
            : string.Empty;
        return $"{log} {diagnostic.LogId}{line}: {Message(diagnostic)}";
    }

    private static string Message(ImportDiagnostic diagnostic)
    {
        long count = diagnostic.Count;
        bool codex = diagnostic.Source == ImportSourceKind.Codex;
        string producer = codex ? "Codex" : "Copilot";
        string supported = codex
            ? $"{CodexSessionLogSource.MinimumVersion} to {CodexSessionLogSource.MaximumVersion}"
            : $"{CopilotSessionLogSource.MinimumVersion} to {CopilotSessionLogSource.MaximumVersion}";
        return diagnostic.Code switch
        {
            ImportDiagnosticCode.MalformedLine => $"{Plural(count, "malformed line")} skipped",
            ImportDiagnosticCode.OversizedLine => $"{Plural(count, "line")} longer than 8 MiB skipped",
            ImportDiagnosticCode.UnterminatedLine => "the last line is incomplete and was skipped",
            ImportDiagnosticCode.UnsupportedUsageShape =>
                $"{Plural(count, diagnostic.Event == ImportEventKind.CompactionComplete ? "compaction record" : "usage record")} skipped: unsupported {diagnostic.Field}",
            ImportDiagnosticCode.MissingSessionStart => codex
                ? "no session_meta first; rollout skipped"
                : "no session.start first; session skipped",
            ImportDiagnosticCode.SessionIdMismatch => "session.start names another session; session skipped",
            ImportDiagnosticCode.UnsupportedProducer => "not a supported session log; skipped",
            ImportDiagnosticCode.UnsupportedProducerVersion => diagnostic.Version is null
                ? $"its {producer} version can't be read ({supported} are supported); skipped"
                : $"{producer} {diagnostic.Version} isn't supported ({supported}); skipped",
            ImportDiagnosticCode.AggregateOnlySession =>
                $"Copilot before {CopilotSessionLogSource.MinimumVersion} logs only session totals (this session: {diagnostic.Version}); {supported} are supported; skipped",
            ImportDiagnosticCode.JournalPredecessor => "earlier journal files aren't read; older requests may be missing",
            ImportDiagnosticCode.CompressedLog => "compressed rollouts (.jsonl.zst) aren't read; rollout skipped",
            ImportDiagnosticCode.UnreadableLog => "the log couldn't be read; skipped",
            ImportDiagnosticCode.UnknownSubagent => $"{Plural(count, "sub-agent request")} recorded without an agent name",
            ImportDiagnosticCode.InheritedRecord => $"{Plural(count, "usage record")} of another thread skipped",
            ImportDiagnosticCode.AmbiguousOverlap =>
                $"{Plural(count, "request")} deferred: this conversation has live spans without a call ID",
            ImportDiagnosticCode.CostOutOfRange => $"{Plural(count, "request")} skipped: cost beyond the ledger's range",
            _ => throw new ArgumentOutOfRangeException(nameof(diagnostic), diagnostic.Code, null),
        };
    }

    private static string Name(ImportSourceKind source) => source == ImportSourceKind.Codex ? "Codex" : "Copilot";

    private static string Coverage(bool complete) => complete ? "complete" : "incomplete";

    // "1 request", "2 requests", "1,000 requests".
    private static string Plural(long count, string noun) =>
        $"{LedgerFormat.Count(count)} {noun}{(count == 1 ? string.Empty : "s")}";

    // The recorded requests' amounts over sources. ApiEquivalentUsd is 0 when nothing was recorded, and null when requests
    // were recorded and none of them has an API-equivalent price.
    private sealed record Totals(
        long Recorded,
        decimal AiCredits,
        decimal AiCreditsUsd,
        decimal FoundryByokUsd,
        decimal LocalSavedUsd,
        decimal? ApiEquivalentUsd,
        long ApiEquivalentUnpricedSpans,
        long ApiEquivalentEstimatedSpans)
    {
        public static Totals Of(IReadOnlyList<ImportSourceReport> sources)
        {
            long recorded = sources.Sum(source => source.Recorded);
            List<decimal> priced = [.. sources.Where(source => source.Recorded > 0 && source.ApiEquivalentUsd is not null)
                .Select(source => source.ApiEquivalentUsd!.Value)];
            return new Totals(
                recorded,
                sources.Sum(source => source.AiCredits),
                sources.Sum(source => source.AiCreditsUsd),
                sources.Sum(source => source.FoundryByokUsd),
                sources.Sum(source => source.LocalSavedUsd),
                recorded == 0 ? 0m : priced.Count == 0 ? null : priced.Sum(),
                sources.Sum(source => source.ApiEquivalentUnpricedSpans),
                sources.Sum(source => source.ApiEquivalentEstimatedSpans));
        }
    }
}
