using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

public sealed partial class SqliteLedgerStore
{
    private const string InsertSql = """
        INSERT INTO spans (
            trace_id, span_id, parent_span_id, operation_name, conversation_id,
            agent_name, provider_name, request_model, response_model, model_id,
            rate_card_model_id, tier_name, server_address, server_port,
            start_time_unix_ms, end_time_unix_ms, input_tokens, output_tokens,
            cache_read_input_tokens, cache_write_input_tokens, billing_channel,
            pricing_status, rates_version, cost_usd_pico, saved_usd_pico,
            repo_root, repo_slug, branch_name, head_commit_sha, recorded_at_unix_ms,
            cache_read_state, cache_write_state, pull_request_number,
            api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id,
            api_rates_version, api_rate_card_sha256,
            ingest_source, canonical_usage_id, imported_tool_call_count
        ) VALUES (
            @trace_id, @span_id, @parent_span_id, @operation_name, @conversation_id,
            @agent_name, @provider_name, @request_model, @response_model, @model_id,
            @rate_card_model_id, @tier_name, @server_address, @server_port,
            @start_time_unix_ms, @end_time_unix_ms, @input_tokens, @output_tokens,
            @cache_read_input_tokens, @cache_write_input_tokens, @billing_channel,
            @pricing_status, @rates_version, @cost_usd_pico, @saved_usd_pico,
            @repo_root, @repo_slug, @branch_name, @head_commit_sha, @recorded_at_unix_ms,
            @cache_read_state, @cache_write_state, @pull_request_number,
            @api_equivalent_usd_pico, @api_equivalent_status, @api_rate_card_model_id,
            @api_rates_version, @api_rate_card_sha256,
            @ingest_source, @canonical_usage_id, @imported_tool_call_count
        ) ON CONFLICT (trace_id, span_id) DO NOTHING
        RETURNING id;
        """;

    // The conditions of a LedgerFilter on a span row, whatever its accounting state, with parameters from AddFilterParameters.
    // A null filter property matches every span.
    private const string SpanFilterSql = """
        (@from IS NULL OR start_time_unix_ms >= @from)
          AND (@to IS NULL OR start_time_unix_ms < @to)
          AND (@repository IS NULL OR COALESCE(repo_slug, repo_root, '(none)') = @repository)
          AND (@branch IS NULL OR COALESCE(branch_name, '(none)') = @branch)
          AND (@pull_request IS NULL OR pull_request_number = @pull_request)
          AND (@agent IS NULL OR COALESCE(agent_name, '(none)') = @agent)
          AND (@conversation_id IS NULL OR conversation_id = @conversation_id)
        """;

    // The conditions of a LedgerFilter, with parameters from AddFilterParameters. A null filter property matches every span.
    // Only effective rows count: an imported row that a live row superseded is kept for diagnosis and never reported.
    private const string FilterSql = SpanFilterSql + "\n  AND accounting_state = 'effective'";

    // An execute_tool link counts unless an imported row that was superseded owns it (its live twin's links count instead).
    private const string EffectiveLinkSql = """
        (owner_span_row_id IS NULL
                 OR EXISTS (SELECT 1 FROM spans WHERE spans.id = span_links.owner_span_row_id AND spans.accounting_state = 'effective'))
        """;

    // CacheMetrics.IsReported: the span carried at least one cache counter.
    private const string ReportedSql = "(cache_read_state = 'reported' OR cache_write_state = 'reported')";

    // A span's input tokens that were neither read from nor written to the cache, never below 0.
    private const string UncachedSql = "MAX(input_tokens - cache_read_input_tokens - cache_write_input_tokens, 0)";

    // A growth sequence: one agent invocation's requests to one model in one repository, branch, and conversation.
    private const string SequenceSql = """
        trace_id, agent_span_id, COALESCE(repo_slug, repo_root, '(none)'), COALESCE(branch_name, '(none)'),
                                 COALESCE(conversation_id, '(none)'), COALESCE(model_id, '(none)')
        """;

    // Budgets with their key's all-time effective spend, in ReadBranchBudgetsAsync's column order, from branch_budgets AS b.
    private const string BranchBudgetSelectSql = """
        SELECT b.repository, b.branch, b.budget_usd_pico, b.set_at_unix_ms, b.alerted_at_unix_ms, b.notified_at_unix_ms,
               (SELECT COALESCE(SUM(s.cost_usd_pico), 0) FROM spans AS s
                 WHERE COALESCE(s.repo_slug, s.repo_root, '(none)') = b.repository
                   AND COALESCE(s.branch_name, '(none)') = b.branch
                   AND s.accounting_state = 'effective') AS spent_usd_pico
        FROM branch_budgets AS b
        """;

    private readonly TimeProvider _timeProvider;
    private readonly string _connectionString;
    private bool _initialized;

    public SqliteLedgerStore(string databasePath, TimeProvider? timeProvider = null)
    {
        DatabasePath = databasePath;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    public string DatabasePath { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(DatabasePath))!;
        Directory.CreateDirectory(directory);

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);

