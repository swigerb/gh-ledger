# Sprint 11: Release Readiness & Version 0.5.0

| | |
|---|---|
| Architect | `ledger-architect` |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET 10 (`net10.0`), SDK 10.0.x, zero Docker |
| Plan written | 2026-11-24, against merged Sprint 10 (`main` at `5f4f4c5`) |
| Requirements | Issue [#72](https://github.com/swigerb/gh-ledger/issues/72), "Release gh-ledger 0.5.0", and the coordinator's scope addition of 2026-11-24: the product's name is gh-ledger |
| Status | Current sprint. Sprint 10 is complete and archived at `.github/plans/archive/sprint-10.md`, with its amendments. |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification section, runs the verification commands, changes the line to `- [x]`, and stops. Tasks 1–5 are sequential, and each builds on the ones before it.

This sprint ships as one pull request, and nobody in it tags, releases, or runs the Release workflow: the coordinator does that after the pull request merges. The orchestrator commits this plan first, together with `.github/plans/archive/sprint-10.md` and the product-name wording of `.github/copilot-instructions.md` and the two `.github/agents` files (see "Edits outside the tasks"), then each task as its own commit, `Sprint 11 Task N: <Title>`, on branch `swigerb-sprint-11-release-0-5-0`, and opens one pull request titled exactly "Sprint 11: Release readiness, gh-ledger naming & version 0.5.0", whose body says `Refs #72` and uses no closing keyword. After every task, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors (warnings are errors), and `dotnet test CopilotLedger.slnx` passes exactly the number of tests the task gives, with 0 failed and 0 skipped.

All of this plan's code, tests, and docs are **copy exactly**. The architect generated every block from the files at `5f4f4c5`, applying the edits in order, so each "replace" block is the text the file holds when you reach it. A script then applied the plan's blocks task by task, in order, to a fresh `git archive 5f4f4c5` with Windows line endings (as a `core.autocrlf=true` checkout has them), and built and tested after each task: 0 warnings, and 1732, 1732, 1733, 1734, and 1735 tests passed. Copy each block character for character, including every comment line, blank line, and line of Markdown. Don't add argument checks, logging, or fast paths that the code doesn't show, and don't tidy code or reword docs.

Each task's specification lists its files, says what changes and why, and then gives its code under "Code (copy exactly)". The summaries aren't code to write; the code section is. It has two kinds of blocks:

- **New file `<path>`:** write the block as the whole file, with `\n` line breaks, one `\n` at the end, and no byte order mark.
- **Edit k of m in `<path>`, replace: … with: …:** make a file's edits in the order given. Each "replace" block occurs exactly once in the file at the moment you make that edit, and no line before it is the block's first line, so search for it as an exact string. Blocks are shown with `\n` line breaks. The repository stores every text file with `\n` line breaks, but a `core.autocrlf=true` checkout gives them CRLF in the working tree, so match and write each block with the file's own line endings, and keep them; that includes `.github/workflows/ci.yml` (Task 5). None of the 33 files this sprint edits has a byte order mark. A block that holds Markdown fences is fenced with four backticks.

Tests are code too: write every test file and test edit exactly as given, in the order given. Don't drop an assertion because it looks redundant, and don't turn an exact comparison into `Contains` or `StartsWith`. Never normalize the product's output in a test; `CliHarness` already turns `\r\n` into `\n` in `Out` and `Error`. The only `ReplaceLineEndings("\n")` this sprint adds is in `VersionPlacesTests`, on repository files, whose line endings depend on the checkout.

Most of this sprint changes text that tests pin. A string in `src`, the tests that expect it, and the guide sample that shows it change in the same task, so every task builds and passes on its own. No task edits `.github/copilot-instructions.md`, `.github/agents/`, or `.github/plans/` beyond its own checkbox, and no task adds a package or a project item: new test files are picked up by the SDK's globs. No test opens a socket, starts a process, runs `git`, or reads a home directory.

Sprint 10's P28 ended a race in the suite for three tests; its fix at the source is still deferred. If a test fails only with `Dry run stopped: the ledger has a -wal or -shm file without the other, …`, run `dotnet test CopilotLedger.slnx` again; if it fails again, or anything else fails, stop and report it.

If a block doesn't apply, the build fails, or the test count differs from the task's, stop and report it. Don't change the plan's code to make it fit.

The final section, "Final step: the pull request", isn't a builder task. It has no checkbox, and `ledger-builder` never runs it.

## Tasks

- [x] Task 1: The Upgrade Steps Check the Rates Overlay (docs & tests). Release blocker U1: after an upgrade, a rates overlay that lists a model name the new release builds in, such as `claude-haiku-4-5-20251001`, which gh-ledger 0.4.1 users priced through an overlay for #67, is invalid, so the daemon doesn't start. The guide's and the reference's upgrade steps and the troubleshooting list say how to find and remove that entry, and a guide sample that the new GS8 checks shows `rates validate`'s exact message.
- [x] Task 2: gh-ledger in Command, Install, Status, and Report Text (CopilotLedger.Cli, docs & tests). The root command's description, the OTel environment warnings, `install` and `uninstall`, `status`'s warning about another installed copy, the `mcp` command's description, the `rates init` overlay's note, the Linux sign-in entry's name, and the HTML report's title and heading say gh-ledger, and so do the tests and guide samples that pin them.
- [ ] Task 3: gh-ledger in Alerts, MCP, and Receipts (CopilotLedger.Cli, CopilotLedger.Mcp, CopilotLedger.Otlp, docs & tests). Breaker and budget alerts, the MCP server's instructions, `set_branch_budget`'s description, MCP's `WARNING:` line, the receipt's heading, and `export-pr`'s hint, which names the PR receipt action; and `ProductNameTests.PN1`, which keeps CopilotLedger out of `src` except as an identifier.
- [ ] Task 4: gh-ledger in the README and the Docs (docs & tests). The README describes gh-ledger as a local AI-spend ledger for GitHub Copilot and an independent open-source project, not a GitHub product; the guide, the reference, the configuration page, and the release steps name the product gh-ledger and the action "the PR receipt action"; and `PN2` checks them.
- [ ] Task 5: Version 0.5.0, Where `docs/releasing.md` Lists It (CopilotLedger.Cli, CI, docs & tests). The project version, the fallback, CI's smoke test, the tests and samples that show the version, and the tag example move to 0.5.0; `docs/releasing.md` lists every place; and `VersionPlacesTests.VR1` checks the places that no other test reads.

## Starting point

- Sprint 10 is complete. Its plan and Tasks 1–16 merged in PR #71, squash-merged on 2026-11-24 as `5f4f4c5` from branch head `170ba4c`, which has the same tree, and CI run 45367441313 passed on `5f4f4c5`. At `5f4f4c5`, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors and `dotnet test CopilotLedger.slnx` passes 1731 tests (0 failed, 0 skipped), measured with SDK 10.0.115 in a `git archive 5f4f4c5` with Windows line endings. Sprint 10's plan is archived at `.github/plans/archive/sprint-10.md`, with its amendments; this plan's commit adds it.
- The latest release is v0.4.1, tagged at `15003fb` and published on 2026-11-22, with six binaries, `rates.json` (`schemaVersion` 1, `ratesVersion` 2026-11-20), and `gh-ledger-checksums.txt`, and no `rates.v2.json`. It reads ledger format 3. `main` migrates ledgers to format 6, and 0.4.1 then refuses them (`Database user_version 6 is newer than the supported version 3.`).
- Issue #72 is the requirements source. The coordinator's release checks recorded there all passed: live capture from Copilot CLI 1.0.114 over `http://localhost`, billed at exactly the AI credits Copilot reports, which confirms #67; import, export, audit, `status --cache`, the backfill, and all six MCP tools on two real 1.0.114 sessions; the upgrade of a ledger that the released 0.4.1 created, with identical totals, trips, and budgets; and a migration under a running 0.4.1 daemon, with no crash or corruption. The receipt smoke pull request, the Release workflow's dry run, the tag, and the asset checks are the coordinator's, after this pull request merges.
- The coordinator added the product's name to this sprint on 2026-11-24 (see "The product's name").
- What this plan depends on, read from the code at `5f4f4c5`:
  - **The version.** `<Version>` in `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`, with `IncludeSourceRevisionInInformationalVersion` false, is `GhLedgerCli.Version`, which `--version`, `status` (text and JSON), `daemon.json` and the daemon's log, receipts, the export's report, manifest, and generator tag, the MCP server's `serverInfo`, and the `User-Agent` of `rates update` and `audit --local-llm` read. The places that write the version are those that `docs/releasing.md` lists after Task 5; tests that read `GhLedgerCli.Version` or a `<version>` placeholder need no change.
  - **Guide samples.** `GuideSamplesTests` runs every `<!-- sample: id -->` of `docs/guide.md` in a `GuideWorld` state: GS1 checks the set of IDs; GS2 runs the command samples that `Commands` lists and expects exit code 0 and no standard error; and GS3–GS7 check the samples that `OtherSamples` lists.
  - **Overlays.** `JsonRateCardCalculator` refuses a merged card in which an overlay's model `id` or alias is a name that a built-in model of the same section already has: `duplicate model identity in the merged rates: <name> (built-in model <id>)` (`JsonRateCardCalculator.cs:593–681`). An overlay model with a built-in `id` replaces that model, aliases included. With such an overlay, `daemon run` logs `Invalid rates overlay: <message>` and exits 1 before it opens the ledger (`DaemonHost.cs:66–74`); `daemon start` then prints `gh-ledger daemon did not start; see <path of daemon.log>.` and exits 1 (`DaemonCommands.cs:115–117`); `rates validate`, `import`, and `audit` write `Invalid rates overlay: <message>` to standard error and exit 1; and `status` shows `Rates:   invalid overlay: <message>` and exits 0.
  - **The receipt action.** swigerb/copilot-ledger-action v1.0.0, which `receipt.yml` pins, finds a receipt by its marker line and its JSON block, never by its heading (see "Verified facts").

## Verified facts (retrieved 2026-11-24)

### GitHub's Copilot pricing (retrieved 2026-11-24 at 13:52 UTC)

[Models and pricing for GitHub Copilot](https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing), compared with the snapshot of 2026-11-20 in `.github/plans/archive/sprint-2.md` ("GitHub price table") and with `githubAiCredits` in `src/CopilotLedger.Core/Pricing/rates.json` (`ratesVersion` 2026-11-23, `source.retrievedOn` 2026-11-20):

- The page was read twice, from its Markdown rendering and by parsing its HTML. It has 48 text-model rows (OpenAI 20, Anthropic 13, Google 5, Microsoft 3, xAI 6, Moonshot AI 1), an empty "Fine-tuned (GitHub)" table, and one image model, GPT Image 2.5 Flare, which gh-ledger leaves out as before. Every row's tier, threshold, input, cached input, cache write, and output price equals the snapshot's.
- The promotions are unchanged: Gemini 3.8 Flash "through December 31, 2026"; Gemini 4 Argon and Gemini 4 Carbon "until December 1, 2026", and Carbon then 4.00 / 0.20 / 20.00; MAI Code 2 and MAI Code 2 Flash "until May 17, 2027", then 1.00 / 0.05 / 5.00 and 0.10 / 0.01 / 0.50.
- `rates.json` has all 34 models at these prices, tiers, and windows; Claude Opus 4.8 (fast mode) stays out because its model id is unknown. In the page's source, `github/docs`, the price table (`data/tables/copilot/models-and-pricing.yml`) last changed on 2026-11-18 and the page on 2026-11-17, both before the snapshot.
- So GitHub's prices, models, and promotion dates haven't changed since 2026-11-20, and this sprint doesn't touch `rates.json` or its `ratesVersion`. The API-equivalent list prices weren't researched again; Sprint 7 verified them on 2026-11-23.

### The release pipeline (checked 2026-11-24)

- For v0.4.1, the latest release, `gh release download --repo swigerb/gh-ledger --pattern rates.v2.json` exits 1 (`no assets match the file pattern`), and `--pattern rates.json` downloads `rates.json`: `schemaVersion` 1, `ratesVersion` 2026-11-20. So the Release workflow's `rates` job compares `main`'s card (`schemaVersion` 2, 2026-11-23) with that file, finds it changed, and passes, because `2026-11-23` sorts after `2026-11-20`. The job runs in a manual run too.
- The `release` job requires `schemaVersion` 2, which `main`'s card has, copies the card as `rates.v2.json`, and derives `rates.json` with `jq '.schemaVersion = 1 | del(.apiEquivalent)'`. The same derivation, made with `System.Text.Json`'s `JsonNode`, which keeps every number's text, and saved as `rates.downloaded.json`: a build of v0.4.1 (`15003fb`) says `using the downloaded rates (2026-11-23)`, and its `status` shows `Rates:   2026-11-23 (downloaded)`; `main` ignores the file silently, because it isn't later than its built-in 2026-11-23 rates. PR #68 checked the same with Sprint 7's card, which hasn't changed since.
- The `build` job requires the tag to be `v` and `<Version>`, and smoke-tests `--version` against `<Version>`. CI's smoke test compares `--version` with a literal, which Task 5 moves to 0.5.0. Nothing else in `release.yml`, `ci.yml`, or `receipt.yml` names the version.

### The receipt action (read 2026-11-24, at `d5eb3423731be103ef50f2720d27d661afc3d426`, v1.0.0)

`receipt.ps1`, read with `gh api` and not changed: it takes the pull request's changed `.md` files under `receipts-directory` (lines 149–157); requires each one's first line to be `<!-- copilot-ledger-receipt -->` (line 102), finds its `` ```json `` block (106–117), and reads `schemaVersion` 1 and `branch` from it (119–131); picks the receipt whose `branch` is the pull request's (167); and updates the first comment by `comment-author` whose body starts with the marker (180–191), or posts one. It never reads the heading, so `### gh-ledger receipt for` works with v1.0.0, and the next push replaces a comment that an earlier receipt left. The action's own messages and README still say "CopilotLedger receipt"; renaming it is deferred.

### GitHub's brand guidelines (retrieved 2026-11-24)

[github.com/logos](https://github.com/logos) redirects to [brand.github.com/foundations/logo](https://brand.github.com/foundations/logo), whose "Don't do these things" says: "Do not use the GitHub name or any GitHub logo in a way that suggests you are GitHub, your offering or project is by GitHub, or that GitHub is endorsing you or your offering or project," and "Do not use GitHub trademarks, logos, or artwork without GitHub's prior written permission."

### OTLP/HTTP retries (retrieved 2026-11-24)

[opentelemetry-proto `docs/specification.md`](https://github.com/open-telemetry/opentelemetry-proto/blob/main/docs/specification.md), "Retryable Response Codes": 429, 502, 503, and 504 are retryable; an overloaded server should answer 429 or 503 and may send `Retry-After`, which the client should honour.

### Copilot CLI (checked 2026-11-24)

`gh release list --repo github/copilot-cli`: 1.0.115 became the latest release on 2026-11-24 at 13:03 UTC. Its notes change no `gen_ai` span attribute that gh-ledger reads: a custom provider configured only by its base URL is now reported as provider `openai` instead of `github`, and exporters set a random `service.instance.id`. `gh ledger import` reads Copilot 1.0.103 to 1.0.114, so it reports 1.0.115 sessions as unsupported, with exit code 2, until fixtures verify them.

### Measured in scratch copies (2026-11-24)

- **U1's overlay.** With `main`'s build and an overlay whose `githubAiCredits.models[0].id` is `claude-haiku-4-5-20251001`: `daemon start` prints `gh-ledger daemon did not start; see <path of daemon.log>.` and exits 1, and `daemon.log` gets `Invalid rates overlay: <path>: githubAiCredits.models[0].id: duplicate model identity in the merged rates: claude-haiku-4-5-20251001 (built-in model claude-haiku-4.5)`; `rates validate`, `import`, and `audit` print that message and exit 1; `status` shows it on its `Rates:` line and exits 0; `branch` works. The built-in GitHub models of 0.4.1 have no aliases; `main`'s have 11 vendor and dated ids on ten models (`claude-opus-5-5`, `claude-sonnet-5-5`, `gpt-5.4-nano-2026-03-17`, `claude-haiku-4-5`, `claude-haiku-4-5-20251001`, `claude-haiku-5-5`, `claude-sonnet-4-20250514`, `claude-sonnet-4-6`, `claude-opus-4-8`, `claude-fable-5-1`, and `claude-fable-5-2`), any of which an overlay may list.
- **The backfill's write lock.** `rates backfill-api-equivalent` on a format 6 ledger with 200,000 spans without a result priced them all in 2.15 s, the process start included: well inside the daemon's 5-second busy timeout.

### Assumptions (not verified)

| # | Assumption | If it's wrong |
|---|---|---|
| A1 | Copilot CLI 1.0.115 exports the `gen_ai` attributes that 1.0.114 does, as its notes suggest | Its live spans could be priced differently; the coordinator's live check after the release would show it |
| A2 | A fork of a GitHub Copilot app session doesn't copy the parent's usage records, with their IDs, into the fork's own log (Sprint 8's A11; S8-13) | Importing such a fork could record a call again when the parent's live row of it has no canonical ID: a row that 0.4.1 recorded, or a BYOK span |

## Release triage

A release blocker, as #72 defines it, is (1) data loss or corruption; (2) wrong billing or double counting of newly recorded data; (3) a privacy leak, where prompt, response, or tool content is stored, logged, exported, or sent; (4) network access beyond what's documented; (5) a crash, or a failed start, upgrade, or migration; (6) a failure in the release pipeline; or (7) docs that would mislead someone upgrading.

The inputs are every item under "Deferred to later sprints" in `.github/plans/archive/sprint-6.md` through `sprint-10.md`, the risk notes in those archives' "Amendments during implementation", and the risk notes in the bodies of PRs #66 ("Known bias under D1", "Other notes") and #68–#71 ("Older binaries", "Risks", "Upgrade notes", "Risks and notes"). An amendment's or a pull request's note that repeats an archive's item points to that item. A "Carried from Sprint N" bundle is one row: none of its members meets a criterion through what Sprints 6–10 changed, and S6-11 discusses the one that touches upgrading.

**Result: 105 items, and one release blocker, U1 (P68-4): a rates overlay that names a model the new release builds in stops the daemon after an upgrade, and the upgrade steps don't say so. Task 1 fixes it. Every other item is resolved, or isn't a blocker for the reason its row gives.**

### The close calls

**(a) Old daemons after a migration (S6-10.2, S8-10, P69-3).** A 0.4.1 daemon writes with its own `INSERT`, which names only format 1 columns (`SqliteLedgerStore.cs:10–28` at `15003fb`), and checks the format only when it starts. So after a 0.5.0 command migrates the ledger (`LedgerMigrations.cs:118–210`), the rows it writes take the new columns' defaults: `legacy_unknown` cache states, even with nonzero counters; no `agent_span_id` or `pull_request_number`; no `api_*` result; and `ingest_source` `otlp`, `accounting_state` `effective`, and no `canonical_usage_id`, which is the shape migration 6 gives every earlier row, and no constraint or index rejects it. Billing: they're priced by 0.4.1's rates, as before the upgrade, and 0.5.0 never reprices billed columns. Double counting: a write-mode import stops while a daemon whose `daemon.json` lacks `importRules` holds the lock (`ImportCommands.cs:202–219`, D29), and after the restart those rows are unidentified live rows, so `UsageAuthority.Decide` defers their conversations' imports (`UsageAuthority.cs:41–103`, I2 and I3). Reports, budgets, and the breaker count each such row once; the backfill prices its API-equivalent columns like any earlier row; cache ratios count it as unreported (`ReportedSql`, `SqliteLedgerStore.cs:62`); and the audit and `--turns` leave it out of agent sequences. 0.4.1 can't start on a format 6 ledger. The coordinator ran this case: no crash or corruption, import refused until the restart, and the new binary stopped the old daemon. Only a 0.4.1 daemon started from a second, older binary in the instant between an import's D29 check and its migration escapes D29, and the calls it records would have to be ones that the import reads, which takes `--include-active`. Not a blocker.

**(b) Daemon writes on a busy ledger (S7-10, S8-19).** `RecordAsync` writes an OTLP request's spans in one `BEGIN IMMEDIATE` transaction with `ON CONFLICT (trace_id, span_id) DO NOTHING` (`SqliteLedgerStore.cs:10–36` and `172–235`). When it fails, as after SQLite's 5-second busy timeout and Microsoft.Data.Sqlite's retries, the transaction rolls back, and the receiver answers `503 Service Unavailable` with `Retry-After: 5` (`OtlpReceiverApp.cs:166–176`), which OTLP/HTTP defines as retryable; a retried request records each span once. So spans are lost only if the exporter gives up while another write holds the lock, which realistic work doesn't reach: the backfill holds it for its whole run (`SqliteLedgerStore.cs:243–252`) and priced 200,000 spans in 2.15 s, and the coordinator measured Sprint 8's import batches under 2 s for 20,000 records in one session. Copilot requests of a lost batch can still be imported from Copilot's session logs. Not a blocker.

**(c) U1: an overlay that names a new built-in model (P68-4), a release blocker.** 0.5.0's built-in GitHub models gain 11 vendor and dated ids as aliases (Sprint 7 Task 12, #67); 0.4.1's have none. An overlay that lists one of them as a GitHub model's `id` or alias is invalid in 0.5.0 (`JsonRateCardCalculator.cs:593–681`), and that is how a 0.4.1 user following 0.4.1's own fix for `unknown_model` ("Add the model, or an alias for it, under `githubAiCredits.models` or `foundryByok.models`") priced Claude Haiku 4.5 requests from Copilot CLI 1.0.114. After the documented upgrade, `gh ledger daemon start` prints `gh-ledger daemon did not start; see <path of daemon.log>.` and exits 1, the daemon at sign-in fails the same way, and nothing is recorded until the overlay is fixed (`docs/cli.md`: "Copilot's telemetry is lost while no daemon is running"). That's a failed start after an upgrade (criterion 5), for exactly the users who worked around the bug that 0.5.0 fixes. gh-ledger reports it precisely: `daemon.log`, `status`, and `rates validate` name the file, the entry, and the built-in model, and `docs/configuration.md` says to remove such a name. But the upgrade steps in `docs/guide.md` and `docs/cli.md` present stop, upgrade, and start as complete (criterion 7). Changing the merge rule, or starting without the overlay, would change pricing, which a release sprint shouldn't, so Task 1 completes the upgrade steps and the troubleshooting list, and a guide sample pins the message. The release notes should say it too.

**(d) Repository slugs from before Sprint 8's strict rule (S9-10).** gh-ledger 0.4.1's `GitRemoteUrl.GetRepositorySlug` (at `15003fb`) took a URL's path without removing a query or a fragment, so a remote such as `https://github.com/o/r?token=…` gave the slug `o/r?token=…`; a credential in the user info was never part of it. Since Sprint 8, the slug is the path without its query or fragment and must be a strict `owner/repo` (`GitRemoteUrl.cs:31–86`), so 0.5.0 records no new such slug. An old one stays in its rows, and 0.5.0 shows it as stored in reports, MCP results, and exports (`ExportRecords.cs:101`), so an export of every repository could put it in a file that someone shares; the local model's payload never has it (`AuditLlmPayload.cs:124` writes `repo-N`). 0.4.1 already showed such a slug in reports, returned it to Copilot through MCP, and wrote it in receipts, which are committed and posted. Git's own authentication never puts a credential in a query, so the case is rare, and a credential isn't prompt, response, or tool content (criterion 3). Not a blocker; a migration that clears non-strict slugs stays deferred.

**(e) Fork copies beside unidentified live rows (S8-13).** `UsageAuthority.Decide` (`UsageAuthority.cs:41–63`) records an imported call with a canonical ID that no row has unless its conversation has an unidentified live row, and a call without one (a BYOK call) unless its conversation has a live row. A fork of a session is a new conversation, so if the GitHub Copilot app's fork copied the parent's usage records, with their IDs, into the fork's log, the copies of calls that only unidentified live rows recorded would be recorded too: rows that 0.4.1 recorded, or BYOK spans. Copies of imported calls are deduplicated by their record aliases, and copies of calls that 0.5.0's daemon recorded with a service request ID are overlap-skipped. Nothing shows that forks copy usage records (assumption A2), and the coordinator's imports of two real sessions recorded nothing twice. Not a blocker; the census stays deferred, and the pull request names the risk.

**(f) The upgrade docs.** Checked against 0.5.0: format 6 is one-way, and `docs/cli.md` "Ledger format" says so, quotes 0.4.1's error, and says to copy `ledger.db` first; the stop–upgrade–start steps restart the daemon, and `import` refuses an older daemon and says how to restart it (`docs/cli.md` "Import past sessions"); `rates backfill-api-equivalent` is optional, and reports mark what it would fill with `*` and a footnote (`docs/cli.md` "API-equivalent USD"); `rates update` on 0.4.1 downloads the latest release's `rates.json`, which 0.5.0's release derives and 0.4.1 accepts, while 0.5.0 downloads `rates.v2.json` (`docs/configuration.md` "Newer rates between releases", and (g)); a `rates.downloaded.json` that 0.4.1 saved is ignored, silently when it isn't later than 0.5.0's built-in rates, which covers every download of 2026-11-23 or earlier, and with the documented warning when a later `schemaVersion` 1 file is (`RateCardFiles.cs:52–60`); and `gh extension upgrade ledger` is the documented command. The one gap is U1.

**(g) The release pipeline.** As "Verified facts" says: the `rates` job passes for `main`'s card (2026-11-23 after 2026-11-20), the `release` job's derivation gives a card that 0.4.1 accepts and 0.5.0 ignores, and the `build` job's tag and smoke checks read `<Version>`. Only CI's smoke test holds a literal version, which Task 5 moves. Nothing else needs to change for v0.5.0.

### Every item

Criteria are numbered as above; "—" means that none applies, as for a feature.

| ID | Source | Item | Criteria | Classification | Reason |
|---|---|---|---|---|---|
| S6-1 | `archive/sprint-6.md:8913` | Migration numbers for #61 and #63 | 5 | Resolved | Sprints 7 and 8 took formats 5 and 6; a 0.4.1 ledger migrates from 3 to 6 in order, as the coordinator checked |
| S6-2 | `archive/sprint-6.md:8914` | #61: pricing of absent cache counters | 2 | Resolved | Sprint 7 kept the pricing and metrics rules apart |
| S6-3 | `archive/sprint-6.md:8915` | #63: `cacheDetailsReported` | 2 | Resolved | Sprint 8 (D28) |
| S6-4 | `archive/sprint-6.md:8916` | #64: the contracts to chart | — | Resolved | Sprint 9 |
| S6-5 | `archive/sprint-6.md:8917` | #65: cache-miss streaks | — | Resolved | Sprint 10 |
| S6-6 | `archive/sprint-6.md:8918` | User turns | — | Not a blocker | A feature that needs attributes captured first |
| S6-7 | `archive/sprint-6.md:8919` | Agent definitions (`gen_ai.agent.id`) | — | Not a blocker | A feature |
| S6-8 | `archive/sprint-6.md:8920` | MCP tools for turns and bloat | — | Not a blocker | A feature; the CLI has them |
| S6-9 | `archive/sprint-6.md:8921` | A fresh capture of cache attributes | 2 | Not a blocker | D1's assumption affects cache ratios, not billing; the coordinator's 1.0.114 capture billed exactly Copilot's credits |
| S6-10.1 | `archive/sprint-6.md:8922`, (1) | `status --cache`'s footnote always mentions `*` | — | Not a blocker | Wording |
| S6-10.2 | `archive/sprint-6.md:8922`, (2) | A pre-format-4 daemon running after a migration | 1, 2, 5 | Not a blocker | See (a) |
| S6-10.3 | `archive/sprint-6.md:8922`, (3) | Growth after the period filter, deltas before it | — | Not a blocker | A report's behavior at the period's start |
| S6-10.4 | `archive/sprint-6.md:8922`, (4) | `GetTurnBreakdownAsync` can't seek its indexes | — | Not a blocker | 29 ms over 160,000 spans |
| S6-10.5 | `archive/sprint-6.md:8922`, (5) | `get_cache_efficiency`'s description lacks Task 12's caveat | — | Not a blocker | A tool's help text |
| S6-10.6 | `archive/sprint-6.md:8922`, (6) | Subagent conversation IDs only from their own spans | — | Not a blocker | Attribution in `--turns --conversation` |
| S6-11 | `archive/sprint-6.md:8923` | Carried from Sprint 5: its 19 items (`archive/sprint-5.md:3945–3963`) | 5, 6, 7 | Not a blocker | Unchanged by Sprints 6–10. The release-pipeline items (lockfiles, build provenance, Arm64 smoke tests) don't make it fail. Upgrading without stopping everything by hand: the stop–upgrade–start steps stay documented, Task 1 completes them, and (a) covers a daemon left running |
| S7-1 | `archive/sprint-7.md:13037` | #63 guidance | — | Resolved | Sprint 8 |
| S7-2 | `archive/sprint-7.md:13038` | #64 guidance | — | Resolved | Sprint 9 |
| S7-3 | `archive/sprint-7.md:13039` | #65 guidance | — | Resolved | Sprint 10, with the billed lens; the API view is S10-2 |
| S7-4 | `archive/sprint-7.md:13040` | Re-pricing unpriced rows | 2 | Not a blocker | API-equivalent amounts are a comparison, never billed, and unpriced rows are marked |
| S7-5 | `archive/sprint-7.md:13041` | MAI list prices; GPT-5.6 Sol after 2026-11-30 | 2 | Not a blocker | No vendor publishes them; the API lens only |
| S7-6 | `archive/sprint-7.md:13042` | Re-billing rows recorded as `unknown_model` | 2 | Not a blocker | Rows that 0.4.1 recorded before the fix, not new data; billed columns are never rewritten, as PR #68 says |
| S7-7 | `archive/sprint-7.md:13043` | More dated ids | 2 | Not a blocker | The request-model fallback bills them when the request model is known, and a rates update can add aliases |
| S7-8 | `archive/sprint-7.md:13044` | Longer cache lifetimes | 2 | Not a blocker | The API lens only |
| S7-9 | `archive/sprint-7.md:13045` | A `--pricing` sample in the guide | 7 | Not a blocker | Not about upgrading; the guide describes the option |
| S7-10 | `archive/sprint-7.md:13046` | The backfill: discoverability, wording, and write lock | 1, 5, 7 | Not a blocker | See (b); unpriced rows are marked `*` with a footnote, and the docs name the command |
| S7-11 | `archive/sprint-7.md:13047` | What `api_rate_card_sha256` covers | 2 | Not a blocker | The provenance of a comparison |
| S7-12 | `archive/sprint-7.md:13048` | What-if fidelity | 2 | Not a blocker | Documented since Sprint 7 Task 14; changes no billed amount |
| S7-13 | `archive/sprint-7.md:13049` | Carried from Sprint 6 | — | Not a blocker | The S6 rows |
| S8-1 | `archive/sprint-8.md:17058` | A Claude Code adapter | — | Not a blocker | A feature; `--source claude` is refused |
| S8-2 | `archive/sprint-8.md:17059` | Copilot BYOK through local OpenAI-compatible servers | 2 | Not a blocker | Imported as `other_provider`: unpriced, never mispriced, as documented |
| S8-3 | `archive/sprint-8.md:17060` | Wider version windows | 2, 5 | Not a blocker | Later versions fail closed: unsupported, exit code 2, never a wrong charge. Copilot CLI 1.0.115 became the latest release today (Risks) |
| S8-4 | `archive/sprint-8.md:17061` | Compactions of more than one request; Codex `.jsonl.zst` and tool calls | 2 | Not a blocker | Unsupported shapes, skipped and reported |
| S8-5 | `archive/sprint-8.md:17062` | VS Code and JetBrains stores | — | Not a blocker | A feature |
| S8-6 | `archive/sprint-8.md:17063` | Re-scanning and incremental parsing | — | Not a blocker | A feature |
| S8-7 | `archive/sprint-8.md:17064` | Agent-invocation grouping for imported spans | — | Not a blocker | Reports show `n/a`, as documented |
| S8-8 | `archive/sprint-8.md:17065` | Coverage notes in reports | — | Not a blocker | A feature; import and export report coverage |
| S8-9 | `archive/sprint-8.md:17066` | #64 and #65 guidance | — | Resolved | Sprints 9 and 10 |
| S8-10 | `archive/sprint-8.md:17067` | Old daemons | 1, 2, 5 | Not a blocker | See (a) |
| S8-11 | `archive/sprint-8.md:17068` | Unmatched supersession keeps coverage incomplete | — | Not a blocker | Reporting; such rows never count again |
| S8-12 | `archive/sprint-8.md:17069` | Carried from Sprint 7 | — | Not a blocker | The S7 rows |
| S8-13 | `archive/sprint-8.md:17070` | Fork copies beside unidentified live rows | 2 | Not a blocker | See (e); the premise is unverified (A2) |
| S8-14 | `archive/sprint-8.md:17071` | Matching superseded rows later | — | Not a blocker | The accuracy of a coverage count |
| S8-15 | `archive/sprint-8.md:17072` | Messages of unexpected exceptions | 3 | Not a blocker | Only gh-ledger's own refusals throw `InvalidOperationException`, and their messages hold no content |
| S8-16 | `archive/sprint-8.md:17073` | Producer versions in warnings | 3 | Not a blocker | A validated version string, never content |
| S8-17 | `archive/sprint-8.md:17074` | A dry run beside a running daemon | 1 | Not a blocker | It opens the ledger read-only; the review found all three files unchanged |
| S8-18 | `archive/sprint-8.md:17075` | The cost of the unchanged check | — | Not a blocker | Performance |
| S8-19 | `archive/sprint-8.md:17076` | Large single sessions import in quadratic time | 1, 5 | Not a blocker | See (b) |
| S9-1 | `archive/sprint-9.md:14936` | A hash-based Content Security Policy | 3, 4 | Not a blocker | `default-src 'none'` loads nothing, and output encoding is the XSS defense |
| S9-2 | `archive/sprint-9.md:14937` | The audit findings section | — | Resolved | Sprint 10 |
| S9-3 | `archive/sprint-9.md:14938` | Coverage notes in reports, receipts, and MCP | — | Not a blocker | A feature |
| S9-4 | `archive/sprint-9.md:14939` | IANA IDs on Windows 10 1703–1809 | 5 | Not a blocker | A documented error for one option; Windows IDs work |
| S9-5 | `archive/sprint-9.md:14940` | Every prompt chain, night hours, PDF or PNG | — | Not a blocker | Features |
| S9-6 | `archive/sprint-9.md:14941` | The ZIP container's bytes across operating systems | — | Not a blocker | Documented; the entries are identical |
| S9-7 | `archive/sprint-9.md:14942` | Failed tool calls share the sixth model's color | — | Not a blocker | Legibility |
| S9-8 | `archive/sprint-9.md:14943` | Same-named local repositories | — | Not a blocker | Display |
| S9-9 | `archive/sprint-9.md:14944` | A record of the daemon's deletions | — | Not a blocker | The precision of tool history; needs a schema change |
| S9-10 | `archive/sprint-9.md:14945` | Repository slugs recorded before Sprint 8's strict rule | 3 | Not a blocker | See (d) |
| S9-11 | `archive/sprint-9.md:14946` | Axis lines at every zoom | — | Not a blocker | Cosmetic |
| S9-12 | `archive/sprint-9.md:14947` | A crash mid-swap leaves only `<zip>.<pid>.bak` | 1 | Not a blocker | The earlier ZIP survives as the `.bak`, and only a killed process gets there |
| S9-13 | `archive/sprint-9.md:14948` | Requests at the very ends of the axis | — | Not a blocker | Cosmetic |
| S10-1 | `archive/sprint-10.md:11856` | Threshold options | — | Not a blocker | A feature |
| S10-2 | `archive/sprint-10.md:11857` | An API-equivalent view of impacts | — | Not a blocker | A feature |
| S10-3 | `archive/sprint-10.md:11858` | Sanitizing ledger strings in the other reports' tables | 3 | Not a blocker | Names, not content, printed as 0.4.1 printed them |
| S10-4 | `archive/sprint-10.md:11859` | Streaming answers | — | Not a blocker | A feature |
| S10-5 | `archive/sprint-10.md:11860` | Tool sequences older than 7 days | — | Not a blocker | A feature |
| S10-6 | `archive/sprint-10.md:11861` | Import diagnostics per trace (A2 of Sprint 10) | — | Not a blocker | The attribution of an audit heuristic; import warns |
| S10-7 | `archive/sprint-10.md:11862` | Compactions in the ledger (P29) | — | Not a blocker | An audit heuristic |
| S10-8 | `archive/sprint-10.md:11863` | A sortable findings table | — | Not a blocker | A feature |
| S10-9 | `archive/sprint-10.md:11864` | The suite's pool-clearing race | 6 | Not a blocker | P28 fixed the three tests that hit it, a CI rerun clears it, and the Release workflow runs no tests |
| P66-1 | PR #66, "Known bias under D1" | Hit ratios too high for providers that never report cache writes | — | Not a blocker | A documented metric bias; billing is unaffected |
| P66-2 | PR #66, "Other notes" 1 | Pull requests only from `refs/pull/<n>/head` | — | Not a blocker | A documented attribution limit |
| P66-3 | PR #66, "Other notes" 2 | A turn is one request | — | Not a blocker | S6-6 |
| P66-4 | PR #66, "Other notes" 3 | The review's six follow-ups | — | Not a blocker | S6-10.1–S6-10.6 |
| P68-1 | PR #68, "Older binaries" | 0.4.1 refuses a `schemaVersion` 2 card and accepts the derived `rates.json` | 6 | Not a blocker | Checked again today with a build of 0.4.1 and the workflow's derivation; see (g) |
| P68-2 | PR #68, "Risks" 1 | No downgrade after the new daemon runs | 5, 7 | Not a blocker | `docs/cli.md` "Ledger format" says so and how to keep a copy; 0.4.1 refuses the ledger with a clear message |
| P68-3 | PR #68, "Risks" 2 | Older spans unpriced in the API lens until the backfill | 7 | Not a blocker | Marked `*` with a footnote; the docs name the backfill |
| P68-4 | PR #68, "Risks" 3 | An overlay that lists a new built-in alias fails with `duplicate model identity` | 5, 7 | **Blocker U1** | See (c); Task 1 |
| P68-5 | PR #68, "Risks" 4 | `unknown_model` rows keep 0 AI credits | 2 | Not a blocker | S7-6 |
| P68-6 | PR #68, "Risks" 5 | The Release workflow's version 2 steps run only on a tag, and the first release needs the later `ratesVersion` | 6 | Not a blocker | See (g) |
| P68-7 | PR #68, "Risks" 6 | MAI models and local requests unpriced in the API lens | — | Not a blocker | Documented; a comparison |
| P69-1 | PR #69, "Upgrade notes" 1 | Earlier versions can't open format 6 | 5, 7 | Not a blocker | P68-2 |
| P69-2 | PR #69, "Upgrade notes" 2; `archive/sprint-8.md` amendment 3 | Migration 6 was edited within Sprint 8 | 5 | Not a blocker | Only unreleased builds of Sprint 8's Tasks 2–13 wrote the earlier format 6; no release or `main` build did |
| P69-3 | PR #69, "Upgrade notes" 3 | Restart the daemon after upgrading | 2, 7 | Not a blocker | The documented steps restart it, and `import` refuses an older daemon and says how; see (a) |
| P69-4 | PR #69, "Upgrade notes" 4 and "Risks and notes" 1 | Supported versions; later ones fail closed | 2 | Not a blocker | S8-3 |
| P69-5 | PR #69, "Risks and notes" 2 | `github.copilot.service_request_id` is verified by capture only | 2 | Not a blocker | A span without it is unidentified, so its session's imports are deferred, never counted twice; the coordinator's 1.0.114 capture had it |
| P69-6 | PR #69, "Risks and notes" 3 | The Unix branch of the denied-folder tests | — | Resolved | CI run 45304384564 passed |
| P69-7 | PR #69, "Risks and notes" 4 | Strict `owner/repo` slugs | 3 | Resolved | A hardening; older slugs are S9-10 |
| P69-8 | PR #69, "Risks and notes" 5 | Large single sessions | 1, 5 | Not a blocker | S8-19 |
| P69-9 | PR #69, "Risks and notes" 6 | A year-0001 timestamp | 5 | Resolved | Sprint 8 Task 13 |
| P70-1 | PR #70, "Risks and notes" 1 | IANA zones on Linux | 5 | Resolved | CI run 45341892607 passed |
| P70-2 | PR #70, "Risks and notes" 2 | Windows before 10 1903 has no `icu.dll` | 5 | Not a blocker | S9-4 |
| P70-3 | PR #70, "Risks and notes" 3 | EC14 on Linux | — | Resolved | CI run 45341892607 passed |
| P70-4 | PR #70, "Risks and notes" 4 | The ZIP container across operating systems | — | Not a blocker | S9-6 |
| P70-5 | PR #70, "Risks and notes" 5 | No query-plan tests | — | Not a blocker | Performance; the results are pinned |
| P70-6 | PR #70, "Risks and notes" 6; `archive/sprint-9.md` amendment 5 | One unidentified intermittent failure | 6 | Not a blocker | Probably S10-9's race |
| P70-7 | PR #70, "Risks and notes" 7 | Sprint 10's alignment on compactions | — | Resolved | Sprint 10 (P29) |
| P70-8 | PR #70, "Risks and notes" 8 | Compactions that an unreleased build imported keep no count | 2 | Not a blocker | Conservative: unknown tool data; no release had that reader |
| P71-1 | PR #71, "Risks and notes" 1 | Unnamed imported sub-agents join the main agent | — | Not a blocker | S10-6 |
| P71-2 | PR #71, "Risks and notes" 2 | A tool span exported after its agent span | — | Not a blocker | The next audit corrects the count |
| P71-3 | PR #71, "Risks and notes" 3 | A short compaction on a premium model | — | Not a blocker | S10-7 |
| P71-4 | PR #71, "Risks and notes" 4 | Heuristics flag legitimate patterns | — | Not a blocker | Documented as review signals |
| P71-5 | PR #71, "Risks and notes" 5 | Agent and tool names reach a local model | 3, 4 | Not a blocker | Names, not content, capped and stripped of controls; opt-in, loopback only, and previewable with `--show-llm-payload`, as documented |
| P71-6 | PR #71, "Risks and notes" 6 | Foundry Local's acceptance of the request is unverified | — | Not a blocker | An optional feature, whose failure is a warning |
| P71-7 | PR #71, "Risks and notes" 8 | No version bump, tag, or release | 6 | Resolved | Task 5; the coordinator tags and releases |

## The product's name

The coordinator added this to the sprint on 2026-11-24, after the user asked:

> With the repo named "gh-ledger" should we rename this to "GitHub Ledger" or "GitHub Copilot Ledger"? "CopilotLedger" seems incorrect now. If we do so, we need to update all documentation references.

The user was unavailable, so the coordinator decided; the user can revisit it in the pull request.

**Decision:** the product's display name is **gh-ledger**, in documentation and in user-visible text. "CopilotLedger" stops being the product's name.

**Why:** both proposed names follow GitHub's own product naming, like "GitHub Copilot", so they would read as official GitHub products, which GitHub's brand guidelines ask projects not to suggest (see "Verified facts"). The tool also covers Codex, Foundry BYOK, and local models now, and gh-ledger matches the repository, the binary, and `gh extension install swigerb/gh-ledger`.

**Rules:**

1. gh-ledger is the product's name in prose. Commands stay `gh ledger <command>`, and the binary `gh-ledger`, under the docs' existing naming rule. The product is "a local AI-spend ledger for GitHub Copilot", which states compatibility, not affiliation, and the README says that gh-ledger is an independent open-source project, not a GitHub product.
2. What changes: README.md and `docs/*.md` (Task 4); user-visible strings in `src`, with the tests and guide samples that pin them (Tasks 2 and 3); and the product-name wording of `.github/copilot-instructions.md` and the two `.github/agents` files, with no rule, model, tool, or handoff changed (the plan commit).
3. What doesn't, because existing installs, data, and the published action rely on it: the C# namespaces, projects, and solution (`CopilotLedger.*`, `CopilotLedger.slnx`); `~/.copilot-ledger` and `COPILOT_LEDGER_HOME`; `.github/copilot-ledger/receipts/` and `<!-- copilot-ledger-receipt -->`; the sign-in items' names (the Windows Run value `CopilotLedger`, the launchd label `io.github.swigerb.copilot-ledger`, and `copilot-ledger.desktop`); the shell-profile markers `# >>> copilot-ledger >>>` and `# <<< copilot-ledger <<<`; notify-send's app name `CopilotLedger`, which notification daemons use for per-app settings and history, and the Windows toast's tag, `copilot-ledger`; the MCP registration name `ledger` and the MCP server's `serverInfo` name, `copilot-ledger`; and everything under `.github/plans/archive/`. Docs that name one of these keep its literal value. The Linux autostart entry's `Name` and `Comment` are what desktop settings show, so they change (Task 2); `install` rewrites an entry that an earlier version wrote, because it compares the whole file, and `uninstall` removes it by path either way.
4. The receipt action keeps its published name, swigerb/copilot-ledger-action ("CopilotLedger PR Receipt"), because renaming it takes a release of that repository. Prose calls it "the PR receipt action", with its link. The receipt's new heading works with the action as it is (see "Verified facts"). Renaming the action and its Marketplace listing is deferred.

### Edits outside the tasks

`ledger-builder` never edits `.github/copilot-instructions.md` or `.github/agents/`, so these wording changes are in the plan's commit. `ledger-architect` made the first two; it doesn't edit its own agent definition, so the orchestrator makes the third before committing.

| File | Line | Before | After |
|---|---:|---|---|
| `.github/copilot-instructions.md` | 1 | `# CopilotLedger: Copilot instructions` | `# gh-ledger: Copilot instructions` |
| `.github/copilot-instructions.md` | 3 | CopilotLedger (`gh-ledger`) is a free, local-first .NET 10 utility. … | gh-ledger is a free, local-first .NET 10 utility. … |
| `.github/agents/ledger-builder.agent.md` | 3 | `description: "CopilotLedger builder. Implements …"` | `description: "gh-ledger builder. Implements …"` |
| `.github/agents/ledger-builder.agent.md` | 20 | … the implementation agent for CopilotLedger (`gh-ledger`). Follow … | … the implementation agent for gh-ledger. Follow … |
| `.github/agents/ledger-architect.agent.md` | 3 | `description: "CopilotLedger architect. Designs …"` | `description: "gh-ledger architect. Designs …"` |
| `.github/agents/ledger-architect.agent.md` | 15 | … the design and review agent for CopilotLedger (`gh-ledger`). Follow … | … the design and review agent for gh-ledger. Follow … |

### What still says CopilotLedger

After Task 5 and the plan commit, these 31 lines outside `.github/plans/` say CopilotLedger, in any case, other than as `CopilotLedger.<name>` (a namespace, a project, or the solution). This plan quotes the old text in its replace blocks and in this section; it becomes `archive/sprint-11.md`.

| File | Lines | Why it stays |
|---|---|---|
| `docs/cli.md` | 36 | Names the Windows Run value, `CopilotLedger`, by its literal value |
| `src/CopilotLedger.Cli/Install/InstallCommands.cs` | 219, 247, 357, 371 | The Run value's name, which `install` writes and checks and `uninstall` removes |
| `src/CopilotLedger.Cli/Install/StartupItem.cs` | 14 | The Run value's registry path, which `install.json` records |
| `src/CopilotLedger.Cli/Alerts/DesktopNotifier.cs` | 80 | notify-send's `--app-name`, which notification daemons use for per-app settings and history |
| `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs` | 48, 65, 107, 124, 385, 694, 776 | Pin the Run value and its path |
| `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs` | 629 | The test name `IN15_CopilotLedgerHomeWarnsBeforeRegistration`, after `COPILOT_LEDGER_HOME` |
| `tests/CopilotLedger.Tests/Cli/AlertDaemonTests.cs` | 276 | Pins notify-send's app name |
| `tests/CopilotLedger.Tests/TestSupport/ManualTimeProvider.cs`, `tests/CopilotLedger.Tests/Cli/ExportTimelineTests.cs` | 6, and 140 | `CopilotLedger-Test-0500`, the ID of a test's custom time zone, never shown to users |
| `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs` | 341 | A code comment about the guide's example ("before CopilotLedger was installed") |
| `tests/CopilotLedger.Tests/Docs/ProductNameTests.cs` | 5, 6, 11, 14, 15, 16, 24, 27, 36, 37, 60, 61 | The guard itself: what it allows, and what it looks for |

## Task 1 specification: The Upgrade Steps Check the Rates Overlay

Release blocker U1 (P68-4; see (c) under "The close calls"). After an upgrade, a rates overlay that lists, as a GitHub model's `id` or alias, a name that a built-in GitHub model of the new release has is invalid. The daemon then doesn't start, from `gh ledger daemon start` or at sign-in, and nothing is recorded until the overlay is fixed. gh-ledger 0.4.1 users who priced Claude Haiku 4.5 requests from Copilot CLI 1.0.114 themselves, as 0.4.1's `unknown_model` fix advises, have such an overlay. gh-ledger already says what's wrong; the upgrade steps now say what to do.

### Files

| File | Change |
|---|---|
| `docs/cli.md` | "Upgrading or removing the extension": a paragraph after the upgrade commands. "Troubleshooting": an entry for a daemon that didn't start |
| `docs/guide.md` | "Upgrade or uninstall": after the `daemon-start` sample, the `rates-validate-upgrade` sample and what to do |
| `tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs` | `OtherSamples` gains `rates-validate-upgrade`; new GS8 and its overlay, `HaikuOverlay` |

### What changes

1. The guide's upgrade walkthrough, after `gh ledger daemon start`, tells a user whose daemon didn't start to run `gh ledger rates validate`, shows its message for an overlay that prices `claude-haiku-4-5-20251001` itself, and says to remove the entry that the message names. The message is what `rates validate` prints in the guide's example, so GS8 checks it.
2. `docs/cli.md` says the same after its upgrade commands, and its troubleshooting list explains a `gh ledger daemon start` that says the daemon did not start: `daemon.log` says why, and after an upgrade, the `duplicate model identity in the merged rates` message means an overlay entry to remove.
3. No code changes. The overlay rule, that one name belongs to one model of a section, stays, and `docs/configuration.md` already states it ("Which model prices a request").

### Code (copy exactly)

Edit 1 of 2 in `docs/cli.md`, replace:

````markdown
gh extension upgrade ledger
gh ledger daemon start
```
````

with:

````markdown
gh extension upgrade ledger
gh ledger daemon start
```

If `gh ledger daemon start` then says that the daemon did not start, run `gh ledger rates validate`. A new release can build in a model name that your rates overlay also lists, such as `claude-haiku-4-5-20251001`, which gh-ledger 0.5.0 builds in and earlier versions priced only through an overlay. Such an overlay is invalid, and the daemon doesn't start until you remove the entry that the message names; see [Which model prices a request](configuration.md#which-model-prices-a-request).
````

Edit 2 of 2 in `docs/cli.md`, replace:

```markdown
- **Port 4318 is in use.** Stop the other program or the old daemon, then run `gh ledger daemon start`.
```

with:

```markdown
- **Port 4318 is in use.** Stop the other program or the old daemon, then run `gh ledger daemon start`.
- **`gh ledger daemon start` says that the daemon did not start.** `daemon.log` in your data directory says why. After an upgrade, `Invalid rates overlay: … duplicate model identity in the merged rates: <name> (built-in model <id>)` means that your rates overlay lists a model name that the new release has built in. Remove that entry from the overlay, check it with `gh ledger rates validate`, and start the daemon.
```

Edit 1 of 1 in `docs/guide.md`, replace:

````markdown
<!-- sample: daemon-start -->
```shell
gh ledger daemon start
```

```text
gh-ledger daemon started (pid 4242) on http://localhost:4318.
```
<!-- /sample -->
````

with:

````markdown
<!-- sample: daemon-start -->
```shell
gh ledger daemon start
```

```text
gh-ledger daemon started (pid 4242) on http://localhost:4318.
```
<!-- /sample -->

If it says instead that the daemon did not start, check your rates overlay:

<!-- sample: rates-validate-upgrade -->
```shell
gh ledger rates validate
```

```text
Invalid rates overlay: C:\Users\you\.copilot-ledger\rates.json: githubAiCredits.models[0].id: duplicate model identity in the merged rates: claude-haiku-4-5-20251001 (built-in model claude-haiku-4.5)
```
<!-- /sample -->

A new release can build in a model name that your overlay also lists, and the daemon doesn't start until the overlay is valid. Here, the overlay prices `claude-haiku-4-5-20251001`, the name that Copilot CLI 1.0.114 reports for Claude Haiku 4.5: gh-ledger 0.4.1 didn't know it, so you may have added it yourself, and gh-ledger 0.5.0 builds it in. Remove the entry that the message names, run `gh ledger rates validate` again, and start the daemon. See [Which model prices a request](configuration.md#which-model-prices-a-request).
````

Edit 1 of 2 in `tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs`, replace:

```csharp
    // The samples that GS3 to GS7 check, and the overlay that the Month state uses.
    private static readonly string[] OtherSamples =
        ["daemon-start", "rates-update", "budget-alert", "receipt", "mcp", "rates-overlay"];
```

with:

```csharp
    // The samples that GS3 to GS8 check, and the overlay that the Month state uses.
    private static readonly string[] OtherSamples =
        ["daemon-start", "rates-update", "budget-alert", "receipt", "mcp", "rates-overlay", "rates-validate-upgrade"];
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs`, replace:

```csharp
        int json = text.LastIndexOf('\n') + 1;
        Assert.Equal(sample.Text, text[..json] + Indent(text[json..]));
    }
```

with:

```csharp
        int json = text.LastIndexOf('\n') + 1;
        Assert.Equal(sample.Text, text[..json] + Indent(text[json..]));
    }

    [Fact]
    public async Task GS8_RatesValidateUpgradeSampleNamesTheOverlayEntry()
    {
        GuideSample sample = Samples.Value["rates-validate-upgrade"];
        Assert.Equal("gh ledger rates validate", sample.Command);
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        string path = world.Harness.Context.RatesOverlayPath;
        await File.WriteAllTextAsync(path, HaikuOverlay);
        _ = await FileRetry.ReadAllTextAsync(path);
        CliRun run = await world.RunAsync(sample.Command!);
        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal(sample.Text + "\n", world.Display(run.Error));
    }

    // An overlay that prices claude-haiku-4-5-20251001, the name that Copilot CLI 1.0.114 reports for Claude Haiku 4.5, as a
    // user of gh-ledger 0.4.1, which didn't know the name, could have written it. gh-ledger 0.5.0 builds the name in.
    private const string HaikuOverlay = """
        {
          "schemaVersion": 1,
          "githubAiCredits": {
            "models": [
              {
                "id": "claude-haiku-4-5-20251001",
                "windows": [
                  { "tiers": [ { "name": "default", "inputPer1M": 1.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 1.25, "outputPer1M": 5.00 } ] }
                ]
              }
            ]
          }
        }
        """;
```

### Tests

- **GS8** (new): in the guide's `Month` state, with `HaikuOverlay` as the overlay, `gh ledger rates validate` exits 1, writes nothing to standard output, and writes the `rates-validate-upgrade` sample's text to standard error, with the data directory shown as `C:\Users\you\.copilot-ledger`. It fails without the guide's sample.
- **GS1** (changed list): `OtherSamples` names `rates-validate-upgrade`, so the guide's samples and the checked ones still match.

### Acceptance criteria

- The guide's "Upgrade or uninstall", and `docs/cli.md`'s "Upgrading or removing the extension" and "Troubleshooting", say what to do when the daemon doesn't start after an upgrade because the overlay names a model that the release builds in.
- `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors, and `dotnet test CopilotLedger.slnx` passes 1732 tests, 0 failed, 0 skipped.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 2 specification: gh-ledger in Command, Install, Status, and Report Text

The product's name (see "The product's name") in what commands print and in the files they write, apart from alerts, MCP, and receipts, which are Task 3's. The tests and guide samples that pin each string change with it.

### Files

| File | Change |
|---|---|
| `docs/guide.md` | The `install` and `uninstall` samples |
| `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs` | The report's `<title>` and `<h1>` |
| `src/CopilotLedger.Cli/GhLedgerCli.cs` | The root command's description |
| `src/CopilotLedger.Cli/Install/CopilotOtelEnvironment.cs` | Three warnings |
| `src/CopilotLedger.Cli/Install/InstallCommands.cs` | `install`'s last line and `uninstall`'s messages |
| `src/CopilotLedger.Cli/Install/StartupItem.cs` | The Linux autostart entry's `Name` and `Comment` |
| `src/CopilotLedger.Cli/Mcp/McpCommand.cs` | The `mcp` command's description |
| `src/CopilotLedger.Cli/Rates/RatesCommands.cs` | The starter overlay's first note |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | `status`'s warning about another installed copy |
| `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs` | EC9: the title |
| `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs` | IN1, IN2, IN10, IN11, IN16, IN17, and IN18 |
| `tests/CopilotLedger.Tests/Cli/RatesCommandTests.cs` | `Scaffold`, which RT1 compares |
| `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs` | `Warnings`, which RP3 uses, and RP13 |

### What changes

| Where | Before | After |
|---|---|---|
| `--help` | `CopilotLedger: local AI spend ledger for GitHub Copilot` | `gh-ledger: a local AI-spend ledger for GitHub Copilot` |
| `install` and `status` warnings | `… instead of to CopilotLedger at http://localhost:4318.`, `… instead of exporting it to CopilotLedger.`, and `… CopilotLedger accepts only http/protobuf and http/json.` | gh-ledger in each |
| `install` | `CopilotLedger is installed. Restart …` | `gh-ledger is installed. Restart …` |
| `uninstall` | `CopilotLedger is not installed; nothing to undo.`, `Would remove` and `Removed the CopilotLedger block from <profile>.`, `Warning: no complete CopilotLedger block in <profile>; …`, and `CopilotLedger is uninstalled. …` | gh-ledger in each; the block's markers stay `copilot-ledger` |
| The Linux autostart entry | `Name=CopilotLedger`, `Comment=Starts the CopilotLedger daemon` | `Name=gh-ledger`, `Comment=Starts the gh-ledger daemon`; the file stays `copilot-ledger.desktop` |
| `mcp --help` | `Run the CopilotLedger MCP server …` | `Run the gh-ledger MCP server …` |
| `rates init`'s overlay | The note `CopilotLedger rates overlay. See docs/configuration.md in the CopilotLedger repository.` | `gh-ledger rates overlay. See docs/configuration.md in the gh-ledger repository.` |
| `status` | `CopilotLedger was installed from <path>; …` | `gh-ledger was installed from <path>; …` |
| `export`'s report | `<title>CopilotLedger report: …</title>` and `<h1>CopilotLedger report</h1>` | `gh-ledger report` in both |

The starter overlay's note changes no rule, and `docs/configuration.md` doesn't quote it, so the pricing docs stay accurate without a change here; Task 4 renames the product there. Nothing that a test or a sample pins outside this table changes.

### Code (copy exactly)

Edit 1 of 2 in `docs/guide.md`, replace:

```markdown
CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.
```

with:

```markdown
gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.
```

Edit 2 of 2 in `docs/guide.md`, replace:

```markdown
CopilotLedger is uninstalled. Your ledger data is still in C:\Users\you\.copilot-ledger.
```

with:

```markdown
gh-ledger is uninstalled. Your ledger data is still in C:\Users\you\.copilot-ledger.
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        Line(html, $"<title>CopilotLedger report: {Text(header.Scope)} ({Text(header.Filter.Period)})</title>");
```

with:

```csharp
        Line(html, $"<title>gh-ledger report: {Text(header.Scope)} ({Text(header.Filter.Period)})</title>");
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        Line(html, "<h1>CopilotLedger report</h1>");
```

with:

```csharp
        Line(html, "<h1>gh-ledger report</h1>");
```

Edit 1 of 1 in `src/CopilotLedger.Cli/GhLedgerCli.cs`, replace:

```csharp
            "CopilotLedger: local AI spend ledger for GitHub Copilot");
```

with:

```csharp
            "gh-ledger: a local AI-spend ledger for GitHub Copilot");
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Install/CopilotOtelEnvironment.cs`, replace:

```csharp
        string? endpoint = Value(lookup, "OTEL_EXPORTER_OTLP_ENDPOINT");
        if (endpoint is not null && !IsLocalEndpoint(endpoint))
        {
            warnings.Add(
                $"OTEL_EXPORTER_OTLP_ENDPOINT is {endpoint}; Copilot exports there instead of to CopilotLedger at http://localhost:4318.");
        }

        if (Value(lookup, "COPILOT_OTEL_FILE_EXPORTER_PATH") is not null)
        {
            warnings.Add(
                "COPILOT_OTEL_FILE_EXPORTER_PATH is set; Copilot writes telemetry to that file instead of exporting it to CopilotLedger.");
```

with:

```csharp
        string? endpoint = Value(lookup, "OTEL_EXPORTER_OTLP_ENDPOINT");
        if (endpoint is not null && !IsLocalEndpoint(endpoint))
        {
            warnings.Add(
                $"OTEL_EXPORTER_OTLP_ENDPOINT is {endpoint}; Copilot exports there instead of to gh-ledger at http://localhost:4318.");
        }

        if (Value(lookup, "COPILOT_OTEL_FILE_EXPORTER_PATH") is not null)
        {
            warnings.Add(
                "COPILOT_OTEL_FILE_EXPORTER_PATH is set; Copilot writes telemetry to that file instead of exporting it to gh-ledger.");
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Install/CopilotOtelEnvironment.cs`, replace:

```csharp
        if (value?.Equals("grpc", StringComparison.OrdinalIgnoreCase) == true)
        {
            warnings.Add(
                $"{name} is grpc; CopilotLedger accepts only http/protobuf and http/json.");
```

with:

```csharp
        if (value?.Equals("grpc", StringComparison.OrdinalIgnoreCase) == true)
        {
            warnings.Add(
                $"{name} is grpc; gh-ledger accepts only http/protobuf and http/json.");
```

Edit 1 of 5 in `src/CopilotLedger.Cli/Install/InstallCommands.cs`, replace:

```csharp
            ? "Dry run: no changes made."
            : "CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.");
```

with:

```csharp
            ? "Dry run: no changes made."
            : "gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.");
```

Edit 2 of 5 in `src/CopilotLedger.Cli/Install/InstallCommands.cs`, replace:

```csharp
            context.Out.WriteLine("CopilotLedger is not installed; nothing to undo.");
```

with:

```csharp
            context.Out.WriteLine("gh-ledger is not installed; nothing to undo.");
```

Edit 3 of 5 in `src/CopilotLedger.Cli/Install/InstallCommands.cs`, replace:

```csharp
                    ? $"Would remove the CopilotLedger block from {profile}."
                    : $"Removed the CopilotLedger block from {profile}.");
```

with:

```csharp
                    ? $"Would remove the gh-ledger block from {profile}."
                    : $"Removed the gh-ledger block from {profile}.");
```

Edit 4 of 5 in `src/CopilotLedger.Cli/Install/InstallCommands.cs`, replace:

```csharp
                    $"Warning: no complete CopilotLedger block in {profile}; edit it by hand.");
```

with:

```csharp
                    $"Warning: no complete gh-ledger block in {profile}; edit it by hand.");
```

Edit 5 of 5 in `src/CopilotLedger.Cli/Install/InstallCommands.cs`, replace:

```csharp
                $"CopilotLedger is uninstalled. Your ledger data is still in {context.DataDirectory}.");
```

with:

```csharp
                $"gh-ledger is uninstalled. Your ledger data is still in {context.DataDirectory}.");
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Install/StartupItem.cs`, replace:

```csharp
            Name=CopilotLedger
            Comment=Starts the CopilotLedger daemon
```

with:

```csharp
            Name=gh-ledger
            Comment=Starts the gh-ledger daemon
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Mcp/McpCommand.cs`, replace:

```csharp
            "Run the CopilotLedger MCP server on standard input and output.");
```

with:

```csharp
            "Run the gh-ledger MCP server on standard input and output.");
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Rates/RatesCommands.cs`, replace:

```csharp
            "CopilotLedger rates overlay. See docs/configuration.md in the CopilotLedger repository.",
```

with:

```csharp
            "gh-ledger rates overlay. See docs/configuration.md in the gh-ledger repository.",
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                $"CopilotLedger was installed from {manifest.ExecutablePath}; run {command} install to use this copy instead.");
```

with:

```csharp
                $"gh-ledger was installed from {manifest.ExecutablePath}; run {command} install to use this copy instead.");
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
        Assert.Equal("CopilotLedger report: all repositories (last 30 days)", page.Root!.Element("head")!.Element("title")!.Value);
```

with:

```csharp
        Assert.Equal("gh-ledger report: all repositories (last 30 days)", page.Root!.Element("head")!.Element("title")!.Value);
```

Edit 1 of 9 in `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs`, replace:

```csharp
            Warning: GitHub CLI needs sh.exe from Git for Windows to run shell aliases, so gh ledger won't work until Git for Windows is installed. Run gh-ledger directly.
            gh-ledger will start when you sign in.
            gh-ledger daemon started (pid {Environment.ProcessId}) on http://localhost:4318.
            CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.
```

with:

```csharp
            Warning: GitHub CLI needs sh.exe from Git for Windows to run shell aliases, so gh ledger won't work until Git for Windows is installed. Run gh-ledger directly.
            gh-ledger will start when you sign in.
            gh-ledger daemon started (pid {Environment.ProcessId}) on http://localhost:4318.
            gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.
```

Edit 2 of 9 in `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs`, replace:

```csharp
            CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.
```

with:

```csharp
            gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.
```

Edit 3 of 9 in `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs`, replace:

```csharp
            Assert.Contains("Removed gh alias: ledger.", uninstall.Out);
            Assert.False(File.Exists(ManifestPath(temp.Path)));
            Assert.EndsWith(
                $"CopilotLedger is uninstalled. Your ledger data is still in {Path.Combine(temp.Path, "data")}.\n",
```

with:

```csharp
            Assert.Contains("Removed gh alias: ledger.", uninstall.Out);
            Assert.False(File.Exists(ManifestPath(temp.Path)));
            Assert.EndsWith(
                $"gh-ledger is uninstalled. Your ledger data is still in {Path.Combine(temp.Path, "data")}.\n",
```

Edit 4 of 9 in `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs`, replace:

```csharp
                "CopilotLedger is not installed; nothing to undo.\n",
```

with:

```csharp
                "gh-ledger is not installed; nothing to undo.\n",
```

Edit 5 of 9 in `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs`, replace:

```csharp
            Name=CopilotLedger
            Comment=Starts the CopilotLedger daemon
            Exec="/opt/bin/gh-ledger" daemon start
```

with:

```csharp
            Name=gh-ledger
            Comment=Starts the gh-ledger daemon
            Exec="/opt/bin/gh-ledger" daemon start
```

Edit 6 of 9 in `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs`, replace:

```csharp
                $"CopilotLedger is uninstalled. Your ledger data is still in {Path.Combine(temp.Path, "data")}.\n",
```

with:

```csharp
                $"gh-ledger is uninstalled. Your ledger data is still in {Path.Combine(temp.Path, "data")}.\n",
```

Edit 7 of 9 in `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs`, replace:

```csharp
            "CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n",
            run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Empty(platform.ProcessRuns);
        Assert.Equal(
            [("CopilotLedger", $"\"{WindowsExtension}\" daemon start")],
```

with:

```csharp
            "gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n",
            run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Empty(platform.ProcessRuns);
        Assert.Equal(
            [("CopilotLedger", $"\"{WindowsExtension}\" daemon start")],
```

Edit 8 of 9 in `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs`, replace:

```csharp
            "CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n",
```

with:

```csharp
            "gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n",
```

Edit 9 of 9 in `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs`, replace:

```csharp
            Name=CopilotLedger
            Comment=Starts the CopilotLedger daemon
```

with:

```csharp
            Name=gh-ledger
            Comment=Starts the gh-ledger daemon
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/RatesCommandTests.cs`, replace:

```csharp
            "CopilotLedger rates overlay. See docs/configuration.md in the CopilotLedger repository.",
```

with:

```csharp
            "gh-ledger rates overlay. See docs/configuration.md in the gh-ledger repository.",
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
        { "OTEL_EXPORTER_OTLP_ENDPOINT", "https://collector.contoso.com:4318", "OTEL_EXPORTER_OTLP_ENDPOINT is https://collector.contoso.com:4318; Copilot exports there instead of to CopilotLedger at http://localhost:4318.", false },
        { "COPILOT_OTEL_FILE_EXPORTER_PATH", "/tmp/x.jsonl", "COPILOT_OTEL_FILE_EXPORTER_PATH is set; Copilot writes telemetry to that file instead of exporting it to CopilotLedger.", false },
```

with:

```csharp
        { "OTEL_EXPORTER_OTLP_ENDPOINT", "https://collector.contoso.com:4318", "OTEL_EXPORTER_OTLP_ENDPOINT is https://collector.contoso.com:4318; Copilot exports there instead of to gh-ledger at http://localhost:4318.", false },
        { "COPILOT_OTEL_FILE_EXPORTER_PATH", "/tmp/x.jsonl", "COPILOT_OTEL_FILE_EXPORTER_PATH is set; Copilot writes telemetry to that file instead of exporting it to gh-ledger.", false },
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
        { "OTEL_EXPORTER_OTLP_PROTOCOL", "grpc", "OTEL_EXPORTER_OTLP_PROTOCOL is grpc; CopilotLedger accepts only http/protobuf and http/json.", false },
```

with:

```csharp
        { "OTEL_EXPORTER_OTLP_PROTOCOL", "grpc", "OTEL_EXPORTER_OTLP_PROTOCOL is grpc; gh-ledger accepts only http/protobuf and http/json.", false },
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
            "CopilotLedger was installed from /opt/old/gh-ledger; run gh-ledger install to use this copy instead.";
```

with:

```csharp
            "gh-ledger was installed from /opt/old/gh-ledger; run gh-ledger install to use this copy instead.";
```

### Tests

Changed: EC9, IN1, IN2, IN10, IN11, IN16, IN17, IN18, RT1, RP3, RP13, and GS2's `install` and `uninstall` samples. Nothing is added; Task 3's PN1 checks that `src` uses the name.

### Acceptance criteria

- Every string in "What changes" says gh-ledger, and the identifiers that "The product's name" keeps are unchanged.
- `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors, and `dotnet test CopilotLedger.slnx` passes 1732 tests, 0 failed, 0 skipped.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 3 specification: gh-ledger in Alerts, MCP, and Receipts

The product's name in alerts, MCP, and receipts, with the tests and guide samples that pin it, and the guard that keeps the name out of `src` except as an identifier.

### Files

| File | Change |
|---|---|
| `docs/guide.md` | The `budget-alert`, `export-pr`, `receipt`, and `mcp` samples |
| `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs` | `export-pr`'s hint |
| `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs` | The receipt's heading |
| `src/CopilotLedger.Mcp/LedgerMcpServer.cs` | The server's instructions |
| `src/CopilotLedger.Mcp/LedgerMcpTools.cs` | `set_branch_budget`'s description and the open trip's `WARNING:` line |
| `src/CopilotLedger.Otlp/Breaker/BreakerMessages.cs` | The two alert titles |
| `tests/CopilotLedger.Tests/Breaker/BreakerRulesTests.cs` | BR4 |
| `tests/CopilotLedger.Tests/Cli/AlertDaemonTests.cs` | `AlertA`, AD1, AD2, and AD3 |
| `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs` | `FeatureReceipt` (EP1–EP3), `EmptyReceipt` (EP5), EP1, EP5, and EP10 |
| `tests/CopilotLedger.Tests/Docs/ProductNameTests.cs` | New: PN1 |
| `tests/CopilotLedger.Tests/Mcp/AuditMcpTests.cs` | AM3 |
| `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs` | MC1, MC3, and MC14 |
| `tests/CopilotLedger.Tests/Otlp/CircuitBreakerMonitorTests.cs` | BM1, `AssertTripAlert`, and `AssertBudgetAlert` |

### What changes

| Where | Before | After |
|---|---|---|
| Alert titles: desktop notifications, `ALERT` lines in `daemon.log`, and the Windows toast | `CopilotLedger circuit breaker tripped`, `CopilotLedger branch budget exceeded` | `gh-ledger circuit breaker tripped`, `gh-ledger branch budget exceeded` |
| MCP server instructions | `CopilotLedger reports the AI spend …` | `gh-ledger reports the AI spend …` |
| `set_branch_budget`'s description | `… CopilotLedger alerts once …` | `… gh-ledger alerts once …` |
| MCP's open-trip warning | `WARNING: CopilotLedger circuit breaker is open for …` | `WARNING: gh-ledger circuit breaker is open for …` |
| `export-pr` | `… the CopilotLedger action posts it on the pull request.` | `… the PR receipt action posts it on the pull request.` |
| The receipt's heading | `### CopilotLedger receipt for <branch>` | `### gh-ledger receipt for <branch>` |

A receipt gets 4 bytes shorter, so EP10's oversized receipt is 68090 bytes instead of 68094. The receipt's marker, file name, JSON data, and `schemaVersion` don't change, and the action finds receipts by the marker and the JSON (see "Verified facts").

**PN1** (new): every file under `src`, skipping `bin` and `obj` folders, names CopilotLedger, or Copilot Ledger, in any case, only as `CopilotLedger.<name>` or in one of three identifiers: the Run value `"CopilotLedger"` (`InstallCommands.cs`), its registry path ending `\Run\CopilotLedger"` (`StartupItem.cs`), and `"--app-name=CopilotLedger"` (`DesktopNotifier.cs`). A failure lists each offending line as `<file>:<line>`.

### Code (copy exactly)

Edit 1 of 5 in `docs/guide.md`, replace:

```markdown
2026-11-21T21:30:00.0000000+00:00 ALERT CopilotLedger branch budget exceeded: contoso/webapp @ feature/search: 766.75 of 750.00 AI credits spent. Run: gh-ledger budget list
```

with:

```markdown
2026-11-21T21:30:00.0000000+00:00 ALERT gh-ledger branch budget exceeded: contoso/webapp @ feature/search: 766.75 of 750.00 AI credits spent. Run: gh-ledger budget list
```

Edit 2 of 5 in `docs/guide.md`, replace:

```markdown
Commit it on the branch and push; the CopilotLedger action posts it on the pull request.
```

with:

```markdown
Commit it on the branch and push; the PR receipt action posts it on the pull request.
```

Edit 3 of 5 in `docs/guide.md`, replace:

```markdown
> ### CopilotLedger receipt for `feature/search`
```

with:

```markdown
> ### gh-ledger receipt for `feature/search`
```

Edit 4 of 5 in `docs/guide.md`, replace:

````markdown
<!-- sample: mcp -->
```text
WARNING: CopilotLedger circuit breaker is open for contoso/webapp @ feature/search: 252.00 AI credits in 5 minutes on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1
````

with:

````markdown
<!-- sample: mcp -->
```text
WARNING: gh-ledger circuit breaker is open for contoso/webapp @ feature/search: 252.00 AI credits in 5 minutes on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1
````

Edit 5 of 5 in `docs/guide.md`, replace:

```markdown
    "WARNING: CopilotLedger circuit breaker is open for contoso/webapp @ feature/search: 252.00 AI credits in 5 minutes on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1"
```

with:

```markdown
    "WARNING: gh-ledger circuit breaker is open for contoso/webapp @ feature/search: 252.00 AI credits in 5 minutes on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1"
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, replace:

```csharp
            "Commit it on the branch and push; the CopilotLedger action posts it on the pull request.");
```

with:

```csharp
            "Commit it on the branch and push; the PR receipt action posts it on the pull request.");
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
        text.Append("### CopilotLedger receipt for ").Append(Code(receipt.Branch)).Append("\n\n");
```

with:

```csharp
        text.Append("### gh-ledger receipt for ").Append(Code(receipt.Branch)).Append("\n\n");
```

Edit 1 of 1 in `src/CopilotLedger.Mcp/LedgerMcpServer.cs`, replace:

```csharp
        "CopilotLedger reports the AI spend that Copilot recorded on this machine, by Git branch. " +
```

with:

```csharp
        "gh-ledger reports the AI spend that Copilot recorded on this machine, by Git branch. " +
```

Edit 1 of 2 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            Description = "Sets a soft AI-credit budget for one Git branch. CopilotLedger alerts once when the branch's billed spend exceeds it; nothing is blocked. Uses the current repository and branch unless you pass both.",
```

with:

```csharp
            Description = "Sets a soft AI-credit budget for one Git branch. gh-ledger alerts once when the branch's billed spend exceeds it; nothing is blocked. Uses the current repository and branch unless you pass both.",
```

Edit 2 of 2 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
                $"WARNING: CopilotLedger circuit breaker is open for {trip.Repository} @ {trip.Branch}: {trip.Detail} on one prompt (trip {trip.Id}). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id {trip.Id}");
