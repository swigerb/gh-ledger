# Sprint 8: `gh ledger import`: Retroactive Backfill from Local Session Stores

| | |
|---|---|
| Architect | `ledger-architect` |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET 10 (`net10.0`), SDK 10.0.x, zero Docker |
| Plan written | 2026-11-23, against merged Sprint 7 (`main` at `385da81`) |
| Requirements | Issue [#63](https://github.com/swigerb/gh-ledger/issues/63), "`gh ledger import`: retroactive backfill from local session stores (Copilot, Claude, Codex)" |
| Status | Current sprint. Sprint 7 is complete and archived at `.github/plans/archive/sprint-7.md`, with its amendments. |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification section, runs the verification commands, changes the line to `- [x]`, and stops. Tasks 1–19 are sequential, and each builds on the ones before it. Tasks 13–16 fix what an independent review of Tasks 1–12 found; see "Review fixes (Tasks 13–16)".

This sprint ships as one pull request. The orchestrator commits this plan first, together with `.github/plans/archive/sprint-7.md`, then each task as its own commit, `Sprint 8 Task N: <Title>`, on branch `swigerb-sprint-8-ledger-import-backfill`, and opens one pull request titled exactly "Sprint 8: `gh ledger import`: retroactive backfill from local session stores (Copilot, Claude, Codex)", with `Closes #63`. After every task, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors (warnings are errors), and `dotnet test CopilotLedger.slnx` passes exactly the number of tests the task gives, with 0 failed and 0 skipped.

All of this plan's code, tests, and docs are **copy exactly**. The architect built every task in a scratch repository made from `git archive 385da81`, outside this repository's worktree. Each task compiled with `TreatWarningsAsErrors` and passed the whole suite: 1052, 1059, 1083, 1097, 1126, 1162, 1188, 1197, 1215, 1231, 1234, and 1236 tests after Tasks 1–12, then 1251, 1260, 1263, and 1264 after Tasks 13–16, and 1267, 1269, and 1269 after Tasks 17–19. The architect then applied this plan's blocks with a script, task by task and in order, to a fresh `git archive 385da81` with Windows line endings (as a `core.autocrlf=true` checkout has them), built and tested after each task, and got the same counts and, line endings aside, the same files. Copy each block character for character, including every comment line, `using` directive, blank line, and line of Markdown. Don't add argument checks (`ThrowIfNull` and the like), cancellation tokens, `ConfigureAwait`, logging, caching, or fast paths that the code doesn't show, and don't tidy code or reword docs.

Each task's specification lists its files, summarizes its contracts and algorithms for review, and then gives its code under "Code (copy exactly)". The summaries aren't code to write; the code section is. It has two kinds of blocks:

- **New file `<path>`:** write the block as the whole file, with `\n` line breaks, one `\n` at the end, and no byte order mark.
- **Edit k of m in `<path>`, replace: … with: …:** make a file's edits in the order given. Each "replace" block occurs exactly once in the file at the moment you make that edit, so search for it as an exact string. None of the files this sprint edits has a byte order mark; keep each file's line endings as they are. No task edits a `.csproj` file.

Tests are code too: write every test file and test edit exactly as given, in the order given. Don't drop an assertion because it looks redundant, and don't turn an exact comparison into `Contains` or `StartsWith`. Never normalize the product's output in a test (no `ReplaceLineEndings`, `Replace("\r\n", …)`, or `Trim` on an actual value); `CliHarness` already turns `\r\n` into `\n` in `Out` and `Error`, and the tests' `Normalize` helpers apply only to expected literals.

If a block doesn't apply, the build fails, or the test count differs from the task's, stop and report it. Don't change the plan's code to make it fit.

The final section, "Final step: the pull request", isn't a builder task. It has no checkbox, and `ledger-builder` never runs it.

## Tasks

- [x] Task 1: Billing Source and Service Request Identity (CopilotLedger.Core, Otlp, Storage, docs & tests). `NormalizedGenAiSpan` gains `ServiceRequestId`, which the normalizer reads from `github.copilot.service_request_id`, and `BillingSource` (`Endpoint` for every live span, `Provider` for imported requests that their provider billed). `UsageIdentity.CanonicalUsageId` is the identity that a live span and an imported record of the same GitHub-billed call share. One early branch in `Calculate` makes an unlisted provider-billed request `other_provider`, a new pricing status.
- [x] Task 2: Ledger Schema Version 6 (CopilotLedger.Storage & tests). Migration 6 adds `ingest_source`, `canonical_usage_id`, `accounting_state`, `superseded_by`, and `imported_tool_call_count` to `spans`, `owner_span_row_id` to `span_links`, a partial unique index over effective imported rows, and the `import_sessions` and `import_record_aliases` tables. Rows recorded earlier stay live, effective, and unidentified; `RecordAsync` writes the identity of new live rows.
- [x] Task 3: The Live and Import Authority Rules (CopilotLedger.Core, Storage & tests). `UsageAuthority` decides every imported record (recorded, duplicate, overlap-skipped, or deferred) and supersedes imported rows when live rows arrive, so each call is charged once and live spans win. `RecordAsync`, `BackfillFromAgentSpansAsync`, and the new `RecordImportedAsync` run it under `BEGIN IMMEDIATE`.
- [x] Task 4: Effective Rows Everywhere (CopilotLedger.Storage & tests). Every query that counts spans or their tool failures (reports, prompt chains, cache efficiency, turns, bloat, budgets, the circuit breaker's trace activity, what-if pricing, and the API-equivalent backfill) leaves out superseded rows.
- [x] Task 5: Session Logs and the Copilot Gate (CopilotLedger.Core, Otlp & tests). The session-log contracts, a bounded JSONL reader that never holds a line over 8 MiB, deny-by-default field extraction that finds `type` wherever it is, content-free diagnostics, and `CopilotSessionLogSource`'s discovery and version gate: Copilot 1.0.103 to 1.0.114, with older sessions recognized from their first line as aggregate-only.
- [x] Task 6: Copilot Usage Records and Compactions (CopilotLedger.Core & tests). `session.usage_record` and `session.compaction_complete` become records with their service request IDs, sub-agent names (envelope `agentId`, then `parentToolCallId`), resumed Git context, prompt traces, and tool calls; every unsupported shape names its first failing field.
- [x] Task 7: Codex Rollouts (CopilotLedger.Core & tests). `CodexSessionLogSource` reads Codex 0.153.0 to 0.171.0 rollouts under `sessions` and `archived_sessions`: one record per `token_usage_record`, keyed by its response ID, with the model of its turn and its provider; inherited records of another thread and compressed rollouts are skipped.
- [x] Task 8: Import Mapping and the Dry-Run Snapshot (CopilotLedger.Core, Storage & tests). `SessionImportMapper` turns records into chat spans and tool links with hashed IDs. `LedgerSnapshot` decides records as `RecordImportedAsync` would, on a read-only or immutable view, without ever creating, migrating, or writing the ledger.
- [x] Task 9: The Importer (CopilotLedger.Storage & tests). `LedgerImporter` discovers logs, skips active, old, and unchanged ones, prices each record with the current rates at its start time, records batches through the authority rules or evaluates them on a snapshot, checkpoints each log, and reports per-source counts, amounts, and diagnostics.
- [x] Task 10: `gh ledger import` (CopilotLedger.Cli & tests). The command, with `--repo` or `--all`, `--source`, `--since`, `--dry-run`, `--include-active`, and `--json`; a per-source table and totals in AI credits and API-equivalent USD; at most 20 content-free warnings; exit code 0, 2 (incomplete coverage), or 1. `IngestFailureLog` keeps the messages of database and file errors only.
- [x] Task 11: The Import Reference and Privacy Allowlist (docs & tests). `docs/cli.md` documents the command, the overlap rules, the report, and the allowlist of every ledger column import writes, which tests compare with the schema and with what import writes; canary fixtures prove that no content reaches the ledger or the output. `docs/configuration.md` documents how imported requests are priced.
- [x] Task 12: The Backfill Walkthrough (docs & tests). `docs/guide.md` gains "Backfill history after installing", with two samples that `GuideSamplesTests` runs, and the README gains a feature line and a privacy section.
- [x] Task 13: Parser and Pricing Review Fixes (CopilotLedger.Core & tests). Review findings 2, 7, 10, and 12: a repository slug comes only from a URL's path and must be a strict `owner/repo`, so no credential in a query or fragment is kept; a provider-billed request is local only by its provider name; a duration that would start a record before the first instant, and a duplicated Codex field, are unsupported shapes.
- [x] Task 14: Unchanged-Log Hashes, Unreadable Stores, and Dry-Run Companions (CopilotLedger.Core, Storage, Cli, docs & tests). Review findings 3, 8, and 9: `import_sessions.log_sha256`, so a log is skipped only when its size, last-write time, and SHA-256 all match; store folders that can't be listed are reported and make coverage incomplete; a dry run refuses a ledger that has only one of `-wal` and `-shm`.
- [x] Task 15: Late Tool Links, Unmatched Supersession, and Nested Repositories (CopilotLedger.Core, Storage, Cli, docs & tests). Review findings 4, 5, and 6: a tool that ends after its request was imported gets its link; imported rows that live spans superseded without matching them are reported as `Superseded, unmatched`; `--repo` resolves a working directory's repository, so nested repositories stay out.
- [x] Task 16: Daemon Import Rules and Failure-Log Privacy (CopilotLedger.Cli, docs & tests). Review findings 1 and 13: the daemon writes `importRules` to `daemon.json`, and `import` stops while a daemon without it runs; `IngestFailureLog` keeps only SQLite's messages; the privacy tests scan the daemon's log.
- [x] Task 17: Escaped Lone Surrogates Never Stop an Import (CopilotLedger.Core & tests). Review finding R1: `Utf8JsonReader.ValueTextEquals` throws `InvalidOperationException` when it unescapes a lone surrogate, and `JsonFields` caught only `JsonException`, so one line such as `{"\uDC00":1,"type":"user.message"}` in any Copilot or Codex log stopped every import and dry run with exit code 1 and printed the exception's message. A name or type that holds one now equals none of the parser's, so its line is skipped, or its property ignored, like any other unknown one.
- [ ] Task 18: Session Folders That Can't Be Entered (CopilotLedger.Core & tests). Review finding R2: discovery tested `events.jsonl` with `FileInfo.Exists`, which is also false when the file can't be examined, so a Copilot session folder this user can't enter passed for one without a log, and the import reported complete coverage and exited with 0. Discovery now reads `Length`: only a missing `events.jsonl` means no log, and any other error is an unreadable entry, with a warning, incomplete coverage, and exit code 2.
- [ ] Task 19: What Import Keeps, Stated Exactly (docs). Review finding R3: the README's privacy paragraph now names everything import keeps, including session IDs, providers, the repository root, tool outcomes and counts, and each log's size, time, and hash; `docs/cli.md` names the three fields the parsers read that it didn't list.

## Starting point

- Sprint 7 is complete. Its plan and Tasks 1–14 merged in PR #68, squash-merged on 2026-11-23 as `385da81` from branch head `3def684`, which has the same tree. On 2026-11-23, at `385da81`, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors and `dotnet test CopilotLedger.slnx` passes 1018 tests (0 failed, 0 skipped), with SDK 10.0.115. Sprint 7's plan is archived at `.github/plans/archive/sprint-7.md`, with its amendments; this plan's commit adds it. The latest release is still v0.4.1.
- Issue #63 is the requirements source. Its design and all 18 acceptance criteria are implemented here, with the deviations listed under "Design decisions"; its type and member names are proposals. The coordinating session settled these questions on 2026-11-23: compaction calls are imported from `session.compaction_complete`, because they have no usage record; the shared live/import identity is the service request ID; sessions from before Copilot 1.0.103 are reported unsupported from their first line, never scanned for usage records; no census of BYOK provider values is run; and the conservative rule I3 stays (see "The authority rules").
- Merged behavior this plan depends on (read from the code at `385da81`):
  - **Pricing.** `JsonRateCardCalculator.Calculate` normalizes the response model, or else the request model, returns `NotBillableOperation` and `InvalidUsage` first, and then classifies the endpoint: local (loopback, a local host or provider name, or a known local model without a host), Foundry BYOK (host suffix or provider name), GitHub (no host, or a GitHub host suffix), and otherwise `UnrecognizedEndpoint`. Since Sprint 7's Tasks 12 and 13 (#67), the GitHub and Foundry BYOK blocks try the request model when the channel lacks the response model, and `CalculateApiEquivalent` falls back to `RequestModel` when no section of the card knows `ModelId`; the built-in GitHub models have vendor and dated aliases such as `claude-haiku-4-5-20251001`. Every result carries `ApiEquivalent`. The card lists `localOffload.providerNames` `ollama`, `lm_studio`, `foundry_local`, and `llama_cpp`, and `foundryByok.providerNames` `azure.ai.inference`.
  - **Storage.** `LedgerMigrations` has versions 1–5; a ledger whose `user_version` is newer than the build's latest is refused (`InvalidOperationException`), so gh-ledger 0.4.1 can't open a version 5 ledger, and Sprint 7's binaries won't open a version 6 one. `RecordAsync` validates every entry before writing and inserts with `ON CONFLICT (trace_id, span_id) DO NOTHING` in one transaction (Microsoft.Data.Sqlite's `BeginTransaction` is `BEGIN IMMEDIATE`). `BackfillFromAgentSpansAsync` assigns `agent_name`, `conversation_id`, `agent_span_id`, and Git columns from `invoke_agent` links. The daemon prunes `span_links` older than 7 days.
  - **Live path.** `OtlpTraceDecoder` → `GenAiSpanNormalizer` → `LedgerIngestor` → `Calculate` → `RecordAsync`, then links, the backfill, and the circuit breaker. `IngestFailureLog` writes `Warning: could not record spans: <type>: <message>` to `daemon.log` at most once a minute; `DaemonCommandTests` pins a `SqliteException` line.
  - **CLI.** Commands return 0 or 1; validator errors go to standard error with exit code 1. `CliContext` holds `CopilotHome` (`COPILOT_HOME`, else `~/.copilot`), and `CliHarness` points it at `<root>/copilot-home`. `RateCardFiles.LoadBase` and `Load` give the rates in use, as `rates backfill-api-equivalent` loads them. `LedgerFormat`, `TableWriter`, and `Period` are internal, and the test project has no `InternalsVisibleTo`.
  - **Tests.** xUnit v2 (`xunit` 2.9.3). `GuideSamplesTests` runs every `docs/guide.md` sample in a `GuideWorld` state and requires exit code 0, empty standard error, and exact output. The test project copies only `docs/configuration.md` and `docs/guide.md` to its output directory, and `CopilotLedger.Storage.csproj` and `CopilotLedger.Tests.csproj` start with a byte order mark, so no task edits them; the allowlist test finds `docs/cli.md` from the repository root instead.
  - **CI.** `.github/workflows/ci.yml` smoke-tests the published binary with `branch --all-repos --since all --json`, `status`, `rates validate`, and MCP `tools/list`. This sprint adds no JSON field before an existing one, so those checks keep passing.

## Verified facts (retrieved 2026-11-23)

### Copilot session logs: the orchestrator's evidence

Source: the orchestrator's metadata-only census of a real Copilot store (event types, key paths, value kinds, counts, and version strings; never values), its paired captures of synthetic sessions with live OpenTelemetry, and sanitized structure dumps (`copilot-1.0.103-main-shapes.jsonl`, `copilot-1.0.103-subagent-shapes.jsonl`, `copilot-1.0.114-subagent-shapes.jsonl`, `copilot-1.0.114-resume-compaction-shapes.jsonl`), all gathered on 2026-11-23. Copilot CLI 1.0.114 came from the official `copilot-win32-x64.zip`, whose SHA-256 matched `SHA256SUMS.txt`.

- A session is `<COPILOT_HOME>/session-state/<session ID>/events.jsonl`, one JSON event per line: `{"type", "data", "id", "timestamp", "parentId", "agentId"?}`. Its first line is `session.start`, with `data.sessionId`, `data.producer` `copilot-agent`, `data.version` 1, `data.copilotVersion`, and `data.context` (`cwd`, and in a Git repository `gitRoot`, `branch`, `headCommit`, `baseCommit`, `repository`, `repositoryHost`, `hostType`). `session.resume` has a `context` and no version. An open session has an `inuse.<pid>.lock` file in its folder.
- **Per-call records start with Copilot 1.0.103.** In the census, 277 sessions span versions 0.0.419 to 1.0.98 and the GitHub Copilot app's runtime `1.0.106-8.unstable.r43566476370.g869eef3`; `session.usage_record` (44,274 records) occurs only in the 1.0.106-8 sessions. Earlier versions write only totals (`session.shutdown`, `session.usage_checkpoint`). Of 279 sessions, all start with a well-formed `session.start`; 219 (1.98 GB) are aggregate-only and 60 (1.9 GB) have usage records.
- `session.usage_record.data.usage` has `model`, `responseModel` (absent in 1.0.103; in 1.0.114 the dated vendor id, such as `claude-haiku-4-5-20251001` for `claude-haiku-4.5`), `inputTokens` (which includes cache reads and writes), `outputTokens`, `cacheReadTokens`, `cacheWriteTokens`, `cacheDetailsReported`, `isByok`, `initiator`, `duration` (milliseconds), `numToolCalls`, `accounting.usageId`, and content-free counters. Non-BYOK records (39,862) add `provider.{id, kind: "copilot", label}` and `serviceRequestId`, on every one of them; BYOK records (4,412) add `byokKind`, `modelProvider`, and `providerType`. 22,303 sub-agent records carry the envelope `agentId` and `usage.parentToolCallId`.
- **Paired identity, 29 of 29 calls** (two captures; 28 non-compaction calls, 18 of them across a resume, and one compaction): a record's `serviceRequestId` equals its live chat span's `github.copilot.service_request_id`, `apiCallId` equals `gen_ai.response.id`, `usageId` equals `apiCallId`, and the session ID equals every span's `gen_ai.conversation.id`; `model` equals `gen_ai.request.model`, and the four token counts equal the span's. `providerCallId` matches nothing. The attribute `github.copilot.service_request_id` is verified by capture only: the OpenTelemetry reference doesn't list it.
- **Compactions aren't usage records.** A background compaction's live chat span (`github.copilot.request.type` `compaction`, provider `github`, request model `claude-haiku-4.5`, no response model, billed) matches no `session.usage_record`. Its usage is `session.compaction_complete.data.compactionTokensUsed`: `requestCount` 1, `inputTokens` 112,223 (the span's), `outputTokens` 18, `cacheReadTokens` 18,967, `cacheWriteTokens` 93,251, `model`, `duration` 891, `provider.kind` `copilot`, without `isByok` or `cacheDetailsReported`; `data.serviceRequestId` equals the span's service request ID. The event also holds content (`summaryContent`, `checkpointPath`, `error`, `failureReason`, `failureDiagnostics`) and IDs that match nothing (`requestId`, `clientRequestId`). In the real store, 1,942 of 2,017 such events have `compactionTokensUsed`, 1,684 have `data.serviceRequestId`, and 1,293 have an envelope `agentId`.
- **Timing.** A record's envelope `timestamp` equals the live span's end within 2 ms, and `timestamp − duration` is within 0.3 s of its start.
- **Sub-agents.** `subagent.started.data.agentName` (such as `explore`) equals the live `invoke_agent` span's `gen_ai.agent.name`; the top-level agent has none. In 1.0.103 a sub-agent's records carry both the envelope `agentId` of its `subagent.started` and `parentToolCallId` equal to that event's `data.toolCallId`; in 1.0.114 only `agentId`. A record's tool events follow it, with the same `agentId` for a sub-agent.
- **Repository.** In a repository whose remote is `https://user:token@github.com/octo-org/octo-repo.git`, `context.repository` is the slug `octo-org/octo-repo`, `gitRoot` the repository root, `cwd` a subdirectory, and `headCommit` 40 hex characters. Live 1.0.114 spans carry no Git attributes.
- 17 sessions of the census are open (`inuse.*.lock`), 3 lines are malformed or longer than 8 MiB, and no session has a `journal.link` event.

### GitHub documentation

- [Copilot OpenTelemetry reference](https://docs.github.com/en/copilot/reference/enterprise-administrators/opentelemetry-reference): chat spans carry `gen_ai.conversation.id` ("Session identifier"), `gen_ai.response.id`, `gen_ai.provider.name`, the usage attributes, and `github.copilot.request.type` ("Set to `compaction` on spans emitted for conversation compaction"); `github.copilot.service_request_id` isn't listed.
- [Adding LLM models to GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/use-byok-models): BYOK provider types are `openai` (also for "services with an OpenAI-compatible API, such as Ollama, vLLM, and Foundry Local"), `azure`, and `anthropic`.

### Codex at `rust-v0.171.0`

Source: [openai/codex](https://github.com/openai/codex) at tag `rust-v0.171.0` (commit `ccf9be8cc458bd6b8336fe84d68d8893f2691a5e`, release 0.171.0 of 2026-11-20); paths under `codex-rs/`.

| Fact | Source |
|---|---|
| A rollout line is `{"timestamp": "<UTC>", "type": "<variant>", "payload": {…}}`; variants include `session_meta`, `response_item`, `compacted`, `turn_context`, `token_usage_record`, and `event_msg` | `history/src/lib.rs:469-480`, `history/src/rollout_payload.rs:36-82` |
| `TokenUsageRecord { thread_id, turn_id, session_id, root_turn_id, response_id, usage, turn_token_usage, thread_token_usage }`, one per completed response with usage; `TokenUsage { input_tokens, cached_input_tokens, cache_write_input_tokens (default 0), output_tokens, reasoning_output_tokens, total_tokens }` | `protocol/src/protocol.rs:2575-2608`; `core/src/session/mod.rs:5749-5771` |
| The same `TokenUsageRecord` at `rust-v0.153.0`, `0.155.0`, `0.160.0`, `0.165.0`, and `0.170.0`; absent at `0.152.0` | `protocol.rs` at each tag |
| `token_count` events carry cumulative totals that can be reset | `protocol/src/protocol.rs:2610-2680` |
| `SessionMeta`: `session_id` (the root thread's ID), `id` (the thread), `cwd`, `cli_version`, `agent_role?`, `model_provider?`; `git: { commit_hash?, branch?, repository_url? }` with credentials removed | `protocol/src/protocol.rs:3514-3645, 3883-3899`; `protocol/src/sanitized_git_url.rs:11-18` |
| `TurnContextItem { turn_id?, …, model }`, once per user turn | `protocol/src/protocol.rs:3694-3800` |
| A child thread keeps inherited items but drops `TokenUsageRecord`; the first `session_meta` of a file is its thread | `core/src/agent/history.rs:87-135`; `rollout/src/recorder.rs:1204-1210` |
| `CODEX_HOME/sessions/YYYY/MM/DD/rollout-YYYY-MM-DDTHH-MM-SS-<threadId>[_<rolloutId>].jsonl`, and `archived_sessions`; `CODEX_HOME` when set and non-empty, else `~/.codex` | `rollout/src/lib.rs:88-89`; `rollout/src/rollout_file_name.rs:10-74`; `utils/home-dir/src/lib.rs:5-60` |
| Compressed rollouts are `.jsonl.zst`, behind a feature that's off by default | `rollout/src/compression.rs:26, 371`; `features/src/lib.rs:1495-1500` |
| Built-in providers `openai`, `amazon-bedrock`, `gcp`, `amazon-bedrock-runtime`, `ollama`, and `lmstudio` | `model-provider-info/src/lib.rs:84-93` |

### Claude Code

[Manage sessions](https://code.claude.com/docs/en/sessions.md): "The entry format is internal to Claude Code and changes between versions, so scripts that parse these files directly can break on any release." The producer is closed source, and there are no local samples.

### .NET and SQLite (probed in the scratch build, SDK 10.0.115, Microsoft.Data.Sqlite 10.0.15)

- `BeginTransaction()` starts `BEGIN IMMEDIATE`; `BeginTransaction(deferred: true)` doesn't take the write lock.
- `ALTER TABLE … ADD COLUMN` on a `STRICT` table accepts `NOT NULL DEFAULT … CHECK (…)` with a `CHECK` that names other columns; existing rows take the defaults.
- A partial unique index is enforced even under `INSERT … ON CONFLICT (trace_id, span_id) DO NOTHING`, so the authority routine checks before it inserts. `… DO NOTHING RETURNING id` returns no row on a conflict.
- A `Mode=ReadOnly` connection to a WAL database creates `-wal` and `-shm` when they're missing; a URI data source `file:///…/ledger.db?immutable=1` reads without creating them, so the dry run uses it when neither exists. `Mode=ReadOnly` on a missing file creates nothing.
- `Utf8JsonReader` throws on content after the root value and on a truncated object; `ValueTextEquals` unescapes (`"t\u0079pe"` equals `type`).

### Assumptions (not verified)

| # | Assumption | If it's wrong |
|---|---|---|
| A1 | Copilot 1.0.104 to 1.0.113 write usage records and compactions as 1.0.103, 1.0.106-8, and 1.0.114 do | A changed field is an unsupported shape (coverage incomplete), never a wrong charge |
| A3 | `initiator` `user` on a main-agent record starts a new prompt | Prompt chains group differently; no amount changes |
| A5 | `usage.providerType` holds the BYOK provider type | BYOK requests are `other_provider`: unpriced, never mispriced |
| A7 | Service request IDs are per-call UUIDs | A collision would merge two charges; only non-nil `D`-form UUIDs of GitHub-billed calls count |
| A8 | Codex `input_tokens` includes cache reads and writes | Records with more cached than input tokens are unsupported shapes |
| A9 | A Codex record's timestamp is close enough to its response's start to pick a rate window | Only a window boundary at 00:00 UTC could differ |
| A11 | Copies of a session that the GitHub Copilot app forks keep `usageId` and `serviceRequestId` | Identities are global, so a copy is a duplicate either way |
| A12 | `compactionTokensUsed` with `requestCount` 1 is the one billed call its `serviceRequestId` names (proven once) | Any other `requestCount`, or `success` other than true, is an unsupported shape |
| A13 | Live chat spans keep `github.copilot.service_request_id` | Live spans without it are unidentified: their sessions defer imports, never double count |

## Design decisions

### Scope and sources (deviation D7)

Import reads Copilot CLI and GitHub Copilot app session logs (`--source copilot`, the default) and Codex rollouts (`--source codex`). Claude Code's format is internal and undocumented, so `--source claude` is refused with a fixed message and nothing is read; `ingest_source` reserves `import:claude`. VS Code and JetBrains stores stay out of scope, as #63 says.

### Which sessions are read (D1, D2, D8)

- **Copilot gate.** A log is read only when its first well-formed event is `session.start` from `copilot-agent`, schema version 1, with its own session ID and a `copilotVersion` whose `MAJOR.MINOR.PATCH` core is 1.0.103 to 1.0.114 (a `-` or `+` suffix is allowed, so `1.0.106-8.unstable.…` is read and `1.0.115-0` isn't). A newer core is `unsupportedProducerVersion`. An older one is **aggregate-only**: reading stops right after `session.start`, so the 1.98 GB of such history here costs one line per session, and the report suggests `--since` when that's the only gap. A resumed segment of a supported session can't be gated (`session.resume` has no version); per-record shape validation guards it.
- **Codex gate.** `session_meta.payload.cli_version` core 0.153.0 to 0.171.0, its IDs UUIDs; usage only from `token_usage_record`, keyed by `response_id`; never `token_count`, `compacted`, or turn and thread totals; the model of the record's turn (`turn_context`), else the latest; records of another thread are skipped as inherited; `.jsonl.zst` rollouts are listed, reported unsupported, and never opened; Codex logs no tool outcomes, so no tool calls.
- **Deny by default.** Each line is read with a bounded buffer (8 MiB; a longer line is discarded unread and counted). The event's `type` is found first, wherever it is in the object; any type other than the handful the parser knows is skipped without reading anything else. Known events are read only through fixed lists of paths, each with a length limit. Diagnostics carry only codes, the parser's own field names, validated IDs and versions, counts, line numbers, and byte offsets.

### The shared identity (D3, D4)

- **`canonical_usage_id`** is the SHA-256 of (`gh-ledger-usage`, `v1`, `github-service-request`, the service request ID in lowercase `D` form), each part length-prefixed. It's set, on both paths by the same function (`UsageIdentity.CanonicalUsageId`), exactly when the request's billing channel is GitHub AI credits, its provider name is exactly `github`, and the ID is a non-nil UUID in `D` form; it's global, not per session. Live spans take the ID from `github.copilot.service_request_id`; imports from `usage.serviceRequestId` of non-BYOK usage records and `data.serviceRequestId` of compactions. BYOK, local, Codex, and unidentified calls have none. `gen_ai.response.id` isn't read.
- **Record aliases.** `import_record_aliases` maps (source, SHA-256 of the record's kind and key) to the row that accounts for it. Keys: a Copilot usage record's `accounting.usageId`, a compaction's service request ID (kind `compaction`, so it never collides with a usage ID), and a Codex `response_id`. Raw keys are never stored.

### The authority rules (D5, D6)

Live spans are authoritative. After every transaction:

- **I1.** No canonical ID belongs to both an effective imported row and an effective live row.
- **I2.** In a session with an effective live row, every effective imported row has a canonical ID.
- **I3.** A session with an effective live row that has no canonical ID has no effective imported rows.
- **I4.** Every alias names a row, and a `live` alias a live row; only imported rows are ever superseded, and nothing is deleted.

An imported record resolves as `Duplicate` when its alias exists or another imported row has its canonical ID; `OverlapSkipped` when a live row has its canonical ID; `AmbiguousOverlap` (deferred) when recording it would break I2 or I3; and otherwise `Recorded`. A live row supersedes the imported row with its canonical ID and the imported rows of its session that it can't be matched to (I2, I3), in its own `BEGIN IMMEDIATE` transaction; so does a session that `BackfillFromAgentSpansAsync` assigns to a live row later. I3 is deliberately conservative: one live span without a service request ID, such as every span recorded before this sprint, defers all of its session's imports. `docs/cli.md` says so. There's no matching by time, model, or token counts anywhere. The unique index covers only effective imported rows (`ux_spans_effective_import_usage`), so two live spans with the same ID both stay effective, as before. Imported rows that a live span supersedes without sharing their canonical ID (I2, I3) may hold calls that the live spans don't cover, so every later import whose scope holds them reports them as `Superseded, unmatched`, with incomplete coverage (Task 15); they're never made effective again.

### Pricing imported requests (D9, D10, D11)

`NormalizedGenAiSpan.BillingSource` is `Endpoint` for every live span and for GitHub-billed Copilot imports (provider `github`, no host), which therefore price exactly as their live spans do, including #67's request-model fallback. Copilot BYOK and Codex imports are `Provider`: one early branch in `Calculate`, after the token checks, makes a `Provider` request whose provider name isn't in `localOffload.providerNames` or `foundryByok.providerNames` `Unpriced` with the new status `other_provider`, 0 AI credits, and its usual `ApiEquivalent`; a listed name continues into the existing local or Foundry block. A `Provider` request is local only by its provider name: the rule that makes a request for a known local model without a host local applies only to `Endpoint` spans (Task 13). A BYOK request's provider name is its `providerType`, lowercased; Codex's is `model_provider`, with `lmstudio` as `lm_studio`. Each record is priced with the rates in use as of its start time; a request outside every window is `no_rate_for_date`.

### Storage: schema version 6 (D6, D14, D16)

Migration 6 (Task 2) adds provenance and authority columns to `spans`, `owner_span_row_id` to `span_links`, and two tables. Existing rows become live (`otlp`), effective, and unidentified; existing links have no owner. An imported tool call becomes an `execute_tool` link owned by the usage record whose response asked for it, only within the 7 days the daemon keeps links; `numToolCalls` goes only to `imported_tool_call_count`. A log's checkpoint (`import_sessions`: size, last-write time, the SHA-256 of the bytes that were parsed, `complete`, and its counts) is written after its last batch, in its own transaction; a log is skipped only when it was imported completely and its size, last-write time, and SHA-256 are unchanged (Task 14). A record read again whose own row still counts gets the links of tool calls that ended since (Task 15).

### Dry run (D15)

`LedgerSnapshot` never calls `InitializeAsync`: with no ledger, it's empty; with neither `-wal` nor `-shm`, it opens `immutable=1`; with both, `Mode=ReadOnly`, in one deferred read transaction; with only one of them, it refuses, because SQLite could create the other (Task 14). It decides every record with the same function as the write path, plus an in-memory overlay of its own decisions. A version 5 ledger is evaluated as one without imports whose live rows are all unidentified.

### CLI (D12, D21, D22, D24, D25)

`gh ledger import (--repo <path> | --all) [--source copilot|codex]... [--since <period>] [--dry-run] [--include-active] [--json]`. `--repo` is a working-tree path, resolved with `FileSystemGitContextResolver`; a session matches by the root it recorded, else by the repository of its working directory, resolved on disk the same way, so a nested repository doesn't match (Task 15). Exit codes: 0 when every source's coverage is complete, 2 when the import finished with incomplete coverage, and 1 for invalid usage, a running daemon without the import rules, or a fatal error. The table has a row per count and a column per source, then the recorded requests' AI credits, GitHub USD, and API-equivalent USD. Warnings about the logs go to standard error, at most 20, then a count; `--json` lists them all as `diagnostics` instead. While a daemon without `importRules` in `daemon.json` holds its lock, `import` exits with 1 and `--dry-run` warns (Task 16). `CliContext.CodexHome` is `CODEX_HOME` when it's set and non-empty, else `~/.codex`, as Codex reads it. `IngestFailureLog` keeps only a `SqliteException`'s message (Task 16), so neither a `JsonException` that quotes a request nor an I/O error that names a path from it reaches `daemon.log`.

### Docs

`docs/cli.md` gains "Import past sessions", with the overlap rules, the report, exit codes, and "What import reads and keeps": the allowlist table between `<!-- import-allowlist -->` markers that a test parses, and the fields read but never stored. `docs/configuration.md` gains "Imported requests". `docs/guide.md` gains "Backfill history after installing", whose samples run in a new `GuideState.Backfill`. The README gains a feature line and a "Privacy" section; the repository has no `SECURITY.md`.

### Deviations from issue #63

| # | Issue | This plan | Why |
|---|---|---|---|
| D1 | "Fail explicitly for unsupported versions" | A version gate: Copilot 1.0.103 to 1.0.114 and Codex 0.153.0 to 0.171.0, checked at the first event; fixtures pin both ends | Only those versions are verified; everything else fails closed |
| D2 | — | Copilot sessions before 1.0.103 are reported as aggregate-only and unsupported from their first line, and never imported, even after a newer Copilot resumed them | They have no per-call records; importing totals would invent spans and double count; scanning them on every run would cost 1.98 GB here |
| D3 | A canonical usage ID from a "proven shared call ID" | The service request ID, global, only for GitHub-billed calls with provider `github` | It's the only ID shared by usage records, compactions, and live spans (29 of 29 paired calls) |
| D4 | — | Record aliases per source, hashed, with no fallback key | Fork copies dedupe; raw IDs could retrieve stored responses from a provider |
| D5 | Live is authoritative; ambiguous overlap is deferred | Invariants I1–I4, with I3 deferring every import of a session that has an unidentified live span | Covers both arrival orders and legacy rows without fuzzy matching |
| D6 | `UNIQUE … WHERE canonical_usage_id IS NOT NULL` | Unique only over effective imported rows, plus a lookup index | Never changes how live spans are recorded; superseded rows don't conflict |
| D7 | A Claude Code adapter | `--source claude` is refused; nothing is read | The format is internal and undocumented; #63 says to fail explicitly without a verified adapter |
| D8 | Codex candidate fields from `token_count` | Only `token_usage_record`, keyed by `response_id`; cache writes reported when present | The verified contract at `rust-v0.171.0`; `token_count` totals are cumulative and resettable |
| D9 | A billing hint on imported spans | `BillingSource` with one early branch in `Calculate`; GitHub-billed imports keep `Endpoint`; a `Provider` request is local only by its provider name (Task 13) | Same classification as their live spans, including #67's fallback; a localized edit; a provider-billed request has no endpoint to be local by |
| D10 | — | New pricing status `other_provider` for direct-provider requests, which keep their API-equivalent price | `unrecognized_endpoint`'s fix (add a host) would mislead |
| D11 | — | A BYOK provider name is `providerType`; a BYOK request through a local OpenAI-compatible server is `other_provider` | No census of provider values (privacy rule); unpriced, never mispriced |
| D12 | Exit codes "following the repository's conventions" | 0, 2 for incomplete coverage, 1 (also while a daemon without the import rules runs, D29) | The repository uses 0 and 1; 2 lets scripts tell partial results apart |
| D13 | Optional `ingestSource` counts over MCP | None; imported rows reach every MCP tool through the store | No schema change for clients; deferred |
| D14 | Tool links within 7 days | Links owned by their record (`owner_span_row_id`); a superseded owner's links don't count | Avoids doubled failure counts when a live span supersedes an import |
| D16 | Checkpoints with `last_record_key`, upserted by `UpsertImportSessionAsync` | `RecordImportedAsync` writes the checkpoint, in its own transaction after a log's last batch; no `last_record_key`; a log is skipped only when its size, last-write time, and `log_sha256`, the SHA-256 of the bytes parsed, all match (Task 14) | A crash before the checkpoint only makes the next run read the log again, and its records resolve as duplicates; size and time are only hints, as #63 says, so a same-size rewrite is read again |
| D20 | — | Compactions are imported from `session.compaction_complete` with a fixed allowlist | They're billed calls without usage records |
| D26 | Every query "reports coverage limitations" | `gh ledger import` reports coverage (its table, JSON, warnings, and exit code), including the imported rows in its scope that live spans superseded without matching them (`Superseded, unmatched`, Task 15); reports, receipts, and MCP exclude superseded rows without notes | Export and audit (#64, #65) don't exist yet; notes everywhere would change many pinned outputs; deferred |
| D27 | `ISessionLogSource` streams records; `Discover(SessionDiscoveryOptions)`; `ProviderKind`, `ProviderId`, `IsByok` | `ReadAsync` returns one `SessionLogReadResult` per log; `Discover(string home)`; records carry `ProviderName` and `BillingSource` | The gate and tool ownership need the whole log; the CLI resolves homes and the importer applies `--since` so it can count what it skips |
| D28 | `cacheDetailsReported:false` → `Absent` | A record with `cacheDetailsReported:false` and a nonzero cache counter keeps that counter `Reported`; a consistent record, both counters 0, is `Absent` | Sprint 6's D2: a nonzero counter can only have been reported, and `Absent` would price cached input as uncached (review finding 11; a clarification, not a change) |
| D29 | — | A write-mode import exits with 1 while a daemon without `importRules` in `daemon.json` holds its lock; a dry run only warns (Task 16) | An earlier daemon records live spans without the authority rules, so a delayed live export of an imported call would count twice; both builds call themselves 0.4.1 |
| D30 | `--repo` imports only that root | A record without a recorded root is in scope when the repository of its working directory, resolved on disk now, is that root; one that can't be resolved is out of scope (Task 15) | A path prefix can't tell a nested repository apart; #63 allows resolving repository identity, never today's branch, for filtering |

### Acceptance criteria traceability

| #63 criterion | Tasks | Tests or deviation |
|---|---|---|
| Privacy canaries absent from the database, `-wal`, `-shm`, stdout, stderr, JSON, and the ingest-failure log | 5, 6, 7, 10, 11, 13, 16 | IP1 (all six canary kinds and more, Copilot and Codex, text, JSON, dry run, and `daemon.log`), IP4 (the daemon's failure log), CS7, CS8, CU12, CU14, CC6, CX9, CX12, CX13, IC5, IC9; Claude per D7 |
| Allowlist test with a schema snapshot | 11 | IP2, IP3 |
| Idempotency, convergence after appends, truncation, replacement, same-size rewrites, cancellation, and restart | 3, 9, 14, 15 | UA3, UA13, IR1, IR2 (4), IR3, IR16 (a same-size rewrite with its old last-write time), IR18 (tool completions appended later) |
| Overlap: one charge live-first, import-first, repeated, and concurrent; identical-looking calls stay distinct; a delayed live export adds nothing | 3, 16 | UA1, UA2, UA3, UA4, UA5, UA6, UA17; IC10 and DM1 (no import beside a daemon without the import rules, D29) |
| Legacy ambiguity: deferral, incomplete coverage, atomic supersession, every query excludes superseded rows, no fuzzy matching | 3, 4, 8, 9, 10, 15 | UA7–UA10, UA15, ER1–ER10, SN3, IR9, IC6, IR19 and IC14 (`Superseded, unmatched`); coverage notes outside `import` per D26 |
| Copilot mapping: `cacheDetailsReported:false` → `Absent`; sub-agent names; resume; Copilot provenance priced as GitHub; Foundry and local provenance; unresolved stays unpriced; no fall-through to Copilot billing; no synthetic endpoint | 1, 6, 8, 9, 13 | CU3, CU4 (2), CU6, CU1 with IR7 and IR15, BI1, BI2, BI3, BI5, BI8, CU7, MP1, CC1–CC6; nonzero counters with `cacheDetailsReported:false` per D28 |
| Claude mapping | — | Deviation D7: refused (IC1, IP1) |
| Codex mapping: repeated snapshots count once; resets and missing IDs explicit; `repository_url` credentials stripped | 7, 9, 13 | CX6, CX7, CX9, CX13, CX14, IR10 |
| `--repo` imports only that root; `--repo` with `--all`, or neither, fails validation | 9, 10, 15 | IR5 (nested and unresolvable working directories, D30), IC1, IC2, IC3 |
| `--dry-run` leaves the ledger byte for byte and prints accurate counts | 8, 9, 10, 14 | SN1, SN2, SN4, SN7, IR9, IC6, IC12 |
| `inuse.*.lock` sessions skipped by default, imported with `--include-active` | 5, 9, 10 | CS1, IR6, IC3 |
| Malformed and oversized lines and unknown types counted without echo; content before `type` and canaries in malformed JSON don't leak; unsupported shapes mark incomplete coverage | 5, 6, 10, 11, 13, 14 | CS4–CS10, CU8, CU13, CC4, CC7, CX14, IC5, IP1; stores that can't be listed: CS15, CX15, IR17, IC13 |
| Missing historical branch stays unknown; aggregate tool counts make no spans | 9 | IR13, IR8, CU11 |
| Imported spans in `branch`, `agents`, `top-prompts`, and MCP with Git attribution; no `invoke_agent` totals | 6, 10 | IC7, IC8, CU11 |
| No `double` money; decimal pricing through `JsonRateCardCalculator` | all | Review; BI*, IR7, IR15, IC3, IC4 use `decimal` and pico-USD integers |
| Hermetic tests | all | `TempDirectory`, `CliHarness` homes (`copilot-home`, `codex-home`), builders that write synthetic logs; no network, no `git` |
| `docs/cli.md` and `docs/guide.md` updated; `GuideSamplesTests` passes | 11, 12 | IP2, IP3, GS1, GS2 |
| Build and tests exit 0 with warnings as errors | all | Every task |

### Review fixes (Tasks 13–16)

An independent review of Tasks 1–12, as built in the scratch repository at `4211484`, found the problems below. Tasks 13–16 fix them on top of Task 12, each with tests that fail without the fix; where these tasks and the design above differ, the tasks win.

| Finding | Problem | Fix | Task |
|---|---|---|---|
| 1 | A daemon started before migration 6 keeps recording live rows without the authority rules, so a delayed live export of an imported call counts twice (I3); both builds call themselves 0.4.1, so a version warning can't tell them apart | `daemon.json` gains `importRules`; while a daemon without it holds the lock, `import` exits with 1 and `--dry-run` warns (D29) | 16 |
| 2 | `GetRepositorySlug` kept a URL's query and fragment: `https://git.example/o/r.git?token=…` stored `o/r.git?token=…` | Only the path counts, and the slug must be `owner/repo` of `[A-Za-z0-9._-]`, on the live path and in import | 13 |
| 3 | Size and last-write time were treated as proof that a log is unchanged | `import_sessions.log_sha256`; a log is skipped only when size, time, and hash all match (D16) | 14 |
| 4 | A tool that ended after its request was imported never got a link | A record read again whose own row still counts gets its new links | 15 |
| 5 | Imported rows that unmatched live spans superseded were never reported | `Superseded, unmatched`: a count, a diagnostic per session, incomplete coverage, exit code 2 (D26) | 15 |
| 6 | `--repo` took in nested repositories through the working-directory fallback | The working directory's repository is resolved on disk; one that can't be resolved is out of scope (D30) | 15 |
| 7 | A provider-billed import that names a Foundry provider and a known local model was priced as local | The hostless known-local-model rule applies only to `Endpoint` spans (D9) | 13 |
| 8 | A dry run beside only one of `-wal` and `-shm` could create the other | The dry run refuses with exit code 1 (D15) | 14 |
| 9 | Errors while listing a store were swallowed, and coverage was reported complete | `UnreadableStore`: a count, a diagnostic, and incomplete coverage; a missing store still has no logs | 14 |
| 10 | A duration that would start a record before `DateTimeOffset.MinValue` threw and stopped the import | An unsupported shape | 13 |
| 11 | `cacheDetailsReported:false` with a nonzero counter stays `Reported` | Kept, and documented as D28 | — |
| 12 | A Codex record with a duplicated `thread_id` was judged inherited before its duplicate was seen | Duplicated fields are unsupported before the thread check | 13 |
| 13 | The privacy tests didn't look at the ingest-failure log, and an `IOException`'s message can name a path from the request | `IngestFailureLog` keeps only `SqliteException` messages (D21 narrowed); IP1 scans `daemon.log`, and IP4 makes the daemon log a failure for a canary-bearing span | 16 |

## Task 1 specification: Billing Source and Service Request Identity

This task adds what both paths need before any import exists: the live span's service request ID, the shared identity built from it, and the billing-source hint with its one branch in `Calculate`. Live spans keep `BillingSource.Endpoint`, so every existing result is unchanged.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Telemetry/BillingSource.cs` | New: `Endpoint`, `Provider` |
| `src/CopilotLedger.Core/Telemetry/UsageIdentity.cs` | New: `GitHubProviderName`, `NormalizeServiceRequestId`, `CanonicalUsageId`, `Sha256Hex` |
| `src/CopilotLedger.Core/Telemetry/NormalizedGenAiSpan.cs` | New `ServiceRequestId` and `BillingSource` |
| `src/CopilotLedger.Core/Pricing/PricingStatus.cs` | New `OtherProvider` |
| `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs` | The `Provider` branch in `Calculate` |
| `src/CopilotLedger.Otlp/Normalization/GenAiAttributeNames.cs` | New `ServiceRequestId` (`github.copilot.service_request_id`) |
| `src/CopilotLedger.Otlp/Normalization/GenAiSpanNormalizer.cs` | Reads it on inference spans |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | Stores `OtherProvider` as `other_provider` |
| `docs/configuration.md` | The `other_provider` row of the pricing statuses |
| `tests/CopilotLedger.Tests/Telemetry/UsageIdentityTests.cs` | New: UI1–UI4 |
| `tests/CopilotLedger.Tests/Pricing/BillingSourceTests.cs` | New: BI1–BI7 |
| `tests/CopilotLedger.Tests/Otlp/GenAiSpanNormalizerTests.cs` | New NZ15, NZ16 |
| `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs` | New S16 |

### Contracts

- `UsageIdentity.NormalizeServiceRequestId(string?)`: the lowercase `D` form of a 36-character string of hex digits and hyphens that `Guid.TryParseExact(…, "D")` accepts and that isn't the nil UUID; otherwise `null`. The character check comes first because `TryParseExact` also accepts forms such as `+0x…`.
- `UsageIdentity.CanonicalUsageId(string? providerName, string? serviceRequestId, BillingChannel channel)`: `Sha256Hex("gh-ledger-usage", "v1", "github-service-request", <normalized ID>)` when `channel` is `GitHubAiCredits`, `providerName` is exactly `github`, and the ID normalizes; otherwise `null`.
- `UsageIdentity.Sha256Hex(params string[] parts)`: the lowercase hex SHA-256 of each part's UTF-8 byte count (4 bytes, big-endian) followed by its bytes, so different part lists never hash the same bytes.
- `NormalizedGenAiSpan.ServiceRequestId` (`string?`, as the span sent it) and `NormalizedGenAiSpan.BillingSource` (default `Endpoint`). The normalizer sets `ServiceRequestId` from a string attribute only.
- `PricingStatus.OtherProvider`, stored as `other_provider`: a request that its provider billed directly, and that provider isn't Foundry BYOK or local.

### Algorithm (`Calculate`)

1. Unchanged: the model, the request model (#67), the `NotBillableOperation` and `InvalidUsage` checks, the uncached input, and `host`.
2. New, right after `host`: when `BillingSource` is `Provider` and `ProviderName` matches neither `localOffload.providerNames` nor `foundryByok.providerNames`, return `Unpriced` with `OtherProvider`, the normalized model, and the uncached input; `Result` prices `ApiEquivalent` as for every request, with its request-model fallback.
3. Otherwise unchanged: local, then Foundry BYOK, then GitHub. A listed provider name reaches the local or Foundry block through the existing provider-name rules, and a `Provider` request never reaches the GitHub block for want of a host.

### Code (copy exactly)

Edit 1 of 1 in `docs/configuration.md`, replace:

```markdown
| `no_rate_for_date` | The model has no price for the request's date | Add a price window that covers the date |
| `unrecognized_endpoint` | The endpoint isn't GitHub, Foundry, or local | Add its host to the matching `hostSuffixes` or `additionalHosts` list |
| `invalid_usage` | Token counts are inconsistent, for example more cached tokens than input tokens | Report it; it usually means a telemetry exporter bug |

### Cache tokens
```

with:

```markdown
| `no_rate_for_date` | The model has no price for the request's date | Add a price window that covers the date |
| `unrecognized_endpoint` | The endpoint isn't GitHub, Foundry, or local | Add its host to the matching `hostSuffixes` or `additionalHosts` list |
| `other_provider` | An imported request that its provider billed directly, and that provider isn't Foundry BYOK or local, such as a Codex request to OpenAI | If the provider is local or Foundry, add its name to `localOffload.providerNames` or `foundryByok.providerNames` |
| `invalid_usage` | Token counts are inconsistent, for example more cached tokens than input tokens | Report it; it usually means a telemetry exporter bug |

### Cache tokens
```

Edit 1 of 1 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        long uncached = span.InputTokens - span.CacheReadInputTokens - span.CacheWriteInputTokens;
        string host = NormalizeHost(span.ServerAddress);

        LocalModelRate? localModel = model is null ? null : _localModels.FirstOrDefault(item => item.Matches(model));
```

with:

```csharp
        long uncached = span.InputTokens - span.CacheReadInputTokens - span.CacheWriteInputTokens;
        string host = NormalizeHost(span.ServerAddress);

        // An imported request that its provider billed (a Copilot BYOK or Codex request) is local or Foundry BYOK only when
        // the card lists its provider name; without an endpoint, it would otherwise be priced as GitHub's.
        if (span.BillingSource == BillingSource.Provider &&
            !MatchesValue(span.ProviderName, _localProviders) &&
            !MatchesValue(span.ProviderName, _foundryProviders))
        {
            return Result(span, BillingChannel.Unpriced, PricingStatus.OtherProvider, model, uncachedInputTokens: uncached);
        }

        LocalModelRate? localModel = model is null ? null : _localModels.FirstOrDefault(item => item.Matches(model));
```

Edit 1 of 1 in `src/CopilotLedger.Core/Pricing/PricingStatus.cs`, replace:

```csharp
    InvalidUsage,           // a negative token count, or cache read + cache write > input
    NotBillableOperation,   // not an inference operation (for example invoke_agent)
}
```

with:

```csharp
    InvalidUsage,           // a negative token count, or cache read + cache write > input
    NotBillableOperation,   // not an inference operation (for example invoke_agent)
    OtherProvider,          // an imported request billed by a provider that isn't GitHub, Foundry BYOK, or local
}
```

New file `src/CopilotLedger.Core/Telemetry/BillingSource.cs`:

```csharp
namespace CopilotLedger.Core.Telemetry;

// Where JsonRateCardCalculator finds a request's billing channel. Live spans keep Endpoint; only an import sets Provider.
public enum BillingSource
{
    // server.address, then gen_ai.provider.name, then a known local model. A request without an endpoint is GitHub's.
    Endpoint,

    // The provider that billed the request, ProviderName: local or Foundry BYOK when the card lists it under
    // localOffload.providerNames or foundryByok.providerNames, and otherwise another provider (OtherProvider), never GitHub.
    Provider,
}
```

Edit 1 of 1 in `src/CopilotLedger.Core/Telemetry/NormalizedGenAiSpan.cs`, replace:

```csharp
    public CacheCounterState CacheReadState { get; init; }   // Reported when the span carried the cache-read attribute
    public CacheCounterState CacheWriteState { get; init; }  // Reported when it carried cache_write (or legacy cache_creation)
}
```

with:

```csharp
    public CacheCounterState CacheReadState { get; init; }   // Reported when the span carried the cache-read attribute
    public CacheCounterState CacheWriteState { get; init; }  // Reported when it carried cache_write (or legacy cache_creation)
    public string? ServiceRequestId { get; init; }           // github.copilot.service_request_id; never stored, only its UsageIdentity
    public BillingSource BillingSource { get; init; }        // Endpoint for live spans
}
```

New file `src/CopilotLedger.Core/Telemetry/UsageIdentity.cs`:

```csharp
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Core.Telemetry;

// The identity that a live span and an imported session-log record of the same GitHub-billed model call share.
public static class UsageIdentity
{
    public const string GitHubProviderName = "github";

    // The lowercase "D" form of a service request ID: 36 hex digits and hyphens that Guid.TryParseExact(value, "D")
    // accepts, other than the nil UUID. null for anything else.
    public static string? NormalizeServiceRequestId(string? value)
    {
        if (value is not { Length: 36 } ||
            !value.All(character => character == '-' || char.IsAsciiHexDigit(character)) ||
            !Guid.TryParseExact(value, "D", out Guid id) ||
            id == Guid.Empty)
        {
            return null;
        }

        return id.ToString("D");
    }

    // canonical_usage_id: the lowercase hex SHA-256 of ("gh-ledger-usage", "v1", "github-service-request", the normalized
    // service request ID). null unless channel is GitHubAiCredits, providerName is exactly "github", and the ID normalizes,
    // so a BYOK, local, or other provider's request never has one.
    public static string? CanonicalUsageId(string? providerName, string? serviceRequestId, BillingChannel channel)
    {
        string? id = NormalizeServiceRequestId(serviceRequestId);
        return channel == BillingChannel.GitHubAiCredits &&
               string.Equals(providerName, GitHubProviderName, StringComparison.Ordinal) &&
               id is not null
            ? Sha256Hex("gh-ledger-usage", "v1", "github-service-request", id)
            : null;
    }

    // The lowercase hex SHA-256 of the parts, each written as its UTF-8 byte count (4 bytes, big-endian) and then its UTF-8
    // bytes, so two different lists of parts never hash the same bytes.
    public static string Sha256Hex(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (string part in parts)
        {
            ArgumentNullException.ThrowIfNull(part, nameof(parts));
            byte[] bytes = Encoding.UTF8.GetBytes(part);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
```

Edit 1 of 1 in `src/CopilotLedger.Otlp/Normalization/GenAiAttributeNames.cs`, replace:

```csharp
    public const string ServerAddress = "server.address";
    public const string ServerPort = "server.port";
    public const string GitRepository = "github.copilot.git.repository";
    public const string GitBranch = "github.copilot.git.branch";
```

with:

```csharp
    public const string ServerAddress = "server.address";
    public const string ServerPort = "server.port";
    public const string ServiceRequestId = "github.copilot.service_request_id";
    public const string GitRepository = "github.copilot.git.repository";
    public const string GitBranch = "github.copilot.git.branch";
```

Edit 1 of 1 in `src/CopilotLedger.Otlp/Normalization/GenAiSpanNormalizer.cs`, replace:

```csharp
                CacheReadState = cacheReadState,
                CacheWriteState = cacheWriteState,
            };
        }
```

with:

```csharp
                CacheReadState = cacheReadState,
                CacheWriteState = cacheWriteState,
                ServiceRequestId = Text(attributes, GenAiAttributeNames.ServiceRequestId),
            };
        }
```

Edit 1 of 1 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        PricingStatus.InvalidUsage => "invalid_usage",
        PricingStatus.NotBillableOperation => "not_billable_operation",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
```

with:

```csharp
        PricingStatus.InvalidUsage => "invalid_usage",
        PricingStatus.NotBillableOperation => "not_billable_operation",
        PricingStatus.OtherProvider => "other_provider",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Otlp/GenAiSpanNormalizerTests.cs`, replace:

```csharp
    }

    private static void AssertLink(
        GenAiSpanLink link,
```

with:

```csharp
    }

    [Fact]
    public void NZ15_ChatSpans_KeepTheirServiceRequestId()
    {
        NormalizedGenAiSpan span = Assert.Single(GenAiSpanNormalizer.Normalize(
            Request(
                CopilotResource,
                Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Str(GenAiAttributeNames.ProviderName, "github"),
                    Str(GenAiAttributeNames.ServiceRequestId, "1F0E9C2A-7B3D-4E5F-8A9B-0C1D2E3F4A5B"),
                ]))).InferenceSpans);

        Assert.Equal("1F0E9C2A-7B3D-4E5F-8A9B-0C1D2E3F4A5B", span.ServiceRequestId);
        Assert.Equal(BillingSource.Endpoint, span.BillingSource);
    }

    [Fact]
    public void NZ16_AServiceRequestIdThatIsNotAStringIsIgnored()
    {
        NormalizedGenAiSpan span = Assert.Single(GenAiSpanNormalizer.Normalize(
            Request(
                CopilotResource,
                Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Int(GenAiAttributeNames.ServiceRequestId, 42),
                ]))).InferenceSpans);

        Assert.Null(span.ServiceRequestId);
    }

    private static void AssertLink(
        GenAiSpanLink link,
```

New file `tests/CopilotLedger.Tests/Pricing/BillingSourceTests.cs`:

```csharp
using System.Globalization;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class BillingSourceTests
{
    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    public static TheoryData<string, PricingStatus, decimal> FoundryModels => new()
    {
        { "gpt-unknown", PricingStatus.ByokUnpriced, 0m },
        { "contoso-gpt61-prod", PricingStatus.Priced, 0.315m },
    };

    [Theory]
    [InlineData("ollama")]
    [InlineData("Ollama")]
    public void BI1_AListedLocalProviderIsLocal(string provider)
    {
        CreditCalculationResult result = Retail.Calculate(Span(provider, "qwen-local", 400_000, 12_000));

        Assert.Equal(BillingChannel.Local, result.Channel);
        Assert.Equal(PricingStatus.LocalOffload, result.Status);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(0.23m, result.EstimatedCloudSavedUsd);
        Assert.Equal(0.3584m, result.ApiEquivalent.UsdCost);
    }

    [Theory]
    [MemberData(nameof(FoundryModels))]
    public void BI2_AListedFoundryProviderIsFoundryByok(string model, PricingStatus status, decimal usd)
    {
        CreditCalculationResult result = Retail.Calculate(Span("azure.ai.inference", model, 100_000, 10_000));

        Assert.Equal(BillingChannel.FoundryByok, result.Channel);
        Assert.Equal(status, result.Status);
        Assert.Equal(usd, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("github")]
    [InlineData(null)]
    public void BI3_AnyOtherProviderIsUnpricedButKeepsItsApiEquivalentPrice(string? provider)
    {
        CreditCalculationResult result = Retail.Calculate(Span(provider, "test-retail", 100_000, 10_000));

        Assert.Equal(BillingChannel.Unpriced, result.Channel);
        Assert.Equal(PricingStatus.OtherProvider, result.Status);
        Assert.Equal("test-retail", result.ModelId);
        Assert.Null(result.RateCardModelId);
        Assert.Null(result.Breakdown);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
        Assert.Equal(0m, result.EstimatedCloudSavedUsd);
        Assert.Equal(100_000, result.UncachedInputTokens);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        Assert.Equal(0.45m, result.ApiEquivalent.UsdCost);
    }

    [Fact]
    public void BI4_AnOtherProviderRequestKeepsItsNormalizedModel()
    {
        CreditCalculationResult result = Retail.Calculate(Span("openai", " GPT Unknown ", 100_000, 10_000));

        Assert.Equal(PricingStatus.OtherProvider, result.Status);
        Assert.Equal("gpt-unknown", result.ModelId);
        Assert.Equal(ApiEquivalentStatus.Unpriced, result.ApiEquivalent.Status);
        Assert.Null(result.ApiEquivalent.UsdCost);
    }

    [Fact]
    public void BI5_OnlyTheEndpointSourceMakesAHostlessRequestGitHubs()
    {
        NormalizedGenAiSpan endpoint = Span("github", "test-retail", 100_000, 10_000) with
        {
            BillingSource = BillingSource.Endpoint,
        };

        CreditCalculationResult github = Retail.Calculate(endpoint);
        CreditCalculationResult provider = Retail.Calculate(endpoint with { BillingSource = BillingSource.Provider });

        Assert.Equal(BillingChannel.GitHubAiCredits, github.Channel);
        Assert.Equal(PricingStatus.Priced, github.Status);
        Assert.Equal(0.15m, github.UsdCost);
        Assert.Equal(15m, github.AiCredits);
        Assert.Equal(BillingChannel.Unpriced, provider.Channel);
        Assert.Equal(PricingStatus.OtherProvider, provider.Status);
        Assert.Equal(0m, provider.UsdCost);
        Assert.Equal(github.ApiEquivalent.UsdCost, provider.ApiEquivalent.UsdCost);
    }

    [Theory]
    [InlineData("invoke_agent", 100_000, 0, PricingStatus.NotBillableOperation)]
    [InlineData("chat", 100, 200, PricingStatus.InvalidUsage)]
    public void BI6_OperationAndUsageChecksStillComeFirst(
        string operation,
        long input,
        long cacheRead,
        PricingStatus status)
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            operationName: operation,
            providerName: "openai",
            requestModel: "test-retail",
            inputTokens: input,
            cacheReadInputTokens: cacheRead) with
        {
            BillingSource = BillingSource.Provider,
        });

        Assert.Equal(BillingChannel.Unpriced, result.Channel);
        Assert.Equal(status, result.Status);
        Assert.Equal(ApiEquivalentStatus.NotApplicable, result.ApiEquivalent.Status);
    }

    // A response model that the card doesn't have, such as a dated id, never hides a request model that it has: the billing
    // source changes only which channel prices the request.
    [Fact]
    public void BI7_ProviderBilledRequestsFallBackToTheirRequestModelAsLiveOnesDo()
    {
        CreditCalculationResult foundry = Retail.Calculate(
            Span("azure.ai.inference", "contoso-gpt61-prod", 100_000, 10_000) with { ResponseModel = "gpt-6.1-sol-2026-10-01" });
        CreditCalculationResult other = Retail.Calculate(
            Span("openai", "test-retail", 100_000, 10_000) with { ResponseModel = "test-retail-2026-10-01" });

        Assert.Equal(
            (BillingChannel.FoundryByok, PricingStatus.Priced, "gpt-6.1-sol", 0.315m),
            (foundry.Channel, foundry.Status, foundry.ModelId, foundry.UsdCost));
        Assert.Equal(
            (BillingChannel.Unpriced, PricingStatus.OtherProvider, "test-retail-2026-10-01", 0m),
            (other.Channel, other.Status, other.ModelId, other.UsdCost));
        Assert.Equal(ApiEquivalentStatus.Priced, other.ApiEquivalent.Status);
        Assert.Equal(0.45m, other.ApiEquivalent.UsdCost);
    }

    private static NormalizedGenAiSpan Span(string? provider, string model, long input, long output) =>
        TestSpans.Chat(
            startTime: DateTimeOffset.Parse("2026-11-20T12:00:00Z", CultureInfo.InvariantCulture),
            providerName: provider,
            requestModel: model,
            inputTokens: input,
            outputTokens: output) with
        {
            BillingSource = BillingSource.Provider,
        };
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs`, replace:

```csharp
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
```

with:

```csharp
    }

    [Fact]
    public async Task S16_RecordAsync_StoresOtherProvider()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        NormalizedGenAiSpan span = TestSpans.Chat(
            providerName: "openai",
            requestModel: "gpt-6.1-sol",
            inputTokens: 100_000,
            outputTokens: 10_000) with
        {
            BillingSource = BillingSource.Provider,
        };

        Assert.Equal(1, await store.RecordAsync(
            [new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null)]));

        await using SqliteConnection connection = new($"Data Source={DatabasePath(temp)}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT billing_channel, pricing_status, cost_usd_pico, api_equivalent_status FROM spans;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("unpriced", reader.GetString(0));
        Assert.Equal("other_provider", reader.GetString(1));
        Assert.Equal(0L, reader.GetInt64(2));
        Assert.Equal("priced", reader.GetString(3));
        Assert.False(await reader.ReadAsync());
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
```

New file `tests/CopilotLedger.Tests/Telemetry/UsageIdentityTests.cs`:

```csharp
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Tests.Telemetry;

public sealed class UsageIdentityTests
{
    private const string ServiceRequestId = "1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b";

    [Theory]
    [InlineData("1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b", "e60cf189bf4c90ec992e7763f23f3af37a01a61852a1e3ec40acaf1618ab6ae6")]
    [InlineData("A0B1C2D3-E4F5-4A6B-8C7D-9E0F1A2B3C4D", "314c4e22f41a318c9f76f436ee6f115046a33276156c566526f215c3028be845")]
    [InlineData("a0b1c2d3-e4f5-4a6b-8c7d-9e0f1a2b3c4d", "314c4e22f41a318c9f76f436ee6f115046a33276156c566526f215c3028be845")]
    public void UI1_AGitHubBilledRequestIsIdentifiedByItsServiceRequestId(string serviceRequestId, string expected)
    {
        Assert.Equal(
            expected,
            UsageIdentity.CanonicalUsageId("github", serviceRequestId, BillingChannel.GitHubAiCredits));
    }

    [Theory]
    [InlineData("github", BillingChannel.FoundryByok)]
    [InlineData("github", BillingChannel.Local)]
    [InlineData("github", BillingChannel.Unpriced)]
    [InlineData("GitHub", BillingChannel.GitHubAiCredits)]
    [InlineData(null, BillingChannel.GitHubAiCredits)]
    public void UI2_OnlyGitHubBilledRequestsOfProviderGitHubAreIdentified(string? provider, BillingChannel channel)
    {
        Assert.Null(UsageIdentity.CanonicalUsageId(provider, ServiceRequestId, channel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b}")]
    [InlineData(" 1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5")]
    [InlineData("1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5")]
    [InlineData("1f0e9c2a7b3d4e5f8a9b0c1d2e3f4a5b")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("+0x0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b")]
    [InlineData("1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5g")]
    public void UI3_AnythingButAUuidInDFormIsNoIdentity(string? serviceRequestId)
    {
        Assert.Null(UsageIdentity.NormalizeServiceRequestId(serviceRequestId));
        Assert.Null(UsageIdentity.CanonicalUsageId("github", serviceRequestId, BillingChannel.GitHubAiCredits));
    }

    [Fact]
    public void UI4_PartsAreLengthPrefixedSoTheyNeverRunTogether()
    {
        Assert.Equal(
            "2463cb7a5fc6adaca7c5ce7d37ffce1c4169f9bde5fe73d3cabeec39da7c9c38",
            UsageIdentity.Sha256Hex("a|b", "c"));
        Assert.Equal(
            "71f0f46e8bc7128a5509db6e0eae771abee3a213b39be06c01dec4ac6d8d5db8",
            UsageIdentity.Sha256Hex("a", "b|c"));
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            UsageIdentity.Sha256Hex());
        Assert.Equal(
            "1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b",
            UsageIdentity.NormalizeServiceRequestId("1F0E9C2A-7B3D-4E5F-8A9B-0C1D2E3F4A5B"));
    }
}
```

### Tests

- **UI1** (3): `CanonicalUsageId("github", id, GitHubAiCredits)` for `1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b` is `e60cf189bf4c90ec992e7763f23f3af37a01a61852a1e3ec40acaf1618ab6ae6`, and for `a0b1c2d3-e4f5-4a6b-8c7d-9e0f1a2b3c4d`, in upper or lower case, `314c4e22f41a318c9f76f436ee6f115046a33276156c566526f215c3028be845`.
- **UI2** (5): no identity for provider `github` on Foundry BYOK, local, or unpriced requests, or for provider `GitHub` or none on GitHub.
- **UI3** (9): no identity for `null`, an empty string, braces, a leading space, 35 characters, no hyphens, the nil UUID, `+0x…`, or a non-hex digit.
- **UI4**: `Sha256Hex("a|b", "c")` and `Sha256Hex("a", "b|c")` differ (both pinned), `Sha256Hex()` is the SHA-256 of nothing, and an upper-case ID normalizes to lower case.
- **BI1** (2, `RetailTestRatesJson`): a `Provider` request of `ollama` or `Ollama` is local: $0.00 billed, $0.23 saved, $0.3584 at API prices.
- **BI2** (2): `azure.ai.inference` is Foundry BYOK: `gpt-unknown` is `ByokUnpriced`, `contoso-gpt61-prod` is priced at $0.315.
- **BI3** (4): providers `openai`, `anthropic`, `github`, and none are `Unpriced`/`OtherProvider` with `ModelId` `test-retail`, no breakdown, 0 cost, credits, and savings, 100,000 uncached input tokens, and $0.45 at API prices.
- **BI4**: an `OtherProvider` request keeps the normalized model `gpt-unknown`, unpriced at API prices.
- **BI5**: the same host-less request is GitHub's ($0.15, 15 AI credits) only as `Endpoint`; as `Provider` it's `OtherProvider` with the same API-equivalent cost.
- **BI6** (2): `invoke_agent` is still `NotBillableOperation` and inconsistent tokens `InvalidUsage`, both `NotApplicable` at API prices.
- **BI7**: a provider-billed request uses the merged request-model fallback: Foundry BYOK with response model `gpt-6.1-sol-2026-10-01` and request model `contoso-gpt61-prod` is priced as `gpt-6.1-sol` ($0.315), and `openai` with response model `test-retail-2026-10-01` keeps that `ModelId`, is `OtherProvider`, and is $0.45 at API prices as its request model.
- **NZ15**: a chat span keeps `github.copilot.service_request_id` as sent (upper case) and `BillingSource.Endpoint`. **NZ16**: an integer attribute gives no `ServiceRequestId`.
- **S16**: an `openai` `Provider` request is stored as `unpriced`, `other_provider`, 0 pico-USD, with API-equivalent status `priced`.

### Acceptance criteria

- Every live result is unchanged: the full suite still passes.
- A provider-billed request is never priced as GitHub's for want of an endpoint (BI3, BI5), and listed local and Foundry providers reach their channels (BI1, BI2), with #67's fallback (BI7).
- `dotnet test CopilotLedger.slnx` passes **1052** tests (1018 + 34).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 2 specification: Ledger Schema Version 6

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/LedgerMigrations.cs` | Migration 6 |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | `RecordAsync` writes `ingest_source`, `canonical_usage_id`, and `imported_tool_call_count` through a row provenance |
| `tests/CopilotLedger.Tests/TestSupport/LedgerSql.cs` | New: raw SQL helpers for tests |
| `tests/CopilotLedger.Tests/Storage/SchemaVersion6Tests.cs` | New: SV1–SV4 |
| `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs`, `SpanLinkStoreTests.cs`, `ApiEquivalentStoreTests.cs` | S1, SL1, SL8, SL10, SL11, and AS4 expect version 6; the tests that rebuild an older ledger drop version 6's objects first |

### DDL (migration 6, in one transaction)

```sql
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

CREATE TABLE import_sessions (… PRIMARY KEY (source, log_id)) STRICT, WITHOUT ROWID;
CREATE TABLE import_record_aliases (… PRIMARY KEY (source, record_key_sha256)) STRICT, WITHOUT ROWID;
```

The two tables are in full in the code. No column holds money as `REAL`, and no money column is added. Existing rows take `otlp`, `effective`, and `NULL`s; existing links have no owner.

### Contracts

- `RecordAsync` writes each live row's `canonical_usage_id` as `UsageIdentity.CanonicalUsageId(span.ProviderName, span.ServiceRequestId, calculation.Channel)`, `ingest_source` `otlp`, and no tool call count. Its validation and return value are unchanged.
- `LedgerSql` (tests): `OpenAsync`, `ExecuteAsync`, `ScalarAsync`, and `RowsAsync`, which joins each row's values with `|`, `NULL` as `null`.

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Storage/LedgerMigrations.cs`, replace:

```csharp
            ALTER TABLE spans ADD COLUMN api_rate_card_sha256 TEXT;
            """),
    ];
}
```

with:

```csharp
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
    ];
}
```

Edit 1 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            cache_read_state, cache_write_state, pull_request_number,
            api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id,
            api_rates_version, api_rate_card_sha256
        ) VALUES (
            @trace_id, @span_id, @parent_span_id, @operation_name, @conversation_id,
```

with:

```csharp
            cache_read_state, cache_write_state, pull_request_number,
            api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id,
            api_rates_version, api_rate_card_sha256,
            ingest_source, canonical_usage_id, imported_tool_call_count
        ) VALUES (
            @trace_id, @span_id, @parent_span_id, @operation_name, @conversation_id,
```

Edit 2 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            @cache_read_state, @cache_write_state, @pull_request_number,
            @api_equivalent_usd_pico, @api_equivalent_status, @api_rate_card_model_id,
            @api_rates_version, @api_rate_card_sha256
        ) ON CONFLICT (trace_id, span_id) DO NOTHING;
        """;
```

with:

```csharp
            @cache_read_state, @cache_write_state, @pull_request_number,
            @api_equivalent_usd_pico, @api_equivalent_status, @api_rate_card_model_id,
            @api_rates_version, @api_rate_card_sha256,
            @ingest_source, @canonical_usage_id, @imported_tool_call_count
        ) ON CONFLICT (trace_id, span_id) DO NOTHING;
        """;
```

Edit 3 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            command.Transaction = transaction;
            command.CommandText = InsertSql;
            AddInsertParameters(command, entry, recordedAtUnixMs);
            inserted += await command.ExecuteNonQueryAsync(cancellationToken);
        }
```

with:

```csharp
            command.Transaction = transaction;
            command.CommandText = InsertSql;
            AddInsertParameters(command, entry.Span, entry.Calculation, LiveProvenance(entry), recordedAtUnixMs);
            inserted += await command.ExecuteNonQueryAsync(cancellationToken);
        }
```

Edit 4 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
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
```

with:

```csharp
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

    private static void AddInsertParameters(
        SqliteCommand command,
        NormalizedGenAiSpan span,
        CreditCalculationResult calculation,
        RowProvenance provenance,
        long recordedAtUnixMs)
    {
        command.Parameters.AddWithValue("@trace_id", span.TraceId);
        command.Parameters.AddWithValue("@span_id", span.SpanId);
```

Edit 5 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
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
```

with:

```csharp
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
```

Edit 6 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        command.Parameters.AddWithValue(
            "@pull_request_number",
            OrDbNull(entry.Git?.PullRequestNumber));
        ApiEquivalentCost retail = calculation.ApiEquivalent;
        command.Parameters.AddWithValue(
```

with:

```csharp
        command.Parameters.AddWithValue(
            "@pull_request_number",
            OrDbNull(provenance.PullRequestNumber));
        ApiEquivalentCost retail = calculation.ApiEquivalent;
        command.Parameters.AddWithValue(
```

Edit 7 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        command.Parameters.AddWithValue("@api_rates_version", retail.RatesVersion);
        command.Parameters.AddWithValue("@api_rate_card_sha256", retail.RateCardSha256);
    }

    // A rollup's group key: its SELECT expressions, and their aliases separated by ", ".
```

with:

```csharp
        command.Parameters.AddWithValue("@api_rates_version", retail.RatesVersion);
        command.Parameters.AddWithValue("@api_rate_card_sha256", retail.RateCardSha256);
        command.Parameters.AddWithValue("@ingest_source", provenance.IngestSource);
        command.Parameters.AddWithValue("@canonical_usage_id", OrDbNull(provenance.CanonicalUsageId));
        command.Parameters.AddWithValue("@imported_tool_call_count", OrDbNull(provenance.ImportedToolCallCount));
    }

    // A rollup's group key: its SELECT expressions, and their aliases separated by ", ".
```

Edit 8 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        string? GitCommitSha);

    // A group's API-equivalent totals: as recorded, or summed by Add as spans are priced again.
    private sealed class RetailTotals(decimal? usd = null, long unpricedSpanCount = 0, long estimatedSpanCount = 0)
```

with:

```csharp
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
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Storage/ApiEquivalentStoreTests.cs`, replace:

```csharp
        {
            await ExecuteAsync(connection, """
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
```

with:

```csharp
        {
            await ExecuteAsync(connection, """
                DROP TABLE import_record_aliases;
                DROP TABLE import_sessions;
                DROP INDEX ux_spans_effective_import_usage;
                DROP INDEX ix_spans_canonical_usage;
                ALTER TABLE span_links DROP COLUMN owner_span_row_id;
                ALTER TABLE spans DROP COLUMN superseded_by;
                ALTER TABLE spans DROP COLUMN imported_tool_call_count;
                ALTER TABLE spans DROP COLUMN accounting_state;
                ALTER TABLE spans DROP COLUMN canonical_usage_id;
                ALTER TABLE spans DROP COLUMN ingest_source;
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Storage/ApiEquivalentStoreTests.cs`, replace:

```csharp
        await store.InitializeAsync();

        await using SqliteConnection check = await OpenAsync(temp);
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(6, before.Length);
        Assert.Equal(
```

with:

```csharp
        await store.InitializeAsync();

        await using SqliteConnection check = await OpenAsync(temp);
        Assert.Equal(6L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(6, before.Length);
        Assert.Equal(
```

New file `tests/CopilotLedger.Tests/Storage/SchemaVersion6Tests.cs`:

```csharp
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class SchemaVersion6Tests
{
    private const string ServiceRequestId = "1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b";
    private const string Canonical = "e60cf189bf4c90ec992e7763f23f3af37a01a61852a1e3ec40acaf1618ab6ae6";

    // Every spans column of a version 5 ledger.
    private const string Version5Columns = """
        id, trace_id, span_id, parent_span_id, operation_name, conversation_id, agent_name, provider_name, request_model,
        response_model, model_id, rate_card_model_id, tier_name, server_address, server_port, start_time_unix_ms,
        end_time_unix_ms, input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens, billing_channel,
        pricing_status, rates_version, cost_usd_pico, saved_usd_pico, repo_root, repo_slug, branch_name, head_commit_sha,
        recorded_at_unix_ms, cache_read_state, cache_write_state, pull_request_number, agent_span_id,
        api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id, api_rates_version, api_rate_card_sha256
        """;

    private const string InsertRowSql = """
        INSERT INTO spans (trace_id, span_id, operation_name, start_time_unix_ms, end_time_unix_ms, input_tokens,
                           output_tokens, cache_read_input_tokens, cache_write_input_tokens, billing_channel, pricing_status,
                           rates_version, cost_usd_pico, saved_usd_pico, recorded_at_unix_ms, ingest_source, canonical_usage_id)
        VALUES (@p0, @p1, 'chat', 0, 0, 0, 0, 0, 0, 'github_ai_credits', 'priced', 'test', 0, 0, 0, @p2, @p3);
        """;

    private static readonly JsonRateCardCalculator Calculator = JsonRateCardCalculator.LoadEmbedded();

    public static TheoryData<string?, string?, string?, string?> LiveIdentities => new()
    {
        { "github", ServiceRequestId, null, Canonical },
        { "github", null, null, null },
        { "openai", ServiceRequestId, null, null },
        { "github", ServiceRequestId, "contoso.openai.azure.com", null },
    };

    [Fact]
    public async Task SV1_Migration6_KeepsEveryRowAsAnEffectiveUnidentifiedLiveRow()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore first = new(path);
        await first.InitializeAsync();
        await first.RecordAsync([Entry("github", ServiceRequestId, null), Entry("openai", null, null)]);
        await first.RecordSpanLinksAsync(
        [
            new GenAiSpanLink
            {
                TraceId = new string('6', 32),
                SpanId = new string('6', 16),
                OperationName = "invoke_agent",
                StartTime = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
            },
        ]);
        await LedgerSql.ExecuteAsync(path, """
            DROP TABLE import_record_aliases;
            DROP TABLE import_sessions;
            DROP INDEX ux_spans_effective_import_usage;
            DROP INDEX ix_spans_canonical_usage;
            ALTER TABLE span_links DROP COLUMN owner_span_row_id;
            ALTER TABLE spans DROP COLUMN superseded_by;
            ALTER TABLE spans DROP COLUMN imported_tool_call_count;
            ALTER TABLE spans DROP COLUMN accounting_state;
            ALTER TABLE spans DROP COLUMN canonical_usage_id;
            ALTER TABLE spans DROP COLUMN ingest_source;
            PRAGMA user_version = 5;
            """);
        IReadOnlyList<string> before = await LedgerSql.RowsAsync(path, $"SELECT {Version5Columns} FROM spans ORDER BY id;");

        // A new process starts with no pooled connections; see SL8.
        SqliteConnection.ClearAllPools();
        await new SqliteLedgerStore(path).InitializeAsync();

        Assert.Equal(6L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
        Assert.Equal(2, before.Count);
        Assert.Equal(before, await LedgerSql.RowsAsync(path, $"SELECT {Version5Columns} FROM spans ORDER BY id;"));
        Assert.Equal(
            ["otlp|effective|null|null|null", "otlp|effective|null|null|null"],
            await LedgerSql.RowsAsync(
                path,
                """
                SELECT ingest_source, accounting_state, canonical_usage_id, superseded_by, imported_tool_call_count
                FROM spans ORDER BY id;
                """));
        Assert.Equal(["null"], await LedgerSql.RowsAsync(path, "SELECT owner_span_row_id FROM span_links;"));
        Assert.Equal(0L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM import_sessions;"));
        Assert.Equal(0L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM import_record_aliases;"));
    }

    [Theory]
    [MemberData(nameof(LiveIdentities))]
    public async Task SV2_OnlyGitHubBilledLiveSpansWithAServiceRequestIdAreIdentified(
        string? provider,
        string? serviceRequestId,
        string? host,
        string? expected)
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();

        Assert.Equal(1, await store.RecordAsync([Entry(provider, serviceRequestId, host)]));

        Assert.Equal(
            [$"otlp|{expected ?? "null"}|effective"],
            await LedgerSql.RowsAsync(path, "SELECT ingest_source, canonical_usage_id, accounting_state FROM spans;"));
    }

    [Fact]
    public async Task SV3_ChecksRejectAnUnknownSourceAndASupersededLiveRow()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([Entry("github", ServiceRequestId, null)]);

        SqliteException source = await Assert.ThrowsAsync<SqliteException>(() => LedgerSql.ExecuteAsync(
            path,
            InsertRowSql,
            new string('a', 32),
            new string('a', 16),
            "import:other",
            null));
        SqliteException superseded = await Assert.ThrowsAsync<SqliteException>(() => LedgerSql.ExecuteAsync(
            path,
            "UPDATE spans SET accounting_state = 'superseded', superseded_by = id WHERE ingest_source = 'otlp';"));

        Assert.Equal(19, source.SqliteErrorCode);
        Assert.Equal(19, superseded.SqliteErrorCode);
        Assert.Equal(
            ["otlp|effective|null"],
            await LedgerSql.RowsAsync(path, "SELECT ingest_source, accounting_state, superseded_by FROM spans;"));
    }

    [Fact]
    public async Task SV4_OnlyEffectiveImportRowsMustHaveDistinctCanonicalIds()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        await new SqliteLedgerStore(path).InitializeAsync();
        await LedgerSql.ExecuteAsync(path, InsertRowSql, new string('1', 32), new string('1', 16), "import:copilot", Canonical);

        SqliteException duplicate = await Assert.ThrowsAsync<SqliteException>(() => LedgerSql.ExecuteAsync(
            path,
            InsertRowSql,
            new string('2', 32),
            new string('2', 16),
            "import:codex",
            Canonical));
        await LedgerSql.ExecuteAsync(
            path,
            "UPDATE spans SET accounting_state = 'superseded', superseded_by = 99 WHERE trace_id = @p0;",
            new string('1', 32));
        await LedgerSql.ExecuteAsync(path, InsertRowSql, new string('2', 32), new string('2', 16), "import:codex", Canonical);
        await LedgerSql.ExecuteAsync(path, InsertRowSql, new string('3', 32), new string('3', 16), "otlp", Canonical);
        await LedgerSql.ExecuteAsync(path, InsertRowSql, new string('4', 32), new string('4', 16), "otlp", Canonical);

        Assert.Equal(19, duplicate.SqliteErrorCode);
        Assert.Equal(2067, duplicate.SqliteExtendedErrorCode);
        Assert.Equal(
            ["import:copilot|superseded", "import:codex|effective", "otlp|effective", "otlp|effective"],
            await LedgerSql.RowsAsync(
                path,
                "SELECT ingest_source, accounting_state FROM spans WHERE canonical_usage_id = @p0 ORDER BY id;",
                Canonical));
    }

    private static LedgerEntry Entry(string? provider, string? serviceRequestId, string? host)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            providerName: provider,
            serverAddress: host,
            requestModel: "mai-code-2",
            inputTokens: 1_000,
            outputTokens: 10) with
        {
            ServiceRequestId = serviceRequestId,
        };
        return new LedgerEntry(span, Calculator.Calculate(span), null);
    }
}
```

Edit 1 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await first.RecordAsync([Entry("mai-code-2", 1000, 100)]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
                ALTER TABLE spans DROP COLUMN api_rate_card_model_id;
```

with:

```csharp
        await first.RecordAsync([Entry("mai-code-2", 1000, 100)]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                DROP TABLE import_record_aliases;
                DROP TABLE import_sessions;
                DROP INDEX ux_spans_effective_import_usage;
                DROP INDEX ix_spans_canonical_usage;
                ALTER TABLE span_links DROP COLUMN owner_span_row_id;
                ALTER TABLE spans DROP COLUMN superseded_by;
                ALTER TABLE spans DROP COLUMN imported_tool_call_count;
                ALTER TABLE spans DROP COLUMN accounting_state;
                ALTER TABLE spans DROP COLUMN canonical_usage_id;
                ALTER TABLE spans DROP COLUMN ingest_source;
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
                ALTER TABLE spans DROP COLUMN api_rate_card_model_id;
```

Edit 2 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();
        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(
            "span_links",
```

with:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();
        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(6L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(
            "span_links",
```

Edit 3 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
            },
        ]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
                ALTER TABLE spans DROP COLUMN api_rate_card_model_id;
                ALTER TABLE spans DROP COLUMN api_rates_version;
```

with:

```csharp
            },
        ]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                DROP TABLE import_record_aliases;
                DROP TABLE import_sessions;
                DROP INDEX ux_spans_effective_import_usage;
                DROP INDEX ix_spans_canonical_usage;
                ALTER TABLE span_links DROP COLUMN owner_span_row_id;
                ALTER TABLE spans DROP COLUMN superseded_by;
                ALTER TABLE spans DROP COLUMN imported_tool_call_count;
                ALTER TABLE spans DROP COLUMN accounting_state;
                ALTER TABLE spans DROP COLUMN canonical_usage_id;
                ALTER TABLE spans DROP COLUMN ingest_source;
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
                ALTER TABLE spans DROP COLUMN api_rate_card_model_id;
                ALTER TABLE spans DROP COLUMN api_rates_version;
```

Edit 4 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM span_links;"));
```

with:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(6L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM span_links;"));
```

Edit 5 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, "PRAGMA user_version = 4;");
        }

        await using SqliteConnection holder = await OpenAsync(DatabasePath(temp));
```

with:

```csharp
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, "PRAGMA user_version = 5;");
        }

        await using SqliteConnection holder = await OpenAsync(DatabasePath(temp));
```

Edit 6 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        {
            version.Transaction = held;
            version.CommandText = "PRAGMA user_version = 5;";
            await version.ExecuteNonQueryAsync();
        }
```

with:

```csharp
        {
            version.Transaction = held;
            version.CommandText = "PRAGMA user_version = 6;";
            await version.ExecuteNonQueryAsync();
        }
```

Edit 7 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await init;

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM breaker_settings;"));
        Assert.Equal(
```

with:

```csharp
        await init;

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(6L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM breaker_settings;"));
        Assert.Equal(
```

Edit 8 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
            await ScalarAsync(
                check,
                "SELECT COUNT(*) FROM pragma_table_info('spans') WHERE name = 'api_equivalent_status';"));
    }

    [Fact]
```

with:

```csharp
            await ScalarAsync(
                check,
                "SELECT COUNT(*) FROM pragma_table_info('spans') WHERE name = 'accounting_state';"));
    }

    [Fact]
```

Edit 9 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        {
            await ExecuteAsync(connection, """
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
```

with:

```csharp
        {
            await ExecuteAsync(connection, """
                DROP TABLE import_record_aliases;
                DROP TABLE import_sessions;
                DROP INDEX ux_spans_effective_import_usage;
                DROP INDEX ix_spans_canonical_usage;
                ALTER TABLE span_links DROP COLUMN owner_span_row_id;
                ALTER TABLE spans DROP COLUMN superseded_by;
                ALTER TABLE spans DROP COLUMN imported_tool_call_count;
                ALTER TABLE spans DROP COLUMN accounting_state;
                ALTER TABLE spans DROP COLUMN canonical_usage_id;
                ALTER TABLE spans DROP COLUMN ingest_source;
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
```

Edit 10 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(
            "ix_spans_conversation_start",
```

with:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(6L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(
            "ix_spans_conversation_start",
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs`, replace:

```csharp
            {
                versionCommand.CommandText = "PRAGMA user_version;";
                Assert.Equal(5L, await versionCommand.ExecuteScalarAsync());
            }
        }
```

with:

```csharp
            {
                versionCommand.CommandText = "PRAGMA user_version;";
                Assert.Equal(6L, await versionCommand.ExecuteScalarAsync());
            }
        }
```

New file `tests/CopilotLedger.Tests/TestSupport/LedgerSql.cs`:

```csharp
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.TestSupport;

// Raw SQL against a test ledger, for what the store doesn't expose. Each call opens its own connection.
public static class LedgerSql
{
    public static async Task<SqliteConnection> OpenAsync(string databasePath)
    {
        SqliteConnection connection = new($"Data Source={databasePath}");
        await connection.OpenAsync();
        return connection;
    }

    public static async Task<int> ExecuteAsync(string databasePath, string sql, params object?[] values)
    {
        await using SqliteConnection connection = await OpenAsync(databasePath);
        await using SqliteCommand command = Command(connection, sql, values);
        return await command.ExecuteNonQueryAsync();
    }

    public static async Task<object?> ScalarAsync(string databasePath, string sql, params object?[] values)
    {
        await using SqliteConnection connection = await OpenAsync(databasePath);
        await using SqliteCommand command = Command(connection, sql, values);
        return await command.ExecuteScalarAsync();
    }

    // Each row as its values joined with "|", NULL as "null" and numbers in invariant culture.
    public static async Task<IReadOnlyList<string>> RowsAsync(string databasePath, string sql, params object?[] values)
    {
        await using SqliteConnection connection = await OpenAsync(databasePath);
        await using SqliteCommand command = Command(connection, sql, values);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        List<string> rows = [];
        while (await reader.ReadAsync())
        {
            string[] fields = new string[reader.FieldCount];
            for (int index = 0; index < reader.FieldCount; index++)
            {
                fields[index] = reader.IsDBNull(index)
                    ? "null"
                    : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)!;
            }

            rows.Add(string.Join('|', fields));
        }

        return rows;
    }

    // values bind to @p0, @p1, … in order.
    private static SqliteCommand Command(SqliteConnection connection, string sql, object?[] values)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        for (int index = 0; index < values.Length; index++)
        {
            command.Parameters.AddWithValue($"@p{index}", values[index] ?? DBNull.Value);
        }

        return command;
    }
}
```

### Tests

- **SV1**: a version 5 ledger with a span and a link migrates to 6; the span is `otlp`, `effective`, without canonical ID, `superseded_by`, or tool call count, and the link has no owner.
- **SV2** (4): a live span gets the canonical ID only when it's GitHub-billed (no Foundry host), from provider `github`, with a service request ID.
- **SV3**: the checks reject an unknown `ingest_source` and a superseded live row.
- **SV4**: two effective imported rows can't share a canonical ID, while a superseded one and two live ones can.
- **S1, SL1, SL8, SL10, SL11, AS4** (changed): `user_version` 6; SL10 simulates the other process committing version 6.

### Acceptance criteria

- The migration matches the DDL above; a version 1 ledger still migrates through every version.
- `dotnet test CopilotLedger.slnx` passes **1059** tests (1052 + 7).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 3 specification: The Live and Import Authority Rules

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Import/ImportSourceKind.cs`, `ImportedRecordKind.cs`, `ImportedGitContext.cs`, `ImportIds.cs` | New |
| `src/CopilotLedger.Storage/ImportedLedgerEntry.cs`, `ImportResolution.cs`, `ImportRecordResult.cs`, `ImportSessionCheckpoint.cs` | New |
| `src/CopilotLedger.Storage/UsageAuthority.cs` | New (internal): the rules |
| `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs` | New: `RecordImportedAsync`, `GetImportSessionAsync` |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | `partial`; `RecordAsync` supersedes; the backfill reconciles; `InsertSpanAsync` returns the row's `id` |
| `tests/CopilotLedger.Tests/TestSupport/ImportEntries.cs` | New: imported and live entries, and the invariant checks |
| `tests/CopilotLedger.Tests/Import/ImportIdsTests.cs` | New: ID1–ID3 |
| `tests/CopilotLedger.Tests/Storage/UsageAuthorityTests.cs` | New: UA1–UA17 |

### Contracts

- `ImportSourceKind` (`Copilot`, `Claude`, `Codex`); `ImportedRecordKind` (`Usage`, `Compaction`); `ImportedGitContext` (`WorkingDirectory`, never stored; `RepositoryRoot`, `RepositorySlug`, `Branch`, `HeadCommitSha`).
- `ImportIds`: `SourceName` (`copilot`, `claude`, `codex`), `KindName` (`usage`, `compaction`), and the hashes `RecordKeySha256(source, kind, key)` (64 hex), `TraceId(source, session, turnKey, kind, key)` (32 hex: per turn, or per record without one), `SpanId(source, session, kind, key)` (16 hex), and `ToolSpanId(source, session, toolCallId)` (16 hex), each a `UsageIdentity.Sha256Hex` tuple with its own prefix and `v1`.
- `ImportedLedgerEntry { Source, RecordKeySha256, Span, Calculation, Git?, ToolCallCount?, ToolLinks }`; `ImportResolution { Recorded, Duplicate, OverlapSkipped, AmbiguousOverlap }`; `ImportRecordResult { Resolutions, ToolLinksRecorded }`; `ImportSessionCheckpoint { Source, LogId, LogSizeBytes, LogLastWriteTime, Complete, Recorded, Duplicates, OverlapSkipped, AmbiguousOverlap, ImportedAt }`.
- `SqliteLedgerStore.RecordImportedAsync(IReadOnlyList<ImportedLedgerEntry> entries, ImportSessionCheckpoint? checkpoint, CancellationToken)`: validates everything first (`ArgumentException`, nothing written), then decides and writes every entry, its alias, the tool links of recorded entries (owned by their row), and the checkpoint, in one `BEGIN IMMEDIATE` transaction; any exception, cancellation included, rolls it all back. `GetImportSessionAsync(source, logId)` reads a checkpoint.

### Algorithm (`UsageAuthority`)

1. Gather, inside the transaction: whether the record's alias exists; the effective row with its canonical ID, live first; whether its session (`conversation_id`) has live rows; and whether one of them has no canonical ID.
2. Decide, first match wins: alias exists → `Duplicate`. With a canonical ID: a live owner → `OverlapSkipped`; an imported owner → `Duplicate`; no owner → `AmbiguousOverlap` when the session has an unidentified live row (I3), else `Recorded`. Without a canonical ID: `AmbiguousOverlap` when the session has any live row (I2), else `Recorded`.
3. Write: `Recorded` inserts the row (`ON CONFLICT … DO NOTHING RETURNING id`; a hash collision becomes a `Duplicate` of the row that has the IDs), its `recorded` alias, and its links; `OverlapSkipped` and a first-time `Duplicate` add a `live` or `duplicate` alias to the owner; `AmbiguousOverlap` writes nothing, so a later run decides again.
4. Live side: after inserting a live row, supersede the effective imported row with its canonical ID (I1), then reconcile its session: supersede its imported rows without a canonical ID (I2), or all of them when any live row of the session has none (I3), with `superseded_by` the new row. `BackfillFromAgentSpansAsync` reconciles each session it assigns, with the session's first live row.

### Code (copy exactly)

New file `src/CopilotLedger.Core/Import/ImportIds.cs`:

```csharp
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;

// The IDs an imported record gets: hashes of its source, session, and keys (UsageIdentity.Sha256Hex), so the same record
// always gets the same IDs and a raw key is never stored.
public static class ImportIds
{
    // copilot, claude, or codex: the import_sessions and import_record_aliases source, and ingest_source after "import:".
    public static string SourceName(ImportSourceKind source) => source switch
    {
        ImportSourceKind.Copilot => "copilot",
        ImportSourceKind.Claude => "claude",
        ImportSourceKind.Codex => "codex",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };

    public static string KindName(ImportedRecordKind kind) => kind switch
    {
        ImportedRecordKind.Usage => "usage",
        ImportedRecordKind.Compaction => "compaction",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    // import_record_aliases.record_key_sha256: 64 lowercase hex characters.
    public static string RecordKeySha256(ImportSourceKind source, ImportedRecordKind kind, string recordKey) =>
        UsageIdentity.Sha256Hex("gh-ledger-import-record", "v1", SourceName(source), KindName(kind), recordKey);

    // A trace per user prompt (turnKey), or per record without one: 32 lowercase hex characters.
    public static string TraceId(
        ImportSourceKind source,
        string sessionId,
        string? turnKey,
        ImportedRecordKind kind,
        string recordKey) =>
        (turnKey is null
            ? UsageIdentity.Sha256Hex(
                "gh-ledger-import-trace", "v1", SourceName(source), sessionId, "record", KindName(kind), recordKey)
            : UsageIdentity.Sha256Hex("gh-ledger-import-trace", "v1", SourceName(source), sessionId, "turn", turnKey))[..32];

    // 16 lowercase hex characters.
    public static string SpanId(ImportSourceKind source, string sessionId, ImportedRecordKind kind, string recordKey) =>
        UsageIdentity.Sha256Hex("gh-ledger-import-span", "v1", SourceName(source), sessionId, KindName(kind), recordKey)[..16];

    // An imported tool call's execute_tool link: 16 lowercase hex characters.
    public static string ToolSpanId(ImportSourceKind source, string sessionId, string toolCallId) =>
        UsageIdentity.Sha256Hex("gh-ledger-import-tool", "v1", SourceName(source), sessionId, toolCallId)[..16];
}
```

New file `src/CopilotLedger.Core/Import/ImportSourceKind.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// A local session store that gh ledger import knows. Claude Code's format is internal and undocumented, so it's never read.
public enum ImportSourceKind
{
    Copilot,
    Claude,
    Codex,
}
```

New file `src/CopilotLedger.Core/Import/ImportedGitContext.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// The Git context a session log recorded for its requests. Never filled from today's working tree.
public sealed record ImportedGitContext
{
    public string? WorkingDirectory { get; init; }   // the recorded cwd; matches --repo, and is never stored
    public string? RepositoryRoot { get; init; }     // the recorded Git root, fully qualified; else null
    public string? RepositorySlug { get; init; }     // "owner/repo"; remote URLs and credentials are never kept
    public string? Branch { get; init; }
    public string? HeadCommitSha { get; init; }      // lowercase, 40 or 64 hex characters
}
```

New file `src/CopilotLedger.Core/Import/ImportedRecordKind.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// What kind of log entry a record comes from. Part of the record's alias and span ID, so keys of different kinds never collide.
public enum ImportedRecordKind
{
    Usage,        // Copilot session.usage_record; Codex token_usage_record
    Compaction,   // Copilot session.compaction_complete with compactionTokensUsed
}
```

New file `src/CopilotLedger.Storage/ImportRecordResult.cs`:

```csharp
namespace CopilotLedger.Storage;

public sealed record ImportRecordResult
{
    public required IReadOnlyList<ImportResolution> Resolutions { get; init; }   // one per entry, in order
    public required int ToolLinksRecorded { get; init; }
}
```

New file `src/CopilotLedger.Storage/ImportResolution.cs`:

```csharp
namespace CopilotLedger.Storage;

// What the live/import authority rules decided for one imported record.
public enum ImportResolution
{
    Recorded,          // a new effective row
    Duplicate,         // this record, or an imported row with its identity, is already in the ledger
    OverlapSkipped,    // a live row with its identity is already in the ledger
    AmbiguousOverlap,  // deferred: the conversation's live rows can't be matched to it
}
```

New file `src/CopilotLedger.Storage/ImportSessionCheckpoint.cs`:

```csharp
using CopilotLedger.Core.Import;

namespace CopilotLedger.Storage;

// What the last import of one session log saw, so an unchanged, completely imported log can be skipped.
public sealed record ImportSessionCheckpoint
{
    public required ImportSourceKind Source { get; init; }
    public required string LogId { get; init; }
    public required long LogSizeBytes { get; init; }
    public required DateTimeOffset LogLastWriteTime { get; init; }   // stored in Unix milliseconds
    public required bool Complete { get; init; }                     // every record of the whole log was resolved
    public required long Recorded { get; init; }
    public required long Duplicates { get; init; }
    public required long OverlapSkipped { get; init; }
    public required long AmbiguousOverlap { get; init; }
    public DateTimeOffset ImportedAt { get; init; }                  // set by the store when it writes the checkpoint
}
```

New file `src/CopilotLedger.Storage/ImportedLedgerEntry.cs`:

```csharp
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

// One imported model call, priced, with where it came from.
public sealed record ImportedLedgerEntry
{
    public required ImportSourceKind Source { get; init; }               // Copilot or Codex
    public required string RecordKeySha256 { get; init; }                // ImportIds.RecordKeySha256: 64 lowercase hex
    public required NormalizedGenAiSpan Span { get; init; }              // SessionImportMapper.ToSpan; ConversationId set
    public required CreditCalculationResult Calculation { get; init; }   // the current rates at Span.StartTime
    public ImportedGitContext? Git { get; init; }
    public long? ToolCallCount { get; init; }                            // the log's own count; never expanded into calls
    public IReadOnlyList<GenAiSpanLink> ToolLinks { get; init; } = [];   // the execute_tool links this call owns
}
```

New file `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs`:

```csharp
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// Session-log records that gh ledger import records, under the same authority rules as live spans (UsageAuthority).
public sealed partial class SqliteLedgerStore
{
    // Resolves each entry by the authority rules under one BEGIN IMMEDIATE transaction: inserts the Recorded ones with an
    // alias and the tool links they own, aliases the others that a row accounts for, and writes checkpoint (when it isn't
    // null) in the same transaction. Validates every entry and the checkpoint first (ArgumentException, nothing written).
    // An exception, cancellation included, rolls the whole call back.
    public async Task<ImportRecordResult> RecordImportedAsync(
        IReadOnlyList<ImportedLedgerEntry> entries,
        ImportSessionCheckpoint? checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        EnsureInitialized();
        foreach (ImportedLedgerEntry entry in entries)
        {
            ValidateImportedEntry(entry, nameof(entries));
        }

        if (checkpoint is not null)
        {
            ValidateCheckpoint(checkpoint, nameof(checkpoint));
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        List<ImportResolution> resolutions = new(entries.Count);
        int links = 0;
        foreach (ImportedLedgerEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string source = ImportIds.SourceName(entry.Source);
            string? canonical = UsageIdentity.CanonicalUsageId(
                entry.Span.ProviderName,
                entry.Span.ServiceRequestId,
                entry.Calculation.Channel);
            UsageAuthority.ImportFacts facts = await UsageAuthority.GatherAsync(
                transaction,
                source,
                entry.RecordKeySha256,
                canonical,
                entry.Span.ConversationId!,
                cancellationToken);
            ImportResolution resolution = UsageAuthority.Decide(facts);
            if (resolution == ImportResolution.Recorded)
            {
                long? id = await InsertSpanAsync(
                    transaction,
                    entry.Span,
                    entry.Calculation,
                    ImportProvenance(entry, canonical),
                    now,
                    cancellationToken);
                if (id is long rowId)
                {
                    await UsageAuthority.AddAliasAsync(
                        transaction, source, entry.RecordKeySha256, rowId, "recorded", now, cancellationToken);
                    foreach (GenAiSpanLink link in entry.ToolLinks)
                    {
                        links += await InsertOwnedLinkAsync(transaction, link, rowId, now, cancellationToken);
                    }
                }
                else
                {
                    // Only colliding trace and span ID hashes get here; the row that has them accounts for the record.
                    await UsageAuthority.AddAliasAsync(
                        transaction,
                        source,
                        entry.RecordKeySha256,
                        await ExistingRowAsync(transaction, entry.Span, cancellationToken),
                        "duplicate",
                        now,
                        cancellationToken);
                    resolution = ImportResolution.Duplicate;
                }
            }
            else if (resolution == ImportResolution.OverlapSkipped ||
                     (resolution == ImportResolution.Duplicate && !facts.AliasExists))
            {
                await UsageAuthority.AddAliasAsync(
                    transaction,
                    source,
                    entry.RecordKeySha256,
                    facts.OwnerRowId!.Value,
                    resolution == ImportResolution.OverlapSkipped ? "live" : "duplicate",
                    now,
                    cancellationToken);
            }

            resolutions.Add(resolution);
        }

        if (checkpoint is not null)
        {
            await UsageAuthority.UpsertCheckpointAsync(transaction, checkpoint, now, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ImportRecordResult { Resolutions = resolutions, ToolLinksRecorded = links };
    }

    // The checkpoint of a log, or null when none was written.
    public async Task<ImportSessionCheckpoint?> GetImportSessionAsync(
        ImportSourceKind source,
        string logId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logId);
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await UsageAuthority.ReadCheckpointAsync(connection, null, source, logId, cancellationToken);
    }

    private static RowProvenance ImportProvenance(ImportedLedgerEntry entry, string? canonicalUsageId) =>
        new(
            "import:" + ImportIds.SourceName(entry.Source),
            canonicalUsageId,
            entry.ToolCallCount,
            entry.Git?.RepositoryRoot,
            entry.Git?.RepositorySlug,
            entry.Git?.Branch,
            entry.Git?.HeadCommitSha,
            null);

    private static async Task<long> ExistingRowAsync(
        SqliteTransaction transaction,
        NormalizedGenAiSpan span,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id FROM spans WHERE trace_id = @trace AND span_id = @span;";
        command.Parameters.AddWithValue("@trace", span.TraceId);
        command.Parameters.AddWithValue("@span", span.SpanId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    // An imported execute_tool link owned by the row that asked for the tool. Returns 1, or 0 when the link exists.
    private static async Task<int> InsertOwnedLinkAsync(
        SqliteTransaction transaction,
        GenAiSpanLink link,
        long ownerRowId,
        long recordedAtUnixMs,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO span_links (
                trace_id, span_id, parent_span_id, operation_name, agent_name, conversation_id, start_time_unix_ms,
                tool_name, tool_exit_code, status_code, recorded_at_unix_ms, owner_span_row_id
            ) VALUES (
                @trace_id, @span_id, @parent_span_id, @operation_name, @agent_name, @conversation_id, @start_time_unix_ms,
                @tool_name, @tool_exit_code, @status_code, @recorded_at_unix_ms, @owner
            ) ON CONFLICT (trace_id, span_id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("@trace_id", link.TraceId);
        command.Parameters.AddWithValue("@span_id", link.SpanId);
        command.Parameters.AddWithValue("@parent_span_id", OrDbNull(link.ParentSpanId));
        command.Parameters.AddWithValue("@operation_name", OrDbNull(link.OperationName));
        command.Parameters.AddWithValue("@agent_name", OrDbNull(link.AgentName));
        command.Parameters.AddWithValue("@conversation_id", OrDbNull(link.ConversationId));
        command.Parameters.AddWithValue("@start_time_unix_ms", link.StartTime.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@tool_name", OrDbNull(link.ToolName));
        command.Parameters.AddWithValue("@tool_exit_code", OrDbNull(link.ToolExitCode));
        command.Parameters.AddWithValue("@status_code", link.StatusCode);
        command.Parameters.AddWithValue("@recorded_at_unix_ms", recordedAtUnixMs);
        command.Parameters.AddWithValue("@owner", ownerRowId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateImportedEntry(ImportedLedgerEntry entry, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(entry, parameterName);
        if (entry.Source is not (ImportSourceKind.Copilot or ImportSourceKind.Codex))
        {
            throw new ArgumentException("Imported entries must come from Copilot or Codex session logs.", parameterName);
        }

        if (entry.RecordKeySha256 is not { Length: 64 } ||
            !entry.RecordKeySha256.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f'))
        {
            throw new ArgumentException("Record keys must be 64 lowercase hex characters.", parameterName);
        }

        if (string.IsNullOrEmpty(entry.Span.ConversationId))
        {
            throw new ArgumentException("Imported entries need a conversation ID.", parameterName);
        }

        ValidateEntry(new LedgerEntry(entry.Span, entry.Calculation, null), parameterName);
        if (entry.ToolCallCount is < 0 ||
            entry.ToolLinks.Any(link => link.OperationName != GenAiOperationNames.ExecuteTool))
        {
            throw new ArgumentException(
                "Tool call counts can't be negative, and imported links must be execute_tool links.",
                parameterName);
        }
    }

    private static void ValidateCheckpoint(ImportSessionCheckpoint checkpoint, string parameterName)
    {
        if (checkpoint.Source is not (ImportSourceKind.Copilot or ImportSourceKind.Codex) ||
            string.IsNullOrEmpty(checkpoint.LogId) ||
            checkpoint.LogSizeBytes < 0 ||
            checkpoint.Recorded < 0 ||
            checkpoint.Duplicates < 0 ||
            checkpoint.OverlapSkipped < 0 ||
            checkpoint.AmbiguousOverlap < 0)
        {
            throw new ArgumentException(
                "A checkpoint must name a Copilot or Codex log, and its size and counts can't be negative.",
                parameterName);
        }
    }
}
```

Edit 1 of 6 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
namespace CopilotLedger.Storage;

public sealed class SqliteLedgerStore
{
    private const string InsertSql = """
```

with:

```csharp
namespace CopilotLedger.Storage;

public sealed partial class SqliteLedgerStore
{
    private const string InsertSql = """
```

Edit 2 of 6 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            @api_rates_version, @api_rate_card_sha256,
            @ingest_source, @canonical_usage_id, @imported_tool_call_count
        ) ON CONFLICT (trace_id, span_id) DO NOTHING;
        """;

    // The conditions of a LedgerFilter, with parameters from AddFilterParameters. A null filter property matches every span.
```

with:

```csharp
            @api_rates_version, @api_rate_card_sha256,
            @ingest_source, @canonical_usage_id, @imported_tool_call_count
        ) ON CONFLICT (trace_id, span_id) DO NOTHING
        RETURNING id;
        """;

    // The conditions of a LedgerFilter, with parameters from AddFilterParameters. A null filter property matches every span.
```

Edit 3 of 6 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            return 0;
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        int inserted = 0;
        long recordedAtUnixMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        foreach (LedgerEntry entry in batch)
```

with:

```csharp
            return 0;
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);

        // BEGIN IMMEDIATE: the authority rules read and write under the write lock, so an import runs before or after this.
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        int inserted = 0;
        long recordedAtUnixMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        foreach (LedgerEntry entry in batch)
```

Edit 4 of 6 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        foreach (LedgerEntry entry in batch)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = InsertSql;
            AddInsertParameters(command, entry.Span, entry.Calculation, LiveProvenance(entry), recordedAtUnixMs);
            inserted += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
```

with:

```csharp
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
```

Edit 5 of 6 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                    changed += await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);
```

with:

```csharp
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
```

Edit 6 of 6 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            entry.Git?.PullRequestNumber);

    private static void AddInsertParameters(
        SqliteCommand command,
```

with:

```csharp
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
```

New file `src/CopilotLedger.Storage/UsageAuthority.cs`:

```csharp
using CopilotLedger.Core.Import;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// The live/import authority rules: every model call is charged once, and live spans win. SqliteLedgerStore runs them inside
// its BEGIN IMMEDIATE transactions, and LedgerSnapshot runs Decide on a read-only snapshot, so a dry run decides as the real
// run would. After every transaction:
//   I1 no canonical ID belongs to both an effective imported row and an effective live row;
//   I2 in a conversation with an effective live row, every effective imported row has a canonical ID;
//   I3 a conversation with an effective live row that has no canonical ID has no effective imported rows;
//   I4 every alias names a row, and a 'live' alias a live row; only imported rows are superseded, and nothing is deleted.
// Live rows are always effective (a CHECK constraint), so "live" below means ingest_source = 'otlp'.
internal static class UsageAuthority
{
    internal enum CanonicalOwner
    {
        None,
        Live,
        Import,
    }

    // What the ledger holds for one imported record. OwnerRowId is the effective row with its canonical ID, live first.
    internal readonly record struct ImportFacts(
        bool AliasExists,
        bool HasCanonicalId,
        CanonicalOwner Owner,
        long? OwnerRowId,
        bool ConversationHasLive,
        bool ConversationHasUnidentifiedLive);

    // The first rule that matches decides.
    internal static ImportResolution Decide(ImportFacts facts)
    {
        if (facts.AliasExists)
        {
            return ImportResolution.Duplicate;            // this record was resolved before
        }

        if (facts.HasCanonicalId)
        {
            return facts.Owner switch
            {
                CanonicalOwner.Live => ImportResolution.OverlapSkipped,    // I1: the live row has the charge
                CanonicalOwner.Import => ImportResolution.Duplicate,       // another copy of the call, such as a fork's
                _ => facts.ConversationHasUnidentifiedLive
                    ? ImportResolution.AmbiguousOverlap                    // I3
                    : ImportResolution.Recorded,
            };
        }

        return facts.ConversationHasLive
            ? ImportResolution.AmbiguousOverlap                            // I2
            : ImportResolution.Recorded;
    }

    internal static async Task<ImportFacts> GatherAsync(
        SqliteTransaction transaction,
        string source,
        string recordKeySha256,
        string? canonicalUsageId,
        string conversationId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            WITH owner AS (
                SELECT id, ingest_source = 'otlp' AS live
                  FROM spans
                 WHERE canonical_usage_id = @canonical AND accounting_state = 'effective'
                 ORDER BY live DESC, id
                 LIMIT 1)
            SELECT EXISTS (SELECT 1 FROM import_record_aliases WHERE source = @source AND record_key_sha256 = @key),
                   (SELECT id FROM owner),
                   (SELECT live FROM owner),
                   EXISTS (SELECT 1 FROM spans WHERE conversation_id = @conversation AND ingest_source = 'otlp'),
                   EXISTS (SELECT 1 FROM spans
                            WHERE conversation_id = @conversation AND ingest_source = 'otlp' AND canonical_usage_id IS NULL);
            """;
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@key", recordKeySha256);
        command.Parameters.AddWithValue("@canonical", (object?)canonicalUsageId ?? DBNull.Value);
        command.Parameters.AddWithValue("@conversation", conversationId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        long? ownerRowId = reader.IsDBNull(1) ? null : reader.GetInt64(1);
        return new ImportFacts(
            reader.GetInt64(0) == 1,
            canonicalUsageId is not null,
            ownerRowId is null ? CanonicalOwner.None : reader.GetInt64(2) == 1 ? CanonicalOwner.Live : CanonicalOwner.Import,
            ownerRowId,
            reader.GetInt64(3) == 1,
            reader.GetInt64(4) == 1);
    }

    // Supersedes the effective imported row with canonicalUsageId, wherever it is (I1).
    internal static async Task<int> SupersedeCanonicalAsync(
        SqliteTransaction transaction,
        string canonicalUsageId,
        long supersededBy,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE spans
               SET accounting_state = 'superseded', superseded_by = @superseded_by
             WHERE canonical_usage_id = @canonical AND ingest_source <> 'otlp' AND accounting_state = 'effective';
            """;
        command.Parameters.AddWithValue("@canonical", canonicalUsageId);
        command.Parameters.AddWithValue("@superseded_by", supersededBy);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Supersedes the conversation's effective imported rows that its live rows can't be matched to: those without a canonical
    // ID (I2), or all of them when a live row has none (I3). supersededBy is the live row that arrived; when it's null, the
    // conversation's first live row, and nothing happens without one.
    internal static async Task<int> ReconcileConversationAsync(
        SqliteTransaction transaction,
        string conversationId,
        long? supersededBy,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE spans
               SET accounting_state = 'superseded',
                   superseded_by = COALESCE(@superseded_by,
                       (SELECT MIN(id) FROM spans WHERE conversation_id = @conversation AND ingest_source = 'otlp'))
             WHERE conversation_id = @conversation
               AND ingest_source <> 'otlp'
               AND accounting_state = 'effective'
               AND EXISTS (SELECT 1 FROM spans WHERE conversation_id = @conversation AND ingest_source = 'otlp')
               AND (canonical_usage_id IS NULL
                    OR EXISTS (SELECT 1 FROM spans
                                WHERE conversation_id = @conversation AND ingest_source = 'otlp' AND canonical_usage_id IS NULL));
            """;
        command.Parameters.AddWithValue("@conversation", conversationId);
        command.Parameters.AddWithValue("@superseded_by", supersededBy is long id ? id : DBNull.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // resolution: recorded (the record's own row), live (the live row with its identity), or duplicate (an earlier row).
    internal static async Task AddAliasAsync(
        SqliteTransaction transaction,
        string source,
        string recordKeySha256,
        long spanRowId,
        string resolution,
        long recordedAtUnixMs,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO import_record_aliases (source, record_key_sha256, span_row_id, resolution, recorded_at_unix_ms)
            VALUES (@source, @key, @row, @resolution, @now);
            """;
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@key", recordKeySha256);
        command.Parameters.AddWithValue("@row", spanRowId);
        command.Parameters.AddWithValue("@resolution", resolution);
        command.Parameters.AddWithValue("@now", recordedAtUnixMs);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task UpsertCheckpointAsync(
        SqliteTransaction transaction,
        ImportSessionCheckpoint checkpoint,
        long importedAtUnixMs,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO import_sessions (
                source, log_id, log_size_bytes, log_last_write_unix_ms, complete, records_recorded, records_duplicate,
                records_overlap, records_ambiguous, imported_at_unix_ms
            ) VALUES (
                @source, @log, @size, @time, @complete, @recorded, @duplicate, @overlap, @ambiguous, @now
            ) ON CONFLICT (source, log_id) DO UPDATE SET
                log_size_bytes = excluded.log_size_bytes,
                log_last_write_unix_ms = excluded.log_last_write_unix_ms,
                complete = excluded.complete,
                records_recorded = excluded.records_recorded,
                records_duplicate = excluded.records_duplicate,
                records_overlap = excluded.records_overlap,
                records_ambiguous = excluded.records_ambiguous,
                imported_at_unix_ms = excluded.imported_at_unix_ms;
            """;
        command.Parameters.AddWithValue("@source", ImportIds.SourceName(checkpoint.Source));
        command.Parameters.AddWithValue("@log", checkpoint.LogId);
        command.Parameters.AddWithValue("@size", checkpoint.LogSizeBytes);
        command.Parameters.AddWithValue("@time", checkpoint.LogLastWriteTime.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@complete", checkpoint.Complete ? 1 : 0);
        command.Parameters.AddWithValue("@recorded", checkpoint.Recorded);
        command.Parameters.AddWithValue("@duplicate", checkpoint.Duplicates);
        command.Parameters.AddWithValue("@overlap", checkpoint.OverlapSkipped);
        command.Parameters.AddWithValue("@ambiguous", checkpoint.AmbiguousOverlap);
        command.Parameters.AddWithValue("@now", importedAtUnixMs);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // A log's checkpoint, or null. transaction may be null for a single read.
    internal static async Task<ImportSessionCheckpoint?> ReadCheckpointAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        ImportSourceKind source,
        string logId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT log_size_bytes, log_last_write_unix_ms, complete, records_recorded, records_duplicate, records_overlap,
                   records_ambiguous, imported_at_unix_ms
            FROM import_sessions
            WHERE source = @source AND log_id = @log;
            """;
        command.Parameters.AddWithValue("@source", ImportIds.SourceName(source));
        command.Parameters.AddWithValue("@log", logId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ImportSessionCheckpoint
        {
            Source = source,
            LogId = logId,
            LogSizeBytes = reader.GetInt64(0),
            LogLastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
            Complete = reader.GetInt64(2) == 1,
            Recorded = reader.GetInt64(3),
            Duplicates = reader.GetInt64(4),
            OverlapSkipped = reader.GetInt64(5),
            AmbiguousOverlap = reader.GetInt64(6),
            ImportedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7)),
        };
    }
}
```

New file `tests/CopilotLedger.Tests/Import/ImportIdsTests.cs`:

```csharp
using CopilotLedger.Core.Import;

namespace CopilotLedger.Tests.Import;

public sealed class ImportIdsTests
{
    private const string Session = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string UsageId = "msg_01SyntheticResponseA00000";
    private const string ServiceRequestId = "1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b";

    [Fact]
    public void ID1_RecordKeysAreHashedPerSourceAndKind()
    {
        Assert.Equal(
            "d488d860e770efc924c94c61c0278727bd343d09038d57371d19f663b1cef26f",
            ImportIds.RecordKeySha256(ImportSourceKind.Copilot, ImportedRecordKind.Usage, UsageId));
        Assert.Equal(
            "d57ba7025ffeec5f437aabb47708a52fa402e16cf9ce6b9cff6cc54b6389705c",
            ImportIds.RecordKeySha256(ImportSourceKind.Copilot, ImportedRecordKind.Compaction, ServiceRequestId));
        Assert.Equal(
            "9c84b999b8610a1f30a2f8033d194e153b8e2cdb90c5caaab0395a36a4690af0",
            ImportIds.RecordKeySha256(ImportSourceKind.Copilot, ImportedRecordKind.Usage, ServiceRequestId));
        Assert.Equal(
            "c30d411d93c6e502c64ef699d33095f2e41203aceef25dbab04af74d67039387",
            ImportIds.RecordKeySha256(ImportSourceKind.Codex, ImportedRecordKind.Usage, "resp_0123456789abcdef"));
    }

    [Fact]
    public void ID2_TraceSpanAndToolIdsArePinned()
    {
        Assert.Equal(
            "24909750d9f376616a575a6eeaaae842",
            ImportIds.TraceId(ImportSourceKind.Copilot, Session, UsageId, ImportedRecordKind.Usage, "other"));
        Assert.Equal(
            "78e16000fe718aa49a587d3ea4e6406e",
            ImportIds.TraceId(ImportSourceKind.Copilot, Session, null, ImportedRecordKind.Usage, UsageId));
        Assert.Equal("b93d6266bdfc46ab", ImportIds.SpanId(ImportSourceKind.Copilot, Session, ImportedRecordKind.Usage, UsageId));
        Assert.Equal(
            "d91fdbb4e5326b07",
            ImportIds.ToolSpanId(ImportSourceKind.Copilot, Session, "toolu_01SyntheticTool000000000"));
    }

    [Theory]
    [InlineData(ImportSourceKind.Copilot, "copilot")]
    [InlineData(ImportSourceKind.Claude, "claude")]
    [InlineData(ImportSourceKind.Codex, "codex")]
    public void ID3_SourceNames(ImportSourceKind source, string name)
    {
        Assert.Equal(name, ImportIds.SourceName(source));
    }
}
```

New file `tests/CopilotLedger.Tests/Storage/UsageAuthorityTests.cs`:

```csharp
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

public sealed class UsageAuthorityTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private const string RowsSql = "SELECT id, ingest_source, accounting_state, superseded_by FROM spans ORDER BY id;";

    [Fact]
    public async Task UA1_LiveFirst_TheImportIsOverlapSkippedAndAliasedToTheLiveRow()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("session-a", S1)]);

        ImportRecordResult result = await store.RecordImportedAsync(
            [ImportEntries.Copilot("usage-1", "session-a", S1)],
            null);

        Assert.Equal([ImportResolution.OverlapSkipped], result.Resolutions);
        Assert.Equal(["1|otlp|effective|null"], await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(
            ["live|1"],
            await LedgerSql.RowsAsync(path, "SELECT resolution, span_row_id FROM import_record_aliases;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA2_ImportFirst_TheLiveRowSupersedesTheImportedRow()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);

        ImportRecordResult result = await store.RecordImportedAsync(
            [ImportEntries.Copilot("usage-1", "session-a", S1)],
            null);
        int inserted = await store.RecordAsync([ImportEntries.Live("session-a", S1)]);

        Assert.Equal([ImportResolution.Recorded], result.Resolutions);
        Assert.Equal(1, inserted);
        Assert.Equal(
            ["1|import:copilot|superseded|2", "2|otlp|effective|null"],
            await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(
            ["recorded|1"],
            await LedgerSql.RowsAsync(path, "SELECT resolution, span_row_id FROM import_record_aliases;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA3_ARepeatedImportIsADuplicate()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportedLedgerEntry entry = ImportEntries.Copilot("usage-1", "session-a", S1);

        ImportRecordResult first = await store.RecordImportedAsync([entry], null);
        ImportRecordResult second = await store.RecordImportedAsync([entry, entry], null);

        Assert.Equal([ImportResolution.Recorded], first.Resolutions);
        Assert.Equal([ImportResolution.Duplicate, ImportResolution.Duplicate], second.Resolutions);
        Assert.Equal(["1|import:copilot|effective|null"], await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(1L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM import_record_aliases;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA4_ConcurrentLiveAndImportWritesChargeEachCallOnce()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore live, string path) = await StoreAsync(temp);
        SqliteLedgerStore import = new(path);
        await import.InitializeAsync();

        for (int round = 0; round < 10; round++)
        {
            string serviceRequestId = $"{round:D8}-0000-4000-8000-000000000001";
            string conversation = $"session-{round}";
            await Task.WhenAll(
                Task.Run(() => live.RecordAsync([ImportEntries.Live(conversation, serviceRequestId)])),
                Task.Run(() => import.RecordImportedAsync(
                    [ImportEntries.Copilot($"usage-{round}", conversation, serviceRequestId)],
                    null)));
        }

        Assert.Equal(
            ["10|10|10"],
            await LedgerSql.RowsAsync(
                path,
                """
                SELECT COUNT(*), COUNT(DISTINCT canonical_usage_id), SUM(ingest_source = 'otlp')
                FROM spans WHERE accounting_state = 'effective';
                """));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA5_IdenticalLookingCallsWithDifferentKeysAreAllCharged()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);

        ImportRecordResult result = await store.RecordImportedAsync(
            [
                ImportEntries.Copilot("usage-1", "session-a", S1),
                ImportEntries.Copilot("usage-2", "session-a", S2),
                ImportEntries.Copilot("usage-3", "session-a", null, byok: true),
                ImportEntries.Copilot("usage-4", "session-a", null, byok: true),
            ],
            null);

        Assert.Equal(Enumerable.Repeat(ImportResolution.Recorded, 4), result.Resolutions);
        Assert.Equal(
            ["4|1"],
            await LedgerSql.RowsAsync(
                path,
                """
                SELECT COUNT(*), COUNT(DISTINCT input_tokens || '/' || output_tokens || '/' || start_time_unix_ms)
                FROM spans WHERE accounting_state = 'effective';
                """));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA6_ALiveExportDaysLaterAddsNoCharge()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportedLedgerEntry entry = ImportEntries.Copilot("usage-1", "session-a", S1);
        LedgerEntry late = ImportEntries.Live("session-a", S1, ImportEntries.Start.AddDays(3));

        await store.RecordImportedAsync([entry], null);
        int first = await store.RecordAsync([late]);
        int again = await store.RecordAsync([late]);
        ImportRecordResult reimport = await store.RecordImportedAsync([entry], null);

        Assert.Equal(1, first);
        Assert.Equal(0, again);
        Assert.Equal([ImportResolution.Duplicate], reimport.Resolutions);
        Assert.Equal(
            ["1|import:copilot|superseded|2", "2|otlp|effective|null"],
            await LedgerSql.RowsAsync(path, RowsSql));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA7_AnUnidentifiedLiveRowDefersItsConversation()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("session-a", null)]);

        ImportRecordResult result = await store.RecordImportedAsync(
            [
                ImportEntries.Copilot("usage-1", "session-a", S1),
                ImportEntries.Copilot("usage-2", "session-a", null, byok: true),
            ],
            null);

        Assert.Equal([ImportResolution.AmbiguousOverlap, ImportResolution.AmbiguousOverlap], result.Resolutions);
        Assert.Equal(["1|otlp|effective|null"], await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(0L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM import_record_aliases;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA8_ALaterUnidentifiedLiveRowSupersedesItsConversationsImports()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportedLedgerEntry first = ImportEntries.Copilot("usage-1", "session-a", S1);
        ImportedLedgerEntry second = ImportEntries.Copilot("usage-2", "session-a", S2);
        await store.RecordImportedAsync([first, second, ImportEntries.Copilot("usage-3", "session-b", S3)], null);

        await store.RecordAsync([ImportEntries.Live("session-a", null)]);
        ImportRecordResult reimport = await store.RecordImportedAsync([first, second], null);

        Assert.Equal([ImportResolution.Duplicate, ImportResolution.Duplicate], reimport.Resolutions);
        Assert.Equal(
            [
                "1|import:copilot|superseded|4",
                "2|import:copilot|superseded|4",
                "3|import:copilot|effective|null",
                "4|otlp|effective|null",
            ],
            await LedgerSql.RowsAsync(path, RowsSql));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA9_AnIdentifiedLiveRowSupersedesOnlyUnidentifiedImports()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        await store.RecordImportedAsync(
            [
                ImportEntries.Copilot("usage-1", "session-a", S1),
                ImportEntries.Copilot("usage-2", "session-a", null, byok: true),
            ],
            null);

        await store.RecordAsync([ImportEntries.Live("session-a", S2)]);

        Assert.Equal(
            ["1|import:copilot|effective|null", "2|import:copilot|superseded|3", "3|otlp|effective|null"],
            await LedgerSql.RowsAsync(path, RowsSql));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA10_AnUnidentifiedRecordIsRecordedOnlyWithoutLiveRows()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportRecordResult alone = await store.RecordImportedAsync(
            [ImportEntries.Copilot("usage-1", "session-a", null, byok: true)],
            null);
        await store.RecordAsync([ImportEntries.Live("session-b", S1)]);

        ImportRecordResult beside = await store.RecordImportedAsync(
            [ImportEntries.Copilot("usage-2", "session-b", null, byok: true)],
            null);

        Assert.Equal([ImportResolution.Recorded], alone.Resolutions);
        Assert.Equal([ImportResolution.AmbiguousOverlap], beside.Resolutions);
        Assert.Equal(["1|import:copilot|effective|null", "2|otlp|effective|null"], await LedgerSql.RowsAsync(path, RowsSql));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA11_TheSameRecordInAnotherConversationIsADuplicate()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);

        ImportRecordResult result = await store.RecordImportedAsync(
            [
                ImportEntries.Copilot("usage-1", "session-a", S1),
                ImportEntries.Copilot("usage-1", "session-fork", S1),
                ImportEntries.Copilot("usage-2", "session-fork", S1),
            ],
            null);

        Assert.Equal(
            [ImportResolution.Recorded, ImportResolution.Duplicate, ImportResolution.Duplicate],
            result.Resolutions);
        Assert.Equal(["1|import:copilot|effective|null"], await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(
            ["duplicate|1", "recorded|1"],
            await LedgerSql.RowsAsync(path, "SELECT resolution, span_row_id FROM import_record_aliases ORDER BY resolution;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA12_TheCheckpointIsWrittenWithTheBatch()
    {
        using TempDirectory temp = new();
        ManualTimeProvider time = new(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero));
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path, time);
        await store.InitializeAsync();
        ImportSessionCheckpoint checkpoint = new()
        {
            Source = ImportSourceKind.Copilot,
            LogId = "session-a",
            LogSizeBytes = 4_096,
            LogLastWriteTime = new DateTimeOffset(2026, 11, 20, 12, 34, 56, 789, TimeSpan.Zero),
            Complete = true,
            Recorded = 1,
            Duplicates = 2,
            OverlapSkipped = 3,
            AmbiguousOverlap = 4,
        };

        await store.RecordImportedAsync([ImportEntries.Copilot("usage-1", "session-a", S1)], checkpoint);
        ImportSessionCheckpoint? written = await store.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a");
        time.Advance(TimeSpan.FromHours(1));
        await store.RecordImportedAsync([], checkpoint with { Complete = false, LogSizeBytes = 8_192 });

        Assert.Equal(checkpoint with { ImportedAt = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero) }, written);
        Assert.Equal(
            checkpoint with
            {
                Complete = false,
                LogSizeBytes = 8_192,
                ImportedAt = new DateTimeOffset(2026, 11, 23, 11, 0, 0, TimeSpan.Zero),
            },
            await store.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a"));
        Assert.Null(await store.GetImportSessionAsync(ImportSourceKind.Codex, "session-a"));
    }

    [Fact]
    public async Task UA13_ACancelledCallWritesNothing()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.RecordImportedAsync(
            [ImportEntries.Copilot("usage-1", "session-a", S1)],
            new ImportSessionCheckpoint
            {
                Source = ImportSourceKind.Copilot,
                LogId = "session-a",
                LogSizeBytes = 1,
                LogLastWriteTime = ImportEntries.Start,
                Complete = true,
                Recorded = 1,
                Duplicates = 0,
                OverlapSkipped = 0,
                AmbiguousOverlap = 0,
            },
            cancelled.Token));

        Assert.Equal(
            ["0|0|0"],
            await LedgerSql.RowsAsync(
                path,
                """
                SELECT (SELECT COUNT(*) FROM spans), (SELECT COUNT(*) FROM import_record_aliases),
                       (SELECT COUNT(*) FROM import_sessions);
                """));
    }

    [Theory]
    [InlineData(0, "Imported entries must come from Copilot or Codex session logs. (Parameter 'entries')")]
    [InlineData(1, "Record keys must be 64 lowercase hex characters. (Parameter 'entries')")]
    [InlineData(2, "Imported entries need a conversation ID. (Parameter 'entries')")]
    public async Task UA14_InvalidEntriesThrowBeforeAnythingIsWritten(int caseNumber, string message)
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportedLedgerEntry valid = ImportEntries.Copilot("usage-1", "session-a", S1);
        ImportedLedgerEntry invalid = caseNumber switch
        {
            0 => valid with { Source = ImportSourceKind.Claude },
            1 => valid with { RecordKeySha256 = valid.RecordKeySha256[..63] },
            _ => valid with { Span = valid.Span with { ConversationId = null } },
        };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => store.RecordImportedAsync([valid, invalid], null));

        Assert.Equal(message, exception.Message);
        Assert.Equal(0L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM spans;"));
    }

    [Fact]
    public async Task UA15_ALiveRowWhoseConversationIsAssignedLaterFollowsTheRules()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        await store.RecordImportedAsync([ImportEntries.Copilot("usage-1", "session-a", null, byok: true)], null);
        LedgerEntry live = ImportEntries.Live(null, null);
        await store.RecordAsync([live]);
        string[] before = [.. await LedgerSql.RowsAsync(path, RowsSql)];

        await store.RecordSpanLinksAsync(
        [
            new GenAiSpanLink
            {
                TraceId = live.Span.TraceId,
                SpanId = "a000000000000001",
                OperationName = "invoke_agent",
                ConversationId = "session-a",
                StartTime = ImportEntries.Start,
            },
            new GenAiSpanLink
            {
                TraceId = live.Span.TraceId,
                SpanId = live.Span.SpanId,
                ParentSpanId = "a000000000000001",
                OperationName = "chat",
                StartTime = ImportEntries.Start,
            },
        ]);
        int changed = await store.BackfillFromAgentSpansAsync([live.Span.TraceId]);

        Assert.Equal(["1|import:copilot|effective|null", "2|otlp|effective|null"], before);
        Assert.Equal(1, changed);
        Assert.Equal(
            ["1|import:copilot|superseded|2", "2|otlp|effective|null"],
            await LedgerSql.RowsAsync(path, RowsSql));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA16_RecordedImportsOwnTheirToolLinks()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportedLedgerEntry entry = ImportEntries.Copilot("usage-1", "session-a", S1);
        entry = entry with
        {
            ToolLinks = [ImportEntries.ToolLink(entry, "toolu-1", 0), ImportEntries.ToolLink(entry, "toolu-2", 1)],
        };

        ImportRecordResult first = await store.RecordImportedAsync([entry], null);
        ImportRecordResult second = await store.RecordImportedAsync([entry], null);

        Assert.Equal(2, first.ToolLinksRecorded);
        Assert.Equal(0, second.ToolLinksRecorded);
        Assert.Equal(
            ["execute_tool|bash|0|1", "execute_tool|bash|1|1"],
            await LedgerSql.RowsAsync(
                path,
                "SELECT operation_name, tool_name, tool_exit_code, owner_span_row_id FROM span_links ORDER BY tool_exit_code;"));
    }

    [Fact]
    public async Task UA17_ACompactionAndAUsageRecordOfOneCallAreChargedOnce()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("session-b", S2)]);

        ImportRecordResult result = await store.RecordImportedAsync(
            [
                ImportEntries.Copilot("usage-1", "session-a", S1),
                ImportEntries.Copilot(S1, "session-a", S1, ImportedRecordKind.Compaction),
                ImportEntries.Copilot(S2, "session-b", S2, ImportedRecordKind.Compaction),
            ],
            null);

        Assert.Equal(
            [ImportResolution.Recorded, ImportResolution.Duplicate, ImportResolution.OverlapSkipped],
            result.Resolutions);
        Assert.Equal(
            ["1|otlp|effective|null", "2|import:copilot|effective|null"],
            await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(
            ["duplicate|2", "live|1", "recorded|2"],
            await LedgerSql.RowsAsync(path, "SELECT resolution, span_row_id FROM import_record_aliases ORDER BY resolution;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    private static async Task<(SqliteLedgerStore Store, string Path)> StoreAsync(TempDirectory temp)
    {
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        return (store, path);
    }
}
```

New file `tests/CopilotLedger.Tests/TestSupport/ImportEntries.cs`:

```csharp
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
```

### Tests

- **ID1**: record keys hash per source and kind (a usage and a compaction key never collide). **ID2**: trace, span, and tool IDs are pinned. **ID3** (3): source names.
- **UA1**: live first, then the import: `OverlapSkipped`, aliased to the live row, one effective charge. **UA2**: import first, then live: the imported row is superseded by the live one in the same transaction. **UA3**: a repeated import is a `Duplicate`. **UA4**: ten rounds of concurrent live and import writes of the same calls leave exactly one effective row per call. **UA5**: calls with identical models, tokens, and times but different keys are all charged. **UA6**: a live export of an imported call days later adds no charge.
- **UA7**: an unidentified live row defers its session's imports. **UA8**: an unidentified live row arriving later supersedes all of its session's imports. **UA9**: an identified live row supersedes only its session's unidentified imports. **UA10**: an unidentified record is recorded only in a session without live rows. **UA11**: the same record in another session is a `Duplicate`.
- **UA12**: the checkpoint is written with the batch. **UA13**: a cancelled call writes nothing. **UA14** (3): invalid entries throw before anything is written. **UA15**: a live row whose session the backfill assigns later follows the rules. **UA16**: recorded imports own their tool links. **UA17**: a compaction and a usage record of one call (one service request ID) are charged once.
- `ImportEntries.AssertInvariantsAsync` checks I1–I4 after the tests that change the ledger.

### Acceptance criteria

- Every arrival order and concurrent writes give one effective charge per call (UA1–UA6, UA17); no rule matches by time or tokens.
- Deferral and supersession follow I2 and I3 (UA7–UA10, UA15); superseded rows are kept.
- `dotnet test CopilotLedger.slnx` passes **1083** tests (1059 + 24).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 4 specification: Effective Rows Everywhere

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | Every query that counts spans or tool failures keeps effective rows only |
| `tests/CopilotLedger.Tests/Storage/EffectiveRowTests.cs` | New: ER1–ER10 |

### The audit

Every `FROM spans` and `JOIN spans` site of `CopilotLedger.Storage` (the only project with SQL):

- `FilterSql`, shared by rollups (`GetRollupAsync`), prompt chains (`GetTopPromptChainsAsync`), what-if repricing, cache efficiency, turns, and bloat, gains `AND accounting_state = 'effective'`.
- `EffectiveLinkSql`, a link counts unless a superseded row owns it, is added to the failed-tool counts of prompt chains and the circuit breaker's tool activity.
- Explicit predicates: the API-equivalent backfill (`WHERE api_equivalent_status IS NULL AND accounting_state = 'effective'`), the circuit breaker's trace activity (the trace summary, its costed spans, and its commits), both budget queries and the budget alert's `UPDATE`, and the turns CTE.
- Not changed: the inserts, `BackfillFromAgentSpansAsync` (it assigns attribution to live rows), and the authority routine (it reads both states on purpose). MCP and receipts read through these methods.

### Code (copy exactly)

Edit 1 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        """;

    // The conditions of a LedgerFilter, with parameters from AddFilterParameters. A null filter property matches every span.
    private const string FilterSql = """
        (@from IS NULL OR start_time_unix_ms >= @from)
```

with:

```csharp
        """;

    // The conditions of a LedgerFilter, with parameters from AddFilterParameters. A null filter property matches every span.
    // Only effective rows count: an imported row that a live row superseded is kept for diagnosis and never reported.
    private const string FilterSql = """
        (@from IS NULL OR start_time_unix_ms >= @from)
```

Edit 2 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
          AND (@pull_request IS NULL OR pull_request_number = @pull_request)
          AND (@agent IS NULL OR COALESCE(agent_name, '(none)') = @agent)
        """;

    // CacheMetrics.IsReported: the span carried at least one cache counter.
```

with:

```csharp
          AND (@pull_request IS NULL OR pull_request_number = @pull_request)
          AND (@agent IS NULL OR COALESCE(agent_name, '(none)') = @agent)
          AND accounting_state = 'effective'
        """;

    // An execute_tool link counts unless an imported row that was superseded owns it (its live twin's links count instead).
    private const string EffectiveLinkSql = """
        (owner_span_row_id IS NULL
                 OR EXISTS (SELECT 1 FROM spans WHERE spans.id = span_links.owner_span_row_id AND spans.accounting_state = 'effective'))
        """;

    // CacheMetrics.IsReported: the span carried at least one cache counter.
```

Edit 3 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                       input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens, request_model
                FROM spans
                WHERE api_equivalent_status IS NULL
                ORDER BY id;
                """;
```

with:

```csharp
                       input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens, request_model
                FROM spans
                WHERE api_equivalent_status IS NULL AND accounting_state = 'effective'
                ORDER BY id;
                """;
```

Edit 4 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        {
            summary.CommandText = """
                SELECT MAX(conversation_id), MAX(COALESCE(repo_slug, repo_root, '(none)')), MAX(COALESCE(branch_name, '(none)')), MAX(repo_root), COUNT(*) FROM spans WHERE trace_id = @trace;
                """;
            summary.Parameters.AddWithValue("@trace", traceId);
```

with:

```csharp
        {
            summary.CommandText = """
                SELECT MAX(conversation_id), MAX(COALESCE(repo_slug, repo_root, '(none)')), MAX(COALESCE(branch_name, '(none)')), MAX(repo_root), COUNT(*) FROM spans WHERE trace_id = @trace AND accounting_state = 'effective';
                """;
            summary.Parameters.AddWithValue("@trace", traceId);
```

Edit 5 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        {
            query.CommandText = """
                SELECT span_id, end_time_unix_ms, cost_usd_pico FROM spans WHERE trace_id = @trace AND cost_usd_pico > 0 ORDER BY end_time_unix_ms, span_id;
                """;
            query.Parameters.AddWithValue("@trace", traceId);
```

with:

```csharp
        {
            query.CommandText = """
                SELECT span_id, end_time_unix_ms, cost_usd_pico FROM spans WHERE trace_id = @trace AND cost_usd_pico > 0 AND accounting_state = 'effective' ORDER BY end_time_unix_ms, span_id;
                """;
            query.Parameters.AddWithValue("@trace", traceId);
```

Edit 6 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = """
                SELECT span_id, start_time_unix_ms, tool_name, tool_exit_code, status_code FROM span_links WHERE trace_id = @trace AND operation_name = 'execute_tool' ORDER BY start_time_unix_ms, span_id;
                """;
            query.Parameters.AddWithValue("@trace", traceId);
```

with:

```csharp
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = $"""
                SELECT span_id, start_time_unix_ms, tool_name, tool_exit_code, status_code FROM span_links WHERE trace_id = @trace AND operation_name = 'execute_tool' AND {EffectiveLinkSql} ORDER BY start_time_unix_ms, span_id;
                """;
            query.Parameters.AddWithValue("@trace", traceId);
```

Edit 7 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        {
            query.CommandText = """
                SELECT span_id, start_time_unix_ms, head_commit_sha FROM spans WHERE trace_id = @trace AND head_commit_sha IS NOT NULL ORDER BY start_time_unix_ms, span_id;
                """;
            query.Parameters.AddWithValue("@trace", traceId);
```

with:

```csharp
        {
            query.CommandText = """
                SELECT span_id, start_time_unix_ms, head_commit_sha FROM spans WHERE trace_id = @trace AND head_commit_sha IS NOT NULL AND accounting_state = 'effective' ORDER BY start_time_unix_ms, span_id;
                """;
            query.Parameters.AddWithValue("@trace", traceId);
```

Edit 8 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                   (SELECT COALESCE(SUM(s.cost_usd_pico), 0) FROM spans AS s
                     WHERE COALESCE(s.repo_slug, s.repo_root, '(none)') = b.repository
                       AND COALESCE(s.branch_name, '(none)') = b.branch) AS spent_usd_pico
            FROM branch_budgets AS b
            WHERE (@repository IS NULL OR b.repository = @repository) AND (@branch IS NULL OR b.branch = @branch) ORDER BY b.repository, b.branch;
```

with:

```csharp
                   (SELECT COALESCE(SUM(s.cost_usd_pico), 0) FROM spans AS s
                     WHERE COALESCE(s.repo_slug, s.repo_root, '(none)') = b.repository
                       AND COALESCE(s.branch_name, '(none)') = b.branch
                       AND s.accounting_state = 'effective') AS spent_usd_pico
            FROM branch_budgets AS b
            WHERE (@repository IS NULL OR b.repository = @repository) AND (@branch IS NULL OR b.branch = @branch) ORDER BY b.repository, b.branch;
```

Edit 9 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE branch_budgets SET alerted_at_unix_ms = @now WHERE repository = @repository AND branch = @branch AND alerted_at_unix_ms IS NULL AND budget_usd_pico < (SELECT COALESCE(SUM(cost_usd_pico), 0) FROM spans WHERE COALESCE(repo_slug, repo_root, '(none)') = @repository AND COALESCE(branch_name, '(none)') = @branch);
            """;
        command.Parameters.AddWithValue("@repository", repository);
```

with:

```csharp
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE branch_budgets SET alerted_at_unix_ms = @now WHERE repository = @repository AND branch = @branch AND alerted_at_unix_ms IS NULL AND budget_usd_pico < (SELECT COALESCE(SUM(cost_usd_pico), 0) FROM spans WHERE COALESCE(repo_slug, repo_root, '(none)') = @repository AND COALESCE(branch_name, '(none)') = @branch AND accounting_state = 'effective');
            """;
        command.Parameters.AddWithValue("@repository", repository);
```

Edit 10 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                   (SELECT COALESCE(SUM(s.cost_usd_pico), 0) FROM spans AS s
                     WHERE COALESCE(s.repo_slug, s.repo_root, '(none)') = b.repository
                       AND COALESCE(s.branch_name, '(none)') = b.branch) AS spent_usd_pico
            FROM branch_budgets AS b
            WHERE b.alerted_at_unix_ms IS NOT NULL AND b.alerted_at_unix_ms >= @since AND b.notified_at_unix_ms IS NULL ORDER BY b.alerted_at_unix_ms, b.repository, b.branch;
```

with:

```csharp
                   (SELECT COALESCE(SUM(s.cost_usd_pico), 0) FROM spans AS s
                     WHERE COALESCE(s.repo_slug, s.repo_root, '(none)') = b.repository
                       AND COALESCE(s.branch_name, '(none)') = b.branch
                       AND s.accounting_state = 'effective') AS spent_usd_pico
            FROM branch_budgets AS b
            WHERE b.alerted_at_unix_ms IS NOT NULL AND b.alerted_at_unix_ms >= @since AND b.notified_at_unix_ms IS NULL ORDER BY b.alerted_at_unix_ms, b.repository, b.branch;
```

Edit 11 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            Enumerable.Range(0, traces.Length).Select(index => $"@t{index}"));
        command.CommandText = $"""
            SELECT trace_id, COUNT(*) FROM span_links WHERE trace_id IN ({parameters}) AND operation_name = 'execute_tool' AND ((tool_exit_code IS NOT NULL AND tool_exit_code <> 0) OR status_code = 2) GROUP BY trace_id;
            """;
        for (int index = 0; index < traces.Length; index++)
```

with:

```csharp
            Enumerable.Range(0, traces.Length).Select(index => $"@t{index}"));
        command.CommandText = $"""
            SELECT trace_id, COUNT(*) FROM span_links WHERE trace_id IN ({parameters}) AND operation_name = 'execute_tool' AND ((tool_exit_code IS NOT NULL AND tool_exit_code <> 0) OR status_code = 2) AND {EffectiveLinkSql} GROUP BY trace_id;
            """;
        for (int index = 0; index < traces.Length; index++)
```

Edit 12 of 12 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                WHERE (@trace IS NULL OR trace_id = @trace)
                  AND (@conversation IS NULL OR conversation_id = @conversation)
            )
            SELECT trace_id, span_id, parent_span_id, conversation_id, agent_span_id, start_time_unix_ms,
```

with:

```csharp
                WHERE (@trace IS NULL OR trace_id = @trace)
                  AND (@conversation IS NULL OR conversation_id = @conversation)
                  AND accounting_state = 'effective'
            )
            SELECT trace_id, span_id, parent_span_id, conversation_id, agent_span_id, start_time_unix_ms,
```

New file `tests/CopilotLedger.Tests/Storage/EffectiveRowTests.cs`:

```csharp
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

// One ledger: a live request (L, $0.003) in session-a on main, the imported twin it superseded (T, $3, with a failed tool
// call), and another imported request (E, $0.006, with a failed tool call) in session-b on feature/x. Only L and E count.
public sealed class EffectiveRowTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    public static TheoryData<LedgerGrouping> Groupings => new()
    {
        LedgerGrouping.Repository,
        LedgerGrouping.Branch,
        LedgerGrouping.Model,
        LedgerGrouping.Agent,
        LedgerGrouping.ChannelModel,
    };

    [Theory]
    [MemberData(nameof(Groupings))]
    public async Task ER1_RollupsCountOnlyEffectiveRows(LedgerGrouping grouping)
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        IReadOnlyList<LedgerRollupRow> rows = await ledger.Store.GetRollupAsync(grouping, LedgerFilter.All);

        Assert.Equal(2, rows.Sum(row => row.SpanCount));
        Assert.Equal(3_000, rows.Sum(row => row.InputTokens));
        Assert.Equal(0.009m, rows.Sum(row => row.AiCreditsUsd));
        Assert.Equal(0.9m, rows.Sum(row => row.AiCredits));
        Assert.Equal(0.009m, rows.Sum(row => row.ApiEquivalentUsd));
    }

    [Fact]
    public async Task ER2_PromptChainsAndCountsLeaveOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        IReadOnlyList<PromptChainRow> chains = await ledger.Store.GetTopPromptChainsAsync(LedgerFilter.All, 10);

        Assert.Equal([ledger.Other.Span.TraceId, ledger.Live.Span.TraceId], chains.Select(chain => chain.TraceId));
        Assert.Equal([0.006m, 0.003m], chains.Select(chain => chain.AiCreditsUsd));
        Assert.All(chains, chain => Assert.Equal(["claude-sonnet-5.5"], chain.Models));
        Assert.Equal(2, await ledger.Store.GetPromptCountAsync(LedgerFilter.All));
    }

    [Fact]
    public async Task ER3_WhatIfPricingLeavesOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);
        JsonRateCardCalculator whatIf = JsonRateCardCalculator.LoadEmbedded();

        LedgerRollupRow total = Assert.Single(
            await ledger.Store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All, whatIf));
        IReadOnlyList<PromptChainRow> chains = await ledger.Store.GetTopPromptChainsAsync(LedgerFilter.All, 10, whatIf);

        Assert.Equal(0.009m, total.ApiEquivalentUsd);
        Assert.Equal(2, total.ApiEquivalentEstimatedSpanCount);
        Assert.Equal([0.006m, 0.003m], chains.Select(chain => chain.ApiEquivalentUsd));
    }

    [Fact]
    public async Task ER4_CacheEfficiencyLeavesOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        CacheEfficiencyRow row = Assert.Single(
            await ledger.Store.GetCacheEfficiencyAsync(LedgerGrouping.Repository, LedgerFilter.All));

        Assert.Equal(2, row.SpanCount);
        Assert.Equal(3_000, row.InputTokens);
        Assert.Equal(2_000, row.MaxInputTokens);
    }

    [Fact]
    public async Task ER5_TurnsLeaveOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        TurnTokenRow turn = Assert.Single(await ledger.Store.GetTurnBreakdownAsync(
            TurnSelector.ForConversation("session-a"),
            LedgerFilter.All));

        Assert.Equal(ledger.Live.Span.SpanId, turn.SpanId);
        Assert.Equal(1_000, turn.InputTokens);
    }

    [Fact]
    public async Task ER6_BloatLeavesOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        ContextBloatReport report = await ledger.Store.GetContextBloatAsync(LedgerFilter.All, 10);

        Assert.Equal(2, report.SpansWithoutAgent);
        Assert.Equal(
            [ledger.Other.Span.TraceId, ledger.Live.Span.TraceId],
            report.ByEstimatedUncachedInput.Select(chain => chain.TraceId));
        Assert.Equal([2_000, 1_000], report.ByEstimatedUncachedInput.Select(chain => chain.EstimatedUncachedInputTokens));
    }

    [Fact]
    public async Task ER7_BudgetsCountOnlyEffectiveSpend()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);
        await ledger.Store.SetBranchBudgetAsync("contoso/webapp", "main", 1m);

        BranchBudgetRow budget = Assert.Single(await ledger.Store.GetBranchBudgetsAsync());
        bool alerted = await ledger.Store.MarkBranchBudgetAlertedAsync("contoso/webapp", "main");

        Assert.Equal(0.3m, budget.SpentAiCredits);
        Assert.False(budget.Exceeded);
        Assert.False(alerted);
        Assert.Empty(await ledger.Store.GetUnnotifiedBudgetAlertsAsync(DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task ER8_TraceActivityLeavesOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        TraceActivity twin = await ledger.Store.GetTraceActivityAsync(ledger.Twin.Span.TraceId);
        TraceActivity live = await ledger.Store.GetTraceActivityAsync(ledger.Live.Span.TraceId);

        Assert.Equal(0, twin.SpanCount);
        Assert.Empty(twin.Costs);
        Assert.Empty(twin.ToolCalls);
        Assert.Empty(twin.Commits);
        Assert.Equal(1, live.SpanCount);
        Assert.Equal([0.003m], live.Costs.Select(cost => cost.CostUsd));
        Assert.Equal([Sha], live.Commits.Select(commit => commit.CommitSha));
    }

    [Fact]
    public async Task ER9_FailedToolCallsOfSupersededRowsDontCount()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        IReadOnlyDictionary<string, long> counts = await ledger.Store.GetFailedToolCallCountsAsync(
            [ledger.Twin.Span.TraceId, ledger.Other.Span.TraceId]);
        TraceActivity other = await ledger.Store.GetTraceActivityAsync(ledger.Other.Span.TraceId);

        Assert.Equal(ledger.Other.Span.TraceId, Assert.Single(counts).Key);
        Assert.Equal(1, counts[ledger.Other.Span.TraceId]);
        Assert.Single(other.ToolCalls);
    }

    [Fact]
    public async Task ER10_TheApiEquivalentBackfillLeavesOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);
        await LedgerSql.ExecuteAsync(
            ledger.Path,
            """
            UPDATE spans SET api_equivalent_usd_pico = NULL, api_equivalent_status = NULL, api_rate_card_model_id = NULL,
                             api_rates_version = NULL, api_rate_card_sha256 = NULL;
            """);

        ApiEquivalentBackfill backfill = await ledger.Store.BackfillApiEquivalentAsync(
            JsonRateCardCalculator.LoadEmbedded(),
            dryRun: true);

        Assert.Equal(2, backfill.SpanCount);
        Assert.Equal(0.009m, backfill.PricedUsd);
    }

    private static async Task<Ledger> SeedAsync(TempDirectory temp)
    {
        string path = Path.Combine(temp.Path, "ledger.db");
        string root = Path.Combine(temp.Path, "webapp");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        ImportedLedgerEntry twin = Imported("usage-t", "session-a", S1, 1_000_000, 100_000, root, "main");
        twin = twin with { ToolLinks = [ImportEntries.ToolLink(twin, "toolu-t", 1)] };
        ImportedLedgerEntry other = Imported("usage-e", "session-b", S2, 2_000, 200, root, "feature/x");
        other = other with { ToolLinks = [ImportEntries.ToolLink(other, "toolu-e", 1)] };
        await store.RecordImportedAsync([twin, other], null);
        LedgerEntry live = ImportEntries.Live("session-a", S1);
        NormalizedGenAiSpan span = live.Span with { RequestModel = "claude-sonnet-5.5" };
        live = new LedgerEntry(
            span,
            ImportEntries.Calculator.Calculate(span),
            new GitWorkspaceContext(root, Path.Combine(root, ".git"), "main", Sha, null, "contoso/webapp"));
        await store.RecordAsync([live]);
        Assert.Equal(
            ["import:copilot|superseded", "import:copilot|effective", "otlp|effective"],
            await LedgerSql.RowsAsync(path, "SELECT ingest_source, accounting_state FROM spans ORDER BY id;"));
        return new Ledger(store, path, live, twin, other);
    }

    private static ImportedLedgerEntry Imported(
        string key,
        string conversation,
        string serviceRequestId,
        long input,
        long output,
        string root,
        string branch)
    {
        ImportedLedgerEntry entry = ImportEntries.Copilot(key, conversation, serviceRequestId);
        NormalizedGenAiSpan span = entry.Span with
        {
            RequestModel = "claude-sonnet-5.5",
            InputTokens = input,
            OutputTokens = output,
        };
        return entry with
        {
            Span = span,
            Calculation = ImportEntries.Calculator.Calculate(span),
            Git = new ImportedGitContext
            {
                RepositoryRoot = root,
                RepositorySlug = "contoso/webapp",
                Branch = branch,
                HeadCommitSha = Sha,
            },
        };
    }

    private sealed record Ledger(
        SqliteLedgerStore Store,
        string Path,
        LedgerEntry Live,
        ImportedLedgerEntry Twin,
        ImportedLedgerEntry Other);
}
```

### Tests

Each test records an effective and a superseded imported row (and links where relevant) and checks that only the effective one counts:

- **ER1** (5): rollups by repository, branch, model, agent, and channel and model. **ER2**: prompt chains and their counts. **ER3**: what-if pricing. **ER4**: cache efficiency. **ER5**: turns. **ER6**: bloat. **ER7**: budgets and the budget alert. **ER8**: the circuit breaker's trace activity. **ER9**: failed tool calls owned by a superseded row don't count. **ER10**: the API-equivalent backfill doesn't price superseded rows.

### Acceptance criteria

- No report, budget, MCP tool, or breaker rule counts a superseded row (ER1–ER10).
- `dotnet test CopilotLedger.slnx` passes **1097** tests (1083 + 14).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 5 specification: Session Logs and the Copilot Gate

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Import/ISessionLogSource.cs`, `SessionLogLocation.cs`, `SessionLogStatus.cs`, `SessionLogReadResult.cs`, `ImportDiagnostic.cs`, `ImportDiagnosticCode.cs`, `ImportEventKind.cs`, `ImportedUsageRecord.cs`, `ImportedToolCall.cs`, `ImportLimits.cs`, `ProducerVersion.cs`, `CopilotSessionIds.cs` | New contracts |
| `src/CopilotLedger.Core/Import/JsonlLineReader.cs`, `JsonFields.cs`, `ImportDiagnosticBag.cs`, `SessionLogs.cs` | New (internal): the bounded reader, deny-by-default extraction, diagnostics, and shared validation |
| `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs` | New: discovery, the gate, and the session state machine's frame (records follow in Task 6) |
| `src/CopilotLedger.Otlp/Attribution/CopilotSessionWorkspaceLocator.cs` | Uses `CopilotSessionIds.IsValid` |
| `tests/CopilotLedger.Tests/TestSupport/CopilotSessionLogs.cs` | New: a builder of synthetic Copilot logs with canaries in content fields |
| `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs` | New: CS1–CS14 |

### Contracts

- `ISessionLogSource { Kind; IReadOnlyList<SessionLogLocation> Discover(string home); Task<SessionLogReadResult> ReadAsync(SessionLogLocation, CancellationToken) }`: discovery is ordered by `LogId` and gives nothing for a missing home; `ReadAsync` reads at most the discovered size and throws only `ArgumentException` and `OperationCanceledException` (an unreadable log is `Unreadable`).
- `SessionLogLocation { Source, LogId, Path, SizeBytes, LastWriteTime, IsActive, IsCompressed }`; `SessionLogStatus { Read, MissingSessionStart, SessionIdMismatch, UnsupportedProducer, UnsupportedProducerVersion, AggregateOnly, Compressed, Unreadable }`; `SessionLogReadResult { Status, ProducerVersion, Records, ToolCalls, Diagnostics, RecordsParsed, UnsupportedUsageShapes, MalformedLines, SkippedEvents }`.
- `ImportDiagnostic { Source, LogId, Code, Event, Field, Version, Count, FirstLine, FirstByteOffset }`: never a byte, key, value, event type, or exception message of a log; `Field` is one of the parser's own path constants.
- `ImportedUsageRecord` and `ImportedToolCall` carry metadata only (no free text); `ImportLimits`: 8 MiB lines, 1,000,000,000 tokens, 86,400,000 ms; `ProducerVersion.TryParse` accepts `MAJOR.MINOR.PATCH` with an optional `-`/`+` suffix of at most 64 characters in all.

### Algorithm

1. **Reader.** Read the file in 64 KiB chunks up to the discovered size. Each `\n`-terminated line goes to the parser with its 1-based number and byte offset. A line longer than 8 MiB is discarded as it streams, never held, and counted as oversized; a last line without `\n` is counted as unterminated and never parsed.
2. **Type first.** `JsonFields.ReadType` scans the whole object with `Utf8JsonReader` for one string `type` at the top level, wherever it is; a line that isn't exactly one JSON object with one string `type` is malformed. A type the parser doesn't know is skipped and counted, without reading anything else.
3. **Allowlist.** For a known type, `JsonFields.Read` extracts only the paths of its `PathTree`, each with a length limit; a duplicated path makes the event malformed (or the record unsupported) and is named only by the parser's own constant.
4. **Copilot gate.** Discovery lists `session-state/<id>/events.jsonl` for valid IDs, with `IsActive` when an `inuse.*.lock` exists. The first event must be `session.start`, with this session's ID, producer `copilot-agent`, schema version 1, and a parseable `copilotVersion`; above 1.0.114 is `UnsupportedProducerVersion`, below 1.0.103 `AggregateOnly`, and reading stops at that line. `journal.link` is reported once.

### Code (copy exactly)

New file `src/CopilotLedger.Core/Import/CopilotSessionIds.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace CopilotLedger.Core.Import;

public static class CopilotSessionIds
{
    // A Copilot session directory name: an ASCII letter or digit, then up to 127 ASCII letters, digits, '.', '_', or '-'.
    public static bool IsValid([NotNullWhen(true)] string? sessionId) =>
        sessionId is { Length: >= 1 and <= 128 } &&
        char.IsAsciiLetterOrDigit(sessionId[0]) &&
        sessionId.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
}
```

New file `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// The session logs that Copilot CLI and the GitHub Copilot app write, <home>/session-state/<session ID>/events.jsonl, as
// Copilot 1.0.103 to 1.0.114 write them. Only the allowlisted metadata of known events is read.
public sealed class CopilotSessionLogSource : ISessionLogSource
{
    public static readonly ProducerVersion MinimumVersion = new(1, 0, 103);
    public static readonly ProducerVersion MaximumVersion = new(1, 0, 114);

    // The events whose allowlisted metadata is read. Every other type is skipped after its "type".
    private static readonly string[] Types = ["session.start", "journal.link"];

    private static readonly ImportEventKind[] Kinds = [ImportEventKind.SessionStart, ImportEventKind.JournalLink];

    private static readonly PathTree StartPaths = new(
        ("data.sessionId", 128),
        ("data.producer", 64),
        ("data.version", 0),
        ("data.copilotVersion", ProducerVersion.MaxLength),
        ("data.context.cwd", 4096),
        ("data.context.gitRoot", 4096),
        ("data.context.branch", 256),
        ("data.context.headCommit", 64),
        ("data.context.repository", 512));

    public ImportSourceKind Kind => ImportSourceKind.Copilot;

    // Each <home>/session-state/<session ID>/events.jsonl whose directory name is a valid session ID.
    public IReadOnlyList<SessionLogLocation> Discover(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        List<SessionLogLocation> logs = [];
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(Path.Combine(home, "session-state")))
            {
                string sessionId = Path.GetFileName(directory);
                if (!CopilotSessionIds.IsValid(sessionId))
                {
                    continue;
                }

                try
                {
                    FileInfo events = new(Path.Combine(directory, "events.jsonl"));
                    if (!events.Exists)
                    {
                        continue;
                    }

                    logs.Add(new SessionLogLocation
                    {
                        Source = ImportSourceKind.Copilot,
                        LogId = sessionId,
                        Path = events.FullName,
                        SizeBytes = events.Length,
                        LastWriteTime = new DateTimeOffset(events.LastWriteTimeUtc, TimeSpan.Zero),
                        IsActive = Directory.EnumerateFiles(directory, "inuse.*.lock").Any(),
                    });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return [.. logs.OrderBy(log => log.LogId, StringComparer.Ordinal)];
    }

    public Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (location.Source != ImportSourceKind.Copilot)
        {
            throw new ArgumentException("The location isn't a Copilot session log.", nameof(location));
        }

        return Task.Run(() => Read(location, cancellationToken), cancellationToken);
    }

    private static SessionLogReadResult Read(SessionLogLocation location, CancellationToken cancellationToken)
    {
        Session session = new(location);
        try
        {
            using FileStream stream = new(
                location.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                0,
                FileOptions.SequentialScan);
            JsonlLineReader.Read(
                stream,
                location.SizeBytes,
                ImportLimits.MaxLineBytes,
                session.OnLine,
                session.OnOversized,
                session.OnUnterminated,
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SessionLogs.NotRead(location, SessionLogStatus.Unreadable, ImportEventKind.Unknown, null, 0, 0);
        }

        return session.Finish();
    }

    // One log's reading state.
    private sealed class Session(SessionLogLocation location)
    {
        private readonly ImportDiagnosticBag _diagnostics = new(ImportSourceKind.Copilot, location.LogId);
        private readonly List<ImportedUsageRecord> _records = [];
        private readonly List<ImportedToolCall> _toolCalls = [];
        private (SessionLogStatus Status, ImportEventKind Kind, string? Version, long Line, long Offset)? _stop;
        private bool _started;
        private bool _journal;
        private string? _version;
        private long _malformed;
        private long _skipped;

        internal bool OnLine(ReadOnlySpan<byte> line, long number, long offset)
        {
            int? type = JsonFields.ReadType(line, Types);
            if (type is null)
            {
                Malformed(ImportDiagnosticCode.MalformedLine, ImportEventKind.Unknown, null, number, offset);
                return true;
            }

            ImportEventKind kind = type < 0 ? ImportEventKind.Unknown : Kinds[type.Value];
            if (!_started)
            {
                return kind == ImportEventKind.SessionStart
                    ? Start(JsonFields.Read(line, StartPaths), number, offset)
                    : Stop(SessionLogStatus.MissingSessionStart, kind, null, number, offset);
            }

            switch (kind)
            {
                case ImportEventKind.JournalLink:
                    if (!_journal)
                    {
                        _journal = true;
                        _diagnostics.Add(ImportDiagnosticCode.JournalPredecessor, kind, null, number, offset);
                    }

                    break;
                default:
                    _skipped++;
                    break;
            }

            return true;
        }

        internal void OnOversized(long number, long offset) =>
            Malformed(ImportDiagnosticCode.OversizedLine, ImportEventKind.Unknown, null, number, offset);

        internal void OnUnterminated(long number, long offset) =>
            Malformed(ImportDiagnosticCode.UnterminatedLine, ImportEventKind.Unknown, null, number, offset);

        internal SessionLogReadResult Finish()
        {
            if (_stop is { } stop)
            {
                return SessionLogs.NotRead(location, stop.Status, stop.Kind, stop.Version, stop.Line, stop.Offset);
            }

            if (!_started)
            {
                return SessionLogs.NotRead(
                    location,
                    SessionLogStatus.MissingSessionStart,
                    ImportEventKind.Unknown,
                    null,
                    0,
                    0);
            }

            return new SessionLogReadResult
            {
                Status = SessionLogStatus.Read,
                ProducerVersion = _version,
                Records = _records,
                ToolCalls = _toolCalls,
                Diagnostics = _diagnostics.ToList(),
                RecordsParsed = 0,
                UnsupportedUsageShapes = 0,
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
            };
        }

        // The gate: this session's own session.start, from copilot-agent schema version 1 and Copilot 1.0.103 to 1.0.114.
        // Reading stops at a session.start that fails it, so an older session's later lines are never read.
        private bool Start(JsonFields fields, long number, long offset)
        {
            if (fields.Duplicate is not null)
            {
                Malformed(ImportDiagnosticCode.MalformedLine, ImportEventKind.SessionStart, fields.Duplicate, number, offset);
                return true;
            }

            string? sessionId = fields.String("data.sessionId");
            if (!CopilotSessionIds.IsValid(sessionId) || !string.Equals(sessionId, location.LogId, StringComparison.Ordinal))
            {
                return Stop(SessionLogStatus.SessionIdMismatch, ImportEventKind.SessionStart, null, number, offset);
            }

            if (fields.String("data.producer") != "copilot-agent" || fields.Integer("data.version") != 1)
            {
                return Stop(SessionLogStatus.UnsupportedProducer, ImportEventKind.SessionStart, null, number, offset);
            }

            string? text = fields.String("data.copilotVersion");
            if (!ProducerVersion.TryParse(text, out ProducerVersion? version))
            {
                return Stop(SessionLogStatus.UnsupportedProducerVersion, ImportEventKind.SessionStart, null, number, offset);
            }

            if (version.CompareTo(MaximumVersion) > 0)
            {
                return Stop(SessionLogStatus.UnsupportedProducerVersion, ImportEventKind.SessionStart, text, number, offset);
            }

            if (version.CompareTo(MinimumVersion) < 0)
            {
                return Stop(SessionLogStatus.AggregateOnly, ImportEventKind.SessionStart, text, number, offset);
            }

            _started = true;
            _version = text;
            return true;
        }

        private bool Stop(SessionLogStatus status, ImportEventKind kind, string? version, long number, long offset)
        {
            _stop = (status, kind, version, number, offset);
            return false;
        }

        private void Malformed(ImportDiagnosticCode code, ImportEventKind kind, string? field, long number, long offset)
        {
            _malformed++;
            _diagnostics.Add(code, kind, field, number, offset);
        }
    }
}
```

New file `src/CopilotLedger.Core/Import/ISessionLogSource.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// One local session store's logs: where they are, and what each one recorded, as metadata only.
public interface ISessionLogSource
{
    ImportSourceKind Kind { get; }

    // The logs under home, ordered by LogId (ordinal). A missing or unreadable home gives none.
    IReadOnlyList<SessionLogLocation> Discover(string home);

    // Reads at most location.SizeBytes bytes of the log. Throws only ArgumentException (a location of another source) and
    // OperationCanceledException; a log that can't be read has Status Unreadable.
    Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken);
}
```

New file `src/CopilotLedger.Core/Import/ImportDiagnostic.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// A content-free problem in a session log, counted per log, code, event, and field. It never holds a byte, key, value, event
// type, or exception message of the log: Field is one of the parser's own path constants, and Version passed
// ProducerVersion.TryParse.
public sealed record ImportDiagnostic
{
    public required ImportSourceKind Source { get; init; }
    public required string LogId { get; init; }
    public required ImportDiagnosticCode Code { get; init; }
    public ImportEventKind Event { get; init; }
    public string? Field { get; init; }               // such as "data.usage.inputTokens"
    public string? Version { get; init; }             // the producer version the diagnostic is about
    public required long Count { get; init; }
    public long FirstLine { get; init; }              // 1-based; 0 for a diagnostic about the whole log
    public long FirstByteOffset { get; init; }        // of that line's first byte
}
```

New file `src/CopilotLedger.Core/Import/ImportDiagnosticBag.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// A log's diagnostics, counted per code, event, and field, each with the line and byte offset where it first occurred.
internal sealed class ImportDiagnosticBag(ImportSourceKind source, string logId)
{
    private readonly Dictionary<(ImportDiagnosticCode Code, ImportEventKind Event, string? Field), ImportDiagnostic> _items =
        [];

    internal void Add(
        ImportDiagnosticCode code,
        ImportEventKind kind,
        string? field,
        long line,
        long byteOffset,
        string? version = null)
    {
        (ImportDiagnosticCode, ImportEventKind, string?) key = (code, kind, field);
        _items[key] = _items.TryGetValue(key, out ImportDiagnostic? item)
            ? item with { Count = item.Count + 1 }
            : new ImportDiagnostic
            {
                Source = source,
                LogId = logId,
                Code = code,
                Event = kind,
                Field = field,
                Version = version,
                Count = 1,
                FirstLine = line,
                FirstByteOffset = byteOffset,
            };
    }

    // In order of first occurrence, then code.
    internal IReadOnlyList<ImportDiagnostic> ToList() =>
        [.. _items.Values.OrderBy(item => item.FirstLine).ThenBy(item => item.Code)];
}
```

New file `src/CopilotLedger.Core/Import/ImportDiagnosticCode.cs`:

```csharp
namespace CopilotLedger.Core.Import;

public enum ImportDiagnosticCode
{
    MalformedLine,                // not one JSON object with one string "type"
    OversizedLine,                // longer than ImportLimits.MaxLineBytes; discarded unread
    UnterminatedLine,             // a last line without "\n", such as one being written
    UnsupportedUsageShape,        // a usage record that fails validation; Field names the first failing field
    MissingSessionStart,
    SessionIdMismatch,
    UnsupportedProducer,
    UnsupportedProducerVersion,
    AggregateOnlySession,
    JournalPredecessor,           // a journal.link: earlier journal files aren't read
    CompressedLog,
    UnreadableLog,
    UnknownSubagent,              // informational: a sub-agent request recorded without an agent name
    InheritedRecord,              // informational: a Codex record of another thread, skipped
    AmbiguousOverlap,             // set by the importer
    CostOutOfRange,               // set by the importer
}
```

New file `src/CopilotLedger.Core/Import/ImportEventKind.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// A session-log event type the parsers know. Every other type, including one that holds content, is Unknown to diagnostics.
public enum ImportEventKind
{
    Unknown,
    SessionStart,
    SessionResume,
    UsageRecord,
    CompactionComplete,
    SubagentStarted,
    ToolExecutionStart,
    ToolExecutionComplete,
    JournalLink,
    SessionMeta,
    TurnContext,
    TokenUsageRecord,
}
```

New file `src/CopilotLedger.Core/Import/ImportLimits.cs`:

```csharp
namespace CopilotLedger.Core.Import;

public static class ImportLimits
{
    public const int MaxLineBytes = 8 * 1024 * 1024;      // a longer line is discarded unread
    public const long MaxTokenCount = 1_000_000_000;      // as GenAiSpanNormalizer.MaxTokenCount
    public const long MaxDurationMilliseconds = 86_400_000;
}
```

New file `src/CopilotLedger.Core/Import/ImportedToolCall.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// A tool call whose start and completion a session log recorded. ToolCallId is never stored; it becomes a hash.
public sealed record ImportedToolCall
{
    public required string SessionId { get; init; }
    public required string OwnerRecordKey { get; init; }     // the usage record whose response asked for the tool
    public required string ToolCallId { get; init; }
    public required string ToolName { get; init; }
    public string? AgentName { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required bool Success { get; init; }
    public long? ExitCode { get; init; }                     // shellExecution.exitCode
}
```

New file `src/CopilotLedger.Core/Import/ImportedUsageRecord.cs`:

```csharp
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;

// One model call from a session log. It deliberately has no free-text field.
public sealed record ImportedUsageRecord
{
    public required ImportSourceKind Source { get; init; }
    public required ImportedRecordKind Kind { get; init; }
    public required string SessionId { get; init; }          // the conversation: the Copilot session ID; Codex payload.session_id
    public required string RecordKey { get; init; }          // Copilot usageId, or a compaction's service request ID; Codex
                                                             // response_id. Never stored.
    public string? TurnKey { get; init; }                    // the user prompt whose trace the call joins; null: its own trace
    public string? ServiceRequestId { get; init; }           // normalized; GitHub-billed Copilot records only. Never stored.
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required string RequestModel { get; init; }       // as logged
    public string? ResponseModel { get; init; }              // as logged; null for compactions and Codex
    public string? ProviderName { get; init; }               // "github" for GitHub-billed Copilot calls; else the provider
    public required BillingSource BillingSource { get; init; }
    public string? AgentName { get; init; }                  // null for the main agent
    public required long InputTokens { get; init; }          // includes cache reads and writes
    public required long OutputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required CacheCounterState CacheReadState { get; init; }    // Reported or Absent; an Absent counter is 0
    public required CacheCounterState CacheWriteState { get; init; }
    public long? ToolCallCount { get; init; }                // the log's own count; never expanded into calls
    public ImportedGitContext? Git { get; init; }
    public required long LineNumber { get; init; }           // 1-based; for diagnostics only
}
```

New file `src/CopilotLedger.Core/Import/JsonFields.cs`:

```csharp
using System.Text.Json;

namespace CopilotLedger.Core.Import;

// Deny-by-default reading of one JSON Lines event with Utf8JsonReader. ReadType finds the top-level "type" without reading
// any other value; Read then takes only the allowlisted paths of a known type, skipping everything else unread. Exception
// messages are never kept, because they can quote the line.
internal sealed class JsonFields
{
    private static readonly JsonReaderOptions Options = new() { MaxDepth = 256 };

    private readonly Dictionary<string, Value> _values = new(StringComparer.Ordinal);

    private JsonFields()
    {
    }

    // The path of the first allowlisted property that appeared twice in one object, or null.
    internal string? Duplicate { get; private set; }

    // The index in types of the line's string "type", -1 for a type that isn't in types, or null when the line isn't one JSON
    // object with exactly one string "type" (a malformed line). Reads no other value.
    internal static int? ReadType(ReadOnlySpan<byte> line, IReadOnlyList<string> types)
    {
        try
        {
            Utf8JsonReader reader = new(line, Options);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            int? found = null;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isType = reader.ValueTextEquals("type"u8);
                reader.Read();
                if (!isType)
                {
                    reader.Skip();
                    continue;
                }

                if (found is not null || reader.TokenType != JsonTokenType.String)
                {
                    return null;
                }

                found = -1;
                for (int index = 0; index < types.Count; index++)
                {
                    if (reader.ValueTextEquals(types[index]))
                    {
                        found = index;
                        break;
                    }
                }
            }

            // Content after the object makes the next Read throw.
            return reader.Read() ? null : found;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The allowlisted values of a line that ReadType accepted.
    internal static JsonFields Read(ReadOnlySpan<byte> line, PathTree paths)
    {
        JsonFields fields = new();
        try
        {
            Utf8JsonReader reader = new(line, Options);
            reader.Read();
            ReadObject(ref reader, paths.Root, fields);
        }
        catch (JsonException)
        {
            fields.Duplicate ??= "";
        }

        return fields;
    }

    internal bool IsPresent(string path) =>
        _values.TryGetValue(path, out Value value) && value.Token != JsonTokenType.Null;

    internal bool IsObject(string path) =>
        _values.TryGetValue(path, out Value value) && value.Token == JsonTokenType.StartObject;

    // A string of 1 to the path's maximum length without control characters, or null.
    internal string? String(string path) =>
        _values.TryGetValue(path, out Value value) && value.Token == JsonTokenType.String ? value.Text : null;

    // An integer that fits in a long, or null (1.0, 1e3, and strings are null).
    internal long? Integer(string path) =>
        _values.TryGetValue(path, out Value value) && value.Token == JsonTokenType.Number ? value.Integer : null;

    internal bool? Boolean(string path) =>
        _values.TryGetValue(path, out Value value)
            ? value.Token switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                _ => null,
            }
            : null;

    private static void ReadObject(ref Utf8JsonReader reader, PathTree.Node node, JsonFields fields)
    {
        List<PathTree.Node>? seen = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            PathTree.Node? child = null;
            foreach (PathTree.Node candidate in node.Children)
            {
                if (reader.ValueTextEquals(candidate.Name))
                {
                    child = candidate;
                    break;
                }
            }

            reader.Read();
            if (child is null)
            {
                reader.Skip();
                continue;
            }

            seen ??= [];
            if (seen.Contains(child))
            {
                fields.Duplicate ??= child.Path;
                reader.Skip();
                continue;
            }

            seen.Add(child);
            if (child.Children.Count > 0 && reader.TokenType == JsonTokenType.StartObject)
            {
                fields._values[child.Path] = new Value(JsonTokenType.StartObject, null, null);
                ReadObject(ref reader, child, fields);
            }
            else
            {
                fields._values[child.Path] = ReadValue(ref reader, child.MaxLength);
            }
        }
    }

    private static Value ReadValue(ref Utf8JsonReader reader, int maxLength)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                if (maxLength == 0 || reader.ValueSpan.Length > 6 * maxLength)
                {
                    return new Value(JsonTokenType.String, null, null);
                }

                try
                {
                    string text = reader.GetString()!;
                    return new Value(
                        JsonTokenType.String,
                        text.Length >= 1 && text.Length <= maxLength && !text.Any(char.IsControl) ? text : null,
                        null);
                }
                catch (InvalidOperationException)
                {
                    return new Value(JsonTokenType.String, null, null);
                }

            case JsonTokenType.Number:
                return new Value(JsonTokenType.Number, null, reader.TryGetInt64(out long number) ? number : null);
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                JsonTokenType token = reader.TokenType;
                reader.Skip();
                return new Value(token, null, null);
            default:
                return new Value(reader.TokenType, null, null);
        }
    }

    private readonly record struct Value(JsonTokenType Token, string? Text, long? Integer);
}

// The allowlisted property paths of one event type ("data.usage.inputTokens"), each with the longest string it may hold
// (0 for numbers and booleans).
internal sealed class PathTree
{
    internal PathTree(params (string Path, int MaxLength)[] leaves)
    {
        foreach ((string path, int maxLength) in leaves)
        {
            Node node = Root;
            string[] names = path.Split('.');
            for (int index = 0; index < names.Length; index++)
            {
                Node? child = node.Children.Find(item => item.Name == names[index]);
                if (child is null)
                {
                    child = new Node(names[index], string.Join('.', names[..(index + 1)]));
                    node.Children.Add(child);
                }

                if (index == names.Length - 1)
                {
                    child.MaxLength = maxLength;
                }

                node = child;
            }
        }
    }

    internal Node Root { get; } = new("", "");

    internal sealed class Node(string name, string path)
    {
        internal string Name { get; } = name;
        internal string Path { get; } = path;
        internal int MaxLength { get; set; }
        internal List<Node> Children { get; } = [];
    }
}
```

New file `src/CopilotLedger.Core/Import/JsonlLineReader.cs`:

```csharp
using System.Buffers;

namespace CopilotLedger.Core.Import;

// Splits the first maxBytes bytes of a JSON Lines stream into lines, reading 64 KiB at a time, and hands each complete line
// to a callback. A line longer than maxLineBytes is skipped without being kept, and a last line without "\n" is reported but
// never handed over. Blank lines are skipped silently. Line numbers are 1-based and count every "\n".
internal static class JsonlLineReader
{
    private const int ChunkBytes = 64 * 1024;

    // line is valid only during the call. Returns false to stop reading.
    internal delegate bool LineHandler(ReadOnlySpan<byte> line, long lineNumber, long byteOffset);

    internal static void Read(
        Stream stream,
        long maxBytes,
        int maxLineBytes,
        LineHandler onLine,
        Action<long, long> onOversized,
        Action<long, long> onUnterminated,
        CancellationToken cancellationToken)
    {
        byte[] chunk = ArrayPool<byte>.Shared.Rent(ChunkBytes);
        byte[] line = ArrayPool<byte>.Shared.Rent(ChunkBytes);
        int lineLength = 0;
        bool oversized = false;
        long lineNumber = 1;
        long lineOffset = 0;
        long position = 0;
        try
        {
            while (position < maxBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = stream.Read(chunk, 0, (int)Math.Min(ChunkBytes, maxBytes - position));
                if (read == 0)
                {
                    break;
                }

                int start = 0;
                while (start < read)
                {
                    int newline = Array.IndexOf(chunk, (byte)'\n', start, read - start);
                    int end = newline < 0 ? read : newline;
                    if (!oversized)
                    {
                        int count = end - start;
                        if (lineLength + count > maxLineBytes)
                        {
                            oversized = true;
                            lineLength = 0;
                        }
                        else
                        {
                            if (lineLength + count > line.Length)
                            {
                                byte[] larger = ArrayPool<byte>.Shared.Rent(
                                    Math.Min(maxLineBytes, Math.Max(line.Length * 2, lineLength + count)));
                                Buffer.BlockCopy(line, 0, larger, 0, lineLength);
                                ArrayPool<byte>.Shared.Return(line);
                                line = larger;
                            }

                            Buffer.BlockCopy(chunk, start, line, lineLength, count);
                            lineLength += count;
                        }
                    }

                    if (newline < 0)
                    {
                        break;
                    }

                    if (oversized)
                    {
                        onOversized(lineNumber, lineOffset);
                    }
                    else
                    {
                        ReadOnlySpan<byte> complete = line.AsSpan(0, lineLength);
                        if (complete.Length > 0 && complete[^1] == (byte)'\r')
                        {
                            complete = complete[..^1];
                        }

                        if (!IsBlank(complete) && !onLine(complete, lineNumber, lineOffset))
                        {
                            return;
                        }
                    }

                    lineNumber++;
                    lineOffset = position + newline + 1;
                    lineLength = 0;
                    oversized = false;
                    start = newline + 1;
                }

                position += read;
            }

            if (oversized)
            {
                onOversized(lineNumber, lineOffset);
            }
            else if (!IsBlank(line.AsSpan(0, lineLength)))
            {
                onUnterminated(lineNumber, lineOffset);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
            ArrayPool<byte>.Shared.Return(line);
        }
    }

    private static bool IsBlank(ReadOnlySpan<byte> line) =>
        !line.ContainsAnyExcept((byte)' ', (byte)'\t', (byte)'\r');
}
```

New file `src/CopilotLedger.Core/Import/ProducerVersion.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace CopilotLedger.Core.Import;

// A producer's MAJOR.MINOR.PATCH version. A prerelease or build suffix is accepted and ignored when comparing, so
// 1.0.106-8.unstable.r43566476370.g869eef3 is 1.0.106.
public sealed record ProducerVersion(int Major, int Minor, int Patch) : IComparable<ProducerVersion>
{
    public const int MaxLength = 64;

    // MAJOR.MINOR.PATCH, each "0" or 1 to 9 digits without a leading zero, then optionally "-" or "+" and at least one of
    // [0-9A-Za-z.+-]; at most 64 characters in all.
    public static bool TryParse(string? value, [NotNullWhen(true)] out ProducerVersion? version)
    {
        version = null;
        if (value is null || value.Length is 0 or > MaxLength)
        {
            return false;
        }

        int position = 0;
        int[] parts = new int[3];
        for (int part = 0; part < 3; part++)
        {
            int start = position;
            while (position < value.Length && char.IsAsciiDigit(value[position]))
            {
                position++;
            }

            int length = position - start;
            if (length is 0 or > 9 || (length > 1 && value[start] == '0'))
            {
                return false;
            }

            parts[part] = int.Parse(value.AsSpan(start, length), System.Globalization.CultureInfo.InvariantCulture);
            if (part < 2)
            {
                if (position >= value.Length || value[position] != '.')
                {
                    return false;
                }

                position++;
            }
        }

        if (position < value.Length)
        {
            if (value[position] is not ('-' or '+') ||
                position + 1 == value.Length ||
                value.AsSpan(position + 1).ContainsAnyExcept(SuffixCharacters))
            {
                return false;
            }
        }

        version = new ProducerVersion(parts[0], parts[1], parts[2]);
        return true;
    }

    public int CompareTo(ProducerVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        int minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    private static readonly System.Buffers.SearchValues<char> SuffixCharacters = System.Buffers.SearchValues.Create(
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.+-");
}
```

New file `src/CopilotLedger.Core/Import/SessionLogLocation.cs`:

```csharp
namespace CopilotLedger.Core.Import;

public sealed record SessionLogLocation
{
    public required ImportSourceKind Source { get; init; }
    public required string LogId { get; init; }              // validated: a Copilot session ID; a Codex rollout ID ("archived/" first)
    public required string Path { get; init; }               // never stored or printed
    public required long SizeBytes { get; init; }            // at discovery; reading stops there
    public required DateTimeOffset LastWriteTime { get; init; }
    public required bool IsActive { get; init; }             // Copilot: an inuse.*.lock file in the session directory
    public bool IsCompressed { get; init; }                  // Codex .jsonl.zst, which is never opened
}
```

New file `src/CopilotLedger.Core/Import/SessionLogReadResult.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// What one session log recorded, as metadata only.
public sealed record SessionLogReadResult
{
    public required SessionLogStatus Status { get; init; }
    public string? ProducerVersion { get; init; }                           // valid (ProducerVersion.TryParse), or null
    public required IReadOnlyList<ImportedUsageRecord> Records { get; init; }
    public required IReadOnlyList<ImportedToolCall> ToolCalls { get; init; }
    public required IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; }
    public required long RecordsParsed { get; init; }             // usage lines of a Read log, valid or not
    public required long UnsupportedUsageShapes { get; init; }
    public required long MalformedLines { get; init; }            // invalid JSON, oversized, or an unterminated last line
    public required long SkippedEvents { get; init; }             // well-formed lines of any other type, known or not
}
```

New file `src/CopilotLedger.Core/Import/SessionLogStatus.cs`:

```csharp
namespace CopilotLedger.Core.Import;

public enum SessionLogStatus
{
    Read,                          // its records follow
    MissingSessionStart,           // Copilot: no session.start first; Codex: no session_meta first
    SessionIdMismatch,             // Copilot: session.start names another session
    UnsupportedProducer,           // another Copilot producer or schema version; a Codex session_meta without its IDs
    UnsupportedProducerVersion,    // a version outside the supported ones, or none that can be read
    AggregateOnly,                 // Copilot before 1.0.103, which logs only session totals; nothing after session.start is read
    Compressed,                    // Codex .jsonl.zst
    Unreadable,                    // the file couldn't be opened or read
}
```

New file `src/CopilotLedger.Core/Import/SessionLogs.cs`:

```csharp
using CopilotLedger.Core.Git;

namespace CopilotLedger.Core.Import;

// What the session-log sources share.
internal static class SessionLogs
{
    // A log that isn't Read: its status diagnostic only, without records, tool calls, or counts.
    internal static SessionLogReadResult NotRead(
        SessionLogLocation location,
        SessionLogStatus status,
        ImportEventKind kind,
        string? version,
        long line,
        long byteOffset) =>
        new()
        {
            Status = status,
            ProducerVersion = version,
            Records = [],
            ToolCalls = [],
            Diagnostics =
            [
                new ImportDiagnostic
                {
                    Source = location.Source,
                    LogId = location.LogId,
                    Code = status switch
                    {
                        SessionLogStatus.MissingSessionStart => ImportDiagnosticCode.MissingSessionStart,
                        SessionLogStatus.SessionIdMismatch => ImportDiagnosticCode.SessionIdMismatch,
                        SessionLogStatus.UnsupportedProducer => ImportDiagnosticCode.UnsupportedProducer,
                        SessionLogStatus.UnsupportedProducerVersion => ImportDiagnosticCode.UnsupportedProducerVersion,
                        SessionLogStatus.AggregateOnly => ImportDiagnosticCode.AggregateOnlySession,
                        SessionLogStatus.Compressed => ImportDiagnosticCode.CompressedLog,
                        SessionLogStatus.Unreadable => ImportDiagnosticCode.UnreadableLog,
                        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
                    },
                    Event = kind,
                    Version = version,
                    Count = 1,
                    FirstLine = line,
                    FirstByteOffset = byteOffset,
                },
            ],
            RecordsParsed = 0,
            UnsupportedUsageShapes = 0,
            MalformedLines = 0,
            SkippedEvents = 0,
        };

    // An "owner/repo" slug from a recorded repository: a remote URL (credentials removed first) or an owner/repo slug; else null.
    internal static string? RepositorySlug(string? repository)
    {
        if (repository is null)
        {
            return null;
        }

        if (repository.Contains("://", StringComparison.Ordinal) || repository.Contains('@', StringComparison.Ordinal))
        {
            return GitRemoteUrl.GetRepositorySlug(GitRemoteUrl.Sanitize(repository));
        }

        string[] parts = repository.Split('/');
        return parts.Length == 2 && parts.All(part => part.Length is >= 1 and <= 100 &&
                                                      part.All(character => char.IsAsciiLetterOrDigit(character) ||
                                                                            character is '.' or '_' or '-'))
            ? repository
            : null;
    }

    // A commit SHA of 40 or 64 hex characters, lowercased; else null.
    internal static string? CommitSha(string? value) =>
        value is { Length: 40 or 64 } && value.All(char.IsAsciiHexDigit) ? value.ToLowerInvariant() : null;

    // A fully qualified path without trailing separators; else null.
    internal static string? FullPath(string? value) =>
        value is not null && Path.IsPathFullyQualified(value) ? Path.TrimEndingDirectorySeparator(value) : null;
}
```

Edit 1 of 3 in `src/CopilotLedger.Otlp/Attribution/CopilotSessionWorkspaceLocator.cs`, replace:

```csharp
using System.Text.RegularExpressions;

namespace CopilotLedger.Otlp.Attribution;
```

with:

```csharp
using CopilotLedger.Core.Import;

namespace CopilotLedger.Otlp.Attribution;
```

Edit 2 of 3 in `src/CopilotLedger.Otlp/Attribution/CopilotSessionWorkspaceLocator.cs`, replace:

```csharp
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _timeToLive;
    private static readonly Regex ConversationIdRegex = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant);

    // COPILOT_HOME when it's set and not blank; otherwise Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot").
```

with:

```csharp
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _timeToLive;

    // COPILOT_HOME when it's set and not blank; otherwise Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot").
```

Edit 3 of 3 in `src/CopilotLedger.Otlp/Attribution/CopilotSessionWorkspaceLocator.cs`, replace:

```csharp
    public string? GetWorkingDirectory(string? conversationId)
    {
        if (conversationId is null || !ConversationIdRegex.IsMatch(conversationId))
        {
            return null;
```

with:

```csharp
    public string? GetWorkingDirectory(string? conversationId)
    {
        if (!CopilotSessionIds.IsValid(conversationId))
        {
            return null;
```

New file `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs`:

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class CopilotSessionGateTests
{
    private const string SessionId = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private static readonly CopilotSessionLogSource Source = new();

    public static TheoryData<string, int, string, SessionLogStatus, string?> Gates => new()
    {
        { "copilot-agent", 1, "1.0.103", SessionLogStatus.Read, "1.0.103" },
        {
            "copilot-agent", 1, "1.0.106-8.unstable.r43566476370.g869eef3", SessionLogStatus.Read,
            "1.0.106-8.unstable.r43566476370.g869eef3"
        },
        { "copilot-agent", 1, "1.0.114", SessionLogStatus.Read, "1.0.114" },
        { "copilot-agent", 1, "1.0.115-0", SessionLogStatus.UnsupportedProducerVersion, "1.0.115-0" },
        { "copilot-agent", 1, "1.0.120", SessionLogStatus.UnsupportedProducerVersion, "1.0.120" },
        { "copilot-agent", 1, "1.0.102", SessionLogStatus.AggregateOnly, "1.0.102" },
        { "copilot-agent", 1, "1.0.98", SessionLogStatus.AggregateOnly, "1.0.98" },
        { "other-agent", 1, "1.0.110", SessionLogStatus.UnsupportedProducer, null },
        { "copilot-agent", 2, "1.0.110", SessionLogStatus.UnsupportedProducer, null },
    };

    [Fact]
    public async Task CS1_DiscoveryListsSessionDirectoriesInOrder()
    {
        using TempDirectory temp = new();
        string second = new CopilotSessionLogs("b-session").Write(temp.Path);
        string first = new CopilotSessionLogs("a-session").Write(temp.Path, active: true);
        string root = Path.Combine(temp.Path, "session-state");
        Directory.CreateDirectory(Path.Combine(root, ".hidden"));
        await File.WriteAllTextAsync(Path.Combine(root, ".hidden", "events.jsonl"), "{}\n");
        Directory.CreateDirectory(Path.Combine(root, "c-without-events"));
        await File.WriteAllTextAsync(Path.Combine(root, "stray.jsonl"), "{}\n");

        IReadOnlyList<SessionLogLocation> logs = Source.Discover(temp.Path);

        Assert.Equal(["a-session", "b-session"], logs.Select(log => log.LogId));
        Assert.Equal([true, false], logs.Select(log => log.IsActive));
        Assert.Equal([Path.GetFullPath(first), Path.GetFullPath(second)], logs.Select(log => log.Path));
        Assert.Equal(new FileInfo(first).Length, logs[0].SizeBytes);
        Assert.Equal(new DateTimeOffset(File.GetLastWriteTimeUtc(first), TimeSpan.Zero), logs[0].LastWriteTime);
        Assert.All(logs, log => Assert.Equal(ImportSourceKind.Copilot, log.Source));
        Assert.Empty(Source.Discover(Path.Combine(temp.Path, "missing")));
    }

    [Theory]
    [MemberData(nameof(Gates))]
    public async Task CS2_TheGateReadsOnlyCopilot1_0_103To1_0_114(
        string producer,
        int schemaVersion,
        string version,
        SessionLogStatus status,
        string? reported)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, version, producer: producer, schemaVersion: schemaVersion)
            .Raw("{not json")
            .UserMessage()
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(status, result.Status);
        Assert.Equal(reported, result.ProducerVersion);
        if (status == SessionLogStatus.Read)
        {
            Assert.Equal(1, result.MalformedLines);
            Assert.Equal(1, result.SkippedEvents);
        }
        else
        {
            ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(
                status switch
                {
                    SessionLogStatus.AggregateOnly => ImportDiagnosticCode.AggregateOnlySession,
                    SessionLogStatus.UnsupportedProducer => ImportDiagnosticCode.UnsupportedProducer,
                    _ => ImportDiagnosticCode.UnsupportedProducerVersion,
                },
                diagnostic.Code);
            Assert.Equal(reported, diagnostic.Version);
            Assert.Equal(ImportEventKind.SessionStart, diagnostic.Event);
            Assert.Equal(1, diagnostic.FirstLine);
            Assert.Equal(0, result.MalformedLines);
            Assert.Equal(0, result.SkippedEvents);
        }
    }

    [Theory]
    [InlineData(false, SessionLogStatus.MissingSessionStart)]
    [InlineData(true, SessionLogStatus.SessionIdMismatch)]
    public async Task CS3_TheFirstEventMustBeThisSessionsStart(bool otherSession, SessionLogStatus status)
    {
        using TempDirectory temp = new();
        string text = otherSession
            ? new CopilotSessionLogs("another-session").Text()
            : "{\"type\":\"user.message\",\"data\":{\"content\":\"" + CopilotSessionLogs.Canary + "\"}}\n"
              + new CopilotSessionLogs(SessionId).Text();
        await WriteAsync(temp.Path, text);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(status, result.Status);
        Assert.Equal(1, Assert.Single(result.Diagnostics).FirstLine);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS4_MalformedLinesAreCountedWithTheirPlaceOnly()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId)
            .Raw("{\"type\":\"user.message\",\"data\":\"" + CopilotSessionLogs.Canary)
            .Raw("[\"" + CopilotSessionLogs.Canary + "\"]")
            .Raw("{\"data\":\"" + CopilotSessionLogs.Canary + "\"}");
        log.Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(3, result.MalformedLines);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.MalformedLine, diagnostic.Code);
        Assert.Equal(ImportEventKind.Unknown, diagnostic.Event);
        Assert.Null(diagnostic.Field);
        Assert.Equal(3, diagnostic.Count);
        Assert.Equal(2, diagnostic.FirstLine);
        Assert.Equal(Encoding.UTF8.GetByteCount(log.Lines[0]) + 1, diagnostic.FirstByteOffset);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS5_ALineLongerThan8MiBIsDiscardedUnread()
    {
        using TempDirectory temp = new();
        string prefix = "{\"type\":\"user.message\",\"data\":{\"content\":\"" + CopilotSessionLogs.Canary;
        string suffix = "\"}}";
        string oversized = prefix + new string('x', ImportLimits.MaxLineBytes + 1 - prefix.Length - suffix.Length) + suffix;
        new CopilotSessionLogs(SessionId).Raw(oversized).UserMessage().Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(ImportLimits.MaxLineBytes + 1, Encoding.UTF8.GetByteCount(oversized));
        Assert.Equal(1, result.MalformedLines);
        Assert.Equal(1, result.SkippedEvents);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.OversizedLine, diagnostic.Code);
        Assert.Equal(2, diagnostic.FirstLine);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS6_AnUnterminatedLastLineIsNeverParsed()
    {
        using TempDirectory temp = new();
        await WriteAsync(temp.Path, new CopilotSessionLogs(SessionId).Text() + "{\"type\":\"journal.link\"}");

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(1, result.MalformedLines);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.UnterminatedLine, diagnostic.Code);
        Assert.Equal(2, diagnostic.FirstLine);
    }

    [Fact]
    public async Task CS7_TheTypeIsFoundAfterContent()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Raw("{\"data\":{\"content\":\"" + CopilotSessionLogs.Canary + "\"},\"type\":\"user.message\"}")
            .Raw("{\"data\":{\"items\":[{\"x\":\"" + CopilotSessionLogs.Canary + "\"}]},\"t\\u0079pe\":\"journal.link\"}")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(0, result.MalformedLines);
        Assert.Equal(1, result.SkippedEvents);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.JournalPredecessor, diagnostic.Code);
        Assert.Equal(3, diagnostic.FirstLine);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS8_UnknownTypesAreCountedAndNeverEchoed()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Event(CopilotSessionLogs.Canary, new JsonObject { ["x"] = 1 })
            .Event("assistant.message", new JsonObject { ["content"] = CopilotSessionLogs.Canary })
            .Event("session.start", new JsonObject { ["sessionId"] = SessionId })
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(3, result.SkippedEvents);
        Assert.Empty(result.Diagnostics);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS9_ATypeGivenTwiceIsMalformed()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId).Raw("{\"type\":\"user.message\",\"type\":\"journal.link\"}").Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(1, result.MalformedLines);
        Assert.Equal(ImportDiagnosticCode.MalformedLine, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task CS10_ContentAfterTheObjectIsMalformed()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId).Raw("{\"type\":\"journal.link\"} {\"type\":\"journal.link\"}").Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(1, result.MalformedLines);
        Assert.Equal(ImportDiagnosticCode.MalformedLine, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task CS11_AJournalLinkIsReportedOnce()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Event("journal.link", new JsonObject { ["previous"] = CopilotSessionLogs.Canary })
            .Event("journal.link", new JsonObject())
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.JournalPredecessor, diagnostic.Code);
        Assert.Equal(ImportEventKind.JournalLink, diagnostic.Event);
        Assert.Equal(1, diagnostic.Count);
        Assert.Equal(2, diagnostic.FirstLine);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS12_BytesAppendedAfterDiscoveryAreLeftForTheNextRun()
    {
        using TempDirectory temp = new();
        string path = new CopilotSessionLogs(SessionId).Write(temp.Path);
        SessionLogLocation location = Assert.Single(Source.Discover(temp.Path));
        await File.AppendAllTextAsync(path, "{not json\n");

        SessionLogReadResult result = await Source.ReadAsync(location, CancellationToken.None);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(0, result.MalformedLines);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("1.0.103", 1, 0, 103)]
    [InlineData("1.0.106-8.unstable.r43566476370.g869eef3", 1, 0, 106)]
    [InlineData("0.171.0+build.5", 0, 171, 0)]
    public void CS13_ProducerVersionsParse(string value, int major, int minor, int patch)
    {
        Assert.True(ProducerVersion.TryParse(value, out ProducerVersion? version));
        Assert.Equal(new ProducerVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("01.0.103")]
    [InlineData("1.0.103-")]
    [InlineData("v1.0.103")]
    [InlineData("1.0.1234567890")]
    public void CS14_OtherVersionsDont(string value)
    {
        Assert.False(ProducerVersion.TryParse(value, out ProducerVersion? version));
        Assert.Null(version);
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home)), CancellationToken.None);

    private static async Task WriteAsync(string home, string text)
    {
        string directory = Path.Combine(home, "session-state", SessionId);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "events.jsonl"), text);
    }

    private static void AssertNoCanary(SessionLogReadResult result) =>
        Assert.DoesNotContain(CopilotSessionLogs.Canary, JsonSerializer.Serialize(result), StringComparison.Ordinal);
}
```

New file `tests/CopilotLedger.Tests/TestSupport/CopilotSessionLogs.cs`:

```csharp
using System.Globalization;
using System.Text.Json.Nodes;

namespace CopilotLedger.Tests.TestSupport;

// A synthetic Copilot session log (events.jsonl) with the event shapes Copilot 1.0.103 to 1.0.114 write, as the sanitized
// captures in the design show them, and made-up values. Content-bearing fields hold Canary, which must never reach the
// ledger, a diagnostic, or any output.
public sealed class CopilotSessionLogs
{
    public const string Canary = "CANARY-5e1f0c-never-store-me";

    private readonly List<string> _lines = [];
    private DateTimeOffset _time;
    private int _sequence;

    // Starts the log with session.start, at start (default 2026-11-20 12:00:00 UTC).
    public CopilotSessionLogs(
        string sessionId,
        string version = "1.0.110",
        DateTimeOffset? start = null,
        JsonObject? context = null,
        string producer = "copilot-agent",
        int schemaVersion = 1)
    {
        SessionId = sessionId;
        _time = start ?? new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        Event(
            "session.start",
            new JsonObject
            {
                ["sessionId"] = sessionId,
                ["version"] = schemaVersion,
                ["producer"] = producer,
                ["copilotVersion"] = version,
                ["startTime"] = Stamp(_time),
                ["selectedModel"] = "claude-haiku-4.5",
                ["context"] = context ?? new JsonObject { ["cwd"] = DefaultWorkingDirectory },
                ["alreadyInUse"] = false,
            });
    }

    public static string DefaultWorkingDirectory { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "work"));

    public string SessionId { get; }

    // The time of the last event; each event is one second after the one before.
    public DateTimeOffset Time => _time;

    public IReadOnlyList<string> Lines => _lines;

    // {"type": type, "data": data, "agentId": agentId (when given), "id": …, "timestamp": …, "parentId": …}, one second
    // after the event before, or at the given time.
    public CopilotSessionLogs Event(string type, JsonObject data, string? agentId = null, DateTimeOffset? at = null)
    {
        _time = at ?? (_lines.Count == 0 ? _time : _time.AddSeconds(1));
        JsonObject line = new() { ["type"] = type, ["data"] = data };
        if (agentId is not null)
        {
            line["agentId"] = agentId;
        }

        line["id"] = Guid(++_sequence);
        line["timestamp"] = Stamp(_time);
        line["parentId"] = _sequence == 1 ? null : Guid(_sequence - 1);
        _lines.Add(line.ToJsonString());
        return this;
    }

    // A line as given, such as a malformed one.
    public CopilotSessionLogs Raw(string line)
    {
        _lines.Add(line);
        return this;
    }

    // A user message: content that's never read.
    public CopilotSessionLogs UserMessage() =>
        Event("user.message", new JsonObject { ["content"] = Canary, ["transformedContent"] = Canary });

    // The log's bytes: each line followed by "\n".
    public string Text() => string.Concat(_lines.Select(line => line + "\n"));

    // Writes <copilotHome>/session-state/<SessionId>/events.jsonl, with an inuse.4242.lock file when active, and returns the
    // events.jsonl path.
    public string Write(string copilotHome, bool active = false)
    {
        string directory = Path.Combine(copilotHome, "session-state", SessionId);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "events.jsonl");
        File.WriteAllText(path, Text());
        if (active)
        {
            File.WriteAllText(Path.Combine(directory, "inuse.4242.lock"), "4242");
        }

        FileRetry.ReadAllText(path);
        return path;
    }

    public static string Stamp(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    // A deterministic UUID for envelope IDs, which the parser never reads.
    private static string Guid(int sequence) =>
        string.Create(CultureInfo.InvariantCulture, $"00000000-0000-4000-8000-{sequence:x12}");
}
```

### Tests

- **CS1**: discovery lists valid session folders with `events.jsonl` in order, sizes, times, and `IsActive`, and skips invalid names. **CS2** (9): the gate reads 1.0.103, `1.0.106-8.unstable.r43566476370.g869eef3`, and 1.0.114; `1.0.115-0` and 1.0.120 are unsupported versions; 1.0.102 and 1.0.98 are aggregate-only; another producer or schema version is unsupported. **CS3** (2): a first event other than this session's start stops reading.
- **CS4**: malformed lines are counted with their line and byte offset only. **CS5**: a line longer than 8 MiB is discarded unread. **CS6**: an unterminated last line is never parsed. **CS7**: `type` is found after content, and the content isn't read. **CS8**: unknown types are counted and never echoed. **CS9**: a `type` given twice is malformed. **CS10**: content after the object is malformed. **CS11**: a `journal.link` is reported once. **CS12**: bytes appended after discovery are left for the next run.
- **CS13** (3), **CS14** (5): `ProducerVersion` parses `1.0.103`, `1.0.106-8.unstable.r43566476370.g869eef3`, and `0.171.0+build.5`, and rejects `1.0`, `01.0.103`, `1.0.103-`, `v1.0.103`, and `1.0.1234567890`.

### Acceptance criteria

- Content-bearing events are skipped after `type`, and no diagnostic holds log content (CS4–CS10); the canary never appears in a result.
- Only Copilot 1.0.103 to 1.0.114 sessions are read; older ones stop at `session.start` (CS2).
- `dotnet test CopilotLedger.slnx` passes **1126** tests (1097 + 29).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 6 specification: Copilot Usage Records and Compactions

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs` | Usage records, compactions, sub-agents, resumes, prompt traces, and tool calls |
| `tests/CopilotLedger.Tests/TestSupport/CopilotSessionLogs.cs` | `Usage`, `Compaction`, `SubagentStarted`, `ToolStart`, `ToolComplete`, `Resume`, and `Context` |
| `tests/CopilotLedger.Tests/Import/CopilotRecordTests.cs` | New: CU1–CU12 |
| `tests/CopilotLedger.Tests/Import/CopilotCompactionTests.cs` | New: CC1–CC6 |

### Mapping

| Record field | `session.usage_record` | `session.compaction_complete` |
|---|---|---|
| Kind, `RecordKey` | `Usage`, `data.usage.accounting.usageId` | `Compaction`, the normalized `data.serviceRequestId` |
| `ServiceRequestId` | `data.usage.serviceRequestId` (not for BYOK) | `data.serviceRequestId` |
| Times | end = envelope `timestamp`; start = end − `data.usage.duration` | end = `timestamp`; start = end − `data.compactionTokensUsed.duration` |
| Models | `RequestModel` = `data.usage.model`, `ResponseModel` = `data.usage.responseModel`, as logged | `RequestModel` = `data.compactionTokensUsed.model`, no response model |
| Provider | `github`, `Endpoint`; BYOK: `providerType` lowercased, `Provider` | `github`, `Endpoint` |
| Tokens and cache states | The four counts; `Reported` when `cacheDetailsReported` is true or the count is above 0, else `Absent` | The four counts; both `Reported` |
| Agent | By envelope `agentId`, else by `parentToolCallId` through `subagent.started` | By envelope `agentId` |
| Turn | A main-agent record with `initiator` `user` starts a turn (its usage ID); others join the current one | The current turn |
| Git | `session.start`'s context, replaced by each `session.resume` that has one | The same |
| Tool calls | `ToolCallCount` = `numToolCalls`; each paired start and completion belongs to the latest record of its agent | None |

### Validation (the first failing field names the unsupported shape)

- Usage records: a valid timestamp and `data.usage` object; `usageId`; `model`; a string `responseModel` when present; four token counts from 0 to 1,000,000,000 with cache reads plus writes at most the input; `cacheDetailsReported` and `isByok` booleans; and, when not BYOK, `provider.kind` `copilot` and a valid service request ID.
- Compactions: without `compactionTokensUsed`, a skipped event. Otherwise `success` true, `requestCount` 1, a valid service request ID, a model, valid token counts, and `provider.kind` `copilot`. `summaryContent`, `checkpointPath`, `error`, `failureReason`, `failureDiagnostics`, `requestId`, and `clientRequestId` are never read.

### Code (copy exactly)

Edit 1 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
namespace CopilotLedger.Core.Import;

// The session logs that Copilot CLI and the GitHub Copilot app write, <home>/session-state/<session ID>/events.jsonl, as
```

with:

```csharp
using System.Globalization;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;

// The session logs that Copilot CLI and the GitHub Copilot app write, <home>/session-state/<session ID>/events.jsonl, as
```

Edit 2 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
    public static readonly ProducerVersion MaximumVersion = new(1, 0, 114);

    // The events whose allowlisted metadata is read. Every other type is skipped after its "type".
    private static readonly string[] Types = ["session.start", "journal.link"];

    private static readonly ImportEventKind[] Kinds = [ImportEventKind.SessionStart, ImportEventKind.JournalLink];

    private static readonly PathTree StartPaths = new(
```

with:

```csharp
    public static readonly ProducerVersion MaximumVersion = new(1, 0, 114);

    // The events whose allowlisted metadata is read. Every other type is skipped after its "type".
    private static readonly string[] Types =
    [
        "session.start",
        "session.resume",
        "session.usage_record",
        "session.compaction_complete",
        "subagent.started",
        "tool.execution_start",
        "tool.execution_complete",
        "journal.link",
    ];

    private static readonly ImportEventKind[] Kinds =
    [
        ImportEventKind.SessionStart,
        ImportEventKind.SessionResume,
        ImportEventKind.UsageRecord,
        ImportEventKind.CompactionComplete,
        ImportEventKind.SubagentStarted,
        ImportEventKind.ToolExecutionStart,
        ImportEventKind.ToolExecutionComplete,
        ImportEventKind.JournalLink,
    ];

    private static readonly PathTree StartPaths = new(
```

Edit 3 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
        ("data.context.headCommit", 64),
        ("data.context.repository", 512));

    public ImportSourceKind Kind => ImportSourceKind.Copilot;
```

with:

```csharp
        ("data.context.headCommit", 64),
        ("data.context.repository", 512));

    private static readonly PathTree ResumePaths = new(
        ("data.context.cwd", 4096),
        ("data.context.gitRoot", 4096),
        ("data.context.branch", 256),
        ("data.context.headCommit", 64),
        ("data.context.repository", 512));

    private static readonly PathTree UsagePaths = new(
        ("timestamp", 64),
        ("agentId", 2048),
        ("data.usage.model", 256),
        ("data.usage.responseModel", 256),
        ("data.usage.inputTokens", 0),
        ("data.usage.outputTokens", 0),
        ("data.usage.cacheReadTokens", 0),
        ("data.usage.cacheWriteTokens", 0),
        ("data.usage.cacheDetailsReported", 0),
        ("data.usage.isByok", 0),
        ("data.usage.provider.kind", 64),
        ("data.usage.providerType", 64),
        ("data.usage.serviceRequestId", 64),
        ("data.usage.accounting.usageId", 2048),
        ("data.usage.parentToolCallId", 2048),
        ("data.usage.duration", 0),
        ("data.usage.numToolCalls", 0),
        ("data.usage.initiator", 64));

    private static readonly PathTree CompactionPaths = new(
        ("timestamp", 64),
        ("agentId", 2048),
        ("data.success", 0),
        ("data.serviceRequestId", 64),
        ("data.compactionTokensUsed.requestCount", 0),
        ("data.compactionTokensUsed.inputTokens", 0),
        ("data.compactionTokensUsed.outputTokens", 0),
        ("data.compactionTokensUsed.cacheReadTokens", 0),
        ("data.compactionTokensUsed.cacheWriteTokens", 0),
        ("data.compactionTokensUsed.model", 256),
        ("data.compactionTokensUsed.duration", 0),
        ("data.compactionTokensUsed.provider.kind", 64));

    private static readonly PathTree SubagentPaths = new(
        ("agentId", 2048),
        ("data.agentName", 256),
        ("data.toolCallId", 2048));

    private static readonly PathTree ToolStartPaths = new(
        ("timestamp", 64),
        ("agentId", 2048),
        ("data.toolCallId", 2048),
        ("data.toolName", 256),
        ("data.parentToolCallId", 2048));

    private static readonly PathTree ToolCompletePaths = new(
        ("data.toolCallId", 2048),
        ("data.success", 0),
        ("data.shellExecution.exitCode", 0));

    public ImportSourceKind Kind => ImportSourceKind.Copilot;
```

Edit 4 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
        private bool _journal;
        private string? _version;
        private long _malformed;
        private long _skipped;

        internal bool OnLine(ReadOnlySpan<byte> line, long number, long offset)
```

with:

```csharp
        private bool _journal;
        private string? _version;
        private ImportedGitContext? _context;
        private string? _turnKey;
        private long _parsed;
        private long _unsupported;
        private long _malformed;
        private long _skipped;

        // Sub-agents: their names by envelope agentId and by the tool call that started them, and their agentIds by that call.
        private readonly Dictionary<string, string> _agentNameByAgentId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _agentNameByToolCallId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _agentIdByToolCallId = new(StringComparer.Ordinal);

        // The latest usage record of each agent (context key), which owns the tool calls that follow it.
        private readonly Dictionary<string, string> _latestRecordByContext = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingTool> _pendingTools = new(StringComparer.Ordinal);

        internal bool OnLine(ReadOnlySpan<byte> line, long number, long offset)
```

Edit 5 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
            switch (kind)
            {
                case ImportEventKind.JournalLink:
                    if (!_journal)
```

with:

```csharp
            switch (kind)
            {
                case ImportEventKind.SessionResume:
                    Resume(JsonFields.Read(line, ResumePaths), number, offset);
                    break;
                case ImportEventKind.UsageRecord:
                    Usage(JsonFields.Read(line, UsagePaths), number, offset);
                    break;
                case ImportEventKind.CompactionComplete:
                    Compaction(JsonFields.Read(line, CompactionPaths), number, offset);
                    break;
                case ImportEventKind.SubagentStarted:
                    SubagentStarted(JsonFields.Read(line, SubagentPaths));
                    break;
                case ImportEventKind.ToolExecutionStart:
                    ToolStart(JsonFields.Read(line, ToolStartPaths));
                    break;
                case ImportEventKind.ToolExecutionComplete:
                    ToolComplete(JsonFields.Read(line, ToolCompletePaths));
                    break;
                case ImportEventKind.JournalLink:
                    if (!_journal)
```

Edit 6 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
                ToolCalls = _toolCalls,
                Diagnostics = _diagnostics.ToList(),
                RecordsParsed = 0,
                UnsupportedUsageShapes = 0,
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
```

with:

```csharp
                ToolCalls = _toolCalls,
                Diagnostics = _diagnostics.ToList(),
                RecordsParsed = _parsed,
                UnsupportedUsageShapes = _unsupported,
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
```

Edit 7 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
            _started = true;
            _version = text;
            return true;
        }

        private bool Stop(SessionLogStatus status, ImportEventKind kind, string? version, long number, long offset)
```

with:

```csharp
            _started = true;
            _version = text;
            _context = Context(fields);
            return true;
        }

        // A resumed session's context replaces the one before for the records that follow.
        private void Resume(JsonFields fields, long number, long offset)
        {
            if (fields.Duplicate is not null)
            {
                Malformed(ImportDiagnosticCode.MalformedLine, ImportEventKind.SessionResume, fields.Duplicate, number, offset);
            }
            else if (fields.IsObject("data.context"))
            {
                _context = Context(fields);
            }
        }

        // A session.usage_record: one model call. The first field that fails validation names the unsupported shape.
        private void Usage(JsonFields fields, long number, long offset)
        {
            const ImportEventKind kind = ImportEventKind.UsageRecord;
            _parsed++;
            if (fields.Duplicate is not null)
            {
                Unsupported(kind, fields.Duplicate, number, offset);
                return;
            }

            if (Time(fields) is not DateTimeOffset end)
            {
                Unsupported(kind, "timestamp", number, offset);
                return;
            }

            if (!fields.IsObject("data.usage"))
            {
                Unsupported(kind, "data.usage", number, offset);
                return;
            }

            string? usageId = fields.String("data.usage.accounting.usageId");
            string? model = fields.String("data.usage.model");
            string? responseModel = fields.String("data.usage.responseModel");
            string? tokens = Tokens(fields, "data.usage.", out long input, out long output, out long cacheRead, out long cacheWrite);
            bool? cacheReported = fields.Boolean("data.usage.cacheDetailsReported");
            bool? byok = fields.Boolean("data.usage.isByok");
            string? serviceRequestId = UsageIdentity.NormalizeServiceRequestId(fields.String("data.usage.serviceRequestId"));
            string? failed =
                usageId is null ? "data.usage.accounting.usageId"
                : model is null ? "data.usage.model"
                : responseModel is null && fields.IsPresent("data.usage.responseModel") ? "data.usage.responseModel"
                : tokens
                ?? (cacheReported is null ? "data.usage.cacheDetailsReported"
                : byok is null ? "data.usage.isByok"
                : byok == false && fields.String("data.usage.provider.kind") != "copilot" ? "data.usage.provider.kind"
                : byok == false && serviceRequestId is null ? "data.usage.serviceRequestId"
                : null);
            if (failed is not null)
            {
                Unsupported(kind, failed, number, offset);
                return;
            }

            (string contextKey, string? agentName) = Attribute(
                fields.String("agentId"),
                fields.String("data.usage.parentToolCallId"),
                kind,
                number,
                offset);
            if (contextKey.Length == 0 && fields.String("data.usage.initiator") == "user")
            {
                _turnKey = usageId;
            }

            _records.Add(new ImportedUsageRecord
            {
                Source = ImportSourceKind.Copilot,
                Kind = ImportedRecordKind.Usage,
                SessionId = location.LogId,
                RecordKey = usageId!,
                TurnKey = _turnKey,
                ServiceRequestId = byok == true ? null : serviceRequestId,
                StartTime = end.AddMilliseconds(-(Duration(fields.Integer("data.usage.duration")) ?? 0)),
                EndTime = end,
                RequestModel = model!,
                ResponseModel = responseModel,
                ProviderName = byok == true
                    ? fields.String("data.usage.providerType")?.ToLowerInvariant()
                    : UsageIdentity.GitHubProviderName,
                BillingSource = byok == true ? BillingSource.Provider : BillingSource.Endpoint,
                AgentName = agentName,
                InputTokens = input,
                OutputTokens = output,
                CacheReadInputTokens = cacheRead,
                CacheWriteInputTokens = cacheWrite,
                CacheReadState = cacheReported == true || cacheRead > 0 ? CacheCounterState.Reported : CacheCounterState.Absent,
                CacheWriteState = cacheReported == true || cacheWrite > 0 ? CacheCounterState.Reported : CacheCounterState.Absent,
                ToolCallCount = Count(fields.Integer("data.usage.numToolCalls")),
                Git = _context,
                LineNumber = number,
            });
            _latestRecordByContext[contextKey] = usageId!;
        }

        // A session.compaction_complete with compactionTokensUsed: the one billed call that summarized the conversation.
        // Without compactionTokensUsed, it reports no usage and is a skipped event.
        private void Compaction(JsonFields fields, long number, long offset)
        {
            const ImportEventKind kind = ImportEventKind.CompactionComplete;
            if (fields.Duplicate is null && !fields.IsPresent("data.compactionTokensUsed"))
            {
                _skipped++;
                return;
            }

            _parsed++;
            string? serviceRequestId = UsageIdentity.NormalizeServiceRequestId(fields.String("data.serviceRequestId"));
            string? model = fields.String("data.compactionTokensUsed.model");
            DateTimeOffset? end = Time(fields);
            string? failed =
                fields.Duplicate
                ?? (end is null ? "timestamp"
                : fields.Boolean("data.success") != true ? "data.success"
                : !fields.IsObject("data.compactionTokensUsed") ? "data.compactionTokensUsed"
                : fields.Integer("data.compactionTokensUsed.requestCount") != 1 ? "data.compactionTokensUsed.requestCount"
                : serviceRequestId is null ? "data.serviceRequestId"
                : model is null ? "data.compactionTokensUsed.model"
                : null);
            long input = 0;
            long output = 0;
            long cacheRead = 0;
            long cacheWrite = 0;
            failed ??= Tokens(fields, "data.compactionTokensUsed.", out input, out output, out cacheRead, out cacheWrite)
                ?? (fields.String("data.compactionTokensUsed.provider.kind") != "copilot"
                    ? "data.compactionTokensUsed.provider.kind"
                    : null);
            if (failed is not null)
            {
                Unsupported(kind, failed, number, offset);
                return;
            }

            (_, string? agentName) = Attribute(fields.String("agentId"), null, kind, number, offset);
            _records.Add(new ImportedUsageRecord
            {
                Source = ImportSourceKind.Copilot,
                Kind = ImportedRecordKind.Compaction,
                SessionId = location.LogId,
                RecordKey = serviceRequestId!,
                TurnKey = _turnKey,
                ServiceRequestId = serviceRequestId,
                StartTime = end!.Value.AddMilliseconds(-(Duration(fields.Integer("data.compactionTokensUsed.duration")) ?? 0)),
                EndTime = end.Value,
                RequestModel = model!,
                ProviderName = UsageIdentity.GitHubProviderName,
                BillingSource = BillingSource.Endpoint,
                AgentName = agentName,
                InputTokens = input,
                OutputTokens = output,
                CacheReadInputTokens = cacheRead,
                CacheWriteInputTokens = cacheWrite,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Reported,
                Git = _context,
                LineNumber = number,
            });
        }

        private void SubagentStarted(JsonFields fields)
        {
            string? agentId = fields.String("agentId");
            string? agentName = fields.String("data.agentName");
            string? toolCallId = fields.String("data.toolCallId");
            if (fields.Duplicate is null && agentId is not null && agentName is not null && toolCallId is not null)
            {
                _agentNameByAgentId[agentId] = agentName;
                _agentNameByToolCallId[toolCallId] = agentName;
                _agentIdByToolCallId[toolCallId] = agentId;
            }
        }

        // A tool call belongs to the latest usage record of its agent, whose response asked for it.
        private void ToolStart(JsonFields fields)
        {
            string? toolCallId = fields.String("data.toolCallId");
            string? toolName = fields.String("data.toolName");
            if (fields.Duplicate is not null || toolCallId is null || toolName is null || Time(fields) is not DateTimeOffset start)
            {
                return;
            }

            string? agentId = fields.String("agentId");
            string? parentToolCallId = fields.String("data.parentToolCallId");
            string contextKey = ContextKey(agentId, parentToolCallId);
            _pendingTools[toolCallId] = new PendingTool(
                _latestRecordByContext.GetValueOrDefault(contextKey),
                toolName,
                AgentName(agentId, parentToolCallId),
                start);
        }

        private void ToolComplete(JsonFields fields)
        {
            string? toolCallId = fields.String("data.toolCallId");
            if (fields.Duplicate is not null ||
                toolCallId is null ||
                fields.Boolean("data.success") is not bool success ||
                !_pendingTools.Remove(toolCallId, out PendingTool? pending) ||
                pending.Owner is null)
            {
                return;
            }

            _toolCalls.Add(new ImportedToolCall
            {
                SessionId = location.LogId,
                OwnerRecordKey = pending.Owner,
                ToolCallId = toolCallId,
                ToolName = pending.ToolName,
                AgentName = pending.AgentName,
                StartTime = pending.StartTime,
                Success = success,
                ExitCode = fields.Integer("data.shellExecution.exitCode"),
            });
        }

        // The context key and agent name of a sub-agent's event: by envelope agentId, else (Copilot 1.0.103's records) by the
        // tool call that started the sub-agent. The main agent has key "" and no name, as its live spans have none.
        private (string ContextKey, string? AgentName) Attribute(
            string? agentId,
            string? parentToolCallId,
            ImportEventKind kind,
            long number,
            long offset)
        {
            string contextKey = ContextKey(agentId, parentToolCallId);
            string? agentName = AgentName(agentId, parentToolCallId);
            if (contextKey.Length > 0 && agentName is null)
            {
                _diagnostics.Add(ImportDiagnosticCode.UnknownSubagent, kind, null, number, offset);
            }

            return (contextKey, agentName);
        }

        private string ContextKey(string? agentId, string? parentToolCallId) =>
            agentId is not null ? "agent:" + agentId
            : parentToolCallId is null ? ""
            : _agentIdByToolCallId.TryGetValue(parentToolCallId, out string? id) ? "agent:" + id
            : "tool:" + parentToolCallId;

        private string? AgentName(string? agentId, string? parentToolCallId) =>
            (agentId is not null ? _agentNameByAgentId.GetValueOrDefault(agentId) : null)
            ?? (parentToolCallId is not null ? _agentNameByToolCallId.GetValueOrDefault(parentToolCallId) : null);

        private bool Stop(SessionLogStatus status, ImportEventKind kind, string? version, long number, long offset)
```

Edit 8 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
            _diagnostics.Add(code, kind, field, number, offset);
        }
    }
}
```

with:

```csharp
            _diagnostics.Add(code, kind, field, number, offset);
        }

        private void Unsupported(ImportEventKind kind, string field, long number, long offset)
        {
            _unsupported++;
            _diagnostics.Add(ImportDiagnosticCode.UnsupportedUsageShape, kind, field, number, offset);
        }

        // The four token counts under prefix, or the first invalid field: each from 0 to ImportLimits.MaxTokenCount, and
        // cache reads plus cache writes no more than the input, which includes them.
        private static string? Tokens(
            JsonFields fields,
            string prefix,
            out long input,
            out long output,
            out long cacheRead,
            out long cacheWrite)
        {
            input = Count(fields.Integer(prefix + "inputTokens")) ?? -1;
            output = Count(fields.Integer(prefix + "outputTokens")) ?? -1;
            cacheRead = Count(fields.Integer(prefix + "cacheReadTokens")) ?? -1;
            cacheWrite = Count(fields.Integer(prefix + "cacheWriteTokens")) ?? -1;
            return input < 0 ? prefix + "inputTokens"
                : output < 0 ? prefix + "outputTokens"
                : cacheRead < 0 ? prefix + "cacheReadTokens"
                : cacheWrite < 0 ? prefix + "cacheWriteTokens"
                : cacheRead + cacheWrite > input ? prefix + "inputTokens"
                : null;
        }

        private static long? Count(long? value) => value is >= 0 and <= ImportLimits.MaxTokenCount ? value : null;

        private static long? Duration(long? value) => value is >= 0 and <= ImportLimits.MaxDurationMilliseconds ? value : null;

        // The envelope timestamp, in UTC.
        private static DateTimeOffset? Time(JsonFields fields) =>
            DateTimeOffset.TryParse(
                fields.String("timestamp"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset time)
                ? time
                : null;

        private static ImportedGitContext Context(JsonFields fields) => new()
        {
            WorkingDirectory = SessionLogs.FullPath(fields.String("data.context.cwd")),
            RepositoryRoot = SessionLogs.FullPath(fields.String("data.context.gitRoot")),
            RepositorySlug = SessionLogs.RepositorySlug(fields.String("data.context.repository")),
            Branch = fields.String("data.context.branch"),
            HeadCommitSha = SessionLogs.CommitSha(fields.String("data.context.headCommit")),
        };

        private sealed record PendingTool(string? Owner, string ToolName, string? AgentName, DateTimeOffset StartTime);
    }
}
```

New file `tests/CopilotLedger.Tests/Import/CopilotCompactionTests.cs`:

```csharp
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class CopilotCompactionTests
{
    private const string SessionId = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S9 = "99999999-9999-4999-8999-999999999999";

    public static TheoryData<int, string> Shapes => new()
    {
        { 0, "data.success" },
        { 1, "data.compactionTokensUsed.requestCount" },
        { 2, "data.compactionTokensUsed.requestCount" },
        { 3, "data.serviceRequestId" },
        { 4, "data.serviceRequestId" },
        { 5, "data.compactionTokensUsed.outputTokens" },
        { 6, "data.compactionTokensUsed.inputTokens" },
        { 7, "data.compactionTokensUsed.model" },
        { 8, "data.compactionTokensUsed.provider.kind" },
    };

    [Fact]
    public async Task CC1_ACompactionIsTheCallThatSummarized()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId, "1.0.114")
            .Usage("msg_user", S1)
            .Compaction(S9.ToUpperInvariant());
        log.Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Equal(2, result.RecordsParsed);
        ImportedUsageRecord record = result.Records[1];
        Assert.Equal(ImportedRecordKind.Compaction, record.Kind);
        Assert.Equal(S9, record.RecordKey);
        Assert.Equal(S9, record.ServiceRequestId);
        Assert.Equal("msg_user", record.TurnKey);
        Assert.Equal(log.Time.AddMilliseconds(-891), record.StartTime);
        Assert.Equal(log.Time, record.EndTime);
        Assert.Equal("claude-haiku-4.5", record.RequestModel);
        Assert.Null(record.ResponseModel);
        Assert.Equal("github", record.ProviderName);
        Assert.Equal(BillingSource.Endpoint, record.BillingSource);
        Assert.Null(record.AgentName);
        Assert.Equal(112_223, record.InputTokens);
        Assert.Equal(18, record.OutputTokens);
        Assert.Equal(18_967, record.CacheReadInputTokens);
        Assert.Equal(93_251, record.CacheWriteInputTokens);
        Assert.Equal(CacheCounterState.Reported, record.CacheReadState);
        Assert.Equal(CacheCounterState.Reported, record.CacheWriteState);
        Assert.Null(record.ToolCallCount);
        Assert.Equal(3, record.LineNumber);
    }

    [Fact]
    public async Task CC2_ASubagentsCompactionIsAttributedByItsAgentId()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .SubagentStarted("agent-1", "explore", "toolu_task")
            .Compaction(S9, agentId: "agent-1")
            .Write(temp.Path);

        ImportedUsageRecord record = Assert.Single((await CopilotRecordTests.ReadAsync(temp.Path)).Records);

        Assert.Equal("explore", record.AgentName);
        Assert.Null(record.TurnKey);
    }

    [Fact]
    public async Task CC3_ACompactionWithoutTokensUsedIsASkippedEvent()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114").Compaction(S9, tokensUsed: false).Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal(0, result.RecordsParsed);
        Assert.Equal(1, result.SkippedEvents);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task CC4_EachUnsupportedCompactionNamesItsField(int caseNumber, string field)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Compaction(
                caseNumber == 3 ? null : caseNumber == 4 ? "not-a-uuid" : S9,
                change: data =>
                {
                    JsonObject used = data["compactionTokensUsed"]!.AsObject();
                    switch (caseNumber)
                    {
                        case 0:
                            data["success"] = false;
                            break;
                        case 1:
                            used["requestCount"] = 2;
                            break;
                        case 2:
                            used.Remove("requestCount");
                            break;
                        case 3:
                            data.Remove("serviceRequestId");
                            break;
                        case 5:
                            used["outputTokens"] = -1;
                            break;
                        case 6:
                            used["cacheWriteTokens"] = 100_000;
                            break;
                        case 7:
                            used.Remove("model");
                            break;
                        case 8:
                            used["provider"]!["kind"] = "byok";
                            break;
                    }
                })
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal(1, result.RecordsParsed);
        Assert.Equal(1, result.UnsupportedUsageShapes);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.UnsupportedUsageShape, diagnostic.Code);
        Assert.Equal(ImportEventKind.CompactionComplete, diagnostic.Event);
        Assert.Equal(field, diagnostic.Field);
    }

    [Fact]
    public async Task CC5_ACompactionDoesntTakeOverTheToolCallsOfTheRecordBefore()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Usage("msg_user", S1)
            .Compaction(S9)
            .ToolStart("toolu_1", "view")
            .ToolComplete("toolu_1", true)
            .Write(temp.Path);

        ImportedToolCall call = Assert.Single((await CopilotRecordTests.ReadAsync(temp.Path)).ToolCalls);

        Assert.Equal("msg_user", call.OwnerRecordKey);
    }

    [Fact]
    public async Task CC6_TheSummaryAndRequestIdsAreNeverRead()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Compaction(
                S9,
                change: data =>
                {
                    data["error"] = new JsonObject { ["message"] = CopilotSessionLogs.Canary };
                    data["failureReason"] = CopilotSessionLogs.Canary;
                    data["failureDiagnostics"] = new JsonArray(CopilotSessionLogs.Canary);
                    data["responsesReasoning"] = CopilotSessionLogs.Canary;
                })
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Single(result.Records);
        CopilotRecordTests.AssertNoCanary(result);
    }
}
```

New file `tests/CopilotLedger.Tests/Import/CopilotRecordTests.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class CopilotRecordTests
{
    private const string SessionId = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static readonly CopilotSessionLogSource Source = new();

    public static TheoryData<int, string> Shapes => new()
    {
        { 0, "data.usage.accounting.usageId" },
        { 1, "data.usage.serviceRequestId" },
        { 2, "data.usage.inputTokens" },
        { 3, "data.usage.outputTokens" },
        { 4, "data.usage.inputTokens" },
        { 5, "data.usage.provider.kind" },
        { 6, "data.usage.model" },
        { 7, "data.usage.model" },
        { 8, "data.usage.isByok" },
    };

    [Fact]
    public async Task CU1_AMainAgentRecordMapsEveryField()
    {
        using TempDirectory temp = new();
        string root = Path.Combine(temp.Path, "webapp");
        string cwd = Path.Combine(root, "src");
        CopilotSessionLogs log = new CopilotSessionLogs(
                SessionId,
                "1.0.103",
                context: CopilotSessionLogs.Context(cwd, root + Path.DirectorySeparatorChar, "main", "contoso/webapp", Sha.ToUpperInvariant()))
            .UserMessage()
            .Usage(
                "msg_01SyntheticA",
                S1.ToUpperInvariant(),
                input: 19_484,
                output: 321,
                cacheWrite: 19_474,
                duration: 3_372,
                numToolCalls: 1);
        log.Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(1, result.RecordsParsed);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            new ImportedUsageRecord
            {
                Source = ImportSourceKind.Copilot,
                Kind = ImportedRecordKind.Usage,
                SessionId = SessionId,
                RecordKey = "msg_01SyntheticA",
                TurnKey = "msg_01SyntheticA",
                ServiceRequestId = S1,
                StartTime = log.Time.AddMilliseconds(-3_372),
                EndTime = log.Time,
                RequestModel = "claude-haiku-4.5",
                ResponseModel = null,
                ProviderName = "github",
                BillingSource = BillingSource.Endpoint,
                AgentName = null,
                InputTokens = 19_484,
                OutputTokens = 321,
                CacheReadInputTokens = 0,
                CacheWriteInputTokens = 19_474,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Reported,
                ToolCallCount = 1,
                Git = new ImportedGitContext
                {
                    WorkingDirectory = cwd,
                    RepositoryRoot = root,
                    RepositorySlug = "contoso/webapp",
                    Branch = "main",
                    HeadCommitSha = Sha,
                },
                LineNumber = 3,
            },
            Assert.Single(result.Records));
    }

    [Fact]
    public async Task CU2_TheResponseModelIsKeptAsLogged()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114")
            .Usage("msg_01SyntheticA", S1, responseModel: "claude-haiku-4-5-20251001")
            .Write(temp.Path);

        ImportedUsageRecord record = Assert.Single((await ReadAsync(temp.Path)).Records);

        Assert.Equal("claude-haiku-4.5", record.RequestModel);
        Assert.Equal("claude-haiku-4-5-20251001", record.ResponseModel);
    }

    [Theory]
    [InlineData(0, CacheCounterState.Absent)]
    [InlineData(500, CacheCounterState.Reported)]
    public async Task CU3_UnreportedCacheDetailsAreAbsentUnlessACounterIsNonzero(long cacheRead, CacheCounterState state)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg_01SyntheticA", S1, cacheRead: cacheRead, cacheDetailsReported: false)
            .Write(temp.Path);

        ImportedUsageRecord record = Assert.Single((await ReadAsync(temp.Path)).Records);

        Assert.Equal(state, record.CacheReadState);
        Assert.Equal(CacheCounterState.Absent, record.CacheWriteState);
        Assert.Equal(cacheRead, record.CacheReadInputTokens);
        Assert.Equal(0, record.CacheWriteInputTokens);
    }

    [Theory]
    [InlineData("1.0.103")]
    [InlineData("1.0.114")]
    public async Task CU4_SubagentRecordsAreAttributedToTheirAgent(string version)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, version)
            .Usage("msg_main", S1)
            .ToolStart("toolu_task", "task")
            .SubagentStarted("agent-1", "explore", "toolu_task")
            .Usage(
                "msg_sub",
                S2,
                initiator: "agent",
                agentId: "agent-1",
                parentToolCallId: version == "1.0.103" ? "toolu_task" : null)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal([null, "explore"], result.Records.Select(record => record.AgentName));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task CU5_AnUnknownSubagentHasNoNameAndAnInformationalDiagnostic()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg_sub", S1, initiator: "agent", agentId: "agent-unknown")
            .Usage("msg_old", S2, initiator: "agent", parentToolCallId: "toolu_unknown")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal([null, null], result.Records.Select(record => record.AgentName));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.UnknownSubagent, diagnostic.Code);
        Assert.Equal(ImportEventKind.UsageRecord, diagnostic.Event);
        Assert.Equal(2, diagnostic.Count);
        Assert.Equal(2, diagnostic.FirstLine);
        Assert.Equal(0, result.UnsupportedUsageShapes);
    }

    [Fact]
    public async Task CU6_AResumeChangesTheContextOfLaterRecordsOnly()
    {
        using TempDirectory temp = new();
        string root = Path.Combine(temp.Path, "webapp");
        new CopilotSessionLogs(
                SessionId,
                context: CopilotSessionLogs.Context(root, root, "main", "contoso/webapp", Sha))
            .Usage("msg_1", S1)
            .Resume(null)
            .Usage("msg_2", S2)
            .Resume(CopilotSessionLogs.Context(root, root, "feature/search", "contoso/webapp", Sha))
            .Usage("msg_3", S3)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(["main", "main", "feature/search"], result.Records.Select(record => record.Git!.Branch));
        Assert.All(result.Records, record => Assert.Equal(root, record.Git!.RepositoryRoot));
    }

    [Fact]
    public async Task CU7_ByokRecordsAreBilledByTheirProvider()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg_byok", null, model: "gpt-6.1-sol", providerType: "OpenAI")
            .Write(temp.Path);

        ImportedUsageRecord record = Assert.Single((await ReadAsync(temp.Path)).Records);

        Assert.Equal(BillingSource.Provider, record.BillingSource);
        Assert.Equal("openai", record.ProviderName);
        Assert.Null(record.ServiceRequestId);
        Assert.Equal("gpt-6.1-sol", record.RequestModel);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task CU8_EachUnsupportedShapeNamesItsField(int caseNumber, string field)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg_ok", S1)
            .Usage(
                "msg_bad",
                S2,
                change: usage =>
                {
                    switch (caseNumber)
                    {
                        case 0:
                            usage["accounting"]!.AsObject().Remove("usageId");
                            break;
                        case 1:
                            usage.Remove("serviceRequestId");
                            break;
                        case 2:
                            usage["inputTokens"] = JsonNode.Parse("1.0");
                            break;
                        case 3:
                            usage["outputTokens"] = -1;
                            break;
                        case 4:
                            usage["cacheReadTokens"] = 1_001;
                            break;
                        case 5:
                            usage["provider"]!["kind"] = "other";
                            break;
                        case 6:
                            usage.Remove("model");
                            break;
                        case 7:
                            usage["model"] = new string('m', 257);
                            break;
                        default:
                            usage.Remove("isByok");
                            break;
                    }
                })
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(2, result.RecordsParsed);
        Assert.Equal(1, result.UnsupportedUsageShapes);
        Assert.Equal(["msg_ok"], result.Records.Select(record => record.RecordKey));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.UnsupportedUsageShape, diagnostic.Code);
        Assert.Equal(ImportEventKind.UsageRecord, diagnostic.Event);
        Assert.Equal(field, diagnostic.Field);
        Assert.Equal(3, diagnostic.FirstLine);
    }

    [Fact]
    public async Task CU9_UserPromptsStartTracesThatOtherRecordsJoin()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Usage("msg_before", S1, initiator: "agent")
            .Usage("msg_user_1", S2)
            .SubagentStarted("agent-1", "explore", "toolu_task")
            .Usage("msg_sub", S3, initiator: "user", agentId: "agent-1")
            .Usage("msg_agent", "44444444-4444-4444-8444-444444444444", initiator: "agent")
            .Usage("msg_user_2", "55555555-5555-4555-8555-555555555555")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(
            [null, "msg_user_1", "msg_user_1", "msg_user_1", "msg_user_2"],
            result.Records.Select(record => record.TurnKey));
    }

    [Fact]
    public async Task CU10_ToolCallsBelongToTheirAgentsLatestRecord()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId)
            .ToolStart("toolu_0", "bash")
            .ToolComplete("toolu_0", true)
            .Usage("msg_main", S1)
            .ToolStart("toolu_1", "bash")
            .ToolComplete("toolu_1", false, exitCode: 2)
            .ToolStart("toolu_2", "view")
            .ToolComplete("toolu_9", true)
            .SubagentStarted("agent-1", "explore", "toolu_task")
            .Usage("msg_sub", S2, initiator: "agent", agentId: "agent-1")
            .ToolStart("toolu_4", "grep", agentId: "agent-1");
        DateTimeOffset grepStart = log.Time;
        log.ToolComplete("toolu_4", true, agentId: "agent-1").Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Collection(
            result.ToolCalls,
            call =>
            {
                Assert.Equal("msg_main", call.OwnerRecordKey);
                Assert.Equal("toolu_1", call.ToolCallId);
                Assert.Equal("bash", call.ToolName);
                Assert.Null(call.AgentName);
                Assert.False(call.Success);
                Assert.Equal(2, call.ExitCode);
            },
            call =>
            {
                Assert.Equal("msg_sub", call.OwnerRecordKey);
                Assert.Equal("toolu_4", call.ToolCallId);
                Assert.Equal("grep", call.ToolName);
                Assert.Equal("explore", call.AgentName);
                Assert.True(call.Success);
                Assert.Null(call.ExitCode);
                Assert.Equal(grepStart, call.StartTime);
                Assert.Equal(SessionId, call.SessionId);
            });
    }

    [Fact]
    public async Task CU11_SessionTotalsNeverBecomeRecords()
    {
        using TempDirectory temp = new();
        JsonObject totals = new()
        {
            ["agentMetrics"] = new JsonObject
            {
                ["main"] = new JsonObject { ["inputTokens"] = 5_000, ["model"] = "claude-haiku-4.5" },
            },
            ["codeChanges"] = new JsonObject { ["filesModified"] = new JsonArray(CopilotSessionLogs.Canary) },
        };
        new CopilotSessionLogs(SessionId)
            .Usage("msg_1", S1)
            .Event("session.usage_checkpoint", totals.DeepClone().AsObject())
            .Event("subagent.completed", new JsonObject { ["inputTokens"] = 5_000, ["agentName"] = "explore" })
            .Event("session.shutdown", totals.DeepClone().AsObject())
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(["msg_1"], result.Records.Select(record => record.RecordKey));
        Assert.Equal(3, result.SkippedEvents);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CU12_NoContentReachesTheResult()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .UserMessage()
            .Event("assistant.message", new JsonObject { ["content"] = CopilotSessionLogs.Canary })
            .Usage("msg_main", S1, numToolCalls: 1)
            .ToolStart("toolu_1", "bash")
            .ToolComplete("toolu_1", false, exitCode: 1)
            .SubagentStarted("agent-1", "explore", "toolu_task")
            .Usage("msg_sub", S2, initiator: "agent", agentId: "agent-1")
            .Usage("msg_byok", null, providerType: "anthropic")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(3, result.Records.Count);
        Assert.Single(result.ToolCalls);
        AssertNoCanary(result);
    }

    internal static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home)), CancellationToken.None);

    internal static void AssertNoCanary(SessionLogReadResult result) =>
        Assert.DoesNotContain(CopilotSessionLogs.Canary, JsonSerializer.Serialize(result), StringComparison.Ordinal);
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/TestSupport/CopilotSessionLogs.cs`, replace:

```csharp
        Event("user.message", new JsonObject { ["content"] = Canary, ["transformedContent"] = Canary });

    // The log's bytes: each line followed by "\n".
    public string Text() => string.Concat(_lines.Select(line => line + "\n"));
```

with:

```csharp
        Event("user.message", new JsonObject { ["content"] = Canary, ["transformedContent"] = Canary });

    // A session.start or session.resume context.
    public static JsonObject Context(
        string cwd,
        string? gitRoot = null,
        string? branch = null,
        string? repository = null,
        string? headCommit = null)
    {
        JsonObject context = new() { ["cwd"] = cwd };
        if (gitRoot is not null)
        {
            context["gitRoot"] = gitRoot;
            context["branch"] = branch;
            context["headCommit"] = headCommit;
            context["baseCommit"] = headCommit;
            context["repository"] = repository;
            context["repositoryHost"] = "github.com";
            context["hostType"] = "github";
        }

        return context;
    }

    public CopilotSessionLogs Resume(JsonObject? context) =>
        Event(
            "session.resume",
            context is null
                ? new JsonObject { ["resumeTime"] = Stamp(_time), ["eventCount"] = _lines.Count }
                : new JsonObject { ["resumeTime"] = Stamp(_time), ["eventCount"] = _lines.Count, ["context"] = context });

    // A session.usage_record with the shape Copilot 1.0.114 writes; with responseModel null, as 1.0.103 writes it. A BYOK
    // record has providerType and no provider or service request ID. change can alter data.usage before it's written.
    public CopilotSessionLogs Usage(
        string usageId,
        string? serviceRequestId,
        string model = "claude-haiku-4.5",
        string? responseModel = null,
        long input = 1_000,
        long output = 100,
        long cacheRead = 0,
        long cacheWrite = 0,
        bool cacheDetailsReported = true,
        string initiator = "user",
        long duration = 1_000,
        long numToolCalls = 0,
        string? agentId = null,
        string? parentToolCallId = null,
        string? providerType = null,
        Action<JsonObject>? change = null)
    {
        JsonObject usage = new() { ["model"] = model };
        if (responseModel is not null)
        {
            usage["responseModel"] = responseModel;
        }

        if (providerType is null)
        {
            usage["provider"] = new JsonObject { ["id"] = "copilot", ["kind"] = "copilot", ["label"] = "GitHub Copilot" };
        }

        usage["modelDisplayName"] = model;
        usage["inputTokens"] = input;
        usage["outputTokens"] = output;
        usage["cacheReadTokens"] = cacheRead;
        usage["cacheWriteTokens"] = cacheWrite;
        usage["reasoningTokens"] = 0;
        usage["cost"] = JsonNode.Parse("0.33");
        usage["duration"] = duration;
        usage["initiator"] = initiator;
        usage["interactionType"] = "conversation-agent";
        usage["isByok"] = providerType is not null;
        usage["apiEndpoint"] = "/v1/messages";
        usage["apiCallId"] = usageId;
        if (providerType is null)
        {
            usage["providerCallId"] = "chatcmpl-" + Canary;
            usage["serviceRequestId"] = serviceRequestId;
        }
        else
        {
            usage["byokKind"] = "custom";
            usage["modelProvider"] = providerType;
            usage["providerType"] = providerType;
        }

        usage["accounting"] = new JsonObject
        {
            ["sourceSessionId"] = SessionId,
            ["sequence"] = _lines.Count,
            ["usageId"] = usageId,
        };
        if (parentToolCallId is not null)
        {
            usage["parentToolCallId"] = parentToolCallId;
        }

        usage["cacheDetailsReported"] = cacheDetailsReported;
        usage["numToolCalls"] = numToolCalls;
        usage["toolCounts"] = new JsonObject { [Canary] = numToolCalls };
        usage["finishReason"] = "tool_calls";
        change?.Invoke(usage);
        return Event("session.usage_record", new JsonObject { ["usage"] = usage }, agentId);
    }

    // A session.compaction_complete with the shape Copilot 1.0.114 writes: its usage in compactionTokensUsed, and content
    // in summaryContent, checkpointPath, and the request IDs that match nothing. change can alter data before it's written.
    public CopilotSessionLogs Compaction(
        string? serviceRequestId,
        string model = "claude-haiku-4.5",
        long input = 112_223,
        long output = 18,
        long cacheRead = 18_967,
        long cacheWrite = 93_251,
        long duration = 891,
        string? agentId = null,
        bool tokensUsed = true,
        Action<JsonObject>? change = null)
    {
        JsonObject data = new()
        {
            ["success"] = true,
            ["outcome"] = "applied",
            ["compactionAttemptId"] = "c0000000-0000-4000-8000-000000000001",
            ["preCompactionMessagesLength"] = 34,
            ["summaryContent"] = Canary,
            ["trigger"] = "threshold",
            ["execution"] = "background",
            ["preCompactionTokens"] = 117_765,
            ["postCompactionTokens"] = 25_805,
            ["clientRequestId"] = Canary,
            ["attemptMetrics"] = new JsonObject { ["durationMs"] = 2_640, ["deadlinePolicy"] = Canary },
        };
        if (tokensUsed)
        {
            data["compactionTokensUsed"] = new JsonObject
            {
                ["requestCount"] = 1,
                ["aiCreditsStatus"] = "complete",
                ["provider"] = new JsonObject { ["id"] = "copilot", ["kind"] = "copilot", ["label"] = "GitHub Copilot" },
                ["modelDisplayName"] = Canary,
                ["inputTokens"] = input,
                ["outputTokens"] = output,
                ["cacheReadTokens"] = cacheRead,
                ["cacheWriteTokens"] = cacheWrite,
                ["copilotUsage"] = new JsonObject { ["totalNanoAiu"] = 11_855_545_000 },
                ["duration"] = duration,
                ["model"] = model,
            };
        }

        data["requestId"] = Canary;
        data["serviceRequestId"] = serviceRequestId;
        data["checkpointNumber"] = 1;
        data["checkpointPath"] = Path.Combine(Canary, "checkpoint.md");
        change?.Invoke(data);
        return Event("session.compaction_complete", data, agentId);
    }

    public CopilotSessionLogs SubagentStarted(string agentId, string agentName, string toolCallId) =>
        Event(
            "subagent.started",
            new JsonObject
            {
                ["toolCallId"] = toolCallId,
                ["agentName"] = agentName,
                ["agentDisplayName"] = Canary,
                ["agentDescription"] = Canary,
                ["model"] = "gpt-6-luna",
                ["agentType"] = agentName,
            },
            agentId);

    public CopilotSessionLogs ToolStart(
        string toolCallId,
        string toolName,
        string? agentId = null,
        string? parentToolCallId = null)
    {
        JsonObject data = new()
        {
            ["toolCallId"] = toolCallId,
            ["toolName"] = toolName,
            ["arguments"] = new JsonObject { ["command"] = Canary },
            ["toolTitle"] = Canary,
        };
        if (parentToolCallId is not null)
        {
            data["parentToolCallId"] = parentToolCallId;
        }

        return Event("tool.execution_start", data, agentId);
    }

    public CopilotSessionLogs ToolComplete(string toolCallId, bool success, long? exitCode = null, string? agentId = null)
    {
        JsonObject data = new()
        {
            ["toolCallId"] = toolCallId,
            ["success"] = success,
            ["result"] = new JsonObject { ["content"] = Canary },
        };
        if (!success)
        {
            data["error"] = new JsonObject { ["code"] = "failure", ["message"] = Canary };
        }

        if (exitCode is long code)
        {
            data["shellExecution"] = new JsonObject { ["exitCode"] = code };
        }

        return Event("tool.execution_complete", data, agentId);
    }

    // The log's bytes: each line followed by "\n".
    public string Text() => string.Concat(_lines.Select(line => line + "\n"));
```

### Tests

- **CU1**: a main-agent record maps every field. **CU2**: the response model is kept as logged (`claude-haiku-4-5-20251001`). **CU3** (2): `cacheDetailsReported` false gives `Absent` unless a count is above 0. **CU4** (2): sub-agent records get their agent's name by envelope `agentId` (1.0.114) and by `parentToolCallId` (1.0.103). **CU5**: an unknown sub-agent has no name and an informational diagnostic. **CU6**: a resume changes the context of later records only. **CU7**: BYOK records are billed by their provider. **CU8** (9): each unsupported shape names its field (`accounting.usageId`, `serviceRequestId`, the token counts, `provider.kind`, `model`, `isByok`). **CU9**: user prompts start traces that other records join. **CU10**: tool calls belong to their agent's latest record. **CU11**: session totals never become records. **CU12**: no content reaches the result.
- **CC1**: a compaction is the call that summarized: key, service request ID, times, model, tokens, and reported cache states. **CC2**: a sub-agent's compaction is attributed by its `agentId`. **CC3**: without `compactionTokensUsed`, it's a skipped event. **CC4** (9): each unsupported compaction names its field. **CC5**: a compaction doesn't take over the tool calls of the record before it. **CC6**: the summary and request IDs are never read.

### Acceptance criteria

- Every mapping row above holds (CU1–CU7, CU9, CU10, CC1, CC2); unsupported shapes are counted and named, never recorded (CU8, CC4).
- `dotnet test CopilotLedger.slnx` passes **1162** tests (1126 + 36).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 7 specification: Codex Rollouts

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs` | New: discovery, the gate, and `token_usage_record` records |
| `src/CopilotLedger.Core/Import/SessionLogs.cs` | New `Uuid`, shared with discovery |
| `tests/CopilotLedger.Tests/TestSupport/CodexRollouts.cs` | New: a builder of synthetic rollouts with canaries in content fields |
| `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs` | New: CX1–CX12 |

### Contracts

- `CodexSessionLogSource.MinimumVersion` 0.153.0 and `MaximumVersion` 0.171.0; `DefaultHome` (`CODEX_HOME` when set and non-empty, else `<user profile>/.codex`) and `ResolveHome(codexHome, userProfile)`.
- Discovery lists `sessions/**/rollout-<yyyy-MM-ddTHH-mm-ss>-<thread>[_<rollout>].jsonl` and the same names in `archived_sessions`, with `LogId` the lowercase IDs joined by `_`, after `archived/` for an archived rollout; `.jsonl.zst` is listed as compressed and reported `Compressed`, never opened.

### Algorithm

1. The first event must be `session_meta` with UUIDs `payload.id` (the thread) and `payload.session_id`, and `payload.cli_version` 0.153.0 to 0.171.0; it also gives the provider (`model_provider` lowercased, `lmstudio` as `lm_studio`), the agent (`agent_role`), the working directory, and Git (`git.repository_url`'s slug with credentials removed, `git.branch`, `git.commit_hash`). Later `session_meta` lines, inherited by a fork, are skipped.
2. `turn_context` remembers each turn's model and the latest one.
3. `token_usage_record` of another thread is skipped as inherited (informational). Otherwise it must have a timestamp, this session's `session_id`, a `response_id`, a `usage` object with valid `input_tokens`, `cached_input_tokens`, and `output_tokens`, a valid `cache_write_input_tokens` when present, cached plus written at most the input, and a model from its turn or the latest one; the first failing field names the unsupported shape.
4. A record: key `response_id`, session `payload.session_id`, turn `root_turn_id` (else `turn_id`), start = end = the line's timestamp, `Provider` billing, cache reads reported, cache writes reported only when present.

### Code (copy exactly)

New file `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`:

```csharp
using System.Globalization;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;

// The rollouts Codex 0.153.0 to 0.171.0 writes: <home>/sessions/YYYY/MM/DD/rollout-<time>-<thread ID>[_<rollout ID>].jsonl,
// and the same names under <home>/archived_sessions. Usage comes only from token_usage_record lines, one per completed
// response; token_count events, compacted snapshots, and turn and thread totals are never read.
public sealed class CodexSessionLogSource : ISessionLogSource
{
    public static readonly ProducerVersion MinimumVersion = new(0, 153, 0);
    public static readonly ProducerVersion MaximumVersion = new(0, 171, 0);

    // The lines whose allowlisted metadata is read. Every other type is skipped after its "type".
    private static readonly string[] Types = ["session_meta", "turn_context", "token_usage_record"];

    private static readonly ImportEventKind[] Kinds =
        [ImportEventKind.SessionMeta, ImportEventKind.TurnContext, ImportEventKind.TokenUsageRecord];

    private static readonly PathTree MetaPaths = new(
        ("payload.id", 64),
        ("payload.session_id", 64),
        ("payload.cli_version", ProducerVersion.MaxLength),
        ("payload.cwd", 4096),
        ("payload.model_provider", 64),
        ("payload.agent_role", 256),
        ("payload.git.commit_hash", 64),
        ("payload.git.branch", 256),
        ("payload.git.repository_url", 2048));

    private static readonly PathTree TurnPaths = new(("payload.turn_id", 2048), ("payload.model", 256));

    private static readonly PathTree UsagePaths = new(
        ("timestamp", 64),
        ("payload.thread_id", 64),
        ("payload.turn_id", 2048),
        ("payload.session_id", 64),
        ("payload.root_turn_id", 2048),
        ("payload.response_id", 2048),
        ("payload.usage.input_tokens", 0),
        ("payload.usage.cached_input_tokens", 0),
        ("payload.usage.cache_write_input_tokens", 0),
        ("payload.usage.output_tokens", 0));

    // CODEX_HOME when it's set and not empty, as Codex reads it; else <user profile>/.codex.
    public static string DefaultHome => ResolveHome(
        Environment.GetEnvironmentVariable("CODEX_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public ImportSourceKind Kind => ImportSourceKind.Codex;

    public static string ResolveHome(string? codexHome, string userProfile) =>
        string.IsNullOrEmpty(codexHome) ? Path.Combine(userProfile, ".codex") : codexHome;

    // Rollouts under <home>/sessions, and under <home>/archived_sessions with LogIds that start with "archived/". A
    // .jsonl.zst rollout is listed as compressed and never opened.
    public IReadOnlyList<SessionLogLocation> Discover(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        List<SessionLogLocation> logs = [];
        AddRollouts(logs, Path.Combine(home, "sessions"), "");
        AddRollouts(logs, Path.Combine(home, "archived_sessions"), "archived/");
        return [.. logs.OrderBy(log => log.LogId, StringComparer.Ordinal)];
    }

    public Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (location.Source != ImportSourceKind.Codex)
        {
            throw new ArgumentException("The location isn't a Codex rollout.", nameof(location));
        }

        return Task.Run(() => Read(location, cancellationToken), cancellationToken);
    }

    private static void AddRollouts(List<SessionLogLocation> logs, string root, string prefix)
    {
        try
        {
            foreach (string path in Directory.EnumerateFiles(root, "rollout-*", SearchOption.AllDirectories))
            {
                if (ParseName(Path.GetFileName(path)) is not (string id, bool compressed))
                {
                    continue;
                }

                try
                {
                    FileInfo file = new(path);
                    logs.Add(new SessionLogLocation
                    {
                        Source = ImportSourceKind.Codex,
                        LogId = prefix + id,
                        Path = file.FullName,
                        SizeBytes = file.Length,
                        LastWriteTime = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                        IsActive = false,
                        IsCompressed = compressed,
                    });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // rollout-YYYY-MM-DDTHH-MM-SS-<thread ID>[_<rollout ID>].jsonl, or .jsonl.zst: the IDs in lowercase, joined by "_".
    private static (string Id, bool Compressed)? ParseName(string name)
    {
        bool compressed = name.EndsWith(".jsonl.zst", StringComparison.Ordinal);
        if (!name.StartsWith("rollout-", StringComparison.Ordinal) ||
            (!compressed && !name.EndsWith(".jsonl", StringComparison.Ordinal)))
        {
            return null;
        }

        string core = name["rollout-".Length..^(compressed ? ".jsonl.zst".Length : ".jsonl".Length)];
        if (core.Length < 20 ||
            core[19] != '-' ||
            !DateTime.TryParseExact(core[..19], "yyyy-MM-dd'T'HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return null;
        }

        string[] ids = core[20..].Split('_');
        string? thread = SessionLogs.Uuid(ids[0]);
        string? rollout = ids.Length == 2 ? SessionLogs.Uuid(ids[1]) : null;
        if (thread is null || ids.Length > 2 || (ids.Length == 2 && rollout is null))
        {
            return null;
        }

        return (rollout is null ? thread : thread + "_" + rollout, compressed);
    }

    private static SessionLogReadResult Read(SessionLogLocation location, CancellationToken cancellationToken)
    {
        if (location.IsCompressed)
        {
            return SessionLogs.NotRead(location, SessionLogStatus.Compressed, ImportEventKind.Unknown, null, 0, 0);
        }

        Rollout rollout = new(location);
        try
        {
            using FileStream stream = new(
                location.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                0,
                FileOptions.SequentialScan);
            JsonlLineReader.Read(
                stream,
                location.SizeBytes,
                ImportLimits.MaxLineBytes,
                rollout.OnLine,
                rollout.OnOversized,
                rollout.OnUnterminated,
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SessionLogs.NotRead(location, SessionLogStatus.Unreadable, ImportEventKind.Unknown, null, 0, 0);
        }

        return rollout.Finish();
    }

    // One rollout's reading state.
    private sealed class Rollout(SessionLogLocation location)
    {
        private readonly ImportDiagnosticBag _diagnostics = new(ImportSourceKind.Codex, location.LogId);
        private readonly List<ImportedUsageRecord> _records = [];
        private readonly Dictionary<string, string> _modelByTurn = new(StringComparer.Ordinal);
        private (SessionLogStatus Status, ImportEventKind Kind, string? Version, long Line, long Offset)? _stop;
        private bool _started;
        private string? _version;
        private string? _threadId;
        private string? _sessionId;
        private string? _providerName;
        private string? _agentName;
        private ImportedGitContext? _git;
        private string? _latestModel;
        private long _parsed;
        private long _unsupported;
        private long _malformed;
        private long _skipped;

        internal bool OnLine(ReadOnlySpan<byte> line, long number, long offset)
        {
            int? type = JsonFields.ReadType(line, Types);
            if (type is null)
            {
                Malformed(ImportDiagnosticCode.MalformedLine, ImportEventKind.Unknown, null, number, offset);
                return true;
            }

            ImportEventKind kind = type < 0 ? ImportEventKind.Unknown : Kinds[type.Value];
            if (!_started)
            {
                return kind == ImportEventKind.SessionMeta
                    ? Meta(JsonFields.Read(line, MetaPaths), number, offset)
                    : Stop(SessionLogStatus.MissingSessionStart, kind, null, number, offset);
            }

            switch (kind)
            {
                case ImportEventKind.TurnContext:
                    Turn(JsonFields.Read(line, TurnPaths));
                    break;
                case ImportEventKind.TokenUsageRecord:
                    Usage(JsonFields.Read(line, UsagePaths), number, offset);
                    break;
                default:
                    _skipped++;
                    break;
            }

            return true;
        }

        internal void OnOversized(long number, long offset) =>
            Malformed(ImportDiagnosticCode.OversizedLine, ImportEventKind.Unknown, null, number, offset);

        internal void OnUnterminated(long number, long offset) =>
            Malformed(ImportDiagnosticCode.UnterminatedLine, ImportEventKind.Unknown, null, number, offset);

        internal SessionLogReadResult Finish()
        {
            if (_stop is { } stop)
            {
                return SessionLogs.NotRead(location, stop.Status, stop.Kind, stop.Version, stop.Line, stop.Offset);
            }

            if (!_started)
            {
                return SessionLogs.NotRead(
                    location,
                    SessionLogStatus.MissingSessionStart,
                    ImportEventKind.Unknown,
                    null,
                    0,
                    0);
            }

            return new SessionLogReadResult
            {
                Status = SessionLogStatus.Read,
                ProducerVersion = _version,
                Records = _records,
                ToolCalls = [],
                Diagnostics = _diagnostics.ToList(),
                RecordsParsed = _parsed,
                UnsupportedUsageShapes = _unsupported,
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
            };
        }

        // The first session_meta is the rollout's identity (Codex's own rule); a fork's later, inherited ones are skipped.
        private bool Meta(JsonFields fields, long number, long offset)
        {
            if (fields.Duplicate is not null)
            {
                Malformed(ImportDiagnosticCode.MalformedLine, ImportEventKind.SessionMeta, fields.Duplicate, number, offset);
                return true;
            }

            _threadId = SessionLogs.Uuid(fields.String("payload.id"));
            _sessionId = SessionLogs.Uuid(fields.String("payload.session_id"));
            if (_threadId is null || _sessionId is null)
            {
                return Stop(SessionLogStatus.UnsupportedProducer, ImportEventKind.SessionMeta, null, number, offset);
            }

            string? text = fields.String("payload.cli_version");
            if (!ProducerVersion.TryParse(text, out ProducerVersion? version))
            {
                return Stop(SessionLogStatus.UnsupportedProducerVersion, ImportEventKind.SessionMeta, null, number, offset);
            }

            if (version.CompareTo(MinimumVersion) < 0 || version.CompareTo(MaximumVersion) > 0)
            {
                return Stop(SessionLogStatus.UnsupportedProducerVersion, ImportEventKind.SessionMeta, text, number, offset);
            }

            string? provider = fields.String("payload.model_provider")?.ToLowerInvariant();
            _started = true;
            _version = text;
            _providerName = provider == "lmstudio" ? "lm_studio" : provider;
            _agentName = fields.String("payload.agent_role");
            _git = new ImportedGitContext
            {
                WorkingDirectory = SessionLogs.FullPath(fields.String("payload.cwd")),
                RepositorySlug = SessionLogs.RepositorySlug(fields.String("payload.git.repository_url")),
                Branch = fields.String("payload.git.branch"),
                HeadCommitSha = SessionLogs.CommitSha(fields.String("payload.git.commit_hash")),
            };
            return true;
        }

        private void Turn(JsonFields fields)
        {
            string? model = fields.String("payload.model");
            if (fields.Duplicate is not null || model is null)
            {
                return;
            }

            _latestModel = model;
            if (fields.String("payload.turn_id") is string turnId)
            {
                _modelByTurn[turnId] = model;
            }
        }

        // A token_usage_record: one completed response. A record of another thread, inherited by a fork, is skipped.
        private void Usage(JsonFields fields, long number, long offset)
        {
            const ImportEventKind kind = ImportEventKind.TokenUsageRecord;
            string? threadId = SessionLogs.Uuid(fields.String("payload.thread_id"));
            if (threadId is not null && threadId != _threadId)
            {
                _diagnostics.Add(ImportDiagnosticCode.InheritedRecord, kind, null, number, offset);
                return;
            }

            _parsed++;
            string? turnId = fields.String("payload.turn_id");
            string? responseId = fields.String("payload.response_id");
            DateTimeOffset? time = DateTimeOffset.TryParse(
                fields.String("timestamp"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed)
                ? parsed
                : null;
            long? input = Count(fields.Integer("payload.usage.input_tokens"));
            long? cached = Count(fields.Integer("payload.usage.cached_input_tokens"));
            long? output = Count(fields.Integer("payload.usage.output_tokens"));
            bool writes = fields.IsPresent("payload.usage.cache_write_input_tokens");
            long? written = writes ? Count(fields.Integer("payload.usage.cache_write_input_tokens")) : 0;
            string? model = (turnId is not null ? _modelByTurn.GetValueOrDefault(turnId) : null) ?? _latestModel;
            string? failed =
                fields.Duplicate
                ?? (time is null ? "timestamp"
                : threadId is null ? "payload.thread_id"
                : SessionLogs.Uuid(fields.String("payload.session_id")) != _sessionId ? "payload.session_id"
                : responseId is null ? "payload.response_id"
                : !fields.IsObject("payload.usage") ? "payload.usage"
                : input is null ? "payload.usage.input_tokens"
                : cached is null ? "payload.usage.cached_input_tokens"
                : output is null ? "payload.usage.output_tokens"
                : written is null ? "payload.usage.cache_write_input_tokens"
                : cached + written > input ? "payload.usage.input_tokens"
                : model is null ? "turn_context.model"
                : null);
            if (failed is not null)
            {
                _unsupported++;
                _diagnostics.Add(ImportDiagnosticCode.UnsupportedUsageShape, kind, failed, number, offset);
                return;
            }

            _records.Add(new ImportedUsageRecord
            {
                Source = ImportSourceKind.Codex,
                Kind = ImportedRecordKind.Usage,
                SessionId = _sessionId!,
                RecordKey = responseId!,
                TurnKey = fields.String("payload.root_turn_id") ?? turnId,
                StartTime = time!.Value,
                EndTime = time.Value,
                RequestModel = model!,
                ProviderName = _providerName,
                BillingSource = BillingSource.Provider,
                AgentName = _agentName,
                InputTokens = input!.Value,
                OutputTokens = output!.Value,
                CacheReadInputTokens = cached!.Value,
                CacheWriteInputTokens = written!.Value,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = writes ? CacheCounterState.Reported : CacheCounterState.Absent,
                Git = _git,
                LineNumber = number,
            });
        }

        private bool Stop(SessionLogStatus status, ImportEventKind kind, string? version, long number, long offset)
        {
            _stop = (status, kind, version, number, offset);
            return false;
        }

        private void Malformed(ImportDiagnosticCode code, ImportEventKind kind, string? field, long number, long offset)
        {
            _malformed++;
            _diagnostics.Add(code, kind, field, number, offset);
        }

        private static long? Count(long? value) => value is >= 0 and <= ImportLimits.MaxTokenCount ? value : null;
    }
}
```

Edit 1 of 1 in `src/CopilotLedger.Core/Import/SessionLogs.cs`, replace:

```csharp
        value is { Length: 40 or 64 } && value.All(char.IsAsciiHexDigit) ? value.ToLowerInvariant() : null;

    // A fully qualified path without trailing separators; else null.
    internal static string? FullPath(string? value) =>
```

with:

```csharp
        value is { Length: 40 or 64 } && value.All(char.IsAsciiHexDigit) ? value.ToLowerInvariant() : null;

    // A UUID of 36 hex digits and hyphens, in lowercase "D" form; else null.
    internal static string? Uuid(string? value) =>
        value is { Length: 36 } &&
        value.All(character => character == '-' || char.IsAsciiHexDigit(character)) &&
        Guid.TryParseExact(value, "D", out Guid id)
            ? id.ToString("D")
            : null;

    // A fully qualified path without trailing separators; else null.
    internal static string? FullPath(string? value) =>
```

New file `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class CodexRolloutTests
{
    private const string Thread = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b";
    private const string Fork = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2c";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static readonly CodexSessionLogSource Source = new();

    public static TheoryData<int, string> Shapes => new()
    {
        { 0, "payload.response_id" },
        { 1, "payload.response_id" },
        { 2, "payload.usage.output_tokens" },
        { 3, "payload.usage.input_tokens" },
        { 4, "payload.usage.input_tokens" },
    };

    [Fact]
    public void CX1_DiscoveryListsRolloutsAndArchivedOnes()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread).Write(temp.Path);
        new CodexRollouts(Thread).Write(temp.Path, rolloutId: Fork.ToUpperInvariant());
        new CodexRollouts(Fork).Write(temp.Path, archived: true);
        new CodexRollouts(Fork).Write(temp.Path, compressed: true);
        string day = Path.Combine(temp.Path, "sessions", "2026", "11", "20");
        File.WriteAllText(Path.Combine(day, "notes.txt"), "x");
        File.WriteAllText(Path.Combine(day, "rollout-2026-11-20T12-00-00-not-a-thread.jsonl"), "x");
        File.WriteAllText(Path.Combine(day, $"rollout-2026-13-20T12-00-00-{Thread}.jsonl"), "x");

        IReadOnlyList<SessionLogLocation> logs = Source.Discover(temp.Path);

        Assert.Equal(
            [Thread, $"{Thread}_{Fork}", Fork, $"archived/{Fork}"],
            logs.Select(log => log.LogId));
        Assert.Equal([false, false, true, false], logs.Select(log => log.IsCompressed));
        Assert.All(logs, log => Assert.False(log.IsActive));
        Assert.Empty(Source.Discover(Path.Combine(temp.Path, "missing")));
    }

    [Theory]
    [InlineData("0.153.0", SessionLogStatus.Read)]
    [InlineData("0.171.0", SessionLogStatus.Read)]
    [InlineData("0.152.0", SessionLogStatus.UnsupportedProducerVersion)]
    [InlineData("0.172.0", SessionLogStatus.UnsupportedProducerVersion)]
    [InlineData("0.173.0-alpha.1", SessionLogStatus.UnsupportedProducerVersion)]
    [InlineData(null, SessionLogStatus.MissingSessionStart)]
    public async Task CX2_TheGateReadsOnlyCodex0_153_0To0_171_0(string? version, SessionLogStatus status)
    {
        using TempDirectory temp = new();
        CodexRollouts rollout = new CodexRollouts(Thread, version: version ?? "0.171.0")
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1");
        if (version is null)
        {
            string path = rollout.Write(temp.Path);
            File.WriteAllLines(path, File.ReadAllLines(path).Skip(1));
        }
        else
        {
            rollout.Write(temp.Path);
        }

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(status, result.Status);
        Assert.Equal(status == SessionLogStatus.Read ? 1 : 0, result.Records.Count);
    }

    [Fact]
    public async Task CX3_ATokenUsageRecordMapsEveryField()
    {
        using TempDirectory temp = new();
        string cwd = Path.Combine(temp.Path, "api");
        CodexRollouts rollout = new CodexRollouts(
                Thread,
                sessionId: Fork,
                modelProvider: "OpenAI",
                agentRole: "explorer",
                git: new JsonObject
                {
                    ["commit_hash"] = Sha,
                    ["branch"] = "feature/x",
                    ["repository_url"] = "https://github.com/contoso/api.git",
                },
                cwd: cwd)
            .TurnContext("turn-2", "gpt-6.1-sol")
            .Usage("resp_2", "turn-2", input: 100, cached: 40, output: 7, cacheWrite: 60, rootTurnId: "turn-1");
        rollout.Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(1, result.RecordsParsed);
        Assert.Equal("0.171.0", result.ProducerVersion);
        Assert.Equal(
            new ImportedUsageRecord
            {
                Source = ImportSourceKind.Codex,
                Kind = ImportedRecordKind.Usage,
                SessionId = Fork,
                RecordKey = "resp_2",
                TurnKey = "turn-1",
                ServiceRequestId = null,
                StartTime = rollout.Time,
                EndTime = rollout.Time,
                RequestModel = "gpt-6.1-sol",
                ResponseModel = null,
                ProviderName = "openai",
                BillingSource = BillingSource.Provider,
                AgentName = "explorer",
                InputTokens = 100,
                OutputTokens = 7,
                CacheReadInputTokens = 40,
                CacheWriteInputTokens = 60,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Reported,
                ToolCallCount = null,
                Git = new ImportedGitContext
                {
                    WorkingDirectory = cwd,
                    RepositoryRoot = null,
                    RepositorySlug = "contoso/api",
                    Branch = "feature/x",
                    HeadCommitSha = Sha,
                },
                LineNumber = 3,
            },
            Assert.Single(result.Records));
    }

    [Fact]
    public async Task CX4_TheModelComesFromTheRecordsTurnElseTheLatestTurnContext()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .TurnContext("turn-1", "gpt-6.1-sol")
            .TurnContext(null, "gpt-6-luna")
            .Usage("resp_1", "turn-1")
            .Usage("resp_2", "turn-9", cacheWrite: null)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(["gpt-6.1-sol", "gpt-6-luna"], result.Records.Select(record => record.RequestModel));
        Assert.Equal(
            [CacheCounterState.Reported, CacheCounterState.Absent],
            result.Records.Select(record => record.CacheWriteState));
    }

    [Theory]
    [InlineData("ollama", "ollama")]
    [InlineData("lmstudio", "lm_studio")]
    [InlineData("openai", "openai")]
    [InlineData(null, null)]
    public async Task CX5_TheModelProviderNamesTheProvider(string? modelProvider, string? providerName)
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread, modelProvider: modelProvider)
            .TurnContext("turn-1", "qwen3.8-27b")
            .Usage("resp_1", "turn-1")
            .Write(temp.Path);

        ImportedUsageRecord record = Assert.Single((await ReadAsync(temp.Path)).Records);

        Assert.Equal(providerName, record.ProviderName);
        Assert.Equal(BillingSource.Provider, record.BillingSource);
    }

    [Fact]
    public async Task CX6_TokenCountsAndCompactedSnapshotsAddNothing()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .TurnContext("turn-1", "gpt-6.1-sol")
            .TokenCount(1_000, 0, 100)
            .Usage("resp_1", "turn-1")
            .TokenCount(1_000, 0, 100)
            .TokenCount(1_000, 0, 100)
            .TokenCount(0, 0, 0)
            .Line(
                "compacted",
                new JsonObject
                {
                    ["message"] = CopilotSessionLogs.Canary,
                    ["compaction_response_id"] = "resp_compact",
                    ["latest_token_usage_record"] = new JsonObject
                    {
                        ["thread_id"] = Thread,
                        ["response_id"] = "resp_1",
                        ["usage"] = new JsonObject { ["input_tokens"] = 1_000 },
                    },
                })
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(["resp_1"], result.Records.Select(record => record.RecordKey));
        Assert.Equal(1, result.RecordsParsed);
        Assert.Equal(5, result.SkippedEvents);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task CX7_UnsupportedRecordsNameTheirField(int caseNumber, string field)
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage(
                caseNumber == 0 ? null : caseNumber == 1 ? "" : "resp_1",
                "turn-1",
                output: caseNumber == 2 ? -1 : 100,
                cached: caseNumber == 3 ? 1_001 : 600,
                cacheWrite: caseNumber == 4 ? 401 : 0,
                change: payload =>
                {
                    if (caseNumber == 0)
                    {
                        payload.Remove("response_id");
                    }
                })
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal(1, result.UnsupportedUsageShapes);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.UnsupportedUsageShape, diagnostic.Code);
        Assert.Equal(ImportEventKind.TokenUsageRecord, diagnostic.Event);
        Assert.Equal(field, diagnostic.Field);
    }

    [Fact]
    public async Task CX8_ARecordOfAnotherThreadIsSkipped()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .Line("session_meta", new JsonObject { ["id"] = Fork, ["session_id"] = Fork, ["cli_version"] = "0.171.0" })
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_parent", "turn-1", threadId: Fork)
            .Usage("resp_own", "turn-1")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(["resp_own"], result.Records.Select(record => record.RecordKey));
        Assert.Equal(1, result.RecordsParsed);
        Assert.Equal(1, result.SkippedEvents);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.InheritedRecord, diagnostic.Code);
        Assert.Equal(4, diagnostic.FirstLine);
    }

    [Fact]
    public async Task CX9_CredentialsInTheRepositoryUrlAreNeverKept()
    {
        using TempDirectory temp = new();
        new CodexRollouts(
                Thread,
                git: new JsonObject { ["repository_url"] = "https://user:" + CopilotSessionLogs.Canary + "@github.com/o/r.git" })
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal("o/r", Assert.Single(result.Records).Git!.RepositorySlug);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CX10_ACompressedRolloutIsNeverOpened()
    {
        using TempDirectory temp = new();
        string path = new CodexRollouts(Thread).Write(temp.Path, compressed: true);
        SessionLogLocation location = Assert.Single(Source.Discover(temp.Path));
        File.Delete(path);

        SessionLogReadResult result = await Source.ReadAsync(location, CancellationToken.None);

        Assert.Equal(SessionLogStatus.Compressed, result.Status);
        Assert.Equal(ImportDiagnosticCode.CompressedLog, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("codex-home")]
    public void CX11_CodexHomeIsCodexHomeWhenSet(string? codexHome)
    {
        string profile = Path.Combine(Path.GetTempPath(), "profile");

        Assert.Equal(
            string.IsNullOrEmpty(codexHome) ? Path.Combine(profile, ".codex") : codexHome,
            CodexSessionLogSource.ResolveHome(codexHome, profile));
    }

    [Fact]
    public async Task CX12_NoContentReachesTheResult()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .UserMessage()
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Line("event_msg", new JsonObject { ["type"] = "agent_message", ["message"] = CopilotSessionLogs.Canary })
            .Line(
                "response_item",
                new JsonObject
                {
                    ["type"] = "function_call",
                    ["name"] = "shell",
                    ["arguments"] = CopilotSessionLogs.Canary,
                    ["call_id"] = CopilotSessionLogs.Canary,
                })
            .Usage("resp_1", "turn-1")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Single(result.Records);
        AssertNoCanary(result);
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home)), CancellationToken.None);

    private static void AssertNoCanary(SessionLogReadResult result) =>
        Assert.DoesNotContain(CopilotSessionLogs.Canary, JsonSerializer.Serialize(result), StringComparison.Ordinal);
}
```

New file `tests/CopilotLedger.Tests/TestSupport/CodexRollouts.cs`:

```csharp
using System.Globalization;
using System.Text.Json.Nodes;

namespace CopilotLedger.Tests.TestSupport;

// A synthetic Codex rollout with the line shapes of Codex 0.153.0 to 0.171.0 (codex-rs at rust-v0.171.0) and made-up
// values. Content-bearing fields hold CopilotSessionLogs.Canary.
public sealed class CodexRollouts
{
    private readonly List<string> _lines = [];
    private DateTimeOffset _time;

    // Starts the rollout with session_meta.
    public CodexRollouts(
        string threadId,
        string? sessionId = null,
        string version = "0.171.0",
        string? modelProvider = "openai",
        string? agentRole = null,
        JsonObject? git = null,
        string? cwd = null,
        DateTimeOffset? start = null)
    {
        ThreadId = threadId;
        SessionId = sessionId ?? threadId;
        _time = start ?? new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        JsonObject payload = new()
        {
            ["session_id"] = SessionId,
            ["id"] = threadId,
            ["timestamp"] = Stamp(_time),
            ["cwd"] = cwd ?? CopilotSessionLogs.DefaultWorkingDirectory,
            ["originator"] = "codex_cli_rs",
            ["cli_version"] = version,
            ["source"] = "cli",
        };
        if (agentRole is not null)
        {
            payload["agent_role"] = agentRole;
        }

        payload["model_provider"] = modelProvider;
        payload["base_instructions"] = new JsonObject { ["text"] = CopilotSessionLogs.Canary };
        if (git is not null)
        {
            payload["git"] = git;
        }

        Line("session_meta", payload);
    }

    public string ThreadId { get; }

    public string SessionId { get; }

    // The time of the last line; each line is one second after the one before.
    public DateTimeOffset Time => _time;

    // {"timestamp": …, "type": type, "payload": payload}, one second after the line before.
    public CodexRollouts Line(string type, JsonObject payload)
    {
        _time = _lines.Count == 0 ? _time : _time.AddSeconds(1);
        _lines.Add(new JsonObject { ["timestamp"] = Stamp(_time), ["type"] = type, ["payload"] = payload }.ToJsonString());
        return this;
    }

    public CodexRollouts Raw(string line)
    {
        _lines.Add(line);
        return this;
    }

    public CodexRollouts TurnContext(string? turnId, string model)
    {
        JsonObject payload = new();
        if (turnId is not null)
        {
            payload["turn_id"] = turnId;
        }

        payload["cwd"] = CopilotSessionLogs.DefaultWorkingDirectory;
        payload["approval_policy"] = "never";
        payload["sandbox_policy"] = new JsonObject { ["type"] = "danger-full-access" };
        payload["model"] = model;
        payload["summary"] = "auto";
        payload["user_instructions"] = CopilotSessionLogs.Canary;
        return Line("turn_context", payload);
    }

    public CodexRollouts UserMessage() =>
        Line(
                "response_item",
                new JsonObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = new JsonArray(new JsonObject { ["type"] = "input_text", ["text"] = CopilotSessionLogs.Canary }),
                })
            .Line(
                "event_msg",
                new JsonObject { ["type"] = "user_message", ["message"] = CopilotSessionLogs.Canary, ["kind"] = "plain" });

    // An event_msg token_count with cumulative and last totals, which import never reads.
    public CodexRollouts TokenCount(long input, long cached, long output) =>
        Line(
            "event_msg",
            new JsonObject
            {
                ["type"] = "token_count",
                ["info"] = new JsonObject
                {
                    ["total_token_usage"] = Usage(input, cached, 0, output),
                    ["last_token_usage"] = Usage(input, cached, 0, output),
                    ["model_context_window"] = 272_000,
                },
                ["rate_limits"] = null,
            });

    // A token_usage_record of this thread (or threadId), for one completed response. change can alter the payload.
    public CodexRollouts Usage(
        string? responseId,
        string turnId,
        long input = 1_000,
        long cached = 0,
        long output = 100,
        long? cacheWrite = 0,
        string? rootTurnId = null,
        string? threadId = null,
        Action<JsonObject>? change = null)
    {
        JsonObject payload = new()
        {
            ["thread_id"] = threadId ?? ThreadId,
            ["turn_id"] = turnId,
            ["session_id"] = SessionId,
            ["root_turn_id"] = rootTurnId ?? turnId,
            ["response_id"] = responseId,
            ["usage"] = Usage(input, cached, cacheWrite, output),
            ["turn_token_usage"] = Usage(input * 10, cached, cacheWrite, output * 10),
            ["thread_token_usage"] = Usage(input * 100, cached, cacheWrite, output * 100),
        };
        change?.Invoke(payload);
        return Line("token_usage_record", payload);
    }

    // The rollout's bytes: each line followed by "\n".
    public string Text() => string.Concat(_lines.Select(line => line + "\n"));

    // Writes <codexHome>/sessions/2026/11/20/rollout-2026-11-20T12-00-00-<ThreadId>.jsonl, or the same name directly under
    // archived_sessions, with .zst appended when compressed, and returns its path.
    public string Write(string codexHome, bool archived = false, bool compressed = false, string? rolloutId = null)
    {
        string directory = archived
            ? Path.Combine(codexHome, "archived_sessions")
            : Path.Combine(codexHome, "sessions", "2026", "11", "20");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(
            directory,
            $"rollout-2026-11-20T12-00-00-{ThreadId}{(rolloutId is null ? "" : "_" + rolloutId)}.jsonl{(compressed ? ".zst" : "")}");
        File.WriteAllText(path, Text());
        FileRetry.ReadAllText(path);
        return path;
    }

    public static string Stamp(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static JsonObject Usage(long input, long cached, long? cacheWrite, long output)
    {
        JsonObject usage = new() { ["input_tokens"] = input, ["cached_input_tokens"] = cached };
        if (cacheWrite is long writes)
        {
            usage["cache_write_input_tokens"] = writes;
        }

        usage["output_tokens"] = output;
        usage["reasoning_output_tokens"] = 0;
        usage["total_tokens"] = input + output;
        return usage;
    }
}
```

### Tests

- **CX1**: discovery lists rollouts and archived rollouts with their IDs, sizes, and compression. **CX2** (6): 0.153.0 and 0.171.0 are read; 0.152.0, 0.172.0, and `0.173.0-alpha.1` are unsupported versions; a rollout without `session_meta` is `MissingSessionStart`. **CX3**: a `token_usage_record` maps every field. **CX4**: the model comes from the record's turn, else the latest `turn_context`. **CX5** (4): `ollama`, `lmstudio` (as `lm_studio`), `openai`, and none name the provider. **CX6**: `token_count` events and `compacted` snapshots add nothing. **CX7** (5): unsupported records name their field (`payload.response_id`, `payload.usage.output_tokens`, `payload.usage.input_tokens`). **CX8**: a record of another thread is skipped. **CX9**: credentials in `repository_url` are never kept. **CX10**: a compressed rollout is never opened. **CX11** (3): `CODEX_HOME` is used when it's set and not empty. **CX12**: no content reaches the result.

### Acceptance criteria

- One record per `token_usage_record` of the rollout's own thread; cumulative totals never become records (CX6, CX8).
- `dotnet test CopilotLedger.slnx` passes **1188** tests (1162 + 26).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 8 specification: Import Mapping and the Dry-Run Snapshot

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Import/SessionImportMapper.cs` | New: `ToSpan`, `ToToolLink` |
| `src/CopilotLedger.Storage/LedgerSnapshot.cs` | New: the read-only snapshot |
| `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs` | `ValidateImportedEntry` becomes `internal`, for the snapshot |
| `tests/CopilotLedger.Tests/Import/SessionImportMapperTests.cs` | New: MP1, MP2 |
| `tests/CopilotLedger.Tests/Storage/LedgerSnapshotTests.cs` | New: SN1–SN6 |

### Contracts

- `SessionImportMapper.ToSpan(ImportedUsageRecord)`: a `chat` span with `ImportIds.TraceId` and `SpanId`, no parent, no endpoint, no working directory, `ConversationId` = the session ID, the record's models, tokens, cache states, agent, `ServiceRequestId`, and `BillingSource`. `ToToolLink(ImportedToolCall, ImportedUsageRecord owner)`: an `execute_tool` link in the owner's trace with `ImportIds.ToolSpanId`, the tool's name, agent, start, and exit code, and status 2 when it failed.
- `LedgerSnapshot.OpenAsync(string databasePath, CancellationToken)`; `SchemaVersion` (0 without a ledger); `EvaluateAsync(entries)` returns what `RecordImportedAsync` would decide, counting its own earlier decisions; `GetImportSessionAsync(source, logId)`; `DisposeAsync`. It throws `InvalidOperationException` for a ledger newer than this build.

### Algorithm (`LedgerSnapshot`)

1. No ledger file: an empty snapshot; nothing is created.
2. Neither `-wal` nor `-shm`: open `file:///…?immutable=1` read-only; otherwise `Mode=ReadOnly`. `Pooling=false`, one deferred read transaction.
3. Version 6: gather facts as the store does. Version 5 or earlier: no aliases or imports, and every live row of a session counts as unidentified.
4. Decide with `UsageAuthority.Decide`, then remember a `Recorded` record's alias and canonical ID, so a later copy in the same run is a `Duplicate`.

### Code (copy exactly)

New file `src/CopilotLedger.Core/Import/SessionImportMapper.cs`:

```csharp
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;

// Turns session-log records into what the ledger records for live spans.
public static class SessionImportMapper
{
    // A chat span of the record's session (its conversation), with the record's IDs (ImportIds), no endpoint, no parent, and
    // no working directory: its Git context comes from the log, never from today's working tree.
    public static NormalizedGenAiSpan ToSpan(ImportedUsageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new NormalizedGenAiSpan
        {
            TraceId = ImportIds.TraceId(record.Source, record.SessionId, record.TurnKey, record.Kind, record.RecordKey),
            SpanId = ImportIds.SpanId(record.Source, record.SessionId, record.Kind, record.RecordKey),
            ParentSpanId = null,
            OperationName = GenAiOperationNames.Chat,
            StartTime = record.StartTime,
            EndTime = record.EndTime,
            ProviderName = record.ProviderName,
            RequestModel = record.RequestModel,
            ResponseModel = record.ResponseModel,
            AgentName = record.AgentName,
            ConversationId = record.SessionId,
            InputTokens = record.InputTokens,
            OutputTokens = record.OutputTokens,
            CacheReadInputTokens = record.CacheReadInputTokens,
            CacheWriteInputTokens = record.CacheWriteInputTokens,
            CacheReadState = record.CacheReadState,
            CacheWriteState = record.CacheWriteState,
            ServiceRequestId = record.ServiceRequestId,
            BillingSource = record.BillingSource,
        };
    }

    // An execute_tool link in the trace of the record that asked for the tool: its name and exit code, and status 2 (error)
    // when it failed.
    public static GenAiSpanLink ToToolLink(ImportedToolCall toolCall, ImportedUsageRecord owner)
    {
        ArgumentNullException.ThrowIfNull(toolCall);
        ArgumentNullException.ThrowIfNull(owner);
        return new GenAiSpanLink
        {
            TraceId = ImportIds.TraceId(owner.Source, owner.SessionId, owner.TurnKey, owner.Kind, owner.RecordKey),
            SpanId = ImportIds.ToolSpanId(owner.Source, toolCall.SessionId, toolCall.ToolCallId),
            ParentSpanId = null,
            OperationName = GenAiOperationNames.ExecuteTool,
            AgentName = toolCall.AgentName,
            ConversationId = toolCall.SessionId,
            StartTime = toolCall.StartTime,
            ToolName = toolCall.ToolName,
            ToolExitCode = toolCall.ExitCode,
            StatusCode = toolCall.Success ? 0 : 2,
        };
    }
}
```

New file `src/CopilotLedger.Storage/LedgerSnapshot.cs`:

```csharp
using System.Text;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// The ledger as one consistent, read-only snapshot, for a dry run: it decides each imported record as RecordImportedAsync
// would, and remembers its own decisions, without ever creating, migrating, or writing the ledger. With neither -wal nor -shm
// beside the ledger, it opens the file as immutable, so SQLite doesn't create them either.
public sealed class LedgerSnapshot : IAsyncDisposable
{
    private readonly SqliteConnection? _connection;
    private readonly SqliteTransaction? _transaction;
    private readonly HashSet<(string Source, string Key)> _aliases = [];
    private readonly HashSet<string> _recordedCanonicalIds = new(StringComparer.Ordinal);

    private LedgerSnapshot(SqliteConnection? connection, SqliteTransaction? transaction, int schemaVersion)
    {
        _connection = connection;
        _transaction = transaction;
        SchemaVersion = schemaVersion;
    }

    // 0 when there's no ledger.
    public int SchemaVersion { get; }

    // Throws InvalidOperationException for a ledger newer than this gh-ledger, as InitializeAsync does.
    public static async Task<LedgerSnapshot> OpenAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        string path = Path.GetFullPath(databasePath);
        if (!File.Exists(path))
        {
            return new LedgerSnapshot(null, null, 0);
        }

        bool companions = File.Exists(path + "-wal") || File.Exists(path + "-shm");
        SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = companions ? path : ImmutableUri(path),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA busy_timeout = 5000;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            int version;
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA user_version;";
                version = Convert.ToInt32(
                    await command.ExecuteScalarAsync(cancellationToken),
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            int latest = LedgerMigrations.Migrations[^1].Version;
            if (version > latest)
            {
                throw new InvalidOperationException(
                    $"Database user_version {version} is newer than the supported version {latest}.");
            }

            return new LedgerSnapshot(connection, connection.BeginTransaction(deferred: true), version);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    // What RecordImportedAsync would decide for these entries now, counting the decisions of earlier calls on this snapshot.
    public async Task<ImportRecordResult> EvaluateAsync(
        IReadOnlyList<ImportedLedgerEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (ImportedLedgerEntry entry in entries)
        {
            SqliteLedgerStore.ValidateImportedEntry(entry, nameof(entries));
        }

        List<ImportResolution> resolutions = new(entries.Count);
        int links = 0;
        foreach (ImportedLedgerEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string source = ImportIds.SourceName(entry.Source);
            string? canonical = UsageIdentity.CanonicalUsageId(
                entry.Span.ProviderName,
                entry.Span.ServiceRequestId,
                entry.Calculation.Channel);
            UsageAuthority.ImportFacts facts = await FactsAsync(
                source,
                entry.RecordKeySha256,
                canonical,
                entry.Span.ConversationId!,
                cancellationToken);
            if (_aliases.Contains((source, entry.RecordKeySha256)))
            {
                facts = facts with { AliasExists = true };
            }

            if (canonical is not null &&
                facts.Owner == UsageAuthority.CanonicalOwner.None &&
                _recordedCanonicalIds.Contains(canonical))
            {
                facts = facts with { Owner = UsageAuthority.CanonicalOwner.Import };
            }

            ImportResolution resolution = UsageAuthority.Decide(facts);
            if (resolution != ImportResolution.AmbiguousOverlap)
            {
                _aliases.Add((source, entry.RecordKeySha256));
            }

            if (resolution == ImportResolution.Recorded)
            {
                links += entry.ToolLinks.Count;
                if (canonical is not null)
                {
                    _recordedCanonicalIds.Add(canonical);
                }
            }

            resolutions.Add(resolution);
        }

        return new ImportRecordResult { Resolutions = resolutions, ToolLinksRecorded = links };
    }

    // The checkpoint of a log, or null when none was written or the ledger has no import checkpoints yet.
    public async Task<ImportSessionCheckpoint?> GetImportSessionAsync(
        ImportSourceKind source,
        string logId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logId);
        return SchemaVersion < 6
            ? null
            : await UsageAuthority.ReadCheckpointAsync(_connection!, _transaction, source, logId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    // Before schema version 6, the ledger has no aliases or usage identities, and every live row is unidentified.
    private async Task<UsageAuthority.ImportFacts> FactsAsync(
        string source,
        string recordKeySha256,
        string? canonicalUsageId,
        string conversationId,
        CancellationToken cancellationToken)
    {
        if (SchemaVersion >= 6)
        {
            return await UsageAuthority.GatherAsync(
                _transaction!,
                source,
                recordKeySha256,
                canonicalUsageId,
                conversationId,
                cancellationToken);
        }

        bool live = false;
        if (SchemaVersion > 0)
        {
            await using SqliteCommand command = _connection!.CreateCommand();
            command.Transaction = _transaction;
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM spans WHERE conversation_id = @conversation);";
            command.Parameters.AddWithValue("@conversation", conversationId);
            live = (long)(await command.ExecuteScalarAsync(cancellationToken))! == 1;
        }

        return new UsageAuthority.ImportFacts(
            false,
            canonicalUsageId is not null,
            UsageAuthority.CanonicalOwner.None,
            null,
            live,
            live);
    }

    // file:///C:/…/ledger.db?immutable=1 (file:///home/… elsewhere), every byte but [A-Za-z0-9-._~/:] percent-encoded.
    private static string ImmutableUri(string path)
    {
        string full = path.Replace('\\', '/');
        StringBuilder encoded = new();
        foreach (byte value in Encoding.UTF8.GetBytes(full))
        {
            char character = (char)value;
            if (value < 0x80 && (char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~' or '/' or ':'))
            {
                encoded.Append(character);
            }
            else
            {
                encoded.Append('%').Append(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return (full.StartsWith('/') ? "file://" : "file:///") + encoded + "?immutable=1";
    }
}
```

Edit 1 of 1 in `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs`, replace:

```csharp
    }

    private static void ValidateImportedEntry(ImportedLedgerEntry entry, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(entry, parameterName);
```

with:

```csharp
    }

    internal static void ValidateImportedEntry(ImportedLedgerEntry entry, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(entry, parameterName);
```

New file `tests/CopilotLedger.Tests/Import/SessionImportMapperTests.cs`:

```csharp
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Tests.Import;

public sealed class SessionImportMapperTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";

    private static readonly ImportedUsageRecord Record = new()
    {
        Source = ImportSourceKind.Copilot,
        Kind = ImportedRecordKind.Usage,
        SessionId = "session-a",
        RecordKey = "msg_1",
        TurnKey = "msg_0",
        ServiceRequestId = S1,
        StartTime = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
        EndTime = new DateTimeOffset(2026, 11, 20, 12, 0, 3, TimeSpan.Zero),
        RequestModel = "claude-haiku-4.5",
        ResponseModel = "claude-haiku-4-5-20251001",
        ProviderName = "github",
        BillingSource = BillingSource.Endpoint,
        AgentName = "explore",
        InputTokens = 1_000,
        OutputTokens = 100,
        CacheReadInputTokens = 600,
        CacheWriteInputTokens = 200,
        CacheReadState = CacheCounterState.Reported,
        CacheWriteState = CacheCounterState.Reported,
        ToolCallCount = 2,
        Git = new ImportedGitContext { RepositorySlug = "contoso/webapp", Branch = "main" },
        LineNumber = 7,
    };

    [Fact]
    public void MP1_ARecordIsAChatSpanOfItsSession()
    {
        Assert.Equal(
            new NormalizedGenAiSpan
            {
                TraceId = ImportIds.TraceId(ImportSourceKind.Copilot, "session-a", "msg_0", ImportedRecordKind.Usage, "msg_1"),
                SpanId = ImportIds.SpanId(ImportSourceKind.Copilot, "session-a", ImportedRecordKind.Usage, "msg_1"),
                ParentSpanId = null,
                OperationName = "chat",
                StartTime = Record.StartTime,
                EndTime = Record.EndTime,
                ProviderName = "github",
                RequestModel = "claude-haiku-4.5",
                ResponseModel = "claude-haiku-4-5-20251001",
                AgentName = "explore",
                ConversationId = "session-a",
                ServerAddress = null,
                ServerPort = null,
                WorkingDirectory = null,
                InputTokens = 1_000,
                OutputTokens = 100,
                CacheReadInputTokens = 600,
                CacheWriteInputTokens = 200,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Reported,
                ServiceRequestId = S1,
                BillingSource = BillingSource.Endpoint,
            },
            SessionImportMapper.ToSpan(Record));
    }

    [Theory]
    [InlineData(true, null, 0)]
    [InlineData(false, 2L, 2)]
    public void MP2_AToolCallIsAnExecuteToolLinkInItsOwnersTrace(bool success, long? exitCode, int status)
    {
        ImportedToolCall call = new()
        {
            SessionId = "session-a",
            OwnerRecordKey = "msg_1",
            ToolCallId = "toolu_1",
            ToolName = "bash",
            AgentName = "explore",
            StartTime = Record.EndTime,
            Success = success,
            ExitCode = exitCode,
        };

        Assert.Equal(
            new GenAiSpanLink
            {
                TraceId = SessionImportMapper.ToSpan(Record).TraceId,
                SpanId = ImportIds.ToolSpanId(ImportSourceKind.Copilot, "session-a", "toolu_1"),
                ParentSpanId = null,
                OperationName = "execute_tool",
                AgentName = "explore",
                ConversationId = "session-a",
                StartTime = Record.EndTime,
                ToolName = "bash",
                ToolExitCode = exitCode,
                StatusCode = status,
            },
            SessionImportMapper.ToToolLink(call, Record));
    }
}
```

New file `tests/CopilotLedger.Tests/Storage/LedgerSnapshotTests.cs`:

```csharp
using System.Security.Cryptography;
using CopilotLedger.Core.Import;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class LedgerSnapshotTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";

    [Fact]
    public async Task SN1_WithoutALedgerNothingIsCreated()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "data", "ledger.db");

        await using (LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path))
        {
            ImportRecordResult result = await snapshot.EvaluateAsync(
                [ImportEntries.Copilot("usage-1", "session-a", S1), ImportEntries.Copilot("usage-1", "session-a", S1)]);

            Assert.Equal(0, snapshot.SchemaVersion);
            Assert.Equal([ImportResolution.Recorded, ImportResolution.Duplicate], result.Resolutions);
            Assert.Null(await snapshot.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a"));
        }

        Assert.False(Directory.Exists(Path.Combine(temp.Path, "data")));
    }

    [Fact]
    public async Task SN2_AnImmutableReadLeavesTheLedgerAndItsCompanionsAsTheyWere()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("session-a", S1)]);
        SqliteConnection.ClearAllPools();
        Assert.False(File.Exists(path + "-wal"));
        Assert.False(File.Exists(path + "-shm"));
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(path));

        await using (LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path))
        {
            ImportRecordResult result = await snapshot.EvaluateAsync([ImportEntries.Copilot("usage-1", "session-a", S1)]);

            Assert.Equal(6, snapshot.SchemaVersion);
            Assert.Equal([ImportResolution.OverlapSkipped], result.Resolutions);
            Assert.False(File.Exists(path + "-wal"));
            Assert.False(File.Exists(path + "-shm"));
        }

        Assert.False(File.Exists(path + "-wal"));
        Assert.False(File.Exists(path + "-shm"));
        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(path)));
    }

    [Fact]
    public async Task SN3_AVersion5LedgerIsntMigratedAndDefersItsLiveConversations()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("session-a", S1)]);
        await LedgerSql.ExecuteAsync(path, """
            DROP TABLE import_record_aliases;
            DROP TABLE import_sessions;
            DROP INDEX ux_spans_effective_import_usage;
            DROP INDEX ix_spans_canonical_usage;
            ALTER TABLE span_links DROP COLUMN owner_span_row_id;
            ALTER TABLE spans DROP COLUMN superseded_by;
            ALTER TABLE spans DROP COLUMN imported_tool_call_count;
            ALTER TABLE spans DROP COLUMN accounting_state;
            ALTER TABLE spans DROP COLUMN canonical_usage_id;
            ALTER TABLE spans DROP COLUMN ingest_source;
            PRAGMA user_version = 5;
            """);
        SqliteConnection.ClearAllPools();

        await using (LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path))
        {
            ImportRecordResult result = await snapshot.EvaluateAsync(
                [ImportEntries.Copilot("usage-1", "session-a", S1), ImportEntries.Copilot("usage-2", "session-b", S2)]);

            Assert.Equal(5, snapshot.SchemaVersion);
            Assert.Equal([ImportResolution.AmbiguousOverlap, ImportResolution.Recorded], result.Resolutions);
            Assert.Null(await snapshot.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a"));
        }

        Assert.Equal(5L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
    }

    [Fact]
    public async Task SN4_ASnapshotDecidesAsTheImportWill()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("session-a", S1)]);
        await store.RecordImportedAsync([ImportEntries.Copilot("usage-0", "session-c", null, byok: true)], null);
        ImportedLedgerEntry[] entries =
        [
            ImportEntries.Copilot("usage-1", "session-a", S1),
            ImportEntries.Copilot("usage-2", "session-b", S2),
            ImportEntries.Copilot("usage-2", "session-b", S2),
            ImportEntries.Copilot(S2, "session-b", S2, ImportedRecordKind.Compaction),
            ImportEntries.Copilot("usage-3", "session-a", null, byok: true),
            ImportEntries.Copilot("usage-0", "session-c", null, byok: true),
        ];

        ImportRecordResult dry;
        await using (LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path))
        {
            dry = await snapshot.EvaluateAsync(entries);
        }

        ImportRecordResult real = await store.RecordImportedAsync(entries, null);

        Assert.Equal(
            [
                ImportResolution.OverlapSkipped,
                ImportResolution.Recorded,
                ImportResolution.Duplicate,
                ImportResolution.Duplicate,
                ImportResolution.AmbiguousOverlap,
                ImportResolution.Duplicate,
            ],
            real.Resolutions);
        Assert.Equal(real.Resolutions, dry.Resolutions);
    }

    [Fact]
    public async Task SN5_ANewerLedgerIsRejected()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        await new SqliteLedgerStore(path).InitializeAsync();
        await LedgerSql.ExecuteAsync(path, "PRAGMA user_version = 99;");
        SqliteConnection.ClearAllPools();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => LedgerSnapshot.OpenAsync(path));

        Assert.Equal("Database user_version 99 is newer than the supported version 6.", exception.Message);
    }

    [Fact]
    public async Task SN6_ASnapshotReadsCheckpoints()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path, new ManualTimeProvider(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero)));
        await store.InitializeAsync();
        ImportSessionCheckpoint checkpoint = new()
        {
            Source = ImportSourceKind.Codex,
            LogId = "archived/019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b",
            LogSizeBytes = 10,
            LogLastWriteTime = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
            Complete = true,
            Recorded = 0,
            Duplicates = 0,
            OverlapSkipped = 0,
            AmbiguousOverlap = 0,
        };
        await store.RecordImportedAsync([], checkpoint);

        await using LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path);

        Assert.Equal(
            checkpoint with { ImportedAt = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero) },
            await snapshot.GetImportSessionAsync(ImportSourceKind.Codex, checkpoint.LogId));
    }
}
```

### Tests

- **MP1**: a record maps to the exact span (pinned IDs, models as logged, no endpoint). **MP2** (2): a tool call maps to an `execute_tool` link in its owner's trace, status 0 or 2.
- **SN1**: without a ledger, nothing is created. **SN2**: an immutable read leaves the ledger's bytes and its missing `-wal` and `-shm` as they were. **SN3**: a version 5 ledger isn't migrated, and its live sessions defer their imports. **SN4**: a snapshot decides as the import then does. **SN5**: a newer ledger is rejected. **SN6**: a snapshot reads checkpoints.

### Acceptance criteria

- A dry run can't create, migrate, or write the ledger (SN1–SN3) and decides as the real run (SN4).
- `dotnet test CopilotLedger.slnx` passes **1197** tests (1188 + 9).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 9 specification: The Importer

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/Import/ImportRequest.cs` | New: `ImportSourceRequest`, `ImportRequest` |
| `src/CopilotLedger.Storage/Import/ImportReport.cs` | New: `ImportReport`, `ImportSourceReport` |
| `src/CopilotLedger.Storage/Import/LedgerImporter.cs` | New |
| `tests/CopilotLedger.Tests/Import/LedgerImporterTests.cs` | New: IR1–IR15 |

### Contracts

- `ImportSourceRequest(ISessionLogSource Source, string Home)`; `ImportRequest { DatabasePath, Sources, RepositoryRoot?, Since?, DryRun, IncludeActive, BatchSize = 500, BatchCommitted? }`.
- `ImportReport { DryRun, Sources, Diagnostics, Complete }`; `ImportSourceReport` has the counts of the table (sessions scanned, skipped unchanged, active, and out of scope, unsupported, aggregate-only, records parsed, out of scope, recorded, duplicates, overlap-skipped, ambiguous, malformed lines, unsupported shapes, cost out of range, skipped events, journal predecessors, unpriced, tool links) and the recorded requests' `AiCredits`, `AiCreditsUsd`, `FoundryByokUsd`, `LocalSavedUsd`, `ApiEquivalentUsd` (`null` when requests were recorded and none has a price), `ApiEquivalentUnpricedSpans`, and `ApiEquivalentEstimatedSpans`. `RecordsParsed` = out of scope + recorded + duplicates + overlap-skipped + ambiguous + unsupported shapes + cost out of range. `Complete` is false while a log is unsupported or has malformed lines, unsupported shapes, ambiguous or out-of-range records, or a journal predecessor.
- `LedgerImporter(ICreditCalculator, TimeProvider?)`, `RunAsync(ImportRequest, CancellationToken)`: throws `ArgumentException` for an invalid request (a Claude source, a duplicate source, a batch size outside 1 to 10,000, or a root that isn't a full path without a trailing separator), `InvalidOperationException` for a newer ledger, and the store's exceptions; committed batches stay.

### Algorithm

1. Open the store (`InitializeAsync`), or a `LedgerSnapshot` for a dry run.
2. For each source in `ImportSourceKind` order and each discovered log: count it; skip it when it's active (without `IncludeActive`), last written before `Since`, or checkpointed complete with the same size and last-write time (milliseconds).
3. Read it. A log that isn't `Read` is unsupported (aggregate-only counted too) and gets no checkpoint.
4. For each record: out of scope when it started before `Since`, or with `RepositoryRoot`, when its recorded root isn't that root (case-insensitive on Windows) or, without a recorded root, its working directory isn't the root or below it. Otherwise map it, price it with the calculator at its start time, and skip it as cost out of range when its cost, savings, or API-equivalent cost doesn't fit pico-USD. Usage records take their tool calls from the last 7 days as links.
5. Record the entries in batches of `BatchSize` (`RecordImportedAsync`, then `BatchCommitted`), or evaluate them on the snapshot; tally each resolution and the recorded amounts.
6. Add log-level `AmbiguousOverlap` and `CostOutOfRange` diagnostics, and, outside a dry run, write the log's checkpoint: complete only when nothing was malformed, unsupported, out of scope, out of range, deferred, or behind a journal link.

### Code (copy exactly)

New file `src/CopilotLedger.Storage/Import/ImportReport.cs`:

```csharp
using CopilotLedger.Core.Import;

namespace CopilotLedger.Storage.Import;

public sealed record ImportReport
{
    public required bool DryRun { get; init; }
    public required IReadOnlyList<ImportSourceReport> Sources { get; init; }       // in ImportSourceKind order
    public required IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; }     // by source, then log, then line
    public bool Complete => Sources.All(source => source.Complete);
}

// What one source's import read and decided. RecordsParsed = RecordsOutOfScope + Recorded + Duplicates + OverlapSkipped +
// AmbiguousOverlap + UnsupportedUsageShapes + CostOutOfRange. The amounts cover the Recorded requests only.
public sealed record ImportSourceReport
{
    public required ImportSourceKind Source { get; init; }
    public required long SessionsScanned { get; init; }             // every log discovered
    public required long SessionsSkippedUnchanged { get; init; }    // completely imported, and unchanged since
    public required long SessionsSkippedActive { get; init; }       // still open, without IncludeActive
    public required long SessionsSkippedOutOfScope { get; init; }   // last written before Since
    public required long SessionsUnsupported { get; init; }         // any Status other than Read
    public required long SessionsAggregateOnly { get; init; }       // of those, Copilot before 1.0.103
    public required long RecordsParsed { get; init; }
    public required long RecordsOutOfScope { get; init; }           // started before Since, or of another repository
    public required long Recorded { get; init; }
    public required long Duplicates { get; init; }
    public required long OverlapSkipped { get; init; }
    public required long AmbiguousOverlap { get; init; }
    public required long MalformedLines { get; init; }
    public required long UnsupportedUsageShapes { get; init; }
    public required long CostOutOfRange { get; init; }
    public required long SkippedEvents { get; init; }
    public required long JournalPredecessors { get; init; }
    public required long Unpriced { get; init; }                    // recorded with a status other than priced or local_offload
    public required long ToolLinksRecorded { get; init; }
    public required decimal AiCredits { get; init; }
    public required decimal AiCreditsUsd { get; init; }
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
    public required decimal? ApiEquivalentUsd { get; init; }        // null when requests were recorded and none has a price
    public required long ApiEquivalentUnpricedSpans { get; init; }
    public required long ApiEquivalentEstimatedSpans { get; init; } // priced without a reported cache counter

    public bool Complete =>
        SessionsUnsupported == 0 &&
        MalformedLines == 0 &&
        UnsupportedUsageShapes == 0 &&
        AmbiguousOverlap == 0 &&
        CostOutOfRange == 0 &&
        JournalPredecessors == 0;
}
```

New file `src/CopilotLedger.Storage/Import/ImportRequest.cs`:

```csharp
using CopilotLedger.Core.Import;

namespace CopilotLedger.Storage.Import;

public sealed record ImportSourceRequest(ISessionLogSource Source, string Home);

public sealed record ImportRequest
{
    public required string DatabasePath { get; init; }
    public required IReadOnlyList<ImportSourceRequest> Sources { get; init; }   // at most one per kind, never Claude
    public string? RepositoryRoot { get; init; }     // --repo: a fully qualified working-tree root
    public DateTimeOffset? Since { get; init; }      // --since; null for all time
    public bool DryRun { get; init; }
    public bool IncludeActive { get; init; }
    public int BatchSize { get; init; } = 500;       // 1 to 10,000 records per transaction
    public Action<int>? BatchCommitted { get; init; } // after each committed batch, with its size; never in a dry run
}
```

New file `src/CopilotLedger.Storage/Import/LedgerImporter.cs`:

```csharp
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage.Import;

// gh ledger import: reads each source's session logs, prices their records with the rates in use as of each record's start
// time, and records them through the live/import authority rules (SqliteLedgerStore.RecordImportedAsync), in batches. A dry
// run decides the same way on a read-only snapshot (LedgerSnapshot) and writes nothing.
public sealed class LedgerImporter
{
    // The daemon prunes span_links older than this; older tool calls aren't imported.
    public static readonly TimeSpan ToolLinkRetention = TimeSpan.FromDays(7);

    private readonly ICreditCalculator _calculator;
    private readonly TimeProvider _timeProvider;

    public LedgerImporter(ICreditCalculator calculator, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        _calculator = calculator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Throws ArgumentException for an invalid request, InvalidOperationException for a ledger newer than this gh-ledger,
    // OperationCanceledException, and the store's exceptions. Batches committed before an exception stay recorded; running
    // the import again resolves them as duplicates.
    public async Task<ImportReport> RunAsync(ImportRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        DateTimeOffset linkCutoff = _timeProvider.GetUtcNow() - ToolLinkRetention;
        SqliteLedgerStore? store = null;
        LedgerSnapshot? snapshot = null;
        if (request.DryRun)
        {
            snapshot = await LedgerSnapshot.OpenAsync(request.DatabasePath, cancellationToken);
        }
        else
        {
            store = new SqliteLedgerStore(request.DatabasePath, _timeProvider);
            await store.InitializeAsync(cancellationToken);
        }

        try
        {
            List<ImportSourceReport> reports = [];
            List<ImportDiagnostic> diagnostics = [];
            foreach (ImportSourceRequest source in request.Sources.OrderBy(source => source.Source.Kind))
            {
                Tally tally = new(source.Source.Kind);
                foreach (SessionLogLocation log in source.Source.Discover(source.Home))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ImportLogAsync(request, source.Source, log, store, snapshot, linkCutoff, tally, diagnostics, cancellationToken);
                }

                reports.Add(tally.ToReport());
            }

            return new ImportReport { DryRun = request.DryRun, Sources = reports, Diagnostics = diagnostics };
        }
        finally
        {
            if (snapshot is not null)
            {
                await snapshot.DisposeAsync();
            }
        }
    }

    private async Task ImportLogAsync(
        ImportRequest request,
        ISessionLogSource source,
        SessionLogLocation log,
        SqliteLedgerStore? store,
        LedgerSnapshot? snapshot,
        DateTimeOffset linkCutoff,
        Tally tally,
        List<ImportDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        tally.SessionsScanned++;
        if (log.IsActive && !request.IncludeActive)
        {
            tally.SessionsSkippedActive++;
            return;
        }

        if (request.Since is DateTimeOffset since && log.LastWriteTime < since)
        {
            tally.SessionsSkippedOutOfScope++;
            return;
        }

        ImportSessionCheckpoint? checkpoint = store is not null
            ? await store.GetImportSessionAsync(log.Source, log.LogId, cancellationToken)
            : await snapshot!.GetImportSessionAsync(log.Source, log.LogId, cancellationToken);
        if (checkpoint is { Complete: true } &&
            checkpoint.LogSizeBytes == log.SizeBytes &&
            checkpoint.LogLastWriteTime.ToUnixTimeMilliseconds() == log.LastWriteTime.ToUnixTimeMilliseconds())
        {
            tally.SessionsSkippedUnchanged++;
            return;
        }

        SessionLogReadResult result = await source.ReadAsync(log, cancellationToken);
        diagnostics.AddRange(result.Diagnostics);
        if (result.Status != SessionLogStatus.Read)
        {
            tally.SessionsUnsupported++;
            if (result.Status == SessionLogStatus.AggregateOnly)
            {
                tally.SessionsAggregateOnly++;
            }

            return;
        }

        long journal = result.Diagnostics.Count(item => item.Code == ImportDiagnosticCode.JournalPredecessor);
        tally.RecordsParsed += result.RecordsParsed;
        tally.MalformedLines += result.MalformedLines;
        tally.UnsupportedUsageShapes += result.UnsupportedUsageShapes;
        tally.SkippedEvents += result.SkippedEvents;
        tally.JournalPredecessors += journal;

        ILookup<string, ImportedToolCall> toolCalls = result.ToolCalls
            .Where(call => call.StartTime >= linkCutoff)
            .ToLookup(call => call.OwnerRecordKey, StringComparer.Ordinal);
        List<ImportedLedgerEntry> entries = [];
        long outOfScope = 0;
        long costOutOfRange = 0;
        foreach (ImportedUsageRecord record in result.Records)
        {
            if (IsOutOfScope(record, request))
            {
                outOfScope++;
                continue;
            }

            NormalizedGenAiSpan span = SessionImportMapper.ToSpan(record);
            CreditCalculationResult calculation;
            try
            {
                calculation = _calculator.Calculate(span);
                _ = PicoUsd.FromUsd(calculation.UsdCost);
                _ = PicoUsd.FromUsd(calculation.EstimatedCloudSavedUsd);
                _ = PicoUsd.FromUsd(calculation.ApiEquivalent.UsdCost ?? 0m);
            }
            catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
            {
                costOutOfRange++;
                continue;
            }

            entries.Add(new ImportedLedgerEntry
            {
                Source = record.Source,
                RecordKeySha256 = ImportIds.RecordKeySha256(record.Source, record.Kind, record.RecordKey),
                Span = span,
                Calculation = calculation,
                Git = record.Git,
                ToolCallCount = record.ToolCallCount,
                ToolLinks = record.Kind == ImportedRecordKind.Usage
                    ? [.. toolCalls[record.RecordKey].Select(call => SessionImportMapper.ToToolLink(call, record))]
                    : [],
            });
        }

        tally.RecordsOutOfScope += outOfScope;
        tally.CostOutOfRange += costOutOfRange;
        long recorded = 0;
        long duplicates = 0;
        long overlap = 0;
        long ambiguous = 0;
        for (int start = 0; start < entries.Count; start += request.BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<ImportedLedgerEntry> batch = entries.GetRange(start, Math.Min(request.BatchSize, entries.Count - start));
            ImportRecordResult resolved = store is not null
                ? await store.RecordImportedAsync(batch, null, cancellationToken)
                : await snapshot!.EvaluateAsync(batch, cancellationToken);
            if (store is not null)
            {
                request.BatchCommitted?.Invoke(batch.Count);
            }

            tally.ToolLinksRecorded += resolved.ToolLinksRecorded;
            for (int index = 0; index < batch.Count; index++)
            {
                switch (resolved.Resolutions[index])
                {
                    case ImportResolution.Recorded:
                        recorded++;
                        tally.AddRecorded(batch[index]);
                        break;
                    case ImportResolution.Duplicate:
                        duplicates++;
                        break;
                    case ImportResolution.OverlapSkipped:
                        overlap++;
                        break;
                    default:
                        ambiguous++;
                        break;
                }
            }
        }

        tally.Recorded += recorded;
        tally.Duplicates += duplicates;
        tally.OverlapSkipped += overlap;
        tally.AmbiguousOverlap += ambiguous;
        foreach ((ImportDiagnosticCode code, long count) in new[]
                 {
                     (ImportDiagnosticCode.AmbiguousOverlap, ambiguous),
                     (ImportDiagnosticCode.CostOutOfRange, costOutOfRange),
                 })
        {
            if (count > 0)
            {
                diagnostics.Add(new ImportDiagnostic
                {
                    Source = log.Source,
                    LogId = log.LogId,
                    Code = code,
                    Count = count,
                });
            }
        }

        // The checkpoint follows the log's last batch. A log is complete when every one of its records was resolved; a crash
        // before the checkpoint only makes the next run read the log again, and its records resolve as duplicates.
        if (store is not null)
        {
            await store.RecordImportedAsync(
                [],
                new ImportSessionCheckpoint
                {
                    Source = log.Source,
                    LogId = log.LogId,
                    LogSizeBytes = log.SizeBytes,
                    LogLastWriteTime = log.LastWriteTime,
                    Complete = result.MalformedLines == 0 &&
                               result.UnsupportedUsageShapes == 0 &&
                               journal == 0 &&
                               outOfScope == 0 &&
                               costOutOfRange == 0 &&
                               ambiguous == 0,
                    Recorded = recorded,
                    Duplicates = duplicates,
                    OverlapSkipped = overlap,
                    AmbiguousOverlap = ambiguous,
                },
                cancellationToken);
        }
    }

    // Out of scope: started before Since, or, with RepositoryRoot, recorded in another repository: by its Git root when the
    // log recorded one, else by its working directory (the root or below it).
    private static bool IsOutOfScope(ImportedUsageRecord record, ImportRequest request)
    {
        if (request.Since is DateTimeOffset since && record.StartTime < since)
        {
            return true;
        }

        if (request.RepositoryRoot is not string root)
        {
            return false;
        }

        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (record.Git?.RepositoryRoot is string recorded)
        {
            return !string.Equals(recorded, root, comparison);
        }

        string? directory = record.Git?.WorkingDirectory;
        return directory is null ||
               !(string.Equals(directory, root, comparison) ||
                 directory.StartsWith(root + Path.DirectorySeparatorChar, comparison));
    }

    private static void Validate(ImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.DatabasePath, nameof(request));
        if (request.BatchSize is < 1 or > 10_000)
        {
            throw new ArgumentException("BatchSize must be 1 to 10,000.", nameof(request));
        }

        if (request.Sources.Any(source => source.Source.Kind == ImportSourceKind.Claude) ||
            request.Sources.Select(source => source.Source.Kind).Distinct().Count() != request.Sources.Count)
        {
            throw new ArgumentException("Sources must be Copilot or Codex, at most one of each.", nameof(request));
        }

        if (request.RepositoryRoot is string root &&
            (!Path.IsPathFullyQualified(root) || Path.TrimEndingDirectorySeparator(root) != root))
        {
            throw new ArgumentException("RepositoryRoot must be a full path without a trailing separator.", nameof(request));
        }
    }

    // One source's counts and amounts as they're added up.
    private sealed class Tally(ImportSourceKind source)
    {
        private decimal _apiEquivalentUsd;
        private bool _apiEquivalentPriced;

        public long SessionsScanned { get; set; }
        public long SessionsSkippedUnchanged { get; set; }
        public long SessionsSkippedActive { get; set; }
        public long SessionsSkippedOutOfScope { get; set; }
        public long SessionsUnsupported { get; set; }
        public long SessionsAggregateOnly { get; set; }
        public long RecordsParsed { get; set; }
        public long RecordsOutOfScope { get; set; }
        public long Recorded { get; set; }
        public long Duplicates { get; set; }
        public long OverlapSkipped { get; set; }
        public long AmbiguousOverlap { get; set; }
        public long MalformedLines { get; set; }
        public long UnsupportedUsageShapes { get; set; }
        public long CostOutOfRange { get; set; }
        public long SkippedEvents { get; set; }
        public long JournalPredecessors { get; set; }
        public long ToolLinksRecorded { get; set; }
        private long Unpriced { get; set; }
        private decimal AiCredits { get; set; }
        private decimal AiCreditsUsd { get; set; }
        private decimal FoundryByokUsd { get; set; }
        private decimal LocalSavedUsd { get; set; }
        private long ApiEquivalentUnpricedSpans { get; set; }
        private long ApiEquivalentEstimatedSpans { get; set; }

        public void AddRecorded(ImportedLedgerEntry entry)
        {
            CreditCalculationResult calculation = entry.Calculation;
            if (calculation.Status is not (PricingStatus.Priced or PricingStatus.LocalOffload))
            {
                Unpriced++;
            }

            AiCredits += calculation.AiCredits;
            AiCreditsUsd += calculation.Channel == BillingChannel.GitHubAiCredits ? calculation.UsdCost : 0m;
            FoundryByokUsd += calculation.Channel == BillingChannel.FoundryByok ? calculation.UsdCost : 0m;
            LocalSavedUsd += calculation.EstimatedCloudSavedUsd;
            if (calculation.ApiEquivalent.UsdCost is decimal usd)
            {
                _apiEquivalentPriced = true;
                _apiEquivalentUsd += usd;
                if (!CacheMetrics.IsReported(entry.Span.CacheReadState, entry.Span.CacheWriteState))
                {
                    ApiEquivalentEstimatedSpans++;
                }
            }
            else
            {
                ApiEquivalentUnpricedSpans++;
            }
        }

        public ImportSourceReport ToReport() => new()
        {
            Source = source,
            SessionsScanned = SessionsScanned,
            SessionsSkippedUnchanged = SessionsSkippedUnchanged,
            SessionsSkippedActive = SessionsSkippedActive,
            SessionsSkippedOutOfScope = SessionsSkippedOutOfScope,
            SessionsUnsupported = SessionsUnsupported,
            SessionsAggregateOnly = SessionsAggregateOnly,
            RecordsParsed = RecordsParsed,
            RecordsOutOfScope = RecordsOutOfScope,
            Recorded = Recorded,
            Duplicates = Duplicates,
            OverlapSkipped = OverlapSkipped,
            AmbiguousOverlap = AmbiguousOverlap,
            MalformedLines = MalformedLines,
            UnsupportedUsageShapes = UnsupportedUsageShapes,
            CostOutOfRange = CostOutOfRange,
            SkippedEvents = SkippedEvents,
            JournalPredecessors = JournalPredecessors,
            Unpriced = Unpriced,
            ToolLinksRecorded = ToolLinksRecorded,
            AiCredits = AiCredits,
            AiCreditsUsd = AiCreditsUsd,
            FoundryByokUsd = FoundryByokUsd,
            LocalSavedUsd = LocalSavedUsd,
            ApiEquivalentUsd = Recorded == 0 ? 0m : _apiEquivalentPriced ? _apiEquivalentUsd : null,
            ApiEquivalentUnpricedSpans = ApiEquivalentUnpricedSpans,
            ApiEquivalentEstimatedSpans = ApiEquivalentEstimatedSpans,
        };
    }
}
```

New file `tests/CopilotLedger.Tests/Import/LedgerImporterTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Storage.Import;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Import;

public sealed class LedgerImporterTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private const string S4 = "44444444-4444-4444-8444-444444444444";
    private const string Thread = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IR1_ASecondRunSkipsUnchangedSessions()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-a").Usage("u1", S1).Usage("u2", S2).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-b").Usage("u3", S3).Write(CopilotHome(temp));

        ImportSourceReport first = Copilot(await RunAsync(temp));
        ImportSourceReport second = Copilot(await RunAsync(temp));

        Assert.Equal((2, 0, 3, 3), (first.SessionsScanned, first.SessionsSkippedUnchanged, first.RecordsParsed, first.Recorded));
        Assert.Equal((2, 2, 0, 0), (second.SessionsScanned, second.SessionsSkippedUnchanged, second.RecordsParsed, second.Recorded));
        Assert.True(first.Complete);
        Assert.Equal(
            first.RecordsParsed,
            first.RecordsOutOfScope + first.Recorded + first.Duplicates + first.OverlapSkipped + first.AmbiguousOverlap
            + first.UnsupportedUsageShapes + first.CostOutOfRange);
        Assert.Equal(3L, await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"));
    }

    [Theory]
    [InlineData("append", 1, 2, 3)]
    [InlineData("truncate", 0, 1, 3)]
    [InlineData("replace", 2, 0, 4)]
    [InlineData("rewrite", 0, 2, 2)]
    public async Task IR2_ChangedSessionsConverge(string change, long recorded, long duplicates, long rows)
    {
        using TempDirectory temp = new();
        string path = Log(change == "truncate" ? ["u1", "u2", "u3"] : ["u1", "u2"]).Write(CopilotHome(temp));
        await RunAsync(temp);
        DateTimeOffset written = new(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        string[] keys = change switch
        {
            "append" => ["u1", "u2", "u3"],
            "truncate" => ["u1"],
            "replace" => ["u3", "u4"],
            _ => ["u1", "u2"],
        };
        Log(keys).Write(CopilotHome(temp));
        File.SetLastWriteTimeUtc(path, written.UtcDateTime.AddMinutes(1));

        ImportSourceReport second = Copilot(await RunAsync(temp));
        ImportSourceReport third = Copilot(await RunAsync(temp));

        Assert.Equal((recorded, duplicates), (second.Recorded, second.Duplicates));
        Assert.Equal(1, third.SessionsSkippedUnchanged);
        Assert.Equal(rows, await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"));

        static CopilotSessionLogs Log(string[] keys)
        {
            CopilotSessionLogs log = new("s-a");
            foreach (string key in keys)
            {
                log.Usage(key, $"{key[1]}{key[1]}{key[1]}{key[1]}0000-0000-4000-8000-000000000000");
            }

            return log;
        }
    }

    [Fact]
    public async Task IR3_ARunCancelledAfterItsFirstBatchConvergesWhenRunAgain()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new("s-a");
        for (int index = 1; index <= 5; index++)
        {
            log.Usage($"u{index}", $"{index}{index}{index}{index}0000-0000-4000-8000-000000000000");
        }

        log.Write(CopilotHome(temp));
        using CancellationTokenSource cancel = new();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(
            temp,
            request => request with { BatchSize = 2, BatchCommitted = _ => cancel.Cancel() },
            cancel.Token));
        long afterCancel = (long)(await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"))!;
        ImportSourceReport rerun = Copilot(await RunAsync(temp, request => request with { BatchSize = 2 }));

        Assert.Equal(2, afterCancel);
        Assert.Equal((3, 2), (rerun.Recorded, rerun.Duplicates));
        Assert.True(rerun.Complete);
        Assert.Equal(5L, await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"));
    }

    [Fact]
    public async Task IR4_SinceSkipsOldSessionsAndOldRecords()
    {
        using TempDirectory temp = new();
        string old = new CopilotSessionLogs("s-old", start: new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero))
            .Usage("u1", S1)
            .Write(CopilotHome(temp));
        File.SetLastWriteTimeUtc(old, new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
        CopilotSessionLogs mixed = new("s-mixed", start: new DateTimeOffset(2026, 11, 19, 23, 59, 59, TimeSpan.Zero));
        mixed.Usage("u2", S2).Usage("u3", S3).Write(CopilotHome(temp));

        ImportSourceReport report = Copilot(await RunAsync(
            temp,
            request => request with { Since = new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero) }));

        Assert.Equal(1, report.SessionsSkippedOutOfScope);
        Assert.Equal((2, 1, 1), (report.RecordsParsed, report.RecordsOutOfScope, report.Recorded));
        Assert.True(report.Complete);
        Assert.False((await (await StoreAsync(temp)).GetImportSessionAsync(ImportSourceKind.Copilot, "s-mixed"))!.Complete);
    }

    [Fact]
    public async Task IR5_RepoImportsOnlyThatRepositorysRecords()
    {
        using TempDirectory temp = new();
        string root = Path.Combine(temp.Path, "webapp");
        string other = Path.Combine(temp.Path, "other");
        new CopilotSessionLogs("s-root", context: CopilotSessionLogs.Context(root, root, "main", "contoso/webapp"))
            .Usage("u1", S1)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-cwd", context: CopilotSessionLogs.Context(Path.Combine(root, "src")))
            .Usage("u2", S2)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-other", context: CopilotSessionLogs.Context(other, other, "main", "contoso/other"))
            .Usage("u3", S3)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-moved", context: CopilotSessionLogs.Context(root, root, "main", "contoso/webapp"))
            .Usage("u4", S4)
            .Resume(CopilotSessionLogs.Context(other, other, "main", "contoso/other"))
            .Usage("u5", "55555555-5555-4555-8555-555555555555")
            .Write(CopilotHome(temp));

        ImportSourceReport report = Copilot(await RunAsync(temp, request => request with { RepositoryRoot = root }));

        Assert.Equal((5, 2, 3), (report.RecordsParsed, report.RecordsOutOfScope, report.Recorded));
        Assert.Equal(
            ["null", "contoso/webapp", "contoso/webapp"],
            await LedgerSql.RowsAsync(Database(temp), "SELECT COALESCE(repo_slug, 'null') FROM spans ORDER BY id;"));
    }

    [Fact]
    public async Task IR6_ActiveSessionsNeedIncludeActive()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-a").Usage("u1", S1).Write(CopilotHome(temp), active: true);

        ImportSourceReport skipped = Copilot(await RunAsync(temp));
        ImportSourceReport included = Copilot(await RunAsync(temp, request => request with { IncludeActive = true }));

        Assert.Equal((1, 0), (skipped.SessionsSkippedActive, skipped.Recorded));
        Assert.True(skipped.Complete);
        Assert.Equal((0, 1), (included.SessionsSkippedActive, included.Recorded));
    }

    [Fact]
    public async Task IR7_RequestsArePricedWithTheCurrentRatesAtTheirStartTime()
    {
        using TempDirectory temp = new();
        (string Session, DateTimeOffset Start, string Model, string Id)[] requests =
        [
            ("s-1", new DateTimeOffset(2026, 11, 30, 12, 0, 0, TimeSpan.Zero), "gemini-4-argon", S1),
            ("s-2", new DateTimeOffset(2026, 12, 2, 12, 0, 0, TimeSpan.Zero), "gemini-4-argon", S2),
            ("s-3", new DateTimeOffset(2027, 5, 16, 12, 0, 0, TimeSpan.Zero), "mai-code-2", S3),
            ("s-4", new DateTimeOffset(2027, 5, 17, 12, 0, 0, TimeSpan.Zero), "mai-code-2", S4),
        ];
        foreach ((string session, DateTimeOffset start, string model, string id) in requests)
        {
            new CopilotSessionLogs(session, start: start).Usage("u-" + session, id, model: model).Write(CopilotHome(temp));
        }

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal(
            [
                "gemini-4-argon|priced|3000000000",
                "gemini-4-argon|no_rate_for_date|0",
                "mai-code-2|priced|750000000",
                "mai-code-2|priced|1500000000",
            ],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT model_id, pricing_status, cost_usd_pico FROM spans ORDER BY start_time_unix_ms;"));
        Assert.Equal(1, report.Unpriced);
        Assert.Equal(0.00525m, report.AiCreditsUsd);
        Assert.Equal(0.525m, report.AiCredits);
    }

    [Fact]
    public async Task IR8_ToolCallsAreImportedOnlyWithinTheLinkRetention()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-old", start: new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero))
            .Usage("u-old", S1, numToolCalls: 3)
            .ToolStart("toolu-old", "bash")
            .ToolComplete("toolu-old", false, exitCode: 1)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-new", start: new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero))
            .Usage("u-new", S2, numToolCalls: 1)
            .ToolStart("toolu-new", "bash")
            .ToolComplete("toolu-new", false, exitCode: 2)
            .Write(CopilotHome(temp));

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal(1, report.ToolLinksRecorded);
        Assert.Equal(
            ["1|bash|2|2"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT owner_span_row_id, tool_name, tool_exit_code, status_code FROM span_links;"));
        Assert.Equal(
            ["s-new|1", "s-old|3"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT conversation_id, imported_tool_call_count FROM spans ORDER BY conversation_id;"));
    }

    [Fact]
    public async Task IR9_ADryRunReportsWhatTheImportDoesAndWritesNothing()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("s-a", S1), ImportEntries.Live("s-b", null)]);
        SqliteConnection.ClearAllPools();
        new CopilotSessionLogs("s-a").Usage("u1", S1).Usage("u2", S2).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-b").Usage("u3", S3).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-c").Usage("u4", S4).Usage("u4", S4).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-d").Usage("u4", S4).Raw("{oops").Write(CopilotHome(temp));
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(Database(temp)));

        ImportReport dry = await RunAsync(temp, request => request with { DryRun = true });

        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(Database(temp))));
        Assert.False(File.Exists(Database(temp) + "-wal"));
        Assert.False(File.Exists(Database(temp) + "-shm"));
        ImportReport real = await RunAsync(temp);
        ImportSourceReport copilot = Copilot(real);
        Assert.Equal(
            (2, 2, 1, 1, 1),
            (copilot.Recorded, copilot.Duplicates, copilot.OverlapSkipped, copilot.AmbiguousOverlap, copilot.MalformedLines));
        Assert.True(dry.DryRun);
        Assert.Equal(real.Sources, dry.Sources);
        Assert.Equal(real.Diagnostics, dry.Diagnostics);
    }

    [Fact]
    public async Task IR10_ACodexResponseInSessionsAndArchivedSessionsIsChargedOnce()
    {
        using TempDirectory temp = new();
        CodexRollouts rollout = new CodexRollouts(Thread).TurnContext("turn-1", "gpt-6.1-sol").Usage("resp_1", "turn-1");
        rollout.Write(CodexHome(temp));
        rollout.Write(CodexHome(temp), archived: true);

        ImportReport report = await RunAsync(temp);

        ImportSourceReport codex = Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Codex);
        Assert.Equal((2, 1, 1), (codex.SessionsScanned, codex.Recorded, codex.Duplicates));
        Assert.Equal(
            ["import:codex|unpriced|other_provider|priced"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT ingest_source, billing_channel, pricing_status, api_equivalent_status FROM spans;"));
    }

    [Fact]
    public async Task IR11_ARecordWhoseCostIsOutOfRangeIsSkippedAndReported()
    {
        using TempDirectory temp = new();
        string overlay = Path.Combine(temp.Path, "rates.json");
        await File.WriteAllTextAsync(
            overlay,
            """
            {
              "schemaVersion": 2,
              "ratesVersion": "ir11",
              "apiEquivalent": {
                "models": [
                  { "id": "ir11-huge", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1000000, "outputPer1M": 1000000 } ] } ] }
                ]
              }
            }
            """);
        new CopilotSessionLogs("s-a")
            .Usage("u1", S1, model: "ir11-huge", input: 10_000_000)
            .Usage("u2", S2)
            .Write(CopilotHome(temp));

        ImportReport report = await RunAsync(temp, calculator: JsonRateCardCalculator.LoadEmbeddedWithOverlay(overlay));

        ImportSourceReport copilot = Copilot(report);
        Assert.Equal((1, 1), (copilot.CostOutOfRange, copilot.Recorded));
        Assert.False(copilot.Complete);
        ImportDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.CostOutOfRange, diagnostic.Code);
        Assert.Equal("s-a", diagnostic.LogId);
        Assert.Equal(1, diagnostic.Count);
    }

    [Fact]
    public async Task IR12_IncompleteSessionsAreReadAgainAndUnsupportedOnesGetNoCheckpoint()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-new", "1.0.120").Usage("u1", S1).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-bad").Usage("u2", S2).Raw("{oops").Write(CopilotHome(temp));

        ImportSourceReport first = Copilot(await RunAsync(temp));
        ImportSourceReport second = Copilot(await RunAsync(temp));

        Assert.Equal((1, 1, 1), (first.SessionsUnsupported, first.MalformedLines, first.Recorded));
        Assert.False(first.Complete);
        Assert.Equal((0, 1, 1), (second.SessionsSkippedUnchanged, second.Duplicates, second.SessionsUnsupported));
        Assert.Null(await (await StoreAsync(temp)).GetImportSessionAsync(ImportSourceKind.Copilot, "s-new"));
    }

    [Fact]
    public async Task IR13_ASessionWithoutARecordedBranchKeepsNone()
    {
        using TempDirectory temp = new();
        string repo = GitFixture.Create(temp.Path);
        new CopilotSessionLogs("s-a", context: new JsonObject { ["cwd"] = repo, ["gitRoot"] = repo })
            .Usage("u1", S1)
            .Write(CopilotHome(temp));

        await RunAsync(temp, request => request with { RepositoryRoot = repo });

        Assert.Equal(
            [$"{repo}|null|null"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT repo_root, COALESCE(branch_name, 'null'), COALESCE(head_commit_sha, 'null') FROM spans;"));
    }

    [Fact]
    public async Task IR14_OldSessionsAreCountedAsAggregateOnly()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-old", "1.0.98").Usage("u1", S1).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-new").Usage("u2", S2).Write(CopilotHome(temp));

        ImportReport report = await RunAsync(temp);

        ImportSourceReport copilot = Copilot(report);
        Assert.Equal((1, 1, 1), (copilot.SessionsUnsupported, copilot.SessionsAggregateOnly, copilot.Recorded));
        Assert.False(report.Complete);
        ImportDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.AggregateOnlySession, diagnostic.Code);
        Assert.Equal("1.0.98", diagnostic.Version);
    }

    // Copilot 1.0.114 records the dated response model claude-haiku-4-5-20251001 for claude-haiku-4.5. Both models reach the
    // card as reported, so the record is priced exactly as its live span is.
    [Fact]
    public async Task IR15_ARecordIsPricedAsItsLiveSpanIs()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-a", "1.0.114")
            .Usage(
                "u1",
                S1,
                model: "claude-haiku-4.5",
                responseModel: "claude-haiku-4-5-20251001",
                input: 100_000,
                output: 10_000,
                cacheRead: 40_000,
                cacheWrite: 20_000)
            .Write(CopilotHome(temp));
        CreditCalculationResult live = JsonRateCardCalculator.LoadEmbedded().Calculate(TestSpans.Chat(
            startTime: new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
            providerName: "github",
            requestModel: "claude-haiku-4.5",
            responseModel: "claude-haiku-4-5-20251001",
            serverAddress: "api.business.githubcopilot.com",
            inputTokens: 100_000,
            outputTokens: 10_000,
            cacheReadInputTokens: 40_000,
            cacheWriteInputTokens: 20_000));

        await RunAsync(temp);

        Assert.Equal(
            ("claude-haiku-4.5", PricingStatus.Priced, 0.119m, 11.9m, 0.119m),
            (live.ModelId, live.Status, live.UsdCost, live.AiCredits, live.ApiEquivalent.UsdCost));
        Assert.Equal(
            ["claude-haiku-4.5|claude-haiku-4.5|claude-haiku-4-5-20251001|github_ai_credits|priced|119000000000|priced|119000000000"],
            await LedgerSql.RowsAsync(
                Database(temp),
                """
                SELECT model_id, request_model, response_model, billing_channel, pricing_status, cost_usd_pico,
                       api_equivalent_status, api_equivalent_usd_pico
                FROM spans;
                """));
    }

    private static string CopilotHome(TempDirectory temp) => Path.Combine(temp.Path, "copilot");

    private static string CodexHome(TempDirectory temp) => Path.Combine(temp.Path, "codex");

    private static string Database(TempDirectory temp) => Path.Combine(temp.Path, "ledger.db");

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Database(temp));
        await store.InitializeAsync();
        return store;
    }

    private static ImportSourceReport Copilot(ImportReport report) =>
        Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Copilot);

    private static Task<ImportReport> RunAsync(
        TempDirectory temp,
        Func<ImportRequest, ImportRequest>? change = null,
        CancellationToken cancellationToken = default,
        JsonRateCardCalculator? calculator = null)
    {
        ImportRequest request = new()
        {
            DatabasePath = Database(temp),
            Sources =
            [
                new ImportSourceRequest(new CopilotSessionLogSource(), CopilotHome(temp)),
                new ImportSourceRequest(new CodexSessionLogSource(), CodexHome(temp)),
            ],
        };
        return new LedgerImporter(calculator ?? JsonRateCardCalculator.LoadEmbedded(), new ManualTimeProvider(Now))
            .RunAsync(change is null ? request : change(request), cancellationToken);
    }
}
```

### Tests

- **IR1**: a second run records nothing, explained by unchanged sessions, and the counts add up. **IR2** (4): appending, truncating, replacing, and a same-size rewrite converge. **IR3**: a run cancelled after its first batch converges when run again. **IR4**: `Since` skips old logs and old records. **IR5**: `RepositoryRoot` matches the recorded root, else a working directory below it, and a resume into another repository is out of scope. **IR6**: active sessions need `IncludeActive`.
- **IR7**: requests are priced with the current rates at their start time; one outside every window is `no_rate_for_date` (1 unpriced, $0.00525 GitHub USD, 0.525 AI credits). **IR8**: tool calls only within 7 days; `numToolCalls` never becomes links. **IR9**: a dry run reports exactly what the real run then does, and leaves the ledger's hash and missing `-wal` and `-shm` as they were. **IR10**: a Codex response in `sessions` and `archived_sessions` is charged once (`other_provider`, priced at API prices). **IR11**: a record whose cost is out of range is skipped and reported. **IR12**: incomplete sessions are read again, and unsupported ones get no checkpoint. **IR13**: a session without a recorded branch keeps none while the working tree is on another branch. **IR14**: old sessions are counted as aggregate-only. **IR15**: a 1.0.114 record with the dated response model `claude-haiku-4-5-20251001` is stored as `claude-haiku-4.5`, priced, 119,000,000,000 pico-USD on both lenses, as its live span is ($0.119, 11.9 AI credits).

### Acceptance criteria

- Re-running converges without double charges (IR1–IR3, IR9, IR10); the scope options work (IR4–IR6); prices are historical with the current card (IR7, IR15).
- `dotnet test CopilotLedger.slnx` passes **1215** tests (1197 + 18).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 10 specification: `gh ledger import`

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Import/ImportCommands.cs` | New: the command |
| `src/CopilotLedger.Cli/GhLedgerCli.cs` | Registers it after `export-pr` |
| `src/CopilotLedger.Cli/CliContext.cs` | New required `CodexHome`; `CreateDefault` sets `CodexSessionLogSource.DefaultHome` |
| `src/CopilotLedger.Cli/Daemon/IngestFailureLog.cs` | `public`; messages of database and file errors only |
| `tests/CopilotLedger.Tests/TestSupport/CliHarness.cs` | `CodexHome` = `<root>/codex-home` |
| `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs` | New: IC1–IC11 |

### Usage and validation

```text
gh ledger import (--repo <path> | --all) [--source copilot|codex]... [--since <period>] [--dry-run] [--include-active] [--json]
```

| Condition | Message (standard error, exit code 1) |
|---|---|
| Neither `--repo` nor `--all` | `Use --repo <path> or --all.` |
| Both | `Use either --repo or --all, not both.` |
| `--repo` blank | `--repo can't be blank.` |
| `--source claude` | `Claude Code import isn't supported: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it.` |
| Another `--source` | `--source must be copilot or codex.` |
| Invalid `--since` | `Invalid --since value <value>. Use today, all, <N>d (1-3650), or yyyy-MM-dd.` |
| `--repo` outside a repository | `<full path> isn't in a Git repository.` |
| Invalid overlay | `Invalid rates overlay: <problem>` |
| `SqliteException` | `Import stopped: SQLite error <code>. Requests recorded before it are kept; run the import again.` (a dry run: `Dry run stopped: SQLite error <code>.`) |
| Newer ledger | The `InvalidOperationException` message |

`--source` defaults to `copilot` and can repeat; `--since` defaults to `all`. Warnings, before any output: ignored downloaded rates, and `Warning: the daemon runs gh-ledger <version>; restart it so live spans follow this version's import rules.` when the daemon holds its lock and `daemon.json` names another version.

### Output

- Text: `Import from local session logs[ for <slug or root>] (<period>[, dry run])`, a blank line, the table (an unlabeled first column; one right-aligned column per source; rows Sessions scanned, Skipped unchanged, Skipped active, Skipped out of scope, Unsupported sessions, Records parsed, Out of scope, Recorded, Duplicate, Overlap-skipped, Ambiguous-overlap, Malformed lines, Unsupported usage shapes, Cost out of range, Unpriced, Coverage), a blank line, then `Recorded <n> request(s): <credits> AI credits (<USD>), API equivalent <cell>.` (dry run: `Importing would record …`), `Foundry BYOK: … Local saved: ….` when either isn't zero, the API-equivalent footnotes when the cell has `*` or `~`, or `Nothing new to record.` (dry run: `There's nothing new to record.`); `Dry run: nothing was written.`; and, with incomplete coverage, `Coverage is incomplete; see the warnings.` or, when old sessions are the only gap, `Coverage is incomplete only because <n> session(s) predate(s) Copilot 1.0.103; use --since to leave it/them out.`
- Warnings on standard error, at most 20, then `Warning: <n> more warning(s); run with --json to see them all.`: `Warning: <Copilot session|Codex rollout> <log ID>[, line <n> (byte <offset>)]: <message>`, with a fixed message per code.
- JSON: `dryRun`, `period`, `from`, `repository`, `repositoryRoot`, `coverage`, `sources` (each with `source`, `coverage`, every count, and the seven amount fields), `totals` (`recorded` and the amounts), and `diagnostics` (`source`, `log`, `code`, `event`, `field`, `version`, `count`, `line`, `byteOffset`).
- Exit code 0 when every source's coverage is complete, else 2.

### Code (copy exactly)

Edit 1 of 4 in `src/CopilotLedger.Cli/CliContext.cs`, replace:

```csharp
using CopilotLedger.Core;
using CopilotLedger.Otlp.Attribution;
using Microsoft.AspNetCore.Hosting;
```

with:

```csharp
using CopilotLedger.Core;
using CopilotLedger.Core.Import;
using CopilotLedger.Otlp.Attribution;
using Microsoft.AspNetCore.Hosting;
```

Edit 2 of 4 in `src/CopilotLedger.Cli/CliContext.cs`, replace:

```csharp
    public required string DataDirectory { get; init; }
    public required string CopilotHome { get; init; }
    public required string WorkingDirectory { get; init; }
    public required TimeProvider TimeProvider { get; init; }
```

with:

```csharp
    public required string DataDirectory { get; init; }
    public required string CopilotHome { get; init; }
    public required string CodexHome { get; init; }                                     // import --source codex
    public required string WorkingDirectory { get; init; }
    public required TimeProvider TimeProvider { get; init; }
```

Edit 3 of 4 in `src/CopilotLedger.Cli/CliContext.cs`, replace:

```csharp
    public string RatesOverlayPath => Path.Combine(DataDirectory, "rates.json");

    // DataDirectory: COPILOT_LEDGER_HOME when set and not blank, else LedgerPaths.DataDirectory.
    // CopilotHome: CopilotSessionWorkspaceLocator.DefaultCopilotHome. WorkingDirectory: Environment.CurrentDirectory.
    // TimeProvider: TimeProvider.System. Platform: new SystemCliPlatform().
    public static CliContext CreateDefault(TextWriter output, TextWriter error)
    {
```

with:

```csharp
    public string RatesOverlayPath => Path.Combine(DataDirectory, "rates.json");

    // DataDirectory: COPILOT_LEDGER_HOME when set and not blank, else LedgerPaths.DataDirectory.
    // CopilotHome: CopilotSessionWorkspaceLocator.DefaultCopilotHome. CodexHome: CodexSessionLogSource.DefaultHome.
    // WorkingDirectory: Environment.CurrentDirectory. TimeProvider: TimeProvider.System. Platform: new SystemCliPlatform().
    public static CliContext CreateDefault(TextWriter output, TextWriter error)
    {
```

Edit 4 of 4 in `src/CopilotLedger.Cli/CliContext.cs`, replace:

```csharp
                : overridePath,
            CopilotHome = CopilotSessionWorkspaceLocator.DefaultCopilotHome,
            WorkingDirectory = Environment.CurrentDirectory,
            TimeProvider = TimeProvider.System,
```

with:

```csharp
                : overridePath,
            CopilotHome = CopilotSessionWorkspaceLocator.DefaultCopilotHome,
            CodexHome = CodexSessionLogSource.DefaultHome,
            WorkingDirectory = Environment.CurrentDirectory,
            TimeProvider = TimeProvider.System,
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Daemon/IngestFailureLog.cs`, replace:

```csharp
namespace CopilotLedger.Cli.Daemon;

// Writes "Warning: could not record spans: <type>: <message>" for the first ingestion failure, then at most once a
// minute, so a ledger that stays unavailable can't flood daemon.log. After the clock steps back, the next failure is
// written at once. A line that can't be written is dropped.
internal sealed class IngestFailureLog(Action<string> write, TimeProvider timeProvider)
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
```

with:

```csharp
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Cli.Daemon;

// Writes "Warning: could not record spans: <type>: <message>" for the first ingestion failure, then at most once a
// minute, so a ledger that stays unavailable can't flood daemon.log. Only SQLite and file-system exceptions keep their
// message; any other is written as "Warning: could not record spans: <type>", because its message can quote the request
// (a JsonException's does). After the clock steps back, the next failure is written at once. A line that can't be
// written is dropped.
public sealed class IngestFailureLog(Action<string> write, TimeProvider timeProvider)
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Daemon/IngestFailureLog.cs`, replace:

```csharp
        try
        {
            write($"Warning: could not record spans: {exception.GetType().Name}: {exception.Message}");
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
```

with:

```csharp
        try
        {
            write(exception is SqliteException or IOException or UnauthorizedAccessException
                ? $"Warning: could not record spans: {exception.GetType().Name}: {exception.Message}"
                : $"Warning: could not record spans: {exception.GetType().Name}");
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
```

Edit 1 of 2 in `src/CopilotLedger.Cli/GhLedgerCli.cs`, replace:

```csharp
using CopilotLedger.Cli.Breaker;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Install;
using CopilotLedger.Cli.Mcp;
```

with:

```csharp
using CopilotLedger.Cli.Breaker;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Import;
using CopilotLedger.Cli.Install;
using CopilotLedger.Cli.Mcp;
```

Edit 2 of 2 in `src/CopilotLedger.Cli/GhLedgerCli.cs`, replace:

```csharp
        }

        root.Add(ReceiptCommands.Create(context));
        root.Add(McpCommand.Create(context));

        return root.Parse(args).InvokeAsync(
```

with:

```csharp
        }

        root.Add(ReceiptCommands.Create(context));
        root.Add(ImportCommands.Create(context));
        root.Add(McpCommand.Create(context));

        return root.Parse(args).InvokeAsync(
```

New file `src/CopilotLedger.Cli/Import/ImportCommands.cs`:

```csharp
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage.Import;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Cli.Import;

// gh ledger import: records past requests from local Copilot and Codex session logs (LedgerImporter) and reports, per
// source, what it read and decided. Exit code 0 when every source's coverage is complete, 2 when the import finished with
// some coverage incomplete, and 1 for invalid usage or a fatal error. Nothing from a log reaches the output except
// validated log IDs and versions, counts, and the parsers' own field names.
internal static class ImportCommands
{
    public const string ClaudeUnsupported =
        "Claude Code import isn't supported: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it.";

    private const int Incomplete = 2;
    private const int MaxWarnings = 20;

    private static readonly JsonWriterOptions JsonOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The table's rows after the header: a label and each source's count.
    private static readonly (string Label, Func<ImportSourceReport, long> Count)[] Rows =
    [
        ("Sessions scanned", report => report.SessionsScanned),
        ("Skipped, unchanged", report => report.SessionsSkippedUnchanged),
        ("Skipped, active", report => report.SessionsSkippedActive),
        ("Skipped, out of scope", report => report.SessionsSkippedOutOfScope),
        ("Unsupported sessions", report => report.SessionsUnsupported),
        ("Records parsed", report => report.RecordsParsed),
        ("Out of scope", report => report.RecordsOutOfScope),
        ("Recorded", report => report.Recorded),
        ("Duplicate", report => report.Duplicates),
        ("Overlap-skipped", report => report.OverlapSkipped),
        ("Ambiguous-overlap", report => report.AmbiguousOverlap),
        ("Malformed lines", report => report.MalformedLines),
        ("Unsupported usage shapes", report => report.UnsupportedUsageShapes),
        ("Cost out of range", report => report.CostOutOfRange),
        ("Unpriced", report => report.Unpriced),
    ];

    public static Command Create(CliContext context)
    {
        Command command = new("import", "Record past requests from local Copilot and Codex session logs.");
        Option<string?> repository = new("--repo")
        {
            Description = "Import only requests from the Git repository at this path.",
        };
        Option<bool> all = new("--all")
        {
            Description = "Import requests from every repository.",
        };
        Option<string[]> sources = new("--source")
        {
            Description = "Session logs to read: copilot (the default) or codex. Repeat it for both.",
        };
        Option<string> since = new("--since")
        {
            Description = "Period: today, all, Nd, or yyyy-MM-dd.",
            DefaultValueFactory = _ => "all",
        };
        since.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string>() ?? string.Empty;
            if (!Period.TryParse(value, context, out _))
            {
                result.AddError(
                    $"Invalid --since value {value}. Use today, all, <N>d (1-3650), or yyyy-MM-dd.");
            }
        });
        Option<bool> dryRun = new("--dry-run")
        {
            Description = "Show what would be recorded without changing the ledger.",
        };
        Option<bool> includeActive = new("--include-active")
        {
            Description = "Also read Copilot sessions that are still open.",
        };
        Option<bool> json = new("--json")
        {
            Description = "Write JSON instead of a table.",
        };
        command.Add(repository);
        command.Add(all);
        command.Add(sources);
        command.Add(since);
        command.Add(dryRun);
        command.Add(includeActive);
        command.Add(json);
        command.Validators.Add(result =>
        {
            string? path = result.GetValue(repository);
            bool everything = result.GetValue(all);
            if (path is null && !everything)
            {
                result.AddError("Use --repo <path> or --all.");
            }
            else if (path is not null && everything)
            {
                result.AddError("Use either --repo or --all, not both.");
            }
            else if (path is not null && string.IsNullOrWhiteSpace(path))
            {
                result.AddError("--repo can't be blank.");
            }

            string[] values = result.GetValue(sources) ?? [];
            if (values.Contains("claude"))
            {
                result.AddError(ClaudeUnsupported);
            }

            if (values.Any(value => value is not ("copilot" or "codex" or "claude")))
            {
                result.AddError("--source must be copilot or codex.");
            }
        });
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
            {
                string[] values = result.GetValue(sources) ?? [];
                List<ImportSourceKind> kinds = [];
                if (values.Length == 0 || values.Contains("copilot"))
                {
                    kinds.Add(ImportSourceKind.Copilot);
                }

                if (values.Contains("codex"))
                {
                    kinds.Add(ImportSourceKind.Codex);
                }

                return RunAsync(
                    context,
                    result.GetValue(repository),
                    kinds,
                    Period.TryParse(result.GetValue(since), context, out Period period)
                        ? period
                        : throw new InvalidOperationException(),
                    result.GetValue(dryRun),
                    result.GetValue(includeActive),
                    result.GetValue(json),
                    cancellationToken);
            });
        return command;
    }

    private static async Task<int> RunAsync(
        CliContext context,
        string? repositoryPath,
        IReadOnlyList<ImportSourceKind> kinds,
        Period period,
        bool dryRun,
        bool includeActive,
        bool json,
        CancellationToken cancellationToken)
    {
        GitWorkspaceContext? git = null;
        if (repositoryPath is not null)
        {
            string path = Path.GetFullPath(repositoryPath, context.WorkingDirectory);
            git = new FileSystemGitContextResolver().Resolve(path);
            if (git is null)
            {
                context.Error.WriteLine($"{path} isn't in a Git repository.");
                return 1;
            }
        }

        RatesBase rates = RateCardFiles.LoadBase(context);
        if (rates.Warning is not null)
        {
            context.Error.WriteLine($"Warning: {rates.Warning}");
        }

        JsonRateCardCalculator calculator;
        try
        {
            calculator = RateCardFiles.Load(rates, RateCardFiles.OverlayPath(context));
        }
        catch (InvalidDataException ex)
        {
            context.Error.WriteLine($"Invalid rates overlay: {ex.Message}");
            return 1;
        }

        // A daemon of another version records live spans by its own rules, so a request it records later might not
        // supersede an imported one.
        if (DaemonFiles.IsHeld(context) &&
            DaemonFiles.TryReadState(context)?.Version is string version &&
            version != GhLedgerCli.Version)
        {
            context.Error.WriteLine(
                $"Warning: the daemon runs gh-ledger {version}; restart it so live spans follow this version's import rules.");
        }

        ImportRequest request = new()
        {
            DatabasePath = context.DatabasePath,
            Sources =
            [
                .. kinds.Select(kind => kind == ImportSourceKind.Copilot
                    ? new ImportSourceRequest(new CopilotSessionLogSource(), context.CopilotHome)
                    : new ImportSourceRequest(new CodexSessionLogSource(), context.CodexHome)),
            ],
            RepositoryRoot = git?.RepositoryRoot,
            Since = period.From,
            DryRun = dryRun,
            IncludeActive = includeActive,
        };
        ImportReport report;
        try
        {
            report = await new LedgerImporter(calculator, context.TimeProvider).RunAsync(request, cancellationToken);
        }
        catch (SqliteException ex)
        {
            context.Error.WriteLine(
                dryRun
                    ? $"Dry run stopped: SQLite error {ex.SqliteErrorCode}."
                    : $"Import stopped: SQLite error {ex.SqliteErrorCode}. Requests recorded before it are kept; run the import again.");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            context.Error.WriteLine(ex.Message);
            return 1;
        }

        if (json)
        {
            WriteJson(context, report, period, git);
        }
        else
        {
            WriteText(context, report, period, git);
        }

        return report.Complete ? 0 : Incomplete;
    }

    private static void WriteText(CliContext context, ImportReport report, Period period, GitWorkspaceContext? git)
    {
        string scope = git is null ? string.Empty : $" for {git.RepositorySlug ?? git.RepositoryRoot}";
        context.Out.WriteLine(
            $"Import from local session logs{scope} ({period.Label}{(report.DryRun ? ", dry run" : string.Empty)})");
        context.Out.WriteLine();
        List<IReadOnlyList<string>> rows =
        [
            .. Rows.Select(row =>
                (IReadOnlyList<string>)[row.Label, .. report.Sources.Select(source => LedgerFormat.Count(row.Count(source)))]),
            ["Coverage", .. report.Sources.Select(source => Coverage(source.Complete))],
        ];
        TableWriter.Write(
            context.Out,
            [new TableColumn(string.Empty, false), .. report.Sources.Select(source => new TableColumn(Name(source.Source), true))],
            rows);
        context.Out.WriteLine();

        Totals totals = Totals.Of(report.Sources);
        if (totals.Recorded == 0)
        {
            context.Out.WriteLine(report.DryRun ? "There's nothing new to record." : "Nothing new to record.");
        }
        else
        {
            string api = LedgerFormat.ApiEquivalent(
                totals.ApiEquivalentUsd,
                totals.ApiEquivalentUnpricedSpans,
                totals.ApiEquivalentEstimatedSpans);
            context.Out.WriteLine(
                $"{(report.DryRun ? "Importing would record" : "Recorded")} {Plural(totals.Recorded, "request")}: {LedgerFormat.Credits(totals.AiCredits)} AI credits ({LedgerFormat.Usd(totals.AiCreditsUsd)}), API equivalent {api}.");
            if (totals.FoundryByokUsd != 0m || totals.LocalSavedUsd != 0m)
            {
                context.Out.WriteLine(
                    $"Foundry BYOK: {LedgerFormat.Usd(totals.FoundryByokUsd)}. Local saved: {LedgerFormat.Usd(totals.LocalSavedUsd)}.");
            }

            if (api.EndsWith('*'))
            {
                context.Out.WriteLine(
                    $"{LedgerFormat.Count(totals.ApiEquivalentUnpricedSpans)} of {Plural(totals.Recorded, "request")} had no API-equivalent price; * marks a partial amount.");
            }

            if (api.StartsWith('~'))
            {
                long estimated = totals.ApiEquivalentEstimatedSpans;
                context.Out.WriteLine(
                    $"{Plural(estimated, "priced request")} reported no cache counters, so all of {(estimated == 1 ? "its" : "their")} input is priced as uncached; ~ marks an estimate.");
            }
        }

        if (report.DryRun)
        {
            context.Out.WriteLine("Dry run: nothing was written.");
        }

        if (!report.Complete)
        {
            long old = report.Sources.Sum(source => source.SessionsAggregateOnly);
            context.Out.WriteLine(
                old > 0 && report.Sources.All(OnlyOldSessionsMissing)
                    ? $"Coverage is incomplete only because {Plural(old, "session")} {(old == 1 ? "predates" : "predate")} Copilot {CopilotSessionLogSource.MinimumVersion}; use --since to leave {(old == 1 ? "it" : "them")} out."
                    : "Coverage is incomplete; see the warnings.");
        }

        foreach (ImportDiagnostic diagnostic in report.Diagnostics.Take(MaxWarnings))
        {
            context.Error.WriteLine($"Warning: {Describe(diagnostic)}");
        }

        if (report.Diagnostics.Count > MaxWarnings)
        {
            int more = report.Diagnostics.Count - MaxWarnings;
            context.Error.WriteLine($"Warning: {Plural(more, "more warning")}; run with --json to see them all.");
        }
    }

    // Every gap in the source's coverage is a session from before Copilot 1.0.103.
    private static bool OnlyOldSessionsMissing(ImportSourceReport source) =>
        source.SessionsUnsupported == source.SessionsAggregateOnly &&
        source.MalformedLines == 0 &&
        source.UnsupportedUsageShapes == 0 &&
        source.AmbiguousOverlap == 0 &&
        source.CostOutOfRange == 0 &&
        source.JournalPredecessors == 0;

    private static void WriteJson(CliContext context, ImportReport report, Period period, GitWorkspaceContext? git)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteBoolean("dryRun", report.DryRun);
        writer.WriteString("period", period.Label);
        LedgerFormat.WriteTime(writer, "from", period.From);
        LedgerFormat.WriteNullable(writer, "repository", git is null ? null : git.RepositorySlug ?? git.RepositoryRoot);
        LedgerFormat.WriteNullable(writer, "repositoryRoot", git?.RepositoryRoot);
        writer.WriteString("coverage", Coverage(report.Complete));
        writer.WriteStartArray("sources");
        foreach (ImportSourceReport source in report.Sources)
        {
            writer.WriteStartObject();
            writer.WriteString("source", ImportIds.SourceName(source.Source));
            writer.WriteString("coverage", Coverage(source.Complete));
            writer.WriteNumber("sessionsScanned", source.SessionsScanned);
            writer.WriteNumber("sessionsSkippedUnchanged", source.SessionsSkippedUnchanged);
            writer.WriteNumber("sessionsSkippedActive", source.SessionsSkippedActive);
            writer.WriteNumber("sessionsSkippedOutOfScope", source.SessionsSkippedOutOfScope);
            writer.WriteNumber("sessionsUnsupported", source.SessionsUnsupported);
            writer.WriteNumber("sessionsAggregateOnly", source.SessionsAggregateOnly);
            writer.WriteNumber("recordsParsed", source.RecordsParsed);
            writer.WriteNumber("recordsOutOfScope", source.RecordsOutOfScope);
            writer.WriteNumber("recorded", source.Recorded);
            writer.WriteNumber("duplicate", source.Duplicates);
            writer.WriteNumber("overlapSkipped", source.OverlapSkipped);
            writer.WriteNumber("ambiguousOverlap", source.AmbiguousOverlap);
            writer.WriteNumber("malformedLines", source.MalformedLines);
            writer.WriteNumber("unsupportedUsageShapes", source.UnsupportedUsageShapes);
            writer.WriteNumber("costOutOfRange", source.CostOutOfRange);
            writer.WriteNumber("journalPredecessors", source.JournalPredecessors);
            writer.WriteNumber("skippedEvents", source.SkippedEvents);
            writer.WriteNumber("unpriced", source.Unpriced);
            writer.WriteNumber("toolLinksRecorded", source.ToolLinksRecorded);
            WriteAmounts(writer, Totals.Of([source]));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        Totals totals = Totals.Of(report.Sources);
        writer.WritePropertyName("totals");
        writer.WriteStartObject();
        writer.WriteNumber("recorded", totals.Recorded);
        WriteAmounts(writer, totals);
        writer.WriteEndObject();
        writer.WriteStartArray("diagnostics");
        foreach (ImportDiagnostic diagnostic in report.Diagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("source", ImportIds.SourceName(diagnostic.Source));
            writer.WriteString("log", diagnostic.LogId);
            writer.WriteString("code", JsonNamingPolicy.CamelCase.ConvertName(diagnostic.Code.ToString()));
            writer.WriteString("event", JsonNamingPolicy.CamelCase.ConvertName(diagnostic.Event.ToString()));
            LedgerFormat.WriteNullable(writer, "field", diagnostic.Field);
            LedgerFormat.WriteNullable(writer, "version", diagnostic.Version);
            writer.WriteNumber("count", diagnostic.Count);
            LedgerFormat.WriteNullable(writer, "line", diagnostic.FirstLine > 0 ? (long?)diagnostic.FirstLine : null);
            LedgerFormat.WriteNullable(
                writer,
                "byteOffset",
                diagnostic.FirstLine > 0 ? (long?)diagnostic.FirstByteOffset : null);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteAmounts(Utf8JsonWriter writer, Totals totals)
    {
        LedgerFormat.WriteMoney(writer, "aiCredits", totals.AiCredits);
        LedgerFormat.WriteMoney(writer, "aiCreditsUsd", totals.AiCreditsUsd);
        LedgerFormat.WriteMoney(writer, "foundryByokUsd", totals.FoundryByokUsd);
        LedgerFormat.WriteMoney(writer, "localSavedUsd", totals.LocalSavedUsd);
        LedgerFormat.WriteApiEquivalent(
            writer,
            totals.ApiEquivalentUsd,
            totals.ApiEquivalentUnpricedSpans,
            totals.ApiEquivalentEstimatedSpans);
    }

    // "<source> <session|rollout> <log ID>[, line <n> (byte <offset>)]: <message>"
    private static string Describe(ImportDiagnostic diagnostic)
    {
        string log = diagnostic.Source == ImportSourceKind.Codex ? "Codex rollout" : "Copilot session";
        string line = diagnostic.FirstLine > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $", line {diagnostic.FirstLine} (byte {diagnostic.FirstByteOffset})")
            : string.Empty;
        return $"{log} {diagnostic.LogId}{line}: {Message(diagnostic)}";
    }

    private static string Message(ImportDiagnostic diagnostic)
    {
        long count = diagnostic.Count;
        bool codex = diagnostic.Source == ImportSourceKind.Codex;
        string producer = codex ? "Codex" : "Copilot";
        string supported = codex
            ? $"{CodexSessionLogSource.MinimumVersion} to {CodexSessionLogSource.MaximumVersion}"
            : $"{CopilotSessionLogSource.MinimumVersion} to {CopilotSessionLogSource.MaximumVersion}";
        return diagnostic.Code switch
        {
            ImportDiagnosticCode.MalformedLine => $"{Plural(count, "malformed line")} skipped",
            ImportDiagnosticCode.OversizedLine => $"{Plural(count, "line")} longer than 8 MiB skipped",
            ImportDiagnosticCode.UnterminatedLine => "the last line is incomplete and was skipped",
            ImportDiagnosticCode.UnsupportedUsageShape =>
                $"{Plural(count, diagnostic.Event == ImportEventKind.CompactionComplete ? "compaction record" : "usage record")} skipped: unsupported {diagnostic.Field}",
            ImportDiagnosticCode.MissingSessionStart => codex
                ? "no session_meta first; rollout skipped"
                : "no session.start first; session skipped",
            ImportDiagnosticCode.SessionIdMismatch => "session.start names another session; session skipped",
            ImportDiagnosticCode.UnsupportedProducer => "not a supported session log; skipped",
            ImportDiagnosticCode.UnsupportedProducerVersion => diagnostic.Version is null
                ? $"its {producer} version can't be read ({supported} are supported); skipped"
                : $"{producer} {diagnostic.Version} isn't supported ({supported}); skipped",
            ImportDiagnosticCode.AggregateOnlySession =>
                $"Copilot before {CopilotSessionLogSource.MinimumVersion} logs only session totals (this session: {diagnostic.Version}); {supported} are supported; skipped",
            ImportDiagnosticCode.JournalPredecessor => "earlier journal files aren't read; older requests may be missing",
            ImportDiagnosticCode.CompressedLog => "compressed rollouts (.jsonl.zst) aren't read; rollout skipped",
            ImportDiagnosticCode.UnreadableLog => "the log couldn't be read; skipped",
            ImportDiagnosticCode.UnknownSubagent => $"{Plural(count, "sub-agent request")} recorded without an agent name",
            ImportDiagnosticCode.InheritedRecord => $"{Plural(count, "usage record")} of another thread skipped",
            ImportDiagnosticCode.AmbiguousOverlap =>
                $"{Plural(count, "request")} deferred: this conversation has live spans without a call ID",
            ImportDiagnosticCode.CostOutOfRange => $"{Plural(count, "request")} skipped: cost beyond the ledger's range",
            _ => throw new ArgumentOutOfRangeException(nameof(diagnostic), diagnostic.Code, null),
        };
    }

    private static string Name(ImportSourceKind source) => source == ImportSourceKind.Codex ? "Codex" : "Copilot";

    private static string Coverage(bool complete) => complete ? "complete" : "incomplete";

    // "1 request", "2 requests", "1,000 requests".
    private static string Plural(long count, string noun) =>
        $"{LedgerFormat.Count(count)} {noun}{(count == 1 ? string.Empty : "s")}";

    // The recorded requests' amounts over sources. ApiEquivalentUsd is 0 when nothing was recorded, and null when requests
    // were recorded and none of them has an API-equivalent price.
    private sealed record Totals(
        long Recorded,
        decimal AiCredits,
        decimal AiCreditsUsd,
        decimal FoundryByokUsd,
        decimal LocalSavedUsd,
        decimal? ApiEquivalentUsd,
        long ApiEquivalentUnpricedSpans,
        long ApiEquivalentEstimatedSpans)
    {
        public static Totals Of(IReadOnlyList<ImportSourceReport> sources)
        {
            long recorded = sources.Sum(source => source.Recorded);
            List<decimal> priced = [.. sources.Where(source => source.Recorded > 0 && source.ApiEquivalentUsd is not null)
                .Select(source => source.ApiEquivalentUsd!.Value)];
            return new Totals(
                recorded,
                sources.Sum(source => source.AiCredits),
                sources.Sum(source => source.AiCreditsUsd),
                sources.Sum(source => source.FoundryByokUsd),
                sources.Sum(source => source.LocalSavedUsd),
                recorded == 0 ? 0m : priced.Count == 0 ? null : priced.Sum(),
                sources.Sum(source => source.ApiEquivalentUnpricedSpans),
                sources.Sum(source => source.ApiEquivalentEstimatedSpans));
        }
    }
}
```

New file `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`:

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Core.Git;
using CopilotLedger.Mcp;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CopilotLedger.Tests.Cli;

public sealed class ImportCommandTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private const string S4 = "44444444-4444-4444-8444-444444444444";
    private const string S5 = "55555555-5555-4555-8555-555555555555";
    private const string Thread = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string, string> InvalidOptions => new()
    {
        { "import", "Use --repo <path> or --all." },
        { "import|--repo|.|--all", "Use either --repo or --all, not both." },
        { "import|--repo| ", "--repo can't be blank." },
        { "import|--all|--source|cursor", "--source must be copilot or codex." },
        {
            "import|--all|--source|claude",
            "Claude Code import isn't supported: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it."
        },
        { "import|--all|--since|yesterday", "Invalid --since value yesterday. Use today, all, <N>d (1-3650), or yyyy-MM-dd." },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public async Task IC1_OptionsAreValidated(string command, string message)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);

        CliRun run = await harness.RunAsync(command.Split('|'));

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message + "\n", run.Error);
        Assert.False(File.Exists(harness.Context.DatabasePath));
    }

    [Fact]
    public async Task IC2_RepoMustBeInAGitRepository()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string work = Path.Combine(temp.Path, "work");
        Directory.CreateDirectory(work);

        CliRun run = await harness.RunInAsync(work, "import", "--repo", ".");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal("", run.Out);
        Assert.Equal($"{work} isn't in a Git repository.\n", run.Error);
        Assert.False(File.Exists(harness.Context.DatabasePath));
    }

    [Fact]
    public async Task IC3_TheTableAndTotalsAreExact()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string repo = GitFixture.Create(temp.Path);
        string other = Path.Combine(temp.Path, "other");
        JsonObject context = CopilotSessionLogs.Context(repo, repo, "feature/import", "swigerb/copilot-ledger", GitFixture.Sha2);
        new CopilotSessionLogs("s-a", context: context)
            .Usage("u1", S1, input: 1_000, output: 100)
            .Usage("u2", S2, input: 2_000, output: 200, cacheRead: 500)
            .Compaction(S3)
            .Write(harness.Context.CopilotHome);
        new CopilotSessionLogs("s-b", context: context.DeepClone().AsObject())
            .Usage("u3", S4)
            .Write(harness.Context.CopilotHome, active: true);
        new CopilotSessionLogs("s-c", context: CopilotSessionLogs.Context(other, other, "main", "contoso/other", GitFixture.Sha1))
            .Usage("u4", S5)
            .Write(harness.Context.CopilotHome);

        CliRun first = await harness.RunInAsync(repo, "import", "--repo", ".");
        CliRun second = await harness.RunInAsync(repo, "import", "--repo", ".");
        CliRun active = await harness.RunInAsync(repo, "import", "--repo", ".", "--include-active");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(
            Normalize("""
            Import from local session logs for swigerb/copilot-ledger (all time)

                                       Copilot
            Sessions scanned                 3
            Skipped, unchanged               0
            Skipped, active                  1
            Skipped, out of scope            0
            Unsupported sessions             0
            Records parsed                   4
            Out of scope                     1
            Recorded                         3
            Duplicate                        0
            Overlap-skipped                  0
            Ambiguous-overlap                0
            Malformed lines                  0
            Unsupported usage shapes         0
            Cost out of range                0
            Unpriced                         0
            Coverage                  complete

            Recorded 3 requests: 12.26 AI credits ($0.12), API equivalent $0.12.

            """),
            first.Out);
        Assert.Equal("", first.Error);
        Assert.Equal(0, second.ExitCode);
        Assert.Contains("Skipped, unchanged               1\n", second.Out);
        Assert.EndsWith("\nNothing new to record.\n", second.Out);
        Assert.Equal(0, active.ExitCode);
        Assert.EndsWith("\nRecorded 1 request: 0.15 AI credits ($0.00), API equivalent $0.00.\n", active.Out);
        Assert.Equal(
            [
                "s-a|feature/import|1000|1500000000",
                "s-a|feature/import|2000|2550000000",
                "s-a|feature/import|112223|118555450000",
                "s-b|feature/import|1000|1500000000",
            ],
            await LedgerSql.RowsAsync(
                harness.Context.DatabasePath,
                """
                SELECT conversation_id, branch_name, input_tokens, cost_usd_pico FROM spans
                ORDER BY conversation_id, start_time_unix_ms;
                """));
    }

    [Fact]
    public async Task IC4_TheJsonIsExact()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        new CopilotSessionLogs("s-a").Usage("u1", S1).Write(harness.Context.CopilotHome);
        new CodexRollouts(Thread).TurnContext("turn-1", "gpt-6.1-sol").Usage("resp_1", "turn-1").Write(harness.Context.CodexHome);

        CliRun run = await harness.RunAsync("import", "--all", "--source", "codex", "--source", "copilot", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Error);
        Assert.Equal(
            Normalize("""
            {
              "dryRun": false,
              "period": "all time",
              "from": null,
              "repository": null,
              "repositoryRoot": null,
              "coverage": "complete",
              "sources": [
                {
                  "source": "copilot",
                  "coverage": "complete",
                  "sessionsScanned": 1,
                  "sessionsSkippedUnchanged": 0,
                  "sessionsSkippedActive": 0,
                  "sessionsSkippedOutOfScope": 0,
                  "sessionsUnsupported": 0,
                  "sessionsAggregateOnly": 0,
                  "recordsParsed": 1,
                  "recordsOutOfScope": 0,
                  "recorded": 1,
                  "duplicate": 0,
                  "overlapSkipped": 0,
                  "ambiguousOverlap": 0,
                  "malformedLines": 0,
                  "unsupportedUsageShapes": 0,
                  "costOutOfRange": 0,
                  "journalPredecessors": 0,
                  "skippedEvents": 0,
                  "unpriced": 0,
                  "toolLinksRecorded": 0,
                  "aiCredits": 0.15,
                  "aiCreditsUsd": 0.0015,
                  "foundryByokUsd": 0,
                  "localSavedUsd": 0,
                  "apiEquivalentUsd": 0.0015,
                  "apiEquivalentUnpricedSpans": 0,
                  "apiEquivalentEstimatedSpans": 0
                },
                {
                  "source": "codex",
                  "coverage": "complete",
                  "sessionsScanned": 1,
                  "sessionsSkippedUnchanged": 0,
                  "sessionsSkippedActive": 0,
                  "sessionsSkippedOutOfScope": 0,
                  "sessionsUnsupported": 0,
                  "sessionsAggregateOnly": 0,
                  "recordsParsed": 1,
                  "recordsOutOfScope": 0,
                  "recorded": 1,
                  "duplicate": 0,
                  "overlapSkipped": 0,
                  "ambiguousOverlap": 0,
                  "malformedLines": 0,
                  "unsupportedUsageShapes": 0,
                  "costOutOfRange": 0,
                  "journalPredecessors": 0,
                  "skippedEvents": 0,
                  "unpriced": 1,
                  "toolLinksRecorded": 0,
                  "aiCredits": 0,
                  "aiCreditsUsd": 0,
                  "foundryByokUsd": 0,
                  "localSavedUsd": 0,
                  "apiEquivalentUsd": 0.003,
                  "apiEquivalentUnpricedSpans": 0,
                  "apiEquivalentEstimatedSpans": 0
                }
              ],
              "totals": {
                "recorded": 2,
                "aiCredits": 0.15,
                "aiCreditsUsd": 0.0015,
                "foundryByokUsd": 0,
                "localSavedUsd": 0,
                "apiEquivalentUsd": 0.0045,
                "apiEquivalentUnpricedSpans": 0,
                "apiEquivalentEstimatedSpans": 0
              },
              "diagnostics": []
            }

            """),
            run.Out);
    }

    [Fact]
    public async Task IC5_AnIncompleteImportExitsWith2AndCappedContentFreeWarnings()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        List<string> expected = [];
        CopilotSessionLogs compaction = new CopilotSessionLogs("s-00")
            .Usage("u-00", Srid(0))
            .Compaction(Srid(100), change: data => data["compactionTokensUsed"]!["requestCount"] = 2);
        compaction.Write(harness.Context.CopilotHome);
        expected.Add(
            $"Warning: Copilot session s-00, line 3 (byte {Offset(compaction, 2)}): 1 compaction record skipped: unsupported data.compactionTokensUsed.requestCount");
        for (int index = 1; index <= 21; index++)
        {
            string id = string.Create(CultureInfo.InvariantCulture, $"s-{index:00}");
            CopilotSessionLogs logs = new CopilotSessionLogs(id)
                .Usage("u-" + id, Srid(index))
                .Raw("{\"type\":\"user.message\",\"data\":{\"content\":\"" + CopilotSessionLogs.Canary + "\"");
            logs.Write(harness.Context.CopilotHome);
            expected.Add($"Warning: Copilot session {id}, line 3 (byte {Offset(logs, 2)}): 1 malformed line skipped");
        }

        CliRun text = await harness.RunAsync("import", "--all");
        CliRun json = await harness.RunAsync("import", "--all", "--json");

        Assert.Equal(2, text.ExitCode);
        Assert.Contains("Records parsed                    23\n", text.Out);
        Assert.Contains("Recorded                          22\n", text.Out);
        Assert.Contains("Malformed lines                   21\n", text.Out);
        Assert.Contains("Unsupported usage shapes           1\n", text.Out);
        Assert.Contains("Coverage                  incomplete\n", text.Out);
        Assert.EndsWith(
            "\nRecorded 22 requests: 3.30 AI credits ($0.03), API equivalent $0.03.\nCoverage is incomplete; see the warnings.\n",
            text.Out);
        Assert.Equal(
            string.Concat(expected.Take(20).Select(line => line + "\n")) +
            "Warning: 2 more warnings; run with --json to see them all.\n",
            text.Error);
        Assert.Equal(2, json.ExitCode);
        Assert.Equal("", json.Error);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Equal("incomplete", document.RootElement.GetProperty("coverage").GetString());
        JsonElement[] diagnostics = [.. document.RootElement.GetProperty("diagnostics").EnumerateArray()];
        Assert.Equal(22, diagnostics.Length);
        Assert.Equal(
            """{"source":"copilot","log":"s-00","code":"unsupportedUsageShape","event":"compactionComplete","field":"data.compactionTokensUsed.requestCount","version":null,"count":1,"line":3,"byteOffset":""" +
            Offset(compaction, 2).ToString(CultureInfo.InvariantCulture) + "}",
            JsonSerializer.Serialize(diagnostics[0]));
        Assert.Equal(22, document.RootElement.GetProperty("sources")[0].GetProperty("duplicate").GetInt64());
        Assert.DoesNotContain(CopilotSessionLogs.Canary, text.Out + text.Error + json.Out);
    }

    [Fact]
    public async Task IC6_ADryRunPrintsWhatTheImportDoesAndLeavesTheLedgerAsItWas()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("s-a", S1)]);
        SqliteConnection.ClearAllPools();
        new CopilotSessionLogs("s-a")
            .Usage("u1", S1, input: 100_000, output: 10_000)
            .Usage("u2", S2, input: 100_000, output: 10_000)
            .Write(harness.Context.CopilotHome);
        new CopilotSessionLogs("s-b").Usage("u3", S3, input: 100_000, output: 10_000).Write(harness.Context.CopilotHome);
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(harness.Context.DatabasePath));

        CliRun dry = await harness.RunAsync("import", "--all", "--dry-run");

        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(harness.Context.DatabasePath)));
        Assert.False(File.Exists(harness.Context.DatabasePath + "-wal"));
        Assert.False(File.Exists(harness.Context.DatabasePath + "-shm"));
        CliRun real = await harness.RunAsync("import", "--all");
        string table = Normalize("""

                                       Copilot
            Sessions scanned                 2
            Skipped, unchanged               0
            Skipped, active                  0
            Skipped, out of scope            0
            Unsupported sessions             0
            Records parsed                   3
            Out of scope                     0
            Recorded                         2
            Duplicate                        0
            Overlap-skipped                  1
            Ambiguous-overlap                0
            Malformed lines                  0
            Unsupported usage shapes         0
            Cost out of range                0
            Unpriced                         0
            Coverage                  complete


            """);
        Assert.Equal(0, dry.ExitCode);
        Assert.Equal(
            "Import from local session logs (all time, dry run)\n" + table +
            "Importing would record 2 requests: 30.00 AI credits ($0.30), API equivalent $0.30.\nDry run: nothing was written.\n",
            dry.Out);
        Assert.Equal(0, real.ExitCode);
        Assert.Equal(
            "Import from local session logs (all time)\n" + table +
            "Recorded 2 requests: 30.00 AI credits ($0.30), API equivalent $0.30.\n",
            real.Out);
        Assert.Equal(3L, await LedgerSql.ScalarAsync(harness.Context.DatabasePath, "SELECT COUNT(*) FROM spans;"));
    }

    [Fact]
    public async Task IC7_ImportedSpendAppearsInTheReportsWithItsRecordedBranch()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string repo = await ImportBranchAsync(temp, harness);

        CliRun branch = await harness.RunInAsync(repo, "branch", "--since", "all", "--json");
        CliRun agents = await harness.RunInAsync(repo, "agents", "--since", "all", "--json");
        CliRun prompts = await harness.RunInAsync(repo, "top-prompts", "--since", "all", "--json");

        using JsonDocument branchJson = JsonDocument.Parse(branch.Out);
        JsonElement row = Assert.Single(branchJson.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal(
            ("swigerb/copilot-ledger", "feature/import", 2L, 22.5m, 0.225m),
            (row.GetProperty("repository").GetString(), row.GetProperty("branch").GetString(),
             row.GetProperty("spans").GetInt64(), row.GetProperty("aiCredits").GetDecimal(),
             row.GetProperty("apiEquivalentUsd").GetDecimal()));
        using JsonDocument agentJson = JsonDocument.Parse(agents.Out);
        Assert.Equal(
            ["(none)|1|15", "explore|1|7.5"],
            agentJson.RootElement.GetProperty("rows").EnumerateArray()
                .Select(item => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{item.GetProperty("agent").GetString()}|{item.GetProperty("spans").GetInt64()}|{item.GetProperty("aiCredits").GetDecimal()}"))
                .Order(StringComparer.Ordinal));
        using JsonDocument promptJson = JsonDocument.Parse(prompts.Out);
        JsonElement prompt = Assert.Single(promptJson.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal(
            ("s-a", "feature/import", 2L, 22.5m, "explore"),
            (prompt.GetProperty("conversationId").GetString(), prompt.GetProperty("branch").GetString(),
             prompt.GetProperty("spans").GetInt64(), prompt.GetProperty("aiCredits").GetDecimal(),
             Assert.Single(prompt.GetProperty("agents").EnumerateArray()).GetString()));
    }

    [Fact]
    public async Task IC8_ImportedSpendAppearsInTheMcpBranchLedger()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string repo = await ImportBranchAsync(temp, harness);
        await using McpTestHarness mcp = await McpTestHarness.StartAsync(new LedgerMcpContext
        {
            DatabasePath = harness.Context.DatabasePath,
            WorkingDirectory = repo,
            GetEnvironmentVariable = _ => null,
            GitResolver = new FileSystemGitContextResolver(ceilingDirectory: temp.Path),
            TimeProvider = harness.TimeProvider,
            Version = "9.9.9",
        });

        CallToolResult result = await mcp.Client.CallToolAsync(
            "get_branch_ledger",
            new Dictionary<string, object?> { ["repository"] = "swigerb/copilot-ledger", ["branch"] = "feature/import" });

        Assert.NotEqual(true, result.IsError);
        using JsonDocument json = JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.Equal(
            (2L, 22.5m, 0.225m, 0.225m, "claude-haiku-4.5"),
            (json.RootElement.GetProperty("spans").GetInt64(), json.RootElement.GetProperty("aiCredits").GetDecimal(),
             json.RootElement.GetProperty("aiCreditsUsd").GetDecimal(), json.RootElement.GetProperty("apiEquivalentUsd").GetDecimal(),
             json.RootElement.GetProperty("topModel").GetString()));
    }

    [Fact]
    public void IC9_TheIngestFailureLogWritesOnlyTheTypeOfOtherExceptions()
    {
        List<string> lines = [];
        ManualTimeProvider clock = new(Now);
        IngestFailureLog log = new(lines.Add, clock);

        log.Report(new JsonException(CopilotSessionLogs.Canary));
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Report(new SqliteException("SQLite Error 5: 'database is locked'.", 5));
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Report(new IOException("There is not enough space on the disk."));
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Report(new ArgumentException(CopilotSessionLogs.Canary));

        Assert.Equal(
            [
                "Warning: could not record spans: JsonException",
                "Warning: could not record spans: SqliteException: SQLite Error 5: 'database is locked'.",
                "Warning: could not record spans: IOException: There is not enough space on the disk.",
                "Warning: could not record spans: ArgumentException",
            ],
            lines);
    }

    [Fact]
    public async Task IC10_ADaemonOfAnotherVersionIsReported()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string data = harness.Context.DataDirectory;
        Directory.CreateDirectory(data);
        await File.WriteAllTextAsync(
            Path.Combine(data, "daemon.json"),
            """{"schemaVersion":1,"processId":4242,"startedAt":"2026-11-23T11:00:00+00:00","endpoint":"http://localhost:4318","version":"0.4.0","databasePath":"ledger.db"}""");
        CliRun run;
        await using (FileStream held = new(Path.Combine(data, "daemon.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            run = await harness.RunAsync("import", "--all");
        }

        CliRun released = await harness.RunAsync("import", "--all");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            "Warning: the daemon runs gh-ledger 0.4.0; restart it so live spans follow this version's import rules.\n",
            run.Error);
        Assert.Equal("", released.Error);
    }

    [Fact]
    public async Task IC11_SessionsFromBeforeCopilot1_0_103AloneSuggestSince()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string old = new CopilotSessionLogs("s-old", "1.0.98", start: new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero))
            .Usage("u1", S1)
            .Write(harness.Context.CopilotHome);
        string recent = new CopilotSessionLogs("s-new").Usage("u2", S2).Write(harness.Context.CopilotHome);
        File.SetLastWriteTimeUtc(old, new DateTime(2026, 10, 1, 12, 1, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(recent, new DateTime(2026, 11, 20, 12, 1, 0, DateTimeKind.Utc));

        CliRun all = await harness.RunAsync("import", "--all");
        CliRun since = await harness.RunAsync("import", "--all", "--since", "2026-11-01");

        Assert.Equal(2, all.ExitCode);
        Assert.Contains("Unsupported sessions               1\n", all.Out);
        Assert.EndsWith(
            "\nRecorded 1 request: 0.15 AI credits ($0.00), API equivalent $0.00.\nCoverage is incomplete only because 1 session predates Copilot 1.0.103; use --since to leave it out.\n",
            all.Out);
        Assert.Equal(
            "Warning: Copilot session s-old, line 1 (byte 0): Copilot before 1.0.103 logs only session totals (this session: 1.0.98); 1.0.103 to 1.0.114 are supported; skipped\n",
            all.Error);
        Assert.Equal(0, since.ExitCode);
        Assert.Contains("Skipped, out of scope            1\n", since.Out);
        Assert.EndsWith("\nNothing new to record.\n", since.Out);
        Assert.Equal("", since.Error);
    }

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static string Srid(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"5e000000-0000-4000-8000-{index:x12}");

    // The byte offset of the line after the first count lines.
    private static long Offset(CopilotSessionLogs logs, int count) =>
        logs.Lines.Take(count).Sum(line => Encoding.UTF8.GetByteCount(line) + 1L);

    // Imports one session of the repository at <temp>/repo, recorded on branch feature/import: a request of the main agent
    // (15 AI credits) and one of its explore sub-agent (7.5 AI credits) in one prompt. Returns the repository's path.
    private static async Task<string> ImportBranchAsync(TempDirectory temp, CliHarness harness)
    {
        string repo = GitFixture.Create(temp.Path);
        new CopilotSessionLogs(
                "s-a",
                context: CopilotSessionLogs.Context(repo, repo, "feature/import", "swigerb/copilot-ledger", GitFixture.Sha2))
            .Usage("u1", S1, input: 100_000, output: 10_000)
            .SubagentStarted("agent-1", "explore", "toolu-1")
            .Usage("u2", S2, input: 50_000, output: 5_000, agentId: "agent-1")
            .Write(harness.Context.CopilotHome);
        CliRun run = await harness.RunInAsync(repo, "import", "--repo", ".");
        Assert.Equal(0, run.ExitCode);
        return repo;
    }
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/TestSupport/CliHarness.cs`, replace:

```csharp
            DataDirectory = Path.Combine(root, "data"),
            CopilotHome = Path.Combine(root, "copilot-home"),
            WorkingDirectory = root,
            TimeProvider = TimeProvider,
```

with:

```csharp
            DataDirectory = Path.Combine(root, "data"),
            CopilotHome = Path.Combine(root, "copilot-home"),
            CodexHome = Path.Combine(root, "codex-home"),
            WorkingDirectory = root,
            TimeProvider = TimeProvider,
```

### Tests

Expected amounts: `claude-haiku-4.5` costs $1.00 per million input tokens, $0.10 cached, $1.25 written, and $5.00 output, on both lenses. 1,000 input and 100 output tokens cost $0.0015 (0.15 AI credits).

- **IC1** (6): each validation message, exit code 1, and no ledger created. **IC2**: `--repo .` outside a repository.
- **IC3**: the exact table and totals for three sessions (one active, one of another repository): `Recorded 3 requests: 12.26 AI credits ($0.12), API equivalent $0.12.`, where the compaction is 118,555,450,000 pico-USD; a second run says `Nothing new to record.`; `--include-active` then records the active session's request.
- **IC4**: the exact JSON for a Copilot request ($0.0015) and a Codex request to OpenAI (`unpriced`, $0.003 at API prices), totals $0.0045.
- **IC5**: 22 sessions with a malformed line or an unsupported compaction exit with 2, record their 22 requests, print exactly 20 warnings and `2 more warnings`, list all 22 in JSON, and never print the canary.
- **IC6**: a dry run prints exactly what the real run then prints (two recorded, one overlap-skipped, $0.30), and leaves the ledger's hash and missing `-wal` and `-shm` as they were.
- **IC7**: imported spend appears in `branch`, `agents`, and `top-prompts` JSON with its recorded branch `feature/import` (22.5 AI credits, $0.225 at API prices; `explore` 7.5). **IC8**: and in MCP `get_branch_ledger`.
- **IC9**: `IngestFailureLog` writes only the type of a `JsonException` or `ArgumentException` that carries a canary, and the messages of `SqliteException` and `IOException`. **IC10**: the daemon-version warning appears while the lock is held, and not after.
- **IC11**: with only a 1.0.98 session missing, exit code 2 and the `--since` hint; `--since 2026-11-01` leaves it out, exit code 0.

### Acceptance criteria

- Validation, output, and exit codes match the tables above (IC1–IC6, IC11); warnings are content-free and capped (IC5).
- Imported spend reaches reports and MCP (IC7, IC8); `daemon.log` can't receive a request's content (IC9).
- `dotnet test CopilotLedger.slnx` passes **1231** tests (1215 + 16).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 11 specification: The Import Reference and Privacy Allowlist

### Files

| File | Change |
|---|---|
| `docs/cli.md` | "Import past sessions": usage, options, sources and versions, "Overlap with live spans", "The import report", exit codes, and "What import reads and keeps" (the allowlist between `<!-- import-allowlist -->` and `<!-- /import-allowlist -->`); a ledger-format note under upgrading; Privacy and Troubleshooting entries |
| `docs/configuration.md` | "Imported requests", and the `providerNames` field reference |
| `tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs` | New: IP1–IP3 |

### The allowlist rule

Each row of the allowlist names, in its first column, the `table.column` names it covers, in backticks. The documented columns of `spans`, `span_links`, `import_sessions`, and `import_record_aliases`, plus the columns that import leaves empty (`agent_span_id`, `parent_span_id`, `pull_request_number`, `server_address`, and `server_port` of `spans`; `git_branch`, `git_commit_sha`, `git_repository_slug`, and `parent_span_id` of `span_links`), must be exactly the schema's columns. A new column fails IP2 until it's documented or listed as left empty.

### Code (copy exactly)

Edit 1 of 3 in `docs/cli.md`, replace:

````markdown
```

## The daemon

- `gh ledger daemon run [--port <port>] [--log-file <path>]` runs the receiver in the foreground. It logs to standard output, or appends to `--log-file`.
````

with:

````markdown
```

**Ledger format.** A gh-ledger version can upgrade the ledger's format when it first opens it, and an earlier version can't open the ledger after that: it stops with `Database user_version <n> is newer than the supported version <m>.` This version upgrades the ledger to format 6, for `gh ledger import`, the first time its daemon or any command opens it. To keep the option of going back to an earlier version, copy `ledger.db` while the daemon is stopped, before you upgrade.

## The daemon

- `gh ledger daemon run [--port <port>] [--log-file <path>]` runs the receiver in the foreground. It logs to standard output, or appends to `--log-file`.
````

Edit 2 of 3 in `docs/cli.md`, replace:

````markdown
- `gh ledger daemon stop` asks the daemon to stop, waits up to 15 seconds, and then ends its process if it's still running.

The port defaults to 4318. Only one daemon runs at a time. Copilot's telemetry is lost while no daemon is running.

## Reports
````

with:

````markdown
- `gh ledger daemon stop` asks the daemon to stop, waits up to 15 seconds, and then ends its process if it's still running.

The port defaults to 4318. Only one daemon runs at a time. Copilot's telemetry is lost while no daemon is running.

## Import past sessions

`gh ledger import` records requests from the session logs that Copilot CLI and the GitHub Copilot app keep on your machine, and from Codex's if you ask, such as the requests made before you installed gh-ledger or while its daemon wasn't running. It reads only the metadata listed in [What import reads and keeps](#what-import-reads-and-keeps), prices each request with the rates in use as of the request's start time (see [Imported requests](configuration.md#imported-requests)), and records it with the Git context its log recorded, as the daemon records a live request. For a walkthrough, see [Backfill history after installing](guide.md#backfill-history-after-installing).

```text
gh ledger import (--repo <path> | --all) [--source copilot|codex]... [--since <period>] [--dry-run] [--include-active] [--json]
```

- `--repo <path>` imports only requests made in the Git repository at `<path>`, a working-tree path such as `.`, unlike the reports' repository key. A session is in the repository when the repository root it recorded is that repository's root, or, when it recorded none, when its working directory is inside it. `--all` imports requests from every repository and from outside any. Use one of the two.
- `--source copilot`, the default, reads Copilot's session logs, `session-state/<session>/events.jsonl` under `~/.copilot`, or under `COPILOT_HOME` when it's set. `--source codex` reads Codex's rollouts under `sessions` and `archived_sessions` in `~/.codex`, or in `CODEX_HOME` when it's set. Repeat `--source` to read both. `--source claude` is refused: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it.
- `--since` takes the reports' periods and defaults to `all`. Logs last written before the period starts aren't read, and requests that started before it aren't recorded.
- `--dry-run` reads the logs and reports what the import would record. It never creates, upgrades, or writes the ledger.
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
- A live span that arrives after its request was imported supersedes the imported row, in the same transaction: the imported row stays in the ledger but no longer counts in any report, MCP tool, budget, or circuit-breaker rule. A live span without a service request ID supersedes every imported row of its session, and any live span supersedes the imported rows of its session that have no ID.

Importing again records nothing twice. A log that was imported completely is skipped while its size and last-write time stay the same. Any other log is read again, and the requests recorded from it before count as `Duplicate`. An import stopped by Ctrl+C or an error keeps the requests it recorded, and running it again completes it.

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
| Malformed lines | Lines that aren't one JSON event, are longer than 8 MiB, or are incomplete |
| Unsupported usage shapes | Usage records with a missing or invalid field |
| Cost out of range | Requests whose cost is beyond the ledger's range of about $9.2 million |
| Unpriced | Recorded requests without a price; see [Pricing statuses](configuration.md#pricing-statuses) |
| Coverage | `complete`, or `incomplete` when a log or record that may hold requests wasn't imported |

The next line totals the recorded requests' AI credits, GitHub USD, and API-equivalent USD, with the marks of [API-equivalent USD](#api-equivalent-usd); a line with Foundry BYOK USD and local savings follows when either isn't zero. A dry run says what the import would record. Warnings on standard error name the source, the session or rollout, and the line and byte where each problem first occurred, and nothing else from the log; after 20, the rest are counted.

`--json` writes no warnings; it lists them as `diagnostics`. Its fields are `dryRun`, `period`, `from`, `repository` (the `owner/repo` slug, or the root when the repository has none) and `repositoryRoot` (both `null` with `--all`), `coverage`, and:

- `sources`: one object per source, with `source`, `coverage`, `sessionsScanned`, `sessionsSkippedUnchanged`, `sessionsSkippedActive`, `sessionsSkippedOutOfScope`, `sessionsUnsupported`, `sessionsAggregateOnly` (the unsupported sessions from before Copilot 1.0.103), `recordsParsed`, `recordsOutOfScope`, `recorded`, `duplicate`, `overlapSkipped`, `ambiguousOverlap`, `malformedLines`, `unsupportedUsageShapes`, `costOutOfRange`, `journalPredecessors`, `skippedEvents` (events of other types), `unpriced`, `toolLinksRecorded`, and the recorded requests' `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans`.
- `totals`: `recorded` and the same amounts over every source.
- `diagnostics`: each with `source`, `log`, `code`, `event`, `field`, `version`, `count`, `line`, and `byteOffset`.

| Exit code | Meaning |
|---|---|
| 0 | Every source's coverage is complete. |
| 2 | The import finished, but some coverage is incomplete; the warnings say why. Everything that could be imported was. |
| 1 | The options are invalid, or an error stopped the import. |

Skipped active, unchanged, and out-of-scope logs don't make coverage incomplete. When the only gap is sessions from before Copilot 1.0.103, the report's last line suggests `--since`, which leaves them out. With `--repo`, logs of other repositories are read again on every run, because they may hold requests of this one.

Imported requests count in every report and MCP tool, as live ones do, with the branch their log recorded. Tool calls are imported only from the last 7 days, as long as the ledger keeps tool calls; an older request keeps only the number of tool calls it made. Imported requests have no agent-invocation span, so `status --cache --turns` shows their `Growth` as `n/a`, and the input-growth ranking of `--bloat` leaves them out. `import` warns when the daemon runs another gh-ledger version: restart the daemon, so that the live spans it records supersede imported rows by this version's rules.

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
| `import_sessions.log_size_bytes`, `import_sessions.log_last_write_unix_ms` | The log file's size and last-write time | The rollout file's size and last-write time |
| `import_sessions.complete`, `import_sessions.records_recorded`, `import_sessions.records_duplicate`, `import_sessions.records_overlap`, `import_sessions.records_ambiguous`, `import_sessions.imported_at_unix_ms` | The import's own results | The import's own results |
| `import_record_aliases.source`, `import_record_aliases.record_key_sha256` | `copilot`, and SHA-256 of the record's usage ID, or of a compaction's service request ID | `codex`, and SHA-256 of `payload.response_id` |
| `import_record_aliases.span_row_id`, `import_record_aliases.resolution`, `import_record_aliases.recorded_at_unix_ms` | Kept by the ledger | Kept by the ledger |
<!-- /import-allowlist -->

Imported rows leave `parent_span_id`, `server_address`, `server_port`, `pull_request_number`, and `agent_span_id` of `spans` empty, and imported tool calls leave `parent_span_id`, `git_repository_slug`, `git_branch`, and `git_commit_sha` of `span_links` empty.

- **Read, never stored:** each line's `type`. From Copilot logs: `data.producer`, `data.version`, and `data.copilotVersion` of `session.start`; the working directory, `data.context.cwd`, which only `--repo` uses; each event's `agentId`; `data.usage.isByok`, `data.usage.provider.kind`, `data.usage.initiator`, and `data.usage.parentToolCallId` of usage records; and `data.success`, `data.compactionTokensUsed.requestCount`, and `data.compactionTokensUsed.provider.kind` of compactions, which decide how a record is read. Usage IDs, service request IDs, and tool call IDs are stored only as the SHA-256 hashes above. From Codex rollouts: `payload.id` (the thread ID), `payload.cli_version`, and `payload.cwd` of `session_meta`, and `payload.thread_id`, `payload.turn_id`, and `payload.root_turn_id` of `token_usage_record`.
- **Never read:** every other field and event, including user and assistant messages, reasoning, tool arguments and results, error messages, compaction summaries (`summaryContent`) and checkpoint paths, instructions, and attachments. Import skips a line as soon as its `type` shows it's none of the events above, and it counts a line that isn't valid JSON without echoing it. Logs are read in place, never copied.

## Reports
````

Edit 3 of 3 in `docs/cli.md`, replace:

```markdown
## Privacy

CopilotLedger stores no prompt or response content, working directories, or remote URLs. Repositories are stored as `owner/repo` slugs or local roots, and credentials are stripped.

## Troubleshooting

- **Nothing is recorded.** Run `gh ledger status`, address its warnings, and restart Copilot.
- **`daemon.log` says `Warning: could not record spans`.** The ledger couldn't be written, for example because the disk is full or the database is damaged, and Copilot is told to retry. The warning repeats at most once a minute; fix the cause shown after it.
- **Port 4318 is in use.** Stop the other program or the old daemon, then run `gh ledger daemon start`.
- **`gh extension upgrade` or `remove` fails on Windows.** A program is running the extension's binary: the daemon, or the MCP server of a Copilot session. Run `gh ledger daemon stop`, close the Copilot CLI sessions, the GitHub Copilot app, and VS Code, then try again.
```

with:

```markdown
## Privacy

CopilotLedger stores no prompt or response content, working directories, or remote URLs. Repositories are stored as `owner/repo` slugs or local roots, and credentials are stripped. `gh ledger import` keeps only the fields in [What import reads and keeps](#what-import-reads-and-keeps), and never reads prompts, responses, tool arguments or results, or compaction summaries.

## Troubleshooting

- **Nothing is recorded.** Run `gh ledger status`, address its warnings, and restart Copilot.
- **`daemon.log` says `Warning: could not record spans`.** The ledger couldn't be written, for example because the disk is full or the database is damaged, and Copilot is told to retry. The warning repeats at most once a minute; fix the cause shown after it. Only database and file errors show their message; any other error shows only its type, because its message could quote the request.
- **`gh ledger import` exits with code 2.** Some coverage is incomplete, and the warnings say why; everything else was imported. A session from before Copilot 1.0.103 is never imported, so leave it out with `--since`. A malformed line or an unsupported usage record is skipped on every run, and the rest of its log is imported.
- **`gh ledger import` defers requests (`Ambiguous-overlap`).** Their session has live spans that can't be matched to them, such as spans an earlier gh-ledger recorded, so import leaves them out rather than risk charging a request twice. The live spans still count.
- **Port 4318 is in use.** Stop the other program or the old daemon, then run `gh ledger daemon start`.
- **`gh extension upgrade` or `remove` fails on Windows.** A program is running the extension's binary: the daemon, or the MCP server of a Copilot session. Run `gh ledger daemon stop`, close the Copilot CLI sessions, the GitHub Copilot app, and VS Code, then try again.
```

Edit 1 of 2 in `docs/configuration.md`, replace:

```markdown
- Prices start and end at 00:00 UTC. A promotion ends the day after its last day. Google publishes the prices that follow its promotions; OpenAI publishes none after GPT-5.6 Sol's, which ends on 2026-12-01. A retired or shut-down model has no list price from that date: Claude Sonnet 4 from 2026-06-15, and GPT-5.3-Codex and GPT-5.4 nano from 2027-04-01. Vendors publish only current prices, so they're assumed for earlier dates, except after a documented change: GPT-5.6 Luna's prices start on 2026-07-30, and GPT-5.6 Sol's on 2026-08-21.
- Vendor API ids, such as `claude-opus-4-8`, and dated snapshot ids, such as `claude-haiku-4-5-20251001`, are aliases.

## Why Foundry BYOK is unpriced by default
```

with:

```markdown
- Prices start and end at 00:00 UTC. A promotion ends the day after its last day. Google publishes the prices that follow its promotions; OpenAI publishes none after GPT-5.6 Sol's, which ends on 2026-12-01. A retired or shut-down model has no list price from that date: Claude Sonnet 4 from 2026-06-15, and GPT-5.3-Codex and GPT-5.4 nano from 2027-04-01. Vendors publish only current prices, so they're assumed for earlier dates, except after a documented change: GPT-5.6 Luna's prices start on 2026-07-30, and GPT-5.6 Sol's on 2026-08-21.
- Vendor API ids, such as `claude-opus-4-8`, and dated snapshot ids, such as `claude-haiku-4-5-20251001`, are aliases.

### Imported requests

`gh ledger import` prices the requests it reads from session logs as the daemon prices live ones: with the rates in use when you run it, your overlay and downloaded rates included, as of each request's start time. A request outside every price window of its model is `no_rate_for_date`; it's never priced at today's price. Each request's response and request models are passed as the log recorded them, so [Which model prices a request](#which-model-prices-a-request) applies unchanged.

A session log has no endpoint, so the channel comes from who billed the request:

- A Copilot request that GitHub billed, and a Copilot compaction, are priced as a live request without an endpoint is: with GitHub AI credits, or as local when the model is one of `localOffload.models`.
- A Copilot BYOK request is local when its `providerType` is in `localOffload.providerNames`, Foundry BYOK when it's in `foundryByok.providerNames`, and `other_provider` otherwise.
- A Codex request is classified the same way by its `model_provider`, with Codex's `lmstudio` matched as `lm_studio`. A Codex request to OpenAI is `other_provider`.

So a Copilot BYOK request through a local server that speaks OpenAI's API, which Copilot logs with provider type `openai`, is `other_provider` when it's imported, even though its live span counts as local when its endpoint is `localhost`. An `other_provider` request bills nothing in the ledger, and it still gets an API-equivalent cost when its model has a list price.

## Why Foundry BYOK is unpriced by default
```

Edit 2 of 2 in `docs/configuration.md`, replace:

```markdown
| `notes` | No | Strings for your own notes |
| `githubAiCredits.hostSuffixes`, `foundryByok.hostSuffixes` | No | Added to the built-in lists |
| `foundryByok.providerNames`, `localOffload.providerNames` | No | Matched against `gen_ai.provider.name` |
| `githubAiCredits.models`, `foundryByok.models` | No | Priced models (see the next table) |
| `localOffload.models` | No | Local model entries: `{ "id": "...", "aliases": ["..."] }` |
```

with:

```markdown
| `notes` | No | Strings for your own notes |
| `githubAiCredits.hostSuffixes`, `foundryByok.hostSuffixes` | No | Added to the built-in lists |
| `foundryByok.providerNames`, `localOffload.providerNames` | No | Matched against `gen_ai.provider.name`, and against the provider of an imported BYOK or Codex request (see [Imported requests](#imported-requests)) |
| `githubAiCredits.models`, `foundryByok.models` | No | Priced models (see the next table) |
| `localOffload.models` | No | Local model entries: `{ "id": "...", "aliases": ["..."] }` |
```

New file `tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// What gh ledger import may keep: only the columns that docs/cli.md lists between its import-allowlist markers, and nothing
// from a content-bearing field of a session log.
public sealed partial class ImportPrivacyTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private const string S4 = "44444444-4444-4444-8444-444444444444";
    private const string Thread = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    // The columns that imported rows (spans), imported tool calls (span_links), and import's own tables leave empty.
    private static readonly Dictionary<string, string[]> LeftEmpty = new(StringComparer.Ordinal)
    {
        ["spans"] = ["agent_span_id", "parent_span_id", "pull_request_number", "server_address", "server_port"],
        ["span_links"] = ["git_branch", "git_commit_sha", "git_repository_slug", "parent_span_id"],
        ["import_sessions"] = [],
        ["import_record_aliases"] = [],
    };

    private static readonly string[] Canaries =
    [
        CopilotSessionLogs.Canary,
        Canary("PROMPT"),
        Canary("RESPONSE"),
        Canary("ARGS"),
        Canary("RESULT"),
        Canary("PATH"),
        Canary("ERRMSG"),
        Canary("TOKEN"),
    ];

    [Fact]
    public async Task IP1_NoCanaryReachesTheLedgerOrTheOutput()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        WriteFixtures(temp, harness);

        CliRun claude = await harness.RunAsync("import", "--all", "--source", "claude");
        CliRun dryRun = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex", "--dry-run");
        CliRun text = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex");
        CliRun json = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex", "--json");

        Assert.Equal((1, 2, 2, 2), (claude.ExitCode, dryRun.ExitCode, text.ExitCode, json.ExitCode));
        Assert.Contains("Recorded 6 requests: ", text.Out);
        Assert.Contains("Warning: Copilot session s-canary, line ", text.Error);
        Assert.Equal(6L, await LedgerSql.ScalarAsync(harness.Context.DatabasePath, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(3L, await LedgerSql.ScalarAsync(harness.Context.DatabasePath, "SELECT COUNT(*) FROM span_links;"));
        List<(string Name, byte[] Bytes)> stored = [];
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string path = harness.Context.DatabasePath + suffix;
            if (File.Exists(path))
            {
                await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using MemoryStream copy = new();
                await stream.CopyToAsync(copy);
                stored.Add(("ledger.db" + suffix, copy.ToArray()));
            }
        }

        Assert.Contains(stored, file => file.Name == "ledger.db");
        string output = string.Concat(
            claude.Out, claude.Error, dryRun.Out, dryRun.Error, text.Out, text.Error, json.Out, json.Error);
        foreach (string canary in Canaries)
        {
            Assert.DoesNotContain(canary, output, StringComparison.Ordinal);
            byte[] bytes = Encoding.UTF8.GetBytes(canary);
            foreach ((string name, byte[] content) in stored)
            {
                Assert.True(content.AsSpan().IndexOf(bytes) < 0, $"{canary} is in {name}.");
            }
        }
    }

    [Fact]
    public async Task IP2_TheSchemaIsTheDocumentedColumnsAndTheOnesImportLeavesEmpty()
    {
        using TempDirectory temp = new();
        string database = Path.Combine(temp.Path, "ledger.db");
        await new SqliteLedgerStore(database).InitializeAsync();
        Dictionary<string, SortedSet<string>> documented = DocumentedColumns();

        Assert.Equal(LeftEmpty.Keys.Order(StringComparer.Ordinal), documented.Keys.Order(StringComparer.Ordinal));
        foreach ((string table, string[] empty) in LeftEmpty)
        {
            Assert.Empty(documented[table].Intersect(empty));
            Assert.Equal(
                documented[table].Union(empty).Order(StringComparer.Ordinal),
                await LedgerSql.RowsAsync(database, $"SELECT name FROM pragma_table_info('{table}') ORDER BY name;"));
        }
    }

    [Fact]
    public async Task IP3_ImportFillsExactlyTheDocumentedColumns()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        WriteFixtures(temp, harness);
        CliRun run = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex");
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("s-repo", S4)]);
        Dictionary<string, SortedSet<string>> documented = DocumentedColumns();

        Assert.Equal(2, run.ExitCode);
        foreach ((string table, string imported) in new[]
                 {
                     ("spans", "ingest_source <> 'otlp'"),
                     ("span_links", "owner_span_row_id IS NOT NULL"),
                     ("import_sessions", "1"),
                     ("import_record_aliases", "1"),
                 })
        {
            SortedSet<string> filled = new(StringComparer.Ordinal);
            foreach (string column in await LedgerSql.RowsAsync(
                         harness.Context.DatabasePath,
                         $"SELECT name FROM pragma_table_info('{table}');"))
            {
                if ((long)(await LedgerSql.ScalarAsync(
                        harness.Context.DatabasePath,
                        $"SELECT COUNT(*) FROM {table} WHERE {imported} AND \"{column}\" IS NOT NULL;"))! > 0)
                {
                    filled.Add(column);
                }
            }

            Assert.Equal(documented[table], filled);
        }
    }

    private static string Canary(string kind) => $"CANARY-{kind}-7c1d";

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    // A Copilot session with every kind of record, sub-agent, and tool call, and canaries in content-bearing fields, in
    // lines that aren't events, and in a working directory that's read but never kept; one in a repository whose remote URL
    // has a credential; and a Codex rollout with canaries in its content and its remote URL.
    private static void WriteFixtures(TempDirectory temp, CliHarness harness)
    {
        string cwd = Path.Combine(temp.Path, Canary("PATH"), "work");
        new CopilotSessionLogs("s-canary", "1.0.114", context: new JsonObject { ["cwd"] = cwd })
            .Event("user.message", new JsonObject { ["content"] = Canary("PROMPT"), ["transformedContent"] = Canary("PROMPT") })
            .Raw("{\"data\":{\"content\":\"" + Canary("PROMPT") + "\"},\"type\":\"user.message\"}")
            .Usage(
                "u1",
                S1,
                responseModel: "claude-haiku-4-5-20251001",
                numToolCalls: 2,
                change: usage => usage[Canary("ARGS")] = Canary("ARGS"))
            .Event("assistant.message", new JsonObject { ["content"] = Canary("RESPONSE"), ["reasoningText"] = Canary("RESPONSE") })
            .ToolStart("toolu-1", "bash")
            .ToolComplete("toolu-1", true, exitCode: 0)
            .Event(
                "tool.execution_start",
                new JsonObject
                {
                    ["toolCallId"] = "toolu-2",
                    ["toolName"] = "view",
                    ["arguments"] = new JsonObject { ["path"] = Canary("PATH"), ["command"] = Canary("ARGS") },
                })
            .Event(
                "tool.execution_complete",
                new JsonObject
                {
                    ["toolCallId"] = "toolu-2",
                    ["success"] = false,
                    ["result"] = new JsonObject { ["content"] = Canary("RESULT") },
                    ["error"] = new JsonObject { ["code"] = "failure", ["message"] = Canary("ERRMSG") },
                })
            .SubagentStarted("agent-1", "explore", "toolu-9")
            .Usage("u2", S2, agentId: "agent-1", parentToolCallId: "toolu-9")
            .ToolStart("toolu-3", "grep", agentId: "agent-1")
            .ToolComplete("toolu-3", false, agentId: "agent-1")
            .Usage("u3", null, model: "qwen3.8-27b", providerType: "ollama")
            .Compaction(
                S3,
                change: data =>
                {
                    data["summaryContent"] = Canary("RESPONSE");
                    data["checkpointPath"] = Canary("PATH");
                    data["error"] = Canary("ERRMSG");
                    data["failureReason"] = Canary("ERRMSG");
                })
            .Raw("{\"type\":\"user.message\",\"data\":{\"content\":\"" + Canary("PROMPT") + "\"")
            .Raw("{\"type\":\"" + Canary("ERRMSG") + "\",\"data\":{\"" + Canary("ARGS") + "\":1}}")
            .Write(harness.Context.CopilotHome);
        string repo = GitFixture.Create(temp.Path);
        new CopilotSessionLogs(
                "s-repo",
                context: CopilotSessionLogs.Context(
                    repo,
                    repo,
                    "feature/import",
                    $"https://user:{Canary("TOKEN")}@github.com/octo-org/octo-repo.git",
                    GitFixture.Sha2))
            .UserMessage()
            .Usage("u4", S4)
            .Write(harness.Context.CopilotHome);
        new CodexRollouts(
                Thread,
                agentRole: "reviewer",
                cwd: cwd,
                git: new JsonObject
                {
                    ["commit_hash"] = GitFixture.Sha1,
                    ["branch"] = "main",
                    ["repository_url"] = $"https://{Canary("TOKEN")}@github.com/octo-org/octo-repo.git",
                })
            .TurnContext("turn-1", "gpt-6.1-sol")
            .UserMessage()
            .Line(
                "response_item",
                new JsonObject { ["type"] = "function_call", ["name"] = "shell", ["arguments"] = Canary("ARGS"), ["call_id"] = "call-1" })
            .Line(
                "response_item",
                new JsonObject { ["type"] = "function_call_output", ["call_id"] = "call-1", ["output"] = Canary("RESULT") })
            .Line("event_msg", new JsonObject { ["type"] = "agent_message", ["message"] = Canary("RESPONSE") })
            .Line("event_msg", new JsonObject { ["type"] = "error", ["message"] = Canary("ERRMSG") })
            .Usage("resp-1", "turn-1", change: payload => payload[Canary("ARGS")] = Canary("PROMPT"))
            .Write(harness.Context.CodexHome);
    }

    // The table.column names in the first column of docs/cli.md's import allowlist, by table.
    private static Dictionary<string, SortedSet<string>> DocumentedColumns()
    {
        string[] lines = File.ReadAllLines(RepositoryFile("docs", "cli.md"));
        int start = Array.IndexOf(lines, "<!-- import-allowlist -->");
        int end = Array.IndexOf(lines, "<!-- /import-allowlist -->");
        Assert.True(start >= 0 && end > start);
        Dictionary<string, SortedSet<string>> columns = new(StringComparer.Ordinal);
        foreach (string line in lines[(start + 1)..end].Where(line => line.StartsWith("| `", StringComparison.Ordinal)))
        {
            foreach (Match match in ColumnName().Matches(line.Split('|')[1]))
            {
                string table = match.Groups[1].Value;
                if (!columns.TryGetValue(table, out SortedSet<string>? names))
                {
                    names = new SortedSet<string>(StringComparer.Ordinal);
                    columns.Add(table, names);
                }

                Assert.True(names.Add(match.Groups[2].Value), $"{match.Value} is listed twice.");
            }
        }

        return columns;
    }

    // A file of the repository, found from the test's output directory: the tests copy only some docs there.
    private static string RepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CopilotLedger.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }

    [GeneratedRegex(@"`([a-z_]+)\.([a-z0-9_]+)`")]
    private static partial Regex ColumnName();
}
```

### Tests

- **IP1**: Copilot and Codex fixtures put `CANARY-PROMPT-7c1d`, `CANARY-RESPONSE-7c1d`, `CANARY-ARGS-7c1d`, `CANARY-RESULT-7c1d`, `CANARY-PATH-7c1d`, `CANARY-ERRMSG-7c1d`, `CANARY-TOKEN-7c1d`, and the builders' canary in messages, reasoning, tool arguments and results, error messages, compaction summaries and checkpoint paths, a working directory, unknown keys and event types, a line with content before `type`, a malformed line, and credentials of remote URLs. After `--source claude` (refused), a dry run, a text run, and a JSON run (exit codes 1, 2, 2, 2; 6 requests and 3 tool links recorded), no canary is in `ledger.db`, its `-wal` or `-shm`, standard output, standard error, or JSON.
- **IP2**: the schema of the four tables is exactly the documented columns plus the ones import leaves empty, and the two lists don't overlap.
- **IP3**: after an import of every record kind (usage, sub-agent, BYOK local, compaction, tool calls, Codex) and a live span that supersedes one import, the columns that imported rows, owned links, and import's tables fill are exactly the documented ones.

### Acceptance criteria

- `docs/cli.md`'s allowlist matches the schema and what import writes (IP2, IP3); no canary leaks (IP1).
- `dotnet test CopilotLedger.slnx` passes **1234** tests (1231 + 3).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 12 specification: The Backfill Walkthrough

### Files

| File | Change |
|---|---|
| `docs/guide.md` | "Backfill history after installing", after "Set up CopilotLedger", with samples `import-dry-run` and `import`; the contents and the intro's sentence about sample state |
| `README.md` | A feature line, the guide's topics, and a "Privacy" section |
| `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs` | New `GuideState.Backfill`: the first prompt's state plus Copilot 1.0.110 logs |
| `tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs` | Maps both samples to `GuideState.Backfill` |

### The Backfill state

`GuideState.Backfill` is `FirstPrompt` (installed at 16:20, the daemon running, one live prompt at 16:25 in session `7d4e1c2a-5b3f-4e8d-9a6c-00000000001f`) plus four session logs under `CopilotHome`: two on 2026-11-20 in `contoso/webapp` on `feature/search` (one with an `explore` sub-agent request, one with a compaction), one on 2026-11-19 in `contoso/api`, and the current session, open (`inuse.4242.lock`). `gh ledger import --repo .` therefore scans 4 sessions, skips 1 as active, parses 6 records, leaves 1 out of scope, and records 5: $0.086 + $0.016 + $0.11 + $0.098 + $0.077 = $0.387, so `38.70 AI credits ($0.39), API equivalent $0.39`. Coverage is complete, so both samples exit with 0 and an empty standard error, as GS2 requires.

### Code (copy exactly)

Edit 1 of 2 in `README.md`, replace:

```markdown
- Foundry BYOK prices, local-model savings, and `rates update` for new prices between releases
- API-equivalent USD: what the same requests would cost at each model's API list prices, or at prices you supply, beside what Copilot billed

## Requirements
```

with:

```markdown
- Foundry BYOK prices, local-model savings, and `rates update` for new prices between releases
- API-equivalent USD: what the same requests would cost at each model's API list prices, or at prices you supply, beside what Copilot billed
- `gh ledger import`: backfill the requests in your Copilot CLI and GitHub Copilot app session logs, and in Codex's, from before you installed CopilotLedger

## Requirements
```

Edit 2 of 2 in `README.md`, replace:

````markdown
```

The [user guide](docs/guide.md) shows the everyday tasks with sample output: reports, budgets, the circuit breaker, pull request receipts, Foundry BYOK and local models, MCP, updates, and uninstalling.

## Documentation
````

with:

````markdown
```

The [user guide](docs/guide.md) shows the everyday tasks with sample output: backfilling history, reports, budgets, the circuit breaker, pull request receipts, Foundry BYOK and local models, MCP, updates, and uninstalling.

## Privacy

The ledger stays on your machine, and CopilotLedger never stores prompt or response content. `gh ledger import` reads Copilot's and Codex's session logs in place and keeps only what pricing and attribution need: models, token counts, times, agent names, the branch, commit, and `owner/repo` slug that a session recorded, tool names and exit codes, and SHA-256 hashes of request IDs. It never reads prompts, responses, tool arguments or results, or compaction summaries, and it doesn't read Claude Code's logs. See [What import reads and keeps](docs/cli.md#what-import-reads-and-keeps).

## Documentation
````

Edit 1 of 2 in `docs/guide.md`, replace:

```markdown
CopilotLedger records what GitHub Copilot spends and shows it by repository, branch, model, agent, and prompt. This guide walks through everyday tasks, one scenario at a time. For every command and option, see the [command reference](cli.md).

The samples come from one example: gh-ledger installed as the GitHub CLI extension on Windows, used in the `contoso/webapp` repository on the `feature/search` branch at 16:30 on 2026-11-21, local time (UTC-5). After setup, every sample starts from the same state: a month of spend, a rates overlay with Foundry BYOK prices, a budget for `feature/search`, and an open circuit-breaker trip, so early samples show things that later sections explain. A test runs every sample, so each one is exactly what gh-ledger prints. Your numbers will differ. gh-ledger's messages name its commands `gh-ledger <command>`; with the extension, run them as `gh ledger <command>`.

- [Set up CopilotLedger](#set-up-copilotledger)
- [See what you spent](#see-what-you-spent)
- [Find the most expensive prompts](#find-the-most-expensive-prompts)
```

with:

```markdown
CopilotLedger records what GitHub Copilot spends and shows it by repository, branch, model, agent, and prompt. This guide walks through everyday tasks, one scenario at a time. For every command and option, see the [command reference](cli.md).

The samples come from one example: gh-ledger installed as the GitHub CLI extension on Windows, used in the `contoso/webapp` repository on the `feature/search` branch at 16:30 on 2026-11-21, local time (UTC-5). After setup and backfill, every sample starts from the same state: a month of spend, a rates overlay with Foundry BYOK prices, a budget for `feature/search`, and an open circuit-breaker trip, so early samples show things that later sections explain. A test runs every sample, so each one is exactly what gh-ledger prints. Your numbers will differ. gh-ledger's messages name its commands `gh-ledger <command>`; with the extension, run them as `gh ledger <command>`.

- [Set up CopilotLedger](#set-up-copilotledger)
- [Backfill history after installing](#backfill-history-after-installing)
- [See what you spent](#see-what-you-spent)
- [Find the most expensive prompts](#find-the-most-expensive-prompts)
```

Edit 2 of 2 in `docs/guide.md`, replace:

````markdown
<!-- /sample -->

If no spans appear, fix any warnings that `status` shows, and see [Troubleshooting](cli.md#troubleshooting).

## See what you spent
````

with:

````markdown
<!-- /sample -->

If no spans appear, fix any warnings that `status` shows, and see [Troubleshooting](cli.md#troubleshooting).

## Backfill history after installing

**Goal:** add the Copilot requests you made before you installed CopilotLedger.

CopilotLedger records requests from the moment its daemon runs, but Copilot CLI and the GitHub Copilot app also keep a log of each session on your machine. `gh ledger import` records the requests in those logs, prices each one for the date it was made, and attributes it to the branch its session was on. See what it would record for this repository first:

<!-- sample: import-dry-run -->
```shell
gh ledger import --repo . --dry-run
```

```text
Import from local session logs for contoso/webapp (all time, dry run)

                           Copilot
Sessions scanned                 4
Skipped, unchanged               0
Skipped, active                  1
Skipped, out of scope            0
Unsupported sessions             0
Records parsed                   6
Out of scope                     1
Recorded                         5
Duplicate                        0
Overlap-skipped                  0
Ambiguous-overlap                0
Malformed lines                  0
Unsupported usage shapes         0
Cost out of range                0
Unpriced                         0
Coverage                  complete

Importing would record 5 requests: 38.70 AI credits ($0.39), API equivalent $0.39.
Dry run: nothing was written.
```
<!-- /sample -->

Two sessions from yesterday hold five requests, including one by the `explore` subagent and a compaction, the request that summarizes a long conversation. A session in `contoso/api` is out of scope, and the session you're in now is still open, so it's skipped: CopilotLedger records it live. Import the five:

<!-- sample: import -->
```shell
gh ledger import --repo .
```

```text
Import from local session logs for contoso/webapp (all time)

                           Copilot
Sessions scanned                 4
Skipped, unchanged               0
Skipped, active                  1
Skipped, out of scope            0
Unsupported sessions             0
Records parsed                   6
Out of scope                     1
Recorded                         5
Duplicate                        0
Overlap-skipped                  0
Ambiguous-overlap                0
Malformed lines                  0
Unsupported usage shapes         0
Cost out of range                0
Unpriced                         0
Coverage                  complete

Recorded 5 requests: 38.70 AI credits ($0.39), API equivalent $0.39.
```
<!-- /sample -->

They now count in every report. Importing again records nothing twice, and a request that the daemon also recorded is charged once. Import reads only what pricing and attribution need, never your prompts or Copilot's responses. Use `--all` for every repository and `--source codex` for Codex's logs; see [Import past sessions](cli.md#import-past-sessions).

## See what you spent
````

Edit 1 of 1 in `tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs`, replace:

```csharp
        ["install"] = GuideState.Fresh,
        ["status-first"] = GuideState.FirstPrompt,
        ["status"] = GuideState.Month,
        ["branch"] = GuideState.Month,
```

with:

```csharp
        ["install"] = GuideState.Fresh,
        ["status-first"] = GuideState.FirstPrompt,
        ["import-dry-run"] = GuideState.Backfill,
        ["import"] = GuideState.Backfill,
        ["status"] = GuideState.Month,
        ["branch"] = GuideState.Month,
```

Edit 1 of 4 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
    Fresh,          // nothing installed and no ledger yet
    FirstPrompt,    // installed at 16:20, the daemon running, one prompt at 16:25
    Month,          // installed, the daemon running since 08:02, a month of spend, one breaker trip, a budget, and an overlay
}
```

with:

```csharp
    Fresh,          // nothing installed and no ledger yet
    FirstPrompt,    // installed at 16:20, the daemon running, one prompt at 16:25
    Backfill,       // FirstPrompt, and Copilot's session logs of earlier sessions
    Month,          // installed, the daemon running since 08:02, a month of spend, one breaker trip, a budget, and an overlay
}
```

Edit 2 of 4 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
        {
            GuideState.Fresh => Now,
            GuideState.FirstPrompt => Utc("2026-11-21T21:20:00Z"),
            _ => Utc("2026-11-21T13:02:00Z"),
        };
```

with:

```csharp
        {
            GuideState.Fresh => Now,
            GuideState.FirstPrompt or GuideState.Backfill => Utc("2026-11-21T21:20:00Z"),
            _ => Utc("2026-11-21T13:02:00Z"),
        };
```

Edit 3 of 4 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
        SqliteLedgerStore store = new(Harness.Context.DatabasePath, _clock);
        await store.InitializeAsync();
        if (state == GuideState.FirstPrompt)
        {
            JsonRateCardCalculator builtIn = JsonRateCardCalculator.LoadEmbedded();
            NormalizedGenAiSpan first = Chat("1f", "c1", "2026-11-21T21:25:00Z", "claude-opus-5.5", null, null, 30_000, 1_500, 20_000);
            await store.RecordAsync([new LedgerEntry(first, builtIn.Calculate(first), Context("feature/search"))]);
            return;
        }
```

with:

```csharp
        SqliteLedgerStore store = new(Harness.Context.DatabasePath, _clock);
        await store.InitializeAsync();
        if (state is GuideState.FirstPrompt or GuideState.Backfill)
        {
            JsonRateCardCalculator builtIn = JsonRateCardCalculator.LoadEmbedded();
            NormalizedGenAiSpan first = Chat("1f", "c1", "2026-11-21T21:25:00Z", "claude-opus-5.5", null, null, 30_000, 1_500, 20_000);
            await store.RecordAsync([new LedgerEntry(first, builtIn.Calculate(first), Context("feature/search"))]);
            if (state == GuideState.Backfill)
            {
                WriteSessionLogs();
            }

            return;
        }
```

Edit 4 of 4 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
        Slug);

    // trace and span are the last characters of 32- and 16-character IDs. GitHub-hosted models report cache counters, and
    // this example's Foundry BYOK and local endpoints (the spans with a serverAddress) don't. A request's parent is its
```

with:

```csharp
        Slug);

    // Copilot 1.0.110's logs, as Copilot CLI writes them under COPILOT_HOME: two sessions in this repository on 2026-11-20,
    // before CopilotLedger was installed, one with an explore subagent and one with a compaction; one in contoso/api; and the
    // session of the prompt at 16:25, which is still open.
    private void WriteSessionLogs()
    {
        string home = Harness.Context.CopilotHome;
        string api = Path.Combine(Path.GetDirectoryName(Repository)!, "api");
        new CopilotSessionLogs(
                "3f9b2c71-6d4e-4a8b-9c1d-2e5f7a9b0c11",
                start: Utc("2026-11-20T14:00:00Z"),
                context: CopilotSessionLogs.Context(Repository, Repository, "feature/search", Slug, Commit))
            .UserMessage()
            .Usage("msg_01A", "0b6f3a2e-9c41-4d7a-8e25-6b1f0c9d3a71", "claude-opus-5.5", input: 40_000, output: 2_000, cacheRead: 30_000)
            .SubagentStarted("agent-1", "explore", "toolu_01")
            .Usage(
                "msg_01B",
                "1c7a4b3f-0d52-4e8b-9f36-7c2a1d0e4b82",
                "claude-haiku-4.5",
                input: 20_000,
                output: 1_000,
                cacheRead: 10_000,
                initiator: "agent",
                agentId: "agent-1")
            .Usage(
                "msg_01C",
                "2d8b5c4a-1e63-4f9c-8a47-8d3b2e1f5c93",
                "claude-opus-5.5",
                input: 60_000,
                output: 3_000,
                cacheRead: 50_000,
                initiator: "agent")
            .Write(home);
        new CopilotSessionLogs(
                "8c2d4e6f-1a3b-4c5d-8e7f-9a0b1c2d3e4f",
                start: Utc("2026-11-20T18:00:00Z"),
                context: CopilotSessionLogs.Context(Repository, Repository, "feature/search", Slug, Commit))
            .UserMessage()
            .Usage("msg_02A", "3e9c6d5b-2f74-4a0d-9b58-9e4c3f2a6d04", "claude-opus-5.5", input: 50_000, output: 2_500, cacheRead: 40_000)
            .Compaction(
                "4fad7e6c-3a85-4b1e-8c69-af5d4a3b7e15",
                "claude-haiku-4.5",
                input: 80_000,
                output: 500,
                cacheRead: 20_000,
                cacheWrite: 50_000,
                duration: 900)
            .Write(home);
        new CopilotSessionLogs(
                "c41e7a9b-2d3f-4b5c-8d6e-7f8a9b0c1d2e",
                start: Utc("2026-11-19T15:00:00Z"),
                context: CopilotSessionLogs.Context(api, api, "main", "contoso/api", Commit))
            .UserMessage()
            .Usage("msg_03A", "5abe8f7d-4b96-4c2f-9d7a-b06e5b4c8f26", "claude-opus-5.5", input: 30_000, output: 1_000)
            .Write(home);
        new CopilotSessionLogs(
                "7d4e1c2a-5b3f-4e8d-9a6c-00000000001f",
                start: Utc("2026-11-21T21:24:00Z"),
                context: CopilotSessionLogs.Context(Repository, Repository, "feature/search", Slug, Commit))
            .UserMessage()
            .Usage("msg_04A", "6bcf908e-5ca7-4d3a-8e8b-c17f6c5d9037", "claude-opus-5.5", input: 30_000, output: 1_500, cacheRead: 20_000)
            .Write(home, active: true);
    }

    // trace and span are the last characters of 32- and 16-character IDs. GitHub-hosted models report cache counters, and
    // this example's Foundry BYOK and local endpoints (the spans with a serverAddress) don't. A request's parent is its
```

### Tests

- **GS1** (changed): the guide's samples include `import-dry-run` and `import`.
- **GS2** (2 more cases): both samples print exactly what the guide shows.

### Acceptance criteria

- `docs/guide.md` walks through backfill with checked samples; the README describes import's privacy.
- `dotnet test CopilotLedger.slnx` passes **1236** tests (1234 + 2).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 13 specification: Parser and Pricing Review Fixes

Review findings 2, 7, 10, and 12 (see "Review fixes (Tasks 13–16)").

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Git/GitRemoteUrl.cs` | `GetRepositorySlug` reads only a URL's path and accepts only a strict `owner/repo` |
| `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs` | The hostless known-local-model rule applies only to `Endpoint` spans |
| `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs` | A duration that would start a record before `DateTimeOffset.MinValue` is an unsupported shape |
| `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs` | A `token_usage_record` that gives an allowlisted field twice is unsupported before its thread is read |
| `tests/CopilotLedger.Tests/Git/GitRemoteUrlTests.cs` | 4 more cases of `UrlHelpers_ReturnExpectedValues` |
| `tests/CopilotLedger.Tests/Pricing/BillingSourceTests.cs` | New: BI8 (3) |
| `tests/CopilotLedger.Tests/Import/CopilotRecordTests.cs` | New: CU13, CU14 (3) |
| `tests/CopilotLedger.Tests/Import/CopilotCompactionTests.cs` | New: CC7 |
| `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs` | New: CX13 (2), CX14 |

### Rules

1. **Slugs (finding 2).** `GetRepositorySlug` trims the value, cuts it at the first `?` or `#`, drops one trailing `/` and then a trailing `.git`, and takes the path after `scheme://host/` (a `file://` URL has none) or after the colon of an SCP-style `host:path` (a Windows drive path has none). The slug is `owner/repo` only when that path has exactly two non-empty segments of 1 to 100 ASCII letters, digits, `.`, `_`, or `-`; anything else is `null`. So `https://git.example/o/r.git?token=…` and `git@git.example:o/r.git?token=…` give `o/r`, and `https://git.example/o/r%3Ftoken.git` gives `null`. The live path (the normalizer and `FileSystemGitContextResolver`) and import (`SessionLogs.RepositorySlug`) share the function, so both are fixed.
2. **Provider-billed local pricing (finding 7).** In `Calculate`, `localByKnownModel` requires `BillingSource == Endpoint`, no host, and a known local model. A `Provider` span is local only by its provider name (`localOffload.providerNames`).
3. **Durations (finding 10).** A usage record's or compaction's start is its end less its duration. When `end − DateTimeOffset.MinValue` is shorter than the duration, the record is an unsupported shape with field `data.usage.duration` or `data.compactionTokensUsed.duration`, checked after every other field, instead of an exception that stops the import.
4. **Duplicated Codex fields (finding 12).** `Usage` checks `JsonFields.Duplicate` first: a record that gives an allowlisted path twice counts as parsed and unsupported, with that path as its field, before `payload.thread_id` decides whether it's inherited.

### Code (copy exactly)

Edit 1 of 2 in `src/CopilotLedger.Core/Git/GitRemoteUrl.cs`, replace:

```csharp
    }

    public static string? GetRepositorySlug(string remoteUrl)
    {
        ArgumentNullException.ThrowIfNull(remoteUrl);
        string value = remoteUrl.Trim();
        if (value.EndsWith('/'))
        {
```

with:

```csharp
    }

    // The "owner/repo" of a remote URL or an SCP-style address: its path, without a query, a fragment, a trailing "/", or
    // ".git", when that path is exactly two parts of 1 to 100 ASCII letters, digits, '.', '_', or '-'. Anything else is null,
    // so a credential in a query or a fragment is never kept.
    public static string? GetRepositorySlug(string remoteUrl)
    {
        ArgumentNullException.ThrowIfNull(remoteUrl);
        string value = remoteUrl.Trim();
        int query = value.IndexOfAny(['?', '#']);
        if (query >= 0)
        {
            value = value[..query];
        }

        if (value.EndsWith('/'))
        {
```

Edit 2 of 2 in `src/CopilotLedger.Core/Git/GitRemoteUrl.cs`, replace:

```csharp
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 2 ? $"{segments[0]}/{segments[1]}" : null;
    }

    private static bool IsWindowsDrivePath(string value, int colon) =>
```

with:

```csharp
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 2 && segments.All(IsSlugPart) ? $"{segments[0]}/{segments[1]}" : null;
    }

    private static bool IsSlugPart(string part) =>
        part.Length is >= 1 and <= 100 &&
        part.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private static bool IsWindowsDrivePath(string value, int colon) =>
```

Edit 1 of 3 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
        }

        // A token_usage_record: one completed response. A record of another thread, inherited by a fork, is skipped.
        private void Usage(JsonFields fields, long number, long offset)
        {
            const ImportEventKind kind = ImportEventKind.TokenUsageRecord;
            string? threadId = SessionLogs.Uuid(fields.String("payload.thread_id"));
            if (threadId is not null && threadId != _threadId)
```

with:

```csharp
        }

        // A token_usage_record: one completed response. A record of another thread, inherited by a fork, is skipped; a record
        // that gives an allowlisted field twice is unsupported before its thread is read, so it can't pass for inherited.
        private void Usage(JsonFields fields, long number, long offset)
        {
            const ImportEventKind kind = ImportEventKind.TokenUsageRecord;
            if (fields.Duplicate is not null)
            {
                _parsed++;
                _unsupported++;
                _diagnostics.Add(ImportDiagnosticCode.UnsupportedUsageShape, kind, fields.Duplicate, number, offset);
                return;
            }

            string? threadId = SessionLogs.Uuid(fields.String("payload.thread_id"));
            if (threadId is not null && threadId != _threadId)
```

Edit 2 of 3 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
            string? model = (turnId is not null ? _modelByTurn.GetValueOrDefault(turnId) : null) ?? _latestModel;
            string? failed =
                fields.Duplicate
                ?? (time is null ? "timestamp"
                : threadId is null ? "payload.thread_id"
                : SessionLogs.Uuid(fields.String("payload.session_id")) != _sessionId ? "payload.session_id"
```

with:

```csharp
            string? model = (turnId is not null ? _modelByTurn.GetValueOrDefault(turnId) : null) ?? _latestModel;
            string? failed =
                time is null ? "timestamp"
                : threadId is null ? "payload.thread_id"
                : SessionLogs.Uuid(fields.String("payload.session_id")) != _sessionId ? "payload.session_id"
```

Edit 3 of 3 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
                : cached + written > input ? "payload.usage.input_tokens"
                : model is null ? "turn_context.model"
                : null);
            if (failed is not null)
            {
```

with:

```csharp
                : cached + written > input ? "payload.usage.input_tokens"
                : model is null ? "turn_context.model"
                : null;
            if (failed is not null)
            {
```

Edit 1 of 6 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
            bool? byok = fields.Boolean("data.usage.isByok");
            string? serviceRequestId = UsageIdentity.NormalizeServiceRequestId(fields.String("data.usage.serviceRequestId"));
            string? failed =
                usageId is null ? "data.usage.accounting.usageId"
```

with:

```csharp
            bool? byok = fields.Boolean("data.usage.isByok");
            string? serviceRequestId = UsageIdentity.NormalizeServiceRequestId(fields.String("data.usage.serviceRequestId"));
            long duration = Duration(fields.Integer("data.usage.duration")) ?? 0;
            string? failed =
                usageId is null ? "data.usage.accounting.usageId"
```

Edit 2 of 6 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
                : byok == false && fields.String("data.usage.provider.kind") != "copilot" ? "data.usage.provider.kind"
                : byok == false && serviceRequestId is null ? "data.usage.serviceRequestId"
                : null);
            if (failed is not null)
```

with:

```csharp
                : byok == false && fields.String("data.usage.provider.kind") != "copilot" ? "data.usage.provider.kind"
                : byok == false && serviceRequestId is null ? "data.usage.serviceRequestId"
                : StartsBeforeTime(end, duration) ? "data.usage.duration"
                : null);
            if (failed is not null)
```

Edit 3 of 6 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
                TurnKey = _turnKey,
                ServiceRequestId = byok == true ? null : serviceRequestId,
                StartTime = end.AddMilliseconds(-(Duration(fields.Integer("data.usage.duration")) ?? 0)),
                EndTime = end,
                RequestModel = model!,
```

with:

```csharp
                TurnKey = _turnKey,
                ServiceRequestId = byok == true ? null : serviceRequestId,
                StartTime = end.AddMilliseconds(-duration),
                EndTime = end,
                RequestModel = model!,
```

Edit 4 of 6 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
            long cacheRead = 0;
            long cacheWrite = 0;
            failed ??= Tokens(fields, "data.compactionTokensUsed.", out input, out output, out cacheRead, out cacheWrite)
                ?? (fields.String("data.compactionTokensUsed.provider.kind") != "copilot"
                    ? "data.compactionTokensUsed.provider.kind"
                    : null);
            if (failed is not null)
```

with:

```csharp
            long cacheRead = 0;
            long cacheWrite = 0;
            long duration = Duration(fields.Integer("data.compactionTokensUsed.duration")) ?? 0;
            failed ??= Tokens(fields, "data.compactionTokensUsed.", out input, out output, out cacheRead, out cacheWrite)
                ?? (fields.String("data.compactionTokensUsed.provider.kind") != "copilot"
                    ? "data.compactionTokensUsed.provider.kind"
                    : StartsBeforeTime(end!.Value, duration) ? "data.compactionTokensUsed.duration"
                    : null);
            if (failed is not null)
```

Edit 5 of 6 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
                TurnKey = _turnKey,
                ServiceRequestId = serviceRequestId,
                StartTime = end!.Value.AddMilliseconds(-(Duration(fields.Integer("data.compactionTokensUsed.duration")) ?? 0)),
                EndTime = end.Value,
                RequestModel = model!,
```

with:

```csharp
                TurnKey = _turnKey,
                ServiceRequestId = serviceRequestId,
                StartTime = end!.Value.AddMilliseconds(-duration),
                EndTime = end.Value,
                RequestModel = model!,
```

Edit 6 of 6 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
        private static long? Duration(long? value) => value is >= 0 and <= ImportLimits.MaxDurationMilliseconds ? value : null;

        // The envelope timestamp, in UTC.
        private static DateTimeOffset? Time(JsonFields fields) =>
```

with:

```csharp
        private static long? Duration(long? value) => value is >= 0 and <= ImportLimits.MaxDurationMilliseconds ? value : null;

        // A call that ended at end and lasted duration milliseconds would have started before the first instant.
        private static bool StartsBeforeTime(DateTimeOffset end, long duration) =>
            end - DateTimeOffset.MinValue < TimeSpan.FromMilliseconds(duration);

        // The envelope timestamp, in UTC.
        private static DateTimeOffset? Time(JsonFields fields) =>
```

Edit 1 of 1 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        }

        LocalModelRate? localModel = model is null ? null : _localModels.FirstOrDefault(item => item.Matches(model));
        bool localByKnownModel = string.IsNullOrWhiteSpace(host) && localModel is not null;
        if (IsLoopback(host) ||
            MatchesValue(host, _localHosts) ||
```

with:

```csharp
        }

        LocalModelRate? localModel = model is null ? null : _localModels.FirstOrDefault(item => item.Matches(model));
        bool localByKnownModel = span.BillingSource == BillingSource.Endpoint &&
                                 string.IsNullOrWhiteSpace(host) &&
                                 localModel is not null;
        if (IsLoopback(host) ||
            MatchesValue(host, _localHosts) ||
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Git/GitRemoteUrlTests.cs`, replace:

```csharp
        { "file:///srv/git/mirror.git", "file:///srv/git/mirror.git", null },
        { "/srv/git/mirror.git", "/srv/git/mirror.git", null },
    };

    [Theory]
```

with:

```csharp
        { "file:///srv/git/mirror.git", "file:///srv/git/mirror.git", null },
        { "/srv/git/mirror.git", "/srv/git/mirror.git", null },
        { "https://git.example/o/r.git?token=CANARY-TOKEN-7c1d", "https://git.example/o/r.git?token=CANARY-TOKEN-7c1d", "o/r" },
        { "https://git.example/o/r#CANARY-TOKEN-7c1d", "https://git.example/o/r#CANARY-TOKEN-7c1d", "o/r" },
        { "git@git.example:o/r.git?token=CANARY-TOKEN-7c1d", "git@git.example:o/r.git?token=CANARY-TOKEN-7c1d", "o/r" },
        { "https://git.example/o/r%3Ftoken.git", "https://git.example/o/r%3Ftoken.git", null },
    };

    [Theory]
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs`, replace:

```csharp
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home)), CancellationToken.None);
```

with:

```csharp
    }

    // A credential in a URL's query or fragment is dropped with them: the slug comes from the path alone.
    [Theory]
    [InlineData("https://git.example/o/r.git?token=" + CopilotSessionLogs.Canary)]
    [InlineData("https://git.example/o/r#" + CopilotSessionLogs.Canary)]
    public async Task CX13_AQueryOrFragmentOfTheRepositoryUrlIsNeverKept(string url)
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread, git: new JsonObject { ["repository_url"] = url })
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal("o/r", Assert.Single(result.Records).Git!.RepositorySlug);
        AssertNoCanary(result);
    }

    // A record that gives thread_id twice is an unsupported shape, whichever thread its first value names.
    [Fact]
    public async Task CX14_ARecordThatGivesAFieldTwiceIsUnsupportedNotInherited()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread)
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Raw(
                """{"timestamp":"2026-11-20T12:00:02.000Z","type":"token_usage_record","payload":{"thread_id":"FORK","thread_id":"THREAD","turn_id":"turn-1","session_id":"THREAD","root_turn_id":"turn-1","response_id":"resp_1","usage":{"input_tokens":1000,"cached_input_tokens":0,"output_tokens":100}}}"""
                    .Replace("FORK", Fork, StringComparison.Ordinal)
                    .Replace("THREAD", Thread, StringComparison.Ordinal))
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal((1, 1), (result.RecordsParsed, result.UnsupportedUsageShapes));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            (ImportDiagnosticCode.UnsupportedUsageShape, "payload.thread_id", 3L),
            (diagnostic.Code, diagnostic.Field, diagnostic.FirstLine));
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home)), CancellationToken.None);
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Import/CopilotCompactionTests.cs`, replace:

```csharp
        CopilotRecordTests.AssertNoCanary(result);
    }
}
```

with:

```csharp
        CopilotRecordTests.AssertNoCanary(result);
    }

    // A compaction that would have started before the first instant is an unsupported shape, not an error.
    [Fact]
    public async Task CC7_ACompactionThatWouldStartBeforeTheFirstInstantIsUnsupported()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, "1.0.114", start: DateTimeOffset.MinValue)
            .Compaction(S9, duration: 5_000)
            .Write(temp.Path);

        SessionLogReadResult result = await CopilotRecordTests.ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal((1, 1), (result.RecordsParsed, result.UnsupportedUsageShapes));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            (ImportDiagnosticCode.UnsupportedUsageShape, ImportEventKind.CompactionComplete, "data.compactionTokensUsed.duration"),
            (diagnostic.Code, diagnostic.Event, diagnostic.Field));
    }
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Import/CopilotRecordTests.cs`, replace:

```csharp
    }

    internal static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home)), CancellationToken.None);
```

with:

```csharp
    }

    // A call that would have started before the first instant can't be recorded; it's an unsupported shape, not an error.
    [Fact]
    public async Task CU13_ARecordThatWouldStartBeforeTheFirstInstantIsUnsupported()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, start: DateTimeOffset.MinValue)
            .Usage("msg_1", S1, duration: 2_000)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Empty(result.Records);
        Assert.Equal((1, 1), (result.RecordsParsed, result.UnsupportedUsageShapes));
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            (ImportDiagnosticCode.UnsupportedUsageShape, "data.usage.duration"),
            (diagnostic.Code, diagnostic.Field));
    }

    // A credential in the recorded repository's query or fragment is dropped with them, and a slug with one is no slug.
    [Theory]
    [InlineData("https://user:" + CopilotSessionLogs.Canary + "@git.example/o/r.git?token=" + CopilotSessionLogs.Canary + "#" + CopilotSessionLogs.Canary, "o/r")]
    [InlineData("git@git.example:o/r.git?token=" + CopilotSessionLogs.Canary, "o/r")]
    [InlineData("o/r?token=" + CopilotSessionLogs.Canary, null)]
    public async Task CU14_AQueryOrFragmentOfTheRepositoryIsNeverKept(string repository, string? slug)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(
                SessionId,
                context: new JsonObject
                {
                    ["cwd"] = CopilotSessionLogs.DefaultWorkingDirectory,
                    ["gitRoot"] = CopilotSessionLogs.DefaultWorkingDirectory,
                    ["repository"] = repository,
                })
            .Usage("msg_1", S1)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(slug, Assert.Single(result.Records).Git!.RepositorySlug);
        AssertNoCanary(result);
    }

    internal static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home)), CancellationToken.None);
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Pricing/BillingSourceTests.cs`, replace:

```csharp
    }

    private static NormalizedGenAiSpan Span(string? provider, string model, long input, long output) =>
        TestSpans.Chat(
```

with:

```csharp
    }

    // A provider-billed request is local only by its provider name: a known local model without an endpoint makes a live
    // span local, but never a Foundry BYOK or another provider's imported request.
    [Theory]
    [InlineData("azure.ai.inference", BillingChannel.FoundryByok, PricingStatus.ByokUnpriced)]
    [InlineData("openai", BillingChannel.Unpriced, PricingStatus.OtherProvider)]
    [InlineData("ollama", BillingChannel.Local, PricingStatus.LocalOffload)]
    public void BI8_AProviderBilledRequestIsLocalOnlyByItsProviderName(
        string provider,
        BillingChannel channel,
        PricingStatus status)
    {
        JsonRateCardCalculator builtIn = JsonRateCardCalculator.LoadEmbedded();

        CreditCalculationResult result = builtIn.Calculate(Span(provider, "qwen3.8-27b", 100_000, 10_000));
        CreditCalculationResult live = builtIn.Calculate(
            Span(provider, "qwen3.8-27b", 100_000, 10_000) with { BillingSource = BillingSource.Endpoint, ProviderName = null });

        Assert.Equal((channel, status), (result.Channel, result.Status));
        Assert.Equal((BillingChannel.Local, PricingStatus.LocalOffload), (live.Channel, live.Status));
    }

    private static NormalizedGenAiSpan Span(string? provider, string model, long input, long output) =>
        TestSpans.Chat(
```

### Tests

- `UrlHelpers_ReturnExpectedValues` (4 more cases): a query token and a fragment token after `https://git.example/o/r`, and a query after an SCP-style address, give `o/r`; `https://git.example/o/r%3Ftoken.git` gives `null`.
- **BI8** (3): a `Provider` request for `qwen3.8-27b`, a built-in local model, with no host, is `foundry_byok` and `byok_unpriced` for `azure.ai.inference`, `unpriced` and `other_provider` for `openai`, and `local` and `local_offload` for `ollama`; the same request as an `Endpoint` span without a provider stays `local`.
- **CU13**: a usage record whose `duration` (2,000 ms) would start it before `DateTimeOffset.MinValue` is 1 parsed, 1 unsupported, with one diagnostic for `data.usage.duration`. **CC7**: the same for a compaction (5,000 ms), `data.compactionTokensUsed.duration`.
- **CU14** (3): `data.context.repository` with credentials in its userinfo, query, and fragment, or an SCP-style address with a query token, gives `o/r`; `o/r?token=…` gives no slug; no canary is in the result.
- **CX13** (2): a `repository_url` with a query or fragment token gives `o/r`, and no canary is in the result. **CX14**: a `token_usage_record` that gives `thread_id` twice, the fork's first, is unsupported with field `payload.thread_id` on line 3, not an inherited record.

### Acceptance criteria

- No credential from a remote URL's query or fragment reaches a slug (GitRemoteUrlTests, CU14, CX13); a provider-billed request is local only by its provider name (BI8); bad durations and duplicated Codex fields are unsupported shapes (CU13, CC7, CX14).
- `dotnet test CopilotLedger.slnx` passes **1251** tests (1236 + 15).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 14 specification: Unchanged-Log Hashes, Unreadable Stores, and Dry-Run Companions

Review findings 3, 8, and 9.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Import/JsonlLineReader.cs` | A new `IncrementalHash? hash` parameter: every byte read is added to it |
| `src/CopilotLedger.Core/Import/SessionLogReadResult.cs` | `ContentSha256` |
| `src/CopilotLedger.Core/Import/SessionLogHash.cs` | New: `ComputeAsync` |
| `src/CopilotLedger.Core/Import/SessionLogDiscovery.cs` | New: `Logs` and `UnreadableEntries` |
| `src/CopilotLedger.Core/Import/ISessionLogSource.cs` | `Discover` returns `SessionLogDiscovery` |
| `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, `CodexSessionLogSource.cs` | Discovery counts unreadable entries; a read hashes the bytes it parses |
| `src/CopilotLedger.Core/Import/ImportDiagnosticCode.cs` | `UnreadableStore` |
| `src/CopilotLedger.Storage/LedgerMigrations.cs` | Migration 6: `import_sessions.log_sha256` (version 6 isn't released, so migration 6 changes in place) |
| `src/CopilotLedger.Storage/ImportSessionCheckpoint.cs` | Required `LogSha256` |
| `src/CopilotLedger.Storage/UsageAuthority.cs` | Writes and reads `log_sha256` |
| `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs` | A checkpoint's `LogSha256` must be 64 lowercase hex characters |
| `src/CopilotLedger.Storage/LedgerSnapshot.cs` | `MissingCompanionMessage`; refuses a ledger with one of `-wal` and `-shm` |
| `src/CopilotLedger.Storage/Import/ImportReport.cs` | `UnreadableStoreEntries`; `Complete` requires 0 |
| `src/CopilotLedger.Storage/Import/LedgerImporter.cs` | Store diagnostics, the hash-checked skip, and the checkpoint's hash |
| `src/CopilotLedger.Cli/Import/ImportCommands.cs` | JSON `unreadableStoreEntries`, the store warning, and the `--since` hint's condition |
| `docs/cli.md` | The unchanged-log rule, `--since` and last-write times, the dry run's refusal, the JSON fields, the allowlist, and coverage |
| `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs` | CS1 changed; new: CS15 |
| `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs` | CX1 changed; new: CX15, CX16 |
| `tests/CopilotLedger.Tests/Import/CopilotRecordTests.cs` | `ReadAsync` uses `Discover(home).Logs` |
| `tests/CopilotLedger.Tests/Import/LedgerImporterTests.cs` | New: IR16, IR17 |
| `tests/CopilotLedger.Tests/Storage/LedgerSnapshotTests.cs` | SN6 changed; new: SN7 (2) |
| `tests/CopilotLedger.Tests/Storage/UsageAuthorityTests.cs` | UA12 and UA13 changed |
| `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs` | IC4 changed; new: IC12, IC13 |

### Contracts and DDL

- `SessionLogDiscovery { required IReadOnlyList<SessionLogLocation> Logs; required long UnreadableEntries; }`; `ISessionLogSource.Discover(string home)` returns it.
- `SessionLogReadResult.ContentSha256` (`string?`): for a `Read` log, the lowercase hex SHA-256 of exactly the bytes parsed, the first `SizeBytes` bytes (fewer when the file shrank).
- `SessionLogHash.ComputeAsync(SessionLogLocation, CancellationToken)`: the same hash, read in 64 KiB chunks; `null` for a compressed log (never opened) or an `IOException` or `UnauthorizedAccessException`.
- `ImportSessionCheckpoint.LogSha256` (required); `ImportSourceReport.UnreadableStoreEntries` (required); `ImportDiagnosticCode.UnreadableStore` (last); `LedgerSnapshot.MissingCompanionMessage` (public `const string`).
- Migration 6's `import_sessions` gains, after `log_last_write_unix_ms`: `log_sha256 TEXT NOT NULL CHECK (length(log_sha256) = 64)`.

### Algorithm

1. **Discovery.** A store folder that doesn't exist is no logs (`DirectoryNotFoundException` while the path isn't a file). A store folder that exists but can't be listed, including a path that's a file, is one unreadable entry, and listing it stops; Codex's other folder is still listed. A session folder or rollout that can't be examined is one unreadable entry, and the others are still listed.
2. **Store diagnostics.** For each source, `UnreadableStoreEntries` is discovery's count; when it's above 0, a diagnostic `UnreadableStore` with an empty `LogId` and that count comes before the source's log diagnostics, and the source's coverage is incomplete.
3. **Unchanged logs.** A log is skipped as unchanged only when its checkpoint is complete, its size and last-write time (milliseconds) match, and `SessionLogHash.ComputeAsync` equals `LogSha256`. Size and time are only hints, so each such log is read once to hash it.
4. **Checkpoints.** The reader hashes every byte it parses, and the checkpoint stores that hash, never one taken after parsing, so bytes appended in between are never marked imported.
5. **Dry run.** `LedgerSnapshot.OpenAsync` throws `InvalidOperationException(MissingCompanionMessage)` when exactly one of `<ledger>-wal` and `<ledger>-shm` exists, before it opens anything. The command prints the message on standard error and exits with 1.

### Output

- Warning: `Warning: Copilot session logs: 1 folder or file couldn't be listed; any logs there weren't imported` (`<n> folders or files couldn't be listed; …` for more; `Codex session logs: …` for Codex).
- Dry-run refusal: `Dry run stopped: the ledger has a -wal or -shm file without the other, and reading it could create the missing one. Open the ledger once with another gh-ledger command, such as status, and try again.`
- JSON: `unreadableStoreEntries` after `sessionsScanned` in each source; a store diagnostic's `log` is `""`.

### Code (copy exactly)

Edit 1 of 5 in `docs/cli.md`, replace:

```markdown
- `--repo <path>` imports only requests made in the Git repository at `<path>`, a working-tree path such as `.`, unlike the reports' repository key. A session is in the repository when the repository root it recorded is that repository's root, or, when it recorded none, when its working directory is inside it. `--all` imports requests from every repository and from outside any. Use one of the two.
- `--source copilot`, the default, reads Copilot's session logs, `session-state/<session>/events.jsonl` under `~/.copilot`, or under `COPILOT_HOME` when it's set. `--source codex` reads Codex's rollouts under `sessions` and `archived_sessions` in `~/.codex`, or in `CODEX_HOME` when it's set. Repeat `--source` to read both. `--source claude` is refused: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it.
- `--since` takes the reports' periods and defaults to `all`. Logs last written before the period starts aren't read, and requests that started before it aren't recorded.
- `--dry-run` reads the logs and reports what the import would record. It never creates, upgrades, or writes the ledger.
- `--include-active` also reads Copilot sessions that are still open, which have an `inuse.*.lock` file in their folder. Without it, they're skipped, and you can import them after they end.
- `--json` writes the report as JSON.
```

with:

```markdown
- `--repo <path>` imports only requests made in the Git repository at `<path>`, a working-tree path such as `.`, unlike the reports' repository key. A session is in the repository when the repository root it recorded is that repository's root, or, when it recorded none, when its working directory is inside it. `--all` imports requests from every repository and from outside any. Use one of the two.
- `--source copilot`, the default, reads Copilot's session logs, `session-state/<session>/events.jsonl` under `~/.copilot`, or under `COPILOT_HOME` when it's set. `--source codex` reads Codex's rollouts under `sessions` and `archived_sessions` in `~/.codex`, or in `CODEX_HOME` when it's set. Repeat `--source` to read both. `--source claude` is refused: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it.
- `--since` takes the reports' periods and defaults to `all`. Logs last written before the period starts aren't read, by the last-write time the file system reports, and requests that started before it aren't recorded.
- `--dry-run` reads the logs and reports what the import would record. It never creates, upgrades, or writes the ledger. It stops with an error when the ledger has a `-wal` or `-shm` file without the other, because reading the ledger then could create the missing file.
- `--include-active` also reads Copilot sessions that are still open, which have an `inuse.*.lock` file in their folder. Without it, they're skipped, and you can import them after they end.
- `--json` writes the report as JSON.
```

Edit 2 of 5 in `docs/cli.md`, replace:

```markdown
- A live span that arrives after its request was imported supersedes the imported row, in the same transaction: the imported row stays in the ledger but no longer counts in any report, MCP tool, budget, or circuit-breaker rule. A live span without a service request ID supersedes every imported row of its session, and any live span supersedes the imported rows of its session that have no ID.

Importing again records nothing twice. A log that was imported completely is skipped while its size and last-write time stay the same. Any other log is read again, and the requests recorded from it before count as `Duplicate`. An import stopped by Ctrl+C or an error keeps the requests it recorded, and running it again completes it.

### The import report
```

with:

```markdown
- A live span that arrives after its request was imported supersedes the imported row, in the same transaction: the imported row stays in the ledger but no longer counts in any report, MCP tool, budget, or circuit-breaker rule. A live span without a service request ID supersedes every imported row of its session, and any live span supersedes the imported rows of its session that have no ID.

Importing again records nothing twice. A log that was imported completely is skipped while its size, last-write time, and SHA-256 hash stay the same, so import reads such a log once to hash it: the size and last-write time alone could miss a log rewritten in place. Any other log is read again, and the requests recorded from it before count as `Duplicate`. An import stopped by Ctrl+C or an error keeps the requests it recorded, and running it again completes it.

### The import report
```

Edit 3 of 5 in `docs/cli.md`, replace:

```markdown
`--json` writes no warnings; it lists them as `diagnostics`. Its fields are `dryRun`, `period`, `from`, `repository` (the `owner/repo` slug, or the root when the repository has none) and `repositoryRoot` (both `null` with `--all`), `coverage`, and:

- `sources`: one object per source, with `source`, `coverage`, `sessionsScanned`, `sessionsSkippedUnchanged`, `sessionsSkippedActive`, `sessionsSkippedOutOfScope`, `sessionsUnsupported`, `sessionsAggregateOnly` (the unsupported sessions from before Copilot 1.0.103), `recordsParsed`, `recordsOutOfScope`, `recorded`, `duplicate`, `overlapSkipped`, `ambiguousOverlap`, `malformedLines`, `unsupportedUsageShapes`, `costOutOfRange`, `journalPredecessors`, `skippedEvents` (events of other types), `unpriced`, `toolLinksRecorded`, and the recorded requests' `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans`.
- `totals`: `recorded` and the same amounts over every source.
- `diagnostics`: each with `source`, `log`, `code`, `event`, `field`, `version`, `count`, `line`, and `byteOffset`.

| Exit code | Meaning |
```

with:

```markdown
`--json` writes no warnings; it lists them as `diagnostics`. Its fields are `dryRun`, `period`, `from`, `repository` (the `owner/repo` slug, or the root when the repository has none) and `repositoryRoot` (both `null` with `--all`), `coverage`, and:

- `sources`: one object per source, with `source`, `coverage`, `sessionsScanned`, `unreadableStoreEntries` (folders and files of the source's store that couldn't be listed), `sessionsSkippedUnchanged`, `sessionsSkippedActive`, `sessionsSkippedOutOfScope`, `sessionsUnsupported`, `sessionsAggregateOnly` (the unsupported sessions from before Copilot 1.0.103), `recordsParsed`, `recordsOutOfScope`, `recorded`, `duplicate`, `overlapSkipped`, `ambiguousOverlap`, `malformedLines`, `unsupportedUsageShapes`, `costOutOfRange`, `journalPredecessors`, `skippedEvents` (events of other types), `unpriced`, `toolLinksRecorded`, and the recorded requests' `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans`.
- `totals`: `recorded` and the same amounts over every source.
- `diagnostics`: each with `source`, `log`, `code`, `event`, `field`, `version`, `count`, `line`, and `byteOffset`. `log` is empty for `unreadableStore`, which is about the source's store rather than one log.

| Exit code | Meaning |
```

Edit 4 of 5 in `docs/cli.md`, replace:

```markdown
| 1 | The options are invalid, or an error stopped the import. |

Skipped active, unchanged, and out-of-scope logs don't make coverage incomplete. When the only gap is sessions from before Copilot 1.0.103, the report's last line suggests `--since`, which leaves them out. With `--repo`, logs of other repositories are read again on every run, because they may hold requests of this one.

Imported requests count in every report and MCP tool, as live ones do, with the branch their log recorded. Tool calls are imported only from the last 7 days, as long as the ledger keeps tool calls; an older request keeps only the number of tool calls it made. Imported requests have no agent-invocation span, so `status --cache --turns` shows their `Growth` as `n/a`, and the input-growth ranking of `--bloat` leaves them out. `import` warns when the daemon runs another gh-ledger version: restart the daemon, so that the live spans it records supersede imported rows by this version's rules.
```

with:

```markdown
| 1 | The options are invalid, or an error stopped the import. |

Skipped active, unchanged, and out-of-scope logs don't make coverage incomplete. A folder or file of a session store that exists but can't be listed does, with a warning such as `Copilot session logs: 1 folder or file couldn't be listed`; a store that doesn't exist only has no logs. When the only gap is sessions from before Copilot 1.0.103, the report's last line suggests `--since`, which leaves them out. With `--repo`, logs of other repositories are read again on every run, because they may hold requests of this one.

Imported requests count in every report and MCP tool, as live ones do, with the branch their log recorded. Tool calls are imported only from the last 7 days, as long as the ledger keeps tool calls; an older request keeps only the number of tool calls it made. Imported requests have no agent-invocation span, so `status --cache --turns` shows their `Growth` as `n/a`, and the input-growth ranking of `--bloat` leaves them out. `import` warns when the daemon runs another gh-ledger version: restart the daemon, so that the live spans it records supersede imported rows by this version's rules.
```

Edit 5 of 5 in `docs/cli.md`, replace:

```markdown
| `span_links.recorded_at_unix_ms` | Kept by the ledger | — |
| `import_sessions.source`, `import_sessions.log_id` | `copilot`, and the session folder's name | `codex`, and the IDs in the rollout's file name, after `archived/` for an archived rollout |
| `import_sessions.log_size_bytes`, `import_sessions.log_last_write_unix_ms` | The log file's size and last-write time | The rollout file's size and last-write time |
| `import_sessions.complete`, `import_sessions.records_recorded`, `import_sessions.records_duplicate`, `import_sessions.records_overlap`, `import_sessions.records_ambiguous`, `import_sessions.imported_at_unix_ms` | The import's own results | The import's own results |
| `import_record_aliases.source`, `import_record_aliases.record_key_sha256` | `copilot`, and SHA-256 of the record's usage ID, or of a compaction's service request ID | `codex`, and SHA-256 of `payload.response_id` |
```

with:

```markdown
| `span_links.recorded_at_unix_ms` | Kept by the ledger | — |
| `import_sessions.source`, `import_sessions.log_id` | `copilot`, and the session folder's name | `codex`, and the IDs in the rollout's file name, after `archived/` for an archived rollout |
| `import_sessions.log_size_bytes`, `import_sessions.log_last_write_unix_ms`, `import_sessions.log_sha256` | The log file's size and last-write time, and the SHA-256 hash of its bytes up to that size | The rollout file's size and last-write time, and the SHA-256 hash of its bytes up to that size |
| `import_sessions.complete`, `import_sessions.records_recorded`, `import_sessions.records_duplicate`, `import_sessions.records_overlap`, `import_sessions.records_ambiguous`, `import_sessions.imported_at_unix_ms` | The import's own results | The import's own results |
| `import_record_aliases.source`, `import_record_aliases.record_key_sha256` | `copilot`, and SHA-256 of the record's usage ID, or of a compaction's service request ID | `codex`, and SHA-256 of `payload.response_id` |
```

Edit 1 of 4 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
    // Every gap in the source's coverage is a session from before Copilot 1.0.103.
    private static bool OnlyOldSessionsMissing(ImportSourceReport source) =>
        source.SessionsUnsupported == source.SessionsAggregateOnly &&
        source.MalformedLines == 0 &&
```

with:

```csharp
    // Every gap in the source's coverage is a session from before Copilot 1.0.103.
    private static bool OnlyOldSessionsMissing(ImportSourceReport source) =>
        source.UnreadableStoreEntries == 0 &&
        source.SessionsUnsupported == source.SessionsAggregateOnly &&
        source.MalformedLines == 0 &&
```

Edit 2 of 4 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
            writer.WriteString("coverage", Coverage(source.Complete));
            writer.WriteNumber("sessionsScanned", source.SessionsScanned);
            writer.WriteNumber("sessionsSkippedUnchanged", source.SessionsSkippedUnchanged);
            writer.WriteNumber("sessionsSkippedActive", source.SessionsSkippedActive);
```

with:

```csharp
            writer.WriteString("coverage", Coverage(source.Complete));
            writer.WriteNumber("sessionsScanned", source.SessionsScanned);
            writer.WriteNumber("unreadableStoreEntries", source.UnreadableStoreEntries);
            writer.WriteNumber("sessionsSkippedUnchanged", source.SessionsSkippedUnchanged);
            writer.WriteNumber("sessionsSkippedActive", source.SessionsSkippedActive);
```

Edit 3 of 4 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
    }

    // "<source> <session|rollout> <log ID>[, line <n> (byte <offset>)]: <message>"
    private static string Describe(ImportDiagnostic diagnostic)
    {
        string log = diagnostic.Source == ImportSourceKind.Codex ? "Codex rollout" : "Copilot session";
        string line = diagnostic.FirstLine > 0
```

with:

```csharp
    }

    // "<source> <session|rollout> <log ID>[, line <n> (byte <offset>)]: <message>", or "<source> session logs: <message>"
    // for the store itself.
    private static string Describe(ImportDiagnostic diagnostic)
    {
        if (diagnostic.Code == ImportDiagnosticCode.UnreadableStore)
        {
            return $"{Name(diagnostic.Source)} session logs: {Message(diagnostic)}";
        }

        string log = diagnostic.Source == ImportSourceKind.Codex ? "Codex rollout" : "Copilot session";
        string line = diagnostic.FirstLine > 0
```

Edit 4 of 4 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
                $"{Plural(count, "request")} deferred: this conversation has live spans without a call ID",
            ImportDiagnosticCode.CostOutOfRange => $"{Plural(count, "request")} skipped: cost beyond the ledger's range",
            _ => throw new ArgumentOutOfRangeException(nameof(diagnostic), diagnostic.Code, null),
        };
```

with:

```csharp
                $"{Plural(count, "request")} deferred: this conversation has live spans without a call ID",
            ImportDiagnosticCode.CostOutOfRange => $"{Plural(count, "request")} skipped: cost beyond the ledger's range",
            ImportDiagnosticCode.UnreadableStore => count == 1
                ? "1 folder or file couldn't be listed; any logs there weren't imported"
                : $"{LedgerFormat.Count(count)} folders or files couldn't be listed; any logs there weren't imported",
            _ => throw new ArgumentOutOfRangeException(nameof(diagnostic), diagnostic.Code, null),
        };
```

Edit 1 of 9 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
using System.Globalization;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;
```

with:

```csharp
using System.Globalization;
using System.Security.Cryptography;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;
```

Edit 2 of 9 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
        string.IsNullOrEmpty(codexHome) ? Path.Combine(userProfile, ".codex") : codexHome;

    // Rollouts under <home>/sessions, and under <home>/archived_sessions with LogIds that start with "archived/". A
    // .jsonl.zst rollout is listed as compressed and never opened.
    public IReadOnlyList<SessionLogLocation> Discover(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        List<SessionLogLocation> logs = [];
        AddRollouts(logs, Path.Combine(home, "sessions"), "");
        AddRollouts(logs, Path.Combine(home, "archived_sessions"), "archived/");
        return [.. logs.OrderBy(log => log.LogId, StringComparer.Ordinal)];
    }

    public Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken)
```

with:

```csharp
        string.IsNullOrEmpty(codexHome) ? Path.Combine(userProfile, ".codex") : codexHome;

    // Rollouts under <home>/sessions, and under <home>/archived_sessions with LogIds that start with "archived/". A
    // .jsonl.zst rollout is listed as compressed and never opened. A folder or rollout that exists but can't be listed or
    // examined is an unreadable entry; listing stops at the first folder that can't be listed.
    public SessionLogDiscovery Discover(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        List<SessionLogLocation> logs = [];
        long unreadable = AddRollouts(logs, Path.Combine(home, "sessions"), "") +
                          AddRollouts(logs, Path.Combine(home, "archived_sessions"), "archived/");
        return new SessionLogDiscovery
        {
            Logs = [.. logs.OrderBy(log => log.LogId, StringComparer.Ordinal)],
            UnreadableEntries = unreadable,
        };
    }

    public Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken)
```

Edit 3 of 9 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
    }

    private static void AddRollouts(List<SessionLogLocation> logs, string root, string prefix)
    {
        try
        {
```

with:

```csharp
    }

    // Adds root's rollouts to logs and returns how many entries couldn't be listed or examined.
    private static long AddRollouts(List<SessionLogLocation> logs, string root, string prefix)
    {
        long unreadable = 0;
        try
        {
```

Edit 4 of 9 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // rollout-YYYY-MM-DDTHH-MM-SS-<thread ID>[_<rollout ID>].jsonl, or .jsonl.zst: the IDs in lowercase, joined by "_".
```

with:

```csharp
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    unreadable++;
                }
            }
        }
        catch (DirectoryNotFoundException) when (!File.Exists(root))
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            unreadable++;
        }

        return unreadable;
    }

    // rollout-YYYY-MM-DDTHH-MM-SS-<thread ID>[_<rollout ID>].jsonl, or .jsonl.zst: the IDs in lowercase, joined by "_".
```

Edit 5 of 9 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
        }

        Rollout rollout = new(location);
        try
        {
```

with:

```csharp
        }

        Rollout rollout = new(location);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
```

Edit 6 of 9 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
                rollout.OnOversized,
                rollout.OnUnterminated,
                cancellationToken);
        }
```

with:

```csharp
                rollout.OnOversized,
                rollout.OnUnterminated,
                hash,
                cancellationToken);
        }
```

Edit 7 of 9 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
        }

        return rollout.Finish();
    }

    // One rollout's reading state.
```

with:

```csharp
        }

        return rollout.Finish(Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    // One rollout's reading state.
```

Edit 8 of 9 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
            Malformed(ImportDiagnosticCode.UnterminatedLine, ImportEventKind.Unknown, null, number, offset);

        internal SessionLogReadResult Finish()
        {
            if (_stop is { } stop)
```

with:

```csharp
            Malformed(ImportDiagnosticCode.UnterminatedLine, ImportEventKind.Unknown, null, number, offset);

        // contentSha256: the SHA-256 of the bytes that were read, which a Read result keeps.
        internal SessionLogReadResult Finish(string contentSha256)
        {
            if (_stop is { } stop)
```

Edit 9 of 9 in `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`, replace:

```csharp
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
            };
        }
```

with:

```csharp
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
                ContentSha256 = contentSha256,
            };
        }
```

Edit 1 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
using System.Globalization;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;
```

with:

```csharp
using System.Globalization;
using System.Security.Cryptography;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;
```

Edit 2 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
    public ImportSourceKind Kind => ImportSourceKind.Copilot;

    // Each <home>/session-state/<session ID>/events.jsonl whose directory name is a valid session ID.
    public IReadOnlyList<SessionLogLocation> Discover(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        List<SessionLogLocation> logs = [];
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(Path.Combine(home, "session-state")))
            {
                string sessionId = Path.GetFileName(directory);
```

with:

```csharp
    public ImportSourceKind Kind => ImportSourceKind.Copilot;

    // Each <home>/session-state/<session ID>/events.jsonl whose directory name is a valid session ID. A session folder, or a
    // session-state that exists but isn't a folder that can be listed, is an unreadable entry.
    public SessionLogDiscovery Discover(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        string root = Path.Combine(home, "session-state");
        List<SessionLogLocation> logs = [];
        long unreadable = 0;
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(root))
            {
                string sessionId = Path.GetFileName(directory);
```

Edit 3 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return [.. logs.OrderBy(log => log.LogId, StringComparer.Ordinal)];
    }

    public Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken)
```

with:

```csharp
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    unreadable++;
                }
            }
        }
        catch (DirectoryNotFoundException) when (!File.Exists(root))
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            unreadable++;
        }

        return new SessionLogDiscovery
        {
            Logs = [.. logs.OrderBy(log => log.LogId, StringComparer.Ordinal)],
            UnreadableEntries = unreadable,
        };
    }

    public Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken)
```

Edit 4 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
    {
        Session session = new(location);
        try
        {
```

with:

```csharp
    {
        Session session = new(location);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
```

Edit 5 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
                session.OnOversized,
                session.OnUnterminated,
                cancellationToken);
        }
```

with:

```csharp
                session.OnOversized,
                session.OnUnterminated,
                hash,
                cancellationToken);
        }
```

Edit 6 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
        }

        return session.Finish();
    }

    // One log's reading state.
```

with:

```csharp
        }

        return session.Finish(Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    // One log's reading state.
```

Edit 7 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
            Malformed(ImportDiagnosticCode.UnterminatedLine, ImportEventKind.Unknown, null, number, offset);

        internal SessionLogReadResult Finish()
        {
            if (_stop is { } stop)
```

with:

```csharp
            Malformed(ImportDiagnosticCode.UnterminatedLine, ImportEventKind.Unknown, null, number, offset);

        // contentSha256: the SHA-256 of the bytes that were read, which a Read result keeps.
        internal SessionLogReadResult Finish(string contentSha256)
        {
            if (_stop is { } stop)
```

Edit 8 of 8 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
            };
        }
```

with:

```csharp
                MalformedLines = _malformed,
                SkippedEvents = _skipped,
                ContentSha256 = contentSha256,
            };
        }
```

Edit 1 of 1 in `src/CopilotLedger.Core/Import/ISessionLogSource.cs`, replace:

```csharp
    ImportSourceKind Kind { get; }

    // The logs under home, ordered by LogId (ordinal). A missing or unreadable home gives none.
    IReadOnlyList<SessionLogLocation> Discover(string home);

    // Reads at most location.SizeBytes bytes of the log. Throws only ArgumentException (a location of another source) and
    // OperationCanceledException; a log that can't be read has Status Unreadable.
    Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken);
}
```

with:

```csharp
    ImportSourceKind Kind { get; }

    // The logs under home, ordered by LogId (ordinal), and how many entries couldn't be listed or examined. A missing home
    // gives none of either.
    SessionLogDiscovery Discover(string home);

    // Reads at most location.SizeBytes bytes of the log. Throws only ArgumentException (a location of another source) and
    // OperationCanceledException; a log that can't be read has Status Unreadable. A Read result has the SHA-256 of the bytes
    // it parsed.
    Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken);
}
```

Edit 1 of 1 in `src/CopilotLedger.Core/Import/ImportDiagnosticCode.cs`, replace:

```csharp
    AmbiguousOverlap,             // set by the importer
    CostOutOfRange,               // set by the importer
}
```

with:

```csharp
    AmbiguousOverlap,             // set by the importer
    CostOutOfRange,               // set by the importer
    UnreadableStore,              // set by the importer: entries of the store that couldn't be listed or examined
}
```

Edit 1 of 4 in `src/CopilotLedger.Core/Import/JsonlLineReader.cs`, replace:

```csharp
using System.Buffers;

namespace CopilotLedger.Core.Import;
```

with:

```csharp
using System.Buffers;
using System.Security.Cryptography;

namespace CopilotLedger.Core.Import;
```

Edit 2 of 4 in `src/CopilotLedger.Core/Import/JsonlLineReader.cs`, replace:

```csharp
// Splits the first maxBytes bytes of a JSON Lines stream into lines, reading 64 KiB at a time, and hands each complete line
// to a callback. A line longer than maxLineBytes is skipped without being kept, and a last line without "\n" is reported but
// never handed over. Blank lines are skipped silently. Line numbers are 1-based and count every "\n".
internal static class JsonlLineReader
{
```

with:

```csharp
// Splits the first maxBytes bytes of a JSON Lines stream into lines, reading 64 KiB at a time, and hands each complete line
// to a callback. A line longer than maxLineBytes is skipped without being kept, and a last line without "\n" is reported but
// never handed over. Blank lines are skipped silently. Line numbers are 1-based and count every "\n". Every byte read is also
// added to hash, when it isn't null.
internal static class JsonlLineReader
{
```

Edit 3 of 4 in `src/CopilotLedger.Core/Import/JsonlLineReader.cs`, replace:

```csharp
        Action<long, long> onOversized,
        Action<long, long> onUnterminated,
        CancellationToken cancellationToken)
    {
```

with:

```csharp
        Action<long, long> onOversized,
        Action<long, long> onUnterminated,
        IncrementalHash? hash,
        CancellationToken cancellationToken)
    {
```

Edit 4 of 4 in `src/CopilotLedger.Core/Import/JsonlLineReader.cs`, replace:

```csharp
                }

                int start = 0;
                while (start < read)
```

with:

```csharp
                }

                hash?.AppendData(chunk, 0, read);
                int start = 0;
                while (start < read)
```

New file `src/CopilotLedger.Core/Import/SessionLogDiscovery.cs`:

```csharp
namespace CopilotLedger.Core.Import;

// What discovery found under a home. A missing home, or a missing store folder in it, is no logs and no unreadable entries;
// a store folder, session folder, or log file that exists but can't be listed or examined is an unreadable entry.
public sealed record SessionLogDiscovery
{
    public required IReadOnlyList<SessionLogLocation> Logs { get; init; }   // ordered by LogId (ordinal)
    public required long UnreadableEntries { get; init; }
}
```

New file `src/CopilotLedger.Core/Import/SessionLogHash.cs`:

```csharp
using System.Security.Cryptography;

namespace CopilotLedger.Core.Import;

// The SHA-256 of a session log's content: the bytes from its start up to the size it had when it was discovered. A log's
// size and last-write time only hint that it's unchanged; this hash decides it.
public static class SessionLogHash
{
    private const int ChunkBytes = 64 * 1024;

    // The lowercase hex SHA-256 of location's first SizeBytes bytes (fewer when it has shrunk since), or null when the log
    // can't be read. A compressed log is never opened, so it has none.
    public static Task<string?> ComputeAsync(SessionLogLocation location, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        return location.IsCompressed
            ? Task.FromResult<string?>(null)
            : Task.Run(() => Compute(location, cancellationToken), cancellationToken);
    }

    private static string? Compute(SessionLogLocation location, CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] chunk = new byte[ChunkBytes];
        try
        {
            using FileStream stream = new(
                location.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                0,
                FileOptions.SequentialScan);
            long position = 0;
            while (position < location.SizeBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = stream.Read(chunk, 0, (int)Math.Min(ChunkBytes, location.SizeBytes - position));
                if (read == 0)
                {
                    break;
                }

                hash.AppendData(chunk, 0, read);
                position += read;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
```

Edit 1 of 1 in `src/CopilotLedger.Core/Import/SessionLogReadResult.cs`, replace:

```csharp
    public required long MalformedLines { get; init; }            // invalid JSON, oversized, or an unterminated last line
    public required long SkippedEvents { get; init; }             // well-formed lines of any other type, known or not
}
```

with:

```csharp
    public required long MalformedLines { get; init; }            // invalid JSON, oversized, or an unterminated last line
    public required long SkippedEvents { get; init; }             // well-formed lines of any other type, known or not
    public string? ContentSha256 { get; init; }                   // a Read log's bytes that were parsed (SessionLogHash)
}
```

Edit 1 of 2 in `src/CopilotLedger.Storage/Import/ImportReport.cs`, replace:

```csharp
    public required ImportSourceKind Source { get; init; }
    public required long SessionsScanned { get; init; }             // every log discovered
    public required long SessionsSkippedUnchanged { get; init; }    // completely imported, and unchanged since
    public required long SessionsSkippedActive { get; init; }       // still open, without IncludeActive
```

with:

```csharp
    public required ImportSourceKind Source { get; init; }
    public required long SessionsScanned { get; init; }             // every log discovered
    public required long UnreadableStoreEntries { get; init; }      // folders and files of the store that couldn't be listed
    public required long SessionsSkippedUnchanged { get; init; }    // completely imported, and unchanged since
    public required long SessionsSkippedActive { get; init; }       // still open, without IncludeActive
```

Edit 2 of 2 in `src/CopilotLedger.Storage/Import/ImportReport.cs`, replace:

```csharp
    public required long ApiEquivalentEstimatedSpans { get; init; } // priced without a reported cache counter

    public bool Complete =>
        SessionsUnsupported == 0 &&
        MalformedLines == 0 &&
```

with:

```csharp
    public required long ApiEquivalentEstimatedSpans { get; init; } // priced without a reported cache counter

    public bool Complete =>
        UnreadableStoreEntries == 0 &&
        SessionsUnsupported == 0 &&
        MalformedLines == 0 &&
```

Edit 1 of 6 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
            {
                Tally tally = new(source.Source.Kind);
                foreach (SessionLogLocation log in source.Source.Discover(source.Home))
                {
                    cancellationToken.ThrowIfCancellationRequested();
```

with:

```csharp
            {
                Tally tally = new(source.Source.Kind);
                SessionLogDiscovery discovery = source.Source.Discover(source.Home);
                tally.UnreadableStoreEntries = discovery.UnreadableEntries;
                if (discovery.UnreadableEntries > 0)
                {
                    diagnostics.Add(new ImportDiagnostic
                    {
                        Source = source.Source.Kind,
                        LogId = string.Empty,
                        Code = ImportDiagnosticCode.UnreadableStore,
                        Count = discovery.UnreadableEntries,
                    });
                }

                foreach (SessionLogLocation log in discovery.Logs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
```

Edit 2 of 6 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
        }

        ImportSessionCheckpoint? checkpoint = store is not null
            ? await store.GetImportSessionAsync(log.Source, log.LogId, cancellationToken)
```

with:

```csharp
        }

        // Size and last-write time only hint that a log is unchanged: it's skipped when the SHA-256 of its content matches too.
        ImportSessionCheckpoint? checkpoint = store is not null
            ? await store.GetImportSessionAsync(log.Source, log.LogId, cancellationToken)
```

Edit 3 of 6 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
        if (checkpoint is { Complete: true } &&
            checkpoint.LogSizeBytes == log.SizeBytes &&
            checkpoint.LogLastWriteTime.ToUnixTimeMilliseconds() == log.LastWriteTime.ToUnixTimeMilliseconds())
        {
            tally.SessionsSkippedUnchanged++;
```

with:

```csharp
        if (checkpoint is { Complete: true } &&
            checkpoint.LogSizeBytes == log.SizeBytes &&
            checkpoint.LogLastWriteTime.ToUnixTimeMilliseconds() == log.LastWriteTime.ToUnixTimeMilliseconds() &&
            await SessionLogHash.ComputeAsync(log, cancellationToken) == checkpoint.LogSha256)
        {
            tally.SessionsSkippedUnchanged++;
```

Edit 4 of 6 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
                    LogSizeBytes = log.SizeBytes,
                    LogLastWriteTime = log.LastWriteTime,
                    Complete = result.MalformedLines == 0 &&
                               result.UnsupportedUsageShapes == 0 &&
```

with:

```csharp
                    LogSizeBytes = log.SizeBytes,
                    LogLastWriteTime = log.LastWriteTime,
                    LogSha256 = result.ContentSha256 ??
                                throw new InvalidOperationException("A log that was read has no content hash."),
                    Complete = result.MalformedLines == 0 &&
                               result.UnsupportedUsageShapes == 0 &&
```

Edit 5 of 6 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
        private bool _apiEquivalentPriced;

        public long SessionsScanned { get; set; }
        public long SessionsSkippedUnchanged { get; set; }
        public long SessionsSkippedActive { get; set; }
```

with:

```csharp
        private bool _apiEquivalentPriced;

        public long SessionsScanned { get; set; }
        public long UnreadableStoreEntries { get; set; }
        public long SessionsSkippedUnchanged { get; set; }
        public long SessionsSkippedActive { get; set; }
```

Edit 6 of 6 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
            Source = source,
            SessionsScanned = SessionsScanned,
            SessionsSkippedUnchanged = SessionsSkippedUnchanged,
            SessionsSkippedActive = SessionsSkippedActive,
```

with:

```csharp
            Source = source,
            SessionsScanned = SessionsScanned,
            UnreadableStoreEntries = UnreadableStoreEntries,
            SessionsSkippedUnchanged = SessionsSkippedUnchanged,
            SessionsSkippedActive = SessionsSkippedActive,
```

Edit 1 of 2 in `src/CopilotLedger.Storage/ImportSessionCheckpoint.cs`, replace:

```csharp
namespace CopilotLedger.Storage;

// What the last import of one session log saw, so an unchanged, completely imported log can be skipped.
public sealed record ImportSessionCheckpoint
{
```

with:

```csharp
namespace CopilotLedger.Storage;

// What the last import of one session log saw, so an unchanged, completely imported log can be skipped: its size and
// last-write time hint that it's unchanged, and the SHA-256 of its content decides.
public sealed record ImportSessionCheckpoint
{
```

Edit 2 of 2 in `src/CopilotLedger.Storage/ImportSessionCheckpoint.cs`, replace:

```csharp
    public required long LogSizeBytes { get; init; }
    public required DateTimeOffset LogLastWriteTime { get; init; }   // stored in Unix milliseconds
    public required bool Complete { get; init; }                     // every record of the whole log was resolved
    public required long Recorded { get; init; }
```

with:

```csharp
    public required long LogSizeBytes { get; init; }
    public required DateTimeOffset LogLastWriteTime { get; init; }   // stored in Unix milliseconds
    public required string LogSha256 { get; init; }                  // SessionLogReadResult.ContentSha256: 64 lowercase hex
    public required bool Complete { get; init; }                     // every record of the whole log was resolved
    public required long Recorded { get; init; }
```

Edit 1 of 1 in `src/CopilotLedger.Storage/LedgerMigrations.cs`, replace:

```csharp
                log_size_bytes         INTEGER NOT NULL CHECK (log_size_bytes >= 0),
                log_last_write_unix_ms INTEGER NOT NULL,
                complete               INTEGER NOT NULL CHECK (complete IN (0, 1)),
                records_recorded       INTEGER NOT NULL CHECK (records_recorded >= 0),
```

with:

```csharp
                log_size_bytes         INTEGER NOT NULL CHECK (log_size_bytes >= 0),
                log_last_write_unix_ms INTEGER NOT NULL,
                log_sha256             TEXT    NOT NULL CHECK (length(log_sha256) = 64),
                complete               INTEGER NOT NULL CHECK (complete IN (0, 1)),
                records_recorded       INTEGER NOT NULL CHECK (records_recorded >= 0),
```

Edit 1 of 3 in `src/CopilotLedger.Storage/LedgerSnapshot.cs`, replace:

```csharp
// The ledger as one consistent, read-only snapshot, for a dry run: it decides each imported record as RecordImportedAsync
// would, and remembers its own decisions, without ever creating, migrating, or writing the ledger. With neither -wal nor -shm
// beside the ledger, it opens the file as immutable, so SQLite doesn't create them either.
public sealed class LedgerSnapshot : IAsyncDisposable
{
    private readonly SqliteConnection? _connection;
    private readonly SqliteTransaction? _transaction;
```

with:

```csharp
// The ledger as one consistent, read-only snapshot, for a dry run: it decides each imported record as RecordImportedAsync
// would, and remembers its own decisions, without ever creating, migrating, or writing the ledger. With neither -wal nor -shm
// beside the ledger, it opens the file as immutable, so SQLite doesn't create them either. With only one of them, SQLite could
// create the other, so it refuses.
public sealed class LedgerSnapshot : IAsyncDisposable
{
    public const string MissingCompanionMessage =
        "Dry run stopped: the ledger has a -wal or -shm file without the other, and reading it could create the missing one. Open the ledger once with another gh-ledger command, such as status, and try again.";

    private readonly SqliteConnection? _connection;
    private readonly SqliteTransaction? _transaction;
```

Edit 2 of 3 in `src/CopilotLedger.Storage/LedgerSnapshot.cs`, replace:

```csharp
    public int SchemaVersion { get; }

    // Throws InvalidOperationException for a ledger newer than this gh-ledger, as InitializeAsync does.
    public static async Task<LedgerSnapshot> OpenAsync(string databasePath, CancellationToken cancellationToken = default)
    {
```

with:

```csharp
    public int SchemaVersion { get; }

    // Throws InvalidOperationException for a ledger newer than this gh-ledger, as InitializeAsync does, and with
    // MissingCompanionMessage when exactly one of -wal and -shm exists.
    public static async Task<LedgerSnapshot> OpenAsync(string databasePath, CancellationToken cancellationToken = default)
    {
```

Edit 3 of 3 in `src/CopilotLedger.Storage/LedgerSnapshot.cs`, replace:

```csharp
        }

        bool companions = File.Exists(path + "-wal") || File.Exists(path + "-shm");
        SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = companions ? path : ImmutableUri(path),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
```

with:

```csharp
        }

        bool wal = File.Exists(path + "-wal");
        if (wal != File.Exists(path + "-shm"))
        {
            throw new InvalidOperationException(MissingCompanionMessage);
        }

        SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = wal ? path : ImmutableUri(path),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
```

Edit 1 of 2 in `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs`, replace:

```csharp
            string.IsNullOrEmpty(checkpoint.LogId) ||
            checkpoint.LogSizeBytes < 0 ||
            checkpoint.Recorded < 0 ||
            checkpoint.Duplicates < 0 ||
```

with:

```csharp
            string.IsNullOrEmpty(checkpoint.LogId) ||
            checkpoint.LogSizeBytes < 0 ||
            checkpoint.LogSha256 is not { Length: 64 } ||
            !checkpoint.LogSha256.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f') ||
            checkpoint.Recorded < 0 ||
            checkpoint.Duplicates < 0 ||
```

Edit 2 of 2 in `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs`, replace:

```csharp
        {
            throw new ArgumentException(
                "A checkpoint must name a Copilot or Codex log, and its size and counts can't be negative.",
                parameterName);
        }
```

with:

```csharp
        {
            throw new ArgumentException(
                "A checkpoint must name a Copilot or Codex log and its SHA-256, and its size and counts can't be negative.",
                parameterName);
        }
```

Edit 1 of 4 in `src/CopilotLedger.Storage/UsageAuthority.cs`, replace:

```csharp
        command.CommandText = """
            INSERT INTO import_sessions (
                source, log_id, log_size_bytes, log_last_write_unix_ms, complete, records_recorded, records_duplicate,
                records_overlap, records_ambiguous, imported_at_unix_ms
            ) VALUES (
                @source, @log, @size, @time, @complete, @recorded, @duplicate, @overlap, @ambiguous, @now
            ) ON CONFLICT (source, log_id) DO UPDATE SET
                log_size_bytes = excluded.log_size_bytes,
                log_last_write_unix_ms = excluded.log_last_write_unix_ms,
                complete = excluded.complete,
                records_recorded = excluded.records_recorded,
```

with:

```csharp
        command.CommandText = """
            INSERT INTO import_sessions (
                source, log_id, log_size_bytes, log_last_write_unix_ms, log_sha256, complete, records_recorded,
                records_duplicate, records_overlap, records_ambiguous, imported_at_unix_ms
            ) VALUES (
                @source, @log, @size, @time, @sha256, @complete, @recorded, @duplicate, @overlap, @ambiguous, @now
            ) ON CONFLICT (source, log_id) DO UPDATE SET
                log_size_bytes = excluded.log_size_bytes,
                log_last_write_unix_ms = excluded.log_last_write_unix_ms,
                log_sha256 = excluded.log_sha256,
                complete = excluded.complete,
                records_recorded = excluded.records_recorded,
```

Edit 2 of 4 in `src/CopilotLedger.Storage/UsageAuthority.cs`, replace:

```csharp
        command.Parameters.AddWithValue("@size", checkpoint.LogSizeBytes);
        command.Parameters.AddWithValue("@time", checkpoint.LogLastWriteTime.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@complete", checkpoint.Complete ? 1 : 0);
        command.Parameters.AddWithValue("@recorded", checkpoint.Recorded);
```

with:

```csharp
        command.Parameters.AddWithValue("@size", checkpoint.LogSizeBytes);
        command.Parameters.AddWithValue("@time", checkpoint.LogLastWriteTime.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@sha256", checkpoint.LogSha256);
        command.Parameters.AddWithValue("@complete", checkpoint.Complete ? 1 : 0);
        command.Parameters.AddWithValue("@recorded", checkpoint.Recorded);
```

Edit 3 of 4 in `src/CopilotLedger.Storage/UsageAuthority.cs`, replace:

```csharp
        command.CommandText = """
            SELECT log_size_bytes, log_last_write_unix_ms, complete, records_recorded, records_duplicate, records_overlap,
                   records_ambiguous, imported_at_unix_ms
            FROM import_sessions
            WHERE source = @source AND log_id = @log;
```

with:

```csharp
        command.CommandText = """
            SELECT log_size_bytes, log_last_write_unix_ms, complete, records_recorded, records_duplicate, records_overlap,
                   records_ambiguous, imported_at_unix_ms, log_sha256
            FROM import_sessions
            WHERE source = @source AND log_id = @log;
```

Edit 4 of 4 in `src/CopilotLedger.Storage/UsageAuthority.cs`, replace:

```csharp
            LogSizeBytes = reader.GetInt64(0),
            LogLastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
            Complete = reader.GetInt64(2) == 1,
            Recorded = reader.GetInt64(3),
```

with:

```csharp
            LogSizeBytes = reader.GetInt64(0),
            LogLastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
            LogSha256 = reader.GetString(8),
            Complete = reader.GetInt64(2) == 1,
            Recorded = reader.GetInt64(3),
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
                  "source": "copilot",
                  "coverage": "complete",
                  "sessionsScanned": 1,
                  "sessionsSkippedUnchanged": 0,
                  "sessionsSkippedActive": 0,
```

with:

```csharp
                  "source": "copilot",
                  "coverage": "complete",
                  "sessionsScanned": 1,
                  "unreadableStoreEntries": 0,
                  "sessionsSkippedUnchanged": 0,
                  "sessionsSkippedActive": 0,
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
                  "coverage": "complete",
                  "sessionsScanned": 1,
                  "sessionsSkippedUnchanged": 0,
                  "sessionsSkippedActive": 0,
```

with:

```csharp
                  "coverage": "complete",
                  "sessionsScanned": 1,
                  "unreadableStoreEntries": 0,
                  "sessionsSkippedUnchanged": 0,
                  "sessionsSkippedActive": 0,
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
    }

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
```

with:

```csharp
    }

    [Fact]
    public async Task IC12_ADryRunRefusesALedgerWithOneCompanionFile()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        await new SqliteLedgerStore(harness.Context.DatabasePath, harness.TimeProvider).InitializeAsync();
        SqliteConnection.ClearAllPools();
        await File.WriteAllBytesAsync(harness.Context.DatabasePath + "-wal", []);
        new CopilotSessionLogs("s-a").Usage("u1", S1).Write(harness.Context.CopilotHome);

        CliRun run = await harness.RunAsync("import", "--all", "--dry-run");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal("", run.Out);
        Assert.Equal(
            "Dry run stopped: the ledger has a -wal or -shm file without the other, and reading it could create the missing one. Open the ledger once with another gh-ledger command, such as status, and try again.\n",
            run.Error);
        Assert.False(File.Exists(harness.Context.DatabasePath + "-shm"));
        Assert.Equal(0, new FileInfo(harness.Context.DatabasePath + "-wal").Length);
    }

    [Fact]
    public async Task IC13_AStoreThatCantBeListedIsAWarning()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        Directory.CreateDirectory(harness.Context.CopilotHome);
        await File.WriteAllTextAsync(Path.Combine(harness.Context.CopilotHome, "session-state"), "not a folder");

        CliRun text = await harness.RunAsync("import", "--all");
        CliRun json = await harness.RunAsync("import", "--all", "--json");

        Assert.Equal(2, text.ExitCode);
        Assert.Contains("Sessions scanned                   0\n", text.Out);
        Assert.Contains("Coverage                  incomplete\n", text.Out);
        Assert.EndsWith("\nNothing new to record.\nCoverage is incomplete; see the warnings.\n", text.Out);
        Assert.Equal(
            "Warning: Copilot session logs: 1 folder or file couldn't be listed; any logs there weren't imported\n",
            text.Error);
        Assert.Equal(2, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Equal(1, document.RootElement.GetProperty("sources")[0].GetProperty("unreadableStoreEntries").GetInt64());
        Assert.Equal(
            """{"source":"copilot","log":"","code":"unreadableStore","event":"unknown","field":null,"version":null,"count":1,"line":null,"byteOffset":null}""",
            JsonSerializer.Serialize(Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray())));
    }

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
```

Edit 1 of 5 in `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs`, replace:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
```

with:

```csharp
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
```

Edit 2 of 5 in `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs`, replace:

```csharp
        File.WriteAllText(Path.Combine(day, $"rollout-2026-13-20T12-00-00-{Thread}.jsonl"), "x");

        IReadOnlyList<SessionLogLocation> logs = Source.Discover(temp.Path);

        Assert.Equal(
```

with:

```csharp
        File.WriteAllText(Path.Combine(day, $"rollout-2026-13-20T12-00-00-{Thread}.jsonl"), "x");

        SessionLogDiscovery discovery = Source.Discover(temp.Path);
        IReadOnlyList<SessionLogLocation> logs = discovery.Logs;

        Assert.Equal(
```

Edit 3 of 5 in `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs`, replace:

```csharp
        Assert.Equal([false, false, true, false], logs.Select(log => log.IsCompressed));
        Assert.All(logs, log => Assert.False(log.IsActive));
        Assert.Empty(Source.Discover(Path.Combine(temp.Path, "missing")));
    }

    [Theory]
```

with:

```csharp
        Assert.Equal([false, false, true, false], logs.Select(log => log.IsCompressed));
        Assert.All(logs, log => Assert.False(log.IsActive));
        Assert.Equal(0, discovery.UnreadableEntries);
        SessionLogDiscovery missing = Source.Discover(Path.Combine(temp.Path, "missing"));
        Assert.Empty(missing.Logs);
        Assert.Equal(0, missing.UnreadableEntries);
    }

    [Theory]
```

Edit 4 of 5 in `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs`, replace:

```csharp
        using TempDirectory temp = new();
        string path = new CodexRollouts(Thread).Write(temp.Path, compressed: true);
        SessionLogLocation location = Assert.Single(Source.Discover(temp.Path));
        File.Delete(path);

        SessionLogReadResult result = await Source.ReadAsync(location, CancellationToken.None);
```

with:

```csharp
        using TempDirectory temp = new();
        string path = new CodexRollouts(Thread).Write(temp.Path, compressed: true);
        SessionLogLocation location = Assert.Single(Source.Discover(temp.Path).Logs);
        File.Delete(path);

        SessionLogReadResult result = await Source.ReadAsync(location, CancellationToken.None);
```

Edit 5 of 5 in `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs`, replace:

```csharp
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home)), CancellationToken.None);

    private static void AssertNoCanary(SessionLogReadResult result) =>
```

with:

```csharp
    }

    // A rollout folder that can't be listed is counted, and the other folder's rollouts are still found.
    [Fact]
    public async Task CX15_ARolloutFolderThatCantBeListedIsCounted()
    {
        using TempDirectory temp = new();
        new CodexRollouts(Thread).Write(temp.Path, archived: true);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "sessions"), "not a folder");

        SessionLogDiscovery discovery = Source.Discover(temp.Path);

        Assert.Equal([$"archived/{Thread}"], discovery.Logs.Select(log => log.LogId));
        Assert.Equal(1, discovery.UnreadableEntries);
    }

    // A rollout's SHA-256 covers the bytes it was read up to.
    [Fact]
    public async Task CX16_AReadRolloutHasTheHashOfItsBytes()
    {
        using TempDirectory temp = new();
        string path = new CodexRollouts(Thread).TurnContext("turn-1", "gpt-6.1-sol").Usage("resp_1", "turn-1").Write(temp.Path);
        SessionLogLocation location = Assert.Single(Source.Discover(temp.Path).Logs);
        byte[] bytes = await File.ReadAllBytesAsync(path);
        await File.AppendAllTextAsync(path, "{\"type\":\"event_msg\"}\n");

        SessionLogReadResult result = await Source.ReadAsync(location, CancellationToken.None);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), result.ContentSha256);
        Assert.Equal(result.ContentSha256, await SessionLogHash.ComputeAsync(location, CancellationToken.None));
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);

    private static void AssertNoCanary(SessionLogReadResult result) =>
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Import/CopilotRecordTests.cs`, replace:

```csharp
    }

    internal static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home)), CancellationToken.None);

    internal static void AssertNoCanary(SessionLogReadResult result) =>
```

with:

```csharp
    }

    internal static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);

    internal static void AssertNoCanary(SessionLogReadResult result) =>
```

Edit 1 of 4 in `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs`, replace:

```csharp
        await File.WriteAllTextAsync(Path.Combine(root, "stray.jsonl"), "{}\n");

        IReadOnlyList<SessionLogLocation> logs = Source.Discover(temp.Path);

        Assert.Equal(["a-session", "b-session"], logs.Select(log => log.LogId));
```

with:

```csharp
        await File.WriteAllTextAsync(Path.Combine(root, "stray.jsonl"), "{}\n");

        SessionLogDiscovery discovery = Source.Discover(temp.Path);
        IReadOnlyList<SessionLogLocation> logs = discovery.Logs;

        Assert.Equal(["a-session", "b-session"], logs.Select(log => log.LogId));
```

Edit 2 of 4 in `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs`, replace:

```csharp
        Assert.Equal(new DateTimeOffset(File.GetLastWriteTimeUtc(first), TimeSpan.Zero), logs[0].LastWriteTime);
        Assert.All(logs, log => Assert.Equal(ImportSourceKind.Copilot, log.Source));
        Assert.Empty(Source.Discover(Path.Combine(temp.Path, "missing")));
    }

    [Theory]
```

with:

```csharp
        Assert.Equal(new DateTimeOffset(File.GetLastWriteTimeUtc(first), TimeSpan.Zero), logs[0].LastWriteTime);
        Assert.All(logs, log => Assert.Equal(ImportSourceKind.Copilot, log.Source));
        Assert.Equal(0, discovery.UnreadableEntries);
        SessionLogDiscovery missing = Source.Discover(Path.Combine(temp.Path, "missing"));
        Assert.Empty(missing.Logs);
        Assert.Equal(0, missing.UnreadableEntries);
    }

    [Theory]
```

Edit 3 of 4 in `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs`, replace:

```csharp
        using TempDirectory temp = new();
        string path = new CopilotSessionLogs(SessionId).Write(temp.Path);
        SessionLogLocation location = Assert.Single(Source.Discover(temp.Path));
        await File.AppendAllTextAsync(path, "{not json\n");

        SessionLogReadResult result = await Source.ReadAsync(location, CancellationToken.None);
```

with:

```csharp
        using TempDirectory temp = new();
        string path = new CopilotSessionLogs(SessionId).Write(temp.Path);
        SessionLogLocation location = Assert.Single(Source.Discover(temp.Path).Logs);
        await File.AppendAllTextAsync(path, "{not json\n");

        SessionLogReadResult result = await Source.ReadAsync(location, CancellationToken.None);
```

Edit 4 of 4 in `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs`, replace:

```csharp
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home)), CancellationToken.None);

    private static async Task WriteAsync(string home, string text)
```

with:

```csharp
    }

    [Fact]
    public async Task CS15_ASessionStoreThatCantBeListedIsCounted()
    {
        using TempDirectory temp = new();
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "session-state"), "not a folder");

        SessionLogDiscovery discovery = Source.Discover(temp.Path);

        Assert.Empty(discovery.Logs);
        Assert.Equal(1, discovery.UnreadableEntries);
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);

    private static async Task WriteAsync(string home, string text)
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Import/LedgerImporterTests.cs`, replace:

```csharp
    }

    private static string CopilotHome(TempDirectory temp) => Path.Combine(temp.Path, "copilot");

    private static string CodexHome(TempDirectory temp) => Path.Combine(temp.Path, "codex");
```

with:

```csharp
    }

    // Size and last-write time only hint that a log is unchanged: a log rewritten with the same size and its old last-write
    // time is read again, because its SHA-256 differs.
    [Fact]
    public async Task IR16_ASameSizeRewriteWithItsOldLastWriteTimeIsReadAgain()
    {
        using TempDirectory temp = new();
        string path = new CopilotSessionLogs("s-a").Usage("u1", S1).Write(CopilotHome(temp));
        byte[] bytes = await FileRetry.ReadAllBytesAsync(path);
        ImportSourceReport first = Copilot(await RunAsync(temp));
        DateTime written = File.GetLastWriteTimeUtc(path);
        ImportSessionCheckpoint? checkpoint =
            await (await StoreAsync(temp)).GetImportSessionAsync(ImportSourceKind.Copilot, "s-a");
        new CopilotSessionLogs("s-a").Usage("u2", S2).Write(CopilotHome(temp));
        File.SetLastWriteTimeUtc(path, written);

        ImportSourceReport second = Copilot(await RunAsync(temp));
        ImportSourceReport third = Copilot(await RunAsync(temp));

        Assert.Equal(1, first.Recorded);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), checkpoint!.LogSha256);
        Assert.Equal(bytes.Length, new FileInfo(path).Length);
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        Assert.Equal((0, 1), (second.SessionsSkippedUnchanged, second.Recorded));
        Assert.Equal((1, 0), (third.SessionsSkippedUnchanged, third.Recorded));
        Assert.Equal(2L, await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"));
    }

    // A store that exists but can't be listed makes its coverage incomplete; a missing one only has no logs.
    [Fact]
    public async Task IR17_AStoreThatCantBeListedIsReported()
    {
        using TempDirectory temp = new();
        Directory.CreateDirectory(CopilotHome(temp));
        await File.WriteAllTextAsync(Path.Combine(CopilotHome(temp), "session-state"), "not a folder");

        ImportReport report = await RunAsync(temp);

        ImportSourceReport copilot = Copilot(report);
        ImportSourceReport codex = Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Codex);
        Assert.Equal((1, 0), (copilot.UnreadableStoreEntries, copilot.SessionsScanned));
        Assert.False(copilot.Complete);
        Assert.Equal((0, true), (codex.UnreadableStoreEntries, codex.Complete));
        Assert.Equal(
            new ImportDiagnostic
            {
                Source = ImportSourceKind.Copilot,
                LogId = "",
                Code = ImportDiagnosticCode.UnreadableStore,
                Count = 1,
            },
            Assert.Single(report.Diagnostics));
    }

    private static string CopilotHome(TempDirectory temp) => Path.Combine(temp.Path, "copilot");

    private static string CodexHome(TempDirectory temp) => Path.Combine(temp.Path, "codex");
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Storage/LedgerSnapshotTests.cs`, replace:

```csharp
            LogSizeBytes = 10,
            LogLastWriteTime = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
            Complete = true,
            Recorded = 0,
```

with:

```csharp
            LogSizeBytes = 10,
            LogLastWriteTime = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
            LogSha256 = new string('c', 64),
            Complete = true,
            Recorded = 0,
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Storage/LedgerSnapshotTests.cs`, replace:

```csharp
            await snapshot.GetImportSessionAsync(ImportSourceKind.Codex, checkpoint.LogId));
    }
}
```

with:

```csharp
            await snapshot.GetImportSessionAsync(ImportSourceKind.Codex, checkpoint.LogId));
    }

    // With only one of -wal and -shm, a read-only open could create the other, so the snapshot refuses before opening.
    [Theory]
    [InlineData("-wal", "-shm")]
    [InlineData("-shm", "-wal")]
    public async Task SN7_ALedgerWithOneCompanionFileIsRefusedUnchanged(string present, string missing)
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("session-a", S1)]);
        SqliteConnection.ClearAllPools();
        await File.WriteAllBytesAsync(path + present, []);
        DateTime written = File.GetLastWriteTimeUtc(path + present);
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(path));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => LedgerSnapshot.OpenAsync(path));

        Assert.Equal(LedgerSnapshot.MissingCompanionMessage, exception.Message);
        Assert.False(File.Exists(path + missing));
        Assert.Equal(0, new FileInfo(path + present).Length);
        Assert.Equal(written, File.GetLastWriteTimeUtc(path + present));
        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(path)));
    }
}
```

Edit 1 of 4 in `tests/CopilotLedger.Tests/Storage/UsageAuthorityTests.cs`, replace:

```csharp
            LogSizeBytes = 4_096,
            LogLastWriteTime = new DateTimeOffset(2026, 11, 20, 12, 34, 56, 789, TimeSpan.Zero),
            Complete = true,
            Recorded = 1,
```

with:

```csharp
            LogSizeBytes = 4_096,
            LogLastWriteTime = new DateTimeOffset(2026, 11, 20, 12, 34, 56, 789, TimeSpan.Zero),
            LogSha256 = new string('a', 64),
            Complete = true,
            Recorded = 1,
```

Edit 2 of 4 in `tests/CopilotLedger.Tests/Storage/UsageAuthorityTests.cs`, replace:

```csharp
        ImportSessionCheckpoint? written = await store.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a");
        time.Advance(TimeSpan.FromHours(1));
        await store.RecordImportedAsync([], checkpoint with { Complete = false, LogSizeBytes = 8_192 });

        Assert.Equal(checkpoint with { ImportedAt = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero) }, written);
```

with:

```csharp
        ImportSessionCheckpoint? written = await store.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a");
        time.Advance(TimeSpan.FromHours(1));
        await store.RecordImportedAsync(
            [],
            checkpoint with { Complete = false, LogSizeBytes = 8_192, LogSha256 = new string('b', 64) });

        Assert.Equal(checkpoint with { ImportedAt = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero) }, written);
```

Edit 3 of 4 in `tests/CopilotLedger.Tests/Storage/UsageAuthorityTests.cs`, replace:

```csharp
                Complete = false,
                LogSizeBytes = 8_192,
                ImportedAt = new DateTimeOffset(2026, 11, 23, 11, 0, 0, TimeSpan.Zero),
            },
```

with:

```csharp
                Complete = false,
                LogSizeBytes = 8_192,
                LogSha256 = new string('b', 64),
                ImportedAt = new DateTimeOffset(2026, 11, 23, 11, 0, 0, TimeSpan.Zero),
            },
```

Edit 4 of 4 in `tests/CopilotLedger.Tests/Storage/UsageAuthorityTests.cs`, replace:

```csharp
                LogSizeBytes = 1,
                LogLastWriteTime = ImportEntries.Start,
                Complete = true,
                Recorded = 1,
```

with:

```csharp
                LogSizeBytes = 1,
                LogLastWriteTime = ImportEntries.Start,
                LogSha256 = new string('a', 64),
                Complete = true,
                Recorded = 1,
```

### Tests

- **CS1** and **CX1** (changed): discovery reports 0 unreadable entries, and a missing home has neither logs nor unreadable entries. **CS15**: a `session-state` that's a file is 1 unreadable entry and no logs. **CX15**: a `sessions` that's a file is 1 unreadable entry, and `archived_sessions` is still listed. **CX16**: a read rollout's `ContentSha256` is the SHA-256 of its bytes at discovery, and `SessionLogHash.ComputeAsync` agrees after bytes are appended.
- **IR16**: a log rewritten with the same size and its old last-write time is read again (its new request is recorded); the first checkpoint's hash is the SHA-256 of the first log's bytes; a third run skips it as unchanged. **IR17**: an unlistable Copilot store is 1 unreadable entry, 0 sessions scanned, and incomplete coverage, with one `UnreadableStore` diagnostic whose log ID is empty; Codex stays complete.
- **SN6**, **UA12**, and **UA13** (changed): checkpoints carry `LogSha256`, and UA12 checks that a later checkpoint replaces it. **SN7** (2): with only an empty `-wal` or only an empty `-shm`, `OpenAsync` throws with `MissingCompanionMessage`, creates no other file, and leaves the companion's length and last-write time and the ledger's hash as they were.
- **IC4** (changed): `unreadableStoreEntries` is 0 in both sources. **IC12**: a dry run beside a lone `-wal` exits with 1, prints the refusal on standard error and nothing on standard output, and creates no `-shm`. **IC13**: an unlistable Copilot store exits with 2, prints the exact warning and `Coverage is incomplete; see the warnings.`, and its JSON has `unreadableStoreEntries` 1 and the diagnostic `{"source":"copilot","log":"","code":"unreadableStore","event":"unknown","field":null,"version":null,"count":1,"line":null,"byteOffset":null}`.

### Acceptance criteria

- A same-size rewrite is never skipped (IR16); discovery failures limit coverage (CS15, CX15, IR17, IC13); a dry run never creates a companion file (SN7, IC12).
- `dotnet test CopilotLedger.slnx` passes **1260** tests (1251 + 9).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 15 specification: Late Tool Links, Unmatched Supersession, and Nested Repositories

Review findings 4, 5, and 6.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Import/ImportDiagnosticCode.cs` | `SupersededUnmatched` |
| `src/CopilotLedger.Storage/UsageAuthority.cs` | `UnmatchedSupersession`, `RecordedRowAsync`, `ReadUnmatchedSupersessionsAsync` |
| `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs` | A duplicate whose own row still counts gets its new tool links; internal `GetUnmatchedSupersessionsAsync` |
| `src/CopilotLedger.Storage/LedgerSnapshot.cs` | The same decisions on the snapshot |
| `src/CopilotLedger.Storage/Import/ImportRequest.cs` | `RepositorySlug` |
| `src/CopilotLedger.Storage/Import/ImportReport.cs` | `SupersededUnmatched`; `Complete` requires 0 |
| `src/CopilotLedger.Storage/Import/LedgerImporter.cs` | An optional `IGitContextResolver`; the working directory's repository for `--repo`; the unmatched-supersession report |
| `src/CopilotLedger.Cli/Import/ImportCommands.cs` | The row, the JSON field, the warning, and `RepositorySlug` |
| `docs/cli.md` | `--repo`, the overlap rules, the report, JSON, coverage, tool calls, and Troubleshooting |
| `docs/guide.md` | Both import samples gain the new row |
| `tests/CopilotLedger.Tests/Import/LedgerImporterTests.cs` | IR5 changed; new: IR18, IR19 |
| `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs` | IC3, IC4, and IC6 changed; new: IC14 |

### Contracts

- `UsageAuthority.UnmatchedSupersession(string ConversationId, string? RepositoryRoot, string? RepositorySlug, long Count)` (internal); `ImportRequest.RepositorySlug` (`string?`); `ImportSourceReport.SupersededUnmatched` (required, after `AmbiguousOverlap`); `ImportDiagnosticCode.SupersededUnmatched` (last).
- `LedgerImporter(ICreditCalculator calculator, TimeProvider? timeProvider = null, IGitContextResolver? gitResolver = null)`; the resolver defaults to `FileSystemGitContextResolver`.

### Algorithm

1. **Late tool links (finding 4).** When `RecordImportedAsync` resolves a record with tool links as `Duplicate` because its alias exists, `RecordedRowAsync` looks for the row of its `recorded` alias that is still effective and has the record's own trace and span IDs: the same record read again from the same log, never another session's copy. Each link is inserted, owned by that row, with `ON CONFLICT (trace_id, span_id) DO NOTHING`, and `ToolLinksRecorded` counts the inserted ones. A dry run counts the links that don't exist yet and weren't counted earlier in the run.
2. **Unmatched supersession (finding 5).** After a source's logs, the importer reads the source's imported rows (`ingest_source = 'import:<source>'`) that are superseded by a row that doesn't share their canonical ID (`NOT EXISTS` a row with `id = superseded_by` and the same non-null `canonical_usage_id`), that started at or after `--since`, grouped by conversation, recorded root, and slug. With `--repo`, a group counts when its recorded root is the repository's root (case-insensitive on Windows), or, without one, when its slug is the repository's slug (case-insensitive). Each conversation that counts adds to `SupersededUnmatched` and gets one `SupersededUnmatched` diagnostic whose `LogId` is its conversation ID, so coverage is incomplete and the exit code is 2. A row superseded by its own call's live span doesn't count, and no row is ever made effective again. A dry run reads the same rows from its snapshot (none before schema version 6).
3. **Nested repositories (finding 6).** A record with a recorded root matches by it, as before. Otherwise its working directory must be the root or below it, and `IGitContextResolver.Resolve(workingDirectory)?.RepositoryRoot` must be the root. A `null` result (a folder that's gone or outside any repository), or an `IOException`, `UnauthorizedAccessException`, `ArgumentException`, or `NotSupportedException`, is out of scope. Results are cached per run by working directory, and only the repository root is used, never today's branch (#63 allows repository identity for filtering).

### Output

- The table gains `Superseded, unmatched` after `Ambiguous-overlap`; JSON gains `supersededUnmatched` after `ambiguousOverlap`.
- Warning: `Warning: Copilot session <conversation ID>: <n> imported request(s) superseded by live spans that can't be matched to it|them; requests the daemon missed may not count` (`Codex session …` for Codex).
- The `--since` hint needs `SupersededUnmatched` 0 too.

### Code (copy exactly)

Edit 1 of 6 in `docs/cli.md`, replace:

````markdown
```

- `--repo <path>` imports only requests made in the Git repository at `<path>`, a working-tree path such as `.`, unlike the reports' repository key. A session is in the repository when the repository root it recorded is that repository's root, or, when it recorded none, when its working directory is inside it. `--all` imports requests from every repository and from outside any. Use one of the two.
- `--source copilot`, the default, reads Copilot's session logs, `session-state/<session>/events.jsonl` under `~/.copilot`, or under `COPILOT_HOME` when it's set. `--source codex` reads Codex's rollouts under `sessions` and `archived_sessions` in `~/.codex`, or in `CODEX_HOME` when it's set. Repeat `--source` to read both. `--source claude` is refused: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it.
- `--since` takes the reports' periods and defaults to `all`. Logs last written before the period starts aren't read, by the last-write time the file system reports, and requests that started before it aren't recorded.
````

with:

````markdown
```

- `--repo <path>` imports only requests made in the Git repository at `<path>`, a working-tree path such as `.`, unlike the reports' repository key. A session is in the repository when the repository root it recorded is that repository's root, or, when it recorded none, when its working directory is in that repository and not in another repository nested inside it. For that, import finds the working directory's repository on disk now, and leaves the session out when it can't, such as when the folder is gone. `--all` imports requests from every repository and from outside any. Use one of the two.
- `--source copilot`, the default, reads Copilot's session logs, `session-state/<session>/events.jsonl` under `~/.copilot`, or under `COPILOT_HOME` when it's set. `--source codex` reads Codex's rollouts under `sessions` and `archived_sessions` in `~/.codex`, or in `CODEX_HOME` when it's set. Repeat `--source` to read both. `--source claude` is refused: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it.
- `--since` takes the reports' periods and defaults to `all`. Logs last written before the period starts aren't read, by the last-write time the file system reports, and requests that started before it aren't recorded.
````

Edit 2 of 6 in `docs/cli.md`, replace:

```markdown
- A Copilot BYOK request and a Codex request have no such ID. They're recorded only from sessions that have no live spans. In a session with live spans, they're deferred (`Ambiguous-overlap`).
- A session's live spans without a service request ID, such as the spans an earlier gh-ledger recorded, can't be matched to anything, so every imported request of that session is deferred.
- A live span that arrives after its request was imported supersedes the imported row, in the same transaction: the imported row stays in the ledger but no longer counts in any report, MCP tool, budget, or circuit-breaker rule. A live span without a service request ID supersedes every imported row of its session, and any live span supersedes the imported rows of its session that have no ID.

Importing again records nothing twice. A log that was imported completely is skipped while its size, last-write time, and SHA-256 hash stay the same, so import reads such a log once to hash it: the size and last-write time alone could miss a log rewritten in place. Any other log is read again, and the requests recorded from it before count as `Duplicate`. An import stopped by Ctrl+C or an error keeps the requests it recorded, and running it again completes it.
```

with:

```markdown
- A Copilot BYOK request and a Codex request have no such ID. They're recorded only from sessions that have no live spans. In a session with live spans, they're deferred (`Ambiguous-overlap`).
- A session's live spans without a service request ID, such as the spans an earlier gh-ledger recorded, can't be matched to anything, so every imported request of that session is deferred.
- A live span that arrives after its request was imported supersedes the imported row, in the same transaction: the imported row stays in the ledger but no longer counts in any report, MCP tool, budget, or circuit-breaker rule. A live span without a service request ID supersedes every imported row of its session, and any live span supersedes the imported rows of its session that have no ID. Those live spans may not cover every request of the session, so each later import whose scope holds such rows reports them as `Superseded, unmatched`, and its coverage as incomplete. They never count again.

Importing again records nothing twice. A log that was imported completely is skipped while its size, last-write time, and SHA-256 hash stay the same, so import reads such a log once to hash it: the size and last-write time alone could miss a log rewritten in place. Any other log is read again, and the requests recorded from it before count as `Duplicate`. An import stopped by Ctrl+C or an error keeps the requests it recorded, and running it again completes it.
```

Edit 3 of 6 in `docs/cli.md`, replace:

```markdown
| Overlap-skipped | Requests that live spans recorded |
| Ambiguous-overlap | Requests deferred because their session's live spans can't be matched to them |
| Malformed lines | Lines that aren't one JSON event, are longer than 8 MiB, or are incomplete |
| Unsupported usage shapes | Usage records with a missing or invalid field |
```

with:

```markdown
| Overlap-skipped | Requests that live spans recorded |
| Ambiguous-overlap | Requests deferred because their session's live spans can't be matched to them |
| Superseded, unmatched | Imported requests in scope that live spans superseded without matching them, from any run; they no longer count, and the live spans may not cover them all |
| Malformed lines | Lines that aren't one JSON event, are longer than 8 MiB, or are incomplete |
| Unsupported usage shapes | Usage records with a missing or invalid field |
```

Edit 4 of 6 in `docs/cli.md`, replace:

```markdown
`--json` writes no warnings; it lists them as `diagnostics`. Its fields are `dryRun`, `period`, `from`, `repository` (the `owner/repo` slug, or the root when the repository has none) and `repositoryRoot` (both `null` with `--all`), `coverage`, and:

- `sources`: one object per source, with `source`, `coverage`, `sessionsScanned`, `unreadableStoreEntries` (folders and files of the source's store that couldn't be listed), `sessionsSkippedUnchanged`, `sessionsSkippedActive`, `sessionsSkippedOutOfScope`, `sessionsUnsupported`, `sessionsAggregateOnly` (the unsupported sessions from before Copilot 1.0.103), `recordsParsed`, `recordsOutOfScope`, `recorded`, `duplicate`, `overlapSkipped`, `ambiguousOverlap`, `malformedLines`, `unsupportedUsageShapes`, `costOutOfRange`, `journalPredecessors`, `skippedEvents` (events of other types), `unpriced`, `toolLinksRecorded`, and the recorded requests' `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans`.
- `totals`: `recorded` and the same amounts over every source.
- `diagnostics`: each with `source`, `log`, `code`, `event`, `field`, `version`, `count`, `line`, and `byteOffset`. `log` is empty for `unreadableStore`, which is about the source's store rather than one log.

| Exit code | Meaning |
```

with:

```markdown
`--json` writes no warnings; it lists them as `diagnostics`. Its fields are `dryRun`, `period`, `from`, `repository` (the `owner/repo` slug, or the root when the repository has none) and `repositoryRoot` (both `null` with `--all`), `coverage`, and:

- `sources`: one object per source, with `source`, `coverage`, `sessionsScanned`, `unreadableStoreEntries` (folders and files of the source's store that couldn't be listed), `sessionsSkippedUnchanged`, `sessionsSkippedActive`, `sessionsSkippedOutOfScope`, `sessionsUnsupported`, `sessionsAggregateOnly` (the unsupported sessions from before Copilot 1.0.103), `recordsParsed`, `recordsOutOfScope`, `recorded`, `duplicate`, `overlapSkipped`, `ambiguousOverlap`, `supersededUnmatched`, `malformedLines`, `unsupportedUsageShapes`, `costOutOfRange`, `journalPredecessors`, `skippedEvents` (events of other types), `unpriced`, `toolLinksRecorded`, and the recorded requests' `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans`.
- `totals`: `recorded` and the same amounts over every source.
- `diagnostics`: each with `source`, `log`, `code`, `event`, `field`, `version`, `count`, `line`, and `byteOffset`. `log` is empty for `unreadableStore`, which is about the source's store rather than one log, and is the session ID for `supersededUnmatched`.

| Exit code | Meaning |
```

Edit 5 of 6 in `docs/cli.md`, replace:

```markdown
| 1 | The options are invalid, or an error stopped the import. |

Skipped active, unchanged, and out-of-scope logs don't make coverage incomplete. A folder or file of a session store that exists but can't be listed does, with a warning such as `Copilot session logs: 1 folder or file couldn't be listed`; a store that doesn't exist only has no logs. When the only gap is sessions from before Copilot 1.0.103, the report's last line suggests `--since`, which leaves them out. With `--repo`, logs of other repositories are read again on every run, because they may hold requests of this one.

Imported requests count in every report and MCP tool, as live ones do, with the branch their log recorded. Tool calls are imported only from the last 7 days, as long as the ledger keeps tool calls; an older request keeps only the number of tool calls it made. Imported requests have no agent-invocation span, so `status --cache --turns` shows their `Growth` as `n/a`, and the input-growth ranking of `--bloat` leaves them out. `import` warns when the daemon runs another gh-ledger version: restart the daemon, so that the live spans it records supersede imported rows by this version's rules.

### What import reads and keeps
```

with:

```markdown
| 1 | The options are invalid, or an error stopped the import. |

Skipped active, unchanged, and out-of-scope logs don't make coverage incomplete. A folder or file of a session store that exists but can't be listed does, with a warning such as `Copilot session logs: 1 folder or file couldn't be listed`; a store that doesn't exist only has no logs. So do `Superseded, unmatched` requests: those that started in the `--since` period and, with `--repo`, recorded that repository's root, or its `owner/repo` slug when they recorded no root. When the only gap is sessions from before Copilot 1.0.103, the report's last line suggests `--since`, which leaves them out. With `--repo`, logs of other repositories are read again on every run, because they may hold requests of this one.

Imported requests count in every report and MCP tool, as live ones do, with the branch their log recorded. Tool calls are imported only from the last 7 days, as long as the ledger keeps tool calls; an older request keeps only the number of tool calls it made. A tool call that ended after its request was imported, such as in a session imported with `--include-active`, is added when a later import reads the session again. Imported requests have no agent-invocation span, so `status --cache --turns` shows their `Growth` as `n/a`, and the input-growth ranking of `--bloat` leaves them out. `import` warns when the daemon runs another gh-ledger version: restart the daemon, so that the live spans it records supersede imported rows by this version's rules.

### What import reads and keeps
```

Edit 6 of 6 in `docs/cli.md`, replace:

```markdown
- **`gh ledger import` exits with code 2.** Some coverage is incomplete, and the warnings say why; everything else was imported. A session from before Copilot 1.0.103 is never imported, so leave it out with `--since`. A malformed line or an unsupported usage record is skipped on every run, and the rest of its log is imported.
- **`gh ledger import` defers requests (`Ambiguous-overlap`).** Their session has live spans that can't be matched to them, such as spans an earlier gh-ledger recorded, so import leaves them out rather than risk charging a request twice. The live spans still count.
- **Port 4318 is in use.** Stop the other program or the old daemon, then run `gh ledger daemon start`.
- **`gh extension upgrade` or `remove` fails on Windows.** A program is running the extension's binary: the daemon, or the MCP server of a Copilot session. Run `gh ledger daemon stop`, close the Copilot CLI sessions, the GitHub Copilot app, and VS Code, then try again.
```

with:

```markdown
- **`gh ledger import` exits with code 2.** Some coverage is incomplete, and the warnings say why; everything else was imported. A session from before Copilot 1.0.103 is never imported, so leave it out with `--since`. A malformed line or an unsupported usage record is skipped on every run, and the rest of its log is imported.
- **`gh ledger import` defers requests (`Ambiguous-overlap`).** Their session has live spans that can't be matched to them, such as spans an earlier gh-ledger recorded, so import leaves them out rather than risk charging a request twice. The live spans still count.
- **`gh ledger import` reports `Superseded, unmatched` requests.** Live spans that can't be matched to them arrived after they were imported and replaced them, as above. The live spans count, but they may not cover every request of the session, so import reports these on every run whose scope holds them; `--since` can leave older ones out.
- **Port 4318 is in use.** Stop the other program or the old daemon, then run `gh ledger daemon start`.
- **`gh extension upgrade` or `remove` fails on Windows.** A program is running the extension's binary: the daemon, or the MCP server of a Copilot session. Run `gh ledger daemon stop`, close the Copilot CLI sessions, the GitHub Copilot app, and VS Code, then try again.
```

Edit 1 of 2 in `docs/guide.md`, replace:

```markdown
Unsupported sessions             0
Records parsed                   6
Out of scope                     1
Recorded                         5
Duplicate                        0
Overlap-skipped                  0
Ambiguous-overlap                0
Malformed lines                  0
Unsupported usage shapes         0
Cost out of range                0
Unpriced                         0
Coverage                  complete

Importing would record 5 requests: 38.70 AI credits ($0.39), API equivalent $0.39.
```

with:

```markdown
Unsupported sessions             0
Records parsed                   6
Out of scope                     1
Recorded                         5
Duplicate                        0
Overlap-skipped                  0
Ambiguous-overlap                0
Superseded, unmatched            0
Malformed lines                  0
Unsupported usage shapes         0
Cost out of range                0
Unpriced                         0
Coverage                  complete

Importing would record 5 requests: 38.70 AI credits ($0.39), API equivalent $0.39.
```

Edit 2 of 2 in `docs/guide.md`, replace:

```markdown
Overlap-skipped                  0
Ambiguous-overlap                0
Malformed lines                  0
Unsupported usage shapes         0
```

with:

```markdown
Overlap-skipped                  0
Ambiguous-overlap                0
Superseded, unmatched            0
Malformed lines                  0
Unsupported usage shapes         0
```

Edit 1 of 7 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
        ("Overlap-skipped", report => report.OverlapSkipped),
        ("Ambiguous-overlap", report => report.AmbiguousOverlap),
        ("Malformed lines", report => report.MalformedLines),
        ("Unsupported usage shapes", report => report.UnsupportedUsageShapes),
```

with:

```csharp
        ("Overlap-skipped", report => report.OverlapSkipped),
        ("Ambiguous-overlap", report => report.AmbiguousOverlap),
        ("Superseded, unmatched", report => report.SupersededUnmatched),
        ("Malformed lines", report => report.MalformedLines),
        ("Unsupported usage shapes", report => report.UnsupportedUsageShapes),
```

Edit 2 of 7 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
            ],
            RepositoryRoot = git?.RepositoryRoot,
            Since = period.From,
            DryRun = dryRun,
```

with:

```csharp
            ],
            RepositoryRoot = git?.RepositoryRoot,
            RepositorySlug = git?.RepositorySlug,
            Since = period.From,
            DryRun = dryRun,
```

Edit 3 of 7 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
        source.UnsupportedUsageShapes == 0 &&
        source.AmbiguousOverlap == 0 &&
        source.CostOutOfRange == 0 &&
        source.JournalPredecessors == 0;
```

with:

```csharp
        source.UnsupportedUsageShapes == 0 &&
        source.AmbiguousOverlap == 0 &&
        source.SupersededUnmatched == 0 &&
        source.CostOutOfRange == 0 &&
        source.JournalPredecessors == 0;
```

Edit 4 of 7 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
            writer.WriteNumber("overlapSkipped", source.OverlapSkipped);
            writer.WriteNumber("ambiguousOverlap", source.AmbiguousOverlap);
            writer.WriteNumber("malformedLines", source.MalformedLines);
            writer.WriteNumber("unsupportedUsageShapes", source.UnsupportedUsageShapes);
```

with:

```csharp
            writer.WriteNumber("overlapSkipped", source.OverlapSkipped);
            writer.WriteNumber("ambiguousOverlap", source.AmbiguousOverlap);
            writer.WriteNumber("supersededUnmatched", source.SupersededUnmatched);
            writer.WriteNumber("malformedLines", source.MalformedLines);
            writer.WriteNumber("unsupportedUsageShapes", source.UnsupportedUsageShapes);
```

Edit 5 of 7 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
    }

    // "<source> <session|rollout> <log ID>[, line <n> (byte <offset>)]: <message>", or "<source> session logs: <message>"
    // for the store itself.
    private static string Describe(ImportDiagnostic diagnostic)
    {
```

with:

```csharp
    }

    // "<source> <session|rollout> <log ID>[, line <n> (byte <offset>)]: <message>", "<source> session <conversation ID>:
    // <message>" for superseded rows, or "<source> session logs: <message>" for the store itself.
    private static string Describe(ImportDiagnostic diagnostic)
    {
```

Edit 6 of 7 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
        {
            return $"{Name(diagnostic.Source)} session logs: {Message(diagnostic)}";
        }

        string log = diagnostic.Source == ImportSourceKind.Codex ? "Codex rollout" : "Copilot session";
```

with:

```csharp
        {
            return $"{Name(diagnostic.Source)} session logs: {Message(diagnostic)}";
        }

        if (diagnostic.Code == ImportDiagnosticCode.SupersededUnmatched)
        {
            return $"{Name(diagnostic.Source)} session {diagnostic.LogId}: {Message(diagnostic)}";
        }

        string log = diagnostic.Source == ImportSourceKind.Codex ? "Codex rollout" : "Copilot session";
```

Edit 7 of 7 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
                ? "1 folder or file couldn't be listed; any logs there weren't imported"
                : $"{LedgerFormat.Count(count)} folders or files couldn't be listed; any logs there weren't imported",
            _ => throw new ArgumentOutOfRangeException(nameof(diagnostic), diagnostic.Code, null),
        };
```

with:

```csharp
                ? "1 folder or file couldn't be listed; any logs there weren't imported"
                : $"{LedgerFormat.Count(count)} folders or files couldn't be listed; any logs there weren't imported",
            ImportDiagnosticCode.SupersededUnmatched =>
                $"{Plural(count, "imported request")} superseded by live spans that can't be matched to {(count == 1 ? "it" : "them")}; requests the daemon missed may not count",
            _ => throw new ArgumentOutOfRangeException(nameof(diagnostic), diagnostic.Code, null),
        };
```

Edit 1 of 1 in `src/CopilotLedger.Core/Import/ImportDiagnosticCode.cs`, replace:

```csharp
    CostOutOfRange,               // set by the importer
    UnreadableStore,              // set by the importer: entries of the store that couldn't be listed or examined
}
```

with:

```csharp
    CostOutOfRange,               // set by the importer
    UnreadableStore,              // set by the importer: entries of the store that couldn't be listed or examined
    SupersededUnmatched,          // set by the importer: LogId is the conversation whose imported rows live rows superseded
}
```

Edit 1 of 2 in `src/CopilotLedger.Storage/Import/ImportReport.cs`, replace:

```csharp
    public required long OverlapSkipped { get; init; }
    public required long AmbiguousOverlap { get; init; }
    public required long MalformedLines { get; init; }
    public required long UnsupportedUsageShapes { get; init; }
```

with:

```csharp
    public required long OverlapSkipped { get; init; }
    public required long AmbiguousOverlap { get; init; }
    public required long SupersededUnmatched { get; init; }         // imported rows in scope that unmatched live rows superseded
    public required long MalformedLines { get; init; }
    public required long UnsupportedUsageShapes { get; init; }
```

Edit 2 of 2 in `src/CopilotLedger.Storage/Import/ImportReport.cs`, replace:

```csharp
        UnsupportedUsageShapes == 0 &&
        AmbiguousOverlap == 0 &&
        CostOutOfRange == 0 &&
        JournalPredecessors == 0;
```

with:

```csharp
        UnsupportedUsageShapes == 0 &&
        AmbiguousOverlap == 0 &&
        SupersededUnmatched == 0 &&
        CostOutOfRange == 0 &&
        JournalPredecessors == 0;
```

Edit 1 of 1 in `src/CopilotLedger.Storage/Import/ImportRequest.cs`, replace:

```csharp
    public required IReadOnlyList<ImportSourceRequest> Sources { get; init; }   // at most one per kind, never Claude
    public string? RepositoryRoot { get; init; }     // --repo: a fully qualified working-tree root
    public DateTimeOffset? Since { get; init; }      // --since; null for all time
    public bool DryRun { get; init; }
```

with:

```csharp
    public required IReadOnlyList<ImportSourceRequest> Sources { get; init; }   // at most one per kind, never Claude
    public string? RepositoryRoot { get; init; }     // --repo: a fully qualified working-tree root
    public string? RepositorySlug { get; init; }     // with RepositoryRoot: its owner/repo slug, for rows without a root
    public DateTimeOffset? Since { get; init; }      // --since; null for all time
    public bool DryRun { get; init; }
```

Edit 1 of 10 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
```

with:

```csharp
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
```

Edit 2 of 10 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
    public static readonly TimeSpan ToolLinkRetention = TimeSpan.FromDays(7);

    private readonly ICreditCalculator _calculator;
    private readonly TimeProvider _timeProvider;

    public LedgerImporter(ICreditCalculator calculator, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        _calculator = calculator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Throws ArgumentException for an invalid request, InvalidOperationException for a ledger newer than this gh-ledger,
```

with:

```csharp
    public static readonly TimeSpan ToolLinkRetention = TimeSpan.FromDays(7);

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly ICreditCalculator _calculator;
    private readonly TimeProvider _timeProvider;
    private readonly IGitContextResolver _gitResolver;

    // gitResolver finds the repository of a record that recorded only its working directory, for RepositoryRoot; it
    // defaults to FileSystemGitContextResolver. Only the repository's root is used, never its current branch.
    public LedgerImporter(
        ICreditCalculator calculator,
        TimeProvider? timeProvider = null,
        IGitContextResolver? gitResolver = null)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        _calculator = calculator;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _gitResolver = gitResolver ?? new FileSystemGitContextResolver();
    }

    // Throws ArgumentException for an invalid request, InvalidOperationException for a ledger newer than this gh-ledger,
```

Edit 3 of 10 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
            List<ImportSourceReport> reports = [];
            List<ImportDiagnostic> diagnostics = [];
            foreach (ImportSourceRequest source in request.Sources.OrderBy(source => source.Source.Kind))
            {
```

with:

```csharp
            List<ImportSourceReport> reports = [];
            List<ImportDiagnostic> diagnostics = [];
            Dictionary<string, string?> roots = new(StringComparer.Ordinal);
            foreach (ImportSourceRequest source in request.Sources.OrderBy(source => source.Source.Kind))
            {
```

Edit 4 of 10 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ImportLogAsync(request, source.Source, log, store, snapshot, linkCutoff, tally, diagnostics, cancellationToken);
                }

                reports.Add(tally.ToReport());
```

with:

```csharp
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ImportLogAsync(
                        request, source.Source, log, store, snapshot, linkCutoff, tally, diagnostics, roots, cancellationToken);
                }

                // Imported rows that live spans superseded without matching them may hold requests the live spans don't
                // cover, so they limit coverage on every run whose scope holds them. They're never made effective again.
                IReadOnlyList<UsageAuthority.UnmatchedSupersession> superseded = store is not null
                    ? await store.GetUnmatchedSupersessionsAsync(source.Source.Kind, request.Since, cancellationToken)
                    : await snapshot!.GetUnmatchedSupersessionsAsync(source.Source.Kind, request.Since, cancellationToken);
                foreach (IGrouping<string, UsageAuthority.UnmatchedSupersession> conversation in superseded
                             .Where(row => IsInRepository(row, request))
                             .GroupBy(row => row.ConversationId, StringComparer.Ordinal))
                {
                    long count = conversation.Sum(row => row.Count);
                    tally.SupersededUnmatched += count;
                    diagnostics.Add(new ImportDiagnostic
                    {
                        Source = source.Source.Kind,
                        LogId = conversation.Key,
                        Code = ImportDiagnosticCode.SupersededUnmatched,
                        Count = count,
                    });
                }

                reports.Add(tally.ToReport());
```

Edit 5 of 10 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
        Tally tally,
        List<ImportDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
```

with:

```csharp
        Tally tally,
        List<ImportDiagnostic> diagnostics,
        Dictionary<string, string?> roots,
        CancellationToken cancellationToken)
    {
```

Edit 6 of 10 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
        foreach (ImportedUsageRecord record in result.Records)
        {
            if (IsOutOfScope(record, request))
            {
                outOfScope++;
```

with:

```csharp
        foreach (ImportedUsageRecord record in result.Records)
        {
            if (IsOutOfScope(record, request, roots))
            {
                outOfScope++;
```

Edit 7 of 10 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
    }

    // Out of scope: started before Since, or, with RepositoryRoot, recorded in another repository: by its Git root when the
    // log recorded one, else by its working directory (the root or below it).
    private static bool IsOutOfScope(ImportedUsageRecord record, ImportRequest request)
    {
        if (request.Since is DateTimeOffset since && record.StartTime < since)
```

with:

```csharp
    }

    // Out of scope: started before Since, or, with RepositoryRoot, recorded in another repository: by its Git root when the
    // log recorded one, else by the Git root of its working directory, found now (roots caches them). A working directory
    // whose repository can't be found is out of scope, and so is one in a repository nested inside RepositoryRoot.
    private bool IsOutOfScope(ImportedUsageRecord record, ImportRequest request, Dictionary<string, string?> roots)
    {
        if (request.Since is DateTimeOffset since && record.StartTime < since)
```

Edit 8 of 10 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
        }

        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (record.Git?.RepositoryRoot is string recorded)
        {
            return !string.Equals(recorded, root, comparison);
        }

        string? directory = record.Git?.WorkingDirectory;
        return directory is null ||
               !(string.Equals(directory, root, comparison) ||
                 directory.StartsWith(root + Path.DirectorySeparatorChar, comparison));
    }

    private static void Validate(ImportRequest request)
```

with:

```csharp
        }

        if (record.Git?.RepositoryRoot is string recorded)
        {
            return !string.Equals(recorded, root, PathComparison);
        }

        string? directory = record.Git?.WorkingDirectory;
        if (directory is null ||
            !(string.Equals(directory, root, PathComparison) ||
              directory.StartsWith(root + Path.DirectorySeparatorChar, PathComparison)))
        {
            return true;
        }

        if (!roots.TryGetValue(directory, out string? resolved))
        {
            try
            {
                resolved = _gitResolver.Resolve(directory)?.RepositoryRoot;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                resolved = null;
            }

            roots[directory] = resolved;
        }

        return !string.Equals(resolved, root, PathComparison);
    }

    // With RepositoryRoot, a superseded row is in scope when it recorded that root, or, without a root, that repository's slug.
    private static bool IsInRepository(UsageAuthority.UnmatchedSupersession row, ImportRequest request) =>
        request.RepositoryRoot is not string root ||
        (row.RepositoryRoot is string recorded
            ? string.Equals(recorded, root, PathComparison)
            : row.RepositorySlug is not null &&
              string.Equals(row.RepositorySlug, request.RepositorySlug, StringComparison.OrdinalIgnoreCase));

    private static void Validate(ImportRequest request)
```

Edit 9 of 10 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
        public long OverlapSkipped { get; set; }
        public long AmbiguousOverlap { get; set; }
        public long MalformedLines { get; set; }
        public long UnsupportedUsageShapes { get; set; }
```

with:

```csharp
        public long OverlapSkipped { get; set; }
        public long AmbiguousOverlap { get; set; }
        public long SupersededUnmatched { get; set; }
        public long MalformedLines { get; set; }
        public long UnsupportedUsageShapes { get; set; }
```

Edit 10 of 10 in `src/CopilotLedger.Storage/Import/LedgerImporter.cs`, replace:

```csharp
            OverlapSkipped = OverlapSkipped,
            AmbiguousOverlap = AmbiguousOverlap,
            MalformedLines = MalformedLines,
            UnsupportedUsageShapes = UnsupportedUsageShapes,
```

with:

```csharp
            OverlapSkipped = OverlapSkipped,
            AmbiguousOverlap = AmbiguousOverlap,
            SupersededUnmatched = SupersededUnmatched,
            MalformedLines = MalformedLines,
            UnsupportedUsageShapes = UnsupportedUsageShapes,
```

Edit 1 of 5 in `src/CopilotLedger.Storage/LedgerSnapshot.cs`, replace:

```csharp
    private readonly HashSet<(string Source, string Key)> _aliases = [];
    private readonly HashSet<string> _recordedCanonicalIds = new(StringComparer.Ordinal);

    private LedgerSnapshot(SqliteConnection? connection, SqliteTransaction? transaction, int schemaVersion)
```

with:

```csharp
    private readonly HashSet<(string Source, string Key)> _aliases = [];
    private readonly HashSet<string> _recordedCanonicalIds = new(StringComparer.Ordinal);
    private readonly HashSet<(string TraceId, string SpanId)> _links = [];

    private LedgerSnapshot(SqliteConnection? connection, SqliteTransaction? transaction, int schemaVersion)
```

Edit 2 of 5 in `src/CopilotLedger.Storage/LedgerSnapshot.cs`, replace:

```csharp
                entry.Span.ConversationId!,
                cancellationToken);
            if (_aliases.Contains((source, entry.RecordKeySha256)))
            {
```

with:

```csharp
                entry.Span.ConversationId!,
                cancellationToken);
            bool aliasInLedger = facts.AliasExists;
            if (_aliases.Contains((source, entry.RecordKeySha256)))
            {
```

Edit 3 of 5 in `src/CopilotLedger.Storage/LedgerSnapshot.cs`, replace:

```csharp
            {
                links += entry.ToolLinks.Count;
                if (canonical is not null)
                {
                    _recordedCanonicalIds.Add(canonical);
                }
            }
```

with:

```csharp
            {
                links += entry.ToolLinks.Count;
                foreach (GenAiSpanLink link in entry.ToolLinks)
                {
                    _links.Add((link.TraceId, link.SpanId));
                }

                if (canonical is not null)
                {
                    _recordedCanonicalIds.Add(canonical);
                }
            }
            else if (resolution == ImportResolution.Duplicate &&
                     aliasInLedger &&
                     entry.ToolLinks.Count > 0 &&
                     await UsageAuthority.RecordedRowAsync(_transaction!, source, entry, cancellationToken) is not null)
            {
                foreach (GenAiSpanLink link in entry.ToolLinks)
                {
                    if (_links.Add((link.TraceId, link.SpanId)) && !await LinkExistsAsync(link, cancellationToken))
                    {
                        links++;
                    }
                }
            }
```

Edit 4 of 5 in `src/CopilotLedger.Storage/LedgerSnapshot.cs`, replace:

```csharp
            : await UsageAuthority.ReadCheckpointAsync(_connection!, _transaction, source, logId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
```

with:

```csharp
            : await UsageAuthority.ReadCheckpointAsync(_connection!, _transaction, source, logId, cancellationToken);
    }

    // As SqliteLedgerStore.GetUnmatchedSupersessionsAsync; none before schema version 6.
    internal async Task<IReadOnlyList<UsageAuthority.UnmatchedSupersession>> GetUnmatchedSupersessionsAsync(
        ImportSourceKind source,
        DateTimeOffset? since,
        CancellationToken cancellationToken) =>
        SchemaVersion < 6
            ? []
            : await UsageAuthority.ReadUnmatchedSupersessionsAsync(_connection!, _transaction, source, since, cancellationToken);

    public async ValueTask DisposeAsync()
```

Edit 5 of 5 in `src/CopilotLedger.Storage/LedgerSnapshot.cs`, replace:

```csharp
    }

    // file:///C:/…/ledger.db?immutable=1 (file:///home/… elsewhere), every byte but [A-Za-z0-9-._~/:] percent-encoded.
    private static string ImmutableUri(string path)
```

with:

```csharp
    }

    private async Task<bool> LinkExistsAsync(GenAiSpanLink link, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = _connection!.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM span_links WHERE trace_id = @trace AND span_id = @span);";
        command.Parameters.AddWithValue("@trace", link.TraceId);
        command.Parameters.AddWithValue("@span", link.SpanId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))! == 1;
    }

    // file:///C:/…/ledger.db?immutable=1 (file:///home/… elsewhere), every byte but [A-Za-z0-9-._~/:] percent-encoded.
    private static string ImmutableUri(string path)
```

Edit 1 of 3 in `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs`, replace:

```csharp
{
    // Resolves each entry by the authority rules under one BEGIN IMMEDIATE transaction: inserts the Recorded ones with an
    // alias and the tool links they own, aliases the others that a row accounts for, and writes checkpoint (when it isn't
    // null) in the same transaction. Validates every entry and the checkpoint first (ArgumentException, nothing written).
    // An exception, cancellation included, rolls the whole call back.
    public async Task<ImportRecordResult> RecordImportedAsync(
        IReadOnlyList<ImportedLedgerEntry> entries,
```

with:

```csharp
{
    // Resolves each entry by the authority rules under one BEGIN IMMEDIATE transaction: inserts the Recorded ones with an
    // alias and the tool links they own, aliases the others that a row accounts for, adds the new tool links of a record read
    // again whose own row still counts, and writes checkpoint (when it isn't null) in the same transaction. Validates every
    // entry and the checkpoint first (ArgumentException, nothing written). An exception, cancellation included, rolls the
    // whole call back.
    public async Task<ImportRecordResult> RecordImportedAsync(
        IReadOnlyList<ImportedLedgerEntry> entries,
```

Edit 2 of 3 in `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs`, replace:

```csharp
                    cancellationToken);
            }

            resolutions.Add(resolution);
```

with:

```csharp
                    cancellationToken);
            }
            else if (resolution == ImportResolution.Duplicate &&
                     entry.ToolLinks.Count > 0 &&
                     await UsageAuthority.RecordedRowAsync(transaction, source, entry, cancellationToken) is long ownRowId)
            {
                // The record's own effective row, read again from its log: tool calls that completed since then get links.
                foreach (GenAiSpanLink link in entry.ToolLinks)
                {
                    links += await InsertOwnedLinkAsync(transaction, link, ownRowId, now, cancellationToken);
                }
            }

            resolutions.Add(resolution);
```

Edit 3 of 3 in `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs`, replace:

```csharp
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await UsageAuthority.ReadCheckpointAsync(connection, null, source, logId, cancellationToken);
    }

    private static RowProvenance ImportProvenance(ImportedLedgerEntry entry, string? canonicalUsageId) =>
```

with:

```csharp
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await UsageAuthority.ReadCheckpointAsync(connection, null, source, logId, cancellationToken);
    }

    // The source's imported rows that started at or after since (all when it's null) and that live rows superseded without
    // sharing their canonical ID (UsageAuthority.ReadUnmatchedSupersessionsAsync).
    internal async Task<IReadOnlyList<UsageAuthority.UnmatchedSupersession>> GetUnmatchedSupersessionsAsync(
        ImportSourceKind source,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await UsageAuthority.ReadUnmatchedSupersessionsAsync(connection, null, source, since, cancellationToken);
    }

    private static RowProvenance ImportProvenance(ImportedLedgerEntry entry, string? canonicalUsageId) =>
```

Edit 1 of 2 in `src/CopilotLedger.Storage/UsageAuthority.cs`, replace:

```csharp
        bool ConversationHasLive,
        bool ConversationHasUnidentifiedLive);

    // The first rule that matches decides.
```

with:

```csharp
        bool ConversationHasLive,
        bool ConversationHasUnidentifiedLive);

    // Imported rows of one conversation, with the Git context they recorded, that live rows superseded without sharing
    // their canonical ID: I2 and I3 retire them whether or not the live spans cover the same calls.
    internal readonly record struct UnmatchedSupersession(
        string ConversationId,
        string? RepositoryRoot,
        string? RepositorySlug,
        long Count);

    // The first rule that matches decides.
```

Edit 2 of 2 in `src/CopilotLedger.Storage/UsageAuthority.cs`, replace:

```csharp
    }

    internal static async Task UpsertCheckpointAsync(
        SqliteTransaction transaction,
```

with:

```csharp
    }

    // The row that recorded this very record of this log (a 'recorded' alias, and the same trace and span IDs), when it
    // still counts; null otherwise, such as for another session's copy of the record.
    internal static async Task<long?> RecordedRowAsync(
        SqliteTransaction transaction,
        string source,
        ImportedLedgerEntry entry,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT s.id
              FROM import_record_aliases AS a
              JOIN spans AS s ON s.id = a.span_row_id
             WHERE a.source = @source AND a.record_key_sha256 = @key AND a.resolution = 'recorded'
               AND s.accounting_state = 'effective' AND s.trace_id = @trace AND s.span_id = @span;
            """;
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@key", entry.RecordKeySha256);
        command.Parameters.AddWithValue("@trace", entry.Span.TraceId);
        command.Parameters.AddWithValue("@span", entry.Span.SpanId);
        return await command.ExecuteScalarAsync(cancellationToken) is long id ? id : null;
    }

    // The source's imported rows that live rows superseded without sharing their canonical ID, from since on (all when it's
    // null), by conversation and recorded Git context. A row superseded by the live span of its own call isn't one of them.
    internal static async Task<IReadOnlyList<UnmatchedSupersession>> ReadUnmatchedSupersessionsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        ImportSourceKind source,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT s.conversation_id, s.repo_root, s.repo_slug, COUNT(*)
              FROM spans AS s
             WHERE s.ingest_source = @ingest_source
               AND s.accounting_state = 'superseded'
               AND (@since IS NULL OR s.start_time_unix_ms >= @since)
               AND NOT EXISTS (SELECT 1 FROM spans AS l
                                WHERE l.id = s.superseded_by AND l.canonical_usage_id = s.canonical_usage_id)
             GROUP BY s.conversation_id, s.repo_root, s.repo_slug
             ORDER BY s.conversation_id, s.repo_root, s.repo_slug;
            """;
        command.Parameters.AddWithValue("@ingest_source", "import:" + ImportIds.SourceName(source));
        command.Parameters.AddWithValue(
            "@since",
            since is DateTimeOffset from ? (object)from.ToUnixTimeMilliseconds() : DBNull.Value);
        List<UnmatchedSupersession> rows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new UnmatchedSupersession(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt64(3)));
        }

        return rows;
    }

    internal static async Task UpsertCheckpointAsync(
        SqliteTransaction transaction,
```

Edit 1 of 5 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
            Overlap-skipped                  0
            Ambiguous-overlap                0
            Malformed lines                  0
            Unsupported usage shapes         0
```

with:

```csharp
            Overlap-skipped                  0
            Ambiguous-overlap                0
            Superseded, unmatched            0
            Malformed lines                  0
            Unsupported usage shapes         0
```

Edit 2 of 5 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
                  "recordsParsed": 1,
                  "recordsOutOfScope": 0,
                  "recorded": 1,
                  "duplicate": 0,
                  "overlapSkipped": 0,
                  "ambiguousOverlap": 0,
                  "malformedLines": 0,
                  "unsupportedUsageShapes": 0,
                  "costOutOfRange": 0,
                  "journalPredecessors": 0,
                  "skippedEvents": 0,
                  "unpriced": 0,
```

with:

```csharp
                  "recordsParsed": 1,
                  "recordsOutOfScope": 0,
                  "recorded": 1,
                  "duplicate": 0,
                  "overlapSkipped": 0,
                  "ambiguousOverlap": 0,
                  "supersededUnmatched": 0,
                  "malformedLines": 0,
                  "unsupportedUsageShapes": 0,
                  "costOutOfRange": 0,
                  "journalPredecessors": 0,
                  "skippedEvents": 0,
                  "unpriced": 0,
```

Edit 3 of 5 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
                  "overlapSkipped": 0,
                  "ambiguousOverlap": 0,
                  "malformedLines": 0,
                  "unsupportedUsageShapes": 0,
```

with:

```csharp
                  "overlapSkipped": 0,
                  "ambiguousOverlap": 0,
                  "supersededUnmatched": 0,
                  "malformedLines": 0,
                  "unsupportedUsageShapes": 0,
```

Edit 4 of 5 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
            Overlap-skipped                  1
            Ambiguous-overlap                0
            Malformed lines                  0
            Unsupported usage shapes         0
```

with:

```csharp
            Overlap-skipped                  1
            Ambiguous-overlap                0
            Superseded, unmatched            0
            Malformed lines                  0
            Unsupported usage shapes         0
```

Edit 5 of 5 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
    }

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
```

with:

```csharp
    }

    [Fact]
    public async Task IC14_RequestsSupersededWithoutAMatchAreAWarning()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        new CopilotSessionLogs("s-a").Usage("u1", S1).Usage("u2", S2).Write(harness.Context.CopilotHome);
        await harness.RunAsync("import", "--all");
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("s-a", null)]);

        CliRun text = await harness.RunAsync("import", "--all");
        CliRun json = await harness.RunAsync("import", "--all", "--json");

        Assert.Equal(2, text.ExitCode);
        Assert.Contains("Superseded, unmatched              2\n", text.Out);
        Assert.EndsWith("\nNothing new to record.\nCoverage is incomplete; see the warnings.\n", text.Out);
        Assert.Equal(
            "Warning: Copilot session s-a: 2 imported requests superseded by live spans that can't be matched to them; requests the daemon missed may not count\n",
            text.Error);
        Assert.Equal(2, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Equal(2, document.RootElement.GetProperty("sources")[0].GetProperty("supersededUnmatched").GetInt64());
        Assert.Equal(
            """{"source":"copilot","log":"s-a","code":"supersededUnmatched","event":"unknown","field":null,"version":null,"count":2,"line":null,"byteOffset":null}""",
            JsonSerializer.Serialize(Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray())));
    }

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
```

Edit 1 of 5 in `tests/CopilotLedger.Tests/Import/LedgerImporterTests.cs`, replace:

```csharp
    private const string S4 = "44444444-4444-4444-8444-444444444444";
    private const string Thread = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
```

with:

```csharp
    private const string S4 = "44444444-4444-4444-8444-444444444444";
    private const string Thread = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b";
    private const string Fork = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2c";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
```

Edit 2 of 5 in `tests/CopilotLedger.Tests/Import/LedgerImporterTests.cs`, replace:

```csharp
    {
        using TempDirectory temp = new();
        string root = Path.Combine(temp.Path, "webapp");
        string other = Path.Combine(temp.Path, "other");
        new CopilotSessionLogs("s-root", context: CopilotSessionLogs.Context(root, root, "main", "contoso/webapp"))
```

with:

```csharp
    {
        using TempDirectory temp = new();
        string root = GitFixture.Create(temp.Path);
        string nested = GitFixture.Create(Path.Combine(root, "vendor"));
        string other = Path.Combine(temp.Path, "other");
        new CopilotSessionLogs("s-root", context: CopilotSessionLogs.Context(root, root, "main", "contoso/webapp"))
```

Edit 3 of 5 in `tests/CopilotLedger.Tests/Import/LedgerImporterTests.cs`, replace:

```csharp
        new CopilotSessionLogs("s-cwd", context: CopilotSessionLogs.Context(Path.Combine(root, "src")))
            .Usage("u2", S2)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-other", context: CopilotSessionLogs.Context(other, other, "main", "contoso/other"))
```

with:

```csharp
        new CopilotSessionLogs("s-cwd", context: CopilotSessionLogs.Context(Path.Combine(root, "src")))
            .Usage("u2", S2)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-nested", context: CopilotSessionLogs.Context(Path.Combine(nested, "src")))
            .Usage("u6", "66666666-6666-4666-8666-666666666666")
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-gone", context: CopilotSessionLogs.Context(Path.Combine(root, "gone")))
            .Usage("u7", "77777777-7777-4777-8777-777777777777")
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-other", context: CopilotSessionLogs.Context(other, other, "main", "contoso/other"))
```

Edit 4 of 5 in `tests/CopilotLedger.Tests/Import/LedgerImporterTests.cs`, replace:

```csharp
            .Usage("u5", "55555555-5555-4555-8555-555555555555")
            .Write(CopilotHome(temp));

        ImportSourceReport report = Copilot(await RunAsync(temp, request => request with { RepositoryRoot = root }));

        Assert.Equal((5, 2, 3), (report.RecordsParsed, report.RecordsOutOfScope, report.Recorded));
        Assert.Equal(
            ["null", "contoso/webapp", "contoso/webapp"],
            await LedgerSql.RowsAsync(Database(temp), "SELECT COALESCE(repo_slug, 'null') FROM spans ORDER BY id;"));
    }

    [Fact]
```

with:

```csharp
            .Usage("u5", "55555555-5555-4555-8555-555555555555")
            .Write(CopilotHome(temp));
        new CodexRollouts(Thread, cwd: Path.Combine(root, "src"))
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1")
            .Write(CodexHome(temp));
        new CodexRollouts(Fork, cwd: Path.Combine(nested, "src"))
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_2", "turn-1")
            .Write(CodexHome(temp));

        ImportReport report = await RunAsync(temp, request => request with { RepositoryRoot = root });

        ImportSourceReport copilot = Copilot(report);
        ImportSourceReport codex = Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Codex);
        Assert.Equal((7, 4, 3), (copilot.RecordsParsed, copilot.RecordsOutOfScope, copilot.Recorded));
        Assert.Equal((2, 1, 1), (codex.RecordsParsed, codex.RecordsOutOfScope, codex.Recorded));
        Assert.Equal(
            ["null", "contoso/webapp", "contoso/webapp"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT COALESCE(repo_slug, 'null') FROM spans WHERE ingest_source = 'import:copilot' ORDER BY id;"));
    }

    [Fact]
```

Edit 5 of 5 in `tests/CopilotLedger.Tests/Import/LedgerImporterTests.cs`, replace:

```csharp
    }

    private static string CopilotHome(TempDirectory temp) => Path.Combine(temp.Path, "copilot");

    private static string CodexHome(TempDirectory temp) => Path.Combine(temp.Path, "codex");
```

with:

```csharp
    }

    // A tool still running when its request was imported gets its link from the run that reads its completion; a dry run
    // counts it the same way. Another session's copy of the request adds no link to the request's row.
    [Fact]
    public async Task IR18_AToolThatEndsAfterItsRequestWasImportedGetsItsLink()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs("s-a").Usage("u1", S1, numToolCalls: 1).ToolStart("toolu-1", "bash");
        log.Write(CopilotHome(temp), active: true);
        ImportSourceReport first = Copilot(await RunAsync(temp, request => request with { IncludeActive = true }));
        log.ToolComplete("toolu-1", false, exitCode: 3).Write(CopilotHome(temp), active: true);

        ImportSourceReport dry = Copilot(await RunAsync(temp, request => request with { IncludeActive = true, DryRun = true }));
        ImportSourceReport second = Copilot(await RunAsync(temp, request => request with { IncludeActive = true }));
        ImportSourceReport third = Copilot(await RunAsync(temp, request => request with { IncludeActive = true }));
        new CopilotSessionLogs("s-copy")
            .Usage("u1", S1, numToolCalls: 1)
            .ToolStart("toolu-1", "bash")
            .ToolComplete("toolu-1", false, exitCode: 3)
            .Write(CopilotHome(temp));
        ImportSourceReport copy = Copilot(await RunAsync(temp, request => request with { IncludeActive = true }));

        Assert.Equal((1, 0), (first.Recorded, first.ToolLinksRecorded));
        Assert.Equal((0, 1, 1), (second.Recorded, second.Duplicates, second.ToolLinksRecorded));
        Assert.Equal(second, dry);
        Assert.Equal((1, 0), (third.SessionsSkippedUnchanged, third.ToolLinksRecorded));
        Assert.Equal((1, 0), (copy.Duplicates, copy.ToolLinksRecorded));
        Assert.True(second.Complete);
        Assert.Equal(
            ["1|bash|3|2"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT owner_span_row_id, tool_name, tool_exit_code, status_code FROM span_links;"));
    }

    // Imported rows that a live span superseded without sharing their service request ID limit coverage on every run whose
    // scope holds them, and a dry run reports them the same way. Rows superseded by their own call's live span don't.
    [Fact]
    public async Task IR19_RowsSupersededWithoutAMatchAreReported()
    {
        using TempDirectory temp = new();
        string root = Path.Combine(temp.Path, "webapp");
        new CopilotSessionLogs("s-a", context: CopilotSessionLogs.Context(root, root, "main", "contoso/webapp"))
            .Usage("u1", S1)
            .Usage("u2", S2)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-b").Usage("u3", S3).Write(CopilotHome(temp));
        await RunAsync(temp);
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("s-a", null), ImportEntries.Live("s-b", S3)]);

        ImportReport report = await RunAsync(temp);
        ImportReport dry = await RunAsync(temp, request => request with { DryRun = true });
        ImportReport repo = await RunAsync(temp, request => request with { RepositoryRoot = root });
        ImportReport otherRepo = await RunAsync(
            temp,
            request => request with { RepositoryRoot = Path.Combine(temp.Path, "other") });
        ImportReport since = await RunAsync(
            temp,
            request => request with { Since = new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero) });

        ImportSourceReport copilot = Copilot(report);
        Assert.Equal((2, 2), (copilot.SessionsSkippedUnchanged, copilot.SupersededUnmatched));
        Assert.False(copilot.Complete);
        Assert.Equal(
            new ImportDiagnostic
            {
                Source = ImportSourceKind.Copilot,
                LogId = "s-a",
                Code = ImportDiagnosticCode.SupersededUnmatched,
                Count = 2,
            },
            Assert.Single(report.Diagnostics));
        Assert.Equal(report.Sources, dry.Sources);
        Assert.Equal(report.Diagnostics, dry.Diagnostics);
        Assert.Equal(2, Copilot(repo).SupersededUnmatched);
        Assert.Equal((0, true), (Copilot(otherRepo).SupersededUnmatched, Copilot(otherRepo).Complete));
        Assert.Equal((0, true), (Copilot(since).SupersededUnmatched, Copilot(since).Complete));
        Assert.Equal(
            ["s-a|superseded", "s-a|superseded", "s-b|superseded"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT conversation_id, accounting_state FROM spans WHERE ingest_source = 'import:copilot' ORDER BY id;"));
    }

    private static string CopilotHome(TempDirectory temp) => Path.Combine(temp.Path, "copilot");

    private static string CodexHome(TempDirectory temp) => Path.Combine(temp.Path, "codex");
```

### Tests

- **IR5** (changed): `RepositoryRoot` is a real repository with another repository nested under `vendor`. Of 7 Copilot records, 4 are out of scope: a working directory in the nested repository, one that no longer exists, another repository, and a resume into another repository; 3 are recorded. Of 2 Codex records, the one whose working directory is in the nested repository is out of scope.
- **IR18**: a tool still running when its request was imported (`IncludeActive`) gets its link once its completion is appended: the next run reports 1 duplicate and 1 tool link, and a dry run before it reports the same; the run after skips the log; another session's copy of the request and tool call adds no link; `span_links` holds exactly `1|bash|3|2`.
- **IR19**: after a live span without a service request ID supersedes session `s-a`'s 2 imported rows, and a live span with `s-b`'s service request ID supersedes `s-b`'s row, a rerun reports `SupersededUnmatched` 2 and one diagnostic for `s-a`, with incomplete coverage; a dry run reports the same; `RepositoryRoot` set to the recorded root still reports 2; another root, or a `Since` after the rows started, reports 0 and complete coverage; all three rows stay superseded.
- **IC3**, **IC4**, and **IC6** (changed): the new row and field, 0. **IC14**: `Superseded, unmatched              2`, exit code 2, the exact warning, JSON `supersededUnmatched` 2, and the diagnostic `{"source":"copilot","log":"s-a","code":"supersededUnmatched","event":"unknown","field":null,"version":null,"count":2,"line":null,"byteOffset":null}`.
- **GS2**: the guide's two import samples show the new row with 0.

### Acceptance criteria

- Tool calls that end after an import get links, never twice and never from a copy (IR18); unmatched supersession is reported, never reversed (IR19, IC14); `--repo` excludes nested repositories and unresolvable working directories (IR5).
- `dotnet test CopilotLedger.slnx` passes **1263** tests (1260 + 3).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 16 specification: Daemon Import Rules and Failure-Log Privacy

Review findings 1 and 13.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Daemon/DaemonFiles.cs` | `DaemonState.ImportRules` (0 when `daemon.json` lacks it) and `CurrentImportRules` = 1 |
| `src/CopilotLedger.Cli/Daemon/DaemonHost.cs` | Writes `importRules` 1 |
| `src/CopilotLedger.Cli/Import/ImportCommands.cs` | The import-rules check replaces the daemon-version warning |
| `src/CopilotLedger.Cli/Daemon/IngestFailureLog.cs` | Only a `SqliteException` keeps its message |
| `docs/cli.md` | Earlier daemons, exit code 1, and Troubleshooting |
| `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs` | The stand-in daemon's `daemon.json` has `importRules` 1 |
| `tests/CopilotLedger.Tests/Cli/DaemonCommandTests.cs` | DM1 changed |
| `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs` | IC9 and IC10 rewritten |
| `tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs` | IP1 changed; new: IP4 |

### Rules

1. **Import rules (finding 1).** This build's daemon writes `"importRules": 1` to `daemon.json`. Before the ledger is opened, while the daemon lock is held and `daemon.json` has no `importRules` of at least 1 (an earlier build, or a state that can't be read), `import` writes `Import stopped: the running daemon doesn't follow the import rules, so a request it records later could be charged twice. Restart it with <command> daemon stop and <command> daemon start, then run the import again.` to standard error and exits with 1, creating nothing. A dry run writes `Warning: the running daemon doesn't follow the import rules, so a request it records after an import could be charged twice. Restart it with <command> daemon stop and <command> daemon start before you import.` and continues. `<command>` is `gh ledger` for the extension, else `gh-ledger`. Both builds call themselves 0.4.1, so the version can't tell them apart, and the version warning is gone: a daemon of another version that has the rules is fine.
2. **Failure-log privacy (finding 13), D21 narrowed.** `IngestFailureLog` keeps the message only of a `SqliteException`: SQLite's messages name tables, columns, and constraints but never a bound value, and the ledger binds every value. Every other exception, `IOException` and `UnauthorizedAccessException` included, is written as its type only, because an I/O message can name a path from the request, such as its working directory. SQLite reports a full disk as `database or disk is full`, so that cause still shows.

### Code (copy exactly)

Edit 1 of 2 in `docs/cli.md`, replace:

````markdown
| 0 | Every source's coverage is complete. |
| 2 | The import finished, but some coverage is incomplete; the warnings say why. Everything that could be imported was. |
| 1 | The options are invalid, or an error stopped the import. |

Skipped active, unchanged, and out-of-scope logs don't make coverage incomplete. A folder or file of a session store that exists but can't be listed does, with a warning such as `Copilot session logs: 1 folder or file couldn't be listed`; a store that doesn't exist only has no logs. So do `Superseded, unmatched` requests: those that started in the `--since` period and, with `--repo`, recorded that repository's root, or its `owner/repo` slug when they recorded no root. When the only gap is sessions from before Copilot 1.0.103, the report's last line suggests `--since`, which leaves them out. With `--repo`, logs of other repositories are read again on every run, because they may hold requests of this one.

Imported requests count in every report and MCP tool, as live ones do, with the branch their log recorded. Tool calls are imported only from the last 7 days, as long as the ledger keeps tool calls; an older request keeps only the number of tool calls it made. A tool call that ended after its request was imported, such as in a session imported with `--include-active`, is added when a later import reads the session again. Imported requests have no agent-invocation span, so `status --cache --turns` shows their `Growth` as `n/a`, and the input-growth ranking of `--bloat` leaves them out. `import` warns when the daemon runs another gh-ledger version: restart the daemon, so that the live spans it records supersede imported rows by this version's rules.

### What import reads and keeps
````

with:

````markdown
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
````

Edit 2 of 2 in `docs/cli.md`, replace:

```markdown
## Troubleshooting

- **Nothing is recorded.** Run `gh ledger status`, address its warnings, and restart Copilot.
- **`daemon.log` says `Warning: could not record spans`.** The ledger couldn't be written, for example because the disk is full or the database is damaged, and Copilot is told to retry. The warning repeats at most once a minute; fix the cause shown after it. Only database and file errors show their message; any other error shows only its type, because its message could quote the request.
- **`gh ledger import` exits with code 2.** Some coverage is incomplete, and the warnings say why; everything else was imported. A session from before Copilot 1.0.103 is never imported, so leave it out with `--since`. A malformed line or an unsupported usage record is skipped on every run, and the rest of its log is imported.
- **`gh ledger import` defers requests (`Ambiguous-overlap`).** Their session has live spans that can't be matched to them, such as spans an earlier gh-ledger recorded, so import leaves them out rather than risk charging a request twice. The live spans still count.
```

with:

```markdown
## Troubleshooting

- **Nothing is recorded.** Run `gh ledger status`, address its warnings, and restart Copilot.
- **`daemon.log` says `Warning: could not record spans`.** The ledger couldn't be written, for example because the disk is full or the database is damaged, and Copilot is told to retry. The warning repeats at most once a minute; fix the cause shown after it. Only database errors show their message, which never quotes a request's values; any other error shows only its type, because its message could quote the request or a path from it.
- **`gh ledger import` exits with code 2.** Some coverage is incomplete, and the warnings say why; everything else was imported. A session from before Copilot 1.0.103 is never imported, so leave it out with `--since`. A malformed line or an unsupported usage record is skipped on every run, and the rest of its log is imported.
- **`gh ledger import` defers requests (`Ambiguous-overlap`).** Their session has live spans that can't be matched to them, such as spans an earlier gh-ledger recorded, so import leaves them out rather than risk charging a request twice. The live spans still count.
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Daemon/DaemonFiles.cs`, replace:

```csharp
internal sealed record DaemonState
{
    public required int SchemaVersion { get; init; }
    public required int ProcessId { get; init; }
```

with:

```csharp
internal sealed record DaemonState
{
    // The import rules this build's daemon applies to the live spans it records (UsageAuthority). gh ledger import needs
    // them: a daemon whose daemon.json lacks importRules (an earlier build) could record a request twice.
    public const int CurrentImportRules = 1;

    public required int SchemaVersion { get; init; }
    public required int ProcessId { get; init; }
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Daemon/DaemonFiles.cs`, replace:

```csharp
    public required string Version { get; init; }
    public required string DatabasePath { get; init; }
}

internal static class DaemonFiles
```

with:

```csharp
    public required string Version { get; init; }
    public required string DatabasePath { get; init; }
    public int ImportRules { get; init; }            // 0 when daemon.json has no importRules
}

internal static class DaemonFiles
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Daemon/DaemonHost.cs`, replace:

```csharp
                Version = GhLedgerCli.Version,
                DatabasePath = context.DatabasePath,
            };
            await DaemonFiles.WriteStateAsync(context, state, cancellationToken);
```

with:

```csharp
                Version = GhLedgerCli.Version,
                DatabasePath = context.DatabasePath,
                ImportRules = DaemonState.CurrentImportRules,
            };
            await DaemonFiles.WriteStateAsync(context, state, cancellationToken);
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Daemon/IngestFailureLog.cs`, replace:

```csharp
namespace CopilotLedger.Cli.Daemon;

// Writes "Warning: could not record spans: <type>: <message>" for the first ingestion failure, then at most once a
// minute, so a ledger that stays unavailable can't flood daemon.log. Only SQLite and file-system exceptions keep their
// message; any other is written as "Warning: could not record spans: <type>", because its message can quote the request
// (a JsonException's does). After the clock steps back, the next failure is written at once. A line that can't be
// written is dropped.
public sealed class IngestFailureLog(Action<string> write, TimeProvider timeProvider)
{
```

with:

```csharp
namespace CopilotLedger.Cli.Daemon;

// Writes "Warning: could not record spans: <type>: <message>" for the first ingestion failure, then at most once a
// minute, so a ledger that stays unavailable can't flood daemon.log. Only a SqliteException keeps its message: SQLite's
// messages name tables, columns, and constraints but never a bound value, and the ledger binds every value. Any other
// exception is written as "Warning: could not record spans: <type>", because its message can quote the request (a
// JsonException's does) or name a path from it (an IOException's can, such as the span's working directory). After the
// clock steps back, the next failure is written at once. A line that can't be written is dropped.
public sealed class IngestFailureLog(Action<string> write, TimeProvider timeProvider)
{
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Daemon/IngestFailureLog.cs`, replace:

```csharp
        try
        {
            write(exception is SqliteException or IOException or UnauthorizedAccessException
                ? $"Warning: could not record spans: {exception.GetType().Name}: {exception.Message}"
                : $"Warning: could not record spans: {exception.GetType().Name}");
```

with:

```csharp
        try
        {
            write(exception is SqliteException
                ? $"Warning: could not record spans: {exception.GetType().Name}: {exception.Message}"
                : $"Warning: could not record spans: {exception.GetType().Name}");
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
using System.Text.Json;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Cli.Reporting;
```

with:

```csharp
using System.Text.Json;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Install;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Cli.Reporting;
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Import/ImportCommands.cs`, replace:

```csharp
        }

        // A daemon of another version records live spans by its own rules, so a request it records later might not
        // supersede an imported one.
        if (DaemonFiles.IsHeld(context) &&
            DaemonFiles.TryReadState(context)?.Version is string version &&
            version != GhLedgerCli.Version)
        {
            context.Error.WriteLine(
                $"Warning: the daemon runs gh-ledger {version}; restart it so live spans follow this version's import rules.");
        }

        ImportRequest request = new()
```

with:

```csharp
        }

        // A daemon without the import rules (DaemonState.ImportRules) records live spans without superseding imported rows,
        // so a request it records after the import could be charged twice. An import stops before it opens the ledger; a
        // dry run, which writes nothing, warns.
        if (DaemonFiles.IsHeld(context) &&
            (DaemonFiles.TryReadState(context)?.ImportRules ?? 0) < DaemonState.CurrentImportRules)
        {
            string command = GhExtension.CommandName(context.Platform);
            string restart = $"Restart it with {command} daemon stop and {command} daemon start";
            if (!dryRun)
            {
                context.Error.WriteLine(
                    $"Import stopped: the running daemon doesn't follow the import rules, so a request it records later could be charged twice. {restart}, then run the import again.");
                return 1;
            }

            context.Error.WriteLine(
                $"Warning: the running daemon doesn't follow the import rules, so a request it records after an import could be charged twice. {restart} before you import.");
        }

        ImportRequest request = new()
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/DaemonCommandTests.cs`, replace:

```csharp
            Path.Combine(temp.Path, "data", "ledger.db"),
            root.GetProperty("databasePath").GetString());

        CliRun start = await harness.RunAsync("daemon", "start");
```

with:

```csharp
            Path.Combine(temp.Path, "data", "ledger.db"),
            root.GetProperty("databasePath").GetString());
        Assert.Equal(1, root.GetProperty("importRules").GetInt32());

        CliRun start = await harness.RunAsync("daemon", "start");
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
    }

    [Fact]
    public void IC9_TheIngestFailureLogWritesOnlyTheTypeOfOtherExceptions()
    {
        List<string> lines = [];
        ManualTimeProvider clock = new(Now);
        IngestFailureLog log = new(lines.Add, clock);

        log.Report(new JsonException(CopilotSessionLogs.Canary));
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Report(new SqliteException("SQLite Error 5: 'database is locked'.", 5));
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Report(new IOException("There is not enough space on the disk."));
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Report(new ArgumentException(CopilotSessionLogs.Canary));

        Assert.Equal(
            [
                "Warning: could not record spans: JsonException",
                "Warning: could not record spans: SqliteException: SQLite Error 5: 'database is locked'.",
                "Warning: could not record spans: IOException: There is not enough space on the disk.",
                "Warning: could not record spans: ArgumentException",
            ],
```

with:

```csharp
    }

    // Only SQLite's own messages are kept, and they never quote a bound value; every other exception's message is dropped.
    [Fact]
    public async Task IC9_TheIngestFailureLogKeepsOnlySqliteMessages()
    {
        List<string> lines = [];
        ManualTimeProvider clock = new(Now);
        IngestFailureLog log = new(lines.Add, clock);
        SqliteException constraint;
        await using (SqliteConnection connection = new("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE t (v TEXT UNIQUE); INSERT INTO t (v) VALUES (@v); INSERT INTO t (v) VALUES (@v);";
            command.Parameters.AddWithValue("@v", CopilotSessionLogs.Canary);
            constraint = await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
        }

        foreach (Exception exception in new Exception[]
                 {
                     constraint,
                     new IOException(CopilotSessionLogs.Canary),
                     new UnauthorizedAccessException(CopilotSessionLogs.Canary),
                     new JsonException(CopilotSessionLogs.Canary),
                     new ArgumentException(CopilotSessionLogs.Canary),
                 })
        {
            log.Report(exception);
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Equal(
            [
                "Warning: could not record spans: SqliteException: SQLite Error 19: 'UNIQUE constraint failed: t.v'.",
                "Warning: could not record spans: IOException",
                "Warning: could not record spans: UnauthorizedAccessException",
                "Warning: could not record spans: JsonException",
                "Warning: could not record spans: ArgumentException",
            ],
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
    }

    [Fact]
    public async Task IC10_ADaemonOfAnotherVersionIsReported()
    {
        using TempDirectory temp = new();
```

with:

```csharp
    }

    [Fact]
    public async Task IC10_ADaemonWithoutTheImportRulesStopsAnImport()
    {
        using TempDirectory temp = new();
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
        string data = harness.Context.DataDirectory;
        Directory.CreateDirectory(data);
        await File.WriteAllTextAsync(
            Path.Combine(data, "daemon.json"),
            """{"schemaVersion":1,"processId":4242,"startedAt":"2026-11-23T11:00:00+00:00","endpoint":"http://localhost:4318","version":"0.4.0","databasePath":"ledger.db"}""");
        CliRun run;
        await using (FileStream held = new(Path.Combine(data, "daemon.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            run = await harness.RunAsync("import", "--all");
        }

        CliRun released = await harness.RunAsync("import", "--all");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            "Warning: the daemon runs gh-ledger 0.4.0; restart it so live spans follow this version's import rules.\n",
            run.Error);
        Assert.Equal("", released.Error);
    }

    [Fact]
```

with:

```csharp
        string data = harness.Context.DataDirectory;
        Directory.CreateDirectory(data);
        new CopilotSessionLogs("s-a").Usage("u1", S1).Write(harness.Context.CopilotHome);
        const string State =
            """{"schemaVersion":1,"processId":4242,"startedAt":"2026-11-23T11:00:00+00:00","endpoint":"http://localhost:4318","version":"0.4.1","databasePath":"ledger.db"}""";
        await File.WriteAllTextAsync(Path.Combine(data, "daemon.json"), State);
        CliRun refused;
        CliRun dry;
        CliRun capable;
        bool createdBeforeCapable;
        await using (FileStream held = new(Path.Combine(data, "daemon.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            refused = await harness.RunAsync("import", "--all");
            dry = await harness.RunAsync("import", "--all", "--dry-run");
            createdBeforeCapable = File.Exists(harness.Context.DatabasePath);
            await File.WriteAllTextAsync(Path.Combine(data, "daemon.json"), State.Replace("}", ",\"importRules\":1}"));
            capable = await harness.RunAsync("import", "--all");
        }

        CliRun released = await harness.RunAsync("import", "--all");

        Assert.Equal((1, ""), (refused.ExitCode, refused.Out));
        Assert.False(createdBeforeCapable);
        Assert.Equal(
            "Import stopped: the running daemon doesn't follow the import rules, so a request it records later could be charged twice. Restart it with gh-ledger daemon stop and gh-ledger daemon start, then run the import again.\n",
            refused.Error);
        Assert.Equal(0, dry.ExitCode);
        Assert.Equal(
            "Warning: the running daemon doesn't follow the import rules, so a request it records after an import could be charged twice. Restart it with gh-ledger daemon stop and gh-ledger daemon start before you import.\n",
            dry.Error);
        Assert.EndsWith("\nImporting would record 1 request: 0.15 AI credits ($0.00), API equivalent $0.00.\nDry run: nothing was written.\n", dry.Out);
        Assert.Equal((0, ""), (capable.ExitCode, capable.Error));
        Assert.EndsWith("\nRecorded 1 request: 0.15 AI credits ($0.00), API equivalent $0.00.\n", capable.Out);
        Assert.Equal((0, ""), (released.ExitCode, released.Error));
        Assert.EndsWith("\nNothing new to record.\n", released.Out);
    }

    [Fact]
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs`, replace:

```csharp
using System.Text;
using System.Text.Json.Nodes;
```

with:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs`, replace:

```csharp
        }

        Assert.Contains(stored, file => file.Name == "ledger.db");
        string output = string.Concat(
            claude.Out, claude.Error, dryRun.Out, dryRun.Error, text.Out, text.Error, json.Out, json.Error);
```

with:

```csharp
        }

        Assert.Contains(stored, file => file.Name == "ledger.db");
        string daemonLog = Path.Combine(harness.Context.DataDirectory, "daemon.log");
        if (File.Exists(daemonLog))
        {
            stored.Add(("daemon.log", await File.ReadAllBytesAsync(daemonLog)));
        }

        string output = string.Concat(
            claude.Out, claude.Error, dryRun.Out, dryRun.Error, text.Out, text.Error, json.Out, json.Error);
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs`, replace:

```csharp
            }

            Assert.Equal(documented[table], filled);
        }
    }
```

with:

```csharp
            }

            Assert.Equal(documented[table], filled);
        }
    }

    // While the ledger can't record a canary-bearing live span, after an import, the daemon's log names the failure and keeps
    // nothing of the span. The in-process daemon writes to DaemonOutput what it writes to daemon.log when it's started.
    [Fact]
    public async Task IP4_TheDaemonLogKeepsNoCanaryWhenItCantRecordASpan()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        WriteFixtures(temp, harness);
        CliRun import = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex");
        await harness.StartInProcessDaemonAsync();
        await LedgerSql.ExecuteAsync(
            harness.Context.DatabasePath,
            "CREATE TRIGGER ip4_reject BEFORE INSERT ON spans BEGIN SELECT RAISE(ABORT, 'ip4 rejects spans'); END;");
        string trace = OtlpFixtures.CopilotChatTraceJson
            .Replace("0f8fad5b-d9cb-469f-a165-70867728950e", Canary("PROMPT"), StringComparison.Ordinal)
            .Replace("\"stringValue\": \"stop\"", $"\"stringValue\": \"{Canary("RESPONSE")}\"", StringComparison.Ordinal);
        HttpStatusCode status;
        using (HttpClient client = harness.CreateDaemonClient())
        using (ByteArrayContent content = new(Encoding.UTF8.GetBytes(trace)))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using HttpResponseMessage response = await client.PostAsync("/v1/traces", content);
            status = response.StatusCode;
        }

        CliRun stop = await harness.RunAsync("daemon", "stop");
        CliRun daemon = await harness.InProcessDaemon!;
        string daemonLog = Path.Combine(harness.Context.DataDirectory, "daemon.log");
        string log = harness.DaemonOutput + (File.Exists(daemonLog) ? await File.ReadAllTextAsync(daemonLog) : "");

        Assert.Equal(2, import.ExitCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal((0, 0), (stop.ExitCode, daemon.ExitCode));
        Assert.Contains(" Warning: could not record spans: SqliteException: ", log);
        Assert.Contains("ip4 rejects spans", log);
        foreach (string canary in Canaries)
        {
            Assert.DoesNotContain(canary, log, StringComparison.Ordinal);
        }
    }
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
                version = GhLedgerCli.Version,
                databasePath = Harness.Context.DatabasePath,
            }));
        _ = FileRetry.ReadAllText(Path.Combine(data, "daemon.json"));
```

with:

```csharp
                version = GhLedgerCli.Version,
                databasePath = Harness.Context.DatabasePath,
                importRules = 1,
            }));
        _ = FileRetry.ReadAllText(Path.Combine(data, "daemon.json"));
```

### Tests

- **DM1** (changed): `daemon.json` has `importRules` 1.
- **IC9** (rewritten): a real `SqliteException`, a UNIQUE constraint failure with the canary bound twice, keeps SQLite's message `SQLite Error 19: 'UNIQUE constraint failed: t.v'.`; an `IOException`, `UnauthorizedAccessException`, `JsonException`, and `ArgumentException` carrying the canary write only their type.
- **IC10** (rewritten): while the daemon lock is held and `daemon.json` has no `importRules`, `import --all` exits with 1, prints the exact message and nothing on standard output, and creates no ledger; `--dry-run` prints the exact warning and reports `Importing would record 1 request`; with `"importRules":1`, the import records it with an empty standard error; after the lock is released, standard error stays empty.
- **IP1** (changed): also scans `daemon.log` when it exists. **IP4**: after an import of the canary fixtures, the in-process daemon can't record a trace with canaries in its conversation ID and finish reasons, because a trigger rejects inserts into `spans`: the response is 503, the daemon's log has ` Warning: could not record spans: SqliteException: ` with the trigger's message `ip4 rejects spans`, and no canary.
- **GS2**: the guide's import samples still exit with 0 and an empty standard error, because the stand-in daemon has the import rules.

### Acceptance criteria

- An import can't run beside a daemon without the import rules (IC10, DM1); `daemon.log` never receives a request's content or paths (IC9, IP4).
- `dotnet test CopilotLedger.slnx` passes **1264** tests (1263 + 1).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 17 specification: Escaped Lone Surrogates Never Stop an Import

Review finding R1. Tasks 17 and 19 fix what the architect's review of Tasks 1–16 (at `2f086d6`) found, and Task 18 what the coordinator's review of Tasks 13–16 found; each code fix comes with tests that fail without it. They were built like Tasks 1–16: in a scratch clone of this worktree at `2f086d6`, with each task's blocks generated from its commit there, then applied by script, in order, to a copy of this worktree at `2f086d6` (Windows line endings), with a build and the whole suite after each task: 1267, 1269, and 1269 tests, and, line endings aside, the same files. The sprint now ends with Task 19.

The review reproduced the problem with the CLI built at `2f086d6` (SDK 10.0.115). A Copilot session log with the line `{"\uDC00":1,"type":"user.message"}` made `gh ledger import --all`, and `--dry-run`, exit with 1 and print `Cannot read invalid UTF-16 JSON text as string. Invalid surrogate value: '0xDC00'.` to standard error, and no later log was read. So did a type such as `"\uDC00\uDC00\uDC00"`, a name with one inside `data.usage` of a usage record, and the same lines in a Codex rollout. `Utf8JsonReader.ValueTextEquals` unescapes an escaped name or string before it compares it, and a lone surrogate makes it throw `InvalidOperationException` (from `ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16`), not `JsonException`; `JsonFields.ReadType` and `JsonFields.Read` caught only `JsonException`. That broke #63's criterion that malformed lines and unknown types are counted without echoing exception messages, and the contract of `ISessionLogSource.ReadAsync`, which throws only `ArgumentException` and `OperationCanceledException`. Copilot and Codex don't write such names or types, but a damaged or edited log can.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Import/JsonFields.cs` | `TextEquals`, through which every name and type is compared: a token that `ValueTextEquals` can't unescape equals nothing |
| `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs` | New: CS16 |
| `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs` | New: CX17 |
| `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs` | New: IC15 |

### Rules

1. `JsonFields.TextEquals(ref Utf8JsonReader reader, string text)` returns `reader.ValueTextEquals(text)`, or `false` when that throws `InvalidOperationException`. That's exact, not a fallback: a token with an escaped lone surrogate doesn't unescape to valid UTF-16, so it never equals one of the parser's names or types, which are all ASCII.
2. `ReadType` compares each top-level name with `type`, and the type with each known type, through `TextEquals`, and `ReadObject` compares each name with the allowlisted names through it. So a name that holds one is skipped unread, like any other unknown name, and a type that holds one is an unknown type: a skipped event, without a diagnostic.
3. Nothing else changes. `ReadValue` already reads a string it can't decode as no value, and a line that isn't JSON is still malformed.

### Code (copy exactly)

Edit 1 of 4 in `src/CopilotLedger.Core/Import/JsonFields.cs`, replace:

```csharp
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isType = reader.ValueTextEquals("type"u8);
                reader.Read();
                if (!isType)
```

with:

```csharp
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isType = TextEquals(ref reader, "type");
                reader.Read();
                if (!isType)
```

Edit 2 of 4 in `src/CopilotLedger.Core/Import/JsonFields.cs`, replace:

```csharp
                for (int index = 0; index < types.Count; index++)
                {
                    if (reader.ValueTextEquals(types[index]))
                    {
                        found = index;
```

with:

```csharp
                for (int index = 0; index < types.Count; index++)
                {
                    if (TextEquals(ref reader, types[index]))
                    {
                        found = index;
```

Edit 3 of 4 in `src/CopilotLedger.Core/Import/JsonFields.cs`, replace:

```csharp
            foreach (PathTree.Node candidate in node.Children)
            {
                if (reader.ValueTextEquals(candidate.Name))
                {
                    child = candidate;
```

with:

```csharp
            foreach (PathTree.Node candidate in node.Children)
            {
                if (TextEquals(ref reader, candidate.Name))
                {
                    child = candidate;
```

Edit 4 of 4 in `src/CopilotLedger.Core/Import/JsonFields.cs`, replace:

```csharp
    }

    private readonly record struct Value(JsonTokenType Token, string? Text, long? Integer);
}
```

with:

```csharp
    }

    // Whether the property name or string at the reader is text. ValueTextEquals throws InvalidOperationException for an
    // escaped lone surrogate, which it can't unescape; a name or string that holds one is never one of the parser's ASCII
    // names or types.
    private static bool TextEquals(ref Utf8JsonReader reader, string text)
    {
        try
        {
            return reader.ValueTextEquals(text);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private readonly record struct Value(JsonTokenType Token, string? Text, long? Integer);
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
    }

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
```

with:

```csharp
    }

    // A line whose name or type holds an escaped lone surrogate is skipped like any other unknown event: the import completes,
    // and no exception message reaches the output.
    [Fact]
    public async Task IC15_AnEscapedLoneSurrogateNeverStopsAnImport()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        new CopilotSessionLogs("s-a")
            .Raw("{\"\\uDC00\\uDC00\":1,\"type\":\"user.message\"}")
            .Raw("{\"type\":\"\\uDC00\\uDC00\\uDC00\"}")
            .Usage("u1", S1)
            .Write(harness.Context.CopilotHome);
        new CodexRollouts(Thread)
            .Raw("{\"\\uDC00\\uDC00\":1,\"type\":\"event_msg\",\"payload\":{}}")
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1")
            .Write(harness.Context.CodexHome);

        CliRun dryRun = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex", "--dry-run");
        CliRun run = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex");

        Assert.Equal((0, "", 0, ""), (dryRun.ExitCode, dryRun.Error, run.ExitCode, run.Error));
        Assert.Equal(
            Normalize("""
            Import from local session logs (all time, dry run)

                                       Copilot     Codex
            Sessions scanned                 1         1
            Skipped, unchanged               0         0
            Skipped, active                  0         0
            Skipped, out of scope            0         0
            Unsupported sessions             0         0
            Records parsed                   1         1
            Out of scope                     0         0
            Recorded                         1         1
            Duplicate                        0         0
            Overlap-skipped                  0         0
            Ambiguous-overlap                0         0
            Superseded, unmatched            0         0
            Malformed lines                  0         0
            Unsupported usage shapes         0         0
            Cost out of range                0         0
            Unpriced                         0         1
            Coverage                  complete  complete

            Importing would record 2 requests: 0.15 AI credits ($0.00), API equivalent $0.00.
            Dry run: nothing was written.

            """),
            dryRun.Out);
        Assert.Equal(
            Normalize("""
            Import from local session logs (all time)

                                       Copilot     Codex
            Sessions scanned                 1         1
            Skipped, unchanged               0         0
            Skipped, active                  0         0
            Skipped, out of scope            0         0
            Unsupported sessions             0         0
            Records parsed                   1         1
            Out of scope                     0         0
            Recorded                         1         1
            Duplicate                        0         0
            Overlap-skipped                  0         0
            Ambiguous-overlap                0         0
            Superseded, unmatched            0         0
            Malformed lines                  0         0
            Unsupported usage shapes         0         0
            Cost out of range                0         0
            Unpriced                         0         1
            Coverage                  complete  complete

            Recorded 2 requests: 0.15 AI credits ($0.00), API equivalent $0.00.

            """),
            run.Out);
    }

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs`, replace:

```csharp
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);
```

with:

```csharp
    }

    // As CS16: a name or type that holds an escaped lone surrogate is none of the parser's, and the rollout is read on.
    [Fact]
    public async Task CX17_AnEscapedLoneSurrogateInANameOrATypeIsNeverOneOfTheParsers()
    {
        using TempDirectory temp = new();
        string path = new CodexRollouts(Thread)
            .Raw("{\"\\uDC00\\uDC00\":\"" + CopilotSessionLogs.Canary + "\",\"type\":\"event_msg\",\"payload\":{}}")
            .Raw("{\"type\":\"\\uDC00\\uDC00\\uDC00\",\"payload\":{\"message\":\"" + CopilotSessionLogs.Canary + "\"}}")
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1")
            .Write(temp.Path);
        await File.WriteAllTextAsync(
            path,
            (await File.ReadAllTextAsync(path)).Replace(
                "\"usage\":{\"input_tokens\":",
                "\"usage\":{\"\\uDC00\\uDC00\":1,\"input_tokens\":",
                StringComparison.Ordinal));

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(
            (1L, 0L, 0L, 2L),
            (result.RecordsParsed, result.UnsupportedUsageShapes, result.MalformedLines, result.SkippedEvents));
        Assert.Equal("resp_1", Assert.Single(result.Records).RecordKey);
        Assert.Empty(result.Diagnostics);
        AssertNoCanary(result);
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs`, replace:

```csharp
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);
```

with:

```csharp
    }

    // ValueTextEquals can't unescape a lone surrogate, so a name or type that holds one is none of the parser's: its line is
    // skipped, or its property ignored, like any other unknown one, and the log is read on.
    [Fact]
    public async Task CS16_AnEscapedLoneSurrogateInANameOrATypeIsNeverOneOfTheParsers()
    {
        using TempDirectory temp = new();
        string usage = new CopilotSessionLogs(SessionId)
            .Usage("u1", "1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b")
            .Lines[1]
            .Replace("\"usage\":{\"model\":", "\"usage\":{\"\\uDC00\\uDC00\":1,\"model\":", StringComparison.Ordinal);
        new CopilotSessionLogs(SessionId)
            .Raw("{\"\\uDC00\\uDC00\":\"" + CopilotSessionLogs.Canary + "\",\"type\":\"user.message\"}")
            .Raw("{\"type\":\"\\uDC00\\uDC00\\uDC00\",\"data\":{\"content\":\"" + CopilotSessionLogs.Canary + "\"}}")
            .Raw(usage)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(
            (1L, 0L, 0L, 2L),
            (result.RecordsParsed, result.UnsupportedUsageShapes, result.MalformedLines, result.SkippedEvents));
        Assert.Equal("u1", Assert.Single(result.Records).RecordKey);
        Assert.Empty(result.Diagnostics);
        AssertNoCanary(result);
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);
```

### Tests

- **CS16**: a Copilot log whose second line has the name `\uDC00\uDC00` before `type` `user.message`, whose third has the type `\uDC00\uDC00\uDC00`, and whose usage record `u1` has the name `\uDC00\uDC00` first in `data.usage`, is read: 1 record parsed (`u1`), 0 unsupported, 0 malformed, 2 skipped events, no diagnostics, and no canary. Without the fix, `ReadAsync` throws `InvalidOperationException`.
- **CX17**: the same for a Codex rollout: the name before `type` `event_msg`, the type, and the name first in `payload.usage` of the `token_usage_record` `resp_1`.
- **IC15**: `import --all --source copilot --source codex`, as a dry run and then for real, on a Copilot session and a Codex rollout with such lines, exits with 0, writes nothing to standard error, and prints the exact tables: one request parsed and recorded per source, and complete coverage.

Each of these lines alone stopped the import before the fix. The names are 12 bytes because `ValueTextEquals` unescapes only a token at least as long as the text it's compared with: `\uDC00` alone, 6 bytes, never reaches the unescaping against `payload.usage`'s shortest name, `input_tokens`.

### Acceptance criteria

- A name or type with an escaped lone surrogate never stops a read, an import, or a dry run, and no exception message reaches the output (CS16, CX17, IC15).
- `dotnet test CopilotLedger.slnx` passes **1267** tests (1264 + 3).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 18 specification: Session Folders That Can't Be Entered

Review finding R2, from the coordinator's review of Tasks 13–16. Discovery skipped a session folder when `FileInfo.Exists` said that its `events.jsonl` didn't exist, but `Exists` is also false when the file can't be examined: it "returns false if any error occurs while trying to determine if the specified file exists", including when "the caller does not have permission to read the file" ([`FileInfo.Exists`](https://learn.microsoft.com/dotnet/api/system.io.fileinfo.exists)), because it catches every exception (dotnet/runtime `release/10.0`, `FileInfo.cs`). So a session folder that this user can't enter, such as one with mode 000, a root-owned 0700 folder left by `sudo copilot`, or one whose ACL denies this user, passed for a folder without a log: `gh ledger import --all` reported `unreadableStoreEntries` 0 and complete coverage, exited with 0, printed no warning, and never imported the session. The review reproduced it on Windows, with deny ACEs and the CLI built at `2f086d6`. Task 14's handler counted only a folder that could be entered but not listed. Codex isn't affected: its discovery reads `Length`, which throws.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs` | `Discover` reads `events.Length`; only a missing `events.jsonl`, or a session folder deleted meanwhile, means no log |
| `tests/CopilotLedger.Tests/TestSupport/DeniedDirectory.cs` | New: keeps the process out of a folder until it's disposed |
| `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs` | New: CS17 |
| `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs` | New: IC16 |

### Rules

1. For each session folder, `Discover` builds the location from `events.Length`, `LastWriteTimeUtc`, and the `inuse.*.lock` listing, as before, but without the `Exists` test. `Length` throws `FileNotFoundException` when there's no `events.jsonl` (on Unix for `ENOENT` and `ENOTDIR`, which leave the attributes at -1; on Windows for "file not found" and "path not found"), and `UnauthorizedAccessException`, or another `IOException`, when the file can't be examined: on Unix the `stat` error is kept and thrown through `Interop.GetExceptionForIoErrno`, which turns `EACCES` into `UnauthorizedAccessException` (dotnet/runtime `release/10.0`: `FileInfo.cs`, `FileSystemInfo.Unix.cs`, `FileStatus.Unix.cs`, `Interop.IOErrors.cs`, retrieved 2026-11-23); on Windows, `ERROR_ACCESS_DENIED` (verified in the review's scratch build).
2. A `FileNotFoundException`, or a `DirectoryNotFoundException` from listing a folder deleted meanwhile, means the folder has no log, as before. Every other `IOException`, and `UnauthorizedAccessException`, is an unreadable entry: the source's coverage is incomplete, and `import` warns `Copilot session logs: 1 folder or file couldn't be listed; any logs there weren't imported` and exits with 2.
3. This is stricter than listing the folder first, which would miss a folder that can be listed but not searched (Unix mode `r--`). A log that can be examined but not read is still read as `Unreadable`, as before.

### Code (copy exactly)

Edit 1 of 3 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
    public ImportSourceKind Kind => ImportSourceKind.Copilot;

    // Each <home>/session-state/<session ID>/events.jsonl whose directory name is a valid session ID. A session folder, or a
    // session-state that exists but isn't a folder that can be listed, is an unreadable entry.
    public SessionLogDiscovery Discover(string home)
    {
```

with:

```csharp
    public ImportSourceKind Kind => ImportSourceKind.Copilot;

    // Each <home>/session-state/<session ID>/events.jsonl whose directory name is a valid session ID. A session folder that
    // can't be entered or listed, or a session-state that exists but isn't a folder that can be listed, is an unreadable entry;
    // a session folder without events.jsonl has no log.
    public SessionLogDiscovery Discover(string home)
    {
```

Edit 2 of 3 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
                }

                try
                {
                    FileInfo events = new(Path.Combine(directory, "events.jsonl"));
                    if (!events.Exists)
                    {
                        continue;
                    }

                    logs.Add(new SessionLogLocation
                    {
```

with:

```csharp
                }

                // FileInfo.Exists is false for a file that can't be examined, too, so it can't tell a folder without a log from
                // one this user can't enter. Length throws FileNotFoundException only when there's no events.jsonl.
                try
                {
                    FileInfo events = new(Path.Combine(directory, "events.jsonl"));
                    logs.Add(new SessionLogLocation
                    {
```

Edit 3 of 3 in `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`, replace:

```csharp
                        IsActive = Directory.EnumerateFiles(directory, "inuse.*.lock").Any(),
                    });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
```

with:

```csharp
                        IsActive = Directory.EnumerateFiles(directory, "inuse.*.lock").Any(),
                    });
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    // No events.jsonl, or the session folder is gone: no log.
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs`, replace:

```csharp
    }

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
```

with:

```csharp
    }

    // A Copilot session folder that this user can't enter makes coverage incomplete, with a warning, and the other sessions are
    // imported. Before, discovery took it for a folder without a log, and the import reported complete coverage with exit code
    // 0. Root on Unix can enter it, so there both sessions are imported.
    [Fact]
    public async Task IC16_ASessionFolderThatCantBeEnteredMakesCoverageIncomplete()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        new CopilotSessionLogs("s-a").Usage("u1", S1).Write(harness.Context.CopilotHome);
        string folder = Path.GetDirectoryName(new CopilotSessionLogs("s-b").Usage("u2", S2).Write(harness.Context.CopilotHome))!;

        CliRun text;
        CliRun json;
        bool enforced;
        using (DeniedDirectory denied = new(folder))
        {
            enforced = denied.Enforced;
            text = await harness.RunAsync("import", "--all");
            json = await harness.RunAsync("import", "--all", "--json");
        }

        Assert.True(enforced || (!OperatingSystem.IsWindows() && Environment.IsPrivilegedProcess));
        Assert.Equal(
            enforced ? 1L : 2L,
            await LedgerSql.ScalarAsync(harness.Context.DatabasePath, "SELECT COUNT(*) FROM spans;"));
        if (!enforced)
        {
            Assert.Equal((0, "", 0, ""), (text.ExitCode, text.Error, json.ExitCode, json.Error));
            return;
        }

        Assert.Equal(2, text.ExitCode);
        Assert.Equal(
            Normalize("""
            Import from local session logs (all time)

                                         Copilot
            Sessions scanned                   1
            Skipped, unchanged                 0
            Skipped, active                    0
            Skipped, out of scope              0
            Unsupported sessions               0
            Records parsed                     1
            Out of scope                       0
            Recorded                           1
            Duplicate                          0
            Overlap-skipped                    0
            Ambiguous-overlap                  0
            Superseded, unmatched              0
            Malformed lines                    0
            Unsupported usage shapes           0
            Cost out of range                  0
            Unpriced                           0
            Coverage                  incomplete

            Recorded 1 request: 0.15 AI credits ($0.00), API equivalent $0.00.
            Coverage is incomplete; see the warnings.

            """),
            text.Out);
        Assert.Equal(
            "Warning: Copilot session logs: 1 folder or file couldn't be listed; any logs there weren't imported\n",
            text.Error);
        Assert.Equal((2, ""), (json.ExitCode, json.Error));
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement copilot = document.RootElement.GetProperty("sources")[0];
        Assert.Equal(
            ("incomplete", 1L, 1L, 1L),
            (document.RootElement.GetProperty("coverage").GetString(),
                copilot.GetProperty("sessionsScanned").GetInt64(),
                copilot.GetProperty("sessionsSkippedUnchanged").GetInt64(),
                copilot.GetProperty("unreadableStoreEntries").GetInt64()));
        Assert.Equal(
            """{"source":"copilot","log":"","code":"unreadableStore","event":"unknown","field":null,"version":null,"count":1,"line":null,"byteOffset":null}""",
            JsonSerializer.Serialize(Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray())));
    }

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs`, replace:

```csharp
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);
```

with:

```csharp
    }

    // A session folder that this user can't enter is an unreadable entry, never a session without a log, and the others are
    // still found; a folder without events.jsonl still has no log. FileInfo.Exists, which is false for a file it can't
    // examine, hid such a folder before. Root on Unix can enter it, so there it's read.
    [Fact]
    public void CS17_ASessionFolderThatCantBeEnteredIsCounted()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("a-session").Write(temp.Path);
        string folder = Path.GetDirectoryName(new CopilotSessionLogs("b-session").Write(temp.Path))!;
        Directory.CreateDirectory(Path.Combine(temp.Path, "session-state", "c-without-events"));

        SessionLogDiscovery discovery;
        bool enforced;
        using (DeniedDirectory denied = new(folder))
        {
            enforced = denied.Enforced;
            discovery = Source.Discover(temp.Path);
        }

        Assert.True(enforced || (!OperatingSystem.IsWindows() && Environment.IsPrivilegedProcess));
        string[] found = enforced ? ["a-session"] : ["a-session", "b-session"];
        Assert.Equal(found, discovery.Logs.Select(log => log.LogId));
        Assert.Equal(enforced ? 1 : 0, discovery.UnreadableEntries);
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);
```

New file `tests/CopilotLedger.Tests/TestSupport/DeniedDirectory.cs`:

```csharp
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CopilotLedger.Tests.TestSupport;

// Keeps this process out of a folder until it's disposed, with in-box APIs only: on Unix, mode 000; on Windows, ACEs that deny
// this user listing the folder and reading its files' data and attributes. Enforced is false when the denial doesn't bind,
// which happens only for root on Unix, whom permission bits don't restrict.
public sealed class DeniedDirectory : IDisposable
{
    private readonly string _path;
    private readonly string[] _files;
    private readonly UnixFileMode _mode;

    public DeniedDirectory(string path)
    {
        _path = path;
        _files = Directory.GetFiles(path);
        if (OperatingSystem.IsWindows())
        {
            Deny(path, _files);
        }
        else
        {
            _mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, UnixFileMode.None);
        }

        try
        {
            _ = Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch (UnauthorizedAccessException)
        {
            Enforced = true;
        }
    }

    // Whether this process can no longer list the folder.
    public bool Enforced { get; }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            Allow(_path, _files);
        }
        else
        {
            File.SetUnixFileMode(_path, _mode);
        }
    }

    // The files' deny rules first, then the folder's, which would keep the files out of reach.
    [SupportedOSPlatform("windows")]
    private static void Deny(string path, string[] files)
    {
        (FileSystemAccessRule folderRule, FileSystemAccessRule fileRule) = Rules();
        foreach (string file in files)
        {
            FileInfo info = new(file);
            FileSecurity security = info.GetAccessControl();
            security.AddAccessRule(fileRule);
            info.SetAccessControl(security);
        }

        DirectoryInfo folder = new(path);
        DirectorySecurity folderSecurity = folder.GetAccessControl();
        folderSecurity.AddAccessRule(folderRule);
        folder.SetAccessControl(folderSecurity);
    }

    // The folder's rule first, then the files'. A security object persists only the rules changed in it, so the rules are
    // removed from fresh copies, never undone by setting the old copies again.
    [SupportedOSPlatform("windows")]
    private static void Allow(string path, string[] files)
    {
        (FileSystemAccessRule folderRule, FileSystemAccessRule fileRule) = Rules();
        DirectoryInfo folder = new(path);
        DirectorySecurity folderSecurity = folder.GetAccessControl();
        folderSecurity.RemoveAccessRule(folderRule);
        folder.SetAccessControl(folderSecurity);
        foreach (string file in files)
        {
            FileInfo info = new(file);
            FileSecurity security = info.GetAccessControl();
            security.RemoveAccessRule(fileRule);
            info.SetAccessControl(security);
        }
    }

    [SupportedOSPlatform("windows")]
    private static (FileSystemAccessRule Folder, FileSystemAccessRule File) Rules()
    {
        SecurityIdentifier user = WindowsIdentity.GetCurrent().User!;
        return (
            new FileSystemAccessRule(
                user,
                FileSystemRights.ListDirectory | FileSystemRights.ReadAttributes | FileSystemRights.ReadExtendedAttributes,
                AccessControlType.Deny),
            new FileSystemAccessRule(
                user,
                FileSystemRights.ReadData | FileSystemRights.ReadAttributes | FileSystemRights.ReadExtendedAttributes,
                AccessControlType.Deny));
    }
}
```

### Tests

`DeniedDirectory` uses in-box APIs only. On Unix, it sets the folder's mode to 000 with `File.SetUnixFileMode` and restores the old mode. On Windows, it adds ACEs that deny the current user (`WindowsIdentity.GetCurrent().User`) `ReadData`, `ReadAttributes`, and `ReadExtendedAttributes` on each file in the folder, then `ListDirectory`, `ReadAttributes`, and `ReadExtendedAttributes` on the folder, and removes them in the reverse order from fresh copies of the ACLs, because a `FileSystemSecurity` persists only the rules changed in it. The denied rights leave reading and changing permissions alone, so restoring always works, and deny ACEs bind an administrator too. `Enforced` is whether the folder can no longer be listed, which fails only where permission bits don't bind: root on Unix. Each test requires `Enforced`, except in a privileged process on Unix, where it expects both sessions to be read instead, so the count is the same everywhere, nothing is skipped, and both CI runners take the denying branch. (Assumption: `ubuntu-latest` runs tests as a user that isn't root. The review ran both branches' code on Windows only; the Unix branch follows the runtime behavior in Rule 1.)

- **CS17**: with `b-session`'s folder denied, discovery lists `a-session` only, with 1 unreadable entry, and `c-without-events`, a folder without `events.jsonl`, is still no log. Without the fix: `a-session`, with 0 unreadable entries.
- **IC16**: with `s-b`'s folder denied, `import --all` exits with 2 and prints the exact table (1 session scanned, 1 request recorded, coverage incomplete) and the warning; `--json` then exits with 2, with `coverage` `incomplete`, `sessionsScanned` 1, `sessionsSkippedUnchanged` 1, `unreadableStoreEntries` 1, and the `unreadableStore` diagnostic; the ledger has 1 span. Without the fix, `import --all` exits with 0.

### Acceptance criteria

- A Copilot session folder that can't be entered makes coverage incomplete, with a warning and exit code 2, and a folder without `events.jsonl` still has no log (CS17, IC16).
- `dotnet test CopilotLedger.slnx` passes **1269** tests (1267 + 2), with none skipped, on Windows and on Linux.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 19 specification: What Import Keeps, Stated Exactly

Review finding R3, from the architect's review of Tasks 1–16. The README's "Privacy" paragraph lists what `gh ledger import` keeps as if the list were complete, but it leaves out kept fields that `docs/cli.md`'s allowlist documents, and that IP2 and IP3 check against the schema and against what import writes: session IDs (`spans.conversation_id`, `import_sessions.log_id`), providers (`spans.provider_name`), the repository root's path (`spans.repo_root`), tool outcomes (`span_links.status_code`), tool call counts (`spans.imported_tool_call_count`), hashes of tool call IDs (`span_links.span_id`), and each log's size, last-write time, and SHA-256 hash (`import_sessions`). And `docs/cli.md`'s "Read, never stored" left out three fields that the parsers' `PathTree`s read, so "Never read: every other field" was wrong about them: `data.toolCallId` of `subagent.started`, `payload.turn_id` of Codex's `turn_context`, and `payload.session_id` of `token_usage_record`. No code changes, and the allowlist table between the markers stays as it is.

### Files

| File | Change |
|---|---|
| `README.md` | The privacy paragraph names every kind of field import keeps |
| `docs/cli.md` | "Read, never stored" names the three fields |

### Code (copy exactly)

Edit 1 of 1 in `README.md`, replace:

```markdown
## Privacy

The ledger stays on your machine, and CopilotLedger never stores prompt or response content. `gh ledger import` reads Copilot's and Codex's session logs in place and keeps only what pricing and attribution need: models, token counts, times, agent names, the branch, commit, and `owner/repo` slug that a session recorded, tool names and exit codes, and SHA-256 hashes of request IDs. It never reads prompts, responses, tool arguments or results, or compaction summaries, and it doesn't read Claude Code's logs. See [What import reads and keeps](docs/cli.md#what-import-reads-and-keeps).

## Documentation
```

with:

```markdown
## Privacy

The ledger stays on your machine, and CopilotLedger never stores prompt or response content. `gh ledger import` reads Copilot's and Codex's session logs in place and keeps only what pricing, attribution, and later imports need: session IDs; models and providers; token counts and times; agent names; the repository root, branch, commit, and `owner/repo` slug that a session recorded; tool names, outcomes, exit codes, and counts; SHA-256 hashes of request and tool call IDs; and each log's size, last-write time, and SHA-256 hash. It never reads prompts, responses, tool arguments or results, or compaction summaries, and it doesn't read Claude Code's logs. See [What import reads and keeps](docs/cli.md#what-import-reads-and-keeps).

## Documentation
```

Edit 1 of 1 in `docs/cli.md`, replace:

```markdown
Imported rows leave `parent_span_id`, `server_address`, `server_port`, `pull_request_number`, and `agent_span_id` of `spans` empty, and imported tool calls leave `parent_span_id`, `git_repository_slug`, `git_branch`, and `git_commit_sha` of `span_links` empty.

- **Read, never stored:** each line's `type`. From Copilot logs: `data.producer`, `data.version`, and `data.copilotVersion` of `session.start`; the working directory, `data.context.cwd`, which only `--repo` uses; each event's `agentId`; `data.usage.isByok`, `data.usage.provider.kind`, `data.usage.initiator`, and `data.usage.parentToolCallId` of usage records; and `data.success`, `data.compactionTokensUsed.requestCount`, and `data.compactionTokensUsed.provider.kind` of compactions, which decide how a record is read. Usage IDs, service request IDs, and tool call IDs are stored only as the SHA-256 hashes above. From Codex rollouts: `payload.id` (the thread ID), `payload.cli_version`, and `payload.cwd` of `session_meta`, and `payload.thread_id`, `payload.turn_id`, and `payload.root_turn_id` of `token_usage_record`.
- **Never read:** every other field and event, including user and assistant messages, reasoning, tool arguments and results, error messages, compaction summaries (`summaryContent`) and checkpoint paths, instructions, and attachments. Import skips a line as soon as its `type` shows it's none of the events above, and it counts a line that isn't valid JSON without echoing it. Logs are read in place, never copied.

## Reports
```

with:

```markdown
Imported rows leave `parent_span_id`, `server_address`, `server_port`, `pull_request_number`, and `agent_span_id` of `spans` empty, and imported tool calls leave `parent_span_id`, `git_repository_slug`, `git_branch`, and `git_commit_sha` of `span_links` empty.

- **Read, never stored:** each line's `type`. From Copilot logs: `data.producer`, `data.version`, and `data.copilotVersion` of `session.start`; the working directory, `data.context.cwd`, which only `--repo` uses; each event's `agentId`, and `data.toolCallId` of `subagent.started`, which tie a sub-agent's events to its name; `data.usage.isByok`, `data.usage.provider.kind`, `data.usage.initiator`, and `data.usage.parentToolCallId` of usage records; and `data.success`, `data.compactionTokensUsed.requestCount`, and `data.compactionTokensUsed.provider.kind` of compactions, which decide how a record is read. Usage IDs, service request IDs, and tool call IDs are stored only as the SHA-256 hashes above. From Codex rollouts: `payload.id` (the thread ID), `payload.cli_version`, and `payload.cwd` of `session_meta`; `payload.turn_id` of `turn_context`, which ties each turn's model to its records; and `payload.thread_id`, `payload.session_id`, `payload.turn_id`, and `payload.root_turn_id` of `token_usage_record`.
- **Never read:** every other field and event, including user and assistant messages, reasoning, tool arguments and results, error messages, compaction summaries (`summaryContent`) and checkpoint paths, instructions, and attachments. Import skips a line as soon as its `type` shows it's none of the events above, and it counts a line that isn't valid JSON without echoing it. Logs are read in place, never copied.

## Reports
```

### Tests

None: no test reads these two sentences. IP2 and IP3 still compare the allowlist table, which this task leaves alone, with the schema and with what import writes.

### Acceptance criteria

- The README names every kind of field in the allowlist, and `docs/cli.md` names every field of `CopilotSessionLogSource`'s and `CodexSessionLogSource`'s `PathTree`s, as kept or as read and never stored.
- `dotnet test CopilotLedger.slnx` passes **1269** tests (1269 + 0).
- This is the sprint's last task. The final step's checks expect 1269 passed tests, not 1264, and the pull request lists nineteen task commits.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Test specifications

Every test is hermetic: it uses `TempDirectory` for files and databases, `ManualTimeProvider` or fixed times, `FakeCliPlatform` and `CliHarness` (with `CopilotHome` and `CodexHome` under the temporary root) for the CLI, and `McpTestHarness` for MCP. Session logs are synthetic, written by `CopilotSessionLogs` and `CodexRollouts`. No test starts a process, runs `git`, uses the network or Docker, or reads `~/.copilot`, `~/.codex`, `~/.claude`, or `~/.copilot-ledger`. Money is `decimal` or integer pico-USD, never `double`, and expected text is compared exactly, normalizing only the expected literal.

| Task | New tests (cases) | Changed tests | Total after |
|---|---|---|---:|
| 0 | — (`main` at `385da81`) | — | 1,018 |
| 1 | UI1 (3), UI2 (5), UI3 (9), UI4, BI1 (2), BI2 (2), BI3 (4), BI4, BI5, BI6 (2), BI7, NZ15, NZ16, S16 | — | 1,052 |
| 2 | SV1, SV2 (4), SV3, SV4 | S1, SL1, SL8, SL10, SL11, AS4 (schema version 6) | 1,059 |
| 3 | ID1, ID2, ID3 (3), UA1–UA13, UA14 (3), UA15, UA16, UA17 | — | 1,083 |
| 4 | ER1 (5), ER2–ER10 | — | 1,097 |
| 5 | CS1, CS2 (9), CS3 (2), CS4–CS12, CS13 (3), CS14 (5) | — | 1,126 |
| 6 | CU1, CU2, CU3 (2), CU4 (2), CU5, CU6, CU7, CU8 (9), CU9–CU12, CC1–CC3, CC4 (9), CC5, CC6 | — | 1,162 |
| 7 | CX1, CX2 (6), CX3, CX4, CX5 (4), CX6, CX7 (5), CX8–CX10, CX11 (3), CX12 | — | 1,188 |
| 8 | MP1, MP2 (2), SN1–SN6 | — | 1,197 |
| 9 | IR1, IR2 (4), IR3–IR15 | — | 1,215 |
| 10 | IC1 (6), IC2–IC11 | — | 1,231 |
| 11 | IP1, IP2, IP3 | — | 1,234 |
| 12 | GS2 (2 more cases) | GS1 (the new sample IDs) | 1,236 |
| 13 | `UrlHelpers_ReturnExpectedValues` (4 more cases), BI8 (3), CU13, CU14 (3), CC7, CX13 (2), CX14 | — | 1,251 |
| 14 | CS15, CX15, CX16, IR16, IR17, SN7 (2), IC12, IC13 | CS1, CX1 (unreadable entries), SN6, UA12, UA13 (`LogSha256`), IC4 (`unreadableStoreEntries`) | 1,260 |
| 15 | IR18, IR19, IC14 | IR5 (nested repositories), IC3, IC4, IC6, and GS2's import samples (`Superseded, unmatched`) | 1,263 |
| 16 | IP4 | DM1 (`importRules`), IC9, IC10 (rewritten), IP1 (`daemon.log`) | 1,264 |
| 17 | CS16, CX17, IC15 | — | 1,267 |
| 18 | CS17, IC16 | — | 1,269 |
| 19 | — (docs only) | — | 1,269 |

Exact values used across tasks:

| Value | What | Where |
|---|---|---|
| `e60cf189bf4c90ec992e7763f23f3af37a01a61852a1e3ec40acaf1618ab6ae6` | `canonical_usage_id` of service request ID `1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b` | UI1 |
| `314c4e22f41a318c9f76f436ee6f115046a33276156c566526f215c3028be845` | … of `a0b1c2d3-e4f5-4a6b-8c7d-9e0f1a2b3c4d`, in either case | UI1 |
| `0.45`, `0.15`, `0.315` | `test-retail` at API and GitHub prices, and the fixture's Foundry BYOK `gpt-6.1-sol`, for 100,000 input and 10,000 output tokens (`RetailTestRatesJson`) | BI2, BI3, BI5, BI7 |
| `0.0015` | `claude-haiku-4.5`, 1,000 input and 100 output tokens, on both lenses (0.15 AI credits) | IC3, IC4, IC11 |
| `0.11855545` | The captured compaction (112,223 input, 18,967 cache reads, 93,251 cache writes, 18 output) at `claude-haiku-4.5`: 118,555,450,000 pico-USD | IC3 |
| `0.003` | `gpt-6.1-sol` at its built-in list price, 1,000 input and 100 output tokens | IC4 |
| `0.119` | `claude-haiku-4.5`, 100,000 input (40,000 cache reads, 20,000 writes) and 10,000 output: 119,000,000,000 pico-USD, 11.9 AI credits | IR15 |
| `0.225` | `claude-haiku-4.5`, 100,000 input and 10,000 output ($0.15) plus 50,000 and 5,000 ($0.075) | IC7, IC8 |
| `0.387` | The guide's five imported requests: 38.70 AI credits | GS2 (`import-dry-run`, `import`) |

## Final step: the pull request (orchestrator, not a builder task)

After Task 19, the orchestrator:

1. Runs `dotnet build CopilotLedger.slnx` (0 warnings, 0 errors) and `dotnet test CopilotLedger.slnx` (1269 passed, 0 failed, 0 skipped), and checks with `git --no-pager diff --stat origin/main` that the branch changes only this plan, `.github/plans/archive/sprint-7.md`, and the files the tasks list.
2. Pushes `swigerb-sprint-8-ledger-import-backfill` and opens one pull request titled exactly "Sprint 8: `gh ledger import`: retroactive backfill from local session stores (Copilot, Claude, Codex)", whose body says `Closes #63`, lists the plan commits, which include the review amendments, and the nineteen task commits, and states the migration number (6), the deviations of "Deviations from issue #63", each with a one-line rationale, and that a version 6 ledger can't be opened by earlier gh-ledger versions.
3. Waits for CI on `ubuntu-latest` and `windows-latest`, including the smoke test of the published binary.
4. Optionally, with a temporary `COPILOT_LEDGER_HOME` and a synthetic session that a real Copilot CLI 1.0.103–1.0.114 wrote in a temporary `COPILOT_HOME` (never a copy of a user's session), runs `gh ledger import --all --dry-run` and then `gh ledger import --all`, and checks that the two reports agree and that a second run records nothing. Never point it at a real home directory.
5. Asks `ledger-architect` to review the branch.

This sprint doesn't bump the version, tag, or release. When the sprint closes, `ledger-architect` archives this plan with its amendments.

## Deferred to later sprints

- **A Claude Code adapter** (D7), once a documented schema or an Agent SDK-based path exists.
- **Copilot BYOK through local OpenAI-compatible servers** (D11): classify them as local when a content-free field proves the endpoint is local.
- **Wider version windows.** Copilot after 1.0.114 and Codex after 0.171.0 fail closed until fixtures verify them; so do sessions started before Copilot 1.0.103 and resumed by a supported version.
- **Compactions** with `requestCount` above 1, or `success` false with usage (A12), and **Codex** `.jsonl.zst` rollouts and tool calls.
- **VS Code and JetBrains Copilot Chat stores** (out of scope in #63), as more `ISessionLogSource` implementations.
- **Re-scanning and incremental parsing:** a `--rescan` option that ignores checkpoints, and resuming a log from a checkpoint offset.
- **Agent-invocation grouping** (`agent_span_id`) for imported spans, so context-growth reports include them.
- **Coverage notes in reports** (D26), such as "N imported, M superseded" in `status`, and an `ingestSource` breakdown over MCP (D13).
- **#64, `gh ledger export`**, can label rows by `ingest_source` and leave out superseded rows; **#65, `gh ledger audit`**, can use `imported_tool_call_count` and imported tool links, labeling missing detailed tool coverage.
- **Old daemons.** `import` refuses while a daemon without the import rules runs (D29), but such a daemon started during an import, or still running after another command upgraded the ledger (possible only off Windows), records live rows without the rules until it restarts; a later migration could re-run the reconciliation.
- **Unmatched supersession.** `Superseded, unmatched` rows keep coverage incomplete on every run whose scope holds them; a way to acknowledge them, or to learn which calls the live spans cover, could close that gap.
- **Carried from Sprint 7:** the backfill's discoverability and batching, what the API rate card hash covers, what-if fidelity, re-billing requests recorded as `unknown_model`, more dated ids, longer cache lifetimes, MAI list prices, and the carry-overs from Sprint 6, as described in `.github/plans/archive/sprint-7.md`.
- **Fork copies beside unidentified live rows (review of Tasks 1–16; A11).** If the GitHub Copilot app's fork copies keep `usageId` and `serviceRequestId`, a fork's copies of calls that only unidentified live rows recorded (rows from before migration 6, or BYOK spans) are recorded: the fork's own conversation has no live rows, so I2 and I3 don't defer them, and no canonical ID matches. A metadata-only census of usage IDs found in more than one session, and of whether a fork's first event names its parent, would show whether such copies need deferring. `accounting.sourceSessionId` isn't a parent pointer: sub-agent records carry the sub-agent's ID there.
- **Matching superseded rows later (review).** `Superseded, unmatched` counts an imported row whose superseding live row lacked its canonical ID even after a live row with that ID arrives; checking every effective live row's canonical ID, not only `superseded_by`'s, would stop reporting rows that the live spans did cover.
- **Messages of unexpected exceptions (review).** `ImportCommands` prints the message of any `InvalidOperationException`. After Task 17, only gh-ledger's own refusals throw one (a newer ledger, a dry run beside one companion file), but a dedicated exception type would keep a future one's message out of the output.
- **Producer versions in warnings (review).** Warnings print a session's validated `copilotVersion` or `cli_version` in full (at most 64 of `[0-9A-Za-z.+-]`); printing only `MAJOR.MINOR.PATCH` would keep every string from a log out of the output.
- **A dry run beside a running daemon (review).** No test hashes `ledger.db`, `-wal`, and `-shm` around a dry run that opens the ledger with `Mode=ReadOnly` while another connection holds it in WAL mode; the review checked it by hand, and all three were unchanged.
- **The cost of the unchanged check (review).** A completely imported log is hashed in full on every run, so a large store costs a full read each time; see "Re-scanning and incremental parsing" above.
