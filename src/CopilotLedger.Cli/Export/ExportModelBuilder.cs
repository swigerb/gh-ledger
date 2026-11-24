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
        LedgerRollupRow totals = ReportCommands.Sum(
            await snapshot.GetRollupAsync(LedgerGrouping.ChannelModel, filter, cancellationToken));
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
        };
    }
}