```

with:

```csharp
                $"WARNING: gh-ledger circuit breaker is open for {trip.Repository} @ {trip.Branch}: {trip.Detail} on one prompt (trip {trip.Id}). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id {trip.Id}");
```

Edit 1 of 1 in `src/CopilotLedger.Otlp/Breaker/BreakerMessages.cs`, replace:

```csharp
    public const string TripTitle = "CopilotLedger circuit breaker tripped";
    public const string BudgetTitle = "CopilotLedger branch budget exceeded";
```

with:

```csharp
    public const string TripTitle = "gh-ledger circuit breaker tripped";
    public const string BudgetTitle = "gh-ledger branch budget exceeded";
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Breaker/BreakerRulesTests.cs`, replace:

```csharp
        Assert.Equal("CopilotLedger circuit breaker tripped", trip.Title);
```

with:

```csharp
        Assert.Equal("gh-ledger circuit breaker tripped", trip.Title);
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Breaker/BreakerRulesTests.cs`, replace:

```csharp
        Assert.Equal("CopilotLedger branch budget exceeded", budget.Title);
```

with:

```csharp
        Assert.Equal("gh-ledger branch budget exceeded", budget.Title);
```

Edit 1 of 6 in `tests/CopilotLedger.Tests/Cli/AlertDaemonTests.cs`, replace:

```csharp
        "ALERT CopilotLedger circuit breaker tripped: swigerb/copilot-ledger @ feature/a: 5 failed tool calls in a row on one prompt. Run: gh-ledger breaker status";
