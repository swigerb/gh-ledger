using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Breaker;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Normalization;
using CopilotLedger.Storage;

namespace CopilotLedger.Otlp.Ingestion;

public sealed class LedgerIngestor : ILedgerIngestor
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SqliteLedgerStore _store;
    private readonly ICreditCalculator _calculator;
    private readonly IGitContextResolver _gitResolver;
    private readonly CopilotSessionWorkspaceLocator _sessionLocator;
    private readonly CircuitBreakerMonitor? _monitor;

    // The caller initializes the store. Throws ArgumentNullException for null arguments.
    public LedgerIngestor(
        SqliteLedgerStore store,
        ICreditCalculator calculator,
        IGitContextResolver gitResolver,
        CopilotSessionWorkspaceLocator sessionLocator)
        : this(store, calculator, gitResolver, sessionLocator, null)
    {
    }

    // monitor null means no circuit breaker. Throws ArgumentNullException for the other null arguments.
    public LedgerIngestor(
        SqliteLedgerStore store,
        ICreditCalculator calculator,
        IGitContextResolver gitResolver,
        CopilotSessionWorkspaceLocator sessionLocator,
        CircuitBreakerMonitor? monitor)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(gitResolver);
        ArgumentNullException.ThrowIfNull(sessionLocator);
        _store = store;
        _calculator = calculator;
        _gitResolver = gitResolver;
        _sessionLocator = sessionLocator;
        _monitor = monitor;
    }

    public async Task<IngestResult> IngestAsync(
        OtlpTraceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(request);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            List<LedgerEntry> entries = [];
            int costRejected = 0;
            foreach (NormalizedGenAiSpan span in batch.InferenceSpans)
            {
                string? cwd = span.WorkingDirectory
                    ?? _sessionLocator.GetWorkingDirectory(span.ConversationId);
                NormalizedGenAiSpan attributed = span with { WorkingDirectory = cwd };

                CreditCalculationResult calculation;
                try
                {
                    calculation = _calculator.Calculate(attributed);
                    _ = PicoUsd.FromUsd(calculation.UsdCost);
                    _ = PicoUsd.FromUsd(calculation.ListUsdCost);
                    _ = PicoUsd.FromUsd(calculation.EstimatedCloudSavedUsd);
                    _ = PicoUsd.FromUsd(calculation.ApiEquivalent.UsdCost ?? 0m);
                }
                catch (Exception ex) when (
                    ex is OverflowException or ArgumentOutOfRangeException)
                {
                    costRejected++;   // "cost out of range": this span gets no ledger entry
                    continue;
                }

                GitWorkspaceContext? git = null;
                if (cwd is not null)
                {
                    try
                    {
                        git = _gitResolver.Resolve(cwd);
                    }
                    catch (Exception ex) when (
                        ex is IOException or UnauthorizedAccessException or ArgumentException)
                    {
                    }
                }

                entries.Add(new LedgerEntry(attributed, calculation, git));
            }

            int recorded = entries.Count == 0
                ? 0
                : await _store.RecordAsync(entries, cancellationToken);
            int rejectedReportedCharges = entries.Count(
                entry => entry.Calculation.ChargeSource == ChargeSource.Rejected);
            if (batch.Links.Count > 0)
            {
                await _store.RecordSpanLinksAsync(batch.Links, cancellationToken);
            }

            string[] traceIds = batch.Links
                .Select(link => link.TraceId)
                .Concat(batch.InferenceSpans.Select(span => span.TraceId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (traceIds.Length > 0)
            {
                await _store.BackfillFromAgentSpansAsync(traceIds, cancellationToken);
                if (_monitor is not null)
                {
                    await _monitor.EvaluateAsync(traceIds, cancellationToken);
                }
            }

            return new IngestResult
            {
                ReceivedSpans = batch.ReceivedSpanCount,
                RejectedSpans = batch.RejectedSpanCount + costRejected,
                FirstRejectionReason = batch.FirstRejectionReason
                    ?? (costRejected > 0 ? "cost out of range" : null),
                InferenceSpans = entries.Count,
                RecordedSpans = recorded,
                RejectedReportedCharges = rejectedReportedCharges,
            };
        }
        finally
        {
            _gate.Release();
        }
    }
}
