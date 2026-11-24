using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage.Import;

// gh ledger import: reads each source's session logs, prices their records with the rates in use as of each record's start
// time, and records them through the live/import authority rules (SqliteLedgerStore.RecordImportedAsync), in batches. A dry
// run decides the same way on a read-only snapshot (LedgerSnapshot) and writes nothing.
public sealed class LedgerImporter
{
    // The daemon prunes span_links older than this; older tool calls aren't imported.
    public static readonly TimeSpan ToolLinkRetention = TimeSpan.FromDays(7);

    private readonly ICreditCalculator _calculator;
    private readonly TimeProvider _timeProvider;

    public LedgerImporter(ICreditCalculator calculator, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        _calculator = calculator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Throws ArgumentException for an invalid request, InvalidOperationException for a ledger newer than this gh-ledger,
    // OperationCanceledException, and the store's exceptions. Batches committed before an exception stay recorded; running
    // the import again resolves them as duplicates.
    public async Task<ImportReport> RunAsync(ImportRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        DateTimeOffset linkCutoff = _timeProvider.GetUtcNow() - ToolLinkRetention;
        SqliteLedgerStore? store = null;
        LedgerSnapshot? snapshot = null;
        if (request.DryRun)
        {
            snapshot = await LedgerSnapshot.OpenAsync(request.DatabasePath, cancellationToken);
        }
        else
        {
            store = new SqliteLedgerStore(request.DatabasePath, _timeProvider);
            await store.InitializeAsync(cancellationToken);
        }

        try
        {
            List<ImportSourceReport> reports = [];
            List<ImportDiagnostic> diagnostics = [];
            foreach (ImportSourceRequest source in request.Sources.OrderBy(source => source.Source.Kind))
            {
                Tally tally = new(source.Source.Kind);
                SessionLogDiscovery discovery = source.Source.Discover(source.Home);
                tally.UnreadableStoreEntries = discovery.UnreadableEntries;
                if (discovery.UnreadableEntries > 0)
                {
                    diagnostics.Add(new ImportDiagnostic
                    {
                        Source = source.Source.Kind,
                        LogId = string.Empty,
                        Code = ImportDiagnosticCode.UnreadableStore,
                        Count = discovery.UnreadableEntries,
                    });
                }

                foreach (SessionLogLocation log in discovery.Logs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ImportLogAsync(request, source.Source, log, store, snapshot, linkCutoff, tally, diagnostics, cancellationToken);
                }

                reports.Add(tally.ToReport());
            }

            return new ImportReport { DryRun = request.DryRun, Sources = reports, Diagnostics = diagnostics };
        }
        finally
        {
            if (snapshot is not null)
            {
                await snapshot.DisposeAsync();
            }
        }
    }

    private async Task ImportLogAsync(
        ImportRequest request,
        ISessionLogSource source,
        SessionLogLocation log,
        SqliteLedgerStore? store,
        LedgerSnapshot? snapshot,
        DateTimeOffset linkCutoff,
        Tally tally,
        List<ImportDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        tally.SessionsScanned++;
        if (log.IsActive && !request.IncludeActive)
        {
            tally.SessionsSkippedActive++;
            return;
        }

        if (request.Since is DateTimeOffset since && log.LastWriteTime < since)
        {
            tally.SessionsSkippedOutOfScope++;
            return;
        }

        // Size and last-write time only hint that a log is unchanged: it's skipped when the SHA-256 of its content matches too.
        ImportSessionCheckpoint? checkpoint = store is not null
            ? await store.GetImportSessionAsync(log.Source, log.LogId, cancellationToken)
            : await snapshot!.GetImportSessionAsync(log.Source, log.LogId, cancellationToken);
        if (checkpoint is { Complete: true } &&
            checkpoint.LogSizeBytes == log.SizeBytes &&
            checkpoint.LogLastWriteTime.ToUnixTimeMilliseconds() == log.LastWriteTime.ToUnixTimeMilliseconds() &&
            await SessionLogHash.ComputeAsync(log, cancellationToken) == checkpoint.LogSha256)
        {
            tally.SessionsSkippedUnchanged++;
            return;
        }

        SessionLogReadResult result = await source.ReadAsync(log, cancellationToken);
        diagnostics.AddRange(result.Diagnostics);
        if (result.Status != SessionLogStatus.Read)
        {
            tally.SessionsUnsupported++;
            if (result.Status == SessionLogStatus.AggregateOnly)
            {
                tally.SessionsAggregateOnly++;
            }

            return;
        }

        long journal = result.Diagnostics.Count(item => item.Code == ImportDiagnosticCode.JournalPredecessor);
        tally.RecordsParsed += result.RecordsParsed;
        tally.MalformedLines += result.MalformedLines;
        tally.UnsupportedUsageShapes += result.UnsupportedUsageShapes;
        tally.SkippedEvents += result.SkippedEvents;
        tally.JournalPredecessors += journal;

        ILookup<string, ImportedToolCall> toolCalls = result.ToolCalls
            .Where(call => call.StartTime >= linkCutoff)
            .ToLookup(call => call.OwnerRecordKey, StringComparer.Ordinal);
        List<ImportedLedgerEntry> entries = [];
        long outOfScope = 0;
        long costOutOfRange = 0;
        foreach (ImportedUsageRecord record in result.Records)
        {
            if (IsOutOfScope(record, request))
            {
                outOfScope++;
                continue;
            }

            NormalizedGenAiSpan span = SessionImportMapper.ToSpan(record);
            CreditCalculationResult calculation;
            try
            {
                calculation = _calculator.Calculate(span);
                _ = PicoUsd.FromUsd(calculation.UsdCost);
                _ = PicoUsd.FromUsd(calculation.EstimatedCloudSavedUsd);
                _ = PicoUsd.FromUsd(calculation.ApiEquivalent.UsdCost ?? 0m);
            }
            catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
            {
                costOutOfRange++;
                continue;
            }

            entries.Add(new ImportedLedgerEntry
            {
                Source = record.Source,
                RecordKeySha256 = ImportIds.RecordKeySha256(record.Source, record.Kind, record.RecordKey),
                Span = span,
                Calculation = calculation,
                Git = record.Git,
                ToolCallCount = record.ToolCallCount,
                ToolLinks = record.Kind == ImportedRecordKind.Usage
                    ? [.. toolCalls[record.RecordKey].Select(call => SessionImportMapper.ToToolLink(call, record))]
                    : [],
            });
        }

        tally.RecordsOutOfScope += outOfScope;
        tally.CostOutOfRange += costOutOfRange;
        long recorded = 0;
        long duplicates = 0;
        long overlap = 0;
        long ambiguous = 0;
        for (int start = 0; start < entries.Count; start += request.BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<ImportedLedgerEntry> batch = entries.GetRange(start, Math.Min(request.BatchSize, entries.Count - start));
            ImportRecordResult resolved = store is not null
                ? await store.RecordImportedAsync(batch, null, cancellationToken)
                : await snapshot!.EvaluateAsync(batch, cancellationToken);
            if (store is not null)
            {
                request.BatchCommitted?.Invoke(batch.Count);
            }

            tally.ToolLinksRecorded += resolved.ToolLinksRecorded;
            for (int index = 0; index < batch.Count; index++)
            {
                switch (resolved.Resolutions[index])
                {
                    case ImportResolution.Recorded:
                        recorded++;
                        tally.AddRecorded(batch[index]);
                        break;
                    case ImportResolution.Duplicate:
                        duplicates++;
                        break;
                    case ImportResolution.OverlapSkipped:
                        overlap++;
                        break;
                    default:
                        ambiguous++;
                        break;
                }
            }
        }

        tally.Recorded += recorded;
        tally.Duplicates += duplicates;
        tally.OverlapSkipped += overlap;
        tally.AmbiguousOverlap += ambiguous;
        foreach ((ImportDiagnosticCode code, long count) in new[]
                 {
                     (ImportDiagnosticCode.AmbiguousOverlap, ambiguous),
                     (ImportDiagnosticCode.CostOutOfRange, costOutOfRange),
                 })
        {
            if (count > 0)
            {
                diagnostics.Add(new ImportDiagnostic
                {
                    Source = log.Source,
                    LogId = log.LogId,
                    Code = code,
                    Count = count,
                });
            }
        }

        // The checkpoint follows the log's last batch. A log is complete when every one of its records was resolved; a crash
        // before the checkpoint only makes the next run read the log again, and its records resolve as duplicates.
        if (store is not null)
        {
            await store.RecordImportedAsync(
                [],
                new ImportSessionCheckpoint
                {
                    Source = log.Source,
                    LogId = log.LogId,
                    LogSizeBytes = log.SizeBytes,
                    LogLastWriteTime = log.LastWriteTime,
                    LogSha256 = result.ContentSha256 ??
                                throw new InvalidOperationException("A log that was read has no content hash."),
                    Complete = result.MalformedLines == 0 &&
                               result.UnsupportedUsageShapes == 0 &&
                               journal == 0 &&
                               outOfScope == 0 &&
                               costOutOfRange == 0 &&
                               ambiguous == 0,
                    Recorded = recorded,
                    Duplicates = duplicates,
                    OverlapSkipped = overlap,
                    AmbiguousOverlap = ambiguous,
                },
                cancellationToken);
        }
    }

    // Out of scope: started before Since, or, with RepositoryRoot, recorded in another repository: by its Git root when the
    // log recorded one, else by its working directory (the root or below it).
    private static bool IsOutOfScope(ImportedUsageRecord record, ImportRequest request)
    {
        if (request.Since is DateTimeOffset since && record.StartTime < since)
        {
            return true;
        }

        if (request.RepositoryRoot is not string root)
        {
            return false;
        }

        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (record.Git?.RepositoryRoot is string recorded)
        {
            return !string.Equals(recorded, root, comparison);
        }

        string? directory = record.Git?.WorkingDirectory;
        return directory is null ||
               !(string.Equals(directory, root, comparison) ||
                 directory.StartsWith(root + Path.DirectorySeparatorChar, comparison));
    }

    private static void Validate(ImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.DatabasePath, nameof(request));
        if (request.BatchSize is < 1 or > 10_000)
        {
            throw new ArgumentException("BatchSize must be 1 to 10,000.", nameof(request));
        }

        if (request.Sources.Any(source => source.Source.Kind == ImportSourceKind.Claude) ||
            request.Sources.Select(source => source.Source.Kind).Distinct().Count() != request.Sources.Count)
        {
            throw new ArgumentException("Sources must be Copilot or Codex, at most one of each.", nameof(request));
        }

        if (request.RepositoryRoot is string root &&
            (!Path.IsPathFullyQualified(root) || Path.TrimEndingDirectorySeparator(root) != root))
        {
            throw new ArgumentException("RepositoryRoot must be a full path without a trailing separator.", nameof(request));
        }
    }

