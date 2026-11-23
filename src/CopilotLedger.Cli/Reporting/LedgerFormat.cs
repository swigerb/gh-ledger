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

    // ratio as a percentage with one decimal place, rounded half away from zero; n/a for null, never 0.0%.
    public static string Percent(decimal? ratio) =>
        ratio is null
            ? "n/a"
            : decimal.Round(ratio.Value * 100m, 1, MidpointRounding.AwayFromZero)
                .ToString("0.0", CultureInfo.InvariantCulture) + "%";

    public static string LocalTime(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static void WriteMoney(
        Utf8JsonWriter writer,
        string name,
        decimal value)
    {
        writer.WritePropertyName(name);
        writer.WriteRawValue(value.ToString("0.############", CultureInfo.InvariantCulture));
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

    public static string FinishJson(Utf8JsonWriter writer, MemoryStream stream)
    {
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