```

with:

```csharp
        "ALERT gh-ledger circuit breaker tripped: swigerb/copilot-ledger @ feature/a: 5 failed tool calls in a row on one prompt. Run: gh-ledger breaker status";
```

Edit 2 of 6 in `tests/CopilotLedger.Tests/Cli/AlertDaemonTests.cs`, replace:

```csharp
            "<toast><visual><binding template=\"ToastGeneric\"><text>CopilotLedger circuit breaker tripped</text><text>swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt. Run: gh-ledger breaker status</text></binding></visual></toast>",
            await FileRetry.ReadAllTextAsync(
                Path.Combine(temp.Path, "data", "alert-toast.xml")));
        Assert.Contains(
            "ALERT CopilotLedger circuit breaker tripped: swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt. Run: gh-ledger breaker status",
```

with:

```csharp
            "<toast><visual><binding template=\"ToastGeneric\"><text>gh-ledger circuit breaker tripped</text><text>swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt. Run: gh-ledger breaker status</text></binding></visual></toast>",
            await FileRetry.ReadAllTextAsync(
                Path.Combine(temp.Path, "data", "alert-toast.xml")));
        Assert.Contains(
            "ALERT gh-ledger circuit breaker tripped: swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt. Run: gh-ledger breaker status",
```

Edit 3 of 6 in `tests/CopilotLedger.Tests/Cli/AlertDaemonTests.cs`, replace:

```csharp
                        "end run",
                        "CopilotLedger circuit breaker tripped",
```

with:

```csharp
                        "end run",
                        "gh-ledger circuit breaker tripped",
```

Edit 4 of 6 in `tests/CopilotLedger.Tests/Cli/AlertDaemonTests.cs`, replace:

```csharp
                        "CopilotLedger branch budget exceeded",
```

with:

```csharp
                        "gh-ledger branch budget exceeded",
```

Edit 5 of 6 in `tests/CopilotLedger.Tests/Cli/AlertDaemonTests.cs`, replace:

```csharp
            "ALERT CopilotLedger branch budget exceeded: swigerb/copilot-ledger @ feature/ledger: 56.00 of 50.00 AI credits spent. Run: gh-ledger budget list",
```

with:

```csharp
            "ALERT gh-ledger branch budget exceeded: swigerb/copilot-ledger @ feature/ledger: 56.00 of 50.00 AI credits spent. Run: gh-ledger budget list",
```

Edit 6 of 6 in `tests/CopilotLedger.Tests/Cli/AlertDaemonTests.cs`, replace:

```csharp
                        "CopilotLedger circuit breaker tripped",
```

with:

```csharp
                        "gh-ledger circuit breaker tripped",
```

Edit 1 of 5 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
        ### CopilotLedger receipt for `feature/ledger`
```

with:

```csharp
        ### gh-ledger receipt for `feature/ledger`
```

Edit 2 of 5 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
        ### CopilotLedger receipt for `fix/@octocat-#7`
```

with:

```csharp
        ### gh-ledger receipt for `fix/@octocat-#7`
```

Edit 3 of 5 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
            $"Wrote {FeatureFile} for feature/ledger.\n" +
            "Commit it on the branch and push; the CopilotLedger action posts it on the pull request.\n",
```

with:

```csharp
            $"Wrote {FeatureFile} for feature/ledger.\n" +
            "Commit it on the branch and push; the PR receipt action posts it on the pull request.\n",
```

Edit 4 of 5 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
            "Commit it on the branch and push; the CopilotLedger action posts it on the pull request.\n",
```

with:

```csharp
            "Commit it on the branch and push; the PR receipt action posts it on the pull request.\n",
```

Edit 5 of 5 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
            "The receipt for feature/ledger is 68094 bytes; a receipt can be at most 65536 bytes.\n";
```

with:

```csharp
            "The receipt for feature/ledger is 68090 bytes; a receipt can be at most 65536 bytes.\n";
```

New file `tests/CopilotLedger.Tests/Docs/ProductNameTests.cs`:

```csharp
using System.Text.RegularExpressions;

namespace CopilotLedger.Tests.Docs;

// The product's name is gh-ledger. CopilotLedger stays only as an identifier that existing installs rely on: the namespaces
// and projects (CopilotLedger.<name>), the Windows sign-in item's Run value, and the app name that notify-send gives
// notification settings.
public sealed partial class ProductNameTests
{
    [Fact]
    public void PN1_TheSourceNamesCopilotLedgerOnlyAsAnIdentifier()
    {
        Assert.Empty(Mentions(FilesUnder(RepositoryFile("src")), line => line
            .Replace("\"CopilotLedger\"", string.Empty, StringComparison.Ordinal)
            .Replace("\\Run\\CopilotLedger\"", string.Empty, StringComparison.Ordinal)
            .Replace("\"--app-name=CopilotLedger\"", string.Empty, StringComparison.Ordinal)));
    }

    // "<file>:<line>" for each line that still names CopilotLedger, or Copilot Ledger, in any case, once removeAllowed and
    // Identifier have taken out what may stay.
    private static string[] Mentions(IEnumerable<string> files, Func<string, string> removeAllowed)
    {
        string root = RepositoryFile();
        return
        [
            .. files.Order(StringComparer.Ordinal).SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (Text: removeAllowed(Identifier().Replace(line, string.Empty)), Number: index + 1))
                .Where(line => line.Text.Contains("CopilotLedger", StringComparison.OrdinalIgnoreCase)
                    || line.Text.Contains("Copilot Ledger", StringComparison.OrdinalIgnoreCase))
                .Select(line => $"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{line.Number}")),
        ];
    }

    // The files under folder, recursively, skipping bin and obj folders.
    private static IEnumerable<string> FilesUnder(string folder) =>
        Directory.GetFiles(folder).Concat(Directory.GetDirectories(folder)
            .Where(directory => Path.GetFileName(directory) is not ("bin" or "obj"))
            .SelectMany(FilesUnder));

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

    // A namespace or project name: CopilotLedger followed by a dot and a letter, as in CopilotLedger.Cli.
    [GeneratedRegex(@"CopilotLedger(?=\.[A-Za-z])")]
    private static partial Regex Identifier();
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Mcp/AuditMcpTests.cs`, replace:

```csharp
        Assert.StartsWith("WARNING: CopilotLedger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: ", lines[0]);
```

with:

```csharp
        Assert.StartsWith("WARNING: gh-ledger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: ", lines[0]);
```

Edit 1 of 4 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
                "Sets a soft AI-credit budget for one Git branch. CopilotLedger alerts once when the branch's billed spend exceeds it; nothing is blocked. Uses the current repository and branch unless you pass both.",
```

with:

```csharp
                "Sets a soft AI-credit budget for one Git branch. gh-ledger alerts once when the branch's billed spend exceeds it; nothing is blocked. Uses the current repository and branch unless you pass both.",
```

Edit 2 of 4 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
                """["WARNING: CopilotLedger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1","WARNING: swigerb/copilot-ledger @ feature/ledger has spent 176.50 of its 150.00 AI credit budget."]""");
```

with:

```csharp
                """["WARNING: gh-ledger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1","WARNING: swigerb/copilot-ledger @ feature/ledger has spent 176.50 of its 150.00 AI credit budget."]""");
```

Edit 3 of 4 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
            """["WARNING: CopilotLedger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1","WARNING: CopilotLedger circuit breaker is open for swigerb/copilot-ledger @ main: 6 failed tool calls in a row on one prompt (trip 3). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 3","WARNING: swigerb/copilot-ledger @ feature/ledger has spent 176.50 of its 150.00 AI credit budget."]""");
