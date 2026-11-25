namespace CopilotLedger.Storage;

internal static class LedgerMigrations
{
    // Tables that every ledger of the latest format has. Opening the ledger adds one that's missing, without changing
    // user_version: an earlier build of the same format ignores it.
    // mcp_tool_names: the names that import learned for MCP tools whose live spans Copilot names by hashes (McpToolNames).
    public static IReadOnlyList<(string Name, string Sql)> Tables { get; } =
    [
        ("mcp_tool_names",
         """
         CREATE TABLE IF NOT EXISTS mcp_tool_names (
             hashed_name TEXT NOT NULL PRIMARY KEY CHECK (length(hashed_name) = 129),
             tool_name   TEXT NOT NULL CHECK (length(tool_name) BETWEEN 1 AND 256)
         ) STRICT, WITHOUT ROWID;
         """),
    ];

    // Indexes that every ledger of the latest format has. Opening the ledger adds one that's missing, without changing
    // user_version: SQLite keeps an index current for every writer, including an earlier build of the same format.
    // ix_spans_conversation_live: the importer's per-record live-span lookups (UsageAuthority), which name 'otlp' literally
    // so that SQLite can use this partial index.
    public static IReadOnlyList<(string Name, string Sql)> Indexes { get; } =
    [
        ("ix_spans_conversation_live",
         "CREATE INDEX IF NOT EXISTS ix_spans_conversation_live ON spans (conversation_id, canonical_usage_id) WHERE ingest_source = 'otlp';"),
    ];

