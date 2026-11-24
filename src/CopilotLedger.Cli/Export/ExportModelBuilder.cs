using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Audit;
using CopilotLedger.Storage;
using CopilotLedger.Storage.Import;

namespace CopilotLedger.Cli.Export;

// Reads everything the report shows from one snapshot, so its parts agree with each other and with the reports.
internal static class ExportModelBuilder
{
    public static async Task<ExportReportModel> BuildAsync(
        LedgerExportSnapshot snapshot,
        ExportInputs inputs,
        CancellationToken cancellationToken)
    {
        LedgerFilter filter = inputs.Filter;
        HashSet<string> roots = new(
            await snapshot.GetRepositoryRootsAsync(filter, cancellationToken),
            StringComparer.Ordinal);
        ExportFilter shown = new()
        {
            Repository = filter.Repository is null
                ? null
                : ExportRecords.Repository(filter.Repository, roots, inputs.IncludeLocalPaths),
            AllRepositories = inputs.AllRepositories,
            Branch = filter.Branch,
            PullRequest = filter.PullRequestNumber,
            Conversation = filter.ConversationId,
            Since = inputs.Since,
            Period = inputs.Period.Label,
            FromUtc = inputs.Period.From?.ToUniversalTime(),
            ToUtc = inputs.GeneratedAt,
        };
        ExportRatesProvenance provenance = await snapshot.GetRatesProvenanceAsync(filter, cancellationToken);
        IReadOnlyList<LedgerRollupRow> byChannelModel =
            await snapshot.GetRollupAsync(LedgerGrouping.ChannelModel, filter, cancellationToken);
        LedgerRollupRow totals = ReportCommands.Sum(byChannelModel);

        // Tool history is the retention before generatedAt, but starts no earlier than the earliest span link the ledger
        // keeps: the daemon deletes links, of tool calls and agent invocations alike, by its own clock, so a past
        // --generated-at can't count on the retention. A ledger that keeps no link keeps no tool history.
        DateTimeOffset retained = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        DateTimeOffset? earliestLink = await snapshot.GetEarliestLinkStartAsync(cancellationToken);
        (DateTimeOffset toolHistoryFrom, ExportToolHistoryStart toolHistoryStart) = earliestLink switch
        {
            null => (inputs.GeneratedAt, ExportToolHistoryStart.NoLinks),
            DateTimeOffset earliest when earliest > retained => (earliest, ExportToolHistoryStart.EarliestLink),
            _ => (retained, ExportToolHistoryStart.Retention),
        };
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
        AuditReport audit = await AuditAsync(snapshot, inputs, shown, cancellationToken);
        audit = audit with
        {
            Findings =
            [
                .. audit.Findings.Select(finding => finding with
                {
                    Scope = finding.Scope with { Repository = Shown(finding.Scope.Repository) },
                }),
            ],
        };
        IReadOnlyList<LedgerRollupRow> byModel = await snapshot.GetRollupAsync(LedgerGrouping.Model, filter, cancellationToken);

        // The timeline draws the latest MaxTimelineSpans requests and, separately, tool calls, in start order.
        ExportSpanRecord[] drawnSpans =
        [
            .. (await snapshot.GetLatestSpansAsync(filter, inputs.MaxTimelineSpans, cancellationToken))
                .Reverse()
                .Select(row => ExportRecords.Span(row, inputs.IncludeLocalPaths)),
        ];
        ExportToolCallRecord[] drawnToolCalls =
        [
            .. (await snapshot.GetLatestToolCallsAsync(filter, inputs.MaxTimelineSpans, cancellationToken))
                .Reverse()
                .Select(ExportRecords.ToolCall),
        ];
        IReadOnlyList<PromptChainRow> promptChains =
            await snapshot.GetTopPromptChainsAsync(filter, ExportLimits.PromptChains, cancellationToken);
        return new ExportReportModel
        {
            Header = new ExportHeader
            {
                ToolVersion = GhLedgerCli.Version,
                GeneratedAt = inputs.GeneratedAt,
                Filter = shown,
                TimeZone = inputs.TimeZone,
                RequestedTimeZone = inputs.RequestedTimeZone,
                RatesVersions = provenance.RatesVersions,
                ApiRateCards = provenance.ApiRateCards,
                Scope = ExportRecords.Scope(shown),
                MaxTimelineSpans = inputs.MaxTimelineSpans,
                IncludeLocalPaths = inputs.IncludeLocalPaths,
            },
            Summary = new ExportSummary
            {
                Totals = totals,
                PromptChains = await snapshot.GetPromptCountAsync(filter, cancellationToken),
            },
            Coverage = new ExportCoverage
            {
                Spans = totals.SpanCount,
                SpansBySource = coverage.SpansBySource,
                SupersededMatchedImportSpans = coverage.SupersededMatchedImportSpans,
                SupersededUnmatchedImportSpans = coverage.SupersededUnmatchedImportSpans,
                LedgerAmbiguousImportRecords = coverage.LedgerAmbiguousImportRecords,
                LedgerIncompleteImportLogs = coverage.LedgerIncompleteImportLogs,
                UnpricedSpans = totals.UnpricedSpanCount,
                ApiEquivalentUnpricedSpans = totals.ApiEquivalentUnpricedSpanCount,
                ApiEquivalentEstimatedSpans = totals.ApiEquivalentEstimatedSpanCount,
                CacheReportedSpans = coverage.CacheReportedSpans,
                CacheAbsentSpans = coverage.CacheAbsentSpans,
                CacheLegacyUnknownSpans = coverage.CacheLegacyUnknownSpans,
                ToolHistoryRetentionDays = LedgerImporter.ToolLinkRetention.Days,
                ToolHistoryFromUtc = toolHistoryFrom,
                ToolHistoryStart = toolHistoryStart,
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
                ImportedRequestsWithoutToolData = coverage.ImportedRequestsWithoutToolData,
                ToolCalls = await snapshot.CountToolCallsAsync(filter, cancellationToken),
                MaxTimelineSpans = inputs.MaxTimelineSpans,
                TimelineSpans = drawnSpans.Length,
                TimelineToolCalls = drawnToolCalls.Length,
            },
            AuditFindings =
            [
                .. audit.Findings
                    .Take(ExportLimits.AuditFindings)
                    .Select(finding => ExportRecords.AuditFinding(finding, audit.Thresholds, inputs.TimeZone)),
            ],
            Audit = audit,
            ByModel = byModel,
            ByAgent = await snapshot.GetRollupAsync(LedgerGrouping.Agent, filter, cancellationToken),
            ByBranch =
            [
                .. (await snapshot.GetRollupAsync(LedgerGrouping.Branch, filter, cancellationToken))
                    .Select(row => row with { Repository = row.Repository is null ? null : Shown(row.Repository) }),
            ],
            ByChannelModel = byChannelModel,
            CacheByModel = await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Model, filter, cancellationToken),
            CacheByAgent = await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Agent, filter, cancellationToken),
            PromptChains = [.. promptChains.Select(row => row with { Repository = Shown(row.Repository) })],
            BreakerTrips =
            [
                .. (await snapshot.GetBreakerTripsAsync(filter, cancellationToken)).Select(trip => trip with
                {
                    Repository = Shown(trip.Repository),
                    RepositoryRoot = inputs.IncludeLocalPaths ? trip.RepositoryRoot : null,
                }),
            ],
            Budgets =
            [
                .. (await snapshot.GetBudgetsAsync(filter, cancellationToken))
                    .Select(row => row with { Repository = Shown(row.Repository) }),
            ],
            Timeline = TimelineLayout.Build(
                drawnSpans,
                drawnToolCalls,
                [.. byModel.Select(row => row.Model ?? "(none)")],
                inputs.TimeZone),
            ChainTurns = await ChainTurnsAsync(snapshot, filter, promptChains, cancellationToken),
        };

        string Shown(string key) => ExportRecords.Repository(key, roots, inputs.IncludeLocalPaths);
    }

    // gh ledger audit of the export's window with the default thresholds, every rule, and the rates' baseline as the economy
    // model; without rates (an invalid overlay), premium_model_trivial_turn isn't evaluated and repriced impacts are null.
    private static async Task<AuditReport> AuditAsync(
        LedgerExportSnapshot snapshot,
        ExportInputs inputs,
        ExportFilter shown,
        CancellationToken cancellationToken)
    {
        AuditInput input = await snapshot.GetAuditInputAsync(inputs.Filter, inputs.GeneratedAt, cancellationToken);
        return AuditAnalyzer.Report(
            input with
            {
                Pricing = inputs.AuditPricing,
                EconomyModel = inputs.AuditPricing?.BaselineModel,
                TimeZone = inputs.TimeZone,
            },
            inputs.GeneratedAt,
            new AuditReportFilter
            {
                Repository = shown.Repository,
                Branch = shown.Branch,
                PullRequestNumber = shown.PullRequest,
                Conversation = shown.Conversation,
                Since = shown.Since,
                Period = shown.Period,
                FromUtc = shown.FromUtc,
                ToUtc = shown.ToUtc,
            },
            AuditSeverity.Info,
            null);
    }

    // The requests, in the export's window, of the first GrowthChains prompt chains, in cost order, that have at least two
    // requests: a single request shows no growth. A chain whose trace ID isn't 32 hexadecimal characters is skipped, as
    // --turns can't select it.
    private static async Task<IReadOnlyList<ExportChainTurns>> ChainTurnsAsync(
        LedgerExportSnapshot snapshot,
        LedgerFilter filter,
        IReadOnlyList<PromptChainRow> promptChains,
        CancellationToken cancellationToken)
    {
        List<ExportChainTurns> chains = [];
        for (int index = 0; index < promptChains.Count && chains.Count < ExportLimits.GrowthChains; index++)
        {
            string trace = promptChains[index].TraceId;
            if (promptChains[index].SpanCount < 2 || trace.Length != 32 || !trace.All(char.IsAsciiHexDigit))
            {
                continue;
            }

            IReadOnlyList<TurnTokenRow> turns =
                await snapshot.GetTurnBreakdownAsync(TurnSelector.ForTrace(trace), filter, cancellationToken);
            List<(string AgentSpan, string Model)> sequences = [];
            List<ExportTurnPoint> points = [];
            foreach (TurnTokenRow turn in turns.Take(ExportLimits.GrowthTurns))
            {
                (string, string) key = (turn.AgentSpanId ?? string.Empty, turn.Model);
                if (!sequences.Contains(key))
                {
                    sequences.Add(key);
                }

                points.Add(new ExportTurnPoint(
                    points.Count,
                    turn.StartTime,
                    turn.Agent,
                    turn.Model,
                    sequences.IndexOf(key),
                    turn.InputTokens));
            }

            chains.Add(new ExportChainTurns(
                index + 1,
                trace,
                promptChains[index].StartTime,
                turns.Count,
                turns.Count == 0 ? 0 : turns.Max(turn => turn.InputTokens),
                points));
        }

        return chains;
    }
}
