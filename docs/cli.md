# gh-ledger command reference

gh-ledger runs a local OpenTelemetry receiver at `http://localhost:4318`, records GitHub Copilot's usage in a SQLite ledger on your machine, and reports AI spend by repository, branch, model, agent, and prompt. It needs no Docker and no cloud service. For walkthroughs with sample output, see the [user guide](guide.md).

**Command names.** This page writes commands as `gh ledger <command>`, which runs gh-ledger as a GitHub CLI extension. If you built gh-ledger yourself and have no `gh ledger` alias, run the binary by its path instead, as `<folder>/gh-ledger <command>`. `gh-ledger` is the program's own name, so `--help` and gh-ledger's messages say `gh-ledger`; with the extension, run the commands they show as `gh ledger`.

## Get gh-ledger

Install the GitHub CLI extension:

```shell
gh extension install swigerb/gh-ledger
```

gh downloads the release binary for your platform to:

- Windows: `%LOCALAPPDATA%\GitHub CLI\extensions\gh-ledger\gh-ledger.exe`
- macOS and Linux: `~/.local/share/gh/extensions/gh-ledger/gh-ledger`

When `XDG_DATA_HOME` is set, gh uses `$XDG_DATA_HOME/gh/extensions/gh-ledger/` instead. The path stays the same when you upgrade, so the sign-in item and an MCP registration keep working.

To build from source instead, publish one single-file, self-contained binary for your runtime identifier:

```shell
dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r <rid> -o artifacts/gh-ledger
```

Use `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64`. Move `gh-ledger` (`gh-ledger.exe` on Windows) from `artifacts/gh-ledger` to a permanent folder, then run `<folder>/gh-ledger install`. `install` points the sign-in item and the `gh ledger` alias at that copy; it doesn't add the folder to `PATH`.

## Install and uninstall

`gh ledger install` makes these changes and records them in `install.json`:

- It sets `COPILOT_OTEL_ENABLED=true`: as a user environment variable on Windows, or in a marked block in your shell profile elsewhere (`.zshrc` for zsh, `.bash_profile` for bash on macOS, `.bashrc` for bash on Linux, and `.profile` otherwise).
- For a binary you built, it sets the `gh ledger` alias when `gh` is on `PATH`. The extension doesn't need one. On Windows, the alias needs `sh.exe` from Git for Windows.
- It registers the daemon to start when you sign in: the `CopilotLedger` value under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` on Windows, `~/Library/LaunchAgents/io.github.swigerb.copilot-ledger.plist` on macOS, or `copilot-ledger.desktop` in `$XDG_CONFIG_HOME/autostart` (default `~/.config/autostart`) on Linux.
- It starts the daemon.

It changes nothing else; in particular, it doesn't edit `PATH` or other OpenTelemetry variables. Use `--dry-run` to print the changes without making them. Afterward, restart the GitHub Copilot app, VS Code, and open terminals so they see `COPILOT_OTEL_ENABLED`.

`install` and `status` warn about settings that keep Copilot's telemetry from reaching CopilotLedger:

- `OTEL_EXPORTER_OTLP_ENDPOINT` set to any endpoint other than `http://localhost:4318`.
- `COPILOT_OTEL_FILE_EXPORTER_PATH`, which makes Copilot write telemetry to a file instead.
- `COPILOT_OTEL_EXPORTER_TYPE` set to anything other than `otlp-http`.
- Any of `OTEL_EXPORTER_OTLP_CERTIFICATE`, `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE`, `OTEL_EXPORTER_OTLP_CLIENT_KEY`, `OTEL_EXPORTER_OTLP_TRACES_CERTIFICATE`, `OTEL_EXPORTER_OTLP_TRACES_CLIENT_CERTIFICATE`, or `OTEL_EXPORTER_OTLP_TRACES_CLIENT_KEY`, which turn off export to `http://` endpoints.
- `OTEL_EXPORTER_OTLP_PROTOCOL` or `OTEL_EXPORTER_OTLP_TRACES_PROTOCOL` set to `grpc`; CopilotLedger accepts only `http/protobuf` and `http/json`.

`install` also warns when `COPILOT_LEDGER_HOME` is set, because the daemon started at sign-in uses it only if your sign-in session sets it too. `status` warns when `install.json` names a different executable, for example after you switch from a binary you built to the extension; run `install` again from the copy you want to keep.

`gh ledger uninstall` stops the daemon, removes the sign-in item, and reverses the environment variable, profile block, and alias changes recorded in `install.json`, restoring the variable's previous value. `--dry-run` prints the changes without making them. Your ledger stays, and so does an MCP registration: remove it with `copilot mcp remove ledger`, and remove the `ledger` entry from VS Code's `mcp.json` if you added one. To remove the extension too, run `gh extension remove ledger` afterward.

**Upgrading or removing the extension.** The daemon runs the extension's binary, and so does each Copilot session that uses the `ledger` MCP server: Copilot CLI, the GitHub Copilot app, and VS Code start `gh-ledger mcp` and keep it running until the session ends. On Windows, gh can't replace or delete the binary while any of them runs it, so `gh extension upgrade ledger` and `gh extension remove ledger` fail. Before either, stop the daemon and close the Copilot sessions and apps that use the MCP server. To upgrade:

```shell
gh ledger daemon stop
gh extension upgrade ledger
gh ledger daemon start
```