    public static IReadOnlyList<(int Version, string Sql)> Migrations { get; } =
    [
        (1, """
            CREATE TABLE spans (
                id                       INTEGER PRIMARY KEY,
                trace_id                 TEXT    NOT NULL,
                span_id                  TEXT    NOT NULL,
                parent_span_id           TEXT,
                operation_name           TEXT    NOT NULL,
                conversation_id          TEXT,
                agent_name               TEXT,
                provider_name            TEXT,
                request_model            TEXT,
                response_model           TEXT,
                model_id                 TEXT,
                rate_card_model_id       TEXT,
                tier_name                TEXT,
                server_address           TEXT,
                server_port              INTEGER,
                start_time_unix_ms       INTEGER NOT NULL,
                end_time_unix_ms         INTEGER NOT NULL,
                input_tokens             INTEGER NOT NULL CHECK (input_tokens >= 0),
                output_tokens            INTEGER NOT NULL CHECK (output_tokens >= 0),
                cache_read_input_tokens  INTEGER NOT NULL CHECK (cache_read_input_tokens >= 0),
                cache_write_input_tokens INTEGER NOT NULL CHECK (cache_write_input_tokens >= 0),
                billing_channel          TEXT    NOT NULL,
                pricing_status           TEXT    NOT NULL,
                rates_version            TEXT    NOT NULL,
                cost_usd_pico            INTEGER NOT NULL CHECK (cost_usd_pico >= 0),
                saved_usd_pico           INTEGER NOT NULL CHECK (saved_usd_pico >= 0),
                repo_root                TEXT,
                repo_slug                TEXT,
                branch_name              TEXT,
                head_commit_sha          TEXT,
                recorded_at_unix_ms      INTEGER NOT NULL,
                UNIQUE (trace_id, span_id)
            ) STRICT;

            CREATE INDEX ix_spans_start_time  ON spans (start_time_unix_ms);
            CREATE INDEX ix_spans_repo_branch ON spans (repo_slug, branch_name);
            CREATE INDEX ix_spans_model       ON spans (model_id);
            CREATE INDEX ix_spans_agent       ON spans (agent_name);
            """),
        (2, """
            CREATE TABLE span_links (
                trace_id            TEXT    NOT NULL,
                span_id             TEXT    NOT NULL,
                parent_span_id      TEXT,
                operation_name      TEXT,
                agent_name          TEXT,
                conversation_id     TEXT,
                git_repository_slug TEXT,
                git_branch          TEXT,
                git_commit_sha      TEXT,
                start_time_unix_ms  INTEGER NOT NULL,
                recorded_at_unix_ms INTEGER NOT NULL,
                PRIMARY KEY (trace_id, span_id)
            ) STRICT, WITHOUT ROWID;

            CREATE INDEX ix_span_links_parent ON span_links (trace_id, parent_span_id);
            CREATE INDEX ix_span_links_start  ON span_links (start_time_unix_ms);
            CREATE INDEX ix_spans_trace       ON spans (trace_id);
            """),
        (3, """
            ALTER TABLE span_links ADD COLUMN tool_name TEXT;
            ALTER TABLE span_links ADD COLUMN tool_exit_code INTEGER;
            ALTER TABLE span_links ADD COLUMN status_code INTEGER NOT NULL DEFAULT 0;

            CREATE TABLE breaker_trips (
                id                   INTEGER PRIMARY KEY,
                rule                 TEXT    NOT NULL CHECK (rule IN ('credit_velocity', 'retry_churn')),
                trace_id             TEXT    NOT NULL,
                conversation_id      TEXT,
                repository           TEXT    NOT NULL,
                branch               TEXT    NOT NULL,
                repo_root            TEXT,
                tripped_at_unix_ms   INTEGER NOT NULL,
                detail               TEXT    NOT NULL,
                window_cost_usd_pico INTEGER CHECK (window_cost_usd_pico >= 0),
                window_minutes       INTEGER,
                failure_count        INTEGER,
                recorded_at_unix_ms  INTEGER NOT NULL,
                notified_at_unix_ms  INTEGER,
                reset_at_unix_ms     INTEGER,
                UNIQUE (rule, trace_id)
            ) STRICT;

            CREATE INDEX ix_breaker_trips_open ON breaker_trips (reset_at_unix_ms, repository, branch);

            CREATE TABLE branch_budgets (
                repository          TEXT    NOT NULL,
                branch              TEXT    NOT NULL,
                budget_usd_pico     INTEGER NOT NULL CHECK (budget_usd_pico > 0),
                set_at_unix_ms      INTEGER NOT NULL,
                alerted_at_unix_ms  INTEGER,
                notified_at_unix_ms INTEGER,
                PRIMARY KEY (repository, branch)
            ) STRICT, WITHOUT ROWID;

            CREATE TABLE breaker_settings (
                id                          INTEGER PRIMARY KEY CHECK (id = 1),
                velocity_threshold_usd_pico INTEGER NOT NULL CHECK (velocity_threshold_usd_pico > 0),
                velocity_window_minutes     INTEGER NOT NULL CHECK (velocity_window_minutes BETWEEN 1 AND 1440),
                churn_failures              INTEGER NOT NULL CHECK (churn_failures BETWEEN 2 AND 100),
                flag_file                   INTEGER NOT NULL CHECK (flag_file IN (0, 1)),
                notifications               INTEGER NOT NULL CHECK (notifications IN (0, 1)),
                updated_at_unix_ms          INTEGER NOT NULL
            ) STRICT;

            INSERT INTO breaker_settings (id, velocity_threshold_usd_pico, velocity_window_minutes, churn_failures, flag_file, notifications, updated_at_unix_ms)
            VALUES (1, 1500000000000, 5, 5, 0, 1, 0);
            """),

        // Rows recorded before version 4 get legacy_unknown cache states, except that a nonzero counter can only have been
        // reported. agent_span_id is each span's nearest invoke_agent ancestor, filled from the span links not yet pruned.
        (4, """
            ALTER TABLE spans ADD COLUMN cache_read_state TEXT NOT NULL DEFAULT 'legacy_unknown'
                CHECK (cache_read_state IN ('reported', 'absent', 'legacy_unknown'));
            ALTER TABLE spans ADD COLUMN cache_write_state TEXT NOT NULL DEFAULT 'legacy_unknown'
                CHECK (cache_write_state IN ('reported', 'absent', 'legacy_unknown'));
            ALTER TABLE spans ADD COLUMN pull_request_number INTEGER
                CHECK (pull_request_number IS NULL OR pull_request_number > 0);
            ALTER TABLE spans ADD COLUMN agent_span_id TEXT;

            CREATE INDEX IF NOT EXISTS ix_spans_conversation_start ON spans (conversation_id, start_time_unix_ms);

            UPDATE spans SET cache_read_state = 'reported' WHERE cache_read_input_tokens > 0;
            UPDATE spans SET cache_write_state = 'reported' WHERE cache_write_input_tokens > 0;

            WITH RECURSIVE below (trace_id, span_id, agent_span_id) AS (
                SELECT child.trace_id, child.span_id, agent.span_id
                  FROM span_links AS agent
                  JOIN span_links AS child
                    ON child.trace_id = agent.trace_id AND child.parent_span_id = agent.span_id
                 WHERE agent.operation_name = 'invoke_agent'
                   AND (child.operation_name IS NULL OR child.operation_name <> 'invoke_agent')
                UNION
                SELECT child.trace_id, child.span_id, below.agent_span_id
                  FROM below
                  JOIN span_links AS child
                    ON child.trace_id = below.trace_id AND child.parent_span_id = below.span_id
                 WHERE child.operation_name IS NULL OR child.operation_name <> 'invoke_agent'
            )
            UPDATE spans
               SET agent_span_id = below.agent_span_id
              FROM below
             WHERE below.trace_id = spans.trace_id AND below.span_id = spans.span_id;
            """),

        // The API-equivalent (retail) lens. Rows recorded before version 5 keep NULL in every new column until
        // gh ledger rates backfill-api-equivalent prices them; api_equivalent_status IS NULL marks such a legacy row.
        (5, """
            ALTER TABLE spans ADD COLUMN api_equivalent_usd_pico INTEGER
                CHECK (api_equivalent_usd_pico IS NULL OR api_equivalent_usd_pico >= 0);
            ALTER TABLE spans ADD COLUMN api_equivalent_status TEXT
                CHECK (api_equivalent_status IS NULL OR api_equivalent_status IN ('priced', 'unpriced', 'not_applicable'));
            ALTER TABLE spans ADD COLUMN api_rate_card_model_id TEXT;
            ALTER TABLE spans ADD COLUMN api_rates_version TEXT;
            ALTER TABLE spans ADD COLUMN api_rate_card_sha256 TEXT;
            """),

        // Import provenance and live/import authority (gh ledger import). Rows recorded earlier are live ('otlp'), effective,
        // and unidentified (no canonical_usage_id), and links recorded earlier have no owner. Only import rows are ever
        // superseded, and only effective import rows must have distinct canonical IDs.
        (6, """
            ALTER TABLE spans ADD COLUMN ingest_source TEXT NOT NULL DEFAULT 'otlp'
                CHECK (ingest_source IN ('otlp', 'import:copilot', 'import:claude', 'import:codex'));
            ALTER TABLE spans ADD COLUMN canonical_usage_id TEXT
                CHECK (canonical_usage_id IS NULL OR length(canonical_usage_id) = 64);
            ALTER TABLE spans ADD COLUMN accounting_state TEXT NOT NULL DEFAULT 'effective'
                CHECK (accounting_state IN ('effective', 'superseded'));
            ALTER TABLE spans ADD COLUMN superseded_by INTEGER
                CHECK ((accounting_state = 'effective') = (superseded_by IS NULL)
                       AND (superseded_by IS NULL OR ingest_source <> 'otlp'));
            ALTER TABLE spans ADD COLUMN imported_tool_call_count INTEGER
                CHECK (imported_tool_call_count IS NULL OR (imported_tool_call_count >= 0 AND ingest_source <> 'otlp'));
            ALTER TABLE span_links ADD COLUMN owner_span_row_id INTEGER;

            CREATE INDEX ix_spans_canonical_usage ON spans (canonical_usage_id) WHERE canonical_usage_id IS NOT NULL;
            CREATE UNIQUE INDEX ux_spans_effective_import_usage ON spans (canonical_usage_id)
                WHERE canonical_usage_id IS NOT NULL AND accounting_state = 'effective' AND ingest_source <> 'otlp';

            CREATE TABLE import_sessions (
                source                 TEXT    NOT NULL CHECK (source IN ('copilot', 'claude', 'codex')),
                log_id                 TEXT    NOT NULL,
                log_size_bytes         INTEGER NOT NULL CHECK (log_size_bytes >= 0),
                log_last_write_unix_ms INTEGER NOT NULL,
                log_sha256             TEXT    NOT NULL CHECK (length(log_sha256) = 64),
                complete               INTEGER NOT NULL CHECK (complete IN (0, 1)),
                records_recorded       INTEGER NOT NULL CHECK (records_recorded >= 0),
                records_duplicate      INTEGER NOT NULL CHECK (records_duplicate >= 0),
                records_overlap        INTEGER NOT NULL CHECK (records_overlap >= 0),
                records_ambiguous      INTEGER NOT NULL CHECK (records_ambiguous >= 0),
                imported_at_unix_ms    INTEGER NOT NULL,
                PRIMARY KEY (source, log_id)
            ) STRICT, WITHOUT ROWID;

            CREATE TABLE import_record_aliases (
                source              TEXT    NOT NULL CHECK (source IN ('copilot', 'claude', 'codex')),
                record_key_sha256   TEXT    NOT NULL CHECK (length(record_key_sha256) = 64),
                span_row_id         INTEGER NOT NULL,
                resolution          TEXT    NOT NULL CHECK (resolution IN ('recorded', 'live', 'duplicate')),
                recorded_at_unix_ms INTEGER NOT NULL,
                PRIMARY KEY (source, record_key_sha256)
            ) STRICT, WITHOUT ROWID;
            """),

        // Charged amounts (Sprint 12). cost_usd_pico stays the charged amount that every report sums; list_cost_usd_pico is the rate card's price, and NULL means "the same as cost_usd_pico" (every row recorded before format 7, and any row a running format-6 daemon writes after the upgrade, was charged at the rate card). import_revision 0 marks logs imported before Sprint 12's import fixes, which the next import reads again.
        (7, """
            ALTER TABLE spans ADD COLUMN list_cost_usd_pico INTEGER
                CHECK (list_cost_usd_pico IS NULL OR list_cost_usd_pico >= 0);
            ALTER TABLE spans ADD COLUMN charge_source TEXT NOT NULL DEFAULT 'rate_card'
                CHECK (charge_source IN ('rate_card', 'reported', 'rejected')
                       AND (charge_source <> 'reported' OR (billing_channel = 'github_ai_credits' AND pricing_status = 'priced')));
            ALTER TABLE import_sessions ADD COLUMN import_revision INTEGER NOT NULL DEFAULT 0 CHECK (import_revision >= 0);
            CREATE INDEX ix_span_links_import_span ON span_links (span_id) WHERE owner_span_row_id IS NOT NULL;
            """),
    ];
}
