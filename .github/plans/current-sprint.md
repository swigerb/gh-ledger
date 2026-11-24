# Sprint 14: Import Speed, Routed Audits, Provenance & 0.6.0

| | |
|---|---|
| Architect | `ledger-architect` |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET (`global.json`'s SDK, unchanged), zero Docker, no new packages |
| Plan written | 2026-11-24, against `main` at `0f96a83` (Sprint 12, PR #88) |
| Branch | `swigerb-sprint-14-import-speed-routed-audits` |
| Requirements | Issue [#82](https://github.com/swigerb/gh-ledger/issues/82) remainder (quadratic import lookup; oversized events import never reads), issue [#80](https://github.com/swigerb/gh-ledger/issues/80) remainder (`premium_model_trivial_turn` and routed requests), issue [#77](https://github.com/swigerb/gh-ledger/issues/77) (release provenance attestations), the 0.6.0 version bump and its 0.5.0 → 0.6.0 upgrade docs |
| Status | Current sprint. Sprint 12's plan is archived verbatim at `.github/plans/archive/sprint-12.md`. Sprint 13's is at `.github/plans/archive/sprint-13.md`. |
| Amended | 2026-11-24, after Task 1 (`30dd55e`): Task 3's oversized rule follows import's normal-size rule instead of a two-type allowlist. 2026-11-24, after Task 2 (`36f8c27`): Task 3's test vectors repaired (PA2). See "Plan amendments". 2026-11-24, after Task 9 (`81d6bf9`): review fixes R1 and R2 added as Tasks 10 and 11. See "Review after Task 9". |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification, runs the verification commands, changes the line to `- [x]`, and stops. Tasks are sequential, and each builds on the ones before it. Each task is one commit, titled `Sprint 14 Task N: <Title>`, with the repository's `Co-authored-by` trailer.

This plan is a **specification**, not copy-exact code. Each task gives:

- the files;
- the public and internal contracts (names, types, and members are fixed);
- schemas and DDL;
- numbered algorithms;
- exact test vectors;
- acceptance criteria;
- verification commands.

Implement exactly those contracts. Where a task says "exactly", the value, string, name, or order is fixed. Where it leaves a detail open (a private helper's name, a loop's shape), choose the simplest option that matches the surrounding code's style. Don't add features, options, logging, caching, or fast paths the task doesn't ask for. Don't refactor code outside the task's files.

After **every** task:

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

The build must report 0 warnings and 0 errors (`TreatWarningsAsErrors` is on in `Directory.Build.props`). The test run must report 0 failed and 0 skipped. Record the passed count in the commit message body as `Tests: <n> passed`. The baseline at `0f96a83` is **1,921 passed, 0 failed, 0 skipped** (measured 2026-11-24 while planning).

Global rules for every task:

- **Money is `decimal` only.** No `double`, `float`, or `Math.Round` on money, and no SQLite `REAL`. The benchmark script's timings are seconds, not money, and may use `[double]`.
- **Hermetic tests.** Use temporary directories, `ManualTimeProvider`, `FakeCliPlatform`, `CliHarness`, `LedgerSql`, and synthetic `CopilotSessionLogs` and `CodexRollouts` fixtures. No test may:
  - open a socket, start a process, or run `git`;
  - use Docker or the network;
  - read `~/.copilot`, `~/.codex`, or `~/.copilot-ledger`.

  The benchmark script (Task 1) is **not** a test. `dotnet test` never runs it.
- **Privacy.** Nothing new is stored. No prompt, response, reasoning, tool argument, tool result, or summary text is read or kept. Task 3 reads, at most, the first 65,536 bytes of an oversized line, and only to find its top-level `type`. It keeps nothing from them. The benchmark reads only the session log it generates.
- **No new packages, and `global.json` is unchanged.** `src/CopilotLedger.Core/Pricing/rates.json` is unchanged in every task, including `ratesVersion`.
- **Public contracts change only as listed.** These are the only public or internal contract changes this sprint:
  - `LedgerMigrations.Indexes` (Task 2);
  - `JsonlLineReader.OversizedHandler` and `JsonlLineReader.PrefixBytes` (Task 3);
  - `JsonFields.ReadTypePrefix` (Task 3);
  - the two `OnOversized` signatures (Task 3);
  - `AuditSpan.ListCostUsd` (Task 4);
  - `<Version>` 0.6.0 (Task 5).

  CLI options, JSON fields, MCP tools, OTLP handling, the ledger format number (7), and every report's output stay as they are.
- **Version strings in top-level docs.** After Task 5, no line in `README.md`, `docs/*.md`, `.github/workflows/*.yml`, `src/`, or `tests/` may name `0.6.0`, except the places that `VersionPlacesTests` lists (VR3). New text says "this version" instead. Naming `0.5.0` or `v0.5.0` is allowed.
- If a contract here doesn't compile against the code, a referenced line has moved, or a test vector disagrees with the code's arithmetic, **stop and report it**. Don't redesign.

## Tasks

- [x] Task 1: Import Benchmark Script and Baseline (tests/benchmarks). A PowerShell 7 script that generates a large synthetic Copilot session, imports it three times with a given binary against temporary homes, and prints a timing table. The baseline is measured with a Release publish of the unchanged tree and recorded in the commit body.
- [x] Task 2: The Conversation-Live Index, Ensured on Every Open (CopilotLedger.Storage, docs & tests). `ix_spans_conversation_live` makes the importer's per-record live-span lookups indexed. Opening any format-7 ledger adds it when it's missing, whether the ledger is new, upgraded from format 6, or a format 7 that Sprint 12 created. The format number stays 7.
- [x] Task 3: Oversized Lines of Unread Types Are Skipped (CopilotLedger.Core, docs & tests). An oversized Copilot line counts as skipped when the first 64 KiB of the line show a complete top-level `type` that isn't one of the event types import reads (`CopilotSessionLogSource.Types`), the same rule as for lines of normal size, so the log can complete. An oversized line of a type import reads, or whose type can't be read from the prefix, stays malformed. Codex is unchanged.
- [x] Task 4: `premium_model_trivial_turn` Skips Routed Requests (CopilotLedger.Core, CopilotLedger.Storage, docs & tests). `AuditSpan.ListCostUsd`. A GitHub AI credits request charged at least 5% below its list price was routed by Auto or Fusion, so the rule leaves it out. No schema change.
- [x] Task 5: Version 0.6.0 (all version places, docs & tests). Step 1 of `docs/releasing.md`, exactly. Rates unchanged.
- [x] Task 6: Release Provenance Attestations (.github/workflows, docs & tests). A SHA-pinned `actions/attest-build-provenance` step in the tag-only `release` job, with `id-token: write` and `attestations: write` on that job only. Verification commands in `docs/cli.md` and `docs/releasing.md`. `ReleaseWorkflowTests`.
- [x] Task 7: 0.5.0 → 0.6.0 Upgrade Docs (docs & tests). Format 6 → 7 is one-way. Copy the data directory first, stop the daemon and close the MCP sessions, upgrade, run `gh ledger import --all` once, and roll back with `--pin v0.5.0` and the copy. `UpgradeDocsTests.UD5`.
- [x] Task 8: Import Benchmark Report (docs/test-reports). Run the Task 1 script against a Release publish of this branch and record before and after in `docs/test-reports/<date>-import-benchmark.md`.
- [x] Task 9: Final Verification (no product change). Build and test gates, a `publish` whose `--version` is `0.6.0`, money, privacy, and boundary sweeps, and confirmation that the rates are unchanged.
- [x] Task 10: LX6 Reaches the Live-Span Lookups (tests only). Review fix R1: LX6 uses service request IDs that import accepts, so its two dry runs reach `UsageAuthority.GatherAsync`, and it asserts what they decided.
- [x] Task 11: The Oversized Rule Is Copilot's (docs only). Review fix R2: `docs/cli.md` says that only a Copilot line longer than 8 MiB can count as a skipped event. A longer Codex line is always malformed.

## Starting point

- `main` at `0f96a83`, "Sprint 12: Billing and import fidelity (#88)", after Sprint 13 (`5d3bfbf`, PR #87).
- Product version: `0.5.0`.
- Ledger format: 7 (`LedgerMigrations.cs:212–220`).
- Copilot import window: 1.0.103–1.0.115.
- Tests: 1,921 passed.
- What this plan depends on, read from the code at `0f96a83`:
  - **Import lookups.** `UsageAuthority.GatherAsync` (`UsageAuthority.cs:230–268`) runs once per imported record. It runs `EXISTS (SELECT 1 FROM spans WHERE conversation_id = @conversation AND ingest_source = 'otlp')` and the same with `AND canonical_usage_id IS NULL`. `ReconcileConversationAsync` (`:290–315`) has the same subqueries. No index starts with `conversation_id`, so each lookup scans `spans`, and importing n records into a ledger of n rows is O(n²).
  - **Store open.** `SqliteLedgerStore.InitializeAsync` (`SqliteLedgerStore.cs:102–175`):
    1. sets WAL;
    2. refuses a newer `user_version`;
    3. runs each pending migration under `BEGIN IMMEDIATE`, re-reading `user_version` inside the transaction;
    4. sets `_initialized = true`.

    `LedgerSnapshot`, which `import --dry-run` uses, opens the ledger read-only and never migrates.
  - **Line reader.** `JsonlLineReader.Read` (`JsonlLineReader.cs`) stops keeping a line's bytes once it passes `maxLineBytes` and calls `onOversized(lineNumber, byteOffset)` at its end, or at EOF. Its only callers are `CopilotSessionLogSource.cs:210` and `CodexSessionLogSource.cs:174`. Both pass `OnOversized`, which counts a malformed line with `ImportDiagnosticCode.OversizedLine` (`CopilotSessionLogSource.cs:317–318`, `CodexSessionLogSource.cs:245–246`). `LedgerImporter` marks a log complete only when `MalformedLines == 0`, so a log with an oversized line is read again by every import.
  - **Audit.** `SqliteLedgerStore.ReadAuditSpansAsync` (`SqliteLedgerStore.Audit.cs:100–180`) selects 25 columns and builds `AuditSpan`. `PremiumModelTrivialTurnRule` filters candidates at lines 22–36. `list_cost_usd_pico` is NULL when the row was charged at the rate card ("the same as `cost_usd_pico`", migration 7's comment). Otherwise it holds the rate card's price beside a reported charge.
  - **Release workflow.** `.github/workflows/release.yml`:
    - top-level `permissions: contents: read`;
    - jobs `build` (matrix of six), `rates`, and `release`;
    - `release` has `if: github.event_name == 'push' && github.ref_type == 'tag'` and `permissions: contents: write`;
    - `release` steps: checkout, download-artifact, "Add the rates and checksums", "Create the release".

    `workflow_dispatch` is a trigger, but the `if` skips `release`.

## Verified facts

| # | Fact | Source (retrieved 2026-11-24) |
|---|---|---|
| F1 | `actions/attest-build-provenance` latest release is `v4.2.2`, published 2026-08-06T19:59:04Z. Tag `v4.2.2` points to commit `4d101475d8b20a2381f78447822ac1eab6504dd8`. | `gh api repos/actions/attest-build-provenance/releases/latest`; `gh api repos/actions/attest-build-provenance/commits/v4.2.2 --jq .sha` (re-checked at plan commit time) |
| F2 | v4 of `attest-build-provenance` is a composite wrapper around `actions/attest` (v4.2.1, `508db95…`). Its README says new implementations should consider `actions/attest` directly. It takes `subject-path` (newline-separated paths or globs), signs with Sigstore (public-good instance for public repositories), and stores the attestation through the attestations API. | https://github.com/actions/attest-build-provenance (README and `action.yml` at `4d10147`) |
| F3 | The required job permissions are `id-token: write` (to get the OIDC token for Sigstore) and `attestations: write` (to persist the attestation). `artifact-metadata: write` and `packages: write` are needed only with `push-to-registry` or a storage record, which this sprint doesn't use. `contents: read` is needed only for a private repository's checkout; the `release` job already has `contents: write` for `gh release create`. | Same README, "Usage" and "Permissions" |
| F4 | swigerb/gh-ledger is public, so attestations go to the public Sigstore instance and anyone can verify them. | `gh repo view swigerb/gh-ledger --json visibility` → `PUBLIC` |
| F5 | `gh attestation verify <file> --repo <owner>/<repo>` verifies by the file's SHA-256 digest. `--signer-workflow <owner>/<repo>/<path>` requires the workflow that signed it, and `--source-ref <ref>` requires the ref it ran on. | `gh attestation verify --help`, gh 2.103.3 |
| F6 | Auto model selection: "If you are on a paid Copilot plan, you qualify for a 10% discount on model costs while using auto model selection in Copilot Chat, or Copilot cloud agent." | https://docs.github.com/en/copilot/concepts/models/auto-model-selection, "Discount for using auto model selection" |
| F7 | Copilot Fusion: "Through November 30, 2026, users on paid Copilot plans receive a 30% discount on model costs when using Copilot Fusion. When you use Copilot Fusion, you're billed for each model it uses, at that model's standard rate. There's no separate charge for Copilot Fusion." | https://docs.github.com/en/copilot/concepts/models/copilot-fusion, "Billing for Copilot Fusion" |
| F8 | One GitHub AI credit is $0.01 (`AiCreditMath`, unchanged since Sprint 10). | Repository code and `docs/configuration.md` |
| F9 | SQLite stores `CREATE INDEX IF NOT EXISTS ix_spans_conversation_live ON spans (conversation_id, canonical_usage_id) WHERE ingest_source = 'otlp';` in `sqlite_master.sql` as `CREATE INDEX ix_spans_conversation_live ON spans (conversation_id, canonical_usage_id) WHERE ingest_source = 'otlp'`. `pragma_index_list('spans')` reports it as `ix_spans_conversation_live\|0\|c\|1` (name, unique, origin, partial). Both `GatherAsync` subqueries plan as `SEARCH spans USING COVERING INDEX ix_spans_conversation_live (conversation_id=?)`, and `… (conversation_id=? AND canonical_usage_id=?)` for the `IS NULL` one. | Planning probe, SQLite 3.50.4 (Python `sqlite3`), 2026-11-24 |
| F10 | SQLite refuses `ALTER TABLE spans DROP COLUMN canonical_usage_id` or `… ingest_source` while a partial index refers to the column ("error in index ix_spans_conversation_live after drop column"). Test fixtures that build an older format by dropping those columns must `DROP INDEX ix_spans_conversation_live;` first. | Same probe |
| F11 | Baseline import of a generated 20,000-record session with two 9 MiB ignored lines, using `0f96a83`'s Release build on the planning machine: run 1 took 141.86 s (exit 2, recorded 20,000, malformed 2). An unchanged re-run took 282.62 s (exit 2, duplicate 20,000, malformed 2), because the log never completes. After manually adding the Task 2 index, the same re-run took 1.9 s. | Planning probe, 2026-11-24 (Task 1 re-measures it reproducibly) |

## Assumptions (not verified against a primary source)

- **A1.** The oversized lines seen in practice are `model.model_call_success` and `model.messages_snapshot` events. They come from issue #82 and the coordinator, and no real session log was opened to confirm them, by design. Task 3's rule doesn't depend on these names: any type that isn't in `CopilotSessionLogSource.Types` is skipped, as a short line of that type already is. The names are used only in tests and the benchmark.
- **A2.** In an oversized event line, the top-level `type` appears within the first 65,536 bytes. If it doesn't, the line stays malformed. That loses no data, but it doesn't help the log complete.
- **A3.** A reported charge for an Auto or Fusion request already reflects the discount (F6, F7), so `cost_usd_pico < list_cost_usd_pico` for routed requests. This is issue #80's premise. Fusion requests after November 30, 2026 are charged at the list price and can't be told apart from a chosen model.
- **A4.** A request charged 5% or more below its list price was routed. Nothing else in the rates (rounding of nano-AIU to pico-USD, or tier boundaries) produces a gap that large.

## Decisions

- **D1. Index, not cache (#82).** A partial covering index `ix_spans_conversation_live ON spans (conversation_id, canonical_usage_id) WHERE ingest_source = 'otlp'` turns each per-record lookup into an O(log n) seek, with the `UsageAuthority` SQL unchanged. Its literal `'otlp'` matches the partial index's `WHERE`, which SQLite requires before it can use the index. A per-import cache was rejected: it must be invalidated by every live write in the same transaction, it duplicates `UsageAuthority`'s decision logic, and it still costs a scan per conversation.
- **D2. Ensured on open, not a migration.** The index is created by `InitializeAsync` after the migration loop, whenever it's missing. That covers:
  - a new ledger;
  - format 6 → 7 (migration 7 runs, then the index is added);
  - a format-7 ledger that Sprint 12 created without the index;
  - a ledger where something dropped it.

  `user_version` stays 7, migration 7's text is unchanged, and an earlier format-7 build (Sprint 12, if any were installed) can still open the ledger. SQLite maintains the index for its writes. Adding migration 8 was rejected: it would make the upgrade one-way a second time for no format change.
- **D3. Dry runs don't create it.** `import --dry-run` never writes the ledger (`docs/cli.md`), so on a ledger that no write command has opened since the upgrade, a dry run is as slow as before. Its results are identical either way. Documented in Task 2.
- **D4. Oversized prefix (amended).** Import already skips a line of normal size as soon as its `type` shows it's none of the events it reads (`docs/cli.md`). Oversized lines follow the same rule. The reader passes each oversized line's first `min(65,536, length)` bytes to the handler. `JsonFields.ReadTypePrefix` reads them as an incomplete JSON block, against `CopilotSessionLogSource.Types`. The line is skipped only if the top-level `type` string is complete, is **not** one of the event types import reads (result -1), and appears after `session.start` with the log not stopped. An oversized line of a type import reads (result ≥ 0), or whose type can't be read from the prefix (result null), stays malformed, as today. This is as safe as an allowlist: a line of a type import never reads can't hold a record import would keep, at any size, so skipping it loses nothing. No list of special oversized types exists. Line hashing, line numbers, and offsets are unchanged. An oversized **unterminated** last line passes an empty prefix and stays malformed, because it's incomplete. A prefix can't see a second `type` property later in the line: the first one decides.
- **D5. Routed means at least 5% below list (#80).** The rule leaves out a span when `ListCostUsd is decimal list && list > 0m && CostUsd <= list * 0.95m`. The literal request was "reported charge less than list cost". The 5% tolerance keeps a request whose charge differs from the rate card by rounding or a small rate drift in the rule, while Auto (10%) and Fusion (30%) clear it. No `ChargeSource` property is added. `ListCostUsd` is `null` when the ledger row's `list_cost_usd_pico` is NULL, which means "the same as the charge".
- **D6. Attest with `actions/attest-build-provenance@4d10147` (v4.2.2).** This is the action that issue #77 names, and it's still maintained. Moving to `actions/attest` is deferred (S14-3). Subjects are the six binaries, `rates.json`, and `rates.v2.json`, listed explicitly (no glob). `gh-ledger-checksums.txt` isn't attested: it's derived from the attested files, and each file is verified by digest.
- **D7. Manual runs never attest.** `workflow_dispatch` keeps skipping the `release` job, so a manual run builds, smoke-tests, checks rates, and uploads workflow artifacts, but never attests and never releases. Only the `release` job, which runs for a pushed `v*` tag, gets `id-token: write` and `attestations: write`. Top-level permissions stay `contents: read`, and `build` and `rates` get nothing new. Attesting manual-run artifacts was rejected: they aren't released, and an attestation for a binary nobody ships only adds noise to the repository's attestation list.
- **D8. Authenticode is out of scope.** Windows binaries stay unsigned. The docs say so, and point to `gh attestation verify` instead.
- **D9. Bump before the docs that would name it.** Task 5 bumps the version before Task 6 and Task 7 write docs that name `0.5.0`. VR3 fails on any unlisted mention of the *current* version. Writing "0.5.0 and earlier" while the version is still 0.5.0 would fail it, and after the bump it's fine.

## Plan amendments

- **PA1 (2026-11-24, after Task 1 at `30dd55e`; coordinator-approved).** Task 3 no longer special-cases `model.model_call_success` and `model.messages_snapshot`. An oversized line whose complete top-level `type`, read from the 64 KiB prefix, isn't one of the event types import reads counts as a skipped event, and the log can complete. An oversized line of a type import reads, or whose type can't be read from the prefix, stays malformed. Changed: the Requirements row, the Task 3 summary line, A1, D4, Task 3's title, contracts, Copilot `OnOversized` algorithm, tests (CS20 becomes a theory, CS21 gains two heads, CS22, CS24, and OV1 renamed, CS25–CS27 added), docs text, and acceptance; Task 8's wording; Task 9's privacy list; "Tests added this sprint"; S14-5. Task 1 and its completed text, including F11, are unchanged. Unchanged: the 64 KiB prefix, "the first `type` decides", the after-`session.start` condition, unterminated last lines, hashing, line numbers and offsets, Codex, D1–D3, D5–D9, and the task order.
- **PA2 (2026-11-24, after Task 2 at `36f8c27`; plan-text repair, no behavior change).** Task 3's tests now agree with PA1's rule and the code. CS5 (an oversized `user.message`, a type import doesn't read) expects MalformedLines 0, SkippedEvents 2, and no diagnostics, instead of PA1's leftover "unchanged" malformed expectation. CS19, CS25, CS26, and OV1 use valid service request IDs (`R1`, `R2`) instead of `"r-1"`/`"r-2"`, which `UsageIdentity.NormalizeServiceRequestId` rejects. `O`, `R1`, `R2`, CX18's fixture, OV1's harness and assertions, the nested reader class names in the contracts, CS18's precondition note, the acceptance text, and "Tests added this sprint" are made explicit. Task 2's LX6 text is left as committed.

## Task 1: Import Benchmark Script and Baseline

**Files:** `tests/benchmarks/Measure-ImportSpeed.ps1` (new). No product or test-project change. The script is outside every project, so `dotnet test` never runs it.

**Contract:**

```powershell
#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Binary,        # a published gh-ledger (gh-ledger.exe on Windows)
    [ValidateRange(2, 1000000)] [int] $Records = 20000,
    [string] $WorkRoot,                              # default: <temp>/gh-ledger-import-benchmark-<yyyyMMdd-HHmmss>
    [switch] $KeepFiles
)
```

**Algorithm:**

1. `$ErrorActionPreference = 'Stop'`. Resolve `$Binary` to a full path, and throw `"Binary <path> doesn't exist."` if it isn't a file.
2. Set `$WorkRoot`, defaulting to `Join-Path ([IO.Path]::GetTempPath()) ("gh-ledger-import-benchmark-" + (Get-Date -Format yyyyMMdd-HHmmss))`. If it exists and has any entry, throw `"WorkRoot <path> isn't empty."`. Create `<WorkRoot>/copilot-home` and `<WorkRoot>/ledger-home`.
3. Generate `<WorkRoot>/copilot-home/session-state/6f1c2d3e-4b5a-4c7d-8e9f-0a1b2c3d4e5f/events.jsonl` with a `System.IO.StreamWriter`: UTF-8 without BOM, `NewLine = "\n"`. Event `n` (1-based) has:
   - ID `'00000000-0000-4000-8000-{0:x12}' -f n`;
   - timestamp 2026-11-20T12:00:00Z plus n seconds, `yyyy-MM-ddTHH:mm:ss.fffZ`, invariant culture;
   - `parentId` null for event 1, else the previous ID.

   Each line is `{"type":"<type>","data":<data>,"id":"<id>","timestamp":"<time>","parentId":<parent>}`.
   1. `session.start` with data `{"sessionId":"6f1c2d3e-4b5a-4c7d-8e9f-0a1b2c3d4e5f","version":1,"producer":"copilot-agent","copilotVersion":"1.0.114","startTime":"2026-11-20T12:00:00.000Z","context":{"cwd":"<WorkRoot with / separators>"},"alreadyInUse":false}`.
   2. For i = 1..Records, `session.usage_record` with data `{"usage":{"model":"claude-haiku-4.5","provider":{"id":"copilot","kind":"copilot","label":"GitHub Copilot"},"modelDisplayName":"claude-haiku-4.5","inputTokens":1000,"outputTokens":100,"cacheReadTokens":0,"cacheWriteTokens":0,"reasoningTokens":0,"duration":1000,"initiator":"user","isByok":false,"apiCallId":"u-<i:d8>","serviceRequestId":"<'10000000-0000-4000-8000-{0:x12}' -f i>","accounting":{"sourceSessionId":"6f1c2d3e-4b5a-4c7d-8e9f-0a1b2c3d4e5f","sequence":<i>,"usageId":"u-<i:d8>"},"cacheDetailsReported":true,"numToolCalls":0}}`.
   3. Right after record `[int]($Records / 2)`, write two ignored oversized lines:
      - `model.messages_snapshot` with data `{"messages":"<x × 9,437,184>"}`;
      - `model.model_call_success` with data `{"response":"<x × 9,437,184>"}`.

      Each is longer than 8 MiB.

   The filler is the letter `x` only: the file never holds real content.
4. Save the current `COPILOT_HOME` and `COPILOT_LEDGER_HOME`, then set them to `<WorkRoot>/copilot-home` and `<WorkRoot>/ledger-home`. In `try`, do runs 1–3; in `finally`, restore both variables (removing any that were unset), and delete `$WorkRoot` unless `-KeepFiles`.
5. Each run uses a `System.Diagnostics.Stopwatch` around `& $Binary import --all --json`, capturing stdout. Record `$LASTEXITCODE`, then parse the JSON. From the `sources` element whose `source` is `copilot`, take `sessionsScanned`, `sessionsSkippedUnchanged`, `recorded`, `duplicate`, `malformedLines`, and `skippedEvents`. If `sessionsScanned` isn't 1, throw `"Run <n> scanned <m> sessions, not 1."`.
   - Run 1: the first import into an empty ledger.
   - Run 2: the same command again, unchanged.
   - Run 3: append `"\n"` to `events.jsonl` (`[IO.File]::AppendAllText`), which changes its bytes but not its events, then the same command.
6. Write to the output (`Write-Output`):
   - `Binary: <path>`;
   - `Version: <& $Binary --version>`;
   - `OS: <[System.Runtime.InteropServices.RuntimeInformation]::OSDescription>`;
   - `Processors: <[Environment]::ProcessorCount>`;
   - `Records: <Records>`;
   - `Log bytes: <length of events.jsonl before run 1>`.

   Then write this markdown table, with seconds formatted `F2` using the invariant culture:

   ```text
   | Run | Seconds | Exit | Scanned | Skipped unchanged | Recorded | Duplicate | Malformed lines | Skipped events |
   |---|---|---|---|---|---|---|---|---|
   | 1 first | … |
   | 2 unchanged | … |
   | 3 changed bytes | … |
   ```

**Baseline (do this in this task, after writing the script):**

```powershell
git stash list   # must be empty; the tree must equal 0f96a83 plus this script
dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r win-x64 -o "$env:TEMP\gh-ledger-bench-before"
pwsh -NoProfile -File tests/benchmarks/Measure-ImportSpeed.ps1 -Binary "$env:TEMP\gh-ledger-bench-before\gh-ledger.exe"
```

On Linux or macOS, use that platform's RID and the binary without `.exe`. The before run takes several minutes; on the planning machine, runs 1 and 2 took about 142 s and 283 s (F11).

**Acceptance:**

- The script runs to completion against the unchanged tree and prints the table. Copy its whole output verbatim into the commit body under `Baseline (0f96a83):`, then `Tests: <n> passed`.
- Expected shape of the before-table. If it differs, report it; don't fix product code in this task.
  - Run 1: exit 2, recorded = Records, malformed lines 2.
  - Run 2: skipped unchanged 0, duplicate = Records, malformed lines 2.
  - Run 3: duplicate = Records, malformed lines 2.
- `git grep -n "\.copilot-ledger\|\.copilot\b" -- tests/benchmarks` finds nothing: the script never names the real homes.
- No test count change. The baseline remains 1,921.

**Verification:** `dotnet build CopilotLedger.slnx`; `dotnet test CopilotLedger.slnx`; the baseline commands above.

## Task 2: The Conversation-Live Index, Ensured on Every Open

**Files:**

- `src/CopilotLedger.Storage/LedgerMigrations.cs`
- `src/CopilotLedger.Storage/SqliteLedgerStore.cs`
- `docs/cli.md`
- New `tests/CopilotLedger.Tests/Storage/ConversationLiveIndexTests.cs`
- The test fixtures named under "Fixture fixes"

`UsageAuthority.cs` is **unchanged**.

**Contract (`LedgerMigrations`):**

```csharp
// Indexes that every ledger of the latest format has. Opening the ledger adds one that's missing, without changing
// user_version: SQLite keeps an index current for every writer, including an earlier build of the same format.
// ix_spans_conversation_live: the importer's per-record live-span lookups (UsageAuthority), which name 'otlp' literally
// so that SQLite can use this partial index.
public static IReadOnlyList<(string Name, string Sql)> Indexes { get; } =
[
    ("ix_spans_conversation_live",
     "CREATE INDEX IF NOT EXISTS ix_spans_conversation_live ON spans (conversation_id, canonical_usage_id) WHERE ingest_source = 'otlp';"),
];
```

`Migrations` and migration 7's text are unchanged.

**Algorithm (`SqliteLedgerStore.InitializeAsync`, after the migration loop, before `_initialized = true`):** for each `(name, sql)` in `LedgerMigrations.Indexes`:

1. Run `SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = @name;` outside a transaction. If it returns 1, continue.
2. `await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);` (BEGIN IMMEDIATE, as the migrations do).
3. Run the same count inside the transaction. If it returns 0, execute `sql` in the transaction.
4. `await transaction.CommitAsync(cancellationToken);`

A second process that waits on the write lock then sees the index and only commits an empty transaction.

**Fixture fixes:** each test that turns a ledger back into an older format by dropping `spans.canonical_usage_id` or `spans.ingest_source` must run `DROP INDEX ix_spans_conversation_live;` first (F10). At `0f96a83`, those scripts are in:

- `tests/CopilotLedger.Tests/Storage/LedgerSnapshotTests.cs` (~line 82);
- `Storage/ApiEquivalentStoreTests.cs` (~line 125);
- `Storage/SchemaVersion6Tests.cs` (~line 72);
- `Storage/SpanLinkStoreTests.cs` (~lines 36, 397, 595).

Also add `DROP INDEX IF EXISTS ix_spans_conversation_live;` to the format-6 script in `SchemaVersion7Tests` (`CreateFormat6Async`), so that it builds a true format 6. Run the suite to find any other such script: the error is `error in index ix_spans_conversation_live after drop column`. Fix only by adding the `DROP INDEX` line; don't change what any test asserts.

**Tests (`ConversationLiveIndexTests`, all through public APIs and `LedgerSql`):**

| ID | Name | Vector | Expected |
|---|---|---|---|
| LX1 | `LX1_ANewLedger_HasTheIndex` | `new SqliteLedgerStore(path, time).InitializeAsync()` | `RowsAsync(path, "SELECT name \|\| '\|' \|\| \"unique\" \|\| '\|' \|\| origin \|\| '\|' \|\| partial FROM pragma_index_list('spans') WHERE name = 'ix_spans_conversation_live'")` is `["ix_spans_conversation_live\|0\|c\|1"]`. `RowsAsync(path, "SELECT name FROM pragma_index_info('ix_spans_conversation_live') ORDER BY seqno")` is `["conversation_id", "canonical_usage_id"]`. `ScalarAsync(path, "SELECT sql FROM sqlite_master WHERE name = 'ix_spans_conversation_live'")` is `CREATE INDEX ix_spans_conversation_live ON spans (conversation_id, canonical_usage_id) WHERE ingest_source = 'otlp'`. `PRAGMA user_version` is 7. |
| LX2 | `LX2_AFormat6Ledger_UpgradesTo7AndGainsTheIndex` | Initialize, record one live span with `TestSpans.Chat(conversationId: "c-1")`, then run `SchemaVersion7Tests`' format-6 script (with the `DROP INDEX` first, as above) and `PRAGMA user_version = 6;`. Open a new store on the path and initialize. | `user_version` 7; the LX1 index row is present; `SELECT COUNT(*) FROM spans` is 1; `charge_source` of that row is `rate_card`. |
| LX3 | `LX3_ASprint12Format7Ledger_GainsTheIndex` | Initialize, record the same span, `DROP INDEX ix_spans_conversation_live;` (user_version stays 7), then open a new store and initialize. | `user_version` 7; the index row is present; the span count is 1. |
| LX4 | `LX4_OpeningIsIdempotentAndConcurrentOpensAgree` | On one new path, `Task.WhenAll` of two `InitializeAsync` calls on two store instances, then a third, sequential `InitializeAsync`. | No exception; `SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ix_spans_conversation_live'` is 1; `user_version` 7. |
| LX5 | `LX5_TheImportLookups_UseTheIndex` | On an initialized empty ledger, `RowsAsync` of `EXPLAIN QUERY PLAN SELECT EXISTS (SELECT 1 FROM spans WHERE conversation_id = 'c' AND ingest_source = 'otlp'), EXISTS (SELECT 1 FROM spans WHERE conversation_id = 'c' AND ingest_source = 'otlp' AND canonical_usage_id IS NULL)`, joining the rows' `detail` column. | The joined text contains `USING COVERING INDEX ix_spans_conversation_live (conversation_id=?)` and `USING COVERING INDEX ix_spans_conversation_live (conversation_id=? AND canonical_usage_id=?)`. If the bundled SQLite words the plan differently, stop and report it; don't loosen the assertion. |
| LX6 | `LX6_ADryRun_NeverCreatesTheIndex_AndDecidesTheSame` | `CliHarness` with a Copilot log (`CopilotSessionLogs("s-a")` with `Usage("u-1", "r-1")` and `Usage("u-2", "r-2")`). Initialize the harness's ledger, insert one live span for conversation `s-a` with `TestSpans.Chat(conversationId: "s-a")` through the store, then `DROP INDEX ix_spans_conversation_live;`. Run `import --all --dry-run --json` (A). Then initialize a store on the path, which adds the index, and run the same command (B). | After A, the index is absent (count 0). A and B have exit codes equal to each other, and identical stdout. After B, the index is present. |

**Docs (`docs/cli.md`):**

- In the `**Ledger format.**` paragraph, after "the first time its daemon or any command opens it.", insert: `Opening a format 7 ledger also adds an index that import uses to match its requests with live ones, when it's missing; on a large ledger that can take a moment once, and it doesn't change the format.` The paragraph must still not contain `format 6` (UD3).
- In the `--dry-run` bullet under "Import past sessions", append: `Because it never writes the ledger, it doesn't add the index that the paragraph on the ledger format describes, so until the daemon or another command has opened the ledger with this version, a dry run of a large session can take much longer than the import itself; its results are the same.`

**Acceptance:**

- LX1–LX6 pass, the fixture fixes change no assertion, and `UpgradeDocsTests` UD1–UD4 still pass (the format stays 7).
- `git diff 0f96a83 -- src/CopilotLedger.Storage/UsageAuthority.cs` is empty.
- Quick check, not a gate: `pwsh -File tests/benchmarks/Measure-ImportSpeed.ps1 -Binary <a Release publish of this commit> -Records 2000` finishes with run 2 taking under 5 s. Note the result in the commit body.

## Task 3: Oversized Lines of Unread Types Are Skipped

**Files:**

- `src/CopilotLedger.Core/Import/JsonlLineReader.cs`
- `src/CopilotLedger.Core/Import/JsonFields.cs`
- `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`
- `src/CopilotLedger.Core/Import/CodexSessionLogSource.cs`
- `docs/cli.md`
- `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs`
- `tests/CopilotLedger.Tests/Import/CodexRolloutTests.cs`
- New `tests/CopilotLedger.Tests/Cli/ImportOversizedTests.cs`

`ImportLimits` is unchanged.

**Contracts:**

```csharp
// JsonlLineReader
internal const int PrefixBytes = 64 * 1024;   // the start of an oversized line that its handler sees

// prefix: the line's first min(PrefixBytes, its length) bytes, valid only during the call; empty for an oversized last
// line without "\n", which is incomplete.
internal delegate void OversizedHandler(ReadOnlySpan<byte> prefix, long lineNumber, long byteOffset);

internal static void Read(
    Stream stream, long maxBytes, int maxLineBytes, LineHandler onLine,
    OversizedHandler onOversized,                // was Action<long, long>
    Action<long, long> onUnterminated, IncrementalHash? hash, CancellationToken cancellationToken);

// JsonFields
// The index in types of the top-level "type" string of a JSON object that may be cut off anywhere: -1 when it's another
// string, and null when the prefix isn't the start of a JSON object, or ends before a top-level "type" string is complete,
// or that value isn't a string. The first top-level "type" decides; a later duplicate isn't seen.
internal static int? ReadTypePrefix(ReadOnlySpan<byte> prefix, IReadOnlyList<string> types);

// CopilotSessionLogSource.Session (the nested per-log reader class that has OnOversized today). It uses the existing
// private static CopilotSessionLogSource.Types list, the event types import reads, unchanged. No other list of types is added.
internal void OnOversized(ReadOnlySpan<byte> prefix, long number, long offset);

// CodexSessionLogSource.Rollout (the nested per-rollout reader class that has OnOversized today)
internal void OnOversized(ReadOnlySpan<byte> prefix, long number, long offset);   // ignores prefix; behavior unchanged
```

**Algorithm (reader):**

1. Keep an `int prefixLength`, initially 0.
2. When a line first exceeds `maxLineBytes` (the `lineLength + count > maxLineBytes` branch), before setting `oversized = true`:
   1. If `lineLength < PrefixBytes`, copy `min(PrefixBytes - lineLength, count)` bytes from `chunk[start..]` to `line[lineLength..]`, and set `prefixLength = lineLength + copied`.
   2. Otherwise, set `prefixLength = PrefixBytes`.

   `line` is always at least `ChunkBytes` (64 KiB) long, because it's rented with that size and only grows, so the copy fits. Don't reset `line`'s bytes until the line ends.
3. At the line's `\n`, call `onOversized(line.AsSpan(0, prefixLength), lineNumber, lineOffset)`. Then reset `prefixLength = 0` along with `lineLength` and `oversized`.
4. At EOF with `oversized`, call `onOversized(ReadOnlySpan<byte>.Empty, lineNumber, lineOffset)`.
5. Everything else is unchanged:
   - hashing every byte read;
   - blank-line handling;
   - `\r` trimming;
   - line numbers and offsets;
   - the 8 MiB limit.

**Algorithm (`ReadTypePrefix`):**

1. Create `Utf8JsonReader reader = new(prefix, isFinalBlock: false, new JsonReaderState(Options))` with the class's existing `Options`. Wrap steps 2–5 in `try { … } catch (JsonException) { return null; }`.
2. If `!reader.Read()` or `reader.TokenType != JsonTokenType.StartObject`, return null.
3. Loop:
   1. If `!reader.Read()`, return null.
   2. If `TokenType == EndObject`, return null.
   3. The token is a `PropertyName`. If `TextEquals(ref reader, "type")`, go to step 4.
   4. Otherwise, if `!reader.Read()` or `!reader.TrySkip()`, return null (the value is cut off).
4. If `!reader.Read()` or `TokenType != String`, return null.
5. For `i` in `0..types.Count`, return `i` if `TextEquals(ref reader, types[i])`. Return -1.

**Algorithm (Copilot `OnOversized`):**

1. If `_started`, `_stop is null`, and `JsonFields.ReadTypePrefix(prefix, Types) == -1` (a complete top-level `type` that import doesn't read), increment `_skipped` and return. Add no diagnostic.
2. Otherwise (before `session.start`, after a stop, a type import reads, or a type the prefix can't show), call `Malformed(ImportDiagnosticCode.OversizedLine, ImportEventKind.Unknown, null, number, offset)`, as today.

**Tests:**

`O(head)` is a private static test helper: `head + new string('x', ImportLimits.MaxLineBytes + 1 - head.Length - 3) + "\"}}"`, an ASCII line exactly `MaxLineBytes + 1` bytes long. Add it to `CopilotSessionGateTests`, and add an identical private copy to `ImportOversizedTests` (OV1). CX18 inlines the same formula. `C` is `CopilotSessionLogs.Canary`. Each `CopilotSessionGateTests` test also calls `AssertNoCanary(result)`.

`R1` is `"11111111-1111-4111-8111-111111111111"` and `R2` is `"22222222-2222-4222-8222-222222222222"`, the same values as `ImportCommandTests.S1` and `S2`. Declare them as private constants in each test class that uses them. A `Usage` record needs a service request ID that `UsageIdentity.NormalizeServiceRequestId` accepts (a non-empty UUID); with anything else, such as `"r-1"`, it's an unsupported usage shape, no record is created, and the log never completes.

| ID | Log (after `session.start` unless stated) | Expected |
|---|---|---|
| CS5 `CS5_ALineLongerThan8MiBIsDiscardedUnread` (name and log unchanged; assertions change, see below) | oversized `user.message` (head `"{\"type\":\"user.message\",\"data\":{\"content\":\"" + C`, as today), then `UserMessage()` | MalformedLines 0, SkippedEvents 2, Diagnostics empty. Still asserts the oversized line is exactly `MaxLineBytes + 1` bytes, and `AssertNoCanary(result)` |
| CS18 `CS18_AnOversizedMessagesSnapshot_IsSkipped` | `Raw(O("{\"type\":\"model.messages_snapshot\",\"data\":{\"messages\":\"" + C))`, then `UserMessage()` | Status Read, MalformedLines 0, SkippedEvents 2, Diagnostics empty |
| CS19 `CS19_AnOversizedModelCallSuccess_IsSkipped_WhereverItsTypeIs` | `Raw(O("{\"id\":\"00000000-0000-4000-8000-000000000002\",\"data\":{\"model\":\"m\",\"nested\":[1,{\"a\":true},null]},\"type\":\"model.model_call_success\",\"response\":\"" + C))`, then `Usage("u-1", R1)` | Status Read, MalformedLines 0, SkippedEvents 1, Records.Count 1, Diagnostics empty |
| CS20 `CS20_AnOversizedEventThatImportReads_StaysMalformed` (theory) | Two heads, each `Raw(O(head))`: `"{\"type\":\"session.usage_record\",\"data\":{\"usage\":{\"model\":\"" + C`; `"{\"type\":\"session.usage\\u005frecord\",\"data\":{\"usage\":{\"model\":\"" + C` (escaped, still a type import reads) | Each: MalformedLines 1, SkippedEvents 0, Records.Count 0, one `OversizedLine` diagnostic at line 2 |
| CS21 `CS21_ATypeBeyondThePrefixOrUnreadable_StaysMalformed` (theory) | Seven heads, each with `O(head)`: `"{\"pad\":\"" + new string('y', 65_508) + "\",\"type\":\"model.messages_snapshot\",\"x\":\""` (the prefix ends inside the `type` value, after `"type":"model.mess`: the value starts at byte 65,526, and the prefix is bytes 0–65,535); `"{\"data\":{\"messages\":\"" + new string('y', 70_000) + "\"},\"type\":\"model.messages_snapshot\",\"x\":\""` (type past 65,536 bytes); `"{\"x\":\""` (no `type` at all); `"{\"type\":7,\"x\":\""`; `"{\"type\":null,\"x\":\""`; `"[{\"type\":\"model.messages_snapshot\",\"x\":\""`; `"{type:\"model.messages_snapshot\",\"x\":\""` | Each: MalformedLines 1, SkippedEvents 0, one `OversizedLine` diagnostic at line 2 |
| CS22 `CS22_AnEscapedUnreadType_IsSkipped` | `Raw(O("{\"type\":\"model.messages\\u005fsnapshot\",\"x\":\""))` | MalformedLines 0, SkippedEvents 1 |
| CS23 `CS23_AnOversizedUnterminatedLastLine_StaysMalformed` | `WriteAsync(home, new CopilotSessionLogs(SessionId).Text() + O("{\"type\":\"model.messages_snapshot\",\"x\":\""))`, with no trailing `\n` | MalformedLines 1, one `OversizedLine` diagnostic at line 2 |
| CS24 `CS24_AnOversizedUnreadLineBeforeSessionStart_StaysMalformed` | `WriteAsync(home, O("{\"type\":\"model.messages_snapshot\",\"x\":\"") + "\n" + new CopilotSessionLogs(SessionId).Text())` | Status Read, MalformedLines 1, one `OversizedLine` diagnostic at line 1 |
| CS25 `CS25_AnOversizedBinaryAsset_IsSkipped` | `Raw(O("{\"type\":\"session.binary_asset\",\"data\":{\"bytes\":\"" + C))`, then `Usage("u-1", R1)` | Status Read, MalformedLines 0, SkippedEvents 1, Records.Count 1, Diagnostics empty |
| CS26 `CS26_AnOversizedLineOfAnUnknownFutureType_IsSkipped` | `Raw(O("{\"type\":\"future.unknown_event\",\"data\":{\"blob\":\"" + C))`, then `Usage("u-1", R1)` | Status Read, MalformedLines 0, SkippedEvents 1, Records.Count 1, Diagnostics empty |
| CS27 `CS27_AnOversizedAssistantMessage_StaysMalformed` | `Raw(O("{\"type\":\"assistant.message\",\"data\":{\"content\":\"" + C))` | MalformedLines 1, SkippedEvents 0, one `OversizedLine` diagnostic at line 2 |
| CX18 `CX18_AnOversizedRolloutLine_StaysMalformed_WhateverItsType` (`CodexRolloutTests`) | `new CodexRollouts(Thread).Raw(head + new string('x', ImportLimits.MaxLineBytes + 1 - head.Length - 3) + "\"}}")`, with `head` = `"{\"type\":\"model.messages_snapshot\",\"payload\":{\"x\":\""` | Status Read, MalformedLines 1, one `OversizedLine` diagnostic at line 2 |
| OV1 `OV1_ALogWhoseOversizedLinesAreUnread_CompletesAndIsSkippedNextTime` (`ImportOversizedTests`, `CliHarness` built as `ImportCommandTests.Harness` builds it, with `ManualTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero))`) | Write `new CopilotSessionLogs("s-ov").Usage("u-1", R1).Raw(O("{\"type\":\"model.messages_snapshot\",\"x\":\"")).Raw(O("{\"type\":\"model.model_call_success\",\"x\":\"")).Usage("u-2", R2)` to `harness.Context.CopilotHome` (not active). Run `import --all --json` twice. | Run 1: exit 0, stderr empty; `sources[0]` (copilot) `recorded` 2, `malformedLines` 0, `unsupportedUsageShapes` 0, `skippedEvents` 2, `coverage` `"complete"`. Run 2: exit 0, stderr empty; `sessionsSkippedUnchanged` 1, `recorded` 0, `duplicate` 0. Neither stdout contains `C` or `xxxxxxxx`. |

Before relying on CS18's "SkippedEvents 2", check that `UserMessage()` counts as one skipped event (CS2 shows it does: `{not json` then `UserMessage()` gives SkippedEvents 1). If an existing exit-code rule makes run 1's exit code differ from 0 with no malformed lines, stop and report it.

**CS5's changed assertions.** `user.message` isn't in `Types`, so under D4 its oversized line is skipped. In `CS5_ALineLongerThan8MiBIsDiscardedUnread`, keep the name, the log, the byte-length assertion, and `AssertNoCanary(result)`. Replace every statement between the byte-length assertion and `AssertNoCanary(result)` with exactly:

```csharp
Assert.Equal(0, result.MalformedLines);
Assert.Equal(2, result.SkippedEvents);
Assert.Empty(result.Diagnostics);
```

(They were `MalformedLines` 1, `SkippedEvents` 1, and one `OversizedLine` diagnostic at line 2. CS20 and CS27 now cover the malformed path.)

**Docs (`docs/cli.md`):**

- Report table row: `| Malformed lines | Lines that aren't one JSON event, are longer than 8 MiB, or are incomplete. A longer line counts as a skipped event instead when its first 64 KiB show a top-level \`type\` that isn't one of the events import reads, the same rule as for shorter lines |`
- In the JSON field list, change `` `skippedEvents` (events of other types) `` to `` `skippedEvents` (events of other types, including those longer than 8 MiB whose type shows in their first 64 KiB) ``.
- In "Repairs" or "The import report", wherever the docs say a log with malformed lines is read again, add no new text. The behavior there is unchanged.

**Acceptance:** CS5 passes with exactly the changed assertions above. All other existing tests pass unchanged. CS18–CS27, CX18, and OV1 pass. The reader's hash test vectors, if any, are unchanged. `git grep -n "Action<long, long> onOversized"` and `git grep -n "IgnoredOversizedTypes" -- src tests` find nothing, and `src/` names neither `model.model_call_success` nor `model.messages_snapshot`. `CopilotSessionLogSource.Types` is unchanged.

## Task 4: `premium_model_trivial_turn` Skips Routed Requests

**Files:**

- `src/CopilotLedger.Core/Audit/AuditSpan.cs`
- `src/CopilotLedger.Core/Audit/PremiumModelTrivialTurnRule.cs`
- `src/CopilotLedger.Storage/SqliteLedgerStore.Audit.cs`
- `docs/cli.md`
- `tests/CopilotLedger.Tests/Audit/VelocityRuleTests.cs`
- `tests/CopilotLedger.Tests/Cli/AuditCommandTests.cs`

No schema, CLI, or JSON change.

**Contracts:**

```csharp
// AuditSpan, after CostUsd
public decimal? ListCostUsd { get; init; }   // the rate card's price beside a reported charge; null: the same as CostUsd

// PremiumModelTrivialTurnRule
private const decimal RoutedChargeRatio = 0.95m;   // charged at least 5% below list: Auto (10%) or Fusion (30%) routed it
```

**Algorithm:**

1. **Store.** Append `list_cost_usd_pico` as the 26th column (index 25) of `ReadAuditSpansAsync`'s `SELECT`, after `ingest_source`. Set `ListCostUsd = rows.IsDBNull(25) ? null : PicoUsd.ToUsd(rows.GetInt64(25))`. No `COALESCE`.
2. **Rule.** In the candidate loop, after the existing `continue` conditions and before the tier lookup, add: `if (span.ListCostUsd is decimal list && list > 0m && span.CostUsd <= list * RoutedChargeRatio) continue;`. Nothing else changes: grouping, thresholds, metrics, impact (still from rates, not `CostUsd`), and severity stay the same.
3. Update `AuditSpan`'s and the rule's header comments in one line each to mention routed requests.

**Test vectors (`AR7` theory, new cases in `PremiumCases` and the `switch`).** Let `At` be the existing `"at"` expected string, `"warning [requests=10 requests, outputTokens=1200 tokens, modelOutputRatePer1M=20 usd_per_1m_tokens, topQuartileOutputRatePer1M=15 usd_per_1m_tokens] 0.273 premium_model_trivial_turn"`. `Trivial(n)` spans cost `0.0312m`.

| Case | Input | Expected |
|---|---|---|
| `routed` | `Trivial(10)` with every `ListCostUsd = 0.0347m` (0.95 × 0.0347 = 0.032965 ≥ 0.0312) | `none` |
| `one routed of ten` | `Trivial(10)`, the span whose ID ends in `5` with `ListCostUsd = 0.0347m` | `none` (9 left) |
| `one routed of eleven` | `Trivial(11)`, the span whose ID ends in `5` with `ListCostUsd = 0.0347m` | `At` (10 left; impact 10 × 0.0273 = 0.273) |
| `list equals charge` | `Trivial(10)` with `ListCostUsd = 0.0312m` | `At` |
| `list within 5%` | `Trivial(10)` with `ListCostUsd = 0.0328m` (0.95 × 0.0328 = 0.03116 < 0.0312) | `At` |
| `boundary skipped` | `Trivial(10)` with `CostUsd = 0.0285m, AiCredits = 2.85m, ListCostUsd = 0.0300m` (0.95 × 0.0300 = 0.0285) | `none` |
| `boundary kept` | `Trivial(10)` with `CostUsd = 0.0286m, AiCredits = 2.86m, ListCostUsd = 0.0300m` | `At` |
| `zero list` | `Trivial(10)` with `CostUsd = 0m, AiCredits = 0m, ListCostUsd = 0m` | `At` |

If `Trivial(11)`'s span IDs make more than one end in `5`, select by `SpanId == AuditFixtures.Span(5, …).SpanId` instead. If `one routed of eleven`'s impact isn't exactly `0.273`, stop and report it.

**CLI test `AC10_RoutedRequests_LeaveThePremiumRule` (`AuditCommandTests`):**

1. `AuditSeeds.CreateAsync(temp.Path)`, then the harness.
2. Run `audit --json --rule premium_model_trivial_turn` (P0) and `audit --json --rule failed_tool_retry --rule cache_miss_streak --rule context_bloat` (O0).
3. `LedgerSql.ExecuteAsync(db, "UPDATE spans SET list_cost_usd_pico = cost_usd_pico WHERE billing_channel = 'github_ai_credits'")`, then run P1.
4. `LedgerSql.ExecuteAsync(db, "UPDATE spans SET list_cost_usd_pico = cost_usd_pico * 10 / 9 WHERE billing_channel = 'github_ai_credits' AND cost_usd_pico > 0")`, then run P2 and O2.

Here `db` is `Path.Combine(temp.Path, "data", "ledger.db")`, as AC9 uses.

Expected:

- P0 and P1: `findingsTotal` 1.
- P2: `findingsTotal` 0 and exit 0.
- O2's `findings` array serializes identically to O0's (compare `GetRawText()` of `findings`).

**Docs (`docs/cli.md`, "The rules"):**

- Replace the `premium_model_trivial_turn` row's "Finds" cell with: `GitHub AI credits requests to a model in the top quarter of output prices, with 200 or fewer output tokens and no tool calls, per agent, model, and day, leaving out requests charged at least 5% below their list price`. The other cells are unchanged.
- Add this bullet right after the **Tool calls.** bullet:

  `- **Routed requests.** With Auto or Copilot Fusion, GitHub picks the model for each request and charges less than its list price: 10% less for Auto on paid plans, and 30% less for Fusion through November 30, 2026. gh-ledger knows a request's list price when Copilot reported its charge, so \`premium_model_trivial_turn\` leaves out requests charged at least 5% below their list price: you didn't choose their model. A routed request without a reported charge, or one charged at the list price, counts like any other.`

- The guide's audit samples don't change: the seeds' `list_cost_usd_pico` is NULL. `GuideSamplesTests` and `SprintThirteenDocsTests.DC1` must pass unchanged. If either fails, stop and report it.

**Acceptance:** all AR7 cases, AC1–AC10, AD1–AD3, and the guide sample tests pass, and no other audit test changes.

## Task 5: Version 0.6.0

Follow `docs/releasing.md` step 1 **exactly**, with `0.6.0`. Skip step 2: `rates.json` is unchanged, and so is its `ratesVersion`.

**Places to change, at `0f96a83`:**

- `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj` (`<Version>`, line 7)
- `src/CopilotLedger.Cli/GhLedgerCli.cs` (the comment, line 18; the fallback after `??`, line 23)
- `.github/workflows/ci.yml` (the smoke-test comparison, line 46)
- Tests: `DaemonCommandTests.DM1` (line 24), `ReportCommandTests.StatusText` (line 648), `ExportCommandTests.EC5` (line 118), `ExportBundleTests.EB2` (line 56)
- `docs/guide.md`: the `status-first` (line 53), `status` (line 169), and receipt (lines 536 and 547) samples
- `docs/cli.md`: the `"toolVersion"` manifest sample (line 398)
- `docs/releasing.md`: `git tag v0.6.0` and `git push origin v0.6.0` in step 4 (lines 26–27)

**Leave:**

- the mentions that step 1 and `VersionPlacesTests.Places` list: `which gh-ledger 0.5.0 builds in` (cli.md), `Upgrading from 0.5.0 or earlier keeps the rows you have` (configuration.md), `and gh-ledger 0.5.0 builds it in` (guide.md), and `` such as `v0.5.0-rc.1` `` (releasing.md);
- the comments in `GuideSamplesTests`, `ModelResolutionTests`, and `RatesUpdateTests`;
- the earlier daemon's `daemon.json` in `ImportCommandTests`;
- `docs/test-reports/` and `.github/plans/`.

Don't touch `VersionPlacesTests.cs` unless VR1–VR3 fail. If they do, stop and report it.

**Acceptance:**

- `git grep -n -E "(^|[^0-9.])0\.5\.0([^0-9]|$)" -- README.md docs/*.md .github/workflows src tests` lists only the "Leave" mentions.
- `git grep -n -E "(^|[^0-9.])0\.6\.0([^0-9]|$)" -- README.md docs/*.md .github/workflows src tests` lists exactly the "Places to change".
- `VersionPlacesTests`, `GuideSamplesTests`, and the four named tests pass.
- `git diff 0f96a83 -- src/CopilotLedger.Core/Pricing/rates.json global.json` is empty.

## Task 6: Release Provenance Attestations

**Files:**

- `.github/workflows/release.yml`
- `docs/releasing.md`
- `docs/cli.md`
- New `tests/CopilotLedger.Tests/Docs/ReleaseWorkflowTests.cs`

**Workflow (`release.yml`).** Change only the `release` job:

```yaml
  release:
    needs: [build, rates]
    if: github.event_name == 'push' && github.ref_type == 'tag'
    runs-on: ubuntu-latest
    permissions:
      contents: write
      id-token: write
      attestations: write
    steps:
      # checkout, download-artifact, and "Add the rates and checksums" unchanged
      - name: Attest the binaries and rates
        uses: actions/attest-build-provenance@4d101475d8b20a2381f78447822ac1eab6504dd8 # v4.2.2
        with:
          subject-path: |
            dist/gh-ledger-windows-amd64.exe
            dist/gh-ledger-windows-arm64.exe
            dist/gh-ledger-linux-amd64
            dist/gh-ledger-linux-arm64
            dist/gh-ledger-darwin-amd64
            dist/gh-ledger-darwin-arm64
            dist/rates.json
            dist/rates.v2.json
      # "Create the release" unchanged
```

Top-level `permissions: contents: read`, `on:` (including `workflow_dispatch:`), `concurrency`, and the `build` and `rates` jobs are unchanged. The new step goes after "Add the rates and checksums" and before "Create the release", so a failed attestation stops before any release exists.

**Docs (`docs/releasing.md`):**

- Step 3: replace "It never creates a release, even when it's started from a tag." with: `It never creates a release or an attestation, even when it's started from a tag: only the release job, which runs for a pushed tag, has the \`id-token: write\` and \`attestations: write\` permissions that attesting needs.`
- In the paragraph after step 4, change "and creates the release with these assets:" to: `attests the six binaries, \`rates.json\`, and \`rates.v2.json\` with \`actions/attest-build-provenance\`, and creates the release with these assets:`
- New section `## Verify a release`, before `## Update the pinned actions`:

  ````markdown
  ## Verify a release

  Each release's binaries, `rates.json`, and `rates.v2.json` have a build provenance attestation: a signed statement, stored by GitHub and recorded in Sigstore's public log, that the Release workflow of this repository built the file with that SHA-256 digest from the tagged commit. gh-ledger 0.5.0 and earlier have none. To check a release's assets:

  ```shell
  gh release download <tag> --repo swigerb/gh-ledger --dir gh-ledger-<tag>
  gh attestation verify gh-ledger-<tag>/gh-ledger-linux-amd64 --repo swigerb/gh-ledger --signer-workflow swigerb/gh-ledger/.github/workflows/release.yml --source-ref refs/tags/<tag>
  ```

  Repeat the second command for each asset. `gh-ledger-checksums.txt` isn't attested: it's computed from the attested files. The Windows binaries aren't signed with Authenticode.
  ````

**Docs (`docs/cli.md`, "Get gh-ledger").** After the paragraph that ends "keep working.", add:

````markdown
**Verify the binary.** Releases have a build provenance attestation for each binary, which shows that this repository's Release workflow built it. To check the one gh installed, run, with the path above for your platform:

```shell
gh attestation verify "<path to gh-ledger>" --repo swigerb/gh-ledger --signer-workflow swigerb/gh-ledger/.github/workflows/release.yml
```

It exits with 0 when an attestation matches the file's SHA-256 digest, and fails otherwise. gh-ledger 0.5.0 and earlier have no attestations, and the Windows binaries aren't signed with Authenticode. See [Verify a release](releasing.md#verify-a-release).
````

**Tests (`ReleaseWorkflowTests`, text only, no YAML package).** Reuse the `RepositoryFile` helper pattern from the other docs tests.

| ID | Name | Assertion |
|---|---|---|
| RW1 | `RW1_EveryActionIsPinnedToACommit` | For every `.github/workflows/*.yml`, every line whose trimmed text starts with `uses:` or `- uses:` matches `^\s*(- )?uses: [A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40} # v\d+\.\d+\.\d+$`, and at least 9 such lines exist in total. |
| RW2 | `RW2_TheReleaseJobAttestsTheBinariesAndRates_BeforeItReleases` | `release.yml`'s lines, trimmed: exactly one is `uses: actions/attest-build-provenance@4d101475d8b20a2381f78447822ac1eab6504dd8 # v4.2.2`. Its index is greater than that of `- name: Add the rates and checksums` and less than that of `- name: Create the release`. The eight lines after `subject-path: \|` are exactly the eight paths above, in order. |
| RW3 | `RW3_OnlyTheTagReleaseJobMayAttest` | Across all workflows, `id-token: write` and `attestations: write` each occur exactly once, in `release.yml`, in the block that follows `    permissions:` after `  release:`, whose three lines are exactly `      contents: write`, `      id-token: write`, `      attestations: write`. `release.yml`'s first `permissions:` block is `permissions:\n  contents: read\n`. It contains the line `    if: github.event_name == 'push' && github.ref_type == 'tag'` and the line `  workflow_dispatch:`. |
| RW4 | `RW4_TheDocsShowHowToVerify` | `docs/cli.md` and `docs/releasing.md` each contain `gh attestation verify ` and `--repo swigerb/gh-ledger --signer-workflow swigerb/gh-ledger/.github/workflows/release.yml`. `docs/releasing.md` contains `## Verify a release` and `--source-ref refs/tags/<tag>`. Both say `Authenticode`. |

**Acceptance:**

- RW1–RW4 pass.
- `git diff 0f96a83 -- .github/workflows/ci.yml .github/workflows/receipt.yml` shows only Task 5's version line.
- The new docs name `0.5.0` and never `0.6.0` (VR3 passes).
- Don't run the workflow; no push or tag.

## Task 7: 0.5.0 → 0.6.0 Upgrade Docs

**Files:** `docs/cli.md`, `docs/guide.md`, `tests/CopilotLedger.Tests/Docs/UpgradeDocsTests.cs`.

**Docs (`docs/cli.md`).** Insert this paragraph right after the `**Ledger format.**` paragraph and before "To upgrade, close the Copilot sessions…". It must come before the first fenced block (UD2), and the Ledger format line itself is unchanged except for Task 2's sentence (UD3):

`**Upgrading from 0.5.0.** gh-ledger 0.5.0 uses ledger format 6, and this version upgrades it to format 7 the first time it opens it, and 0.5.0 can't open it after that. Follow the steps below in order: close the Copilot sessions and apps that use the MCP server and stop the daemon, copy your data directory, upgrade, start the daemon, and run \`gh ledger import --all\` once. To go back, stop the daemon, run \`gh extension remove ledger\` and \`gh extension install swigerb/gh-ledger --pin v0.5.0\`, put the copy in place of your data directory, and start the daemon.`

After the existing `gh extension upgrade ledger` / `gh ledger daemon start` block, and before the overlay troubleshooting paragraph, add: `Then run \`gh ledger import --all\` once; see [Repairs](#repairs).` The sentence has no fenced block, so UD1's order still holds.

**Docs (`docs/guide.md`).** After the paragraph that follows the `gh ledger import --all` block ("It reads each session log again once…"), and before "To remove gh-ledger, undo `install`:", add:

`If you upgraded from gh-ledger 0.5.0 and need to go back, stop the daemon, run \`gh extension remove ledger\` and then \`gh extension install swigerb/gh-ledger --pin v0.5.0\`, put the copy you made in place of your data directory, and start the daemon. 0.5.0 can't open the ledger this version upgraded, and the copy doesn't have what gh-ledger recorded after you made it.`

**Test (`UpgradeDocsTests`):**

`UD5_TheUpgradeFrom050_CanBeRolledBackWithThePinAndTheCopy`:

1. Take the cli.md steps (`**Upgrading or removing the extension.**` … `## The daemon`):
   - exactly one line starts with `**Upgrading from 0.5.0.**`, and its index is greater than the `**Ledger format.**` line's and less than the first fenced line's;
   - that line contains `format 6`, `format 7`, `gh ledger daemon stop` *or* `stop the daemon`, `copy your data directory`, `gh ledger import --all`, and `gh extension install swigerb/gh-ledger --pin v0.5.0`.
2. In the guide steps (`## Upgrade or uninstall` … `To remove gh-ledger, undo \`install\`:`), a line containing `gh extension install swigerb/gh-ledger --pin v0.5.0` comes after the line `gh ledger import --all`.
3. Neither added text contains `0.6.0`.

**Acceptance:**

- UD1–UD5, VR1–VR3, `GuideSamplesTests`, and `ProductNameTests` pass.
- In `docs/cli.md`, the steps still read in this order: stop → copy → upgrade → start → import.

## Task 8: Import Benchmark Report

**Files:** new `docs/test-reports/<yyyy-MM-dd>-import-benchmark.md`, using the date the after-run is measured. `docs/test-reports/` is outside VR3's and PN2's scan, but write "gh-ledger" for the product all the same.

**Procedure:**

```powershell
dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r win-x64 -o "$env:TEMP\gh-ledger-bench-after"
pwsh -NoProfile -File tests/benchmarks/Measure-ImportSpeed.ps1 -Binary "$env:TEMP\gh-ledger-bench-after\gh-ledger.exe"
```

Use the same machine as Task 1 if possible, and say in the report whether it is.

**Report contents, in this order:**

1. Title `# Import speed benchmark`.
2. What was measured: the generated session (Records, the two 9 MiB lines of types import never reads, after record Records/2, Copilot 1.0.114 shape), and that only generated data was read, with temporary `COPILOT_HOME` and `COPILOT_LEDGER_HOME`.
3. Environment: OS, processors, .NET SDK (`dotnet --version`), and the before and after commits (`0f96a83` with Task 1's script, and this task's parent commit).
4. The command lines above.
5. The before table (from Task 1's commit body, verbatim) and the after table (verbatim), then a three-row comparison table `| Run | Before (s) | After (s) | Change |`.
6. Results against these targets, each marked met or not met:
   - after run 1: exit 0, recorded = Records, malformed lines 0, skipped events 2, and seconds ≤ 20% of before run 1;
   - after run 2: skipped unchanged 1, recorded 0, duplicate 0, exit 0;
   - after run 3: duplicate = Records, recorded 0, malformed lines 0, exit 0, seconds ≤ 10.

Record a missed target as not met. Don't tune code in this task. If a target is missed, report it in the commit body, and the architect will add a task.

**Acceptance:** the report exists with every section, and its after numbers come from one script run whose full output is also pasted into the commit body. No code changes.

## Task 9: Final Verification (no product change)

Run, in a clean tree:

1. `dotnet build CopilotLedger.slnx` must report 0 warnings and 0 errors. `dotnet test CopilotLedger.slnx` must report 0 failed and 0 skipped, with a passed count above 1,921. Record the count.
2. `dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r win-x64 -o "$env:TEMP\gh-ledger-final"`, then `& "$env:TEMP\gh-ledger-final\gh-ledger.exe" --version` must print exactly `0.6.0`. Use the matching RID on another OS. Then run `rates validate` with `COPILOT_LEDGER_HOME` set to a new temporary directory; it must exit 0.
3. Money: `git diff 0f96a83 -- src | Select-String -Pattern '\b(double|float)\b|REAL|Math\.Round'` finds no added line about money. The benchmark script is outside `src`.
4. Privacy:
   - `git diff 0f96a83 -- src` adds no column, no table, and no stored field;
   - Task 3 keeps nothing from a prefix;
   - CS18–CS27 and OV1 assert that the canary never appears.
5. Boundaries:
   - `git diff 0f96a83 --stat -- global.json src/CopilotLedger.Core/Pricing/rates.json` is empty;
   - `git diff 0f96a83 -- src/CopilotLedger.Storage/LedgerMigrations.cs` adds only `Indexes`;
   - no `.csproj` gains a `PackageReference`;
   - the `UsageAuthority.cs` diff is empty.
6. Contracts: the only public or internal contract changes are those in the global rules (compare `git diff 0f96a83 -- src`).
7. Docs:
   - `docs/configuration.md` is unchanged (no pricing configuration changed);
   - `docs/cli.md` describes the index, the dry-run note, the oversized rule, the routed rule, the verification command, and the 0.5.0 upgrade;
   - `docs/releasing.md` describes attestations and the manual-run rule;
   - no release notes file was added.
8. Check off this task, and commit with the results in the body.

## Review after Task 9 (2026-11-24, `ledger-architect`)

Reviewed `0f96a83..81d6bf9` (13 commits, 38 files) against this plan, issues #82, #80, and #77, and `.github/copilot-instructions.md`.

- `dotnet build CopilotLedger.slnx`: 0 warnings, 0 errors.
- `dotnet test CopilotLedger.slnx`: 1,960 passed, 0 failed, 0 skipped. That's 1,921 plus the 39 this sprint adds: LX 6, CS 17 (theory rows included), CX18, OV1, AR7 8, AC10, RW 4, UD5.
- A `win-x64` Release publish prints `0.6.0`, and `rates validate` exits 0 against an empty `COPILOT_LEDGER_HOME`.
- Re-checked:
  - F1: `v4.2.2` is still the latest release and still points to `4d10147…`. Its `create-storage-record` input applies only with `push-to-registry`, so the job needs no `artifact-metadata` permission.
  - F6 and F7: the Auto 10% and Fusion 30% (through November 30, 2026) text is unchanged on docs.github.com.
- No money `double`, `float`, `REAL`, or `Math.Round` was added. There's no new column, table, package, or `global.json` or `rates.json` change, and `UsageAuthority.cs` and `docs/configuration.md` are unchanged.
- The version places match VR3, and the benchmark report's numbers and percentages match the Task 1 and Task 8 commit bodies.
- Not a defect: `AuditQueryTests` gained `ListCostUsd = 0.0272m` in its expected record. Live rows store their list price, so the record's new property has to be in the expected value. That's not a weakened assertion.
- Not a defect: `JsonlLineReader`'s copy branch (`lineLength < PrefixBytes`) only runs when `maxLineBytes` is under 64 KiB, so no product path reaches it. With 8 MiB, the prefix is always already in `line`.

Findings that need a fix:

- **R1 (minor, test gap).** LX6 writes `Usage("u-1", "r-1")` and `Usage("u-2", "r-2")`. `UsageIdentity.NormalizeServiceRequestId` rejects those IDs (PA2), so both dry runs report `unsupportedUsageShapes` 2 and never call `UsageAuthority.GatherAsync`. "Decides the same" is therefore never tested, and that's the claim behind D3 and the `--dry-run` docs sentence ("its results are the same"). The planning probe (a scratch copy outside the repository, deleted afterward) found that with `R1` and `R2`, both runs exit 2 with `recordsParsed` 2, `unsupportedUsageShapes` 0, `ambiguousOverlap` 2, `recorded` 0, and one `ambiguousOverlap` diagnostic with count 2, and their stdout is identical. `ambiguousOverlap` comes from the indexed `canonical_usage_id IS NULL` live-span subquery. Fixed by Task 10.
- **R2 (minor, docs mismatch).** In `docs/cli.md`, the report table's Malformed lines row and the `skippedEvents` JSON note give the 64 KiB rule for every source. Codex's `OnOversized` ignores the prefix (CX18), so a Codex line longer than 8 MiB is always malformed. The plan's own Task 3 text caused this. Fixed by Task 11.

## Task 10: LX6 Reaches the Live-Span Lookups

**Files:** `tests/CopilotLedger.Tests/Storage/ConversationLiveIndexTests.cs` only. No product, docs, or other test change.

**Changes:**

1. Add `using System.Text.Json;`.
2. Add these private constants to `ConversationLiveIndexTests`, the same values as Task 3's:

   ```csharp
   private const string R1 = "11111111-1111-4111-8111-111111111111";
   private const string R2 = "22222222-2222-4222-8222-222222222222";
   ```

3. In `LX6_ADryRun_NeverCreatesTheIndex_AndDecidesTheSame`, change `.Usage("u-1", "r-1")` to `.Usage("u-1", R1)` and `.Usage("u-2", "r-2")` to `.Usage("u-2", R2)`. Keep the name, the setup, and every existing assertion: the index count is 0 after A and 1 after B, and exit codes and stdout are equal.
4. After the existing assertions, assert for **each** of `first` and `second`. A private static local function or helper, such as `AssertDeferred(CliRun run)`, is fine:
   - `run.ExitCode` is 2;
   - parse `run.Out` with `JsonDocument`, and take `sources[0]`:
     - `source` is `"copilot"`;
     - `recordsParsed` is 2;
     - `unsupportedUsageShapes` is 0;
     - `ambiguousOverlap` is 2;
     - `recorded` is 0;
   - root `diagnostics` has exactly one element, with `code` `"ambiguousOverlap"` and `count` 2.

If any of these values differs, stop and report it. Don't change the setup to make it pass.

**Acceptance:**

- LX6 passes with the new assertions. The test count stays 1,960.
- `git grep -n '"r-1"\|"r-2"' -- tests/CopilotLedger.Tests/Storage/ConversationLiveIndexTests.cs` finds nothing.
- `git diff HEAD~1 --stat` lists only `ConversationLiveIndexTests.cs` and this plan file. The plan file changes only to check off this task.

**Verification:** `dotnet build CopilotLedger.slnx` (0 warnings, 0 errors), then `dotnet test CopilotLedger.slnx` (0 failed, 0 skipped). Record `Tests: <n> passed` in the commit body.

## Task 11: The Oversized Rule Is Copilot's

**Files:** `docs/cli.md` only.

**Changes:**

1. Replace the report table's Malformed lines row with exactly:

   `| Malformed lines | Lines that aren't one JSON event, are longer than 8 MiB, or are incomplete. A longer Copilot line counts as a skipped event instead when its first 64 KiB show a top-level \`type\` that isn't one of the events import reads, the same rule as for shorter lines. A longer Codex line is always malformed |`

2. In the JSON field list, replace `` `skippedEvents` (events of other types, including those longer than 8 MiB whose type shows in their first 64 KiB) `` with `` `skippedEvents` (events of other types, including Copilot events longer than 8 MiB whose type shows in their first 64 KiB) ``.

No other text changes. The new text names no version, and the product is written "gh-ledger" wherever it's named.

**Acceptance:**

- `Select-String -Path docs/cli.md -SimpleMatch 'A longer Copilot line counts as a skipped event'` and `… -SimpleMatch 'including Copilot events longer than 8 MiB'` each find one line.
- `Select-String -Path docs/cli.md -SimpleMatch 'A longer line counts as a skipped event'` finds nothing.
- `git diff HEAD~1 --stat` lists only `docs/cli.md` and this plan file.
- Every docs test passes unchanged: `VersionPlacesTests`, `ProductNameTests`, `UpgradeDocsTests`, `GuideSamplesTests`, and `ReleaseWorkflowTests`.
- The test count stays 1,960.

**Verification:** `dotnet build CopilotLedger.slnx`, then `dotnet test CopilotLedger.slnx`. Record `Tests: <n> passed` in the commit body. After this task, hand back to `ledger-architect` for review mode.

## Tests added this sprint

| Task | File | Tests |
|---|---|---|
| 2 | `Storage/ConversationLiveIndexTests.cs` | LX1–LX6 |
| 3 | `Import/CopilotSessionGateTests.cs` | CS18–CS27 |
| 3 | `Import/CodexRolloutTests.cs` | CX18 |
| 3 | `Cli/ImportOversizedTests.cs` | OV1 |
| 4 | `Audit/VelocityRuleTests.cs` | AR7: 8 new cases |
| 4 | `Cli/AuditCommandTests.cs` | AC10 |
| 6 | `Docs/ReleaseWorkflowTests.cs` | RW1–RW4 |
| 7 | `Docs/UpgradeDocsTests.cs` | UD5 |

Changed, with assertions unchanged:

- the format-downgrade fixtures (Task 2);
- the four version tests (Task 5).

Changed assertions:

- `CS5_ALineLongerThan8MiBIsDiscardedUnread` (Task 3, PA2): an oversized `user.message` is now a skipped event.
- `LX6_ADryRun_NeverCreatesTheIndex_AndDecidesTheSame` (Task 10, R1): valid service request IDs, and assertions on what both dry runs decided. Nothing is loosened.

## After Task 11 (orchestrator)

Hand back to `ledger-architect` for review mode. Release notes for 0.6.0 aren't committed to the repository: the coordinator's final report drafts them from this plan and the Task 8 report. Don't push, tag, open a PR, or create a release from a builder run.

## Deferred to later sprints

- **S14-1.** `--dry-run` on a ledger without the index stays slow (D3). Making `LedgerSnapshot` use an in-memory temporary index, or the importer cache conversation facts in dry runs, is deferred.
- **S14-2.** Showing routed (Auto/Fusion) requests and their discount in reports, receipts, and MCP extends S12-2. So does telling a Fusion request from a chosen model after the Fusion discount ends (A3).
- **S14-3.** Moving from `actions/attest-build-provenance` to `actions/attest` (F2), and attesting `gh-ledger-checksums.txt` or an SBOM.
- **S14-4.** Authenticode signing of the Windows binaries (#77, out of scope; D8).
- **S14-5.** Confirming against a real 1.0.115 log, with consent, which event types grow past 8 MiB (A1), and widening the prefix if `type` is ever later than 64 KiB (A2).
- **Carried from Sprint 12** (see `.github/plans/archive/sprint-12.md`):
  - S12-1 to S12-8, and S12-10 to S12-12, unchanged;
  - S12-9 (profiling the duplicate path on large stores) is done by Tasks 1, 2, and 8.
- Everything Sprint 11 deferred, unchanged (see `.github/plans/archive/sprint-11.md`, "Deferred to later sprints").
