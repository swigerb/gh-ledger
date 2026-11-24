using CopilotLedger.Cli.Reporting;
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
        DateTimeOffset toolHistoryFrom = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
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
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
            },
            AuditFindings = [],
            ByModel = await snapshot.GetRollupAsync(LedgerGrouping.Model, filter, cancellationToken),
            ByAgent = await snapshot.GetRollupAsync(LedgerGrouping.Agent, filter, cancellationToken),
            ByBranch =
            [
                .. (await snapshot.GetRollupAsync(LedgerGrouping.Branch, filter, cancellationToken))
                    .Select(row => row with { Repository = row.Repository is null ? null : Shown(row.Repository) }),
            ],
            ByChannelModel = byChannelModel,
            CacheByModel = await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Model, filter, cancellationToken),
            CacheByAgent = await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Agent, filter, cancellationToken),
            PromptChains =
            [
                .. (await snapshot.GetTopPromptChainsAsync(filter, ExportLimits.PromptChains, cancellationToken))
                    .Select(row => row with { Repository = Shown(row.Repository) }),
            ],
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
        };

        string Shown(string key) => ExportRecords.Repository(key, roots, inputs.IncludeLocalPaths);
    }
}
