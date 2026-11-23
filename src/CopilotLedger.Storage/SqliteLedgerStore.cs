using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

public sealed class SqliteLedgerStore
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
            cache_read_state, cache_write_state, pull_request_number
        ) VALUES (
            @trace_id, @span_id, @parent_span_id, @operation_name, @conversation_id,
            @agent_name, @provider_name, @request_model, @response_model, @model_id,
            @rate_card_model_id, @tier_name, @server_address, @server_port,
            @start_time_unix_ms, @end_time_unix_ms, @input_tokens, @output_tokens,
            @cache_read_input_tokens, @cache_write_input_tokens, @billing_channel,
            @pricing_status, @rates_version, @cost_usd_pico, @saved_usd_pico,
            @repo_root, @repo_slug, @branch_name, @head_commit_sha, @recorded_at_unix_ms,
            @cache_read_state, @cache_write_state, @pull_request_number
        ) ON CONFLICT (trace_id, span_id) DO NOTHING;
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
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        int inserted = 0;
        long recordedAtUnixMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        foreach (LedgerEntry entry in batch)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = InsertSql;
            AddInsertParameters(command, entry, recordedAtUnixMs);
            inserted += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    public async Task<IReadOnlyList<LedgerRollupRow>> GetRollupAsync(
        LedgerGrouping grouping,
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = GetRollupSql(grouping);
        command.Parameters.AddWithValue("@from", OrDbNull(filter.From?.ToUnixTimeMilliseconds()));
        command.Parameters.AddWithValue("@to", OrDbNull(filter.To?.ToUnixTimeMilliseconds()));
        command.Parameters.AddWithValue("@repository", OrDbNull(filter.Repository));
        command.Parameters.AddWithValue("@branch", OrDbNull(filter.Branch));

        List<LedgerRollupRow> rows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            decimal aiCreditsUsd =
                PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("github_usd_pico")));
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
                AiCredits = AiCreditMath.UsdToAiCredits(aiCreditsUsd),
                AiCreditsUsd = aiCreditsUsd,
                FoundryByokUsd =
                    PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("byok_usd_pico"))),
                LocalSavedUsd =
                    PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("saved_usd_pico"))),
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
                SELECT MAX(conversation_id), MAX(COALESCE(repo_slug, repo_root, '(none)')), MAX(COALESCE(branch_name, '(none)')), MAX(repo_root), COUNT(*) FROM spans WHERE trace_id = @trace;
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
                SELECT span_id, end_time_unix_ms, cost_usd_pico FROM spans WHERE trace_id = @trace AND cost_usd_pico > 0 ORDER BY end_time_unix_ms, span_id;
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
            query.CommandText = """
                SELECT span_id, start_time_unix_ms, tool_name, tool_exit_code, status_code FROM span_links WHERE trace_id = @trace AND operation_name = 'execute_tool' ORDER BY start_time_unix_ms, span_id;
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
                SELECT span_id, start_time_unix_ms, head_commit_sha FROM spans WHERE trace_id = @trace AND head_commit_sha IS NOT NULL ORDER BY start_time_unix_ms, span_id;
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
        command.CommandText = """
            SELECT b.repository, b.branch, b.budget_usd_pico, b.set_at_unix_ms, b.alerted_at_unix_ms, b.notified_at_unix_ms,
                   (SELECT COALESCE(SUM(s.cost_usd_pico), 0) FROM spans AS s
                     WHERE COALESCE(s.repo_slug, s.repo_root, '(none)') = b.repository
                       AND COALESCE(s.branch_name, '(none)') = b.branch) AS spent_usd_pico
            FROM branch_budgets AS b
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
            UPDATE branch_budgets SET alerted_at_unix_ms = @now WHERE repository = @repository AND branch = @branch AND alerted_at_unix_ms IS NULL AND budget_usd_pico < (SELECT COALESCE(SUM(cost_usd_pico), 0) FROM spans WHERE COALESCE(repo_slug, repo_root, '(none)') = @repository AND COALESCE(branch_name, '(none)') = @branch);
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
                       AND COALESCE(s.branch_name, '(none)') = b.branch) AS spent_usd_pico
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
            SELECT trace_id, COUNT(*) FROM span_links WHERE trace_id IN ({parameters}) AND operation_name = 'execute_tool' AND ((tool_exit_code IS NOT NULL AND tool_exit_code <> 0) OR status_code = 2) GROUP BY trace_id;
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
    public async Task<IReadOnlyList<PromptChainRow>> GetTopPromptChainsAsync(
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
        List<PromptChainData> data = [];
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = """
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
                       SUM(saved_usd_pico)                                                                AS saved_usd_pico
                FROM spans
                WHERE (@from IS NULL OR start_time_unix_ms >= @from)
                  AND (@to IS NULL OR start_time_unix_ms < @to)
                  AND (@repository IS NULL OR COALESCE(repo_slug, repo_root, '(none)') = @repository)
                  AND (@branch IS NULL OR COALESCE(branch_name, '(none)') = @branch)
                GROUP BY trace_id
                ORDER BY github_usd_pico + byok_usd_pico DESC, saved_usd_pico DESC, start_ms DESC, trace_id ASC
                LIMIT @limit;
                """;
            query.Parameters.AddWithValue("@from", OrDbNull(filter.From?.ToUnixTimeMilliseconds()));
            query.Parameters.AddWithValue("@to", OrDbNull(filter.To?.ToUnixTimeMilliseconds()));
            query.Parameters.AddWithValue("@repository", OrDbNull(filter.Repository));
            query.Parameters.AddWithValue("@branch", OrDbNull(filter.Branch));
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
                    reader.GetInt64(reader.GetOrdinal("saved_usd_pico"))));
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
        await using (SqliteCommand query = connection.CreateCommand())
        {
            string traceParameters = string.Join(
                ", ",
                Enumerable.Range(0, data.Count).Select(index => $"@t{index}"));
            query.CommandText = $"""
                SELECT DISTINCT trace_id, agent_name, model_id
                FROM spans
                WHERE (@from IS NULL OR start_time_unix_ms >= @from)
                  AND (@to IS NULL OR start_time_unix_ms < @to)
                  AND (@repository IS NULL OR COALESCE(repo_slug, repo_root, '(none)') = @repository)
                  AND (@branch IS NULL OR COALESCE(branch_name, '(none)') = @branch)
                  AND trace_id IN ({traceParameters});
                """;
            query.Parameters.AddWithValue("@from", OrDbNull(filter.From?.ToUnixTimeMilliseconds()));
            query.Parameters.AddWithValue("@to", OrDbNull(filter.To?.ToUnixTimeMilliseconds()));
            query.Parameters.AddWithValue("@repository", OrDbNull(filter.Repository));
            query.Parameters.AddWithValue("@branch", OrDbNull(filter.Branch));
            for (int index = 0; index < data.Count; index++)
            {
                query.Parameters.AddWithValue($"@t{index}", data[index].TraceId);
            }

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

        List<PromptChainRow> rows = [];
        foreach (PromptChainData item in data)
        {
            decimal aiCreditsUsd = PicoUsd.ToUsd(item.GitHubUsdPico);
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
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(DISTINCT trace_id)
            FROM spans
            WHERE (@from IS NULL OR start_time_unix_ms >= @from)
              AND (@to IS NULL OR start_time_unix_ms < @to)
              AND (@repository IS NULL OR COALESCE(repo_slug, repo_root, '(none)') = @repository)
              AND (@branch IS NULL OR COALESCE(branch_name, '(none)') = @branch);
            """;
        command.Parameters.AddWithValue("@from", OrDbNull(filter.From?.ToUnixTimeMilliseconds()));
        command.Parameters.AddWithValue("@to", OrDbNull(filter.To?.ToUnixTimeMilliseconds()));
        command.Parameters.AddWithValue("@repository", OrDbNull(filter.Repository));
        command.Parameters.AddWithValue("@branch", OrDbNull(filter.Branch));
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
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
    }

    private static void AddInsertParameters(
        SqliteCommand command,
        LedgerEntry entry,
        long recordedAtUnixMs)
    {
        NormalizedGenAiSpan span = entry.Span;
        CreditCalculationResult calculation = entry.Calculation;

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
        command.Parameters.AddWithValue("@repo_root", OrDbNull(entry.Git?.RepositoryRoot));
        command.Parameters.AddWithValue("@repo_slug", OrDbNull(entry.Git?.RepositorySlug));
        command.Parameters.AddWithValue("@branch_name", OrDbNull(entry.Git?.BranchName));
        command.Parameters.AddWithValue(
            "@head_commit_sha",
            OrDbNull(entry.Git?.HeadCommitSha));
        command.Parameters.AddWithValue("@recorded_at_unix_ms", recordedAtUnixMs);
        command.Parameters.AddWithValue("@cache_read_state", ToDatabaseString(span.CacheReadState));
        command.Parameters.AddWithValue("@cache_write_state", ToDatabaseString(span.CacheWriteState));
        command.Parameters.AddWithValue(
            "@pull_request_number",
            OrDbNull(entry.Git?.PullRequestNumber));
    }

    private static string GetRollupSql(LedgerGrouping grouping)
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
            LedgerGrouping.ChannelModel =>
                ("""
                 billing_channel AS channel,
                          COALESCE(model_id, '(none)') AS model
                 """,
                 "channel, model"),
            _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, null),
        };

        return $"""
            SELECT {keyColumns},
                   COUNT(*)                                                                          AS span_count,
                   SUM(CASE WHEN pricing_status IN ('priced', 'local_offload') THEN 0 ELSE 1 END)    AS unpriced_span_count,
                   SUM(input_tokens)                                                                 AS input_tokens,
                   SUM(output_tokens)                                                                AS output_tokens,
                   SUM(cache_read_input_tokens)                                                      AS cache_read_input_tokens,
                   SUM(cache_write_input_tokens)                                                     AS cache_write_input_tokens,
                   SUM(CASE WHEN billing_channel = 'github_ai_credits' THEN cost_usd_pico ELSE 0 END) AS github_usd_pico,
                   SUM(CASE WHEN billing_channel = 'foundry_byok' THEN cost_usd_pico ELSE 0 END)      AS byok_usd_pico,
                   SUM(saved_usd_pico)                                                               AS saved_usd_pico
            FROM spans
            WHERE (@from IS NULL OR start_time_unix_ms >= @from)
              AND (@to IS NULL OR start_time_unix_ms < @to)
              AND (@repository IS NULL OR COALESCE(repo_slug, repo_root, '(none)') = @repository)
              AND (@branch IS NULL OR COALESCE(branch_name, '(none)') = @branch)
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
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static string ToDatabaseString(CacheCounterState state) => state switch
    {
        CacheCounterState.Reported => "reported",
        CacheCounterState.Absent => "absent",
        CacheCounterState.LegacyUnknown => "legacy_unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
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
        long SavedUsdPico);
}