**Ledger format.** A gh-ledger version can upgrade the ledger's format when it first opens it, and an earlier version can't open the ledger after that: it stops with `Database user_version <n> is newer than the supported version <m>.` This version upgrades the ledger to format 6, for `gh ledger import`, the first time its daemon or any command opens it. To keep the option of going back to an earlier version, copy `ledger.db` while the daemon is stopped, before you upgrade.

## The daemon

- `gh ledger daemon run [--port <port>] [--log-file <path>]` runs the receiver in the foreground. It logs to standard output, or appends to `--log-file`.
- `gh ledger daemon start [--port <port>]` starts it as a detached process that logs to `daemon.log`. The daemon gets the null device as its standard input, output, and error, so a script can capture the command's output, for example `started=$(gh ledger daemon start)`.
- `gh ledger daemon stop` asks the daemon to stop, waits up to 15 seconds, and then ends its process if it's still running.

The port defaults to 4318. Only one daemon runs at a time. Copilot's telemetry is lost while no daemon is running.

## Import past sessions

`gh ledger import` records requests from the session logs that Copilot CLI and the GitHub Copilot app keep on your machine, and from Codex's if you ask, such as the requests made before you installed gh-ledger or while its daemon wasn't running. It reads only the metadata listed in [What import reads and keeps](#what-import-reads-and-keeps), prices each request with the rates in use as of the request's start time (see [Imported requests](configuration.md#imported-requests)), and records it with the Git context its log recorded, as the daemon records a live request. For a walkthrough, see [Backfill history after installing](guide.md#backfill-history-after-installing).

```text
gh ledger import (--repo <path> | --all) [--source copilot|codex]... [--since <period>] [--dry-run] [--include-active] [--json]
```

- `--repo <path>` imports only requests made in the Git repository at `<path>`, a working-tree path such as `.`, unlike the reports' repository key. A session is in the repository when the repository root it recorded is that repository's root, or, when it recorded none, when its working directory is in that repository and not in another repository nested inside it. For that, import finds the working directory's repository on disk now, and leaves the session out when it can't, such as when the folder is gone. `--all` imports requests from every repository and from outside any. Use one of the two.
- `--source copilot`, the default, reads Copilot's session logs, `session-state/<session>/events.jsonl` under `~/.copilot`, or under `COPILOT_HOME` when it's set. `--source codex` reads Codex's rollouts under `sessions` and `archived_sessions` in `~/.codex`, or in `CODEX_HOME` when it's set. Repeat `--source` to read both. `--source claude` is refused: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it.
- `--since` takes the reports' periods and defaults to `all`. Logs last written before the period starts aren't read, by the last-write time the file system reports, and requests that started before it aren't recorded.
- `--dry-run` reads the logs and reports what the import would record. It never creates, upgrades, or writes the ledger. It stops with an error when the ledger has a `-wal` or `-shm` file without the other, because reading the ledger then could create the missing file.
- `--include-active` also reads Copilot sessions that are still open, which have an `inuse.*.lock` file in their folder. Without it, they're skipped, and you can import them after they end.
- `--json` writes the report as JSON.

| Source | Versions read | Not imported |
|---|---|---|
| Copilot CLI and the GitHub Copilot app | 1.0.103 to 1.0.114, by `copilotVersion` in the session's first event | Sessions that an earlier version started, which log only session totals, even when a supported version resumed them; sessions of later versions, until gh-ledger supports them; a session's earlier journal files |
| Codex | 0.153.0 to 0.171.0, by `cli_version` | Compressed rollouts (`.jsonl.zst`); tool calls, whose outcome Codex doesn't log; and the requests of another thread that a forked rollout copied |

### Overlap with live spans

Every call is charged once, and live spans win. Requests are never matched by time, model, or token counts:

- A request that GitHub billed carries a service request ID, and so does a compaction, the request that summarizes a long conversation. The live span of the same call carries the same ID, as `github.copilot.service_request_id`. An imported request whose ID a live span has isn't recorded (`Overlap-skipped`), and neither is one whose ID an imported request already has, such as a request in a copy of a session that the GitHub Copilot app made (`Duplicate`).
- A Copilot BYOK request and a Codex request have no such ID. They're recorded only from sessions that have no live spans. In a session with live spans, they're deferred (`Ambiguous-overlap`).
- A session's live spans without a service request ID, such as the spans an earlier gh-ledger recorded, can't be matched to anything, so every imported request of that session is deferred.
- A live span that arrives after its request was imported supersedes the imported row, in the same transaction: the imported row stays in the ledger but no longer counts in any report, MCP tool, budget, or circuit-breaker rule. A live span without a service request ID supersedes every imported row of its session, and any live span supersedes the imported rows of its session that have no ID. Those live spans may not cover every request of the session, so each later import whose scope holds such rows reports them as `Superseded, unmatched`, and its coverage as incomplete. They never count again.

Importing again records nothing twice. A log that was imported completely is skipped while its size, last-write time, and SHA-256 hash stay the same, so import reads such a log once to hash it: the size and last-write time alone could miss a log rewritten in place. Any other log is read again, and the requests recorded from it before count as `Duplicate`. An import stopped by Ctrl+C or an error keeps the requests it recorded, and running it again completes it.

### The import report

The table has a column for each source:

| Row | What it counts |
|---|---|
| Sessions scanned | Logs found |
| Skipped, unchanged | Logs imported completely before, and unchanged since |
| Skipped, active | Open Copilot sessions, without `--include-active` |
| Skipped, out of scope | Logs last written before `--since` |
| Unsupported sessions | Logs that weren't read: an unsupported or unreadable version, another format, a compressed rollout, or a file that couldn't be opened |
| Records parsed | Usage records in the logs that were read |
| Out of scope | Requests from another repository, with `--repo`, or from before `--since` |
| Recorded | Requests recorded |
| Duplicate | Requests recorded before |
| Overlap-skipped | Requests that live spans recorded |
| Ambiguous-overlap | Requests deferred because their session's live spans can't be matched to them |
| Superseded, unmatched | Imported requests in scope that live spans superseded without matching them, from any run; they no longer count, and the live spans may not cover them all |
| Malformed lines | Lines that aren't one JSON event, are longer than 8 MiB, or are incomplete |
| Unsupported usage shapes | Usage records with a missing or invalid field |
| Cost out of range | Requests whose cost is beyond the ledger's range of about $9.2 million |
| Unpriced | Recorded requests without a price; see [Pricing statuses](configuration.md#pricing-statuses) |
| Coverage | `complete`, or `incomplete` when a log or record that may hold requests wasn't imported |

The next line totals the recorded requests' AI credits, GitHub USD, and API-equivalent USD, with the marks of [API-equivalent USD](#api-equivalent-usd); a line with Foundry BYOK USD and local savings follows when either isn't zero. A dry run says what the import would record. Warnings on standard error name the source, the session or rollout, and the line and byte where each problem first occurred, and nothing else from the log; after 20, the rest are counted.

`--json` writes no warnings; it lists them as `diagnostics`. Its fields are `dryRun`, `period`, `from`, `repository` (the `owner/repo` slug, or the root when the repository has none) and `repositoryRoot` (both `null` with `--all`), `coverage`, and:

- `sources`: one object per source, with `source`, `coverage`, `sessionsScanned`, `unreadableStoreEntries` (folders and files of the source's store that couldn't be listed), `sessionsSkippedUnchanged`, `sessionsSkippedActive`, `sessionsSkippedOutOfScope`, `sessionsUnsupported`, `sessionsAggregateOnly` (the unsupported sessions from before Copilot 1.0.103), `recordsParsed`, `recordsOutOfScope`, `recorded`, `duplicate`, `overlapSkipped`, `ambiguousOverlap`, `supersededUnmatched`, `malformedLines`, `unsupportedUsageShapes`, `costOutOfRange`, `journalPredecessors`, `skippedEvents` (events of other types), `unpriced`, `toolLinksRecorded`, and the recorded requests' `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans`.
- `totals`: `recorded` and the same amounts over every source.
- `diagnostics`: each with `source`, `log`, `code`, `event`, `field`, `version`, `count`, `line`, and `byteOffset`. `log` is empty for `unreadableStore`, which is about the source's store rather than one log, and is the session ID for `supersededUnmatched`.

| Exit code | Meaning |
|---|---|
| 0 | Every source's coverage is complete. |
| 2 | The import finished, but some coverage is incomplete; the warnings say why. Everything that could be imported was. |
| 1 | The options are invalid, a running daemon doesn't follow the import rules, or an error stopped the import. |

Skipped active, unchanged, and out-of-scope logs don't make coverage incomplete. A folder or file of a session store that exists but can't be listed does, with a warning such as `Copilot session logs: 1 folder or file couldn't be listed`; a store that doesn't exist only has no logs. So do `Superseded, unmatched` requests: those that started in the `--since` period and, with `--repo`, recorded that repository's root, or its `owner/repo` slug when they recorded no root. When the only gap is sessions from before Copilot 1.0.103, the report's last line suggests `--since`, which leaves them out. With `--repo`, logs of other repositories are read again on every run, because they may hold requests of this one.

Imported requests count in every report and MCP tool, as live ones do, with the branch their log recorded. Tool calls are imported only from the last 7 days, as long as the ledger keeps tool calls; an older request keeps only the number of tool calls it made. A tool call that ended after its request was imported, such as in a session imported with `--include-active`, is added when a later import reads the session again. Imported requests have no agent-invocation span, so `status --cache --turns` shows their `Growth` as `n/a`, and the input-growth ranking of `--bloat` leaves them out.

A daemon started by an earlier gh-ledger doesn't follow the import rules of [Overlap with live spans](#overlap-with-live-spans): a live span it records after an import wouldn't supersede the imported row of the same request, which would then count twice. So while the daemon that's running doesn't say in `daemon.json` that it follows them, `import` stops with exit code 1, and `--dry-run` only warns. Restart the daemon first:

```shell
gh ledger daemon stop
gh ledger daemon start
```

### What import reads and keeps

These are the only fields import keeps, and where each comes from. Fields in no column are never stored.

<!-- import-allowlist -->
| Ledger column | Copilot session log | Codex rollout |
|---|---|---|
| `spans.ingest_source` | `import:copilot` | `import:codex` |
| `spans.trace_id` | SHA-256 of the session ID and the usage ID (`data.usage.accounting.usageId`) of the prompt's first request, which `data.usage.initiator` `user` marks | SHA-256 of the session ID and `payload.root_turn_id`, or `payload.turn_id` |
| `spans.span_id` | SHA-256 of the session ID and `data.usage.accounting.usageId`, or a compaction's `data.serviceRequestId` | SHA-256 of the session ID and `payload.response_id` |
| `spans.operation_name` | `chat` | `chat` |
| `spans.conversation_id` | The session ID, `data.sessionId` of `session.start` | `payload.session_id` of `session_meta` |
| `spans.agent_name` | `data.agentName` of the `subagent.started` event that the record's `agentId`, or its `data.usage.parentToolCallId`, names | `payload.agent_role` of `session_meta` |
| `spans.provider_name` | `github`; for BYOK, `data.usage.providerType` in lowercase | `payload.model_provider` of `session_meta` in lowercase, with `lmstudio` as `lm_studio` |
| `spans.request_model` | `data.usage.model`; for a compaction, `data.compactionTokensUsed.model` | `payload.model` of the turn's `turn_context` |
| `spans.response_model` | `data.usage.responseModel` | — |
| `spans.start_time_unix_ms`, `spans.end_time_unix_ms` | The event's `timestamp` less `data.usage.duration` (`data.compactionTokensUsed.duration`), and the `timestamp` | `timestamp` |
| `spans.input_tokens`, `spans.output_tokens`, `spans.cache_read_input_tokens`, `spans.cache_write_input_tokens` | `data.usage.inputTokens`, `outputTokens`, `cacheReadTokens`, and `cacheWriteTokens` (under `data.compactionTokensUsed` for a compaction) | `payload.usage.input_tokens`, `output_tokens`, `cached_input_tokens`, and `cache_write_input_tokens` (0 when absent) |
| `spans.cache_read_state`, `spans.cache_write_state` | `data.usage.cacheDetailsReported`, or a count above 0; reported for a compaction | Reported; cache writes only when `cache_write_input_tokens` is present |
| `spans.repo_root` | `data.context.gitRoot` of `session.start`, or of the latest `session.resume` | — |
| `spans.repo_slug` | `data.context.repository`: an `owner/repo` slug, or the slug of a remote URL, credentials removed | The slug of `payload.git.repository_url`, credentials removed |
| `spans.branch_name`, `spans.head_commit_sha` | `data.context.branch` and `data.context.headCommit` | `payload.git.branch` and `payload.git.commit_hash` |
| `spans.canonical_usage_id` | SHA-256 of `data.usage.serviceRequestId`, or of a compaction's `data.serviceRequestId`; none for BYOK | — |
| `spans.imported_tool_call_count` | `data.usage.numToolCalls` | — |
| `spans.model_id`, `spans.rate_card_model_id`, `spans.tier_name`, `spans.billing_channel`, `spans.pricing_status`, `spans.rates_version`, `spans.cost_usd_pico`, `spans.saved_usd_pico`, `spans.api_equivalent_usd_pico`, `spans.api_equivalent_status`, `spans.api_rate_card_model_id`, `spans.api_rates_version`, `spans.api_rate_card_sha256` | Priced from the fields above, as for live spans | Priced from the fields above, as for live spans |
| `spans.id`, `spans.recorded_at_unix_ms`, `spans.accounting_state`, `spans.superseded_by` | Kept by the ledger | Kept by the ledger |
| `span_links.trace_id`, `span_links.conversation_id`, `span_links.owner_span_row_id` | The usage record whose response asked for the tool: the latest one of the tool's agent | — |
| `span_links.span_id` | SHA-256 of the session ID and `data.toolCallId` | — |
| `span_links.operation_name` | `execute_tool` | — |
| `span_links.agent_name` | As `spans.agent_name`, from the tool's `agentId` or `data.parentToolCallId` | — |
| `span_links.start_time_unix_ms`, `span_links.tool_name` | `timestamp` and `data.toolName` of `tool.execution_start` | — |
| `span_links.tool_exit_code`, `span_links.status_code` | `data.shellExecution.exitCode` and `data.success` of `tool.execution_complete` | — |
| `span_links.recorded_at_unix_ms` | Kept by the ledger | — |
| `import_sessions.source`, `import_sessions.log_id` | `copilot`, and the session folder's name | `codex`, and the IDs in the rollout's file name, after `archived/` for an archived rollout |
| `import_sessions.log_size_bytes`, `import_sessions.log_last_write_unix_ms`, `import_sessions.log_sha256` | The log file's size and last-write time, and the SHA-256 hash of its bytes up to that size | The rollout file's size and last-write time, and the SHA-256 hash of its bytes up to that size |
| `import_sessions.complete`, `import_sessions.records_recorded`, `import_sessions.records_duplicate`, `import_sessions.records_overlap`, `import_sessions.records_ambiguous`, `import_sessions.imported_at_unix_ms` | The import's own results | The import's own results |
| `import_record_aliases.source`, `import_record_aliases.record_key_sha256` | `copilot`, and SHA-256 of the record's usage ID, or of a compaction's service request ID | `codex`, and SHA-256 of `payload.response_id` |
| `import_record_aliases.span_row_id`, `import_record_aliases.resolution`, `import_record_aliases.recorded_at_unix_ms` | Kept by the ledger | Kept by the ledger |
<!-- /import-allowlist -->

Imported rows leave `parent_span_id`, `server_address`, `server_port`, `pull_request_number`, and `agent_span_id` of `spans` empty, and imported tool calls leave `parent_span_id`, `git_repository_slug`, `git_branch`, and `git_commit_sha` of `span_links` empty.

- **Read, never stored:** each line's `type`. From Copilot logs: `data.producer`, `data.version`, and `data.copilotVersion` of `session.start`; the working directory, `data.context.cwd`, which only `--repo` uses; each event's `agentId`, and `data.toolCallId` of `subagent.started`, which tie a sub-agent's events to its name; `data.usage.isByok`, `data.usage.provider.kind`, `data.usage.initiator`, and `data.usage.parentToolCallId` of usage records; and `data.success`, `data.compactionTokensUsed.requestCount`, and `data.compactionTokensUsed.provider.kind` of compactions, which decide how a record is read. Usage IDs, service request IDs, and tool call IDs are stored only as the SHA-256 hashes above. From Codex rollouts: `payload.id` (the thread ID), `payload.cli_version`, and `payload.cwd` of `session_meta`; `payload.turn_id` of `turn_context`, which ties each turn's model to its records; and `payload.thread_id`, `payload.session_id`, `payload.turn_id`, and `payload.root_turn_id` of `token_usage_record`.
- **Never read:** every other field and event, including user and assistant messages, reasoning, tool arguments and results, error messages, compaction summaries (`summaryContent`) and checkpoint paths, instructions, and attachments. Import skips a line as soon as its `type` shows it's none of the events above, and it counts a line that isn't valid JSON without echoing it. Logs are read in place, never copied.

## Reports

| Command | Reports |
|---|---|
| `gh ledger status [--pricing <path>] [--json]` | The daemon, ledger, rates, Copilot's export setting, warnings, and totals, billed amounts compared with API list prices, and cache hit ratios for today and the last 7 and 30 days |
| `gh ledger branch [--repo <key> \| --all-repos] [--since <period>] [--pricing <path>] [--json]` | Spend by branch in the current repository, or in another or every repository; outside a Git repository, every repository |
| `gh ledger agents [--repo <key>] [--since <period>] [--pricing <path>] [--json]` | Spend by agent in every repository, or in one with `--repo` |
| `gh ledger top-prompts [--repo <key>] [--since <period>] [--limit <n>] [--pricing <path>] [--json]` | The most expensive prompt chains in every repository, or in one with `--repo`; `--limit` is 1 to 100 and defaults to 10 |
| `gh ledger status --cache ...` | Prompt-cache efficiency; see [Prompt-cache efficiency](#prompt-cache-efficiency) |

- A repository key is an `owner/repo` slug, a local repository root for a repository whose `origin` has no `owner/repo` path, or `(none)` for spend outside any repository.
- `--since` takes `today`, `all`, `1d` through `3650d`, or a `yyyy-MM-dd` date; the default is `30d`.
- Tables round money to cents; JSON keeps exact amounts.
- Spend is split into GitHub AI credits (`AI credits` and `GitHub USD`), Foundry BYOK (`BYOK USD`), and local models, whose `Local saved` is the cloud spend they avoided. `Unpriced` counts requests without a price; see [configuration.md](configuration.md). `API equiv USD` compares the same requests with API list prices; see [API-equivalent USD](#api-equivalent-usd).
- An agent shows as `(none)` when Copilot reports no agent name, as its default agent does. Built-in subagents such as `explore` report their own names, and a custom agent reports the `name` in its `.agent.md` file, or the file's name without `.agent.md`.
- `status --json` adds a `cache` object to each period, and each row of `branch --json` and `agents --json` adds `cacheReportedSpans`, `cacheUnreportedSpans`, and `cacheHitRatio`. See [Prompt-cache efficiency](#prompt-cache-efficiency).

### API-equivalent USD

`API equiv USD` is what the same requests would cost at each model's API list prices: a comparison, never part of what you're billed, and never counted toward budgets or the circuit breaker. See [API-equivalent prices](configuration.md#api-equivalent-prices) for how requests are priced.

- `Unpriced` means no request in the row has an API-equivalent price, never $0.00. A row with no requests shows `$0.00`.
- `*` after an amount means some of the row's requests have no API-equivalent price and are left out: their model has no list price, such as the MAI models, the request is local and the local baseline has none, its token counts are inconsistent, or it was recorded before gh-ledger had API list prices (see `gh ledger rates backfill-api-equivalent` in [Rates](#rates)).
- `~` before an amount means it's an estimate: some of its requests reported no cache counters, so all of their input is priced as uncached, which can be more than they cost.
- A footnote under the table counts the requests behind each mark it uses. In `status`, it counts the last 30 days, which include the other periods.
- `status` adds a table that compares, for each period, `GitHub USD` with the same GitHub-billed requests `At API prices`, and `Local saved` with the local requests at API list prices (`Local at API prices`). `GitHub/API` is GitHub USD divided by the API-equivalent amount, with AI credits at $0.01, rounded to two decimal places; it shows `n/a` unless every GitHub-billed request in the period has both prices and the API-equivalent amount isn't 0, and `~` marks an estimate. Retail and avoided amounts are never added to billed spend.

In JSON, amounts are exact. The names follow the other fields: GitHub AI credits used are `aiCredits`, local-offload savings are `localSavedUsd`, and API-equivalent USD is `apiEquivalentUsd`.

- `status --json` periods, rows of `branch --json` and `agents --json`, and rows of `top-prompts --json` add, after `localSavedUsd`: `apiEquivalentUsd` (`null` when no request has an API-equivalent price), `apiEquivalentUnpricedSpans` (the requests without one), and `apiEquivalentEstimatedSpans` (priced requests that reported no cache counters).
- `status --json` periods also add `lenses`, before `cache`. `lenses.copilot` covers the GitHub-billed requests: `spans`, `unpricedSpans`, `aiCredits`, `aiCreditsUsd`, the three API-equivalent fields, and `billedToApiRatio`, the exact `GitHub/API` ratio or `null`. `lenses.local` covers the local requests: `spans`, `localSavedUsd`, and the three API-equivalent fields.

`--pricing <path>` prices the API-equivalent amounts with a complete rates file instead of the results recorded with each request, for example a price list your company negotiated, and changes nothing else; see [Compare with other prices](configuration.md#compare-with-other-prices---pricing). The report's second line, or the line after `Rates:` in `status`, names the file, and the JSON adds `pricingSource`: `kind` (`whatIf`), `path`, `ratesVersion`, and `apiRateCardSha256`, after `repository`, or after `ratesError` in `status`. `--pricing` can't be used with `--cache`. A file that can't be used stops the command with `Invalid pricing file:` and the problem.

## Prompt-cache efficiency

Copilot reports how many of each request's input tokens the model read from its prompt cache and how many it wrote to it. Cache reads cost much less than uncached input, so a falling hit ratio, or a context that grows on every request, raises spend without changing the result. For a walkthrough, see [Find context bloat](guide.md#find-context-bloat).

| Command | Reports |
|---|---|
| `gh ledger status --cache [--by model\|branch\|pr\|agent] [--repo <key>] [--branch <name>] [--pr <n>] [--since <period>] [--json]` | Cache efficiency by model (the default), branch, pull request, or agent, in every repository or in one with `--repo`; `--branch` and `--pr` keep one branch or pull request |
| `gh ledger status --cache --turns (--trace <traceId> \| --conversation <id>) [--json]` | Each model request of one prompt chain, or of one Copilot session, in start order |
| `gh ledger status --cache --bloat [--repo <key>] [--since <period>] [--limit <n>] [--json]` | The prompt chains with the most uncached input and the most input growth; `--limit` is 1 to 100 and defaults to 10 |

- `Input` counts all input tokens, including those read from and written to the cache. `Uncached` is the rest: input minus cache reads and cache writes.
- Copilot sends a cache-read or cache-write count only when it isn't zero, so when a request sends one, the other is 0. A request that sends neither, as many Foundry BYOK and local endpoints do, has unknown cache use. Requests recorded by earlier versions of gh-ledger count as unknown, unless they recorded a cache read or write.
- `Cache reported` counts the requests with known cache use. `Hit %` is cache reads divided by input over those requests, rounded to one decimal place. It shows `n/a` when no request in the row reported cache counters; a real zero shows as `0.0%`. The cache columns are totals over the same requests, so they show `n/a` too, and `*` marks a total that leaves some of the row's requests out. A footnote counts the requests left out.
- A request that missed the cache and wrote nothing to it also sends neither count, so it's left out as unknown, not counted as a miss. For a model whose provider reports cache reads but never cache writes, as some providers with automatic prompt caching do, every miss is like that: its `Hit %` covers only the requests that read from the cache, so it's higher than the model's real hit ratio. When a model's `Cache reported` is less than its `Spans`, its `Hit %` is probably too high.
- `--by pr` groups requests by pull request when the branch's Git config tracks `refs/pull/<n>/head`, as `gh pr checkout` sets up for some pull requests from forks. Other requests show as `(no PR) <branch>`. gh-ledger reads only the local Git config and never asks GitHub.
- A request, or turn, is one model call. `--turns` lists each request with its cache use and `Growth`, the change in input from the previous request of the same agent invocation and model, so a subagent's requests are compared only with each other. `-` marks the first request of an invocation, and `n/a` a request whose agent invocation wasn't recorded.
- `--bloat` ranks prompt chains whose requests all reported cache counters by uncached input, and prompt chains by their largest input growth within one agent invocation and model. Chains with requests that reported no cache counters are ranked separately by estimated uncached input, which counts all input of those requests as uncached, as pricing does. Ties go to the lower trace ID.
- Without `--cache`, `status` takes only `--json`. With `--turns`, give `--trace` or `--conversation`, and optionally `--json`. `--by`, `--branch`, and `--pr` can't be used with `--bloat`, and `--limit` needs `--bloat`.

In JSON, `cacheHitRatio` is the exact ratio or `null`, and token counts that are unknown are `null`:

- `status --json` periods: `cache` has `cacheReportedSpans`, `cacheUnreportedSpans`, `reportedInputTokens`, `reportedCacheReadInputTokens`, and `cacheHitRatio`.
- `status --cache --json` rows: the group (`model`; `agent`; `repository` and `branch`; or `repository`, `pullRequest`, and `branch`, where `pullRequest` is `null` for `(no PR)` rows), then `spans`, `cacheReportedSpans`, `cacheUnreportedSpans`, `inputTokens`, `outputTokens`, `maxInputTokens`, `averageInputTokensPerTurn`, `reportedInputTokens`, `reportedCacheReadInputTokens`, `reportedCacheWriteInputTokens`, `uncachedInputTokens` (observed, over the requests that reported cache counters), `estimatedUncachedInputTokens` (over all requests, counting unknown cache use as uncached), and `cacheHitRatio`.
- `status --cache --turns --json` turns: IDs, time, Git context, agent, model, tokens, `cacheReadState` and `cacheWriteState` (`reported`, `absent`, or `legacy_unknown`), the observed cache and uncached tokens, `cacheHitRatio`, and `inputDeltaFromPreviousTurn`.
- `status --cache --bloat --json`: `byUncachedInput`, `byInputGrowth`, and `byEstimatedUncachedInput`, and `spansWithoutAgent`, the requests left out of input growth.

## Circuit breaker

The daemon checks every prompt against two rules and trips the breaker when one is broken:

- **Credit velocity:** one prompt's billed spend, GitHub AI credits plus Foundry BYOK at $0.01 per credit, exceeds 150 AI credits within 5 minutes.
- **Retry churn:** one prompt makes 5 failed tool calls in a row, with no successful command and no new commit between them. A failed call is a shell command with a non-zero exit code or a tool call that Copilot reports as an error. Copilot doesn't export the commands themselves, so any failing command counts. VS Code's Local agent harness may not report exit codes, so this rule may not trip there.

A trip writes an `ALERT` line to `daemon.log` and shows a desktop notification: a toast on Windows, a notification on macOS, and `notify-send` on Linux when it's installed. It doesn't stop the agent. Each rule trips at most once per prompt, and a trip stays open until you reset it.

- `gh ledger breaker status [--json]` shows the rules and the open trips.
- `gh ledger breaker reset` closes the current branch's open trips. Use `--id <id>` for one trip, `--repo <key> --branch <name>` for another branch, or `--all`.
- `gh ledger breaker config` shows the rules and changes them with `--velocity-credits <credits>` (1 to 1,000,000, with up to 2 decimal places), `--velocity-minutes <minutes>` (1 to 1,440), and `--churn-failures <count>` (2 to 100). `--notifications off` keeps the `ALERT` line but stops desktop notifications.

With `--flag-file on`, the daemon also writes `.copilot-ledger/CIRCUIT_OPEN` in the repository root while a trip is open there, so a wrapper script can stop an agent. The file holds the open trips as JSON, a `.copilot-ledger/.gitignore` keeps it out of commits, and `breaker reset` removes it. Trips outside a local repository get no file. If the daemon logs that it couldn't update the file, run `gh ledger breaker config --flag-file on` again.

## Budgets

A budget is a soft cap in AI credits for one branch. When the branch's billed spend exceeds it, the daemon alerts once, the same way as a breaker trip; nothing is blocked. Foundry BYOK spend counts at $0.01 per credit.

- `gh ledger budget set <credits> [--repo <key> --branch <name>]` sets the current branch's budget, or another branch's. Credits are more than 0 and at most 1,000,000, with up to 2 decimal places. Setting a budget again re-arms its alert.
- `gh ledger budget clear [--repo <key> --branch <name>]` removes it.
- `gh ledger budget list [--json]` lists every budget with its spend.

## Pull request receipts

`gh ledger export-pr` writes the current branch's AI spend receipt to `.github/copilot-ledger/receipts/<branch>-<hash>.md` in the repository, where `<branch>` is the branch name with characters other than letters, digits, `.`, `_`, and `-` replaced by `-`. Commit the file on the branch and push it; the [CopilotLedger PR Receipt action](https://github.com/swigerb/copilot-ledger-action), `uses: swigerb/copilot-ledger-action@v1`, posts it on the pull request.

The receipt covers all spend recorded on the branch: GitHub AI credits by model, Foundry BYOK spend, local-model tokens and the cloud spend they avoided, unpriced requests, circuit-breaker trips, and the most expensive prompt, with the data as JSON. A turn is one model request. Its totals equal the branch's row in `gh ledger branch --since all --json`. Branch and model names appear in code spans, so they never become mentions or links.

The `API-equivalent (retail)` line and column compare the same turns with API list prices; they're never part of the billed amounts (see [API-equivalent USD](#api-equivalent-usd)). The line shows `none` without turns and `Unpriced` when no turn has an API-equivalent price, and otherwise says how many turns it leaves out and how many make it an estimate. The column writes `partial` and `estimate` instead of `*` and `~`, which are Markdown syntax. The JSON data adds `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans` to `totals` and to each model, and keeps `schemaVersion` 1.

- `--stdout` writes the Markdown to standard output instead of the file.
- `--json` writes the JSON data to standard output instead of the file.
- `--pricing <path>` prices the API-equivalent line and column with another rates file, as the reports do. The receipt names the file and its `ratesVersion` but not its folder, and the JSON adds `pricingSource` after `generator`.

`export-pr` needs a branch with at least one commit and a GitHub remote named `origin`, and it refuses a receipt over 65,536 bytes, the most the action posts.

## MCP server

`gh ledger mcp` runs an MCP server on standard input and output, so Copilot can read the ledger and see the circuit breaker. Register it with Copilot CLI using the gh-ledger binary's path, which for the extension is shown under [Get gh-ledger](#get-gh-ledger). In PowerShell on Windows:

```powershell
copilot mcp add ledger -- "$env:LOCALAPPDATA\GitHub CLI\extensions\gh-ledger\gh-ledger.exe" mcp
```

On macOS and Linux:

```shell
copilot mcp add ledger -- ~/.local/share/gh/extensions/gh-ledger/gh-ledger mcp
```

That adds `ledger` to `~/.copilot/mcp-config.json`, which VS Code's Copilot agent harness also reads. The GitHub Copilot app runs the same agent runtime; if it doesn't list the server, add it in the app's MCP settings with the same command. For VS Code's Local agent harness, add the server to `mcp.json` under `servers`, with the binary's path as `"command"` and `["mcp"]` as `"args"`.

The server has five tools. All but `get_cache_efficiency` use the current repository and branch unless you pass `repository` and `branch`:

- `get_branch_ledger`: the branch's spend, top model, budget, circuit-breaker state, and prompt-cache hit ratio.
- `get_top_expensive_prompts`: the branch's most expensive prompt chains, with output tokens and failed tool calls; `limit` (1 to 20, default 5) and `orderBy` (`cost`, `outputTokens`, `failedToolCalls`, or `apiEquivalentUsd`, which lists the chains without an API-equivalent price last).
- `get_agent_breakdown`: the branch's spend and prompt-cache hit ratio by agent.
- `get_cache_efficiency`: prompt-cache efficiency in the current repository, or in `repository`, like `status --cache`. `by` groups rows by `model` (the default), `branch`, `pr`, or `agent`; `branch` and `pullRequest` keep one branch or pull request; `since` takes the same periods as `--since`; and `limit` (1 to 50, default 10) keeps the rows with the most input.
- `set_branch_budget`: sets the branch's budget in AI credits (`aiCredits`).

The first four are read-only. `set_branch_budget` changes only the budget, so Copilot asks before running it; in prompt mode, allow it with `--allow-tool 'ledger(set_branch_budget)'`. While the breaker is open for the branch or the session, or the branch is over budget, every result starts with a `WARNING:` line; for `get_cache_efficiency`, the branch is the current one. No tool can reset the breaker.

`get_branch_ledger`, `get_top_expensive_prompts`, and `get_agent_breakdown` add `apiEquivalentUsd` (`null` when no request has an API-equivalent price), `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans` after `localSavedUsd`, as the reports do; see [API-equivalent USD](#api-equivalent-usd). They're for comparison: budgets and `billedAiCredits` count only billed spend. No tool takes a file path, so `--pricing` isn't available to Copilot.

## Rates

- `gh ledger rates init [--force]` creates a starter rates overlay, `rates.json` in the data directory.
- `gh ledger rates validate [--file <path> | --pricing <path>]` checks the overlay, or another file, with the same rules the daemon uses. With `--pricing`, it checks a complete rates file the way the reports' `--pricing` does.
- `gh ledger rates update` downloads `rates.v2.json` from the latest gh-ledger release, or its `rates.json` if it has none, and keeps it when it's newer than the rates in use, has API list prices (`schemaVersion` 2), and your overlay still applies. It uses your system proxy. Restart the daemon afterward; it reads rates when it starts.
- `gh ledger rates backfill-api-equivalent [--dry-run]` gives the requests recorded before gh-ledger had API list prices their API-equivalent prices, with the rates in use, and prints how many were priced. It changes nothing else, and `--dry-run` writes nothing. See [API-equivalent prices](configuration.md#api-equivalent-prices).

See [configuration.md](configuration.md) for pricing, the overlay format, and where files are kept.

## Privacy

CopilotLedger stores no prompt or response content, working directories, or remote URLs. Repositories are stored as `owner/repo` slugs or local roots, and credentials are stripped. `gh ledger import` keeps only the fields in [What import reads and keeps](#what-import-reads-and-keeps), and never reads prompts, responses, tool arguments or results, or compaction summaries.

## Troubleshooting

- **Nothing is recorded.** Run `gh ledger status`, address its warnings, and restart Copilot.
- **`daemon.log` says `Warning: could not record spans`.** The ledger couldn't be written, for example because the disk is full or the database is damaged, and Copilot is told to retry. The warning repeats at most once a minute; fix the cause shown after it. Only database errors show their message, which never quotes a request's values; any other error shows only its type, because its message could quote the request or a path from it.
- **`gh ledger import` exits with code 2.** Some coverage is incomplete, and the warnings say why; everything else was imported. A session from before Copilot 1.0.103 is never imported, so leave it out with `--since`. A malformed line or an unsupported usage record is skipped on every run, and the rest of its log is imported.
- **`gh ledger import` defers requests (`Ambiguous-overlap`).** Their session has live spans that can't be matched to them, such as spans an earlier gh-ledger recorded, so import leaves them out rather than risk charging a request twice. The live spans still count.
- **`gh ledger import` reports `Superseded, unmatched` requests.** Live spans that can't be matched to them arrived after they were imported and replaced them, as above. The live spans count, but they may not cover every request of the session, so import reports these on every run whose scope holds them; `--since` can leave older ones out.
- **Port 4318 is in use.** Stop the other program or the old daemon, then run `gh ledger daemon start`.
- **`gh extension upgrade` or `remove` fails on Windows.** A program is running the extension's binary: the daemon, or the MCP server of a Copilot session. Run `gh ledger daemon stop`, close the Copilot CLI sessions, the GitHub Copilot app, and VS Code, then try again.
- **Apps started from the desktop record nothing.** On macOS and Linux, apps started outside a terminal may not read your shell profile, so they don't see `COPILOT_OTEL_ENABLED`. Start them from a terminal.
- **Move the data directory.** Set `COPILOT_LEDGER_HOME` before running gh-ledger. The daemon started at sign-in uses it only if your sign-in session sets it too, for example as a Windows user environment variable.
- **A console window flashes at sign-in.** Windows 11 24H2 and later start gh-ledger without a window; earlier versions show it for about a second.