    // One source's counts and amounts as they're added up.
    private sealed class Tally(ImportSourceKind source)
    {
        private decimal _apiEquivalentUsd;
        private bool _apiEquivalentPriced;

        public long SessionsScanned { get; set; }
        public long UnreadableStoreEntries { get; set; }
        public long SessionsSkippedUnchanged { get; set; }
        public long SessionsSkippedActive { get; set; }
        public long SessionsSkippedOutOfScope { get; set; }
        public long SessionsUnsupported { get; set; }
        public long SessionsAggregateOnly { get; set; }
        public long RecordsParsed { get; set; }
        public long RecordsOutOfScope { get; set; }
        public long Recorded { get; set; }
        public long Duplicates { get; set; }
        public long OverlapSkipped { get; set; }
        public long AmbiguousOverlap { get; set; }
        public long MalformedLines { get; set; }
        public long UnsupportedUsageShapes { get; set; }
        public long CostOutOfRange { get; set; }
        public long SkippedEvents { get; set; }
        public long JournalPredecessors { get; set; }
        public long ToolLinksRecorded { get; set; }
        private long Unpriced { get; set; }
        private decimal AiCredits { get; set; }
        private decimal AiCreditsUsd { get; set; }
        private decimal FoundryByokUsd { get; set; }
        private decimal LocalSavedUsd { get; set; }
        private long ApiEquivalentUnpricedSpans { get; set; }
        private long ApiEquivalentEstimatedSpans { get; set; }

