using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Cli.Receipts;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// The export's JSON: indented, with "\n" line breaks. Property order is part of the contract.
internal static class ExportJson
{
    // ledger-data: the default encoder escapes <, >, and &, so the block can't end its script element.
    public static readonly JsonWriterOptions Options = new() { Indented = true, NewLine = "\n" };

    // The bundle's files, escaped only as JSON requires, as the reports' JSON is: values read as written, so generatedAtUtc
    // can be pasted into --generated-at.
    public static readonly JsonWriterOptions FileOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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
                writer.WritePropertyName("minWidth");
                writer.WriteRawValue(Number(timeline.MinWidth));
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

    // manifest.json: the replay inputs, provenance, privacy, the other entries' hashes, and the coverage.
    public static byte[] Manifest(ExportReportModel model, IReadOnlyList<ExportFileHash> files) => Document(writer =>
    {
        ExportHeader header = model.Header;
        writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
        writer.WriteString("tool", "gh-ledger");
        writer.WriteString("toolVersion", header.ToolVersion);
        LedgerFormat.WriteTime(writer, "generatedAtUtc", header.GeneratedAt);
        writer.WriteStartArray("ratesVersions");
        foreach (string version in header.RatesVersions)
        {
            writer.WriteStringValue(version);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("apiRateCards");
        foreach (ExportApiRateCard card in header.ApiRateCards)
        {
            writer.WriteStartObject();
            writer.WriteString("ratesVersion", card.RatesVersion);
            writer.WriteString("sha256", card.Sha256);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteFilter(writer, header.Filter);
        WriteTimeZone(writer, model);
        writer.WriteStartObject("options");
        writer.WriteNumber("maxTimelineSpans", header.MaxTimelineSpans);
        writer.WriteBoolean("includeLocalPaths", header.IncludeLocalPaths);
        writer.WriteEndObject();
        writer.WriteStartObject("privacy");
        writer.WriteBoolean("contentFree", true);
        writer.WriteBoolean("localPathsIncluded", header.IncludeLocalPaths);
        writer.WriteStartArray("excluded");
        foreach (string excluded in new[] { "prompts", "responses", "tool arguments", "tool results", "file contents", "remote URLs" })
        {
            writer.WriteStringValue(excluded);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteStartArray("files");
        foreach (ExportFileHash file in files)
        {
            writer.WriteStartObject();
            writer.WriteString("path", file.Path);
            writer.WriteNumber("bytes", file.Bytes);
            writer.WriteString("sha256", file.Sha256);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteCoverage(writer, model.Coverage);
    });

    // telemetry/summary.json: the totals and every table of the report, as the reports' --json rows.
    public static byte[] Summary(ExportReportModel model) => Document(writer =>
    {
        writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
        writer.WriteStartObject("totals");
        WriteRollupFields(writer, model.Summary.Totals);
        writer.WriteNumber("promptChains", model.Summary.PromptChains);
        writer.WriteEndObject();
        WriteRollups(writer, "byModel", model.ByModel, row => writer.WriteString("model", row.Model));
        WriteRollups(writer, "byAgent", model.ByAgent, row => writer.WriteString("agent", row.Agent));
        WriteRollups(writer, "byBranch", model.ByBranch, row =>
        {
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
        });
        WriteRollups(writer, "byChannelModel", model.ByChannelModel, row =>
        {
            writer.WriteString("channel", ReceiptMarkdown.ChannelId(row.Channel!.Value));
            writer.WriteString("model", row.Model);
        });
        WriteCaches(writer, "cacheByModel", model.CacheByModel, row => writer.WriteString("model", row.Model));
        WriteCaches(writer, "cacheByAgent", model.CacheByAgent, row => writer.WriteString("agent", row.Agent));
        writer.WriteStartArray("breakerTrips");
        foreach (BreakerTrip trip in model.BreakerTrips)
        {
            writer.WriteStartObject();
            writer.WriteString("rule", BreakerRuleNames.ToName(trip.Rule));
            writer.WriteString("traceId", trip.TraceId);
            LedgerFormat.WriteNullable(writer, "conversationId", trip.ConversationId);
            writer.WriteString("repository", trip.Repository);
            if (model.Header.IncludeLocalPaths)
            {
                LedgerFormat.WriteNullable(writer, "repositoryRoot", trip.RepositoryRoot);
            }

            writer.WriteString("branch", trip.Branch);
            LedgerFormat.WriteTime(writer, "trippedAt", trip.TrippedAt);
            writer.WriteBoolean("open", trip.ResetAt is null);
            LedgerFormat.WriteTime(writer, "resetAt", trip.ResetAt);
            WriteNullableMoney(writer, "windowCostUsd", trip.WindowCostUsd);
            LedgerFormat.WriteNullable(writer, "windowMinutes", trip.WindowMinutes);
            LedgerFormat.WriteNullable(writer, "failureCount", trip.FailureCount);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("budgets");
        foreach (BranchBudgetRow budget in model.Budgets)
        {
            writer.WriteStartObject();
            writer.WriteString("repository", budget.Repository);
            writer.WriteString("branch", budget.Branch);
            LedgerFormat.WriteMoney(writer, "budgetAiCredits", budget.BudgetAiCredits);
            LedgerFormat.WriteMoney(writer, "spentAiCredits", budget.SpentAiCredits);
            writer.WriteBoolean("exceeded", budget.Exceeded);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteCoverage(writer, model.Coverage);
    });

    // telemetry/prompt-chains.json: the top chains as top-prompts --json rows, and how many chains there are.
    public static byte[] PromptChains(ExportReportModel model) => Document(writer =>
    {
        writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
        writer.WriteNumber("limit", ExportLimits.PromptChains);
        writer.WriteNumber("total", model.Summary.PromptChains);
        writer.WriteStartArray("chains");
        for (int index = 0; index < model.PromptChains.Count; index++)
        {
            LedgerFormat.WritePromptChain(writer, model.PromptChains[index], index + 1);
        }

        writer.WriteEndArray();
    });

    // telemetry/spans.json: every selected request, read and written a page at a time.
    public static async Task WriteSpansAsync(
        Stream stream,
        LedgerExportSnapshot snapshot,
        ExportInputs inputs,
        CancellationToken cancellationToken)
    {
        await using (Utf8JsonWriter writer = new(stream, FileOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
            writer.WriteStartArray("spans");
            ExportCursor? after = null;
            IReadOnlyList<ExportSpanRow> page;
            do
            {
                page = await snapshot.GetSpansAsync(inputs.Filter, after, ExportLimits.PageSize, cancellationToken);
                foreach (ExportSpanRow row in page)
                {
                    WriteSpan(writer, ExportRecords.Span(row, inputs.IncludeLocalPaths), inputs.IncludeLocalPaths);
                }

                await writer.FlushAsync(cancellationToken);
                after = page.Count == 0
                    ? null
                    : new ExportCursor(page[^1].StartTime.ToUnixTimeMilliseconds(), page[^1].TraceId, page[^1].SpanId);
            }
            while (page.Count == ExportLimits.PageSize);

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        await stream.WriteAsync(NewLine, cancellationToken);
    }

    // telemetry/tool-calls.json: every tool call of the selected prompt chains, a page at a time.
    public static async Task WriteToolCallsAsync(
        Stream stream,
        LedgerExportSnapshot snapshot,
        ExportInputs inputs,
        CancellationToken cancellationToken)
    {
        await using (Utf8JsonWriter writer = new(stream, FileOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
            writer.WriteStartArray("toolCalls");
            ExportCursor? after = null;
            IReadOnlyList<ExportToolCallRow> page;
            do
            {
                page = await snapshot.GetToolCallsAsync(inputs.Filter, after, ExportLimits.PageSize, cancellationToken);
                foreach (ExportToolCallRow row in page)
                {
                    WriteToolCall(writer, ExportRecords.ToolCall(row));
                }

                await writer.FlushAsync(cancellationToken);
                after = page.Count == 0
                    ? null
                    : new ExportCursor(page[^1].StartTime.ToUnixTimeMilliseconds(), page[^1].TraceId, page[^1].SpanId);
            }
            while (page.Count == ExportLimits.PageSize);

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        await stream.WriteAsync(NewLine, cancellationToken);
    }

    private static readonly byte[] NewLine = [(byte)'\n'];

    // One JSON object, then "\n".
    private static byte[] Document(Action<Utf8JsonWriter> write)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, FileOptions))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        stream.Write(NewLine);
        return stream.ToArray();
    }

    // What branch and agents --json rows have after their key.
    private static void WriteRollupFields(Utf8JsonWriter writer, LedgerRollupRow row)
    {
        LedgerFormat.WriteTotals(writer, row);
        LedgerFormat.WriteApiEquivalent(writer, row);
        LedgerFormat.WriteCacheCoverage(writer, row);
    }

    private static void WriteRollups(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyList<LedgerRollupRow> rows,
        Action<LedgerRollupRow> writeKey)
    {
        writer.WriteStartArray(name);
        foreach (LedgerRollupRow row in rows)
        {
            writer.WriteStartObject();
            writeKey(row);
            WriteRollupFields(writer, row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteCaches(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyList<CacheEfficiencyRow> rows,
        Action<CacheEfficiencyRow> writeKey)
    {
        writer.WriteStartArray(name);
        foreach (CacheEfficiencyRow row in rows)
        {
            writer.WriteStartObject();
            writeKey(row);
            LedgerFormat.WriteCacheFields(writer, row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteSpan(Utf8JsonWriter writer, ExportSpanRecord span, bool includeLocalPaths)
    {
        writer.WriteStartObject();
        writer.WriteString("traceId", span.TraceId);
        writer.WriteString("spanId", span.SpanId);
        LedgerFormat.WriteNullable(writer, "parentSpanId", span.ParentSpanId);
        writer.WriteString("operationName", span.OperationName);
        LedgerFormat.WriteNullable(writer, "conversationId", span.ConversationId);
        LedgerFormat.WriteNullable(writer, "agent", span.Agent);
        LedgerFormat.WriteNullable(writer, "provider", span.Provider);
        LedgerFormat.WriteNullable(writer, "requestModel", span.RequestModel);
        LedgerFormat.WriteNullable(writer, "responseModel", span.ResponseModel);
        LedgerFormat.WriteNullable(writer, "model", span.Model);
        LedgerFormat.WriteTime(writer, "startTime", span.StartTime);
        LedgerFormat.WriteTime(writer, "endTime", span.EndTime);
        writer.WriteNumber("inputTokens", span.InputTokens);
        writer.WriteNumber("cacheReadInputTokens", span.CacheReadInputTokens);
        writer.WriteNumber("cacheWriteInputTokens", span.CacheWriteInputTokens);
        writer.WriteNumber("uncachedInputTokens", span.UncachedInputTokens);
        writer.WriteNumber("outputTokens", span.OutputTokens);
        writer.WriteString("cacheReadState", span.CacheReadState);
        writer.WriteString("cacheWriteState", span.CacheWriteState);
        writer.WriteString("billingChannel", ReceiptMarkdown.ChannelId(span.BillingChannel));
        writer.WriteString("pricingStatus", span.PricingStatus);
        writer.WriteString("ratesVersion", span.RatesVersion);
        LedgerFormat.WriteMoney(writer, "costUsd", span.CostUsd);
        LedgerFormat.WriteMoney(writer, "aiCredits", span.AiCredits);
        LedgerFormat.WriteMoney(writer, "savedUsd", span.SavedUsd);
        WriteNullableMoney(writer, "apiEquivalentUsd", span.ApiEquivalentUsd);
        LedgerFormat.WriteNullable(writer, "apiEquivalentStatus", span.ApiEquivalentStatus);
        LedgerFormat.WriteNullable(writer, "apiRatesVersion", span.ApiRatesVersion);
        LedgerFormat.WriteNullable(writer, "apiRateCardSha256", span.ApiRateCardSha256);
        writer.WriteString("repository", span.Repository);
        if (includeLocalPaths)
        {
            LedgerFormat.WriteNullable(writer, "repositoryRoot", span.RepositoryRoot);
        }

        LedgerFormat.WriteNullable(writer, "branch", span.Branch);
        LedgerFormat.WriteNullable(writer, "headCommit", span.HeadCommit);
        LedgerFormat.WriteNullable(writer, "pullRequest", span.PullRequest);
        writer.WriteString("ingestSource", span.IngestSource);
        writer.WriteEndObject();
    }

    private static void WriteToolCall(Utf8JsonWriter writer, ExportToolCallRecord call)
    {
        writer.WriteStartObject();
        writer.WriteString("traceId", call.TraceId);
        writer.WriteString("spanId", call.SpanId);
        LedgerFormat.WriteNullable(writer, "conversationId", call.ConversationId);
        LedgerFormat.WriteNullable(writer, "agent", call.Agent);
        LedgerFormat.WriteNullable(writer, "toolName", call.ToolName);
        LedgerFormat.WriteTime(writer, "startTime", call.StartTime);
        LedgerFormat.WriteNullable(writer, "exitCode", call.ExitCode);
        writer.WriteString("status", call.Status);
        writer.WriteBoolean("failed", call.Failed);
        writer.WriteString("ingestSource", call.IngestSource);
        writer.WriteEndObject();
    }

    private static void WriteNullableMoney(Utf8JsonWriter writer, string name, decimal? value)
    {
        if (value is decimal amount)
        {
            LedgerFormat.WriteMoney(writer, name, amount);
        }
        else
        {
            writer.WriteNull(name);
        }
    }
}