```

with:

```csharp
            """["WARNING: gh-ledger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1","WARNING: gh-ledger circuit breaker is open for swigerb/copilot-ledger @ main: 6 failed tool calls in a row on one prompt (trip 3). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 3","WARNING: swigerb/copilot-ledger @ feature/ledger has spent 176.50 of its 150.00 AI credit budget."]""");
```

Edit 4 of 4 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
            "WARNING: CopilotLedger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1";
```

with:

```csharp
            "WARNING: gh-ledger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1";
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Otlp/CircuitBreakerMonitorTests.cs`, replace:

```csharp
        Assert.Equal("CopilotLedger circuit breaker tripped", alert.Title);
        Assert.Equal(
            "swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt. Run: gh-ledger breaker status",
```

with:

```csharp
        Assert.Equal("gh-ledger circuit breaker tripped", alert.Title);
        Assert.Equal(
            "swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt. Run: gh-ledger breaker status",
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Otlp/CircuitBreakerMonitorTests.cs`, replace:

```csharp
        Assert.Equal("CopilotLedger circuit breaker tripped", alert.Title);
```

with:

```csharp
        Assert.Equal("gh-ledger circuit breaker tripped", alert.Title);
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Otlp/CircuitBreakerMonitorTests.cs`, replace:

```csharp
        Assert.Equal("CopilotLedger branch budget exceeded", alert.Title);
```