        public void AddRecorded(ImportedLedgerEntry entry)
        {
            CreditCalculationResult calculation = entry.Calculation;
            if (calculation.Status is not (PricingStatus.Priced or PricingStatus.LocalOffload))
            {
                Unpriced++;
            }

            AiCredits += calculation.AiCredits;
            AiCreditsUsd += calculation.Channel == BillingChannel.GitHubAiCredits ? calculation.UsdCost : 0m;
            FoundryByokUsd += calculation.Channel == BillingChannel.FoundryByok ? calculation.UsdCost : 0m;
            LocalSavedUsd += calculation.EstimatedCloudSavedUsd;
            if (calculation.ApiEquivalent.UsdCost is decimal usd)
            {
                _apiEquivalentPriced = true;
                _apiEquivalentUsd += usd;
                if (!CacheMetrics.IsReported(entry.Span.CacheReadState, entry.Span.CacheWriteState))
                {
                    ApiEquivalentEstimatedSpans++;
                }
            }
            else
            {
                ApiEquivalentUnpricedSpans++;
            }
        }

        public ImportSourceReport ToReport() => new()
        {
            Source = source,
            SessionsScanned = SessionsScanned,
            UnreadableStoreEntries = UnreadableStoreEntries,
            SessionsSkippedUnchanged = SessionsSkippedUnchanged,
            SessionsSkippedActive = SessionsSkippedActive,
            SessionsSkippedOutOfScope = SessionsSkippedOutOfScope,
            SessionsUnsupported = SessionsUnsupported,
            SessionsAggregateOnly = SessionsAggregateOnly,
            RecordsParsed = RecordsParsed,
            RecordsOutOfScope = RecordsOutOfScope,
            Recorded = Recorded,
            Duplicates = Duplicates,
            OverlapSkipped = OverlapSkipped,
            AmbiguousOverlap = AmbiguousOverlap,
            MalformedLines = MalformedLines,
            UnsupportedUsageShapes = UnsupportedUsageShapes,
            CostOutOfRange = CostOutOfRange,
            SkippedEvents = SkippedEvents,
            JournalPredecessors = JournalPredecessors,
            Unpriced = Unpriced,
            ToolLinksRecorded = ToolLinksRecorded,
            AiCredits = AiCredits,
            AiCreditsUsd = AiCreditsUsd,
            FoundryByokUsd = FoundryByokUsd,
            LocalSavedUsd = LocalSavedUsd,
            ApiEquivalentUsd = Recorded == 0 ? 0m : _apiEquivalentPriced ? _apiEquivalentUsd : null,
            ApiEquivalentUnpricedSpans = ApiEquivalentUnpricedSpans,
            ApiEquivalentEstimatedSpans = ApiEquivalentEstimatedSpans,
        };
    }
}