        await using (SqliteCommand journalModeCommand = connection.CreateCommand())
        {
            journalModeCommand.CommandText = "PRAGMA journal_mode = WAL;";
            string journalMode = (string?)await journalModeCommand.ExecuteScalarAsync(cancellationToken)
                ?? string.Empty;
            if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The database did not enable WAL journal mode. It returned '{journalMode}'.");
            }
        }

        int userVersion = await GetUserVersionAsync(connection, cancellationToken);
        int latestVersion = LedgerMigrations.Migrations[^1].Version;
        if (userVersion > latestVersion)
        {
            throw new InvalidOperationException(
                $"Database user_version {userVersion} is newer than the supported version {latestVersion}.");
        }

        foreach ((int version, string sql) in LedgerMigrations.Migrations)
        {
            if (version <= userVersion)
            {
                continue;
            }

            // BEGIN IMMEDIATE takes the write lock before the version is read again, so a second process waits here
            // and then skips the migrations the first one committed. Disposing an uncommitted transaction rolls it back.
            await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            await using (SqliteCommand versionQuery = connection.CreateCommand())
            {
                versionQuery.Transaction = transaction;
                versionQuery.CommandText = "PRAGMA user_version;";
                userVersion = Convert.ToInt32(
                    await versionQuery.ExecuteScalarAsync(cancellationToken),
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            if (version <= userVersion)
            {
                continue;
            }

            await using (SqliteCommand migrationCommand = connection.CreateCommand())
            {
                migrationCommand.Transaction = transaction;
                migrationCommand.CommandText = sql;
                await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (SqliteCommand versionCommand = connection.CreateCommand())
            {
                versionCommand.Transaction = transaction;
                versionCommand.CommandText = $"PRAGMA user_version = {version};";
                await versionCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            userVersion = version;
        }

        _initialized = true;
    }

    public async Task<int> RecordAsync(
        IEnumerable<LedgerEntry> entries,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        List<LedgerEntry> batch = [.. entries];
        foreach (LedgerEntry entry in batch)
        {
            ValidateEntry(entry, nameof(entries));
        }

        if (batch.Count == 0)
        {
            return 0;
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);

        // BEGIN IMMEDIATE: the authority rules read and write under the write lock, so an import runs before or after this.
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        int inserted = 0;
        long recordedAtUnixMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        foreach (LedgerEntry entry in batch)
        {
            RowProvenance provenance = LiveProvenance(entry);
            if (await InsertSpanAsync(
                    transaction,
                    entry.Span,
                    entry.Calculation,
                    provenance,
                    recordedAtUnixMs,
                    cancellationToken) is not long id)
            {
                continue;
            }

            inserted++;

            // A live row supersedes the imported row of the same call, wherever it is (I1), and the imported rows of its
            // conversation that it can't be matched to (I2, I3).
            if (provenance.CanonicalUsageId is not null)
            {
                await UsageAuthority.SupersedeCanonicalAsync(
                    transaction,
                    provenance.CanonicalUsageId,
                    id,
                    cancellationToken);
            }

            if (entry.Span.ConversationId is not null)
            {
                await UsageAuthority.ReconcileConversationAsync(
                    transaction,
                    entry.Span.ConversationId,
                    id,
                    cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    // Gives every span recorded without an API-equivalent result (api_equivalent_status IS NULL: recorded before schema
    // version 5) the result calculator gives it, as LedgerIngestor would have when it recorded the span: priced as of its start
    // time, a local span as the card's local retail baseline. Writes only the five api_* columns, never the billed cost,
    // savings, channel, pricing status, or rates_version, and writes nothing when dryRun is true. A span whose price is beyond
    // the pico-USD range keeps no result. Reads and writes in one transaction, so spans recorded meanwhile, which have a
    // result, never mix in.
    public async Task<ApiEquivalentBackfill> BackfillApiEquivalentAsync(
        JsonRateCardCalculator calculator,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        EnsureInitialized();

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: dryRun);
        List<(long Id, ApiEquivalentCost Cost, long? Pico)> results = [];
        long outOfRange = 0;
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = """
                SELECT id, operation_name, billing_channel, model_id, start_time_unix_ms,
                       input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens, request_model
                FROM spans
                WHERE api_equivalent_status IS NULL AND accounting_state = 'effective'
                ORDER BY id;
                """;
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ApiEquivalentCost cost = calculator.CalculateApiEquivalent(new ApiEquivalentRequest
                {
                    OperationName = reader.GetString(1),
                    Channel = ToBillingChannel(reader.GetString(2)),
                    ModelId = reader.IsDBNull(3) ? null : reader.GetString(3),
                    RequestModel = reader.IsDBNull(9) ? null : reader.GetString(9),
                    StartTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4)),
                    InputTokens = reader.GetInt64(5),
                    OutputTokens = reader.GetInt64(6),
                    CacheReadInputTokens = reader.GetInt64(7),
                    CacheWriteInputTokens = reader.GetInt64(8),
                });
                long? pico = null;
                if (cost.UsdCost is decimal usd)
                {
                    try
                    {
                        pico = PicoUsd.FromUsd(usd);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        outOfRange++;
                        continue;
                    }
                }

                results.Add((reader.GetInt64(0), cost, pico));
            }
        }

        long updated = 0;
        if (!dryRun && results.Count > 0)
        {
            await using SqliteCommand update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE spans
                   SET api_equivalent_usd_pico = @pico,
                       api_equivalent_status = @status,
                       api_rate_card_model_id = @model,
                       api_rates_version = @version,
                       api_rate_card_sha256 = @sha256
                 WHERE id = @id AND api_equivalent_status IS NULL;
                """;
            SqliteParameter id = update.Parameters.Add("@id", SqliteType.Integer);
            SqliteParameter pico = update.Parameters.Add("@pico", SqliteType.Integer);
            SqliteParameter status = update.Parameters.Add("@status", SqliteType.Text);
            SqliteParameter model = update.Parameters.Add("@model", SqliteType.Text);
            update.Parameters.AddWithValue("@version", calculator.RatesVersion);
            update.Parameters.AddWithValue("@sha256", calculator.ApiRateCardSha256);
            foreach ((long Id, ApiEquivalentCost Cost, long? Pico) result in results)
            {
                id.Value = result.Id;
                pico.Value = OrDbNull(result.Pico);
                status.Value = ToDatabaseString(result.Cost.Status);
                model.Value = OrDbNull(result.Cost.RateCardModelId);
                updated += await update.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        return new ApiEquivalentBackfill
        {
            SpanCount = results.Count + outOfRange,
            PricedSpanCount = results.Count(result => result.Cost.Status == ApiEquivalentStatus.Priced),
            PricedUsd = results.Sum(result => result.Cost.UsdCost ?? 0m),
            UnpricedSpanCount = results.Count(result => result.Cost.Status == ApiEquivalentStatus.Unpriced),
            NotApplicableSpanCount =
                results.Count(result => result.Cost.Status == ApiEquivalentStatus.NotApplicable),
            OutOfRangeSpanCount = outOfRange,
            UpdatedSpanCount = updated,
        };
    }

    public Task<IReadOnlyList<LedgerRollupRow>> GetRollupAsync(
        LedgerGrouping grouping,
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        GetRollupAsync(grouping, filter, null, cancellationToken);

    // whatIf: null for the API-equivalent results recorded with the spans. Otherwise, a rate card that prices every span
    // again, as LedgerIngestor would have priced it with that card, for the three ApiEquivalent fields only; every other field
    // is as recorded, and nothing is written.
    public async Task<IReadOnlyList<LedgerRollupRow>> GetRollupAsync(
        LedgerGrouping grouping,
        LedgerFilter filter,
        JsonRateCardCalculator? whatIf,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);

        // One read transaction, so both queries see the same spans.
        await using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
        return await ReadRollupAsync(snapshot, grouping, filter, whatIf, cancellationToken);
    }

    // GetRollupAsync's queries, in a read transaction that the caller owns.
    internal static async Task<IReadOnlyList<LedgerRollupRow>> ReadRollupAsync(
        SqliteTransaction snapshot,
        LedgerGrouping grouping,
        LedgerFilter filter,
        JsonRateCardCalculator? whatIf,
        CancellationToken cancellationToken)
    {
        SqliteConnection connection = snapshot.Connection!;
        (string keyColumns, string keyAliases) = RollupKey(grouping);
        Dictionary<string, RetailTotals>? repriced = whatIf is null
            ? null
            : await RepriceAsync(
                snapshot,
                keyColumns,
                keyAliases,
                FilterSql,
                query => AddFilterParameters(query, filter),
                whatIf,
                cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = GetRollupSql(grouping);
        AddFilterParameters(command, filter);

        List<LedgerRollupRow> rows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            decimal aiCreditsUsd =
                PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("github_usd_pico")));
            RetailTotals retail = repriced is null
                ? new RetailTotals(
                    ReadUsd(reader, "api_equivalent_usd_pico"),
                    reader.GetInt64(reader.GetOrdinal("api_equivalent_unpriced_span_count")),
                    reader.GetInt64(reader.GetOrdinal("api_equivalent_estimated_span_count")))
                : repriced[GroupKey(reader, keyAliases)];
            rows.Add(new LedgerRollupRow
            {
                Repository = grouping is LedgerGrouping.Repository or LedgerGrouping.Branch
                    ? reader.GetString(reader.GetOrdinal("repository"))
                    : null,
                Branch = grouping == LedgerGrouping.Branch
                    ? reader.GetString(reader.GetOrdinal("branch"))
                    : null,
                Model = grouping is LedgerGrouping.Model or LedgerGrouping.ChannelModel
                    ? reader.GetString(reader.GetOrdinal("model"))
                    : null,
                Agent = grouping == LedgerGrouping.Agent
                    ? reader.GetString(reader.GetOrdinal("agent"))
                    : null,
                Channel = grouping == LedgerGrouping.ChannelModel
                    ? ToBillingChannel(reader.GetString(reader.GetOrdinal("channel")))
                    : null,
                SpanCount = reader.GetInt64(reader.GetOrdinal("span_count")),
                UnpricedSpanCount = reader.GetInt64(reader.GetOrdinal("unpriced_span_count")),
                InputTokens = reader.GetInt64(reader.GetOrdinal("input_tokens")),
                OutputTokens = reader.GetInt64(reader.GetOrdinal("output_tokens")),
                CacheReadInputTokens =
                    reader.GetInt64(reader.GetOrdinal("cache_read_input_tokens")),
                CacheWriteInputTokens =
                    reader.GetInt64(reader.GetOrdinal("cache_write_input_tokens")),
                CacheReportedSpanCount =
                    reader.GetInt64(reader.GetOrdinal("cache_reported_span_count")),
                ReportedInputTokens = reader.GetInt64(reader.GetOrdinal("reported_input_tokens")),
                ReportedCacheReadInputTokens =
                    reader.GetInt64(reader.GetOrdinal("reported_cache_read_input_tokens")),
                ReportedUncachedInputTokens =
                    reader.GetInt64(reader.GetOrdinal("reported_uncached_input_tokens")),
                AiCredits = AiCreditMath.UsdToAiCredits(aiCreditsUsd),
                AiCreditsUsd = aiCreditsUsd,
                FoundryByokUsd =
                    PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("byok_usd_pico"))),
                LocalSavedUsd =
                    PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("saved_usd_pico"))),
                ApiEquivalentUsd = retail.Usd,
                ApiEquivalentUnpricedSpanCount = retail.UnpricedSpanCount,
                ApiEquivalentEstimatedSpanCount = retail.EstimatedSpanCount,
            });
        }

        return rows;
    }

    // Prompt-cache efficiency by group, largest input first, then by the group key. grouping is Repository, Branch, Model,
    // Agent, or PullRequest, which groups spans by repository and pull request number, and spans without one by repository
    // and branch. Throws ArgumentOutOfRangeException for ChannelModel.
    public async Task<IReadOnlyList<CacheEfficiencyRow>> GetCacheEfficiencyAsync(
        LedgerGrouping grouping,
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await ReadCacheEfficiencyAsync(connection, null, grouping, filter, cancellationToken);
    }

    // GetCacheEfficiencyAsync's query on connection, in transaction when it isn't null.
    internal static async Task<IReadOnlyList<CacheEfficiencyRow>> ReadCacheEfficiencyAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        LedgerGrouping grouping,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        (string keyColumns, string keyAliases) = grouping switch
        {
            LedgerGrouping.Repository =>
                ("COALESCE(repo_slug, repo_root, '(none)') AS repository",
                 "repository"),
            LedgerGrouping.Branch =>
                ("""
                 COALESCE(repo_slug, repo_root, '(none)') AS repository,
                          COALESCE(branch_name, '(none)') AS branch
                 """,
                 "repository, branch"),
            LedgerGrouping.Model =>
                ("COALESCE(model_id, '(none)') AS model",
                 "model"),
            LedgerGrouping.Agent =>
                ("COALESCE(agent_name, '(none)') AS agent",
                 "agent"),
            LedgerGrouping.PullRequest =>
                ("""
                 COALESCE(repo_slug, repo_root, '(none)') AS repository,
                          pull_request_number AS pull_request,
                          CASE WHEN pull_request_number IS NULL THEN COALESCE(branch_name, '(none)') END AS branch
                 """,
                 "repository, pull_request, branch"),
            _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, null),
        };

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {keyColumns},
                   COUNT(*)                                                                AS span_count,
                   SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END)                          AS cache_reported_span_count,
                   SUM(input_tokens)                                                       AS input_tokens,
                   SUM(output_tokens)                                                      AS output_tokens,
                   MAX(input_tokens)                                                       AS max_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN input_tokens ELSE 0 END)               AS reported_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN cache_read_input_tokens ELSE 0 END)    AS reported_cache_read_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN cache_write_input_tokens ELSE 0 END)   AS reported_cache_write_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN {UncachedSql} ELSE 0 END)              AS reported_uncached_input_tokens,
                   SUM({UncachedSql})                                                      AS estimated_uncached_input_tokens
            FROM spans
            WHERE {FilterSql}
            GROUP BY {keyAliases}
            ORDER BY input_tokens DESC, {keyAliases} ASC;
            """;
        AddFilterParameters(command, filter);

        List<CacheEfficiencyRow> rows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            bool hasRepository = grouping is LedgerGrouping.Repository
                or LedgerGrouping.Branch
                or LedgerGrouping.PullRequest;
            bool hasBranch = grouping is LedgerGrouping.Branch or LedgerGrouping.PullRequest
                && !reader.IsDBNull(reader.GetOrdinal("branch"));
            rows.Add(new CacheEfficiencyRow
            {
                Repository = hasRepository ? reader.GetString(reader.GetOrdinal("repository")) : null,
                Branch = hasBranch ? reader.GetString(reader.GetOrdinal("branch")) : null,
                PullRequestNumber = grouping == LedgerGrouping.PullRequest
                    && !reader.IsDBNull(reader.GetOrdinal("pull_request"))
                        ? reader.GetInt32(reader.GetOrdinal("pull_request"))
                        : null,
                Model = grouping == LedgerGrouping.Model
                    ? reader.GetString(reader.GetOrdinal("model"))
                    : null,
                Agent = grouping == LedgerGrouping.Agent
                    ? reader.GetString(reader.GetOrdinal("agent"))
                    : null,
                SpanCount = reader.GetInt64(reader.GetOrdinal("span_count")),
                CacheReportedSpanCount = reader.GetInt64(reader.GetOrdinal("cache_reported_span_count")),
                InputTokens = reader.GetInt64(reader.GetOrdinal("input_tokens")),
                OutputTokens = reader.GetInt64(reader.GetOrdinal("output_tokens")),
                MaxInputTokens = reader.GetInt64(reader.GetOrdinal("max_input_tokens")),
                ReportedInputTokens = reader.GetInt64(reader.GetOrdinal("reported_input_tokens")),
                ReportedCacheReadInputTokens =
                    reader.GetInt64(reader.GetOrdinal("reported_cache_read_input_tokens")),
                ReportedCacheWriteInputTokens =
                    reader.GetInt64(reader.GetOrdinal("reported_cache_write_input_tokens")),
                ReportedUncachedInputTokens =
                    reader.GetInt64(reader.GetOrdinal("reported_uncached_input_tokens")),
                EstimatedUncachedInputTokens =
                    reader.GetInt64(reader.GetOrdinal("estimated_uncached_input_tokens")),
            });
        }

        return rows;
    }

    // Inserts links in one transaction and returns the number inserted. Existing (trace_id, span_id) rows are kept unchanged.
    // Throws InvalidOperationException if InitializeAsync hasn't run.
    public async Task<int> RecordSpanLinksAsync(
        IEnumerable<GenAiSpanLink> links,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        List<GenAiSpanLink> batch = [.. links];
        if (batch.Count == 0)
        {
            return 0;
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        int inserted = 0;
        long recordedAtUnixMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        foreach (GenAiSpanLink link in batch)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO span_links (
                    trace_id, span_id, parent_span_id, operation_name, agent_name,
                    conversation_id, git_repository_slug, git_branch, git_commit_sha,
                    start_time_unix_ms, tool_name, tool_exit_code, status_code, recorded_at_unix_ms
                ) VALUES (
                    @trace_id, @span_id, @parent_span_id, @operation_name, @agent_name,
                    @conversation_id, @git_repository_slug, @git_branch, @git_commit_sha,
                    @start_time_unix_ms, @tool_name, @tool_exit_code, @status_code, @recorded_at_unix_ms
                ) ON CONFLICT (trace_id, span_id) DO NOTHING;
                """;
            command.Parameters.AddWithValue("@trace_id", link.TraceId);
            command.Parameters.AddWithValue("@span_id", link.SpanId);
            command.Parameters.AddWithValue("@parent_span_id", OrDbNull(link.ParentSpanId));
            command.Parameters.AddWithValue("@operation_name", OrDbNull(link.OperationName));
            command.Parameters.AddWithValue("@agent_name", OrDbNull(link.AgentName));
            command.Parameters.AddWithValue(
                "@conversation_id",
                OrDbNull(link.ConversationId));
            command.Parameters.AddWithValue(
                "@git_repository_slug",
                OrDbNull(link.GitRepositorySlug));
            command.Parameters.AddWithValue("@git_branch", OrDbNull(link.GitBranch));
            command.Parameters.AddWithValue("@git_commit_sha", OrDbNull(link.GitCommitSha));
            command.Parameters.AddWithValue(
                "@start_time_unix_ms",
                link.StartTime.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("@tool_name", OrDbNull(link.ToolName));
            command.Parameters.AddWithValue("@tool_exit_code", OrDbNull(link.ToolExitCode));
            command.Parameters.AddWithValue("@status_code", link.StatusCode);
            command.Parameters.AddWithValue("@recorded_at_unix_ms", recordedAtUnixMs);
            inserted += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    // One trace's breaker inputs, from the five queries below on one connection. Throws ArgumentException for a blank traceId.
    public async Task<TraceActivity> GetTraceActivityAsync(
        string traceId,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(traceId))
        {
            throw new ArgumentException("Trace ID cannot be blank.", nameof(traceId));
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        string? conversationId = null;
        string repository = "(none)";
        string branch = "(none)";
        string? repositoryRoot = null;
        long spanCount = 0;
        await using (SqliteCommand summary = connection.CreateCommand())
        {
            summary.CommandText = """
                SELECT MAX(conversation_id), MAX(COALESCE(repo_slug, repo_root, '(none)')), MAX(COALESCE(branch_name, '(none)')), MAX(repo_root), COUNT(*) FROM spans WHERE trace_id = @trace AND accounting_state = 'effective';
                """;
            summary.Parameters.AddWithValue("@trace", traceId);
            await using SqliteDataReader reader = await summary.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                conversationId = reader.IsDBNull(0) ? null : reader.GetString(0);
                spanCount = reader.GetInt64(4);
                if (spanCount > 0)
                {
                    repository = reader.GetString(1);
                    branch = reader.GetString(2);
                }

                repositoryRoot = reader.IsDBNull(3) ? null : reader.GetString(3);
            }
        }

        if (conversationId is null)
        {
            await using SqliteCommand query = connection.CreateCommand();
            query.CommandText = """
                SELECT MAX(conversation_id) FROM span_links WHERE trace_id = @trace;
                """;
            query.Parameters.AddWithValue("@trace", traceId);
            object? value = await query.ExecuteScalarAsync(cancellationToken);
            conversationId = value is null or DBNull ? null : (string)value;
        }

        List<CostEvent> costs = [];
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = """
                SELECT span_id, end_time_unix_ms, cost_usd_pico FROM spans WHERE trace_id = @trace AND cost_usd_pico > 0 AND accounting_state = 'effective' ORDER BY end_time_unix_ms, span_id;
                """;
            query.Parameters.AddWithValue("@trace", traceId);
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                costs.Add(new CostEvent(
                    reader.GetString(0),
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
                    PicoUsd.ToUsd(reader.GetInt64(2))));
            }
        }

        List<ToolCallEvent> toolCalls = [];
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = $"""
                SELECT span_id, start_time_unix_ms, tool_name, tool_exit_code, status_code FROM span_links WHERE trace_id = @trace AND operation_name = 'execute_tool' AND {EffectiveLinkSql} ORDER BY start_time_unix_ms, span_id;
                """;
            query.Parameters.AddWithValue("@trace", traceId);
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                toolCalls.Add(new ToolCallEvent(
                    reader.GetString(0),
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    reader.GetInt32(4)));
            }
        }

        List<CommitObservation> commits = [];
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = """
                SELECT span_id, start_time_unix_ms, head_commit_sha FROM spans WHERE trace_id = @trace AND head_commit_sha IS NOT NULL AND accounting_state = 'effective' ORDER BY start_time_unix_ms, span_id;
                """;
            query.Parameters.AddWithValue("@trace", traceId);
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                commits.Add(new CommitObservation(
                    reader.GetString(0),
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
                    reader.GetString(2)));
            }
        }

        return new TraceActivity
        {
            TraceId = traceId,
            ConversationId = conversationId,
            Repository = repository,
            Branch = branch,
            RepositoryRoot = repositoryRoot,
            SpanCount = spanCount,
            Costs = costs,
            ToolCalls = toolCalls,
            Commits = commits,
        };
    }

    // Inserts the trip unless (Rule, TraceId) already has one, and returns the new id, or null when it was skipped.
    // Throws ArgumentException for a blank TraceId, Repository, Branch, or Detail; ArgumentOutOfRangeException for a negative WindowCostUsd.
    public async Task<long?> RecordBreakerTripAsync(
        NewBreakerTrip trip,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(trip.TraceId))
        {
            throw new ArgumentException("Trace ID cannot be blank.", nameof(trip));
        }

        if (string.IsNullOrWhiteSpace(trip.Repository))
        {
            throw new ArgumentException("Repository cannot be blank.", nameof(trip));
        }

        if (string.IsNullOrWhiteSpace(trip.Branch))
        {
            throw new ArgumentException("Branch cannot be blank.", nameof(trip));
        }

        if (string.IsNullOrWhiteSpace(trip.Detail))
        {
            throw new ArgumentException("Detail cannot be blank.", nameof(trip));
        }

        if (trip.WindowCostUsd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trip));
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO breaker_trips (rule, trace_id, conversation_id, repository, branch, repo_root, tripped_at_unix_ms, detail, window_cost_usd_pico, window_minutes, failure_count, recorded_at_unix_ms) VALUES (@rule, @trace_id, @conversation_id, @repository, @branch, @repo_root, @tripped_at_unix_ms, @detail, @window_cost_usd_pico, @window_minutes, @failure_count, @now) ON CONFLICT (rule, trace_id) DO NOTHING RETURNING id;
            """;
        command.Parameters.AddWithValue("@rule", BreakerRuleNames.ToName(trip.Rule));
        command.Parameters.AddWithValue("@trace_id", trip.TraceId);
        command.Parameters.AddWithValue("@conversation_id", OrDbNull(trip.ConversationId));
        command.Parameters.AddWithValue("@repository", trip.Repository);
        command.Parameters.AddWithValue("@branch", trip.Branch);
        command.Parameters.AddWithValue("@repo_root", OrDbNull(trip.RepositoryRoot));
        command.Parameters.AddWithValue("@tripped_at_unix_ms", trip.TrippedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@detail", trip.Detail);
        object windowCostUsdPico = trip.WindowCostUsd.HasValue
            ? PicoUsd.FromUsd(trip.WindowCostUsd.Value)
            : DBNull.Value;
        command.Parameters.AddWithValue("@window_cost_usd_pico", windowCostUsdPico);
        command.Parameters.AddWithValue("@window_minutes", OrDbNull(trip.WindowMinutes));
        command.Parameters.AddWithValue("@failure_count", OrDbNull(trip.FailureCount));
        command.Parameters.AddWithValue("@now", _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : (long)value;
    }

    // Open trips (reset_at_unix_ms IS NULL) that match every non-null filter property, newest first.
    public async Task<IReadOnlyList<BreakerTrip>> GetOpenBreakerTripsAsync(
        BreakerTripFilter filter,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, rule, trace_id, conversation_id, repository, branch, repo_root, tripped_at_unix_ms, detail, window_cost_usd_pico, window_minutes, failure_count, recorded_at_unix_ms, notified_at_unix_ms, reset_at_unix_ms FROM breaker_trips WHERE reset_at_unix_ms IS NULL AND (@id IS NULL OR id = @id) AND (@repository IS NULL OR repository = @repository) AND (@branch IS NULL OR branch = @branch) AND (@repo_root IS NULL OR repo_root = @repo_root) ORDER BY tripped_at_unix_ms DESC, id DESC;
            """;
        AddTripFilterParameters(command, filter);
        return await ReadTripsAsync(command, cancellationToken);
    }

    // Resets the open trips that match the filter and returns them, with ResetAt set, in ascending Id order.
    public async Task<IReadOnlyList<BreakerTrip>> ResetBreakerTripsAsync(
        BreakerTripFilter filter,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE breaker_trips SET reset_at_unix_ms = @now WHERE reset_at_unix_ms IS NULL AND (@id IS NULL OR id = @id) AND (@repository IS NULL OR repository = @repository) AND (@branch IS NULL OR branch = @branch) AND (@repo_root IS NULL OR repo_root = @repo_root) RETURNING id, rule, trace_id, conversation_id, repository, branch, repo_root, tripped_at_unix_ms, detail, window_cost_usd_pico, window_minutes, failure_count, recorded_at_unix_ms, notified_at_unix_ms, reset_at_unix_ms;
            """;
        AddTripFilterParameters(command, filter);
        command.Parameters.AddWithValue("@now", _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        List<BreakerTrip> trips = [.. await ReadTripsAsync(command, cancellationToken)];
        trips.Sort((left, right) => left.Id.CompareTo(right.Id));
        return trips;
    }

    // Sets notified_at_unix_ms when it's NULL. Returns true when a row changed.
    public async Task<bool> MarkBreakerTripNotifiedAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE breaker_trips SET notified_at_unix_ms = @now WHERE id = @id AND notified_at_unix_ms IS NULL;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@now", _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    // Open, never-notified trips recorded at or after recordedSince, oldest first.
    public async Task<IReadOnlyList<BreakerTrip>> GetUnnotifiedBreakerTripsAsync(
        DateTimeOffset recordedSince,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, rule, trace_id, conversation_id, repository, branch, repo_root, tripped_at_unix_ms, detail, window_cost_usd_pico, window_minutes, failure_count, recorded_at_unix_ms, notified_at_unix_ms, reset_at_unix_ms FROM breaker_trips WHERE reset_at_unix_ms IS NULL AND notified_at_unix_ms IS NULL AND recorded_at_unix_ms >= @since ORDER BY recorded_at_unix_ms, id;
            """;
        command.Parameters.AddWithValue("@since", recordedSince.ToUnixTimeMilliseconds());
        return await ReadTripsAsync(command, cancellationToken);
    }

    // Trips, open or reset, whose repository and branch keys equal the arguments (ordinal).
    public async Task<long> CountBreakerTripsAsync(
        string repository,
        string branch,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM breaker_trips WHERE repository = @repository AND branch = @branch;
            """;
        command.Parameters.AddWithValue("@repository", repository);
        command.Parameters.AddWithValue("@branch", branch);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    // The settings row; BreakerSettings.Default if the row is missing.
    public async Task<BreakerSettings> GetBreakerSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT velocity_threshold_usd_pico, velocity_window_minutes, churn_failures, flag_file, notifications FROM breaker_settings WHERE id = 1;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return BreakerSettings.Default;
        }

        return new BreakerSettings
        {
            VelocityCredits = PicoUsd.ToUsd(reader.GetInt64(0)) / AiCreditMath.UsdPerAiCredit,
            VelocityWindowMinutes = reader.GetInt32(1),
            ChurnFailures = reader.GetInt32(2),
            FlagFile = reader.GetInt64(3) != 0,
            Notifications = reader.GetInt64(4) != 0,
        };
    }

    // Throws ArgumentNullException for null; ArgumentOutOfRangeException when !settings.IsValid, without writing.
    public async Task UpdateBreakerSettingsAsync(
        BreakerSettings settings,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(settings));
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO breaker_settings (id, velocity_threshold_usd_pico, velocity_window_minutes, churn_failures, flag_file, notifications, updated_at_unix_ms) VALUES (1, @threshold, @minutes, @failures, @flag_file, @notifications, @now) ON CONFLICT (id) DO UPDATE SET velocity_threshold_usd_pico = excluded.velocity_threshold_usd_pico, velocity_window_minutes = excluded.velocity_window_minutes, churn_failures = excluded.churn_failures, flag_file = excluded.flag_file, notifications = excluded.notifications, updated_at_unix_ms = excluded.updated_at_unix_ms;
            """;
        command.Parameters.AddWithValue("@threshold", PicoUsd.FromUsd(settings.VelocityThresholdUsd));
        command.Parameters.AddWithValue("@minutes", settings.VelocityWindowMinutes);
        command.Parameters.AddWithValue("@failures", settings.ChurnFailures);
        command.Parameters.AddWithValue("@flag_file", settings.FlagFile ? 1 : 0);
        command.Parameters.AddWithValue("@notifications", settings.Notifications ? 1 : 0);
        command.Parameters.AddWithValue("@now", _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Inserts or replaces the budget and clears alerted_at and notified_at. Throws ArgumentException for a blank key, and
    // ArgumentOutOfRangeException unless 0 < aiCredits <= 1,000,000 and aiCredits % 0.01m == 0m.
    public async Task SetBranchBudgetAsync(
        string repository,
        string branch,
        decimal aiCredits,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ValidateBranchBudgetKey(repository, branch);
        if (aiCredits <= 0 || aiCredits > 1_000_000m || aiCredits % 0.01m != 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(aiCredits));
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO branch_budgets (repository, branch, budget_usd_pico, set_at_unix_ms, alerted_at_unix_ms, notified_at_unix_ms) VALUES (@repository, @branch, @budget_usd_pico, @now, NULL, NULL) ON CONFLICT (repository, branch) DO UPDATE SET budget_usd_pico = excluded.budget_usd_pico, set_at_unix_ms = excluded.set_at_unix_ms, alerted_at_unix_ms = NULL, notified_at_unix_ms = NULL;
            """;
        command.Parameters.AddWithValue("@repository", repository);
        command.Parameters.AddWithValue("@branch", branch);
        command.Parameters.AddWithValue(
            "@budget_usd_pico",
            PicoUsd.FromUsd(aiCredits * AiCreditMath.UsdPerAiCredit));
        command.Parameters.AddWithValue("@now", _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Returns true when a budget was deleted.
    public async Task<bool> ClearBranchBudgetAsync(
        string repository,
        string branch,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM branch_budgets WHERE repository = @repository AND branch = @branch;
            """;
        command.Parameters.AddWithValue("@repository", repository);
        command.Parameters.AddWithValue("@branch", branch);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    // Budgets with their spend, ordered by repository, then branch (SQLite BINARY collation). A null argument matches every value.
    public async Task<IReadOnlyList<BranchBudgetRow>> GetBranchBudgetsAsync(
        string? repository = null,
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            {BranchBudgetSelectSql}
            WHERE (@repository IS NULL OR b.repository = @repository) AND (@branch IS NULL OR b.branch = @branch) ORDER BY b.repository, b.branch;
            """;
        command.Parameters.AddWithValue("@repository", OrDbNull(repository));
        command.Parameters.AddWithValue("@branch", OrDbNull(branch));
        return await ReadBranchBudgetsAsync(command, cancellationToken);
    }

    // Sets alerted_at_unix_ms when it's NULL and the key's spend, summed in the same statement, exceeds the stored budget.
    // Returns true when a row changed. Checking in the UPDATE means a budget re-set after the caller's read is never marked.
    public async Task<bool> MarkBranchBudgetAlertedAsync(
        string repository,
        string branch,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE branch_budgets SET alerted_at_unix_ms = @now WHERE repository = @repository AND branch = @branch AND alerted_at_unix_ms IS NULL AND budget_usd_pico < (SELECT COALESCE(SUM(cost_usd_pico), 0) FROM spans WHERE COALESCE(repo_slug, repo_root, '(none)') = @repository AND COALESCE(branch_name, '(none)') = @branch AND accounting_state = 'effective');
            """;
        command.Parameters.AddWithValue("@repository", repository);
        command.Parameters.AddWithValue("@branch", branch);
        command.Parameters.AddWithValue("@now", _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    // Sets notified_at_unix_ms when alerted_at_unix_ms is set and notified_at_unix_ms is NULL. Returns true when a row changed.
    public async Task<bool> MarkBranchBudgetNotifiedAsync(
        string repository,
        string branch,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE branch_budgets SET notified_at_unix_ms = @now WHERE repository = @repository AND branch = @branch AND alerted_at_unix_ms IS NOT NULL AND notified_at_unix_ms IS NULL;
            """;
        command.Parameters.AddWithValue("@repository", repository);
        command.Parameters.AddWithValue("@branch", branch);
        command.Parameters.AddWithValue("@now", _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    // Budgets alerted at or after alertedSince and never notified, ordered by alerted_at, then repository, then branch.
    public async Task<IReadOnlyList<BranchBudgetRow>> GetUnnotifiedBudgetAlertsAsync(
        DateTimeOffset alertedSince,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.repository, b.branch, b.budget_usd_pico, b.set_at_unix_ms, b.alerted_at_unix_ms, b.notified_at_unix_ms,
                   (SELECT COALESCE(SUM(s.cost_usd_pico), 0) FROM spans AS s
                     WHERE COALESCE(s.repo_slug, s.repo_root, '(none)') = b.repository
                       AND COALESCE(s.branch_name, '(none)') = b.branch
                       AND s.accounting_state = 'effective') AS spent_usd_pico
            FROM branch_budgets AS b
            WHERE b.alerted_at_unix_ms IS NOT NULL AND b.alerted_at_unix_ms >= @since AND b.notified_at_unix_ms IS NULL ORDER BY b.alerted_at_unix_ms, b.repository, b.branch;
            """;
        command.Parameters.AddWithValue("@since", alertedSince.ToUnixTimeMilliseconds());
        return await ReadBranchBudgetsAsync(command, cancellationToken);
    }

    // For each given trace with at least one failed execute_tool link (an exit code other than 0, or status 2), that count.
    // Traces without failures are absent. An empty input returns an empty dictionary without a query.
    public async Task<IReadOnlyDictionary<string, long>> GetFailedToolCallCountsAsync(
        IEnumerable<string> traceIds,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        string[] traces = traceIds.Distinct(StringComparer.Ordinal).ToArray();
        if (traces.Length == 0)
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        string parameters = string.Join(
            ", ",
            Enumerable.Range(0, traces.Length).Select(index => $"@t{index}"));
        command.CommandText = $"""
            SELECT trace_id, COUNT(*) FROM span_links WHERE trace_id IN ({parameters}) AND operation_name = 'execute_tool' AND ((tool_exit_code IS NOT NULL AND tool_exit_code <> 0) OR status_code = 2) AND {EffectiveLinkSql} GROUP BY trace_id;
            """;
        for (int index = 0; index < traces.Length; index++)
        {
            command.Parameters.AddWithValue($"@t{index}", traces[index]);
        }

        Dictionary<string, long> counts = new(StringComparer.Ordinal);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts.Add(reader.GetString(0), reader.GetInt64(1));
        }

        return counts;
    }

    // For each trace, copies each invoke_agent link's span ID, agent name, conversation id, and Git slug/branch/sha onto spans
    // rows below it, without crossing a nested invoke_agent, so agent_span_id is each row's nearest invoke_agent ancestor.
    // Returns the total rows changed by all UPDATE statements. One transaction.
    // Throws InvalidOperationException if InitializeAsync hasn't run.
    public async Task<int> BackfillFromAgentSpansAsync(
        IEnumerable<string> traceIds,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        string[] traces = traceIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (traces.Length == 0)
        {
            return 0;
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        int changed = 0;
        foreach (string trace in traces)
        {
            List<AgentLink> agents = [];
            await using (SqliteCommand read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = """
                    SELECT span_id, agent_name, conversation_id, git_repository_slug, git_branch, git_commit_sha FROM span_links WHERE trace_id = @trace AND operation_name = 'invoke_agent' ORDER BY span_id;
                    """;
                read.Parameters.AddWithValue("@trace", trace);
                await using SqliteDataReader reader =
                    await read.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    agents.Add(new AgentLink(
                        reader.GetString(0),
                        reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4),
                        reader.IsDBNull(5) ? null : reader.GetString(5)));
                }
            }

            foreach (AgentLink agent in agents)
            {
                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = """
                        WITH RECURSIVE sub(span_id) AS (
                            SELECT span_id FROM span_links
                             WHERE trace_id = @trace AND parent_span_id = @agentSpanId
                               AND (operation_name IS NULL OR operation_name <> 'invoke_agent')
                            UNION
                            SELECT l.span_id FROM span_links AS l JOIN sub ON l.parent_span_id = sub.span_id
                             WHERE l.trace_id = @trace
                               AND (l.operation_name IS NULL OR l.operation_name <> 'invoke_agent')
                        )
                        UPDATE spans
                           SET agent_name      = COALESCE(agent_name, @agentName),
                               conversation_id = COALESCE(conversation_id, @conversationId),
                               agent_span_id   = COALESCE(agent_span_id, @agentSpanId)
                         WHERE trace_id = @trace
                           AND span_id IN (SELECT span_id FROM sub)
                           AND ((agent_name IS NULL AND @agentName IS NOT NULL)
                             OR (conversation_id IS NULL AND @conversationId IS NOT NULL)
                             OR agent_span_id IS NULL);
                        """;
                    command.Parameters.AddWithValue("@trace", trace);
                    command.Parameters.AddWithValue("@agentSpanId", agent.SpanId);
                    command.Parameters.AddWithValue("@agentName", OrDbNull(agent.AgentName));
                    command.Parameters.AddWithValue(
                        "@conversationId",
                        OrDbNull(agent.ConversationId));
                    changed += await command.ExecuteNonQueryAsync(cancellationToken);
                }

                if (agent.GitRepositorySlug is not null)
                {
                    await using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = """
                        WITH RECURSIVE sub(span_id) AS (
                            SELECT span_id FROM span_links
                             WHERE trace_id = @trace AND parent_span_id = @agentSpanId
                               AND (operation_name IS NULL OR operation_name <> 'invoke_agent')
                            UNION
                            SELECT l.span_id FROM span_links AS l JOIN sub ON l.parent_span_id = sub.span_id
                             WHERE l.trace_id = @trace
                               AND (l.operation_name IS NULL OR l.operation_name <> 'invoke_agent')
                        )
                        UPDATE spans
                           SET repo_slug       = @slug,
                               branch_name     = @branch,
                               head_commit_sha = @sha
                         WHERE trace_id = @trace
                           AND span_id IN (SELECT span_id FROM sub)
                           AND repo_root IS NULL AND repo_slug IS NULL;
                        """;
                    command.Parameters.AddWithValue("@trace", trace);
                    command.Parameters.AddWithValue("@agentSpanId", agent.SpanId);
                    command.Parameters.AddWithValue("@slug", agent.GitRepositorySlug);
                    command.Parameters.AddWithValue("@branch", OrDbNull(agent.GitBranch));
                    command.Parameters.AddWithValue("@sha", OrDbNull(agent.GitCommitSha));
                    changed += await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            // A live row whose conversation was just assigned follows the authority rules as if it had arrived with it.
            foreach (string conversation in agents
                .Select(agent => agent.ConversationId)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal))
            {
                await UsageAuthority.ReconcileConversationAsync(transaction, conversation, null, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    // Deletes span_links rows whose start_time_unix_ms < startedBefore.ToUnixTimeMilliseconds(). Returns the rows deleted.
    public async Task<int> PruneSpanLinksAsync(
        DateTimeOffset startedBefore,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM span_links WHERE start_time_unix_ms < @before;
            """;
        command.Parameters.AddWithValue(
            "@before",
            startedBefore.ToUnixTimeMilliseconds());
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Prompt chains (one per trace_id), most expensive first. Throws ArgumentOutOfRangeException unless 1 <= limit <= 1000.
    public Task<IReadOnlyList<PromptChainRow>> GetTopPromptChainsAsync(
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetTopPromptChainsAsync(filter, limit, null, cancellationToken);

    // whatIf as in GetRollupAsync. The chains and their order don't depend on it.
    public async Task<IReadOnlyList<PromptChainRow>> GetTopPromptChainsAsync(
        LedgerFilter filter,
        int limit,
        JsonRateCardCalculator? whatIf,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);

        // One read transaction, so every query sees the same spans.
        await using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
        return await ReadTopPromptChainsAsync(snapshot, filter, limit, whatIf, cancellationToken);
    }

    // GetTopPromptChainsAsync's queries, in a read transaction that the caller owns.
    internal static async Task<IReadOnlyList<PromptChainRow>> ReadTopPromptChainsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        int limit,
        JsonRateCardCalculator? whatIf,
        CancellationToken cancellationToken)
    {
        SqliteConnection connection = snapshot.Connection!;
        List<PromptChainData> data = [];
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = snapshot;
            query.CommandText = $"""
                SELECT trace_id,
                       MIN(start_time_unix_ms)                                                            AS start_ms,
                       MAX(end_time_unix_ms)                                                              AS end_ms,
                       MAX(conversation_id)                                                               AS conversation_id,
                       MAX(COALESCE(repo_slug, repo_root, '(none)'))                                      AS repository,
                       MAX(COALESCE(branch_name, '(none)'))                                               AS branch,
                       COUNT(*)                                                                           AS span_count,
                       SUM(CASE WHEN pricing_status IN ('priced', 'local_offload') THEN 0 ELSE 1 END)     AS unpriced_span_count,
                       SUM(input_tokens)                                                                  AS input_tokens,
                       SUM(output_tokens)                                                                 AS output_tokens,
                       SUM(cache_read_input_tokens)                                                       AS cache_read_input_tokens,
                       SUM(cache_write_input_tokens)                                                      AS cache_write_input_tokens,
                       SUM(CASE WHEN billing_channel = 'github_ai_credits' THEN cost_usd_pico ELSE 0 END) AS github_usd_pico,
                       SUM(CASE WHEN billing_channel = 'foundry_byok' THEN cost_usd_pico ELSE 0 END)      AS byok_usd_pico,
                       SUM(saved_usd_pico)                                                                AS saved_usd_pico,
                       SUM(api_equivalent_usd_pico)                                                       AS api_equivalent_usd_pico,
                       SUM(CASE WHEN api_equivalent_usd_pico IS NULL THEN 1 ELSE 0 END)                   AS api_equivalent_unpriced_span_count,
                       SUM(CASE WHEN api_equivalent_usd_pico IS NOT NULL AND NOT {ReportedSql} THEN 1 ELSE 0 END)
                                                                                                          AS api_equivalent_estimated_span_count
                FROM spans
                WHERE {FilterSql}
                GROUP BY trace_id
                ORDER BY github_usd_pico + byok_usd_pico DESC, saved_usd_pico DESC, start_ms DESC, trace_id ASC
                LIMIT @limit;
                """;
            AddFilterParameters(query, filter);
            query.Parameters.AddWithValue("@limit", limit);
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                data.Add(new PromptChainData(
                    reader.GetString(reader.GetOrdinal("trace_id")),
                    reader.IsDBNull(reader.GetOrdinal("conversation_id"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("conversation_id")),
                    reader.GetInt64(reader.GetOrdinal("start_ms")),
                    reader.GetInt64(reader.GetOrdinal("end_ms")),
                    reader.GetString(reader.GetOrdinal("repository")),
                    reader.GetString(reader.GetOrdinal("branch")),
                    reader.GetInt64(reader.GetOrdinal("span_count")),
                    reader.GetInt64(reader.GetOrdinal("unpriced_span_count")),
                    reader.GetInt64(reader.GetOrdinal("input_tokens")),
                    reader.GetInt64(reader.GetOrdinal("output_tokens")),
                    reader.GetInt64(reader.GetOrdinal("cache_read_input_tokens")),
                    reader.GetInt64(reader.GetOrdinal("cache_write_input_tokens")),
                    reader.GetInt64(reader.GetOrdinal("github_usd_pico")),
                    reader.GetInt64(reader.GetOrdinal("byok_usd_pico")),
                    reader.GetInt64(reader.GetOrdinal("saved_usd_pico")),
                    ReadUsd(reader, "api_equivalent_usd_pico"),
                    reader.GetInt64(reader.GetOrdinal("api_equivalent_unpriced_span_count")),
                    reader.GetInt64(reader.GetOrdinal("api_equivalent_estimated_span_count"))));
            }
        }

        if (data.Count == 0)
        {
            return [];
        }

        Dictionary<string, List<string>> agents = data.ToDictionary(
            row => row.TraceId,
            _ => new List<string>(),
            StringComparer.Ordinal);
        Dictionary<string, List<string>> models = data.ToDictionary(
            row => row.TraceId,
            _ => new List<string>(),
            StringComparer.Ordinal);
        string traceParameters = string.Join(
            ", ",
            Enumerable.Range(0, data.Count).Select(index => $"@t{index}"));
        void AddTraceParameters(SqliteCommand query)
        {
            AddFilterParameters(query, filter);
            for (int index = 0; index < data.Count; index++)
            {
                query.Parameters.AddWithValue($"@t{index}", data[index].TraceId);
            }
        }

        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = snapshot;
            query.CommandText = $"""
                SELECT DISTINCT trace_id, agent_name, model_id
                FROM spans
                WHERE {FilterSql}
                  AND trace_id IN ({traceParameters});
                """;
            AddTraceParameters(query);

            await using SqliteDataReader reader =
                await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                string trace = reader.GetString(0);
                if (!reader.IsDBNull(1))
                {
                    agents[trace].Add(reader.GetString(1));
                }

                if (!reader.IsDBNull(2))
                {
                    models[trace].Add(reader.GetString(2));
                }
            }
        }

        Dictionary<string, RetailTotals>? repriced = whatIf is null
            ? null
            : await RepriceAsync(
                snapshot,
                "trace_id",
                "trace_id",
                $"{FilterSql} AND trace_id IN ({traceParameters})",
                AddTraceParameters,
                whatIf,
                cancellationToken);
        List<PromptChainRow> rows = [];
        foreach (PromptChainData item in data)
        {
            decimal aiCreditsUsd = PicoUsd.ToUsd(item.GitHubUsdPico);
            RetailTotals retail = repriced is null
                ? new RetailTotals(
                    item.ApiEquivalentUsd,
                    item.ApiEquivalentUnpricedSpanCount,
                    item.ApiEquivalentEstimatedSpanCount)
                : repriced[GroupKey([item.TraceId])];
            rows.Add(new PromptChainRow
            {
                TraceId = item.TraceId,
                ConversationId = item.ConversationId,
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(item.StartMs),
                EndTime = DateTimeOffset.FromUnixTimeMilliseconds(item.EndMs),
                Repository = item.Repository,
                Branch = item.Branch,
                Agents = agents[item.TraceId].Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal).ToArray(),
                Models = models[item.TraceId].Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal).ToArray(),
                SpanCount = item.SpanCount,
                UnpricedSpanCount = item.UnpricedSpanCount,
                InputTokens = item.InputTokens,
                OutputTokens = item.OutputTokens,
                CacheReadInputTokens = item.CacheReadInputTokens,
                CacheWriteInputTokens = item.CacheWriteInputTokens,
                AiCredits = AiCreditMath.UsdToAiCredits(aiCreditsUsd),
                AiCreditsUsd = aiCreditsUsd,
                FoundryByokUsd = PicoUsd.ToUsd(item.ByokUsdPico),
                LocalSavedUsd = PicoUsd.ToUsd(item.SavedUsdPico),
                ApiEquivalentUsd = retail.Usd,
                ApiEquivalentUnpricedSpanCount = retail.UnpricedSpanCount,
                ApiEquivalentEstimatedSpanCount = retail.EstimatedSpanCount,
            });
        }

        return rows;
    }

    // Distinct trace_id values (prompts) among the spans that match the filter.
    public async Task<long> GetPromptCountAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await ReadPromptCountAsync(connection, null, filter, cancellationToken);
    }

    // GetPromptCountAsync's query on connection, in transaction when it isn't null.
    internal static async Task<long> ReadPromptCountAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT COUNT(DISTINCT trace_id)
            FROM spans
            WHERE {FilterSql};
            """;
        AddFilterParameters(command, filter);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    // The selector's inference spans that match filter, by start time, then span ID, then trace ID. invoke_agent spans are
    // never stored as spans, so they're never listed. A span's input delta compares it with the previous span of its
    // sequence (SequenceSql); it's null for a sequence's first span and for a span without a known invoke_agent ancestor.
    // Deltas are computed over all the selector's spans before filter applies.
    // Throws ArgumentNullException for a null selector or filter.
    public async Task<IReadOnlyList<TurnTokenRow>> GetTurnBreakdownAsync(
        TurnSelector selector,
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(filter);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await ReadTurnBreakdownAsync(connection, null, selector, filter, cancellationToken);
    }

    // GetTurnBreakdownAsync's query on connection, in transaction when it isn't null.
    internal static async Task<IReadOnlyList<TurnTokenRow>> ReadTurnBreakdownAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        TurnSelector selector,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            WITH selected AS (
                SELECT *,
                       CASE WHEN agent_span_id IS NULL THEN NULL
                            ELSE input_tokens - LAG(input_tokens) OVER (
                                PARTITION BY {SequenceSql}
                                ORDER BY start_time_unix_ms, span_id)
                       END AS input_delta
                FROM spans
                WHERE (@trace IS NULL OR trace_id = @trace)
                  AND (@conversation IS NULL OR conversation_id = @conversation)
                  AND accounting_state = 'effective'
            )
            SELECT trace_id, span_id, parent_span_id, conversation_id, agent_span_id, start_time_unix_ms,
                   COALESCE(repo_slug, repo_root, '(none)') AS repository,
                   COALESCE(branch_name, '(none)')          AS branch,
                   pull_request_number, agent_name,
                   COALESCE(model_id, '(none)')             AS model,
                   input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens,
                   cache_read_state, cache_write_state, input_delta
            FROM selected
            WHERE {FilterSql}
            ORDER BY start_time_unix_ms, span_id, trace_id;
            """;
        command.Parameters.AddWithValue("@trace", OrDbNull(selector.TraceId));
        command.Parameters.AddWithValue("@conversation", OrDbNull(selector.ConversationId));
        AddFilterParameters(command, filter);

        List<TurnTokenRow> rows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            CacheCounterState readState = ToCacheCounterState(reader.GetString(15));
            CacheCounterState writeState = ToCacheCounterState(reader.GetString(16));
            bool reported = CacheMetrics.IsReported(readState, writeState);
            rows.Add(new TurnTokenRow
            {
                TraceId = reader.GetString(0),
                SpanId = reader.GetString(1),
                ParentSpanId = reader.IsDBNull(2) ? null : reader.GetString(2),
                ConversationId = reader.IsDBNull(3) ? null : reader.GetString(3),
                AgentSpanId = reader.IsDBNull(4) ? null : reader.GetString(4),
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
                Repository = reader.GetString(6),
                Branch = reader.GetString(7),
                PullRequestNumber = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Agent = reader.IsDBNull(9) ? null : reader.GetString(9),
                Model = reader.GetString(10),
                InputTokens = reader.GetInt64(11),
                OutputTokens = reader.GetInt64(12),
                CacheReadState = readState,
                CacheWriteState = writeState,
                CacheReadInputTokens = reported ? reader.GetInt64(13) : null,
                CacheWriteInputTokens = reported ? reader.GetInt64(14) : null,
                InputDeltaFromPreviousTurn = reader.IsDBNull(17) ? null : reader.GetInt64(17),
            });
        }

        return rows;
    }

    // Ranks the prompt chains that match filter, limit of each ranking; see ContextBloatReport. A chain's growth is its
    // sequence (SequenceSql) with the largest peak input less first input, by start time then span ID, among the spans
    // with a known invoke_agent ancestor; ties between a chain's sequences go to the lowest agent span ID, then model.
    // Throws ArgumentOutOfRangeException unless 1 <= limit <= 1000.
    public async Task<ContextBloatReport> GetContextBloatAsync(
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        IReadOnlyList<BloatChainRow> complete = await ReadBloatChainsAsync(
            connection,
            filter,
            limit,
            "=",
            "reported_uncached_input_tokens",
            cancellationToken);
        IReadOnlyList<BloatChainRow> incomplete = await ReadBloatChainsAsync(
            connection,
            filter,
            limit,
            "<",
            "estimated_uncached_input_tokens",
            cancellationToken);

        List<InputGrowthRow> growth = [];
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = $"""
                WITH turns AS (
                    SELECT trace_id, agent_span_id, conversation_id, agent_name, start_time_unix_ms, input_tokens,
                           COALESCE(repo_slug, repo_root, '(none)') AS repository,
                           COALESCE(branch_name, '(none)')          AS branch,
                           COALESCE(conversation_id, '(none)')      AS conversation,
                           COALESCE(model_id, '(none)')             AS model,
                           FIRST_VALUE(input_tokens) OVER (
                               PARTITION BY {SequenceSql}
                               ORDER BY start_time_unix_ms, span_id) AS first_input
                    FROM spans
                    WHERE {FilterSql} AND agent_span_id IS NOT NULL
                ),
                sequences AS (
                    SELECT trace_id, agent_span_id, repository, branch, conversation, model,
                           MAX(conversation_id)    AS conversation_id,
                           MAX(agent_name)         AS agent,
                           MIN(start_time_unix_ms) AS start_ms,
                           COUNT(*)                AS turn_count,
                           MAX(first_input)        AS first_input,
                           MAX(input_tokens)       AS peak_input
                    FROM turns
                    GROUP BY trace_id, agent_span_id, repository, branch, conversation, model
                ),
                ranked AS (
                    SELECT *,
                           ROW_NUMBER() OVER (
                               PARTITION BY trace_id
                               ORDER BY peak_input - first_input DESC, agent_span_id, model, repository, branch, conversation
                           ) AS rank_in_trace
                    FROM sequences
                )
                SELECT trace_id, conversation_id, agent_span_id, agent, model, repository, branch,
                       start_ms, turn_count, first_input, peak_input
                FROM ranked
                WHERE rank_in_trace = 1 AND peak_input - first_input > 0
                ORDER BY peak_input - first_input DESC, trace_id ASC
                LIMIT @limit;
                """;
            AddFilterParameters(query, filter);
            query.Parameters.AddWithValue("@limit", limit);
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                growth.Add(new InputGrowthRow
                {
                    TraceId = reader.GetString(0),
                    ConversationId = reader.IsDBNull(1) ? null : reader.GetString(1),
                    AgentSpanId = reader.GetString(2),
                    Agent = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Model = reader.GetString(4),
                    Repository = reader.GetString(5),
                    Branch = reader.GetString(6),
                    StartTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7)),
                    TurnCount = reader.GetInt64(8),
                    FirstInputTokens = reader.GetInt64(9),
                    PeakInputTokens = reader.GetInt64(10),
                });
            }
        }

        long withoutAgent;
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = $"""
                SELECT COUNT(*) FROM spans WHERE {FilterSql} AND agent_span_id IS NULL;
                """;
            AddFilterParameters(query, filter);
            withoutAgent = (long)(await query.ExecuteScalarAsync(cancellationToken))!;
        }

        return new ContextBloatReport
        {
            ByUncachedInput = complete,
            ByEstimatedUncachedInput = incomplete,
            ByInputGrowth = growth,
            SpansWithoutAgent = withoutAgent,
        };
    }

    // A read snapshot for gh ledger export: one connection and one deferred transaction that every query of the export shares.
    // Its first read starts the snapshot, so rows recorded afterward never appear in it. Throws InvalidOperationException if
    // InitializeAsync hasn't run.
    public async Task<LedgerExportSnapshot> OpenExportSnapshotAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        try
        {
            SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
            await using (SqliteCommand pin = connection.CreateCommand())
            {
                pin.Transaction = transaction;
                pin.CommandText = "SELECT COUNT(*) FROM sqlite_schema;";
                await pin.ExecuteScalarAsync(cancellationToken);
            }

            return new LedgerExportSnapshot(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<int> GetUserVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddTripFilterParameters(
        SqliteCommand command,
        BreakerTripFilter filter)
    {
        command.Parameters.AddWithValue("@id", OrDbNull(filter.Id));
        command.Parameters.AddWithValue("@repository", OrDbNull(filter.Repository));
        command.Parameters.AddWithValue("@branch", OrDbNull(filter.Branch));
        command.Parameters.AddWithValue("@repo_root", OrDbNull(filter.RepositoryRoot));
    }

    // The parameters of FilterSql.
    private static void AddFilterParameters(
        SqliteCommand command,
        LedgerFilter filter)
    {
        command.Parameters.AddWithValue("@from", OrDbNull(filter.From?.ToUnixTimeMilliseconds()));
        command.Parameters.AddWithValue("@to", OrDbNull(filter.To?.ToUnixTimeMilliseconds()));
        command.Parameters.AddWithValue("@repository", OrDbNull(filter.Repository));
        command.Parameters.AddWithValue("@branch", OrDbNull(filter.Branch));
        command.Parameters.AddWithValue("@pull_request", OrDbNull(filter.PullRequestNumber));
        command.Parameters.AddWithValue("@agent", OrDbNull(filter.Agent));
        command.Parameters.AddWithValue("@conversation_id", OrDbNull(filter.ConversationId));
    }

    private static async Task<IReadOnlyList<BreakerTrip>> ReadTripsAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        List<BreakerTrip> trips = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string ruleName = reader.GetString(1);
            if (!BreakerRuleNames.TryParse(ruleName, out BreakerRule rule))
            {
                throw new InvalidDataException($"Unknown breaker rule '{ruleName}'.");
            }

            trips.Add(new BreakerTrip
            {
                Id = reader.GetInt64(0),
                Rule = rule,
                TraceId = reader.GetString(2),
                ConversationId = reader.IsDBNull(3) ? null : reader.GetString(3),
                Repository = reader.GetString(4),
                Branch = reader.GetString(5),
                RepositoryRoot = reader.IsDBNull(6) ? null : reader.GetString(6),
                TrippedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7)),
                Detail = reader.GetString(8),
                WindowCostUsd = reader.IsDBNull(9) ? null : PicoUsd.ToUsd(reader.GetInt64(9)),
                WindowMinutes = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                FailureCount = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                RecordedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(12)),
                NotifiedAt = reader.IsDBNull(13)
                    ? null
                    : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(13)),
                ResetAt = reader.IsDBNull(14)
                    ? null
                    : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(14)),
            });
        }

        return trips;
    }

    private static async Task<IReadOnlyList<BranchBudgetRow>> ReadBranchBudgetsAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        List<BranchBudgetRow> rows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new BranchBudgetRow
            {
                Repository = reader.GetString(0),
                Branch = reader.GetString(1),
                BudgetAiCredits =
                    PicoUsd.ToUsd(reader.GetInt64(2)) / AiCreditMath.UsdPerAiCredit,
                SetAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)),
                AlertedAt = reader.IsDBNull(4)
                    ? null
                    : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4)),
                NotifiedAt = reader.IsDBNull(5)
                    ? null
                    : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
                SpentAiCredits =
                    PicoUsd.ToUsd(reader.GetInt64(6)) / AiCreditMath.UsdPerAiCredit,
            });
        }

        return rows;
    }

    // Chains whose reported span count compares to their span count with comparison ("=" or "<"), ordered by orderBy
    // (a column alias) descending, then trace ID.
    private static async Task<IReadOnlyList<BloatChainRow>> ReadBloatChainsAsync(
        SqliteConnection connection,
        LedgerFilter filter,
        int limit,
        string comparison,
        string orderBy,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand query = connection.CreateCommand();
        query.CommandText = $"""
            SELECT trace_id,
                   MIN(start_time_unix_ms)                                                 AS start_ms,
                   MAX(conversation_id)                                                    AS conversation_id,
                   MAX(COALESCE(repo_slug, repo_root, '(none)'))                           AS repository,
                   MAX(COALESCE(branch_name, '(none)'))                                    AS branch,
                   COUNT(*)                                                                AS span_count,
                   SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END)                          AS cache_reported_span_count,
                   SUM(input_tokens)                                                       AS input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN input_tokens ELSE 0 END)               AS reported_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN cache_read_input_tokens ELSE 0 END)    AS reported_cache_read_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN {UncachedSql} ELSE 0 END)              AS reported_uncached_input_tokens,
                   SUM({UncachedSql})                                                      AS estimated_uncached_input_tokens
            FROM spans
            WHERE {FilterSql}
            GROUP BY trace_id
            HAVING SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END) {comparison} COUNT(*)
            ORDER BY {orderBy} DESC, trace_id ASC
            LIMIT @limit;
            """;
        AddFilterParameters(query, filter);
        query.Parameters.AddWithValue("@limit", limit);
        List<BloatChainRow> rows = [];
        await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new BloatChainRow
            {
                TraceId = reader.GetString(0),
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
                ConversationId = reader.IsDBNull(2) ? null : reader.GetString(2),
                Repository = reader.GetString(3),
                Branch = reader.GetString(4),
                SpanCount = reader.GetInt64(5),
                CacheReportedSpanCount = reader.GetInt64(6),
                InputTokens = reader.GetInt64(7),
                ReportedInputTokens = reader.GetInt64(8),
                ReportedCacheReadInputTokens = reader.GetInt64(9),
                ReportedUncachedInputTokens = reader.GetInt64(10),
                EstimatedUncachedInputTokens = reader.GetInt64(11),
            });
        }

        return rows;
    }

    private static void ValidateBranchBudgetKey(string repository, string branch)
    {
        if (string.IsNullOrWhiteSpace(repository) || string.IsNullOrWhiteSpace(branch))
        {
            throw new ArgumentException("Repository and branch cannot be blank.");
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "InitializeAsync must complete before the ledger can be used.");
        }
    }

    private static void ValidateEntry(LedgerEntry entry, string parameterName)
    {
        NormalizedGenAiSpan span = entry.Span;
        if (entry.Calculation.Status == PricingStatus.NotBillableOperation
            || span.InputTokens < 0
            || span.OutputTokens < 0
            || span.CacheReadInputTokens < 0
            || span.CacheWriteInputTokens < 0)
        {
            throw new ArgumentException(
                "Entries must be billable and all token counts must be non-negative.",
                parameterName);
        }

        if (span.CacheReadState is not (CacheCounterState.Reported or CacheCounterState.Absent)
            || span.CacheWriteState is not (CacheCounterState.Reported or CacheCounterState.Absent)
            || (span.CacheReadState == CacheCounterState.Absent && span.CacheReadInputTokens != 0)
            || (span.CacheWriteState == CacheCounterState.Absent && span.CacheWriteInputTokens != 0))
        {
            throw new ArgumentException(
                "Cache counter states must be Reported or Absent, and an Absent counter must be 0.",
                parameterName);
        }

        if (entry.Git?.PullRequestNumber is <= 0)
        {
            throw new ArgumentException("Pull request numbers must be positive.", parameterName);
        }

        ApiEquivalentCost? retail = entry.Calculation.ApiEquivalent;
        if (retail is null
            || !Enum.IsDefined(retail.Status)
            || (retail.Status == ApiEquivalentStatus.Priced) != (retail.Breakdown is not null)
            || (retail.Status == ApiEquivalentStatus.Priced) != (retail.RateCardModelId is not null)
            || retail.RatesVersion is null
            || retail.RateCardSha256 is null)
        {
            throw new ArgumentException(
                "An API-equivalent cost needs its rates, and a model and breakdown exactly when it's Priced.",
                parameterName);
        }
    }

    // A live row's provenance: OTLP, with the usage identity of a GitHub-billed span (UsageIdentity.CanonicalUsageId).
    private static RowProvenance LiveProvenance(LedgerEntry entry) =>
        new(
            "otlp",
            UsageIdentity.CanonicalUsageId(
                entry.Span.ProviderName,
                entry.Span.ServiceRequestId,
                entry.Calculation.Channel),
            null,
            entry.Git?.RepositoryRoot,
            entry.Git?.RepositorySlug,
            entry.Git?.BranchName,
            entry.Git?.HeadCommitSha,
            entry.Git?.PullRequestNumber);

    // Inserts a spans row and returns its id, or null when (trace_id, span_id) already has a row.
    private static async Task<long?> InsertSpanAsync(
        SqliteTransaction transaction,
        NormalizedGenAiSpan span,
        CreditCalculationResult calculation,
        RowProvenance provenance,
        long recordedAtUnixMs,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = InsertSql;
        AddInsertParameters(command, span, calculation, provenance, recordedAtUnixMs);
        return await command.ExecuteScalarAsync(cancellationToken) is long id ? id : null;
    }

    private static void AddInsertParameters(
        SqliteCommand command,
        NormalizedGenAiSpan span,
        CreditCalculationResult calculation,
        RowProvenance provenance,
        long recordedAtUnixMs)
    {
        command.Parameters.AddWithValue("@trace_id", span.TraceId);
        command.Parameters.AddWithValue("@span_id", span.SpanId);
        command.Parameters.AddWithValue("@parent_span_id", OrDbNull(span.ParentSpanId));
        command.Parameters.AddWithValue("@operation_name", span.OperationName);
        command.Parameters.AddWithValue("@conversation_id", OrDbNull(span.ConversationId));
        command.Parameters.AddWithValue("@agent_name", OrDbNull(span.AgentName));
        command.Parameters.AddWithValue("@provider_name", OrDbNull(span.ProviderName));
        command.Parameters.AddWithValue("@request_model", OrDbNull(span.RequestModel));
        command.Parameters.AddWithValue("@response_model", OrDbNull(span.ResponseModel));
        command.Parameters.AddWithValue("@model_id", OrDbNull(calculation.ModelId));
        command.Parameters.AddWithValue("@rate_card_model_id", OrDbNull(calculation.RateCardModelId));
        command.Parameters.AddWithValue("@tier_name", OrDbNull(calculation.TierName));
        command.Parameters.AddWithValue("@server_address", OrDbNull(span.ServerAddress));
        command.Parameters.AddWithValue("@server_port", OrDbNull(span.ServerPort));
        command.Parameters.AddWithValue(
            "@start_time_unix_ms",
            span.StartTime.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue(
            "@end_time_unix_ms",
            span.EndTime.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@input_tokens", span.InputTokens);
        command.Parameters.AddWithValue("@output_tokens", span.OutputTokens);
        command.Parameters.AddWithValue(
            "@cache_read_input_tokens",
            span.CacheReadInputTokens);
        command.Parameters.AddWithValue(
            "@cache_write_input_tokens",
            span.CacheWriteInputTokens);
        command.Parameters.AddWithValue(
            "@billing_channel",
            ToDatabaseString(calculation.Channel));
        command.Parameters.AddWithValue(
            "@pricing_status",
            ToDatabaseString(calculation.Status));
        command.Parameters.AddWithValue("@rates_version", calculation.RatesVersion);
        command.Parameters.AddWithValue(
            "@cost_usd_pico",
            PicoUsd.FromUsd(calculation.UsdCost));
        command.Parameters.AddWithValue(
            "@saved_usd_pico",
            PicoUsd.FromUsd(calculation.EstimatedCloudSavedUsd));
        command.Parameters.AddWithValue("@repo_root", OrDbNull(provenance.RepositoryRoot));
        command.Parameters.AddWithValue("@repo_slug", OrDbNull(provenance.RepositorySlug));
        command.Parameters.AddWithValue("@branch_name", OrDbNull(provenance.BranchName));
        command.Parameters.AddWithValue(
            "@head_commit_sha",
            OrDbNull(provenance.HeadCommitSha));
        command.Parameters.AddWithValue("@recorded_at_unix_ms", recordedAtUnixMs);
        command.Parameters.AddWithValue("@cache_read_state", ToDatabaseString(span.CacheReadState));
        command.Parameters.AddWithValue("@cache_write_state", ToDatabaseString(span.CacheWriteState));
        command.Parameters.AddWithValue(
            "@pull_request_number",
            OrDbNull(provenance.PullRequestNumber));
        ApiEquivalentCost retail = calculation.ApiEquivalent;
        command.Parameters.AddWithValue(
            "@api_equivalent_usd_pico",
            OrDbNull(retail.UsdCost is decimal retailUsd ? PicoUsd.FromUsd(retailUsd) : (long?)null));
        command.Parameters.AddWithValue("@api_equivalent_status", ToDatabaseString(retail.Status));
        command.Parameters.AddWithValue("@api_rate_card_model_id", OrDbNull(retail.RateCardModelId));
        command.Parameters.AddWithValue("@api_rates_version", retail.RatesVersion);
        command.Parameters.AddWithValue("@api_rate_card_sha256", retail.RateCardSha256);
        command.Parameters.AddWithValue("@ingest_source", provenance.IngestSource);
        command.Parameters.AddWithValue("@canonical_usage_id", OrDbNull(provenance.CanonicalUsageId));
        command.Parameters.AddWithValue("@imported_tool_call_count", OrDbNull(provenance.ImportedToolCallCount));
    }

    // A rollup's group key: its SELECT expressions, and their aliases separated by ", ".
    private static (string Columns, string Aliases) RollupKey(LedgerGrouping grouping) => grouping switch
    {
        LedgerGrouping.Repository =>
            ("COALESCE(repo_slug, repo_root, '(none)') AS repository",
             "repository"),
        LedgerGrouping.Branch =>
            ("""
             COALESCE(repo_slug, repo_root, '(none)') AS repository,
                      COALESCE(branch_name, '(none)') AS branch
             """,
             "repository, branch"),
        LedgerGrouping.Model =>
            ("COALESCE(model_id, '(none)') AS model",
             "model"),
        LedgerGrouping.Agent =>
            ("COALESCE(agent_name, '(none)') AS agent",
             "agent"),
        LedgerGrouping.ChannelModel =>
            ("""
             billing_channel AS channel,
                      COALESCE(model_id, '(none)') AS model
             """,
             "channel, model"),
        _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, null),
    };

    private static string GetRollupSql(LedgerGrouping grouping)
    {
        (string keyColumns, string keyAliases) = RollupKey(grouping);

        return $"""
            SELECT {keyColumns},
                   COUNT(*)                                                                          AS span_count,
                   SUM(CASE WHEN pricing_status IN ('priced', 'local_offload') THEN 0 ELSE 1 END)    AS unpriced_span_count,
                   SUM(input_tokens)                                                                 AS input_tokens,
                   SUM(output_tokens)                                                                AS output_tokens,
                   SUM(cache_read_input_tokens)                                                      AS cache_read_input_tokens,
                   SUM(cache_write_input_tokens)                                                     AS cache_write_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END)                                    AS cache_reported_span_count,
                   SUM(CASE WHEN {ReportedSql} THEN input_tokens ELSE 0 END)                         AS reported_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN cache_read_input_tokens ELSE 0 END)              AS reported_cache_read_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN {UncachedSql} ELSE 0 END)                        AS reported_uncached_input_tokens,
                   SUM(CASE WHEN billing_channel = 'github_ai_credits' THEN cost_usd_pico ELSE 0 END) AS github_usd_pico,
                   SUM(CASE WHEN billing_channel = 'foundry_byok' THEN cost_usd_pico ELSE 0 END)      AS byok_usd_pico,
                   SUM(saved_usd_pico)                                                               AS saved_usd_pico,
                   SUM(api_equivalent_usd_pico)                                                      AS api_equivalent_usd_pico,
                   SUM(CASE WHEN api_equivalent_usd_pico IS NULL THEN 1 ELSE 0 END)                  AS api_equivalent_unpriced_span_count,
                   SUM(CASE WHEN api_equivalent_usd_pico IS NOT NULL AND NOT {ReportedSql} THEN 1 ELSE 0 END)
                                                                                                     AS api_equivalent_estimated_span_count
            FROM spans
            WHERE {FilterSql}
            GROUP BY {keyAliases}
            ORDER BY github_usd_pico + byok_usd_pico DESC, saved_usd_pico DESC, {keyAliases} ASC;
            """;
    }

    private static string ToDatabaseString(BillingChannel channel) => channel switch
    {
        BillingChannel.GitHubAiCredits => "github_ai_credits",
        BillingChannel.FoundryByok => "foundry_byok",
        BillingChannel.Local => "local",
        BillingChannel.Unpriced => "unpriced",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
    };

    // The API-equivalent totals of the spans that match where, priced with whatIf instead of their recorded results, by
    // group key (GroupKey of the keyAliases columns). Streams the spans, never writes, and never reads a recorded result.
    private static async Task<Dictionary<string, RetailTotals>> RepriceAsync(
        SqliteTransaction snapshot,
        string keyColumns,
        string keyAliases,
        string where,
        Action<SqliteCommand> addParameters,
        JsonRateCardCalculator whatIf,
        CancellationToken cancellationToken)
    {
        Dictionary<string, RetailTotals> totals = new(StringComparer.Ordinal);
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT {keyColumns},
                   operation_name                                AS what_if_operation,
                   billing_channel                               AS what_if_channel,
                   model_id                                      AS what_if_model,
                   request_model                                 AS what_if_request_model,
                   start_time_unix_ms                            AS what_if_start,
                   input_tokens                                  AS what_if_input,
                   output_tokens                                 AS what_if_output,
                   cache_read_input_tokens                       AS what_if_cache_read,
                   cache_write_input_tokens                      AS what_if_cache_write,
                   CASE WHEN {ReportedSql} THEN 1 ELSE 0 END     AS what_if_reported
            FROM spans
            WHERE {where};
            """;
        addParameters(command);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            int model = reader.GetOrdinal("what_if_model");
            int requestModel = reader.GetOrdinal("what_if_request_model");
            ApiEquivalentCost cost = whatIf.CalculateApiEquivalent(new ApiEquivalentRequest
            {
                OperationName = reader.GetString(reader.GetOrdinal("what_if_operation")),
                Channel = ToBillingChannel(reader.GetString(reader.GetOrdinal("what_if_channel"))),
                ModelId = reader.IsDBNull(model) ? null : reader.GetString(model),
                RequestModel = reader.IsDBNull(requestModel) ? null : reader.GetString(requestModel),
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(
                    reader.GetInt64(reader.GetOrdinal("what_if_start"))),
                InputTokens = reader.GetInt64(reader.GetOrdinal("what_if_input")),
                OutputTokens = reader.GetInt64(reader.GetOrdinal("what_if_output")),
                CacheReadInputTokens = reader.GetInt64(reader.GetOrdinal("what_if_cache_read")),
                CacheWriteInputTokens = reader.GetInt64(reader.GetOrdinal("what_if_cache_write")),
            });
            string key = GroupKey(reader, keyAliases);
            if (!totals.TryGetValue(key, out RetailTotals? group))
            {
                group = new RetailTotals();
                totals.Add(key, group);
            }

            group.Add(cost, reader.GetInt64(reader.GetOrdinal("what_if_reported")) == 1);
        }

        return totals;
    }

    // The values of the keyAliases columns, each prefixed with its length so that different keys never collide.
    private static string GroupKey(SqliteDataReader reader, string keyAliases) =>
        GroupKey(keyAliases.Split(", ").Select(alias => reader.GetString(reader.GetOrdinal(alias))));

    private static string GroupKey(IEnumerable<string> values) =>
        string.Concat(values.Select(value =>
            $"{value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{value}"));

    private static BillingChannel ToBillingChannel(string value) => value switch
    {
        "github_ai_credits" => BillingChannel.GitHubAiCredits,
        "foundry_byok" => BillingChannel.FoundryByok,
        "local" => BillingChannel.Local,
        "unpriced" => BillingChannel.Unpriced,
        _ => throw new InvalidDataException($"Unknown billing channel '{value}'."),
    };

    private static string ToDatabaseString(PricingStatus status) => status switch
    {
        PricingStatus.Priced => "priced",
        PricingStatus.LocalOffload => "local_offload",
        PricingStatus.ByokUnpriced => "byok_unpriced",
        PricingStatus.UnknownModel => "unknown_model",
        PricingStatus.NoRateForDate => "no_rate_for_date",
        PricingStatus.UnrecognizedEndpoint => "unrecognized_endpoint",
        PricingStatus.InvalidUsage => "invalid_usage",
        PricingStatus.NotBillableOperation => "not_billable_operation",
        PricingStatus.OtherProvider => "other_provider",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static string ToDatabaseString(CacheCounterState state) => state switch
    {
        CacheCounterState.Reported => "reported",
        CacheCounterState.Absent => "absent",
        CacheCounterState.LegacyUnknown => "legacy_unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static string ToDatabaseString(ApiEquivalentStatus status) => status switch
    {
        ApiEquivalentStatus.Priced => "priced",
        ApiEquivalentStatus.Unpriced => "unpriced",
        ApiEquivalentStatus.NotApplicable => "not_applicable",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    // A nullable pico-USD column as USD.
    private static decimal? ReadUsd(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : PicoUsd.ToUsd(reader.GetInt64(ordinal));
    }

    private static CacheCounterState ToCacheCounterState(string value) => value switch
    {
        "reported" => CacheCounterState.Reported,
        "absent" => CacheCounterState.Absent,
        "legacy_unknown" => CacheCounterState.LegacyUnknown,
        _ => throw new InvalidDataException($"Unknown cache counter state '{value}'."),
    };

    private static object OrDbNull<T>(T? value) where T : struct =>
        value.HasValue ? value.Value : DBNull.Value;

    private static object OrDbNull(string? value) => value is null ? DBNull.Value : value;

    private sealed record AgentLink(
        string SpanId,
        string? AgentName,
        string? ConversationId,
        string? GitRepositorySlug,
        string? GitBranch,
        string? GitCommitSha);

    // Where a row came from ('otlp' or 'import:<source>'), its usage identity, and its Git attribution.
    private sealed record RowProvenance(
        string IngestSource,
        string? CanonicalUsageId,
        long? ImportedToolCallCount,
        string? RepositoryRoot,
        string? RepositorySlug,
        string? BranchName,
        string? HeadCommitSha,
        int? PullRequestNumber);

    // A group's API-equivalent totals: as recorded, or summed by Add as spans are priced again.
    private sealed class RetailTotals(decimal? usd = null, long unpricedSpanCount = 0, long estimatedSpanCount = 0)
    {
        public decimal? Usd { get; private set; } = usd;
        public long UnpricedSpanCount { get; private set; } = unpricedSpanCount;
        public long EstimatedSpanCount { get; private set; } = estimatedSpanCount;

        public void Add(ApiEquivalentCost cost, bool cacheReported)
        {
            if (cost.UsdCost is not decimal usdCost)
            {
                UnpricedSpanCount++;
                return;
            }

            Usd = (Usd ?? 0m) + usdCost;
            if (!cacheReported)
            {
                EstimatedSpanCount++;
            }
        }
    }

    private sealed record PromptChainData(
        string TraceId,
        string? ConversationId,
        long StartMs,
        long EndMs,
        string Repository,
        string Branch,
        long SpanCount,
        long UnpricedSpanCount,
        long InputTokens,
        long OutputTokens,
        long CacheReadInputTokens,
        long CacheWriteInputTokens,
        long GitHubUsdPico,
        long ByokUsdPico,
        long SavedUsdPico,
        decimal? ApiEquivalentUsd,
        long ApiEquivalentUnpricedSpanCount,
        long ApiEquivalentEstimatedSpanCount);
}
