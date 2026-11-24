using System.Globalization;
using System.Text.Json;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// The export's JSON: indented with "\n" line breaks and the default encoder, which escapes <, >, and & so the JSON can sit
// inside a script element. Property order is part of the contract.
internal static class ExportJson
{
    public static readonly JsonWriterOptions Options = new() { Indented = true, NewLine = "\n" };

    // The report's ledger-data block: what the script needs, and what a reader of the page may want to check.
    public static byte[] LedgerData(ExportReportModel model)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, Options))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
            LedgerFormat.WriteTime(writer, "generatedAtUtc", model.Header.GeneratedAt);
            writer.WriteString("toolVersion", model.Header.ToolVersion);
            WriteFilter(writer, model.Header.Filter);
            WriteTimeZone(writer, model);
            ExportTimeline timeline = model.Timeline;
            if (timeline.IsEmpty)
            {
                writer.WriteNull("timeline");
            }
            else
            {
                writer.WriteStartObject("timeline");
                LedgerFormat.WriteTime(writer, "fromUtc", DateTimeOffset.FromUnixTimeMilliseconds(timeline.FromUnixMs));
                LedgerFormat.WriteTime(writer, "toUtc", DateTimeOffset.FromUnixTimeMilliseconds(timeline.ToUnixMs));
                writer.WriteNumber("width", TimelineLayout.Width);
                writer.WriteStartArray("ticks");
                foreach (ExportAxisTick tick in timeline.Ticks)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("x");
                    writer.WriteRawValue(Number(tick.X));
                    writer.WriteNumber("level", tick.Level);
                    writer.WriteString("label", tick.Label);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            WriteCoverage(writer, model.Coverage);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    // "filter": the selection, with the shown repository key.
    public static void WriteFilter(Utf8JsonWriter writer, ExportFilter filter)
    {
        writer.WriteStartObject("filter");
        LedgerFormat.WriteNullable(writer, "repo", filter.Repository);
        writer.WriteBoolean("allRepositories", filter.AllRepositories);
        LedgerFormat.WriteNullable(writer, "branch", filter.Branch);
        LedgerFormat.WriteNullable(writer, "pr", filter.PullRequest);
        LedgerFormat.WriteNullable(writer, "conversation", filter.Conversation);
        writer.WriteString("since", filter.Since);
        writer.WriteString("period", filter.Period);
        LedgerFormat.WriteTime(writer, "fromUtc", filter.FromUtc);
        LedgerFormat.WriteTime(writer, "toUtc", filter.ToUtc);
        writer.WriteEndObject();
    }

    // "timeZone": the requested and resolved zone, the night hours, and the timeline's nights in UTC.
    public static void WriteTimeZone(Utf8JsonWriter writer, ExportReportModel model)
    {
        writer.WriteStartObject("timeZone");
        LedgerFormat.WriteNullable(writer, "requested", model.Header.RequestedTimeZone);
        writer.WriteString("id", model.Header.TimeZone.Id);
        writer.WriteString("nightStartLocal", ExportTime.NightStart.ToString("HH:mm", CultureInfo.InvariantCulture));
        writer.WriteString("nightEndLocal", ExportTime.NightEnd.ToString("HH:mm", CultureInfo.InvariantCulture));
        writer.WriteStartArray("nightBandsUtc");
        foreach (UtcBand band in model.Timeline.NightBands)
        {
            writer.WriteStartObject();
            LedgerFormat.WriteTime(writer, "fromUtc", band.FromUtc);
            LedgerFormat.WriteTime(writer, "toUtc", band.ToUtc);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    // "coverage": requests, tool history, and the timeline, each with its own completeness.
    public static void WriteCoverage(Utf8JsonWriter writer, ExportCoverage coverage)
    {
        writer.WriteStartObject("coverage");
        writer.WriteBoolean("requestsComplete", coverage.RequestsComplete);
        writer.WriteStartArray("warnings");
        foreach (string warning in coverage.Warnings)
        {
            writer.WriteStringValue(warning);
        }

        writer.WriteEndArray();
        writer.WriteNumber("spans", coverage.Spans);
        writer.WriteStartArray("spansBySource");
        foreach (ExportSourceCount source in coverage.SpansBySource)
        {
            writer.WriteStartObject();
            writer.WriteString("source", source.Source);
            writer.WriteNumber("spans", source.Spans);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteNumber("supersededMatchedImportSpans", coverage.SupersededMatchedImportSpans);
        writer.WriteNumber("supersededUnmatchedImportSpans", coverage.SupersededUnmatchedImportSpans);
        writer.WriteNumber("ledgerAmbiguousImportRecords", coverage.LedgerAmbiguousImportRecords);
        writer.WriteNumber("ledgerIncompleteImportLogs", coverage.LedgerIncompleteImportLogs);
        writer.WriteNumber("unpricedSpans", coverage.UnpricedSpans);
        writer.WriteNumber("apiEquivalentUnpricedSpans", coverage.ApiEquivalentUnpricedSpans);
        writer.WriteNumber("apiEquivalentEstimatedSpans", coverage.ApiEquivalentEstimatedSpans);
        writer.WriteNumber("cacheReportedSpans", coverage.CacheReportedSpans);
        writer.WriteNumber("cacheAbsentSpans", coverage.CacheAbsentSpans);
        writer.WriteNumber("cacheLegacyUnknownSpans", coverage.CacheLegacyUnknownSpans);
        writer.WriteStartObject("toolHistory");
        writer.WriteBoolean("complete", coverage.ToolHistoryComplete);
        writer.WriteNumber("retentionDays", coverage.ToolHistoryRetentionDays);
        LedgerFormat.WriteTime(writer, "retainedFromUtc", coverage.ToolHistoryFromUtc);
        writer.WriteNumber("spansBeforeRetention", coverage.SpansBeforeToolHistory);
        writer.WriteNumber("importedToolCallsWithoutDetail", coverage.ImportedToolCallsWithoutDetail);
        writer.WriteEndObject();
        writer.WriteStartObject("timeline");
        writer.WriteBoolean("truncated", coverage.TimelineTruncated);
        writer.WriteNumber("maxSpans", coverage.MaxTimelineSpans);
        writer.WriteNumber("spans", coverage.Spans);
        writer.WriteNumber("spansDrawn", coverage.TimelineSpans);
        writer.WriteNumber("toolCalls", coverage.ToolCalls);
        writer.WriteNumber("toolCallsDrawn", coverage.TimelineToolCalls);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    // A coordinate with at most three decimal places, invariant.
    public static string Number(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
