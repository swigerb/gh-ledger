using System.Globalization;
using System.Text;
using System.Text.Json;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Reporting;

internal static class LedgerFormat
{
    public static string Credits(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero)
            .ToString("N2", CultureInfo.InvariantCulture);

    public static string Usd(decimal value) =>
        "$" + Credits(value);

    public static string Count(long value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    // An API-equivalent amount: Unpriced when nothing was priced (usd is null), never $0.00. Otherwise the amount, with "~"
    // before it when it's an estimate (estimatedSpans reported no cache counters, so all of their input was priced as
    // uncached) and "*" after it when it leaves out unpricedSpans, which have no price.
    public static string ApiEquivalent(decimal? usd, long unpricedSpans, long estimatedSpans) =>
        usd is null
            ? "Unpriced"
            : $"{(estimatedSpans > 0 ? "~" : "")}{Usd(usd.Value)}{(unpricedSpans > 0 ? "*" : "")}";

    public static string ApiEquivalent(LedgerRollupRow row) =>
        ApiEquivalent(row.ApiEquivalentUsd, row.ApiEquivalentUnpricedSpanCount, row.ApiEquivalentEstimatedSpanCount);

    public static string ApiEquivalent(PromptChainRow row) =>
        ApiEquivalent(row.ApiEquivalentUsd, row.ApiEquivalentUnpricedSpanCount, row.ApiEquivalentEstimatedSpanCount);

    // ratio as a percentage with one decimal place, rounded half away from zero; n/a for null, never 0.0%.
    public static string Percent(decimal? ratio) =>
        ratio is null
            ? "n/a"
            : decimal.Round(ratio.Value * 100m, 1, MidpointRounding.AwayFromZero)
                .ToString("0.0", CultureInfo.InvariantCulture) + "%";

    public static string LocalTime(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string LocalTimeSeconds(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone)
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    // value with every digit it has, as WriteMoney writes it.
    public static string Exact(decimal value) =>
        value.ToString("0.############", CultureInfo.InvariantCulture);

    public static void WriteMoney(
        Utf8JsonWriter writer,
        string name,
        decimal value)
    {
        writer.WritePropertyName(name);
        writer.WriteRawValue(Exact(value));
    }

    // The API-equivalent fields that report rows have after WriteTotals.
    public static void WriteApiEquivalent(Utf8JsonWriter writer, LedgerRollupRow row) =>
        WriteApiEquivalent(
            writer,
            row.ApiEquivalentUsd,
            row.ApiEquivalentUnpricedSpanCount,
            row.ApiEquivalentEstimatedSpanCount);

    public static void WriteApiEquivalent(Utf8JsonWriter writer, PromptChainRow row) =>
        WriteApiEquivalent(
            writer,
            row.ApiEquivalentUsd,
            row.ApiEquivalentUnpricedSpanCount,
            row.ApiEquivalentEstimatedSpanCount);

    // apiEquivalentUsd (null when nothing was priced, never 0 for unknown prices), apiEquivalentUnpricedSpans, and
    // apiEquivalentEstimatedSpans.
    public static void WriteApiEquivalent(
        Utf8JsonWriter writer,
        decimal? usd,
        long unpricedSpans,
        long estimatedSpans)
    {
        if (usd is null)
        {
            writer.WriteNull("apiEquivalentUsd");
        }
        else
        {
            WriteMoney(writer, "apiEquivalentUsd", usd.Value);
        }

        writer.WriteNumber("apiEquivalentUnpricedSpans", unpricedSpans);
        writer.WriteNumber("apiEquivalentEstimatedSpans", estimatedSpans);
    }

    // value with every digit decimal division gave it, or null. Used for ratios and averages.
    public static void WriteDecimal(
        Utf8JsonWriter writer,
        string name,
        decimal? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WritePropertyName(name);
        writer.WriteRawValue(value.Value.ToString(CultureInfo.InvariantCulture));
    }

    public static void WriteTime(
        Utf8JsonWriter writer,
        string name,
        DateTimeOffset? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WriteString(
            name,
            value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    public static void WriteNullable(
        Utf8JsonWriter writer,
        string name,
        string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WriteString(name, value);
    }

    public static void WriteNullable(
        Utf8JsonWriter writer,
        string name,
        long? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WriteNumber(name, value.Value);
    }

    public static void WriteTotals(
        Utf8JsonWriter writer,
        LedgerRollupRow row)
    {
        writer.WriteNumber("spans", row.SpanCount);
        writer.WriteNumber("unpricedSpans", row.UnpricedSpanCount);
        writer.WriteNumber("inputTokens", row.InputTokens);
        writer.WriteNumber("outputTokens", row.OutputTokens);
        writer.WriteNumber("cacheReadInputTokens", row.CacheReadInputTokens);
        writer.WriteNumber("cacheWriteInputTokens", row.CacheWriteInputTokens);
        WriteMoney(writer, "aiCredits", row.AiCredits);
        WriteMoney(writer, "aiCreditsUsd", row.AiCreditsUsd);
        WriteMoney(writer, "foundryByokUsd", row.FoundryByokUsd);
        WriteMoney(writer, "localSavedUsd", row.LocalSavedUsd);
    }

    // The cache fields that branch and agents rows end with. Receipts don't write them.
    public static void WriteCacheCoverage(
        Utf8JsonWriter writer,
        LedgerRollupRow row)
    {
        writer.WriteNumber("cacheReportedSpans", row.CacheReportedSpanCount);
        writer.WriteNumber("cacheUnreportedSpans", row.CacheUnreportedSpanCount);
        WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
    }

    public static void WriteTotals(
        Utf8JsonWriter writer,
        PromptChainRow row)
    {
        writer.WriteNumber("spans", row.SpanCount);
        writer.WriteNumber("unpricedSpans", row.UnpricedSpanCount);
        writer.WriteNumber("inputTokens", row.InputTokens);
        writer.WriteNumber("outputTokens", row.OutputTokens);
        writer.WriteNumber("cacheReadInputTokens", row.CacheReadInputTokens);
        writer.WriteNumber("cacheWriteInputTokens", row.CacheWriteInputTokens);
        WriteMoney(writer, "aiCredits", row.AiCredits);
        WriteMoney(writer, "aiCreditsUsd", row.AiCreditsUsd);
        WriteMoney(writer, "foundryByokUsd", row.FoundryByokUsd);
        WriteMoney(writer, "localSavedUsd", row.LocalSavedUsd);
    }

    // A top-prompts --json row, which gh ledger export's prompt-chains.json shares.
    public static void WritePromptChain(Utf8JsonWriter writer, PromptChainRow row, int rank)
    {
        writer.WriteStartObject();
        writer.WriteNumber("rank", rank);
        writer.WriteString("traceId", row.TraceId);
        WriteNullable(writer, "conversationId", row.ConversationId);
        WriteTime(writer, "startTime", row.StartTime);
        WriteTime(writer, "endTime", row.EndTime);
        writer.WriteString("repository", row.Repository);
        writer.WriteString("branch", row.Branch);
        writer.WriteStartArray("agents");
        foreach (string agent in row.Agents)
        {
            writer.WriteStringValue(agent);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("models");
        foreach (string model in row.Models)
        {
            writer.WriteStringValue(model);
        }

        writer.WriteEndArray();
        WriteTotals(writer, row);
        WriteApiEquivalent(writer, row);
        writer.WriteEndObject();
    }

    // The fields of a status --cache --json row after its key, which gh ledger export's summary.json shares.
    public static void WriteCacheFields(Utf8JsonWriter writer, CacheEfficiencyRow row)
    {
        writer.WriteNumber("spans", row.SpanCount);
        writer.WriteNumber("cacheReportedSpans", row.CacheReportedSpanCount);
        writer.WriteNumber("cacheUnreportedSpans", row.CacheUnreportedSpanCount);
        writer.WriteNumber("inputTokens", row.InputTokens);
        writer.WriteNumber("outputTokens", row.OutputTokens);
        writer.WriteNumber("maxInputTokens", row.MaxInputTokens);
        WriteDecimal(writer, "averageInputTokensPerTurn", row.AverageInputTokensPerTurn);
        writer.WriteNumber("reportedInputTokens", row.ReportedInputTokens);
        writer.WriteNumber("reportedCacheReadInputTokens", row.ReportedCacheReadInputTokens);
        writer.WriteNumber("reportedCacheWriteInputTokens", row.ReportedCacheWriteInputTokens);
        WriteNullable(writer, "uncachedInputTokens", row.UncachedInputTokens);
        writer.WriteNumber("estimatedUncachedInputTokens", row.EstimatedUncachedInputTokens);
        WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
    }

    public static string FinishJson(Utf8JsonWriter writer, MemoryStream stream)
    {
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
