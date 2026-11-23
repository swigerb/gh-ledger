using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.TestSupport;

// Imported and live entries for the live/import authority rules, and a check of the rules' invariants.
public static class ImportEntries
{
    public static readonly JsonRateCardCalculator Calculator = JsonRateCardCalculator.LoadEmbedded();

    public static readonly DateTimeOffset Start = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    // A Copilot record that GitHub billed (provider github) with serviceRequestId, or, when byok is true, a BYOK request to
    // OpenAI, which has no usage identity.
    public static ImportedLedgerEntry Copilot(
        string key,
        string conversation,
        string? serviceRequestId,
        ImportedRecordKind kind = ImportedRecordKind.Usage,
        bool byok = false,
        IReadOnlyList<GenAiSpanLink>? toolLinks = null)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: Start,
            providerName: byok ? "openai" : "github",
            requestModel: "mai-code-2",
            conversationId: conversation,
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = ImportIds.TraceId(ImportSourceKind.Copilot, conversation, null, kind, key),
            SpanId = ImportIds.SpanId(ImportSourceKind.Copilot, conversation, kind, key),
            ServiceRequestId = serviceRequestId,
            BillingSource = byok ? BillingSource.Provider : BillingSource.Endpoint,
        };
        return new ImportedLedgerEntry
        {
            Source = ImportSourceKind.Copilot,
            RecordKeySha256 = ImportIds.RecordKeySha256(ImportSourceKind.Copilot, kind, key),
            Span = span,
            Calculation = Calculator.Calculate(span),
            ToolLinks = toolLinks ?? [],
        };
    }

    // A live chat span that GitHub billed, with serviceRequestId when it isn't null.
    public static LedgerEntry Live(string? conversation, string? serviceRequestId, DateTimeOffset? start = null)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: start ?? Start,
            providerName: "github",
            requestModel: "mai-code-2",
            conversationId: conversation,
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            ServiceRequestId = serviceRequestId,
        };
        return new LedgerEntry(span, Calculator.Calculate(span), null);
    }

    // An execute_tool link that entry owns.
    public static GenAiSpanLink ToolLink(ImportedLedgerEntry entry, string toolCallId, long? exitCode) => new()
    {
        TraceId = entry.Span.TraceId,
        SpanId = ImportIds.ToolSpanId(ImportSourceKind.Copilot, entry.Span.ConversationId!, toolCallId),
        OperationName = "execute_tool",
        ConversationId = entry.Span.ConversationId,
        StartTime = Start,
        ToolName = "bash",
        ToolExitCode = exitCode,
    };

    // UsageAuthority's invariants I1–I4 hold in the ledger at databasePath.
    public static async Task AssertInvariantsAsync(string databasePath)
    {
        Assert.Equal(
            0L,
            await LedgerSql.ScalarAsync(
                databasePath,
                """
                SELECT COUNT(*) FROM spans AS imported JOIN spans AS live ON live.canonical_usage_id = imported.canonical_usage_id
                WHERE imported.ingest_source <> 'otlp' AND imported.accounting_state = 'effective' AND live.ingest_source = 'otlp';
                """));
        Assert.Equal(
            0L,
            await LedgerSql.ScalarAsync(
                databasePath,
                """
                SELECT COUNT(*) FROM spans AS imported
                WHERE imported.ingest_source <> 'otlp' AND imported.accounting_state = 'effective'
                  AND imported.canonical_usage_id IS NULL
                  AND EXISTS (SELECT 1 FROM spans AS live
                               WHERE live.conversation_id = imported.conversation_id AND live.ingest_source = 'otlp');
                """));
        Assert.Equal(
            0L,
            await LedgerSql.ScalarAsync(
                databasePath,
                """
                SELECT COUNT(*) FROM spans AS imported
                WHERE imported.ingest_source <> 'otlp' AND imported.accounting_state = 'effective'
                  AND EXISTS (SELECT 1 FROM spans AS live
                               WHERE live.conversation_id = imported.conversation_id AND live.ingest_source = 'otlp'
                                 AND live.canonical_usage_id IS NULL);
                """));
        Assert.Equal(
            0L,
            await LedgerSql.ScalarAsync(
                databasePath,
                """
                SELECT (SELECT COUNT(*) FROM import_record_aliases AS alias
                         WHERE NOT EXISTS (SELECT 1 FROM spans WHERE spans.id = alias.span_row_id)
                            OR (alias.resolution = 'live'
                                AND NOT EXISTS (SELECT 1 FROM spans WHERE spans.id = alias.span_row_id AND ingest_source = 'otlp')))
                     + (SELECT COUNT(*) FROM spans WHERE accounting_state = 'superseded' AND ingest_source = 'otlp');
                """));
    }
}
