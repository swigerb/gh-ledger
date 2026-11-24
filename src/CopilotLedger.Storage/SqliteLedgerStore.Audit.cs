using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// The queries of gh ledger audit and MCP's get_audit_findings, which LedgerExportSnapshot runs in its read transaction. They
// read effective rows (FilterSql) and the tool calls of their prompt chains (ExportToolCallWhereSql), and never write.
// A live request's agent invocation is its nearest invoke_agent ancestor (agent_span_id, set at ingest); an imported Copilot
// request without an agent name is its prompt's main agent ("main"); other imported requests have none. A tool call
// belongs to the latest request of its invocation that started before it, so a live request's tool calls are those of its
// invocation that start after it and no later than the invocation's next request. That count is known only while the
// invocation's invoke_agent link is kept: links are pruned by start time, and the agent's own link comes first.
public sealed partial class SqliteLedgerStore
{
    // The longest chain of parents the nearest-agent walk follows.
    private const int MaxAgentDepth = 64;

    // The imported main agent's invocation ID within its trace.
    private const string ImportedMainAgent = "main";

    // The filter's spans as AuditSpans, in their own snapshot. Throws ArgumentNullException for a null filter.
    public async Task<IReadOnlyList<AuditSpan>> GetAuditSpansAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        await using LedgerExportSnapshot snapshot = await OpenExportSnapshotAsync(cancellationToken);
        return await snapshot.GetAuditSpansAsync(filter, cancellationToken);
    }

    // The tool calls of the filter's prompt chains as AuditToolCalls, in their own snapshot. Throws ArgumentNullException for
    // a null filter.
    public async Task<IReadOnlyList<AuditToolCall>> GetAuditToolCallsAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        await using LedgerExportSnapshot snapshot = await OpenExportSnapshotAsync(cancellationToken);
        return await snapshot.GetAuditToolCallsAsync(filter, cancellationToken);
    }

    // The filter's effective spans, ascending by (start time, trace ID, span ID).
    internal static async Task<IReadOnlyList<AuditSpan>> ReadAuditSpansAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        Dictionary<string, Dictionary<string, AuditLink>> links = await ReadAuditLinksAsync(snapshot, filter, cancellationToken);

        // The start times of each live invocation's tool calls.
        Dictionary<(string Trace, string Agent), List<long>> toolStarts = [];
        foreach ((string trace, Dictionary<string, AuditLink> tree) in links)
        {
            foreach (AuditLink link in tree.Values.Where(link => link.OperationName == GenAiOperationNames.ExecuteTool))
            {
                if (NearestAgent(tree, link.ParentSpanId) is not string agent)
                {
                    continue;
                }

                if (!toolStarts.TryGetValue((trace, agent), out List<long>? starts))
                {
                    toolStarts[(trace, agent)] = starts = [];
                }

                starts.Add(link.StartTimeUnixMs);
            }
        }

        // The start of the next request of each live request's invocation, null for the last.
        Dictionary<(string Trace, string Span), long?> nextStarts = [];
        await using (SqliteCommand command = snapshot.Connection!.CreateCommand())
        {
            command.Transaction = snapshot;
            command.CommandText = $"""
                SELECT trace_id, span_id, agent_span_id, start_time_unix_ms
                FROM spans
                WHERE accounting_state = 'effective' AND agent_span_id IS NOT NULL
                  AND trace_id IN (SELECT trace_id FROM spans WHERE {FilterSql})
                ORDER BY trace_id, agent_span_id, start_time_unix_ms, span_id;
                """;
            AddFilterParameters(command, filter);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            (string Trace, string Span, string Agent)? previous = null;
            while (await reader.ReadAsync(cancellationToken))
            {
                (string trace, string span, string agent, long start) =
                    (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3));
                if (previous is { } last && last.Trace == trace && last.Agent == agent)
                {
                    nextStarts[(last.Trace, last.Span)] = start;
                }

                nextStarts[(trace, span)] = null;
                previous = (trace, span, agent);
            }
        }

        await using SqliteCommand select = snapshot.Connection!.CreateCommand();
        select.Transaction = snapshot;
        select.CommandText = $"""
            SELECT trace_id, span_id, parent_span_id, conversation_id, start_time_unix_ms, end_time_unix_ms, agent_name,
                   agent_span_id, COALESCE(model_id, '(none)'), request_model, input_tokens, cache_read_input_tokens,
                   cache_write_input_tokens, output_tokens, cache_read_state, cache_write_state, billing_channel, pricing_status,
                   cost_usd_pico, api_equivalent_usd_pico, imported_tool_call_count, COALESCE(repo_slug, repo_root, '(none)'),
                   COALESCE(branch_name, '(none)'), pull_request_number, ingest_source
            FROM spans
            WHERE {FilterSql}
            ORDER BY start_time_unix_ms, trace_id, span_id;
            """;
        AddFilterParameters(select, filter);
        await using SqliteDataReader rows = await select.ExecuteReaderAsync(cancellationToken);
        List<AuditSpan> spans = [];
        while (await rows.ReadAsync(cancellationToken))
        {
            string trace = rows.GetString(0);
            string spanId = rows.GetString(1);
            long start = rows.GetInt64(4);
            string? agentName = TextOrNull(rows, 6);
            string? agentSpanId = TextOrNull(rows, 7);
            string source = rows.GetString(24);
            BillingChannel channel = ToBillingChannel(rows.GetString(16));
            decimal cost = PicoUsd.ToUsd(rows.GetInt64(18));
            string? invocation;
            long? toolCount;
            if (source == "otlp")
            {
                invocation = agentSpanId;
                bool known = agentSpanId is not null &&
                    links.TryGetValue(trace, out Dictionary<string, AuditLink>? tree) &&
                    tree.TryGetValue(agentSpanId, out AuditLink? agent) &&
                    agent.OperationName == GenAiOperationNames.InvokeAgent;
                long? next = nextStarts.GetValueOrDefault((trace, spanId));
                toolCount = known
                    ? toolStarts.GetValueOrDefault((trace, agentSpanId!))?.LongCount(at => at > start && (next is null || at <= next)) ?? 0
                    : null;
            }
            else
            {
                invocation = source == "import:copilot" && agentName is null ? ImportedMainAgent : null;
                toolCount = rows.IsDBNull(20) ? null : rows.GetInt64(20);
            }

            spans.Add(new AuditSpan
            {
                TraceId = trace,
                SpanId = spanId,
                ParentSpanId = TextOrNull(rows, 2),
                ConversationId = TextOrNull(rows, 3),
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(start),
                EndTime = DateTimeOffset.FromUnixTimeMilliseconds(rows.GetInt64(5)),
                AgentName = agentName,
                AgentInvocationId = invocation,
                Model = rows.GetString(8),
                RequestModel = TextOrNull(rows, 9),
                InputTokens = rows.GetInt64(10),
                CacheReadInputTokens = rows.GetInt64(11),
                CacheWriteInputTokens = rows.GetInt64(12),
                OutputTokens = rows.GetInt64(13),
                CacheReadState = ToCacheCounterState(rows.GetString(14)),
                CacheWriteState = ToCacheCounterState(rows.GetString(15)),
                Channel = channel,
                PricingStatus = ToPricingStatus(rows.GetString(17)),
                CostUsd = cost,
                AiCredits = channel == BillingChannel.GitHubAiCredits ? AiCreditMath.UsdToAiCredits(cost) : 0m,
                ApiEquivalentUsd = rows.IsDBNull(19) ? null : PicoUsd.ToUsd(rows.GetInt64(19)),
                ToolCallCount = toolCount,
                Repository = rows.GetString(21),
                Branch = rows.GetString(22),
                PullRequestNumber = rows.IsDBNull(23) ? null : rows.GetInt32(23),
                IngestSource = source,
            });
        }

        return spans;
    }

    // The execute_tool links that count, start in the filter's period, and belong to a prompt chain with a selected span,
    // ascending by (start time, trace ID, span ID). Each takes its chain's repository, branch, and pull request: those of the
    // chain's first selected span.
    internal static async Task<IReadOnlyList<AuditToolCall>> ReadAuditToolCallsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        Dictionary<string, Dictionary<string, AuditLink>> links = await ReadAuditLinksAsync(snapshot, filter, cancellationToken);
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT trace_id, repository, branch, pull_request_number FROM (
                SELECT trace_id, COALESCE(repo_slug, repo_root, '(none)') AS repository, COALESCE(branch_name, '(none)') AS branch,
                       pull_request_number,
                       ROW_NUMBER() OVER (PARTITION BY trace_id ORDER BY start_time_unix_ms, span_id) AS position
                FROM spans WHERE {FilterSql})
            WHERE position = 1
            ORDER BY trace_id;

            SELECT trace_id, span_id, parent_span_id, conversation_id, agent_name, start_time_unix_ms, tool_name, tool_exit_code,
                   status_code,
                   (SELECT owner.ingest_source FROM spans AS owner WHERE owner.id = span_links.owner_span_row_id),
                   (SELECT owner.agent_name FROM spans AS owner WHERE owner.id = span_links.owner_span_row_id)
            FROM span_links
            WHERE {ExportToolCallWhereSql}
            ORDER BY start_time_unix_ms, trace_id, span_id;
            """;
        AddFilterParameters(command, filter);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        Dictionary<string, (string Repository, string Branch, int? PullRequest)> chains = new(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            chains[reader.GetString(0)] =
                (reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetInt32(3));
        }

        await reader.NextResultAsync(cancellationToken);
        List<AuditToolCall> calls = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            string trace = reader.GetString(0);
            string? parent = TextOrNull(reader, 2);
            string? ownerSource = TextOrNull(reader, 9);
            string? invocation = null;
            if (ownerSource is null)
            {
                invocation = links.TryGetValue(trace, out Dictionary<string, AuditLink>? tree) ? NearestAgent(tree, parent) : null;
            }
            else if (ownerSource == "import:copilot" && reader.IsDBNull(10))
            {
                invocation = ImportedMainAgent;
            }

            (string repository, string branch, int? pullRequest) = chains[trace];
            calls.Add(new AuditToolCall
            {
                TraceId = trace,
                SpanId = reader.GetString(1),
                ParentSpanId = parent,
                ConversationId = TextOrNull(reader, 3),
                AgentName = TextOrNull(reader, 4),
                AgentInvocationId = invocation,
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
                ToolName = TextOrNull(reader, 6),
                ExitCode = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                StatusCode = reader.GetInt32(8),
                Imported = ownerSource is not null,
                Repository = repository,
                Branch = branch,
                PullRequestNumber = pullRequest,
            });
        }

        return calls;
    }

    // The live links (no owner row) of the prompt chains with a selected span, by trace and span ID.
    private static async Task<Dictionary<string, Dictionary<string, AuditLink>>> ReadAuditLinksAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT trace_id, span_id, parent_span_id, operation_name, start_time_unix_ms
            FROM span_links
            WHERE owner_span_row_id IS NULL AND trace_id IN (SELECT trace_id FROM spans WHERE {FilterSql})
            ORDER BY trace_id, span_id;
            """;
        AddFilterParameters(command, filter);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        Dictionary<string, Dictionary<string, AuditLink>> links = new(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            string trace = reader.GetString(0);
            if (!links.TryGetValue(trace, out Dictionary<string, AuditLink>? tree))
            {
                links[trace] = tree = new Dictionary<string, AuditLink>(StringComparer.Ordinal);
            }

            tree[reader.GetString(1)] = new AuditLink(TextOrNull(reader, 2), TextOrNull(reader, 3), reader.GetInt64(4));
        }

        return links;
    }

    // The span ID of the nearest invoke_agent link at or above parentSpanId in tree, or null when the chain of parents
    // leaves the tree or is longer than MaxAgentDepth.
    private static string? NearestAgent(Dictionary<string, AuditLink> tree, string? parentSpanId)
    {
        string? current = parentSpanId;
        for (int step = 0; step < MaxAgentDepth && current is not null; step++)
        {
            if (!tree.TryGetValue(current, out AuditLink? link))
            {
                return null;
            }

            if (link.OperationName == GenAiOperationNames.InvokeAgent)
            {
                return current;
            }

            current = link.ParentSpanId;
        }

        return null;
    }

    private static PricingStatus ToPricingStatus(string value) => value switch
    {
        "priced" => PricingStatus.Priced,
        "local_offload" => PricingStatus.LocalOffload,
        "byok_unpriced" => PricingStatus.ByokUnpriced,
        "unknown_model" => PricingStatus.UnknownModel,
        "no_rate_for_date" => PricingStatus.NoRateForDate,
        "unrecognized_endpoint" => PricingStatus.UnrecognizedEndpoint,
        "invalid_usage" => PricingStatus.InvalidUsage,
        "not_billable_operation" => PricingStatus.NotBillableOperation,
        "other_provider" => PricingStatus.OtherProvider,
        _ => throw new InvalidDataException($"Unknown pricing status '{value}'."),
    };

    private sealed record AuditLink(string? ParentSpanId, string? OperationName, long StartTimeUnixMs);
}