with:

```csharp
        Assert.Equal("gh-ledger branch budget exceeded", alert.Title);
```

### Tests

Changed: BR4, AD1, AD2, AD3, BM1, BM4 and BM11 (through `AssertTripAlert` and `AssertBudgetAlert`), EP1, EP2, EP3, EP5, EP10, AM3, MC1, MC3, MC14, and GS2's `export-pr`, GS5 (`budget-alert`), GS6 (`receipt`), and GS7 (`mcp`). New: PN1, which fails on `main` at every string that Tasks 2 and 3 change.

### Acceptance criteria

- Every string in "What changes" says gh-ledger or names the PR receipt action, and PN1 passes.
- `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors, and `dotnet test CopilotLedger.slnx` passes 1733 tests, 0 failed, 0 skipped.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 4 specification: gh-ledger in the README and the Docs

The product's name in README.md and `docs/`, and the guard that keeps it there.

### Files

| File | Change |
|---|---|
| `README.md` | The title; the description, "a local AI-spend ledger for GitHub Copilot"; the sentence that gh-ledger is an independent open-source project, not a GitHub product; the action's name twice; three more mentions |
| `docs/cli.md` | Three mentions, and the action's name |
| `docs/configuration.md` | The title, the files heading, and 16 more mentions |
| `docs/guide.md` | The title, the introduction, the setup heading and its contents link, and eight more lines, including the sample workflow's `name` and the commit message |
| `docs/releasing.md` | The action's name |
| `tests/CopilotLedger.Tests/Docs/ProductNameTests.cs` | New: PN2 |

### What changes

1. The README opens with "gh-ledger is a local AI-spend ledger for GitHub Copilot." and ends its introduction with "gh-ledger is an independent open-source project, not a GitHub product."
2. Every other "CopilotLedger" in prose in README.md and `docs/*.md` becomes gh-ledger, lowercase even at the start of a sentence, as the docs already write it. "The CopilotLedger PR Receipt action" becomes "the PR receipt action", with the same link. `docs/cli.md`'s mention of the Run value `CopilotLedger` stays.
3. Two headings change, and their anchors with them: the guide's "Set up gh-ledger", whose contents link becomes `#set-up-gh-ledger`, and configuration.md's "Where gh-ledger keeps its files", which nothing links to.
4. **PN2** (new): README.md and every `docs/*.md` file name CopilotLedger only as `CopilotLedger.<name>` (project paths and the solution) or in "the `CopilotLedger` value under", the Run value. Like PN1, it checks Copilot Ledger too, in any case.

### Code (copy exactly)

Edit 1 of 6 in `README.md`, replace:

```markdown
# CopilotLedger

CopilotLedger shows what GitHub Copilot costs you. Through Copilot's OpenTelemetry export, it records the GitHub AI credits that Copilot CLI, the GitHub Copilot app, and VS Code spend, and attributes them to repository, branch, model, agent, and prompt. It also tracks Microsoft Foundry BYOK spend and what local models save you.

It runs on your machine as the `gh ledger` GitHub CLI extension, with no Docker or cloud service, and it never stores prompt or response content.
```

with:

```markdown
# gh-ledger

gh-ledger is a local AI-spend ledger for GitHub Copilot. It shows what Copilot costs you: through Copilot's OpenTelemetry export, it records the GitHub AI credits that Copilot CLI, the GitHub Copilot app, and VS Code spend, and attributes them to repository, branch, model, agent, and prompt. It also tracks Microsoft Foundry BYOK spend and what local models save you.

It runs on your machine as the `gh ledger` GitHub CLI extension, with no Docker or cloud service, and it never stores prompt or response content. gh-ledger is an independent open-source project, not a GitHub product.
```

Edit 2 of 6 in `README.md`, replace:

```markdown
- Pull request receipts, posted by the [CopilotLedger PR Receipt action](https://github.com/swigerb/copilot-ledger-action)
```

with:

```markdown
- Pull request receipts, posted by the [PR receipt action](https://github.com/swigerb/copilot-ledger-action)
```

Edit 3 of 6 in `README.md`, replace:

```markdown
- `gh ledger import`: backfill the requests in your Copilot CLI and GitHub Copilot app session logs, and in Codex's, from before you installed CopilotLedger
```

with:

```markdown
- `gh ledger import`: backfill the requests in your Copilot CLI and GitHub Copilot app session logs, and in Codex's, from before you installed gh-ledger
```

Edit 4 of 6 in `README.md`, replace:

```markdown
- Git repositories, for per-branch attribution. CopilotLedger reads `.git` directly and never runs `git`.
```

with:

```markdown
- Git repositories, for per-branch attribution. gh-ledger reads `.git` directly and never runs `git`.
```

Edit 5 of 6 in `README.md`, replace:

```markdown
The ledger stays on your machine, and CopilotLedger never stores prompt or response content. `gh ledger import` reads Copilot's and Codex's session logs in place and keeps only what pricing, attribution, and later imports need: session IDs; models and providers; token counts and times; agent names; the repository root, branch, commit, and `owner/repo` slug that a session recorded; tool names, outcomes, exit codes, and counts; SHA-256 hashes of request and tool call IDs; and each log's size, last-write time, and SHA-256 hash. It never reads prompts, responses, tool arguments or results, or compaction summaries, and it doesn't read Claude Code's logs. See [What import reads and keeps](docs/cli.md#what-import-reads-and-keeps). `gh ledger export` writes only the fields in [What export includes](docs/cli.md#what-export-includes): never prompts, responses, tool arguments or results, or file contents, and local repository roots only with `--include-local-paths`. `gh ledger audit --local-llm` sends a summary of its findings, never content or identifiers, only to a model on this machine, and never stores the answer.
```

with:

```markdown
The ledger stays on your machine, and gh-ledger never stores prompt or response content. `gh ledger import` reads Copilot's and Codex's session logs in place and keeps only what pricing, attribution, and later imports need: session IDs; models and providers; token counts and times; agent names; the repository root, branch, commit, and `owner/repo` slug that a session recorded; tool names, outcomes, exit codes, and counts; SHA-256 hashes of request and tool call IDs; and each log's size, last-write time, and SHA-256 hash. It never reads prompts, responses, tool arguments or results, or compaction summaries, and it doesn't read Claude Code's logs. See [What import reads and keeps](docs/cli.md#what-import-reads-and-keeps). `gh ledger export` writes only the fields in [What export includes](docs/cli.md#what-export-includes): never prompts, responses, tool arguments or results, or file contents, and local repository roots only with `--include-local-paths`. `gh ledger audit --local-llm` sends a summary of its findings, never content or identifiers, only to a model on this machine, and never stores the answer.
```

Edit 6 of 6 in `README.md`, replace:

```markdown
- [CopilotLedger PR Receipt action](https://github.com/swigerb/copilot-ledger-action): posting receipts on pull requests
```

with:

```markdown
- [PR receipt action](https://github.com/swigerb/copilot-ledger-action): posting receipts on pull requests
```

Edit 1 of 4 in `docs/cli.md`, replace:

```markdown
`install` and `status` warn about settings that keep Copilot's telemetry from reaching CopilotLedger:
```

with:

```markdown
`install` and `status` warn about settings that keep Copilot's telemetry from reaching gh-ledger:
```

Edit 2 of 4 in `docs/cli.md`, replace:

```markdown
- `OTEL_EXPORTER_OTLP_PROTOCOL` or `OTEL_EXPORTER_OTLP_TRACES_PROTOCOL` set to `grpc`; CopilotLedger accepts only `http/protobuf` and `http/json`.
```

with:

```markdown
- `OTEL_EXPORTER_OTLP_PROTOCOL` or `OTEL_EXPORTER_OTLP_TRACES_PROTOCOL` set to `grpc`; gh-ledger accepts only `http/protobuf` and `http/json`.
```

Edit 3 of 4 in `docs/cli.md`, replace:

```markdown
`gh ledger export-pr` writes the current branch's AI spend receipt to `.github/copilot-ledger/receipts/<branch>-<hash>.md` in the repository, where `<branch>` is the branch name with characters other than letters, digits, `.`, `_`, and `-` replaced by `-`. Commit the file on the branch and push it; the [CopilotLedger PR Receipt action](https://github.com/swigerb/copilot-ledger-action), `uses: swigerb/copilot-ledger-action@v1`, posts it on the pull request.
```

with:

```markdown
`gh ledger export-pr` writes the current branch's AI spend receipt to `.github/copilot-ledger/receipts/<branch>-<hash>.md` in the repository, where `<branch>` is the branch name with characters other than letters, digits, `.`, `_`, and `-` replaced by `-`. Commit the file on the branch and push it; the [PR receipt action](https://github.com/swigerb/copilot-ledger-action), `uses: swigerb/copilot-ledger-action@v1`, posts it on the pull request.
```

Edit 4 of 4 in `docs/cli.md`, replace:

```markdown
CopilotLedger stores no prompt or response content, working directories, or remote URLs. Repositories are stored as `owner/repo` slugs or local roots, and credentials are stripped. `gh ledger import` keeps only the fields in [What import reads and keeps](#what-import-reads-and-keeps), and never reads prompts, responses, tool arguments or results, or compaction summaries. `gh ledger export` writes only the fields in [What export includes](#what-export-includes), and shows local repository roots only with `--include-local-paths`. `gh ledger audit` reads the same metadata; with `--local-llm`, it sends a summary of its findings only to a model on this machine, as [Local model suggestions](#local-model-suggestions) describes, and never stores the answer.
```

with:

```markdown
gh-ledger stores no prompt or response content, working directories, or remote URLs. Repositories are stored as `owner/repo` slugs or local roots, and credentials are stripped. `gh ledger import` keeps only the fields in [What import reads and keeps](#what-import-reads-and-keeps), and never reads prompts, responses, tool arguments or results, or compaction summaries. `gh ledger export` writes only the fields in [What export includes](#what-export-includes), and shows local repository roots only with `--include-local-paths`. `gh ledger audit` reads the same metadata; with `--local-llm`, it sends a summary of its findings only to a model on this machine, as [Local model suggestions](#local-model-suggestions) describes, and never stores the answer.
```

Edit 1 of 16 in `docs/configuration.md`, replace:

```markdown
# Configuring CopilotLedger

CopilotLedger works with no configuration. This page explains how it prices usage and how to change that with one optional file.

## Where CopilotLedger keeps its files
```

with:

```markdown
# Configuring gh-ledger

gh-ledger works with no configuration. This page explains how it prices usage and how to change that with one optional file.

## Where gh-ledger keeps its files
```

Edit 2 of 16 in `docs/configuration.md`, replace:

```markdown
CopilotLedger never stores prompt or response content, and it strips credentials from Git remote URLs before saving them.
```

with:

```markdown
gh-ledger never stores prompt or response content, and it strips credentials from Git remote URLs before saving them.
```

Edit 3 of 16 in `docs/configuration.md`, replace:

```markdown
- GitHub hasn't published the prices that follow the promotions for Gemini 4 Argon (ending 2026-12-01) and Gemini 3.8 Flash (ending after 2026-12-31). Later requests for them are recorded with status `no_rate_for_date` until you add a price window or a CopilotLedger update adds the price.
```

with:

```markdown
- GitHub hasn't published the prices that follow the promotions for Gemini 4 Argon (ending 2026-12-01) and Gemini 3.8 Flash (ending after 2026-12-31). Later requests for them are recorded with status `no_rate_for_date` until you add a price window or a gh-ledger update adds the price.
```

Edit 4 of 16 in `docs/configuration.md`, replace:

```markdown
The built-in GitHub models have the vendors' API ids and dated snapshot ids as aliases, the same ones as their API list prices. If your overlay adds one of these names as a GitHub model or alias, for example to price `claude-haiku-4-5-20251001` before CopilotLedger did, remove it: a name that a built-in model of the same section already uses makes the overlay invalid.
```

with:

```markdown
The built-in GitHub models have the vendors' API ids and dated snapshot ids as aliases, the same ones as their API list prices. If your overlay adds one of these names as a GitHub model or alias, for example to price `claude-haiku-4-5-20251001` before gh-ledger did, remove it: a name that a built-in model of the same section already uses makes the overlay invalid.
```

Edit 5 of 16 in `docs/configuration.md`, replace:

```markdown
Besides what each request is billed, CopilotLedger records what the same request would cost at its model's API list prices, from the `apiEquivalent` section of the rates (see [API list prices](#api-list-prices)). This API-equivalent cost is a comparison. It's never added to billed spend, and budgets and the circuit breaker ignore it.
```

with:

```markdown
Besides what each request is billed, gh-ledger records what the same request would cost at its model's API list prices, from the `apiEquivalent` section of the rates (see [API list prices](#api-list-prices)). This API-equivalent cost is a comparison. It's never added to billed spend, and budgets and the circuit breaker ignore it.
```

Edit 6 of 16 in `docs/configuration.md`, replace:

```markdown
Each request keeps the API-equivalent result it was recorded with, along with the rates version and the SHA-256 of the API list prices that gave it. Requests recorded before gh-ledger had API list prices have none, and reports count them as unpriced until you run `gh ledger rates backfill-api-equivalent`. It prices them with the rates in use, your overlay included, as of each request's start time, and records that rates version and hash beside the request's billing rates version. It never changes what a request was billed or saved, and it skips requests that already have a result. With `--dry-run`, it shows the counts and writes nothing. A request whose API-equivalent cost would be beyond the ledger's range of about $9.2 million keeps no result; CopilotLedger doesn't record a new request with such a cost at all.
```

with:

```markdown
Each request keeps the API-equivalent result it was recorded with, along with the rates version and the SHA-256 of the API list prices that gave it. Requests recorded before gh-ledger had API list prices have none, and reports count them as unpriced until you run `gh ledger rates backfill-api-equivalent`. It prices them with the rates in use, your overlay included, as of each request's start time, and records that rates version and hash beside the request's billing rates version. It never changes what a request was billed or saved, and it skips requests that already have a result. With `--dry-run`, it shows the counts and writes nothing. A request whose API-equivalent cost would be beyond the ledger's range of about $9.2 million keeps no result; gh-ledger doesn't record a new request with such a cost at all.
```

Edit 7 of 16 in `docs/configuration.md`, replace:

```markdown
With bring-your-own-key (BYOK), Microsoft Foundry bills you directly, not GitHub. Azure prices depend on region, deployment type, and your agreement, so CopilotLedger can't know yours.
```

with:

```markdown
With bring-your-own-key (BYOK), Microsoft Foundry bills you directly, not GitHub. Azure prices depend on region, deployment type, and your agreement, so gh-ledger can't know yours.
```

Edit 8 of 16 in `docs/configuration.md`, replace:

```markdown
CopilotLedger's base rates are `rates.downloaded.json` when that file is valid, newer than the built-in rates, and has `schemaVersion` 2, and the built-in rates otherwise. So after you upgrade gh-ledger to newer built-in rates, an older download is ignored. A download that can't be read, and a newer one with `schemaVersion` 1, such as one that an older gh-ledger saved, are ignored with a warning in `gh ledger status`, `gh ledger rates validate`, and the daemon log. Your overlay applies on top of either. `gh ledger status` shows `(downloaded)` after the rates version when the download is in use.
```

with:

```markdown
gh-ledger's base rates are `rates.downloaded.json` when that file is valid, newer than the built-in rates, and has `schemaVersion` 2, and the built-in rates otherwise. So after you upgrade gh-ledger to newer built-in rates, an older download is ignored. A download that can't be read, and a newer one with `schemaVersion` 1, such as one that an older gh-ledger saved, are ignored with a warning in `gh ledger status`, `gh ledger rates validate`, and the daemon log. Your overlay applies on top of either. `gh ledger status` shows `(downloaded)` after the rates version when the download is in use.
```

Edit 9 of 16 in `docs/configuration.md`, replace:

```markdown
To change pricing, create `~/.copilot-ledger/rates.json`. CopilotLedger merges it over the built-in rates when it loads them, so you only write what you want to change.
```

with:

```markdown
To change pricing, create `~/.copilot-ledger/rates.json`. gh-ledger merges it over the built-in rates when it loads them, so you only write what you want to change.
```

Edit 10 of 16 in `docs/configuration.md`, replace:

```markdown
If the file is invalid, CopilotLedger refuses to load it and reports the first problem it finds. It never silently ignores a broken file. Each error starts with the file's path, then says where the problem is and what's wrong:
```

with:

```markdown
If the file is invalid, gh-ledger refuses to load it and reports the first problem it finds. It never silently ignores a broken file. Each error starts with the file's path, then says where the problem is and what's wrong:
```

Edit 11 of 16 in `docs/configuration.md`, replace:

````markdown
CopilotLedger reports:

```text
/home/me/.copilot-ledger/rates.json: foundryByok.models[0].windows[0].tiers[0].outputPer1M: is required
````

with:

````markdown
gh-ledger reports:

```text
/home/me/.copilot-ledger/rates.json: foundryByok.models[0].windows[0].tiers[0].outputPer1M: is required
````

Edit 12 of 16 in `docs/configuration.md`, replace:

````markdown
CopilotLedger reports:

```text
/home/me/.copilot-ledger/rates.json: localOffload.baselineModel: does not resolve to a githubAiCredits model in the merged rates: gpt-missing
````

with:

````markdown
gh-ledger reports:

```text
/home/me/.copilot-ledger/rates.json: localOffload.baselineModel: does not resolve to a githubAiCredits model in the merged rates: gpt-missing
````

Edit 13 of 16 in `docs/configuration.md`, replace:

````markdown
CopilotLedger reports:

```text
/home/me/.copilot-ledger/rates.json: line 4, column 1: invalid JSON
````

with:

````markdown
gh-ledger reports:

```text
/home/me/.copilot-ledger/rates.json: line 4, column 1: invalid JSON
````

Edit 14 of 16 in `docs/configuration.md`, replace:

```markdown
Local runtimes such as Foundry Local, Ollama, and LM Studio run models on your own GPU, so local requests cost $0.00. CopilotLedger recognizes them by their `localhost` endpoint.
```

with:

```markdown
Local runtimes such as Foundry Local, Ollama, and LM Studio run models on your own GPU, so local requests cost $0.00. gh-ledger recognizes them by their `localhost` endpoint.
```

Edit 15 of 16 in `docs/configuration.md`, replace:

```markdown
To show what running locally is worth, CopilotLedger prices the same tokens at **MAI Code 2**'s GitHub rates, a deliberately conservative comparison. That amount is reported as local savings and is never billed.
```

with:

```markdown
To show what running locally is worth, gh-ledger prices the same tokens at **MAI Code 2**'s GitHub rates, a deliberately conservative comparison. That amount is reported as local savings and is never billed.
```

Edit 16 of 16 in `docs/configuration.md`, replace:

```markdown
If GitHub adds a model or changes a price before CopilotLedger ships updated rates, add or replace the model under `githubAiCredits.models`, using the same shape as the Foundry example.
```

with:

```markdown
If GitHub adds a model or changes a price before gh-ledger ships updated rates, add or replace the model under `githubAiCredits.models`, using the same shape as the Foundry example.
```

Edit 1 of 10 in `docs/guide.md`, replace:

```markdown
# CopilotLedger user guide

CopilotLedger records what GitHub Copilot spends and shows it by repository, branch, model, agent, and prompt. This guide walks through everyday tasks, one scenario at a time. For every command and option, see the [command reference](cli.md).
```

with:

```markdown
# gh-ledger user guide

gh-ledger records what GitHub Copilot spends and shows it by repository, branch, model, agent, and prompt. This guide walks through everyday tasks, one scenario at a time. For every command and option, see the [command reference](cli.md).
```

Edit 2 of 10 in `docs/guide.md`, replace:

```markdown
- [Set up CopilotLedger](#set-up-copilotledger)
```

with:

```markdown
- [Set up gh-ledger](#set-up-gh-ledger)
```

Edit 3 of 10 in `docs/guide.md`, replace:

```markdown
## Set up CopilotLedger
```

with:

```markdown
## Set up gh-ledger
```

Edit 4 of 10 in `docs/guide.md`, replace:

```markdown
**Goal:** add the Copilot requests you made before you installed CopilotLedger.

CopilotLedger records requests from the moment its daemon runs, but Copilot CLI and the GitHub Copilot app also keep a log of each session on your machine. `gh ledger import` records the requests in those logs, prices each one for the date it was made, and attributes it to the branch its session was on. See what it would record for this repository first:
```

with:

```markdown
**Goal:** add the Copilot requests you made before you installed gh-ledger.

gh-ledger records requests from the moment its daemon runs, but Copilot CLI and the GitHub Copilot app also keep a log of each session on your machine. `gh ledger import` records the requests in those logs, prices each one for the date it was made, and attributes it to the branch its session was on. See what it would record for this repository first:
```

Edit 5 of 10 in `docs/guide.md`, replace:

```markdown
Two sessions from yesterday hold five requests, including one by the `explore` subagent and a compaction, the request that summarizes a long conversation. A session in `contoso/api` is out of scope, and the session you're in now is still open, so it's skipped: CopilotLedger records it live. Import the five:
```

with:

```markdown
Two sessions from yesterday hold five requests, including one by the `explore` subagent and a compaction, the request that summarizes a long conversation. A session in `contoso/api` is out of scope, and the session you're in now is still open, so it's skipped: gh-ledger records it live. Import the five:
```

Edit 6 of 10 in `docs/guide.md`, replace:

```markdown
name: CopilotLedger receipt
```

with:

```markdown
name: gh-ledger receipt
```

Edit 7 of 10 in `docs/guide.md`, replace:

```markdown
git commit -m "Add CopilotLedger receipt"
```

with:

```markdown
git commit -m "Add gh-ledger receipt"
```

Edit 8 of 10 in `docs/guide.md`, replace:

```markdown
CopilotLedger records requests to local runtimes, such as Foundry Local and Ollama, at $0.00 and estimates what they saved. Foundry bills BYOK requests to you directly, not through GitHub, so CopilotLedger records them unpriced until you add your prices. Create a rates overlay:
```

with:

```markdown
gh-ledger records requests to local runtimes, such as Foundry Local and Ollama, at $0.00 and estimates what they saved. Foundry bills BYOK requests to you directly, not through GitHub, so gh-ledger records them unpriced until you add your prices. Create a rates overlay:
```

Edit 9 of 10 in `docs/guide.md`, replace:

```markdown
**Goal:** move to a new gh-ledger release, or remove CopilotLedger.
```

with:

```markdown
**Goal:** move to a new gh-ledger release, or remove gh-ledger.
```

Edit 10 of 10 in `docs/guide.md`, replace:

```markdown
To remove CopilotLedger, undo `install`:
```

with:

```markdown
To remove gh-ledger, undo `install`:
```

Edit 1 of 1 in `docs/releasing.md`, replace:

```markdown
Releases are GitHub Releases of this repository. They make `gh extension install swigerb/gh-ledger` work, and `gh ledger rates update` downloads their `rates.v2.json`. The CopilotLedger PR Receipt action is released from [its own repository](https://github.com/swigerb/copilot-ledger-action).
```

with:

```markdown
Releases are GitHub Releases of this repository. They make `gh extension install swigerb/gh-ledger` work, and `gh ledger rates update` downloads their `rates.v2.json`. The PR receipt action is released from [its own repository](https://github.com/swigerb/copilot-ledger-action).
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Docs/ProductNameTests.cs`, replace:

```csharp
            .Replace("\"--app-name=CopilotLedger\"", string.Empty, StringComparison.Ordinal)));
    }
```

with:

```csharp
            .Replace("\"--app-name=CopilotLedger\"", string.Empty, StringComparison.Ordinal)));
    }

    [Fact]
    public void PN2_TheReadmeAndTheDocsNameTheProductGhLedger()
    {
        string[] files = [RepositoryFile("README.md"), .. Directory.GetFiles(RepositoryFile("docs"), "*.md")];

        Assert.Empty(Mentions(files, line => line.Replace("the `CopilotLedger` value under", string.Empty, StringComparison.Ordinal)));
    }
```

### Tests

New: PN2, which fails on `main`. No guide sample changes in this task, and the docs that tests read keep the parts they read: `RateOverlayTests.DocsExamples_MatchRealMessages` (configuration.md's error examples), `AuditDocsTests`, `ExportAllowlistTests`, and `ImportPrivacyTests` (cli.md's tables).

### Acceptance criteria

- README.md and the docs name the product gh-ledger, and PN2 passes.
- `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors, and `dotnet test CopilotLedger.slnx` passes 1734 tests, 0 failed, 0 skipped.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 5 specification: Version 0.5.0, Where `docs/releasing.md` Lists It

### Files

| File | Change |
|---|---|
| `.github/workflows/ci.yml` | The version that the smoke test compares `--version` with |
| `docs/cli.md` | The manifest sample's `toolVersion` |
| `docs/guide.md` | The `status-first` and `status` samples, and the receipt sample's footer and `generator` |
| `docs/releasing.md` | Step 1 lists every place that holds the version; step 4 tags `v0.5.0` |
| `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj` | `<Version>0.5.0</Version>` |
| `src/CopilotLedger.Cli/GhLedgerCli.cs` | The `AssemblyInformationalVersionAttribute` comment and the fallback after `??` |
| `tests/CopilotLedger.Tests/Cli/DaemonCommandTests.cs` | DM1 |
| `tests/CopilotLedger.Tests/Cli/ExportBundleTests.cs` | EB2 |
| `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs` | EC5 |
| `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs` | `StatusText`, which RP1, RP10, RP12, and RP13 compare |
| `tests/CopilotLedger.Tests/Docs/VersionPlacesTests.cs` | New: VR1 |

### What changes

1. 0.4.1 becomes 0.5.0 in every place that `docs/releasing.md` step 1 lists after this task: `<Version>`; the comment and the fallback in `GhLedgerCli.cs`; CI's smoke test; DM1, `StatusText`, EC5, and EB2; the guide's `status-first` and `status` samples and the receipt, which shows the version twice; `docs/cli.md`'s manifest sample; and the tag example.
2. These name an earlier version on purpose and don't change: "gh-ledger 0.4.1 and earlier" in `docs/configuration.md` and in the assets table of `docs/releasing.md`; the comments in `ModelResolutionTests` and `RatesUpdateTests`; the earlier daemon's `daemon.json` in `ImportCommandTests`; and the historical mentions that Task 1 adds. `v0.5.0-rc.1`, `docs/releasing.md`'s example of a prerelease tag, stays.
3. `docs/releasing.md` step 1 names every place, adds EC5, EB2, the manifest sample, and the tag, and says which test checks what, what reads the version as it runs, and which earlier versions to leave.
4. **VR1** (new): with `GhLedgerCli.Version`, which is `<Version>`, `GhLedgerCli.cs` holds `    // AssemblyInformationalVersionAttribute: "<version>"` and `        ?? "<version>";`, `ci.yml` holds `--version | tr -d '\r')" = "<version>"`, `docs/cli.md` holds `  "toolVersion": "<version>",`, and `docs/releasing.md` holds `   git tag v<version>` followed by `   git push origin v<version>`. The other places have their own tests, so a release that misses any place fails a test.

### Code (copy exactly)

Edit 1 of 1 in `.github/workflows/ci.yml`, replace:

```yaml
          test "$(./artifacts/gh-ledger/gh-ledger --version | tr -d '\r')" = "0.4.1"
```

with:

```yaml
          test "$(./artifacts/gh-ledger/gh-ledger --version | tr -d '\r')" = "0.5.0"
```

Edit 1 of 1 in `docs/cli.md`, replace:

```markdown
  "toolVersion": "0.4.1",
```

with:

```markdown
  "toolVersion": "0.5.0",
```

Edit 1 of 4 in `docs/guide.md`, replace:

````markdown
<!-- sample: status-first -->
```shell
gh ledger status
```

```text
gh-ledger 0.4.1 (GitHub CLI extension)
````

with:

````markdown
<!-- sample: status-first -->
```shell
gh ledger status
```

```text
gh-ledger 0.5.0 (GitHub CLI extension)
````

Edit 2 of 4 in `docs/guide.md`, replace:

````markdown
<!-- sample: status -->
```shell
gh ledger status
```

```text
gh-ledger 0.4.1 (GitHub CLI extension)
````

with:

````markdown
<!-- sample: status -->
```shell
gh ledger status
```

```text
gh-ledger 0.5.0 (GitHub CLI extension)
````

Edit 3 of 4 in `docs/guide.md`, replace:

```markdown
> Repository `contoso/webapp` • 4 prompts • commit `4f9c2e1d8b7a` • generated 2026-11-21 21:30 UTC by gh-ledger 0.4.1
```

with:

```markdown
> Repository `contoso/webapp` • 4 prompts • commit `4f9c2e1d8b7a` • generated 2026-11-21 21:30 UTC by gh-ledger 0.5.0
```

Edit 4 of 4 in `docs/guide.md`, replace:

```markdown
>   "generator": "gh-ledger 0.4.1",
```

with:

```markdown
>   "generator": "gh-ledger 0.5.0",
```

Edit 1 of 2 in `docs/releasing.md`, replace:

```markdown
1. Change the version everywhere it's written, and merge that change:
   - `<Version>` in `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`;
   - in `src/CopilotLedger.Cli/GhLedgerCli.cs`, the `AssemblyInformationalVersionAttribute` comment and the fallback after `??`;
   - the version that the smoke test in `.github/workflows/ci.yml` compares with;
   - the tests that expect it: `DaemonCommandTests.DM1` and `ReportCommandTests.StatusText`;
   - the samples in `docs/guide.md` that show it: both `status` samples and the receipt. `GuideSamplesTests` fails until they match.
```

with:

```markdown
1. Change the version everywhere it's written, and merge that change:
   - `<Version>` in `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`;
   - in `src/CopilotLedger.Cli/GhLedgerCli.cs`, the `AssemblyInformationalVersionAttribute` comment and the fallback after `??`;
   - the version that the smoke test in `.github/workflows/ci.yml` compares with;
   - the tests that expect it: `DaemonCommandTests.DM1`, `ReportCommandTests.StatusText`, `ExportCommandTests.EC5`, and `ExportBundleTests.EB2`;
   - the samples in `docs/guide.md` that show it: `status-first`, `status`, and the receipt, which shows it twice. `GuideSamplesTests` fails until they match;
   - the manifest sample under "The bundle" in `docs/cli.md`, as `"toolVersion"`;
   - the tag in step 4.

   `VersionPlacesTests.VR1` fails until the comment, the fallback, the smoke test, the manifest sample, and the tag show the `<Version>`. Everything else reads the version as it runs: `--version`, `status`, `daemon.json` and the daemon's log, receipts, exports, the MCP server, and the `User-Agent` that `rates update` and `audit --local-llm` send. Leave the earlier versions that the docs and tests name on purpose: "gh-ledger 0.4.1 and earlier" in `docs/configuration.md` and in the table of assets below, the comments in `ModelResolutionTests` and `RatesUpdateTests`, and the earlier daemon's `daemon.json` in `ImportCommandTests`.
```

Edit 2 of 2 in `docs/releasing.md`, replace:

```markdown
   git tag v0.4.1
   git push origin v0.4.1
```

with:

```markdown
   git tag v0.5.0
   git push origin v0.5.0
```

Edit 1 of 1 in `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`, replace:

```xml
    <Version>0.4.1</Version>
```

with:

```xml
    <Version>0.5.0</Version>
```

Edit 1 of 1 in `src/CopilotLedger.Cli/GhLedgerCli.cs`, replace:

```csharp
    // AssemblyInformationalVersionAttribute: "0.4.1"
    public static string Version { get; } =
        typeof(GhLedgerCli).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "0.4.1";
```

with:

```csharp
    // AssemblyInformationalVersionAttribute: "0.5.0"
    public static string Version { get; } =
        typeof(GhLedgerCli).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "0.5.0";
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/DaemonCommandTests.cs`, replace:

```csharp
        Assert.Equal("0.4.1", root.GetProperty("version").GetString());
```

with:

```csharp
        Assert.Equal("0.5.0", root.GetProperty("version").GetString());
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/ExportBundleTests.cs`, replace:

```csharp
        Assert.Equal((1, "gh-ledger", "0.4.1"), (root.GetProperty("schemaVersion").GetInt32(), root.GetProperty("tool").GetString(), root.GetProperty("toolVersion").GetString()));
```

with:

```csharp
        Assert.Equal((1, "gh-ledger", "0.5.0"), (root.GetProperty("schemaVersion").GetInt32(), root.GetProperty("tool").GetString(), root.GetProperty("toolVersion").GetString()));
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
        Assert.Equal("2026-11-20 22:00 by gh-ledger 0.4.1", ExportFiles.Meta(page, "Generated"));
```

with:

```csharp
        Assert.Equal("2026-11-20 22:00 by gh-ledger 0.5.0", ExportFiles.Meta(page, "Generated"));
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
    private static string StatusText(string root) =>
        Normalize($"""
        gh-ledger 0.4.1
```

with:

```csharp
    private static string StatusText(string root) =>
        Normalize($"""
        gh-ledger 0.5.0
```

New file `tests/CopilotLedger.Tests/Docs/VersionPlacesTests.cs`:

```csharp
using CopilotLedger.Cli;

namespace CopilotLedger.Tests.Docs;

// Step 1 of docs/releasing.md lists every place that writes the version. The places that no other test reads must show the
// project's <Version> too, so a release that misses one fails here.
public sealed class VersionPlacesTests
{
    [Fact]
    public void VR1_TheListedPlacesShowTheProjectVersion()
    {
        string version = GhLedgerCli.Version;
        string cli = Text("src", "CopilotLedger.Cli", "GhLedgerCli.cs");

        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
        Assert.Contains($"    // AssemblyInformationalVersionAttribute: \"{version}\"\n", cli);
        Assert.Contains($"        ?? \"{version}\";\n", cli);
        Assert.Contains($"--version | tr -d '\\r')\" = \"{version}\"\n", Text(".github", "workflows", "ci.yml"));
        Assert.Contains($"  \"toolVersion\": \"{version}\",\n", Text("docs", "cli.md"));
        Assert.Contains($"   git tag v{version}\n   git push origin v{version}\n", Text("docs", "releasing.md"));
    }

    // A repository file's text with \n line breaks, whatever the checkout's.
    private static string Text(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CopilotLedger.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory.FullName, .. parts])).ReplaceLineEndings("\n");
    }
}
```

### Tests

Changed: DM1, RP1, RP10, RP12, and RP13 (through `StatusText`), EC5, EB2, and GS2's `status-first` and `status` and GS6 (`receipt`). New: VR1.

### Acceptance criteria

- `gh-ledger --version` prints `0.5.0`, and every place that `docs/releasing.md` lists shows it.
- Outside `.github/plans/`, `0.4.1` appears only in the historical places of item 2.
- `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors, and `dotnet test CopilotLedger.slnx` passes 1735 tests, 0 failed, 0 skipped.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Test specifications

Every test is hermetic: temporary directories, `ManualTimeProvider`, `FakeCliPlatform`, `CliHarness`, `McpTestHarness`, and `GuideWorld` only. No test opens a socket, starts a process, runs `git`, or reads `~/.copilot`, `~/.codex`, or `~/.copilot-ledger`. PN1, PN2, and VR1 read the repository's own files, as `ExportPrivacyTests.EP6` and `AuditDocsTests` do, and money is untouched.

| Task | New tests | Changed tests | Total after |
|---|---|---|---:|
| 0 | — (the starting point) | — | 1,731 |
| 1 | GS8 | GS1 (the list of samples) | 1,732 |
| 2 | — | EC9, IN1, IN2, IN10, IN11, IN16, IN17, IN18, RT1, RP3, RP13, GS2 (`install`, `uninstall`) | 1,732 |
| 3 | PN1 | BR4, AD1–AD3, BM1, BM4, BM11, EP1–EP3, EP5, EP10, AM3, MC1, MC3, MC14, GS2 (`export-pr`), GS5, GS6, GS7 | 1,733 |
| 4 | PN2 | — | 1,734 |
| 5 | VR1 | DM1, RP1, RP10, RP12, RP13, EC5, EB2, GS2 (`status-first`, `status`), GS6 | 1,735 |

Exact values used across tasks:

| Value | What | Where |
|---|---|---|
| `claude-haiku-4-5-20251001` | The name that Copilot CLI 1.0.114 reports for Claude Haiku 4.5, an alias of the built-in `claude-haiku-4.5` since Sprint 7 Task 12 | Task 1: GS8, the guide, `docs/cli.md` |
| `68090` | EP10's oversized receipt in bytes, 4 fewer with `gh-ledger` in its heading | Task 3 |
| `0.5.0` | The version | Task 5 |

## Final step: the pull request (orchestrator, not a builder task)

After Task 5, the orchestrator:

1. Runs `dotnet build CopilotLedger.slnx` (0 warnings, 0 errors) and `dotnet test CopilotLedger.slnx` (1735 passed, 0 failed, 0 skipped), and checks with `git --no-pager diff --stat origin/main` that the branch changes only this plan, `.github/plans/archive/sprint-10.md`, `.github/copilot-instructions.md`, the two `.github/agents` files, and the files the tasks list.
2. Publishes the `win-x64` binary and, with a temporary `COPILOT_LEDGER_HOME`, checks that `--version` prints `0.5.0`, that `--help` starts with the new description, and that `rates validate` with GS8's overlay prints the guide's message and exits 1. It never uses a real home directory.
3. Pushes `swigerb-sprint-11-release-0-5-0` and opens one pull request titled exactly "Sprint 11: Release readiness, gh-ledger naming & version 0.5.0", whose body says `Refs #72` and uses no closing keyword (the coordinator closes #72 after the release), with these sections:
   - `## Changes`: the plan commit (this plan, Sprint 10's archive, and the product-name wording of `.github/copilot-instructions.md` and the two agent files) and the five task commits, each with what it changes; the naming decision, its rationale, and its rules; and "What still says CopilotLedger", with each line and the reason it stays.
   - `## Release triage`: 105 items classified, and the one blocker, U1, with its evidence and Task 1; the close calls (a)–(g), one line each; and the non-blockers by group, each with its reason, as "Release triage" and "Deferred to later sprints" give them. Say explicitly that U1 is the only release blocker.
   - `## Verification`: the build (0 warnings), the test totals (1731 before; 1732, 1732, 1733, 1734, and 1735 after the tasks), CI, the byte check of each task, and the rates comparison (GitHub's page unchanged since 2026-11-20, so `rates.json` is unchanged).
   - `## Risks`: Copilot CLI 1.0.115, the latest release since 2026-11-24, whose sessions import as unsupported (exit code 2) and whose live spans aren't verified (A1); U1's users, whom the release notes should tell to remove the overlay entry; fork copies beside unidentified live rows (A2); a 0.4.1 daemon left running after the upgrade on macOS or Linux, which records rows without cache states or agent links until it restarts, while import refuses (a); no downgrade, because 0.4.1 can't open format 6, so keep a copy of `ledger.db` first; text that scripts may match changes (alert titles, the receipt heading, MCP warnings, `--help`, and the report's title), while identifiers don't; the receipt action still calls itself CopilotLedger PR Receipt until it's renamed; and the suite's pool-clearing race (S10-9).
4. Waits for CI on `ubuntu-latest` and `windows-latest`, whose smoke test now expects `--version` to print `0.5.0`. If a job fails only with the dry-run race that "How to use this plan" describes, it reruns the job.
5. Asks `ledger-architect` to review the branch.

Nobody in this sprint creates a tag or a release or runs the Release workflow. After the pull request merges, the coordinator runs issue #72's remaining checks: the receipt end to end, through a draft smoke pull request and swigerb/copilot-ledger-action; a manual Release workflow run on `main` (the dry run); the tag `v0.5.0`; and the assets, the checksums, `rates.v2.json`, the derived `rates.json`, and `rates update` on 0.4.1 and 0.5.0. When the sprint closes, `ledger-architect` archives this plan with its amendments.

## Deferred to later sprints

None of these is a release blocker; "Release triage" gives each one's reason.

- **Upgrades:** a `gh ledger upgrade` that stops and restarts what holds the binary (from Sprint 5); a later migration that repeats migration 4's state updates and migration 6's reconciliation for rows that an old daemon wrote after the upgrade (S6-10.2, S8-10); and backup advice in `docs/cli.md` "Ledger format" that also names `ledger.db-wal`, which holds the latest writes while a Copilot session's MCP server keeps the ledger open.
- **Rates and pricing:** re-pricing unpriced rows (S7-4); MAI list prices and GPT-5.6 Sol's price after 2026-11-30 (S7-5); re-billing rows recorded as `unknown_model` (S7-6); more dated ids (S7-7); longer cache lifetimes (S7-8); a `--pricing` sample in the guide (S7-9); the backfill's discoverability and wording (S7-10); what the API rate card hash covers (S7-11); and what-if fidelity (S7-12).
- **Import:** a Claude Code adapter (S8-1); local BYOK classification (S8-2); wider version windows, now including Copilot CLI 1.0.115 (S8-3); compactions of more than one request, and Codex's compressed rollouts and tool calls (S8-4); VS Code and JetBrains stores (S8-5); re-scanning, incremental parsing, and the unchanged check's cost (S8-6, S8-18); agent grouping for imported spans (S8-7); unmatched supersession, and matching superseded rows later (S8-11, S8-14); a census of fork copies (S8-13, A2); a dedicated exception type (S8-15); `MAJOR.MINOR.PATCH` in warnings (S8-16); a test of a dry run beside a running daemon (S8-17); and session facts that make large sessions linear (S8-19).
- **Export:** a hash-based CSP (S9-1); IANA IDs on Windows 10 1703–1809 (S9-4); every prompt chain, night hours, and PDF or PNG output (S9-5); the ZIP container's bytes across operating systems (S9-6); a distinct color for failed tool calls (S9-7); telling same-named repositories apart (S9-8); a record of the daemon's deletions (S9-9); a migration that clears non-strict slugs (S9-10); axis lines at every zoom and padding at the axis's ends (S9-11, S9-13); and copying, rather than moving, the replaced ZIP (S9-12).
- **Audit:** threshold options and an economy-model argument for MCP (S10-1); an API-equivalent view of impacts (S10-2); streaming answers (S10-4); tool sequences older than 7 days (S10-5); import diagnostics per trace (S10-6); a compaction column (S10-7); and a sortable findings table (S10-8).
- **Reports and cache:** sanitizing ledger strings in every console table (S10-3); user turns, agent definitions, and MCP tools for turns and bloat (S6-6–S6-8); a fresh capture of cache attributes (S6-9); Sprint 6's other review follow-ups (S6-10.1 and S6-10.3–S6-10.6); and coverage notes in reports, receipts, and MCP (S8-8, S9-3).
- **Tests and CI:** fixing the suite's pool-clearing race at its source (S10-9), and query-plan tests (P70-5).
- **Carried from Sprint 5** (S6-11): `gh ledger` in every message, workflow lockfiles, the guide on macOS and Linux, reconciliation with `nano_aiu`, breaker state in `status`, MCP registration in `install`, `COPILOT_LEDGER_HOME` in sign-in items, headless Linux, a tool gate, webhooks, smarter retry churn, signed receipts and a pre-push hook, `export-pr --since`, build provenance, Arm64 smoke tests, checking for newer rates automatically, receipts for fork pull requests, and Sprint 2's carry-overs, as `.github/plans/archive/sprint-5.md` describes them.
- **The name:** renaming swigerb/copilot-ledger-action and its Marketplace listing, now "CopilotLedger PR Receipt", which takes a release of that repository and a new pin in `receipt.yml`; and the one code comment in `GuideWorld.cs` that still says CopilotLedger.
