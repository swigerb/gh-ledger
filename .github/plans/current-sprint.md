# Sprint 7: Dual-Lens Costing: API-Equivalent USD vs. Copilot AI Credits vs. Local Offload Savings

| | |
|---|---|
| Architect | `ledger-architect` |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET 10 (`net10.0`), SDK 10.0.x, zero Docker |
| Plan written | 2026-11-23, against merged Sprint 6 (`main` at `3aaf971`) |
| Requirements | Issue [#61](https://github.com/swigerb/gh-ledger/issues/61), "Dual-lens costing engine: API-equivalent USD vs. Copilot AI Credits vs. local offload savings" |
| Status | Current sprint. Sprint 6 is complete and archived at `.github/plans/archive/sprint-6.md`. |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification section, runs the verification commands, changes the line to `- [x]`, and stops. Tasks 1–11 are sequential, and each builds on the ones before it.

This sprint ships as one pull request. The orchestrator commits this plan first, together with `.github/plans/archive/sprint-6.md`, then each task as its own commit, `Sprint 7 Task N: <Title>`, on branch `swigerb-sprint-7-dual-lens-costing-engine`, and opens one pull request titled exactly "Sprint 7: Dual-lens costing engine: API-equivalent USD vs. Copilot AI Credits vs. local offload savings", with `Closes #61`. After every task, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors (warnings are errors), and `dotnet test CopilotLedger.slnx` passes exactly the number of tests the task gives, with 0 failed and 0 skipped.

All of this plan's code, tests, data, workflow changes, and docs are **copy exactly**. The architect built every task in a scratch repository made from `git archive 3aaf971`, outside this repository's worktree. Each task compiled with `TreatWarningsAsErrors` and passed the whole suite: 831, 864, 934, 939, 949, 955, 963, 968, 970, 982, and 986 tests after Tasks 1–11. The architect then applied this plan's blocks with a script, task by task and in order, to a fresh `git archive 3aaf971` with Windows line endings (as a `core.autocrlf=true` checkout has them), built and tested after each task, and got the same counts and, line endings aside, the same files. Copy each block character for character, including every comment line, `using` directive, blank line, line of JSON, and line of Markdown. Don't add argument checks (`ThrowIfNull` and the like), cancellation tokens, `ConfigureAwait`, logging, caching, or fast paths that the code doesn't show, and don't tidy code or reword docs.

Each task's specification lists its files, summarizes its contracts and algorithms for review, and then gives its code under "Code (copy exactly)". The summaries aren't code to write; the code section is. It has two kinds of blocks:

- **New file `<path>`:** write the block as the whole file, with `\n` line breaks, one `\n` at the end, and no byte order mark.
- **Edit k of m in `<path>`, replace: … with: …:** make a file's edits in the order given. Each "replace" block occurs exactly once in the file at the moment you make that edit, so search for it as an exact string. None of the files this sprint edits has a byte order mark; keep each file's line endings as they are.

Tests are code too: write every test file and test edit exactly as given, in the order given. Don't drop an assertion because it looks redundant, and don't turn an exact comparison into `Contains` or `StartsWith`. Never normalize the product's output in a test (no `ReplaceLineEndings`, `Replace("\r\n", …)`, or `Trim` on an actual value); `CliHarness` already turns `\r\n` into `\n` in `Out` and `Error`.

If a block doesn't apply, the build fails, or the test count differs from the task's, stop and report it. Don't change the plan's code to make it fit.

The final section, "Final step: the pull request", isn't a builder task. It has no checkbox, and `ledger-builder` never runs it.

## Tasks

- [x] Task 1: Rate-Card Schema Version 2 (CopilotLedger.Core, docs & tests). `RateCardReader` accepts `schemaVersion` 1 or 2 and an `apiEquivalent` section (`description`, `localBaselineModel`, and priced `models` with optional `vendor` and `sourceUrl`) only in version 2, where a complete card must have it. `JsonRateCardCalculator` validates and merges the section by model `id`, exposes `SchemaVersion`, and computes `ApiRateCardSha256`, the SHA-256 of a canonical JSON form of the API list prices.
- [x] Task 2: API-Equivalent Pricing (CopilotLedger.Core, docs & tests). New `ApiEquivalentStatus`, `ApiEquivalentCost`, and `ApiEquivalentRequest`; `CreditCalculationResult.ApiEquivalent` prices every request at API list prices beside its billed result, with the same model, alias, window, and tier rules and the same `tokens × rate / 1,000,000` math. `CalculateApiEquivalent` prices a recorded request for backfill and what-if.
- [x] Task 3: Built-In API List Prices (CopilotLedger.Core, docs & tests). The embedded `rates.json` moves to `schemaVersion` 2 and `ratesVersion` `2026-11-23` and gains the public list prices of the 31 GitHub models that have one, each verified on its vendor's page; the three MAI models have none. Tests check every price against an independent transcription and pin the card's hash.
- [x] Task 4: Version 2 Rates Downloads (CopilotLedger.Cli, workflows, docs & tests). `rates update` downloads `rates.v2.json`, falls back to `rates.json` when the latest release has none, and never lets a later `schemaVersion` 1 card replace built-in API list prices. The Release workflow publishes both assets, the v1 one derived from the v2 card, and checks `ratesVersion` against either.
- [x] Task 5: Ledger Schema Version 5 (CopilotLedger.Storage, Otlp, Cli, Mcp & tests). Migration 5 adds the five `api_*` columns, nullable, so rows recorded earlier have no API-equivalent result. `RecordAsync` writes and checks them, rollups and prompt chains gain `ApiEquivalentUsd`, `ApiEquivalentUnpricedSpanCount`, and `ApiEquivalentEstimatedSpanCount`, and the ingestor rejects an API-equivalent cost beyond the pico-USD range.
- [ ] Task 6: Backfill API-Equivalent Prices (CopilotLedger.Storage, Cli, docs & tests). `gh ledger rates backfill-api-equivalent [--dry-run]` gives the spans recorded without a result the one the rates in use give them, records that card's version and hash, and never touches billed columns.
- [ ] Task 7: What-If Pricing in the Store (CopilotLedger.Storage & tests). `GetRollupAsync` and `GetTopPromptChainsAsync` take an optional rate card that prices every span again, in C# `decimal`, for the API-equivalent fields only, without writing.
- [ ] Task 8: API-Equivalent USD in Reports (CopilotLedger.Cli, docs & tests). `status`, `branch`, `agents`, and `top-prompts` gain an `API equiv USD` column (`Unpriced`, `$12.34*` partial, `~$12.34` estimate) with counting footnotes, and JSON fields after `localSavedUsd`; `status` adds a table comparing GitHub-billed and local requests with their API list prices, and a `lenses` JSON object. The guide's samples change to match.
- [ ] Task 9: API-Equivalent USD in Receipts (CopilotLedger.Cli, docs & tests). `export-pr` adds an `API-equivalent (retail)` line and column, in words rather than marks, and the three fields to its JSON data, within the 65,536-byte cap.
- [ ] Task 10: What-If Pricing with --pricing (CopilotLedger.Cli, docs & tests). `status`, `branch`, `agents`, `top-prompts`, and `export-pr` take `--pricing <path>`, a complete rate card whose API list prices replace the recorded API-equivalent results for that report only, labeled in text and JSON; `rates validate --pricing <path>` checks such a file with the same messages.
- [ ] Task 11: API-Equivalent USD over MCP (CopilotLedger.Mcp, docs & tests). `get_branch_ledger`, `get_agent_breakdown`, and `get_top_expensive_prompts` add the three fields, `orderBy` accepts `apiEquivalentUsd`, and no tool takes a file path.

## Starting point

- Sprint 6 is complete. Its Tasks 1–12 merged in PR #66, squash-merged as `3aaf971` from `c94d590`. On 2026-11-23, at `3aaf971`, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors and `dotnet test CopilotLedger.slnx` passes 804 tests (0 failed, 0 skipped), with SDK 10.0.115 (`global.json` asks for 10.0.100 with `rollForward: latestFeature`). Sprint 6's plan is archived at `.github/plans/archive/sprint-6.md`, with its amendments. The latest release is v0.4.1, at `15003fb` (Sprint 5).
- Issue #61 is the requirements source. Its "Prescriptive Implementation Plan" and all 15 acceptance criteria are implemented here, with the deviations listed under "Design decisions"; its names are proposals. The coordinating session added these requirements: the migration is version 5, because Sprint 6 took version 4; every list price is re-verified on its vendor's page; spans priced without cache counters are disclosed as estimates; old binaries keep working with the release's rates; and budgets stay on billed credits.
- Merged behavior this plan depends on (read from the code at `3aaf971`):
  - **Rate card.** The embedded `src/CopilotLedger.Core/Pricing/rates.json` has `schemaVersion` 1, `ratesVersion` `2026-11-20`, 34 `githubAiCredits` models, no `foundryByok` models, and `localOffload.baselineModel` `mai-code-2`. `RateCardReader.ReadRoot` rejects unknown keys and accepts only `schemaVersion` 1. `JsonRateCardCalculator` merges an overlay by model `id` (`MergeModels`), checks identities across the merge, and resolves the local baseline among the GitHub models.
  - **Pricing.** `Calculate` normalizes the response model, or else the request model, and returns `NotBillableOperation` for a non-inference operation and `InvalidUsage` for inconsistent token counts. It then classifies the endpoint in this order: local (loopback, a local host or provider, or a known local model without a host), Foundry BYOK (host suffix or provider), GitHub (no host, or a GitHub host suffix), and otherwise `UnrecognizedEndpoint`. `ModelId` is the canonical id of the card that matched, or else the normalized model. Rates have at most 6 decimal places and are at most 1,000,000, and `AiCreditMath.CostUsd` is `tokens × rate / 1,000,000` in `decimal`, so every cost is a whole number of pico-USD.
  - **Downloads.** `RatesUpdate.DownloadUrl` is `…/releases/latest/download/rates.json`. `RatesUpdate.RunAsync` parses the download with the built-in rules before it writes anything, compares `ratesVersion` dates, and saves only a later card that merges with the overlay. `RateCardFiles.LoadBase` uses `rates.downloaded.json` when it's valid and its `ratesVersion` is later than the built-in one; that replaces the whole base card.
  - **Releases.** `.github/workflows/release.yml`'s `rates` job downloads the latest release's `rates.json` and fails when the built-in card changed without a later `ratesVersion`. The `release` job copies the built-in card to `dist/rates.json` and checksums the binaries and `rates.json`. v0.4.1's only rates asset is `rates.json` (see "Verified facts").
  - **Storage.** `LedgerMigrations` has versions 1–4; Sprint 6's version 4 added the cache states. `RecordAsync` validates every entry before it writes and rejects `CacheCounterState.LegacyUnknown`, which only the migration produces. `ReportedSql` is Sprint 6's rule that a span reported its cache usage when either counter is `reported`. `LedgerRollupRow` and `PromptChainRow` are built by `GetRollupAsync`, `GetTopPromptChainsAsync`, `ReportCommands.Sum`, `ReceiptCommands.NoSpend`, and `LedgerMcpTools.EmptyRollup`. `LedgerIngestor` rejects a span whose billed cost or savings don't convert to pico-USD ("cost out of range").
  - **CLI.** `status`, `branch`, `agents`, and `top-prompts` share `ReportCommands.MoneyColumns` (Spans, Unpriced, AI credits, GitHub USD, BYOK USD, Local saved). `status` sums a `Repository` rollup per period. JSON totals come from `LedgerFormat.WriteTotals`, which receipts use too. `LedgerFormat` is internal, and the test project has no `InternalsVisibleTo`.
  - **Receipts.** `export-pr` writes `ReceiptMarkdown.ToMarkdown`, with the JSON data block from `ToJson` (`schemaVersion` 1), and refuses a receipt over `ReceiptMarkdown.MaximumBytes` (65,536). The CopilotLedger PR Receipt action reads the receipt's `schemaVersion` and branch, so new JSON fields don't affect it.
  - **MCP.** `gh ledger mcp` serves five tools with hand-written schemas (`additionalProperties: false`) and per-tool allowed-argument lists, so an unknown argument is an error; every tool but `set_branch_budget` is read-only, all have `OpenWorldHint = false`, and results start with a `WARNING:` line for each open breaker trip or exceeded budget. Budgets compare billed AI credits (GitHub plus Foundry BYOK at $0.01 a credit) with the limit.
  - **Tests.** xUnit v2 (`xunit` 2.9.3); `TheoryData` takes at most 10 type arguments. `RateFixtures.ByokTestRatesJson` is a `schemaVersion` 1 card; `PublishedRates` builds release cards from the embedded text. `GuideSamplesTests` runs every `docs/guide.md` sample against `GuideWorld`; `ReportCommandTests`, `ReceiptCommandTests`, and `LedgerMcpServerTests` pin exact output.
  - **CI.** `.github/workflows/ci.yml` builds and tests on `ubuntu-latest` and `windows-latest`, then smoke-tests the published binary: it greps `branch --all-repos --since all --json` for `"spans": 1,` and `"aiCredits": 50,`, runs `status` and `rates validate`, and checks that the MCP `tools/list` includes `get_branch_ledger`. This sprint keeps all of that passing: new JSON fields come after existing ones, so neither grepped property becomes the last one of its object.

## Verified facts (retrieved 2026-11-23)

### GitHub's AI credit prices

Source: [Models and pricing](https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing) (GitHub Docs), the `source` of the embedded card. The page's per-token prices for the 34 models match the embedded card's `githubAiCredits` section, so this sprint doesn't change them. 1 AI credit is $0.01.

### Vendor API list prices

Every price below was read on its vendor's page on 2026-11-23, for standard processing on the vendor's global endpoint (not batch, flex, priority, fast mode, or regional prices), in USD per 1,000,000 tokens. The coordinating session's research of the same day (`retail-rates-research.md`) was an input, not a source; each value was checked against the page again.

- **Anthropic** ([Pricing](https://platform.claude.com/docs/en/about-claude/pricing); [Model deprecations](https://platform.claude.com/docs/en/about-claude/model-deprecations)): base input, 5-minute cache write (1.25× input, the API's default cache lifetime), 1-hour cache write (2×), cache hit, and output for each model. Claude Haiku 5.5 bills long-context prices above 100,000 input tokens, counting cache reads and writes. Claude Sonnet 4 was retired on the Claude API on 2026-06-15. The API's model ids use dashes (`claude-opus-4-8`), and some models have dated snapshot ids (`claude-haiku-4-5-20251001`, `claude-sonnet-4-20250514`).
- **OpenAI** ([Pricing](https://developers.openai.com/api/docs/pricing); [Deprecations](https://developers.openai.com/api/docs/deprecations); [Changelog](https://developers.openai.com/api/docs/changelog)): standard-tier input, cached input, cache write (30-minute default lifetime, 1.25× input), and output, with long-context prices above 272K input tokens. GPT-5.6 Sol's promotional prices run "until November 30, 2026", and no later price is published; its current prices date from 2026-08-21. GPT-5.6 Luna's prices were cut 80% on 2026-07-30, and its long-context threshold is 272K (GitHub's card uses 200K for its own rate). GPT-5.3-Codex and GPT-5.4 nano shut down on the API on 2027-04-01 and list no cache-write price; GPT-5.4 nano's snapshot is `gpt-5.4-nano-2026-03-17`.
- **Google** ([Gemini API pricing](https://ai.google.dev/gemini-api/docs/pricing)): Gemini 4 Argon and Gemini 4 Carbon are $2.00 input, $0.10 cached, and $10.00 output through November 30, 2026, then $4.00, $0.20, and $20.00 from December 1; Gemini 3.8 Flash is $0.75, $0.075, and $3.75 through December 31, 2026, then $1.50, $0.15, and $7.50. Gemini 4 Hydrogen is $1.50, $0.075, and $7.50, and Gemini 4 Neon $3.00, $0.15, and $15.00. Google lists context-caching storage per hour but no cache-write token price.
- **xAI** ([Pricing](https://docs.x.ai/developers/pricing); [Grok 4.5](https://docs.x.ai/developers/models/grok-4.5)): long-context prices apply from 200,000 input tokens ("≥ 200k"). Grok 4.5's cached input is $0.30 (and $0.60 long-context), not GitHub's $0.50 and $1.00; Grok 4.6 and 4.7 are $0.50 and $1.00. No cache-write price.
- **Moonshot** ([Chat pricing](https://platform.kimi.ai/docs/pricing/chat); [Context caching](https://platform.kimi.ai/docs/guide/context-caching.md)): Kimi K3 is $3.00 input, $0.30 cache hit, $3.00 cache write (5-minute lifetime), and $15.00 output, from the page's price table.
- **Microsoft** ([Foundry models sold by Microsoft](https://azure.microsoft.com/en-us/pricing/details/ai-foundry-models/microsoft/); [Models sold directly by Azure](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure); [MAI Code 2 launch](https://microsoft.ai/news/introducing-mai-code-2-and-mai-code-2-flash/)): no public API list price for `mai-code-2`, `mai-code-2-flash`, or `mai-code-1.1-flash`. The Azure page's only MAI row, "MAI Code Flash 1.1", is in its fine-tuning table and showed no price ("$-") on 2026-11-23; the coordinating session's research had read hosting and fine-tuning usage prices from the page's data, but those aren't inference list prices for the Copilot model, so this plan doesn't use them.

So 31 of the 34 GitHub models have a vendor list price. For 30 of them the per-token prices equal GitHub's; Grok 4.5's cached input differs, and so do GPT-5.6 Luna's long-context threshold and the windows below. The built-in `apiEquivalent` section (Task 3) is exactly this table:

| Model | Vendor | Window (UTC) | Tier (max input) | Input | Cached input | Cache write | Output |
|---|---|---|---|---:|---:|---:|---:|
| `claude-opus-5.5` | anthropic | … – … | default | 4 | 0.2 | 5 | 20 |
| `claude-sonnet-5.5` | anthropic | … – … | default | 2 | 0.1 | 2.5 | 10 |
| `gpt-6.1-sol` | openai | … – … | default (≤ 272,000) | 2 | 0.1 | 2.5 | 10 |
| `gpt-6.1-sol` | openai | … – … | long_context | 4 | 0.2 | 5 | 15 |
| `gpt-6-astra` | openai | … – … | default (≤ 272,000) | 10 | 1 | 12.5 | 50 |
| `gpt-6-astra` | openai | … – … | long_context | 20 | 2 | 25 | 75 |
| `gpt-5.3-codex` | openai | … – 2027-04-01 | default | 1.75 | 0.175 | — | 14 |
| `gpt-5.4-nano` | openai | … – 2027-04-01 | default | 0.2 | 0.02 | — | 1.25 |
| `gpt-5.6-luna` | openai | 2026-07-30 – … | default (≤ 272,000) | 0.2 | 0.02 | 0.25 | 1.2 |
| `gpt-5.6-luna` | openai | 2026-07-30 – … | long_context | 0.4 | 0.04 | 0.5 | 1.8 |
| `gpt-5.6-sol` | openai | 2026-08-21 – 2026-12-01 | default (≤ 272,000) | 4 | 0.4 | 5 | 20 |
| `gpt-5.6-sol` | openai | 2026-08-21 – 2026-12-01 | long_context | 8 | 0.8 | 10 | 30 |
| `gpt-6-luna` | openai | … – … | default (≤ 272,000) | 0.1 | 0.01 | 0.125 | 0.5 |
| `gpt-6-luna` | openai | … – … | long_context | 0.2 | 0.02 | 0.25 | 0.75 |
| `gpt-6-sol` | openai | … – … | default (≤ 272,000) | 2 | 0.2 | 2.5 | 10 |
| `gpt-6-sol` | openai | … – … | long_context | 4 | 0.4 | 5 | 15 |
| `gpt-6.1-terra` | openai | … – … | default (≤ 272,000) | 1 | 0.05 | 1.25 | 5 |
| `gpt-6.1-terra` | openai | … – … | long_context | 2 | 0.1 | 2.5 | 7.5 |
| `gpt-6.2-astra` | openai | … – … | default (≤ 272,000) | 8 | 0.4 | 10 | 40 |
| `gpt-6.2-astra` | openai | … – … | long_context | 16 | 0.8 | 20 | 60 |
| `gpt-6.2-sol` | openai | … – … | default (≤ 272,000) | 2 | 0.1 | 2.5 | 10 |
| `gpt-6.2-sol` | openai | … – … | long_context | 4 | 0.2 | 5 | 15 |
| `claude-haiku-4.5` | anthropic | … – … | default | 1 | 0.1 | 1.25 | 5 |
| `claude-haiku-5.5` | anthropic | … – … | default (≤ 100,000) | 0.1 | 0.01 | 0.125 | 0.5 |
| `claude-haiku-5.5` | anthropic | … – … | long_context | 0.5 | 0.05 | 0.625 | 2.5 |
| `claude-sonnet-4` | anthropic | … – 2026-06-15 | default | 3 | 0.3 | 3.75 | 15 |
| `claude-sonnet-4.6` | anthropic | … – … | default | 3 | 0.3 | 3.75 | 15 |
| `claude-opus-4.8` | anthropic | … – … | default | 5 | 0.5 | 6.25 | 25 |
| `claude-sonnet-5` | anthropic | … – … | default | 2 | 0.2 | 2.5 | 10 |
| `claude-fable-5` | anthropic | … – … | default | 10 | 1 | 12.5 | 50 |
| `claude-fable-5.1` | anthropic | … – … | default | 10 | 0.25 | 12.5 | 50 |
| `claude-fable-5.2` | anthropic | … – … | default | 8 | 0.2 | 10 | 40 |
| `gemini-3.8-flash` | google | … – 2027-01-01 | default | 0.75 | 0.075 | — | 3.75 |
| `gemini-3.8-flash` | google | 2027-01-01 – … | default | 1.5 | 0.15 | — | 7.5 |
| `gemini-4-argon` | google | … – 2026-12-01 | default | 2 | 0.1 | — | 10 |
| `gemini-4-argon` | google | 2026-12-01 – … | default | 4 | 0.2 | — | 20 |
| `gemini-4-carbon` | google | … – 2026-12-01 | default | 2 | 0.1 | — | 10 |
| `gemini-4-carbon` | google | 2026-12-01 – … | default | 4 | 0.2 | — | 20 |
| `gemini-4-hydrogen` | google | … – … | default | 1.5 | 0.075 | — | 7.5 |
| `gemini-4-neon` | google | … – … | default | 3 | 0.15 | — | 15 |
| `grok-4.5` | xai | … – … | default (≤ 199,999) | 2 | 0.3 | — | 6 |
| `grok-4.5` | xai | … – … | long_context | 4 | 0.6 | — | 12 |
| `grok-4.6` | xai | … – … | default (≤ 199,999) | 2 | 0.5 | — | 6 |
| `grok-4.6` | xai | … – … | long_context | 4 | 1 | — | 12 |
| `grok-4.7` | xai | … – … | default (≤ 199,999) | 2 | 0.5 | — | 6 |
| `grok-4.7` | xai | … – … | long_context | 4 | 1 | — | 12 |
| `kimi-k3` | moonshot | … – … | default | 3 | 0.3 | 3 | 15 |

`…` is an open window bound; `—` means the vendor lists no such price, so those tokens are priced as input.

### Release assets and `gh`

- `gh release view v0.4.1 --repo swigerb/gh-ledger --json assets` lists the six binaries, `rates.json` (with `"schemaVersion": 1`), and `gh-ledger-checksums.txt`, and no `rates.v2.json`.
- `gh release download --repo swigerb/gh-ledger --pattern rates.v2.json --dir <dir>` exits 1 with "no assets match the file pattern", which the Release workflow's `if` relies on (Task 4).

### gh-ledger 0.4.1 and a `schemaVersion` 2 card

- **Code** (at `15003fb`, tag `v0.4.1`): `RatesUpdate.RunAsync` calls `JsonRateCardCalculator.Parse` on the downloaded text before it writes anything, and `RateCardReader.ValidateSchemaVersion` throws `schemaVersion: must be 1`, so the command prints `The downloaded rates are invalid: schemaVersion: must be 1` and exits 1, keeping `rates.downloaded.json`.
- **Scratch run.** The architect built `git archive 15003fb` with a throwaway test (not part of this plan) that ran `rates update` through `FakeHttpHandler`. Given Task 3's card, it exited 1 with exactly that message on standard error and left an existing `rates.downloaded.json` byte for byte; given the derived `schemaVersion` 1 card that Task 4's workflow publishes as `rates.json`, it printed `Updated rates from 2026-11-20 to 2026-11-23.`, and `rates validate` and `status` then used it (`Rates:   2026-11-23 (downloaded)`). `jq` isn't installed on the architect's machine, so the derivation `jq '.schemaVersion = 1 | del(.apiEquivalent)'` was reproduced with `System.Text.Json.Nodes`, as `PublishedRates.SchemaVersion1` (Task 4) does.

### .NET, SQLite, and hashing (probed in the scratch build)

- `Microsoft.Data.Sqlite` (the bundled SQLite) adds a nullable column with a `CHECK` to a `STRICT` table with `ALTER TABLE … ADD COLUMN`, and drops it again with `DROP COLUMN`. `SUM` over an `INTEGER` column ignores `NULL`s and is `NULL` when every value is `NULL`. `BeginTransaction(deferred: true)` starts a deferred transaction whose reads see one snapshot in WAL mode.
- Every API-equivalent cost is a whole number of pico-USD, as billed costs are, because rates have at most 6 decimal places: for example 1 cache-read token at $0.30 per 1M is `0.0000003` USD, 300,000 pico-USD. `long.MaxValue` pico-USD is about $9.22 million.
- `JsonNode.DeepEquals` compares objects without regard to property order, which the MCP tests rely on. `JsonElement.GetRawText` keeps the source's indentation.
- System.CommandLine 2.0.15: `result.GetResult(option) is { Implicit: false }` is true only for an option the user typed, and a validator's error goes to standard error with exit code 1.
- The three pinned canonical hashes (see "The canonical API rate card hash") were computed again by an independent Python implementation of the canonical form, from the card files, and match.

### Assumptions (not verified)

- **Prices before the vendors' pages.** Vendors publish current prices only. A window without `effectiveFrom` assumes the current price held earlier too, except where a page documents a change: GPT-5.6 Luna's window starts on 2026-07-30 and GPT-5.6 Sol's on 2026-08-21, so their earlier requests are unpriced.
- **Times.** Vendors give dates without times or time zones. Windows start and end at 00:00 UTC, and a promotion ends at the start of the day after its last day.
- **Thresholds.** "272K" is 272,000 tokens and xAI's "≥ 200k" is 200,000 (so its default tier's inclusive `maxInputTokens` is 199,999). The thresholds count all input tokens, cache reads and writes included; Anthropic says so for Claude Haiku 5.5, and it's assumed for OpenAI and xAI.
- **GPT-5.6 Sol after its promotion.** OpenAI publishes no price after 2026-11-30, so later requests are unpriced until a rates update adds one.
- **Ids.** The ids Copilot reports for these models are the embedded card's `githubAiCredits` ids, as in earlier sprints; the vendors' own ids are aliases.

## Design decisions

### Rate-card schema version 2 (deviation D1)

The card stays one document, as the issue asks, so the overlay, downloads, and `rates validate` keep working on one file. `schemaVersion` may be 1 or 2, and only version 2 may have the new `apiEquivalent` section. The section has `description` (text), `models` (priced models, each with optional `vendor` and `sourceUrl`), and `localBaselineModel` (see "Local requests").

- `apiEquivalent` models follow every rule of the other priced sections: at most 6 decimal places, rates up to 1,000,000, sorted non-overlapping windows, tiers with increasing `maxInputTokens`, and ids and aliases unique within the section, with JSON-path errors. A GitHub or Foundry BYOK model may share an id with an API-equivalent model.
- **Deviation D1.** The issue makes the section optional in version 2. A complete card (the built-in card, a download, or a `--pricing` file) with `schemaVersion` 2 must have it, with `models`; an overlay needn't. A version 2 card without list prices would turn the whole lens into `Unpriced` without saying why, and requiring the section catches a broken release asset or what-if file.
- An overlay merges `apiEquivalent` models by `id` (replace or add), and its `localBaselineModel` replaces the base's. The merged card's `schemaVersion` is the higher of the two, so a version 1 overlay works on the version 2 built-in card.

### The canonical API rate card hash

Every recorded result names the card that judged it by `ratesVersion` and `api_rate_card_sha256`, the lowercase hex SHA-256 of this UTF-8 text, which no formatting, key order, or line ending of the card changes:

1. A compact JSON object (no whitespace, `UnsafeRelaxedJsonEscaping`): `{"localBaselineModel":<id or null>,"models":[…]}`, where `localBaselineModel` is the normalized id of the API-equivalent model that prices local requests, after resolution (see "Local requests").
2. `models` sorted ordinally by normalized id. Each model is `{"id":…,"aliases":[…],"windows":[…]}`: its normalized id, its normalized aliases sorted ordinally, and its windows in card order.
3. Each window is `{"effectiveFrom":…,"effectiveUntil":…,"tiers":[…]}`, with bounds as UTC round-trip (`"O"`) strings, such as `"2026-12-01T00:00:00.0000000+00:00"`, or `null`. Each tier is `{"name":…,"maxInputTokens":…,"inputPer1M":…,"cachedInputPer1M":…,"cacheWritePer1M":…,"outputPer1M":…}`, in card order, with rates as JSON numbers formatted `0.######` (exact, because rates have at most 6 decimal places) or `null`.
4. `vendor`, `sourceUrl`, `description`, and `notes` aren't hashed, because they don't change a price.

Pinned values: a card without API-equivalent models, `{"localBaselineModel":null,"models":[]}`, is `49a4384900a30f23c85c0bb1f208f01164855e992f1da709cca54615f954746b`; `RateFixtures.RetailTestRatesJson` is `0075a4b28916a99fc339c7c8acad551cd5828b35c094d60d3fb9e0b0d939977c`; Task 3's built-in card is `634d783f8123f6aa8555220e830f7016b883083af08a043f29ca975a6b725045`.

### Built-in list prices and their windows

- The built-in card gets the 31 list prices in "Verified facts", with `vendor` and `sourceUrl`, and 7 more `notes` that state the policies below. Models without a public list price (the three MAI models) are left out and price as `Unpriced`; no rate is invented.
- **Window policy.** Windows start and end at 00:00 UTC. A promotion ends the day after its last day; Google publishes the prices that follow (Argon and Carbon from 2026-12-01, Gemini 3.8 Flash from 2027-01-01), and OpenAI publishes none after GPT-5.6 Sol's, so its window ends on 2026-12-01. A retired or shut-down model's prices end on that date: Claude Sonnet 4 on 2026-06-15, and GPT-5.3-Codex and GPT-5.4 nano on 2027-04-01. A documented price change starts a window (GPT-5.6 Luna 2026-07-30, GPT-5.6 Sol 2026-08-21).
- **Cache writes** are priced for each vendor's default (shortest) cache lifetime: 5 minutes at Anthropic and Moonshot, 30 minutes at OpenAI. Google and xAI list no cache-write price, so their cache writes price as input, as the credit lens already does for a missing rate.
- **Aliases.** A vendor's own API id (`claude-opus-4-8`) and a dated snapshot id (`claude-haiku-4-5-20251001`) are aliases of the Copilot id. Moving aliases such as `grok-4.5-latest` aren't added, because they can point at another model later.
- **`ratesVersion` moves to `2026-11-23`**, because the card changed (the Release workflow requires it), and every test and doc that shows the version changes with it.

### Local requests (deviation D2)

The issue prices a local request as the `apiEquivalent` entry of `localOffload.baselineModel`. That baseline is `mai-code-2`, which has no list price, so with the built-in card every local request is `Unpriced`. This plan keeps that default, and so follows the issue, and adds one optional field (**deviation D2**): `apiEquivalent.localBaselineModel` names the API-equivalent model that prices local requests, and must resolve to one. Without it, local requests use the API-equivalent model with the `localOffload.baselineModel` id or alias, when there is one. A user who wants local work valued at, say, Claude Sonnet 5.5's list price sets the field in the overlay; tests use a synthetic card whose baseline has a list price. `EstimatedCloudSavedUsd`, the savings at GitHub's baseline rates, doesn't change.

### Pricing a request

- `JsonRateCardCalculator.Calculate` fills `CreditCalculationResult.ApiEquivalent` on every result, whatever the credit lens decided. It prices the request as its credit-lens `ModelId` (the canonical GitHub or Foundry BYOK id when a card matched, else the normalized reported model), so aliases resolve once, and a local request as the local retail baseline. Only the channel's `Local` value matters: a request on an unrecognized endpoint, or a GitHub model unknown to GitHub's card, still gets an API-equivalent price when its model has one.
- `NotApplicable`: a non-inference operation (such as `invoke_agent`) or inconsistent token counts. Such requests are never priced in any lens, so an invalid token partition never gets a made-up price. `Unpriced`: no API-equivalent model, or no window for the request's start time. `UsdCost` is `null` unless `Priced`, never 0 for an unknown price.
- The math is the credit lens's: `AiCreditMath.CostUsd(tokens, rate)` for uncached input, cache reads (`cachedInputPer1M`, else `inputPer1M`), cache writes (`cacheWritePer1M`, else `inputPer1M`), and output, with the tier chosen by total input tokens.
- `CalculateApiEquivalent(ApiEquivalentRequest)` is the same pricing for a recorded request: the stored operation, billing channel, model id, start time, and token counts. Ingestion, backfill, and what-if all use it, so they always agree.
- **Estimates.** Sprint 6 stores whether each cache counter was reported. A request priced without cache counters (both `Absent` or `LegacyUnknown`) was priced as if all of its input were uncached, which can overstate its cost. Such a priced request is an *estimate*: rollups count them (`ApiEquivalentEstimatedSpanCount`, with Sprint 6's `ReportedSql`), tables mark the amount with `~` and a footnote, JSON and MCP carry `apiEquivalentEstimatedSpans`, and receipts say "estimated".

### Storage: schema version 5 (deviation D3)

- Migration 5 adds the issue's five columns, all nullable: `api_equivalent_usd_pico INTEGER` (`CHECK … >= 0`), `api_equivalent_status TEXT` (`CHECK … IN ('priced', 'unpriced', 'not_applicable')`), `api_rate_card_model_id`, `api_rates_version`, and `api_rate_card_sha256`. Rows recorded before it keep `NULL` in all five; `api_equivalent_status IS NULL` marks such a legacy row. A version 1 ledger still migrates through 2, 3, 4, and 5.
- `RecordAsync` writes the five columns from `ApiEquivalent`: the pico-USD cost or `NULL`, the status, the model id or `NULL`, and the card's `ratesVersion` and hash on every row. It rejects a result that's `Priced` without a model and breakdown, or has one without being `Priced`, or has an undefined status or no rates. `rates_version`, the billing card's version, doesn't change meaning.
- Rollups and prompt chains: `ApiEquivalentUsd` is `SUM(api_equivalent_usd_pico)`, an integer `SUM` that ignores `NULL`, so it's `null` when the group has spans but none is priced. `ApiEquivalentUnpricedSpanCount` counts the group's spans without a price (unpriced, not applicable, and legacy). `ApiEquivalentEstimatedSpanCount` counts priced spans that reported no cache counters. Rows built in C# for an empty group (`NoSpend`, `EmptyRollup`, and `Sum` of no rows) have `ApiEquivalentUsd` 0, because nothing is unpriced.
- **Deviation D3, the cost range.** The ingestor checks that the API-equivalent cost converts to pico-USD, as it checks the billed cost and savings, and rejects the span as "cost out of range" when it doesn't (over about $9.22 million for one request). A comparison should never drop billed spend, but only absurd token counts reach the limit, and the existing rule already treats savings, another estimate, this way.

### Backfill

`gh ledger rates backfill-api-equivalent [--dry-run]` loads the rates the daemon would (base rates and overlay), then `SqliteLedgerStore.BackfillApiEquivalentAsync` reads every row with `api_equivalent_status IS NULL` and prices it with `CalculateApiEquivalent` as of its start time. Without `--dry-run`, it writes only the five `api_*` columns, with that card's `ratesVersion` and hash, `WHERE id = @id AND api_equivalent_status IS NULL`, in one immediate transaction that also holds the read, so spans the daemon records meanwhile (which have a result) never mix in. It never changes `cost_usd_pico`, `saved_usd_pico`, `billing_channel`, `pricing_status`, `rates_version`, or any other column, and skips rows that already have a result, so it records each result once. A cost beyond the pico-USD range stays `NULL` and is counted as out of range. `--dry-run` reads in a deferred transaction and writes nothing. Rows recorded as `unpriced` stay that way; `--pricing` compares them at other prices.

### What-if pricing (deviations D4 and D5)

- `GetRollupAsync` and `GetTopPromptChainsAsync` gain overloads with a `JsonRateCardCalculator? whatIf`. With a card, the store streams each matching span's group key, operation, channel, model id, start time, and token counts, prices it with `CalculateApiEquivalent` in C# `decimal`, and sums per group; every other field comes from the same SQL as before, in the same deferred read transaction. Nothing is written. Prompt chains and their order don't depend on the card.
- `--pricing <path>` on `status`, `branch`, `agents`, `top-prompts`, and `export-pr` takes a complete, standalone rate card (`WhatIfPricing.Load`): read on its own, never merged with the overlay or downloaded rates, `schemaVersion` 2. Only the API-equivalent amounts change; AI credits, GitHub USD, BYOK USD, local savings, counts, warnings, and budgets keep their recorded values. Reports add `Pricing: what-if <path> (ratesVersion <v>), for API-equivalent amounts only` under the title (in `status`, under `Rates:`), and JSON adds `pricingSource` (`kind` `whatIf`, `path`, `ratesVersion`, `apiRateCardSha256`). A receipt names the file without its folder, because receipts are posted publicly. `--pricing` can't be used with `--cache`.
- **Deviation D4.** The issue says an invalid file "fails with the same message as `rates validate --file`", but `--file` validates an overlay, which may be partial. This plan adds `gh ledger rates validate --pricing <path>`, which checks a complete card with the same code, so the reports and `rates validate --pricing` print the same `Invalid pricing file: <problem>` message.
- **Deviation D5.** The issue lists the spans' "recorded local baseline identity" among the streamed fields. A local span is priced as the what-if card's own local retail baseline instead, as ingestion would with that card, because the recorded `rate_card_model_id` is the GitHub baseline of the card that billed it, which a standalone card needn't price.
- MCP tools don't take `--pricing` or any path.

### Reports and the three lenses (deviation D6)

- `MoneyColumns` gains `API equiv USD` after `BYOK USD`, in `status`, `branch`, and `agents`, and `top-prompts` gains the same column. A cell is `Unpriced` when the group has spans and none is priced; otherwise the amount, rounded to cents with `MidpointRounding.AwayFromZero` (`LedgerFormat.Usd`), with `*` after it when some of the group's spans are unpriced and `~` before it when some priced spans reported no cache counters. An empty group shows `$0.00`.
- Footnotes follow Sprint 6's style: after a blank line, `<n> of <m spans> had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.` and `<n> priced span(s) reported no cache counters, so all of its/their input is priced as uncached; ~ marks an estimate.`, each only when a cell uses its mark. The counts are the table's totals; in `status`, whose rows overlap, they count the last 30 days and say so.
- `status` adds a second table, the three lenses side by side per period: `GitHub USD` and the same GitHub-billed requests `At API prices`, their ratio `GitHub/API`, then `Local saved` and the local requests `Local at API prices`. It comes from a `ChannelModel` rollup per period, summed per channel. The ratio is AI credits × $0.01 divided by the API-equivalent amount of the same requests, shown to two decimal places (`1.00x`, `~` for an estimate) only when every GitHub-billed request has both prices and the denominator isn't 0, and `n/a` otherwise. Retail and avoided amounts are never added to billed spend.
- **Deviation D6** (presentation the issue leaves open or gives as an example): the `~` mark and its footnote (the coordinating session's estimate disclosure); the footnote wording and per-table counts, instead of the example `* 3 spans unpriced in API-equivalent lens`; and the lens table's columns, including `Local at API prices`, which answers the issue's "savings measured against one baseline only".
- JSON keeps the camelCase names: the issue's `credits_used` is the existing `aiCredits`, `local_offload_saved_usd` is `localSavedUsd`, and `api_equivalent_usd` is the new `apiEquivalentUsd`, an exact number or `null`. Report rows add `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans` after the totals (`LedgerFormat.WriteApiEquivalent`, called after `WriteTotals`, so receipts change only in Task 9). `status --json` periods add `lenses` (`copilot` and `local`, with `billedToApiRatio`) before `cache`.
- Formatting uses `CultureInfo.InvariantCulture`, checked under `de-DE`.

### Receipts

The summary adds `- **API-equivalent (retail):**` after the other spend lines: `none` without spans, `Unpriced (<n turns> without an API-equivalent price)` when nothing is priced, and otherwise `$<amount> at API list prices`, followed by `• <n turns> unpriced` and `• estimated: <n turns> reported no cache counters` when they apply. With `--pricing`, it says `at what-if prices from <file>, ratesVersion <v>`, in code spans. The details table gains an `API-equivalent` column: `Unpriced`, the amount, or the amount with `(partial)`, `(estimate)`, or `(partial, estimate)`. Receipts use words, not `*` and `~`, which are Markdown emphasis and strikethrough syntax. The JSON data adds the three fields to `totals` and each model, and `pricingSource` with `--pricing`; `schemaVersion` stays 1, because the change only adds fields. The receipt stays within 65,536 bytes; EP10's oversized receipt now measures 68,094 bytes.

### MCP

`get_branch_ledger`, `get_agent_breakdown`, and `get_top_expensive_prompts` add the three fields after `localSavedUsd` (`apiEquivalentUsd` is `null` when unpriced), and `get_branch_ledger`'s description mentions them. `orderBy` accepts `apiEquivalentUsd`: chains with a price first, highest first, then the unpriced ones, each group keeping the existing order (cost, then start time, then trace ID). Budgets and `billedAiCredits` stay on billed spend. No tool gains an argument, so no tool takes a path; the schemas keep `additionalProperties: false`, and an unknown argument such as `pricing` is still an error. `ReadOnlyHint`, `OpenWorldHint`, and the `WARNING:` lines don't change.

### Downloads and releases (deviation D7)

- New binaries download `releases/latest/download/rates.v2.json`. When the latest release has none (HTTP 404, as for v0.4.1), they download that release's `rates.json` instead. A later card with a lower `schemaVersion` than the built-in card is never saved: `rates update` reports `The published rates (<v>) are schemaVersion 1, without API-equivalent prices; this gh-ledger needs schemaVersion 2. Nothing changed.` and exits 1; a same or older version reports as before.
- **The `LoadBase` hazard.** A downloaded card that's later than the built-in one replaces the whole base. So `LoadBase` ignores a later download whose `schemaVersion` is lower than the built-in card's, with the warning `ignored <path>: its schemaVersion 1 rates have no API-equivalent prices; run gh-ledger rates update to replace them.` Such a file can exist only if an older binary downloaded it (or a user wrote it). `rates update` then replaces it with the release's `rates.v2.json`.
- **Release workflow.** The `release` job checks that the built-in card has `schemaVersion` 2, publishes it as `rates.v2.json`, and publishes `rates.json` for gh-ledger 0.4.1 and earlier, derived with `jq '.schemaVersion = 1 | del(.apiEquivalent)'`; checksums cover both. The `rates` job compares the built-in card with the latest release's `rates.v2.json`, or with its `rates.json` when it has none, so the first v2 release compares with v0.4.1's v1 card and requires the moved `ratesVersion`.
- **Old binaries.** gh-ledger 0.4.1 downloads `rates.json`, which stays a valid version 1 card with the same credit prices (RU14 checks every one), so it keeps updating. Given a version 2 card, it fails closed (see "Verified facts"); RU12 checks the same path in new binaries with `schemaVersion` 3.
- **Deviation D7.** The issue asks only to point `RatesUpdate` at `rates.v2.json` and keep publishing a v1 `rates.json`; the fallback, the schema check in `rates update`, and the `LoadBase` rule are additions that keep the first release after this sprint, and an ignored download, safe.

### Docs

`docs/configuration.md` documents schema version 2, the `apiEquivalent` section and its overlay rules, the built-in prices and their policies, API-equivalent pricing and estimates, the backfill, `--pricing`, and the `rates.v2.json` asset. `docs/cli.md` documents the column, its marks and footnotes, the lens table, the JSON fields and their names, `--pricing`, the backfill, the receipt line, and the MCP fields. `docs/releasing.md` lists both rates assets. `docs/guide.md` explains the column and `--pricing`, and its `status`, `status-first`, `branch`, `agents`, `top-prompts`, `receipt`, and `mcp` samples change to match, checked by `GuideSamplesTests`. `README.md` lists the feature.

### Deviations from issue #61

| # | Issue #61 | This plan | Why |
|---|---|---|---|
| — | Migration "v4", "migrating from v3 to v4" | Migration 5, from version 4; a version 1 ledger still migrates through every version | Sprint 6 (#62) took version 4; required by the coordinating session |
| D1 | `apiEquivalent` optional in `schemaVersion` 2 | Required, with `models`, in a complete version 2 card; optional in an overlay | A complete card without list prices silently disables the lens |
| D2 | Local spans use the entry of `localOffload.baselineModel` | The same by default, plus an optional `apiEquivalent.localBaselineModel` | The built-in baseline `mai-code-2` has no list price, so local spans would always be unpriced |
| D3 | — | An API-equivalent cost beyond the pico-USD range rejects the span at ingest; the backfill leaves it unpriced | Same rule as billed costs and savings; only absurd token counts reach it |
| D4 | An invalid `--pricing` file fails like `rates validate --file` | New `rates validate --pricing`, with the reports' messages | `--file` validates an overlay, not a complete card |
| D5 | What-if streams the "recorded local baseline identity" | Local spans use the what-if card's own local retail baseline | Matches ingestion with that card; the recorded id is the billing card's |
| D6 | Cell rules `Unpriced`, `$12.34*`, `$12.34`; an example footnote; a three-lens block | The same rules plus `~` for estimates, counting footnotes, and a lens table with `Local at API prices`; `$0.00` for an empty group | Discloses Sprint 6's cache-counter uncertainty; the issue's footnote and block are examples |
| D7 | Point `RatesUpdate` at `rates.v2.json`; keep publishing v1 `rates.json` | Also fall back to `rates.json` on 404, refuse a later v1 card, and ignore a later v1 download in `LoadBase` | Keeps the first v2 release and old downloads from dropping the lens |

### Acceptance criteria traceability

| Issue #61 acceptance criterion | Tasks | Tests |
|---|---|---|
| `RateCardReader` accepts `schemaVersion` 1 (no `apiEquivalent`) and 2; rejects `apiEquivalent` under v1, more than 6 decimal places, and overlapping windows, with JSON-path errors | 1 | AK1 (10), AK2 (7), AK5; RateCardLoadingTests (one more case, `apiEquivalent: is required`); the `must be 1 or 2` cases of RateCardLoadingTests, RateOverlayTests, and RatesCommandTests |
| The overlay adds or overrides `apiEquivalent` models by `id` | 1, 2 | AK3, AK4, AP9 |
| `UsdCost` is exactly `tokens × rate / 1,000,000m`, `TheoryData<…, decimal>` for uncached, cache-read, cache-write, output, and multi-tier windows | 2, 3 | AP1 (10), AP10 (3), AR1 (56) |
| An unknown model is `Unpriced` with `UsdCost == null`; storage writes `api_equivalent_usd_pico IS NULL` (asserted directly); the table shows `Unpriced`; JSON shows `null`; never 0 | 2, 5, 8, 9, 11 | AP2 (4), AS1, AE1, AE2, EP12, MC4, MC5 |
| `invoke_agent` and other non-inference spans are `NotApplicable`, never priced in any lens | 2, 5 | AP3 (4), AS1 (`not_applicable`), S5 |
| A local span gets its cost from the baseline's entry; `localSavedUsd` unchanged (regression) | 2, 5, 8 | AP4, AS1, AS2, AE3 |
| Migrating keeps every row and value; legacy rows have `api_equivalent_status IS NULL`; `--dry-run` writes nothing; without it, only `NULL` rows change and no credit, BYOK, or local column (before/after comparison) | 5, 6 | AS4, S1, SL1, SL8, SL10, SL11, AB1, AB2, RB1, RB2 |
| `--pricing` changes only the retail comparison on all affected commands, labels the source, keeps recorded credit, BYOK, and savings values and warnings, writes nothing; an invalid file fails with the `rates validate` message | 7, 10 | WI1 (5), WI2, WI3, WI4, WF1, WF2 (4), WF3 (5), WF4, WF5 (D4) |
| The backfill records its own version and hash without overwriting billing provenance; a retail-mapped model on an unrecognized endpoint gets a retail estimate | 2, 5, 6 | AB2, AP6, AS1 |
| `status`, `branch`, `agents`, and `top-prompts` show `API equiv USD` with the Unpriced, partial, and priced rules; JSON has `apiEquivalentUsd` and `apiEquivalentUnpricedSpans` | 8 | AE1, AE2, AE3, AE4, StatusText, RP4, RP6, RP7, RP15, GS2 |
| MCP tools return the fields; `orderBy: apiEquivalentUsd` works; the schema rejects unknown arguments; no argument takes a path | 11 | MC1, MC2, MC5, MC6, MC8 (3 more `pricing` cases), MC15 |
| An older binary given the version 2 card fails closed during `rates update`, keeps its file, and prints a clear message | 4 | Verified on v0.4.1 (see "Verified facts"); RU12 (the same path in new binaries); RU11, RU13, RU14 |
| No `float`, `double`, or `REAL` for money; invariant culture, verified under `de-DE` | all | Review; AE5, CR6 |
| `docs/configuration.md`, `docs/cli.md`, and `docs/guide.md` updated; `GuideSamplesTests` passes | 1–4, 6, 8–11 | GS1, GS2, GS6, GS7, RateOverlayTests (`rates-error-example`) |
| Build and tests exit 0 with warnings as errors; hermetic, no network, no `git` | all | the whole suite |

## Task 1 specification: Rate-Card Schema Version 2

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Pricing/RateCardDocument.cs` | `ApiEquivalent` section; `Vendor` and `SourceUrl` on priced models |
| `src/CopilotLedger.Core/Pricing/RateCardReader.cs` | `schemaVersion` 1 or 2; reads `apiEquivalent` only in version 2, required in a complete version 2 card |
| `src/CopilotLedger.Core/Pricing/ModelRateCard.cs` | Comment: an API-equivalent model's channel is `Unpriced` |
| `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs` | Validates, merges, and hashes the section; `SchemaVersion`, `ApiRateCardSha256` |
| `tests/CopilotLedger.Tests/TestSupport/RateFixtures.cs` | New `RetailTestRatesJson`, a complete version 2 card with fictional prices |
| `tests/CopilotLedger.Tests/Pricing/ApiEquivalentCardTests.cs` | New: AK1–AK7 |
| `tests/CopilotLedger.Tests/Pricing/RateCardLoadingTests.cs`, `RateOverlayTests.cs`, `tests/CopilotLedger.Tests/Cli/RatesCommandTests.cs` | `schemaVersion` 3 is now the unsupported example: `must be 1 or 2`; one more case, `apiEquivalent: is required` |
| `docs/configuration.md` | `schemaVersion`, the `apiEquivalent` overlay section ("API list prices"), and the field reference |

### Contracts

```csharp
// RateCardDocument (internal) gains:
public ApiEquivalentSection? ApiEquivalent { get; init; }   // schemaVersion 2 only

internal sealed class ApiEquivalentSection
{
    public string? Description { get; init; }
    public string? LocalBaselineModel { get; init; }
    public IReadOnlyList<PricedModelDocument>? Models { get; init; }
}

// PricedModelDocument gains Vendor and SourceUrl (string?), read only in apiEquivalent.models.

// JsonRateCardCalculator (public) gains:
public int SchemaVersion { get; }          // 1, or 2; a merged card has the higher of the two
public string ApiRateCardSha256 { get; }   // lowercase hex; see "The canonical API rate card hash"
```

- **Reader.** `schemaVersion` must be 1 or 2 (`schemaVersion: must be 1 or 2`). `apiEquivalent` under version 1 fails with `apiEquivalent: is allowed only when schemaVersion is 2`. A complete (base) version 2 card needs `apiEquivalent` (`apiEquivalent: is required`) and `apiEquivalent.models`; an overlay needs neither. `vendor` and `sourceUrl` are known properties only in `apiEquivalent.models` (elsewhere: `is not a known property`).
- **Validation.** `apiEquivalent.models` go through the same `ValidateModels` as the other sections, with paths under `apiEquivalent.models`. A blank `localBaselineModel` is `must not be blank`, and one that names no API-equivalent model is `does not resolve to an apiEquivalent model: <id>` (`… in the merged rates: <id>` for an overlay). Model identities are unique within the section, across an overlay merge too.
- **Merge.** API-equivalent models merge by `id` through `MergeModels`, like the other sections; the overlay's `localBaselineModel` wins; `description` stays the base's.
- **Local retail baseline.** `apiEquivalent.localBaselineModel` when set; otherwise the API-equivalent model with the `localOffload.baselineModel` id or alias, or none.

### Code (copy exactly)

Edit 1 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Pricing;
```

with:

```csharp
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Pricing;
```

Edit 2 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        IReadOnlyList<string> localHosts,
        IReadOnlyList<string> localProviders,
        ModelRateCard baselineCard)
    {
        RatesVersion = ratesVersion;
```

with:

```csharp
        IReadOnlyList<string> localHosts,
        IReadOnlyList<string> localProviders,
        ModelRateCard baselineCard,
        int schemaVersion,
        IReadOnlyList<ModelRateCard> retailModels,
        ModelRateCard? retailLocalBaseline)
    {
        RatesVersion = ratesVersion;
```

Edit 3 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        _localProviders = localProviders;
        _baselineCard = baselineCard;
    }

    public string RatesVersion { get; }

    public static JsonRateCardCalculator LoadEmbedded() =>
        Create(ReadEmbedded(), null, null);
```

with:

```csharp
        _localProviders = localProviders;
        _baselineCard = baselineCard;
        SchemaVersion = schemaVersion;
        ApiRateCardSha256 = ApiRateCardHash(retailModels, retailLocalBaseline);
    }

    public string RatesVersion { get; }

    // 1, or 2 when the card can have an apiEquivalent section. A merged card has the higher of the two versions.
    public int SchemaVersion { get; }

    // The SHA-256 of the canonical API-equivalent card, in lowercase hex; see ApiRateCardHash.
    public string ApiRateCardSha256 { get; }

    public static JsonRateCardCalculator LoadEmbedded() =>
        Create(ReadEmbedded(), null, null);
```

Edit 4 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            _ = ValidateNames(overlay.LocalOffload.ProviderNames, "localOffload.providerNames");
        }
    }

    private static RateCardDocument Merge(RateCardDocument baseDocument, RateCardDocument overlay)
```

with:

```csharp
            _ = ValidateNames(overlay.LocalOffload.ProviderNames, "localOffload.providerNames");
        }

        if (overlay.ApiEquivalent?.Models is not null)
        {
            _ = ValidateModels(
                overlay.ApiEquivalent.Models,
                BillingChannel.Unpriced,
                "apiEquivalent.models");
        }

        if (overlay.ApiEquivalent?.LocalBaselineModel is not null &&
            NormalizeModel(overlay.ApiEquivalent.LocalBaselineModel) is null)
        {
            throw Invalid(null, "apiEquivalent.localBaselineModel", "must not be blank");
        }
    }

    private static RateCardDocument Merge(RateCardDocument baseDocument, RateCardDocument overlay)
```

Edit 5 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        return new RateCardDocument
        {
            SchemaVersion = 1,
            RatesVersion = ratesVersion,
            Source = baseDocument.Source,
```

with:

```csharp
        return new RateCardDocument
        {
            SchemaVersion = Math.Max(baseDocument.SchemaVersion ?? 1, overlay.SchemaVersion ?? 1),
            RatesVersion = ratesVersion,
            Source = baseDocument.Source,
```

Edit 6 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
                ProviderNames = MergeStrings(baseDocument.LocalOffload!.ProviderNames!, overlay.LocalOffload?.ProviderNames),
            },
        };
    }
```

with:

```csharp
                ProviderNames = MergeStrings(baseDocument.LocalOffload!.ProviderNames!, overlay.LocalOffload?.ProviderNames),
            },
            ApiEquivalent = baseDocument.ApiEquivalent is null && overlay.ApiEquivalent is null
                ? null
                : new ApiEquivalentSection
                {
                    Description = baseDocument.ApiEquivalent?.Description,
                    LocalBaselineModel = overlay.ApiEquivalent?.LocalBaselineModel
                        ?? baseDocument.ApiEquivalent?.LocalBaselineModel,
                    Models = MergeModels(baseDocument.ApiEquivalent?.Models ?? [], overlay.ApiEquivalent?.Models),
                },
        };
    }
```

Edit 7 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        }

        string baseline = NormalizeModel(merged.LocalOffload!.BaselineModel)!;
        bool resolves = merged.GithubAiCredits!.Models!.Any(model =>
```

with:

```csharp
        }

        if (overlay.ApiEquivalent?.Models is not null)
        {
            ValidateMergedModelIdentities(
                baseDocument.ApiEquivalent?.Models ?? [],
                overlay.ApiEquivalent.Models,
                "apiEquivalent.models");
        }

        string baseline = NormalizeModel(merged.LocalOffload!.BaselineModel)!;
        bool resolves = merged.GithubAiCredits!.Models!.Any(model =>
```

Edit 8 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
                $"does not resolve to a githubAiCredits model in the merged rates: {baseline}");
        }
    }

    private static void ValidateMergedModelIdentities(
```

with:

```csharp
                $"does not resolve to a githubAiCredits model in the merged rates: {baseline}");
        }

        string? retailBaseline = NormalizeModel(merged.ApiEquivalent?.LocalBaselineModel);
        if (retailBaseline is not null &&
            !merged.ApiEquivalent!.Models!.Any(model =>
                NormalizeModel(model.Id) == retailBaseline ||
                model.Aliases.Any(alias => NormalizeModel(alias) == retailBaseline)))
        {
            throw Invalid(
                null,
                "apiEquivalent.localBaselineModel",
                $"does not resolve to an apiEquivalent model in the merged rates: {retailBaseline}");
        }
    }

    private static void ValidateMergedModelIdentities(
```

Edit 9 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        }

        return new JsonRateCardCalculator(
            document.RatesVersion,
```

with:

```csharp
        }

        IReadOnlyList<ModelRateCard> retailModels = document.ApiEquivalent?.Models is null
            ? []
            : ValidateModels(document.ApiEquivalent.Models, BillingChannel.Unpriced, "apiEquivalent.models");

        // Local requests are priced at retail as apiEquivalent.localBaselineModel when it's set, or else as the
        // localOffload baseline, which may have no apiEquivalent price.
        ModelRateCard? retailLocalBaseline = FindModel(retailModels, normalizedBaseline);
        if (document.ApiEquivalent?.LocalBaselineModel is not null)
        {
            string retailBaseline = NormalizeModel(document.ApiEquivalent.LocalBaselineModel)
                ?? throw Invalid(null, "apiEquivalent.localBaselineModel", "must not be blank");
            retailLocalBaseline = FindModel(retailModels, retailBaseline)
                ?? throw Invalid(
                    null,
                    "apiEquivalent.localBaselineModel",
                    $"does not resolve to an apiEquivalent model: {retailBaseline}");
        }

        return new JsonRateCardCalculator(
            document.RatesVersion,
```

Edit 10 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            ValidateNames(document.LocalOffload.AdditionalHosts!, "localOffload.additionalHosts"),
            ValidateNames(document.LocalOffload.ProviderNames!, "localOffload.providerNames"),
            baseline);
    }

    private static IReadOnlyList<ModelRateCard> ValidateModels(
```

with:

```csharp
            ValidateNames(document.LocalOffload.AdditionalHosts!, "localOffload.additionalHosts"),
            ValidateNames(document.LocalOffload.ProviderNames!, "localOffload.providerNames"),
            baseline,
            document.SchemaVersion ?? 1,
            retailModels,
            retailLocalBaseline);
    }

    private static IReadOnlyList<ModelRateCard> ValidateModels(
```

Edit 11 of 11 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        };

    private static string ReadEmbedded()
    {
```

with:

```csharp
        };

    // SHA-256 of the canonical API-equivalent card: compact UTF-8 JSON {"localBaselineModel":…,"models":[…]}. The local
    // baseline is the ID of the apiEquivalent model that prices local requests, or null. Models are in ordinal ID order,
    // each with its normalized ID, its normalized aliases in ordinal order, and its windows and tiers in card order. Rates
    // are JSON numbers without trailing zeros, window bounds are UTC "O" timestamps or null, and maxInputTokens is a
    // number or null. vendor, sourceUrl, description, and notes don't change any price, so they aren't hashed.
    private static string ApiRateCardHash(IReadOnlyList<ModelRateCard> models, ModelRateCard? localBaseline)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            if (localBaseline is null)
            {
                writer.WriteNull("localBaselineModel");
            }
            else
            {
                writer.WriteString("localBaselineModel", localBaseline.ModelId);
            }

            writer.WriteStartArray("models");
            foreach (ModelRateCard model in models.OrderBy(item => item.ModelId, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", model.ModelId);
                writer.WriteStartArray("aliases");
                foreach (string alias in model.Aliases.Order(StringComparer.Ordinal))
                {
                    writer.WriteStringValue(alias);
                }

                writer.WriteEndArray();
                writer.WriteStartArray("windows");
                foreach (RateWindow window in model.Windows)
                {
                    writer.WriteStartObject();
                    WriteInstant(writer, "effectiveFrom", window.EffectiveFrom);
                    WriteInstant(writer, "effectiveUntil", window.EffectiveUntil);
                    writer.WriteStartArray("tiers");
                    foreach (RateTier tier in window.Tiers)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", tier.Name);
                        if (tier.MaxInputTokens is long maxInputTokens)
                        {
                            writer.WriteNumber("maxInputTokens", maxInputTokens);
                        }
                        else
                        {
                            writer.WriteNull("maxInputTokens");
                        }

                        WriteRate(writer, "inputPer1M", tier.InputPer1M);
                        WriteRate(writer, "cachedInputPer1M", tier.CachedInputPer1M);
                        WriteRate(writer, "cacheWritePer1M", tier.CacheWritePer1M);
                        WriteRate(writer, "outputPer1M", tier.OutputPer1M);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteInstant(Utf8JsonWriter writer, string name, DateTimeOffset? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WriteString(name, value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    // A validated rate has at most 6 decimal places, so "0.######" writes it exactly.
    private static void WriteRate(Utf8JsonWriter writer, string name, decimal? rate)
    {
        if (rate is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WritePropertyName(name);
        writer.WriteRawValue(rate.Value.ToString("0.######", CultureInfo.InvariantCulture));
    }

    private static string ReadEmbedded()
    {
```

Edit 1 of 1 in `src/CopilotLedger.Core/Pricing/ModelRateCard.cs`, replace:

```csharp
public sealed record ModelRateCard(
    string ModelId,                    // canonical normalized id
    BillingChannel Channel,            // GitHubAiCredits or FoundryByok
    IReadOnlyList<string> Aliases,     // normalized
    IReadOnlyList<RateWindow> Windows);
```

with:

```csharp
public sealed record ModelRateCard(
    string ModelId,                    // canonical normalized id
    BillingChannel Channel,            // GitHubAiCredits or FoundryByok; Unpriced for an apiEquivalent (retail) model
    IReadOnlyList<string> Aliases,     // normalized
    IReadOnlyList<RateWindow> Windows);
```

Edit 1 of 2 in `src/CopilotLedger.Core/Pricing/RateCardDocument.cs`, replace:

```csharp
    public FoundryRateSection? FoundryByok { get; init; }

    public LocalRateSection? LocalOffload { get; init; }
}

internal sealed class RateCardSource
```

with:

```csharp
    public FoundryRateSection? FoundryByok { get; init; }

    public LocalRateSection? LocalOffload { get; init; }

    public ApiEquivalentSection? ApiEquivalent { get; init; }   // schemaVersion 2 only
}

internal sealed class RateCardSource
```

Edit 2 of 2 in `src/CopilotLedger.Core/Pricing/RateCardDocument.cs`, replace:

```csharp
}

internal sealed class PricedModelDocument
{
    public required string Id { get; init; }

    public IReadOnlyList<string> Aliases { get; init; } = [];

    public required IReadOnlyList<RateWindowDocument> Windows { get; init; }
```

with:

```csharp
}

internal sealed class ApiEquivalentSection
{
    public string? Description { get; init; }

    public string? LocalBaselineModel { get; init; }

    public IReadOnlyList<PricedModelDocument>? Models { get; init; }
}

internal sealed class PricedModelDocument
{
    public required string Id { get; init; }

    public string? Vendor { get; init; }      // apiEquivalent models only

    public string? SourceUrl { get; init; }   // apiEquivalent models only

    public IReadOnlyList<string> Aliases { get; init; } = [];

    public required IReadOnlyList<RateWindowDocument> Windows { get; init; }
```

Edit 1 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
            }

            ValidateSchemaVersion(root);
            return ReadRoot(root, isBase);
        }
    }

    private static RateCardDocument ReadRoot(JsonElement root, bool isBase)
    {
        string? ratesVersion = null;
```

with:

```csharp
            }

            int schemaVersion = ValidateSchemaVersion(root);
            return ReadRoot(root, isBase, schemaVersion);
        }
    }

    private static RateCardDocument ReadRoot(JsonElement root, bool isBase, int schemaVersion)
    {
        string? ratesVersion = null;
```

Edit 2 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
        FoundryRateSection? foundryByok = null;
        LocalRateSection? localOffload = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema =
```

with:

```csharp
        FoundryRateSection? foundryByok = null;
        LocalRateSection? localOffload = null;
        ApiEquivalentSection? apiEquivalent = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema =
```

Edit 3 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
            "foundryByok",
            "localOffload",
        ];

        foreach (JsonProperty property in root.EnumerateObject())
```

with:

```csharp
            "foundryByok",
            "localOffload",
            "apiEquivalent",
        ];

        foreach (JsonProperty property in root.EnumerateObject())
```

Edit 4 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
                        : ReadLocalSection(property.Value, path, isBase);
                    break;
            }
        }
```

with:

```csharp
                        : ReadLocalSection(property.Value, path, isBase);
                    break;
                case "apiEquivalent":
                    if (schemaVersion < 2)
                    {
                        throw Error(path, "is allowed only when schemaVersion is 2");
                    }

                    apiEquivalent = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadApiEquivalentSection(property.Value, path, isBase);
                    break;
            }
        }
```

Edit 5 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
            Require(foundryByok is not null, "foundryByok", "is required");
            Require(localOffload is not null, "localOffload", "is required");
        }

        return new RateCardDocument
        {
            SchemaVersion = 1,
            RatesVersion = ratesVersion,
            Source = source,
```

with:

```csharp
            Require(foundryByok is not null, "foundryByok", "is required");
            Require(localOffload is not null, "localOffload", "is required");
            Require(schemaVersion < 2 || apiEquivalent is not null, "apiEquivalent", "is required");
        }

        return new RateCardDocument
        {
            SchemaVersion = schemaVersion,
            RatesVersion = ratesVersion,
            Source = source,
```

Edit 6 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
            FoundryByok = foundryByok,
            LocalOffload = localOffload,
        };
    }
```

with:

```csharp
            FoundryByok = foundryByok,
            LocalOffload = localOffload,
            ApiEquivalent = apiEquivalent,
        };
    }
```

Edit 7 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
    }

    private static IReadOnlyList<PricedModelDocument> ReadPricedModels(JsonElement value, string path)
    {
        RequireArray(value, path);
```

with:

```csharp
    }

    private static ApiEquivalentSection ReadApiEquivalentSection(JsonElement value, string path, bool isBase)
    {
        RequireObject(value, path);
        string? description = null;
        string? localBaselineModel = null;
        IReadOnlyList<PricedModelDocument>? models = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = ["description", "localBaselineModel", "models"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "description":
                    description = ReadOptionalString(property.Value, propertyPath);
                    break;
                case "localBaselineModel":
                    localBaselineModel = ReadOptionalString(property.Value, propertyPath);
                    break;
                case "models":
                    models = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadPricedModels(property.Value, propertyPath, retail: true);
                    break;
            }
        }

        if (isBase)
        {
            Require(models is not null, Join(path, "models"), "is required");
        }

        return new ApiEquivalentSection
        {
            Description = description,
            LocalBaselineModel = localBaselineModel,
            Models = models,
        };
    }

    // retail: an apiEquivalent model, which may also have vendor and sourceUrl.
    private static IReadOnlyList<PricedModelDocument> ReadPricedModels(JsonElement value, string path, bool retail = false)
    {
        RequireArray(value, path);
```

Edit 8 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
        {
            string itemPath = $"{path}[{index++}]";
            models.Add(ReadPricedModel(item, itemPath));
        }

        return models;
```

with:

```csharp
        {
            string itemPath = $"{path}[{index++}]";
            models.Add(ReadPricedModel(item, itemPath, retail));
        }

        return models;
```

Edit 9 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
    }

    private static PricedModelDocument ReadPricedModel(JsonElement value, string path)
    {
        RequireObject(value, path);
        string? id = null;
        IReadOnlyList<string> aliases = [];
        IReadOnlyList<RateWindowDocument>? windows = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = ["id", "aliases", "windows"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
```

with:

```csharp
    }

    private static PricedModelDocument ReadPricedModel(JsonElement value, string path, bool retail)
    {
        RequireObject(value, path);
        string? id = null;
        string? vendor = null;
        string? sourceUrl = null;
        IReadOnlyList<string> aliases = [];
        IReadOnlyList<RateWindowDocument>? windows = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = retail
            ? ["id", "vendor", "sourceUrl", "aliases", "windows"]
            : ["id", "aliases", "windows"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
```

Edit 10 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
            : ["id", "aliases", "windows"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "id":
                    id = ReadString(property.Value, propertyPath);
                    break;
                case "aliases":
                    aliases = ReadStringArray(property.Value, propertyPath);
```

with:

```csharp
            : ["id", "aliases", "windows"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "id":
                    id = ReadString(property.Value, propertyPath);
                    break;
                case "vendor":
                    vendor = ReadOptionalString(property.Value, propertyPath);
                    break;
                case "sourceUrl":
                    sourceUrl = ReadOptionalString(property.Value, propertyPath);
                    break;
                case "aliases":
                    aliases = ReadStringArray(property.Value, propertyPath);
```

Edit 11 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
        Require(id is not null, Join(path, "id"), "is required");
        Require(windows is not null, Join(path, "windows"), "is required");
        return new PricedModelDocument { Id = id!, Aliases = aliases, Windows = windows! };
    }

    private static IReadOnlyList<RateWindowDocument> ReadWindows(JsonElement value, string path)
```

with:

```csharp
        Require(id is not null, Join(path, "id"), "is required");
        Require(windows is not null, Join(path, "windows"), "is required");
        return new PricedModelDocument
        {
            Id = id!,
            Vendor = vendor,
            SourceUrl = sourceUrl,
            Aliases = aliases,
            Windows = windows!,
        };
    }

    private static IReadOnlyList<RateWindowDocument> ReadWindows(JsonElement value, string path)
```

Edit 12 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
    }

    private static void ValidateSchemaVersion(JsonElement root)
    {
        JsonElement value;
```

with:

```csharp
    }

    // The document's schema version, 1 or 2. Throws InvalidDataException for anything else.
    private static int ValidateSchemaVersion(JsonElement root)
    {
        JsonElement value;
```

Edit 13 of 13 in `src/CopilotLedger.Core/Pricing/RateCardReader.cs`, replace:

```csharp
        }

        if (schemaVersion != 1)
        {
            throw Error("schemaVersion", "must be 1");
        }
    }

    private static string? ReadOptionalString(JsonElement value, string path) =>
```

with:

```csharp
        }

        if (schemaVersion is not (1 or 2))
        {
            throw Error("schemaVersion", "must be 1 or 2");
        }

        return schemaVersion;
    }

    private static string? ReadOptionalString(JsonElement value, string path) =>
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Cli/RatesCommandTests.cs`, replace:

```csharp
    public static TheoryData<string, string> InvalidFiles => new()
    {
        { """{ "schemaVersion": 2 }""", "schemaVersion: must be 1" },
        { "{ \"schemaVersion\": 1,\n  \"foundryByok\": { \"models\": [ }\n}\n", "line 2, column 32: invalid JSON" },
        { """{ "schemaVersion": 1, "foundryBYOK": { } }""", "foundryBYOK: is not a known property; did you mean foundryByok?" },
```

with:

```csharp
    public static TheoryData<string, string> InvalidFiles => new()
    {
        { """{ "schemaVersion": 3 }""", "schemaVersion: must be 1 or 2" },
        { "{ \"schemaVersion\": 1,\n  \"foundryByok\": { \"models\": [ }\n}\n", "line 2, column 32: invalid JSON" },
        { """{ "schemaVersion": 1, "foundryBYOK": { } }""", "foundryBYOK: is not a known property; did you mean foundryByok?" },
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Cli/RatesCommandTests.cs`, replace:

```csharp
        await using var harness = new CliHarness(temp.Path);
        string path = Path.Combine(temp.Path, "bad.json");
        await File.WriteAllTextAsync(path, """{ "schemaVersion": 2 }""");

        CliRun relative = await harness.RunInAsync(
```

with:

```csharp
        await using var harness = new CliHarness(temp.Path);
        string path = Path.Combine(temp.Path, "bad.json");
        await File.WriteAllTextAsync(path, """{ "schemaVersion": 3 }""");

        CliRun relative = await harness.RunInAsync(
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Cli/RatesCommandTests.cs`, replace:

```csharp
        Assert.Empty(relative.Out);
        Assert.Equal(
            $"Invalid rates overlay: {path}: schemaVersion: must be 1\n",
            relative.Error);
    }
```

with:

```csharp
        Assert.Empty(relative.Out);
        Assert.Equal(
            $"Invalid rates overlay: {path}: schemaVersion: must be 1 or 2\n",
            relative.Error);
    }
```

New file `tests/CopilotLedger.Tests/Pricing/ApiEquivalentCardTests.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class ApiEquivalentCardTests
{
    // The SHA-256 of RetailTestRatesJson's canonical API-equivalent card.
    private const string RetailFixtureHash = "0075a4b28916a99fc339c7c8acad551cd5828b35c094d60d3fb9e0b0d939977c";

    // The SHA-256 of a card without apiEquivalent models: {"localBaselineModel":null,"models":[]}.
    private const string EmptyCardHash = "49a4384900a30f23c85c0bb1f208f01164855e992f1da709cca54615f954746b";

    private const string AddedModel = """
        { "id": "test-new", "aliases": ["test-new-latest"], "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1.5, "outputPer1M": 6 } ] } ] }
        """;

    private const string ReplacedModel = """
        { "id": "Test Retail", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 2.5, "cachedInputPer1M": 0.25, "outputPer1M": 12.5 } ] } ] }
        """;

    // Each case replaces every occurrence of find in RetailTestRatesJson.
    public static TheoryData<string, string, string> InvalidCards => new()
    {
        { "\"schemaVersion\": 2,", "\"schemaVersion\": 1,", "apiEquivalent: is allowed only when schemaVersion is 2" },
        { "\"schemaVersion\": 2,", "\"schemaVersion\": 3,", "schemaVersion: must be 1 or 2" },
        { "\"inputPer1M\": 3.00,", "\"inputPer1M\": 3.0000001,", "apiEquivalent.models[0].windows[0].tiers[0].inputPer1M: must have no more than 6 decimal places" },
        { "\"effectiveFrom\": \"2026-12-01T00:00:00Z\",", "\"effectiveFrom\": \"2026-11-30T00:00:00Z\",", "apiEquivalent.models[0].windows[1].effectiveFrom: windows must be sorted and must not overlap" },
        { "\"aliases\": [\"test-retail-2026-11-01\"],", "\"aliases\": [\"Test Retail\"],", "apiEquivalent.models[0].aliases[0]: duplicate model identity: test-retail" },
        { "\"description\": \"Fictional retail prices for tests only.\",", "\"Description\": \"x\",", "apiEquivalent.Description: is not a known property; did you mean description?" },
        { "\"description\": \"Fictional retail prices for tests only.\",", "\"localBaselineModel\": \"test-missing\",", "apiEquivalent.localBaselineModel: does not resolve to an apiEquivalent model: test-missing" },
        { "\"description\": \"Fictional retail prices for tests only.\",", "\"localBaselineModel\": \" \",", "apiEquivalent.localBaselineModel: must not be blank" },
        { "{ \"id\": \"test-baseline\", \"aliases\": [],", "{ \"id\": \"test-baseline\", \"vendor\": \"x\", \"aliases\": [],", "githubAiCredits.models[1].vendor: is not a known property" },
        { "\"vendor\": \"test-vendor\",", "\"vendor\": 5,", "apiEquivalent.models[0].vendor: must be a string" },
    };

    public static TheoryData<string, string> InvalidOverlays => new()
    {
        { """{ "schemaVersion": 1, "apiEquivalent": { } }""", "overlay: apiEquivalent: is allowed only when schemaVersion is 2" },
        { """{ "schemaVersion": 2, "apiEquivalent": { "models": [ { "id": "test-new", "aliases": ["test-baseline"], "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] } ] } ] } }""", "overlay: apiEquivalent.models[0].aliases[0]: duplicate model identity in the merged rates: test-baseline (built-in model test-baseline)" },
        { """{ "schemaVersion": 2, "apiEquivalent": { "localBaselineModel": "test-missing" } }""", "overlay: apiEquivalent.localBaselineModel: does not resolve to an apiEquivalent model in the merged rates: test-missing" },
        { """{ "schemaVersion": 2, "apiEquivalent": { "localBaselineModel": "" } }""", "overlay: apiEquivalent.localBaselineModel: must not be blank" },
        { """{ "schemaVersion": 2, "apiEquivalent": { "models": [ { "id": "test-new", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1.0000001 } ] } ] } ] } }""", "overlay: apiEquivalent.models[0].windows[0].tiers[0].outputPer1M: must have no more than 6 decimal places" },
        { """{ "schemaVersion": 2, "apiEquivalent": { "models": [ { "id": "test-new", "windows": [ { "effectiveFrom": "2027-01-01T00:00:00Z", "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] }, { "effectiveFrom": "2026-12-01T00:00:00Z", "tiers": [ { "name": "default", "inputPer1M": 2, "outputPer1M": 2 } ] } ] } ] } }""", "overlay: apiEquivalent.models[0].windows[1].effectiveFrom: windows must be sorted and must not overlap" },
        { """{ "schemaVersion": 2, "githubAiCredits": { "models": [ { "id": "test-new", "vendor": "x", "windows": [] } ] } }""", "overlay: githubAiCredits.models[0].vendor: is not a known property" },
    };

    [Theory]
    [MemberData(nameof(InvalidCards))]
    public void AK1_InvalidCardsReportExactPaths(string find, string replacement, string expected)
    {
        Assert.Contains(find, RateFixtures.RetailTestRatesJson);
        string json = RateFixtures.RetailTestRatesJson.Replace(find, replacement, StringComparison.Ordinal);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => JsonRateCardCalculator.Parse(json));

        Assert.Equal(expected, exception.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidOverlays))]
    public void AK2_InvalidOverlaysReportExactPaths(string overlay, string expected)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson, overlay));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public void AK3_OverlayReplacesAndAddsModelsById()
    {
        string overlay = $$"""{ "schemaVersion": 2, "ratesVersion": "contoso", "apiEquivalent": { "models": [ {{ReplacedModel}}, {{AddedModel}} ] } }""";
        JsonNode expected = JsonNode.Parse(RateFixtures.RetailTestRatesJson)!;
        JsonArray models = expected["apiEquivalent"]!["models"]!.AsArray();
        models[0] = JsonNode.Parse(ReplacedModel);
        models.Add(JsonNode.Parse(AddedModel));

        JsonRateCardCalculator merged = JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson, overlay);

        Assert.Equal("retail-fixture+contoso", merged.RatesVersion);
        Assert.Equal(2, merged.SchemaVersion);
        Assert.Equal(JsonRateCardCalculator.Parse(expected.ToJsonString()).ApiRateCardSha256, merged.ApiRateCardSha256);
        Assert.NotEqual(RetailFixtureHash, merged.ApiRateCardSha256);
    }

    [Fact]
    public void AK4_OverlayChoosesTheLocalBaseline()
    {
        JsonNode expected = JsonNode.Parse(RateFixtures.RetailTestRatesJson)!;
        expected["apiEquivalent"]!["localBaselineModel"] = "test-no-cache";

        JsonRateCardCalculator merged = JsonRateCardCalculator.Parse(
            RateFixtures.RetailTestRatesJson,
            """{ "schemaVersion": 2, "apiEquivalent": { "localBaselineModel": "Test No Cache" } }""");

        Assert.Equal(JsonRateCardCalculator.Parse(expected.ToJsonString()).ApiRateCardSha256, merged.ApiRateCardSha256);
        Assert.NotEqual(RetailFixtureHash, merged.ApiRateCardSha256);
    }

    [Fact]
    public void AK5_SchemaVersionsAndPinnedHashes()
    {
        JsonRateCardCalculator retail = JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);
        JsonRateCardCalculator credits = JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson);
        JsonRateCardCalculator upgraded = JsonRateCardCalculator.Parse(
            RateFixtures.ByokTestRatesJson,
            $$"""{ "schemaVersion": 2, "apiEquivalent": { "models": [ {{AddedModel}} ] } }""");

        Assert.Equal(2, retail.SchemaVersion);
        Assert.Equal(RetailFixtureHash, retail.ApiRateCardSha256);
        Assert.Equal(1, credits.SchemaVersion);
        Assert.Equal(EmptyCardHash, credits.ApiRateCardSha256);
        Assert.Equal(2, upgraded.SchemaVersion);
        Assert.Equal("test-fixture+user", upgraded.RatesVersion);
        Assert.NotEqual(EmptyCardHash, upgraded.ApiRateCardSha256);
    }

    [Fact]
    public void AK6_TheHashIgnoresFormattingOrderAndDescriptions()
    {
        JsonNode card = JsonNode.Parse(RateFixtures.RetailTestRatesJson)!;
        JsonArray models = card["apiEquivalent"]!["models"]!.AsArray();
        JsonNode[] reversed = models.Reverse().Select(model => model!.DeepClone()).ToArray();
        models.Clear();
        foreach (JsonNode model in reversed)
        {
            model["vendor"] = "another-vendor";
            model["sourceUrl"] = "https://example.invalid/other";
            models.Add(model);
        }

        JsonNode retail = models.Single(model => model!["id"]!.GetValue<string>() == "test-retail")!;
        retail["id"] = "Test Retail";
        retail["aliases"] = new JsonArray("TEST-RETAIL-2026-11-01");
        retail["windows"]![0]!["tiers"]![0]!["inputPer1M"] = 3m;
        retail["windows"]![0]!["tiers"]![0]!["outputPer1M"] = 15.000000m;
        card["apiEquivalent"]!["description"] = "Another description.";
        card["notes"] = new JsonArray("Other notes.");
        string reformatted = card.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .ReplaceLineEndings("\r\n");

        Assert.Equal(RetailFixtureHash, JsonRateCardCalculator.Parse(reformatted).ApiRateCardSha256);
    }

    [Theory]
    [InlineData("\"outputPer1M\": 3.20", "\"outputPer1M\": 3.21")]
    [InlineData("\"maxInputTokens\": 200000, \"inputPer1M\": 4.00", "\"maxInputTokens\": 200001, \"inputPer1M\": 4.00")]
    [InlineData("\"effectiveFrom\": \"2026-06-01T00:00:00Z\"", "\"effectiveFrom\": \"2026-06-01T00:00:01Z\"")]
    [InlineData("\"aliases\": [\"test-retail-2026-11-01\"]", "\"aliases\": [\"test-retail-2026-11-01\", \"test-retail-latest\"]")]
    [InlineData("\"name\": \"long_context\", \"maxInputTokens\": null, \"inputPer1M\": 6.00", "\"name\": \"long\", \"maxInputTokens\": null, \"inputPer1M\": 6.00")]
    public void AK7_AnyPricingChangeChangesTheHash(string find, string replacement)
    {
        Assert.Contains(find, RateFixtures.RetailTestRatesJson);
        string changed = RateFixtures.RetailTestRatesJson.Replace(find, replacement, StringComparison.Ordinal);

        Assert.NotEqual(RetailFixtureHash, JsonRateCardCalculator.Parse(changed).ApiRateCardSha256);
    }
}
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Pricing/RateCardLoadingTests.cs`, replace:

```csharp
    public static TheoryData<string, string, string> InvalidDocuments => new()
    {
        { "\"schemaVersion\": 1,", "\"schemaVersion\": 2,", "schemaVersion: must be 1" },
        { "\"inputPer1M\": 2.00,", "\"inputPer1M\": 2.0000001,", "githubAiCredits.models[0].windows[0].tiers[0].inputPer1M: must have no more than 6 decimal places" },
        { "\"baselineModel\": \"claude-sonnet-5.5\"", "\"baselineModel\": \"gpt-missing\"", "localOffload.baselineModel: does not resolve to a githubAiCredits model: gpt-missing" },
```

with:

```csharp
    public static TheoryData<string, string, string> InvalidDocuments => new()
    {
        { "\"schemaVersion\": 1,", "\"schemaVersion\": 3,", "schemaVersion: must be 1 or 2" },
        { "\"schemaVersion\": 1,", "\"schemaVersion\": 2,", "apiEquivalent: is required" },
        { "\"inputPer1M\": 2.00,", "\"inputPer1M\": 2.0000001,", "githubAiCredits.models[0].windows[0].tiers[0].inputPer1M: must have no more than 6 decimal places" },
        { "\"baselineModel\": \"claude-sonnet-5.5\"", "\"baselineModel\": \"gpt-missing\"", "localOffload.baselineModel: does not resolve to a githubAiCredits model: gpt-missing" },
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Pricing/RateCardLoadingTests.cs`, replace:

```csharp
            () => JsonRateCardCalculator.Parse(
                RateFixtures.ByokTestRatesJson,
                """{ "schemaVersion": 2 }"""));

        Assert.Equal("overlay: schemaVersion: must be 1", exception.Message);
    }

    [Fact]
```

with:

```csharp
            () => JsonRateCardCalculator.Parse(
                RateFixtures.ByokTestRatesJson,
                """{ "schemaVersion": 3 }"""));

        Assert.Equal("overlay: schemaVersion: must be 1 or 2", exception.Message);
    }

    [Fact]
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Pricing/RateOverlayTests.cs`, replace:

```csharp
    {
        // Sprint 1's invalid overlays, now with exact messages.
        { """{ "schemaVersion": 2 }""", "<file>: schemaVersion: must be 1" },
        { """{ "schemaVersion": 1, "foundryBYOK": { } }""", "<file>: foundryBYOK: is not a known property; did you mean foundryByok?" },
        { """{ "schemaVersion": 1, "localOffload": { "baselineModel": "gpt-missing" } }""", "<file>: localOffload.baselineModel: does not resolve to a githubAiCredits model in the merged rates: gpt-missing" },
```

with:

```csharp
    {
        // Sprint 1's invalid overlays, now with exact messages.
        { """{ "schemaVersion": 3 }""", "<file>: schemaVersion: must be 1 or 2" },
        { """{ "schemaVersion": 1, "foundryBYOK": { } }""", "<file>: foundryBYOK: is not a known property; did you mean foundryByok?" },
        { """{ "schemaVersion": 1, "localOffload": { "baselineModel": "gpt-missing" } }""", "<file>: localOffload.baselineModel: does not resolve to a githubAiCredits model in the merged rates: gpt-missing" },
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Pricing/RateOverlayTests.cs`, replace:

```csharp
        { """{ "schemaVersion": null }""", "<file>: schemaVersion: is required" },
        { """{ "schemaVersion": "1" }""", "<file>: schemaVersion: must be an integer" },
        { """{ "foundryBYOK": { }, "schemaVersion": 2 }""", "<file>: schemaVersion: must be 1" },

        // Unknown and repeated properties.
```

with:

```csharp
        { """{ "schemaVersion": null }""", "<file>: schemaVersion: is required" },
        { """{ "schemaVersion": "1" }""", "<file>: schemaVersion: must be an integer" },
        { """{ "foundryBYOK": { }, "schemaVersion": 3 }""", "<file>: schemaVersion: must be 1 or 2" },

        // Unknown and repeated properties.
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/TestSupport/RateFixtures.cs`, replace:

```csharp
        }
        """;
}
```

with:

```csharp
        }
        """;

    // A schemaVersion 2 card with fictional rates. GitHub prices test-retail and the local baseline test-baseline, and
    // Foundry BYOK prices gpt-6.1-sol (alias contoso-gpt61-prod). apiEquivalent prices test-retail in two windows with two
    // tiers each, test-baseline, test-no-cache without cache rates, and gpt-6.1-sol from 2026-06-01.
    public const string RetailTestRatesJson = """
        {
          "schemaVersion": 2,
          "ratesVersion": "retail-fixture",
          "source": { "url": "https://example.invalid/rates", "retrievedOn": "2026-11-23" },
          "notes": ["Fictional rates for tests only."],
          "githubAiCredits": {
            "hostSuffixes": ["githubcopilot.com"],
            "models": [
              { "id": "test-retail", "aliases": [], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 1.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 1.25, "outputPer1M": 5.00 } ] } ] },
              { "id": "test-baseline", "aliases": [], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 0.50, "cachedInputPer1M": 0.05, "cacheWritePer1M": null, "outputPer1M": 2.50 } ] } ] }
            ]
          },
          "foundryByok": {
            "hostSuffixes": ["services.ai.azure.com", "openai.azure.com", "cognitiveservices.azure.com"],
            "providerNames": ["azure.ai.inference"],
            "models": [
              { "id": "gpt-6.1-sol", "aliases": ["contoso-gpt61-prod"], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 1.75, "cachedInputPer1M": 0.175, "cacheWritePer1M": null, "outputPer1M": 14.00 } ] } ] }
            ]
          },
          "localOffload": {
            "baselineModel": "test-baseline",
            "models": [],
            "additionalHosts": [],
            "providerNames": ["ollama"]
          },
          "apiEquivalent": {
            "description": "Fictional retail prices for tests only.",
            "models": [
              {
                "id": "test-retail",
                "vendor": "test-vendor",
                "sourceUrl": "https://example.invalid/pricing",
                "aliases": ["test-retail-2026-11-01"],
                "windows": [
                  {
                    "effectiveFrom": null,
                    "effectiveUntil": "2026-12-01T00:00:00Z",
                    "tiers": [
                      { "name": "default", "maxInputTokens": 200000, "inputPer1M": 3.00, "cachedInputPer1M": 0.30, "cacheWritePer1M": 3.75, "outputPer1M": 15.00 },
                      { "name": "long_context", "maxInputTokens": null, "inputPer1M": 6.00, "cachedInputPer1M": 0.60, "cacheWritePer1M": 7.50, "outputPer1M": 22.50 }
                    ]
                  },
                  {
                    "effectiveFrom": "2026-12-01T00:00:00Z",
                    "effectiveUntil": null,
                    "tiers": [
                      { "name": "default", "maxInputTokens": 200000, "inputPer1M": 4.00, "cachedInputPer1M": 0.40, "cacheWritePer1M": 5.00, "outputPer1M": 20.00 },
                      { "name": "long_context", "maxInputTokens": null, "inputPer1M": 8.00, "cachedInputPer1M": 0.80, "cacheWritePer1M": 10.00, "outputPer1M": 30.00 }
                    ]
                  }
                ]
              },
              { "id": "test-baseline", "vendor": "test-vendor", "sourceUrl": "https://example.invalid/pricing", "aliases": [], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 0.80, "cachedInputPer1M": 0.08, "cacheWritePer1M": 1.00, "outputPer1M": 3.20 } ] } ] },
              { "id": "test-no-cache", "vendor": "test-vendor", "sourceUrl": "https://example.invalid/pricing", "aliases": [], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": null, "cacheWritePer1M": null, "outputPer1M": 8.00 } ] } ] },
              { "id": "gpt-6.1-sol", "vendor": "test-vendor", "sourceUrl": "https://example.invalid/pricing", "aliases": [], "windows": [ { "effectiveFrom": "2026-06-01T00:00:00Z", "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 2.50, "outputPer1M": 10.00 } ] } ] }
            ]
          }
        }
        """;
}
```

Edit 1 of 6 in `docs/configuration.md`, replace:

```markdown
Run `gh ledger rates init` to create a starter file, and `gh ledger rates validate` to check it against the same rules the daemon uses. The daemon reads the file when it starts, so restart it after a change: `gh ledger daemon stop`, then `gh ledger daemon start`.

- Every section is optional except `"schemaVersion": 1`.
- A model with a new `id` is added. A model with the same `id` as a built-in one replaces it.
- Host and provider lists are added to the built-in lists.
- `localOffload.baselineModel` replaces the built-in value.
- `ratesVersion` is an optional label. Each request in the ledger records which rates priced it, for example `2026-11-20+contoso-2026-11`, or `2026-11-20+user` if you leave the label out.
- JSON doesn't allow comments, so put your own notes in the optional `notes` array.
```

with:

```markdown
Run `gh ledger rates init` to create a starter file, and `gh ledger rates validate` to check it against the same rules the daemon uses. The daemon reads the file when it starts, so restart it after a change: `gh ledger daemon stop`, then `gh ledger daemon start`.

- Every section is optional except `schemaVersion`, which is `1`, or `2` when the file has an `apiEquivalent` section (see [API list prices](#api-list-prices)).
- A model with a new `id` is added. A model with the same `id` as a built-in one replaces it.
- Host and provider lists are added to the built-in lists.
- `localOffload.baselineModel` and `apiEquivalent.localBaselineModel` replace the built-in values.
- `ratesVersion` is an optional label. Each request in the ledger records which rates priced it, for example `2026-11-20+contoso-2026-11`, or `2026-11-20+user` if you leave the label out.
- JSON doesn't allow comments, so put your own notes in the optional `notes` array.
```

Edit 2 of 6 in `docs/configuration.md`, replace:

```markdown
- Most errors give a path into your file, such as `foundryByok.models[0].windows[0].tiers[0].outputPer1M`. Indexes count from 0, and they count only the entries in your file.
- If the file isn't valid JSON, the error gives the line and column where reading stopped, counting from 1.
- Two checks compare your file with the built-in rates: a model `id` or alias that a built-in model already uses, and a `localOffload.baselineModel` that isn't a GitHub model. These errors say `in the merged rates`.
- Property names are case-sensitive, and each property can appear only once in its object.

For example, this file leaves out a tier's `outputPer1M`:
```

with:

```markdown
- Most errors give a path into your file, such as `foundryByok.models[0].windows[0].tiers[0].outputPer1M`. Indexes count from 0, and they count only the entries in your file.
- If the file isn't valid JSON, the error gives the line and column where reading stopped, counting from 1.
- Three checks compare your file with the built-in rates: a model `id` or alias that a built-in model of the same section already uses, a `localOffload.baselineModel` that isn't a GitHub model, and an `apiEquivalent.localBaselineModel` that isn't an API-equivalent model. These errors say `in the merged rates`.
- Property names are case-sensitive, and each property can appear only once in its object.

For example, this file leaves out a tier's `outputPer1M`:
```

Edit 3 of 6 in `docs/configuration.md`, replace:

````markdown
- `effectiveFrom` (inclusive) and `effectiveUntil` (exclusive) bound a price window. Use UTC timestamps such as `2027-05-17T00:00:00Z`.

## Field reference

Overlay sections:
````

with:

````markdown
- `effectiveFrom` (inclusive) and `effectiveUntil` (exclusive) bound a price window. Use UTC timestamps such as `2027-05-17T00:00:00Z`.

### API list prices

The `apiEquivalent` section holds API list prices: what a model costs when you call its vendor's API yourself, in USD per 1,000,000 tokens. They're for comparison only. They never change AI credits, Foundry BYOK spend, local savings, or budgets. The section needs `"schemaVersion": 2`. For example, to compare with a price list your company negotiated:

```json
{
  "schemaVersion": 2,
  "ratesVersion": "contoso-2026-12",
  "apiEquivalent": {
    "models": [
      {
        "id": "claude-sonnet-5.5",
        "vendor": "anthropic",
        "sourceUrl": "https://contoso.example/agreements/anthropic-2026",
        "windows": [
          { "tiers": [ { "name": "default", "inputPer1M": 1.60, "cachedInputPer1M": 0.08, "cacheWritePer1M": 2.00, "outputPer1M": 8.00 } ] }
        ]
      }
    ]
  }
}
```

- API-equivalent models have the same fields and rules as the other priced models, plus `vendor` and `sourceUrl`, which are optional notes for people. Their ids and aliases must be unique within `apiEquivalent`, but a GitHub or Foundry BYOK model can have the same id.
- A model with the same `id` as a built-in API-equivalent model replaces it, and a model with a new `id` is added.
- `localBaselineModel` is optional. It names the API-equivalent model whose list prices value local requests, and it must be one of them. Without it, local requests use the list prices of `localOffload.baselineModel`, if it has any.
- `description` is optional text.

## Field reference

Overlay sections:
````

Edit 4 of 6 in `docs/configuration.md`, replace:

```markdown
| Field | Required | Notes |
|---|---|---|
| `schemaVersion` | Yes | Must be `1` |
| `ratesVersion` | No | Label recorded with each priced request |
| `notes` | No | Strings for your own notes |
```

with:

```markdown
| Field | Required | Notes |
|---|---|---|
| `schemaVersion` | Yes | `1`, or `2` when the file has `apiEquivalent` |
| `ratesVersion` | No | Label recorded with each priced request |
| `notes` | No | Strings for your own notes |
```

Edit 5 of 6 in `docs/configuration.md`, replace:

```markdown
| `localOffload.baselineModel` | No | A GitHub model id or alias used to estimate savings |
| `localOffload.additionalHosts` | No | Hosts treated as local |

Priced model entries:
```

with:

```markdown
| `localOffload.baselineModel` | No | A GitHub model id or alias used to estimate savings |
| `localOffload.additionalHosts` | No | Hosts treated as local |
| `apiEquivalent.models` | No | API list prices, as priced models (see the next table) |
| `apiEquivalent.localBaselineModel` | No | An API-equivalent model id or alias that values local requests |
| `apiEquivalent.description` | No | Text for your own notes |

Priced model entries:
```

Edit 6 of 6 in `docs/configuration.md`, replace:

```markdown
|---|---|---|
| `id` | Yes | — |
| `aliases` | No | `[]` |
| `windows` | Yes, at least one | — |
```

with:

```markdown
|---|---|---|
| `id` | Yes | — |
| `vendor`, `sourceUrl` | No; only in `apiEquivalent.models` | None |
| `aliases` | No | `[]` |
| `windows` | Yes, at least one | — |
```

### Tests

- **AK1** (10 cases, each a one-string change to `RetailTestRatesJson`): `apiEquivalent` under `schemaVersion` 1; `schemaVersion` 3; a rate with 7 decimal places; overlapping windows; a duplicate alias; an unknown `Description`; a `localBaselineModel` that doesn't resolve; a blank one; `vendor` on a GitHub model; a numeric `vendor`. Each throws `InvalidDataException` with the exact JSON-path message.
- **AK2** (7 cases): the same rules for an overlay, with the `overlay:` prefix and `in the merged rates` where the merge matters.
- **AK3**: an overlay replaces `test-retail` and adds `test-new`; the merged hash equals that of a card written with both changes. **AK4**: an overlay's `localBaselineModel` resolves by alias. **AK5**: `SchemaVersion` and the pinned hashes of the fixture and of a version 1 card, and a version 2 overlay on a version 1 card. **AK6**: reordering models, changing `vendor`, `sourceUrl`, `description`, and `notes`, writing ids and aliases in another case, `3` for `3.00`, and Windows line endings don't change the hash. **AK7** (5 cases): a rate, a tier bound, a window bound, an alias, or a tier name does.
- `RateCardLoadingTests` gains one case, `apiEquivalent: is required`; its, `RateOverlayTests`', and `RatesCommandTests`' unsupported-version cases move to `schemaVersion` 3.

### Acceptance criteria

- The reader accepts version 1 without `apiEquivalent` and version 2 with it, and rejects `apiEquivalent` under version 1, a seventh decimal place, and overlapping windows with JSON-path messages (AK1, AK2).
- Overlays add and replace API-equivalent models by `id` (AK3); the hash is deterministic and covers every price (AK5–AK7).
- Every existing pricing test passes unchanged, apart from the unsupported-version cases.
- `dotnet test CopilotLedger.slnx` passes **831** tests (804 + 27).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 2 specification: API-Equivalent Pricing

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Pricing/ApiEquivalentStatus.cs` | New: `Priced`, `Unpriced`, `NotApplicable` |
| `src/CopilotLedger.Core/Pricing/ApiEquivalentCost.cs` | New: the result, with the card's `RatesVersion` and `RateCardSha256` |
| `src/CopilotLedger.Core/Pricing/ApiEquivalentRequest.cs` | New: the stored facts that price a recorded request |
| `src/CopilotLedger.Core/Pricing/CreditCalculationResult.cs` | `required ApiEquivalentCost ApiEquivalent` |
| `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs` | `CalculateApiEquivalent`; every result is priced at API list prices too |
| `tests/CopilotLedger.Tests/Pricing/ApiEquivalentPricingTests.cs` | New: AP1–AP10 |
| `docs/configuration.md` | New "API-equivalent prices" section |

### Contracts

```csharp
namespace CopilotLedger.Core.Pricing;

public enum ApiEquivalentStatus { Priced, Unpriced, NotApplicable }

public sealed record ApiEquivalentCost
{
    public required ApiEquivalentStatus Status { get; init; }
    public required string RatesVersion { get; init; }       // the card that judged the request
    public required string RateCardSha256 { get; init; }     // that card's ApiRateCardSha256
    public string? RateCardModelId { get; init; }            // null unless Priced
    public string? TierName { get; init; }                   // null unless Priced
    public TokenCostBreakdown? Breakdown { get; init; }      // null unless Priced
    public decimal? UsdCost => Breakdown?.TotalUsd;          // null unless Priced, never 0 for unknown
}

public sealed record ApiEquivalentRequest
{
    public required string OperationName { get; init; }
    public required BillingChannel Channel { get; init; }
    public string? ModelId { get; init; }                    // CreditCalculationResult.ModelId
    public required DateTimeOffset StartTime { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
}

// CreditCalculationResult gains:
public required ApiEquivalentCost ApiEquivalent { get; init; }

// JsonRateCardCalculator gains:
public ApiEquivalentCost CalculateApiEquivalent(ApiEquivalentRequest request);
```

### Algorithm (`CalculateApiEquivalent`)

1. A non-inference operation, a negative count, or cache reads and writes beyond the input tokens: `NotApplicable`.
2. The card: the local retail baseline when `Channel` is `Local`; otherwise the API-equivalent model whose id or alias is the normalized `ModelId`; none when there's no model.
3. The window and tier: the same `Select` as the credit lens, by start time and total input tokens. No card or no window: `Unpriced`.
4. `Priced`: the breakdown `CostUsd(uncached, input)`, `CostUsd(cacheRead, cachedInput ?? input)`, `CostUsd(cacheWrite, cacheWrite ?? input)`, `CostUsd(output, output)`, with the model's canonical id and the tier name.

Every result carries the card's `RatesVersion` and `ApiRateCardSha256`. `Calculate` calls it with the span's facts and the credit lens's channel and `ModelId`, for every result, including `NotBillableOperation` and `InvalidUsage`.

### Code (copy exactly)

New file `src/CopilotLedger.Core/Pricing/ApiEquivalentCost.cs`:

```csharp
namespace CopilotLedger.Core.Pricing;

// A request priced at API list prices. It's a comparison: it never changes the request's billed amounts.
public sealed record ApiEquivalentCost
{
    public required ApiEquivalentStatus Status { get; init; }
    public required string RatesVersion { get; init; }       // the rate card that judged the request
    public required string RateCardSha256 { get; init; }     // that card's JsonRateCardCalculator.ApiRateCardSha256
    public string? RateCardModelId { get; init; }            // the apiEquivalent model applied; null unless Priced
    public string? TierName { get; init; }                   // null unless Priced
    public TokenCostBreakdown? Breakdown { get; init; }      // null unless Priced
    public decimal? UsdCost => Breakdown?.TotalUsd;          // null unless Priced, never 0 for unknown
}
```

New file `src/CopilotLedger.Core/Pricing/ApiEquivalentRequest.cs`:

```csharp
namespace CopilotLedger.Core.Pricing;

// The stored facts that the API-equivalent lens prices: a recorded request, or one being recorded.
public sealed record ApiEquivalentRequest
{
    public required string OperationName { get; init; }
    public required BillingChannel Channel { get; init; }    // Local requests are priced as the card's local retail baseline
    public string? ModelId { get; init; }                    // CreditCalculationResult.ModelId; null when the request named no model
    public required DateTimeOffset StartTime { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
}
```

New file `src/CopilotLedger.Core/Pricing/ApiEquivalentStatus.cs`:

```csharp
namespace CopilotLedger.Core.Pricing;

// How the API-equivalent (retail) lens judged a request.
public enum ApiEquivalentStatus
{
    Priced,          // an apiEquivalent model and window priced it
    Unpriced,        // no apiEquivalent model, or no window for the request's start time
    NotApplicable,   // not an inference operation, or inconsistent token counts (InvalidUsage)
}
```

Edit 1 of 1 in `src/CopilotLedger.Core/Pricing/CreditCalculationResult.cs`, replace:

```csharp
    public decimal AiCredits { get; init; }               // UsdCost / 0.01m when Channel is GitHubAiCredits; otherwise 0
    public decimal EstimatedCloudSavedUsd { get; init; }  // Local only: Breakdown.TotalUsd; otherwise 0
}
```

with:

```csharp
    public decimal AiCredits { get; init; }               // UsdCost / 0.01m when Channel is GitHubAiCredits; otherwise 0
    public decimal EstimatedCloudSavedUsd { get; init; }  // Local only: Breakdown.TotalUsd; otherwise 0
    public required ApiEquivalentCost ApiEquivalent { get; init; }   // the same request at API list prices
}
```

Edit 1 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
    private readonly IReadOnlyList<string> _localProviders;
    private readonly ModelRateCard _baselineCard;

    private JsonRateCardCalculator(
```

with:

```csharp
    private readonly IReadOnlyList<string> _localProviders;
    private readonly ModelRateCard _baselineCard;
    private readonly IReadOnlyList<ModelRateCard> _retailModels;
    private readonly ModelRateCard? _retailLocalBaseline;

    private JsonRateCardCalculator(
```

Edit 2 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        _localProviders = localProviders;
        _baselineCard = baselineCard;
        SchemaVersion = schemaVersion;
        ApiRateCardSha256 = ApiRateCardHash(retailModels, retailLocalBaseline);
```

with:

```csharp
        _localProviders = localProviders;
        _baselineCard = baselineCard;
        _retailModels = retailModels;
        _retailLocalBaseline = retailLocalBaseline;
        SchemaVersion = schemaVersion;
        ApiRateCardSha256 = ApiRateCardHash(retailModels, retailLocalBaseline);
```

Edit 3 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        if (!GenAiOperationNames.IsInference(span.OperationName))
        {
            return Result(BillingChannel.Unpriced, PricingStatus.NotBillableOperation, model);
        }

        if (span.InputTokens < 0 ||
```

with:

```csharp
        if (!GenAiOperationNames.IsInference(span.OperationName))
        {
            return Result(span, BillingChannel.Unpriced, PricingStatus.NotBillableOperation, model);
        }

        if (span.InputTokens < 0 ||
```

Edit 4 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            span.CacheWriteInputTokens > span.InputTokens - span.CacheReadInputTokens)
        {
            return Result(BillingChannel.Unpriced, PricingStatus.InvalidUsage, model);
        }

        long uncached = span.InputTokens - span.CacheReadInputTokens - span.CacheWriteInputTokens;
```

with:

```csharp
            span.CacheWriteInputTokens > span.InputTokens - span.CacheReadInputTokens)
        {
            return Result(span, BillingChannel.Unpriced, PricingStatus.InvalidUsage, model);
        }

        long uncached = span.InputTokens - span.CacheReadInputTokens - span.CacheWriteInputTokens;
```

Edit 5 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            {
                return Result(
                    BillingChannel.Local,
                    PricingStatus.NoRateForDate,
```

with:

```csharp
            {
                return Result(
                    span,
                    BillingChannel.Local,
                    PricingStatus.NoRateForDate,
```

Edit 6 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            TokenCostBreakdown breakdown = Price(span, selection.Tier, uncached);
            return Result(
                BillingChannel.Local,
                PricingStatus.LocalOffload,
```

with:

```csharp
            TokenCostBreakdown breakdown = Price(span, selection.Tier, uncached);
            return Result(
                span,
                BillingChannel.Local,
                PricingStatus.LocalOffload,
```

Edit 7 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            {
                return Result(
                    BillingChannel.FoundryByok,
                    PricingStatus.UnknownModel,
```

with:

```csharp
            {
                return Result(
                    span,
                    BillingChannel.FoundryByok,
                    PricingStatus.UnknownModel,
```

Edit 8 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            {
                return Result(
                    BillingChannel.FoundryByok,
                    PricingStatus.ByokUnpriced,
```

with:

```csharp
            {
                return Result(
                    span,
                    BillingChannel.FoundryByok,
                    PricingStatus.ByokUnpriced,
```

Edit 9 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            {
                return Result(
                    BillingChannel.FoundryByok,
                    PricingStatus.NoRateForDate,
```

with:

```csharp
            {
                return Result(
                    span,
                    BillingChannel.FoundryByok,
                    PricingStatus.NoRateForDate,
```

Edit 10 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            TokenCostBreakdown breakdown = Price(span, selection.Tier, uncached);
            return Result(
                BillingChannel.FoundryByok,
                PricingStatus.Priced,
```

with:

```csharp
            TokenCostBreakdown breakdown = Price(span, selection.Tier, uncached);
            return Result(
                span,
                BillingChannel.FoundryByok,
                PricingStatus.Priced,
```

Edit 11 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            if (model is null)
            {
                return Result(
                    BillingChannel.GitHubAiCredits,
                    PricingStatus.UnknownModel,
```

with:

```csharp
            if (model is null)
            {
                return Result(
                    span,
                    BillingChannel.GitHubAiCredits,
                    PricingStatus.UnknownModel,
```

Edit 12 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            {
                return Result(
                    BillingChannel.GitHubAiCredits,
                    PricingStatus.UnknownModel,
```

with:

```csharp
            {
                return Result(
                    span,
                    BillingChannel.GitHubAiCredits,
                    PricingStatus.UnknownModel,
```

Edit 13 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            {
                return Result(
                    BillingChannel.GitHubAiCredits,
                    PricingStatus.NoRateForDate,
```

with:

```csharp
            {
                return Result(
                    span,
                    BillingChannel.GitHubAiCredits,
                    PricingStatus.NoRateForDate,
```

Edit 14 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            TokenCostBreakdown breakdown = Price(span, selection.Tier, uncached);
            return Result(
                BillingChannel.GitHubAiCredits,
                PricingStatus.Priced,
```

with:

```csharp
            TokenCostBreakdown breakdown = Price(span, selection.Tier, uncached);
            return Result(
                span,
                BillingChannel.GitHubAiCredits,
                PricingStatus.Priced,
```

Edit 15 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        }

        return Result(
            BillingChannel.Unpriced,
            PricingStatus.UnrecognizedEndpoint,
```

with:

```csharp
        }

        return Result(
            span,
            BillingChannel.Unpriced,
            PricingStatus.UnrecognizedEndpoint,
```

Edit 16 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
    }

    private static JsonRateCardCalculator Create(string baseJson, string? overlayJson, string? overlayName)
    {
```

with:

```csharp
    }

    // Prices one request at this card's API list prices (apiEquivalent), whatever its billing channel priced: a local
    // request as the local retail baseline, and any other request as its ModelId. Pure and thread-safe. Throws only
    // ArgumentNullException; an unknown model or date is Unpriced, and a non-inference operation or inconsistent token
    // counts are NotApplicable.
    public ApiEquivalentCost CalculateApiEquivalent(ApiEquivalentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!GenAiOperationNames.IsInference(request.OperationName) ||
            request.InputTokens < 0 ||
            request.OutputTokens < 0 ||
            request.CacheReadInputTokens < 0 ||
            request.CacheWriteInputTokens < 0 ||
            request.CacheReadInputTokens > request.InputTokens ||
            request.CacheWriteInputTokens > request.InputTokens - request.CacheReadInputTokens)
        {
            return Retail(ApiEquivalentStatus.NotApplicable);
        }

        string? model = NormalizeModel(request.ModelId);
        ModelRateCard? card = request.Channel == BillingChannel.Local
            ? _retailLocalBaseline
            : model is null ? null : FindModel(_retailModels, model);
        PricedSelection? selection = card is null
            ? null
            : Select(card, request.StartTime, request.InputTokens);
        if (selection is null)
        {
            return Retail(ApiEquivalentStatus.Unpriced);
        }

        long uncached = request.InputTokens - request.CacheReadInputTokens - request.CacheWriteInputTokens;
        return Retail(
            ApiEquivalentStatus.Priced,
            card!.ModelId,
            selection.Tier.Name,
            Price(request, selection.Tier, uncached));
    }

    private static JsonRateCardCalculator Create(string baseJson, string? overlayJson, string? overlayName)
    {
```

Edit 17 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            AiCreditMath.CostUsd(span.OutputTokens, tier.OutputPer1M));

    private CreditCalculationResult Result(
        BillingChannel channel,
        PricingStatus status,
```

with:

```csharp
            AiCreditMath.CostUsd(span.OutputTokens, tier.OutputPer1M));

    private static TokenCostBreakdown Price(ApiEquivalentRequest request, RateTier tier, long uncached) =>
        new(
            AiCreditMath.CostUsd(uncached, tier.InputPer1M),
            AiCreditMath.CostUsd(request.CacheReadInputTokens, tier.CachedInputPer1M ?? tier.InputPer1M),
            AiCreditMath.CostUsd(request.CacheWriteInputTokens, tier.CacheWritePer1M ?? tier.InputPer1M),
            AiCreditMath.CostUsd(request.OutputTokens, tier.OutputPer1M));

    // The credit lens's result for span, with the same request priced in the API-equivalent lens.
    private CreditCalculationResult Result(
        NormalizedGenAiSpan span,
        BillingChannel channel,
        PricingStatus status,
```

Edit 18 of 18 in `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
            AiCredits = aiCredits,
            EstimatedCloudSavedUsd = estimatedCloudSavedUsd,
        };

    // SHA-256 of the canonical API-equivalent card: compact UTF-8 JSON {"localBaselineModel":…,"models":[…]}. The local
```

with:

```csharp
            AiCredits = aiCredits,
            EstimatedCloudSavedUsd = estimatedCloudSavedUsd,
            ApiEquivalent = CalculateApiEquivalent(new ApiEquivalentRequest
            {
                OperationName = span.OperationName,
                Channel = channel,
                ModelId = modelId,
                StartTime = span.StartTime,
                InputTokens = span.InputTokens,
                OutputTokens = span.OutputTokens,
                CacheReadInputTokens = span.CacheReadInputTokens,
                CacheWriteInputTokens = span.CacheWriteInputTokens,
            }),
        };

    private ApiEquivalentCost Retail(
        ApiEquivalentStatus status,
        string? rateCardModelId = null,
        string? tierName = null,
        TokenCostBreakdown? breakdown = null) =>
        new()
        {
            Status = status,
            RatesVersion = RatesVersion,
            RateCardSha256 = ApiRateCardSha256,
            RateCardModelId = rateCardModelId,
            TierName = tierName,
            Breakdown = breakdown,
        };

    // SHA-256 of the canonical API-equivalent card: compact UTF-8 JSON {"localBaselineModel":…,"models":[…]}. The local
```

New file `tests/CopilotLedger.Tests/Pricing/ApiEquivalentPricingTests.cs`:

```csharp
using System.Globalization;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class ApiEquivalentPricingTests
{
    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    // start, input, cache read, cache write, and output tokens of test-retail, then the tier and the expected uncached
    // input, cache read, cache write, and output USD at RetailTestRatesJson's API-equivalent prices.
    public static TheoryData<string, long, long, long, long, string, decimal, decimal, decimal, decimal> Vectors => new()
    {
        { "2026-11-20T12:00:00Z", 100_000, 0, 0, 0, "default", 0.3m, 0m, 0m, 0m },
        { "2026-11-20T12:00:00Z", 100_000, 60_000, 0, 0, "default", 0.12m, 0.018m, 0m, 0m },
        { "2026-11-20T12:00:00Z", 100_000, 0, 20_000, 0, "default", 0.24m, 0m, 0.075m, 0m },
        { "2026-11-20T12:00:00Z", 0, 0, 0, 10_000, "default", 0m, 0m, 0m, 0.15m },
        { "2026-11-20T12:00:00Z", 200_000, 120_000, 30_000, 5_000, "default", 0.15m, 0.036m, 0.1125m, 0.075m },
        { "2026-11-20T12:00:00Z", 200_001, 120_000, 30_000, 5_000, "long_context", 0.300006m, 0.072m, 0.225m, 0.1125m },
        { "2026-11-30T23:59:59.9999999Z", 100_000, 60_000, 20_000, 10_000, "default", 0.06m, 0.018m, 0.075m, 0.15m },
        { "2026-12-01T00:00:00Z", 100_000, 60_000, 20_000, 10_000, "default", 0.08m, 0.024m, 0.1m, 0.2m },
        { "2026-12-15T00:00:00Z", 300_000, 0, 0, 1_000, "long_context", 2.4m, 0m, 0m, 0.03m },
        { "2026-11-20T12:00:00Z", 1, 1, 0, 0, "default", 0m, 0.0000003m, 0m, 0m },
    };

    public static TheoryData<string, string, decimal> Models => new()
    {
        { "Test Retail", "test-retail", 0.45m },
        { "test-retail-2026-11-01", "test-retail", 0.45m },
        { "test-no-cache", "test-no-cache", 0.28m },
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void AP1_PricesEachKindOfTokenExactly(
        string start,
        long input,
        long cacheRead,
        long cacheWrite,
        long output,
        string tier,
        decimal uncachedUsd,
        decimal cacheReadUsd,
        decimal cacheWriteUsd,
        decimal outputUsd)
    {
        ApiEquivalentCost cost = Retail.Calculate(TestSpans.Chat(
            startTime: DateTimeOffset.Parse(start, CultureInfo.InvariantCulture),
            requestModel: "test-retail",
            inputTokens: input,
            outputTokens: output,
            cacheReadInputTokens: cacheRead,
            cacheWriteInputTokens: cacheWrite)).ApiEquivalent;

        Assert.Equal(ApiEquivalentStatus.Priced, cost.Status);
        Assert.Equal("test-retail", cost.RateCardModelId);
        Assert.Equal(tier, cost.TierName);
        Assert.NotNull(cost.Breakdown);
        Assert.Equal(uncachedUsd, cost.Breakdown.UncachedInputUsd);
        Assert.Equal(cacheReadUsd, cost.Breakdown.CacheReadUsd);
        Assert.Equal(cacheWriteUsd, cost.Breakdown.CacheWriteUsd);
        Assert.Equal(outputUsd, cost.Breakdown.OutputUsd);
        Assert.Equal(uncachedUsd + cacheReadUsd + cacheWriteUsd + outputUsd, cost.UsdCost);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void AP10_ModelNamesAndMissingCacheRates(string model, string rateCardModelId, decimal usd)
    {
        ApiEquivalentCost cost = Retail.Calculate(Span(model)).ApiEquivalent;

        Assert.Equal(ApiEquivalentStatus.Priced, cost.Status);
        Assert.Equal(rateCardModelId, cost.RateCardModelId);
        Assert.Equal(usd, cost.UsdCost);
    }

    [Theory]
    [InlineData("gpt-unknown", null, "2026-11-20T12:00:00Z")]
    [InlineData(null, null, "2026-11-20T12:00:00Z")]
    [InlineData("gpt-6.1-sol", null, "2026-05-31T23:59:59Z")]
    [InlineData("gpt-unknown", "contoso.openai.azure.com", "2026-11-20T12:00:00Z")]
    public void AP2_AnUnknownModelOrDateIsUnpricedNeverZero(string? model, string? host, string start)
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            startTime: DateTimeOffset.Parse(start, CultureInfo.InvariantCulture),
            serverAddress: host,
            requestModel: model,
            inputTokens: 100_000,
            outputTokens: 10_000));

        Assert.Equal(ApiEquivalentStatus.Unpriced, result.ApiEquivalent.Status);
        Assert.Null(result.ApiEquivalent.UsdCost);
        Assert.Null(result.ApiEquivalent.Breakdown);
        Assert.Null(result.ApiEquivalent.RateCardModelId);
        Assert.Null(result.ApiEquivalent.TierName);
    }

    [Theory]
    [InlineData("invoke_agent", 100_000, 0, 0, 10_000)]
    [InlineData("execute_tool", 100_000, 0, 0, 10_000)]
    [InlineData("chat", 100, 80, 30, 0)]
    [InlineData("chat", 100, 0, 0, -1)]
    public void AP3_NonInferenceAndInvalidUsageAreNotApplicable(
        string operation,
        long input,
        long cacheRead,
        long cacheWrite,
        long output)
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            operationName: operation,
            requestModel: "test-retail",
            inputTokens: input,
            outputTokens: output,
            cacheReadInputTokens: cacheRead,
            cacheWriteInputTokens: cacheWrite));

        Assert.Equal(
            operation == "chat" ? PricingStatus.InvalidUsage : PricingStatus.NotBillableOperation,
            result.Status);
        Assert.Equal(ApiEquivalentStatus.NotApplicable, result.ApiEquivalent.Status);
        Assert.Null(result.ApiEquivalent.UsdCost);
        Assert.Null(result.ApiEquivalent.Breakdown);
    }

    [Fact]
    public async Task AP4_LocalRequestsUseTheRetailBaselineAndKeepTheirSavings()
    {
        using TempDirectory temp = new();
        string overlay = Path.Combine(temp.Path, "rates.json");
        await File.WriteAllTextAsync(
            overlay,
            """{ "schemaVersion": 2, "apiEquivalent": { "localBaselineModel": "test-retail" } }""");
        await FileRetry.ReadAllTextAsync(overlay);
        NormalizedGenAiSpan span = TestSpans.Chat(
            serverAddress: "127.0.0.1",
            requestModel: "qwen-local",
            inputTokens: 400_000,
            outputTokens: 12_000);

        CreditCalculationResult baseline = Retail.Calculate(span);
        CreditCalculationResult chosen =
            JsonRateCardCalculator.LoadWithOverlay(RateFixtures.RetailTestRatesJson, overlay).Calculate(span);
        CreditCalculationResult withoutRetail =
            JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson).Calculate(span);
        CreditCalculationResult embedded = JsonRateCardCalculator.LoadEmbedded().Calculate(span);

        Assert.Equal(PricingStatus.LocalOffload, baseline.Status);
        Assert.Equal(0.23m, baseline.EstimatedCloudSavedUsd);
        Assert.Equal(ApiEquivalentStatus.Priced, baseline.ApiEquivalent.Status);
        Assert.Equal("test-baseline", baseline.ApiEquivalent.RateCardModelId);
        Assert.Equal(0.3584m, baseline.ApiEquivalent.UsdCost);
        Assert.Equal(0.23m, chosen.EstimatedCloudSavedUsd);
        Assert.Equal("test-retail", chosen.ApiEquivalent.RateCardModelId);
        Assert.Equal("long_context", chosen.ApiEquivalent.TierName);
        Assert.Equal(2.67m, chosen.ApiEquivalent.UsdCost);
        Assert.Equal(0.92m, withoutRetail.EstimatedCloudSavedUsd);
        Assert.Equal(ApiEquivalentStatus.Unpriced, withoutRetail.ApiEquivalent.Status);
        Assert.Null(withoutRetail.ApiEquivalent.UsdCost);
        Assert.Equal(PricingStatus.LocalOffload, embedded.Status);
        Assert.Equal(0.23m, embedded.EstimatedCloudSavedUsd);
        Assert.Equal("mai-code-2", embedded.RateCardModelId);
    }

    [Fact]
    public void AP5_ByokRequestsUseTheirCanonicalModel()
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            serverAddress: "contoso.openai.azure.com",
            requestModel: "contoso-gpt61-prod",
            inputTokens: 100_000,
            outputTokens: 10_000));

        Assert.Equal(BillingChannel.FoundryByok, result.Channel);
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal("gpt-6.1-sol", result.ModelId);
        Assert.Equal(0.315m, result.UsdCost);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        Assert.Equal("gpt-6.1-sol", result.ApiEquivalent.RateCardModelId);
        Assert.Equal(0.3m, result.ApiEquivalent.UsdCost);
    }

    [Fact]
    public void AP6_AnUnrecognizedEndpointStillGetsARetailEstimate()
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            serverAddress: "llm.example.com",
            requestModel: "test-retail",
            inputTokens: 100_000,
            outputTokens: 10_000));

        Assert.Equal(BillingChannel.Unpriced, result.Channel);
        Assert.Equal(PricingStatus.UnrecognizedEndpoint, result.Status);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        Assert.Equal(0.45m, result.ApiEquivalent.UsdCost);
    }

    [Fact]
    public void AP7_EveryResultRecordsTheCardThatJudgedIt()
    {
        CreditCalculationResult[] results =
        [
            Retail.Calculate(TestSpans.Chat(requestModel: "test-retail", inputTokens: 1_000)),
            Retail.Calculate(TestSpans.Chat(requestModel: "gpt-unknown", inputTokens: 1_000)),
            Retail.Calculate(TestSpans.Chat(operationName: "invoke_agent", requestModel: "test-retail")),
        ];

        Assert.Equal(
            [ApiEquivalentStatus.Priced, ApiEquivalentStatus.Unpriced, ApiEquivalentStatus.NotApplicable],
            results.Select(result => result.ApiEquivalent.Status));
        Assert.All(results, result =>
        {
            Assert.Equal("retail-fixture", result.ApiEquivalent.RatesVersion);
            Assert.Equal(Retail.ApiRateCardSha256, result.ApiEquivalent.RateCardSha256);
        });
    }

    public static TheoryData<BillingChannel, string?, ApiEquivalentStatus, string?, decimal?> Requests => new()
    {
        { BillingChannel.GitHubAiCredits, "test-retail", ApiEquivalentStatus.Priced, "test-retail", 0.45m },
        { BillingChannel.Unpriced, "Test Retail", ApiEquivalentStatus.Priced, "test-retail", 0.45m },
        { BillingChannel.FoundryByok, "test-baseline", ApiEquivalentStatus.Priced, "test-baseline", 0.112m },
        { BillingChannel.Local, "qwen-local", ApiEquivalentStatus.Priced, "test-baseline", 0.112m },
        { BillingChannel.Local, null, ApiEquivalentStatus.Priced, "test-baseline", 0.112m },
        { BillingChannel.GitHubAiCredits, null, ApiEquivalentStatus.Unpriced, null, null },
        { BillingChannel.GitHubAiCredits, "mai-code-2", ApiEquivalentStatus.Unpriced, null, null },
    };

    [Theory]
    [MemberData(nameof(Requests))]
    public void AP8_RecordedRequestsArePricedByChannelAndModel(
        BillingChannel channel,
        string? model,
        ApiEquivalentStatus status,
        string? rateCardModelId,
        decimal? usd)
    {
        ApiEquivalentCost cost = Retail.CalculateApiEquivalent(new ApiEquivalentRequest
        {
            OperationName = "chat",
            Channel = channel,
            ModelId = model,
            StartTime = DateTimeOffset.Parse("2026-11-20T12:00:00Z", CultureInfo.InvariantCulture),
            InputTokens = 100_000,
            OutputTokens = 10_000,
            CacheReadInputTokens = 0,
            CacheWriteInputTokens = 0,
        });

        Assert.Equal(status, cost.Status);
        Assert.Equal(rateCardModelId, cost.RateCardModelId);
        Assert.Equal(usd, cost.UsdCost);
    }

    [Fact]
    public async Task AP9_AnOverlayAddsAndReplacesApiEquivalentModelsById()
    {
        using TempDirectory temp = new();
        string overlay = Path.Combine(temp.Path, "rates.json");
        await File.WriteAllTextAsync(
            overlay,
            """
            {
              "schemaVersion": 2,
              "ratesVersion": "contoso-2026-12",
              "apiEquivalent": {
                "models": [
                  { "id": "test-retail", "windows": [ { "tiers": [ { "name": "negotiated", "inputPer1M": 1.50, "cachedInputPer1M": 0.15, "outputPer1M": 7.50 } ] } ] },
                  { "id": "test-added", "vendor": "contoso", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 0.25, "outputPer1M": 1.25 } ] } ] }
                ]
              }
            }
            """);
        await FileRetry.ReadAllTextAsync(overlay);
        JsonRateCardCalculator merged = JsonRateCardCalculator.LoadWithOverlay(RateFixtures.RetailTestRatesJson, overlay);

        CreditCalculationResult replaced = merged.Calculate(Span("test-retail"));
        CreditCalculationResult added = merged.Calculate(Span("test-added"));
        CreditCalculationResult kept = merged.Calculate(Span("test-no-cache"));

        Assert.Equal("retail-fixture+contoso-2026-12", merged.RatesVersion);
        Assert.Equal(PricingStatus.Priced, replaced.Status);
        Assert.Equal(0.15m, replaced.UsdCost);
        Assert.Equal("negotiated", replaced.ApiEquivalent.TierName);
        Assert.Equal(0.225m, replaced.ApiEquivalent.UsdCost);
        Assert.Equal(PricingStatus.UnknownModel, added.Status);
        Assert.Equal(0.0375m, added.ApiEquivalent.UsdCost);
        Assert.Equal(0.28m, kept.ApiEquivalent.UsdCost);
        Assert.Equal("retail-fixture+contoso-2026-12", replaced.ApiEquivalent.RatesVersion);
        Assert.Equal(merged.ApiRateCardSha256, replaced.ApiEquivalent.RateCardSha256);
    }

    private static NormalizedGenAiSpan Span(string model) => TestSpans.Chat(
        requestModel: model,
        inputTokens: 100_000,
        outputTokens: 10_000);
}
```

Edit 1 of 1 in `docs/configuration.md`, replace:

```markdown
Cache-efficiency reports, such as `gh ledger status --cache`, don't treat such a request as a cache miss: its cache use is unknown, so hit ratios and observed cache totals leave it out, and a group of such requests shows `n/a` rather than `0.0%`. A cache miss that writes nothing to the cache also reports neither count, so it's left out too, and a hit ratio can be higher than the real one. See [Prompt-cache efficiency](cli.md#prompt-cache-efficiency).

## Why Foundry BYOK is unpriced by default

With bring-your-own-key (BYOK), Microsoft Foundry bills you directly, not GitHub. Azure prices depend on region, deployment type, and your agreement, so CopilotLedger can't know yours.
```

with:

```markdown
Cache-efficiency reports, such as `gh ledger status --cache`, don't treat such a request as a cache miss: its cache use is unknown, so hit ratios and observed cache totals leave it out, and a group of such requests shows `n/a` rather than `0.0%`. A cache miss that writes nothing to the cache also reports neither count, so it's left out too, and a hit ratio can be higher than the real one. See [Prompt-cache efficiency](cli.md#prompt-cache-efficiency).

### API-equivalent prices

Besides what each request is billed, CopilotLedger records what the same request would cost at its model's API list prices, from the `apiEquivalent` section of the rates (see [API list prices](#api-list-prices)). This API-equivalent cost is a comparison. It's never added to billed spend, and budgets and the circuit breaker ignore it.

- A request is priced as the model it's recorded with: a GitHub or Foundry BYOK model's id once its aliases are resolved, or else the model name the request reported. A local request is priced as the local baseline instead. Nothing else depends on the billing channel, so a request to an unrecognized endpoint still gets an API-equivalent cost when its model has a list price.
- A request whose model has no API list price, or no price for the request's date, is unpriced in this view. It has no API-equivalent cost, never $0.00.
- A request with inconsistent token counts (`invalid_usage`) gets no API-equivalent cost either.
- Cache reads and writes are priced at the list prices for them, as billing is. A request that reported no cache counters is priced as if all of its input were uncached, so its API-equivalent cost is an estimate, and reports mark it as one.

## Why Foundry BYOK is unpriced by default

With bring-your-own-key (BYOK), Microsoft Foundry bills you directly, not GitHub. Azure prices depend on region, deployment type, and your agreement, so CopilotLedger can't know yours.
```

### Tests

- **AP1** (10 `TheoryData<string, long, long, long, long, string, decimal, decimal, decimal, decimal>` cases): uncached input, cache reads, cache writes, and output alone and together; the `default`/`long_context` boundary (200,000 and 200,001 input tokens); both sides of a window bound; and 1 cache-read token (`0.0000003`). Each part of the breakdown, and their sum, is exact.
- **AP2** (4): an unknown model, no model, a date before the model's window, and an unknown Foundry BYOK model are `Unpriced` with `null` cost, breakdown, model, and tier. **AP3** (4): `invoke_agent`, `execute_tool`, cache tokens beyond input, and negative output are `NotApplicable`.
- **AP4**: a local request is priced as `test-baseline` (`0.3584`), or as the overlay's `localBaselineModel` (`2.67`, long-context), while its savings stay `0.23`; a card without list prices leaves it `Unpriced`, and the built-in card keeps its savings at the `mai-code-2` baseline. **AP5**: a Foundry BYOK alias prices as the canonical `gpt-6.1-sol`. **AP6**: an unrecognized endpoint with a listed model gets `0.45`. **AP7**: every status records the card's version and hash.
- **AP8** (7): `CalculateApiEquivalent` by channel and model, including a local request without a model and an unlisted MAI model. **AP9**: an overlay replaces and adds API-equivalent models by `id`. **AP10** (3): a display-name id, a dated alias, and a model without cache rates.

### Acceptance criteria

- `UsdCost` is exactly `tokens × rate / 1,000,000` per kind of token, with multi-tier windows (AP1), `null` for unknown models and dates (AP2), and `NotApplicable` for non-inference operations and invalid usage (AP3).
- Local requests use the retail baseline and keep their savings (AP4); a listed model on an unrecognized endpoint is priced (AP6).
- `dotnet test CopilotLedger.slnx` passes **864** tests (831 + 33).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 3 specification: Built-In API List Prices

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Pricing/rates.json` | `schemaVersion` 2, `ratesVersion` `2026-11-23`, 7 more `notes`, and the `apiEquivalent` section (31 models) |
| `tests/CopilotLedger.Tests/Pricing/BuiltInApiRatesTests.cs` | New: AR1–AR6 |
| `tests/CopilotLedger.Tests/TestSupport/PublishedRates.cs` | The embedded card's `ratesVersion` line |
| `tests/CopilotLedger.Tests/Pricing/JsonRateCardCalculatorTests.cs`, `RatesRefreshTests.cs`, `RateOverlayTests.cs`, `tests/CopilotLedger.Tests/Cli/RatesCommandTests.cs`, `RatesUpdateTests.cs`, `ReportCommandTests.cs` | `2026-11-20` becomes `2026-11-23` where the built-in version shows |
| `docs/configuration.md`, `docs/guide.md` | The built-in version, and the built-in list prices and their policies |

### Data

The `apiEquivalent` section is the table in "Verified facts", with each model's `vendor` and `sourceUrl` and these aliases: the Anthropic API ids (`claude-opus-5-5`, `claude-sonnet-4-6`, …), `claude-haiku-4-5-20251001`, `claude-sonnet-4-20250514`, and `gpt-5.4-nano-2026-03-17`. The `githubAiCredits`, `foundryByok`, and `localOffload` sections don't change. The 7 new `notes` state the source and processing tier, the ids and aliases, cache-write lifetimes, tier thresholds, the window policy, and the three models without a list price.

### Code (copy exactly)

Edit 1 of 3 in `src/CopilotLedger.Core/Pricing/rates.json`, replace:

```json
{
  "schemaVersion": 1,
  "ratesVersion": "2026-11-20",
  "source": {
    "url": "https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing",
```

with:

```json
{
  "schemaVersion": 2,
  "ratesVersion": "2026-11-23",
  "source": {
    "url": "https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing",
```

Edit 2 of 3 in `src/CopilotLedger.Core/Pricing/rates.json`, replace:

```json
    "Model ids are the ids Copilot reports. gpt-5.4-nano, claude-sonnet-4, claude-opus-4.8, claude-fable-5, claude-fable-5.1, claude-fable-5.2, gemini-4-neon, grok-4.5, and kimi-k3 are derived from the pricing page's display names. Claude Opus 4.8 (fast mode) is omitted because its id is unknown.",
    "foundryByok.models is empty because Azure prices vary by region, deployment type, and agreement; add yours in ~/.copilot-ledger/rates.json (see docs/configuration.md).",
    "Local spans bill 0 USD; savings price the same tokens at the GitHub rates of localOffload.baselineModel. localOffload.models maps local runtime ids to one canonical model."
  ],
  "githubAiCredits": {
```

with:

```json
    "Model ids are the ids Copilot reports. gpt-5.4-nano, claude-sonnet-4, claude-opus-4.8, claude-fable-5, claude-fable-5.1, claude-fable-5.2, gemini-4-neon, grok-4.5, and kimi-k3 are derived from the pricing page's display names. Claude Opus 4.8 (fast mode) is omitted because its id is unknown.",
    "foundryByok.models is empty because Azure prices vary by region, deployment type, and agreement; add yours in ~/.copilot-ledger/rates.json (see docs/configuration.md).",
    "Local spans bill 0 USD; savings price the same tokens at the GitHub rates of localOffload.baselineModel. localOffload.models maps local runtime ids to one canonical model.",
    "apiEquivalent holds each vendor's public API list prices (standard processing, global endpoint; not batch, flex, priority, fast mode, or regional prices), retrieved on 2026-11-23 from each model's sourceUrl. They are a comparison and are never billed.",
    "apiEquivalent ids are the ids Copilot reports. Aliases add the vendor's own API ids, such as claude-opus-4-8, and dated snapshot ids, but not moving aliases such as grok-4.5-latest.",
    "API cache writes use the vendor's default cache lifetime: 5 minutes at Anthropic and Moonshot, 30 minutes at OpenAI. Google and xAI list no cache-write price, so their cache writes are priced at inputPer1M.",
    "API tiers: OpenAI's long-context prices apply above 272,000 input tokens, Claude Haiku 5.5's above 100,000, and xAI's from 200,000, so xAI's default tiers end at 199999. Thresholds count all input tokens, including cache reads and writes.",
    "API windows start and end at 00:00:00Z. A promotion ends on the day after its last day: gemini-4-argon's and gemini-4-carbon's on 2026-12-01 and gemini-3.8-flash's on 2027-01-01, where Google's published later prices begin, and gpt-5.6-sol's on 2026-12-01, after which OpenAI publishes no price. A retired or shut-down model's prices end on that date: claude-sonnet-4's on 2026-06-15, and gpt-5.3-codex's and gpt-5.4-nano's on 2027-04-01. A documented price change starts a window: gpt-5.6-luna's on 2026-07-30 and gpt-5.6-sol's on 2026-08-21. Earlier prices that vendors don't publish are left out.",
    "mai-code-2, mai-code-2-flash, and mai-code-1.1-flash have no public API list price, so they have no apiEquivalent entry and are unpriced in that view. Local requests are too, because their baseline is mai-code-2, unless an overlay sets apiEquivalent.localBaselineModel."
  ],
  "githubAiCredits": {
```

Edit 3 of 3 in `src/CopilotLedger.Core/Pricing/rates.json`, replace:

```json
    "additionalHosts": [],
    "providerNames": ["ollama", "lm_studio", "foundry_local", "llama_cpp"]
  }
}
```

with:

```json
    "additionalHosts": [],
    "providerNames": ["ollama", "lm_studio", "foundry_local", "llama_cpp"]
  },
  "apiEquivalent": {
    "description": "Public API list prices of each model's vendor, in USD per 1,000,000 tokens, retrieved 2026-11-23. For comparison only; never billed.",
    "models": [
      {
        "id": "claude-opus-5.5",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": ["claude-opus-5-5"],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 4.00, "cachedInputPer1M": 0.20, "cacheWritePer1M": 5.00, "outputPer1M": 20.00 }
            ]
          }
        ]
      },
      {
        "id": "claude-sonnet-5.5",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": ["claude-sonnet-5-5"],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 2.50, "outputPer1M": 10.00 }
            ]
          }
        ]
      },
      {
        "id": "gpt-6.1-sol",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 272000, "inputPer1M": 2.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 2.50, "outputPer1M": 10.00 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 4.00, "cachedInputPer1M": 0.20, "cacheWritePer1M": 5.00, "outputPer1M": 15.00 }
            ]
          }
        ]
      },
      {
        "id": "gpt-6-astra",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 272000, "inputPer1M": 10.00, "cachedInputPer1M": 1.00, "cacheWritePer1M": 12.50, "outputPer1M": 50.00 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 20.00, "cachedInputPer1M": 2.00, "cacheWritePer1M": 25.00, "outputPer1M": 75.00 }
            ]
          }
        ]
      },
      {
        "id": "gpt-5.3-codex",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": "2027-04-01T00:00:00Z",
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 1.75, "cachedInputPer1M": 0.175, "cacheWritePer1M": null, "outputPer1M": 14.00 }
            ]
          }
        ]
      },
      {
        "id": "gpt-5.4-nano",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": ["gpt-5.4-nano-2026-03-17"],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": "2027-04-01T00:00:00Z",
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 0.20, "cachedInputPer1M": 0.02, "cacheWritePer1M": null, "outputPer1M": 1.25 }
            ]
          }
        ]
      },
      {
        "id": "gpt-5.6-luna",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": "2026-07-30T00:00:00Z",
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 272000, "inputPer1M": 0.20, "cachedInputPer1M": 0.02, "cacheWritePer1M": 0.25, "outputPer1M": 1.20 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 0.40, "cachedInputPer1M": 0.04, "cacheWritePer1M": 0.50, "outputPer1M": 1.80 }
            ]
          }
        ]
      },
      {
        "id": "gpt-5.6-sol",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": "2026-08-21T00:00:00Z",
            "effectiveUntil": "2026-12-01T00:00:00Z",
            "tiers": [
              { "name": "default", "maxInputTokens": 272000, "inputPer1M": 4.00, "cachedInputPer1M": 0.40, "cacheWritePer1M": 5.00, "outputPer1M": 20.00 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 8.00, "cachedInputPer1M": 0.80, "cacheWritePer1M": 10.00, "outputPer1M": 30.00 }
            ]
          }
        ]
      },
      {
        "id": "gpt-6-luna",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 272000, "inputPer1M": 0.10, "cachedInputPer1M": 0.01, "cacheWritePer1M": 0.125, "outputPer1M": 0.50 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 0.20, "cachedInputPer1M": 0.02, "cacheWritePer1M": 0.25, "outputPer1M": 0.75 }
            ]
          }
        ]
      },
      {
        "id": "gpt-6-sol",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 272000, "inputPer1M": 2.00, "cachedInputPer1M": 0.20, "cacheWritePer1M": 2.50, "outputPer1M": 10.00 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 4.00, "cachedInputPer1M": 0.40, "cacheWritePer1M": 5.00, "outputPer1M": 15.00 }
            ]
          }
        ]
      },
      {
        "id": "gpt-6.1-terra",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 272000, "inputPer1M": 1.00, "cachedInputPer1M": 0.05, "cacheWritePer1M": 1.25, "outputPer1M": 5.00 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 2.50, "outputPer1M": 7.50 }
            ]
          }
        ]
      },
      {
        "id": "gpt-6.2-astra",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 272000, "inputPer1M": 8.00, "cachedInputPer1M": 0.40, "cacheWritePer1M": 10.00, "outputPer1M": 40.00 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 16.00, "cachedInputPer1M": 0.80, "cacheWritePer1M": 20.00, "outputPer1M": 60.00 }
            ]
          }
        ]
      },
      {
        "id": "gpt-6.2-sol",
        "vendor": "openai",
        "sourceUrl": "https://developers.openai.com/api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 272000, "inputPer1M": 2.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 2.50, "outputPer1M": 10.00 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 4.00, "cachedInputPer1M": 0.20, "cacheWritePer1M": 5.00, "outputPer1M": 15.00 }
            ]
          }
        ]
      },
      {
        "id": "claude-haiku-4.5",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": ["claude-haiku-4-5", "claude-haiku-4-5-20251001"],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 1.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 1.25, "outputPer1M": 5.00 }
            ]
          }
        ]
      },
      {
        "id": "claude-haiku-5.5",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": ["claude-haiku-5-5"],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 100000, "inputPer1M": 0.10, "cachedInputPer1M": 0.01, "cacheWritePer1M": 0.125, "outputPer1M": 0.50 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 0.50, "cachedInputPer1M": 0.05, "cacheWritePer1M": 0.625, "outputPer1M": 2.50 }
            ]
          }
        ]
      },
      {
        "id": "claude-sonnet-4",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": ["claude-sonnet-4-20250514"],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": "2026-06-15T00:00:00Z",
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 3.00, "cachedInputPer1M": 0.30, "cacheWritePer1M": 3.75, "outputPer1M": 15.00 }
            ]
          }
        ]
      },
      {
        "id": "claude-sonnet-4.6",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": ["claude-sonnet-4-6"],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 3.00, "cachedInputPer1M": 0.30, "cacheWritePer1M": 3.75, "outputPer1M": 15.00 }
            ]
          }
        ]
      },
      {
        "id": "claude-opus-4.8",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": ["claude-opus-4-8"],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 5.00, "cachedInputPer1M": 0.50, "cacheWritePer1M": 6.25, "outputPer1M": 25.00 }
            ]
          }
        ]
      },
      {
        "id": "claude-sonnet-5",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": 0.20, "cacheWritePer1M": 2.50, "outputPer1M": 10.00 }
            ]
          }
        ]
      },
      {
        "id": "claude-fable-5",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 10.00, "cachedInputPer1M": 1.00, "cacheWritePer1M": 12.50, "outputPer1M": 50.00 }
            ]
          }
        ]
      },
      {
        "id": "claude-fable-5.1",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": ["claude-fable-5-1"],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 10.00, "cachedInputPer1M": 0.25, "cacheWritePer1M": 12.50, "outputPer1M": 50.00 }
            ]
          }
        ]
      },
      {
        "id": "claude-fable-5.2",
        "vendor": "anthropic",
        "sourceUrl": "https://platform.claude.com/docs/en/about-claude/pricing",
        "aliases": ["claude-fable-5-2"],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 8.00, "cachedInputPer1M": 0.20, "cacheWritePer1M": 10.00, "outputPer1M": 40.00 }
            ]
          }
        ]
      },
      {
        "id": "gemini-3.8-flash",
        "vendor": "google",
        "sourceUrl": "https://ai.google.dev/gemini-api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": "2027-01-01T00:00:00Z",
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 0.75, "cachedInputPer1M": 0.075, "cacheWritePer1M": null, "outputPer1M": 3.75 }
            ]
          },
          {
            "effectiveFrom": "2027-01-01T00:00:00Z",
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 1.50, "cachedInputPer1M": 0.15, "cacheWritePer1M": null, "outputPer1M": 7.50 }
            ]
          }
        ]
      },
      {
        "id": "gemini-4-argon",
        "vendor": "google",
        "sourceUrl": "https://ai.google.dev/gemini-api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": "2026-12-01T00:00:00Z",
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": null, "outputPer1M": 10.00 }
            ]
          },
          {
            "effectiveFrom": "2026-12-01T00:00:00Z",
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 4.00, "cachedInputPer1M": 0.20, "cacheWritePer1M": null, "outputPer1M": 20.00 }
            ]
          }
        ]
      },
      {
        "id": "gemini-4-carbon",
        "vendor": "google",
        "sourceUrl": "https://ai.google.dev/gemini-api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": "2026-12-01T00:00:00Z",
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": null, "outputPer1M": 10.00 }
            ]
          },
          {
            "effectiveFrom": "2026-12-01T00:00:00Z",
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 4.00, "cachedInputPer1M": 0.20, "cacheWritePer1M": null, "outputPer1M": 20.00 }
            ]
          }
        ]
      },
      {
        "id": "gemini-4-hydrogen",
        "vendor": "google",
        "sourceUrl": "https://ai.google.dev/gemini-api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 1.50, "cachedInputPer1M": 0.075, "cacheWritePer1M": null, "outputPer1M": 7.50 }
            ]
          }
        ]
      },
      {
        "id": "gemini-4-neon",
        "vendor": "google",
        "sourceUrl": "https://ai.google.dev/gemini-api/docs/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 3.00, "cachedInputPer1M": 0.15, "cacheWritePer1M": null, "outputPer1M": 15.00 }
            ]
          }
        ]
      },
      {
        "id": "grok-4.5",
        "vendor": "xai",
        "sourceUrl": "https://docs.x.ai/developers/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 199999, "inputPer1M": 2.00, "cachedInputPer1M": 0.30, "cacheWritePer1M": null, "outputPer1M": 6.00 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 4.00, "cachedInputPer1M": 0.60, "cacheWritePer1M": null, "outputPer1M": 12.00 }
            ]
          }
        ]
      },
      {
        "id": "grok-4.6",
        "vendor": "xai",
        "sourceUrl": "https://docs.x.ai/developers/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 199999, "inputPer1M": 2.00, "cachedInputPer1M": 0.50, "cacheWritePer1M": null, "outputPer1M": 6.00 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 4.00, "cachedInputPer1M": 1.00, "cacheWritePer1M": null, "outputPer1M": 12.00 }
            ]
          }
        ]
      },
      {
        "id": "grok-4.7",
        "vendor": "xai",
        "sourceUrl": "https://docs.x.ai/developers/pricing",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": 199999, "inputPer1M": 2.00, "cachedInputPer1M": 0.50, "cacheWritePer1M": null, "outputPer1M": 6.00 },
              { "name": "long_context", "maxInputTokens": null, "inputPer1M": 4.00, "cachedInputPer1M": 1.00, "cacheWritePer1M": null, "outputPer1M": 12.00 }
            ]
          }
        ]
      },
      {
        "id": "kimi-k3",
        "vendor": "moonshot",
        "sourceUrl": "https://platform.kimi.ai/docs/pricing/chat",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 3.00, "cachedInputPer1M": 0.30, "cacheWritePer1M": 3.00, "outputPer1M": 15.00 }
            ]
          }
        ]
      }
    ]
  }
}
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Cli/RatesCommandTests.cs`, replace:

```csharp
        Assert.Equal(0, missing.ExitCode);
        Assert.Equal(
            $"No rates overlay at {path}; using the built-in rates (2026-11-20).\n",
            missing.Out);
        await harness.RunAsync("rates", "init");
```

with:

```csharp
        Assert.Equal(0, missing.ExitCode);
        Assert.Equal(
            $"No rates overlay at {path}; using the built-in rates (2026-11-23).\n",
            missing.Out);
        await harness.RunAsync("rates", "init");
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Cli/RatesCommandTests.cs`, replace:

```csharp
        Assert.Equal(0, created.ExitCode);
        Assert.Equal(
            $"{path} is valid. Rates version: 2026-11-20+user.\n",
            created.Out);
    }
```

with:

```csharp
        Assert.Equal(0, created.ExitCode);
        Assert.Equal(
            $"{path} is valid. Rates version: 2026-11-23+user.\n",
            created.Out);
    }
```

Edit 1 of 5 in `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs`, replace:

```csharp
        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("Updated rates from 2026-11-20 to 2026-12-01.\n", run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(
```

with:

```csharp
        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("Updated rates from 2026-11-23 to 2026-12-01.\n", run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(
```

Edit 2 of 5 in `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs`, replace:

```csharp
    public static TheoryData<string, string> NotNewer => new()
    {
        { "2026-11-20", "Rates are up to date (2026-11-20).\n" },
        { "2026-10-01", "The published rates (2026-10-01) are older than the rates in use (2026-11-20); nothing changed.\n" },
    };

    [Theory]
```

with:

```csharp
    public static TheoryData<string, string> NotNewer => new()
    {
        { "2026-11-23", "Rates are up to date (2026-11-23).\n" },
        { "2026-10-01", "The published rates (2026-10-01) are older than the rates in use (2026-11-23); nothing changed.\n" },
    };

    [Theory]
```

Edit 3 of 5 in `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs`, replace:

```csharp
        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            warning + $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the built-in rates (2026-11-20).\n",
            validate.Out);
        Assert.Equal(0, status.ExitCode);
        using JsonDocument document = JsonDocument.Parse(status.Out);
        Assert.Equal("2026-11-20", document.RootElement.GetProperty("ratesVersion").GetString());
        Assert.Equal("built-in", document.RootElement.GetProperty("ratesSource").GetString());
        Assert.Equal(
```

with:

```csharp
        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            warning + $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the built-in rates (2026-11-23).\n",
            validate.Out);
        Assert.Equal(0, status.ExitCode);
        using JsonDocument document = JsonDocument.Parse(status.Out);
        Assert.Equal("2026-11-23", document.RootElement.GetProperty("ratesVersion").GetString());
        Assert.Equal("built-in", document.RootElement.GetProperty("ratesSource").GetString());
        Assert.Equal(
```

Edit 4 of 5 in `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs`, replace:

```csharp
            document.RootElement.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(0, update.ExitCode);
        Assert.Equal(warning + "Updated rates from 2026-11-20 to 2026-12-01.\n", update.Out);
    }

    [Fact]
```

with:

```csharp
            document.RootElement.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(0, update.ExitCode);
        Assert.Equal(warning + "Updated rates from 2026-11-23 to 2026-12-01.\n", update.Out);
    }

    [Fact]
```

Edit 5 of 5 in `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs`, replace:

```csharp
        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the built-in rates (2026-11-20).\n",
            validate.Out);
    }
```

with:

```csharp
        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the built-in rates (2026-11-23).\n",
            validate.Out);
    }
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
        Assert.True(root.GetProperty("copilotOtelEnabled").GetBoolean());
        Assert.Empty(root.GetProperty("warnings").EnumerateArray());
        Assert.Equal("2026-11-20", root.GetProperty("ratesVersion").GetString());
        JsonElement[] periods = root.GetProperty("periods").EnumerateArray().ToArray();
        Assert.Equal(3, periods.Length);
```

with:

```csharp
        Assert.True(root.GetProperty("copilotOtelEnabled").GetBoolean());
        Assert.Empty(root.GetProperty("warnings").EnumerateArray());
        Assert.Equal("2026-11-23", root.GetProperty("ratesVersion").GetString());
        JsonElement[] periods = root.GetProperty("periods").EnumerateArray().ToArray();
        Assert.Equal(3, periods.Length);
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
        Assert.Equal(
            StatusText(temp.Path).Replace(
                "Rates:   2026-11-20",
                $"Rates:   invalid overlay: {expectedError}",
                StringComparison.Ordinal),
```

with:

```csharp
        Assert.Equal(
            StatusText(temp.Path).Replace(
                "Rates:   2026-11-23",
                $"Rates:   invalid overlay: {expectedError}",
                StringComparison.Ordinal),
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
        Daemon:  not running; start it with: gh-ledger daemon start
        Ledger:  {Path.Combine(root, "data", "ledger.db")}
        Rates:   2026-11-20
        Copilot: OTel export not enabled; run: gh-ledger install

        Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
```

with:

```csharp
        Daemon:  not running; start it with: gh-ledger daemon start
        Ledger:  {Path.Combine(root, "data", "ledger.db")}
        Rates:   2026-11-23
        Copilot: OTel export not enabled; run: gh-ledger install

        Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
```

New file `tests/CopilotLedger.Tests/Pricing/BuiltInApiRatesTests.cs`:

```csharp
using System.Globalization;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class BuiltInApiRatesTests
{
    private static readonly JsonRateCardCalculator Embedded = JsonRateCardCalculator.LoadEmbedded();

    // model, start, input tokens, tier (null: no API list price for that date), then the expected UncachedInputUsd,
    // CacheReadUsd, CacheWriteUsd, and OutputUsd. Every case also has 10,000 cache-read, 5,000 cache-write, and 2,000
    // output tokens.
    public static TheoryData<string, string, long, string?, decimal, decimal, decimal, decimal> ListPrices => new()
    {
        { "claude-opus-5.5", "2026-11-23T12:00:00Z", 100_000, "default", 0.34m, 0.002m, 0.025m, 0.04m },
        { "claude-sonnet-5.5", "2026-11-23T12:00:00Z", 100_000, "default", 0.17m, 0.001m, 0.0125m, 0.02m },
        { "gpt-6.1-sol", "2026-11-23T12:00:00Z", 272_000, "default", 0.514m, 0.001m, 0.0125m, 0.02m },
        { "gpt-6.1-sol", "2026-11-23T12:00:00Z", 272_001, "long_context", 1.028004m, 0.002m, 0.025m, 0.03m },
        { "gpt-6-astra", "2026-11-23T12:00:00Z", 272_000, "default", 2.57m, 0.01m, 0.0625m, 0.1m },
        { "gpt-6-astra", "2026-11-23T12:00:00Z", 272_001, "long_context", 5.14002m, 0.02m, 0.125m, 0.15m },
        { "gpt-5.3-codex", "2026-11-23T12:00:00Z", 100_000, "default", 0.14875m, 0.00175m, 0.00875m, 0.028m },
        { "gpt-5.3-codex", "2027-03-31T23:59:59Z", 100_000, "default", 0.14875m, 0.00175m, 0.00875m, 0.028m },
        { "gpt-5.3-codex", "2027-04-01T00:00:00Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "gpt-5.4-nano", "2026-11-23T12:00:00Z", 100_000, "default", 0.017m, 0.0002m, 0.001m, 0.0025m },
        { "gpt-5.4-nano", "2027-04-01T00:00:00Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "gpt-5.6-luna", "2026-07-29T23:59:59Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "gpt-5.6-luna", "2026-07-30T00:00:00Z", 100_000, "default", 0.017m, 0.0002m, 0.00125m, 0.0024m },
        { "gpt-5.6-luna", "2026-11-23T12:00:00Z", 272_000, "default", 0.0514m, 0.0002m, 0.00125m, 0.0024m },
        { "gpt-5.6-luna", "2026-11-23T12:00:00Z", 272_001, "long_context", 0.1028004m, 0.0004m, 0.0025m, 0.0036m },
        { "gpt-5.6-sol", "2026-08-20T23:59:59Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "gpt-5.6-sol", "2026-08-21T00:00:00Z", 272_000, "default", 1.028m, 0.004m, 0.025m, 0.04m },
        { "gpt-5.6-sol", "2026-11-23T12:00:00Z", 272_001, "long_context", 2.056008m, 0.008m, 0.05m, 0.06m },
        { "gpt-5.6-sol", "2026-11-30T23:59:59.9999999Z", 100_000, "default", 0.34m, 0.004m, 0.025m, 0.04m },
        { "gpt-5.6-sol", "2026-12-01T00:00:00Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "gpt-6-luna", "2026-11-23T12:00:00Z", 272_000, "default", 0.0257m, 0.0001m, 0.000625m, 0.001m },
        { "gpt-6-luna", "2026-11-23T12:00:00Z", 272_001, "long_context", 0.0514002m, 0.0002m, 0.00125m, 0.0015m },
        { "gpt-6-sol", "2026-11-23T12:00:00Z", 272_000, "default", 0.514m, 0.002m, 0.0125m, 0.02m },
        { "gpt-6-sol", "2026-11-23T12:00:00Z", 272_001, "long_context", 1.028004m, 0.004m, 0.025m, 0.03m },
        { "gpt-6.1-terra", "2026-11-23T12:00:00Z", 272_000, "default", 0.257m, 0.0005m, 0.00625m, 0.01m },
        { "gpt-6.1-terra", "2026-11-23T12:00:00Z", 272_001, "long_context", 0.514002m, 0.001m, 0.0125m, 0.015m },
        { "gpt-6.2-astra", "2026-11-23T12:00:00Z", 272_000, "default", 2.056m, 0.004m, 0.05m, 0.08m },
        { "gpt-6.2-astra", "2026-11-23T12:00:00Z", 272_001, "long_context", 4.112016m, 0.008m, 0.1m, 0.12m },
        { "gpt-6.2-sol", "2026-11-23T12:00:00Z", 272_000, "default", 0.514m, 0.001m, 0.0125m, 0.02m },
        { "gpt-6.2-sol", "2026-11-23T12:00:00Z", 272_001, "long_context", 1.028004m, 0.002m, 0.025m, 0.03m },
        { "claude-haiku-4.5", "2026-11-23T12:00:00Z", 100_000, "default", 0.085m, 0.001m, 0.00625m, 0.01m },
        { "claude-haiku-5.5", "2026-11-23T12:00:00Z", 100_000, "default", 0.0085m, 0.0001m, 0.000625m, 0.001m },
        { "claude-haiku-5.5", "2026-11-23T12:00:00Z", 100_001, "long_context", 0.0425005m, 0.0005m, 0.003125m, 0.005m },
        { "claude-sonnet-4", "2026-06-14T23:59:59Z", 100_000, "default", 0.255m, 0.003m, 0.01875m, 0.03m },
        { "claude-sonnet-4", "2026-06-15T00:00:00Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "claude-sonnet-4.6", "2026-11-23T12:00:00Z", 100_000, "default", 0.255m, 0.003m, 0.01875m, 0.03m },
        { "claude-opus-4.8", "2026-11-23T12:00:00Z", 100_000, "default", 0.425m, 0.005m, 0.03125m, 0.05m },
        { "claude-sonnet-5", "2026-11-23T12:00:00Z", 100_000, "default", 0.17m, 0.002m, 0.0125m, 0.02m },
        { "claude-fable-5", "2026-11-23T12:00:00Z", 100_000, "default", 0.85m, 0.01m, 0.0625m, 0.1m },
        { "claude-fable-5.1", "2026-11-23T12:00:00Z", 100_000, "default", 0.85m, 0.0025m, 0.0625m, 0.1m },
        { "claude-fable-5.2", "2026-11-23T12:00:00Z", 100_000, "default", 0.68m, 0.002m, 0.05m, 0.08m },
        { "gemini-3.8-flash", "2026-12-31T23:59:59.9999999Z", 100_000, "default", 0.06375m, 0.00075m, 0.00375m, 0.0075m },
        { "gemini-3.8-flash", "2027-01-01T00:00:00Z", 100_000, "default", 0.1275m, 0.0015m, 0.0075m, 0.015m },
        { "gemini-4-argon", "2026-11-30T23:59:59.9999999Z", 100_000, "default", 0.17m, 0.001m, 0.01m, 0.02m },
        { "gemini-4-argon", "2026-12-01T00:00:00Z", 100_000, "default", 0.34m, 0.002m, 0.02m, 0.04m },
        { "gemini-4-carbon", "2026-11-30T23:59:59.9999999Z", 100_000, "default", 0.17m, 0.001m, 0.01m, 0.02m },
        { "gemini-4-carbon", "2026-12-01T00:00:00Z", 100_000, "default", 0.34m, 0.002m, 0.02m, 0.04m },
        { "gemini-4-hydrogen", "2026-11-23T12:00:00Z", 100_000, "default", 0.1275m, 0.00075m, 0.0075m, 0.015m },
        { "gemini-4-neon", "2026-11-23T12:00:00Z", 100_000, "default", 0.255m, 0.0015m, 0.015m, 0.03m },
        { "grok-4.5", "2026-11-23T12:00:00Z", 199_999, "default", 0.369998m, 0.003m, 0.01m, 0.012m },
        { "grok-4.5", "2026-11-23T12:00:00Z", 200_000, "long_context", 0.74m, 0.006m, 0.02m, 0.024m },
        { "grok-4.6", "2026-11-23T12:00:00Z", 199_999, "default", 0.369998m, 0.005m, 0.01m, 0.012m },
        { "grok-4.6", "2026-11-23T12:00:00Z", 200_000, "long_context", 0.74m, 0.01m, 0.02m, 0.024m },
        { "grok-4.7", "2026-11-23T12:00:00Z", 199_999, "default", 0.369998m, 0.005m, 0.01m, 0.012m },
        { "grok-4.7", "2026-11-23T12:00:00Z", 200_000, "long_context", 0.74m, 0.01m, 0.02m, 0.024m },
        { "kimi-k3", "2026-11-23T12:00:00Z", 100_000, "default", 0.255m, 0.003m, 0.015m, 0.03m },
    };

    public static TheoryData<string, string, string> Aliases => new()
    {
        { "claude-opus-4-8", "claude-opus-4.8", "2026-11-23T12:00:00Z" },
        { "claude-opus-5-5", "claude-opus-5.5", "2026-11-23T12:00:00Z" },
        { "claude-haiku-4-5", "claude-haiku-4.5", "2026-11-23T12:00:00Z" },
        { "claude-haiku-4-5-20251001", "claude-haiku-4.5", "2026-11-23T12:00:00Z" },
        { "claude-sonnet-4-20250514", "claude-sonnet-4", "2026-06-01T00:00:00Z" },
        { "gpt-5.4-nano-2026-03-17", "gpt-5.4-nano", "2026-11-23T12:00:00Z" },
    };

    [Theory]
    [MemberData(nameof(ListPrices))]
    public void AR1_BuiltInListPricesMatchTheVendorsPages(
        string model,
        string start,
        long input,
        string? tier,
        decimal uncachedInputUsd,
        decimal cacheReadUsd,
        decimal cacheWriteUsd,
        decimal outputUsd)
    {
        ApiEquivalentCost cost = Embedded.Calculate(TestSpans.Chat(
            startTime: DateTimeOffset.Parse(start, CultureInfo.InvariantCulture),
            requestModel: model,
            inputTokens: input,
            cacheReadInputTokens: 10_000,
            cacheWriteInputTokens: 5_000,
            outputTokens: 2_000)).ApiEquivalent;

        if (tier is null)
        {
            Assert.Equal(ApiEquivalentStatus.Unpriced, cost.Status);
            Assert.Null(cost.UsdCost);
            return;
        }

        Assert.Equal(ApiEquivalentStatus.Priced, cost.Status);
        Assert.Equal(model, cost.RateCardModelId);
        Assert.Equal(tier, cost.TierName);
        Assert.NotNull(cost.Breakdown);
        Assert.Equal(uncachedInputUsd, cost.Breakdown.UncachedInputUsd);
        Assert.Equal(cacheReadUsd, cost.Breakdown.CacheReadUsd);
        Assert.Equal(cacheWriteUsd, cost.Breakdown.CacheWriteUsd);
        Assert.Equal(outputUsd, cost.Breakdown.OutputUsd);
    }

    [Theory]
    [InlineData("mai-code-2", null)]
    [InlineData("mai-code-2-flash", null)]
    [InlineData("mai-code-1.1-flash", null)]
    [InlineData("qwen3.8-27b-cuda-gpu:1", "127.0.0.1")]
    public void AR2_ModelsWithoutAListPriceAreUnpriced(string model, string? host)
    {
        CreditCalculationResult result = Embedded.Calculate(TestSpans.Chat(
            serverAddress: host,
            requestModel: model,
            inputTokens: 400_000,
            outputTokens: 12_000));

        Assert.Equal(host is null ? PricingStatus.Priced : PricingStatus.LocalOffload, result.Status);
        Assert.Equal(ApiEquivalentStatus.Unpriced, result.ApiEquivalent.Status);
        Assert.Null(result.ApiEquivalent.UsdCost);
        Assert.Equal(host is null ? 0m : 0.23m, result.EstimatedCloudSavedUsd);
    }

    [Fact]
    public void AR3_EveryGitHubModelWithAListPriceHasOneFromItsVendor()
    {
        JsonNode card = JsonNode.Parse(PublishedRates.Embedded())!;
        string[] github = card["githubAiCredits"]!["models"]!.AsArray()
            .Select(model => model!["id"]!.GetValue<string>())
            .ToArray();
        JsonNode[] retail = card["apiEquivalent"]!["models"]!.AsArray().Select(model => model!).ToArray();
        Dictionary<string, string> pages = new(StringComparer.Ordinal)
        {
            ["anthropic"] = "https://platform.claude.com/docs/en/about-claude/pricing",
            ["openai"] = "https://developers.openai.com/api/docs/pricing",
            ["google"] = "https://ai.google.dev/gemini-api/docs/pricing",
            ["xai"] = "https://docs.x.ai/developers/pricing",
            ["moonshot"] = "https://platform.kimi.ai/docs/pricing/chat",
        };

        Assert.Equal(2, card["schemaVersion"]!.GetValue<int>());
        Assert.Equal(31, retail.Length);
        Assert.Equal(
            github.Where(id => !id.StartsWith("mai-", StringComparison.Ordinal)),
            retail.Select(model => model["id"]!.GetValue<string>()));
        Assert.All(retail, model =>
            Assert.Equal(pages[model["vendor"]!.GetValue<string>()], model["sourceUrl"]!.GetValue<string>()));
        Assert.Equal(
            ["mai-code-2", "mai-code-1.1-flash", "mai-code-2-flash"],
            github.Where(id => id.StartsWith("mai-", StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(Aliases))]
    public void AR4_VendorIdsAndSnapshotsAreAliases(string alias, string model, string start)
    {
        CreditCalculationResult result = Embedded.Calculate(TestSpans.Chat(
            startTime: DateTimeOffset.Parse(start, CultureInfo.InvariantCulture),
            requestModel: alias,
            inputTokens: 1_000,
            outputTokens: 100));

        Assert.Equal(PricingStatus.UnknownModel, result.Status);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        Assert.Equal(model, result.ApiEquivalent.RateCardModelId);
    }

    [Fact]
    public void AR5_TheBuiltInCardIsPinned()
    {
        Assert.Equal("2026-11-23", Embedded.RatesVersion);
        Assert.Equal(2, Embedded.SchemaVersion);
        Assert.Equal("634d783f8123f6aa8555220e830f7016b883083af08a043f29ca975a6b725045", Embedded.ApiRateCardSha256);
    }

    // Where the vendor's list price differs from GitHub's rate: Grok 4.5's cached input, and GPT-5.6 Luna's long-context
    // threshold (OpenAI: above 272,000 input tokens; GitHub: above 200,000).
    [Theory]
    [InlineData("grok-4.5", 150_000, 100_000)]
    [InlineData("gpt-5.6-luna", 200_001, 0)]
    public void AR6_ListPricesCanDifferFromGitHubsRates(string model, long input, long cacheRead)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: model,
            inputTokens: input,
            cacheReadInputTokens: cacheRead,
            outputTokens: 1_000);

        CreditCalculationResult result = Embedded.Calculate(span);

        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        if (model == "grok-4.5")
        {
            Assert.Equal("default", result.TierName);
            Assert.Equal(0.05m, result.Breakdown!.CacheReadUsd);
            Assert.Equal(0.03m, result.ApiEquivalent.Breakdown!.CacheReadUsd);
            Assert.Equal(0.156m, result.UsdCost);
            Assert.Equal(0.136m, result.ApiEquivalent.UsdCost);
        }
        else
        {
            Assert.Equal("long_context", result.TierName);
            Assert.Equal("default", result.ApiEquivalent.TierName);
            Assert.Equal(0.0818004m, result.UsdCost);
            Assert.Equal(0.0412002m, result.ApiEquivalent.UsdCost);
        }
    }
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Pricing/JsonRateCardCalculatorTests.cs`, replace:

```csharp
    public void LoadEmbedded_Succeeds()
    {
        Assert.Equal("2026-11-20", JsonRateCardCalculator.LoadEmbedded().RatesVersion);
    }

    [Theory]
```

with:

```csharp
    public void LoadEmbedded_Succeeds()
    {
        Assert.Equal("2026-11-23", JsonRateCardCalculator.LoadEmbedded().RatesVersion);
    }

    [Theory]
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Pricing/RateOverlayTests.cs`, replace:

```csharp
        Assert.Equal(0.315m, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
        Assert.Equal("2026-11-20+contoso-2026-11", result.RatesVersion);
    }

    [Fact]
```

with:

```csharp
        Assert.Equal(0.315m, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
        Assert.Equal("2026-11-23+contoso-2026-11", result.RatesVersion);
    }

    [Fact]
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Pricing/RateOverlayTests.cs`, replace:

```csharp
        Assert.Equal(0.92m, result.EstimatedCloudSavedUsd);
        Assert.Equal("claude-sonnet-5.5", result.RateCardModelId);
        Assert.Equal("2026-11-20+user", result.RatesVersion);
    }

    [Fact]
```

with:

```csharp
        Assert.Equal(0.92m, result.EstimatedCloudSavedUsd);
        Assert.Equal("claude-sonnet-5.5", result.RateCardModelId);
        Assert.Equal("2026-11-23+user", result.RatesVersion);
    }

    [Fact]
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Pricing/RateOverlayTests.cs`, replace:

```csharp
            WriteOverlay(temp, """{ "schemaVersion": 1, "ratesVersion": " custom " }"""));

        Assert.Equal("2026-11-20+ custom ", calculator.RatesVersion);
    }

    private static string WriteOverlay(TempDirectory temp, string overlay)
```

with:

```csharp
            WriteOverlay(temp, """{ "schemaVersion": 1, "ratesVersion": " custom " }"""));

        Assert.Equal("2026-11-23+ custom ", calculator.RatesVersion);
    }

    private static string WriteOverlay(TempDirectory temp, string overlay)
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Pricing/RatesRefreshTests.cs`, replace:

```csharp
    public void RR4_RatesVersion_IsUnchanged() =>
        Assert.Equal(
            "2026-11-20",
            JsonRateCardCalculator.LoadEmbedded().RatesVersion);
}
```

with:

```csharp
    public void RR4_RatesVersion_IsUnchanged() =>
        Assert.Equal(
            "2026-11-23",
            JsonRateCardCalculator.LoadEmbedded().RatesVersion);
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/TestSupport/PublishedRates.cs`, replace:

```csharp
    }

    // The embedded rates with "ratesVersion": "2026-11-20" replaced by ratesVersion.
    public static string WithVersion(string ratesVersion) =>
        Embedded().Replace(
            "\"ratesVersion\": \"2026-11-20\"",
            $"\"ratesVersion\": \"{ratesVersion}\"",
            StringComparison.Ordinal);
```

with:

```csharp
    }

    // The embedded rates with "ratesVersion": "2026-11-23" replaced by ratesVersion.
    public static string WithVersion(string ratesVersion) =>
        Embedded().Replace(
            "\"ratesVersion\": \"2026-11-23\"",
            $"\"ratesVersion\": \"{ratesVersion}\"",
            StringComparison.Ordinal);
```

Edit 1 of 2 in `docs/configuration.md`, replace:

```markdown
- Cache reads and writes are priced at the list prices for them, as billing is. A request that reported no cache counters is priced as if all of its input were uncached, so its API-equivalent cost is an estimate, and reports mark it as one.

## Why Foundry BYOK is unpriced by default

With bring-your-own-key (BYOK), Microsoft Foundry bills you directly, not GitHub. Azure prices depend on region, deployment type, and your agreement, so CopilotLedger can't know yours.
```

with:

```markdown
- Cache reads and writes are priced at the list prices for them, as billing is. A request that reported no cache counters is priced as if all of its input were uncached, so its API-equivalent cost is an estimate, and reports mark it as one.

The built-in API list prices are each vendor's public prices on 2026-11-23 for the 31 GitHub models that have them, for standard processing on the vendor's global endpoint: not batch, flex, priority, fast mode, or regional endpoints. Each model's `sourceUrl` is its vendor's price page. Where a list price differs from GitHub's rate, as Grok 4.5's cached input and GPT-5.6 Luna's long-context threshold do, the list price is used. Where the vendors' pages leave a choice open:

- MAI Code 2, MAI Code 2 Flash, and MAI Code 1.1 Flash have no public API list price, so their requests are unpriced in this view. So are local requests, because their baseline is MAI Code 2; to price them, set `apiEquivalent.localBaselineModel` in your overlay.
- Cache writes are priced for the vendor's default cache lifetime: 5 minutes at Anthropic and Moonshot, and 30 minutes at OpenAI. Longer lifetimes cost more. Google and xAI list no cache-write price, so cache writes are priced as input.
- Prices start and end at 00:00 UTC. A promotion ends the day after its last day. Google publishes the prices that follow its promotions; OpenAI publishes none after GPT-5.6 Sol's, which ends on 2026-12-01. A retired or shut-down model has no list price from that date: Claude Sonnet 4 from 2026-06-15, and GPT-5.3-Codex and GPT-5.4 nano from 2027-04-01. Vendors publish only current prices, so they're assumed for earlier dates, except after a documented change: GPT-5.6 Luna's prices start on 2026-07-30, and GPT-5.6 Sol's on 2026-08-21.
- Vendor API ids, such as `claude-opus-4-8`, and dated snapshot ids, such as `claude-haiku-4-5-20251001`, are aliases.

## Why Foundry BYOK is unpriced by default

With bring-your-own-key (BYOK), Microsoft Foundry bills you directly, not GitHub. Azure prices depend on region, deployment type, and your agreement, so CopilotLedger can't know yours.
```

Edit 2 of 2 in `docs/configuration.md`, replace:

```markdown
- Host and provider lists are added to the built-in lists.
- `localOffload.baselineModel` and `apiEquivalent.localBaselineModel` replace the built-in values.
- `ratesVersion` is an optional label. Each request in the ledger records which rates priced it, for example `2026-11-20+contoso-2026-11`, or `2026-11-20+user` if you leave the label out.
- JSON doesn't allow comments, so put your own notes in the optional `notes` array.

If the file is invalid, CopilotLedger refuses to load it and reports the first problem it finds. It never silently ignores a broken file. Each error starts with the file's path, then says where the problem is and what's wrong:
```

with:

```markdown
- Host and provider lists are added to the built-in lists.
- `localOffload.baselineModel` and `apiEquivalent.localBaselineModel` replace the built-in values.
- `ratesVersion` is an optional label. Each request in the ledger records which rates priced it, for example `2026-11-23+contoso-2026-11`, or `2026-11-23+user` if you leave the label out.
- JSON doesn't allow comments, so put your own notes in the optional `notes` array.

If the file is invalid, CopilotLedger refuses to load it and reports the first problem it finds. It never silently ignores a broken file. Each error starts with the file's path, then says where the problem is and what's wrong:
```

Edit 1 of 4 in `docs/guide.md`, replace:

```markdown
Daemon:  running (pid 4242) on http://localhost:4318 since 2026-11-21 16:20
Ledger:  C:\Users\you\.copilot-ledger\ledger.db
Rates:   2026-11-20
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
```

with:

```markdown
Daemon:  running (pid 4242) on http://localhost:4318 since 2026-11-21 16:20
Ledger:  C:\Users\you\.copilot-ledger\ledger.db
Rates:   2026-11-23
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
```

Edit 2 of 4 in `docs/guide.md`, replace:

```markdown
Daemon:  running (pid 4242) on http://localhost:4318 since 2026-11-21 08:02
Ledger:  C:\Users\you\.copilot-ledger\ledger.db
Rates:   2026-11-20+contoso-2026-11
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
```

with:

```markdown
Daemon:  running (pid 4242) on http://localhost:4318 since 2026-11-21 08:02
Ledger:  C:\Users\you\.copilot-ledger\ledger.db
Rates:   2026-11-23+contoso-2026-11
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
```

Edit 3 of 4 in `docs/guide.md`, replace:

````markdown
```

```text
C:\Users\you\.copilot-ledger\rates.json is valid. Rates version: 2026-11-20+contoso-2026-11.
```
<!-- /sample -->
````

with:

````markdown
```

```text
C:\Users\you\.copilot-ledger\rates.json is valid. Rates version: 2026-11-23+contoso-2026-11.
```
<!-- /sample -->
````

Edit 4 of 4 in `docs/guide.md`, replace:

````markdown
```

```text
Updated rates from 2026-11-20 to 2026-12-15.
Restart the daemon to price new spans with them: gh ledger daemon stop, then gh ledger daemon start.
```
````

with:

````markdown
```

```text
Updated rates from 2026-11-23 to 2026-12-15.
Restart the daemon to price new spans with them: gh ledger daemon stop, then gh ledger daemon start.
```
````

### Tests

- **AR1** (56 `TheoryData<string, string, long, string?, decimal, decimal, decimal, decimal>` cases): every built-in model, both sides of every tier bound, and both sides of the window bounds, with expected amounts computed from a transcription of the vendors' pages made separately from `rates.json`, each with 10,000 cache-read, 5,000 cache-write, and 2,000 output tokens; cases with a `null` tier check that dates outside the windows (after GPT-5.6 Sol's promotion, after a shutdown or retirement, before a documented change) are `Unpriced`.
- **AR2** (4): the three MAI models and a local request are `Unpriced`, with their billing and savings unchanged. **AR3**: 31 models, each with its vendor's page as `sourceUrl`, and every GitHub model except the MAI ones has one. **AR4** (6): vendor ids and snapshots resolve as aliases. **AR5**: the built-in version, schema, and hash `634d783f…`. **AR6** (2): Grok 4.5's cached input and GPT-5.6 Luna's threshold differ from GitHub's.

### Acceptance criteria

- The built-in card has the 31 verified list prices and no price for the MAI models (AR1–AR3); the credit lens doesn't change.
- `dotnet test CopilotLedger.slnx` passes **934** tests (864 + 70).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 4 specification: Version 2 Rates Downloads

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Rates/RatesUpdate.cs` | `DownloadUrl` is `rates.v2.json`; `LegacyDownloadUrl` `rates.json` on 404; refuses a later lower-schema card |
| `src/CopilotLedger.Cli/Rates/RateCardFiles.cs` | `LoadBase` ignores a later download with a lower `schemaVersion`, with a warning |
| `.github/workflows/release.yml` | The `rates` check reads `rates.v2.json` or `rates.json`; the release publishes both, with checksums |
| `tests/CopilotLedger.Tests/TestSupport/PublishedRates.cs` | `Url` (v2), `LegacyUrl`, and `SchemaVersion1` |
| `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs` | RU3's 404 case names `LegacyUrl`; new RU11–RU14 |
| `docs/configuration.md`, `docs/cli.md`, `docs/releasing.md` | The two assets and the rules |

### Algorithm (`rates update`)

1. GET `DownloadUrl`. On HTTP 404, dispose the response and GET `LegacyDownloadUrl`. Errors name the URL tried last.
2. Parse, check the `ratesVersion` date, and compare it with the rates in use, as before: older or the same changes nothing (exit 0).
3. A later card whose `schemaVersion` is lower than the built-in card's: `The published rates (<v>) are schemaVersion <n>, without API-equivalent prices; this gh-ledger needs schemaVersion <m>. Nothing changed.` on standard error, exit 1.
4. Otherwise merge with the overlay and save, as before.

`LoadBase` keeps its rules, and adds one: a valid, later download whose `schemaVersion` is lower than the built-in card's is ignored with the warning `ignored <path>: its schemaVersion <n> rates have no API-equivalent prices; run <command> rates update to replace them.`, which `status`, `rates validate`, `rates update`, and the daemon show like the other `LoadBase` warnings.

### Code (copy exactly)

Edit 1 of 5 in `src/CopilotLedger.Cli/Rates/RateCardFiles.cs`, replace:

```csharp
using System.Globalization;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Rates;
```

with:

```csharp
using System.Globalization;
using CopilotLedger.Cli.Install;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Rates;
```

Edit 2 of 5 in `src/CopilotLedger.Cli/Rates/RateCardFiles.cs`, replace:

```csharp
        Path.Combine(context.DataDirectory, "rates.downloaded.json");

    // The downloaded rates when rates.downloaded.json is valid and its ratesVersion date is later than the built-in one;
    // otherwise the built-in rates. A downloaded file that isn't later is ignored without a warning.
    public static RatesBase LoadBase(CliContext context)
    {
        string builtIn = JsonRateCardCalculator.LoadEmbedded().RatesVersion;
        string path = DownloadedPath(context);
        if (!File.Exists(path))
```

with:

```csharp
        Path.Combine(context.DataDirectory, "rates.downloaded.json");

    // The downloaded rates when rates.downloaded.json is valid, its ratesVersion date is later than the built-in one, and its
    // schemaVersion is at least the built-in one's; otherwise the built-in rates. A download that isn't later is ignored
    // without a warning, and a later download with an older schemaVersion is ignored with one, so a schemaVersion 1 file
    // never replaces built-in API-equivalent prices.
    public static RatesBase LoadBase(CliContext context)
    {
        JsonRateCardCalculator builtInRates = JsonRateCardCalculator.LoadEmbedded();
        string builtIn = builtInRates.RatesVersion;
        string path = DownloadedPath(context);
        if (!File.Exists(path))
```

Edit 3 of 5 in `src/CopilotLedger.Cli/Rates/RateCardFiles.cs`, replace:

```csharp
        }

        string json;
        string version;
        try
        {
            json = File.ReadAllText(path);
            version = JsonRateCardCalculator.Parse(json).RatesVersion;
        }
        catch (Exception ex) when (
```

with:

```csharp
        }

        string json;
        JsonRateCardCalculator downloadedRates;
        try
        {
            json = File.ReadAllText(path);
            downloadedRates = JsonRateCardCalculator.Parse(json);
        }
        catch (Exception ex) when (
```

Edit 4 of 5 in `src/CopilotLedger.Cli/Rates/RateCardFiles.cs`, replace:

```csharp
        }

        if (!TryParseVersion(version, out DateOnly downloaded))
        {
```

with:

```csharp
        }

        string version = downloadedRates.RatesVersion;
        if (!TryParseVersion(version, out DateOnly downloaded))
        {
```

Edit 5 of 5 in `src/CopilotLedger.Cli/Rates/RateCardFiles.cs`, replace:

```csharp
        }

        return downloaded > ParseVersion(builtIn)
            ? new RatesBase(json, version, null)
            : new RatesBase(null, builtIn, null);
    }

    // The base rates with the overlay at overlayPath merged on top; null overlayPath means no overlay.
```

with:

```csharp
        }

        if (downloaded <= ParseVersion(builtIn))
        {
            return new RatesBase(null, builtIn, null);
        }

        return downloadedRates.SchemaVersion < builtInRates.SchemaVersion
            ? new RatesBase(
                null,
                builtIn,
                $"ignored {path}: its schemaVersion {downloadedRates.SchemaVersion} rates have no API-equivalent prices; run {GhExtension.CommandName(context.Platform)} rates update to replace them.")
            : new RatesBase(json, version, null);
    }

    // The base rates with the overlay at overlayPath merged on top; null overlayPath means no overlay.
```

Edit 1 of 9 in `src/CopilotLedger.Cli/Rates/RatesUpdate.cs`, replace:

```csharp
{
    public const string DownloadUrl =
        "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json";

    private const int MaximumBytes = 1_048_576;
```

with:

```csharp
{
    public const string DownloadUrl =
        "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.v2.json";

    // Releases before rates.v2.json attach only this schemaVersion 1 copy, which RunAsync takes only when it's older or
    // the same: a newer one would drop the API-equivalent prices.
    public const string LegacyDownloadUrl =
        "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json";

    private const int MaximumBytes = 1_048_576;
```

Edit 2 of 9 in `src/CopilotLedger.Cli/Rates/RatesUpdate.cs`, replace:

```csharp
    {
        byte[] bytes;
        using (HttpClient client = new(
            context.CreateHttpHandler?.Invoke() ?? CreateDefaultHandler(),
```

with:

```csharp
    {
        byte[] bytes;
        string url = DownloadUrl;
        using (HttpClient client = new(
            context.CreateHttpHandler?.Invoke() ?? CreateDefaultHandler(),
```

Edit 3 of 9 in `src/CopilotLedger.Cli/Rates/RatesUpdate.cs`, replace:

```csharp
            try
            {
                using HttpResponseMessage response = await client.GetAsync(
                    DownloadUrl,
                    cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    context.Error.WriteLine(
                        $"Could not download rates from {DownloadUrl}: HTTP {(int)response.StatusCode}.");
                    return 1;
                }

                bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                context.Error.WriteLine($"Could not download rates from {DownloadUrl}: {ex.Message}");
                return 1;
            }
```

with:

```csharp
            try
            {
                HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // The latest release predates rates.v2.json.
                    response.Dispose();
                    url = LegacyDownloadUrl;
                    response = await client.GetAsync(url, cancellationToken);
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        context.Error.WriteLine(
                            $"Could not download rates from {url}: HTTP {(int)response.StatusCode}.");
                        return 1;
                    }

                    bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                context.Error.WriteLine($"Could not download rates from {url}: {ex.Message}");
                return 1;
            }
```

Edit 4 of 9 in `src/CopilotLedger.Cli/Rates/RatesUpdate.cs`, replace:

```csharp
            {
                context.Error.WriteLine(
                    $"Could not download rates from {DownloadUrl}: no response within 30 seconds.");
                return 1;
            }
```

with:

```csharp
            {
                context.Error.WriteLine(
                    $"Could not download rates from {url}: no response within 30 seconds.");
                return 1;
            }
```

Edit 5 of 9 in `src/CopilotLedger.Cli/Rates/RatesUpdate.cs`, replace:

```csharp
        }

        string version;
        try
        {
            version = JsonRateCardCalculator.Parse(json).RatesVersion;
        }
        catch (InvalidDataException ex)
```

with:

```csharp
        }

        JsonRateCardCalculator published;
        try
        {
            published = JsonRateCardCalculator.Parse(json);
        }
        catch (InvalidDataException ex)
```

Edit 6 of 9 in `src/CopilotLedger.Cli/Rates/RatesUpdate.cs`, replace:

```csharp
        }

        if (!RateCardFiles.TryParseVersion(version, out DateOnly published))
        {
            context.Error.WriteLine(
```

with:

```csharp
        }

        string version = published.RatesVersion;
        if (!RateCardFiles.TryParseVersion(version, out DateOnly publishedOn))
        {
            context.Error.WriteLine(
```

Edit 7 of 9 in `src/CopilotLedger.Cli/Rates/RatesUpdate.cs`, replace:

```csharp
        }

        DateOnly inUse = RateCardFiles.ParseVersion(current.Version);
        if (published < inUse)
        {
            context.Out.WriteLine(
```

with:

```csharp
        }

        DateOnly inUse = RateCardFiles.ParseVersion(current.Version);
        if (publishedOn < inUse)
        {
            context.Out.WriteLine(
```

Edit 8 of 9 in `src/CopilotLedger.Cli/Rates/RatesUpdate.cs`, replace:

```csharp
        }

        if (published == inUse)
        {
            context.Out.WriteLine($"Rates are up to date ({current.Version}).");
```

with:

```csharp
        }

        if (publishedOn == inUse)
        {
            context.Out.WriteLine($"Rates are up to date ({current.Version}).");
```

Edit 9 of 9 in `src/CopilotLedger.Cli/Rates/RatesUpdate.cs`, replace:

```csharp
        }

        string? overlay = RateCardFiles.OverlayPath(context);
        if (overlay is not null)
```

with:

```csharp
        }

        int schemaVersion = JsonRateCardCalculator.LoadEmbedded().SchemaVersion;
        if (published.SchemaVersion < schemaVersion)
        {
            context.Error.WriteLine(
                $"The published rates ({version}) are schemaVersion {published.SchemaVersion}, without API-equivalent prices; this gh-ledger needs schemaVersion {schemaVersion}. Nothing changed.");
            return 1;
        }

        string? overlay = RateCardFiles.OverlayPath(context);
        if (overlay is not null)
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs`, replace:

```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;
```

with:

```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Cli;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs`, replace:

```csharp
    {
        { "offline", $"Could not download rates from {PublishedRates.Url}: No such host is known. (github.com:443)\n" },
        { "404", $"Could not download rates from {PublishedRates.Url}: HTTP 404.\n" },
        { "timeout", $"Could not download rates from {PublishedRates.Url}: no response within 30 seconds.\n" },
        { "not-utf8", "The downloaded rates are invalid: the file isn't UTF-8.\n" },
```

with:

```csharp
    {
        { "offline", $"Could not download rates from {PublishedRates.Url}: No such host is known. (github.com:443)\n" },
        { "404", $"Could not download rates from {PublishedRates.LegacyUrl}: HTTP 404.\n" },
        { "timeout", $"Could not download rates from {PublishedRates.Url}: no response within 30 seconds.\n" },
        { "not-utf8", "The downloaded rates are invalid: the file isn't UTF-8.\n" },
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs`, replace:

```csharp
            out _));
    }
}
```

with:

```csharp
            out _));
    }

    public static TheoryData<string, int, string, string> LegacyReleases => new()
    {
        { "2026-11-20", 0, "The published rates (2026-11-20) are older than the rates in use (2026-11-25); nothing changed.\n", "" },
        { "2026-12-01", 1, "", "The published rates (2026-12-01) are schemaVersion 1, without API-equivalent prices; this gh-ledger needs schemaVersion 2. Nothing changed.\n" },
    };

    // A release before rates.v2.json attaches only schemaVersion 1 rates.json, which may not replace the rates in use.
    [Theory]
    [MemberData(nameof(LegacyReleases))]
    public async Task RU11_AReleaseWithoutVersion2RatesFallsBackToItsRatesJson(
        string version,
        int exitCode,
        string output,
        string error)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.WithVersion("2026-11-25"));
        byte[] before = await FileRetry.ReadAllBytesAsync(downloaded);
        FakeHttpHandler handler = new(request => request.RequestUri!.AbsoluteUri == PublishedRates.Url
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.SchemaVersion1(version))));

        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(exitCode, run.ExitCode);
        Assert.Equal(output, run.Out);
        Assert.Equal(error, run.Error);
        Assert.Equal(
            [
                new HttpCall("GET", PublishedRates.Url, $"gh-ledger/{GhLedgerCli.Version}"),
                new HttpCall("GET", PublishedRates.LegacyUrl, $"gh-ledger/{GhLedgerCli.Version}"),
            ],
            handler.Calls);
        Assert.Equal(before, await FileRetry.ReadAllBytesAsync(downloaded));
    }

    // gh-ledger 0.4.1 rejects schemaVersion 2 the same way: "The downloaded rates are invalid: schemaVersion: must be 1".
    [Fact]
    public async Task RU12_RatesWithAnUnsupportedSchemaVersionFailClosed()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.WithVersion("2026-11-25"));
        byte[] before = await FileRetry.ReadAllBytesAsync(downloaded);
        string future = PublishedRates.WithVersion("2026-12-01").Replace(
            "\"schemaVersion\": 2,",
            "\"schemaVersion\": 3,",
            StringComparison.Ordinal);

        CliRun run = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(future))),
            "rates",
            "update");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal("The downloaded rates are invalid: schemaVersion: must be 1 or 2\n", run.Error);
        Assert.Equal(before, await FileRetry.ReadAllBytesAsync(downloaded));
    }

    [Fact]
    public async Task RU13_LaterSchemaVersion1DownloadsAreIgnoredWithAWarning()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.SchemaVersion1("2026-12-01"));
        await FileRetry.ReadAllTextAsync(downloaded);
        string warning =
            $"Warning: ignored {downloaded}: its schemaVersion 1 rates have no API-equivalent prices; run gh-ledger rates update to replace them.\n";

        CliRun validate = await harness.RunAsync("rates", "validate");
        CliRun status = await harness.RunAsync("status", "--json");
        CliRun update = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-01")))),
            "rates",
            "update");

        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            warning + $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the built-in rates (2026-11-23).\n",
            validate.Out);
        using JsonDocument document = JsonDocument.Parse(status.Out);
        Assert.Equal("2026-11-23", document.RootElement.GetProperty("ratesVersion").GetString());
        Assert.Equal("built-in", document.RootElement.GetProperty("ratesSource").GetString());
        Assert.Equal(
            [warning[9..^1]],
            document.RootElement.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(0, update.ExitCode);
        Assert.Equal(warning + "Updated rates from 2026-11-23 to 2026-12-01.\n", update.Out);
        Assert.Equal(PublishedRates.WithVersion("2026-12-01"), await FileRetry.ReadAllTextAsync(downloaded));
    }

    // The Release workflow publishes these rates as rates.json for gh-ledger 0.4.x and earlier.
    [Fact]
    public void RU14_SchemaVersion1RatesKeepEveryCreditPrice()
    {
        string legacy = PublishedRates.SchemaVersion1("2026-11-23");
        using JsonDocument document = JsonDocument.Parse(legacy);
        JsonRateCardCalculator v1 = JsonRateCardCalculator.Parse(legacy);
        JsonRateCardCalculator v2 = JsonRateCardCalculator.LoadEmbedded();
        string[] models = JsonNode.Parse(legacy)!["githubAiCredits"]!["models"]!.AsArray()
            .Select(model => model!["id"]!.GetValue<string>())
            .ToArray();

        Assert.Equal(
            ["schemaVersion", "ratesVersion", "source", "notes", "githubAiCredits", "foundryByok", "localOffload"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, v1.SchemaVersion);
        Assert.Equal("2026-11-23", v1.RatesVersion);
        Assert.Equal(34, models.Length);
        foreach (string model in models)
        {
            NormalizedGenAiSpan span = TestSpans.Chat(
                requestModel: model,
                inputTokens: 300_000,
                cacheReadInputTokens: 100_000,
                cacheWriteInputTokens: 10_000,
                outputTokens: 5_000);
            CreditCalculationResult legacyResult = v1.Calculate(span);
            CreditCalculationResult result = v2.Calculate(span);
            Assert.Equal(result.UsdCost, legacyResult.UsdCost);
            Assert.Equal(result.AiCredits, legacyResult.AiCredits);
            Assert.Equal(ApiEquivalentStatus.Unpriced, legacyResult.ApiEquivalent.Status);
        }
    }
}
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/TestSupport/PublishedRates.cs`, replace:

```csharp
public static class PublishedRates
{
    public const string Url = "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json";

    // The embedded rates.json text, exactly as built into CopilotLedger.Core.
```

with:

```csharp
public static class PublishedRates
{
    public const string Url = "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.v2.json";

    public const string LegacyUrl = "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json";

    // The embedded rates.json text, exactly as built into CopilotLedger.Core.
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/TestSupport/PublishedRates.cs`, replace:

```csharp
    public static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    // WithVersion(ratesVersion) plus a GitHub model gpt-new, alias gpt-preview, at $1 input, $0.10 cached input, and
    // $4 output per 1M tokens.
```

with:

```csharp
    public static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    // WithVersion(ratesVersion) as the Release workflow turns it into the rates.json of older gh-ledger versions:
    // jq '.schemaVersion = 1 | del(.apiEquivalent)'.
    public static string SchemaVersion1(string ratesVersion)
    {
        JsonObject document = JsonNode.Parse(WithVersion(ratesVersion))!.AsObject();
        document["schemaVersion"] = 1;
        document.Remove("apiEquivalent");
        return document.ToJsonString();
    }

    // WithVersion(ratesVersion) plus a GitHub model gpt-new, alias gpt-preview, at $1 input, $0.10 cached input, and
    // $4 output per 1M tokens.
```

Edit 1 of 1 in `docs/cli.md`, replace:

```markdown
- `gh ledger rates init [--force]` creates a starter rates overlay, `rates.json` in the data directory.
- `gh ledger rates validate [--file <path>]` checks the overlay, or another file, with the same rules the daemon uses.
- `gh ledger rates update` downloads `rates.json` from the latest gh-ledger release and keeps it when it's newer than the rates in use and your overlay still applies. It uses your system proxy. Restart the daemon afterward; it reads rates when it starts.

See [configuration.md](configuration.md) for pricing, the overlay format, and where files are kept.
```

with:

```markdown
- `gh ledger rates init [--force]` creates a starter rates overlay, `rates.json` in the data directory.
- `gh ledger rates validate [--file <path>]` checks the overlay, or another file, with the same rules the daemon uses.
- `gh ledger rates update` downloads `rates.v2.json` from the latest gh-ledger release, or its `rates.json` if it has none, and keeps it when it's newer than the rates in use, has API list prices (`schemaVersion` 2), and your overlay still applies. It uses your system proxy. Restart the daemon afterward; it reads rates when it starts.

See [configuration.md](configuration.md) for pricing, the overlay format, and where files are kept.
```

Edit 1 of 1 in `docs/configuration.md`, replace:

```markdown
## Newer rates between releases: `gh ledger rates update`

Each gh-ledger release attaches the `rates.json` it was built with. `gh ledger rates update` downloads the latest release's copy from `https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json`, through your system proxy, and checks it with the same rules as the built-in rates.

Rates versions are dates. When the download is newer than the rates in use, `rates update` saves it as `rates.downloaded.json`; when it's the same or older, nothing changes. If your overlay doesn't merge with the newer rates, nothing is saved, and the error names the problem in your overlay.

CopilotLedger's base rates are `rates.downloaded.json` when that file is valid and newer than the built-in rates, and the built-in rates otherwise. So after you upgrade gh-ledger to newer built-in rates, an older download is ignored. A download that can't be read is ignored with a warning in `gh ledger status`, `gh ledger rates validate`, and the daemon log. Your overlay applies on top of either. `gh ledger status` shows `(downloaded)` after the rates version when the download is in use.

The daemon reads rates when it starts, so restart it after an update. Spans already recorded keep the prices they were recorded with.
```

with:

```markdown
## Newer rates between releases: `gh ledger rates update`

Each gh-ledger release attaches the rates it was built with as `rates.v2.json`. It also attaches them as `rates.json`, with `schemaVersion` 1 and without API list prices, for gh-ledger 0.4.1 and earlier, which read only `schemaVersion` 1. `gh ledger rates update` downloads the latest release's copy from `https://github.com/swigerb/gh-ledger/releases/latest/download/rates.v2.json`, through your system proxy, and checks it with the same rules as the built-in rates. If the latest release has no `rates.v2.json`, it downloads that release's `rates.json` instead.

Rates versions are dates. When the download is newer than the rates in use, `rates update` saves it as `rates.downloaded.json`; when it's the same or older, nothing changes. A newer download with `schemaVersion` 1 isn't saved either, because it has no API list prices, and `rates update` reports an error. If your overlay doesn't merge with the newer rates, nothing is saved, and the error names the problem in your overlay.

CopilotLedger's base rates are `rates.downloaded.json` when that file is valid, newer than the built-in rates, and has `schemaVersion` 2, and the built-in rates otherwise. So after you upgrade gh-ledger to newer built-in rates, an older download is ignored. A download that can't be read, and a newer one with `schemaVersion` 1, such as one that an older gh-ledger saved, are ignored with a warning in `gh ledger status`, `gh ledger rates validate`, and the daemon log. Your overlay applies on top of either. `gh ledger status` shows `(downloaded)` after the rates version when the download is in use.

The daemon reads rates when it starts, so restart it after an update. Spans already recorded keep the prices they were recorded with.
```

Edit 1 of 4 in `docs/releasing.md`, replace:

```markdown
# Releasing gh-ledger

Releases are GitHub Releases of this repository. They make `gh extension install swigerb/gh-ledger` work, and `gh ledger rates update` downloads their `rates.json`. The CopilotLedger PR Receipt action is released from [its own repository](https://github.com/swigerb/copilot-ledger-action).

## Cut a release
```

with:

```markdown
# Releasing gh-ledger

Releases are GitHub Releases of this repository. They make `gh extension install swigerb/gh-ledger` work, and `gh ledger rates update` downloads their `rates.v2.json`. The CopilotLedger PR Receipt action is released from [its own repository](https://github.com/swigerb/copilot-ledger-action).

## Cut a release
```

Edit 2 of 4 in `docs/releasing.md`, replace:

```markdown
   - the tests that expect it: `DaemonCommandTests.DM1` and `ReportCommandTests.StatusText`;
   - the samples in `docs/guide.md` that show it: both `status` samples and the receipt. `GuideSamplesTests` fails until they match.
2. If `src/CopilotLedger.Core/Pricing/rates.json` changed since the latest release, set its `ratesVersion` to the date of the change (`yyyy-MM-dd`), later than the latest release's. `gh ledger rates update` only takes rates with a later version, and the Release workflow fails when `rates.json` changed but its `ratesVersion` didn't move forward. In the same change, replace the old date wherever it's shown:
   - the samples in `docs/guide.md` that show it: `status-first`, `status`, `rates-validate`, and `rates-update`. The `rates-update` sample and `GuideSamplesTests.GS4` download version `2026-12-15`; if that isn't later than the new date, change it in both;
   - the tests that expect it: in `ReportCommandTests` (including `StatusText`), `RatesCommandTests`, `RatesUpdateTests`, `RateOverlayTests`, and `JsonRateCardCalculatorTests`, and in `TestSupport/PublishedRates.cs`. `RatesUpdateTests` downloads versions before, equal to, and after the old date; keep each in the same place relative to the new one.
```

with:

```markdown
   - the tests that expect it: `DaemonCommandTests.DM1` and `ReportCommandTests.StatusText`;
   - the samples in `docs/guide.md` that show it: both `status` samples and the receipt. `GuideSamplesTests` fails until they match.
2. If `src/CopilotLedger.Core/Pricing/rates.json` changed since the latest release, set its `ratesVersion` to the date of the change (`yyyy-MM-dd`), later than the latest release's. `gh ledger rates update` only takes rates with a later version, and the Release workflow fails when `rates.json` changed but its `ratesVersion` didn't move forward; it compares `rates.json` with the latest release's `rates.v2.json`, or with its `rates.json` when it has none. In the same change, replace the old date wherever it's shown:
   - the samples in `docs/guide.md` that show it: `status-first`, `status`, `rates-validate`, and `rates-update`. The `rates-update` sample and `GuideSamplesTests.GS4` download version `2026-12-15`; if that isn't later than the new date, change it in both;
   - the tests that expect it: in `ReportCommandTests` (including `StatusText`), `RatesCommandTests`, `RatesUpdateTests`, `RateOverlayTests`, and `JsonRateCardCalculatorTests`, and in `TestSupport/PublishedRates.cs`. `RatesUpdateTests` downloads versions before, equal to, and after the old date; keep each in the same place relative to the new one.
```

Edit 3 of 4 in `docs/releasing.md`, replace:

````markdown
   ```

When a `v*` tag is pushed, the Release workflow checks that the tag equals `v` plus the project version, builds a single-file binary for each platform, smoke-tests the same three, checks `ratesVersion` against the latest release's `rates.json`, and creates the release with these assets:

| Asset | Platform |
````

with:

````markdown
   ```

When a `v*` tag is pushed, the Release workflow checks that the tag equals `v` plus the project version, builds a single-file binary for each platform, smoke-tests the same three, checks `ratesVersion` against the latest release's rates, and creates the release with these assets:

| Asset | Platform |
````

Edit 4 of 4 in `docs/releasing.md`, replace:

```markdown
| `gh-ledger-darwin-amd64` | macOS x64 |
| `gh-ledger-darwin-arm64` | macOS Apple silicon |
| `rates.json` | The built-in rates, for `gh ledger rates update` |
| `gh-ledger-checksums.txt` | SHA-256 checksums of the files above |

GitHub CLI picks a release asset by the `<os>-<arch>` suffix of its name, so keep these names. A tag with a hyphen, such as `v0.5.0-rc.1`, makes a prerelease, which `gh extension install` and `rates update` ignore because they use the latest release.
```

with:

```markdown
| `gh-ledger-darwin-amd64` | macOS x64 |
| `gh-ledger-darwin-arm64` | macOS Apple silicon |
| `rates.v2.json` | The built-in rates, for `gh ledger rates update` |
| `rates.json` | The built-in rates without `apiEquivalent`, as `schemaVersion` 1, for `gh ledger rates update` in gh-ledger 0.4.1 and earlier, which refuse `schemaVersion` 2 |
| `gh-ledger-checksums.txt` | SHA-256 checksums of the files above |

GitHub CLI picks a release asset by the `<os>-<arch>` suffix of its name, so keep these names. A tag with a hyphen, such as `v0.5.0-rc.1`, makes a prerelease, which `gh extension install` and `rates update` ignore because they use the latest release.
```

Edit 1 of 2 in `.github/workflows/release.yml`, replace:

```yaml
          GH_TOKEN: ${{ github.token }}
        run: |
          if ! gh release download --repo "$GITHUB_REPOSITORY" --pattern rates.json --dir latest; then
            echo "Skipping the check: no rates.json was downloaded from the latest release."
            exit 0
          fi
          current=src/CopilotLedger.Core/Pricing/rates.json
          old=$(jq -r .ratesVersion latest/rates.json)
          new=$(jq -r .ratesVersion "$current")
          if cmp -s <(jq -S . latest/rates.json) <(jq -S . "$current"); then
            echo "rates.json is unchanged since the latest release ($old)."
          elif [[ "$new" > "$old" ]]; then
```

with:

```yaml
          GH_TOKEN: ${{ github.token }}
        run: |
          if gh release download --repo "$GITHUB_REPOSITORY" --pattern rates.v2.json --dir latest; then
            published=latest/rates.v2.json
          elif gh release download --repo "$GITHUB_REPOSITORY" --pattern rates.json --dir latest; then
            published=latest/rates.json
          else
            echo "Skipping the check: no rates were downloaded from the latest release."
            exit 0
          fi
          current=src/CopilotLedger.Core/Pricing/rates.json
          old=$(jq -r .ratesVersion "$published")
          new=$(jq -r .ratesVersion "$current")
          if cmp -s <(jq -S . "$published") <(jq -S . "$current"); then
            echo "rates.json is unchanged since the latest release ($old)."
          elif [[ "$new" > "$old" ]]; then
```

Edit 2 of 2 in `.github/workflows/release.yml`, replace:

```yaml
      - name: Add the rates and checksums
        run: |
          cp src/CopilotLedger.Core/Pricing/rates.json dist/rates.json
          cd dist
          sha256sum gh-ledger-* rates.json > gh-ledger-checksums.txt
          cat gh-ledger-checksums.txt
      - name: Create the release
```

with:

```yaml
      - name: Add the rates and checksums
        run: |
          rates=src/CopilotLedger.Core/Pricing/rates.json
          test "$(jq .schemaVersion "$rates")" = 2
          cp "$rates" dist/rates.v2.json
          jq '.schemaVersion = 1 | del(.apiEquivalent)' "$rates" > dist/rates.json
          cd dist
          sha256sum gh-ledger-* rates.json rates.v2.json > gh-ledger-checksums.txt
          cat gh-ledger-checksums.txt
      - name: Create the release
```

### Tests

- **RU3**'s 404 case now fails on `LegacyUrl`, after both requests. **RU11** (2): a release without `rates.v2.json` falls back to `rates.json` (both calls recorded); an older v1 card changes nothing, and a later one is refused with exit 1 and the file kept. **RU12**: `schemaVersion` 3 fails closed with `schemaVersion: must be 1 or 2`, keeping the file, as gh-ledger 0.4.1 does with version 2. **RU13**: a later v1 download is ignored with the warning in `rates validate`, `status --json`, and `rates update`, which replaces it. **RU14**: the derived v1 card has the v1 keys only, parses as version 1, and prices all 34 GitHub models exactly as the v2 card does.

### Acceptance criteria

- New binaries read `rates.v2.json`, fall back to `rates.json`, and never let a v1 card replace built-in list prices (RU11, RU13); unsupported versions fail closed (RU12); the v1 asset keeps every credit price (RU14).
- `release.yml` publishes `rates.v2.json` and the derived `rates.json`, with checksums for both, and its `rates` check handles a latest release with either. Don't push a tag or run a release.
- `dotnet test CopilotLedger.slnx` passes **939** tests (934 + 5).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 5 specification: Ledger Schema Version 5

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/LedgerMigrations.cs` | Migration 5 |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | Insert, validate, and sum the five columns; prompt chains carry them |
| `src/CopilotLedger.Storage/LedgerRollupRow.cs`, `PromptChainRow.cs` | `ApiEquivalentUsd`, `ApiEquivalentUnpricedSpanCount`, `ApiEquivalentEstimatedSpanCount` |
| `src/CopilotLedger.Otlp/Ingestion/LedgerIngestor.cs` | Rejects an API-equivalent cost beyond pico-USD |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` (`Sum`), `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs` (`NoSpend`), `src/CopilotLedger.Mcp/LedgerMcpTools.cs` (`EmptyRollup`) | Set the new required properties; no output changes yet |
| `tests/CopilotLedger.Tests/Storage/ApiEquivalentStoreTests.cs` | New: AS1–AS5 |
| `tests/CopilotLedger.Tests/Otlp/LedgerIngestorTests.cs` | New IG11 |
| `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, `SqliteLedgerStoreTests.cs` | SL1, SL8, and SL11 drop the five columns too; SL10 and S1 expect version 5 |

### DDL (migration 5, in one transaction)

```sql
ALTER TABLE spans ADD COLUMN api_equivalent_usd_pico INTEGER
    CHECK (api_equivalent_usd_pico IS NULL OR api_equivalent_usd_pico >= 0);
ALTER TABLE spans ADD COLUMN api_equivalent_status TEXT
    CHECK (api_equivalent_status IS NULL OR api_equivalent_status IN ('priced', 'unpriced', 'not_applicable'));
ALTER TABLE spans ADD COLUMN api_rate_card_model_id TEXT;
ALTER TABLE spans ADD COLUMN api_rates_version TEXT;
ALTER TABLE spans ADD COLUMN api_rate_card_sha256 TEXT;
```

### Contracts

```csharp
// LedgerRollupRow and PromptChainRow gain:
public required decimal? ApiEquivalentUsd { get; init; }              // null when the group has spans and none is priced
public required long ApiEquivalentUnpricedSpanCount { get; init; }    // spans without a price, legacy rows included
public required long ApiEquivalentEstimatedSpanCount { get; init; }   // priced spans that reported no cache counters
```

- **Insert.** `@api_equivalent_usd_pico` is `PicoUsd.FromUsd(UsdCost)` or `NULL`; `@api_equivalent_status` is `priced`, `unpriced`, or `not_applicable`; `@api_rate_card_model_id` is `RateCardModelId` or `NULL`; `@api_rates_version` and `@api_rate_card_sha256` are the card's, on every row.
- **Validation** (before anything is written): `ArgumentException` "An API-equivalent cost needs its rates, and a model and breakdown exactly when it's Priced." when `ApiEquivalent` is `null`, its status is undefined, `Priced` and a breakdown or a model don't go together, or its rates version or hash is `null`.
- **Sums** (rollups and prompt chains): `SUM(api_equivalent_usd_pico)`, `SUM(CASE WHEN api_equivalent_usd_pico IS NULL THEN 1 ELSE 0 END)`, and `SUM(CASE WHEN api_equivalent_usd_pico IS NOT NULL AND NOT {ReportedSql} THEN 1 ELSE 0 END)`. Ordering doesn't change.
- **C# rows.** `Sum` adds the amounts and is `null` only when it has rows and every amount is `null`; `NoSpend` and `EmptyRollup` have `0`.
- **Ingest.** `PicoUsd.FromUsd(calculation.ApiEquivalent.UsdCost ?? 0m)` joins the existing checks, so an out-of-range cost rejects only its span ("cost out of range").

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, replace:

```csharp
        FoundryByokUsd = 0m,
        LocalSavedUsd = 0m,
    };

    public static Command Create(CliContext context)
```

with:

```csharp
        FoundryByokUsd = 0m,
        LocalSavedUsd = 0m,
        ApiEquivalentUsd = 0m,
        ApiEquivalentUnpricedSpanCount = 0,
        ApiEquivalentEstimatedSpanCount = 0,
    };

    public static Command Create(CliContext context)
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            FoundryByokUsd = rows.Sum(row => row.FoundryByokUsd),
            LocalSavedUsd = rows.Sum(row => row.LocalSavedUsd),
        };

    private static string[] MoneyCells(LedgerRollupRow row) =>
```

with:

```csharp
            FoundryByokUsd = rows.Sum(row => row.FoundryByokUsd),
            LocalSavedUsd = rows.Sum(row => row.LocalSavedUsd),
            ApiEquivalentUsd = rows.Count > 0 && rows.All(row => row.ApiEquivalentUsd is null)
                ? null
                : rows.Sum(row => row.ApiEquivalentUsd ?? 0m),
            ApiEquivalentUnpricedSpanCount = rows.Sum(row => row.ApiEquivalentUnpricedSpanCount),
            ApiEquivalentEstimatedSpanCount = rows.Sum(row => row.ApiEquivalentEstimatedSpanCount),
        };

    private static string[] MoneyCells(LedgerRollupRow row) =>
```

Edit 1 of 1 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
        FoundryByokUsd = 0m,
        LocalSavedUsd = 0m,
    };

    private static JsonElement ParseSchema(string json)
```

with:

```csharp
        FoundryByokUsd = 0m,
        LocalSavedUsd = 0m,
        ApiEquivalentUsd = 0m,
        ApiEquivalentUnpricedSpanCount = 0,
        ApiEquivalentEstimatedSpanCount = 0,
    };

    private static JsonElement ParseSchema(string json)
```

Edit 1 of 1 in `src/CopilotLedger.Otlp/Ingestion/LedgerIngestor.cs`, replace:

```csharp
                    _ = PicoUsd.FromUsd(calculation.UsdCost);
                    _ = PicoUsd.FromUsd(calculation.EstimatedCloudSavedUsd);
                }
                catch (Exception ex) when (
```

with:

```csharp
                    _ = PicoUsd.FromUsd(calculation.UsdCost);
                    _ = PicoUsd.FromUsd(calculation.EstimatedCloudSavedUsd);
                    _ = PicoUsd.FromUsd(calculation.ApiEquivalent.UsdCost ?? 0m);
                }
                catch (Exception ex) when (
```

Edit 1 of 1 in `src/CopilotLedger.Storage/LedgerMigrations.cs`, replace:

```csharp
             WHERE below.trace_id = spans.trace_id AND below.span_id = spans.span_id;
            """),
    ];
}
```

with:

```csharp
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
    ];
}
```

Edit 1 of 1 in `src/CopilotLedger.Storage/LedgerRollupRow.cs`, replace:

```csharp
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
    public long CacheUnreportedSpanCount => SpanCount - CacheReportedSpanCount;
    public decimal? CacheHitRatio => CacheMetrics.HitRatio(ReportedCacheReadInputTokens, ReportedInputTokens);
```

with:

```csharp
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
    public required decimal? ApiEquivalentUsd { get; init; }               // at API list prices; null when spans exist but none is priced
    public required long ApiEquivalentUnpricedSpanCount { get; init; }     // spans without an API-equivalent price, legacy rows included
    public required long ApiEquivalentEstimatedSpanCount { get; init; }    // priced spans that reported no cache counters
    public long CacheUnreportedSpanCount => SpanCount - CacheReportedSpanCount;
    public decimal? CacheHitRatio => CacheMetrics.HitRatio(ReportedCacheReadInputTokens, ReportedInputTokens);
```

Edit 1 of 1 in `src/CopilotLedger.Storage/PromptChainRow.cs`, replace:

```csharp
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
}
```

with:

```csharp
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
    public required decimal? ApiEquivalentUsd { get; init; }               // as in LedgerRollupRow
    public required long ApiEquivalentUnpricedSpanCount { get; init; }
    public required long ApiEquivalentEstimatedSpanCount { get; init; }
}
```

Edit 1 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            pricing_status, rates_version, cost_usd_pico, saved_usd_pico,
            repo_root, repo_slug, branch_name, head_commit_sha, recorded_at_unix_ms,
            cache_read_state, cache_write_state, pull_request_number
        ) VALUES (
            @trace_id, @span_id, @parent_span_id, @operation_name, @conversation_id,
```

with:

```csharp
            pricing_status, rates_version, cost_usd_pico, saved_usd_pico,
            repo_root, repo_slug, branch_name, head_commit_sha, recorded_at_unix_ms,
            cache_read_state, cache_write_state, pull_request_number,
            api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id,
            api_rates_version, api_rate_card_sha256
        ) VALUES (
            @trace_id, @span_id, @parent_span_id, @operation_name, @conversation_id,
```

Edit 2 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            @pricing_status, @rates_version, @cost_usd_pico, @saved_usd_pico,
            @repo_root, @repo_slug, @branch_name, @head_commit_sha, @recorded_at_unix_ms,
            @cache_read_state, @cache_write_state, @pull_request_number
        ) ON CONFLICT (trace_id, span_id) DO NOTHING;
        """;
```

with:

```csharp
            @pricing_status, @rates_version, @cost_usd_pico, @saved_usd_pico,
            @repo_root, @repo_slug, @branch_name, @head_commit_sha, @recorded_at_unix_ms,
            @cache_read_state, @cache_write_state, @pull_request_number,
            @api_equivalent_usd_pico, @api_equivalent_status, @api_rate_card_model_id,
            @api_rates_version, @api_rate_card_sha256
        ) ON CONFLICT (trace_id, span_id) DO NOTHING;
        """;
```

Edit 3 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                LocalSavedUsd =
                    PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("saved_usd_pico"))),
            });
        }
```

with:

```csharp
                LocalSavedUsd =
                    PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("saved_usd_pico"))),
                ApiEquivalentUsd = ReadUsd(reader, "api_equivalent_usd_pico"),
                ApiEquivalentUnpricedSpanCount =
                    reader.GetInt64(reader.GetOrdinal("api_equivalent_unpriced_span_count")),
                ApiEquivalentEstimatedSpanCount =
                    reader.GetInt64(reader.GetOrdinal("api_equivalent_estimated_span_count")),
            });
        }
```

Edit 4 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                       SUM(CASE WHEN billing_channel = 'github_ai_credits' THEN cost_usd_pico ELSE 0 END) AS github_usd_pico,
                       SUM(CASE WHEN billing_channel = 'foundry_byok' THEN cost_usd_pico ELSE 0 END)      AS byok_usd_pico,
                       SUM(saved_usd_pico)                                                                AS saved_usd_pico
                FROM spans
                WHERE {FilterSql}
```

with:

```csharp
                       SUM(CASE WHEN billing_channel = 'github_ai_credits' THEN cost_usd_pico ELSE 0 END) AS github_usd_pico,
                       SUM(CASE WHEN billing_channel = 'foundry_byok' THEN cost_usd_pico ELSE 0 END)      AS byok_usd_pico,
                       SUM(saved_usd_pico)                                                                AS saved_usd_pico,
                       SUM(api_equivalent_usd_pico)                                                       AS api_equivalent_usd_pico,
                       SUM(CASE WHEN api_equivalent_usd_pico IS NULL THEN 1 ELSE 0 END)                   AS api_equivalent_unpriced_span_count,
                       SUM(CASE WHEN api_equivalent_usd_pico IS NOT NULL AND NOT {ReportedSql} THEN 1 ELSE 0 END)
                                                                                                          AS api_equivalent_estimated_span_count
                FROM spans
                WHERE {FilterSql}
```

Edit 5 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                    reader.GetInt64(reader.GetOrdinal("github_usd_pico")),
                    reader.GetInt64(reader.GetOrdinal("byok_usd_pico")),
                    reader.GetInt64(reader.GetOrdinal("saved_usd_pico"))));
            }
        }
```

with:

```csharp
                    reader.GetInt64(reader.GetOrdinal("github_usd_pico")),
                    reader.GetInt64(reader.GetOrdinal("byok_usd_pico")),
                    reader.GetInt64(reader.GetOrdinal("saved_usd_pico")),
                    ReadUsd(reader, "api_equivalent_usd_pico"),
                    reader.GetInt64(reader.GetOrdinal("api_equivalent_unpriced_span_count")),
                    reader.GetInt64(reader.GetOrdinal("api_equivalent_estimated_span_count"))));
            }
        }
```

Edit 6 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                FoundryByokUsd = PicoUsd.ToUsd(item.ByokUsdPico),
                LocalSavedUsd = PicoUsd.ToUsd(item.SavedUsdPico),
            });
        }
```

with:

```csharp
                FoundryByokUsd = PicoUsd.ToUsd(item.ByokUsdPico),
                LocalSavedUsd = PicoUsd.ToUsd(item.SavedUsdPico),
                ApiEquivalentUsd = item.ApiEquivalentUsd,
                ApiEquivalentUnpricedSpanCount = item.ApiEquivalentUnpricedSpanCount,
                ApiEquivalentEstimatedSpanCount = item.ApiEquivalentEstimatedSpanCount,
            });
        }
```

Edit 7 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            throw new ArgumentException("Pull request numbers must be positive.", parameterName);
        }
    }

    private static void AddInsertParameters(
```

with:

```csharp
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

    private static void AddInsertParameters(
```

Edit 8 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            "@pull_request_number",
            OrDbNull(entry.Git?.PullRequestNumber));
    }

    private static string GetRollupSql(LedgerGrouping grouping)
```

with:

```csharp
            "@pull_request_number",
            OrDbNull(entry.Git?.PullRequestNumber));
        ApiEquivalentCost retail = calculation.ApiEquivalent;
        command.Parameters.AddWithValue(
            "@api_equivalent_usd_pico",
            OrDbNull(retail.UsdCost is decimal retailUsd ? PicoUsd.FromUsd(retailUsd) : (long?)null));
        command.Parameters.AddWithValue("@api_equivalent_status", ToDatabaseString(retail.Status));
        command.Parameters.AddWithValue("@api_rate_card_model_id", OrDbNull(retail.RateCardModelId));
        command.Parameters.AddWithValue("@api_rates_version", retail.RatesVersion);
        command.Parameters.AddWithValue("@api_rate_card_sha256", retail.RateCardSha256);
    }

    private static string GetRollupSql(LedgerGrouping grouping)
```

Edit 9 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                   SUM(CASE WHEN billing_channel = 'github_ai_credits' THEN cost_usd_pico ELSE 0 END) AS github_usd_pico,
                   SUM(CASE WHEN billing_channel = 'foundry_byok' THEN cost_usd_pico ELSE 0 END)      AS byok_usd_pico,
                   SUM(saved_usd_pico)                                                               AS saved_usd_pico
            FROM spans
            WHERE {FilterSql}
```

with:

```csharp
                   SUM(CASE WHEN billing_channel = 'github_ai_credits' THEN cost_usd_pico ELSE 0 END) AS github_usd_pico,
                   SUM(CASE WHEN billing_channel = 'foundry_byok' THEN cost_usd_pico ELSE 0 END)      AS byok_usd_pico,
                   SUM(saved_usd_pico)                                                               AS saved_usd_pico,
                   SUM(api_equivalent_usd_pico)                                                      AS api_equivalent_usd_pico,
                   SUM(CASE WHEN api_equivalent_usd_pico IS NULL THEN 1 ELSE 0 END)                  AS api_equivalent_unpriced_span_count,
                   SUM(CASE WHEN api_equivalent_usd_pico IS NOT NULL AND NOT {ReportedSql} THEN 1 ELSE 0 END)
                                                                                                     AS api_equivalent_estimated_span_count
            FROM spans
            WHERE {FilterSql}
```

Edit 10 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
    };

    private static CacheCounterState ToCacheCounterState(string value) => value switch
    {
```

with:

```csharp
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
```

Edit 11 of 11 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        long GitHubUsdPico,
        long ByokUsdPico,
        long SavedUsdPico);
}
```

with:

```csharp
        long GitHubUsdPico,
        long ByokUsdPico,
        long SavedUsdPico,
        decimal? ApiEquivalentUsd,
        long ApiEquivalentUnpricedSpanCount,
        long ApiEquivalentEstimatedSpanCount);
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Otlp/LedgerIngestorTests.cs`, replace:

```csharp
    }

    private static OtlpSpan IgSpan(string spanId, params OtlpAttribute[] attributes) =>
        Span(
```

with:

```csharp
    }

    // An API-equivalent cost beyond the pico-USD range rejects its span like a billed one does, even when nothing billed it.
    [Fact]
    public async Task IG11_ApiEquivalentCostOutOfRange_RejectsOnlyItsSpan()
    {
        using TempDirectory temp = new();
        string overlay = Path.Combine(temp.Path, "ig11-rates.json");
        File.WriteAllText(
            overlay,
            """
            {
              "schemaVersion": 2,
              "ratesVersion": "ig11",
              "apiEquivalent": {
                "models": [
                  { "id": "ig11-huge", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1000000, "outputPer1M": 1000000 } ] } ] }
                ]
              }
            }
            """);
        Harness harness = await Harness.CreateAsync(
            temp,
            JsonRateCardCalculator.LoadEmbeddedWithOverlay(overlay));
        OtlpSpan huge = IgSpan("0000000000000001",
            Str("gen_ai.request.model", "ig11-huge"),
            Int("gen_ai.usage.input_tokens", 10_000_000));
        OtlpSpan large = IgSpan("0000000000000003",
            Str("gen_ai.request.model", "ig11-huge"),
            Int("gen_ai.usage.input_tokens", 9_000_000));
        OtlpSpan valid = IgSpan("0000000000000002",
            Str("gen_ai.request.model", "mai-code-2"),
            Int("gen_ai.usage.input_tokens", 1000),
            Int("gen_ai.usage.output_tokens", 100));

        IngestResult result = await harness.Ingestor.IngestAsync(
            Request(CopilotResource, huge, large, valid));

        AssertResult(result, 3, 1, "cost out of range", 2, 2);
        LedgerRollupRow total = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Repository,
            LedgerFilter.All));
        Assert.Equal(2, total.SpanCount);
        Assert.Equal(9_000_000m, total.ApiEquivalentUsd);
        Assert.Equal(1, total.ApiEquivalentUnpricedSpanCount);
        Assert.Equal(0.00075m, total.AiCreditsUsd);
    }

    private static OtlpSpan IgSpan(string spanId, params OtlpAttribute[] attributes) =>
        Span(
```

New file `tests/CopilotLedger.Tests/Storage/ApiEquivalentStoreTests.cs`:

```csharp
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class ApiEquivalentStoreTests
{
    private const string T1 = "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1";
    private const string T2 = "a2a2a2a2a2a2a2a2a2a2a2a2a2a2a2a2";
    private const string T3 = "a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3";
    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    // Every outcome of the API-equivalent lens, as stored: the rate card's version and hash on every row, and NULL money for
    // the spans it couldn't price.
    [Fact]
    public async Task AS1_RecordAsync_StoresEachApiEquivalentOutcome()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);

        Assert.Equal(6, await store.RecordAsync(Entries()));

        await using SqliteConnection connection = await OpenAsync(temp);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT span_id, billing_channel, pricing_status, api_equivalent_usd_pico, api_equivalent_status,
                   api_rate_card_model_id, api_rates_version, api_rate_card_sha256
            FROM spans
            ORDER BY span_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        object?[][] expected =
        [
            ["a000000000000001", "github_ai_credits", "priced", 1_560_000_000_000L, "priced", "test-retail"],
            ["a000000000000002", "github_ai_credits", "unknown_model", null, "unpriced", null],
            ["a000000000000003", "unpriced", "invalid_usage", null, "not_applicable", null],
            ["a000000000000004", "local", "local_offload", 11_200_000_000L, "priced", "test-baseline"],
            ["a000000000000005", "foundry_byok", "priced", 300_000_000_000L, "priced", "gpt-6.1-sol"],
            ["a000000000000006", "unpriced", "unrecognized_endpoint", 4_500_000_000L, "priced", "test-retail"],
        ];
        foreach (object?[] row in expected)
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(row[0], reader.GetString(0));
            Assert.Equal(row[1], reader.GetString(1));
            Assert.Equal(row[2], reader.GetString(2));
            Assert.Equal(row[3], reader.IsDBNull(3) ? null : reader.GetInt64(3));
            Assert.Equal(row[4], reader.GetString(4));
            Assert.Equal(row[5], reader.IsDBNull(5) ? null : reader.GetString(5));
            Assert.Equal("retail-fixture", reader.GetString(6));
            Assert.Equal(Retail.ApiRateCardSha256, reader.GetString(7));
        }

        Assert.False(await reader.ReadAsync());
        Assert.Equal(
            2L,
            await ScalarAsync(connection, "SELECT COUNT(*) FROM spans WHERE api_equivalent_usd_pico IS NULL;"));
        Assert.Equal(
            0L,
            await ScalarAsync(connection, "SELECT COUNT(*) FROM spans WHERE api_equivalent_usd_pico = 0;"));
    }

    [Fact]
    public async Task AS2_Rollups_SumPricedSpansAndCountTheRest()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(Entries());

        Assert.Collection(
            await store.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All),
            row => AssertRetail(row.Model, row, "gpt-6.1-sol", 0.3m, 0, 1),
            row => AssertRetail(row.Model, row, "test-retail", 1.5645m, 1, 1),
            row => AssertRetail(row.Model, row, "qwen-local", 0.0112m, 0, 1),
            row => AssertRetail(row.Model, row, "test-unknown", null, 1, 0));
        LedgerRollupRow total = Assert.Single(
            await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
        AssertRetail(total.Repository, total, "(none)", 1.8757m, 2, 3);
        Assert.Equal(0.2725m, total.AiCreditsUsd);
        Assert.Equal(27.25m, total.AiCredits);
        Assert.Equal(0.315m, total.FoundryByokUsd);
        Assert.Equal(0.0075m, total.LocalSavedUsd);
    }

    [Fact]
    public async Task AS3_PromptChains_CarryApiEquivalentTotals()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(Entries());

        Assert.Collection(
            await store.GetTopPromptChainsAsync(LedgerFilter.All, 10),
            row => AssertRetail(row.TraceId, row, T3, 0.3045m, 0, 2),
            row => AssertRetail(row.TraceId, row, T1, 1.56m, 1, 0),
            row => AssertRetail(row.TraceId, row, T2, 0.0112m, 1, 1));
    }

    // A version 4 ledger keeps every row and value; its rows have no API-equivalent price until a backfill.
    [Fact]
    public async Task AS4_Migration5_KeepsEveryRowAndLeavesLegacyRowsUnpriced()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore first = await StoreAsync(temp);
        await first.RecordAsync(Entries());
        await using (SqliteConnection connection = await OpenAsync(temp))
        {
            await ExecuteAsync(connection, """
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
                ALTER TABLE spans DROP COLUMN api_rate_card_model_id;
                ALTER TABLE spans DROP COLUMN api_rates_version;
                ALTER TABLE spans DROP COLUMN api_rate_card_sha256;
                PRAGMA user_version = 4;
                """);
        }

        string[] before;
        await using (SqliteConnection connection = await OpenAsync(temp))
        {
            before = await RowsAsync(connection, "SELECT * FROM spans ORDER BY id;");
        }

        // A new process starts with no pooled connections; see SL8.
        SqliteConnection.ClearAllPools();
        SqliteLedgerStore store = new(DatabasePath(temp));
        await store.InitializeAsync();

        await using SqliteConnection check = await OpenAsync(temp);
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(6, before.Length);
        Assert.Equal(
            before,
            await RowsAsync(
                check,
                """
                SELECT * FROM (
                    SELECT id, trace_id, span_id, parent_span_id, operation_name, conversation_id, agent_name, provider_name,
                           request_model, response_model, model_id, rate_card_model_id, tier_name, server_address, server_port,
                           start_time_unix_ms, end_time_unix_ms, input_tokens, output_tokens, cache_read_input_tokens,
                           cache_write_input_tokens, billing_channel, pricing_status, rates_version, cost_usd_pico,
                           saved_usd_pico, repo_root, repo_slug, branch_name, head_commit_sha, recorded_at_unix_ms,
                           cache_read_state, cache_write_state, pull_request_number, agent_span_id
                    FROM spans)
                ORDER BY id;
                """));
        Assert.Equal(
            6L,
            await ScalarAsync(
                check,
                """
                SELECT COUNT(*) FROM spans
                WHERE api_equivalent_usd_pico IS NULL AND api_equivalent_status IS NULL AND api_rate_card_model_id IS NULL
                  AND api_rates_version IS NULL AND api_rate_card_sha256 IS NULL;
                """));
        LedgerRollupRow total = Assert.Single(
            await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
        AssertRetail(total.Repository, total, "(none)", null, 6, 0);
        Assert.Equal(0.2725m, total.AiCreditsUsd);
        Assert.Equal(0.315m, total.FoundryByokUsd);
        Assert.Equal(0.0075m, total.LocalSavedUsd);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task AS5_RecordAsync_RejectsInconsistentApiEquivalentCosts(int caseNumber)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        LedgerEntry[] entries = Entries();
        LedgerEntry priced = entries[0];
        LedgerEntry unpriced = entries[1];
        ApiEquivalentCost cost = priced.Calculation.ApiEquivalent;
        ApiEquivalentCost invalid = caseNumber switch
        {
            0 => cost with { Breakdown = null },
            1 => cost with { RateCardModelId = null },
            2 => unpriced.Calculation.ApiEquivalent with { Breakdown = cost.Breakdown },
            3 => cost with { Status = (ApiEquivalentStatus)7 },
            _ => null!,
        };
        LedgerEntry entry = priced with { Calculation = priced.Calculation with { ApiEquivalent = invalid } };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => store.RecordAsync([unpriced, entry]));

        Assert.Equal(
            "An API-equivalent cost needs its rates, and a model and breakdown exactly when it's Priced. (Parameter 'entries')",
            exception.Message);
        Assert.Empty(await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
    }

    // T1: a GitHub request priced in both lenses, and an unknown model. T2: inconsistent token counts, and a local request
    // priced as the card's local baseline. T3: a Foundry BYOK request, and an unrecognized endpoint whose model has a list
    // price. Only the first request reported cache counters.
    private static LedgerEntry[] Entries() =>
    [
        Entry(T1, "a000000000000001", TestSpans.Chat(
            requestModel: "test-retail",
            inputTokens: 300_000,
            cacheReadInputTokens: 100_000,
            cacheWriteInputTokens: 50_000,
            outputTokens: 10_000)),
        Entry(T1, "a000000000000002", TestSpans.Chat(
            requestModel: "test-unknown",
            inputTokens: 1_000,
            outputTokens: 100)),
        Entry(T2, "a000000000000003", TestSpans.Chat(
            requestModel: "test-retail",
            inputTokens: 1_000,
            cacheReadInputTokens: 2_000,
            outputTokens: 100)),
        Entry(T2, "a000000000000004", TestSpans.Chat(
            providerName: "ollama",
            requestModel: "qwen-local",
            inputTokens: 10_000,
            outputTokens: 1_000)),
        Entry(T3, "a000000000000005", TestSpans.Chat(
            serverAddress: "contoso.openai.azure.com",
            requestModel: "contoso-gpt61-prod",
            inputTokens: 100_000,
            outputTokens: 10_000)),
        Entry(T3, "a000000000000006", TestSpans.Chat(
            serverAddress: "llm.example.com",
            requestModel: "test-retail",
            inputTokens: 1_000,
            outputTokens: 100)),
    ];

    private static LedgerEntry Entry(string trace, string span, NormalizedGenAiSpan template)
    {
        NormalizedGenAiSpan value = template with { TraceId = trace, SpanId = span };
        return new LedgerEntry(value, Retail.Calculate(value), null);
    }

    private static void AssertRetail(
        string? key,
        LedgerRollupRow row,
        string expectedKey,
        decimal? usd,
        long unpriced,
        long estimated)
    {
        Assert.Equal(expectedKey, key);
        Assert.Equal(usd, row.ApiEquivalentUsd);
        Assert.Equal(unpriced, row.ApiEquivalentUnpricedSpanCount);
        Assert.Equal(estimated, row.ApiEquivalentEstimatedSpanCount);
    }

    private static void AssertRetail(
        string key,
        PromptChainRow row,
        string expectedKey,
        decimal? usd,
        long unpriced,
        long estimated)
    {
        Assert.Equal(expectedKey, key);
        Assert.Equal(usd, row.ApiEquivalentUsd);
        Assert.Equal(unpriced, row.ApiEquivalentUnpricedSpanCount);
        Assert.Equal(estimated, row.ApiEquivalentEstimatedSpanCount);
    }

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(DatabasePath(temp));
        await store.InitializeAsync();
        return store;
    }

    private static string DatabasePath(TempDirectory temp) => Path.Combine(temp.Path, "ledger.db");

    private static async Task<SqliteConnection> OpenAsync(TempDirectory temp)
    {
        SqliteConnection connection = new($"Data Source={DatabasePath(temp)}");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    // Each row as its values, NULL as "null", separated by '|'.
    private static async Task<string[]> RowsAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        List<string> rows = [];
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join(
                '|',
                Enumerable.Range(0, reader.FieldCount)
                    .Select(index => reader.IsDBNull(index)
                        ? "null"
                        : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture))));
        }

        return [.. rows];
    }
}
```

Edit 1 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await first.RecordAsync([Entry("mai-code-2", 1000, 100)]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                DROP INDEX ix_spans_conversation_start;
                ALTER TABLE spans DROP COLUMN cache_read_state;
```

with:

```csharp
        await first.RecordAsync([Entry("mai-code-2", 1000, 100)]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
                ALTER TABLE spans DROP COLUMN api_rate_card_model_id;
                ALTER TABLE spans DROP COLUMN api_rates_version;
                ALTER TABLE spans DROP COLUMN api_rate_card_sha256;
                DROP INDEX ix_spans_conversation_start;
                ALTER TABLE spans DROP COLUMN cache_read_state;
```

Edit 2 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();
        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(4L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(
            "span_links",
```

with:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();
        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
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
                DROP INDEX ix_spans_conversation_start;
                ALTER TABLE spans DROP COLUMN cache_read_state;
```

with:

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
                ALTER TABLE spans DROP COLUMN api_rate_card_sha256;
                DROP INDEX ix_spans_conversation_start;
                ALTER TABLE spans DROP COLUMN cache_read_state;
```

Edit 4 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(4L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM span_links;"));
```

with:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM span_links;"));
```

Edit 5 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, "PRAGMA user_version = 3;");
        }

        await using SqliteConnection holder = await OpenAsync(DatabasePath(temp));
```

with:

```csharp
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, "PRAGMA user_version = 4;");
        }

        await using SqliteConnection holder = await OpenAsync(DatabasePath(temp));
```

Edit 6 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        {
            version.Transaction = held;
            version.CommandText = "PRAGMA user_version = 4;";
            await version.ExecuteNonQueryAsync();
        }
```

with:

```csharp
        {
            version.Transaction = held;
            version.CommandText = "PRAGMA user_version = 5;";
            await version.ExecuteNonQueryAsync();
        }
```

Edit 7 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await init;

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(4L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM breaker_settings;"));
        Assert.Equal(
```

with:

```csharp
        await init;

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM breaker_settings;"));
        Assert.Equal(
```

Edit 8 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
            await ScalarAsync(
                check,
                "SELECT COUNT(*) FROM pragma_table_info('span_links') WHERE name = 'tool_name';"));
    }

    [Fact]
```

with:

```csharp
            await ScalarAsync(
                check,
                "SELECT COUNT(*) FROM pragma_table_info('spans') WHERE name = 'api_equivalent_status';"));
    }

    [Fact]
```

Edit 9 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        {
            await ExecuteAsync(connection, """
                DROP INDEX ix_spans_conversation_start;
                ALTER TABLE spans DROP COLUMN cache_read_state;
```

with:

```csharp
        {
            await ExecuteAsync(connection, """
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
                ALTER TABLE spans DROP COLUMN api_rate_card_model_id;
                ALTER TABLE spans DROP COLUMN api_rates_version;
                ALTER TABLE spans DROP COLUMN api_rate_card_sha256;
                DROP INDEX ix_spans_conversation_start;
                ALTER TABLE spans DROP COLUMN cache_read_state;
```

Edit 10 of 10 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(4L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(
            "ix_spans_conversation_start",
```

with:

```csharp
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(
            "ix_spans_conversation_start",
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs`, replace:

```csharp
            {
                versionCommand.CommandText = "PRAGMA user_version;";
                Assert.Equal(4L, await versionCommand.ExecuteScalarAsync());
            }
        }
```

with:

```csharp
            {
                versionCommand.CommandText = "PRAGMA user_version;";
                Assert.Equal(5L, await versionCommand.ExecuteScalarAsync());
            }
        }
```

### Tests

`ApiEquivalentStoreTests.Entries()` records six spans with `RetailTestRatesJson`: a GitHub request with cache reads and writes (long-context, `1.56`), an unknown model, inconsistent token counts, a local request (`0.0112`), a Foundry BYOK request (`0.3`), and an unrecognized endpoint with a listed model (`0.0045`), in three traces.

- **AS1**: the stored channel, pricing status, pico-USD cost, status, model, rates version, and hash of each; two `NULL` costs asserted directly, and no `0`.
- **AS2**: by model, `0.3`, `1.5645` (one unpriced, one estimated), `0.0112`, and `null`; the repository total `1.8757` with 2 unpriced and 3 estimated, and billed amounts `0.2725`, `27.25` credits, `0.315`, and `0.0075` savings.
- **AS3**: the prompt chains' totals, `0.3045`, `1.56`, and `0.0112`.
- **AS4**: a version 4 ledger (the five columns dropped) migrates to version 5 with every other column of every row unchanged and all five new columns `NULL`; its rollup is `null` with 6 unpriced spans and the same billed amounts.
- **AS5** (5): each inconsistent result throws and writes nothing.
- **IG11**: with an overlay listing `ig11-huge` at $1 a token, a 10,000,000-token request is rejected as "cost out of range", while a 9,000,000-token one ($9,000,000) and a `mai-code-2` request are recorded.
- SL1, SL8, and SL11 also drop the five columns before setting the old version, and SL1, SL8, SL10, SL11, and S1 expect version 5; SL10's other process now applies migration 5 first.

### Acceptance criteria

- Migration 5 keeps every row and value, and legacy rows have `api_equivalent_status IS NULL` (AS4); a version 1, 2, or 3 ledger migrates to 5 (SL1, SL8, SL11).
- Unpriced spans store `NULL`, never 0 (AS1); sums ignore them and count them (AS2, AS3).
- No `REAL`, `double`, or `float`; money stays `INTEGER` pico-USD and `decimal`.
- `dotnet test CopilotLedger.slnx` passes **949** tests (939 + 10).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 6 specification: Backfill API-Equivalent Prices

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/ApiEquivalentBackfill.cs` | New: what a backfill found |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | New `BackfillApiEquivalentAsync` |
| `src/CopilotLedger.Cli/Rates/RatesCommands.cs` | New `rates backfill-api-equivalent [--dry-run]` |
| `tests/CopilotLedger.Tests/Storage/ApiEquivalentStoreTests.cs` | `Entries()` becomes `internal` for reuse |
| `tests/CopilotLedger.Tests/Storage/ApiEquivalentBackfillTests.cs` | New: AB1–AB3 |
| `tests/CopilotLedger.Tests/Cli/RatesBackfillTests.cs` | New: RB1–RB3 |
| `docs/configuration.md`, `docs/cli.md` | The backfill |

### Contracts

```csharp
public sealed record ApiEquivalentBackfill
{
    public required long SpanCount { get; init; }               // spans without a result before the backfill
    public required long PricedSpanCount { get; init; }
    public required decimal PricedUsd { get; init; }
    public required long UnpricedSpanCount { get; init; }
    public required long NotApplicableSpanCount { get; init; }
    public required long OutOfRangeSpanCount { get; init; }     // left without a result
    public required long UpdatedSpanCount { get; init; }        // 0 for a dry run
}

// SqliteLedgerStore gains:
public Task<ApiEquivalentBackfill> BackfillApiEquivalentAsync(
    JsonRateCardCalculator calculator, bool dryRun, CancellationToken cancellationToken = default);
```

### Algorithm

1. Begin a transaction: immediate, or deferred for a dry run.
2. `SELECT id, operation_name, billing_channel, model_id, start_time_unix_ms, input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens FROM spans WHERE api_equivalent_status IS NULL ORDER BY id`, pricing each row with `CalculateApiEquivalent`. A cost that `PicoUsd.FromUsd` rejects counts as out of range and is skipped.
3. Unless it's a dry run, update each priced, unpriced, or not-applicable row's five `api_*` columns, with the calculator's `RatesVersion` and `ApiRateCardSha256`, `WHERE id = @id AND api_equivalent_status IS NULL`, and commit.

The command prints, after any `LoadBase` warning:

```text
5 spans have no API-equivalent result. With rates 2026-11-23:
  Priced:          2 ($3.16)
  Unpriced:        3
  Not applicable:  0
Recorded 5 results. Billed AI credits, BYOK USD, and local savings didn't change.
```

with `  Out of range:    <n>, left without a result` before the last line when there are any, `Dry run: nothing was written.` as the last line with `--dry-run`, and only `Every span has an API-equivalent result; nothing to backfill.` when no row lacks one. An invalid overlay prints `Invalid rates overlay: <message>` and exits 1 before anything is read.

### Code (copy exactly)

Edit 1 of 3 in `src/CopilotLedger.Cli/Rates/RatesCommands.cs`, replace:

```csharp
using System.CommandLine.Parsing;
using System.Text;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Rates;
```

with:

```csharp
using System.CommandLine.Parsing;
using System.Text;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Rates;
```

Edit 2 of 3 in `src/CopilotLedger.Cli/Rates/RatesCommands.cs`, replace:

```csharp
                RatesUpdate.RunAsync(context, cancellationToken));
        rates.Add(update);
        return rates;
    }
```

with:

```csharp
                RatesUpdate.RunAsync(context, cancellationToken));
        rates.Add(update);

        Command backfill = new(
            "backfill-api-equivalent",
            "Price the spans recorded without API-equivalent prices, at the rates in use.");
        Option<bool> dryRun = new("--dry-run")
        {
            Description = "Show what would be recorded without writing it.",
        };
        backfill.Add(dryRun);
        backfill.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                BackfillAsync(context, result.GetValue(dryRun), cancellationToken));
        rates.Add(backfill);
        return rates;
    }
```

Edit 3 of 3 in `src/CopilotLedger.Cli/Rates/RatesCommands.cs`, replace:

```csharp
        }
    }
}
```

with:

```csharp
        }
    }

    // Prices the legacy spans with the rates the daemon would load: the base rates and the overlay.
    private static async Task<int> BackfillAsync(
        CliContext context,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        RatesBase rates = RateCardFiles.LoadBase(context);
        if (rates.Warning is not null)
        {
            context.Out.WriteLine($"Warning: {rates.Warning}");
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

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        ApiEquivalentBackfill backfill =
            await store.BackfillApiEquivalentAsync(calculator, dryRun, cancellationToken);
        if (backfill.SpanCount == 0)
        {
            context.Out.WriteLine("Every span has an API-equivalent result; nothing to backfill.");
            return 0;
        }

        context.Out.WriteLine(
            $"{LedgerFormat.Count(backfill.SpanCount)} spans have no API-equivalent result. With rates {calculator.RatesVersion}:");
        context.Out.WriteLine(
            $"  Priced:          {LedgerFormat.Count(backfill.PricedSpanCount)} ({LedgerFormat.Usd(backfill.PricedUsd)})");
        context.Out.WriteLine($"  Unpriced:        {LedgerFormat.Count(backfill.UnpricedSpanCount)}");
        context.Out.WriteLine($"  Not applicable:  {LedgerFormat.Count(backfill.NotApplicableSpanCount)}");
        if (backfill.OutOfRangeSpanCount > 0)
        {
            context.Out.WriteLine(
                $"  Out of range:    {LedgerFormat.Count(backfill.OutOfRangeSpanCount)}, left without a result");
        }

        context.Out.WriteLine(
            dryRun
                ? "Dry run: nothing was written."
                : $"Recorded {LedgerFormat.Count(backfill.UpdatedSpanCount)} results. Billed AI credits, BYOK USD, and local savings didn't change.");
        return 0;
    }
}
```

New file `src/CopilotLedger.Storage/ApiEquivalentBackfill.cs`:

```csharp
namespace CopilotLedger.Storage;

// What SqliteLedgerStore.BackfillApiEquivalentAsync found: the spans recorded without an API-equivalent result, by the
// result the rate card gave them.
public sealed record ApiEquivalentBackfill
{
    public required long SpanCount { get; init; }               // spans without a result before the backfill
    public required long PricedSpanCount { get; init; }
    public required decimal PricedUsd { get; init; }            // the priced spans at API list prices
    public required long UnpricedSpanCount { get; init; }
    public required long NotApplicableSpanCount { get; init; }
    public required long OutOfRangeSpanCount { get; init; }     // priced beyond the pico-USD range; left without a result
    public required long UpdatedSpanCount { get; init; }        // rows written; 0 for a dry run
}
```

Edit 1 of 1 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
    }

    public async Task<IReadOnlyList<LedgerRollupRow>> GetRollupAsync(
        LedgerGrouping grouping,
```

with:

```csharp
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
                       input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens
                FROM spans
                WHERE api_equivalent_status IS NULL
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

    public async Task<IReadOnlyList<LedgerRollupRow>> GetRollupAsync(
        LedgerGrouping grouping,
```

New file `tests/CopilotLedger.Tests/Cli/RatesBackfillTests.cs`:

```csharp
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Cli;

public sealed class RatesBackfillTests
{
    private const string Legacy =
        "UPDATE spans SET (api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id, api_rates_version, api_rate_card_sha256) = (NULL, NULL, NULL, NULL, NULL);";

    private const string Results =
        "SELECT span_id, api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id, api_rates_version FROM spans ORDER BY span_id;";

    [Fact]
    public async Task RB1_DryRunCountsTheLegacySpansAndWritesNothing()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await LegacyHarnessAsync(temp.Path);
        string[] before = await RowsAsync(temp.Path, "SELECT * FROM spans ORDER BY id;");

        CliRun run = await harness.RunAsync("rates", "backfill-api-equivalent", "--dry-run");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            """
            5 spans have no API-equivalent result. With rates 2026-11-23:
              Priced:          2 ($3.16)
              Unpriced:        3
              Not applicable:  0
            Dry run: nothing was written.

            """.ReplaceLineEndings("\n"),
            run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(before, await RowsAsync(temp.Path, "SELECT * FROM spans ORDER BY id;"));
    }

    [Fact]
    public async Task RB2_BackfillRecordsEachResultOnce()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await LegacyHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("rates", "backfill-api-equivalent");
        CliRun again = await harness.RunAsync("rates", "backfill-api-equivalent");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            """
            5 spans have no API-equivalent result. With rates 2026-11-23:
              Priced:          2 ($3.16)
              Unpriced:        3
              Not applicable:  0
            Recorded 5 results. Billed AI credits, BYOK USD, and local savings didn't change.

            """.ReplaceLineEndings("\n"),
            run.Out);
        Assert.Equal(
            [
                "1111111111111111|2860000000000|priced|claude-opus-5.5|2026-11-23",
                "2222222222222221|null|unpriced|null|2026-11-23",
                "2222222222222222|null|unpriced|null|2026-11-23",
                "3333333333333333|300000000000|priced|gpt-6.1-sol|2026-11-23",
                "4444444444444444|null|unpriced|null|2026-11-23",
            ],
            await RowsAsync(temp.Path, Results));
        Assert.Equal(0, again.ExitCode);
        Assert.Equal("Every span has an API-equivalent result; nothing to backfill.\n", again.Out);
    }

    // The backfill prices with the rates the daemon would use, so an invalid overlay stops it before it writes.
    [Fact]
    public async Task RB3_AnInvalidOverlayStopsTheBackfill()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await LegacyHarnessAsync(temp.Path);
        string overlay = Path.Combine(temp.Path, "data", "rates.json");
        await File.WriteAllTextAsync(overlay, """{ "schemaVersion": 3 }""");

        CliRun run = await harness.RunAsync("rates", "backfill-api-equivalent");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal($"Invalid rates overlay: {overlay}: schemaVersion: must be 1 or 2\n", run.Error);
        Assert.Equal(
            5L,
            long.Parse(
                (await RowsAsync(temp.Path, "SELECT COUNT(*) FROM spans WHERE api_equivalent_status IS NULL;"))[0],
                System.Globalization.CultureInfo.InvariantCulture));
    }

    // The seeded ledger of ReportCommandTests, as recorded before schema version 5.
    private static async Task<CliHarness> LegacyHarnessAsync(string root)
    {
        CliHarness harness = new(root);
        SqliteLedgerStore store = new(Path.Combine(root, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        await LedgerSeeds.SeedChainsAsync(store, Path.Combine(root, "repo"), Path.Combine(root, "api"));
        await RowsAsync(root, Legacy);
        return harness;
    }

    // Each row as its values, NULL as "null", separated by '|'.
    private static async Task<string[]> RowsAsync(string root, string sql)
    {
        await using SqliteConnection connection = new($"Data Source={Path.Combine(root, "data", "ledger.db")}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        List<string> rows = [];
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join(
                '|',
                Enumerable.Range(0, reader.FieldCount)
                    .Select(index => reader.IsDBNull(index)
                        ? "null"
                        : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture))));
        }

        return [.. rows];
    }
}
```

New file `tests/CopilotLedger.Tests/Storage/ApiEquivalentBackfillTests.cs`:

```csharp
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class ApiEquivalentBackfillTests
{
    private const string ApiColumns =
        "api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id, api_rates_version, api_rate_card_sha256";

    // Every column except the five api_* columns.
    private const string RecordedColumns = """
        id, trace_id, span_id, parent_span_id, operation_name, conversation_id, agent_name, provider_name, request_model,
        response_model, model_id, rate_card_model_id, tier_name, server_address, server_port, start_time_unix_ms,
        end_time_unix_ms, input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens, billing_channel,
        pricing_status, rates_version, cost_usd_pico, saved_usd_pico, repo_root, repo_slug, branch_name, head_commit_sha,
        recorded_at_unix_ms, cache_read_state, cache_write_state, pull_request_number, agent_span_id
        """;

    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    // Legacy spans get the results ingestion gives new ones; a dry run reports them and writes nothing.
    [Fact]
    public async Task AB1_Backfill_GivesLegacySpansTheResultsIngestionWould()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(ApiEquivalentStoreTests.Entries());
        await using SqliteConnection connection = await OpenAsync(temp);
        string[] ingested = await RowsAsync(connection, $"SELECT {ApiColumns} FROM spans ORDER BY id;");
        await ExecuteAsync(connection, $"UPDATE spans SET ({ApiColumns}) = (NULL, NULL, NULL, NULL, NULL);");
        string[] legacy = await RowsAsync(connection, "SELECT * FROM spans ORDER BY id;");
        string[] recorded = await RowsAsync(connection, $"SELECT {RecordedColumns} FROM spans ORDER BY id;");

        ApiEquivalentBackfill dryRun = await store.BackfillApiEquivalentAsync(Retail, dryRun: true);

        AssertBackfill(dryRun, 6, 4, 1.8757m, 1, 1, 0, 0);
        Assert.Equal(legacy, await RowsAsync(connection, "SELECT * FROM spans ORDER BY id;"));

        ApiEquivalentBackfill backfill = await store.BackfillApiEquivalentAsync(Retail, dryRun: false);

        AssertBackfill(backfill, 6, 4, 1.8757m, 1, 1, 0, 6);
        Assert.Equal(ingested, await RowsAsync(connection, $"SELECT {ApiColumns} FROM spans ORDER BY id;"));
        Assert.Equal(recorded, await RowsAsync(connection, $"SELECT {RecordedColumns} FROM spans ORDER BY id;"));
        AssertBackfill(await store.BackfillApiEquivalentAsync(Retail, dryRun: false), 0, 0, 0m, 0, 0, 0, 0);
    }

    // Only rows without a result change, and only in the api_* columns: they record the backfill's rates while
    // rates_version and every billed amount keep what was recorded.
    [Fact]
    public async Task AB2_Backfill_FillsOnlyRowsWithoutAResult()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(ApiEquivalentStoreTests.Entries());
        await using SqliteConnection connection = await OpenAsync(temp);
        await ExecuteAsync(
            connection,
            $"UPDATE spans SET ({ApiColumns}) = (NULL, NULL, NULL, NULL, NULL) WHERE span_id IN ('a000000000000001', 'a000000000000005');");
        string[] before = await RowsAsync(connection, $"SELECT {RecordedColumns} FROM spans ORDER BY id;");
        string[] untouched = await RowsAsync(
            connection,
            $"SELECT {ApiColumns} FROM spans WHERE api_equivalent_status IS NOT NULL ORDER BY id;");
        JsonRateCardCalculator builtIn = JsonRateCardCalculator.LoadEmbedded();

        ApiEquivalentBackfill backfill = await store.BackfillApiEquivalentAsync(builtIn, dryRun: false);

        AssertBackfill(backfill, 2, 1, 0.3m, 1, 0, 0, 2);
        Assert.Equal(before, await RowsAsync(connection, $"SELECT {RecordedColumns} FROM spans ORDER BY id;"));
        Assert.Equal(
            untouched,
            await RowsAsync(
                connection,
                $"SELECT {ApiColumns} FROM spans WHERE span_id NOT IN ('a000000000000001', 'a000000000000005') ORDER BY id;"));
        Assert.Equal(
            [
                $"a000000000000001|retail-fixture|null|unpriced|null|2026-11-23|{builtIn.ApiRateCardSha256}",
                $"a000000000000005|retail-fixture|300000000000|priced|gpt-6.1-sol|2026-11-23|{builtIn.ApiRateCardSha256}",
            ],
            await RowsAsync(
                connection,
                $"SELECT span_id, rates_version, {ApiColumns} FROM spans WHERE span_id IN ('a000000000000001', 'a000000000000005') ORDER BY id;"));
    }

    [Fact]
    public async Task AB3_Backfill_LeavesPricesBeyondTheRangeWithoutAResult()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(ApiEquivalentStoreTests.Entries());
        await using SqliteConnection connection = await OpenAsync(temp);
        await ExecuteAsync(
            connection,
            $"""
            UPDATE spans SET ({ApiColumns}) = (NULL, NULL, NULL, NULL, NULL), input_tokens = 10000000
            WHERE span_id = 'a000000000000002';
            """);
        JsonRateCardCalculator huge = JsonRateCardCalculator.Parse(
            RateFixtures.RetailTestRatesJson,
            """
            {
              "schemaVersion": 2,
              "apiEquivalent": {
                "models": [
                  { "id": "test-unknown", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1000000, "outputPer1M": 1000000 } ] } ] }
                ]
              }
            }
            """);

        ApiEquivalentBackfill backfill = await store.BackfillApiEquivalentAsync(huge, dryRun: false);

        AssertBackfill(backfill, 1, 0, 0m, 0, 0, 1, 0);
        Assert.Equal(
            1L,
            await ScalarAsync(connection, "SELECT COUNT(*) FROM spans WHERE api_equivalent_status IS NULL;"));
    }

    private static void AssertBackfill(
        ApiEquivalentBackfill backfill,
        long spans,
        long priced,
        decimal pricedUsd,
        long unpriced,
        long notApplicable,
        long outOfRange,
        long updated)
    {
        Assert.Equal(spans, backfill.SpanCount);
        Assert.Equal(priced, backfill.PricedSpanCount);
        Assert.Equal(pricedUsd, backfill.PricedUsd);
        Assert.Equal(unpriced, backfill.UnpricedSpanCount);
        Assert.Equal(notApplicable, backfill.NotApplicableSpanCount);
        Assert.Equal(outOfRange, backfill.OutOfRangeSpanCount);
        Assert.Equal(updated, backfill.UpdatedSpanCount);
    }

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        return store;
    }

    private static async Task<SqliteConnection> OpenAsync(TempDirectory temp)
    {
        SqliteConnection connection = new($"Data Source={Path.Combine(temp.Path, "ledger.db")}");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    // Each row as its values, NULL as "null", separated by '|'.
    private static async Task<string[]> RowsAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        List<string> rows = [];
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join(
                '|',
                Enumerable.Range(0, reader.FieldCount)
                    .Select(index => reader.IsDBNull(index)
                        ? "null"
                        : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture))));
        }

        return [.. rows];
    }
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Storage/ApiEquivalentStoreTests.cs`, replace:

```csharp
    // priced as the card's local baseline. T3: a Foundry BYOK request, and an unrecognized endpoint whose model has a list
    // price. Only the first request reported cache counters.
    private static LedgerEntry[] Entries() =>
    [
        Entry(T1, "a000000000000001", TestSpans.Chat(
```

with:

```csharp
    // priced as the card's local baseline. T3: a Foundry BYOK request, and an unrecognized endpoint whose model has a list
    // price. Only the first request reported cache counters.
    internal static LedgerEntry[] Entries() =>
    [
        Entry(T1, "a000000000000001", TestSpans.Chat(
```

Edit 1 of 1 in `docs/cli.md`, replace:

```markdown
- `gh ledger rates validate [--file <path>]` checks the overlay, or another file, with the same rules the daemon uses.
- `gh ledger rates update` downloads `rates.v2.json` from the latest gh-ledger release, or its `rates.json` if it has none, and keeps it when it's newer than the rates in use, has API list prices (`schemaVersion` 2), and your overlay still applies. It uses your system proxy. Restart the daemon afterward; it reads rates when it starts.

See [configuration.md](configuration.md) for pricing, the overlay format, and where files are kept.
```

with:

```markdown
- `gh ledger rates validate [--file <path>]` checks the overlay, or another file, with the same rules the daemon uses.
- `gh ledger rates update` downloads `rates.v2.json` from the latest gh-ledger release, or its `rates.json` if it has none, and keeps it when it's newer than the rates in use, has API list prices (`schemaVersion` 2), and your overlay still applies. It uses your system proxy. Restart the daemon afterward; it reads rates when it starts.
- `gh ledger rates backfill-api-equivalent [--dry-run]` gives the requests recorded before gh-ledger had API list prices their API-equivalent prices, with the rates in use, and prints how many were priced. It changes nothing else, and `--dry-run` writes nothing. See [API-equivalent prices](configuration.md#api-equivalent-prices).

See [configuration.md](configuration.md) for pricing, the overlay format, and where files are kept.
```

Edit 1 of 1 in `docs/configuration.md`, replace:

```markdown
- Cache reads and writes are priced at the list prices for them, as billing is. A request that reported no cache counters is priced as if all of its input were uncached, so its API-equivalent cost is an estimate, and reports mark it as one.

The built-in API list prices are each vendor's public prices on 2026-11-23 for the 31 GitHub models that have them, for standard processing on the vendor's global endpoint: not batch, flex, priority, fast mode, or regional endpoints. Each model's `sourceUrl` is its vendor's price page. Where a list price differs from GitHub's rate, as Grok 4.5's cached input and GPT-5.6 Luna's long-context threshold do, the list price is used. Where the vendors' pages leave a choice open:

- MAI Code 2, MAI Code 2 Flash, and MAI Code 1.1 Flash have no public API list price, so their requests are unpriced in this view. So are local requests, because their baseline is MAI Code 2; to price them, set `apiEquivalent.localBaselineModel` in your overlay.
```

with:

```markdown
- Cache reads and writes are priced at the list prices for them, as billing is. A request that reported no cache counters is priced as if all of its input were uncached, so its API-equivalent cost is an estimate, and reports mark it as one.

Each request keeps the API-equivalent result it was recorded with, along with the rates version and the SHA-256 of the API list prices that gave it. Requests recorded before gh-ledger had API list prices have none, and reports count them as unpriced until you run `gh ledger rates backfill-api-equivalent`. It prices them with the rates in use, your overlay included, as of each request's start time, and records that rates version and hash beside the request's billing rates version. It never changes what a request was billed or saved, and it skips requests that already have a result. With `--dry-run`, it shows the counts and writes nothing. A request whose API-equivalent cost would be beyond the ledger's range of about $9.2 million keeps no result; CopilotLedger doesn't record a new request with such a cost at all.

The built-in API list prices are each vendor's public prices on 2026-11-23 for the 31 GitHub models that have them, for standard processing on the vendor's global endpoint: not batch, flex, priority, fast mode, or regional endpoints. Each model's `sourceUrl` is its vendor's price page. Where a list price differs from GitHub's rate, as Grok 4.5's cached input and GPT-5.6 Luna's long-context threshold do, the list price is used. Where the vendors' pages leave a choice open:

- MAI Code 2, MAI Code 2 Flash, and MAI Code 1.1 Flash have no public API list price, so their requests are unpriced in this view. So are local requests, because their baseline is MAI Code 2; to price them, set `apiEquivalent.localBaselineModel` in your overlay.
```

### Tests

- **AB1**: after `NULL`ing every result of `Entries()`, a dry run reports 6 spans, 4 priced (`1.8757`), 1 unpriced, and 1 not applicable, and changes no byte of any row; the backfill then restores exactly what ingestion recorded, changes no other column, and a second backfill finds nothing.
- **AB2**: with two legacy rows and the built-in card, only those two change, to `unpriced` and `priced` (`300000000000`) with version `2026-11-23` and the built-in hash, while their `rates_version` stays `retail-fixture` and every other row and column is unchanged.
- **AB3**: a cost beyond the range stays `NULL` and counts as out of range.
- **RB1–RB3**: the exact dry-run output and an unchanged ledger; the exact backfill output, each row's result, and `nothing to backfill` the second time; an invalid overlay stops the backfill.

### Acceptance criteria

- `--dry-run` writes nothing; without it, only rows with `api_equivalent_status IS NULL` change, and only in the five `api_*` columns, recording the card's own version and hash (AB1, AB2, RB1, RB2).
- `dotnet test CopilotLedger.slnx` passes **955** tests (949 + 6).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 7 specification: What-If Pricing in the Store

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | `GetRollupAsync` and `GetTopPromptChainsAsync` overloads with `whatIf`; `RollupKey`, `RepriceAsync`, `GroupKey`, `RetailTotals` |
| `tests/CopilotLedger.Tests/Storage/WhatIfPricingTests.cs` | New: WI1–WI4 |

### Contracts

```csharp
// The existing signatures delegate with whatIf: null.
public Task<IReadOnlyList<LedgerRollupRow>> GetRollupAsync(
    LedgerGrouping grouping, LedgerFilter filter, JsonRateCardCalculator? whatIf, CancellationToken cancellationToken = default);
public Task<IReadOnlyList<PromptChainRow>> GetTopPromptChainsAsync(
    LedgerFilter filter, int limit, JsonRateCardCalculator? whatIf, CancellationToken cancellationToken = default);
```

### Algorithm

1. Open one deferred transaction, so every query reads the same spans.
2. With `whatIf`, stream the matching spans (`RepriceAsync`): the group key columns (`RollupKey`, the same expressions as `GetRollupSql`, or `trace_id` for prompt chains), the operation, channel, model id, start time, token counts, and whether `ReportedSql` holds. Price each with `whatIf.CalculateApiEquivalent` and add it to its group's `RetailTotals`: an amount (`decimal`) and an estimate count, or an unpriced count.
3. Run the existing query. Each row takes its three API-equivalent fields from its group's `RetailTotals` (looked up by `GroupKey`, each key value prefixed with its length so keys can't collide), or from the recorded sums without `whatIf`.

### Code (copy exactly)

Edit 1 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
    }

    public async Task<IReadOnlyList<LedgerRollupRow>> GetRollupAsync(
        LedgerGrouping grouping,
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
```

with:

```csharp
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
```

Edit 2 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        EnsureInitialized();

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = GetRollupSql(grouping);
        AddFilterParameters(command, filter);
```

with:

```csharp
        EnsureInitialized();

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);

        // One read transaction, so both queries see the same spans.
        await using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
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
```

Edit 3 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            decimal aiCreditsUsd =
                PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("github_usd_pico")));
            rows.Add(new LedgerRollupRow
            {
```

with:

```csharp
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
```

Edit 4 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                LocalSavedUsd =
                    PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("saved_usd_pico"))),
                ApiEquivalentUsd = ReadUsd(reader, "api_equivalent_usd_pico"),
                ApiEquivalentUnpricedSpanCount =
                    reader.GetInt64(reader.GetOrdinal("api_equivalent_unpriced_span_count")),
                ApiEquivalentEstimatedSpanCount =
                    reader.GetInt64(reader.GetOrdinal("api_equivalent_estimated_span_count")),
            });
        }
```

with:

```csharp
                LocalSavedUsd =
                    PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("saved_usd_pico"))),
                ApiEquivalentUsd = retail.Usd,
                ApiEquivalentUnpricedSpanCount = retail.UnpricedSpanCount,
                ApiEquivalentEstimatedSpanCount = retail.EstimatedSpanCount,
            });
        }
```

Edit 5 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
    }

    // Prompt chains (one per trace_id), most expensive first. Throws ArgumentOutOfRangeException unless 1 <= limit <= 1000.
    public async Task<IReadOnlyList<PromptChainRow>> GetTopPromptChainsAsync(
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
```

with:

```csharp
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
```

Edit 6 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        List<PromptChainData> data = [];
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = $"""
                SELECT trace_id,
```

with:

```csharp
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);

        // One read transaction, so every query sees the same spans.
        await using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
        List<PromptChainData> data = [];
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = snapshot;
            query.CommandText = $"""
                SELECT trace_id,
```

Edit 7 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            _ => new List<string>(),
            StringComparer.Ordinal);
        await using (SqliteCommand query = connection.CreateCommand())
        {
            string traceParameters = string.Join(
                ", ",
                Enumerable.Range(0, data.Count).Select(index => $"@t{index}"));
            query.CommandText = $"""
                SELECT DISTINCT trace_id, agent_name, model_id
```

with:

```csharp
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
```

Edit 8 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                  AND trace_id IN ({traceParameters});
                """;
            AddFilterParameters(query, filter);
            for (int index = 0; index < data.Count; index++)
            {
                query.Parameters.AddWithValue($"@t{index}", data[index].TraceId);
            }

            await using SqliteDataReader reader =
```

with:

```csharp
                  AND trace_id IN ({traceParameters});
                """;
            AddTraceParameters(query);

            await using SqliteDataReader reader =
```

Edit 9 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        }

        List<PromptChainRow> rows = [];
        foreach (PromptChainData item in data)
        {
            decimal aiCreditsUsd = PicoUsd.ToUsd(item.GitHubUsdPico);
            rows.Add(new PromptChainRow
            {
```

with:

```csharp
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
```

Edit 10 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                FoundryByokUsd = PicoUsd.ToUsd(item.ByokUsdPico),
                LocalSavedUsd = PicoUsd.ToUsd(item.SavedUsdPico),
                ApiEquivalentUsd = item.ApiEquivalentUsd,
                ApiEquivalentUnpricedSpanCount = item.ApiEquivalentUnpricedSpanCount,
                ApiEquivalentEstimatedSpanCount = item.ApiEquivalentEstimatedSpanCount,
            });
        }
```

with:

```csharp
                FoundryByokUsd = PicoUsd.ToUsd(item.ByokUsdPico),
                LocalSavedUsd = PicoUsd.ToUsd(item.SavedUsdPico),
                ApiEquivalentUsd = retail.Usd,
                ApiEquivalentUnpricedSpanCount = retail.UnpricedSpanCount,
                ApiEquivalentEstimatedSpanCount = retail.EstimatedSpanCount,
            });
        }
```

Edit 11 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
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
```

with:

```csharp
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
```

Edit 12 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
    };

    private static BillingChannel ToBillingChannel(string value) => value switch
    {
```

with:

```csharp
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
            ApiEquivalentCost cost = whatIf.CalculateApiEquivalent(new ApiEquivalentRequest
            {
                OperationName = reader.GetString(reader.GetOrdinal("what_if_operation")),
                Channel = ToBillingChannel(reader.GetString(reader.GetOrdinal("what_if_channel"))),
                ModelId = reader.IsDBNull(model) ? null : reader.GetString(model),
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
```

Edit 13 of 13 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        string? GitCommitSha);

    private sealed record PromptChainData(
        string TraceId,
```

with:

```csharp
        string? GitCommitSha);

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
```

New file `tests/CopilotLedger.Tests/Storage/WhatIfPricingTests.cs`:

```csharp
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class WhatIfPricingTests
{
    private const string Legacy =
        "UPDATE spans SET (api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id, api_rates_version, api_rate_card_sha256) = (NULL, NULL, NULL, NULL, NULL);";

    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    // Pricing again with the card the spans were recorded with gives back exactly the recorded rollup.
    [Theory]
    [InlineData(LedgerGrouping.Repository)]
    [InlineData(LedgerGrouping.Branch)]
    [InlineData(LedgerGrouping.Model)]
    [InlineData(LedgerGrouping.Agent)]
    [InlineData(LedgerGrouping.ChannelModel)]
    public async Task WI1_TheRecordingCard_ReproducesTheRecordedRollup(LedgerGrouping grouping)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);

        Assert.Equal(
            await store.GetRollupAsync(grouping, LedgerFilter.All),
            await store.GetRollupAsync(grouping, LedgerFilter.All, Retail));
    }

    // Another card changes only the API-equivalent fields.
    [Fact]
    public async Task WI2_AnotherCard_ChangesOnlyTheApiEquivalentFields()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        IReadOnlyList<LedgerRollupRow> recorded = await store.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All);

        IReadOnlyList<LedgerRollupRow> whatIf = await store.GetRollupAsync(
            LedgerGrouping.Model,
            LedgerFilter.All,
            JsonRateCardCalculator.LoadEmbedded());

        Assert.Equal(
            [
                recorded[0] with { ApiEquivalentUsd = 0.3m, ApiEquivalentUnpricedSpanCount = 0, ApiEquivalentEstimatedSpanCount = 1 },
                recorded[1] with { ApiEquivalentUsd = null, ApiEquivalentUnpricedSpanCount = 3, ApiEquivalentEstimatedSpanCount = 0 },
                recorded[2] with { ApiEquivalentUsd = null, ApiEquivalentUnpricedSpanCount = 1, ApiEquivalentEstimatedSpanCount = 0 },
                recorded[3] with { ApiEquivalentUsd = null, ApiEquivalentUnpricedSpanCount = 1, ApiEquivalentEstimatedSpanCount = 0 },
            ],
            whatIf);
        Assert.Equal(["gpt-6.1-sol", "test-retail", "qwen-local", "test-unknown"], whatIf.Select(row => row.Model));
    }

    // Spans recorded without a result are priced too, and nothing is written.
    [Fact]
    public async Task WI3_LegacySpans_ArePricedWithoutWriting()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await RowsAsync(temp, Legacy);
        string[] before = await RowsAsync(temp, "SELECT * FROM spans ORDER BY id;");

        LedgerRollupRow recorded = Assert.Single(
            await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
        LedgerRollupRow whatIf = Assert.Single(
            await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All, Retail));
        IReadOnlyList<PromptChainRow> chains =
            await store.GetTopPromptChainsAsync(LedgerFilter.All, 10, Retail);

        Assert.Null(recorded.ApiEquivalentUsd);
        Assert.Equal(6, recorded.ApiEquivalentUnpricedSpanCount);
        Assert.Equal(
            recorded with { ApiEquivalentUsd = 1.8757m, ApiEquivalentUnpricedSpanCount = 2, ApiEquivalentEstimatedSpanCount = 3 },
            whatIf);
        Assert.Collection(
            chains,
            row => AssertChain(row, "a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3", 0.3045m, 0, 2),
            row => AssertChain(row, "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1", 1.56m, 1, 0),
            row => AssertChain(row, "a2a2a2a2a2a2a2a2a2a2a2a2a2a2a2a2", 0.0112m, 1, 1));
        Assert.Equal(before, await RowsAsync(temp, "SELECT * FROM spans ORDER BY id;"));
    }

    // The chains, their order, and every recorded amount don't depend on the card.
    [Fact]
    public async Task WI4_PromptChains_KeepTheirOrderAndRecordedAmounts()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        LedgerFilter filter = new() { Repository = "(none)" };

        IReadOnlyList<PromptChainRow> recorded = await store.GetTopPromptChainsAsync(filter, 2);
        IReadOnlyList<PromptChainRow> whatIf =
            await store.GetTopPromptChainsAsync(filter, 2, JsonRateCardCalculator.LoadEmbedded());

        Assert.Equal(recorded.Select(Recorded), whatIf.Select(Recorded));
        Assert.Collection(
            recorded,
            row => AssertChain(row, "a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3", 0.3045m, 0, 2),
            row => AssertChain(row, "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1", 1.56m, 1, 0));
        Assert.Collection(
            whatIf,
            row => AssertChain(row, "a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3", 0.3m, 1, 1),
            row => AssertChain(row, "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1", null, 2, 0));
    }

    private static string Recorded(PromptChainRow row) =>
        string.Join(
            '|',
            row.TraceId,
            string.Join(',', row.Agents),
            string.Join(',', row.Models),
            row.SpanCount,
            row.UnpricedSpanCount,
            row.InputTokens,
            row.OutputTokens,
            row.AiCredits,
            row.AiCreditsUsd,
            row.FoundryByokUsd,
            row.LocalSavedUsd);

    private static void AssertChain(
        PromptChainRow row,
        string traceId,
        decimal? usd,
        long unpriced,
        long estimated)
    {
        Assert.Equal(traceId, row.TraceId);
        Assert.Equal(usd, row.ApiEquivalentUsd);
        Assert.Equal(unpriced, row.ApiEquivalentUnpricedSpanCount);
        Assert.Equal(estimated, row.ApiEquivalentEstimatedSpanCount);
    }

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        await store.RecordAsync(ApiEquivalentStoreTests.Entries());
        return store;
    }

    // Each row as its values, NULL as "null", separated by '|'.
    private static async Task<string[]> RowsAsync(TempDirectory temp, string sql)
    {
        await using SqliteConnection connection = new($"Data Source={Path.Combine(temp.Path, "ledger.db")}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        List<string> rows = [];
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join(
                '|',
                Enumerable.Range(0, reader.FieldCount)
                    .Select(index => reader.IsDBNull(index)
                        ? "null"
                        : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture))));
        }

        return [.. rows];
    }
}
```

### Tests

- **WI1** (5 groupings): repricing with the card the spans were recorded with gives exactly the recorded rollup.
- **WI2**: the built-in card changes only the three API-equivalent fields of each model row (`0.3`; `null` with 3 unpriced; and so on).
- **WI3**: after the results are `NULL`ed, recorded totals are `null` with 6 unpriced, and what-if totals equal ingestion's, `1.8757`, for the rollup and each prompt chain; no row changes.
- **WI4**: prompt chains keep their order and recorded amounts under another card.

### Acceptance criteria

- What-if pricing changes only the API-equivalent fields and writes nothing (WI2, WI3); it agrees with ingestion (WI1).
- `dotnet test CopilotLedger.slnx` passes **963** tests (955 + 8).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 8 specification: API-Equivalent USD in Reports

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs` | `ApiEquivalent` cells and `WriteApiEquivalent` JSON |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | The column, footnotes, the `status` lens table, and JSON fields |
| `tests/CopilotLedger.Tests/Cli/ApiEquivalentReportTests.cs` | New: AE1–AE5 |
| `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs` | RP4, RP6, RP7, `StatusText`, and RP15's last four JSON names |
| `docs/cli.md`, `docs/guide.md` | "API-equivalent USD"; the `status-first`, `status`, `branch`, `top-prompts`, and `agents` samples and their explanation |

### Display rules

| Group | Cell |
|---|---|
| Spans, none priced | `Unpriced` |
| No spans | `$0.00` |
| Every span priced, all with cache counters | `$12.34` |
| Some spans unpriced | `$12.34*` |
| Some priced spans without cache counters | `~$12.34` |
| Both | `~$12.34*` |

Amounts round to cents half away from zero (`LedgerFormat.Usd`, invariant culture). After a table whose cells use a mark, a blank line and, in this order:

```text
<n> of <m spans>[ in the last 30 days] had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
<n> priced span(s)[ in the last 30 days] reported no cache counters, so all of its/their input is priced as uncached; ~ marks an estimate.
```

The counts are the table's sums; in `status`, those of the last 30 days, which include the other periods, with the bracketed words.

### `status`

The spend table gains the column, and a second table follows it, from a `ChannelModel` rollup of each period:

```text
Period        GitHub USD  At API prices  GitHub/API  Local saved  Local at API prices
```

`GitHub USD` and `At API prices` cover the GitHub-billed requests; `GitHub/API` is `AiCredits × 0.01 / ApiEquivalentUsd` of the same requests, rounded half away from zero to two decimal places with an `x` (and `~` for an estimate), or `n/a` unless every one of them is priced in both views and the amount isn't 0. `Local saved` and `Local at API prices` cover the local requests. The footnotes follow this table and cover both.

### JSON

- `status` periods, `branch`, `agents`, and `top-prompts` rows add `apiEquivalentUsd` (exact, or `null`), `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans` right after `localSavedUsd`, through `LedgerFormat.WriteApiEquivalent`; `WriteTotals` doesn't change, so receipts don't yet.
- `status` periods add `lenses` before `cache`: `copilot` (`spans`, `unpricedSpans`, `aiCredits`, `aiCreditsUsd`, the three fields, `billedToApiRatio`, the exact ratio or `null`) and `local` (`spans`, `localSavedUsd`, the three fields).

### Code (copy exactly)

Edit 1 of 2 in `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs`, replace:

```csharp
        value.ToString("N0", CultureInfo.InvariantCulture);

    // ratio as a percentage with one decimal place, rounded half away from zero; n/a for null, never 0.0%.
    public static string Percent(decimal? ratio) =>
```

with:

```csharp
        value.ToString("N0", CultureInfo.InvariantCulture);

    // An API-equivalent amount: Unpriced when nothing was priced (usd is null), never $0.00. Otherwise the amount, with "~"
    // before it when it's an estimate (estimatedSpans reported no cache counters, so all of their input was priced as
    // uncached) and "*" after it when it leaves out unpricedSpans, which have no price.
    public static string ApiEquivalent(decimal? usd, long unpricedSpans, long estimatedSpans) =>
        usd is null
            ? "Unpriced"
            : $"{(estimatedSpans > 0 ? "~" : "")}{Usd(usd.Value)}{(unpricedSpans > 0 ? "*" : "")}";

    public static string ApiEquivalent(LedgerRollupRow row) =>
        ApiEquivalent(row.ApiEquivalentUsd, row.ApiEquivalentUnpricedSpanCount, row.ApiEquivalentEstimatedSpanCount);

    public static string ApiEquivalent(PromptChainRow row) =>
        ApiEquivalent(row.ApiEquivalentUsd, row.ApiEquivalentUnpricedSpanCount, row.ApiEquivalentEstimatedSpanCount);

    // ratio as a percentage with one decimal place, rounded half away from zero; n/a for null, never 0.0%.
    public static string Percent(decimal? ratio) =>
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs`, replace:

```csharp
    }

    // value with every digit decimal division gave it, or null. Used for ratios and averages.
    public static void WriteDecimal(
```

with:

```csharp
    }

    // The API-equivalent fields that report rows have after WriteTotals.
    public static void WriteApiEquivalent(Utf8JsonWriter writer, LedgerRollupRow row) =>
        WriteApiEquivalent(
            writer,
            row.ApiEquivalentUsd,
            row.ApiEquivalentUnpricedSpanCount,
            row.ApiEquivalentEstimatedSpanCount);

    public static void WriteApiEquivalent(Utf8JsonWriter writer, PromptChainRow row) =>
        WriteApiEquivalent(
            writer,
            row.ApiEquivalentUsd,
            row.ApiEquivalentUnpricedSpanCount,
            row.ApiEquivalentEstimatedSpanCount);

    // apiEquivalentUsd (null when nothing was priced, never 0 for unknown prices), apiEquivalentUnpricedSpans, and
    // apiEquivalentEstimatedSpans.
    public static void WriteApiEquivalent(
        Utf8JsonWriter writer,
        decimal? usd,
        long unpricedSpans,
        long estimatedSpans)
    {
        if (usd is null)
        {
            writer.WriteNull("apiEquivalentUsd");
        }
        else
        {
            WriteMoney(writer, "apiEquivalentUsd", usd.Value);
        }

        writer.WriteNumber("apiEquivalentUnpricedSpans", unpricedSpans);
        writer.WriteNumber("apiEquivalentEstimatedSpans", estimatedSpans);
    }

    // value with every digit decimal division gave it, or null. Used for ratios and averages.
    public static void WriteDecimal(
```

Edit 1 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        new("GitHub USD", true),
        new("BYOK USD", true),
        new("Local saved", true),
    ];
```

with:

```csharp
        new("GitHub USD", true),
        new("BYOK USD", true),
        new("API equiv USD", true),
        new("Local saved", true),
    ];
```

Edit 2 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            ("Last 30 days", RequirePeriod(context, "30d")),
        ];
        List<(string Label, Period Period, LedgerRollupRow Total)> totals = [];
        foreach ((string label, Period period) in periods)
        {
            IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
                LedgerGrouping.Repository,
                new LedgerFilter { From = period.From },
                cancellationToken);
            totals.Add((label, period, Sum(rows)));
        }

        if (json)
```

with:

```csharp
            ("Last 30 days", RequirePeriod(context, "30d")),
        ];
        List<(string Label, Period Period, StatusTotals Totals)> totals = [];
        foreach ((string label, Period period) in periods)
        {
            IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
                LedgerGrouping.ChannelModel,
                new LedgerFilter { From = period.From },
                cancellationToken);
            totals.Add((label, period, new StatusTotals(
                Sum(rows),
                Sum([.. rows.Where(row => row.Channel == BillingChannel.GitHubAiCredits)]),
                Sum([.. rows.Where(row => row.Channel == BillingChannel.Local)]))));
        }

        if (json)
```

Edit 3 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            [new("Period", false), .. MoneyColumns],
            totals.Select(item =>
                (IReadOnlyList<string>)[item.Label, .. MoneyCells(item.Total)]).ToArray());
        context.Out.WriteLine();
        TableWriter.Write(
```

with:

```csharp
            [new("Period", false), .. MoneyColumns],
            totals.Select(item =>
                (IReadOnlyList<string>)[item.Label, .. MoneyCells(item.Totals.All)]).ToArray());
        context.Out.WriteLine();
        string[][] lenses = totals.Select(item => new[]
        {
            item.Label,
            LedgerFormat.Usd(item.Totals.Copilot.AiCreditsUsd),
            LedgerFormat.ApiEquivalent(item.Totals.Copilot),
            BilledToApi(item.Totals.Copilot),
            LedgerFormat.Usd(item.Totals.Local.LocalSavedUsd),
            LedgerFormat.ApiEquivalent(item.Totals.Local),
        }).ToArray();
        TableWriter.Write(
            context.Out,
            [
                new("Period", false),
                new("GitHub USD", true),
                new("At API prices", true),
                new("GitHub/API", true),
                new("Local saved", true),
                new("Local at API prices", true),
            ],
            lenses);
        LedgerRollupRow widest = totals[^1].Totals.All;
        WriteApiEquivalentFootnotes(
            context,
            [
                .. totals.Select(item => LedgerFormat.ApiEquivalent(item.Totals.All)),
                .. lenses.SelectMany(row => new[] { row[2], row[5] }),
            ],
            widest.SpanCount,
            widest.ApiEquivalentUnpricedSpanCount,
            widest.ApiEquivalentEstimatedSpanCount,
            " in the last 30 days");

        context.Out.WriteLine();
        TableWriter.Write(
```

Edit 4 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                [
                    item.Label,
                    LedgerFormat.Percent(item.Total.CacheHitRatio),
                    $"{LedgerFormat.Count(item.Total.CacheReportedSpanCount)} of {Spans(item.Total.SpanCount)}",
                ]).ToArray());
        return 0;
```

with:

```csharp
                [
                    item.Label,
                    LedgerFormat.Percent(item.Totals.All.CacheHitRatio),
                    $"{LedgerFormat.Count(item.Totals.All.CacheReportedSpanCount)} of {Spans(item.Totals.All.SpanCount)}",
                ]).ToArray());
        return 0;
```

Edit 5 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        }).ToArray();
        TableWriter.Write(context.Out, [.. leading, .. MoneyColumns], tableRows);
        return 0;
    }
```

with:

```csharp
        }).ToArray();
        TableWriter.Write(context.Out, [.. leading, .. MoneyColumns], tableRows);
        WriteApiEquivalentFootnotes(
            context,
            [.. rows.Select(LedgerFormat.ApiEquivalent)],
            rows.Sum(row => row.SpanCount),
            rows.Sum(row => row.ApiEquivalentUnpricedSpanCount),
            rows.Sum(row => row.ApiEquivalentEstimatedSpanCount),
            string.Empty);
        return 0;
    }
```

Edit 6 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            new("GitHub USD", true),
            new("BYOK USD", true),
            new("Local saved", true),
            new("Trace", false),
```

with:

```csharp
            new("GitHub USD", true),
            new("BYOK USD", true),
            new("API equiv USD", true),
            new("Local saved", true),
            new("Trace", false),
```

Edit 7 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                LedgerFormat.Usd(row.AiCreditsUsd),
                LedgerFormat.Usd(row.FoundryByokUsd),
                LedgerFormat.Usd(row.LocalSavedUsd),
                row.TraceId,
            ]).ToArray();
        TableWriter.Write(context.Out, columns, tableRows);
        return 0;
    }
```

with:

```csharp
                LedgerFormat.Usd(row.AiCreditsUsd),
                LedgerFormat.Usd(row.FoundryByokUsd),
                LedgerFormat.ApiEquivalent(row),
                LedgerFormat.Usd(row.LocalSavedUsd),
                row.TraceId,
            ]).ToArray();
        TableWriter.Write(context.Out, columns, tableRows);
        WriteApiEquivalentFootnotes(
            context,
            [.. rows.Select(LedgerFormat.ApiEquivalent)],
            rows.Sum(row => row.SpanCount),
            rows.Sum(row => row.ApiEquivalentUnpricedSpanCount),
            rows.Sum(row => row.ApiEquivalentEstimatedSpanCount),
            string.Empty);
        return 0;
    }
```

Edit 8 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        bool enabled,
        IReadOnlyList<string> warnings,
        IReadOnlyList<(string Label, Period Period, LedgerRollupRow Total)> totals,
        bool extension,
        bool downloadedRates)
```

with:

```csharp
        bool enabled,
        IReadOnlyList<string> warnings,
        IReadOnlyList<(string Label, Period Period, StatusTotals Totals)> totals,
        bool extension,
        bool downloadedRates)
```

Edit 9 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        writer.WriteEndArray();
        writer.WriteStartArray("periods");
        foreach ((string _, Period period, LedgerRollupRow total) in totals)
        {
            writer.WriteStartObject();
            writer.WriteString("label", period.Label);
            LedgerFormat.WriteTime(writer, "from", period.From);
            LedgerFormat.WriteTotals(writer, total);
            writer.WritePropertyName("cache");
            writer.WriteStartObject();
```

with:

```csharp
        writer.WriteEndArray();
        writer.WriteStartArray("periods");
        foreach ((string _, Period period, StatusTotals periodTotals) in totals)
        {
            LedgerRollupRow total = periodTotals.All;
            writer.WriteStartObject();
            writer.WriteString("label", period.Label);
            LedgerFormat.WriteTime(writer, "from", period.From);
            LedgerFormat.WriteTotals(writer, total);
            LedgerFormat.WriteApiEquivalent(writer, total);
            writer.WritePropertyName("lenses");
            writer.WriteStartObject();
            writer.WritePropertyName("copilot");
            writer.WriteStartObject();
            writer.WriteNumber("spans", periodTotals.Copilot.SpanCount);
            writer.WriteNumber("unpricedSpans", periodTotals.Copilot.UnpricedSpanCount);
            LedgerFormat.WriteMoney(writer, "aiCredits", periodTotals.Copilot.AiCredits);
            LedgerFormat.WriteMoney(writer, "aiCreditsUsd", periodTotals.Copilot.AiCreditsUsd);
            LedgerFormat.WriteApiEquivalent(
                writer,
                periodTotals.Copilot.ApiEquivalentUsd,
                periodTotals.Copilot.ApiEquivalentUnpricedSpanCount,
                periodTotals.Copilot.ApiEquivalentEstimatedSpanCount);
            LedgerFormat.WriteDecimal(writer, "billedToApiRatio", BilledToApiRatio(periodTotals.Copilot));
            writer.WriteEndObject();
            writer.WritePropertyName("local");
            writer.WriteStartObject();
            writer.WriteNumber("spans", periodTotals.Local.SpanCount);
            LedgerFormat.WriteMoney(writer, "localSavedUsd", periodTotals.Local.LocalSavedUsd);
            LedgerFormat.WriteApiEquivalent(
                writer,
                periodTotals.Local.ApiEquivalentUsd,
                periodTotals.Local.ApiEquivalentUnpricedSpanCount,
                periodTotals.Local.ApiEquivalentEstimatedSpanCount);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WritePropertyName("cache");
            writer.WriteStartObject();
```

Edit 10 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            }

            LedgerFormat.WriteTotals(writer, row);
            LedgerFormat.WriteCacheCoverage(writer, row);
            writer.WriteEndObject();
```

with:

```csharp
            }

            LedgerFormat.WriteTotals(writer, row);
            LedgerFormat.WriteApiEquivalent(writer, row);
            LedgerFormat.WriteCacheCoverage(writer, row);
            writer.WriteEndObject();
```

Edit 11 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            writer.WriteEndArray();
            LedgerFormat.WriteTotals(writer, row);
            writer.WriteEndObject();
        }
```

with:

```csharp
            writer.WriteEndArray();
            LedgerFormat.WriteTotals(writer, row);
            LedgerFormat.WriteApiEquivalent(writer, row);
            writer.WriteEndObject();
        }
```

Edit 12 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        LedgerFormat.Usd(row.AiCreditsUsd),
        LedgerFormat.Usd(row.FoundryByokUsd),
        LedgerFormat.Usd(row.LocalSavedUsd),
    ];
```

with:

```csharp
        LedgerFormat.Usd(row.AiCreditsUsd),
        LedgerFormat.Usd(row.FoundryByokUsd),
        LedgerFormat.ApiEquivalent(row),
        LedgerFormat.Usd(row.LocalSavedUsd),
    ];
```

Edit 13 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        $"{LedgerFormat.Count(count)} {(count == 1 ? "span" : "spans")}";

    private static string Requests(long count) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? "request" : "requests")}";
```

with:

```csharp
        $"{LedgerFormat.Count(count)} {(count == 1 ? "span" : "spans")}";

    // A blank line and the footnotes for a table's API-equivalent cells: one when a cell has "*", then one when a cell has
    // "~". spans, unpricedSpans, and estimatedSpans are the table's totals, for the spans that scope names.
    private static void WriteApiEquivalentFootnotes(
        CliContext context,
        IReadOnlyList<string> cells,
        long spans,
        long unpricedSpans,
        long estimatedSpans,
        string scope)
    {
        bool partial = cells.Any(cell => cell.EndsWith('*'));
        bool estimate = cells.Any(cell => cell.StartsWith('~'));
        if (!partial && !estimate)
        {
            return;
        }

        context.Out.WriteLine();
        if (partial)
        {
            context.Out.WriteLine(
                $"{LedgerFormat.Count(unpricedSpans)} of {Spans(spans)}{scope} had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.");
        }

        if (estimate)
        {
            context.Out.WriteLine(
                $"{LedgerFormat.Count(estimatedSpans)} priced {(estimatedSpans == 1 ? "span" : "spans")}{scope} reported no cache counters, so all of {(estimatedSpans == 1 ? "its" : "their")} input is priced as uncached; ~ marks an estimate.");
        }
    }

    private static string Requests(long count) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? "request" : "requests")}";
```

Edit 14 of 14 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
    private static Period RequirePeriod(CliContext context, string value) =>
        ParsePeriod(context, value);
}
```

with:

```csharp
    private static Period RequirePeriod(CliContext context, string value) =>
        ParsePeriod(context, value);

    // GitHub-billed USD (AI credits at $0.01 each) per USD of the same requests at API list prices. null unless both views
    // priced every one of them and the API-equivalent amount isn't 0.
    private static decimal? BilledToApiRatio(LedgerRollupRow copilot) =>
        copilot.SpanCount > 0
        && copilot.UnpricedSpanCount == 0
        && copilot.ApiEquivalentUnpricedSpanCount == 0
        && copilot.ApiEquivalentUsd is decimal api
        && api != 0m
            ? copilot.AiCredits * AiCreditMath.UsdPerAiCredit / api
            : null;

    // BilledToApiRatio with two decimal places, rounded half away from zero, as 1.25x, after "~" when the API-equivalent amount
    // is an estimate; n/a when there's no ratio.
    private static string BilledToApi(LedgerRollupRow copilot) =>
        BilledToApiRatio(copilot) is decimal ratio
            ? $"{(copilot.ApiEquivalentEstimatedSpanCount > 0 ? "~" : "")}{decimal.Round(ratio, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture)}x"
            : "n/a";

    // A status period's totals: All its spans, the GitHub-billed (Copilot) ones, and the Local ones.
    private sealed record StatusTotals(LedgerRollupRow All, LedgerRollupRow Copilot, LedgerRollupRow Local);
}
```

New file `tests/CopilotLedger.Tests/Cli/ApiEquivalentReportTests.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class ApiEquivalentReportTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-21T14:00:00Z", CultureInfo.InvariantCulture);

    private static ManualTimeProvider Time => new(
        DateTimeOffset.Parse("2026-11-21T15:00:00Z", CultureInfo.InvariantCulture),
        ManualTimeProvider.UtcMinus5);

    [Fact]
    public async Task AE1_BranchTableAndJsonShowEachRule()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path, Branches(big: false));

        CliRun text = await harness.RunAsync("branch", "--all-repos", "--since", "all");
        CliRun json = await harness.RunAsync("branch", "--all-repos", "--since", "all", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            """
            Branches in all repositories (all time)

            Repository   Branch    Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
            contoso/app  estimate      1         0        0.00       $0.00     $0.32         ~$0.30        $0.00
            contoso/app  partial       2         1        7.40       $0.07     $0.00         $0.22*        $0.00
            contoso/app  priced        1         0        7.40       $0.07     $0.00          $0.22        $0.00
            contoso/app  both          2         1        1.50       $0.02     $0.00        ~$0.05*        $0.00
            contoso/app  unpriced      1         1        0.00       $0.00     $0.00       Unpriced        $0.00

            3 of 7 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
            2 priced spans reported no cache counters, so all of their input is priced as uncached; ~ marks an estimate.

            """.ReplaceLineEndings("\n"),
            text.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement[] rows = document.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(
            ["0.3|0|1", "0.222|1|0", "0.222|0|0", "0.045|1|1", "null|1|0"],
            rows.Select(row => string.Join(
                '|',
                row.GetProperty("apiEquivalentUsd").ValueKind == JsonValueKind.Null
                    ? "null"
                    : row.GetProperty("apiEquivalentUsd").GetRawText(),
                row.GetProperty("apiEquivalentUnpricedSpans").GetInt64(),
                row.GetProperty("apiEquivalentEstimatedSpans").GetInt64())));
        Assert.Equal(
            ["localSavedUsd", "apiEquivalentUsd", "apiEquivalentUnpricedSpans", "apiEquivalentEstimatedSpans", "cacheReportedSpans"],
            rows[0].EnumerateObject().Select(property => property.Name).SkipWhile(name => name != "localSavedUsd").Take(5));
        Assert.Contains("\"apiEquivalentUsd\": null,", json.Out);
    }

    [Fact]
    public async Task AE2_TopPromptsShowTheColumnAndJsonFields()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path, Branches(big: false));

        CliRun text = await harness.RunAsync("top-prompts", "--since", "all", "--limit", "3");
        CliRun json = await harness.RunAsync("top-prompts", "--since", "all", "--limit", "3", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            """
            Top 3 prompts by cost in all repositories (all time)

            #  Started           Repository   Agents  Models                     Spans  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved  Trace
            1  2026-11-21 09:00  contoso/app  -       gpt-6.1-sol                    1        0.00       $0.00     $0.32         ~$0.30        $0.00  44444444444444444444444444444444
            2  2026-11-21 09:00  contoso/app  -       test-retail                    1        7.40       $0.07     $0.00          $0.22        $0.00  11111111111111111111111111111111
            3  2026-11-21 09:00  contoso/app  -       test-retail, test-unknown      2        7.40       $0.07     $0.00         $0.22*        $0.00  22222222222222222222222222222222

            1 of 4 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
            1 priced span reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.

            """.ReplaceLineEndings("\n"),
            text.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement partial = document.RootElement.GetProperty("rows")[2];
        Assert.Equal(
            ["localSavedUsd", "apiEquivalentUsd", "apiEquivalentUnpricedSpans", "apiEquivalentEstimatedSpans"],
            partial.EnumerateObject().Select(property => property.Name).TakeLast(4));
        Assert.Equal(0.222m, partial.GetProperty("apiEquivalentUsd").GetDecimal());
        Assert.Equal(1, partial.GetProperty("apiEquivalentUnpricedSpans").GetInt64());
    }

    // With every GitHub-billed request priced both ways, status shows their ratio; local requests compare their savings with
    // their list prices.
    [Fact]
    public async Task AE3_StatusComparesTheSameRequestsInEachView()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path, Lenses());

        CliRun text = await harness.RunAsync("status");
        CliRun json = await harness.RunAsync("status", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            """
            Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
            Today             2         0        7.40       $0.07     $0.00         ~$0.23        $0.01
            Last 7 days       2         0        7.40       $0.07     $0.00         ~$0.23        $0.01
            Last 30 days      2         0        7.40       $0.07     $0.00         ~$0.23        $0.01

            Period        GitHub USD  At API prices  GitHub/API  Local saved  Local at API prices
            Today              $0.07          $0.22       0.33x        $0.01               ~$0.01
            Last 7 days        $0.07          $0.22       0.33x        $0.01               ~$0.01
            Last 30 days       $0.07          $0.22       0.33x        $0.01               ~$0.01

            1 priced span in the last 30 days reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.

            Period        Cache hit  Cache reported
            Today             40.0%  1 of 2 spans
            Last 7 days       40.0%  1 of 2 spans
            Last 30 days      40.0%  1 of 2 spans

            """.ReplaceLineEndings("\n"),
            text.Out[(text.Out.IndexOf("\n\n", StringComparison.Ordinal) + 2)..]);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement period = document.RootElement.GetProperty("periods")[0];
        Assert.Equal(
            ["localSavedUsd", "apiEquivalentUsd", "apiEquivalentUnpricedSpans", "apiEquivalentEstimatedSpans", "lenses", "cache"],
            period.EnumerateObject().Select(property => property.Name).TakeLast(6));
        Assert.Equal(
            """
            {
                    "copilot": {
                      "spans": 1,
                      "unpricedSpans": 0,
                      "aiCredits": 7.4,
                      "aiCreditsUsd": 0.074,
                      "apiEquivalentUsd": 0.222,
                      "apiEquivalentUnpricedSpans": 0,
                      "apiEquivalentEstimatedSpans": 0,
                      "billedToApiRatio": 0.3333333333333333333333333333
                    },
                    "local": {
                      "spans": 1,
                      "localSavedUsd": 0.0075,
                      "apiEquivalentUsd": 0.0112,
                      "apiEquivalentUnpricedSpans": 0,
                      "apiEquivalentEstimatedSpans": 1
                    }
                  }
            """.ReplaceLineEndings("\n"),
            period.GetProperty("lenses").GetRawText().ReplaceLineEndings("\n"));
    }

    // The ratio needs every GitHub-billed request priced in both views.
    [Fact]
    public async Task AE4_StatusLeavesOutTheRatioWhenARequestHasNoPrice()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path, Branches(big: false));

        CliRun json = await harness.RunAsync("status", "--json");

        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement copilot = document.RootElement.GetProperty("periods")[0].GetProperty("lenses").GetProperty("copilot");
        Assert.Equal(3, copilot.GetProperty("spans").GetInt64());
        Assert.Equal(1, copilot.GetProperty("unpricedSpans").GetInt64());
        Assert.Equal(0.444m, copilot.GetProperty("apiEquivalentUsd").GetDecimal());
        Assert.Equal(1, copilot.GetProperty("apiEquivalentUnpricedSpans").GetInt64());
        Assert.Equal(JsonValueKind.Null, copilot.GetProperty("billedToApiRatio").ValueKind);
    }

    [Fact]
    public async Task AE5_OutputIsTheSameUnderGermanCulture()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path, Branches(big: true));
        string[][] commands =
        [
            ["status"],
            ["status", "--json"],
            ["branch", "--all-repos", "--since", "all"],
            ["branch", "--all-repos", "--since", "all", "--json"],
            ["agents", "--since", "all"],
            ["agents", "--since", "all", "--json"],
            ["top-prompts", "--since", "all"],
            ["top-prompts", "--since", "all", "--json"],
        ];
        List<string> invariant = [];
        List<string> german = [];
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            foreach (string[] command in commands)
            {
                invariant.Add((await harness.RunAsync(command)).Out);
            }

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            foreach (string[] command in commands)
            {
                german.Add((await harness.RunAsync(command)).Out);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }

        Assert.Equal(invariant, german);
        Assert.Contains("~$1,050.03*", german[2]);
        Assert.Contains("\"apiEquivalentUsd\": 1050.031845,", german[3]);
        Assert.Contains("\"billedToApiRatio\": null", german[1]);
    }

    // Branch, trace, and span of the AE1, AE2, AE4, and AE5 requests, priced with RateFixtures.RetailTestRatesJson. Each
    // request is its own trace, except that the two of "partial" share one. big makes one request cost over $1,000.
    private static IReadOnlyList<(string Branch, string Trace, NormalizedGenAiSpan Span)> Branches(bool big) =>
    [
        ("priced", "1", TestSpans.Chat(
            startTime: Start,
            requestModel: "test-retail",
            inputTokens: 100_000,
            cacheReadInputTokens: 40_000,
            outputTokens: 2_000)),
        ("partial", "2", TestSpans.Chat(
            startTime: Start,
            requestModel: "test-retail",
            inputTokens: 100_000,
            cacheReadInputTokens: 40_000,
            outputTokens: 2_000)),
        ("partial", "2", TestSpans.Chat(
            startTime: Start,
            requestModel: "test-unknown",
            inputTokens: 1_000,
            outputTokens: 100)),
        ("estimate", "4", TestSpans.Chat(
            startTime: Start,
            serverAddress: "contoso.openai.azure.com",
            requestModel: "gpt-6.1-sol",
            inputTokens: 100_000,
            outputTokens: 10_000)),
        ("unpriced", "5", TestSpans.Chat(
            startTime: Start.AddDays(-40),
            requestModel: "test-unknown",
            inputTokens: 1_000,
            outputTokens: 100)),
        ("both", "6", TestSpans.Chat(
            startTime: Start.AddDays(-40),
            requestModel: "test-retail",
            inputTokens: 10_000,
            outputTokens: big ? 70_000_123 : 1_000)),
        ("both", "7", TestSpans.Chat(
            startTime: Start.AddDays(-40),
            requestModel: "test-unknown",
            inputTokens: 1_000,
            outputTokens: 100)),
    ];

    // A GitHub request with cache reads, and a local one, both priced in every view.
    private static IReadOnlyList<(string Branch, string Trace, NormalizedGenAiSpan Span)> Lenses() =>
    [
        ("main", "1", TestSpans.Chat(
            startTime: Start,
            requestModel: "test-retail",
            inputTokens: 100_000,
            cacheReadInputTokens: 40_000,
            outputTokens: 2_000)),
        ("main", "2", TestSpans.Chat(
            startTime: Start,
            providerName: "ollama",
            requestModel: "qwen-local",
            inputTokens: 10_000,
            outputTokens: 1_000)),
    ];

    private static async Task<CliHarness> SeededHarnessAsync(
        string root,
        IReadOnlyList<(string Branch, string Trace, NormalizedGenAiSpan Span)> seed)
    {
        CliHarness harness = new(root, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(Path.Combine(root, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        JsonRateCardCalculator retail = JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);
        string repository = Path.Combine(root, "app");
        int index = 0;
        await store.RecordAsync(seed.Select(item =>
        {
            NormalizedGenAiSpan span = item.Span with
            {
                TraceId = new string(item.Trace[0], 32),
                SpanId = $"{++index:x16}",
            };
            GitWorkspaceContext git = new(
                repository,
                Path.Combine(repository, ".git"),
                item.Branch,
                "0123456789abcdef0123456789abcdef01234567",
                "https://github.com/contoso/app.git",
                "contoso/app");
            return new LedgerEntry(span, retail.Calculate(span), git);
        }));
        return harness;
    }
}
```

Edit 1 of 5 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
            Branches in swigerb/copilot-ledger (last 30 days)

            Branch          Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
            main                1         0      286.00       $2.86     $0.00        $0.00
            feature/ledger      2         0       56.00       $0.56     $0.00        $0.23

            """),
```

with:

```csharp
            Branches in swigerb/copilot-ledger (last 30 days)

            Branch          Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
            main                1         0      286.00       $2.86     $0.00          $2.86        $0.00
            feature/ledger      2         0       56.00       $0.56     $0.00       Unpriced        $0.23

            """),
```

Edit 2 of 5 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
            Agents in all repositories (all time)

            Agent             Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
            ledger-architect      1         0      286.00       $2.86     $0.00        $0.00
            ledger-builder        3         1       56.00       $0.56     $0.00        $0.23
            (none)                1         0        0.00       $0.00     $0.32        $0.00

            """),
```

with:

```csharp
            Agents in all repositories (all time)

            Agent             Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
            ledger-architect      1         0      286.00       $2.86     $0.00          $2.86        $0.00
            ledger-builder        3         1       56.00       $0.56     $0.00       Unpriced        $0.23
            (none)                1         0        0.00       $0.00     $0.32       Unpriced        $0.00

            """),
```

Edit 3 of 5 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
            Top 2 prompts by cost in all repositories (all time)

            #  Started           Repository              Agents            Models                   Spans  AI credits  GitHub USD  BYOK USD  Local saved  Trace
            1  2026-11-21 09:00  swigerb/copilot-ledger  ledger-architect  claude-opus-5.5              1      286.00       $2.86     $0.00        $0.00  11111111111111111111111111111111
            2  2026-11-21 08:00  swigerb/copilot-ledger  ledger-builder    mai-code-2, qwen3.8-27b      2       56.00       $0.56     $0.00        $0.23  22222222222222222222222222222222

            """),
```

with:

```csharp
            Top 2 prompts by cost in all repositories (all time)

            #  Started           Repository              Agents            Models                   Spans  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved  Trace
            1  2026-11-21 09:00  swigerb/copilot-ledger  ledger-architect  claude-opus-5.5              1      286.00       $2.86     $0.00          $2.86        $0.00  11111111111111111111111111111111
            2  2026-11-21 08:00  swigerb/copilot-ledger  ledger-builder    mai-code-2, qwen3.8-27b      2       56.00       $0.56     $0.00       Unpriced        $0.23  22222222222222222222222222222222

            """),
```

Edit 4 of 5 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
    {
        Assert.Equal(
            ["localSavedUsd", "cacheReportedSpans", "cacheUnreportedSpans", "cacheHitRatio"],
            row.EnumerateObject().Select(property => property.Name).TakeLast(4));
        Assert.Equal(reported, row.GetProperty("cacheReportedSpans").GetInt64());
```

with:

```csharp
    {
        Assert.Equal(
            ["apiEquivalentEstimatedSpans", "cacheReportedSpans", "cacheUnreportedSpans", "cacheHitRatio"],
            row.EnumerateObject().Select(property => property.Name).TakeLast(4));
        Assert.Equal(reported, row.GetProperty("cacheReportedSpans").GetInt64());
```

Edit 5 of 5 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
        Copilot: OTel export not enabled; run: gh-ledger install

        Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
        Today             3         0      342.00       $3.42     $0.00        $0.23
        Last 7 days       4         0      342.00       $3.42     $0.32        $0.23
        Last 30 days      4         0      342.00       $3.42     $0.32        $0.23

        Period        Cache hit  Cache reported
```

with:

```csharp
        Copilot: OTel export not enabled; run: gh-ledger install

        Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
        Today             3         0      342.00       $3.42     $0.00         $2.86*        $0.23
        Last 7 days       4         0      342.00       $3.42     $0.32         $2.86*        $0.23
        Last 30 days      4         0      342.00       $3.42     $0.32         $2.86*        $0.23

        Period        GitHub USD  At API prices  GitHub/API  Local saved  Local at API prices
        Today              $3.42         $2.86*         n/a        $0.23             Unpriced
        Last 7 days        $3.42         $2.86*         n/a        $0.23             Unpriced
        Last 30 days       $3.42         $2.86*         n/a        $0.23             Unpriced

        3 of 4 spans in the last 30 days had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.

        Period        Cache hit  Cache reported
```

Edit 1 of 2 in `docs/cli.md`, replace:

```markdown
| Command | Reports |
|---|---|
| `gh ledger status [--json]` | The daemon, ledger, rates, Copilot's export setting, warnings, and totals and cache hit ratios for today and the last 7 and 30 days |
| `gh ledger branch [--repo <key> \| --all-repos] [--since <period>] [--json]` | Spend by branch in the current repository, or in another or every repository; outside a Git repository, every repository |
| `gh ledger agents [--repo <key>] [--since <period>] [--json]` | Spend by agent in every repository, or in one with `--repo` |
```

with:

```markdown
| Command | Reports |
|---|---|
| `gh ledger status [--json]` | The daemon, ledger, rates, Copilot's export setting, warnings, and totals, billed amounts compared with API list prices, and cache hit ratios for today and the last 7 and 30 days |
| `gh ledger branch [--repo <key> \| --all-repos] [--since <period>] [--json]` | Spend by branch in the current repository, or in another or every repository; outside a Git repository, every repository |
| `gh ledger agents [--repo <key>] [--since <period>] [--json]` | Spend by agent in every repository, or in one with `--repo` |
```

Edit 2 of 2 in `docs/cli.md`, replace:

```markdown
- `--since` takes `today`, `all`, `1d` through `3650d`, or a `yyyy-MM-dd` date; the default is `30d`.
- Tables round money to cents; JSON keeps exact amounts.
- Spend is split into GitHub AI credits (`AI credits` and `GitHub USD`), Foundry BYOK (`BYOK USD`), and local models, whose `Local saved` is the cloud spend they avoided. `Unpriced` counts requests without a price; see [configuration.md](configuration.md).
- An agent shows as `(none)` when Copilot reports no agent name, as its default agent does. Built-in subagents such as `explore` report their own names, and a custom agent reports the `name` in its `.agent.md` file, or the file's name without `.agent.md`.
- `status --json` adds a `cache` object to each period, and each row of `branch --json` and `agents --json` adds `cacheReportedSpans`, `cacheUnreportedSpans`, and `cacheHitRatio`. See [Prompt-cache efficiency](#prompt-cache-efficiency).

## Prompt-cache efficiency

Copilot reports how many of each request's input tokens the model read from its prompt cache and how many it wrote to it. Cache reads cost much less than uncached input, so a falling hit ratio, or a context that grows on every request, raises spend without changing the result. For a walkthrough, see [Find context bloat](guide.md#find-context-bloat).
```

with:

```markdown
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

## Prompt-cache efficiency

Copilot reports how many of each request's input tokens the model read from its prompt cache and how many it wrote to it. Cache reads cost much less than uncached input, so a falling hit ratio, or a context that grows on every request, raises spend without changing the result. For a walkthrough, see [Find context bloat](guide.md#find-context-bloat).
```

Edit 1 of 5 in `docs/guide.md`, replace:

```markdown
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
Today             1         0        7.40       $0.07     $0.00        $0.00
Last 7 days       1         0        7.40       $0.07     $0.00        $0.00
Last 30 days      1         0        7.40       $0.07     $0.00        $0.00

Period        Cache hit  Cache reported
```

with:

```markdown
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
Today             1         0        7.40       $0.07     $0.00          $0.07        $0.00
Last 7 days       1         0        7.40       $0.07     $0.00          $0.07        $0.00
Last 30 days      1         0        7.40       $0.07     $0.00          $0.07        $0.00

Period        GitHub USD  At API prices  GitHub/API  Local saved  Local at API prices
Today              $0.07          $0.07       1.00x        $0.00                $0.00
Last 7 days        $0.07          $0.07       1.00x        $0.00                $0.00
Last 30 days       $0.07          $0.07       1.00x        $0.00                $0.00

Period        Cache hit  Cache reported
```

Edit 2 of 5 in `docs/guide.md`, replace:

```markdown
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
Today             8         0      617.75       $6.18     $0.49        $0.17
Last 7 days      10         0      764.00       $7.64     $0.49        $0.17
Last 30 days     11         0      908.00       $9.08     $0.49        $0.17

Period        Cache hit  Cache reported
```

with:

```markdown
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
Today             8         0      617.75       $6.18     $0.49        ~$6.49*        $0.17
Last 7 days      10         0      764.00       $7.64     $0.49        ~$7.69*        $0.17
Last 30 days     11         0      908.00       $9.08     $0.49        ~$9.13*        $0.17

Period        GitHub USD  At API prices  GitHub/API  Local saved  Local at API prices
Today              $6.18         $5.99*         n/a        $0.17             Unpriced
Last 7 days        $7.64         $7.19*         n/a        $0.17             Unpriced
Last 30 days       $9.08         $8.63*         n/a        $0.17             Unpriced

3 of 11 spans in the last 30 days had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
1 priced span in the last 30 days reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.

Period        Cache hit  Cache reported
```

Edit 3 of 5 in `docs/guide.md`, replace:

````markdown
Branches in contoso/webapp (last 30 days)

Branch             Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
feature/search         8         0      617.75       $6.18     $0.49        $0.17
main                   2         0      170.25       $1.70     $0.00        $0.00
fix/login-timeout      1         0      120.00       $1.20     $0.00        $0.00
```
<!-- /sample -->

- `AI credits` and `GitHub USD` are what GitHub bills; 1 AI credit is $0.01. `BYOK USD` is what Microsoft Foundry bills you directly, and `Local saved` is what local models saved you. `Unpriced` counts requests that have no price.
- `branch` covers the current repository. Add `--all-repos` for every repository, `--since` for another period, such as `today`, `7d`, `all`, or a date, and `--json` for exact amounts.

## Find the most expensive prompts
````

with:

````markdown
Branches in contoso/webapp (last 30 days)

Branch             Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
feature/search         8         0      617.75       $6.18     $0.49        ~$6.49*        $0.17
main                   2         0      170.25       $1.70     $0.00         $1.44*        $0.00
fix/login-timeout      1         0      120.00       $1.20     $0.00          $1.20        $0.00

3 of 11 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
1 priced span reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.
```
<!-- /sample -->

- `AI credits` and `GitHub USD` are what GitHub bills; 1 AI credit is $0.01. `BYOK USD` is what Microsoft Foundry bills you directly, and `Local saved` is what local models saved you. `Unpriced` counts requests that have no price.
- `API equiv USD` is what the same requests would cost at each model's API list price, for comparison only. It shows `Unpriced` when no request had a list price, `*` when some had none and are left out, such as MAI models and local models, and `~` when it's an estimate because some requests didn't report their cache use. See [API-equivalent prices](configuration.md#api-equivalent-prices).
- In `status`, the second table compares what GitHub billed with the same requests at API list prices, and local savings with the local requests at API list prices. `GitHub/API` is their ratio, shown only when every GitHub request has both prices.
- `branch` covers the current repository. Add `--all-repos` for every repository, `--since` for another period, such as `today`, `7d`, `all`, or a date, and `--json` for exact amounts.

## Find the most expensive prompts
````

Edit 4 of 5 in `docs/guide.md`, replace:

````markdown
Top 3 prompts by cost in all repositories (last 30 days)

#  Started           Repository      Agents       Models           Spans  AI credits  GitHub USD  BYOK USD  Local saved  Trace
1  2026-11-21 11:20  contoso/webapp  -            claude-opus-5.5      4      504.00       $5.04     $0.00        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2
2  2026-11-03 10:30  contoso/webapp  explore      claude-opus-5.5      1      144.00       $1.44     $0.00        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00a7
3  2026-11-19 10:00  contoso/webapp  code-review  claude-opus-5.5      1      120.00       $1.20     $0.00        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00e5
```
<!-- /sample -->
````

with:

````markdown
Top 3 prompts by cost in all repositories (last 30 days)

#  Started           Repository      Agents       Models           Spans  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved  Trace
1  2026-11-21 11:20  contoso/webapp  -            claude-opus-5.5      4      504.00       $5.04     $0.00          $5.04        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2
2  2026-11-03 10:30  contoso/webapp  explore      claude-opus-5.5      1      144.00       $1.44     $0.00          $1.44        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00a7
3  2026-11-19 10:00  contoso/webapp  code-review  claude-opus-5.5      1      120.00       $1.20     $0.00          $1.20        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00e5
```
<!-- /sample -->
````

Edit 5 of 5 in `docs/guide.md`, replace:

````markdown
Agents in all repositories (last 30 days)

Agent        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
(none)           8         0      625.25       $6.25     $0.49        $0.17
explore          2         0      162.75       $1.63     $0.00        $0.00
code-review      1         0      120.00       $1.20     $0.00        $0.00
```
<!-- /sample -->
````

with:

````markdown
Agents in all repositories (last 30 days)

Agent        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
(none)           8         0      625.25       $6.25     $0.49        ~$6.49*        $0.17
explore          2         0      162.75       $1.63     $0.00         $1.44*        $0.00
code-review      1         0      120.00       $1.20     $0.00          $1.20        $0.00

3 of 11 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
1 priced span reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.
```
<!-- /sample -->
````

### Tests

`ApiEquivalentReportTests` records requests on five branches of `contoso/app` with `RetailTestRatesJson`: `priced` (cache reads, `$0.22`), `partial` (that and an unknown model), `estimate` (Foundry BYOK without cache counters), `unpriced` (an unknown model), and `both` (a listed model without cache counters, and an unknown one).

- **AE1**: `branch --all-repos --since all` shows every rule and both footnotes exactly; its JSON has `0.3|0|1`, `0.222|1|0`, `0.222|0|0`, `0.045|1|1`, and `null|1|0`, in that order, right after `localSavedUsd`.
- **AE2**: `top-prompts` shows the column and footnotes; its JSON rows end with the three fields.
- **AE3**: with a GitHub request and a local one, both priced, `status` shows `0.33x` and the estimate footnote, and `lenses` is exact, with `billedToApiRatio` `0.3333333333333333333333333333`.
- **AE4**: a GitHub request without a price makes `billedToApiRatio` `null`.
- **AE5**: eight `status`, `branch`, `agents`, and `top-prompts` outputs, text and JSON, are identical under the invariant culture and `de-DE`, including `~$1,050.03*` and `1050.031845`.
- `StatusText`, RP4, RP6, and RP7 show the column (`$2.86`, `Unpriced`, `$2.86*`), the lens table, and the footnote; RP15's rows end with `apiEquivalentEstimatedSpans` before the cache fields. The guide samples change to the outputs in the code.

### Acceptance criteria

- The four reports show `API equiv USD` with the `Unpriced`, partial, estimate, and priced rules, and their JSON has `apiEquivalentUsd` and `apiEquivalentUnpricedSpans` (AE1–AE4, RP4–RP7).
- `status` compares the same GitHub-billed requests in both views and shows the ratio only when it's complete (AE3, AE4); nothing adds retail or avoided amounts to billed spend.
- Output doesn't depend on the current culture (AE5).
- `dotnet test CopilotLedger.slnx` passes **968** tests (963 + 5).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 9 specification: API-Equivalent USD in Receipts

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs` | The `API-equivalent (retail)` line and column; the three fields in the JSON data |
| `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs` | `FeatureReceipt`, `EmptyReceipt`, and EP10's size; new EP11 and EP12 |
| `docs/cli.md`, `docs/guide.md` | The receipt's new line and column; the `receipt` sample |

### Output

- After the `Not priced` line (or `Local offload` when there's none) and before `Circuit breaker`: `- **API-equivalent (retail):** ` and then `none` (no spans); `Unpriced (<n turns> without an API-equivalent price)` (no turn priced); or `$<amount> at API list prices`, then ` • <n turns> unpriced` and ` • estimated: <n turns> reported no cache counters` when they apply.
- The details table's header is `| Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | API-equivalent | Cloud spend avoided |`, with one more `---:`. Each model's cell is `Unpriced`, `$<amount>`, or `$<amount> (partial)`, `(estimate)`, or `(partial, estimate)`.
- The JSON data adds `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans` after `localSavedUsd` in `totals` and in each model; `schemaVersion` stays 1.

### Code (copy exactly)

Edit 1 of 6 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
            writer.WriteStartObject();
            LedgerFormat.WriteTotals(writer, receipt.Totals);
            writer.WriteEndObject();
            writer.WriteNumber("prompts", receipt.Prompts);
```

with:

```csharp
            writer.WriteStartObject();
            LedgerFormat.WriteTotals(writer, receipt.Totals);
            LedgerFormat.WriteApiEquivalent(writer, receipt.Totals);
            writer.WriteEndObject();
            writer.WriteNumber("prompts", receipt.Prompts);
```

Edit 2 of 6 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
                writer.WriteString("model", row.Model);
                LedgerFormat.WriteTotals(writer, row);
                writer.WriteEndObject();
            }
```

with:

```csharp
                writer.WriteString("model", row.Model);
                LedgerFormat.WriteTotals(writer, row);
                LedgerFormat.WriteApiEquivalent(writer, row);
                writer.WriteEndObject();
            }
```

Edit 3 of 6 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
        }

        text.Append("- **Circuit breaker:** ").Append(Plural(receipt.BreakerTrips, "trip", "trips"))
            .Append(" • max single-prompt burn: ").Append(LedgerFormat.Credits(receipt.MaxPromptCredits))
```

with:

```csharp
        }

        text.Append("- **API-equivalent (retail):** ").Append(ApiEquivalentSummary(receipt)).Append('\n');
        text.Append("- **Circuit breaker:** ").Append(Plural(receipt.BreakerTrips, "trip", "trips"))
            .Append(" • max single-prompt burn: ").Append(LedgerFormat.Credits(receipt.MaxPromptCredits))
```

Edit 4 of 6 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
        else
        {
            text.Append("| Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | Cloud spend avoided |\n");
            text.Append("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |\n");
            foreach (LedgerRollupRow row in receipt.Models)
            {
```

with:

```csharp
        else
        {
            text.Append("| Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | API-equivalent | Cloud spend avoided |\n");
            text.Append("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |\n");
            foreach (LedgerRollupRow row in receipt.Models)
            {
```

Edit 5 of 6 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
                    .Append(" | ").Append(LedgerFormat.Count(row.OutputTokens))
                    .Append(" | ").Append(Cost(row))
                    .Append(" | ").Append(LedgerFormat.Usd(row.LocalSavedUsd))
                    .Append(" |\n");
```

with:

```csharp
                    .Append(" | ").Append(LedgerFormat.Count(row.OutputTokens))
                    .Append(" | ").Append(Cost(row))
                    .Append(" | ").Append(ApiEquivalentCell(row))
                    .Append(" | ").Append(LedgerFormat.Usd(row.LocalSavedUsd))
                    .Append(" |\n");
```

Edit 6 of 6 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
    };

    private static LedgerRollupRow[] Rows(PullRequestReceipt receipt, BillingChannel channel) =>
        receipt.Models.Where(row => row.Channel == channel).ToArray();
```

with:

```csharp
    };

    // The receipt's API-equivalent line: none without spans, and Unpriced when no turn has an API-equivalent price. Otherwise
    // the amount at API list prices, then the turns it leaves out and the turns that make it an estimate, in words rather than
    // the report marks, because * and ~ are Markdown syntax.
    private static string ApiEquivalentSummary(PullRequestReceipt receipt)
    {
        LedgerRollupRow totals = receipt.Totals;
        if (receipt.Models.Count == 0)
        {
            return "none";
        }

        if (totals.ApiEquivalentUsd is not decimal usd)
        {
            return $"Unpriced ({Plural(totals.SpanCount, "turn", "turns")} without an API-equivalent price)";
        }

        StringBuilder line = new();
        line.Append(LedgerFormat.Usd(usd)).Append(" at API list prices");
        if (totals.ApiEquivalentUnpricedSpanCount > 0)
        {
            line.Append(" • ").Append(Plural(totals.ApiEquivalentUnpricedSpanCount, "turn", "turns")).Append(" unpriced");
        }

        if (totals.ApiEquivalentEstimatedSpanCount > 0)
        {
            line.Append(" • estimated: ")
                .Append(Plural(totals.ApiEquivalentEstimatedSpanCount, "turn", "turns"))
                .Append(" reported no cache counters");
        }

        return line.ToString();
    }

    // A model row's API-equivalent amount: Unpriced, or the amount, noting when it leaves out turns or is an estimate.
    private static string ApiEquivalentCell(LedgerRollupRow row)
    {
        if (row.ApiEquivalentUsd is not decimal usd)
        {
            return "Unpriced";
        }

        List<string> notes = [];
        if (row.ApiEquivalentUnpricedSpanCount > 0)
        {
            notes.Add("partial");
        }

        if (row.ApiEquivalentEstimatedSpanCount > 0)
        {
            notes.Add("estimate");
        }

        return notes.Count == 0 ? LedgerFormat.Usd(usd) : $"{LedgerFormat.Usd(usd)} ({string.Join(", ", notes)})";
    }

    private static LedgerRollupRow[] Rows(PullRequestReceipt receipt, BillingChannel channel) =>
        receipt.Models.Where(row => row.Channel == channel).ToArray();
```

Edit 1 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
        - **Local offload:** 412,000 tokens across 1 turn ($0.00 billed • $0.23 cloud spend avoided): `qwen3.8-27b` 412,000
        - **Not priced:** 1 turn: `mystery-1`
        - **Circuit breaker:** 2 trips • max single-prompt burn: 317.50 credits

        <details>
```

with:

```csharp
        - **Local offload:** 412,000 tokens across 1 turn ($0.00 billed • $0.23 cloud spend avoided): `qwen3.8-27b` 412,000
        - **Not priced:** 1 turn: `mystery-1`
        - **API-equivalent (retail):** $2.86 at API list prices • 5 turns unpriced
        - **Circuit breaker:** 2 trips • max single-prompt burn: 317.50 credits

        <details>
```

Edit 2 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
        <summary>Details and data</summary>

        | Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | Cloud spend avoided |
        | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
        | GitHub AI Credits | `claude-opus-5.5` | 1 | 0 | 1,200,000 | 50,000 | 286.00 credits ($2.86) | $0.00 |
        | GitHub AI Credits | `mai-code-2` | 1 | 0 | 1,000,000 | 100,000 | 56.00 credits ($0.56) | $0.00 |
        | Foundry BYOK | `gpt-6.1-sol` | 1 | 0 | 100,000 | 10,000 | $0.32 | $0.00 |
        | Local | `qwen3.8-27b` | 1 | 0 | 400,000 | 12,000 | $0.00 | $0.23 |
        | GitHub AI Credits | `gpt-unknown` | 1 | 1 | 1,000 | 100 | 0.00 credits ($0.00) | $0.00 |
        | Not priced | `mystery-1` | 1 | 1 | 2,000 | 200 | $0.00 | $0.00 |

        Repository `swigerb/copilot-ledger` • 3 prompts • commit `0123456789ab` • generated 2026-11-21 15:00 UTC by gh-ledger <version>
```

with:

```csharp
        <summary>Details and data</summary>

        | Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | API-equivalent | Cloud spend avoided |
        | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
        | GitHub AI Credits | `claude-opus-5.5` | 1 | 0 | 1,200,000 | 50,000 | 286.00 credits ($2.86) | $2.86 | $0.00 |
        | GitHub AI Credits | `mai-code-2` | 1 | 0 | 1,000,000 | 100,000 | 56.00 credits ($0.56) | Unpriced | $0.00 |
        | Foundry BYOK | `gpt-6.1-sol` | 1 | 0 | 100,000 | 10,000 | $0.32 | Unpriced | $0.00 |
        | Local | `qwen3.8-27b` | 1 | 0 | 400,000 | 12,000 | $0.00 | Unpriced | $0.23 |
        | GitHub AI Credits | `gpt-unknown` | 1 | 1 | 1,000 | 100 | 0.00 credits ($0.00) | Unpriced | $0.00 |
        | Not priced | `mystery-1` | 1 | 1 | 2,000 | 200 | $0.00 | Unpriced | $0.00 |

        Repository `swigerb/copilot-ledger` • 3 prompts • commit `0123456789ab` • generated 2026-11-21 15:00 UTC by gh-ledger <version>
```

Edit 3 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
            "aiCreditsUsd": 3.42,
            "foundryByokUsd": 0.315,
            "localSavedUsd": 0.23
          },
          "prompts": 3,
```

with:

```csharp
            "aiCreditsUsd": 3.42,
            "foundryByokUsd": 0.315,
            "localSavedUsd": 0.23,
            "apiEquivalentUsd": 2.86,
            "apiEquivalentUnpricedSpans": 5,
            "apiEquivalentEstimatedSpans": 0
          },
          "prompts": 3,
```

Edit 4 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
              "aiCreditsUsd": 2.86,
              "foundryByokUsd": 0,
              "localSavedUsd": 0
            },
            {
```

with:

```csharp
              "aiCreditsUsd": 2.86,
              "foundryByokUsd": 0,
              "localSavedUsd": 0,
              "apiEquivalentUsd": 2.86,
              "apiEquivalentUnpricedSpans": 0,
              "apiEquivalentEstimatedSpans": 0
            },
            {
```

Edit 5 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
              "aiCreditsUsd": 0.56,
              "foundryByokUsd": 0,
              "localSavedUsd": 0
            },
            {
```

with:

```csharp
              "aiCreditsUsd": 0.56,
              "foundryByokUsd": 0,
              "localSavedUsd": 0,
              "apiEquivalentUsd": null,
              "apiEquivalentUnpricedSpans": 1,
              "apiEquivalentEstimatedSpans": 0
            },
            {
```

Edit 6 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0.315,
              "localSavedUsd": 0
            },
            {
```

with:

```csharp
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0.315,
              "localSavedUsd": 0,
              "apiEquivalentUsd": null,
              "apiEquivalentUnpricedSpans": 1,
              "apiEquivalentEstimatedSpans": 0
            },
            {
```

Edit 7 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0.23
            },
            {
```

with:

```csharp
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0.23,
              "apiEquivalentUsd": null,
              "apiEquivalentUnpricedSpans": 1,
              "apiEquivalentEstimatedSpans": 0
            },
            {
```

Edit 8 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0
            },
            {
```

with:

```csharp
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0,
              "apiEquivalentUsd": null,
              "apiEquivalentUnpricedSpans": 1,
              "apiEquivalentEstimatedSpans": 0
            },
            {
```

Edit 9 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0
            }
          ]
```

with:

```csharp
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0,
              "apiEquivalentUsd": null,
              "apiEquivalentUnpricedSpans": 1,
              "apiEquivalentEstimatedSpans": 0
            }
          ]
```

Edit 10 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
        - **Foundry BYOK:** none
        - **Local offload:** none
        - **Circuit breaker:** 0 trips • max single-prompt burn: 0.00 credits

        <details>
```

with:

```csharp
        - **Foundry BYOK:** none
        - **Local offload:** none
        - **API-equivalent (retail):** none
        - **Circuit breaker:** 0 trips • max single-prompt burn: 0.00 credits

        <details>
```

Edit 11 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
            "aiCreditsUsd": 0,
            "foundryByokUsd": 0,
            "localSavedUsd": 0
          },
          "prompts": 0,
```

with:

```csharp
            "aiCreditsUsd": 0,
            "foundryByokUsd": 0,
            "localSavedUsd": 0,
            "apiEquivalentUsd": 0,
            "apiEquivalentUnpricedSpans": 0,
            "apiEquivalentEstimatedSpans": 0
          },
          "prompts": 0,
```

Edit 12 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
        string repo = Path.Combine(temp.Path, "repo");
        const string Error =
            "The receipt for feature/ledger is 67762 bytes; a receipt can be at most 65536 bytes.\n";

        CliRun file = await harness.RunInAsync(repo, "export-pr");
```

with:

```csharp
        string repo = Path.Combine(temp.Path, "repo");
        const string Error =
            "The receipt for feature/ledger is 68094 bytes; a receipt can be at most 65536 bytes.\n";

        CliRun file = await harness.RunInAsync(repo, "export-pr");
```

Edit 13 of 13 in `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs`, replace:

```csharp
    }

    private static string Expected(string template) =>
        template.ReplaceLineEndings("\n").Replace("<version>", GhLedgerCli.Version, StringComparison.Ordinal);
```

with:

```csharp
    }

    [Fact]
    public async Task EP11_ApiEquivalentTotalsReconcileWithBranchJson()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun receipt = await harness.RunInAsync(repo, "export-pr", "--json");
        CliRun branch = await harness.RunInAsync(repo, "branch", "--since", "all", "--json");

        using JsonDocument receiptJson = JsonDocument.Parse(receipt.Out);
        using JsonDocument branchJson = JsonDocument.Parse(branch.Out);
        JsonElement totals = receiptJson.RootElement.GetProperty("totals");
        JsonElement row = Assert.Single(
            branchJson.RootElement.GetProperty("rows").EnumerateArray(),
            item => item.GetProperty("branch").GetString() == "feature/ledger");
        foreach (string name in new[] { "apiEquivalentUsd", "apiEquivalentUnpricedSpans", "apiEquivalentEstimatedSpans" })
        {
            Assert.Equal(row.GetProperty(name).GetRawText(), totals.GetProperty(name).GetRawText());
        }

        JsonElement[] models = receiptJson.RootElement.GetProperty("models").EnumerateArray().ToArray();
        Assert.Equal(
            totals.GetProperty("apiEquivalentUsd").GetDecimal(),
            models.Where(item => item.GetProperty("apiEquivalentUsd").ValueKind != JsonValueKind.Null)
                .Sum(item => item.GetProperty("apiEquivalentUsd").GetDecimal()));
        Assert.Equal(
            totals.GetProperty("apiEquivalentUnpricedSpans").GetInt64(),
            models.Sum(item => item.GetProperty("apiEquivalentUnpricedSpans").GetInt64()));
    }

    // A branch whose turns all lack a list price says Unpriced, never $0.00.
    [Fact]
    public async Task EP12_ReceiptsWithoutApiEquivalentPricesSayUnpriced()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(requestModel: "mai-code-2", inputTokens: 1_000, outputTokens: 100);
        await store.RecordAsync(
        [
            new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), GitFixture.Feature(temp.Path)),
        ]);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun markdown = await harness.RunInAsync(repo, "export-pr", "--stdout");
        CliRun json = await harness.RunInAsync(repo, "export-pr", "--json");

        Assert.Equal(0, markdown.ExitCode);
        Assert.Contains(
            "\n- **API-equivalent (retail):** Unpriced (1 turn without an API-equivalent price)\n",
            markdown.Out);
        Assert.Contains(
            "| GitHub AI Credits | `mai-code-2` | 1 | 0 | 1,000 | 100 | 0.08 credits ($0.00) | Unpriced | $0.00 |\n",
            markdown.Out);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Equal(
            JsonValueKind.Null,
            document.RootElement.GetProperty("totals").GetProperty("apiEquivalentUsd").ValueKind);
    }

    private static string Expected(string template) =>
        template.ReplaceLineEndings("\n").Replace("<version>", GhLedgerCli.Version, StringComparison.Ordinal);
```

Edit 1 of 1 in `docs/cli.md`, replace:

```markdown
The receipt covers all spend recorded on the branch: GitHub AI credits by model, Foundry BYOK spend, local-model tokens and the cloud spend they avoided, unpriced requests, circuit-breaker trips, and the most expensive prompt, with the data as JSON. A turn is one model request. Its totals equal the branch's row in `gh ledger branch --since all --json`. Branch and model names appear in code spans, so they never become mentions or links.

- `--stdout` writes the Markdown to standard output instead of the file.
- `--json` writes the JSON data to standard output instead of the file.
```

with:

```markdown
The receipt covers all spend recorded on the branch: GitHub AI credits by model, Foundry BYOK spend, local-model tokens and the cloud spend they avoided, unpriced requests, circuit-breaker trips, and the most expensive prompt, with the data as JSON. A turn is one model request. Its totals equal the branch's row in `gh ledger branch --since all --json`. Branch and model names appear in code spans, so they never become mentions or links.

The `API-equivalent (retail)` line and column compare the same turns with API list prices; they're never part of the billed amounts (see [API-equivalent USD](#api-equivalent-usd)). The line shows `none` without turns and `Unpriced` when no turn has an API-equivalent price, and otherwise says how many turns it leaves out and how many make it an estimate. The column writes `partial` and `estimate` instead of `*` and `~`, which are Markdown syntax. The JSON data adds `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans` to `totals` and to each model, and keeps `schemaVersion` 1.

- `--stdout` writes the Markdown to standard output instead of the file.
- `--json` writes the JSON data to standard output instead of the file.
```

Edit 1 of 7 in `docs/guide.md`, replace:

```markdown
> - **Foundry BYOK:** $0.49 across 1 turn: `gpt-6.1-sol` $0.49
> - **Local offload:** 309,000 tokens across 1 turn ($0.00 billed • $0.17 cloud spend avoided): `qwen3.8-27b` 309,000
> - **Circuit breaker:** 1 trip • max single-prompt burn: 504.00 credits
>
```

with:

```markdown
> - **Foundry BYOK:** $0.49 across 1 turn: `gpt-6.1-sol` $0.49
> - **Local offload:** 309,000 tokens across 1 turn ($0.00 billed • $0.17 cloud spend avoided): `qwen3.8-27b` 309,000
> - **API-equivalent (retail):** $6.49 at API list prices • 2 turns unpriced • estimated: 1 turn reported no cache counters
> - **Circuit breaker:** 1 trip • max single-prompt burn: 504.00 credits
>
```

Edit 2 of 7 in `docs/guide.md`, replace:

```markdown
> <summary>Details and data</summary>
>
> | Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | Cloud spend avoided |
> | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
> | GitHub AI Credits | `claude-opus-5.5` | 5 | 0 | 2,400,000 | 95,000 | 599.00 credits ($5.99) | $0.00 |
> | Foundry BYOK | `gpt-6.1-sol` | 1 | 0 | 200,000 | 10,000 | $0.49 | $0.00 |
> | GitHub AI Credits | `mai-code-2` | 1 | 0 | 300,000 | 15,000 | 18.75 credits ($0.19) | $0.00 |
> | Local | `qwen3.8-27b` | 1 | 0 | 300,000 | 9,000 | $0.00 | $0.17 |
>
> Repository `contoso/webapp` • 4 prompts • commit `4f9c2e1d8b7a` • generated 2026-11-21 21:30 UTC by gh-ledger 0.4.1
```

with:

```markdown
> <summary>Details and data</summary>
>
> | Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | API-equivalent | Cloud spend avoided |
> | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
> | GitHub AI Credits | `claude-opus-5.5` | 5 | 0 | 2,400,000 | 95,000 | 599.00 credits ($5.99) | $5.99 | $0.00 |
> | Foundry BYOK | `gpt-6.1-sol` | 1 | 0 | 200,000 | 10,000 | $0.49 | $0.50 (estimate) | $0.00 |
> | GitHub AI Credits | `mai-code-2` | 1 | 0 | 300,000 | 15,000 | 18.75 credits ($0.19) | Unpriced | $0.00 |
> | Local | `qwen3.8-27b` | 1 | 0 | 300,000 | 9,000 | $0.00 | Unpriced | $0.17 |
>
> Repository `contoso/webapp` • 4 prompts • commit `4f9c2e1d8b7a` • generated 2026-11-21 21:30 UTC by gh-ledger 0.4.1
```

Edit 3 of 7 in `docs/guide.md`, replace:

```markdown
>     "aiCreditsUsd": 6.1775,
>     "foundryByokUsd": 0.49,
>     "localSavedUsd": 0.1725
>   },
>   "prompts": 4,
```

with:

```markdown
>     "aiCreditsUsd": 6.1775,
>     "foundryByokUsd": 0.49,
>     "localSavedUsd": 0.1725,
>     "apiEquivalentUsd": 6.49,
>     "apiEquivalentUnpricedSpans": 2,
>     "apiEquivalentEstimatedSpans": 1
>   },
>   "prompts": 4,
```

Edit 4 of 7 in `docs/guide.md`, replace:

```markdown
>       "aiCreditsUsd": 5.99,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0
>     },
>     {
```

with:

```markdown
>       "aiCreditsUsd": 5.99,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0,
>       "apiEquivalentUsd": 5.99,
>       "apiEquivalentUnpricedSpans": 0,
>       "apiEquivalentEstimatedSpans": 0
>     },
>     {
```

Edit 5 of 7 in `docs/guide.md`, replace:

```markdown
>       "aiCreditsUsd": 0,
>       "foundryByokUsd": 0.49,
>       "localSavedUsd": 0
>     },
>     {
```

with:

```markdown
>       "aiCreditsUsd": 0,
>       "foundryByokUsd": 0.49,
>       "localSavedUsd": 0,
>       "apiEquivalentUsd": 0.5,
>       "apiEquivalentUnpricedSpans": 0,
>       "apiEquivalentEstimatedSpans": 1
>     },
>     {
```

Edit 6 of 7 in `docs/guide.md`, replace:

```markdown
>       "aiCreditsUsd": 0.1875,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0
>     },
>     {
```

with:

```markdown
>       "aiCreditsUsd": 0.1875,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0,
>       "apiEquivalentUsd": null,
>       "apiEquivalentUnpricedSpans": 1,
>       "apiEquivalentEstimatedSpans": 0
>     },
>     {
```

Edit 7 of 7 in `docs/guide.md`, replace:

```markdown
>       "aiCreditsUsd": 0,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0.1725
>     }
>   ]
```

with:

```markdown
>       "aiCreditsUsd": 0,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0.1725,
>       "apiEquivalentUsd": null,
>       "apiEquivalentUnpricedSpans": 1,
>       "apiEquivalentEstimatedSpans": 0
>     }
>   ]
```

### Tests

- `FeatureReceipt` (EP1–EP3) gains `- **API-equivalent (retail):** $2.86 at API list prices • 5 turns unpriced`, the column, and the fields; `EmptyReceipt` (EP5) gains `none` and the fields, with `apiEquivalentUsd` 0. EP10's oversized receipt is now 68,094 bytes.
- **EP11**: the receipt's three totals equal the branch's row in `branch --since all --json`, and the models' amounts and unpriced counts add up to them.
- **EP12**: a branch with only `mai-code-2` says `Unpriced (1 turn without an API-equivalent price)`, its row says `Unpriced`, and its JSON `apiEquivalentUsd` is `null`.

### Acceptance criteria

- Receipts show the API-equivalent amount, `Unpriced` when there's none, in words that Markdown doesn't format, and stay within 65,536 bytes; their JSON reconciles with `branch --json` (EP1–EP3, EP5, EP10–EP12, GS6).
- `dotnet test CopilotLedger.slnx` passes **970** tests (968 + 2).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 10 specification: What-If Pricing with --pricing

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Rates/WhatIfPricing.cs` | New: loading, labeling, and the shared `--pricing` option |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | `--pricing` on `status`, `branch`, `agents`, and `top-prompts` |
| `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, `PullRequestReceipt.cs`, `ReceiptMarkdown.cs` | `--pricing` on `export-pr` |
| `src/CopilotLedger.Cli/Rates/RatesCommands.cs` | `rates validate --pricing <path>` |
| `tests/CopilotLedger.Tests/Cli/WhatIfCommandTests.cs` | New: WF1–WF5 |
| `docs/configuration.md`, `docs/cli.md`, `docs/guide.md` | "Compare with other prices: `--pricing`" and the option |

### Contracts

```csharp
// CopilotLedger.Cli.Rates (internal)
internal sealed record WhatIfPricing(string Path, JsonRateCardCalculator Calculator)
{
    public string Label(bool fileNameOnly = false);              // what-if <path> (ratesVersion <v>)
    public static WhatIfPricing? Load(CliContext context, string file);   // null after writing the error
    public void Write(Utf8JsonWriter writer, bool fileNameOnly = false);  // "pricingSource": { … }
    public static Option<string?> CreateOption();                // --pricing
}
```

### Rules

- `Load` resolves the file against the working directory, then reads and parses it as a complete card with `JsonRateCardCalculator.Parse`, alone. Problems, each written as `Invalid pricing file: <problem>` with exit 1: `<path> does not exist.`, `<path>: <reader message>`, `<path>: schemaVersion: must be 2, so that the file has API list prices`, or `<path>: could not read it: <message>`.
- The file is loaded before the ledger is opened (before the Git checks in `export-pr`), and its calculator is passed to the store's `whatIf` overloads. Everything else in the report is unchanged.
- Text: `Pricing: what-if <path> (ratesVersion <v>), for API-equivalent amounts only` under `Rates:` in `status`, and under the title in `branch`, `agents`, and `top-prompts`. JSON: `pricingSource` after `ratesError` in `status` and after `repository` in the others, and only with `--pricing`.
- `export-pr` names the file without its folder in the Markdown (`at what-if prices from <file>, ratesVersion <v>`, both in code spans) and in `pricingSource.path`, after `generator`.
- `status --cache --pricing` fails: `--pricing can't be used with --cache.` `rates validate` takes `--file` or `--pricing`, not both, and with `--pricing` prints `<path> is a valid pricing file. Rates version: <v>.`

### Code (copy exactly)

Edit 1 of 2 in `src/CopilotLedger.Cli/Rates/RatesCommands.cs`, replace:

```csharp
        rates.Add(init);

        Command validate = new("validate", "Validate a rates overlay.");
        Option<string?> file = new("--file")
        {
            Description = "Path to the rates overlay.",
        };
        validate.Add(file);
        validate.SetAction(
            (ParseResult result, CancellationToken _) =>
                Task.FromResult(Validate(context, result.GetValue(file))));
        rates.Add(validate);

        Command update = new("update", "Download the latest published rates.");
```

with:

```csharp
        rates.Add(init);

        Command validate = new("validate", "Validate a rates overlay, or a --pricing file.");
        Option<string?> file = new("--file")
        {
            Description = "Path to the rates overlay.",
        };
        Option<string?> pricing = new("--pricing")
        {
            Description = "Path to a complete rate card to check as the reports' --pricing checks it.",
        };
        validate.Add(file);
        validate.Add(pricing);
        validate.Validators.Add(result =>
        {
            if (result.GetValue(file) is not null && result.GetValue(pricing) is not null)
            {
                result.AddError("Use either --file or --pricing, not both.");
            }
        });
        validate.SetAction(
            (ParseResult result, CancellationToken _) =>
                Task.FromResult(
                    result.GetValue(pricing) is string pricingFile
                        ? ValidatePricing(context, pricingFile)
                        : Validate(context, result.GetValue(file))));
        rates.Add(validate);

        Command update = new("update", "Download the latest published rates.");
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Rates/RatesCommands.cs`, replace:

```csharp
    }

    // Prices the legacy spans with the rates the daemon would load: the base rates and the overlay.
    private static async Task<int> BackfillAsync(
```

with:

```csharp
    }

    // Checks a --pricing file with the rules and the messages of the reports' --pricing.
    private static int ValidatePricing(CliContext context, string file)
    {
        WhatIfPricing? pricing = WhatIfPricing.Load(context, file);
        if (pricing is null)
        {
            return 1;
        }

        context.Out.WriteLine(
            $"{pricing.Path} is a valid pricing file. Rates version: {pricing.Calculator.RatesVersion}.");
        return 0;
    }

    // Prices the legacy spans with the rates the daemon would load: the base rates and the overlay.
    private static async Task<int> BackfillAsync(
```

New file `src/CopilotLedger.Cli/Rates/WhatIfPricing.cs`:

```csharp
using System.CommandLine;
using System.Text.Json;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Rates;

// A --pricing file: a complete rate card whose API list prices replace the recorded API-equivalent results in a report. Path
// is the file's full path. Nothing else in the report changes, and nothing is saved.
internal sealed record WhatIfPricing(string Path, JsonRateCardCalculator Calculator)
{
    // "what-if <path> (ratesVersion <version>)"; with fileNameOnly, the path is the file's name, as receipts show it.
    public string Label(bool fileNameOnly = false) =>
        $"what-if {(fileNameOnly ? System.IO.Path.GetFileName(Path) : Path)} (ratesVersion {Calculator.RatesVersion})";

    // Reads file, relative to the working directory, as a complete rate card with API list prices (schemaVersion 2), on its
    // own: neither the overlay nor the downloaded rates apply. Returns null after writing "Invalid pricing file: <problem>" to
    // context.Error when the file can't be used.
    public static WhatIfPricing? Load(CliContext context, string file)
    {
        string path = System.IO.Path.GetFullPath(file, context.WorkingDirectory);
        string? problem = null;
        JsonRateCardCalculator? calculator = null;
        if (!File.Exists(path))
        {
            problem = $"{path} does not exist.";
        }
        else
        {
            try
            {
                calculator = JsonRateCardCalculator.Parse(File.ReadAllText(path));
                if (calculator.SchemaVersion < 2)
                {
                    problem = $"{path}: schemaVersion: must be 2, so that the file has API list prices";
                }
            }
            catch (InvalidDataException ex)
            {
                problem = $"{path}: {ex.Message}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problem = $"{path}: could not read it: {ex.Message}";
            }
        }

        if (problem is not null)
        {
            context.Error.WriteLine($"Invalid pricing file: {problem}");
            return null;
        }

        return new WhatIfPricing(path, calculator!);
    }

    // "pricingSource": kind whatIf, the path (its file name with fileNameOnly), the ratesVersion, and the apiRateCardSha256
    // of the file's API list prices.
    public void Write(Utf8JsonWriter writer, bool fileNameOnly = false)
    {
        writer.WritePropertyName("pricingSource");
        writer.WriteStartObject();
        writer.WriteString("kind", "whatIf");
        writer.WriteString("path", fileNameOnly ? System.IO.Path.GetFileName(Path) : Path);
        writer.WriteString("ratesVersion", Calculator.RatesVersion);
        writer.WriteString("apiRateCardSha256", Calculator.ApiRateCardSha256);
        writer.WriteEndObject();
    }

    // The --pricing option of status, branch, agents, top-prompts, and export-pr.
    public static Option<string?> CreateOption() =>
        new("--pricing")
        {
            Description =
                "Price the API-equivalent amounts with this complete rate card instead. Nothing else changes, and nothing is saved.",
        };
}
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Receipts/PullRequestReceipt.cs`, replace:

```csharp
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Receipts;
```

with:

```csharp
using CopilotLedger.Cli.Rates;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Receipts;
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Receipts/PullRequestReceipt.cs`, replace:

```csharp
    public required decimal MaxPromptCredits { get; init; }                // costliest prompt chain: (AiCreditsUsd + FoundryByokUsd) / 0.01m; 0 without spans
    public required long BreakerTrips { get; init; }                       // CountBreakerTripsAsync
}
```

with:

```csharp
    public required decimal MaxPromptCredits { get; init; }                // costliest prompt chain: (AiCreditsUsd + FoundryByokUsd) / 0.01m; 0 without spans
    public required long BreakerTrips { get; init; }                       // CountBreakerTripsAsync
    public WhatIfPricing? Pricing { get; init; }                           // --pricing; null for the recorded API-equivalent results
}
```

Edit 1 of 7 in `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, replace:

```csharp
using System.CommandLine.Parsing;
using System.Text;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
```

with:

```csharp
using System.CommandLine.Parsing;
using System.Text;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
```

Edit 2 of 7 in `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, replace:

```csharp
            Description = "Write the receipt's JSON data to standard output instead of the receipt file.",
        };
        command.Add(stdout);
        command.Add(json);
        command.Validators.Add(result =>
        {
```

with:

```csharp
            Description = "Write the receipt's JSON data to standard output instead of the receipt file.",
        };
        Option<string?> pricing = WhatIfPricing.CreateOption();
        command.Add(stdout);
        command.Add(json);
        command.Add(pricing);
        command.Validators.Add(result =>
        {
```

Edit 3 of 7 in `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, replace:

```csharp
                    result.GetValue(stdout),
                    result.GetValue(json),
                    cancellationToken));
        return command;
```

with:

```csharp
                    result.GetValue(stdout),
                    result.GetValue(json),
                    result.GetValue(pricing),
                    cancellationToken));
        return command;
```

Edit 4 of 7 in `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, replace:

```csharp
        bool stdout,
        bool json,
        CancellationToken cancellationToken)
    {
        GitWorkspaceContext? git =
            new FileSystemGitContextResolver().Resolve(context.WorkingDirectory);
```

with:

```csharp
        bool stdout,
        bool json,
        string? pricingFile,
        CancellationToken cancellationToken)
    {
        WhatIfPricing? pricing = null;
        if (pricingFile is not null)
        {
            pricing = WhatIfPricing.Load(context, pricingFile);
            if (pricing is null)
            {
                return 1;
            }
        }

        GitWorkspaceContext? git =
            new FileSystemGitContextResolver().Resolve(context.WorkingDirectory);
```

Edit 5 of 7 in `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, replace:

```csharp
            LedgerGrouping.Branch,
            filter,
            cancellationToken);
        IReadOnlyList<PromptChainRow> costliest = await store.GetTopPromptChainsAsync(
```

with:

```csharp
            LedgerGrouping.Branch,
            filter,
            pricing?.Calculator,
            cancellationToken);
        IReadOnlyList<PromptChainRow> costliest = await store.GetTopPromptChainsAsync(
```

Edit 6 of 7 in `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, replace:

```csharp
                LedgerGrouping.ChannelModel,
                filter,
                cancellationToken),
            Prompts = await store.GetPromptCountAsync(filter, cancellationToken),
```

with:

```csharp
                LedgerGrouping.ChannelModel,
                filter,
                pricing?.Calculator,
                cancellationToken),
            Prompts = await store.GetPromptCountAsync(filter, cancellationToken),
```

Edit 7 of 7 in `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, replace:

```csharp
                git.BranchName,
                cancellationToken),
        };

        if (json)
```

with:

```csharp
                git.BranchName,
                cancellationToken),
            Pricing = pricing,
        };

        if (json)
```

Edit 1 of 3 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
            LedgerFormat.WriteTime(writer, "generatedAt", receipt.GeneratedAt);
            writer.WriteString("generator", $"gh-ledger {GhLedgerCli.Version}");
            writer.WritePropertyName("totals");
            writer.WriteStartObject();
```

with:

```csharp
            LedgerFormat.WriteTime(writer, "generatedAt", receipt.GeneratedAt);
            writer.WriteString("generator", $"gh-ledger {GhLedgerCli.Version}");
            receipt.Pricing?.Write(writer, fileNameOnly: true);
            writer.WritePropertyName("totals");
            writer.WriteStartObject();
```

Edit 2 of 3 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
    };

    // The receipt's API-equivalent line: none without spans, and Unpriced when no turn has an API-equivalent price. Otherwise
    // the amount at API list prices, then the turns it leaves out and the turns that make it an estimate, in words rather than
    // the report marks, because * and ~ are Markdown syntax.
    private static string ApiEquivalentSummary(PullRequestReceipt receipt)
    {
```

with:

```csharp
    };

    // The receipt's API-equivalent line: none without spans, and Unpriced when no turn has an API-equivalent price. Otherwise
    // the amount at API list prices, or at the --pricing file's prices, then the turns it leaves out and the turns that make it
    // an estimate, in words rather than the report marks, because * and ~ are Markdown syntax.
    private static string ApiEquivalentSummary(PullRequestReceipt receipt)
    {
```

Edit 3 of 3 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
        }

        StringBuilder line = new();
        line.Append(LedgerFormat.Usd(usd)).Append(" at API list prices");
        if (totals.ApiEquivalentUnpricedSpanCount > 0)
        {
```

with:

```csharp
        }

        StringBuilder line = new();
        line.Append(LedgerFormat.Usd(usd)).Append(
            receipt.Pricing is null
                ? " at API list prices"
                : $" at what-if prices from {Code(Path.GetFileName(receipt.Pricing.Path))}, ratesVersion {Code(receipt.Pricing.Calculator.RatesVersion)}");
        if (totals.ApiEquivalentUnpricedSpanCount > 0)
        {
```

Edit 1 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        command.Add(bloat);
        command.Add(limit);
        command.Validators.Add(result =>
        {
            string[] given = new Option[] { by, repository, branch, pullRequest, since, turns, trace, conversation, bloat, limit }
                .Where(option => result.GetResult(option) is { Implicit: false })
```

with:

```csharp
        command.Add(bloat);
        command.Add(limit);
        Option<string?> pricing = WhatIfPricing.CreateOption();
        command.Add(pricing);
        command.Validators.Add(result =>
        {
            if (result.GetValue(cache) && result.GetResult(pricing) is { Implicit: false })
            {
                result.AddError("--pricing can't be used with --cache.");
                return;
            }

            string[] given = new Option[] { by, repository, branch, pullRequest, since, turns, trace, conversation, bloat, limit }
                .Where(option => result.GetResult(option) is { Implicit: false })
```

Edit 2 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            (ParseResult result, CancellationToken cancellationToken) =>
                !result.GetValue(cache)
                    ? StatusAsync(context, result.GetValue(json), cancellationToken)
                    : result.GetValue(turns)
                        ? TurnsAsync(
```

with:

```csharp
            (ParseResult result, CancellationToken cancellationToken) =>
                !result.GetValue(cache)
                    ? StatusAsync(context, result.GetValue(json), result.GetValue(pricing), cancellationToken)
                    : result.GetValue(turns)
                        ? TurnsAsync(
```

Edit 3 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        Option<string> since = SinceOption(context);
        Option<bool> json = JsonOption();
        command.Add(repository);
        command.Add(allRepositories);
        command.Add(since);
        command.Add(json);
        command.Validators.Add(result =>
        {
```

with:

```csharp
        Option<string> since = SinceOption(context);
        Option<bool> json = JsonOption();
        Option<string?> pricing = WhatIfPricing.CreateOption();
        command.Add(repository);
        command.Add(allRepositories);
        command.Add(since);
        command.Add(json);
        command.Add(pricing);
        command.Validators.Add(result =>
        {
```

Edit 4 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                    result.GetValue(allRepositories),
                    ParsePeriod(context, result.GetValue(since)),
                    result.GetValue(json),
                    cancellationToken));
        return command;
```

with:

```csharp
                    result.GetValue(allRepositories),
                    ParsePeriod(context, result.GetValue(since)),
                    result.GetValue(json),
                    result.GetValue(pricing),
                    cancellationToken));
        return command;
```

Edit 5 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        Option<string> since = SinceOption(context);
        Option<bool> json = JsonOption();
        command.Add(repository);
        command.Add(since);
        command.Add(json);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
```

with:

```csharp
        Option<string> since = SinceOption(context);
        Option<bool> json = JsonOption();
        Option<string?> pricing = WhatIfPricing.CreateOption();
        command.Add(repository);
        command.Add(since);
        command.Add(json);
        command.Add(pricing);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
```

Edit 6 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                    ParsePeriod(context, result.GetValue(since)),
                    result.GetValue(json),
                    cancellationToken));
        return command;
```

with:

```csharp
                    ParsePeriod(context, result.GetValue(since)),
                    result.GetValue(json),
                    result.GetValue(pricing),
                    cancellationToken));
        return command;
```

Edit 7 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        });
        Option<bool> json = JsonOption();
        command.Add(repository);
        command.Add(since);
        command.Add(limit);
        command.Add(json);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
```

with:

```csharp
        });
        Option<bool> json = JsonOption();
        Option<string?> pricing = WhatIfPricing.CreateOption();
        command.Add(repository);
        command.Add(since);
        command.Add(limit);
        command.Add(json);
        command.Add(pricing);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
```

Edit 8 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                    result.GetValue(limit),
                    result.GetValue(json),
                    cancellationToken));
        return command;
```

with:

```csharp
                    result.GetValue(limit),
                    result.GetValue(json),
                    result.GetValue(pricing),
                    cancellationToken));
        return command;
```

Edit 9 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        CliContext context,
        bool json,
        CancellationToken cancellationToken)
    {
        bool running = DaemonFiles.IsHeld(context);
        DaemonState? state = running ? DaemonFiles.TryReadState(context) : null;
```

with:

```csharp
        CliContext context,
        bool json,
        string? pricingFile,
        CancellationToken cancellationToken)
    {
        if (!TryLoadPricing(context, pricingFile, out WhatIfPricing? pricing))
        {
            return 1;
        }

        bool running = DaemonFiles.IsHeld(context);
        DaemonState? state = running ? DaemonFiles.TryReadState(context) : null;
```

Edit 10 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                LedgerGrouping.ChannelModel,
                new LedgerFilter { From = period.From },
                cancellationToken);
            totals.Add((label, period, new StatusTotals(
```

with:

```csharp
                LedgerGrouping.ChannelModel,
                new LedgerFilter { From = period.From },
                pricing?.Calculator,
                cancellationToken);
            totals.Add((label, period, new StatusTotals(
```

Edit 11 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                totals,
                extension,
                rates.DownloadedJson is not null);
            return 0;
        }
```

with:

```csharp
                totals,
                extension,
                rates.DownloadedJson is not null,
                pricing);
            return 0;
        }
```

Edit 12 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        context.Out.WriteLine(
            $"Rates:   {(ratesError is not null ? $"invalid overlay: {ratesError}" : rates.DownloadedJson is null ? ratesVersion : $"{ratesVersion} (downloaded)")}");
        context.Out.WriteLine(
            $"Copilot: {(enabled
```

with:

```csharp
        context.Out.WriteLine(
            $"Rates:   {(ratesError is not null ? $"invalid overlay: {ratesError}" : rates.DownloadedJson is null ? ratesVersion : $"{ratesVersion} (downloaded)")}");
        WritePricingLabel(context, pricing);
        context.Out.WriteLine(
            $"Copilot: {(enabled
```

Edit 13 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        string? requestedRepository,
        bool allRepositories,
        Period period,
        bool json,
        CancellationToken cancellationToken)
    {
```

with:

```csharp
        string? requestedRepository,
        bool allRepositories,
        Period period,
        bool json,
        string? pricingFile,
        CancellationToken cancellationToken)
    {
```

Edit 14 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            period,
            json,
            cancellationToken);
    }
```

with:

```csharp
            period,
            json,
            pricingFile,
            cancellationToken);
    }
```

Edit 15 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        Period period,
        bool json,
        CancellationToken cancellationToken) =>
        await RollupRowsAsync(
```

with:

```csharp
        Period period,
        bool json,
        string? pricingFile,
        CancellationToken cancellationToken) =>
        await RollupRowsAsync(
```

Edit 16 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            period,
            json,
            cancellationToken);

    private static async Task<int> RollupRowsAsync(
```

with:

```csharp
            period,
            json,
            pricingFile,
            cancellationToken);

    private static async Task<int> RollupRowsAsync(
```

Edit 17 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        Period period,
        bool json,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
```

with:

```csharp
        Period period,
        bool json,
        string? pricingFile,
        CancellationToken cancellationToken)
    {
        if (!TryLoadPricing(context, pricingFile, out WhatIfPricing? pricing))
        {
            return 1;
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
```

Edit 18 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                Repository = repository,
            },
            cancellationToken);
        string scope = repository ?? "all repositories";
```

with:

```csharp
                Repository = repository,
            },
            pricing?.Calculator,
            cancellationToken);
        string scope = repository ?? "all repositories";
```

Edit 19 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        if (json)
        {
            WriteRollupJson(context, period, repository, grouping, rows);
            return 0;
        }
```

with:

```csharp
        if (json)
        {
            WriteRollupJson(context, period, repository, grouping, rows, pricing);
            return 0;
        }
```

Edit 20 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        }

        context.Out.WriteLine(title);
        context.Out.WriteLine();
        TableColumn[] leading = grouping == LedgerGrouping.Branch && allRepositories
```

with:

```csharp
        }

        context.Out.WriteLine(title);
        WritePricingLabel(context, pricing);
        context.Out.WriteLine();
        TableColumn[] leading = grouping == LedgerGrouping.Branch && allRepositories
```

Edit 21 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
    private static async Task<int> TopPromptsAsync(
        CliContext context,
        string? repository,
        Period period,
        int limit,
        bool json,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
```

with:

```csharp
    private static async Task<int> TopPromptsAsync(
        CliContext context,
        string? repository,
        Period period,
        int limit,
        bool json,
        string? pricingFile,
        CancellationToken cancellationToken)
    {
        if (!TryLoadPricing(context, pricingFile, out WhatIfPricing? pricing))
        {
            return 1;
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
```

Edit 22 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            },
            limit,
            cancellationToken);
        string scope = repository ?? "all repositories";
        if (json)
        {
            WriteTopPromptsJson(context, period, repository, rows);
            return 0;
        }
```

with:

```csharp
            },
            limit,
            pricing?.Calculator,
            cancellationToken);
        string scope = repository ?? "all repositories";
        if (json)
        {
            WriteTopPromptsJson(context, period, repository, rows, pricing);
            return 0;
        }
```

Edit 23 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        context.Out.WriteLine(
            $"Top {rows.Count} {prompt} by cost in {scope} ({period.Label})");
        context.Out.WriteLine();
        TableColumn[] columns =
```

with:

```csharp
        context.Out.WriteLine(
            $"Top {rows.Count} {prompt} by cost in {scope} ({period.Label})");
        WritePricingLabel(context, pricing);
        context.Out.WriteLine();
        TableColumn[] columns =
```

Edit 24 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        IReadOnlyList<(string Label, Period Period, StatusTotals Totals)> totals,
        bool extension,
        bool downloadedRates)
    {
        using MemoryStream stream = new();
```

with:

```csharp
        IReadOnlyList<(string Label, Period Period, StatusTotals Totals)> totals,
        bool extension,
        bool downloadedRates,
        WhatIfPricing? pricing)
    {
        using MemoryStream stream = new();
```

Edit 25 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        }

        writer.WriteBoolean("copilotOtelEnabled", enabled);
        writer.WriteStartArray("warnings");
```

with:

```csharp
        }

        pricing?.Write(writer);
        writer.WriteBoolean("copilotOtelEnabled", enabled);
        writer.WriteStartArray("warnings");
```

Edit 26 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        string? repository,
        LedgerGrouping grouping,
        IReadOnlyList<LedgerRollupRow> rows)
    {
        using MemoryStream stream = new();
```

with:

```csharp
        string? repository,
        LedgerGrouping grouping,
        IReadOnlyList<LedgerRollupRow> rows,
        WhatIfPricing? pricing)
    {
        using MemoryStream stream = new();
```

Edit 27 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        }

        writer.WriteStartArray("rows");
        foreach (LedgerRollupRow row in rows)
```

with:

```csharp
        }

        pricing?.Write(writer);
        writer.WriteStartArray("rows");
        foreach (LedgerRollupRow row in rows)
```

Edit 28 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        Period period,
        string? repository,
        IReadOnlyList<PromptChainRow> rows)
    {
        using MemoryStream stream = new();
```

with:

```csharp
        Period period,
        string? repository,
        IReadOnlyList<PromptChainRow> rows,
        WhatIfPricing? pricing)
    {
        using MemoryStream stream = new();
```

Edit 29 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        }

        writer.WriteStartArray("rows");
        for (int index = 0; index < rows.Count; index++)
```

with:

```csharp
        }

        pricing?.Write(writer);
        writer.WriteStartArray("rows");
        for (int index = 0; index < rows.Count; index++)
```

Edit 30 of 30 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        ParsePeriod(context, value);

    // GitHub-billed USD (AI credits at $0.01 each) per USD of the same requests at API list prices. null unless both views
    // priced every one of them and the API-equivalent amount isn't 0.
```

with:

```csharp
        ParsePeriod(context, value);

    // The --pricing file: false after writing its error when it's invalid; pricing is null without one.
    private static bool TryLoadPricing(CliContext context, string? file, out WhatIfPricing? pricing)
    {
        pricing = file is null ? null : WhatIfPricing.Load(context, file);
        return file is null || pricing is not null;
    }

    // The line under a report's title that names the --pricing file.
    private static void WritePricingLabel(CliContext context, WhatIfPricing? pricing)
    {
        if (pricing is not null)
        {
            context.Out.WriteLine($"Pricing: {pricing.Label()}, for API-equivalent amounts only");
        }
    }
    // GitHub-billed USD (AI credits at $0.01 each) per USD of the same requests at API list prices. null unless both views
    // priced every one of them and the API-equivalent amount isn't 0.
```

New file `tests/CopilotLedger.Tests/Cli/WhatIfCommandTests.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Cli;

public sealed class WhatIfCommandTests
{
    private static ManualTimeProvider Time => new(
        DateTimeOffset.Parse("2026-11-21T15:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        ManualTimeProvider.UtcMinus5);

    [Fact]
    public async Task WF1_BranchShowsTheWhatIfAmountsAndLabel()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string path = Path.Combine(temp.Path, "negotiated.json");

        CliRun run = await harness.RunAsync("branch", "--all-repos", "--since", "all", "--pricing", "negotiated.json");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            $"""
            Branches in all repositories (all time)
            Pricing: what-if {path} (ratesVersion retail-fixture), for API-equivalent amounts only

            Repository              Branch          Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
            swigerb/copilot-ledger  main                1         0      286.00       $2.86     $0.00       Unpriced        $0.00
            swigerb/copilot-ledger  feature/ledger      2         0       56.00       $0.56     $0.00        ~$0.36*        $0.23
            contoso/api             main                1         0        0.00       $0.00     $0.32         ~$0.30        $0.00
            (none)                  (none)              1         1        0.00       $0.00     $0.00       Unpriced        $0.00

            3 of 5 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
            2 priced spans reported no cache counters, so all of their input is priced as uncached; ~ marks an estimate.

            """.ReplaceLineEndings("\n"),
            run.Out);
        Assert.Equal(string.Empty, run.Error);
    }

    // Only the API-equivalent fields change; recorded amounts, counts, and warnings don't, and nothing is written.
    [Theory]
    [InlineData("status")]
    [InlineData("branch")]
    [InlineData("agents")]
    [InlineData("top-prompts")]
    public async Task WF2_JsonChangesOnlyTheApiEquivalentFields(string command)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        harness.Platform.ProcessVariables["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc";
        string[] report = command == "status" ? ["status", "--json"] : [command, "--since", "all", "--json"];
        string[] before = await RowsAsync(temp.Path);

        CliRun recorded = await harness.RunAsync(report);
        CliRun whatIf = await harness.RunAsync([.. report, "--pricing", "negotiated.json"]);

        Assert.Equal(0, whatIf.ExitCode);
        JsonObject recordedJson = JsonNode.Parse(recorded.Out)!.AsObject();
        JsonObject whatIfJson = JsonNode.Parse(whatIf.Out)!.AsObject();
        Assert.Equal(
            $$"""{"kind":"whatIf","path":{{JsonSerializer.Serialize(Path.Combine(temp.Path, "negotiated.json"))}},"ratesVersion":"retail-fixture","apiRateCardSha256":"{{JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson).ApiRateCardSha256}}"}""",
            whatIfJson["pricingSource"]!.ToJsonString());
        Assert.Null(recordedJson["pricingSource"]);
        Assert.NotEqual(recorded.Out, whatIf.Out);
        Assert.Equal(WithoutApiEquivalent(recordedJson), WithoutApiEquivalent(whatIfJson));
        Assert.Equal(before, await RowsAsync(temp.Path));
    }

    public static TheoryData<string, string> InvalidFiles => new()
    {
        { "missing", "Invalid pricing file: <path> does not exist.\n" },
        { "not-json", "Invalid pricing file: <path>: line 1, column 2: invalid JSON\n" },
        { "schema-1", "Invalid pricing file: <path>: schemaVersion: must be 2, so that the file has API list prices\n" },
        { "schema-3", "Invalid pricing file: <path>: schemaVersion: must be 1 or 2\n" },
        { "overlay", "Invalid pricing file: <path>: ratesVersion: is required\n" },
    };

    // Every command that takes --pricing, and rates validate --pricing, refuse an unusable file with the same message.
    [Theory]
    [MemberData(nameof(InvalidFiles))]
    public async Task WF3_InvalidFilesFailWithTheSameMessage(string file, string error)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string path = Path.Combine(temp.Path, $"{file}.json");
        string? text = file switch
        {
            "not-json" => "{",
            "schema-1" => RateFixtures.ByokTestRatesJson,
            "schema-3" => RateFixtures.RetailTestRatesJson.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 3", StringComparison.Ordinal),
            "overlay" => """{ "schemaVersion": 2, "apiEquivalent": { "models": [] } }""",
            _ => null,
        };
        if (text is not null)
        {
            await File.WriteAllTextAsync(path, text);
        }

        string[][] commands =
        [
            ["status", "--pricing", path],
            ["branch", "--pricing", path],
            ["agents", "--pricing", path],
            ["top-prompts", "--pricing", path],
            ["export-pr", "--pricing", path],
            ["rates", "validate", "--pricing", path],
        ];
        foreach (string[] command in commands)
        {
            CliRun run = await harness.RunInAsync(Path.Combine(temp.Path, "repo"), command);
            Assert.Equal(1, run.ExitCode);
            Assert.Equal(string.Empty, run.Out);
            Assert.Equal(error.Replace("<path>", path, StringComparison.Ordinal), run.Error);
        }
    }

    [Fact]
    public async Task WF4_RatesValidateAcceptsAPricingFile()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun valid = await harness.RunAsync("rates", "validate", "--pricing", "negotiated.json");
        CliRun both = await harness.RunAsync("rates", "validate", "--file", "x.json", "--pricing", "negotiated.json");
        CliRun cache = await harness.RunAsync("status", "--cache", "--pricing", "negotiated.json");

        Assert.Equal(0, valid.ExitCode);
        Assert.Equal(
            $"{Path.Combine(temp.Path, "negotiated.json")} is a valid pricing file. Rates version: retail-fixture.\n",
            valid.Out);
        Assert.Equal(1, both.ExitCode);
        Assert.Contains("Use either --file or --pricing, not both.", both.Error);
        Assert.Equal(1, cache.ExitCode);
        Assert.Contains("--pricing can't be used with --cache.", cache.Error);
    }

    // A receipt names the file, not its local path, in the Markdown and the JSON.
    [Fact]
    public async Task WF5_ReceiptsLabelWhatIfPrices()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");
        string path = Path.Combine(temp.Path, "negotiated.json");

        CliRun markdown = await harness.RunInAsync(repo, "export-pr", "--stdout", "--pricing", path);
        CliRun json = await harness.RunInAsync(repo, "export-pr", "--json", "--pricing", path);

        Assert.Equal(0, markdown.ExitCode);
        Assert.Contains(
            "\n- **API-equivalent (retail):** $0.36 at what-if prices from `negotiated.json`, ratesVersion `retail-fixture` • 1 turn unpriced • estimated: 1 turn reported no cache counters\n",
            markdown.Out);
        Assert.DoesNotContain(temp.Path, markdown.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement source = document.RootElement.GetProperty("pricingSource");
        Assert.Equal("negotiated.json", source.GetProperty("path").GetString());
        Assert.Equal(
            ["schemaVersion", "repository", "branch", "commit", "generatedAt", "generator", "pricingSource", "totals"],
            document.RootElement.EnumerateObject().Select(property => property.Name).Take(8));
        Assert.Equal(0.3584m, document.RootElement.GetProperty("totals").GetProperty("apiEquivalentUsd").GetDecimal());
        Assert.Equal(56m, document.RootElement.GetProperty("totals").GetProperty("aiCredits").GetDecimal());
    }

    // The JSON without its API-equivalent fields and pricingSource, at every depth.
    private static string WithoutApiEquivalent(JsonNode node)
    {
        JsonNode copy = node.DeepClone();
        Strip(copy);
        return copy.ToJsonString();

        static void Strip(JsonNode? item)
        {
            if (item is JsonObject value)
            {
                foreach (string name in new[] { "apiEquivalentUsd", "apiEquivalentUnpricedSpans", "apiEquivalentEstimatedSpans", "billedToApiRatio", "pricingSource" })
                {
                    value.Remove(name);
                }

                foreach (KeyValuePair<string, JsonNode?> property in value)
                {
                    Strip(property.Value);
                }
            }
            else if (item is JsonArray array)
            {
                foreach (JsonNode? element in array)
                {
                    Strip(element);
                }
            }
        }
    }

    // The LedgerSeeds ledger, recorded with the built-in rates, the GitFixture repository, and negotiated.json, a complete
    // rate card with RateFixtures.RetailTestRatesJson's fictional list prices.
    private static async Task<CliHarness> SeededHarnessAsync(string root)
    {
        GitFixture.Create(root);
        CliHarness harness = new(root, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(Path.Combine(root, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        await LedgerSeeds.SeedChainsAsync(store, Path.Combine(root, "repo"), Path.Combine(root, "api"));
        string negotiated = Path.Combine(root, "negotiated.json");
        await File.WriteAllTextAsync(negotiated, RateFixtures.RetailTestRatesJson);
        await FileRetry.ReadAllTextAsync(negotiated);
        return harness;
    }

    // Every row of spans, each as its values separated by '|'.
    private static async Task<string[]> RowsAsync(string root)
    {
        await using SqliteConnection connection = new($"Data Source={Path.Combine(root, "data", "ledger.db")}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM spans ORDER BY id;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        List<string> rows = [];
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join(
                '|',
                Enumerable.Range(0, reader.FieldCount)
                    .Select(index => reader.IsDBNull(index)
                        ? "null"
                        : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture))));
        }

        return [.. rows];
    }
}
```

Edit 1 of 4 in `docs/cli.md`, replace:

```markdown
| Command | Reports |
|---|---|
| `gh ledger status [--json]` | The daemon, ledger, rates, Copilot's export setting, warnings, and totals, billed amounts compared with API list prices, and cache hit ratios for today and the last 7 and 30 days |
| `gh ledger branch [--repo <key> \| --all-repos] [--since <period>] [--json]` | Spend by branch in the current repository, or in another or every repository; outside a Git repository, every repository |
| `gh ledger agents [--repo <key>] [--since <period>] [--json]` | Spend by agent in every repository, or in one with `--repo` |
| `gh ledger top-prompts [--repo <key>] [--since <period>] [--limit <n>] [--json]` | The most expensive prompt chains in every repository, or in one with `--repo`; `--limit` is 1 to 100 and defaults to 10 |
| `gh ledger status --cache ...` | Prompt-cache efficiency; see [Prompt-cache efficiency](#prompt-cache-efficiency) |

- A repository key is an `owner/repo` slug, a local repository root for a repository whose `origin` has no `owner/repo` path, or `(none)` for spend outside any repository.
```

with:

```markdown
| Command | Reports |
|---|---|
| `gh ledger status [--pricing <path>] [--json]` | The daemon, ledger, rates, Copilot's export setting, warnings, and totals, billed amounts compared with API list prices, and cache hit ratios for today and the last 7 and 30 days |
| `gh ledger branch [--repo <key> \| --all-repos] [--since <period>] [--pricing <path>] [--json]` | Spend by branch in the current repository, or in another or every repository; outside a Git repository, every repository |
| `gh ledger agents [--repo <key>] [--since <period>] [--pricing <path>] [--json]` | Spend by agent in every repository, or in one with `--repo` |
| `gh ledger top-prompts [--repo <key>] [--since <period>] [--limit <n>] [--pricing <path>] [--json]` | The most expensive prompt chains in every repository, or in one with `--repo`; `--limit` is 1 to 100 and defaults to 10 |
| `gh ledger status --cache ...` | Prompt-cache efficiency; see [Prompt-cache efficiency](#prompt-cache-efficiency) |

- A repository key is an `owner/repo` slug, a local repository root for a repository whose `origin` has no `owner/repo` path, or `(none)` for spend outside any repository.
```

Edit 2 of 4 in `docs/cli.md`, replace:

```markdown
- `status --json` periods also add `lenses`, before `cache`. `lenses.copilot` covers the GitHub-billed requests: `spans`, `unpricedSpans`, `aiCredits`, `aiCreditsUsd`, the three API-equivalent fields, and `billedToApiRatio`, the exact `GitHub/API` ratio or `null`. `lenses.local` covers the local requests: `spans`, `localSavedUsd`, and the three API-equivalent fields.

## Prompt-cache efficiency

Copilot reports how many of each request's input tokens the model read from its prompt cache and how many it wrote to it. Cache reads cost much less than uncached input, so a falling hit ratio, or a context that grows on every request, raises spend without changing the result. For a walkthrough, see [Find context bloat](guide.md#find-context-bloat).
```

with:

```markdown
- `status --json` periods also add `lenses`, before `cache`. `lenses.copilot` covers the GitHub-billed requests: `spans`, `unpricedSpans`, `aiCredits`, `aiCreditsUsd`, the three API-equivalent fields, and `billedToApiRatio`, the exact `GitHub/API` ratio or `null`. `lenses.local` covers the local requests: `spans`, `localSavedUsd`, and the three API-equivalent fields.

`--pricing <path>` prices the API-equivalent amounts with a complete rates file instead of the results recorded with each request, for example a price list your company negotiated, and changes nothing else; see [Compare with other prices](configuration.md#compare-with-other-prices---pricing). The report's second line, or the line after `Rates:` in `status`, names the file, and the JSON adds `pricingSource`: `kind` (`whatIf`), `path`, `ratesVersion`, and `apiRateCardSha256`, after `repository`, or after `ratesError` in `status`. `--pricing` can't be used with `--cache`. A file that can't be used stops the command with `Invalid pricing file:` and the problem.

## Prompt-cache efficiency

Copilot reports how many of each request's input tokens the model read from its prompt cache and how many it wrote to it. Cache reads cost much less than uncached input, so a falling hit ratio, or a context that grows on every request, raises spend without changing the result. For a walkthrough, see [Find context bloat](guide.md#find-context-bloat).
```

Edit 3 of 4 in `docs/cli.md`, replace:

```markdown
- `--stdout` writes the Markdown to standard output instead of the file.
- `--json` writes the JSON data to standard output instead of the file.

`export-pr` needs a branch with at least one commit and a GitHub remote named `origin`, and it refuses a receipt over 65,536 bytes, the most the action posts.
```

with:

```markdown
- `--stdout` writes the Markdown to standard output instead of the file.
- `--json` writes the JSON data to standard output instead of the file.
- `--pricing <path>` prices the API-equivalent line and column with another rates file, as the reports do. The receipt names the file and its `ratesVersion` but not its folder, and the JSON adds `pricingSource` after `generator`.

`export-pr` needs a branch with at least one commit and a GitHub remote named `origin`, and it refuses a receipt over 65,536 bytes, the most the action posts.
```

Edit 4 of 4 in `docs/cli.md`, replace:

```markdown
## Rates

- `gh ledger rates init [--force]` creates a starter rates overlay, `rates.json` in the data directory.
- `gh ledger rates validate [--file <path>]` checks the overlay, or another file, with the same rules the daemon uses.
- `gh ledger rates update` downloads `rates.v2.json` from the latest gh-ledger release, or its `rates.json` if it has none, and keeps it when it's newer than the rates in use, has API list prices (`schemaVersion` 2), and your overlay still applies. It uses your system proxy. Restart the daemon afterward; it reads rates when it starts.
- `gh ledger rates backfill-api-equivalent [--dry-run]` gives the requests recorded before gh-ledger had API list prices their API-equivalent prices, with the rates in use, and prints how many were priced. It changes nothing else, and `--dry-run` writes nothing. See [API-equivalent prices](configuration.md#api-equivalent-prices).
```

with:

```markdown
## Rates

- `gh ledger rates init [--force]` creates a starter rates overlay, `rates.json` in the data directory.
- `gh ledger rates validate [--file <path> | --pricing <path>]` checks the overlay, or another file, with the same rules the daemon uses. With `--pricing`, it checks a complete rates file the way the reports' `--pricing` does.
- `gh ledger rates update` downloads `rates.v2.json` from the latest gh-ledger release, or its `rates.json` if it has none, and keeps it when it's newer than the rates in use, has API list prices (`schemaVersion` 2), and your overlay still applies. It uses your system proxy. Restart the daemon afterward; it reads rates when it starts.
- `gh ledger rates backfill-api-equivalent [--dry-run]` gives the requests recorded before gh-ledger had API list prices their API-equivalent prices, with the rates in use, and prints how many were priced. It changes nothing else, and `--dry-run` writes nothing. See [API-equivalent prices](configuration.md#api-equivalent-prices).
```

Edit 1 of 1 in `docs/configuration.md`, replace:

```markdown
- `description` is optional text.

## Field reference

Overlay sections:
```

with:

```markdown
- `description` is optional text.

An overlay changes the API-equivalent prices of requests recorded from then on. To see what requests already recorded would cost at other prices, use `--pricing` instead.

### Compare with other prices: `--pricing`

`gh ledger status`, `branch`, `agents`, `top-prompts`, and `export-pr` take `--pricing <path>`, a rates file whose API list prices replace the recorded API-equivalent amounts in that report:

- The file is a complete rates document with `"schemaVersion": 2`, like the built-in rates, not an overlay. It needs every section, and neither your overlay nor downloaded rates apply to it. A release's `rates.v2.json` is a good start.
- Only the API-equivalent amounts change. AI credits, GitHub USD, BYOK USD, local savings, budgets, and the circuit breaker keep their recorded values, and nothing is written to the ledger.
- Each request is priced as CopilotLedger would price it with that file when recording it: as of the request's start time, and a local request as the file's local baseline. Requests recorded before gh-ledger had API list prices are priced too.
- Reports name the file in a `Pricing:` line, and their JSON adds `pricingSource` with its `path`, `ratesVersion`, and `apiRateCardSha256`. A receipt names the file without its folder.
- `gh ledger rates validate --pricing <path>` checks a file with the same rules and messages.

## Field reference

Overlay sections:
```

Edit 1 of 1 in `docs/guide.md`, replace:

```markdown
- `API equiv USD` is what the same requests would cost at each model's API list price, for comparison only. It shows `Unpriced` when no request had a list price, `*` when some had none and are left out, such as MAI models and local models, and `~` when it's an estimate because some requests didn't report their cache use. See [API-equivalent prices](configuration.md#api-equivalent-prices).
- In `status`, the second table compares what GitHub billed with the same requests at API list prices, and local savings with the local requests at API list prices. `GitHub/API` is their ratio, shown only when every GitHub request has both prices.
- `branch` covers the current repository. Add `--all-repos` for every repository, `--since` for another period, such as `today`, `7d`, `all`, or a date, and `--json` for exact amounts.

## Find the most expensive prompts
```

with:

```markdown
- `API equiv USD` is what the same requests would cost at each model's API list price, for comparison only. It shows `Unpriced` when no request had a list price, `*` when some had none and are left out, such as MAI models and local models, and `~` when it's an estimate because some requests didn't report their cache use. See [API-equivalent prices](configuration.md#api-equivalent-prices).
- In `status`, the second table compares what GitHub billed with the same requests at API list prices, and local savings with the local requests at API list prices. `GitHub/API` is their ratio, shown only when every GitHub request has both prices.
- To compare with other prices, such as a price list your company negotiated, add `--pricing <file>` to `status`, `branch`, `agents`, `top-prompts`, or `export-pr`. Only the API-equivalent amounts change; see [Compare with other prices](configuration.md#compare-with-other-prices---pricing).
- `branch` covers the current repository. Add `--all-repos` for every repository, `--since` for another period, such as `today`, `7d`, `all`, or a date, and `--json` for exact amounts.

## Find the most expensive prompts
```

### Tests

`WhatIfCommandTests` records the `LedgerSeeds` ledger with the built-in card and writes `negotiated.json`, a copy of `RetailTestRatesJson`.

- **WF1**: `branch --all-repos --since all --pricing negotiated.json` shows the `Pricing:` line, the what-if amounts (`Unpriced`, `~$0.36*`, `~$0.30`, `Unpriced`), the recorded billed amounts, and both footnotes, exactly.
- **WF2** (4: `status`, `branch`, `agents`, `top-prompts`): the JSON with `--pricing` differs from the JSON without it, has the exact `pricingSource`, and equals it once the API-equivalent fields and `pricingSource` are removed at every depth, warnings included; no row of the ledger changes.
- **WF3** (5: missing, invalid JSON, `schemaVersion` 1, `schemaVersion` 3, and an overlay-like file): all five commands and `rates validate --pricing` exit 1 with the same message and no output.
- **WF4**: `rates validate --pricing` accepts the file; `--file` with `--pricing`, and `status --cache --pricing`, fail.
- **WF5**: `export-pr --stdout --pricing` labels the line with the file name only and prices it at `$0.36` with one turn unpriced and one estimated; the JSON has `pricingSource.path` `negotiated.json` after `generator`, `apiEquivalentUsd` `0.3584`, and the recorded `aiCredits` 56.

### Acceptance criteria

- `--pricing` changes only the API-equivalent comparison on `status`, `branch`, `agents`, `top-prompts`, and `export-pr`, labels the source, keeps every recorded amount and warning, and writes nothing (WF1, WF2, WF5).
- An invalid file fails everywhere with the message `rates validate --pricing` gives (WF3, deviation D4).
- `dotnet test CopilotLedger.slnx` passes **982** tests (970 + 12).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 11 specification: API-Equivalent USD over MCP

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Mcp/LedgerMcpTools.cs` | The three fields; `orderBy: apiEquivalentUsd`; the schema and `get_branch_ledger`'s description |
| `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs` | Expected results; 3 more MC8 cases; new MC15 |
| `docs/cli.md`, `docs/guide.md`, `README.md` | The MCP fields; the `mcp` sample; the feature |

### Contracts

- `get_branch_ledger`, `get_agent_breakdown` rows, and `get_top_expensive_prompts` prompts add `apiEquivalentUsd` (a number or `null`), `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans` after `localSavedUsd`.
- `orderBy` is `cost`, `outputTokens`, `failedToolCalls`, or `apiEquivalentUsd`; the schema's enum and the error message `orderBy must be cost, outputTokens, failedToolCalls, or apiEquivalentUsd.` say so. `apiEquivalentUsd` orders by whether a chain is priced, then by its amount, highest first, then by the existing tiebreakers.
- No tool gains an argument; `ReadOnlyHint`, `OpenWorldHint`, budgets, and the `WARNING:` lines don't change.

### Code (copy exactly)

Edit 1 of 8 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            "branch": { "type": "string", "minLength": 1, "description": "Branch name, or (none). Pass with repository." },
            "limit": { "type": "integer", "minimum": 1, "maximum": 20, "default": 5, "description": "Number of prompt chains." },
            "orderBy": { "type": "string", "enum": ["cost", "outputTokens", "failedToolCalls"], "default": "cost", "description": "Sort key, highest first." }
          },
          "additionalProperties": false
```

with:

```csharp
            "branch": { "type": "string", "minLength": 1, "description": "Branch name, or (none). Pass with repository." },
            "limit": { "type": "integer", "minimum": 1, "maximum": 20, "default": 5, "description": "Number of prompt chains." },
            "orderBy": { "type": "string", "enum": ["cost", "outputTokens", "failedToolCalls", "apiEquivalentUsd"], "default": "cost", "description": "Sort key, highest first. With apiEquivalentUsd, prompt chains without an API-equivalent price come last." }
          },
          "additionalProperties": false
```

Edit 2 of 8 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            Name = "get_branch_ledger",
            Title = "Branch ledger",
            Description = "Spend on one Git branch: AI credits, USD, BYOK USD, local savings, top model, budget, and circuit-breaker status. Uses the current repository and branch unless you pass both.",
            InputSchema = BranchSchema.Clone(),
            Annotations = new ToolAnnotations
```

with:

```csharp
            Name = "get_branch_ledger",
            Title = "Branch ledger",
            Description = "Spend on one Git branch: AI credits, USD, BYOK USD, local savings, API-equivalent USD (the same requests at API list prices, for comparison only), top model, budget, and circuit-breaker status. Uses the current repository and branch unless you pass both.",
            InputSchema = BranchSchema.Clone(),
            Annotations = new ToolAnnotations
```

Edit 3 of 8 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            if (orderByElement.ValueKind != JsonValueKind.String)
            {
                return Error("orderBy must be cost, outputTokens, or failedToolCalls.");
            }

            string? requested = orderByElement.GetString();
            if (requested is not ("cost" or "outputTokens" or "failedToolCalls"))
            {
                return Error("orderBy must be cost, outputTokens, or failedToolCalls.");
            }

            orderBy = requested;
```

with:

```csharp
            if (orderByElement.ValueKind != JsonValueKind.String)
            {
                return Error("orderBy must be cost, outputTokens, failedToolCalls, or apiEquivalentUsd.");
            }

            string? requested = orderByElement.GetString();
            if (requested is not ("cost" or "outputTokens" or "failedToolCalls" or "apiEquivalentUsd"))
            {
                return Error("orderBy must be cost, outputTokens, failedToolCalls, or apiEquivalentUsd.");
            }

            orderBy = requested;
```

Edit 4 of 8 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            ["foundryByokUsd"] = Money(total.FoundryByokUsd),
            ["localSavedUsd"] = Money(total.LocalSavedUsd),
            ["billedAiCredits"] = Money(
                BilledAiCredits(total.AiCredits, total.FoundryByokUsd)),
```

with:

```csharp
            ["foundryByokUsd"] = Money(total.FoundryByokUsd),
            ["localSavedUsd"] = Money(total.LocalSavedUsd),
            ["apiEquivalentUsd"] = NullableMoney(total.ApiEquivalentUsd),
            ["apiEquivalentUnpricedSpans"] = total.ApiEquivalentUnpricedSpanCount,
            ["apiEquivalentEstimatedSpans"] = total.ApiEquivalentEstimatedSpanCount,
            ["billedAiCredits"] = Money(
                BilledAiCredits(total.AiCredits, total.FoundryByokUsd)),
```

Edit 5 of 8 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            "failedToolCalls" => candidates.OrderByDescending(
                row => failures.GetValueOrDefault(row.TraceId)),
            _ => candidates.OrderByDescending(
                row => row.AiCreditsUsd + row.FoundryByokUsd),
```

with:

```csharp
            "failedToolCalls" => candidates.OrderByDescending(
                row => failures.GetValueOrDefault(row.TraceId)),
            "apiEquivalentUsd" => candidates
                .OrderBy(row => row.ApiEquivalentUsd is null)
                .ThenByDescending(row => row.ApiEquivalentUsd ?? 0m),
            _ => candidates.OrderByDescending(
                row => row.AiCreditsUsd + row.FoundryByokUsd),
```

Edit 6 of 8 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
                ["cacheWriteInputTokens"] = row.CacheWriteInputTokens,
                ["aiCredits"] = Money(row.AiCredits),
                ["aiCreditsUsd"] = Money(row.AiCreditsUsd),
                ["foundryByokUsd"] = Money(row.FoundryByokUsd),
                ["localSavedUsd"] = Money(row.LocalSavedUsd),
                ["billedAiCredits"] = Money(
                    BilledAiCredits(row.AiCredits, row.FoundryByokUsd)),
```

with:

```csharp
                ["cacheWriteInputTokens"] = row.CacheWriteInputTokens,
                ["aiCredits"] = Money(row.AiCredits),
                ["aiCreditsUsd"] = Money(row.AiCreditsUsd),
                ["foundryByokUsd"] = Money(row.FoundryByokUsd),
                ["localSavedUsd"] = Money(row.LocalSavedUsd),
                ["apiEquivalentUsd"] = NullableMoney(row.ApiEquivalentUsd),
                ["apiEquivalentUnpricedSpans"] = row.ApiEquivalentUnpricedSpanCount,
                ["apiEquivalentEstimatedSpans"] = row.ApiEquivalentEstimatedSpanCount,
                ["billedAiCredits"] = Money(
                    BilledAiCredits(row.AiCredits, row.FoundryByokUsd)),
```

Edit 7 of 8 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
                ["foundryByokUsd"] = Money(row.FoundryByokUsd),
                ["localSavedUsd"] = Money(row.LocalSavedUsd),
                ["billedAiCredits"] = Money(
                    BilledAiCredits(row.AiCredits, row.FoundryByokUsd)),
```

with:

```csharp
                ["foundryByokUsd"] = Money(row.FoundryByokUsd),
                ["localSavedUsd"] = Money(row.LocalSavedUsd),
                ["apiEquivalentUsd"] = NullableMoney(row.ApiEquivalentUsd),
                ["apiEquivalentUnpricedSpans"] = row.ApiEquivalentUnpricedSpanCount,
                ["apiEquivalentEstimatedSpans"] = row.ApiEquivalentEstimatedSpanCount,
                ["billedAiCredits"] = Money(
                    BilledAiCredits(row.AiCredits, row.FoundryByokUsd)),
```

Edit 8 of 8 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            value.ToString("0.############", CultureInfo.InvariantCulture))!;

    // value with every digit decimal division gave it, or null.
    private static JsonNode? Ratio(decimal? value) =>
```

with:

```csharp
            value.ToString("0.############", CultureInfo.InvariantCulture))!;

    // An API-equivalent amount, or null when nothing was priced: never 0 for an unknown price.
    private static JsonNode? NullableMoney(decimal? value) =>
        value is null ? null : Money(value.Value);

    // value with every digit decimal division gave it, or null.
    private static JsonNode? Ratio(decimal? value) =>
```

Edit 1 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
            "branch": { "type": "string", "minLength": 1, "description": "Branch name, or (none). Pass with repository." },
            "limit": { "type": "integer", "minimum": 1, "maximum": 20, "default": 5, "description": "Number of prompt chains." },
            "orderBy": { "type": "string", "enum": ["cost", "outputTokens", "failedToolCalls"], "default": "cost", "description": "Sort key, highest first." }
          },
          "additionalProperties": false
```

with:

```csharp
            "branch": { "type": "string", "minLength": 1, "description": "Branch name, or (none). Pass with repository." },
            "limit": { "type": "integer", "minimum": 1, "maximum": 20, "default": 5, "description": "Number of prompt chains." },
            "orderBy": { "type": "string", "enum": ["cost", "outputTokens", "failedToolCalls", "apiEquivalentUsd"], "default": "cost", "description": "Sort key, highest first. With apiEquivalentUsd, prompt chains without an API-equivalent price come last." }
          },
          "additionalProperties": false
```

Edit 2 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
          "foundryByokUsd": 0,
          "localSavedUsd": 0.23,
          "billedAiCredits": 176.5,
          "topModel": "mai-code-2",
```

with:

```csharp
          "foundryByokUsd": 0,
          "localSavedUsd": 0.23,
          "apiEquivalentUsd": 0.7,
          "apiEquivalentUnpricedSpans": 3,
          "apiEquivalentEstimatedSpans": 1,
          "billedAiCredits": 176.5,
          "topModel": "mai-code-2",
```

Edit 3 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
                "get_branch_ledger",
                "Branch ledger",
                "Spend on one Git branch: AI credits, USD, BYOK USD, local savings, top model, budget, and circuit-breaker status. Uses the current repository and branch unless you pass both.",
                BranchSchema,
                readOnly: true,
```

with:

```csharp
                "get_branch_ledger",
                "Branch ledger",
                "Spend on one Git branch: AI credits, USD, BYOK USD, local savings, API-equivalent USD (the same requests at API list prices, for comparison only), top model, budget, and circuit-breaker status. Uses the current repository and branch unless you pass both.",
                BranchSchema,
                readOnly: true,
```

Edit 4 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
                """{"repository":"contoso/api","branch":"main"}"""),
            """
            {"repository":"contoso/api","branch":"main","spans":1,"unpricedSpans":0,"inputTokens":100000,"outputTokens":10000,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"uncachedInputTokens":null,"cacheReportedSpans":0,"cacheHitRatio":null,"aiCredits":0,"aiCreditsUsd":0,"foundryByokUsd":0.315,"localSavedUsd":0,"billedAiCredits":31.5,"topModel":"gpt-6.1-sol","budget":null,"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
        await AssertResultAsync(
```

with:

```csharp
                """{"repository":"contoso/api","branch":"main"}"""),
            """
            {"repository":"contoso/api","branch":"main","spans":1,"unpricedSpans":0,"inputTokens":100000,"outputTokens":10000,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"uncachedInputTokens":null,"cacheReportedSpans":0,"cacheHitRatio":null,"aiCredits":0,"aiCreditsUsd":0,"foundryByokUsd":0.315,"localSavedUsd":0,"apiEquivalentUsd":null,"apiEquivalentUnpricedSpans":1,"apiEquivalentEstimatedSpans":0,"billedAiCredits":31.5,"topModel":"gpt-6.1-sol","budget":null,"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
        await AssertResultAsync(
```

Edit 5 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
                """{"repository":"swigerb/copilot-ledger","branch":"nope"}"""),
            """
            {"repository":"swigerb/copilot-ledger","branch":"nope","spans":0,"unpricedSpans":0,"inputTokens":0,"outputTokens":0,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"uncachedInputTokens":null,"cacheReportedSpans":0,"cacheHitRatio":null,"aiCredits":0,"aiCreditsUsd":0,"foundryByokUsd":0,"localSavedUsd":0,"billedAiCredits":0,"topModel":null,"budget":null,"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
    }
```

with:

```csharp
                """{"repository":"swigerb/copilot-ledger","branch":"nope"}"""),
            """
            {"repository":"swigerb/copilot-ledger","branch":"nope","spans":0,"unpricedSpans":0,"inputTokens":0,"outputTokens":0,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"uncachedInputTokens":null,"cacheReportedSpans":0,"cacheHitRatio":null,"aiCredits":0,"aiCreditsUsd":0,"foundryByokUsd":0,"localSavedUsd":0,"apiEquivalentUsd":0,"apiEquivalentUnpricedSpans":0,"apiEquivalentEstimatedSpans":0,"billedAiCredits":0,"topModel":null,"budget":null,"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
    }
```

Edit 6 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
            await CallAsync(harness, "get_agent_breakdown", "{}"),
            """
            {"repository":"swigerb/copilot-ledger","branch":"feature/ledger","agents":[{"agent":"(none)","spans":2,"unpricedSpans":0,"inputTokens":110000,"outputTokens":250000,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"uncachedInputTokens":null,"cacheReportedSpans":0,"cacheHitRatio":null,"aiCredits":120.5,"aiCreditsUsd":1.205,"foundryByokUsd":0,"localSavedUsd":0,"billedAiCredits":120.5},{"agent":"ledger-builder","spans":2,"unpricedSpans":0,"inputTokens":1400000,"outputTokens":112000,"cacheReadInputTokens":400000,"cacheWriteInputTokens":0,"uncachedInputTokens":600000,"cacheReportedSpans":1,"cacheHitRatio":0.4,"aiCredits":56,"aiCreditsUsd":0.56,"foundryByokUsd":0,"localSavedUsd":0.23,"billedAiCredits":56}],"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
    }
```

with:

```csharp
            await CallAsync(harness, "get_agent_breakdown", "{}"),
            """
            {"repository":"swigerb/copilot-ledger","branch":"feature/ledger","agents":[{"agent":"(none)","spans":2,"unpricedSpans":0,"inputTokens":110000,"outputTokens":250000,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"uncachedInputTokens":null,"cacheReportedSpans":0,"cacheHitRatio":null,"aiCredits":120.5,"aiCreditsUsd":1.205,"foundryByokUsd":0,"localSavedUsd":0,"apiEquivalentUsd":0.7,"apiEquivalentUnpricedSpans":1,"apiEquivalentEstimatedSpans":1,"billedAiCredits":120.5},{"agent":"ledger-builder","spans":2,"unpricedSpans":0,"inputTokens":1400000,"outputTokens":112000,"cacheReadInputTokens":400000,"cacheWriteInputTokens":0,"uncachedInputTokens":600000,"cacheReportedSpans":1,"cacheHitRatio":0.4,"aiCredits":56,"aiCreditsUsd":0.56,"foundryByokUsd":0,"localSavedUsd":0.23,"apiEquivalentUsd":null,"apiEquivalentUnpricedSpans":2,"apiEquivalentEstimatedSpans":0,"billedAiCredits":56}],"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
    }
```

Edit 7 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
        { "get_top_expensive_prompts", """{"limit":5.5}""", "limit must be an integer from 1 to 20." },
        { "get_top_expensive_prompts", """{"limit":"5"}""", "limit must be an integer from 1 to 20." },
        { "get_top_expensive_prompts", """{"orderBy":"Cost"}""", "orderBy must be cost, outputTokens, or failedToolCalls." },
        { "get_top_expensive_prompts", """{"orderBy":null}""", "orderBy must be cost, outputTokens, or failedToolCalls." },
        { "set_branch_budget", "{}", "aiCredits is required." },
        { "set_branch_budget", """{"aiCredits":0}""", "aiCredits must be a number greater than 0 and at most 1000000, with at most 2 decimal places." },
```

with:

```csharp
        { "get_top_expensive_prompts", """{"limit":5.5}""", "limit must be an integer from 1 to 20." },
        { "get_top_expensive_prompts", """{"limit":"5"}""", "limit must be an integer from 1 to 20." },
        { "get_top_expensive_prompts", """{"orderBy":"Cost"}""", "orderBy must be cost, outputTokens, failedToolCalls, or apiEquivalentUsd." },
        { "get_top_expensive_prompts", """{"orderBy":null}""", "orderBy must be cost, outputTokens, failedToolCalls, or apiEquivalentUsd." },
        { "get_top_expensive_prompts", """{"pricing":"rates.json"}""", "Unknown argument: pricing." },
        { "get_branch_ledger", """{"pricing":"rates.json"}""", "Unknown argument: pricing." },
        { "get_agent_breakdown", """{"pricing":"rates.json"}""", "Unknown argument: pricing." },
        { "set_branch_budget", "{}", "aiCredits is required." },
        { "set_branch_budget", """{"aiCredits":0}""", "aiCredits must be a number greater than 0 and at most 1000000, with at most 2 decimal places." },
```

Edit 8 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
        new(Path.Combine(root, "data", "ledger.db"), new ManualTimeProvider(Now));

    private static async Task SeedAsync(string root)
    {
```

with:

```csharp
        new(Path.Combine(root, "data", "ledger.db"), new ManualTimeProvider(Now));

    // Prompt chains with an API-equivalent price come first, highest first; the rest keep their cost order.
    [Fact]
    public async Task MC15_TopPromptsByApiEquivalentUsd()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        SqliteLedgerStore store = Store(temp.Path);
        await store.InitializeAsync();
        JsonRateCardCalculator rates = JsonRateCardCalculator.LoadEmbedded();
        (string Trace, string Model, long Input, long Output)[] requests =
        [
            ("a", "claude-sonnet-5.5", 10_000, 1_000),
            ("b", "mai-code-2", 1_000_000, 100_000),
            ("c", "claude-opus-5.5", 100_000, 10_000),
        ];
        await store.RecordAsync(requests.Select(request =>
        {
            NormalizedGenAiSpan span = TestSpans.Chat(
                startTime: Now,
                requestModel: request.Model,
                inputTokens: request.Input,
                outputTokens: request.Output) with
            {
                TraceId = new string(request.Trace[0], 32),
                SpanId = new string(request.Trace[0], 16),
            };
            return new LedgerEntry(span, rates.Calculate(span), GitFixture.Feature(temp.Path));
        }));
        await using McpTestHarness harness = await StartAsync(Context(temp.Path, []));

        CallToolResult byCost = await CallAsync(harness, "get_top_expensive_prompts", "{}");
        CallToolResult byApi = await CallAsync(
            harness,
            "get_top_expensive_prompts",
            """{"orderBy":"apiEquivalentUsd"}""");

        Assert.Equal(["b", "c", "a"], Traces(byCost));
        Assert.Equal(["c", "a", "b"], Traces(byApi));
        JsonNode result = JsonNode.Parse(Assert.NotNull(byApi.StructuredContent).GetRawText())!;
        Assert.Equal("apiEquivalentUsd", result["orderBy"]!.GetValue<string>());
        Assert.Equal(
            "[0.6,0.03,null]",
            new JsonArray(result["prompts"]!.AsArray()
                .Select(prompt => prompt!["apiEquivalentUsd"]?.DeepClone())
                .ToArray()).ToJsonString());

        static string[] Traces(CallToolResult result) =>
            JsonNode.Parse(Assert.NotNull(result.StructuredContent).GetRawText())!["prompts"]!.AsArray()
                .Select(prompt => prompt!["traceId"]!.GetValue<string>()[..1])
                .ToArray();
    }

    private static async Task SeedAsync(string root)
    {
```

Edit 9 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
        ["foundryByokUsd"] = 0,
        ["localSavedUsd"] = 0,
        ["billedAiCredits"] = 70,
        ["failedToolCalls"] = 5,
```

with:

```csharp
        ["foundryByokUsd"] = 0,
        ["localSavedUsd"] = 0,
        ["apiEquivalentUsd"] = 0.7m,
        ["apiEquivalentUnpricedSpans"] = 0,
        ["apiEquivalentEstimatedSpans"] = 1,
        ["billedAiCredits"] = 70,
        ["failedToolCalls"] = 5,
```

Edit 10 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
        ["foundryByokUsd"] = 0,
        ["localSavedUsd"] = 0.23m,
        ["billedAiCredits"] = 56,
        ["failedToolCalls"] = 0,
```

with:

```csharp
        ["foundryByokUsd"] = 0,
        ["localSavedUsd"] = 0.23m,
        ["apiEquivalentUsd"] = null,
        ["apiEquivalentUnpricedSpans"] = 2,
        ["apiEquivalentEstimatedSpans"] = 0,
        ["billedAiCredits"] = 56,
        ["failedToolCalls"] = 0,
```

Edit 11 of 11 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
        ["foundryByokUsd"] = 0,
        ["localSavedUsd"] = 0,
        ["billedAiCredits"] = 50.5m,
        ["failedToolCalls"] = 1,
```

with:

```csharp
        ["foundryByokUsd"] = 0,
        ["localSavedUsd"] = 0,
        ["apiEquivalentUsd"] = null,
        ["apiEquivalentUnpricedSpans"] = 1,
        ["apiEquivalentEstimatedSpans"] = 0,
        ["billedAiCredits"] = 50.5m,
        ["failedToolCalls"] = 1,
```

Edit 1 of 2 in `docs/cli.md`, replace:

```markdown
The server has five tools. All but `get_cache_efficiency` use the current repository and branch unless you pass `repository` and `branch`:

- `get_branch_ledger`: the branch's spend, top model, budget, circuit-breaker state, and prompt-cache hit ratio.
- `get_top_expensive_prompts`: the branch's most expensive prompt chains, with output tokens and failed tool calls; `limit` (1 to 20, default 5) and `orderBy` (`cost`, `outputTokens`, or `failedToolCalls`).
- `get_agent_breakdown`: the branch's spend and prompt-cache hit ratio by agent.
- `get_cache_efficiency`: prompt-cache efficiency in the current repository, or in `repository`, like `status --cache`. `by` groups rows by `model` (the default), `branch`, `pr`, or `agent`; `branch` and `pullRequest` keep one branch or pull request; `since` takes the same periods as `--since`; and `limit` (1 to 50, default 10) keeps the rows with the most input.
```

with:

```markdown
The server has five tools. All but `get_cache_efficiency` use the current repository and branch unless you pass `repository` and `branch`:

- `get_branch_ledger`: the branch's spend, top model, budget, circuit-breaker state, and prompt-cache hit ratio.
- `get_top_expensive_prompts`: the branch's most expensive prompt chains, with output tokens and failed tool calls; `limit` (1 to 20, default 5) and `orderBy` (`cost`, `outputTokens`, `failedToolCalls`, or `apiEquivalentUsd`, which lists the chains without an API-equivalent price last).
- `get_agent_breakdown`: the branch's spend and prompt-cache hit ratio by agent.
- `get_cache_efficiency`: prompt-cache efficiency in the current repository, or in `repository`, like `status --cache`. `by` groups rows by `model` (the default), `branch`, `pr`, or `agent`; `branch` and `pullRequest` keep one branch or pull request; `since` takes the same periods as `--since`; and `limit` (1 to 50, default 10) keeps the rows with the most input.
```

Edit 2 of 2 in `docs/cli.md`, replace:

```markdown
The first four are read-only. `set_branch_budget` changes only the budget, so Copilot asks before running it; in prompt mode, allow it with `--allow-tool 'ledger(set_branch_budget)'`. While the breaker is open for the branch or the session, or the branch is over budget, every result starts with a `WARNING:` line; for `get_cache_efficiency`, the branch is the current one. No tool can reset the breaker.

## Rates

- `gh ledger rates init [--force]` creates a starter rates overlay, `rates.json` in the data directory.
```

with:

```markdown
The first four are read-only. `set_branch_budget` changes only the budget, so Copilot asks before running it; in prompt mode, allow it with `--allow-tool 'ledger(set_branch_budget)'`. While the breaker is open for the branch or the session, or the branch is over budget, every result starts with a `WARNING:` line; for `get_cache_efficiency`, the branch is the current one. No tool can reset the breaker.

`get_branch_ledger`, `get_top_expensive_prompts`, and `get_agent_breakdown` add `apiEquivalentUsd` (`null` when no request has an API-equivalent price), `apiEquivalentUnpricedSpans`, and `apiEquivalentEstimatedSpans` after `localSavedUsd`, as the reports do; see [API-equivalent USD](#api-equivalent-usd). They're for comparison: budgets and `billedAiCredits` count only billed spend. No tool takes a file path, so `--pricing` isn't available to Copilot.

## Rates

- `gh ledger rates init [--force]` creates a starter rates overlay, `rates.json` in the data directory.
```

Edit 1 of 1 in `docs/guide.md`, replace:

```markdown
  "foundryByokUsd": 0.49,
  "localSavedUsd": 0.1725,
  "billedAiCredits": 666.75,
  "topModel": "claude-opus-5.5",
```

with:

```markdown
  "foundryByokUsd": 0.49,
  "localSavedUsd": 0.1725,
  "apiEquivalentUsd": 6.49,
  "apiEquivalentUnpricedSpans": 2,
  "apiEquivalentEstimatedSpans": 1,
  "billedAiCredits": 666.75,
  "topModel": "claude-opus-5.5",
```

Edit 1 of 1 in `README.md`, replace:

```markdown
- An MCP server, so Copilot can check spend, budgets, and the circuit breaker
- Foundry BYOK prices, local-model savings, and `rates update` for new prices between releases

## Requirements
```

with:

```markdown
- An MCP server, so Copilot can check spend, budgets, and the circuit breaker
- Foundry BYOK prices, local-model savings, and `rates update` for new prices between releases
- API-equivalent USD: what the same requests would cost at each model's API list prices, or at prices you supply, beside what Copilot billed

## Requirements
```

### Tests

- MC1 (the prompt schema and branch description), MC2 and MC3 (`Branch`: `0.7`, 3 unpriced, 1 estimated, with the budget still `176.5` billed credits), MC4 (`null` for an unpriced branch, `0` for an empty one), MC5 (each prompt's fields), and MC6 (each agent's) expect the new fields. GS7's sample adds them too.
- MC8 gains three cases: `pricing` is an unknown argument of `get_top_expensive_prompts`, `get_branch_ledger`, and `get_agent_breakdown`; the `orderBy` messages change.
- **MC15**: three chains, `b` the most expensive but unpriced (`mai-code-2`), `c` and `a` priced; `cost` orders `b, c, a`, and `apiEquivalentUsd` orders `c, a, b` with `[0.6,0.03,null]`.

### Acceptance criteria

- The tools return the fields, `orderBy: apiEquivalentUsd` works, unknown arguments are rejected, and no argument takes a path (MC1–MC6, MC8, MC15).
- `dotnet test CopilotLedger.slnx` passes **986** tests (982 + 4).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Test specifications

Every test is hermetic: it uses `TempDirectory` for files and databases, `ManualTimeProvider` or fixed start times for time, `FakeCliPlatform`, `CliHarness`, and `FakeHttpHandler` for the CLI, and `McpTestHarness` or in-process pipes for MCP. No test starts a process, runs `git`, uses the network or Docker, or reads `~/.copilot-ledger`. Money is `decimal` (`TheoryData<…, decimal>` or `decimal` arguments, never `[InlineData(double)]`), and expected text is compared exactly, with `ReplaceLineEndings("\n")` only on the expected literal.

| Task | New tests (cases) | Changed tests | Total after |
|---|---|---|---:|
| 0 | — (`main` at `3aaf971`) | — | 804 |
| 1 | AK1 (10), AK2 (7), AK3, AK4, AK5, AK6, AK7 (5); 1 more RateCardLoadingTests case | Unsupported-version cases of RateCardLoadingTests, RateOverlayTests, and RatesCommandTests | 831 |
| 2 | AP1 (10), AP2 (4), AP3 (4), AP4, AP5, AP6, AP7, AP8 (7), AP9, AP10 (3) | — | 864 |
| 3 | AR1 (56), AR2 (4), AR3, AR4 (6), AR5, AR6 (2) | Tests that show the built-in `ratesVersion` | 934 |
| 4 | RU11 (2), RU12, RU13, RU14 | RU3 (`404`) | 939 |
| 5 | AS1, AS2, AS3, AS4, AS5 (5), IG11 | S1, SL1, SL8, SL10, SL11 (schema version 5) | 949 |
| 6 | AB1, AB2, AB3, RB1, RB2, RB3 | — | 955 |
| 7 | WI1 (5), WI2, WI3, WI4 | — | 963 |
| 8 | AE1, AE2, AE3, AE4, AE5 | StatusText, RP4, RP6, RP7, RP15; GS2 `status-first`, `status`, `branch`, `top-prompts`, `agents` (guide text) | 968 |
| 9 | EP11, EP12 | EP1, EP2, EP3, EP5, EP10; GS6 (guide text) | 970 |
| 10 | WF1, WF2 (4), WF3 (5), WF4, WF5 | — | 982 |
| 11 | MC8 (3 more cases), MC15 | MC1, MC2, MC3, MC4, MC5, MC6, MC9; GS7 (guide text) | 986 |

Exact `decimal` values used across tasks, all at `RateFixtures.RetailTestRatesJson`'s fictional prices unless noted:

| Value | What | Where |
|---|---|---|
| `0.3584` | 400,000 local input and 12,000 output at `test-baseline` ($0.80/$3.20) | AP4, WF5 |
| `2.67` | The same request at `test-retail`'s long-context prices | AP4 |
| `1.56` | 300,000 input (100,000 cache reads, 50,000 writes) and 10,000 output, long-context | AS1, AS3, WI3 |
| `1.8757` | `1.56 + 0.0112 + 0.3 + 0.0045` | AS2, AB1, WI3 |
| `0.222` | 100,000 input (40,000 cache reads) and 2,000 output, default tier | AE1–AE4 |
| `0.3333333333333333333333333333` | `0.074 / 0.222`, GitHub over API | AE3 |
| `2.86` | `claude-opus-5.5` at the built-in list price, `LedgerSeeds`' first request | StatusText, RP4, RB2, EP1 |
| `0.3` | `gpt-6.1-sol` at the built-in or fixture list price, 100,000 input and 10,000 output | AS2, AB2, RB2, WI2 |
| `0.7` | `claude-sonnet-5.5` at the built-in list price, 100,000 input and 50,000 output | MC2, MC3, MC5, MC6 |

## Final step: the pull request (orchestrator, not a builder task)

After Task 11, the orchestrator:

1. Runs `dotnet build CopilotLedger.slnx` (0 warnings, 0 errors) and `dotnet test CopilotLedger.slnx` (986 passed, 0 failed, 0 skipped), and checks with `git --no-pager diff --stat origin/main` that the branch changes only this plan, `.github/plans/archive/sprint-6.md`, and the files the tasks list.
2. Pushes `swigerb-sprint-7-dual-lens-costing-engine` and opens one pull request titled exactly "Sprint 7: Dual-lens costing engine: API-equivalent USD vs. Copilot AI Credits vs. local offload savings", whose body says `Closes #61`, lists the eleven task commits, and states the migration renumbering and deviations D1–D7 from "Design decisions", each with a one-line rationale.
3. Waits for CI on `ubuntu-latest` and `windows-latest`, including the smoke test of the published binary.
4. Asks `ledger-architect` to review the branch.

This sprint doesn't bump the version, tag, or release. The first release after it publishes `rates.v2.json` and the derived `rates.json`; its `rates` check compares the built-in card with v0.4.1's `rates.json` and requires the moved `ratesVersion`, which Task 3 sets. When the sprint closes, `ledger-architect` archives this plan with its amendments.

## Deferred to later sprints

- **#63, `gh ledger import`.** Imported Claude and Codex sessions have no Copilot credit channel, so the API-equivalent lens is their main cost view. Record them through `RecordAsync` with a `CreditCalculationResult` whose `ApiEquivalent` comes from `CalculateApiEquivalent`, so they get the five `api_*` columns; map `cacheDetailsReported` onto `CacheCounterState` as Sprint 6 noted (`RecordAsync` rejects `LegacyUnknown`). Its migration, if it needs one, is version 6.
- **#64, `gh ledger export`.** All three lenses are available: `LedgerRollupRow` and `PromptChainRow` carry the API-equivalent fields, `GetRollupAsync` and `GetTopPromptChainsAsync` take a `whatIf` card, and the `status --json` `lenses` object shows the per-channel comparison. An export should carry `apiEquivalentEstimatedSpans` and `pricingSource` so estimates and what-if prices stay labeled.
- **#65, `gh ledger audit`.** Waste estimates can use `ApiEquivalentUsd` per prompt chain, keeping estimated and unpriced spans apart as reports do.
- **Re-pricing unpriced rows.** The backfill fills only rows without a result, as the issue asks, so requests recorded as `unpriced` (all local requests and MAI models, with the built-in card) stay so after a later card prices them. A `--include-unpriced` option, or an overlay that sets `apiEquivalent.localBaselineModel` before requests are recorded, could change that; until then, `--pricing` compares them.
- **List prices for the MAI models**, when Microsoft publishes them, and a price for GPT-5.6 Sol after 2026-11-30.
- **Longer cache lifetimes.** Anthropic's 1-hour and OpenAI's longer cache writes cost more; the ledger doesn't know which lifetime a request used.
- **A `--pricing` sample in the guide.** `GuideSamplesTests` would need `GuideWorld` to provide a pricing file; the guide describes the option instead.
- **Carried from Sprint 6:** user turns (`github.copilot.turn_id`), agent definitions (`gen_ai.agent.id`), MCP tools for turns and bloat, a fresh capture of cache attributes, the optional follow-ups of Sprint 6's review, and Sprint 5's carry-overs, as described in `.github/plans/archive/sprint-6.md`.
