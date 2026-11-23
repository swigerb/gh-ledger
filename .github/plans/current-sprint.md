# Sprint 6: Prompt-Cache Efficiency & Per-Turn Token Breakdown

| | |
|---|---|
| Architect | `ledger-architect` |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET 10 (`net10.0`), SDK 10.0.x, zero Docker |
| Plan written | 2026-11-23, against merged Sprint 5 (`main` at `15003fb`, released as v0.4.1) |
| Requirements | Issue [#62](https://github.com/swigerb/gh-ledger/issues/62), "Prompt-cache efficiency & per-turn token breakdown" |
| Status | Current sprint. Sprint 5 is complete and archived at `.github/plans/archive/sprint-5.md`. |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification section, runs the verification commands, changes the line to `- [x]`, and stops. Tasks 1–11 are sequential, and each builds on the ones before it.

This sprint ships as one pull request. The orchestrator commits this plan first, then each task as its own commit, `Sprint 6 Task N: <Title>`, on branch `swigerb-prompt-cache-efficiency-per-turn-tokens`, and opens one pull request, "Sprint 6: Prompt-cache efficiency & per-turn token breakdown", with `Closes #62`. After every task, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors (warnings are errors), and `dotnet test CopilotLedger.slnx` passes exactly the number of tests the task gives, with 0 failed and 0 skipped.

All of this plan's code, tests, and docs are **copy exactly**. The architect built every task in a scratch worktree of `15003fb` outside this repository's worktree. Each task compiled with `TreatWarningsAsErrors` and passed the whole suite: 697, 721, 732, 734, 743, 749, 751, 768, 786, 801, and 804 tests after Tasks 1–11. The architect then applied this plan's blocks, task by task and in order, to a fresh copy of `15003fb`, and got the same files and the same counts. Copy each block character for character, including every comment line, `using` directive, blank line, and line of Markdown. Don't add argument checks (`ThrowIfNull` and the like), cancellation tokens, `ConfigureAwait`, logging, caching, or fast paths that the code doesn't show, and don't tidy code or reword docs.

Each task's specification lists its files, summarizes its contracts and algorithms for review, and then gives its code under "Code (copy exactly)". The summaries aren't code to write; the code section is. It has two kinds of blocks:

- **New file `<path>`:** write the block as the whole file, with `\n` line breaks, one `\n` at the end, and no byte order mark.
- **Edit k of m in `<path>`, replace: … with: …:** make a file's edits in the order given. Each "replace" block occurs exactly once in the file at the moment you make that edit, so search for it as an exact string. None of the files this sprint edits has a byte order mark; keep each file's line endings as they are.

Tests are code too: write every test file and test edit exactly as given, in the order given. Don't drop an assertion because it looks redundant, and don't turn an exact comparison into `Contains` or `StartsWith`. Never normalize the product's output in a test (no `ReplaceLineEndings`, `Replace("\r\n", …)`, or `Trim` on an actual value); `CliHarness` already turns `\r\n` into `\n` in `Out` and `Error`.

If a block doesn't apply, the build fails, or the test count differs from the task's, stop and report it. Don't change the plan's code to make it fit.

The final section, "Final step: the pull request", isn't a builder task. It has no checkbox, and `ledger-builder` never runs it.

## Tasks

- [x] Task 1: Cache Counter States (CopilotLedger.Core, Otlp & tests). New `CacheCounterState` and `CacheMetrics`. `NormalizedGenAiSpan` gains `CacheReadState` and `CacheWriteState`, and `GenAiSpanNormalizer` records whether each cache counter was present, with token values unchanged. `TestSpans.Chat` marks a nonzero counter as reported, so existing fixtures stay valid.
- [ ] Task 2: Pull Request Numbers from Git Config (CopilotLedger.Core & tests). `GitWorkspaceContext` gains an init-only `PullRequestNumber`, which `FileSystemGitContextResolver` reads from an explicit `[branch "<current>"] merge = refs/pull/<n>/head` in the repository's config and from nowhere else. `GitFixture.Create` can write that config.
- [ ] Task 3: Ledger Schema Version 4 (CopilotLedger.Storage & tests). Migration v4 adds the two cache-state columns, `pull_request_number`, `agent_span_id`, and an index on conversation and start time; upgrades legacy rows whose nonzero counters prove they were reported; and fills `agent_span_id` from the span links not yet pruned. `RecordAsync` writes and validates the new columns, and `BackfillFromAgentSpansAsync` records each span's nearest `invoke_agent` ancestor.
- [ ] Task 4: Cache Coverage in Rollups (CopilotLedger.Storage, Cli, Mcp & tests). `LedgerFilter` gains `PullRequestNumber` and `Agent`, which every filtered query honors through one shared SQL fragment. `LedgerRollupRow` gains the sums over spans that reported cache usage, `CacheUnreportedSpanCount`, and an exact `decimal?` `CacheHitRatio`.
- [ ] Task 5: Cache-Efficiency Query (CopilotLedger.Storage & tests). New `CacheEfficiencyRow`, `LedgerGrouping.PullRequest`, and `SqliteLedgerStore.GetCacheEfficiencyAsync`, which groups spans by model, agent, repository, branch, or pull request.
- [ ] Task 6: Turn Breakdown and Context-Bloat Queries (CopilotLedger.Storage & tests). New `TurnSelector`, `TurnTokenRow`, `BloatChainRow`, `InputGrowthRow`, and `ContextBloatReport`; `GetTurnBreakdownAsync` lists one trace's or conversation's requests with input growth per agent invocation and model, and `GetContextBloatAsync` ranks prompt chains by uncached input, estimated uncached input, and input growth.
- [ ] Task 7: Cache Hit in status, branch, and agents (CopilotLedger.Cli, docs & tests). Plain `status` adds a cache table for its three periods and a `cache` object to each JSON period, and `branch --json` and `agents --json` rows add `cacheReportedSpans`, `cacheUnreportedSpans`, and `cacheHitRatio`. The guide's two `status` samples change to match.
- [ ] Task 8: status --cache (CopilotLedger.Cli & tests). `gh ledger status --cache [--by model|branch|pr|agent] [--repo <key>] [--branch <name>] [--pr <n>] [--since <period>] [--json]`, with `n/a` for unknown values, `*` on partial totals, and a footnote that counts the spans without cache counters.
- [ ] Task 9: status --cache --turns and --bloat (CopilotLedger.Cli & tests). The per-request breakdown of one trace or conversation, and the context-bloat rankings, as tables and JSON, with their option rules.
- [ ] Task 10: MCP get_cache_efficiency (CopilotLedger.Storage, Cli, Mcp, docs & tests). A fifth MCP tool, read-only. `get_branch_ledger` and `get_agent_breakdown` add `uncachedInputTokens`, `cacheReportedSpans`, and `cacheHitRatio`. The `--since` grammar moves to a shared `LedgerPeriod`.
- [ ] Task 11: Docs and the Context-Bloat Walkthrough (docs & tests). `docs/cli.md` documents prompt-cache efficiency, `docs/configuration.md` explains how requests without cache counters are priced and reported, and `docs/guide.md` gains a tested "Find context bloat" walkthrough.

## Starting point

- Sprint 5 is complete. Tasks 1–5 merged in PRs #53, #54, #55, #56, and #59, plus the out-of-plan DM6 fix in PR #58 (`main` at `15003fb`). gh-ledger v0.4.1 is released at `15003fb`. On 2026-11-23, at `15003fb`, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors and `dotnet test CopilotLedger.slnx` passes 671 tests (0 failed, 0 skipped), with SDK 10.0.115 (`global.json` asks for 10.0.100 with `rollForward: latestFeature`). Sprint 5's plan is archived at `.github/plans/archive/sprint-5.md`, with its amendments.
- Issue #62 is the requirements source. Its "Prescriptive Implementation Plan" and acceptance criteria are implemented here, with the deviations listed under "Design decisions"; its names are proposals.
- Merged behavior this plan depends on (read from the code at `15003fb`):
  - **Normalizer.** `GenAiSpanNormalizer.TryNormalize` reads `gen_ai.usage.input_tokens`, `output_tokens`, `cache_read.input_tokens`, and `cache_write.input_tokens` (or the legacy `cache_creation.input_tokens` when `cache_write` is absent) through `TryToken` (line 247), which returns 0 for a missing attribute. When cache reads plus cache writes exceed input, it adds them to input (lines 121–127). Only `chat`, `text_completion`, and `generate_content` spans become `NormalizedGenAiSpan`s; every span with an operation name, and every span from a Copilot resource, becomes a `GenAiSpanLink`. So `invoke_agent` and `execute_tool` spans are never rows of `spans`.
  - **Span contract.** `NormalizedGenAiSpan` has `long CacheReadInputTokens` and `long CacheWriteInputTokens` and nothing that says whether they were reported.
  - **Pricing.** `JsonRateCardCalculator` prices input less cache reads and cache writes at `inputPer1M`, cache reads at `cachedInputPer1M`, and cache writes at `cacheWritePer1M` (each falling back to `inputPer1M`), and returns `invalid_usage` when a cache counter exceeds what's left of input. This sprint doesn't change pricing.
  - **Schema.** `LedgerMigrations.All` has versions 1–3. `spans.cache_read_input_tokens` and `cache_write_input_tokens` are `INTEGER NOT NULL` (version 1). `InitializeAsync` runs each pending migration in its own transaction and then sets `user_version`.
  - **Ingestion and lineage.** `LedgerIngestor.IngestAsync` records a batch's entries, then its span links, then calls `BackfillFromAgentSpansAsync` for every trace in the batch, then the breaker. `BackfillFromAgentSpansAsync` copies each `invoke_agent` link's agent name and conversation ID onto the `spans` rows below it, without crossing a nested `invoke_agent`, and skips an agent link that has neither. `DaemonHost` deletes span links that started more than 7 days ago (`PruneSpanLinksAsync`, lines 90–93 and 165–171), so lineage kept only in `span_links` disappears. Copilot exports spans as they end, so a request's `chat` span usually arrives before its `invoke_agent` ancestor.
  - **Git.** `GitWorkspaceContext` is a positional record with six parameters, constructed in product code and tests. `FileSystemGitContextResolver.Resolve` reads `HEAD`, the branch's ref, and `<commonDir>/config` without running `git`; `ReadOriginUrl` keeps the first `[remote "origin"] url`. `CachingGitContextResolver` caches each working directory's result for 5 seconds.
  - **Queries.** `LedgerFilter` has `From`, `To`, `Repository`, and `Branch`. `LedgerGrouping` has `Repository`, `Branch`, `Model`, `Agent`, and `ChannelModel`. `LedgerRollupRow` has token sums and money, and no ratio or coverage. `GetRollupAsync`, both queries of `GetTopPromptChainsAsync`, and `GetPromptCountAsync` each repeat the four filter conditions.
  - **CLI.** `status` takes only `--json` and shows the daemon, ledger, rates, Copilot's export setting, warnings, and one table of spend for today and the last 7 and 30 days. `branch`, `agents`, and `top-prompts` take `--repo`, `--since` (`SinceOption`, parsed by `Period.TryParse`), and `--json`. JSON rows are written by `LedgerFormat.WriteTotals`, which receipts use too.
  - **MCP.** `gh ledger mcp` serves four tools, `get_branch_ledger`, `get_top_expensive_prompts`, `get_agent_breakdown`, and `set_branch_budget`, with hand-written schemas and allowed-argument lists, and starts each result's text with a `WARNING:` line for each open breaker trip or exceeded budget.
  - **Tests.** xUnit v2 (`xunit` 2.9.3); tests use only public APIs, with no `InternalsVisibleTo`. `TestSpans.Chat`, `LedgerSeeds`, `GuideWorld`, `ReceiptSeeds`, `OtlpFixtures`, `CopilotTraces`, and tests that build spans inline give spans nonzero cache counters. `GuideSamplesTests` runs every `docs/guide.md` sample against `GuideWorld`; `ReportCommandTests.StatusText` and the MCP tests (`LedgerMcpServerTests`, through `McpTestHarness`) pin exact output. `GitFixture.Create` writes a repository on branch `feature/ledger` with an `origin` of `https://github.com/swigerb/copilot-ledger.git`.
  - **CI.** `.github/workflows/ci.yml` builds and tests on `ubuntu-latest` and `windows-latest`, then smoke-tests the published binary: it sends one trace without cache counters and greps the output of `branch --all-repos --since all --json` for `"spans": 1,` and `"aiCredits": 50,`, runs `status`, and checks that the MCP `tools/list` includes `get_branch_ledger`. This sprint keeps all of that passing: new JSON fields come after `localSavedUsd`, so neither grepped property becomes the last one of its object.

## Verified facts (retrieved 2026-11-23)

### OpenTelemetry GenAI semantic conventions

Source: `docs/gen-ai/client-inference.md` in [open-telemetry/semantic-conventions-genai](https://github.com/open-telemetry/semantic-conventions-genai/blob/main/docs/gen-ai/client-inference.md) (`main`, where the GenAI conventions now live; status Development).

- `gen_ai.usage.cache_read.input_tokens` is "the number of input tokens served from a provider-managed cache" and `gen_ai.usage.cache_write.input_tokens` "the number of input tokens written to a provider-managed cache". Both have the requirement level **"Recommended: When available and non-zero"**, so an instrumentation that follows the conventions leaves a zero counter out.
- Notes [23] and [24]: each cache counter's value "SHOULD be included in `gen_ai.usage.input_tokens`". Note [28]: `gen_ai.usage.input_tokens` "SHOULD include cached tokens and input tokens of all modalities". So uncached input is `input_tokens − cache_read − cache_write`, which the ledger already assumes.
- The conventions no longer define `gen_ai.usage.cache_creation.input_tokens`; the ledger keeps reading it as the legacy name of `cache_write` (`GenAiAttributeNames.LegacyCacheCreationInputTokens`).

### GitHub Copilot's OpenTelemetry reference

Source: [OpenTelemetry reference](https://docs.github.com/en/copilot/reference/enterprise-administrators/opentelemetry-reference) (GitHub Docs), which covers Copilot CLI, the GitHub Copilot app, and VS Code sessions using Agent Host.

- Each agent interaction is a span tree: an `invoke_agent` root with `chat` and `execute_tool` children. "Subagent invocations are linked into the same trace by context propagation," and a subagent has its own `invoke_agent` span.
- A `chat` span is "one span per LLM request" and carries `gen_ai.usage.cache_write.input_tokens`, `gen_ai.usage.cache_read.input_tokens`, `gen_ai.usage.input_tokens` ("input tokens this turn"), `gen_ai.usage.output_tokens`, `github.copilot.turn_id`, `github.copilot.interaction_id`, and `github.copilot.request.type`, which is `compaction` on conversation-compaction requests. `invoke_agent` spans carry totals over all of their turns.
- The page doesn't say when the cache attributes are left out. [Monitor agent usage with OpenTelemetry](https://code.visualstudio.com/docs/agents/guides/monitoring-agents) (VS Code docs), for VS Code's Local agent harness, lists `gen_ai.usage.cache_read.input_tokens` and `gen_ai.usage.cache_creation.input_tokens` on `chat` spans, each "when available".

### What Copilot CLI sends (Sprint 2's live capture)

Source: `.github/plans/archive/sprint-2.md`, "Live capture" (Copilot CLI 1.0.112, 2026-11-20).

- On `chat` spans, `gen_ai.usage.cache_read.input_tokens` and `gen_ai.usage.cache_write.input_tokens` "appeared only when non-zero". The recorded fixture `CopilotTraces.TraceBA` shows it: its first request (span `00f067aa0ba902bb`) has a cache write of 3,662 tokens and no cache-read attribute, the next has both, and the third has only a cache read.
- The default agent's `invoke_agent` span has no `gen_ai.agent.name`; a subagent's `invoke_agent` span has one and is a child of an `execute_tool task` span. `chat` spans arrive before their `invoke_agent` ancestors.

### `gh pr checkout` and `.git/config`

Source: `pkg/cmd/pr/checkout/checkout.go` in [cli/cli at v2.105.0](https://github.com/cli/cli/blob/v2.105.0/pkg/cmd/pr/checkout/checkout.go), the latest release (2026-11-18); v2.103.3 has the same logic.

- When the pull request's head repository is already a local remote (for example, a branch of the same repository), `gh pr checkout` runs `git checkout -b <branch> --track <remote>/<head>`, so the branch's `merge` is `refs/heads/<head>`.
- Otherwise (`cmdsForMissingRemote`: typically a fork with no local remote, or a deleted head repository), it fetches `refs/pull/<n>/head` from the base repository and, when the branch has no `merge` setting yet, writes `branch.<branch>.remote`, `branch.<branch>.pushRemote`, and `branch.<branch>.merge`. The `merge` value is `refs/pull/<n>/head`, unless the maintainer can modify the pull request and its head repository exists; then it's `refs/heads/<head>` on the head repository's URL.
- `--detach` checks out `FETCH_HEAD` with a detached `HEAD` and writes no branch settings.
- So `[branch "<b>"] merge = refs/pull/<n>/head` establishes the pull request number when it's there, and many pull request checkouts don't have it. That's the only source this sprint reads.

### .NET, SQLite, and System.CommandLine (probed in the scratch build)

- `Microsoft.Data.Sqlite` 10.0.15 (the bundled SQLite) supports window functions (`LAG`, `FIRST_VALUE`, `ROW_NUMBER`), `UPDATE … FROM`, recursive CTEs in `UPDATE`, and `ALTER TABLE … DROP COLUMN` on a column with its own `CHECK`. A pooled connection keeps the schema it loaded, so a test that changes the schema and initializes again calls `SqliteConnection.ClearAllPools()` first, as SL8 already does.
- `decimal` division of two `long`s gives up to 28 significant digits, rounding the last half away from zero; for example `(decimal)3662 / 7429` is `0.4929331000134607618791223583`, and `(decimal)160000 / 3` is `53333.333333333333333333333333`. `decimal.ToString(CultureInfo.InvariantCulture)` writes every digit, which `Utf8JsonWriter.WriteRawValue` and `JsonNode.Parse` keep.
- In System.CommandLine 2.0.15, `result.GetResult(option) is { Implicit: false }` is true only for an option the user typed; `Option.Name` includes its `--`. A command validator's error goes to standard error with exit code 1, and the command's help is written to standard output.
- The CLI project sets `<InvariantGlobalization>true</InvariantGlobalization>`, but the test host has culture data, so a test can set `CultureInfo.CurrentCulture` to `de-DE`.

### Assumptions (not verified)

- **Copilot leaves out a zero cache counter on every client.** Verified for Copilot CLI 1.0.112 (Sprint 2) and stated by the conventions; not captured again this sprint (Copilot CLI 1.0.103 is installed here), and not captured for the GitHub Copilot app or VS Code. The design below depends on it; see "Cache usage is per span".
- **`github.copilot.turn_id` groups a user turn's requests.** The reference names the attribute without defining it, so this sprint treats each `chat` span as one turn and doesn't read `turn_id`. See "Deferred".

## Design decisions

### Cache usage is per span (deviation D1)

Each span records two states, one per counter, exactly as the issue asks: `Reported` when the attribute was present (even with value 0), `Absent` when it wasn't, and `LegacyUnknown` for rows recorded before schema version 4. The token columns stay `INTEGER NOT NULL`, and an absent counter is still stored as 0, so pricing, rollups, and receipts don't change.

The metrics need one rule for whether a span's cache usage is known. The issue's rule is per counter: the hit ratio covers spans whose cache *read* was reported, and observed uncached input needs *both* counters. That rule can't work with what Copilot sends. The conventions make both counters "Recommended: When available and non-zero", and Copilot CLI sent each one only when it wasn't zero (see "Verified facts"). So a request that missed the cache never reports a cache read, and under the issue's rule every miss would drop out of the hit ratio: the ratio would describe only the requests that hit, and a real 0% could never appear. In the recorded `CopilotTraces.TraceBA`, the `explore` subagent's two requests read 3,662 of 7,429 input tokens (49.3%), but the issue's rule would report 97.3%, from the second request alone. And because a request rarely both reads and writes, observed uncached input would be unknown for most requests.

So this plan uses one rule per span, in two places that must agree: `CacheMetrics.IsReported` in C# and `ReportedSql` in SQL. **A span reported its cache usage when at least one of its counters is `Reported`.** On such a span an absent counter is the zero the conventions leave out, and the stored 0 is used as observed. A span with neither counter reported (`Absent` or `LegacyUnknown`) has unknown cache usage, as a Foundry BYOK or local endpoint that never reports caching does: it's left out of the hit ratio and of every observed cache or uncached total, counted in `CacheUnreportedSpanCount`, and shown as `n/a`, never as `0.0%`. A group with no reported span has a `null` ratio and `null` observed totals. Mixed groups mark observed totals as partial (`*` in tables), and estimated totals, which count unknown spans' input as uncached the way pricing does, are labeled as estimates.

This differs from the issue's acceptance criterion "Missing write counters make observed uncached input unknown without hiding a known cache-read ratio … weighted hit ratios use only the input denominator from read-reported spans", and from its implementation notes (`CASE WHEN cache_read_state = 'reported'`; "observed `uncachedInputTokens` is nullable unless both cache counters are reported"). The rest of that criterion holds: mixed groups label partial and estimated totals. A provider that reports cache reads but never cache writes would have its writes counted as 0, which overstates uncached input the same way pricing does today. **The coordinating session accepted this deviation, with D2–D5, on 2026-11-23:** a span with neither counter stays unknown, so no zero-hit metric is made up from missing counters, and the per-counter rule's bias toward hits is avoided. Every place that shows a ratio also shows how many spans reported cache counters. One bias remains: a model that reports cache reads but never cache writes, such as OpenAI-style automatic caching, sends no counter at all on a miss, so its misses stay unreported and are left out of the ratio. If the issue's per-counter rule is ever preferred, the code change is small (`CacheMetrics.IsReported`, `ReportedSql`, and the nullability in `TurnTokenRow`), but many expected values in Tasks 4–11 change.

### Schema version 4 (deviations D2 and D3)

Migration v4 is the next number after v3. Issues #61 and #63 take later numbers (see "Deferred").

- It adds the issue's three columns and index as written, plus `agent_span_id TEXT` (null when unknown).
- Rows recorded before v4 get `legacy_unknown` for both states. The issue lets the architect upgrade `cache_read_state` to `reported` where `cache_read_input_tokens > 0`, and this plan does. **Deviation D2:** it also sets `cache_write_state` to `reported` where `cache_write_input_tokens > 0`, by the same proof: a nonzero counter can only have been reported. With D1, that keeps a legacy cold request (a cache write and no read) in the hit ratio as the miss it was. Zero-valued counters stay `legacy_unknown`.
- **Deviation D3, `agent_span_id`.** The issue asks for growth per sub-agent *instance*, identified by the nearest `invoke_agent` ancestor's trace and span IDs, and for "attribution unavailable" when lineage is missing. Lineage lives in `span_links`, which the daemon prunes after 7 days, so a query-time walk would lose it for older spans. Instead, `BackfillFromAgentSpansAsync`, which already walks each `invoke_agent` link's descendants without crossing a nested `invoke_agent` and runs for every trace in every batch, also sets `agent_span_id = COALESCE(agent_span_id, <invoke_agent span ID>)`. Because the walk stops at nested agents, only a span's nearest agent can reach it, whatever order the spans arrive in. The migration fills the column the same way from the links still kept. A span's agent identity is `(trace_id, agent_span_id)`.
- `RecordAsync` writes both states and `pull_request_number`, and rejects an entry whose state is `LegacyUnknown` (only the migration produces it), whose `Absent` counter isn't 0 (the normalizer can't produce one), or whose pull request number is 0 or less. In `BackfillFromAgentSpansAsync`, the guard that skipped an agent link with neither a name nor a conversation ID goes, so every agent's `UPDATE` runs, and its `WHERE` also matches rows whose `agent_span_id` is still null. A second backfill of the same trace still changes no rows, and SL3–SL5 keep their counts; SL12 checks the new ones.

### Fixtures

New spans default to `Absent`/`Absent` (the enum's default value), so a fixture that gives a span a nonzero cache counter would now fail validation. Every test that records spans builds them with `TestSpans.Chat` or through the normalizer, so the fix is in one place: `TestSpans.Chat` gains optional `cacheReadState` and `cacheWriteState`, which default to `Reported` for a nonzero counter and `Absent` for 0. No other fixture changes until Task 7, when `GuideWorld` marks its GitHub-hosted spans `Reported` (so a request that read nothing shows a real 0.0%) and its Foundry BYOK and local spans `Absent`, and gives its first prompt 20,000 cache-read tokens, so the guide's first `status` sample shows a typical hit rather than a first-run 0.0%.

### Pull request numbers

- `GitWorkspaceContext.PullRequestNumber` is an init-only property, not a seventh positional parameter, so the existing constructor calls don't change. Record equality includes it.
- `FileSystemGitContextResolver` reads it from the config it already reads: `n` from the last `merge` value in a `[branch "<current branch>"]` section (section names case-insensitive, the branch name case-sensitive, as in Git), when that value is exactly `refs/pull/<n>/head` and `n` is 1 to 2,147,483,647 without a sign or leading zeros. A detached `HEAD`, any other `merge` value, or no section gives `null`. Linked worktrees read the common config. There's no branch-name inference, no network call, and no `git` process. `CachingGitContextResolver` caches the whole context, so it needs no change.
- The ingestor stores the number with each span in `pull_request_number`. `--by pr` groups spans by `(repository, pull request)`, and spans without a number by `(repository, branch)`, labeled `(no PR) <branch>`.

### Queries

- **One filter.** `LedgerFilter` gains `PullRequestNumber` and `Agent` (`(none)` matches spans without an agent). Every filtered query uses one SQL fragment, `FilterSql`, with parameters from `AddFilterParameters`, so the six conditions are written once. The CLI and MCP filter by pull request (`--pr`, `pullRequest`) but not by agent; `--by agent` groups instead, and `Agent` is there for queries and for #64 and #65.
- **Coverage sums.** `LedgerRollupRow` and `CacheEfficiencyRow` sum input, cache reads, and uncached input over the reported spans only. Uncached input is `MAX(input − cache read − cache write, 0)` per span (`UncachedSql`), so a span with inconsistent counters (status `invalid_usage`) can't make a total negative. Ratios are computed in C# as `decimal` from `long` sums (`CacheMetrics.HitRatio`), never rounded in Core or Storage, and `null` when the denominator is 0.
- **`GetCacheEfficiencyAsync`** groups by `Model`, `Agent`, `Repository`, `Branch`, or the new `PullRequest`, and orders rows by input tokens, largest first, then by the group key. `PullRequest` isn't a valid grouping for `GetRollupAsync`, which throws for it, and `ChannelModel` isn't valid for `GetCacheEfficiencyAsync`.
- **Turns.** A turn is one inference span, that is, one model request; Copilot's `github.copilot.turn_id` isn't stored (see "Deferred"). `GetTurnBreakdownAsync` takes a `TurnSelector`, which holds either a trace ID (validated as 32 hexadecimal characters and lowercased) or a conversation ID, never both, plus a `LedgerFilter`. It lists spans by start time, then span ID, then trace ID. `InputDeltaFromPreviousTurn` compares a span with the previous span of its *sequence*: the same trace, `agent_span_id`, repository, branch, conversation, and model, ordered by start time and then span ID. So interleaved subagents and requests that start in the same millisecond never mix. Deltas are computed over all of the selector's spans before the filter applies, and they're `null` for a sequence's first span and for a span whose `agent_span_id` is unknown.
- **Context bloat.** `GetContextBloatAsync` ranks prompt chains (traces) three ways, each up to `limit`, ties going to the lower trace ID: chains whose spans all reported cache usage, by observed uncached input; chains with at least one span that didn't, by estimated uncached input, ranked separately so an unknown total is never compared with an observed one; and chains by input growth, the largest `peak input − first input` among the chain's sequences, counting only growth above 0. Spans with an unknown agent are left out of growth and counted in `SpansWithoutAgent`.

### CLI

- **Plain `status`** keeps its daemon and configuration lines and its spend table for today and the last 7 and 30 days, and adds a second table: `Period`, `Cache hit` (`53.7%` or `n/a`), and `Cache reported` (`6 of 8 spans`). **Deviation D4:** the issue's example is a line per period, `Cache hit: 72.4% (reported on 1,240 of 1,300 spans)`; a table matches the spend table above it. JSON adds a `cache` object to each period.
- **Percentages** come from `LedgerFormat.Percent`: the ratio times 100, rounded to one decimal place with `MidpointRounding.AwayFromZero`, formatted `0.0` in the invariant culture, plus `%`; `null` is `n/a`. JSON writes the exact ratio with `LedgerFormat.WriteDecimal`.
- **`status --cache`** reports by `--by model` (the default), `branch`, `pr`, or `agent`, in every repository or in `--repo`, with `--branch`, `--pr`, and `--since` (default `30d`). Columns are the group, then `Spans`, `Cache reported`, `Input`, `Cache read`, `Cache write`, `Uncached`, `Output`, and `Hit %`. **Deviation D5:** instead of one `Group` column, the group columns are named `Model`, `Agent`, `Branch`, or `Pull request`, with a `Repository` column first for branches and pull requests when `--repo` isn't given. `Cache read`, `Cache write`, and `Uncached` show `n/a` for a row with no reported span and end in `*` when some of the row's spans are unreported; `Hit %` is the ratio over the reported spans, or `n/a`. A footnote counts the unreported spans.
- **`--turns`** takes `--trace <traceId>` or `--conversation <id>`; **`--bloat`** takes `--repo`, `--since`, and `--limit` (1–100, default 10). Option rules, checked in this order: without `--cache`, any breakdown option is an error; `--turns` and `--bloat` are exclusive; `--turns` takes only `--trace` or `--conversation` (exactly one); `--trace` and `--conversation` need `--turns`; `--by`, `--branch`, and `--pr` can't be used with `--bloat`; and `--limit` needs `--bloat`.
- `branch --json` and `agents --json` rows end with `cacheReportedSpans`, `cacheUnreportedSpans`, and `cacheHitRatio`, after the totals, so the CI smoke test's `"spans": 1,` and `"aiCredits": 50,` still match. Their tables don't change. Receipts don't change: they use `LedgerFormat.WriteTotals`, which this sprint doesn't touch.

### MCP

- **`get_cache_efficiency`** is read-only (`ReadOnlyHint = true`, `OpenWorldHint = false`) and takes `repository`, `branch`, `pullRequest` (an integer of at least 1), `since` (the `--since` grammar, default `30d`), `by` (`model`, `branch`, `pr`, or `agent`, default `model`), and `limit` (1–50, default 10). The issue leaves defaults open, so: `repository` defaults to the current repository, or the tool returns an error outside one; `branch` and `pullRequest` are independent filters, so the "pass repository and branch together" rule of the other tools doesn't apply; and the warnings describe the current branch, or only the current session's trips outside a repository. Rows have the same fields as `status --cache --json`.
- `get_branch_ledger` and `get_agent_breakdown` add `uncachedInputTokens` (observed, or `null`), `cacheReportedSpans`, and `cacheHitRatio` after `cacheWriteInputTokens`.
- The `--since` grammar moves from the CLI's `Period` into `CopilotLedger.Storage.LedgerPeriod`, which both use; `Period` keeps its signature and delegates.

### Docs

`docs/cli.md` gains a "Prompt-cache efficiency" section, `docs/configuration.md` a "Cache tokens" section on pricing, and `docs/guide.md` a "Find context bloat" walkthrough with three tested samples. For the walkthrough, `GuideWorld`'s prompt that trips the breaker now grows from 357,500 to 642,500 input tokens over its four requests, with cache reads chosen so each request still costs 126.00 AI credits, and the guide's world records span links, so every money sample stays the same.

### Deviations from issue #62

| # | Issue #62 | This plan | Why |
|---|---|---|---|
| D1 | Hit ratio over spans with `cache_read_state = 'reported'`; observed uncached input only when both counters are reported (AC "Missing write counters make observed uncached input unknown…") | A span reported its cache usage when either counter is reported; on such a span an absent counter is 0 | Copilot and the conventions leave out zero counters, so the per-counter rule would drop every cache miss. Accepted by the coordinating session on 2026-11-23. |
| D2 | The heuristic upgrade sets `cache_read_state` where `cache_read_input_tokens > 0` | Also sets `cache_write_state` where `cache_write_input_tokens > 0` | The same proof; keeps legacy cold requests in the ratio |
| D3 | Three new columns and an index | Also `agent_span_id TEXT`, filled at ingest and by the migration | Span links are pruned after 7 days |
| D4 | Plain `status`: a `Cache hit: …` line per period | A second table with `Cache hit` and `Cache reported` | Matches the spend table; the issue's line is an example |
| D5 | `status --cache` table column `Group` | Named group columns, plus `Repository` when unscoped | Clearer, and matches `branch` and `agents` |

### Acceptance criteria traceability

| Issue #62 acceptance criterion | Tasks | Tests |
|---|---|---|
| Normalizer tests cover read+write, read only, legacy `cache_creation` only, and neither; token values unchanged | 1 | NZ13 (7 cases), NZ14 |
| The migration keeps every row and marks legacy rows `legacy_unknown`; the heuristic upgrade only for nonzero counters | 3 | SL11, S1, SL1, SL8, SL10 (D2: SL11 checks both counters) |
| `CacheHitRatio` is exact `decimal`, checked with `TheoryData<…, decimal>`: 0 reported spans → `null`; zero input → `null`; mixed → reported spans only; all hits → 1; true zero → 0 shown as `0.0%` | 1, 4, 5, 9, 11 | CM1, CM2, CE1, CE2, S14, CR7 and CR9 (`0.0%`), GS2 (`cache-bloat`) |
| An all-absent group shows `n/a` in tables and `null` in JSON, never `0.0%` | 4, 5, 7, 8, 9, 10 | CE2, RP14, RP15, CR1, CR2, CR7, CR8, MC4, MC6, MC13 |
| Missing write counters …; mixed groups label partial/estimated totals; ratios use the read-reported denominator | 5, 6, 8, 9 | D1 replaces the first and last parts; CE1 (case 4), CE3, CR1 (`*`), CR9 (`Est. uncached`), TB1 |
| `--by pr` groups by `pull_request_number`; a `GitFixture` config with `refs/pull/42/head` gives PR 42 without `git`; other branches show `(no PR) <branch>` | 2, 3, 5, 8 | Resolve_PullRequestCheckout_ReadsTheNumberFromTheMergeRef, Resolve_MergeConfig_YieldsOnlyExplicitPullRequestRefs, IG10, CE4, CR3 |
| `--turns --trace` lists inference spans in start order with the breakdown and `InputDeltaFromPreviousTurn`; `invoke_agent` spans excluded | 6, 9 | TB1, TB2, CR7, CR8 |
| `--bloat` ranks by uncached input and input growth deterministically, ties by trace ID | 6, 9 | CB1, CB2, CR9, CR10 |
| `status` shows the cache summary and its JSON `cache` object | 7 | StatusText, RP14, GS2 (`status`, `status-first`) |
| `status --cache --by model\|branch\|pr\|agent`; plain `status` keeps its details and periods; interleaved agents and equal timestamps don't mix growth; unknown PRs stay unknown | 6, 7, 8 | CR1, CR3, StatusText, TB1, TB2 |
| MCP `get_cache_efficiency` validates arguments, returns the documented fields, is read-only, and has `McpTestHarness` tests including the `WARNING:` prefix | 10 | MC1, MC8 (13 new cases), MC13, MC14 |
| No `double`, `float`, or `REAL`; invariant culture, verified under `de-DE` | all | CM1, CR6 |
| `docs/cli.md`, `configuration.md`, and `guide.md` updated; `GuideSamplesTests` passes | 7, 10, 11 | GS1, GS2 (`cache`, `cache-bloat`, `cache-turns`), GS7 |
| Build and tests exit 0 with warnings as errors; hermetic, no network, no `git` | all | the whole suite |

## Task 1 specification: Cache Counter States

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Telemetry/CacheCounterState.cs` | New: `Absent` (0, the default), `Reported`, `LegacyUnknown` |
| `src/CopilotLedger.Core/Telemetry/CacheMetrics.cs` | New: `IsReported` and `HitRatio` |
| `src/CopilotLedger.Core/Telemetry/NormalizedGenAiSpan.cs` | Adds `CacheReadState` and `CacheWriteState` |
| `src/CopilotLedger.Otlp/Normalization/GenAiSpanNormalizer.cs` | Records each counter's state through a `TryToken` overload |
| `tests/CopilotLedger.Tests/Otlp/GenAiSpanNormalizerTests.cs` | New NZ13 and NZ14 |
| `tests/CopilotLedger.Tests/Telemetry/CacheMetricsTests.cs` | New: CM1–CM3 |
| `tests/CopilotLedger.Tests/TestSupport/TestSpans.cs` | `Chat` takes optional states; a nonzero counter defaults to `Reported` |

### Contracts

```csharp
namespace CopilotLedger.Core.Telemetry;

public enum CacheCounterState { Absent, Reported, LegacyUnknown }

public static class CacheMetrics
{
    public static bool IsReported(CacheCounterState cacheRead, CacheCounterState cacheWrite);   // either is Reported
    public static decimal? HitRatio(long cacheReadInputTokens, long inputTokens);              // null when inputTokens is 0
}

// NormalizedGenAiSpan gains:
public CacheCounterState CacheReadState { get; init; }    // default Absent
public CacheCounterState CacheWriteState { get; init; }   // default Absent
```

- The normalizer sets `CacheReadState` to `Reported` when `gen_ai.usage.cache_read.input_tokens` is present, whatever its value, and `Absent` otherwise. `CacheWriteState` follows `gen_ai.usage.cache_write.input_tokens`, or, when that's absent, the legacy `gen_ai.usage.cache_creation.input_tokens`. Token values, the input adjustment, and rejections don't change; the new `TryToken` overload sets the state and then calls the existing `TryToken`.
- `HitRatio` is `(decimal)cacheRead / input`, unrounded. `IsReported` is D1's rule (see "Design decisions").

### Code (copy exactly)

New file `src/CopilotLedger.Core/Telemetry/CacheCounterState.cs`:

```csharp
namespace CopilotLedger.Core.Telemetry;

// Whether a span carried one prompt-cache counter (gen_ai.usage.cache_read.input_tokens, or cache_write.input_tokens with
// its legacy cache_creation.input_tokens). The counter's value is stored either way; an absent counter is stored as 0.
public enum CacheCounterState
{
    Absent,          // the span didn't carry the attribute
    Reported,        // the span carried the attribute
    LegacyUnknown,   // the span was recorded before the ledger stored counter states
}
```

New file `src/CopilotLedger.Core/Telemetry/CacheMetrics.cs`:

```csharp
namespace CopilotLedger.Core.Telemetry;

public static class CacheMetrics
{
    // True when the span reported its prompt-cache usage, that is, it carried at least one cache counter. The OTel GenAI
    // conventions leave out a cache counter whose value is zero, so the other counter on such a span is a reported 0.
    // A span with neither counter is unknown: its provider may not report cache usage at all.
    public static bool IsReported(CacheCounterState cacheRead, CacheCounterState cacheWrite) =>
        cacheRead == CacheCounterState.Reported || cacheWrite == CacheCounterState.Reported;

    // cacheReadInputTokens / inputTokens, computed exactly in decimal; null when inputTokens is 0. Never rounds.
    public static decimal? HitRatio(long cacheReadInputTokens, long inputTokens) =>
        inputTokens == 0 ? null : (decimal)cacheReadInputTokens / inputTokens;
}
```

Edit 1 of 1 in `src/CopilotLedger.Core/Telemetry/NormalizedGenAiSpan.cs`, replace:

```csharp
    public long CacheReadInputTokens { get; init; }          // gen_ai.usage.cache_read.input_tokens
    public long CacheWriteInputTokens { get; init; }         // gen_ai.usage.cache_write.input_tokens (legacy: cache_creation)
}
```

with:

```csharp
    public long CacheReadInputTokens { get; init; }          // gen_ai.usage.cache_read.input_tokens
    public long CacheWriteInputTokens { get; init; }         // gen_ai.usage.cache_write.input_tokens (legacy: cache_creation)
    public CacheCounterState CacheReadState { get; init; }   // Reported when the span carried the cache-read attribute
    public CacheCounterState CacheWriteState { get; init; }  // Reported when it carried cache_write (or legacy cache_creation)
}
```

Edit 1 of 5 in `src/CopilotLedger.Otlp/Normalization/GenAiSpanNormalizer.cs`, replace:

```csharp
        long cacheRead = 0;
        long cacheWrite = 0;

        if (isInference)
```

with:

```csharp
        long cacheRead = 0;
        long cacheWrite = 0;
        CacheCounterState cacheReadState = CacheCounterState.Absent;
        CacheCounterState cacheWriteState = CacheCounterState.Absent;

        if (isInference)
```

Edit 2 of 5 in `src/CopilotLedger.Otlp/Normalization/GenAiSpanNormalizer.cs`, replace:

```csharp
            if (!TryToken(attributes, GenAiAttributeNames.InputTokens, out input, out rejectionReason) ||
                !TryToken(attributes, GenAiAttributeNames.OutputTokens, out output, out rejectionReason) ||
                !TryToken(attributes, GenAiAttributeNames.CacheReadInputTokens, out cacheRead, out rejectionReason) ||
                !TryToken(
                    attributes,
```

with:

```csharp
            if (!TryToken(attributes, GenAiAttributeNames.InputTokens, out input, out rejectionReason) ||
                !TryToken(attributes, GenAiAttributeNames.OutputTokens, out output, out rejectionReason) ||
                !TryToken(
                    attributes,
                    GenAiAttributeNames.CacheReadInputTokens,
                    out cacheRead,
                    out cacheReadState,
                    out rejectionReason) ||
                !TryToken(
                    attributes,
```

Edit 3 of 5 in `src/CopilotLedger.Otlp/Normalization/GenAiSpanNormalizer.cs`, replace:

```csharp
                        : GenAiAttributeNames.LegacyCacheCreationInputTokens,
                    out cacheWrite,
                    out rejectionReason))
            {
                return false;
```

with:

```csharp
                        : GenAiAttributeNames.LegacyCacheCreationInputTokens,
                    out cacheWrite,
                    out cacheWriteState,
                    out rejectionReason))
            {
                return false;
```

Edit 4 of 5 in `src/CopilotLedger.Otlp/Normalization/GenAiSpanNormalizer.cs`, replace:

```csharp
                CacheReadInputTokens = cacheRead,
                CacheWriteInputTokens = cacheWrite,
            };
        }

        return true;
```

with:

```csharp
                CacheReadInputTokens = cacheRead,
                CacheWriteInputTokens = cacheWrite,
                CacheReadState = cacheReadState,
                CacheWriteState = cacheWriteState,
            };
        }

        return true;
```

Edit 5 of 5 in `src/CopilotLedger.Otlp/Normalization/GenAiSpanNormalizer.cs`, replace:

```csharp
        return true;
    }

    private static int? ReadServerPort(IReadOnlyList<OtlpAttribute> attributes)
    {
        OtlpAttribute? attribute = FindAttribute(attributes, GenAiAttributeNames.ServerPort);
```

with:

```csharp
        return true;
    }

    // Like TryToken, and also says whether the span carried the attribute. A missing attribute is Absent with value 0.
    private static bool TryToken(
        IReadOnlyList<OtlpAttribute> attributes,
        string key,
        out long value,
        out CacheCounterState state,
        out string? rejectionReason)
    {
        state = FindAttribute(attributes, key) is null
            ? CacheCounterState.Absent
            : CacheCounterState.Reported;
        return TryToken(attributes, key, out value, out rejectionReason);
    }

    private static int? ReadServerPort(IReadOnlyList<OtlpAttribute> attributes)
    {
        OtlpAttribute? attribute = FindAttribute(attributes, GenAiAttributeNames.ServerPort);
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Otlp/GenAiSpanNormalizerTests.cs`, replace:

```csharp
            link => AssertLink(link, null, null, null, 0));
    }

    private static void AssertLink(
        GenAiSpanLink link,
```

with:

```csharp
            link => AssertLink(link, null, null, null, 0));
    }

    [Theory]
    [InlineData(0, CacheCounterState.Reported, CacheCounterState.Reported, 300, 100)]
    [InlineData(1, CacheCounterState.Reported, CacheCounterState.Absent, 300, 0)]
    [InlineData(2, CacheCounterState.Absent, CacheCounterState.Reported, 0, 200)]
    [InlineData(3, CacheCounterState.Absent, CacheCounterState.Absent, 0, 0)]
    [InlineData(4, CacheCounterState.Reported, CacheCounterState.Absent, 0, 0)]
    [InlineData(5, CacheCounterState.Absent, CacheCounterState.Reported, 0, 0)]
    [InlineData(6, CacheCounterState.Absent, CacheCounterState.Reported, 0, 0)]
    public void NZ13_CacheCounterStates_SayWhichCountersTheSpanCarried(
        int caseNumber,
        CacheCounterState readState,
        CacheCounterState writeState,
        long cacheRead,
        long cacheWrite)
    {
        OtlpAttribute[] cache = caseNumber switch
        {
            0 =>
            [
                Int(GenAiAttributeNames.CacheReadInputTokens, 300),
                Int(GenAiAttributeNames.CacheWriteInputTokens, 100),
            ],
            1 => [Int(GenAiAttributeNames.CacheReadInputTokens, 300)],
            2 => [Int(GenAiAttributeNames.LegacyCacheCreationInputTokens, 200)],
            3 => [],
            4 => [Int(GenAiAttributeNames.CacheReadInputTokens, 0)],
            5 => [Int(GenAiAttributeNames.LegacyCacheCreationInputTokens, 0)],
            6 =>
            [
                Int(GenAiAttributeNames.CacheWriteInputTokens, 0),
                Int(GenAiAttributeNames.LegacyCacheCreationInputTokens, 50),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(caseNumber)),
        };

        NormalizedGenAiSpan span = Assert.Single(GenAiSpanNormalizer.Normalize(
            Request(
                CopilotResource,
                Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Int(GenAiAttributeNames.InputTokens, 1000),
                    Int(GenAiAttributeNames.OutputTokens, 7),
                    .. cache,
                ]))).InferenceSpans);

        Assert.Equal(readState, span.CacheReadState);
        Assert.Equal(writeState, span.CacheWriteState);
        Assert.Equal(cacheRead, span.CacheReadInputTokens);
        Assert.Equal(cacheWrite, span.CacheWriteInputTokens);
        Assert.Equal(1000, span.InputTokens);
        Assert.Equal(7, span.OutputTokens);
    }

    [Fact]
    public void NZ14_CopilotCapture_LeavesOutZeroCacheCounters()
    {
        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(CopilotTraces.TraceBA());

        Assert.Collection(
            batch.InferenceSpans,
            span =>
            {
                Assert.Equal("00f067aa0ba902bb", span.SpanId);
                Assert.Equal(CacheCounterState.Absent, span.CacheReadState);
                Assert.Equal(CacheCounterState.Reported, span.CacheWriteState);
                Assert.Equal(0, span.CacheReadInputTokens);
                Assert.Equal(3662, span.CacheWriteInputTokens);
                Assert.True(CacheMetrics.IsReported(span.CacheReadState, span.CacheWriteState));
            },
            span =>
            {
                Assert.Equal("00f067aa0ba902bd", span.SpanId);
                Assert.Equal(CacheCounterState.Reported, span.CacheReadState);
                Assert.Equal(CacheCounterState.Reported, span.CacheWriteState);
                Assert.Equal(3662, span.CacheReadInputTokens);
                Assert.Equal(99, span.CacheWriteInputTokens);
            },
            span =>
            {
                Assert.Equal("00f067aa0ba902b8", span.SpanId);
                Assert.Equal(CacheCounterState.Reported, span.CacheReadState);
                Assert.Equal(CacheCounterState.Absent, span.CacheWriteState);
                Assert.Equal(15872, span.CacheReadInputTokens);
                Assert.Equal(0, span.CacheWriteInputTokens);
            });
        NormalizedGenAiSpan unreported = Assert.Single(
            GenAiSpanNormalizer.Normalize(CopilotTraces.TraceF()).InferenceSpans);
        Assert.Equal(CacheCounterState.Absent, unreported.CacheReadState);
        Assert.Equal(CacheCounterState.Absent, unreported.CacheWriteState);
        Assert.False(CacheMetrics.IsReported(unreported.CacheReadState, unreported.CacheWriteState));
    }

    private static void AssertLink(
        GenAiSpanLink link,
```

New file `tests/CopilotLedger.Tests/Telemetry/CacheMetricsTests.cs`:

```csharp
using System.Globalization;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Tests.Telemetry;

public sealed class CacheMetricsTests
{
    public static TheoryData<long, long, decimal> HitRatios => new()
    {
        { 1, 2, 0.5m },
        { 0, 100, 0m },
        { 100, 100, 1m },
        { 1_450_000, 2_600_000, 0.5576923076923076923076923077m },
        { 1, 3, 0.3333333333333333333333333333m },
        { 2, 3, 0.6666666666666666666666666667m },
        { 3_662, 7_429, 0.4929331000134607618791223583m },
        { 1_950_000, 4_000_000, 0.4875m },
    };

    [Theory]
    [MemberData(nameof(HitRatios))]
    public void CM1_HitRatio_IsTheExactDecimalQuotient(long cacheRead, long input, decimal expected)
    {
        decimal? ratio = CacheMetrics.HitRatio(cacheRead, input);

        Assert.Equal(expected, ratio);
        Assert.Equal(
            expected.ToString(CultureInfo.InvariantCulture),
            ratio!.Value.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CM2_HitRatio_IsNullWithoutInput()
    {
        Assert.Null(CacheMetrics.HitRatio(0, 0));
        Assert.Null(CacheMetrics.HitRatio(5, 0));
    }

    [Theory]
    [InlineData(CacheCounterState.Reported, CacheCounterState.Reported, true)]
    [InlineData(CacheCounterState.Reported, CacheCounterState.Absent, true)]
    [InlineData(CacheCounterState.Reported, CacheCounterState.LegacyUnknown, true)]
    [InlineData(CacheCounterState.Absent, CacheCounterState.Reported, true)]
    [InlineData(CacheCounterState.LegacyUnknown, CacheCounterState.Reported, true)]
    [InlineData(CacheCounterState.Absent, CacheCounterState.Absent, false)]
    [InlineData(CacheCounterState.Absent, CacheCounterState.LegacyUnknown, false)]
    [InlineData(CacheCounterState.LegacyUnknown, CacheCounterState.Absent, false)]
    [InlineData(CacheCounterState.LegacyUnknown, CacheCounterState.LegacyUnknown, false)]
    public void CM3_IsReported_NeedsOneReportedCounter(
        CacheCounterState cacheRead,
        CacheCounterState cacheWrite,
        bool expected)
    {
        Assert.Equal(expected, CacheMetrics.IsReported(cacheRead, cacheWrite));
    }
}
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/TestSupport/TestSpans.cs`, replace:

```csharp
        long outputTokens = 0,
        long cacheReadInputTokens = 0,
        long cacheWriteInputTokens = 0)
    {
        DateTimeOffset start = startTime ?? new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
```

with:

```csharp
        long outputTokens = 0,
        long cacheReadInputTokens = 0,
        long cacheWriteInputTokens = 0,
        CacheCounterState? cacheReadState = null,
        CacheCounterState? cacheWriteState = null)
    {
        DateTimeOffset start = startTime ?? new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/TestSupport/TestSpans.cs`, replace:

```csharp
            CacheReadInputTokens = cacheReadInputTokens,
            CacheWriteInputTokens = cacheWriteInputTokens,
        };
    }
}
```

with:

```csharp
            CacheReadInputTokens = cacheReadInputTokens,
            CacheWriteInputTokens = cacheWriteInputTokens,

            // Only a reported counter can be nonzero. Pass a state to record a reported 0.
            CacheReadState = cacheReadState
                ?? (cacheReadInputTokens == 0 ? CacheCounterState.Absent : CacheCounterState.Reported),
            CacheWriteState = cacheWriteState
                ?? (cacheWriteInputTokens == 0 ? CacheCounterState.Absent : CacheCounterState.Reported),
        };
    }
}
```

### Tests

- **NZ13** (7 cases): read and write present → `Reported`/`Reported` with 300 and 100; read only → `Reported`/`Absent` with 300 and 0; legacy `cache_creation` only → `Absent`/`Reported` with 0 and 200; neither → `Absent`/`Absent`; a present 0 is `Reported`, for cache read, for `cache_creation`, and for `cache_write` 0 with `cache_creation` 50 (the write counter wins, 0). Input stays 1,000 and output 7 in every case.
- **NZ14**: the recorded Copilot CLI capture `CopilotTraces.TraceBA` gives (`Absent`, `Reported` 3,662), (`Reported` 3,662, `Reported` 99), and (`Reported` 15,872, `Absent`); the first span `IsReported`. `CopilotTraces.TraceF`'s span has neither counter and isn't reported.
- **CM1** (8 `TheoryData<long, long, decimal>` cases): 1/2 → `0.5`; 0/100 → `0`; 100/100 → `1`; 1,450,000/2,600,000 → `0.5576923076923076923076923077`; 1/3 → `0.3333333333333333333333333333`; 2/3 → `0.6666666666666666666666666667`; 3,662/7,429 → `0.4929331000134607618791223583`; 1,950,000/4,000,000 → `0.4875`, each also compared as invariant text. **CM2**: input 0 → `null`. **CM3** (9 cases): reported when either state is `Reported`, not for any mix of `Absent` and `LegacyUnknown`.

### Acceptance criteria

- The four attribute combinations the issue lists produce the states above, with token values unchanged (NZ13, NZ14), and every existing normalizer, pricing, and storage test passes unchanged.
- `HitRatio` is exact `decimal`, never rounded; no `double` or `float`.
- `dotnet test CopilotLedger.slnx` passes **697** tests (671 + 26).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 2 specification: Pull Request Numbers from Git Config

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Git/GitWorkspaceContext.cs` | Adds `int? PullRequestNumber { get; init; }` |
| `src/CopilotLedger.Core/Git/FileSystemGitContextResolver.cs` | `ReadOriginUrl` becomes `ReadConfig`, which also reads the branch's `merge`; new `ParsePullRequestRef` |
| `tests/CopilotLedger.Tests/TestSupport/GitFixture.cs` | `Create(string root, int? pullRequest = null)` |
| `tests/CopilotLedger.Tests/Git/FileSystemGitContextResolverTests.cs` | Four new tests |
| `tests/CopilotLedger.Tests/Git/GitWorkspaceContextTests.cs` | One new test |
| `tests/CopilotLedger.Tests/Git/CachingGitContextResolverTests.cs` | New CG4 |

### Contract and algorithm

```csharp
public sealed record GitWorkspaceContext(/* the same six positional parameters */)
{
    public int? PullRequestNumber { get; init; }
    public bool IsDetachedHead => BranchName is null && HeadCommitSha is not null;
}
```

`ReadConfig(configPath, branch)` makes one pass over `<commonDir>/config`:

1. A section header line sets two flags: the `origin` section (`[remote "origin"]`, as before) and the branch section (`[branch "<branch>"]`, only when `branch` isn't null). Section names compare without regard to case and subsection names exactly, as `TryReadSection` already parses them.
2. A line without `=` is skipped. Otherwise its key is the text before the first `=`, trimmed.
3. In the `origin` section, the first `url` line (any case) sets the origin URL through `ReadConfigValue` and `GitRemoteUrl.Sanitize`, and later `url` lines are ignored. This is `ReadOriginUrl`'s behavior.
4. In the branch section, every `merge` line (any case) sets the pull request number to `ParsePullRequestRef(ReadConfigValue(value))`, so the last one wins, including a later one that isn't a pull request ref.
5. `ParsePullRequestRef` returns `n` only for exactly `refs/pull/<n>/head` where `<n>` is ASCII digits without a leading zero that parse as an `int` (1 to 2,147,483,647); anything else is `null`.

`Resolve` passes the branch it read from `HEAD` (null when detached) and sets `PullRequestNumber` with an object initializer.

### Code (copy exactly)

Edit 1 of 5 in `src/CopilotLedger.Core/Git/FileSystemGitContextResolver.cs`, replace:

```csharp
namespace CopilotLedger.Core.Git;

public sealed class FileSystemGitContextResolver : IGitContextResolver
```

with:

```csharp
using System.Globalization;

namespace CopilotLedger.Core.Git;

public sealed class FileSystemGitContextResolver : IGitContextResolver
```

Edit 2 of 5 in `src/CopilotLedger.Core/Git/FileSystemGitContextResolver.cs`, replace:

```csharp
                        commit = head.ToLowerInvariant();
                    }
                }

                string? remoteUrl = null;
                string configPath = Path.Combine(commonDirectory, "config");
                if (File.Exists(configPath))
                {
                    remoteUrl = ReadOriginUrl(configPath);
                }

                return new GitWorkspaceContext(
```

with:

```csharp
                        commit = head.ToLowerInvariant();
                    }
                }

                string? remoteUrl = null;
                int? pullRequestNumber = null;
                string configPath = Path.Combine(commonDirectory, "config");
                if (File.Exists(configPath))
                {
                    (remoteUrl, pullRequestNumber) = ReadConfig(configPath, branch);
                }

                return new GitWorkspaceContext(
```

Edit 3 of 5 in `src/CopilotLedger.Core/Git/FileSystemGitContextResolver.cs`, replace:

```csharp
                    commit,
                    remoteUrl,
                    remoteUrl is null ? null : GitRemoteUrl.GetRepositorySlug(remoteUrl));
            }

            if (_ceilingDirectory is not null &&
```

with:

```csharp
                    commit,
                    remoteUrl,
                    remoteUrl is null ? null : GitRemoteUrl.GetRepositorySlug(remoteUrl))
                {
                    PullRequestNumber = pullRequestNumber,
                };
            }

            if (_ceilingDirectory is not null &&
```

Edit 4 of 5 in `src/CopilotLedger.Core/Git/FileSystemGitContextResolver.cs`, replace:

```csharp
        return null;
    }

    private static string? ReadOriginUrl(string configPath)
    {
        string? contents = ReadAllLines(configPath);
        if (contents is null)
        {
            return null;
        }

        bool originSection = false;
        foreach (string rawLine in contents.Split('\n'))
        {
            string line = rawLine.Trim().TrimEnd('\r');
```

with:

```csharp
        return null;
    }

    // Returns remote.origin.url, read as before: the first url line in an origin section, sanitized. When branch isn't null,
    // also returns n from the last merge value in a [branch "<branch>"] section when that value is refs/pull/<n>/head.
    private static (string? OriginUrl, int? PullRequestNumber) ReadConfig(string configPath, string? branch)
    {
        string? contents = ReadAllLines(configPath);
        if (contents is null)
        {
            return (null, null);
        }

        bool originSection = false;
        bool branchSection = false;
        bool originRead = false;
        string? originUrl = null;
        int? pullRequestNumber = null;
        foreach (string rawLine in contents.Split('\n'))
        {
            string line = rawLine.Trim().TrimEnd('\r');
```

Edit 5 of 5 in `src/CopilotLedger.Core/Git/FileSystemGitContextResolver.cs`, replace:

```csharp
                    name.Equals("remote", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(subsection, "origin", StringComparison.Ordinal);
                continue;
            }

            if (!originSection)
            {
                continue;
            }

            int equals = line.IndexOf('=');
            if (equals < 0 || !line[..equals].Trim().Equals("url", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? value = ReadConfigValue(line[(equals + 1)..]);
            return value is null ? null : GitRemoteUrl.Sanitize(value);
        }

        return null;
    }

    private static bool TryReadSection(string line, out string name, out string? subsection)
```

with:

```csharp
                    name.Equals("remote", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(subsection, "origin", StringComparison.Ordinal);
                branchSection =
                    branch is not null &&
                    name.Equals("branch", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(subsection, branch, StringComparison.Ordinal);
                continue;
            }

            int equals = line.IndexOf('=');
            if (equals < 0)
            {
                continue;
            }

            string key = line[..equals].Trim();
            if (originSection && !originRead && key.Equals("url", StringComparison.OrdinalIgnoreCase))
            {
                originRead = true;
                string? value = ReadConfigValue(line[(equals + 1)..]);
                originUrl = value is null ? null : GitRemoteUrl.Sanitize(value);
            }
            else if (branchSection && key.Equals("merge", StringComparison.OrdinalIgnoreCase))
            {
                pullRequestNumber = ParsePullRequestRef(ReadConfigValue(line[(equals + 1)..]));
            }
        }

        return (originUrl, pullRequestNumber);
    }

    // n for "refs/pull/<n>/head", where n is 1 to int.MaxValue written without a sign or leading zeros; otherwise null.
    private static int? ParsePullRequestRef(string? value)
    {
        const string Prefix = "refs/pull/";
        const string Suffix = "/head";
        if (value is null ||
            value.Length <= Prefix.Length + Suffix.Length ||
            !value.StartsWith(Prefix, StringComparison.Ordinal) ||
            !value.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return null;
        }

        string digits = value[Prefix.Length..^Suffix.Length];
        return digits[0] != '0' &&
               digits.All(char.IsAsciiDigit) &&
               int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? number
            : null;
    }

    private static bool TryReadSection(string line, out string name, out string? subsection)
```

Edit 1 of 1 in `src/CopilotLedger.Core/Git/GitWorkspaceContext.cs`, replace:

```csharp
    string? RepositorySlug)     // "owner/repo"; null if not derivable
{
    public bool IsDetachedHead => BranchName is null && HeadCommitSha is not null;
}
```

with:

```csharp
    string? RepositorySlug)     // "owner/repo"; null if not derivable
{
    // n when the repository's config has [branch "<BranchName>"] merge = refs/pull/<n>/head, as gh pr checkout writes for
    // a pull request whose head it fetched from the base repository; null otherwise, including for detached HEADs.
    public int? PullRequestNumber { get; init; }

    public bool IsDetachedHead => BranchName is null && HeadCommitSha is not null;
}
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Git/CachingGitContextResolverTests.cs`, replace:

```csharp
        Assert.Equal(2, inner.Calls);
    }

    private static GitWorkspaceContext Context(string root) =>
        new(root, Path.Combine(root, ".git"), "main", null, null, null);
```

with:

```csharp
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public void CG4_CachedContext_KeepsItsPullRequestNumber()
    {
        ManualTimeProvider time = Time();
        FakeResolver inner = new(Context("/r") with { PullRequestNumber = 42 });
        CachingGitContextResolver resolver = new(inner, time);

        Assert.Equal(42, resolver.Resolve("/r")!.PullRequestNumber);
        time.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(42, resolver.Resolve("/r")!.PullRequestNumber);
        Assert.Equal(1, inner.Calls);
    }

    private static GitWorkspaceContext Context(string root) =>
        new(root, Path.Combine(root, ".git"), "main", null, null, null);
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Git/FileSystemGitContextResolverTests.cs`, replace:

```csharp
        Assert.Null(Resolve(temp, repo));
    }

    private static GitWorkspaceContext? Resolve(TempDirectory temp, string workingDirectory) =>
        new FileSystemGitContextResolver(temp.Path).Resolve(workingDirectory);
```

with:

```csharp
        Assert.Null(Resolve(temp, repo));
    }

    [Fact]
    public void Resolve_PullRequestCheckout_ReadsTheNumberFromTheMergeRef()
    {
        using TempDirectory temp = new();
        using TempDirectory plain = new();
        string repo = GitFixture.Create(temp.Path, pullRequest: 42);
        string other = GitFixture.Create(plain.Path);

        GitWorkspaceContext? result = Resolve(temp, Path.Combine(repo, "src"));
        GitWorkspaceContext? withoutPullRequest = Resolve(plain, other);

        Assert.NotNull(result);
        Assert.Equal("feature/ledger", result.BranchName);
        Assert.Equal("swigerb/copilot-ledger", result.RepositorySlug);
        Assert.Equal(42, result.PullRequestNumber);
        Assert.NotNull(withoutPullRequest);
        Assert.Null(withoutPullRequest.PullRequestNumber);
    }

    public static TheoryData<string, int?> MergeConfigs => new()
    {
        { "[branch \"feature/x\"]\n\tremote = origin\n\tmerge = refs/pull/42/head\n", 42 },
        { "[branch \"feature/x\"]\n\tremote = origin\n\tmerge = refs/heads/feature/x\n", null },
        { "[branch \"other\"]\n\tmerge = refs/pull/42/head\n", null },
        { "[branch \"Feature/X\"]\n\tmerge = refs/pull/42/head\n", null },
        { "[Branch \"feature/x\"]\n\tMERGE = \"refs/pull/7/head\" ; gh pr checkout\n", 7 },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/42/head\n\tmerge = refs/heads/feature/x\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/42/head\n[branch \"feature/x\"]\n\tremote = origin\n", 42 },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/42/head\n[branch \"feature/x\"]\n\tmerge = refs/pull/43/head\n", 43 },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/2147483647/head\n", 2147483647 },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/2147483648/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/0/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/042/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/-1/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/+1/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/4 2/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/42/merge\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull//head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = \"refs/pull/42/head\n", null },
    };

    [Theory]
    [MemberData(nameof(MergeConfigs))]
    public void Resolve_MergeConfig_YieldsOnlyExplicitPullRequestRefs(string branchConfig, int? expected)
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", "ref: refs/heads/feature/x\n");
        Write(git, "refs/heads/feature/x", Sha1 + "\n");
        Write(git, "config", $"[remote \"origin\"]\n\turl = {OriginUrl}\n" + branchConfig);

        GitWorkspaceContext? result = Resolve(temp, repo);

        Assert.NotNull(result);
        Assert.Equal(expected, result.PullRequestNumber);
        Assert.Equal(OriginUrl, result.RemoteOriginUrl);
        Assert.Equal("swigerb/copilot-ledger", result.RepositorySlug);
    }

    [Fact]
    public void Resolve_DetachedHead_HasNoPullRequest()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", Sha1 + "\n");
        Write(git, "config", "[branch \"feature/x\"]\n\tmerge = refs/pull/42/head\n");

        GitWorkspaceContext? result = Resolve(temp, repo);

        Assert.NotNull(result);
        Assert.True(result.IsDetachedHead);
        Assert.Null(result.PullRequestNumber);
    }

    [Fact]
    public void Resolve_LinkedWorktree_ReadsTheMergeRefFromTheCommonConfig()
    {
        using TempDirectory temp = new();
        string main = CreateDirectory(temp, "main");
        string mainGit = CreateDirectory(main, ".git");
        Write(mainGit, "HEAD", "ref: refs/heads/main\n");
        Write(
            mainGit,
            "config",
            $"[remote \"origin\"]\n\turl = {OriginUrl}\n[branch \"wt-branch\"]\n\tremote = origin\n\tmerge = refs/pull/9/head\n");
        Write(mainGit, "refs/heads/wt-branch", Sha1 + "\n");
        string worktreeGit = CreateDirectory(mainGit, "worktrees", "wt");
        Write(worktreeGit, "HEAD", "ref: refs/heads/wt-branch\n");
        Write(worktreeGit, "commondir", "../..\n");
        string worktree = CreateDirectory(temp, "wt");
        Write(worktree, ".git", "gitdir: " + worktreeGit + "\n");

        GitWorkspaceContext? worktreeResult = Resolve(temp, worktree);
        GitWorkspaceContext? mainResult = Resolve(temp, main);

        Assert.Equal(9, worktreeResult!.PullRequestNumber);
        Assert.Equal("main", mainResult!.BranchName);
        Assert.Null(mainResult.PullRequestNumber);
    }

    private static GitWorkspaceContext? Resolve(TempDirectory temp, string workingDirectory) =>
        new FileSystemGitContextResolver(temp.Path).Resolve(workingDirectory);
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Git/GitWorkspaceContextTests.cs`, replace:

```csharp
        Assert.Equal(expected, context.IsDetachedHead);
    }
}
```

with:

```csharp
        Assert.Equal(expected, context.IsDetachedHead);
    }

    [Fact]
    public void PullRequestNumber_DefaultsToNullAndTakesPartInEquality()
    {
        GitWorkspaceContext context = new("/repo", "/repo/.git", "feature/x", null, null, null);
        GitWorkspaceContext pullRequest = context with { PullRequestNumber = 42 };

        Assert.Null(context.PullRequestNumber);
        Assert.Equal(42, pullRequest.PullRequestNumber);
        Assert.NotEqual(context, pullRequest);
        Assert.Equal(pullRequest, context with { PullRequestNumber = 42 });
    }
}
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/TestSupport/GitFixture.cs`, replace:

```csharp
using CopilotLedger.Core.Git;

namespace CopilotLedger.Tests.TestSupport;
```

with:

```csharp
using System.Globalization;
using CopilotLedger.Core.Git;

namespace CopilotLedger.Tests.TestSupport;
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/TestSupport/GitFixture.cs`, replace:

```csharp
    // Creates <root>/repo with .git/HEAD "ref: refs/heads/feature/ledger\n", .git/refs/heads/feature/ledger Sha1 + "\n",
    // .git/config "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n", and an empty <root>/repo/src.
    // Returns <root>/repo.
    public static string Create(string root)
    {
        string repo = Path.Combine(root, "repo");
```

with:

```csharp
    // Creates <root>/repo with .git/HEAD "ref: refs/heads/feature/ledger\n", .git/refs/heads/feature/ledger Sha1 + "\n",
    // .git/config "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n", and an empty <root>/repo/src.
    // With pullRequest, the config also has "[branch \"feature/ledger\"]\n\tremote = origin\n\tmerge = refs/pull/<n>/head\n",
    // as gh pr checkout writes it. Returns <root>/repo.
    public static string Create(string root, int? pullRequest = null)
    {
        string repo = Path.Combine(root, "repo");
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/TestSupport/GitFixture.cs`, replace:

```csharp
        File.WriteAllText(
            Path.Combine(git, "config"),
            "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n");

        // Reading the files back waits out a brief antivirus lock, so the resolver sees them when the test runs.
```

with:

```csharp
        File.WriteAllText(
            Path.Combine(git, "config"),
            "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n" +
            (pullRequest is null
                ? string.Empty
                : $"[branch \"feature/ledger\"]\n\tremote = origin\n\tmerge = refs/pull/{pullRequest.Value.ToString(CultureInfo.InvariantCulture)}/head\n"));

        // Reading the files back waits out a brief antivirus lock, so the resolver sees them when the test runs.
```

### Tests

- **Resolve_PullRequestCheckout_ReadsTheNumberFromTheMergeRef:** `GitFixture.Create(root, pullRequest: 42)` resolves from `repo/src` to branch `feature/ledger`, slug `swigerb/copilot-ledger`, and pull request 42; a fixture without `pullRequest` has `null`.
- **Resolve_MergeConfig_YieldsOnlyExplicitPullRequestRefs** (19 cases, branch `feature/x`): `refs/pull/42/head` → 42; `refs/heads/feature/x` → null; another branch's section → null; `[branch "Feature/X"]` → null; `[Branch "feature/x"]` with `MERGE = "refs/pull/7/head" ; gh pr checkout` → 7; a later `merge = refs/heads/…` → null; a later section without `merge` → 42; a later section with `refs/pull/43/head` → 43; 2147483647 → 2147483647; 2147483648, 0, 042, -1, +1, `4 2`, `/merge`, `refs/pull/head`, `refs/pull//head`, and an unterminated quote → null. Every case keeps the origin URL and slug.
- **Resolve_DetachedHead_HasNoPullRequest** and **Resolve_LinkedWorktree_ReadsTheMergeRefFromTheCommonConfig** (the worktree's branch gets 9 from the common config; the main worktree's `main` gets null).
- **PullRequestNumber_DefaultsToNullAndTakesPartInEquality** and **CG4** (the cached context keeps 42, with one inner call in 4 seconds).

### Acceptance criteria

- The number comes only from an explicit `refs/pull/<n>/head` merge ref of the current branch in the on-disk config, with no `git` process, no network, and no inference from branch names.
- The six-parameter constructor is unchanged; no other constructor call changes.
- `dotnet test CopilotLedger.slnx` passes **721** tests (697 + 24).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 3 specification: Ledger Schema Version 4

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/LedgerMigrations.cs` | Migration 4 |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | Inserts the new columns, validates them, and records `agent_span_id` in the backfill |
| `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs` | S1 expects version 4; new S12 and S13 |
| `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs` | SL1, SL8, and SL10 move to version 4; new SL11 and SL12 and the `ChatEntry` helper |
| `tests/CopilotLedger.Tests/Otlp/LedgerIngestorTests.cs` | New IG10 |

### DDL (migration 4, in one transaction)

```sql
ALTER TABLE spans ADD COLUMN cache_read_state TEXT NOT NULL DEFAULT 'legacy_unknown'
    CHECK (cache_read_state IN ('reported', 'absent', 'legacy_unknown'));
ALTER TABLE spans ADD COLUMN cache_write_state TEXT NOT NULL DEFAULT 'legacy_unknown'
    CHECK (cache_write_state IN ('reported', 'absent', 'legacy_unknown'));
ALTER TABLE spans ADD COLUMN pull_request_number INTEGER
    CHECK (pull_request_number IS NULL OR pull_request_number > 0);
ALTER TABLE spans ADD COLUMN agent_span_id TEXT;
CREATE INDEX IF NOT EXISTS ix_spans_conversation_start ON spans (conversation_id, start_time_unix_ms);
```

Then the migration:

1. sets `cache_read_state = 'reported'` where `cache_read_input_tokens > 0`, and `cache_write_state = 'reported'` where `cache_write_input_tokens > 0` (D2); every other legacy row keeps `legacy_unknown`;
2. fills `agent_span_id` with a recursive CTE over `span_links`: start from each `invoke_agent` link's children that aren't `invoke_agent`, follow children that aren't `invoke_agent`, and set each matching `spans` row's `agent_span_id` to the `invoke_agent` span it started from (`UPDATE … FROM`). Rows below no recorded `invoke_agent` keep `NULL`.

### `SqliteLedgerStore` changes

- `InsertSql` and `AddInsertParameters` add `cache_read_state`, `cache_write_state` (`reported`, `absent`, or `legacy_unknown` from `ToDatabaseString`), and `pull_request_number` (`entry.Git?.PullRequestNumber`, or `NULL`).
- `ValidateEntry` adds two checks, after the existing ones:
  - unless both states are `Reported` or `Absent`, and every `Absent` counter is 0: `ArgumentException("Cache counter states must be Reported or Absent, and an Absent counter must be 0.", parameterName)`;
  - when `entry.Git?.PullRequestNumber` is 0 or less: `ArgumentException("Pull request numbers must be positive.", parameterName)`.
- `BackfillFromAgentSpansAsync` runs the first `UPDATE` for every agent link, also setting `agent_span_id = COALESCE(agent_span_id, @agentSpanId)`, and matches rows where `agent_span_id IS NULL` too. The Git `UPDATE` is unchanged.

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Storage/LedgerMigrations.cs`, replace:

```csharp
            VALUES (1, 1500000000000, 5, 5, 0, 1, 0);
            """),
    ];
}
```

with:

```csharp
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
    ];
}
```

Edit 1 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            cache_read_input_tokens, cache_write_input_tokens, billing_channel,
            pricing_status, rates_version, cost_usd_pico, saved_usd_pico,
            repo_root, repo_slug, branch_name, head_commit_sha, recorded_at_unix_ms
        ) VALUES (
            @trace_id, @span_id, @parent_span_id, @operation_name, @conversation_id,
```

with:

```csharp
            cache_read_input_tokens, cache_write_input_tokens, billing_channel,
            pricing_status, rates_version, cost_usd_pico, saved_usd_pico,
            repo_root, repo_slug, branch_name, head_commit_sha, recorded_at_unix_ms,
            cache_read_state, cache_write_state, pull_request_number
        ) VALUES (
            @trace_id, @span_id, @parent_span_id, @operation_name, @conversation_id,
```

Edit 2 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            @cache_read_input_tokens, @cache_write_input_tokens, @billing_channel,
            @pricing_status, @rates_version, @cost_usd_pico, @saved_usd_pico,
            @repo_root, @repo_slug, @branch_name, @head_commit_sha, @recorded_at_unix_ms
        ) ON CONFLICT (trace_id, span_id) DO NOTHING;
        """;

    private readonly TimeProvider _timeProvider;
```

with:

```csharp
            @cache_read_input_tokens, @cache_write_input_tokens, @billing_channel,
            @pricing_status, @rates_version, @cost_usd_pico, @saved_usd_pico,
            @repo_root, @repo_slug, @branch_name, @head_commit_sha, @recorded_at_unix_ms,
            @cache_read_state, @cache_write_state, @pull_request_number
        ) ON CONFLICT (trace_id, span_id) DO NOTHING;
        """;

    private readonly TimeProvider _timeProvider;
```

Edit 3 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        return counts;
    }

    // For each trace, copies each invoke_agent link's agent name, conversation id, and Git slug/branch/sha onto spans rows below it,
    // without crossing a nested invoke_agent. Returns the total rows changed by all UPDATE statements. One transaction.
    // Throws InvalidOperationException if InitializeAsync hasn't run.
    public async Task<int> BackfillFromAgentSpansAsync(
```

with:

```csharp
        return counts;
    }

    // For each trace, copies each invoke_agent link's span ID, agent name, conversation id, and Git slug/branch/sha onto spans
    // rows below it, without crossing a nested invoke_agent, so agent_span_id is each row's nearest invoke_agent ancestor.
    // Returns the total rows changed by all UPDATE statements. One transaction.
    // Throws InvalidOperationException if InitializeAsync hasn't run.
    public async Task<int> BackfillFromAgentSpansAsync(
```

Edit 4 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            foreach (AgentLink agent in agents)
            {
                if (agent.AgentName is not null || agent.ConversationId is not null)
                {
                    await using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = """
```

with:

```csharp
            foreach (AgentLink agent in agents)
            {
                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = """
```

Edit 5 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                        UPDATE spans
                           SET agent_name      = COALESCE(agent_name, @agentName),
                               conversation_id = COALESCE(conversation_id, @conversationId)
                         WHERE trace_id = @trace
                           AND span_id IN (SELECT span_id FROM sub)
                           AND ((agent_name IS NULL AND @agentName IS NOT NULL)
                             OR (conversation_id IS NULL AND @conversationId IS NOT NULL));
                        """;
                    command.Parameters.AddWithValue("@trace", trace);
```

with:

```csharp
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
```

Edit 6 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                parameterName);
        }
    }

    private static void AddInsertParameters(
```

with:

```csharp
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
```

Edit 7 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
            OrDbNull(entry.Git?.HeadCommitSha));
        command.Parameters.AddWithValue("@recorded_at_unix_ms", recordedAtUnixMs);
    }

    private static string GetRollupSql(LedgerGrouping grouping)
```

with:

```csharp
            OrDbNull(entry.Git?.HeadCommitSha));
        command.Parameters.AddWithValue("@recorded_at_unix_ms", recordedAtUnixMs);
        command.Parameters.AddWithValue("@cache_read_state", ToDatabaseString(span.CacheReadState));
        command.Parameters.AddWithValue("@cache_write_state", ToDatabaseString(span.CacheWriteState));
        command.Parameters.AddWithValue(
            "@pull_request_number",
            OrDbNull(entry.Git?.PullRequestNumber));
    }

    private static string GetRollupSql(LedgerGrouping grouping)
```

Edit 8 of 8 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static object OrDbNull<T>(T? value) where T : struct =>
        value.HasValue ? value.Value : DBNull.Value;
```

with:

```csharp
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
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Otlp/LedgerIngestorTests.cs`, replace:

```csharp
        AssertRollup(model, "mai-code-2-flash", 4, 0.086175m, 0.00086175m);
    }

    private static OtlpSpan IgSpan(string spanId, params OtlpAttribute[] attributes) =>
        Span(
```

with:

```csharp
        AssertRollup(model, "mai-code-2-flash", 4, 0.086175m, 0.00086175m);
    }

    [Fact]
    public async Task IG10_PullRequestCheckout_RecordsThePullRequestAndCacheStates()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        string repo = GitFixture.Create(Path.Combine(temp.Path, "pr"), pullRequest: 42);

        AssertResult(
            await harness.Ingestor.IngestAsync(
                CopilotTraces.TraceE([Str("process.working_directory", repo)])),
            1,
            0,
            null,
            1,
            1);
        AssertResult(
            await harness.Ingestor.IngestAsync(CopilotTraces.TraceF()),
            2,
            0,
            null,
            1,
            1);

        await using SqliteConnection connection = new($"Data Source={harness.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT span_id, branch_name, pull_request_number, cache_read_state, cache_write_state, agent_span_id FROM spans ORDER BY span_id;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("0f00000000000002", reader.GetString(0));
        Assert.True(reader.IsDBNull(1));
        Assert.True(reader.IsDBNull(2));
        Assert.Equal("absent", reader.GetString(3));
        Assert.Equal("absent", reader.GetString(4));
        Assert.Equal("0f00000000000001", reader.GetString(5));
        Assert.True(await reader.ReadAsync());
        Assert.Equal("e4c5e6a7b8c9d0e1", reader.GetString(0));
        Assert.Equal("feature/ledger", reader.GetString(1));
        Assert.Equal(42L, reader.GetInt64(2));
        Assert.Equal("reported", reader.GetString(3));
        Assert.Equal("reported", reader.GetString(4));
        Assert.True(reader.IsDBNull(5));
        Assert.False(await reader.ReadAsync());
    }

    private static OtlpSpan IgSpan(string spanId, params OtlpAttribute[] attributes) =>
        Span(
```

Edit 1 of 4 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
    private const string Sha1 = "0123456789abcdef0123456789abcdef01234567";
    private static readonly JsonRateCardCalculator Calculator =
        JsonRateCardCalculator.LoadEmbedded();

    [Fact]
    public async Task SL1_Migration2_UpgradesVersion1AndKeepsRows()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore first = await StoreAsync(temp);
        await first.RecordAsync([Entry("mai-code-2", 1000, 100)]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                DROP INDEX ix_spans_trace;
                DROP TABLE span_links;
                DROP TABLE breaker_trips;
                DROP TABLE branch_budgets;
                DROP TABLE breaker_settings;
                PRAGMA user_version = 1;
                """);
        }

        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();
        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(3L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(
            "span_links",
            await ScalarAsync(
                check,
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'span_links';"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM spans;"));
    }

    [Fact]
    public async Task SL2_RecordSpanLinksAsync_InsertsOnce()
    {
        using TempDirectory temp = new();
```

with:

```csharp
    private const string Sha1 = "0123456789abcdef0123456789abcdef01234567";
    private static readonly JsonRateCardCalculator Calculator =
        JsonRateCardCalculator.LoadEmbedded();

    [Fact]
    public async Task SL1_Migration2_UpgradesVersion1AndKeepsRows()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore first = await StoreAsync(temp);
        await first.RecordAsync([Entry("mai-code-2", 1000, 100)]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                DROP INDEX ix_spans_conversation_start;
                ALTER TABLE spans DROP COLUMN cache_read_state;
                ALTER TABLE spans DROP COLUMN cache_write_state;
                ALTER TABLE spans DROP COLUMN pull_request_number;
                ALTER TABLE spans DROP COLUMN agent_span_id;
                DROP INDEX ix_spans_trace;
                DROP TABLE span_links;
                DROP TABLE breaker_trips;
                DROP TABLE branch_budgets;
                DROP TABLE breaker_settings;
                PRAGMA user_version = 1;
                """);
        }

        // A new process starts with no pooled connections; see SL8.
        SqliteConnection.ClearAllPools();
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();
        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(4L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(
            "span_links",
            await ScalarAsync(
                check,
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'span_links';"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM spans;"));
    }

    [Fact]
    public async Task SL2_RecordSpanLinksAsync_InsertsOnce()
    {
        using TempDirectory temp = new();
```

Edit 2 of 4 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        await first.RecordSpanLinksAsync(
        [
            Link("8", "8888888888888881", null, "execute_tool") with
            {
                ToolName = "powershell",
                ToolExitCode = 1,
                StatusCode = 2,
            },
        ]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                DROP TABLE breaker_trips;
                DROP TABLE branch_budgets;
                DROP TABLE breaker_settings;
                ALTER TABLE span_links DROP COLUMN tool_name;
                ALTER TABLE span_links DROP COLUMN tool_exit_code;
                ALTER TABLE span_links DROP COLUMN status_code;
                PRAGMA user_version = 2;
                """);
        }

        // A new process starts with no pooled connections. A pooled connection keeps the schema it loaded,
        // and SQLite checks ALTER TABLE ADD COLUMN against that cached schema.
        SqliteConnection.ClearAllPools();
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(3L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM span_links;"));
        Assert.Equal(
            1L,
            await ScalarAsync(
                check,
                "SELECT tool_name IS NULL AND tool_exit_code IS NULL AND status_code = 0 FROM span_links;"));
        Assert.Equal(
            1500000000000L,
            await ScalarAsync(
```

with:

```csharp
        await first.RecordSpanLinksAsync(
        [
            Link("8", "8888888888888881", null, "execute_tool") with
            {
                ToolName = "powershell",
                ToolExitCode = 1,
                StatusCode = 2,
            },
        ]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                DROP INDEX ix_spans_conversation_start;
                ALTER TABLE spans DROP COLUMN cache_read_state;
                ALTER TABLE spans DROP COLUMN cache_write_state;
                ALTER TABLE spans DROP COLUMN pull_request_number;
                ALTER TABLE spans DROP COLUMN agent_span_id;
                DROP TABLE breaker_trips;
                DROP TABLE branch_budgets;
                DROP TABLE breaker_settings;
                ALTER TABLE span_links DROP COLUMN tool_name;
                ALTER TABLE span_links DROP COLUMN tool_exit_code;
                ALTER TABLE span_links DROP COLUMN status_code;
                PRAGMA user_version = 2;
                """);
        }

        // A new process starts with no pooled connections. A pooled connection keeps the schema it loaded,
        // and SQLite checks ALTER TABLE ADD COLUMN against that cached schema.
        SqliteConnection.ClearAllPools();
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(4L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM span_links;"));
        Assert.Equal(
            1L,
            await ScalarAsync(
                check,
                "SELECT tool_name IS NULL AND tool_exit_code IS NULL AND status_code = 0 FROM span_links;"));
        Assert.Equal(
            1500000000000L,
            await ScalarAsync(
```

Edit 3 of 4 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
        Assert.True(reader.IsDBNull(2));
        Assert.Equal(1, reader.GetInt32(3));
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task SL10_AnotherProcessAppliesMigrationFirst()
    {
        using TempDirectory temp = new();
        await StoreAsync(temp);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, "PRAGMA user_version = 2;");
        }

        await using SqliteConnection holder = await OpenAsync(DatabasePath(temp));
        SqliteTransaction held = holder.BeginTransaction(deferred: false);
        Task init = Task.Run(
            () => new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync());

        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.False(init.IsCompleted);
        await using (SqliteCommand version = holder.CreateCommand())
        {
            version.Transaction = held;
            version.CommandText = "PRAGMA user_version = 3;";
            await version.ExecuteNonQueryAsync();
        }

        held.Commit();
        await init;

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(3L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM breaker_settings;"));
        Assert.Equal(
            1L,
            await ScalarAsync(
                check,
                "SELECT COUNT(*) FROM pragma_table_info('span_links') WHERE name = 'tool_name';"));
    }

    private static void AssertChain(
        PromptChainRow row,
        string trace,
        string start,
        string end,
        string repository,
        string branch,
        IReadOnlyList<string> agents,
        IReadOnlyList<string> models,
```

with:

```csharp
        Assert.True(reader.IsDBNull(2));
        Assert.Equal(1, reader.GetInt32(3));
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task SL10_AnotherProcessAppliesMigrationFirst()
    {
        using TempDirectory temp = new();
        await StoreAsync(temp);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, "PRAGMA user_version = 3;");
        }

        await using SqliteConnection holder = await OpenAsync(DatabasePath(temp));
        SqliteTransaction held = holder.BeginTransaction(deferred: false);
        Task init = Task.Run(
            () => new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync());

        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.False(init.IsCompleted);
        await using (SqliteCommand version = holder.CreateCommand())
        {
            version.Transaction = held;
            version.CommandText = "PRAGMA user_version = 4;";
            await version.ExecuteNonQueryAsync();
        }

        held.Commit();
        await init;

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(4L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1L, await ScalarAsync(check, "SELECT COUNT(*) FROM breaker_settings;"));
        Assert.Equal(
            1L,
            await ScalarAsync(
                check,
                "SELECT COUNT(*) FROM pragma_table_info('span_links') WHERE name = 'tool_name';"));
    }

    [Fact]
    public async Task SL11_Migration4_KeepsRowsMarksLegacyStatesAndFillsAgents()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore first = await StoreAsync(temp);
        const string trace = "0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c";
        await first.RecordAsync(
        [
            ChatEntry(trace, "c000000000000001", "a000000000000001", 1_000, 800, 100),
            ChatEntry(trace, "c000000000000002", "e000000000000001", 1_000, 300, 0),
            ChatEntry(trace, "c000000000000003", "a000000000000002", 1_000, 0, 200),
            ChatEntry(trace, "c000000000000004", "f000000000000009", 1_000, 0, 0),
        ]);
        await first.RecordSpanLinksAsync(
        [
            Link(trace, "a000000000000001", null, "invoke_agent"),
            Link(trace, "e000000000000001", "a000000000000001", "execute_tool"),
            Link(trace, "a000000000000002", "e000000000000001", "invoke_agent", "explore"),
            Link(trace, "c000000000000001", "a000000000000001", "chat"),
            Link(trace, "c000000000000002", "e000000000000001", "chat"),
            Link(trace, "c000000000000003", "a000000000000002", "chat"),
            Link(trace, "c000000000000004", "f000000000000009", "chat"),
        ]);
        await using (SqliteConnection connection = await OpenAsync(DatabasePath(temp)))
        {
            await ExecuteAsync(connection, """
                DROP INDEX ix_spans_conversation_start;
                ALTER TABLE spans DROP COLUMN cache_read_state;
                ALTER TABLE spans DROP COLUMN cache_write_state;
                ALTER TABLE spans DROP COLUMN pull_request_number;
                ALTER TABLE spans DROP COLUMN agent_span_id;
                PRAGMA user_version = 3;
                """);
        }

        // A new process starts with no pooled connections; see SL8.
        SqliteConnection.ClearAllPools();
        await new SqliteLedgerStore(DatabasePath(temp)).InitializeAsync();

        await using SqliteConnection check = await OpenAsync(DatabasePath(temp));
        Assert.Equal(4L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(
            "ix_spans_conversation_start",
            await ScalarAsync(
                check,
                "SELECT name FROM sqlite_master WHERE type = 'index' AND name = 'ix_spans_conversation_start';"));
        Assert.Equal(4_000L, await ScalarAsync(check, "SELECT SUM(input_tokens) FROM spans;"));
        await using SqliteCommand command = check.CreateCommand();
        command.CommandText =
            "SELECT span_id, cache_read_state, cache_write_state, pull_request_number, agent_span_id FROM spans ORDER BY span_id;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        string?[][] rows =
        [
            ["c000000000000001", "reported", "reported", null, "a000000000000001"],
            ["c000000000000002", "reported", "legacy_unknown", null, "a000000000000001"],
            ["c000000000000003", "legacy_unknown", "reported", null, "a000000000000002"],
            ["c000000000000004", "legacy_unknown", "legacy_unknown", null, null],
        ];
        foreach (string?[] row in rows)
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(row[0], reader.GetString(0));
            Assert.Equal(row[1], reader.GetString(1));
            Assert.Equal(row[2], reader.GetString(2));
            Assert.True(reader.IsDBNull(3));
            Assert.Equal(row[4], reader.IsDBNull(4) ? null : reader.GetString(4));
        }

        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task SL12_Backfill_RecordsEachSpansNearestAgent()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        const string trace = "0d0d0d0d0d0d0d0d0d0d0d0d0d0d0d0d";
        await store.RecordAsync(
        [
            ChatEntry(trace, "c000000000000001", "a000000000000001", 1_000, 0, 0),
            ChatEntry(trace, "c000000000000002", "a000000000000002", 2_000, 0, 0),
            ChatEntry(trace, "c000000000000003", "a000000000000002", 3_000, 0, 0),
        ]);
        await store.RecordSpanLinksAsync(
        [
            Link(trace, "c000000000000001", "a000000000000001", "chat"),
            Link(trace, "c000000000000002", "a000000000000002", "chat"),
            Link(trace, "c000000000000003", "a000000000000002", "chat"),
        ]);
        Assert.Equal(0, await store.BackfillFromAgentSpansAsync([trace]));

        await store.RecordSpanLinksAsync(
        [
            Link(trace, "a000000000000002", "e000000000000001", "invoke_agent", "explore", "conv-d"),
        ]);
        Assert.Equal(2, await store.BackfillFromAgentSpansAsync([trace]));

        await store.RecordSpanLinksAsync(
        [
            Link(trace, "e000000000000001", "a000000000000001", "execute_tool"),
            Link(trace, "a000000000000001", null, "invoke_agent"),
        ]);
        Assert.Equal(1, await store.BackfillFromAgentSpansAsync([trace]));
        Assert.Equal(0, await store.BackfillFromAgentSpansAsync([trace]));

        await using SqliteConnection connection = await OpenAsync(DatabasePath(temp));
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT span_id, agent_span_id, agent_name, conversation_id FROM spans ORDER BY span_id;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("c000000000000001", reader.GetString(0));
        Assert.Equal("a000000000000001", reader.GetString(1));
        Assert.True(reader.IsDBNull(2));
        Assert.True(reader.IsDBNull(3));
        Assert.True(await reader.ReadAsync());
        Assert.Equal("c000000000000002", reader.GetString(0));
        Assert.Equal("a000000000000002", reader.GetString(1));
        Assert.Equal("explore", reader.GetString(2));
        Assert.Equal("conv-d", reader.GetString(3));
        Assert.True(await reader.ReadAsync());
        Assert.Equal("c000000000000003", reader.GetString(0));
        Assert.Equal("a000000000000002", reader.GetString(1));
        Assert.Equal("explore", reader.GetString(2));
        Assert.Equal("conv-d", reader.GetString(3));
        Assert.False(await reader.ReadAsync());
    }

    private static void AssertChain(
        PromptChainRow row,
        string trace,
        string start,
        string end,
        string repository,
        string branch,
        IReadOnlyList<string> agents,
        IReadOnlyList<string> models,
```

Edit 4 of 4 in `tests/CopilotLedger.Tests/Storage/SpanLinkStoreTests.cs`, replace:

```csharp
    private static LedgerEntry Entry(string model, long input, long output)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: model,
            inputTokens: input,
            outputTokens: output) with
        {
            TraceId = "5b8efff798038103d269b633813fc60c",
            SpanId = "eee19b7ec3c1b174",
            ParentSpanId = "eee19b7ec3c1b173",
        };
        return new LedgerEntry(span, Calculator.Calculate(span), null);
    }

    private static GenAiSpanLink Link(
        string trace,
        string span,
        string? parent,
        string? operation = null,
        string? agent = null,
        string? conversation = null,
        string? slug = null,
        string? branch = null,
```

with:

```csharp
    private static LedgerEntry Entry(string model, long input, long output)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: model,
            inputTokens: input,
            outputTokens: output) with
        {
            TraceId = "5b8efff798038103d269b633813fc60c",
            SpanId = "eee19b7ec3c1b174",
            ParentSpanId = "eee19b7ec3c1b173",
        };
        return new LedgerEntry(span, Calculator.Calculate(span), null);
    }

    // A mai-code-2 chat span; a nonzero cache counter is Reported and a zero one is Absent, as TestSpans.Chat sets them.
    private static LedgerEntry ChatEntry(
        string trace,
        string span,
        string parent,
        long input,
        long cacheRead,
        long cacheWrite)
    {
        NormalizedGenAiSpan chat = TestSpans.Chat(
            requestModel: "mai-code-2",
            inputTokens: input,
            outputTokens: 10,
            cacheReadInputTokens: cacheRead,
            cacheWriteInputTokens: cacheWrite) with
        {
            TraceId = trace,
            SpanId = span,
            ParentSpanId = parent,
        };
        return new LedgerEntry(chat, Calculator.Calculate(chat), null);
    }

    private static GenAiSpanLink Link(
        string trace,
        string span,
        string? parent,
        string? operation = null,
        string? agent = null,
        string? conversation = null,
        string? slug = null,
        string? branch = null,
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs`, replace:

```csharp
            await using (SqliteCommand versionCommand = connection.CreateCommand())
            {
                versionCommand.CommandText = "PRAGMA user_version;";
                Assert.Equal(3L, await versionCommand.ExecuteScalarAsync());
            }
        }

        await store.InitializeAsync();
```

with:

```csharp
            await using (SqliteCommand versionCommand = connection.CreateCommand())
            {
                versionCommand.CommandText = "PRAGMA user_version;";
                Assert.Equal(4L, await versionCommand.ExecuteScalarAsync());
            }
        }

        await store.InitializeAsync();
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs`, replace:

```csharp
        Assert.Equal(0.0000025m, v12.AiCredits);
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
```

with:

```csharp
        Assert.Equal(0.0000025m, v12.AiCredits);
    }

    [Fact]
    public async Task S12_RecordAsync_StoresCacheStatesAndPullRequest()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbedded();
        NormalizedGenAiSpan reported = TestSpans.Chat(
            requestModel: "claude-opus-5.5",
            inputTokens: 1_000,
            outputTokens: 10,
            cacheReadInputTokens: 600,
            cacheWriteState: CacheCounterState.Reported) with
        {
            TraceId = new string('a', 32),
            SpanId = "aaaaaaaaaaaaaaa1",
        };
        NormalizedGenAiSpan absent = TestSpans.Chat(
            requestModel: "mai-code-2",
            inputTokens: 500,
            outputTokens: 5) with
        {
            TraceId = new string('a', 32),
            SpanId = "aaaaaaaaaaaaaaa2",
        };
        GitWorkspaceContext pullRequest =
            Git(temp.Path, "swigerb/copilot-ledger", "feature/ledger") with { PullRequestNumber = 42 };

        Assert.Equal(2, await store.RecordAsync(
        [
            new LedgerEntry(reported, calculator.Calculate(reported), pullRequest),
            new LedgerEntry(absent, calculator.Calculate(absent), null),
        ]));

        await using SqliteConnection connection = new($"Data Source={DatabasePath(temp)}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT span_id, cache_read_state, cache_write_state, pull_request_number, agent_span_id FROM spans ORDER BY span_id;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("aaaaaaaaaaaaaaa1", reader.GetString(0));
        Assert.Equal("reported", reader.GetString(1));
        Assert.Equal("reported", reader.GetString(2));
        Assert.Equal(42L, reader.GetInt64(3));
        Assert.True(reader.IsDBNull(4));
        Assert.True(await reader.ReadAsync());
        Assert.Equal("aaaaaaaaaaaaaaa2", reader.GetString(0));
        Assert.Equal("absent", reader.GetString(1));
        Assert.Equal("absent", reader.GetString(2));
        Assert.True(reader.IsDBNull(3));
        Assert.True(reader.IsDBNull(4));
        Assert.False(await reader.ReadAsync());
    }

    [Theory]
    [InlineData(0, "Cache counter states must be Reported or Absent, and an Absent counter must be 0. (Parameter 'entries')")]
    [InlineData(1, "Cache counter states must be Reported or Absent, and an Absent counter must be 0. (Parameter 'entries')")]
    [InlineData(2, "Cache counter states must be Reported or Absent, and an Absent counter must be 0. (Parameter 'entries')")]
    [InlineData(3, "Cache counter states must be Reported or Absent, and an Absent counter must be 0. (Parameter 'entries')")]
    [InlineData(4, "Cache counter states must be Reported or Absent, and an Absent counter must be 0. (Parameter 'entries')")]
    [InlineData(5, "Pull request numbers must be positive. (Parameter 'entries')")]
    [InlineData(6, "Pull request numbers must be positive. (Parameter 'entries')")]
    public async Task S13_RecordAsync_RejectsInconsistentCacheStatesAndPullRequests(int caseNumber, string message)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        IReadOnlyList<LedgerEntry> seed = CreateSeedEntries(temp.Path);
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: "mai-code-2",
            inputTokens: 1_000,
            outputTokens: 10);
        span = caseNumber switch
        {
            0 => span with { CacheReadState = CacheCounterState.LegacyUnknown },
            1 => span with { CacheWriteState = CacheCounterState.LegacyUnknown },
            2 => span with { CacheReadInputTokens = 5 },
            3 => span with { CacheWriteInputTokens = 5 },
            4 => span with { CacheReadState = (CacheCounterState)7 },
            _ => span,
        };
        GitWorkspaceContext? git = caseNumber switch
        {
            5 => Git(temp.Path, "swigerb/copilot-ledger", "main") with { PullRequestNumber = 0 },
            6 => Git(temp.Path, "swigerb/copilot-ledger", "main") with { PullRequestNumber = -1 },
            _ => null,
        };
        LedgerEntry invalid = new(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), git);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => store.RecordAsync([seed[0], invalid]));

        Assert.Equal(message, exception.Message);
        Assert.Empty(await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
```

### Tests

- **S1, SL1, SL8, SL10** now expect `user_version` 4. SL1 and SL8 also drop the version 4 index and columns before downgrading, and SL1 clears the connection pools first, as SL8 does.
- **SL11:** four legacy rows, (read 800, write 100), (300, 0), (0, 200), and (0, 0), migrate to (`reported`, `reported`), (`reported`, `legacy_unknown`), (`legacy_unknown`, `reported`), and (`legacy_unknown`, `legacy_unknown`); input still sums to 4,000; the index exists; and `agent_span_id` is the root agent for a span under it and under its `execute_tool` span, the `explore` agent for a span below that nested agent, and `NULL` for a span whose parent was never recorded.
- **SL12:** the backfill returns 0 before any `invoke_agent` link, 2 when the subagent's link arrives (which also sets `explore` and `conv-d`), 1 when the root's link without a name arrives, and 0 when run again; each span keeps its nearest agent.
- **S12:** a span with a cache read and a reported 0 write stores `reported`/`reported`, one without counters stores `absent`/`absent`, and pull request 42 is stored. **S13** (7 cases): `LegacyUnknown` in either state, a nonzero `Absent` read or write, an undefined state (`(CacheCounterState)7`), and pull request 0 or -1 throw `ArgumentException` with the messages above plus ` (Parameter 'entries')`, and nothing is recorded, not even the valid entry in the same batch.
- **IG10:** ingesting `CopilotTraces.TraceE` from a `GitFixture` repository with pull request 42, then `TraceF`, stores pull request 42 and `reported`/`reported` for TraceE's span, and `absent`/`absent` with `agent_span_id` `0f00000000000001` for TraceF's span.

### Acceptance criteria

- Existing rows are kept; legacy rows are `legacy_unknown` except where a counter is nonzero.
- New rows always have explicit `reported` or `absent` states; `spans.pull_request_number` holds the resolved pull request.
- `dotnet test CopilotLedger.slnx` passes **732** tests (721 + 11).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 4 specification: Cache Coverage in Rollups

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/LedgerFilter.cs` | Adds `PullRequestNumber` and `Agent` |
| `src/CopilotLedger.Storage/LedgerRollupRow.cs` | Adds four required sums and two computed properties |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | `FilterSql`, `ReportedSql`, `UncachedSql`, and `AddFilterParameters`; the filtered queries use them; rollups sum coverage |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | `Sum` adds the new sums |
| `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs` | `NoSpend` sets the new sums to 0 |
| `src/CopilotLedger.Mcp/LedgerMcpTools.cs` | `EmptyRollup` sets the new sums to 0 |
| `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs` | New S14 and S15, and the helpers `AssertCoverage` and `FilterSpan` |

### Contracts

```csharp
public sealed record LedgerFilter
{
    // From, To, Repository, Branch as before
    public int? PullRequestNumber { get; init; }   // spans recorded with this pull request number
    public string? Agent { get; init; }            // agent name, or (none) for spans without one
}

public sealed record LedgerRollupRow
{
    // existing members as before, then:
    public required long CacheReportedSpanCount { get; init; }         // spans where CacheMetrics.IsReported
    public required long ReportedInputTokens { get; init; }
    public required long ReportedCacheReadInputTokens { get; init; }
    public required long ReportedUncachedInputTokens { get; init; }    // Σ max(input − read − write, 0) over those spans
    public long CacheUnreportedSpanCount => SpanCount - CacheReportedSpanCount;
    public decimal? CacheHitRatio => CacheMetrics.HitRatio(ReportedCacheReadInputTokens, ReportedInputTokens);
}
```

SQL fragments, each used by every query that needs it:

```sql
-- FilterSql: a null parameter matches every span
(@from IS NULL OR start_time_unix_ms >= @from)
  AND (@to IS NULL OR start_time_unix_ms < @to)
  AND (@repository IS NULL OR COALESCE(repo_slug, repo_root, '(none)') = @repository)
  AND (@branch IS NULL OR COALESCE(branch_name, '(none)') = @branch)
  AND (@pull_request IS NULL OR pull_request_number = @pull_request)
  AND (@agent IS NULL OR COALESCE(agent_name, '(none)') = @agent)
-- ReportedSql (CacheMetrics.IsReported)
(cache_read_state = 'reported' OR cache_write_state = 'reported')
-- UncachedSql
MAX(input_tokens - cache_read_input_tokens - cache_write_input_tokens, 0)
```

`GetRollupAsync`, both queries in `GetTopPromptChainsAsync`, and `GetPromptCountAsync` use `WHERE {FilterSql}` and `AddFilterParameters`, with the same results as before for the four existing filter properties. The rollup adds `SUM(CASE WHEN {ReportedSql} THEN … ELSE 0 END)` for the span count, input, cache reads, and `{UncachedSql}`.

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs`, replace:

```csharp
        CacheReadInputTokens = 0,
        CacheWriteInputTokens = 0,
        AiCredits = 0m,
        AiCreditsUsd = 0m,
```

with:

```csharp
        CacheReadInputTokens = 0,
        CacheWriteInputTokens = 0,
        CacheReportedSpanCount = 0,
        ReportedInputTokens = 0,
        ReportedCacheReadInputTokens = 0,
        ReportedUncachedInputTokens = 0,
        AiCredits = 0m,
        AiCreditsUsd = 0m,
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            CacheReadInputTokens = rows.Sum(row => row.CacheReadInputTokens),
            CacheWriteInputTokens = rows.Sum(row => row.CacheWriteInputTokens),
            AiCredits = rows.Sum(row => row.AiCredits),
            AiCreditsUsd = rows.Sum(row => row.AiCreditsUsd),
```

with:

```csharp
            CacheReadInputTokens = rows.Sum(row => row.CacheReadInputTokens),
            CacheWriteInputTokens = rows.Sum(row => row.CacheWriteInputTokens),
            CacheReportedSpanCount = rows.Sum(row => row.CacheReportedSpanCount),
            ReportedInputTokens = rows.Sum(row => row.ReportedInputTokens),
            ReportedCacheReadInputTokens = rows.Sum(row => row.ReportedCacheReadInputTokens),
            ReportedUncachedInputTokens = rows.Sum(row => row.ReportedUncachedInputTokens),
            AiCredits = rows.Sum(row => row.AiCredits),
            AiCreditsUsd = rows.Sum(row => row.AiCreditsUsd),
```

Edit 1 of 1 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
        CacheReadInputTokens = 0,
        CacheWriteInputTokens = 0,
        AiCredits = 0m,
        AiCreditsUsd = 0m,
```

with:

```csharp
        CacheReadInputTokens = 0,
        CacheWriteInputTokens = 0,
        CacheReportedSpanCount = 0,
        ReportedInputTokens = 0,
        ReportedCacheReadInputTokens = 0,
        ReportedUncachedInputTokens = 0,
        AiCredits = 0m,
        AiCreditsUsd = 0m,
```

Edit 1 of 1 in `src/CopilotLedger.Storage/LedgerFilter.cs`, replace:

```csharp
    public string? Repository { get; init; }
    public string? Branch { get; init; }
}
```

with:

```csharp
    public string? Repository { get; init; }
    public string? Branch { get; init; }
    public int? PullRequestNumber { get; init; }   // spans recorded with this pull request number
    public string? Agent { get; init; }            // agent name, or (none) for spans without one
}
```

Edit 1 of 2 in `src/CopilotLedger.Storage/LedgerRollupRow.cs`, replace:

```csharp
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Storage;
```

with:

```csharp
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;
```

Edit 2 of 2 in `src/CopilotLedger.Storage/LedgerRollupRow.cs`, replace:

```csharp
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required decimal AiCredits { get; init; }
    public required decimal AiCreditsUsd { get; init; }
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
}
```

with:

```csharp
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required long CacheReportedSpanCount { get; init; }         // spans that reported cache usage (CacheMetrics.IsReported)
    public required long ReportedInputTokens { get; init; }            // input tokens of those spans
    public required long ReportedCacheReadInputTokens { get; init; }   // cache-read tokens of those spans
    public required long ReportedUncachedInputTokens { get; init; }    // their input less cache reads and writes, at least 0 a span
    public required decimal AiCredits { get; init; }
    public required decimal AiCreditsUsd { get; init; }
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
    public long CacheUnreportedSpanCount => SpanCount - CacheReportedSpanCount;
    public decimal? CacheHitRatio => CacheMetrics.HitRatio(ReportedCacheReadInputTokens, ReportedInputTokens);
}
```

Edit 1 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        ) ON CONFLICT (trace_id, span_id) DO NOTHING;
        """;

    private readonly TimeProvider _timeProvider;
```

with:

```csharp
        ) ON CONFLICT (trace_id, span_id) DO NOTHING;
        """;

    // The conditions of a LedgerFilter, with parameters from AddFilterParameters. A null filter property matches every span.
    private const string FilterSql = """
        (@from IS NULL OR start_time_unix_ms >= @from)
          AND (@to IS NULL OR start_time_unix_ms < @to)
          AND (@repository IS NULL OR COALESCE(repo_slug, repo_root, '(none)') = @repository)
          AND (@branch IS NULL OR COALESCE(branch_name, '(none)') = @branch)
          AND (@pull_request IS NULL OR pull_request_number = @pull_request)
          AND (@agent IS NULL OR COALESCE(agent_name, '(none)') = @agent)
        """;

    // CacheMetrics.IsReported: the span carried at least one cache counter.
    private const string ReportedSql = "(cache_read_state = 'reported' OR cache_write_state = 'reported')";

    // A span's input tokens that were neither read from nor written to the cache, never below 0.
    private const string UncachedSql = "MAX(input_tokens - cache_read_input_tokens - cache_write_input_tokens, 0)";

    private readonly TimeProvider _timeProvider;
```

Edit 2 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = GetRollupSql(grouping);
        command.Parameters.AddWithValue("@from", OrDbNull(filter.From?.ToUnixTimeMilliseconds()));
        command.Parameters.AddWithValue("@to", OrDbNull(filter.To?.ToUnixTimeMilliseconds()));
        command.Parameters.AddWithValue("@repository", OrDbNull(filter.Repository));
        command.Parameters.AddWithValue("@branch", OrDbNull(filter.Branch));

        List<LedgerRollupRow> rows = [];
```

with:

```csharp
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = GetRollupSql(grouping);
        AddFilterParameters(command, filter);

        List<LedgerRollupRow> rows = [];
```

Edit 3 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                CacheWriteInputTokens =
                    reader.GetInt64(reader.GetOrdinal("cache_write_input_tokens")),
                AiCredits = AiCreditMath.UsdToAiCredits(aiCreditsUsd),
                AiCreditsUsd = aiCreditsUsd,
```

with:

```csharp
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
```

Edit 4 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = """
                SELECT trace_id,
                       MIN(start_time_unix_ms)                                                            AS start_ms,
```

with:

```csharp
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = $"""
                SELECT trace_id,
                       MIN(start_time_unix_ms)                                                            AS start_ms,
```

Edit 5 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
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
```

with:

```csharp
                       SUM(saved_usd_pico)                                                                AS saved_usd_pico
                FROM spans
                WHERE {FilterSql}
                GROUP BY trace_id
                ORDER BY github_usd_pico + byok_usd_pico DESC, saved_usd_pico DESC, start_ms DESC, trace_id ASC
                LIMIT @limit;
                """;
            AddFilterParameters(query, filter);
            query.Parameters.AddWithValue("@limit", limit);
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
```

Edit 6 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
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
```

with:

```csharp
                SELECT DISTINCT trace_id, agent_name, model_id
                FROM spans
                WHERE {FilterSql}
                  AND trace_id IN ({traceParameters});
                """;
            AddFilterParameters(query, filter);
            for (int index = 0; index < data.Count; index++)
            {
                query.Parameters.AddWithValue($"@t{index}", data[index].TraceId);
```

Edit 7 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
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
```

with:

```csharp
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT COUNT(DISTINCT trace_id)
            FROM spans
            WHERE {FilterSql};
            """;
        AddFilterParameters(command, filter);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
```

Edit 8 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        command.Parameters.AddWithValue("@branch", OrDbNull(filter.Branch));
        command.Parameters.AddWithValue("@repo_root", OrDbNull(filter.RepositoryRoot));
    }

    private static async Task<IReadOnlyList<BreakerTrip>> ReadTripsAsync(
```

with:

```csharp
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
    }

    private static async Task<IReadOnlyList<BreakerTrip>> ReadTripsAsync(
```

Edit 9 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
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
```

with:

```csharp
                   SUM(cache_read_input_tokens)                                                      AS cache_read_input_tokens,
                   SUM(cache_write_input_tokens)                                                     AS cache_write_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END)                                    AS cache_reported_span_count,
                   SUM(CASE WHEN {ReportedSql} THEN input_tokens ELSE 0 END)                         AS reported_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN cache_read_input_tokens ELSE 0 END)              AS reported_cache_read_input_tokens,
                   SUM(CASE WHEN {ReportedSql} THEN {UncachedSql} ELSE 0 END)                        AS reported_uncached_input_tokens,
                   SUM(CASE WHEN billing_channel = 'github_ai_credits' THEN cost_usd_pico ELSE 0 END) AS github_usd_pico,
                   SUM(CASE WHEN billing_channel = 'foundry_byok' THEN cost_usd_pico ELSE 0 END)      AS byok_usd_pico,
                   SUM(saved_usd_pico)                                                               AS saved_usd_pico
            FROM spans
            WHERE {FilterSql}
            GROUP BY {keyAliases}
            ORDER BY github_usd_pico + byok_usd_pico DESC, saved_usd_pico DESC, {keyAliases} ASC;
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs`, replace:

```csharp
        Assert.Empty(await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
```

with:

```csharp
        Assert.Empty(await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
    }

    [Fact]
    public async Task S14_Rollups_CountCacheCoverage()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        Assert.Collection(
            await store.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All),
            row => AssertCoverage(row, "claude-opus-5.5", 1, 1, 1_200_000, 800_000, 300_000, 0.6666666666666666666666666667m),
            row => AssertCoverage(row, "mai-code-2", 1, 1, 1_000_000, 400_000, 600_000, 0.4m),
            row => AssertCoverage(row, "gpt-6.1-sol", 1, 0, 0, 0, 0, null),
            row => AssertCoverage(row, "qwen3.8-27b", 1, 0, 0, 0, 0, null),
            row => AssertCoverage(row, "gpt-unknown", 1, 0, 0, 0, 0, null));
        Assert.Collection(
            await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All),
            row => AssertCoverage(row, "swigerb/copilot-ledger", 3, 2, 2_200_000, 1_200_000, 900_000, 0.5454545454545454545454545455m),
            row => AssertCoverage(row, "contoso/api", 1, 0, 0, 0, 0, null),
            row => AssertCoverage(row, "(none)", 1, 0, 0, 0, 0, null));
    }

    [Fact]
    public async Task S15_Filters_SelectPullRequestsAndAgents()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbedded();
        GitWorkspaceContext feature =
            Git(temp.Path, "swigerb/copilot-ledger", "feature/ledger") with { PullRequestNumber = 42 };
        GitWorkspaceContext main = Git(temp.Path, "swigerb/copilot-ledger", "main");
        NormalizedGenAiSpan p1 = FilterSpan('7', "claude-opus-5.5", "ledger-builder");
        NormalizedGenAiSpan p2 = FilterSpan('8', "mai-code-2", null);
        NormalizedGenAiSpan p3 = FilterSpan('9', "mai-code-2", "ledger-builder");
        await store.RecordAsync(
        [
            new LedgerEntry(p1, calculator.Calculate(p1), feature),
            new LedgerEntry(p2, calculator.Calculate(p2), feature),
            new LedgerEntry(p3, calculator.Calculate(p3), main),
        ]);

        Assert.Equal(2, Assert.Single(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            new LedgerFilter { PullRequestNumber = 42 })).SpanCount);
        Assert.Empty(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            new LedgerFilter { PullRequestNumber = 7 }));
        Assert.Equal(2, Assert.Single(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            new LedgerFilter { Agent = "ledger-builder" })).SpanCount);
        Assert.Equal(1, Assert.Single(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            new LedgerFilter { Agent = "(none)" })).SpanCount);
        Assert.Equal(1, Assert.Single(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            new LedgerFilter { PullRequestNumber = 42, Agent = "ledger-builder" })).SpanCount);
        Assert.Equal(
            [new string('7', 32), new string('8', 32)],
            (await store.GetTopPromptChainsAsync(new LedgerFilter { PullRequestNumber = 42 }, 10))
                .Select(row => row.TraceId));
        Assert.Equal(
            [new string('7', 32), new string('9', 32)],
            (await store.GetTopPromptChainsAsync(new LedgerFilter { Agent = "ledger-builder" }, 10))
                .Select(row => row.TraceId));
        Assert.Equal(2, await store.GetPromptCountAsync(new LedgerFilter { PullRequestNumber = 42 }));
        Assert.Equal(1, await store.GetPromptCountAsync(new LedgerFilter { Agent = "(none)" }));
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs`, replace:

```csharp
        Assert.Equal(savedUsd, row.LocalSavedUsd);
    }
}
```

with:

```csharp
        Assert.Equal(savedUsd, row.LocalSavedUsd);
    }

    // key is the row's Model, or its Repository for a Repository rollup.
    private static void AssertCoverage(
        LedgerRollupRow row,
        string key,
        long spans,
        long reported,
        long reportedInput,
        long reportedCacheRead,
        long reportedUncached,
        decimal? hitRatio)
    {
        Assert.Equal(key, row.Model ?? row.Repository);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(reported, row.CacheReportedSpanCount);
        Assert.Equal(spans - reported, row.CacheUnreportedSpanCount);
        Assert.Equal(reportedInput, row.ReportedInputTokens);
        Assert.Equal(reportedCacheRead, row.ReportedCacheReadInputTokens);
        Assert.Equal(reportedUncached, row.ReportedUncachedInputTokens);
        Assert.Equal(hitRatio, row.CacheHitRatio);
    }

    private static NormalizedGenAiSpan FilterSpan(char trace, string model, string? agent) =>
        TestSpans.Chat(
            startTime: Cutover.AddHours(-3),
            requestModel: model,
            agentName: agent,
            inputTokens: 10_000,
            outputTokens: 1_000) with
        {
            TraceId = new string(trace, 32),
            SpanId = new string(trace, 16),
        };
}
```

### Tests

- **S14:** over the seeded ledger, by model: `claude-opus-5.5` 1 span, 1 reported, reported input 1,200,000, cache read 800,000, uncached 300,000, ratio `0.6666666666666666666666666667`; `mai-code-2` 1,000,000, 400,000, 600,000, `0.4`; `gpt-6.1-sol`, `qwen3.8-27b`, and `gpt-unknown` 0 reported and a `null` ratio. By repository: `swigerb/copilot-ledger` 3 spans, 2 reported, 2,200,000, 1,200,000, 900,000, `0.5454545454545454545454545455`; `contoso/api` and `(none)` `null`.
- **S15:** pull request 42 selects 2 of 3 spans, pull request 7 none; agent `ledger-builder` 2, `(none)` 1, both filters together 1; `GetTopPromptChainsAsync` and `GetPromptCountAsync` honor both filters.

### Acceptance criteria

- One `FilterSql` serves every filtered query; existing rollup, prompt-chain, and receipt tests pass unchanged.
- `dotnet test CopilotLedger.slnx` passes **734** tests (732 + 2).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 5 specification: Cache-Efficiency Query

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/CacheEfficiencyRow.cs` | New |
| `src/CopilotLedger.Storage/LedgerGrouping.cs` | Adds `PullRequest` |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | New `GetCacheEfficiencyAsync` |
| `tests/CopilotLedger.Tests/Storage/CacheEfficiencyTests.cs` | New: CE1–CE5 |

### Contracts

```csharp
public enum LedgerGrouping { Repository, Branch, Model, Agent, ChannelModel, PullRequest }

public sealed record CacheEfficiencyRow
{
    public string? Repository { get; init; }        // Repository, Branch, and PullRequest groupings
    public string? Branch { get; init; }            // Branch; PullRequest when PullRequestNumber is null
    public int? PullRequestNumber { get; init; }    // PullRequest
    public string? Model { get; init; }             // Model
    public string? Agent { get; init; }             // Agent
    public required long SpanCount { get; init; }
    public required long CacheReportedSpanCount { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long MaxInputTokens { get; init; }
    public required long ReportedInputTokens { get; init; }
    public required long ReportedCacheReadInputTokens { get; init; }
    public required long ReportedCacheWriteInputTokens { get; init; }
    public required long ReportedUncachedInputTokens { get; init; }     // Σ max(input − read − write, 0) over reported spans
    public required long EstimatedUncachedInputTokens { get; init; }    // the same over all spans, as pricing counts them
    public long CacheUnreportedSpanCount => SpanCount - CacheReportedSpanCount;
    public long? UncachedInputTokens => CacheReportedSpanCount == 0 ? null : ReportedUncachedInputTokens;   // observed
    public decimal? CacheHitRatio => CacheMetrics.HitRatio(ReportedCacheReadInputTokens, ReportedInputTokens);
    public decimal? AverageInputTokensPerTurn => SpanCount == 0 ? null : (decimal)InputTokens / SpanCount;
}

public Task<IReadOnlyList<CacheEfficiencyRow>> GetCacheEfficiencyAsync(
    LedgerGrouping grouping, LedgerFilter filter, CancellationToken cancellationToken = default);
```

### Algorithm

1. Group keys: `Repository` → `COALESCE(repo_slug, repo_root, '(none)')`; `Branch` → that and `COALESCE(branch_name, '(none)')`; `Model` → `COALESCE(model_id, '(none)')`; `Agent` → `COALESCE(agent_name, '(none)')`; `PullRequest` → the repository, `pull_request_number`, and `CASE WHEN pull_request_number IS NULL THEN COALESCE(branch_name, '(none)') END`, so spans with a number group by repository and number, and the rest by repository and branch. `ChannelModel` throws `ArgumentOutOfRangeException`.
2. Sums over all filtered spans: count, input, output, `MAX(input_tokens)`, and `SUM({UncachedSql})` as the estimate; and over the spans matching `ReportedSql`: count, input, cache reads, cache writes, and `{UncachedSql}`.
3. Order by input tokens descending, then the group keys ascending.
4. `GetRollupAsync` keeps throwing `ArgumentOutOfRangeException` for any grouping it doesn't know, now including `PullRequest`.

### Code (copy exactly)

New file `src/CopilotLedger.Storage/CacheEfficiencyRow.cs`:

```csharp
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

// One group's prompt-cache efficiency. Counts and sums cover all the group's spans, except that Reported* sums cover only
// the spans that reported cache usage (CacheMetrics.IsReported). Nothing is rounded.
public sealed record CacheEfficiencyRow
{
    public string? Repository { get; init; }        // Repository, Branch, and PullRequest groupings
    public string? Branch { get; init; }            // Branch grouping, and PullRequest grouping when PullRequestNumber is null
    public int? PullRequestNumber { get; init; }    // PullRequest grouping; null for the spans of a branch without one
    public string? Model { get; init; }             // Model grouping
    public string? Agent { get; init; }             // Agent grouping
    public required long SpanCount { get; init; }
    public required long CacheReportedSpanCount { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long MaxInputTokens { get; init; }
    public required long ReportedInputTokens { get; init; }
    public required long ReportedCacheReadInputTokens { get; init; }
    public required long ReportedCacheWriteInputTokens { get; init; }
    public required long ReportedUncachedInputTokens { get; init; }     // input less cache reads and writes, at least 0 a span
    public required long EstimatedUncachedInputTokens { get; init; }    // the same over all spans, as pricing counts them
    public long CacheUnreportedSpanCount => SpanCount - CacheReportedSpanCount;

    // Observed uncached input; null when no span reported cache usage. It covers only the reported spans.
    public long? UncachedInputTokens => CacheReportedSpanCount == 0 ? null : ReportedUncachedInputTokens;
    public decimal? CacheHitRatio => CacheMetrics.HitRatio(ReportedCacheReadInputTokens, ReportedInputTokens);
    public decimal? AverageInputTokensPerTurn => SpanCount == 0 ? null : (decimal)InputTokens / SpanCount;
}
```

Edit 1 of 1 in `src/CopilotLedger.Storage/LedgerGrouping.cs`, replace:

```csharp
    Agent,
    ChannelModel,
}
```

with:

```csharp
    Agent,
    ChannelModel,
    PullRequest,     // GetCacheEfficiencyAsync only: repository and pull request, or repository and branch without one
}
```

Edit 1 of 1 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                LocalSavedUsd =
                    PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("saved_usd_pico"))),
            });
        }

        return rows;
```

with:

```csharp
                LocalSavedUsd =
                    PicoUsd.ToUsd(reader.GetInt64(reader.GetOrdinal("saved_usd_pico"))),
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

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
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
```

New file `tests/CopilotLedger.Tests/Storage/CacheEfficiencyTests.cs`:

```csharp
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

public sealed class CacheEfficiencyTests
{
    private static readonly JsonRateCardCalculator Calculator = JsonRateCardCalculator.LoadEmbedded();

    public static TheoryData<int, decimal> HitRatios => new()
    {
        { 0, 1m },
        { 1, 0m },
        { 2, 0.25m },
        { 3, 0.1666666666666666666666666667m },
        { 4, 0.4929331000134607618791223583m },
    };

    [Theory]
    [MemberData(nameof(HitRatios))]
    public async Task CE1_HitRatio_UsesOnlyTheSpansThatReportedCacheUsage(int caseNumber, decimal expected)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        LedgerEntry[] entries = caseNumber switch
        {
            // All input read from the cache.
            0 => [Entry("1", input: 1_000, cacheRead: 1_000)],

            // True misses: a cache-read counter of 0, and a span that reported only a cache write.
            1 =>
            [
                Entry("1", input: 1_000, readState: CacheCounterState.Reported),
                Entry("2", input: 500, cacheWrite: 500),
            ],

            // A span without cache counters is left out, not counted as a miss.
            2 => [Entry("1", input: 1_000, cacheRead: 250), Entry("2", input: 3_000)],
            3 =>
            [
                Entry("1", input: 2_000, cacheRead: 500),
                Entry("2", input: 1_000, cacheWrite: 400),
                Entry("3", input: 5_000),
            ],

            // Copilot CLI's explore subagent: its first call wrote the cache and left out cache_read.
            4 => [Entry("1", input: 3_665, cacheWrite: 3_662), Entry("2", input: 3_764, cacheRead: 3_662, cacheWrite: 99)],
            _ => throw new ArgumentOutOfRangeException(nameof(caseNumber)),
        };
        await store.RecordAsync(entries);

        CacheEfficiencyRow row = Assert.Single(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, LedgerFilter.All));

        Assert.Equal(expected, row.CacheHitRatio);
    }

    [Fact]
    public async Task CE2_HitRatio_IsNullWithoutReportedInput()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        Assert.Empty(await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, LedgerFilter.All));

        await store.RecordAsync(
        [
            Entry("1", model: "qwen3.8-27b-cuda-gpu:1", input: 1_000),
            Entry("2", model: "qwen3.8-27b-cuda-gpu:1", input: 3_000),
            Entry("3", model: "mai-code-2", input: 0, readState: CacheCounterState.Reported),
        ]);

        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, LedgerFilter.All),
            row =>
            {
                Assert.Equal("qwen3.8-27b", row.Model);
                Assert.Equal(2, row.SpanCount);
                Assert.Equal(0, row.CacheReportedSpanCount);
                Assert.Equal(2, row.CacheUnreportedSpanCount);
                Assert.Null(row.CacheHitRatio);
                Assert.Null(row.UncachedInputTokens);
                Assert.Equal(0, row.ReportedInputTokens);
                Assert.Equal(4_000, row.EstimatedUncachedInputTokens);
            },
            row =>
            {
                Assert.Equal("mai-code-2", row.Model);
                Assert.Equal(1, row.SpanCount);
                Assert.Equal(1, row.CacheReportedSpanCount);
                Assert.Null(row.CacheHitRatio);
                Assert.Equal(0, row.UncachedInputTokens);
            });
    }

    [Fact]
    public async Task CE3_Row_SeparatesObservedAndEstimatedTotals()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(
        [
            Entry("1", input: 1_000, output: 40, cacheRead: 600, cacheWrite: 100),
            Entry("2", input: 500, output: 20),
            Entry("3", input: 300, output: 10, cacheRead: 300),
        ]);

        CacheEfficiencyRow row = Assert.Single(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, LedgerFilter.All));

        Assert.Equal("mai-code-2", row.Model);
        Assert.Null(row.Repository);
        Assert.Null(row.Branch);
        Assert.Null(row.PullRequestNumber);
        Assert.Null(row.Agent);
        Assert.Equal(3, row.SpanCount);
        Assert.Equal(2, row.CacheReportedSpanCount);
        Assert.Equal(1, row.CacheUnreportedSpanCount);
        Assert.Equal(1_800, row.InputTokens);
        Assert.Equal(70, row.OutputTokens);
        Assert.Equal(1_000, row.MaxInputTokens);
        Assert.Equal(1_300, row.ReportedInputTokens);
        Assert.Equal(900, row.ReportedCacheReadInputTokens);
        Assert.Equal(100, row.ReportedCacheWriteInputTokens);
        Assert.Equal(300, row.ReportedUncachedInputTokens);
        Assert.Equal(300, row.UncachedInputTokens);
        Assert.Equal(800, row.EstimatedUncachedInputTokens);
        Assert.Equal(600m, row.AverageInputTokensPerTurn);
        Assert.Equal(0.6923076923076923076923076923m, row.CacheHitRatio);
    }

    [Fact]
    public async Task CE4_Groupings_KeyRowsAndOrderThemByInput()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        GitWorkspaceContext feature = GitFixture.Feature(temp.Path) with { PullRequestNumber = 42 };
        GitWorkspaceContext main = GitFixture.Feature(temp.Path) with { BranchName = "main" };
        GitWorkspaceContext fix = GitFixture.Feature(temp.Path) with { BranchName = "fix/login" };
        await store.RecordAsync(
        [
            Entry("1", model: "claude-opus-5.5", agent: "explore", git: feature, input: 4_000, cacheRead: 3_000),
            Entry("2", model: "mai-code-2", git: feature, input: 2_000, cacheRead: 500),
            Entry("3", model: "mai-code-2", agent: "explore", git: main, input: 1_000),
            Entry("4", model: "claude-opus-5.5", git: fix, input: 1_000, cacheRead: 1_000),
            Entry("5", model: "mai-code-2", input: 500, cacheRead: 100),
        ]);

        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, LedgerFilter.All),
            row => AssertRow(row, null, null, null, "claude-opus-5.5", null, 2, 2, 5_000, 0.8m),
            row => AssertRow(row, null, null, null, "mai-code-2", null, 3, 2, 3_500, 0.24m));
        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Agent, LedgerFilter.All),
            row => AssertRow(row, null, null, null, null, "explore", 2, 1, 5_000, 0.75m),
            row => AssertRow(row, null, null, null, null, "(none)", 3, 3, 3_500, 0.4571428571428571428571428571m));
        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Branch, LedgerFilter.All),
            row => AssertRow(row, "swigerb/copilot-ledger", "feature/ledger", null, null, null, 2, 2, 6_000, 0.5833333333333333333333333333m),
            row => AssertRow(row, "swigerb/copilot-ledger", "fix/login", null, null, null, 1, 1, 1_000, 1m),
            row => AssertRow(row, "swigerb/copilot-ledger", "main", null, null, null, 1, 0, 1_000, null),
            row => AssertRow(row, "(none)", "(none)", null, null, null, 1, 1, 500, 0.2m));
        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.PullRequest, LedgerFilter.All),
            row => AssertRow(row, "swigerb/copilot-ledger", null, 42, null, null, 2, 2, 6_000, 0.5833333333333333333333333333m),
            row => AssertRow(row, "swigerb/copilot-ledger", "fix/login", null, null, null, 1, 1, 1_000, 1m),
            row => AssertRow(row, "swigerb/copilot-ledger", "main", null, null, null, 1, 0, 1_000, null),
            row => AssertRow(row, "(none)", "(none)", null, null, null, 1, 1, 500, 0.2m));
        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Repository, LedgerFilter.All),
            row => AssertRow(row, "swigerb/copilot-ledger", null, null, null, null, 4, 3, 8_000, 0.6428571428571428571428571429m),
            row => AssertRow(row, "(none)", null, null, null, null, 1, 1, 500, 0.2m));
        Assert.Collection(
            await store.GetCacheEfficiencyAsync(
                LedgerGrouping.Model,
                new LedgerFilter { PullRequestNumber = 42, Agent = "(none)" }),
            row => AssertRow(row, null, null, null, "mai-code-2", null, 1, 1, 2_000, 0.25m));
    }

    [Fact]
    public async Task CE5_UnsupportedGroupings_Throw()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.GetCacheEfficiencyAsync(LedgerGrouping.ChannelModel, LedgerFilter.All));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.GetRollupAsync(LedgerGrouping.PullRequest, LedgerFilter.All));
    }

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        return store;
    }

    // A chat span in trace 0e0e...0e<id>; a nonzero cache counter is Reported and a zero one is Absent unless a state is given.
    private static LedgerEntry Entry(
        string id,
        string model = "mai-code-2",
        string? agent = null,
        GitWorkspaceContext? git = null,
        long input = 0,
        long output = 0,
        long cacheRead = 0,
        long cacheWrite = 0,
        CacheCounterState? readState = null)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: model,
            agentName: agent,
            inputTokens: input,
            outputTokens: output,
            cacheReadInputTokens: cacheRead,
            cacheWriteInputTokens: cacheWrite,
            cacheReadState: readState) with
        {
            TraceId = "0e0e0e0e0e0e0e0e0e0e0e0e0e0e0e" + id.PadLeft(2, '0'),
            SpanId = "0e0e0e0e0e0e0e" + id.PadLeft(2, '0'),
        };
        return new LedgerEntry(span, Calculator.Calculate(span), git);
    }

    private static void AssertRow(
        CacheEfficiencyRow row,
        string? repository,
        string? branch,
        int? pullRequest,
        string? model,
        string? agent,
        long spans,
        long reported,
        long input,
        decimal? hitRatio)
    {
        Assert.Equal(repository, row.Repository);
        Assert.Equal(branch, row.Branch);
        Assert.Equal(pullRequest, row.PullRequestNumber);
        Assert.Equal(model, row.Model);
        Assert.Equal(agent, row.Agent);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(reported, row.CacheReportedSpanCount);
        Assert.Equal(input, row.InputTokens);
        Assert.Equal(hitRatio, row.CacheHitRatio);
    }
}
```

### Tests

- **CE1** (5 `TheoryData<int, decimal>` cases, one row each): all input read → `1`; a reported 0 cache read plus a span that reported only a 500-token write → `0`; 250 of 1,000 with an unreported 3,000-token span → `0.25`; 500 of 3,000 reported input with an unreported span → `0.1666666666666666666666666667`; Copilot CLI's `explore` pair (3,665 with a 3,662 write, then 3,764 with a 3,662 read and a 99 write) → `0.4929331000134607618791223583`.
- **CE2:** an empty ledger has no rows; two `qwen3.8-27b` spans without counters have 0 reported spans, `null` ratio, `null` observed uncached, and an estimate of 4,000; a `mai-code-2` span with a reported 0 and no input has a `null` ratio and observed uncached 0.
- **CE3:** one row separates observed and estimated totals: 3 spans, 2 reported, input 1,800, output 70, max 1,000, reported input 1,300, reads 900, writes 100, observed uncached 300, estimate 800, average `600`, ratio `0.6923076923076923076923076923`.
- **CE4:** the five groupings key and order rows (for example, by pull request: PR 42 with 6,000 input and `0.5833333333333333333333333333`, then `fix/login` and `main` without one, tied at 1,000 and ordered by branch, then `(none)`), and the new filters combine.
- **CE5:** `GetCacheEfficiencyAsync(ChannelModel, …)` and `GetRollupAsync(PullRequest, …)` throw `ArgumentOutOfRangeException`.

### Acceptance criteria

- Ratios are exact `decimal` from integer sums, `null` when no span reported cache usage or reported input is 0.
- `dotnet test CopilotLedger.slnx` passes **743** tests (734 + 9).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 6 specification: Turn Breakdown and Context-Bloat Queries

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/TurnSelector.cs` | New |
| `src/CopilotLedger.Storage/TurnTokenRow.cs` | New |
| `src/CopilotLedger.Storage/BloatChainRow.cs` | New |
| `src/CopilotLedger.Storage/InputGrowthRow.cs` | New |
| `src/CopilotLedger.Storage/ContextBloatReport.cs` | New |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | `SequenceSql`; new `GetTurnBreakdownAsync`, `GetContextBloatAsync`, `ReadBloatChainsAsync`, and `ToCacheCounterState` |
| `tests/CopilotLedger.Tests/TestSupport/TurnSeeds.cs` | New: four prompt chains with span links |
| `tests/CopilotLedger.Tests/Storage/TurnBreakdownTests.cs` | New: TB1–TB4, CB1, CB2 |

### Contracts

```csharp
public sealed record TurnSelector            // exactly one of TraceId and ConversationId is set
{
    public string? TraceId { get; }          // 32 lowercase hexadecimal characters
    public string? ConversationId { get; }
    public static TurnSelector ForTrace(string traceId);              // ArgumentNullException; ArgumentException unless 32 hex characters; lowercases
    public static TurnSelector ForConversation(string conversationId);  // ArgumentNullException; ArgumentException when blank
}

public sealed record TurnTokenRow
{
    // TraceId, SpanId, ParentSpanId?, ConversationId?, AgentSpanId?, StartTime, Repository, Branch, PullRequestNumber?,
    // Agent?, Model, InputTokens, OutputTokens, CacheReadState, CacheWriteState,
    // CacheReadInputTokens?, CacheWriteInputTokens?   — null unless CacheMetrics.IsReported for the span
    // InputDeltaFromPreviousTurn?                     — null for a sequence's first span and when AgentSpanId is null
    public long? UncachedInputTokens { get; }        // max(input − read − write, 0), or null
    public decimal? CacheHitRatio { get; }           // CacheMetrics.HitRatio(read, input), or null
}

public sealed record BloatChainRow     // one trace: TraceId, ConversationId?, StartTime (earliest), Repository, Branch, SpanCount,
                                       // CacheReportedSpanCount, InputTokens, ReportedInputTokens, ReportedCacheReadInputTokens,
                                       // ReportedUncachedInputTokens, EstimatedUncachedInputTokens; CacheHitRatio
public sealed record InputGrowthRow    // a trace's best sequence: TraceId, ConversationId?, AgentSpanId, Agent?, Model, Repository,
                                       // Branch, StartTime, TurnCount, FirstInputTokens, PeakInputTokens; InputGrowthTokens
public sealed record ContextBloatReport
{
    public required IReadOnlyList<BloatChainRow> ByUncachedInput { get; init; }
    public required IReadOnlyList<BloatChainRow> ByEstimatedUncachedInput { get; init; }
    public required IReadOnlyList<InputGrowthRow> ByInputGrowth { get; init; }
    public required long SpansWithoutAgent { get; init; }
}

public Task<IReadOnlyList<TurnTokenRow>> GetTurnBreakdownAsync(
    TurnSelector selector, LedgerFilter filter, CancellationToken cancellationToken = default);   // ArgumentNullException
public Task<ContextBloatReport> GetContextBloatAsync(
    LedgerFilter filter, int limit, CancellationToken cancellationToken = default);   // ArgumentOutOfRangeException unless 1 ≤ limit ≤ 1000
```

### Algorithms

A *sequence* is `SequenceSql`: `trace_id, agent_span_id, COALESCE(repo_slug, repo_root, '(none)'), COALESCE(branch_name, '(none)'), COALESCE(conversation_id, '(none)'), COALESCE(model_id, '(none)')`, ordered by `start_time_unix_ms, span_id`.

`GetTurnBreakdownAsync`:

1. Select the spans of `selector` (trace or conversation) in a CTE, adding `input_delta`: `NULL` when `agent_span_id` is null, otherwise `input_tokens - LAG(input_tokens) OVER (PARTITION BY {SequenceSql} ORDER BY start_time_unix_ms, span_id)`.
2. Apply `FilterSql` to the CTE's rows, so filtering never changes a delta.
3. Order by `start_time_unix_ms, span_id, trace_id`.
4. Map states with `ToCacheCounterState`; when the span isn't `IsReported`, the cache counters are `null`.

`GetContextBloatAsync`:

1. `ByUncachedInput`: traces matching the filter whose reported span count equals their span count, ordered by reported uncached input descending, then trace ID, up to `limit`.
2. `ByEstimatedUncachedInput`: traces with fewer reported spans than spans, ordered by estimated uncached input descending, then trace ID.
3. `ByInputGrowth`: over the filtered spans with an `agent_span_id`, each sequence's first input (`FIRST_VALUE`), peak input (`MAX`), request count, and first start; per trace, the sequence with the largest `peak − first`, ties going to the lower agent span ID, then model, repository, branch, and conversation; only growth above 0; ordered by growth descending, then trace ID.
4. `SpansWithoutAgent`: the filtered spans whose `agent_span_id` is null.

### Test data: `TurnSeeds`

Four prompt chains on 2026-11-21 (UTC), recorded with span links and then back-filled:

| Trace | Span | Start | Agent (invoke_agent span) | Model | Input | Cache read | Cache write |
|---|---|---|---|---|---:|---:|---:|
| B1 (`conv-b`, PR 42) | `c100000000000001` | 10:00:00 | default (`a100000000000001`) | claude-opus-5.5 | 10,000 | — | 9,000 |
| | `5100000000000001` | 10:00:05 | explore (`a100000000000002`) | mai-code-2 | 3,000 | — | — |
| | `5100000000000002` | 10:00:10 | explore | mai-code-2 | 5,000 | 2,000 | — |
| | `c100000000000002` | 10:00:10 | default | claude-opus-5.5 | 12,000 | 9,000 | 2,500 |
| | `c100000000000003` | 10:00:20 | default | claude-opus-5.5 | 11,000 | 11,000 | — |
| | `0100000000000001` | 10:00:30 | unknown (parent `f100000000000009` never recorded) | claude-opus-5.5 | 20,000 | — | — |
| B2 (`conv-b`, PR 42) | `d200000000000001` | 10:05:00 | default (`a200000000000001`) | claude-opus-5.5 | 15,000 | 10,000 | — |
| B3 (`conv-c`, no PR) | `e300000000000001` | 11:00:00 | default (`a300000000000001`) | claude-opus-5.5 | 20,000 | 5,000 | — |
| | `e300000000000002` | 11:01:00 | default | claude-opus-5.5 | 30,000 | 5,000 | — |
| B0 (no conversation or Git) | `f000000000000001` | 12:00:00 | unknown (no links) | mai-code-2 | 5,000 | reported 0 | — |

"—" is an absent counter. B1's explore agent's `invoke_agent` span is a child of an `execute_tool` span under the default agent, as in Copilot CLI, and its second request starts in the same millisecond as the default agent's second.

### Code (copy exactly)

New file `src/CopilotLedger.Storage/BloatChainRow.cs`:

```csharp
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

// One prompt chain (one trace) ranked by uncached input in a ContextBloatReport. Reported* sums cover only the spans that
// reported cache usage (CacheMetrics.IsReported).
public sealed record BloatChainRow
{
    public required string TraceId { get; init; }
    public string? ConversationId { get; init; }
    public required DateTimeOffset StartTime { get; init; }             // earliest span start
    public required string Repository { get; init; }                    // same key as the Repository rollup
    public required string Branch { get; init; }                        // same key as the Branch rollup
    public required long SpanCount { get; init; }
    public required long CacheReportedSpanCount { get; init; }
    public required long InputTokens { get; init; }
    public required long ReportedInputTokens { get; init; }
    public required long ReportedCacheReadInputTokens { get; init; }
    public required long ReportedUncachedInputTokens { get; init; }     // input less cache reads and writes, at least 0 a span
    public required long EstimatedUncachedInputTokens { get; init; }    // the same over all spans, as pricing counts them
    public decimal? CacheHitRatio => CacheMetrics.HitRatio(ReportedCacheReadInputTokens, ReportedInputTokens);
}
```

New file `src/CopilotLedger.Storage/ContextBloatReport.cs`:

```csharp
namespace CopilotLedger.Storage;

// GetContextBloatAsync's rankings. Each list holds at most the limit given, and ties go to the lower trace ID.
public sealed record ContextBloatReport
{
    public required IReadOnlyList<BloatChainRow> ByUncachedInput { get; init; }            // chains whose spans all reported cache usage, by ReportedUncachedInputTokens
    public required IReadOnlyList<BloatChainRow> ByEstimatedUncachedInput { get; init; }   // the other chains, by EstimatedUncachedInputTokens
    public required IReadOnlyList<InputGrowthRow> ByInputGrowth { get; init; }             // each chain's largest sequence growth above 0
    public required long SpansWithoutAgent { get; init; }                                  // spans left out of growth: no known invoke_agent ancestor
}
```

New file `src/CopilotLedger.Storage/InputGrowthRow.cs`:

```csharp
namespace CopilotLedger.Storage;

// A prompt chain's sequence with the most input growth: one agent invocation's requests to one model in one repository,
// branch, and conversation.
public sealed record InputGrowthRow
{
    public required string TraceId { get; init; }
    public string? ConversationId { get; init; }
    public required string AgentSpanId { get; init; }     // the invoke_agent span of the sequence
    public string? Agent { get; init; }                   // gen_ai.agent.name; null when the agent has none
    public required string Model { get; init; }           // model id, or (none)
    public required string Repository { get; init; }
    public required string Branch { get; init; }
    public required DateTimeOffset StartTime { get; init; }   // the sequence's first request
    public required long TurnCount { get; init; }             // requests in the sequence
    public required long FirstInputTokens { get; init; }
    public required long PeakInputTokens { get; init; }
    public long InputGrowthTokens => PeakInputTokens - FirstInputTokens;
}
```

Edit 1 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
    // A span's input tokens that were neither read from nor written to the cache, never below 0.
    private const string UncachedSql = "MAX(input_tokens - cache_read_input_tokens - cache_write_input_tokens, 0)";

    private readonly TimeProvider _timeProvider;
```

with:

```csharp
    // A span's input tokens that were neither read from nor written to the cache, never below 0.
    private const string UncachedSql = "MAX(input_tokens - cache_read_input_tokens - cache_write_input_tokens, 0)";

    // A growth sequence: one agent invocation's requests to one model in one repository, branch, and conversation.
    private const string SequenceSql = """
        trace_id, agent_span_id, COALESCE(repo_slug, repo_root, '(none)'), COALESCE(branch_name, '(none)'),
                                 COALESCE(conversation_id, '(none)'), COALESCE(model_id, '(none)')
        """;

    private readonly TimeProvider _timeProvider;
```

Edit 2 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new(_connectionString);
```

with:

```csharp
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
        await using SqliteCommand command = connection.CreateCommand();
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

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new(_connectionString);
```

Edit 3 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
                SpentAiCredits =
                    PicoUsd.ToUsd(reader.GetInt64(6)) / AiCreditMath.UsdPerAiCredit,
            });
        }

        return rows;
```

with:

```csharp
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
```

Edit 4 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static object OrDbNull<T>(T? value) where T : struct =>
        value.HasValue ? value.Value : DBNull.Value;
```

with:

```csharp
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static CacheCounterState ToCacheCounterState(string value) => value switch
    {
        "reported" => CacheCounterState.Reported,
        "absent" => CacheCounterState.Absent,
        "legacy_unknown" => CacheCounterState.LegacyUnknown,
        _ => throw new InvalidDataException($"Unknown cache counter state '{value}'."),
    };

    private static object OrDbNull<T>(T? value) where T : struct =>
        value.HasValue ? value.Value : DBNull.Value;
```

New file `src/CopilotLedger.Storage/TurnSelector.cs`:

```csharp
namespace CopilotLedger.Storage;

// The inference spans of one trace, or of one conversation, for GetTurnBreakdownAsync.
public sealed record TurnSelector
{
    private TurnSelector(string? traceId, string? conversationId)
    {
        TraceId = traceId;
        ConversationId = conversationId;
    }

    public string? TraceId { get; }          // 32 lowercase hexadecimal characters; null when ConversationId is set
    public string? ConversationId { get; }   // null when TraceId is set

    // Throws ArgumentNullException for null, and ArgumentException unless traceId is 32 hexadecimal characters.
    // Matches the lowercase trace ID that the ledger stores.
    public static TurnSelector ForTrace(string traceId)
    {
        ArgumentNullException.ThrowIfNull(traceId);
        if (traceId.Length != 32 || !traceId.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("A trace ID is 32 hexadecimal characters.", nameof(traceId));
        }

        return new TurnSelector(traceId.ToLowerInvariant(), null);
    }

    // Throws ArgumentNullException for null, and ArgumentException for a blank conversationId.
    public static TurnSelector ForConversation(string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        return new TurnSelector(null, conversationId);
    }
}
```

New file `src/CopilotLedger.Storage/TurnTokenRow.cs`:

```csharp
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

// One inference span, that is one model request, in a turn breakdown. The cache properties are null when the span
// reported no cache usage (CacheMetrics.IsReported); on a span that did, a counter it left out is 0.
public sealed record TurnTokenRow
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? ConversationId { get; init; }
    public string? AgentSpanId { get; init; }                  // the nearest invoke_agent ancestor in the trace; null when unknown
    public required DateTimeOffset StartTime { get; init; }
    public required string Repository { get; init; }           // same key as the Repository rollup
    public required string Branch { get; init; }               // same key as the Branch rollup
    public int? PullRequestNumber { get; init; }
    public string? Agent { get; init; }                        // gen_ai.agent.name; null when the span has none
    public required string Model { get; init; }                // model id, or (none)
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required CacheCounterState CacheReadState { get; init; }
    public required CacheCounterState CacheWriteState { get; init; }
    public long? CacheReadInputTokens { get; init; }
    public long? CacheWriteInputTokens { get; init; }

    // InputTokens less the previous span's in the same sequence; null for a sequence's first span and when AgentSpanId is null.
    public long? InputDeltaFromPreviousTurn { get; init; }

    public long? UncachedInputTokens => CacheReadInputTokens is null
        ? null
        : Math.Max(InputTokens - CacheReadInputTokens.Value - CacheWriteInputTokens.GetValueOrDefault(), 0);

    public decimal? CacheHitRatio => CacheReadInputTokens is null
        ? null
        : CacheMetrics.HitRatio(CacheReadInputTokens.Value, InputTokens);
}
```

New file `tests/CopilotLedger.Tests/Storage/TurnBreakdownTests.cs`:

```csharp
using System.Globalization;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

public sealed class TurnBreakdownTests
{
    private const string Root = "a100000000000001";
    private const string Explore = "a100000000000002";

    [Fact]
    public async Task TB1_Trace_ListsRequestsInStartOrderWithDeltasPerSequence()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        IReadOnlyList<TurnTokenRow> rows = await store.GetTurnBreakdownAsync(
            TurnSelector.ForTrace(TurnSeeds.B1),
            LedgerFilter.All);

        Assert.Collection(
            rows,
            row => AssertTurn(row, "c100000000000001", "10:00:00", Root, null, "claude-opus-5.5", 10_000, 500, CacheCounterState.Absent, CacheCounterState.Reported, 0, 9_000, 1_000, 0m, null),
            row => AssertTurn(row, "5100000000000001", "10:00:05", Explore, "explore", "mai-code-2", 3_000, 100, CacheCounterState.Absent, CacheCounterState.Absent, null, null, null, null, null),
            row => AssertTurn(row, "5100000000000002", "10:00:10", Explore, "explore", "mai-code-2", 5_000, 100, CacheCounterState.Reported, CacheCounterState.Absent, 2_000, 0, 3_000, 0.4m, 2_000),
            row => AssertTurn(row, "c100000000000002", "10:00:10", Root, null, "claude-opus-5.5", 12_000, 500, CacheCounterState.Reported, CacheCounterState.Reported, 9_000, 2_500, 500, 0.75m, 2_000),
            row => AssertTurn(row, "c100000000000003", "10:00:20", Root, null, "claude-opus-5.5", 11_000, 500, CacheCounterState.Reported, CacheCounterState.Absent, 11_000, 0, 0, 1m, -1_000),
            row => AssertTurn(row, "0100000000000001", "10:00:30", null, null, "claude-opus-5.5", 20_000, 500, CacheCounterState.Absent, CacheCounterState.Absent, null, null, null, null, null));
        TurnTokenRow first = rows[0];
        Assert.Equal(TurnSeeds.B1, first.TraceId);
        Assert.Equal(Root, first.ParentSpanId);
        Assert.Equal("conv-b", first.ConversationId);
        Assert.Equal("swigerb/copilot-ledger", first.Repository);
        Assert.Equal("feature/ledger", first.Branch);
        Assert.Equal(42, first.PullRequestNumber);
        Assert.Equal("f100000000000009", rows[5].ParentSpanId);
    }

    [Fact]
    public async Task TB2_Conversation_KeepsEachPromptsAgentSeparate()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        IReadOnlyList<TurnTokenRow> rows = await store.GetTurnBreakdownAsync(
            TurnSelector.ForConversation("conv-b"),
            LedgerFilter.All);

        Assert.Equal(
            ["c100000000000001", "5100000000000001", "5100000000000002", "c100000000000002", "c100000000000003", "0100000000000001", "d200000000000001"],
            rows.Select(row => row.SpanId));
        TurnTokenRow next = rows[6];
        AssertTurn(next, "d200000000000001", "10:05:00", "a200000000000001", null, "claude-opus-5.5", 15_000, 500, CacheCounterState.Reported, CacheCounterState.Absent, 10_000, 0, 5_000, 0.6666666666666666666666666667m, null);
        Assert.Equal(TurnSeeds.B2, next.TraceId);
        Assert.Empty(await store.GetTurnBreakdownAsync(TurnSelector.ForConversation("conv-none"), LedgerFilter.All));
    }

    [Fact]
    public async Task TB3_Filter_AppliesAfterDeltasAreComputed()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        IReadOnlyList<TurnTokenRow> explore = await store.GetTurnBreakdownAsync(
            TurnSelector.ForTrace(TurnSeeds.B1.ToUpperInvariant()),
            new LedgerFilter { Agent = "explore" });
        IReadOnlyList<TurnTokenRow> later = await store.GetTurnBreakdownAsync(
            TurnSelector.ForTrace(TurnSeeds.B1),
            new LedgerFilter { From = DateTimeOffset.Parse("2026-11-21T10:00:10Z", CultureInfo.InvariantCulture) });

        Assert.Equal([null, 2_000], explore.Select(row => row.InputDeltaFromPreviousTurn));
        Assert.Equal(
            ["5100000000000002", "c100000000000002", "c100000000000003", "0100000000000001"],
            later.Select(row => row.SpanId));
        Assert.Equal([2_000, 2_000, -1_000, null], later.Select(row => row.InputDeltaFromPreviousTurn));
    }

    [Fact]
    public void TB4_Selectors_ValidateTheirIds()
    {
        Assert.Equal(TurnSeeds.B1, TurnSelector.ForTrace(TurnSeeds.B1.ToUpperInvariant()).TraceId);
        Assert.Null(TurnSelector.ForTrace(TurnSeeds.B1).ConversationId);
        Assert.Equal("conv-b", TurnSelector.ForConversation("conv-b").ConversationId);
        Assert.Null(TurnSelector.ForConversation("conv-b").TraceId);
        Assert.Throws<ArgumentException>(() => TurnSelector.ForTrace("b1"));
        Assert.Throws<ArgumentException>(() => TurnSelector.ForTrace(new string('g', 32)));
        Assert.Throws<ArgumentNullException>(() => TurnSelector.ForTrace(null!));
        Assert.Throws<ArgumentException>(() => TurnSelector.ForConversation(" "));
        Assert.Throws<ArgumentNullException>(() => TurnSelector.ForConversation(null!));
    }

    [Fact]
    public async Task CB1_Bloat_RanksChainsByUncachedInputAndGrowth()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        ContextBloatReport report = await store.GetContextBloatAsync(LedgerFilter.All, 10);

        Assert.Collection(
            report.ByUncachedInput,
            row => AssertChain(row, TurnSeeds.B3, "11:00:00", 2, 2, 50_000, 40_000, 40_000, 0.2m),
            row => AssertChain(row, TurnSeeds.B0, "12:00:00", 1, 1, 5_000, 5_000, 5_000, 0m),
            row => AssertChain(row, TurnSeeds.B2, "10:05:00", 1, 1, 15_000, 5_000, 5_000, 0.6666666666666666666666666667m));
        BloatChainRow incomplete = Assert.Single(report.ByEstimatedUncachedInput);
        AssertChain(incomplete, TurnSeeds.B1, "10:00:00", 6, 4, 61_000, 4_500, 27_500, 0.5789473684210526315789473684m);
        Assert.Equal("conv-b", incomplete.ConversationId);
        Assert.Equal("swigerb/copilot-ledger", incomplete.Repository);
        Assert.Equal("feature/ledger", incomplete.Branch);
        Assert.Equal(38_000, incomplete.ReportedInputTokens);
        Assert.Equal(22_000, incomplete.ReportedCacheReadInputTokens);
        Assert.Collection(
            report.ByInputGrowth,
            row => AssertGrowth(row, TurnSeeds.B3, "a300000000000001", null, "11:00:00", 2, 20_000, 30_000, 10_000),
            row => AssertGrowth(row, TurnSeeds.B1, Root, null, "10:00:00", 3, 10_000, 12_000, 2_000));
        Assert.Equal("conv-c", report.ByInputGrowth[0].ConversationId);
        Assert.Equal("claude-opus-5.5", report.ByInputGrowth[1].Model);
        Assert.Equal("swigerb/copilot-ledger", report.ByInputGrowth[1].Repository);
        Assert.Equal("feature/ledger", report.ByInputGrowth[1].Branch);
        Assert.Equal(2, report.SpansWithoutAgent);
    }

    [Fact]
    public async Task CB2_Bloat_HonorsLimitAndFilter()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        ContextBloatReport one = await store.GetContextBloatAsync(LedgerFilter.All, 1);
        ContextBloatReport explore = await store.GetContextBloatAsync(new LedgerFilter { Agent = "explore" }, 10);

        Assert.Equal([TurnSeeds.B3], one.ByUncachedInput.Select(row => row.TraceId));
        Assert.Equal([TurnSeeds.B1], one.ByEstimatedUncachedInput.Select(row => row.TraceId));
        Assert.Equal([TurnSeeds.B3], one.ByInputGrowth.Select(row => row.TraceId));
        Assert.Empty(explore.ByUncachedInput);
        AssertChain(Assert.Single(explore.ByEstimatedUncachedInput), TurnSeeds.B1, "10:00:05", 2, 1, 8_000, 3_000, 6_000, 0.4m);
        AssertGrowth(Assert.Single(explore.ByInputGrowth), TurnSeeds.B1, Explore, "explore", "10:00:05", 2, 3_000, 5_000, 2_000);
        Assert.Equal(0, explore.SpansWithoutAgent);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetContextBloatAsync(LedgerFilter.All, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetContextBloatAsync(LedgerFilter.All, 1001));
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        await TurnSeeds.SeedAsync(store, temp.Path);
        return store;
    }

    private static void AssertTurn(
        TurnTokenRow row,
        string spanId,
        string start,
        string? agentSpanId,
        string? agent,
        string model,
        long input,
        long output,
        CacheCounterState readState,
        CacheCounterState writeState,
        long? cacheRead,
        long? cacheWrite,
        long? uncached,
        decimal? hitRatio,
        long? delta)
    {
        Assert.Equal(spanId, row.SpanId);
        Assert.Equal(DateTimeOffset.Parse($"2026-11-21T{start}Z", CultureInfo.InvariantCulture), row.StartTime);
        Assert.Equal(agentSpanId, row.AgentSpanId);
        Assert.Equal(agent, row.Agent);
        Assert.Equal(model, row.Model);
        Assert.Equal(input, row.InputTokens);
        Assert.Equal(output, row.OutputTokens);
        Assert.Equal(readState, row.CacheReadState);
        Assert.Equal(writeState, row.CacheWriteState);
        Assert.Equal(cacheRead, row.CacheReadInputTokens);
        Assert.Equal(cacheWrite, row.CacheWriteInputTokens);
        Assert.Equal(uncached, row.UncachedInputTokens);
        Assert.Equal(hitRatio, row.CacheHitRatio);
        Assert.Equal(delta, row.InputDeltaFromPreviousTurn);
    }

    private static void AssertChain(
        BloatChainRow row,
        string traceId,
        string start,
        long spans,
        long reported,
        long input,
        long reportedUncached,
        long estimatedUncached,
        decimal? hitRatio)
    {
        Assert.Equal(traceId, row.TraceId);
        Assert.Equal(DateTimeOffset.Parse($"2026-11-21T{start}Z", CultureInfo.InvariantCulture), row.StartTime);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(reported, row.CacheReportedSpanCount);
        Assert.Equal(input, row.InputTokens);
        Assert.Equal(reportedUncached, row.ReportedUncachedInputTokens);
        Assert.Equal(estimatedUncached, row.EstimatedUncachedInputTokens);
        Assert.Equal(hitRatio, row.CacheHitRatio);
    }

    private static void AssertGrowth(
        InputGrowthRow row,
        string traceId,
        string agentSpanId,
        string? agent,
        string start,
        long turns,
        long first,
        long peak,
        long growth)
    {
        Assert.Equal(traceId, row.TraceId);
        Assert.Equal(agentSpanId, row.AgentSpanId);
        Assert.Equal(agent, row.Agent);
        Assert.Equal(DateTimeOffset.Parse($"2026-11-21T{start}Z", CultureInfo.InvariantCulture), row.StartTime);
        Assert.Equal(turns, row.TurnCount);
        Assert.Equal(first, row.FirstInputTokens);
        Assert.Equal(peak, row.PeakInputTokens);
        Assert.Equal(growth, row.InputGrowthTokens);
    }
}
```

New file `tests/CopilotLedger.Tests/TestSupport/TurnSeeds.cs`:

```csharp
using System.Globalization;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.TestSupport;

// Four prompt chains on 2026-11-21 (UTC) for turn and context-bloat tests, with their span links back-filled.
// B1 (conversation conv-b, swigerb/copilot-ledger feature/ledger, pull request 42): the default agent's claude-opus-5.5
// requests c1, c2, and c3, and an explore subagent's mai-code-2 requests s1 and s2, interleaved with them; s2 starts at the
// same millisecond as c2. o1's parent was never recorded. B2 (conv-b, pull request 42): one request after B1. B3 (conv-c,
// feature/ledger): two growing requests. B0 (no conversation or Git): one request without span links.
public static class TurnSeeds
{
    public const string B0 = "b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0";
    public const string B1 = "b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1";
    public const string B2 = "b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2";
    public const string B3 = "b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3";

    public static async Task SeedAsync(SqliteLedgerStore store, string root)
    {
        JsonRateCardCalculator rates = JsonRateCardCalculator.LoadEmbedded();
        GitWorkspaceContext pullRequest = GitFixture.Feature(root) with { PullRequestNumber = 42 };
        GitWorkspaceContext feature = GitFixture.Feature(root);
        (NormalizedGenAiSpan Span, GitWorkspaceContext? Git)[] spans =
        [
            (Chat(B1, "c100000000000001", "a100000000000001", "10:00:00", "claude-opus-5.5", 10_000, cacheWrite: 9_000), pullRequest),
            (Chat(B1, "5100000000000001", "a100000000000002", "10:00:05", "mai-code-2", 3_000), pullRequest),
            (Chat(B1, "5100000000000002", "a100000000000002", "10:00:10", "mai-code-2", 5_000, cacheRead: 2_000), pullRequest),
            (Chat(B1, "c100000000000002", "a100000000000001", "10:00:10", "claude-opus-5.5", 12_000, cacheRead: 9_000, cacheWrite: 2_500), pullRequest),
            (Chat(B1, "c100000000000003", "a100000000000001", "10:00:20", "claude-opus-5.5", 11_000, cacheRead: 11_000), pullRequest),
            (Chat(B1, "0100000000000001", "f100000000000009", "10:00:30", "claude-opus-5.5", 20_000), pullRequest),
            (Chat(B2, "d200000000000001", "a200000000000001", "10:05:00", "claude-opus-5.5", 15_000, cacheRead: 10_000), pullRequest),
            (Chat(B3, "e300000000000001", "a300000000000001", "11:00:00", "claude-opus-5.5", 20_000, cacheRead: 5_000, conversation: "conv-c"), feature),
            (Chat(B3, "e300000000000002", "a300000000000001", "11:01:00", "claude-opus-5.5", 30_000, cacheRead: 5_000, conversation: "conv-c"), feature),
            (Chat(B0, "f000000000000001", null, "12:00:00", "mai-code-2", 5_000, conversation: null) with
            {
                CacheReadState = CacheCounterState.Reported,
            }, null),
        ];
        await store.RecordAsync(
            spans.Select(item => new LedgerEntry(item.Span, rates.Calculate(item.Span), item.Git)).ToArray());
        await store.RecordSpanLinksAsync(
        [
            Link(B1, "a100000000000001", null, "invoke_agent", "10:00:00"),
            Link(B1, "e100000000000001", "a100000000000001", "execute_tool", "10:00:04"),
            Link(B1, "a100000000000002", "e100000000000001", "invoke_agent", "10:00:04", "explore"),
            .. spans.Where(item => item.Span.ParentSpanId is not null).Select(item => Link(
                item.Span.TraceId,
                item.Span.SpanId,
                item.Span.ParentSpanId,
                "chat",
                item.Span.StartTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                conversation: item.Span.ConversationId)),
            Link(B2, "a200000000000001", null, "invoke_agent", "10:05:00"),
            Link(B3, "a300000000000001", null, "invoke_agent", "11:00:00", conversation: "conv-c"),
        ]);
        await store.BackfillFromAgentSpansAsync([B0, B1, B2, B3]);
    }

    private static NormalizedGenAiSpan Chat(
        string trace,
        string span,
        string? parent,
        string time,
        string model,
        long input,
        long cacheRead = 0,
        long cacheWrite = 0,
        string? conversation = "conv-b") =>
        TestSpans.Chat(
            parentSpanId: parent,
            startTime: At(time),
            requestModel: model,
            conversationId: conversation,
            inputTokens: input,
            outputTokens: model == "mai-code-2" ? 100 : 500,
            cacheReadInputTokens: cacheRead,
            cacheWriteInputTokens: cacheWrite) with
        {
            TraceId = trace,
            SpanId = span,
        };

    private static GenAiSpanLink Link(
        string trace,
        string span,
        string? parent,
        string operation,
        string time,
        string? agent = null,
        string? conversation = "conv-b") => new()
    {
        TraceId = trace,
        SpanId = span,
        ParentSpanId = parent,
        OperationName = operation,
        AgentName = agent,
        ConversationId = conversation,
        StartTime = At(time),
    };

    private static DateTimeOffset At(string time) =>
        DateTimeOffset.Parse($"2026-11-21T{time}Z", CultureInfo.InvariantCulture);
}
```

### Tests

- **TB1** (trace B1): six rows in start order, with deltas `null`, `null`, `+2,000` (explore), `+2,000` (default, despite the explore request at the same millisecond), `-1,000`, and `null` (unknown agent); per-row states, observed counters, uncached (1,000, n/a, 3,000, 500, 0, n/a), and ratios `0`, `null`, `0.4`, `0.75`, `1`, `null`.
- **TB2** (conversation `conv-b`): B1's six rows, then B2's, whose delta is `null` because it's another prompt's agent; ratio `0.6666666666666666666666666667`. An unknown conversation is empty.
- **TB3:** filters apply after deltas: agent `explore` gives deltas `null` and `2,000`; `From` 10:00:10 gives four rows with deltas `2,000`, `2,000`, `-1,000`, and `null`. An uppercase trace ID is accepted.
- **TB4:** selector validation, including uppercase IDs being lowercased.
- **CB1:** `ByUncachedInput` is B3 (40,000, ratio `0.2`), B0 (5,000, `0`), and B2 (5,000, `0.6666666666666666666666666667`), the tie at 5,000 going to the lower trace ID; `ByEstimatedUncachedInput` is B1 (6 spans, 4 reported, input 61,000, observed 4,500, estimate 27,500, ratio `0.5789473684210526315789473684`); `ByInputGrowth` is B3 (10,000) and then B1's default agent (10,000 → 12,000), which wins its tie with the explore agent's 2,000 growth on the lower agent span ID; `SpansWithoutAgent` is 2.
- **CB2:** `limit` 1 keeps the first of each ranking; agent `explore` leaves only B1's explore requests (estimate 6,000, ratio `0.4`, growth 2,000); limits 0 and 1001 throw.

### Acceptance criteria

- Turns are inference spans in start order (`invoke_agent` spans are never `spans` rows), with exact per-turn values and deltas that never mix interleaved agents or equal timestamps.
- Bloat rankings are deterministic, rank unknown totals separately, and break ties by trace ID.
- `dotnet test CopilotLedger.slnx` passes **749** tests (743 + 6).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 7 specification: Cache Hit in status, branch, and agents

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs` | New `Percent`, `WriteDecimal`, and `WriteCacheCoverage` |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | Plain `status` prints the cache table and writes each period's `cache` object; `branch` and `agents` JSON rows end with the cache coverage; new `Spans` helper |
| `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs` | `StatusText` gains the cache table; new RP14 and RP15, and the helpers `AssertCache` and `AssertCoverage` |
| `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs` | Spans get explicit states; the first prompt reads 20,000 tokens from the cache |
| `docs/guide.md` | The `status-first` and `status` samples |

### Contracts

```csharp
// LedgerFormat
public static string Percent(decimal? ratio);   // "n/a", or Round(ratio × 100, 1, AwayFromZero) as "0.0" + "%"
public static void WriteDecimal(Utf8JsonWriter writer, string name, decimal? value);   // every digit, or null
public static void WriteCacheCoverage(Utf8JsonWriter writer, LedgerRollupRow row);    // cacheReportedSpans, cacheUnreportedSpans, cacheHitRatio
```

Plain `status`, after the spend table, prints a blank line and a second table:

```text
Period        Cache hit  Cache reported
Today             53.7%  6 of 8 spans
Last 7 days       52.7%  8 of 10 spans
Last 30 days      48.8%  9 of 11 spans
```

`Cache reported` is `<reported> of <spans> span(s)`, with `span` for exactly 1. `status --json` ends each period object with:

```json
"cache": {
  "cacheReportedSpans": 6,
  "cacheUnreportedSpans": 2,
  "reportedInputTokens": 2700000,
  "reportedCacheReadInputTokens": 1450000,
  "cacheHitRatio": 0.537037037037037037037037037
}
```

`branch --json` and `agents --json` rows add `cacheReportedSpans`, `cacheUnreportedSpans`, and `cacheHitRatio` after `localSavedUsd`. Tables of `branch` and `agents`, `top-prompts`, and receipts don't change.

`GuideWorld` marks a span `Reported` for both counters when it has no `serverAddress` (GitHub-hosted) and `Absent` otherwise (this example's Foundry BYOK and local endpoints), and its first prompt now reads 20,000 of its 30,000 input tokens from the cache, so the guide's first `status` sample shows a typical cache hit (66.7%) rather than a first-run 0.0% that could look like a fault. That prompt now costs 7.40 AI credits ($0.07) instead of 15.00; the Month state's money doesn't change.

### Code (copy exactly)

Edit 1 of 3 in `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs`, replace:

```csharp
        value.ToString("N0", CultureInfo.InvariantCulture);

    public static string LocalTime(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone)
```

with:

```csharp
        value.ToString("N0", CultureInfo.InvariantCulture);

    // ratio as a percentage with one decimal place, rounded half away from zero; n/a for null, never 0.0%.
    public static string Percent(decimal? ratio) =>
        ratio is null
            ? "n/a"
            : decimal.Round(ratio.Value * 100m, 1, MidpointRounding.AwayFromZero)
                .ToString("0.0", CultureInfo.InvariantCulture) + "%";

    public static string LocalTime(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone)
```

Edit 2 of 3 in `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs`, replace:

```csharp
        writer.WritePropertyName(name);
        writer.WriteRawValue(value.ToString("0.############", CultureInfo.InvariantCulture));
    }

    public static void WriteTime(
```

with:

```csharp
        writer.WritePropertyName(name);
        writer.WriteRawValue(value.ToString("0.############", CultureInfo.InvariantCulture));
    }

    // value with every digit decimal division gave it, or null. Used for ratios and averages.
    public static void WriteDecimal(
        Utf8JsonWriter writer,
        string name,
        decimal? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WritePropertyName(name);
        writer.WriteRawValue(value.Value.ToString(CultureInfo.InvariantCulture));
    }

    public static void WriteTime(
```

Edit 3 of 3 in `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs`, replace:

```csharp
        WriteMoney(writer, "localSavedUsd", row.LocalSavedUsd);
    }

    public static void WriteTotals(
        Utf8JsonWriter writer,
```

with:

```csharp
        WriteMoney(writer, "localSavedUsd", row.LocalSavedUsd);
    }

    // The cache fields that branch and agents rows end with. Receipts don't write them.
    public static void WriteCacheCoverage(
        Utf8JsonWriter writer,
        LedgerRollupRow row)
    {
        writer.WriteNumber("cacheReportedSpans", row.CacheReportedSpanCount);
        writer.WriteNumber("cacheUnreportedSpans", row.CacheUnreportedSpanCount);
        WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
    }

    public static void WriteTotals(
        Utf8JsonWriter writer,
```

Edit 1 of 4 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            totals.Select(item =>
                (IReadOnlyList<string>)[item.Label, .. MoneyCells(item.Total)]).ToArray());
        return 0;
    }

    private static async Task<int> BranchAsync(
```

with:

```csharp
            totals.Select(item =>
                (IReadOnlyList<string>)[item.Label, .. MoneyCells(item.Total)]).ToArray());
        context.Out.WriteLine();
        TableWriter.Write(
            context.Out,
            [new("Period", false), new("Cache hit", true), new("Cache reported", false)],
            totals.Select(item =>
                (IReadOnlyList<string>)
                [
                    item.Label,
                    LedgerFormat.Percent(item.Total.CacheHitRatio),
                    $"{LedgerFormat.Count(item.Total.CacheReportedSpanCount)} of {Spans(item.Total.SpanCount)}",
                ]).ToArray());
        return 0;
    }

    private static async Task<int> BranchAsync(
```

Edit 2 of 4 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            LedgerFormat.WriteTime(writer, "from", period.From);
            LedgerFormat.WriteTotals(writer, total);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
```

with:

```csharp
            LedgerFormat.WriteTime(writer, "from", period.From);
            LedgerFormat.WriteTotals(writer, total);
            writer.WritePropertyName("cache");
            writer.WriteStartObject();
            writer.WriteNumber("cacheReportedSpans", total.CacheReportedSpanCount);
            writer.WriteNumber("cacheUnreportedSpans", total.CacheUnreportedSpanCount);
            writer.WriteNumber("reportedInputTokens", total.ReportedInputTokens);
            writer.WriteNumber("reportedCacheReadInputTokens", total.ReportedCacheReadInputTokens);
            LedgerFormat.WriteDecimal(writer, "cacheHitRatio", total.CacheHitRatio);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
```

Edit 3 of 4 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                writer.WriteString("agent", row.Agent);
            }

            LedgerFormat.WriteTotals(writer, row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
```

with:

```csharp
                writer.WriteString("agent", row.Agent);
            }

            LedgerFormat.WriteTotals(writer, row);
            LedgerFormat.WriteCacheCoverage(writer, row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
```

Edit 4 of 4 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        values.Count == 0 ? "-" : string.Join(", ", values);

    private static bool IsOtelEnabled(Func<string, string?> lookup)
    {
        string? enabled = lookup("COPILOT_OTEL_ENABLED");
```

with:

```csharp
        values.Count == 0 ? "-" : string.Join(", ", values);

    private static string Spans(long count) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? "span" : "spans")}";

    private static bool IsOtelEnabled(Func<string, string?> lookup)
    {
        string? enabled = lookup("COPILOT_OTEL_ENABLED");
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
            document.RootElement.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()));
    }

    private static async Task<CliHarness> SeededHarnessAsync(
        string root,
```

with:

```csharp
            document.RootElement.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task RP14_StatusJsonHasEachPeriodsCacheCoverage()
    {
        using TempDirectory temp = new();
        using TempDirectory empty = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        await using CliHarness emptyHarness = new(empty.Path, new FakeCliPlatform(), Time);

        CliRun run = await harness.RunAsync("status", "--json");
        CliRun emptyRun = await emptyHarness.RunAsync("status", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"cacheHitRatio\": 0.5454545454545454545454545455\n", run.Out);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        JsonElement[] periods = json.RootElement.GetProperty("periods").EnumerateArray().ToArray();
        AssertCache(periods[0], 2, 1, 2_200_000, 1_200_000, 0.5454545454545454545454545455m);
        AssertCache(periods[1], 2, 2, 2_200_000, 1_200_000, 0.5454545454545454545454545455m);
        AssertCache(periods[2], 2, 2, 2_200_000, 1_200_000, 0.5454545454545454545454545455m);
        Assert.Equal("cache", periods[0].EnumerateObject().Last().Name);
        Assert.Equal(0, emptyRun.ExitCode);
        using JsonDocument emptyJson = JsonDocument.Parse(emptyRun.Out);
        foreach (JsonElement period in emptyJson.RootElement.GetProperty("periods").EnumerateArray())
        {
            AssertCache(period, 0, 0, 0, 0, null);
        }
    }

    [Fact]
    public async Task RP15_BranchAndAgentRowsEndWithCacheCoverage()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun branch = await harness.RunAsync("branch", "--all-repos", "--json");
        CliRun agents = await harness.RunAsync("agents", "--since", "all", "--json");

        Assert.Equal(0, branch.ExitCode);
        using JsonDocument branchJson = JsonDocument.Parse(branch.Out);
        JsonElement[] branchRows = branchJson.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(3, branchRows.Length);
        AssertCoverage(branchRows[0], 1, 0, 0.6666666666666666666666666667m);
        AssertCoverage(branchRows[1], 1, 1, 0.4m);
        AssertCoverage(branchRows[2], 0, 1, null);
        Assert.Equal(0, agents.ExitCode);
        using JsonDocument agentJson = JsonDocument.Parse(agents.Out);
        JsonElement[] agentRows = agentJson.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(["ledger-architect", "ledger-builder", "(none)"], agentRows.Select(row => row.GetProperty("agent").GetString()));
        AssertCoverage(agentRows[0], 1, 0, 0.6666666666666666666666666667m);
        AssertCoverage(agentRows[1], 1, 2, 0.4m);
        AssertCoverage(agentRows[2], 0, 1, null);
    }

    private static async Task<CliHarness> SeededHarnessAsync(
        string root,
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
        Assert.Equal(saved, item.GetProperty("localSavedUsd").GetDecimal());
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static void AssertRollup(
```

with:

```csharp
        Assert.Equal(saved, item.GetProperty("localSavedUsd").GetDecimal());
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static void AssertCache(
        JsonElement period,
        long reported,
        long unreported,
        long reportedInput,
        long reportedCacheRead,
        decimal? hitRatio)
    {
        JsonElement cache = period.GetProperty("cache");
        Assert.Equal(
            ["cacheReportedSpans", "cacheUnreportedSpans", "reportedInputTokens", "reportedCacheReadInputTokens", "cacheHitRatio"],
            cache.EnumerateObject().Select(property => property.Name));
        Assert.Equal(reported, cache.GetProperty("cacheReportedSpans").GetInt64());
        Assert.Equal(unreported, cache.GetProperty("cacheUnreportedSpans").GetInt64());
        Assert.Equal(reportedInput, cache.GetProperty("reportedInputTokens").GetInt64());
        Assert.Equal(reportedCacheRead, cache.GetProperty("reportedCacheReadInputTokens").GetInt64());
        JsonElement ratio = cache.GetProperty("cacheHitRatio");
        Assert.Equal(hitRatio, ratio.ValueKind == JsonValueKind.Null ? null : ratio.GetDecimal());
    }

    private static void AssertCoverage(
        JsonElement row,
        long reported,
        long unreported,
        decimal? hitRatio)
    {
        Assert.Equal(
            ["localSavedUsd", "cacheReportedSpans", "cacheUnreportedSpans", "cacheHitRatio"],
            row.EnumerateObject().Select(property => property.Name).TakeLast(4));
        Assert.Equal(reported, row.GetProperty("cacheReportedSpans").GetInt64());
        Assert.Equal(unreported, row.GetProperty("cacheUnreportedSpans").GetInt64());
        JsonElement ratio = row.GetProperty("cacheHitRatio");
        Assert.Equal(hitRatio, ratio.ValueKind == JsonValueKind.Null ? null : ratio.GetDecimal());
    }

    private static void AssertRollup(
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
        Last 30 days      4         0      342.00       $3.42     $0.32        $0.23

        """);
}
```

with:

```csharp
        Last 30 days      4         0      342.00       $3.42     $0.32        $0.23

        Period        Cache hit  Cache reported
        Today             54.5%  2 of 3 spans
        Last 7 days       54.5%  2 of 4 spans
        Last 30 days      54.5%  2 of 4 spans

        """);
}
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
        if (state == GuideState.FirstPrompt)
        {
            JsonRateCardCalculator builtIn = JsonRateCardCalculator.LoadEmbedded();
            NormalizedGenAiSpan first = Chat("1f", "c1", "2026-11-21T21:25:00Z", "claude-opus-5.5", null, null, 30_000, 1_500);
            await store.RecordAsync([new LedgerEntry(first, builtIn.Calculate(first), Context("feature/search"))]);
            return;
```

with:

```csharp
        if (state == GuideState.FirstPrompt)
        {
            JsonRateCardCalculator builtIn = JsonRateCardCalculator.LoadEmbedded();
            NormalizedGenAiSpan first = Chat("1f", "c1", "2026-11-21T21:25:00Z", "claude-opus-5.5", null, null, 30_000, 1_500, 20_000);
            await store.RecordAsync([new LedgerEntry(first, builtIn.Calculate(first), Context("feature/search"))]);
            return;
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
        Slug);

    // trace and span are the last characters of 32- and 16-character IDs.
    private static NormalizedGenAiSpan Chat(
        string trace,
```

with:

```csharp
        Slug);

    // trace and span are the last characters of 32- and 16-character IDs. GitHub-hosted models report cache counters, and
    // this example's Foundry BYOK and local endpoints (the spans with a serverAddress) don't.
    private static NormalizedGenAiSpan Chat(
        string trace,
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            cacheReadInputTokens: cacheReadInputTokens) with
        {
            TraceId = "0f3c9a7e5d1b4f2a8c6e0d9b7a5c" + trace.PadLeft(4, '0'),
```

with:

```csharp
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            cacheReadInputTokens: cacheReadInputTokens,
            cacheReadState: serverAddress is null ? CacheCounterState.Reported : CacheCounterState.Absent,
            cacheWriteState: serverAddress is null ? CacheCounterState.Reported : CacheCounterState.Absent) with
        {
            TraceId = "0f3c9a7e5d1b4f2a8c6e0d9b7a5c" + trace.PadLeft(4, '0'),
```

Edit 1 of 2 in `docs/guide.md`, replace:

````markdown
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
Today             1         0       15.00       $0.15     $0.00        $0.00
Last 7 days       1         0       15.00       $0.15     $0.00        $0.00
Last 30 days      1         0       15.00       $0.15     $0.00        $0.00
```
<!-- /sample -->
````

with:

````markdown
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
Today             1         0        7.40       $0.07     $0.00        $0.00
Last 7 days       1         0        7.40       $0.07     $0.00        $0.00
Last 30 days      1         0        7.40       $0.07     $0.00        $0.00

Period        Cache hit  Cache reported
Today             66.7%  1 of 1 span
Last 7 days       66.7%  1 of 1 span
Last 30 days      66.7%  1 of 1 span
```
<!-- /sample -->
````

Edit 2 of 2 in `docs/guide.md`, replace:

````markdown
Last 7 days      10         0      764.00       $7.64     $0.49        $0.17
Last 30 days     11         0      908.00       $9.08     $0.49        $0.17
```
<!-- /sample -->
````

with:

````markdown
Last 7 days      10         0      764.00       $7.64     $0.49        $0.17
Last 30 days     11         0      908.00       $9.08     $0.49        $0.17

Period        Cache hit  Cache reported
Today             53.7%  6 of 8 spans
Last 7 days       52.7%  8 of 10 spans
Last 30 days      48.8%  9 of 11 spans
```
<!-- /sample -->
````

### Tests

- **StatusText** ends with the cache table: `54.5%` for each period, `2 of 3 spans`, `2 of 4 spans`, `2 of 4 spans`.
- **RP14:** each `status --json` period's `cache` object has exactly the five properties in order, with 2 reported spans, 1 or 2 unreported, reported input 2,200,000, reads 1,200,000, and ratio `0.5454545454545454545454545455`, written with every digit; `cache` is the period's last property; an empty ledger gives 0, 0, 0, 0, and `null`.
- **RP15:** the last four properties of every `branch --json` and `agents --json` row are `localSavedUsd`, `cacheReportedSpans`, `cacheUnreportedSpans`, and `cacheHitRatio`, with ratios `0.6666666666666666666666666667`, `0.4`, and `null`.
- **GS2** (`status-first`, `status`): the guide samples above.

### Acceptance criteria

- Plain `status` keeps its daemon and configuration lines and all three spend periods, and adds the cache table and JSON `cache` object; a period without reported spans shows `n/a` and `null`, never `0.0%`.
- The CI smoke test's greps still match: `"spans": 1,` and `"aiCredits": 50,` are followed by other properties.
- `dotnet test CopilotLedger.slnx` passes **751** tests (749 + 2).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 8 specification: status --cache

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | `status` gains `--cache`, `--by`, `--repo`, `--branch`, `--pr`, and `--since`, a validator, and the cache report: `CacheColumns`, `CacheAsync`, `CacheCells`, `Observed`, `Scope`, and `WriteCacheJson` |
| `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs` | New `WriteNullable(writer, name, string?)` and `WriteNullable(writer, name, long?)` |
| `tests/CopilotLedger.Tests/Cli/CacheReportTests.cs` | New: CR1–CR6 |

### Grammar

```text
gh ledger status [--json]
gh ledger status --cache [--by model|branch|pr|agent] [--repo <key>] [--branch <name>] [--pr <n>] [--since <period>] [--json]
```

- `status`'s description becomes "Show daemon, configuration, and recent spend, or prompt-cache efficiency."
- `--by` defaults to `model`; anything else but `branch`, `pr`, or `agent` is "--by must be model, branch, pr, or agent.". `--pr` below 1 is "--pr must be a positive integer.". `--repo` and `--since` reuse `RepositoryOption()` and `SinceOption(context)` (default `30d`, with its existing message).
- Without `--cache`, the first of `--by`, `--repo`, `--branch`, `--pr`, and `--since` that the user typed is an error: "<option> requires --cache.". (Task 9 adds `--turns`, `--trace`, `--conversation`, `--bloat`, and `--limit`, and the rest of the rules.)

### Output

1. Rows come from `GetCacheEfficiencyAsync` with the filter `{ Repository, Branch, PullRequestNumber, From = period.From }`.
2. No rows: `No spans recorded in <scope> (<period>).`, where `<scope>` is the repository or `all repositories`, then `, branch <name>` and `, pull request #<n>` for those filters.
3. Otherwise a title, `Cache efficiency by <model|branch|pull request|agent> in <scope> (<period>)`, a blank line, and a table. Its leading columns are `Model`, `Agent`, `Branch`, or `Pull request` (`#<n>`, or `(no PR) <branch>`), with `Repository` first for branches and pull requests when `--repo` isn't given. Then `Spans`, `Cache reported`, `Input`, `Cache read`, `Cache write`, `Uncached`, `Output`, and `Hit %`, right-aligned. `Cache read`, `Cache write`, and `Uncached` are the reported spans' totals: `n/a` when the row has none, with `*` appended when it also has unreported spans.
4. When any row has unreported spans: a blank line and `<n> of <total> span(s) reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.`
5. `--json` writes `period`, `from`, `by`, `repository`, `branch`, `pullRequest` (the filters, or `null`), and `rows`. Each row has its keys (`model`; `agent`; `repository`, `branch`; or `repository`, `pullRequest`, `branch`), then `spans`, `cacheReportedSpans`, `cacheUnreportedSpans`, `inputTokens`, `outputTokens`, `maxInputTokens`, `averageInputTokensPerTurn`, `reportedInputTokens`, `reportedCacheReadInputTokens`, `reportedCacheWriteInputTokens`, `uncachedInputTokens` (observed, or `null`), `estimatedUncachedInputTokens`, and `cacheHitRatio` (exact, or `null`).

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs`, replace:

```csharp
            value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    public static void WriteTotals(
        Utf8JsonWriter writer,
```

with:

```csharp
            value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    public static void WriteNullable(
        Utf8JsonWriter writer,
        string name,
        string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WriteString(name, value);
    }

    public static void WriteNullable(
        Utf8JsonWriter writer,
        string name,
        long? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WriteNumber(name, value.Value);
    }

    public static void WriteTotals(
        Utf8JsonWriter writer,
```

Edit 1 of 5 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        new("Local saved", true),
    ];

    public static IEnumerable<Command> Create(CliContext context)
    {
        yield return CreateStatus(context);
```

with:

```csharp
        new("Local saved", true),
    ];

    private static readonly TableColumn[] CacheColumns =
    [
        new("Spans", true),
        new("Cache reported", true),
        new("Input", true),
        new("Cache read", true),
        new("Cache write", true),
        new("Uncached", true),
        new("Output", true),
        new("Hit %", true),
    ];

    public static IEnumerable<Command> Create(CliContext context)
    {
        yield return CreateStatus(context);
```

Edit 2 of 5 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
    private static Command CreateStatus(CliContext context)
    {
        Command command = new("status", "Show daemon, configuration, and recent spend.");
        Option<bool> json = JsonOption();
        command.Add(json);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                StatusAsync(context, result.GetValue(json), cancellationToken));
        return command;
    }

    private static Command CreateBranch(CliContext context)
```

with:

```csharp
    private static Command CreateStatus(CliContext context)
    {
        Command command = new("status", "Show daemon, configuration, and recent spend, or prompt-cache efficiency.");
        Option<bool> json = JsonOption();
        Option<bool> cache = new("--cache")
        {
            Description = "Report prompt-cache efficiency instead.",
        };
        Option<string> by = new("--by")
        {
            Description = "Group --cache rows by model, branch, pr, or agent.",
            DefaultValueFactory = _ => "model",
        };
        by.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<string>() is not ("model" or "branch" or "pr" or "agent"))
            {
                result.AddError("--by must be model, branch, pr, or agent.");
            }
        });
        Option<string?> repository = RepositoryOption();
        Option<string?> branch = new("--branch")
        {
            Description = "Limit --cache to this branch, or (none).",
        };
        Option<int?> pullRequest = new("--pr")
        {
            Description = "Limit --cache to this pull request number.",
        };
        pullRequest.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is < 1)
            {
                result.AddError("--pr must be a positive integer.");
            }
        });
        Option<string> since = SinceOption(context);
        command.Add(json);
        command.Add(cache);
        command.Add(by);
        command.Add(repository);
        command.Add(branch);
        command.Add(pullRequest);
        command.Add(since);
        command.Validators.Add(result =>
        {
            string[] given = new Option[] { by, repository, branch, pullRequest, since }
                .Where(option => result.GetResult(option) is { Implicit: false })
                .Select(option => option.Name)
                .ToArray();
            if (!result.GetValue(cache) && given.Length > 0)
            {
                result.AddError($"{given[0]} requires --cache.");
            }
        });
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                !result.GetValue(cache)
                    ? StatusAsync(context, result.GetValue(json), cancellationToken)
                    : CacheAsync(
                        context,
                        result.GetValue(by)!,
                        new LedgerFilter
                        {
                            Repository = result.GetValue(repository),
                            Branch = result.GetValue(branch),
                            PullRequestNumber = result.GetValue(pullRequest),
                        },
                        ParsePeriod(context, result.GetValue(since)),
                        result.GetValue(json),
                        cancellationToken));
        return command;
    }

    private static Command CreateBranch(CliContext context)
```

Edit 3 of 5 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        return 0;
    }

    private static void WriteStatusJson(
        CliContext context,
```

with:

```csharp
        return 0;
    }

    private static async Task<int> CacheAsync(
        CliContext context,
        string by,
        LedgerFilter filter,
        Period period,
        bool json,
        CancellationToken cancellationToken)
    {
        LedgerGrouping grouping = by switch
        {
            "branch" => LedgerGrouping.Branch,
            "pr" => LedgerGrouping.PullRequest,
            "agent" => LedgerGrouping.Agent,
            _ => LedgerGrouping.Model,
        };
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        IReadOnlyList<CacheEfficiencyRow> rows = await store.GetCacheEfficiencyAsync(
            grouping,
            filter with { From = period.From },
            cancellationToken);
        if (json)
        {
            WriteCacheJson(context, period, by, filter, rows);
            return 0;
        }

        string scope = Scope(filter);
        if (rows.Count == 0)
        {
            context.Out.WriteLine($"No spans recorded in {scope} ({period.Label}).");
            return 0;
        }

        context.Out.WriteLine(
            $"Cache efficiency by {(by == "pr" ? "pull request" : by)} in {scope} ({period.Label})");
        context.Out.WriteLine();
        bool repositoryColumn = filter.Repository is null
            && grouping is LedgerGrouping.Branch or LedgerGrouping.PullRequest;
        TableColumn[] leading = grouping switch
        {
            LedgerGrouping.Model => [new("Model", false)],
            LedgerGrouping.Agent => [new("Agent", false)],
            LedgerGrouping.Branch => repositoryColumn
                ? [new("Repository", false), new("Branch", false)]
                : [new("Branch", false)],
            _ => repositoryColumn
                ? [new("Repository", false), new("Pull request", false)]
                : [new("Pull request", false)],
        };
        IReadOnlyList<IReadOnlyList<string>> tableRows = rows.Select(row =>
        {
            string key = grouping switch
            {
                LedgerGrouping.Model => row.Model!,
                LedgerGrouping.Agent => row.Agent!,
                LedgerGrouping.Branch => row.Branch!,
                _ => row.PullRequestNumber is int number
                    ? "#" + number.ToString(CultureInfo.InvariantCulture)
                    : $"(no PR) {row.Branch}",
            };
            string[] identifiers = repositoryColumn ? [row.Repository!, key] : [key];
            return (IReadOnlyList<string>)[.. identifiers, .. CacheCells(row)];
        }).ToArray();
        TableWriter.Write(context.Out, [.. leading, .. CacheColumns], tableRows);
        long unreported = rows.Sum(row => row.CacheUnreportedSpanCount);
        if (unreported > 0)
        {
            context.Out.WriteLine();
            context.Out.WriteLine(
                $"{LedgerFormat.Count(unreported)} of {Spans(rows.Sum(row => row.SpanCount))} reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.");
        }

        return 0;
    }

    private static void WriteStatusJson(
        CliContext context,
```

Edit 4 of 5 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static LedgerRollupRow Sum(IReadOnlyList<LedgerRollupRow> rows) =>
        new()
```

with:

```csharp
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteCacheJson(
        CliContext context,
        Period period,
        string by,
        LedgerFilter filter,
        IReadOnlyList<CacheEfficiencyRow> rows)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteString("period", period.Label);
        LedgerFormat.WriteTime(writer, "from", period.From);
        writer.WriteString("by", by);
        LedgerFormat.WriteNullable(writer, "repository", filter.Repository);
        LedgerFormat.WriteNullable(writer, "branch", filter.Branch);
        LedgerFormat.WriteNullable(writer, "pullRequest", filter.PullRequestNumber);
        writer.WriteStartArray("rows");
        foreach (CacheEfficiencyRow row in rows)
        {
            writer.WriteStartObject();
            switch (by)
            {
                case "model":
                    writer.WriteString("model", row.Model);
                    break;
                case "agent":
                    writer.WriteString("agent", row.Agent);
                    break;
                case "branch":
                    writer.WriteString("repository", row.Repository);
                    writer.WriteString("branch", row.Branch);
                    break;
                default:
                    writer.WriteString("repository", row.Repository);
                    LedgerFormat.WriteNullable(writer, "pullRequest", row.PullRequestNumber);
                    LedgerFormat.WriteNullable(writer, "branch", row.Branch);
                    break;
            }

            writer.WriteNumber("spans", row.SpanCount);
            writer.WriteNumber("cacheReportedSpans", row.CacheReportedSpanCount);
            writer.WriteNumber("cacheUnreportedSpans", row.CacheUnreportedSpanCount);
            writer.WriteNumber("inputTokens", row.InputTokens);
            writer.WriteNumber("outputTokens", row.OutputTokens);
            writer.WriteNumber("maxInputTokens", row.MaxInputTokens);
            LedgerFormat.WriteDecimal(writer, "averageInputTokensPerTurn", row.AverageInputTokensPerTurn);
            writer.WriteNumber("reportedInputTokens", row.ReportedInputTokens);
            writer.WriteNumber("reportedCacheReadInputTokens", row.ReportedCacheReadInputTokens);
            writer.WriteNumber("reportedCacheWriteInputTokens", row.ReportedCacheWriteInputTokens);
            LedgerFormat.WriteNullable(writer, "uncachedInputTokens", row.UncachedInputTokens);
            writer.WriteNumber("estimatedUncachedInputTokens", row.EstimatedUncachedInputTokens);
            LedgerFormat.WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static LedgerRollupRow Sum(IReadOnlyList<LedgerRollupRow> rows) =>
        new()
```

Edit 5 of 5 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        LedgerFormat.Usd(row.LocalSavedUsd),
    ];

    private static string Joined(IReadOnlyList<string> values) =>
```

with:

```csharp
        LedgerFormat.Usd(row.LocalSavedUsd),
    ];

    private static string[] CacheCells(CacheEfficiencyRow row) =>
    [
        LedgerFormat.Count(row.SpanCount),
        LedgerFormat.Count(row.CacheReportedSpanCount),
        LedgerFormat.Count(row.InputTokens),
        Observed(row, row.ReportedCacheReadInputTokens),
        Observed(row, row.ReportedCacheWriteInputTokens),
        Observed(row, row.ReportedUncachedInputTokens),
        LedgerFormat.Count(row.OutputTokens),
        LedgerFormat.Percent(row.CacheHitRatio),
    ];

    // A total over the spans that reported cache usage: n/a when none did, and marked * when only some did.
    private static string Observed(CacheEfficiencyRow row, long value) =>
        row.CacheReportedSpanCount == 0
            ? "n/a"
            : LedgerFormat.Count(value) + (row.CacheUnreportedSpanCount > 0 ? "*" : string.Empty);

    private static string Scope(LedgerFilter filter) =>
        (filter.Repository ?? "all repositories")
        + (filter.Branch is null ? string.Empty : $", branch {filter.Branch}")
        + (filter.PullRequestNumber is int number
            ? ", pull request #" + number.ToString(CultureInfo.InvariantCulture)
            : string.Empty);

    private static string Joined(IReadOnlyList<string> values) =>
```

New file `tests/CopilotLedger.Tests/Cli/CacheReportTests.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Ingestion;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using static CopilotLedger.Tests.TestSupport.OtlpRequests;

namespace CopilotLedger.Tests.Cli;

public sealed class CacheReportTests
{
    private static ManualTimeProvider Time => new(
        DateTimeOffset.Parse("2026-11-21T15:00:00Z", CultureInfo.InvariantCulture),
        ManualTimeProvider.UtcMinus5);

    public static TheoryData<string, string> Groupings => new()
    {
        {
            "status --cache",
            """
            Cache efficiency by model in all repositories (last 30 days)

            Model            Spans  Cache reported      Input  Cache read  Cache write  Uncached   Output  Hit %
            claude-opus-5.5      1               1  1,200,000     800,000      100,000   300,000   50,000  66.7%
            mai-code-2           1               1  1,000,000     400,000            0   600,000  100,000  40.0%
            qwen3.8-27b          1               0    400,000         n/a          n/a       n/a   12,000    n/a
            gpt-6.1-sol          1               0    100,000         n/a          n/a       n/a   10,000    n/a

            2 of 4 spans reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.

            """
        },
        {
            "status --cache --by branch",
            """
            Cache efficiency by branch in all repositories (last 30 days)

            Repository              Branch          Spans  Cache reported      Input  Cache read  Cache write  Uncached   Output  Hit %
            swigerb/copilot-ledger  feature/ledger      2               1  1,400,000    400,000*           0*  600,000*  112,000  40.0%
            swigerb/copilot-ledger  main                1               1  1,200,000     800,000      100,000   300,000   50,000  66.7%
            contoso/api             main                1               0    100,000         n/a          n/a       n/a   10,000    n/a

            2 of 4 spans reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.

            """
        },
        {
            "status --cache --by pr --since all",
            """
            Cache efficiency by pull request in all repositories (all time)

            Repository              Pull request            Spans  Cache reported      Input  Cache read  Cache write  Uncached   Output  Hit %
            swigerb/copilot-ledger  (no PR) feature/ledger      2               1  1,400,000    400,000*           0*  600,000*  112,000  40.0%
            swigerb/copilot-ledger  (no PR) main                1               1  1,200,000     800,000      100,000   300,000   50,000  66.7%
            contoso/api             (no PR) main                1               0    100,000         n/a          n/a       n/a   10,000    n/a
            (none)                  (no PR) (none)              1               0      1,000         n/a          n/a       n/a      100    n/a

            3 of 5 spans reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.

            """
        },
        {
            "status --cache --by agent --repo swigerb/copilot-ledger",
            """
            Cache efficiency by agent in swigerb/copilot-ledger (last 30 days)

            Agent             Spans  Cache reported      Input  Cache read  Cache write  Uncached   Output  Hit %
            ledger-builder        2               1  1,400,000    400,000*           0*  600,000*  112,000  40.0%
            ledger-architect      1               1  1,200,000     800,000      100,000   300,000   50,000  66.7%

            1 of 3 spans reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.

            """
        },
        {
            "status --cache --by pr --repo swigerb/copilot-ledger --branch main",
            """
            Cache efficiency by pull request in swigerb/copilot-ledger, branch main (last 30 days)

            Pull request  Spans  Cache reported      Input  Cache read  Cache write  Uncached  Output  Hit %
            (no PR) main      1               1  1,200,000     800,000      100,000   300,000  50,000  66.7%

            """
        },
    };

    [Theory]
    [MemberData(nameof(Groupings))]
    public async Task CR1_CacheTablesGroupByModelBranchPullRequestAndAgent(string command, string expected)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync(command.Split(' '));

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(expected.ReplaceLineEndings("\n"), run.Out);
    }

    [Fact]
    public async Task CR2_CacheJsonHasExactRowsAndNullsForUnreportedGroups()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("status", "--cache", "--json");
        CliRun pullRequests = await harness.RunAsync("status", "--cache", "--by", "pr", "--pr", "42", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"cacheHitRatio\": 0.6666666666666666666666666667\n", run.Out);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        JsonElement root = json.RootElement;
        Assert.Equal(
            ["period", "from", "by", "repository", "branch", "pullRequest", "rows"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("last 30 days", root.GetProperty("period").GetString());
        Assert.Equal("model", root.GetProperty("by").GetString());
        JsonElement[] rows = root.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(
            ["model", "spans", "cacheReportedSpans", "cacheUnreportedSpans", "inputTokens", "outputTokens", "maxInputTokens", "averageInputTokensPerTurn", "reportedInputTokens", "reportedCacheReadInputTokens", "reportedCacheWriteInputTokens", "uncachedInputTokens", "estimatedUncachedInputTokens", "cacheHitRatio"],
            rows[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(["claude-opus-5.5", "mai-code-2", "qwen3.8-27b", "gpt-6.1-sol"], rows.Select(row => row.GetProperty("model").GetString()));
        Assert.Equal(1_200_000, rows[0].GetProperty("maxInputTokens").GetInt64());
        Assert.Equal(1_200_000m, rows[0].GetProperty("averageInputTokensPerTurn").GetDecimal());
        Assert.Equal(800_000, rows[0].GetProperty("reportedCacheReadInputTokens").GetInt64());
        Assert.Equal(100_000, rows[0].GetProperty("reportedCacheWriteInputTokens").GetInt64());
        Assert.Equal(300_000, rows[0].GetProperty("uncachedInputTokens").GetInt64());
        Assert.Equal(0.6666666666666666666666666667m, rows[0].GetProperty("cacheHitRatio").GetDecimal());
        Assert.Equal(0.4m, rows[1].GetProperty("cacheHitRatio").GetDecimal());
        foreach (JsonElement unreported in rows[2..])
        {
            Assert.Equal(0, unreported.GetProperty("cacheReportedSpans").GetInt64());
            Assert.Equal(1, unreported.GetProperty("cacheUnreportedSpans").GetInt64());
            Assert.Equal(JsonValueKind.Null, unreported.GetProperty("uncachedInputTokens").ValueKind);
            Assert.Equal(JsonValueKind.Null, unreported.GetProperty("cacheHitRatio").ValueKind);
        }

        Assert.Equal(400_000, rows[2].GetProperty("estimatedUncachedInputTokens").GetInt64());
        Assert.Equal(0, pullRequests.ExitCode);
        using JsonDocument pullRequestJson = JsonDocument.Parse(pullRequests.Out);
        Assert.Equal(42, pullRequestJson.RootElement.GetProperty("pullRequest").GetInt32());
        Assert.Empty(pullRequestJson.RootElement.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task CR3_PullRequestCheckout_GroupsSpansByItsNumber()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);
        string pullRequestRepo = GitFixture.Create(Path.Combine(temp.Path, "pr"), pullRequest: 42);
        string plainRepo = GitFixture.Create(Path.Combine(temp.Path, "plain"));
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        LedgerIngestor ingestor = new(
            store,
            JsonRateCardCalculator.LoadEmbedded(),
            new FileSystemGitContextResolver(ceilingDirectory: temp.Path),
            new CopilotSessionWorkspaceLocator(Path.Combine(temp.Path, "copilot-home")));
        await ingestor.IngestAsync(ChatIn(pullRequestRepo, "c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1", 10_000, 8_000));
        await ingestor.IngestAsync(ChatIn(pullRequestRepo, "c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2", 20_000, 0));
        await ingestor.IngestAsync(ChatIn(plainRepo, "c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3", 5_000, 1_000));

        CliRun run = await harness.RunAsync("status", "--cache", "--by", "pr");
        CliRun filtered = await harness.RunAsync("status", "--cache", "--pr", "42", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            """
            Cache efficiency by pull request in all repositories (last 30 days)

            Repository              Pull request            Spans  Cache reported   Input  Cache read  Cache write  Uncached  Output  Hit %
            swigerb/copilot-ledger  #42                         2               2  30,000       8,000            0    22,000      20  26.7%
            swigerb/copilot-ledger  (no PR) feature/ledger      1               1   5,000       1,000            0     4,000      10  20.0%

            """.ReplaceLineEndings("\n"),
            run.Out);
        Assert.Equal(0, filtered.ExitCode);
        using JsonDocument json = JsonDocument.Parse(filtered.Out);
        JsonElement row = Assert.Single(json.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal("mai-code-2", row.GetProperty("model").GetString());
        Assert.Equal(2, row.GetProperty("spans").GetInt64());
        Assert.Equal(0.2666666666666666666666666667m, row.GetProperty("cacheHitRatio").GetDecimal());
    }

    public static TheoryData<string, string> InvalidOptions => new()
    {
        { "status --by model", "--by requires --cache." },
        { "status --json --repo a/b", "--repo requires --cache." },
        { "status --branch main", "--branch requires --cache." },
        { "status --pr 4", "--pr requires --cache." },
        { "status --since 7d", "--since requires --cache." },
        { "status --cache --by foo", "--by must be model, branch, pr, or agent." },
        { "status --cache --pr 0", "--pr must be a positive integer." },
        { "status --cache --since yesterday", "Invalid --since value yesterday. Use today, all, <N>d (1-3650), or yyyy-MM-dd." },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public async Task CR4_BreakdownOptionsAreValidated(string command, string message)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);

        CliRun run = await harness.RunAsync(command.Split(' '));

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message, run.Error);
    }

    [Fact]
    public async Task CR5_EmptyLedger_ReportsNoSpans()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);

        CliRun text = await harness.RunAsync("status", "--cache", "--repo", "contoso/webapp");
        CliRun json = await harness.RunAsync("status", "--cache", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal("No spans recorded in contoso/webapp (last 30 days).\n", text.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Empty(document.RootElement.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task CR6_OutputIsTheSameUnderGermanCulture()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string[][] commands =
        [
            ["status"],
            ["status", "--json"],
            ["status", "--cache"],
            ["status", "--cache", "--json"],
            ["branch", "--all-repos", "--json"],
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
        Assert.Contains("  66.7%\n", german[2]);
        Assert.Contains("1,200,000", german[2]);
        Assert.Contains("\"cacheHitRatio\": 0.6666666666666666666666666667", german[3]);
        Assert.Contains("54.5%", german[0]);
    }

    private static async Task<CliHarness> SeededHarnessAsync(string root)
    {
        string git = Path.Combine(root, "repo", ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(
            Path.Combine(git, "config"),
            "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n");
        CliHarness harness = new(root, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(
            Path.Combine(root, "data", "ledger.db"),
            harness.TimeProvider);
        await store.InitializeAsync();
        await LedgerSeeds.SeedChainsAsync(
            store,
            Path.Combine(root, "repo"),
            Path.Combine(root, "api"));
        return harness;
    }

    // One mai-code-2 chat span at 2026-11-20T18:33:24.707Z in repo with a cache read, or, when cacheRead is 0, a cache write of
    // 0, so the span reports its cache usage either way.
    private static OtlpTraceRequest ChatIn(string repo, string traceId, long input, long cacheRead)
    {
        List<OtlpAttribute> attributes =
        [
            Str("gen_ai.operation.name", "chat"),
            Str("gen_ai.request.model", "mai-code-2"),
            Int("gen_ai.usage.input_tokens", input),
            Int("gen_ai.usage.output_tokens", 10),
        ];
        if (cacheRead > 0)
        {
            attributes.Add(Int("gen_ai.usage.cache_read.input_tokens", cacheRead));
        }
        else
        {
            attributes.Add(Int("gen_ai.usage.cache_write.input_tokens", 0));
        }

        return Request(
            [.. CopilotResource, Str("process.working_directory", repo)],
            Span(traceId: traceId, spanId: traceId[..16], attributes: [.. attributes]));
    }
}
```

### Tests

- **CR1** (5 exact tables over `LedgerSeeds.SeedChainsAsync` at 2026-11-21 15:00 UTC, UTC-5): by model (`66.7%`, `40.0%`, and two `n/a` rows; footnote `2 of 4 spans`); by branch, with `400,000*`, `0*`, and `600,000*` for the half-reported `feature/ledger`; by pull request since `all`, with `(no PR)` rows for every branch, including `(none)`; by agent in one repository; and by pull request in one repository and branch, with no footnote.
- **CR2:** the JSON property order above, rows ordered by input, `averageInputTokensPerTurn` `1200000`, `cacheHitRatio` `0.6666666666666666666666666667` written with every digit, `null` observed totals and ratios for unreported rows, and an estimate of 400,000; `--by pr --pr 42 --json` echoes `"pullRequest": 42` with no rows.
- **CR3:** spans ingested by `LedgerIngestor` from a `GitFixture` repository whose config has `refs/pull/42/head` group under `#42` (2 spans, 30,000 input, `26.7%`), and spans from a plain repository under `(no PR) feature/ledger`; `--pr 42 --json` gives one row with ratio `0.2666666666666666666666666667`.
- **CR4** (8 cases): the "requires --cache" errors for `--by`, `--repo` (with `--json`), `--branch`, `--pr`, and `--since`; a bad `--by`; `--pr 0`; and `--since yesterday`. Each exits 1 with its message on standard error.
- **CR5:** an empty ledger prints `No spans recorded in contoso/webapp (last 30 days).` and JSON with no rows.
- **CR6:** `status`, `status --json`, `status --cache`, `status --cache --json`, and `branch --all-repos --json` print the same text under the invariant culture and under `de-DE`, with `66.7%`, `1,200,000`, `54.5%`, and the full-precision ratio.

### Acceptance criteria

- `--by model|branch|pr|agent` all work; a group without reported spans shows `n/a` and `null`, never `0.0%`; partial totals are marked; output is culture-invariant.
- Plain `status` and `status --json` are unchanged from Task 7.
- `dotnet test CopilotLedger.slnx` passes **768** tests (751 + 17).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 9 specification: status --cache --turns and --bloat

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | `status` gains `--turns`, `--trace`, `--conversation`, `--bloat`, and `--limit`, the full option rules, and `TurnsAsync`, `BloatAsync`, `WriteSection`, `WriteTurnsJson`, `WriteBloatJson`, `WriteChains`, `Growth`, `StateName`, and `Requests` |
| `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs` | New `LocalTimeSeconds` |
| `tests/CopilotLedger.Tests/Cli/CacheReportTests.cs` | New CR7–CR11 and `TurnHarnessAsync` |

### Grammar and rules

```text
gh ledger status --cache --turns (--trace <traceId> | --conversation <id>) [--json]
gh ledger status --cache --bloat [--repo <key>] [--since <period>] [--limit <n>] [--json]
```

Option validators: `--trace` must be 32 hexadecimal characters ("--trace must be a 32-character hexadecimal trace ID."), `--conversation` can't be blank ("--conversation can't be blank."), and `--limit` is 1 to 100, default 10 ("--limit must be from 1 to 100."). The command validator replaces Task 8's and checks, in order, over the options the user typed (`--json` and `--cache` aside):

1. Without `--cache`, the first one typed: "<option> requires --cache."
2. `--turns` and `--bloat` together: "Use either --turns or --bloat, not both."
3. With `--turns`: any option but `--trace` and `--conversation`, "<option> can't be used with --turns."; then neither or both of `--trace` and `--conversation`, "Use --turns with either --trace <traceId> or --conversation <id>."
4. Without `--turns`: `--trace` or `--conversation`, "<option> requires --turns."; with `--bloat`, any of `--by`, `--branch`, and `--pr`, "<option> can't be used with --bloat."; without `--bloat`, `--limit`, "--limit requires --bloat."

### `--turns` output

- Rows come from `GetTurnBreakdownAsync(TurnSelector.ForTrace(…)` or `ForConversation(…), LedgerFilter.All)`. None: `No model requests recorded for trace <id>.` (or `conversation <id>`).
- Otherwise `Model requests in trace <id>` (or `conversation <id>`), a blank line, and a table: `#`, `Started` (local time with seconds), `Agent` (`(none)` without a name), `Model`, `Input`, `Cache read`, `Cache write`, `Uncached`, `Output`, `Hit %`, `Growth`, and, for a conversation, `Trace`. Unknown cache values are `n/a`. `Growth` is `-` for a sequence's first request (the span has an agent but no delta), `n/a` without a known agent, and the signed delta otherwise (`+2,000`, `-1,000`).
- Then a blank line and the footnotes: always `Growth is the change in input from the previous request by the same agent invocation and model; - marks its first request.`; when a row has no agent, `n/a growth: the request's agent invocation isn't recorded, so it isn't compared with others.`; when a row reported no cache counters, `<n> of <total> request(s) reported no cache counters.`
- `--json`: `traceId`, `conversationId` (the selector), and `turns`, each with `turn` (1-based) and the fields listed in CR8, including `cacheReadState` and `cacheWriteState` (`reported`, `absent`, or `legacy_unknown`) and `inputDeltaFromPreviousTurn`.

### `--bloat` output

- `GetContextBloatAsync` over `{ From = period.From, Repository = --repo }` with `--limit`.
- `Context bloat in <repository or all repositories> (<period>)`, then three sections, each a title line and a table, or `None.` when it's empty, separated by blank lines: `Prompt chains by uncached input` (`#`, `Started`, `Repository`, `Spans`, `Input`, `Uncached`, `Hit %`, `Trace`); `Prompt chains by input growth within one agent invocation and model` (`#`, `Started`, `Repository`, `Agent`, `Model`, `Requests`, `First input`, `Peak input`, `Growth`, `Trace`); and `Prompt chains with requests that reported no cache counters, by estimated uncached input` (`#`, `Started`, `Repository`, `Spans`, `Cache reported`, `Input`, `Est. uncached`, `Trace`).
- When the third section has rows: a blank line and `Est. uncached is an estimate: it counts all input of a request without cache counters as uncached, as pricing does.` When spans lack an agent: a blank line and `Input growth leaves out <n> request(s) without a recorded agent invocation.`
- `--json`: `period`, `from`, `repository`, `limit`, `byUncachedInput`, `byInputGrowth`, `byEstimatedUncachedInput`, and `spansWithoutAgent`, with the row fields listed in CR10.

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs`, replace:

```csharp
        TimeZoneInfo.ConvertTime(value, zone)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static void WriteMoney(
```

with:

```csharp
        TimeZoneInfo.ConvertTime(value, zone)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string LocalTimeSeconds(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone)
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static void WriteMoney(
```

Edit 1 of 8 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Reporting;
```

with:

```csharp
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Reporting;
```

Edit 2 of 8 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        });
        Option<string> since = SinceOption(context);
        command.Add(json);
        command.Add(cache);
```

with:

```csharp
        });
        Option<string> since = SinceOption(context);
        Option<bool> turns = new("--turns")
        {
            Description = "With --cache, list the model requests of one trace or conversation.",
        };
        Option<string?> trace = new("--trace")
        {
            Description = "The trace ID for --turns.",
        };
        trace.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string?>() ?? string.Empty;
            if (value.Length != 32 || !value.All(char.IsAsciiHexDigit))
            {
                result.AddError("--trace must be a 32-character hexadecimal trace ID.");
            }
        });
        Option<string?> conversation = new("--conversation")
        {
            Description = "The conversation (Copilot session) ID for --turns.",
        };
        conversation.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string?>()))
            {
                result.AddError("--conversation can't be blank.");
            }
        });
        Option<bool> bloat = new("--bloat")
        {
            Description = "With --cache, rank prompt chains by uncached input and input growth.",
        };
        Option<int> limit = new("--limit")
        {
            Description = "Prompt chains in each --bloat ranking.",
            DefaultValueFactory = _ => 10,
        };
        limit.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 1 or > 100)
            {
                result.AddError("--limit must be from 1 to 100.");
            }
        });
        command.Add(json);
        command.Add(cache);
```

Edit 3 of 8 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        command.Add(pullRequest);
        command.Add(since);
        command.Validators.Add(result =>
        {
            string[] given = new Option[] { by, repository, branch, pullRequest, since }
                .Where(option => result.GetResult(option) is { Implicit: false })
                .Select(option => option.Name)
                .ToArray();
            if (!result.GetValue(cache) && given.Length > 0)
            {
                result.AddError($"{given[0]} requires --cache.");
            }
        });
```

with:

```csharp
        command.Add(pullRequest);
        command.Add(since);
        command.Add(turns);
        command.Add(trace);
        command.Add(conversation);
        command.Add(bloat);
        command.Add(limit);
        command.Validators.Add(result =>
        {
            string[] given = new Option[] { by, repository, branch, pullRequest, since, turns, trace, conversation, bloat, limit }
                .Where(option => result.GetResult(option) is { Implicit: false })
                .Select(option => option.Name)
                .ToArray();
            if (!result.GetValue(cache))
            {
                if (given.Length > 0)
                {
                    result.AddError($"{given[0]} requires --cache.");
                }

                return;
            }

            bool turnsView = result.GetValue(turns);
            bool bloatView = result.GetValue(bloat);
            string? error;
            if (turnsView && bloatView)
            {
                error = "Use either --turns or --bloat, not both.";
            }
            else if (turnsView)
            {
                string? other = given.FirstOrDefault(name => name is not ("--turns" or "--trace" or "--conversation"));
                error = other is not null
                    ? $"{other} can't be used with --turns."
                    : given.Contains("--trace") == given.Contains("--conversation")
                        ? "Use --turns with either --trace <traceId> or --conversation <id>."
                        : null;
            }
            else
            {
                string? turnsOnly = given.FirstOrDefault(name => name is "--trace" or "--conversation");
                string? notForBloat = given.FirstOrDefault(name => name is "--by" or "--branch" or "--pr");
                error = turnsOnly is not null
                    ? $"{turnsOnly} requires --turns."
                    : bloatView && notForBloat is not null
                        ? $"{notForBloat} can't be used with --bloat."
                        : !bloatView && given.Contains("--limit")
                            ? "--limit requires --bloat."
                            : null;
            }

            if (error is not null)
            {
                result.AddError(error);
            }
        });
```

Edit 4 of 8 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                !result.GetValue(cache)
                    ? StatusAsync(context, result.GetValue(json), cancellationToken)
                    : CacheAsync(
                        context,
                        result.GetValue(by)!,
                        new LedgerFilter
                        {
                            Repository = result.GetValue(repository),
                            Branch = result.GetValue(branch),
                            PullRequestNumber = result.GetValue(pullRequest),
                        },
                        ParsePeriod(context, result.GetValue(since)),
                        result.GetValue(json),
                        cancellationToken));
        return command;
    }

    private static Command CreateBranch(CliContext context)
```

with:

```csharp
                !result.GetValue(cache)
                    ? StatusAsync(context, result.GetValue(json), cancellationToken)
                    : result.GetValue(turns)
                        ? TurnsAsync(
                            context,
                            result.GetValue(trace) is string traceId
                                ? TurnSelector.ForTrace(traceId)
                                : TurnSelector.ForConversation(result.GetValue(conversation)!),
                            result.GetValue(json),
                            cancellationToken)
                        : result.GetValue(bloat)
                            ? BloatAsync(
                                context,
                                result.GetValue(repository),
                                ParsePeriod(context, result.GetValue(since)),
                                result.GetValue(limit),
                                result.GetValue(json),
                                cancellationToken)
                            : CacheAsync(
                                context,
                                result.GetValue(by)!,
                                new LedgerFilter
                                {
                                    Repository = result.GetValue(repository),
                                    Branch = result.GetValue(branch),
                                    PullRequestNumber = result.GetValue(pullRequest),
                                },
                                ParsePeriod(context, result.GetValue(since)),
                                result.GetValue(json),
                                cancellationToken));
        return command;
    }

    private static Command CreateBranch(CliContext context)
```

Edit 5 of 8 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        return 0;
    }

    private static void WriteStatusJson(
        CliContext context,
```

with:

```csharp
        return 0;
    }

    private static async Task<int> TurnsAsync(
        CliContext context,
        TurnSelector selector,
        bool json,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        IReadOnlyList<TurnTokenRow> rows = await store.GetTurnBreakdownAsync(
            selector,
            LedgerFilter.All,
            cancellationToken);
        if (json)
        {
            WriteTurnsJson(context, selector, rows);
            return 0;
        }

        string scope = selector.TraceId is string traceId
            ? $"trace {traceId}"
            : $"conversation {selector.ConversationId}";
        if (rows.Count == 0)
        {
            context.Out.WriteLine($"No model requests recorded for {scope}.");
            return 0;
        }

        bool conversation = selector.ConversationId is not null;
        context.Out.WriteLine($"Model requests in {scope}");
        context.Out.WriteLine();
        TableColumn[] columns =
        [
            new("#", true),
            new("Started", false),
            new("Agent", false),
            new("Model", false),
            new("Input", true),
            new("Cache read", true),
            new("Cache write", true),
            new("Uncached", true),
            new("Output", true),
            new("Hit %", true),
            new("Growth", true),
            .. conversation ? [new TableColumn("Trace", false)] : Array.Empty<TableColumn>(),
        ];
        IReadOnlyList<IReadOnlyList<string>> tableRows = rows.Select((row, index) =>
            (IReadOnlyList<string>)
            [
                (index + 1).ToString(CultureInfo.InvariantCulture),
                LedgerFormat.LocalTimeSeconds(row.StartTime, context.TimeProvider.LocalTimeZone),
                row.Agent ?? "(none)",
                row.Model,
                LedgerFormat.Count(row.InputTokens),
                row.CacheReadInputTokens is long read ? LedgerFormat.Count(read) : "n/a",
                row.CacheWriteInputTokens is long write ? LedgerFormat.Count(write) : "n/a",
                row.UncachedInputTokens is long uncached ? LedgerFormat.Count(uncached) : "n/a",
                LedgerFormat.Count(row.OutputTokens),
                LedgerFormat.Percent(row.CacheHitRatio),
                Growth(row),
                .. conversation ? [row.TraceId] : Array.Empty<string>(),
            ]).ToArray();
        TableWriter.Write(context.Out, columns, tableRows);
        context.Out.WriteLine();
        context.Out.WriteLine(
            "Growth is the change in input from the previous request by the same agent invocation and model; - marks its first request.");
        if (rows.Any(row => row.AgentSpanId is null))
        {
            context.Out.WriteLine(
                "n/a growth: the request's agent invocation isn't recorded, so it isn't compared with others.");
        }

        long unreported = rows.Count(row => row.CacheReadInputTokens is null);
        if (unreported > 0)
        {
            context.Out.WriteLine(
                $"{LedgerFormat.Count(unreported)} of {Requests(rows.Count)} reported no cache counters.");
        }

        return 0;
    }

    private static async Task<int> BloatAsync(
        CliContext context,
        string? repository,
        Period period,
        int limit,
        bool json,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        ContextBloatReport report = await store.GetContextBloatAsync(
            new LedgerFilter { From = period.From, Repository = repository },
            limit,
            cancellationToken);
        if (json)
        {
            WriteBloatJson(context, period, repository, limit, report);
            return 0;
        }

        TimeZoneInfo zone = context.TimeProvider.LocalTimeZone;
        context.Out.WriteLine($"Context bloat in {repository ?? "all repositories"} ({period.Label})");
        context.Out.WriteLine();
        context.Out.WriteLine("Prompt chains by uncached input");
        WriteSection(
            context,
            [
                new("#", true),
                new("Started", false),
                new("Repository", false),
                new("Spans", true),
                new("Input", true),
                new("Uncached", true),
                new("Hit %", true),
                new("Trace", false),
            ],
            report.ByUncachedInput.Select((row, index) =>
                (IReadOnlyList<string>)
                [
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    LedgerFormat.LocalTime(row.StartTime, zone),
                    row.Repository,
                    LedgerFormat.Count(row.SpanCount),
                    LedgerFormat.Count(row.InputTokens),
                    LedgerFormat.Count(row.ReportedUncachedInputTokens),
                    LedgerFormat.Percent(row.CacheHitRatio),
                    row.TraceId,
                ]).ToArray());
        context.Out.WriteLine();
        context.Out.WriteLine("Prompt chains by input growth within one agent invocation and model");
        WriteSection(
            context,
            [
                new("#", true),
                new("Started", false),
                new("Repository", false),
                new("Agent", false),
                new("Model", false),
                new("Requests", true),
                new("First input", true),
                new("Peak input", true),
                new("Growth", true),
                new("Trace", false),
            ],
            report.ByInputGrowth.Select((row, index) =>
                (IReadOnlyList<string>)
                [
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    LedgerFormat.LocalTime(row.StartTime, zone),
                    row.Repository,
                    row.Agent ?? "(none)",
                    row.Model,
                    LedgerFormat.Count(row.TurnCount),
                    LedgerFormat.Count(row.FirstInputTokens),
                    LedgerFormat.Count(row.PeakInputTokens),
                    LedgerFormat.Count(row.InputGrowthTokens),
                    row.TraceId,
                ]).ToArray());
        context.Out.WriteLine();
        context.Out.WriteLine("Prompt chains with requests that reported no cache counters, by estimated uncached input");
        WriteSection(
            context,
            [
                new("#", true),
                new("Started", false),
                new("Repository", false),
                new("Spans", true),
                new("Cache reported", true),
                new("Input", true),
                new("Est. uncached", true),
                new("Trace", false),
            ],
            report.ByEstimatedUncachedInput.Select((row, index) =>
                (IReadOnlyList<string>)
                [
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    LedgerFormat.LocalTime(row.StartTime, zone),
                    row.Repository,
                    LedgerFormat.Count(row.SpanCount),
                    LedgerFormat.Count(row.CacheReportedSpanCount),
                    LedgerFormat.Count(row.InputTokens),
                    LedgerFormat.Count(row.EstimatedUncachedInputTokens),
                    row.TraceId,
                ]).ToArray());
        if (report.ByEstimatedUncachedInput.Count > 0)
        {
            context.Out.WriteLine();
            context.Out.WriteLine(
                "Est. uncached is an estimate: it counts all input of a request without cache counters as uncached, as pricing does.");
        }

        if (report.SpansWithoutAgent > 0)
        {
            context.Out.WriteLine();
            context.Out.WriteLine(
                $"Input growth leaves out {Requests(report.SpansWithoutAgent)} without a recorded agent invocation.");
        }

        return 0;
    }

    // A ranking's table, or "None." when it has no rows.
    private static void WriteSection(
        CliContext context,
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0)
        {
            context.Out.WriteLine("None.");
            return;
        }

        TableWriter.Write(context.Out, columns, rows);
    }

    private static void WriteStatusJson(
        CliContext context,
```

Edit 6 of 8 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static LedgerRollupRow Sum(IReadOnlyList<LedgerRollupRow> rows) =>
        new()
```

with:

```csharp
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteTurnsJson(
        CliContext context,
        TurnSelector selector,
        IReadOnlyList<TurnTokenRow> rows)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        LedgerFormat.WriteNullable(writer, "traceId", selector.TraceId);
        LedgerFormat.WriteNullable(writer, "conversationId", selector.ConversationId);
        writer.WriteStartArray("turns");
        for (int index = 0; index < rows.Count; index++)
        {
            TurnTokenRow row = rows[index];
            writer.WriteStartObject();
            writer.WriteNumber("turn", index + 1);
            writer.WriteString("traceId", row.TraceId);
            writer.WriteString("spanId", row.SpanId);
            LedgerFormat.WriteNullable(writer, "parentSpanId", row.ParentSpanId);
            LedgerFormat.WriteNullable(writer, "conversationId", row.ConversationId);
            LedgerFormat.WriteNullable(writer, "agentSpanId", row.AgentSpanId);
            LedgerFormat.WriteTime(writer, "startTime", row.StartTime);
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
            LedgerFormat.WriteNullable(writer, "pullRequest", row.PullRequestNumber);
            LedgerFormat.WriteNullable(writer, "agent", row.Agent);
            writer.WriteString("model", row.Model);
            writer.WriteNumber("inputTokens", row.InputTokens);
            writer.WriteNumber("outputTokens", row.OutputTokens);
            writer.WriteString("cacheReadState", StateName(row.CacheReadState));
            writer.WriteString("cacheWriteState", StateName(row.CacheWriteState));
            LedgerFormat.WriteNullable(writer, "cacheReadInputTokens", row.CacheReadInputTokens);
            LedgerFormat.WriteNullable(writer, "cacheWriteInputTokens", row.CacheWriteInputTokens);
            LedgerFormat.WriteNullable(writer, "uncachedInputTokens", row.UncachedInputTokens);
            LedgerFormat.WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
            LedgerFormat.WriteNullable(writer, "inputDeltaFromPreviousTurn", row.InputDeltaFromPreviousTurn);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteBloatJson(
        CliContext context,
        Period period,
        string? repository,
        int limit,
        ContextBloatReport report)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteString("period", period.Label);
        LedgerFormat.WriteTime(writer, "from", period.From);
        LedgerFormat.WriteNullable(writer, "repository", repository);
        writer.WriteNumber("limit", limit);
        WriteChains(writer, "byUncachedInput", report.ByUncachedInput);
        writer.WriteStartArray("byInputGrowth");
        for (int index = 0; index < report.ByInputGrowth.Count; index++)
        {
            InputGrowthRow row = report.ByInputGrowth[index];
            writer.WriteStartObject();
            writer.WriteNumber("rank", index + 1);
            writer.WriteString("traceId", row.TraceId);
            LedgerFormat.WriteNullable(writer, "conversationId", row.ConversationId);
            writer.WriteString("agentSpanId", row.AgentSpanId);
            LedgerFormat.WriteNullable(writer, "agent", row.Agent);
            writer.WriteString("model", row.Model);
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
            LedgerFormat.WriteTime(writer, "startTime", row.StartTime);
            writer.WriteNumber("turns", row.TurnCount);
            writer.WriteNumber("firstInputTokens", row.FirstInputTokens);
            writer.WriteNumber("peakInputTokens", row.PeakInputTokens);
            writer.WriteNumber("inputGrowthTokens", row.InputGrowthTokens);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteChains(writer, "byEstimatedUncachedInput", report.ByEstimatedUncachedInput);
        writer.WriteNumber("spansWithoutAgent", report.SpansWithoutAgent);
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteChains(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyList<BloatChainRow> rows)
    {
        writer.WriteStartArray(name);
        for (int index = 0; index < rows.Count; index++)
        {
            BloatChainRow row = rows[index];
            writer.WriteStartObject();
            writer.WriteNumber("rank", index + 1);
            writer.WriteString("traceId", row.TraceId);
            LedgerFormat.WriteNullable(writer, "conversationId", row.ConversationId);
            LedgerFormat.WriteTime(writer, "startTime", row.StartTime);
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
            writer.WriteNumber("spans", row.SpanCount);
            writer.WriteNumber("cacheReportedSpans", row.CacheReportedSpanCount);
            writer.WriteNumber("cacheUnreportedSpans", row.SpanCount - row.CacheReportedSpanCount);
            writer.WriteNumber("inputTokens", row.InputTokens);
            writer.WriteNumber("reportedInputTokens", row.ReportedInputTokens);
            writer.WriteNumber("reportedCacheReadInputTokens", row.ReportedCacheReadInputTokens);
            LedgerFormat.WriteNullable(
                writer,
                "uncachedInputTokens",
                row.CacheReportedSpanCount == 0 ? null : row.ReportedUncachedInputTokens);
            writer.WriteNumber("estimatedUncachedInputTokens", row.EstimatedUncachedInputTokens);
            LedgerFormat.WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static LedgerRollupRow Sum(IReadOnlyList<LedgerRollupRow> rows) =>
        new()
```

Edit 7 of 8 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            : string.Empty);

    private static string Joined(IReadOnlyList<string> values) =>
        values.Count == 0 ? "-" : string.Join(", ", values);
```

with:

```csharp
            : string.Empty);

    // + and the input added since the sequence's previous request, the decrease, - for its first request, or n/a when
    // the request's agent invocation isn't known.
    private static string Growth(TurnTokenRow row) =>
        row.AgentSpanId is null
            ? "n/a"
            : row.InputDeltaFromPreviousTurn is not long delta
                ? "-"
                : delta > 0
                    ? "+" + LedgerFormat.Count(delta)
                    : LedgerFormat.Count(delta);

    private static string StateName(CacheCounterState state) => state switch
    {
        CacheCounterState.Reported => "reported",
        CacheCounterState.Absent => "absent",
        _ => "legacy_unknown",
    };

    private static string Joined(IReadOnlyList<string> values) =>
        values.Count == 0 ? "-" : string.Join(", ", values);
```

Edit 8 of 8 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
    private static string Spans(long count) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? "span" : "spans")}";

    private static bool IsOtelEnabled(Func<string, string?> lookup)
```

with:

```csharp
    private static string Spans(long count) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? "span" : "spans")}";

    private static string Requests(long count) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? "request" : "requests")}";

    private static bool IsOtelEnabled(Func<string, string?> lookup)
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/CacheReportTests.cs`, replace:

```csharp
        Assert.Contains("54.5%", german[0]);
    }

    private static async Task<CliHarness> SeededHarnessAsync(string root)
    {
        string git = Path.Combine(root, "repo", ".git");
```

with:

```csharp
        Assert.Contains("54.5%", german[0]);
    }

    [Fact]
    public async Task CR7_Turns_ListRequestsInStartOrderWithGrowthPerAgentAndModel()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await TurnHarnessAsync(temp.Path);

        CliRun trace = await harness.RunAsync("status", "--cache", "--turns", "--trace", TurnSeeds.B1);
        CliRun conversation = await harness.RunAsync("status", "--cache", "--turns", "--conversation", "conv-b");
        CliRun growing = await harness.RunAsync("status", "--cache", "--turns", "--trace", TurnSeeds.B3.ToUpperInvariant());
        CliRun none = await harness.RunAsync("status", "--cache", "--turns", "--trace", new string('0', 32));

        Assert.Equal(0, trace.ExitCode);
        Assert.Equal(
            """
            Model requests in trace b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1

            #  Started              Agent    Model             Input  Cache read  Cache write  Uncached  Output   Hit %  Growth
            1  2026-11-21 05:00:00  (none)   claude-opus-5.5  10,000           0        9,000     1,000     500    0.0%       -
            2  2026-11-21 05:00:05  explore  mai-code-2        3,000         n/a          n/a       n/a     100     n/a       -
            3  2026-11-21 05:00:10  explore  mai-code-2        5,000       2,000            0     3,000     100   40.0%  +2,000
            4  2026-11-21 05:00:10  (none)   claude-opus-5.5  12,000       9,000        2,500       500     500   75.0%  +2,000
            5  2026-11-21 05:00:20  (none)   claude-opus-5.5  11,000      11,000            0         0     500  100.0%  -1,000
            6  2026-11-21 05:00:30  (none)   claude-opus-5.5  20,000         n/a          n/a       n/a     500     n/a     n/a

            Growth is the change in input from the previous request by the same agent invocation and model; - marks its first request.
            n/a growth: the request's agent invocation isn't recorded, so it isn't compared with others.
            2 of 6 requests reported no cache counters.

            """.ReplaceLineEndings("\n"),
            trace.Out);
        Assert.Equal(0, conversation.ExitCode);
        Assert.Equal(
            """
            Model requests in conversation conv-b

            #  Started              Agent    Model             Input  Cache read  Cache write  Uncached  Output   Hit %  Growth  Trace
            1  2026-11-21 05:00:00  (none)   claude-opus-5.5  10,000           0        9,000     1,000     500    0.0%       -  b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1
            2  2026-11-21 05:00:05  explore  mai-code-2        3,000         n/a          n/a       n/a     100     n/a       -  b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1
            3  2026-11-21 05:00:10  explore  mai-code-2        5,000       2,000            0     3,000     100   40.0%  +2,000  b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1
            4  2026-11-21 05:00:10  (none)   claude-opus-5.5  12,000       9,000        2,500       500     500   75.0%  +2,000  b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1
            5  2026-11-21 05:00:20  (none)   claude-opus-5.5  11,000      11,000            0         0     500  100.0%  -1,000  b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1
            6  2026-11-21 05:00:30  (none)   claude-opus-5.5  20,000         n/a          n/a       n/a     500     n/a     n/a  b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1
            7  2026-11-21 05:05:00  (none)   claude-opus-5.5  15,000      10,000            0     5,000     500   66.7%       -  b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2

            Growth is the change in input from the previous request by the same agent invocation and model; - marks its first request.
            n/a growth: the request's agent invocation isn't recorded, so it isn't compared with others.
            2 of 7 requests reported no cache counters.

            """.ReplaceLineEndings("\n"),
            conversation.Out);
        Assert.Equal(0, growing.ExitCode);
        Assert.Equal(
            """
            Model requests in trace b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3

            #  Started              Agent   Model             Input  Cache read  Cache write  Uncached  Output  Hit %   Growth
            1  2026-11-21 06:00:00  (none)  claude-opus-5.5  20,000       5,000            0    15,000     500  25.0%        -
            2  2026-11-21 06:01:00  (none)  claude-opus-5.5  30,000       5,000            0    25,000     500  16.7%  +10,000

            Growth is the change in input from the previous request by the same agent invocation and model; - marks its first request.

            """.ReplaceLineEndings("\n"),
            growing.Out);
        Assert.Equal(0, none.ExitCode);
        Assert.Equal("No model requests recorded for trace 00000000000000000000000000000000.\n", none.Out);
    }

    [Fact]
    public async Task CR8_TurnsJson_HasEveryRequestsCounterStatesAndDelta()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await TurnHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("status", "--cache", "--turns", "--trace", TurnSeeds.B1, "--json");

        Assert.Equal(0, run.ExitCode);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        Assert.Equal(TurnSeeds.B1, json.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("conversationId").ValueKind);
        JsonElement[] turns = json.RootElement.GetProperty("turns").EnumerateArray().ToArray();
        Assert.Equal(6, turns.Length);
        Assert.Equal(
            ["turn", "traceId", "spanId", "parentSpanId", "conversationId", "agentSpanId", "startTime", "repository", "branch", "pullRequest", "agent", "model", "inputTokens", "outputTokens", "cacheReadState", "cacheWriteState", "cacheReadInputTokens", "cacheWriteInputTokens", "uncachedInputTokens", "cacheHitRatio", "inputDeltaFromPreviousTurn"],
            turns[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal([1, 2, 3, 4, 5, 6], turns.Select(turn => turn.GetProperty("turn").GetInt32()));
        Assert.Equal(
            ["absent", "absent", "reported", "reported", "reported", "absent"],
            turns.Select(turn => turn.GetProperty("cacheReadState").GetString()));
        Assert.Equal(
            ["reported", "absent", "absent", "reported", "absent", "absent"],
            turns.Select(turn => turn.GetProperty("cacheWriteState").GetString()));
        Assert.Equal(
            [null, null, 2_000L, 2_000L, -1_000L, null],
            turns.Select(turn => turn.GetProperty("inputDeltaFromPreviousTurn") is { ValueKind: JsonValueKind.Number } delta
                ? (long?)delta.GetInt64()
                : null));
        Assert.Equal(42, turns[0].GetProperty("pullRequest").GetInt32());
        Assert.Equal("2026-11-21T10:00:00.0000000+00:00", turns[0].GetProperty("startTime").GetString());
        Assert.Equal(0m, turns[0].GetProperty("cacheHitRatio").GetDecimal());
        Assert.Equal(9_000, turns[0].GetProperty("cacheWriteInputTokens").GetInt64());
        Assert.Equal(JsonValueKind.Null, turns[1].GetProperty("cacheReadInputTokens").ValueKind);
        Assert.Equal(JsonValueKind.Null, turns[1].GetProperty("cacheHitRatio").ValueKind);
        Assert.Equal("explore", turns[1].GetProperty("agent").GetString());
        Assert.Equal(0.75m, turns[3].GetProperty("cacheHitRatio").GetDecimal());
        Assert.Equal(JsonValueKind.Null, turns[5].GetProperty("agentSpanId").ValueKind);
    }

    [Fact]
    public async Task CR9_Bloat_RanksChainsAndLabelsTheEstimate()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await TurnHarnessAsync(temp.Path);

        CliRun all = await harness.RunAsync("status", "--cache", "--bloat");
        CliRun one = await harness.RunAsync("status", "--cache", "--bloat", "--limit", "1", "--repo", "swigerb/copilot-ledger");
        CliRun empty = await harness.RunAsync("status", "--cache", "--bloat", "--repo", "contoso/webapp");

        Assert.Equal(0, all.ExitCode);
        Assert.Equal(
            """
            Context bloat in all repositories (last 30 days)

            Prompt chains by uncached input
            #  Started           Repository              Spans   Input  Uncached  Hit %  Trace
            1  2026-11-21 06:00  swigerb/copilot-ledger      2  50,000    40,000  20.0%  b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3
            2  2026-11-21 07:00  (none)                      1   5,000     5,000   0.0%  b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0
            3  2026-11-21 05:05  swigerb/copilot-ledger      1  15,000     5,000  66.7%  b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2

            Prompt chains by input growth within one agent invocation and model
            #  Started           Repository              Agent   Model            Requests  First input  Peak input  Growth  Trace
            1  2026-11-21 06:00  swigerb/copilot-ledger  (none)  claude-opus-5.5         2       20,000      30,000  10,000  b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3
            2  2026-11-21 05:00  swigerb/copilot-ledger  (none)  claude-opus-5.5         3       10,000      12,000   2,000  b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1

            Prompt chains with requests that reported no cache counters, by estimated uncached input
            #  Started           Repository              Spans  Cache reported   Input  Est. uncached  Trace
            1  2026-11-21 05:00  swigerb/copilot-ledger      6               4  61,000         27,500  b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1

            Est. uncached is an estimate: it counts all input of a request without cache counters as uncached, as pricing does.

            Input growth leaves out 2 requests without a recorded agent invocation.

            """.ReplaceLineEndings("\n"),
            all.Out);
        Assert.Equal(0, one.ExitCode);
        Assert.Equal(
            """
            Context bloat in swigerb/copilot-ledger (last 30 days)

            Prompt chains by uncached input
            #  Started           Repository              Spans   Input  Uncached  Hit %  Trace
            1  2026-11-21 06:00  swigerb/copilot-ledger      2  50,000    40,000  20.0%  b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3

            Prompt chains by input growth within one agent invocation and model
            #  Started           Repository              Agent   Model            Requests  First input  Peak input  Growth  Trace
            1  2026-11-21 06:00  swigerb/copilot-ledger  (none)  claude-opus-5.5         2       20,000      30,000  10,000  b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3b3

            Prompt chains with requests that reported no cache counters, by estimated uncached input
            #  Started           Repository              Spans  Cache reported   Input  Est. uncached  Trace
            1  2026-11-21 05:00  swigerb/copilot-ledger      6               4  61,000         27,500  b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1

            Est. uncached is an estimate: it counts all input of a request without cache counters as uncached, as pricing does.

            Input growth leaves out 1 request without a recorded agent invocation.

            """.ReplaceLineEndings("\n"),
            one.Out);
        Assert.Equal(0, empty.ExitCode);
        Assert.Equal(
            """
            Context bloat in contoso/webapp (last 30 days)

            Prompt chains by uncached input
            None.

            Prompt chains by input growth within one agent invocation and model
            None.

            Prompt chains with requests that reported no cache counters, by estimated uncached input
            None.

            """.ReplaceLineEndings("\n"),
            empty.Out);
    }

    [Fact]
    public async Task CR10_BloatJson_HasTheThreeRankings()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await TurnHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("status", "--cache", "--bloat", "--json");

        Assert.Equal(0, run.ExitCode);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        JsonElement root = json.RootElement;
        Assert.Equal(
            ["period", "from", "repository", "limit", "byUncachedInput", "byInputGrowth", "byEstimatedUncachedInput", "spansWithoutAgent"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(10, root.GetProperty("limit").GetInt32());
        Assert.Equal(2, root.GetProperty("spansWithoutAgent").GetInt64());
        JsonElement[] uncached = root.GetProperty("byUncachedInput").EnumerateArray().ToArray();
        Assert.Equal(
            [TurnSeeds.B3, TurnSeeds.B0, TurnSeeds.B2],
            uncached.Select(row => row.GetProperty("traceId").GetString()));
        Assert.Equal(
            ["rank", "traceId", "conversationId", "startTime", "repository", "branch", "spans", "cacheReportedSpans", "cacheUnreportedSpans", "inputTokens", "reportedInputTokens", "reportedCacheReadInputTokens", "uncachedInputTokens", "estimatedUncachedInputTokens", "cacheHitRatio"],
            uncached[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(40_000, uncached[0].GetProperty("uncachedInputTokens").GetInt64());
        Assert.Equal(0.2m, uncached[0].GetProperty("cacheHitRatio").GetDecimal());
        JsonElement[] growth = root.GetProperty("byInputGrowth").EnumerateArray().ToArray();
        Assert.Equal(
            ["rank", "traceId", "conversationId", "agentSpanId", "agent", "model", "repository", "branch", "startTime", "turns", "firstInputTokens", "peakInputTokens", "inputGrowthTokens"],
            growth[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal([10_000L, 2_000L], growth.Select(row => row.GetProperty("inputGrowthTokens").GetInt64()));
        JsonElement estimated = Assert.Single(root.GetProperty("byEstimatedUncachedInput").EnumerateArray());
        Assert.Equal(TurnSeeds.B1, estimated.GetProperty("traceId").GetString());
        Assert.Equal(2, estimated.GetProperty("cacheUnreportedSpans").GetInt64());
        Assert.Equal(4_500, estimated.GetProperty("uncachedInputTokens").GetInt64());
        Assert.Equal(27_500, estimated.GetProperty("estimatedUncachedInputTokens").GetInt64());
        Assert.Equal(0.5789473684210526315789473684m, estimated.GetProperty("cacheHitRatio").GetDecimal());
    }

    public static TheoryData<string, string> InvalidViews => new()
    {
        { "status --turns", "--turns requires --cache." },
        { "status --cache --turns", "Use --turns with either --trace <traceId> or --conversation <id>." },
        { "status --cache --turns --trace b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1 --conversation conv-b", "Use --turns with either --trace <traceId> or --conversation <id>." },
        { "status --cache --turns --trace b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1 --by model", "--by can't be used with --turns." },
        { "status --cache --turns --conversation conv-b --since 7d", "--since can't be used with --turns." },
        { "status --cache --turns --trace xyz", "--trace must be a 32-character hexadecimal trace ID." },
        { "status --cache --trace b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1", "--trace requires --turns." },
        { "status --cache --conversation conv-b", "--conversation requires --turns." },
        { "status --cache --turns --bloat", "Use either --turns or --bloat, not both." },
        { "status --cache --bloat --by model", "--by can't be used with --bloat." },
        { "status --cache --bloat --pr 4", "--pr can't be used with --bloat." },
        { "status --cache --limit 5", "--limit requires --bloat." },
        { "status --cache --bloat --limit 0", "--limit must be from 1 to 100." },
        { "status --cache --bloat --limit 101", "--limit must be from 1 to 100." },
    };

    [Theory]
    [MemberData(nameof(InvalidViews))]
    public async Task CR11_TurnAndBloatOptionsAreValidated(string command, string message)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);

        CliRun run = await harness.RunAsync(command.Split(' '));

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message, run.Error);
    }

    private static async Task<CliHarness> TurnHarnessAsync(string root)
    {
        CliHarness harness = new(root, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        await TurnSeeds.SeedAsync(store, root);
        return harness;
    }

    private static async Task<CliHarness> SeededHarnessAsync(string root)
    {
        string git = Path.Combine(root, "repo", ".git");
```

### Tests

All over `TurnSeeds` (Task 6) at 2026-11-21 15:00 UTC, shown in UTC-5.

- **CR7:** the exact `--turns` tables for trace B1 (growth `-`, `-`, `+2,000`, `+2,000`, `-1,000`, `n/a`; ratios `0.0%`, `n/a`, `40.0%`, `75.0%`, `100.0%`, `n/a`; all three footnotes), conversation `conv-b` (with the `Trace` column and B2's request as its own first, `-`), trace B3 given in uppercase (`+10,000`, only the growth footnote), and an unknown trace (`No model requests recorded for trace 000…0.`).
- **CR8:** `--turns --json` for B1: the 21 property names in order, turn numbers 1–6, read states `absent, absent, reported, reported, reported, absent`, write states `reported, absent, absent, reported, absent, absent`, deltas `null, null, 2000, 2000, -1000, null`, pull request 42, ratio `0` on the first turn and `0.75` on the fourth, and `null` cache values and agent where unknown.
- **CR9:** the exact `--bloat` report (B3, B0, B2 by uncached input; B3 and B1 by growth; B1 by estimate; both footnotes, with `2 requests`), with `--limit 1 --repo swigerb/copilot-ledger` (one row each, `1 request`), and for a repository without spans (`None.` three times).
- **CR10:** `--bloat --json`: the root and row property names in order, `limit` 10, `spansWithoutAgent` 2, observed uncached 40,000 and ratio `0.2` for B3, growth 10,000 and 2,000, and B1's estimate row (2 unreported spans, observed 4,500, estimate 27,500, ratio `0.5789473684210526315789473684`).
- **CR11** (14 cases): every rule and message above, plus `--trace xyz` and `--limit` 0 and 101.

### Acceptance criteria

- `--turns` lists inference spans in start order with per-request values and `InputDeltaFromPreviousTurn`; `--bloat` ranks deterministically, ties by trace ID, with unknown totals ranked separately and the estimate labeled.
- `status --cache` without `--turns` or `--bloat` behaves as in Task 8, and CR1–CR6 pass unchanged.
- `dotnet test CopilotLedger.slnx` passes **786** tests (768 + 18).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 10 specification: MCP get_cache_efficiency

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/LedgerPeriod.cs` | New: the `--since` grammar, moved from the CLI |
| `src/CopilotLedger.Cli/Reporting/Period.cs` | Delegates to `LedgerPeriod.TryParse`; same signature and results |
| `src/CopilotLedger.Mcp/LedgerMcpTools.cs` | New tool `get_cache_efficiency`; `get_branch_ledger` and `get_agent_breakdown` add three fields; `ResolveCurrent` |
| `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs` | MC1, MC2, MC4, MC6, and MC11 expectations; 13 new MC8 cases; new MC13 and MC14 and `SeedPullRequestAsync` |
| `docs/guide.md` | The `mcp` sample gains the three fields |
| `docs/cli.md` | "MCP server" lists five tools |

### Contracts

```csharp
namespace CopilotLedger.Storage;

public sealed record LedgerPeriod(string Label, DateTimeOffset? From)
{
    // today, all, Nd (1–3650, counting today), or yyyy-MM-dd, ignoring case and surrounding white space; periods start at
    // local midnight in timeProvider's time zone. Labels: "today", "all time", "last N days", "since yyyy-MM-dd".
    public static bool TryParse(string? value, TimeProvider timeProvider, [NotNullWhen(true)] out LedgerPeriod? period);
}
```

Tool `get_cache_efficiency` (fourth in `tools/list`, before `set_branch_budget`):

- Title `Cache efficiency`; annotations `ReadOnlyHint = true`, `OpenWorldHint = false`; the description and input schema are in the code below.
- Arguments: `repository` (non-empty string), `branch` (non-empty string), `pullRequest` (integer ≥ 1), `since` (string, default `30d`), `by` (`model`, `branch`, `pr`, or `agent`, default `model`), and `limit` (integer 1–50, default 10). Any other argument is rejected with "Unknown argument: <name>." (the alphabetically first one).
- Messages: "repository must be a non-empty string.", "branch must be a non-empty string.", "pullRequest must be an integer of at least 1.", "since must be today, all, <N>d (1-3650), or yyyy-MM-dd.", "by must be model, branch, pr, or agent.", and "limit must be an integer from 1 to 50.".
- Scope: `repository`, or else the current repository (the slug, or the root without one) of the session directory, found the same way the other tools find it (`ResolveCurrent`). With neither: "No Git repository at <directory>. Pass repository.". `branch` and `pullRequest` filter independently.
- Result: `repository`, `branch`, `pullRequest`, `period` (label), `from` (round-trip UTC, or `null`), `by`, `limit`, `rows` (the first `limit` rows of `GetCacheEfficiencyAsync`, each with the fields of `status --cache --json`), `circuitBreaker`, and `warnings`. The warnings and trips are those of the current branch, or, outside a repository, only the current session's trips; the text content starts with one `WARNING:` line each, as for the other tools.

`get_branch_ledger` and `get_agent_breakdown` add, after `cacheWriteInputTokens`: `uncachedInputTokens` (the observed sum, or `null` with no reported span), `cacheReportedSpans`, and `cacheHitRatio` (exact, or `null`).

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Cli/Reporting/Period.cs`, replace:

```csharp
using System.Globalization;

namespace CopilotLedger.Cli.Reporting;

internal sealed record Period(string Label, DateTimeOffset? From)
{
    public static bool TryParse(
        string? value,
        CliContext context,
        out Period period)
    {
        string text = value?.Trim() ?? string.Empty;
        DateTimeOffset now = context.TimeProvider.GetUtcNow();
        TimeZoneInfo zone = context.TimeProvider.LocalTimeZone;
        DateOnly today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, zone).DateTime);

        if (text.Equals("today", StringComparison.OrdinalIgnoreCase))
        {
            period = new Period("today", Midnight(today, zone));
            return true;
        }

        if (text.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            period = new Period("all time", null);
            return true;
        }

        if (text.EndsWith("d", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(
                text[..^1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int days) &&
            days is >= 1 and <= 3650)
        {
            period = new Period(
                days == 1 ? "today" : $"last {days} days",
                Midnight(today.AddDays(-(days - 1)), zone));
            return true;
        }

        if (DateOnly.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly date))
        {
            period = new Period($"since {text}", Midnight(date, zone));
            return true;
        }

        period = null!;
        return false;
    }

    private static DateTimeOffset Midnight(DateOnly date, TimeZoneInfo zone)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
```

with:

```csharp
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Reporting;

internal sealed record Period(string Label, DateTimeOffset? From)
{
    // LedgerPeriod.TryParse at the context's clock, which the MCP server's since argument shares.
    public static bool TryParse(
        string? value,
        CliContext context,
        out Period period)
    {
        if (LedgerPeriod.TryParse(value, context.TimeProvider, out LedgerPeriod? parsed))
        {
            period = new Period(parsed.Label, parsed.From);
            return true;
        }

        period = null!;
        return false;
    }
}
```

Edit 1 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
          "type": "object",
          "properties": {
            "repository": { "type": "string", "minLength": 1, "description": "Repository key: owner/repo, a repository root, or (none). Pass with branch." },
            "branch": { "type": "string", "minLength": 1, "description": "Branch name, or (none). Pass with repository." },
            "aiCredits": { "type": "number", "exclusiveMinimum": 0, "maximum": 1000000, "description": "Budget in AI credits (1 AI credit = $0.01), with at most 2 decimal places." }
          },
          "required": ["aiCredits"],
          "additionalProperties": false
        }
        """;
    private static readonly JsonElement BranchSchema = ParseSchema(BranchSchemaJson);
    private static readonly JsonElement PromptSchema = ParseSchema(PromptSchemaJson);
    private static readonly JsonElement BudgetSchema = ParseSchema(BudgetSchemaJson);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private static readonly Tool[] Tools =
    [
        new()
        {
            Name = "get_branch_ledger",
```

with:

```csharp
          "type": "object",
          "properties": {
            "repository": { "type": "string", "minLength": 1, "description": "Repository key: owner/repo, a repository root, or (none). Pass with branch." },
            "branch": { "type": "string", "minLength": 1, "description": "Branch name, or (none). Pass with repository." },
            "aiCredits": { "type": "number", "exclusiveMinimum": 0, "maximum": 1000000, "description": "Budget in AI credits (1 AI credit = $0.01), with at most 2 decimal places." }
          },
          "required": ["aiCredits"],
          "additionalProperties": false
        }
        """;
    private const string CacheSchemaJson =
        """
        {
          "type": "object",
          "properties": {
            "repository": { "type": "string", "minLength": 1, "description": "Repository key: owner/repo, a repository root, or (none). Defaults to the current repository." },
            "branch": { "type": "string", "minLength": 1, "description": "Only this branch, or (none)." },
            "pullRequest": { "type": "integer", "minimum": 1, "description": "Only this pull request number." },
            "since": { "type": "string", "default": "30d", "description": "Period: today, all, Nd (1 to 3650 days), or yyyy-MM-dd." },
            "by": { "type": "string", "enum": ["model", "branch", "pr", "agent"], "default": "model", "description": "Group rows by model, branch, pull request, or agent." },
            "limit": { "type": "integer", "minimum": 1, "maximum": 50, "default": 10, "description": "Number of rows, most input first." }
          },
          "additionalProperties": false
        }
        """;
    private static readonly JsonElement BranchSchema = ParseSchema(BranchSchemaJson);
    private static readonly JsonElement PromptSchema = ParseSchema(PromptSchemaJson);
    private static readonly JsonElement BudgetSchema = ParseSchema(BudgetSchemaJson);
    private static readonly JsonElement CacheSchema = ParseSchema(CacheSchemaJson);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private static readonly Tool[] Tools =
    [
        new()
        {
            Name = "get_branch_ledger",
```

Edit 2 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            Description = "Spend on one Git branch split by agent (gen_ai.agent.name). Uses the current repository and branch unless you pass both.",
            InputSchema = BranchSchema.Clone(),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = true,
                OpenWorldHint = false,
            },
        },
        new()
        {
            Name = "set_branch_budget",
            Title = "Set branch budget",
            Description = "Sets a soft AI-credit budget for one Git branch. CopilotLedger alerts once when the branch's billed spend exceeds it; nothing is blocked. Uses the current repository and branch unless you pass both.",
            InputSchema = BudgetSchema.Clone(),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = false,
                DestructiveHint = false,
                IdempotentHint = true,
```

with:

```csharp
            Description = "Spend on one Git branch split by agent (gen_ai.agent.name). Uses the current repository and branch unless you pass both.",
            InputSchema = BranchSchema.Clone(),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = true,
                OpenWorldHint = false,
            },
        },
        new()
        {
            Name = "get_cache_efficiency",
            Title = "Cache efficiency",
            Description = "Prompt-cache efficiency in one repository, grouped by model, branch, pull request, or agent: the cache hit ratio, input read from and written to the cache, uncached input, and the largest prompt. Spans that reported no cache counters count as unknown, never as misses. Uses the current repository unless you pass one.",
            InputSchema = CacheSchema.Clone(),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = true,
                OpenWorldHint = false,
            },
        },
        new()
        {
            Name = "set_branch_budget",
            Title = "Set branch budget",
            Description = "Sets a soft AI-credit budget for one Git branch. CopilotLedger alerts once when the branch's billed spend exceeds it; nothing is blocked. Uses the current repository and branch unless you pass both.",
            InputSchema = BudgetSchema.Clone(),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = false,
                DestructiveHint = false,
                IdempotentHint = true,
```

Edit 3 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
    public async ValueTask<CallToolResult> CallAsync(
        CallToolRequestParams request,
        CancellationToken cancellationToken)
    {
        string name = request.Name;
        string[] allowed = name switch
        {
            "get_branch_ledger" or "get_agent_breakdown" => ["repository", "branch"],
            "get_top_expensive_prompts" =>
                ["repository", "branch", "limit", "orderBy"],
            "set_branch_budget" => ["repository", "branch", "aiCredits"],
            _ => throw new McpProtocolException(
                $"Unknown tool: {name}",
                McpErrorCode.InvalidParams),
        };
        IDictionary<string, JsonElement> arguments =
            request.Arguments ?? new Dictionary<string, JsonElement>();
        CallToolResult? invalid = Validate(name, allowed, arguments, out ToolArguments values);
        if (invalid is not null)
        {
            return invalid;
        }

        ScopeResult scope = ResolveScope(values.Repository, values.Branch);
        if (scope.Error is not null)
        {
            return Error(scope.Error);
        }

        try
        {
            SqliteLedgerStore store = await OpenStoreAsync(cancellationToken);
            JsonObject result = name switch
            {
                "get_branch_ledger" => await BranchLedgerAsync(
                    store,
                    scope.Repository!,
```

with:

```csharp
    public async ValueTask<CallToolResult> CallAsync(
        CallToolRequestParams request,
        CancellationToken cancellationToken)
    {
        string name = request.Name;
        string[] allowed = name switch
        {
            "get_branch_ledger" or "get_agent_breakdown" => ["repository", "branch"],
            "get_top_expensive_prompts" =>
                ["repository", "branch", "limit", "orderBy"],
            "get_cache_efficiency" =>
                ["repository", "branch", "pullRequest", "since", "by", "limit"],
            "set_branch_budget" => ["repository", "branch", "aiCredits"],
            _ => throw new McpProtocolException(
                $"Unknown tool: {name}",
                McpErrorCode.InvalidParams),
        };
        IDictionary<string, JsonElement> arguments =
            request.Arguments ?? new Dictionary<string, JsonElement>();
        CallToolResult? invalid = Validate(
            name,
            allowed,
            arguments,
            _context.TimeProvider,
            out ToolArguments values);
        if (invalid is not null)
        {
            return invalid;
        }

        // get_cache_efficiency reports one repository, the current one by default, and warns about the current branch.
        GitWorkspaceContext? current = null;
        ScopeResult scope;
        if (name == "get_cache_efficiency")
        {
            (string directory, current) = ResolveCurrent();
            string? repository = values.Repository ?? current?.RepositorySlug ?? current?.RepositoryRoot;
            if (repository is null)
            {
                return Error($"No Git repository at {directory}. Pass repository.");
            }

            scope = new ScopeResult(repository, values.Branch, null);
        }
        else
        {
            scope = ResolveScope(values.Repository, values.Branch);
            if (scope.Error is not null)
            {
                return Error(scope.Error);
            }
        }

        try
        {
            SqliteLedgerStore store = await OpenStoreAsync(cancellationToken);
            JsonObject result = name switch
            {
                "get_branch_ledger" => await BranchLedgerAsync(
                    store,
                    scope.Repository!,
```

Edit 4 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
                    scope.Branch!,
                    values.OrderBy,
                    values.Limit,
                    cancellationToken),
                "get_agent_breakdown" => await AgentBreakdownAsync(
                    store,
                    scope.Repository!,
                    scope.Branch!,
                    cancellationToken),
                "set_branch_budget" => await SetBudgetAsync(
                    store,
                    scope.Repository!,
                    scope.Branch!,
                    values.AiCredits,
                    cancellationToken),
                _ => throw new UnreachableException(),
```

with:

```csharp
                    scope.Branch!,
                    values.OrderBy,
                    values.Limit,
                    cancellationToken),
                "get_agent_breakdown" => await AgentBreakdownAsync(
                    store,
                    scope.Repository!,
                    scope.Branch!,
                    cancellationToken),
                "get_cache_efficiency" => await CacheEfficiencyAsync(
                    store,
                    scope.Repository!,
                    current,
                    values,
                    cancellationToken),
                "set_branch_budget" => await SetBudgetAsync(
                    store,
                    scope.Repository!,
                    scope.Branch!,
                    values.AiCredits,
                    cancellationToken),
                _ => throw new UnreachableException(),
```

Edit 5 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            or InvalidOperationException)
        {
            return Error(
                $"The ledger at {_context.DatabasePath} is unavailable: {exception.Message}");
        }
    }

    private static CallToolResult? Validate(
        string name,
        IReadOnlyCollection<string> allowed,
        IDictionary<string, JsonElement> arguments,
        out ToolArguments values)
    {
        values = ToolArguments.Empty;
        string? unknown = arguments.Keys
            .Where(key => !allowed.Contains(key))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        if (unknown is not null)
        {
            return Error($"Unknown argument: {unknown}.");
```

with:

```csharp
            or InvalidOperationException)
        {
            return Error(
                $"The ledger at {_context.DatabasePath} is unavailable: {exception.Message}");
        }
    }

    private static CallToolResult? Validate(
        string name,
        IReadOnlyCollection<string> allowed,
        IDictionary<string, JsonElement> arguments,
        TimeProvider timeProvider,
        out ToolArguments values)
    {
        values = ToolArguments.Empty;
        string? unknown = arguments.Keys
            .Where(key => !allowed.Contains(key))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        if (unknown is not null)
        {
            return Error($"Unknown argument: {unknown}.");
```

Edit 6 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
        if (arguments.TryGetValue("branch", out JsonElement branchElement))
        {
            if (branchElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(branchElement.GetString()))
            {
                return Error("branch must be a non-empty string.");
            }

            branch = branchElement.GetString();
        }

        if ((repository is null) != (branch is null))
        {
            return Error("Pass repository and branch together, or neither.");
        }

        int limit = 5;
        if (arguments.TryGetValue("limit", out JsonElement limitElement)
            && (limitElement.ValueKind != JsonValueKind.Number
                || !limitElement.TryGetInt32(out limit)
                || limit is < 1 or > 20))
        {
            return Error("limit must be an integer from 1 to 20.");
        }

        string orderBy = "cost";
        if (arguments.TryGetValue("orderBy", out JsonElement orderByElement))
        {
            if (orderByElement.ValueKind != JsonValueKind.String)
            {
                return Error("orderBy must be cost, outputTokens, or failedToolCalls.");
            }

            string? requested = orderByElement.GetString();
```

with:

```csharp
        if (arguments.TryGetValue("branch", out JsonElement branchElement))
        {
            if (branchElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(branchElement.GetString()))
            {
                return Error("branch must be a non-empty string.");
            }

            branch = branchElement.GetString();
        }

        bool cacheTool = name == "get_cache_efficiency";
        if (!cacheTool && (repository is null) != (branch is null))
        {
            return Error("Pass repository and branch together, or neither.");
        }

        int maximum = cacheTool ? 50 : 20;
        int limit = cacheTool ? 10 : 5;
        if (arguments.TryGetValue("limit", out JsonElement limitElement)
            && (limitElement.ValueKind != JsonValueKind.Number
                || !limitElement.TryGetInt32(out limit)
                || limit < 1
                || limit > maximum))
        {
            return Error($"limit must be an integer from 1 to {maximum}.");
        }

        string orderBy = "cost";
        if (arguments.TryGetValue("orderBy", out JsonElement orderByElement))
        {
            if (orderByElement.ValueKind != JsonValueKind.String)
            {
                return Error("orderBy must be cost, outputTokens, or failedToolCalls.");
            }

            string? requested = orderByElement.GetString();
```

Edit 7 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            && (creditElement.ValueKind != JsonValueKind.Number
                || !creditElement.TryGetDecimal(out aiCredits)
                || aiCredits <= 0m
                || aiCredits > 1_000_000m
                || aiCredits % 0.01m != 0m))
        {
            return Error(
                "aiCredits must be a number greater than 0 and at most 1000000, with at most 2 decimal places.");
        }

        values = new ToolArguments(repository, branch, limit, orderBy, aiCredits);
        return null;
    }

    private ScopeResult ResolveScope(string? repository, string? branch)
    {
        if (repository is not null && branch is not null)
        {
            return new ScopeResult(repository, branch, null);
        }

        string sessionDirectory =
            _context.GetEnvironmentVariable("COPILOT_SESSION_CWD") ?? string.Empty;
        string directory =
            !string.IsNullOrWhiteSpace(sessionDirectory)
            && Path.IsPathFullyQualified(sessionDirectory)
            && Directory.Exists(sessionDirectory)
                ? sessionDirectory
                : _context.WorkingDirectory;
```

with:

```csharp
            && (creditElement.ValueKind != JsonValueKind.Number
                || !creditElement.TryGetDecimal(out aiCredits)
                || aiCredits <= 0m
                || aiCredits > 1_000_000m
                || aiCredits % 0.01m != 0m))
        {
            return Error(
                "aiCredits must be a number greater than 0 and at most 1000000, with at most 2 decimal places.");
        }

        int? pullRequest = null;
        if (arguments.TryGetValue("pullRequest", out JsonElement pullRequestElement))
        {
            if (pullRequestElement.ValueKind != JsonValueKind.Number
                || !pullRequestElement.TryGetInt32(out int number)
                || number < 1)
            {
                return Error("pullRequest must be an integer of at least 1.");
            }

            pullRequest = number;
        }

        LedgerPeriod? period = null;
        if (cacheTool)
        {
            string since = "30d";
            if (arguments.TryGetValue("since", out JsonElement sinceElement))
            {
                since = sinceElement.ValueKind == JsonValueKind.String
                    ? sinceElement.GetString()!
                    : string.Empty;
            }

            if (!LedgerPeriod.TryParse(since, timeProvider, out period))
            {
                return Error("since must be today, all, <N>d (1-3650), or yyyy-MM-dd.");
            }
        }

        string by = "model";
        if (arguments.TryGetValue("by", out JsonElement byElement))
        {
            string? requested = byElement.ValueKind == JsonValueKind.String
                ? byElement.GetString()
                : null;
            if (requested is not ("model" or "branch" or "pr" or "agent"))
            {
                return Error("by must be model, branch, pr, or agent.");
            }

            by = requested;
        }

        values = new ToolArguments(repository, branch, limit, orderBy, aiCredits, pullRequest, period, by);
        return null;
    }

    private ScopeResult ResolveScope(string? repository, string? branch)
    {
        if (repository is not null && branch is not null)
        {
            return new ScopeResult(repository, branch, null);
        }

        (string directory, GitWorkspaceContext? git) = ResolveCurrent();
        return git is null
            ? new ScopeResult(
                null,
                null,
                $"No Git repository at {directory}. Pass repository and branch.")
            : new ScopeResult(
                git.RepositorySlug ?? git.RepositoryRoot,
                git.BranchName ?? "(none)",
                null);
    }

    // The session's directory, COPILOT_SESSION_CWD when it's a full path to a directory or else the server's, and its Git
    // context, null outside a repository.
    private (string Directory, GitWorkspaceContext? Git) ResolveCurrent()
    {
        string sessionDirectory =
            _context.GetEnvironmentVariable("COPILOT_SESSION_CWD") ?? string.Empty;
        string directory =
            !string.IsNullOrWhiteSpace(sessionDirectory)
            && Path.IsPathFullyQualified(sessionDirectory)
            && Directory.Exists(sessionDirectory)
                ? sessionDirectory
                : _context.WorkingDirectory;
```

Edit 8 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
        try
        {
            git = _context.GitResolver.Resolve(directory);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
        }

        return git is null
            ? new ScopeResult(
                null,
                null,
                $"No Git repository at {directory}. Pass repository and branch.")
            : new ScopeResult(
                git.RepositorySlug ?? git.RepositoryRoot,
                git.BranchName ?? "(none)",
                null);
    }

    private async Task<SqliteLedgerStore> OpenStoreAsync(
        CancellationToken cancellationToken)
    {
        await _storeGate.WaitAsync(cancellationToken);
        try
        {
            if (_store is null)
            {
                SqliteLedgerStore store = new(
```

with:

```csharp
        try
        {
            git = _context.GitResolver.Resolve(directory);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
        }

        return (directory, git);
    }

    private async Task<SqliteLedgerStore> OpenStoreAsync(
        CancellationToken cancellationToken)
    {
        await _storeGate.WaitAsync(cancellationToken);
        try
        {
            if (_store is null)
            {
                SqliteLedgerStore store = new(
```

Edit 9 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            ["repository"] = repository,
            ["branch"] = branch,
            ["spans"] = total.SpanCount,
            ["unpricedSpans"] = total.UnpricedSpanCount,
            ["inputTokens"] = total.InputTokens,
            ["outputTokens"] = total.OutputTokens,
            ["cacheReadInputTokens"] = total.CacheReadInputTokens,
            ["cacheWriteInputTokens"] = total.CacheWriteInputTokens,
            ["aiCredits"] = Money(total.AiCredits),
            ["aiCreditsUsd"] = Money(total.AiCreditsUsd),
            ["foundryByokUsd"] = Money(total.FoundryByokUsd),
            ["localSavedUsd"] = Money(total.LocalSavedUsd),
            ["billedAiCredits"] = Money(
                BilledAiCredits(total.AiCredits, total.FoundryByokUsd)),
            ["topModel"] = models.FirstOrDefault()?.Model,
            ["budget"] = warnings.Budget,
```

with:

```csharp
            ["repository"] = repository,
            ["branch"] = branch,
            ["spans"] = total.SpanCount,
            ["unpricedSpans"] = total.UnpricedSpanCount,
            ["inputTokens"] = total.InputTokens,
            ["outputTokens"] = total.OutputTokens,
            ["cacheReadInputTokens"] = total.CacheReadInputTokens,
            ["cacheWriteInputTokens"] = total.CacheWriteInputTokens,
            ["uncachedInputTokens"] = Uncached(total),
            ["cacheReportedSpans"] = total.CacheReportedSpanCount,
            ["cacheHitRatio"] = Ratio(total.CacheHitRatio),
            ["aiCredits"] = Money(total.AiCredits),
            ["aiCreditsUsd"] = Money(total.AiCreditsUsd),
            ["foundryByokUsd"] = Money(total.FoundryByokUsd),
            ["localSavedUsd"] = Money(total.LocalSavedUsd),
            ["billedAiCredits"] = Money(
                BilledAiCredits(total.AiCredits, total.FoundryByokUsd)),
            ["topModel"] = models.FirstOrDefault()?.Model,
            ["budget"] = warnings.Budget,
```

Edit 10 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
            cancellationToken);
        JsonArray agents = [];
        foreach (LedgerRollupRow row in rows)
        {
            agents.Add(new JsonObject
            {
                ["agent"] = row.Agent,
                ["spans"] = row.SpanCount,
                ["unpricedSpans"] = row.UnpricedSpanCount,
                ["inputTokens"] = row.InputTokens,
                ["outputTokens"] = row.OutputTokens,
                ["cacheReadInputTokens"] = row.CacheReadInputTokens,
                ["cacheWriteInputTokens"] = row.CacheWriteInputTokens,
                ["aiCredits"] = Money(row.AiCredits),
                ["aiCreditsUsd"] = Money(row.AiCreditsUsd),
                ["foundryByokUsd"] = Money(row.FoundryByokUsd),
                ["localSavedUsd"] = Money(row.LocalSavedUsd),
                ["billedAiCredits"] = Money(
                    BilledAiCredits(row.AiCredits, row.FoundryByokUsd)),
            });
        }

        return new JsonObject
        {
            ["repository"] = repository,
            ["branch"] = branch,
            ["agents"] = agents,
            ["circuitBreaker"] = warnings.CircuitBreaker,
            ["warnings"] = new JsonArray(
                warnings.Warnings.Select(message => JsonValue.Create(message)).ToArray()),
        };
    }

    private async Task<JsonObject> SetBudgetAsync(
        SqliteLedgerStore store,
        string repository,
        string branch,
        decimal aiCredits,
```

with:

```csharp
            cancellationToken);
        JsonArray agents = [];
        foreach (LedgerRollupRow row in rows)
        {
            agents.Add(new JsonObject
            {
                ["agent"] = row.Agent,
                ["spans"] = row.SpanCount,
                ["unpricedSpans"] = row.UnpricedSpanCount,
                ["inputTokens"] = row.InputTokens,
                ["outputTokens"] = row.OutputTokens,
                ["cacheReadInputTokens"] = row.CacheReadInputTokens,
                ["cacheWriteInputTokens"] = row.CacheWriteInputTokens,
                ["uncachedInputTokens"] = Uncached(row),
                ["cacheReportedSpans"] = row.CacheReportedSpanCount,
                ["cacheHitRatio"] = Ratio(row.CacheHitRatio),
                ["aiCredits"] = Money(row.AiCredits),
                ["aiCreditsUsd"] = Money(row.AiCreditsUsd),
                ["foundryByokUsd"] = Money(row.FoundryByokUsd),
                ["localSavedUsd"] = Money(row.LocalSavedUsd),
                ["billedAiCredits"] = Money(
                    BilledAiCredits(row.AiCredits, row.FoundryByokUsd)),
            });
        }

        return new JsonObject
        {
            ["repository"] = repository,
            ["branch"] = branch,
            ["agents"] = agents,
            ["circuitBreaker"] = warnings.CircuitBreaker,
            ["warnings"] = new JsonArray(
                warnings.Warnings.Select(message => JsonValue.Create(message)).ToArray()),
        };
    }

    // current is the session's Git context, whose branch the warnings describe; without one, they come only from trips of
    // the current Copilot session.
    private async Task<JsonObject> CacheEfficiencyAsync(
        SqliteLedgerStore store,
        string repository,
        GitWorkspaceContext? current,
        ToolArguments values,
        CancellationToken cancellationToken)
    {
        LedgerGrouping grouping = values.By switch
        {
            "branch" => LedgerGrouping.Branch,
            "pr" => LedgerGrouping.PullRequest,
            "agent" => LedgerGrouping.Agent,
            _ => LedgerGrouping.Model,
        };
        IReadOnlyList<CacheEfficiencyRow> rows = await store.GetCacheEfficiencyAsync(
            grouping,
            new LedgerFilter
            {
                From = values.Period!.From,
                Repository = repository,
                Branch = values.Branch,
                PullRequestNumber = values.PullRequest,
            },
            cancellationToken);
        WarningResult warnings = await WarningsAsync(
            store,
            current is null ? null : current.RepositorySlug ?? current.RepositoryRoot,
            current is null ? null : current.BranchName ?? "(none)",
            cancellationToken);
        JsonArray resultRows = [];
        foreach (CacheEfficiencyRow row in rows.Take(values.Limit))
        {
            JsonObject item = grouping switch
            {
                LedgerGrouping.Model => new JsonObject { ["model"] = row.Model },
                LedgerGrouping.Agent => new JsonObject { ["agent"] = row.Agent },
                LedgerGrouping.Branch => new JsonObject
                {
                    ["repository"] = row.Repository,
                    ["branch"] = row.Branch,
                },
                _ => new JsonObject
                {
                    ["repository"] = row.Repository,
                    ["pullRequest"] = row.PullRequestNumber,
                    ["branch"] = row.Branch,
                },
            };
            item["spans"] = row.SpanCount;
            item["cacheReportedSpans"] = row.CacheReportedSpanCount;
            item["cacheUnreportedSpans"] = row.CacheUnreportedSpanCount;
            item["inputTokens"] = row.InputTokens;
            item["outputTokens"] = row.OutputTokens;
            item["maxInputTokens"] = row.MaxInputTokens;
            item["averageInputTokensPerTurn"] = Ratio(row.AverageInputTokensPerTurn);
            item["reportedInputTokens"] = row.ReportedInputTokens;
            item["reportedCacheReadInputTokens"] = row.ReportedCacheReadInputTokens;
            item["reportedCacheWriteInputTokens"] = row.ReportedCacheWriteInputTokens;
            item["uncachedInputTokens"] = row.UncachedInputTokens;
            item["estimatedUncachedInputTokens"] = row.EstimatedUncachedInputTokens;
            item["cacheHitRatio"] = Ratio(row.CacheHitRatio);
            resultRows.Add(item);
        }

        return new JsonObject
        {
            ["repository"] = repository,
            ["branch"] = values.Branch,
            ["pullRequest"] = values.PullRequest,
            ["period"] = values.Period.Label,
            ["from"] = values.Period.From is DateTimeOffset from ? FormatTime(from) : null,
            ["by"] = values.By,
            ["limit"] = values.Limit,
            ["rows"] = resultRows,
            ["circuitBreaker"] = warnings.CircuitBreaker,
            ["warnings"] = new JsonArray(
                warnings.Warnings.Select(message => JsonValue.Create(message)).ToArray()),
        };
    }

    private async Task<JsonObject> SetBudgetAsync(
        SqliteLedgerStore store,
        string repository,
        string branch,
        decimal aiCredits,
```

Edit 11 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
        return new JsonObject
        {
            ["repository"] = repository,
            ["branch"] = branch,
            ["budget"] = warnings.Budget,
            ["circuitBreaker"] = warnings.CircuitBreaker,
            ["warnings"] = new JsonArray(
                warnings.Warnings.Select(message => JsonValue.Create(message)).ToArray()),
        };
    }

    private async Task<WarningResult> WarningsAsync(
        SqliteLedgerStore store,
        string repository,
        string branch,
        CancellationToken cancellationToken)
    {
        string? sessionId = _context.GetEnvironmentVariable("COPILOT_AGENT_SESSION_ID");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = null;
        }

        IReadOnlyList<BreakerTrip> allTrips =
            await store.GetOpenBreakerTripsAsync(
                BreakerTripFilter.All,
                cancellationToken);
        BreakerTrip[] trips = allTrips
            .Where(trip =>
                (string.Equals(trip.Repository, repository, StringComparison.Ordinal)
                    && string.Equals(trip.Branch, branch, StringComparison.Ordinal))
                || (sessionId is not null
                    && string.Equals(
                        trip.ConversationId,
                        sessionId,
                        StringComparison.Ordinal)))
            .ToArray();
        IReadOnlyList<BranchBudgetRow> budgets =
            await store.GetBranchBudgetsAsync(
                repository,
                branch,
                cancellationToken);
        BranchBudgetRow? budget = budgets.SingleOrDefault();
        List<string> warningText = [];
        JsonArray openTrips = [];
        foreach (BreakerTrip trip in trips)
        {
            bool currentSession =
```

with:

```csharp
        return new JsonObject
        {
            ["repository"] = repository,
            ["branch"] = branch,
            ["budget"] = warnings.Budget,
            ["circuitBreaker"] = warnings.CircuitBreaker,
            ["warnings"] = new JsonArray(
                warnings.Warnings.Select(message => JsonValue.Create(message)).ToArray()),
        };
    }

    // repository and branch are the current branch; when they're null, warnings come only from the current session's trips.
    private async Task<WarningResult> WarningsAsync(
        SqliteLedgerStore store,
        string? repository,
        string? branch,
        CancellationToken cancellationToken)
    {
        string? sessionId = _context.GetEnvironmentVariable("COPILOT_AGENT_SESSION_ID");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = null;
        }

        IReadOnlyList<BreakerTrip> allTrips =
            await store.GetOpenBreakerTripsAsync(
                BreakerTripFilter.All,
                cancellationToken);
        BreakerTrip[] trips = allTrips
            .Where(trip =>
                (repository is not null
                    && string.Equals(trip.Repository, repository, StringComparison.Ordinal)
                    && string.Equals(trip.Branch, branch, StringComparison.Ordinal))
                || (sessionId is not null
                    && string.Equals(
                        trip.ConversationId,
                        sessionId,
                        StringComparison.Ordinal)))
            .ToArray();
        IReadOnlyList<BranchBudgetRow> budgets = repository is null || branch is null
            ? []
            : await store.GetBranchBudgetsAsync(
                repository,
                branch,
                cancellationToken);
        BranchBudgetRow? budget = budgets.SingleOrDefault();
        List<string> warningText = [];
        JsonArray openTrips = [];
        foreach (BreakerTrip trip in trips)
        {
            bool currentSession =
```

Edit 12 of 12 in `src/CopilotLedger.Mcp/LedgerMcpTools.cs`, replace:

```csharp
        return document.RootElement.Clone();
    }

    private static decimal BilledAiCredits(decimal aiCredits, decimal byokUsd) =>
        aiCredits + byokUsd / AiCreditMath.UsdPerAiCredit;

    private static JsonNode Money(decimal value) =>
        JsonNode.Parse(
            value.ToString("0.############", CultureInfo.InvariantCulture))!;

    private static string Credits(decimal credits) =>
        decimal.Round(credits, 2, MidpointRounding.AwayFromZero)
            .ToString("N2", CultureInfo.InvariantCulture);

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private sealed record ToolArguments(
        string? Repository,
        string? Branch,
        int Limit,
        string OrderBy,
        decimal AiCredits)
    {
        public static ToolArguments Empty { get; } =
            new(null, null, 5, "cost", decimal.Zero);
    }

    private sealed record ScopeResult(
        string? Repository,
        string? Branch,
        string? Error);

    private sealed record WarningResult(
        JsonObject? Budget,
        JsonObject CircuitBreaker,
```

with:

```csharp
        return document.RootElement.Clone();
    }

    private static decimal BilledAiCredits(decimal aiCredits, decimal byokUsd) =>
        aiCredits + byokUsd / AiCreditMath.UsdPerAiCredit;

    private static JsonNode Money(decimal value) =>
        JsonNode.Parse(
            value.ToString("0.############", CultureInfo.InvariantCulture))!;

    // value with every digit decimal division gave it, or null.
    private static JsonNode? Ratio(decimal? value) =>
        value is null
            ? null
            : JsonNode.Parse(value.Value.ToString(CultureInfo.InvariantCulture));

    // Observed uncached input; null when no span reported cache usage.
    private static long? Uncached(LedgerRollupRow row) =>
        row.CacheReportedSpanCount == 0 ? null : row.ReportedUncachedInputTokens;

    private static string Credits(decimal credits) =>
        decimal.Round(credits, 2, MidpointRounding.AwayFromZero)
            .ToString("N2", CultureInfo.InvariantCulture);

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private sealed record ToolArguments(
        string? Repository,
        string? Branch,
        int Limit,
        string OrderBy,
        decimal AiCredits,
        int? PullRequest,
        LedgerPeriod? Period,
        string By)
    {
        public static ToolArguments Empty { get; } =
            new(null, null, 5, "cost", decimal.Zero, null, null, "model");
    }

    private sealed record ScopeResult(
        string? Repository,
        string? Branch,
        string? Error);

    private sealed record WarningResult(
        JsonObject? Budget,
        JsonObject CircuitBreaker,
```

New file `src/CopilotLedger.Storage/LedgerPeriod.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace CopilotLedger.Storage;

// A report period and where it starts; From is null for all time.
public sealed record LedgerPeriod(string Label, DateTimeOffset? From)
{
    // value is today, all, Nd (1 to 3650 days, counting today), or a yyyy-MM-dd date, without regard to case or surrounding
    // white space. Periods start at local midnight in timeProvider's time zone. Returns false for anything else.
    public static bool TryParse(
        string? value,
        TimeProvider timeProvider,
        [NotNullWhen(true)] out LedgerPeriod? period)
    {
        string text = value?.Trim() ?? string.Empty;
        DateTimeOffset now = timeProvider.GetUtcNow();
        TimeZoneInfo zone = timeProvider.LocalTimeZone;
        DateOnly today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, zone).DateTime);

        if (text.Equals("today", StringComparison.OrdinalIgnoreCase))
        {
            period = new LedgerPeriod("today", Midnight(today, zone));
            return true;
        }

        if (text.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            period = new LedgerPeriod("all time", null);
            return true;
        }

        if (text.EndsWith("d", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(
                text[..^1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int days) &&
            days is >= 1 and <= 3650)
        {
            period = new LedgerPeriod(
                days == 1 ? "today" : $"last {days} days",
                Midnight(today.AddDays(-(days - 1)), zone));
            return true;
        }

        if (DateOnly.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly date))
        {
            period = new LedgerPeriod($"since {text}", Midnight(date, zone));
            return true;
        }

        period = null;
        return false;
    }

    private static DateTimeOffset Midnight(DateOnly date, TimeZoneInfo zone)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
```

Edit 1 of 10 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
    private const string T5 = "55555555555555555555555555555555";
    private const string T6 = "66666666666666666666666666666666";
    private const string Hash =
        "c3d1fe831856c77818bb8c1242d32d6efa6c37366eb902e56c5fd0054c1dcbc3/918a44e755cc5eee09b08d214831e8339a645158ef1f831a0fa02bd43999700f";
```

with:

```csharp
    private const string T5 = "55555555555555555555555555555555";
    private const string T6 = "66666666666666666666666666666666";
    private const string T7 = "77777777777777777777777777777777";
    private const string Hash =
        "c3d1fe831856c77818bb8c1242d32d6efa6c37366eb902e56c5fd0054c1dcbc3/918a44e755cc5eee09b08d214831e8339a645158ef1f831a0fa02bd43999700f";
```

Edit 2 of 10 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
          "additionalProperties": false
        }
        """;
    private const string Branch =
        """
        {
          "repository": "swigerb/copilot-ledger",
```

with:

```csharp
          "additionalProperties": false
        }
        """;
    private const string CacheSchema =
        """
        {
          "type": "object",
          "properties": {
            "repository": { "type": "string", "minLength": 1, "description": "Repository key: owner/repo, a repository root, or (none). Defaults to the current repository." },
            "branch": { "type": "string", "minLength": 1, "description": "Only this branch, or (none)." },
            "pullRequest": { "type": "integer", "minimum": 1, "description": "Only this pull request number." },
            "since": { "type": "string", "default": "30d", "description": "Period: today, all, Nd (1 to 3650 days), or yyyy-MM-dd." },
            "by": { "type": "string", "enum": ["model", "branch", "pr", "agent"], "default": "model", "description": "Group rows by model, branch, pull request, or agent." },
            "limit": { "type": "integer", "minimum": 1, "maximum": 50, "default": 10, "description": "Number of rows, most input first." }
          },
          "additionalProperties": false
        }
        """;
    private const string Branch =
        """
        {
          "repository": "swigerb/copilot-ledger",
```

Edit 3 of 10 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
          "cacheReadInputTokens": 400000,
          "cacheWriteInputTokens": 0,
          "aiCredits": 176.5,
          "aiCreditsUsd": 1.765,
```

with:

```csharp
          "cacheReadInputTokens": 400000,
          "cacheWriteInputTokens": 0,
          "uncachedInputTokens": 600000,
          "cacheReportedSpans": 1,
          "cacheHitRatio": 0.4,
          "aiCredits": 176.5,
          "aiCreditsUsd": 1.765,
```

Edit 4 of 10 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
                "Spend on one Git branch split by agent (gen_ai.agent.name). Uses the current repository and branch unless you pass both.",
                BranchSchema,
                readOnly: true,
                destructive: null,
```

with:

```csharp
                "Spend on one Git branch split by agent (gen_ai.agent.name). Uses the current repository and branch unless you pass both.",
                BranchSchema,
                readOnly: true,
                destructive: null,
                idempotent: null),
            tool => AssertTool(
                tool,
                "get_cache_efficiency",
                "Cache efficiency",
                "Prompt-cache efficiency in one repository, grouped by model, branch, pull request, or agent: the cache hit ratio, input read from and written to the cache, uncached input, and the largest prompt. Spans that reported no cache counters count as unknown, never as misses. Uses the current repository unless you pass one.",
                CacheSchema,
                readOnly: true,
                destructive: null,
```

Edit 5 of 10 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
                """{"repository":"contoso/api","branch":"main"}"""),
            """
            {"repository":"contoso/api","branch":"main","spans":1,"unpricedSpans":0,"inputTokens":100000,"outputTokens":10000,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"aiCredits":0,"aiCreditsUsd":0,"foundryByokUsd":0.315,"localSavedUsd":0,"billedAiCredits":31.5,"topModel":"gpt-6.1-sol","budget":null,"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
        await AssertResultAsync(
```

with:

```csharp
                """{"repository":"contoso/api","branch":"main"}"""),
            """
            {"repository":"contoso/api","branch":"main","spans":1,"unpricedSpans":0,"inputTokens":100000,"outputTokens":10000,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"uncachedInputTokens":null,"cacheReportedSpans":0,"cacheHitRatio":null,"aiCredits":0,"aiCreditsUsd":0,"foundryByokUsd":0.315,"localSavedUsd":0,"billedAiCredits":31.5,"topModel":"gpt-6.1-sol","budget":null,"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
        await AssertResultAsync(
```

Edit 6 of 10 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
                """{"repository":"swigerb/copilot-ledger","branch":"nope"}"""),
            """
            {"repository":"swigerb/copilot-ledger","branch":"nope","spans":0,"unpricedSpans":0,"inputTokens":0,"outputTokens":0,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"aiCredits":0,"aiCreditsUsd":0,"foundryByokUsd":0,"localSavedUsd":0,"billedAiCredits":0,"topModel":null,"budget":null,"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
    }

    [Fact]
```

with:

```csharp
                """{"repository":"swigerb/copilot-ledger","branch":"nope"}"""),
            """
            {"repository":"swigerb/copilot-ledger","branch":"nope","spans":0,"unpricedSpans":0,"inputTokens":0,"outputTokens":0,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"uncachedInputTokens":null,"cacheReportedSpans":0,"cacheHitRatio":null,"aiCredits":0,"aiCreditsUsd":0,"foundryByokUsd":0,"localSavedUsd":0,"billedAiCredits":0,"topModel":null,"budget":null,"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
    }

    [Fact]
```

Edit 7 of 10 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
            await CallAsync(harness, "get_agent_breakdown", "{}"),
            """
            {"repository":"swigerb/copilot-ledger","branch":"feature/ledger","agents":[{"agent":"(none)","spans":2,"unpricedSpans":0,"inputTokens":110000,"outputTokens":250000,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"aiCredits":120.5,"aiCreditsUsd":1.205,"foundryByokUsd":0,"localSavedUsd":0,"billedAiCredits":120.5},{"agent":"ledger-builder","spans":2,"unpricedSpans":0,"inputTokens":1400000,"outputTokens":112000,"cacheReadInputTokens":400000,"cacheWriteInputTokens":0,"aiCredits":56,"aiCreditsUsd":0.56,"foundryByokUsd":0,"localSavedUsd":0.23,"billedAiCredits":56}],"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
    }

    [Fact]
```

with:

```csharp
            await CallAsync(harness, "get_agent_breakdown", "{}"),
            """
            {"repository":"swigerb/copilot-ledger","branch":"feature/ledger","agents":[{"agent":"(none)","spans":2,"unpricedSpans":0,"inputTokens":110000,"outputTokens":250000,"cacheReadInputTokens":0,"cacheWriteInputTokens":0,"uncachedInputTokens":null,"cacheReportedSpans":0,"cacheHitRatio":null,"aiCredits":120.5,"aiCreditsUsd":1.205,"foundryByokUsd":0,"localSavedUsd":0,"billedAiCredits":120.5},{"agent":"ledger-builder","spans":2,"unpricedSpans":0,"inputTokens":1400000,"outputTokens":112000,"cacheReadInputTokens":400000,"cacheWriteInputTokens":0,"uncachedInputTokens":600000,"cacheReportedSpans":1,"cacheHitRatio":0.4,"aiCredits":56,"aiCreditsUsd":0.56,"foundryByokUsd":0,"localSavedUsd":0.23,"billedAiCredits":56}],"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
    }

    [Fact]
```

Edit 8 of 10 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
        { "set_branch_budget", """{"aiCredits":"5"}""", "aiCredits must be a number greater than 0 and at most 1000000, with at most 2 decimal places." },
        { "set_branch_budget", """{"aiCredits":5,"repository":"a/b"}""", "Pass repository and branch together, or neither." },
    };

    [Theory]
```

with:

```csharp
        { "set_branch_budget", """{"aiCredits":"5"}""", "aiCredits must be a number greater than 0 and at most 1000000, with at most 2 decimal places." },
        { "set_branch_budget", """{"aiCredits":5,"repository":"a/b"}""", "Pass repository and branch together, or neither." },
        { "get_branch_ledger", """{"since":"all"}""", "Unknown argument: since." },
        { "get_cache_efficiency", """{"orderBy":"cost"}""", "Unknown argument: orderBy." },
        { "get_cache_efficiency", """{"repository":""}""", "repository must be a non-empty string." },
        { "get_cache_efficiency", """{"branch":null}""", "branch must be a non-empty string." },
        { "get_cache_efficiency", """{"pullRequest":0}""", "pullRequest must be an integer of at least 1." },
        { "get_cache_efficiency", """{"pullRequest":"7"}""", "pullRequest must be an integer of at least 1." },
        { "get_cache_efficiency", """{"pullRequest":7.5}""", "pullRequest must be an integer of at least 1." },
        { "get_cache_efficiency", """{"since":"0d"}""", "since must be today, all, <N>d (1-3650), or yyyy-MM-dd." },
        { "get_cache_efficiency", """{"since":30}""", "since must be today, all, <N>d (1-3650), or yyyy-MM-dd." },
        { "get_cache_efficiency", """{"by":"Model"}""", "by must be model, branch, pr, or agent." },
        { "get_cache_efficiency", """{"by":"channel"}""", "by must be model, branch, pr, or agent." },
        { "get_cache_efficiency", """{"limit":0}""", "limit must be an integer from 1 to 50." },
        { "get_cache_efficiency", """{"limit":51}""", "limit must be an integer from 1 to 50." },
    };

    [Theory]
```

Edit 9 of 10 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
                    Version = "1.0",
                },
            });
        Assert.Equal(4, (await client.ListToolsAsync()).Count);
        Implementation? serverInfo = client.ServerInfo;
        Assert.NotNull(serverInfo);
```

with:

```csharp
                    Version = "1.0",
                },
            });
        Assert.Equal(5, (await client.ListToolsAsync()).Count);
        Implementation? serverInfo = client.ServerInfo;
        Assert.NotNull(serverInfo);
```

Edit 10 of 10 in `tests/CopilotLedger.Tests/Mcp/LedgerMcpServerTests.cs`, replace:

```csharp
            $"The ledger at {databasePath} is unavailable: ",
            Text(result));
    }

    private static async Task<McpTestHarness> StartAsync(
```

with:

```csharp
            $"The ledger at {databasePath} is unavailable: ",
            Text(result));
    }

    [Fact]
    public async Task MC13_CacheEfficiency()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await SeedAsync(temp.Path);
        await SeedPullRequestAsync(temp.Path);
        await using McpTestHarness harness = await StartAsync(
            Context(temp.Path, []));
        await AssertResultAsync(
            await CallAsync(harness, "get_cache_efficiency", "{}"),
            """
            {"repository":"swigerb/copilot-ledger","branch":null,"pullRequest":null,"period":"last 30 days","from":"2026-10-23T00:00:00.0000000+00:00","by":"model","limit":10,"rows":[{"model":"claude-opus-5.5","spans":2,"cacheReportedSpans":2,"cacheUnreportedSpans":0,"inputTokens":1250000,"outputTokens":51000,"maxInputTokens":1200000,"averageInputTokensPerTurn":625000,"reportedInputTokens":1250000,"reportedCacheReadInputTokens":840000,"reportedCacheWriteInputTokens":100000,"uncachedInputTokens":310000,"estimatedUncachedInputTokens":310000,"cacheHitRatio":0.672},{"model":"mai-code-2","spans":2,"cacheReportedSpans":1,"cacheUnreportedSpans":1,"inputTokens":1010000,"outputTokens":300000,"maxInputTokens":1000000,"averageInputTokensPerTurn":505000,"reportedInputTokens":1000000,"reportedCacheReadInputTokens":400000,"reportedCacheWriteInputTokens":0,"uncachedInputTokens":600000,"estimatedUncachedInputTokens":610000,"cacheHitRatio":0.4},{"model":"qwen3.8-27b","spans":1,"cacheReportedSpans":0,"cacheUnreportedSpans":1,"inputTokens":400000,"outputTokens":12000,"maxInputTokens":400000,"averageInputTokensPerTurn":400000,"reportedInputTokens":0,"reportedCacheReadInputTokens":0,"reportedCacheWriteInputTokens":0,"uncachedInputTokens":null,"estimatedUncachedInputTokens":400000,"cacheHitRatio":null},{"model":"claude-sonnet-5.5","spans":1,"cacheReportedSpans":0,"cacheUnreportedSpans":1,"inputTokens":100000,"outputTokens":50000,"maxInputTokens":100000,"averageInputTokensPerTurn":100000,"reportedInputTokens":0,"reportedCacheReadInputTokens":0,"reportedCacheWriteInputTokens":0,"uncachedInputTokens":null,"estimatedUncachedInputTokens":100000,"cacheHitRatio":null}],"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
        await AssertResultAsync(
            await CallAsync(harness, "get_cache_efficiency", """{"by":"pr"}"""),
            """
            {"repository":"swigerb/copilot-ledger","branch":null,"pullRequest":null,"period":"last 30 days","from":"2026-10-23T00:00:00.0000000+00:00","by":"pr","limit":10,"rows":[{"repository":"swigerb/copilot-ledger","pullRequest":null,"branch":"feature/ledger","spans":4,"cacheReportedSpans":1,"cacheUnreportedSpans":3,"inputTokens":1510000,"outputTokens":362000,"maxInputTokens":1000000,"averageInputTokensPerTurn":377500,"reportedInputTokens":1000000,"reportedCacheReadInputTokens":400000,"reportedCacheWriteInputTokens":0,"uncachedInputTokens":600000,"estimatedUncachedInputTokens":1110000,"cacheHitRatio":0.4},{"repository":"swigerb/copilot-ledger","pullRequest":null,"branch":"main","spans":1,"cacheReportedSpans":1,"cacheUnreportedSpans":0,"inputTokens":1200000,"outputTokens":50000,"maxInputTokens":1200000,"averageInputTokensPerTurn":1200000,"reportedInputTokens":1200000,"reportedCacheReadInputTokens":800000,"reportedCacheWriteInputTokens":100000,"uncachedInputTokens":300000,"estimatedUncachedInputTokens":300000,"cacheHitRatio":0.6666666666666666666666666667},{"repository":"swigerb/copilot-ledger","pullRequest":7,"branch":null,"spans":1,"cacheReportedSpans":1,"cacheUnreportedSpans":0,"inputTokens":50000,"outputTokens":1000,"maxInputTokens":50000,"averageInputTokensPerTurn":50000,"reportedInputTokens":50000,"reportedCacheReadInputTokens":40000,"reportedCacheWriteInputTokens":0,"uncachedInputTokens":10000,"estimatedUncachedInputTokens":10000,"cacheHitRatio":0.8}],"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
        await AssertResultAsync(
            await CallAsync(
                harness,
                "get_cache_efficiency",
                """{"repository":"contoso/api","by":"branch","since":"all","limit":1}"""),
            """
            {"repository":"contoso/api","branch":null,"pullRequest":null,"period":"all time","from":null,"by":"branch","limit":1,"rows":[{"repository":"contoso/api","branch":"main","spans":1,"cacheReportedSpans":0,"cacheUnreportedSpans":1,"inputTokens":100000,"outputTokens":10000,"maxInputTokens":100000,"averageInputTokensPerTurn":100000,"reportedInputTokens":0,"reportedCacheReadInputTokens":0,"reportedCacheWriteInputTokens":0,"uncachedInputTokens":null,"estimatedUncachedInputTokens":100000,"cacheHitRatio":null}],"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
        await AssertResultAsync(
            await CallAsync(
                harness,
                "get_cache_efficiency",
                """{"branch":"feature/ledger","by":"agent","since":"today"}"""),
            """
            {"repository":"swigerb/copilot-ledger","branch":"feature/ledger","pullRequest":null,"period":"today","from":"2026-11-21T00:00:00.0000000+00:00","by":"agent","limit":10,"rows":[{"agent":"ledger-builder","spans":2,"cacheReportedSpans":1,"cacheUnreportedSpans":1,"inputTokens":1400000,"outputTokens":112000,"maxInputTokens":1000000,"averageInputTokensPerTurn":700000,"reportedInputTokens":1000000,"reportedCacheReadInputTokens":400000,"reportedCacheWriteInputTokens":0,"uncachedInputTokens":600000,"estimatedUncachedInputTokens":1000000,"cacheHitRatio":0.4},{"agent":"(none)","spans":3,"cacheReportedSpans":1,"cacheUnreportedSpans":2,"inputTokens":160000,"outputTokens":251000,"maxInputTokens":100000,"averageInputTokensPerTurn":53333.333333333333333333333333,"reportedInputTokens":50000,"reportedCacheReadInputTokens":40000,"reportedCacheWriteInputTokens":0,"uncachedInputTokens":10000,"estimatedUncachedInputTokens":120000,"cacheHitRatio":0.8}],"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
        await AssertResultAsync(
            await CallAsync(
                harness,
                "get_cache_efficiency",
                """{"pullRequest":7,"since":"2026-11-21"}"""),
            """
            {"repository":"swigerb/copilot-ledger","branch":null,"pullRequest":7,"period":"since 2026-11-21","from":"2026-11-21T00:00:00.0000000+00:00","by":"model","limit":10,"rows":[{"model":"claude-opus-5.5","spans":1,"cacheReportedSpans":1,"cacheUnreportedSpans":0,"inputTokens":50000,"outputTokens":1000,"maxInputTokens":50000,"averageInputTokensPerTurn":50000,"reportedInputTokens":50000,"reportedCacheReadInputTokens":40000,"reportedCacheWriteInputTokens":0,"uncachedInputTokens":10000,"estimatedUncachedInputTokens":10000,"cacheHitRatio":0.8}],"circuitBreaker":{"state":"closed","openTrips":[]},"warnings":[]}
            """);
    }

    [Fact]
    public async Task MC14_CacheEfficiencyScopeAndWarnings()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await SeedAsync(temp.Path);
        SqliteLedgerStore store = Store(temp.Path);
        await store.InitializeAsync();
        await store.RecordBreakerTripAsync(new NewBreakerTrip
        {
            Rule = BreakerRule.RetryChurn,
            TraceId = T5,
            ConversationId = "conv-5",
            Repository = "swigerb/copilot-ledger",
            Branch = "feature/ledger",
            RepositoryRoot = Path.Combine(temp.Path, "repo"),
            TrippedAt = DateTimeOffset.Parse(
                "2026-11-21T15:00:05Z",
                CultureInfo.InvariantCulture),
            Detail = "5 failed tool calls in a row",
            FailureCount = 5,
        });
        await store.RecordBreakerTripAsync(new NewBreakerTrip
        {
            Rule = BreakerRule.RetryChurn,
            TraceId = new string('a', 32),
            ConversationId = "conv-other",
            Repository = "contoso/api",
            Branch = "main",
            TrippedAt = DateTimeOffset.Parse(
                "2026-11-21T09:00:00Z",
                CultureInfo.InvariantCulture),
            Detail = "7 failed tool calls in a row",
            FailureCount = 7,
        });
        await store.SetBranchBudgetAsync(
            "swigerb/copilot-ledger",
            "feature/ledger",
            150m);
        const string Trip =
            """{"id":1,"rule":"retry_churn","repository":"swigerb/copilot-ledger","branch":"feature/ledger","traceId":"55555555555555555555555555555555","conversationId":"conv-5","currentSession":CURRENT,"trippedAt":"2026-11-21T15:00:05.0000000+00:00","detail":"5 failed tool calls in a row","resetCommand":"gh-ledger breaker reset --id 1"}""";
        const string TripWarning =
            "WARNING: CopilotLedger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1";
        const string BudgetWarning =
            "WARNING: swigerb/copilot-ledger @ feature/ledger has spent 176.50 of its 150.00 AI credit budget.";

        await using (McpTestHarness current = await StartAsync(
            Context(temp.Path, [])))
        {
            JsonObject expected = JsonNode.Parse(
                """
                {"repository":"contoso/api","branch":null,"pullRequest":null,"period":"all time","from":null,"by":"model","limit":10,"rows":[{"model":"gpt-6.1-sol","spans":1,"cacheReportedSpans":0,"cacheUnreportedSpans":1,"inputTokens":100000,"outputTokens":10000,"maxInputTokens":100000,"averageInputTokensPerTurn":100000,"reportedInputTokens":0,"reportedCacheReadInputTokens":0,"reportedCacheWriteInputTokens":0,"uncachedInputTokens":null,"estimatedUncachedInputTokens":100000,"cacheHitRatio":null}]}
                """)!.AsObject();
            expected["circuitBreaker"] = JsonNode.Parse(
                $$"""{"state":"open","openTrips":[{{Trip.Replace("CURRENT", "false", StringComparison.Ordinal)}}]}""");
            expected["warnings"] = new JsonArray(TripWarning, BudgetWarning);
            await AssertResultAsync(
                await CallAsync(
                    current,
                    "get_cache_efficiency",
                    """{"repository":"contoso/api","since":"all"}"""),
                expected);
        }

        Dictionary<string, string> env = [];
        LedgerMcpContext outside = Context(temp.Path, env) with
        {
            WorkingDirectory = temp.Path,
        };
        await using (McpTestHarness missing = await StartAsync(outside))
        {
            CallToolResult result = await CallAsync(
                missing,
                "get_cache_efficiency",
                "{}");
            Assert.True(result.IsError);
            Assert.Equal(
                $"No Git repository at {temp.Path}. Pass repository.",
                Text(result));
            Assert.Null(result.StructuredContent);
        }

        env["COPILOT_AGENT_SESSION_ID"] = "conv-5";
        await using McpTestHarness session = await StartAsync(outside);
        JsonObject expectedSession = JsonNode.Parse(
            """
            {"repository":"swigerb/copilot-ledger","branch":null,"pullRequest":null,"period":"last 30 days","from":"2026-10-23T00:00:00.0000000+00:00","by":"model","limit":1,"rows":[{"model":"claude-opus-5.5","spans":1,"cacheReportedSpans":1,"cacheUnreportedSpans":0,"inputTokens":1200000,"outputTokens":50000,"maxInputTokens":1200000,"averageInputTokensPerTurn":1200000,"reportedInputTokens":1200000,"reportedCacheReadInputTokens":800000,"reportedCacheWriteInputTokens":100000,"uncachedInputTokens":300000,"estimatedUncachedInputTokens":300000,"cacheHitRatio":0.6666666666666666666666666667}]}
            """)!.AsObject();
        expectedSession["circuitBreaker"] = JsonNode.Parse(
            $$"""{"state":"open","openTrips":[{{Trip.Replace("CURRENT", "true", StringComparison.Ordinal)}}]}""");
        expectedSession["warnings"] = new JsonArray(TripWarning);
        await AssertResultAsync(
            await CallAsync(
                session,
                "get_cache_efficiency",
                """{"repository":"swigerb/copilot-ledger","limit":1}"""),
            expectedSession);
    }

    private static async Task SeedPullRequestAsync(string root)
    {
        SqliteLedgerStore store = Store(root);
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: DateTimeOffset.Parse(
                "2026-11-21T15:30:00Z",
                CultureInfo.InvariantCulture),
            requestModel: "claude-opus-5.5",
            conversationId: "conv-7",
            inputTokens: 50_000,
            outputTokens: 1_000,
            cacheReadInputTokens: 40_000) with
        {
            TraceId = T7,
            SpanId = "7777777777777777",
        };
        await store.RecordAsync([
            new LedgerEntry(
                span,
                JsonRateCardCalculator.LoadEmbedded().Calculate(span),
                GitFixture.Feature(root) with { PullRequestNumber = 7 }),
        ]);
    }

    private static async Task<McpTestHarness> StartAsync(
```

Edit 1 of 1 in `docs/cli.md`, replace:

```markdown
That adds `ledger` to `~/.copilot/mcp-config.json`, which VS Code's Copilot agent harness also reads. The GitHub Copilot app runs the same agent runtime; if it doesn't list the server, add it in the app's MCP settings with the same command. For VS Code's Local agent harness, add the server to `mcp.json` under `servers`, with the binary's path as `"command"` and `["mcp"]` as `"args"`.

The server has four tools. Each uses the current repository and branch unless you pass `repository` and `branch`:

- `get_branch_ledger`: the branch's spend, top model, budget, and circuit-breaker state.
- `get_top_expensive_prompts`: the branch's most expensive prompt chains, with output tokens and failed tool calls; `limit` (1 to 20, default 5) and `orderBy` (`cost`, `outputTokens`, or `failedToolCalls`).
- `get_agent_breakdown`: the branch's spend by agent.
- `set_branch_budget`: sets the branch's budget in AI credits (`aiCredits`).

The first three are read-only. `set_branch_budget` changes only the budget, so Copilot asks before running it; in prompt mode, allow it with `--allow-tool 'ledger(set_branch_budget)'`. While the breaker is open for the branch or the session, or the branch is over budget, every result starts with a `WARNING:` line. No tool can reset the breaker.

## Rates
```

with:

```markdown
That adds `ledger` to `~/.copilot/mcp-config.json`, which VS Code's Copilot agent harness also reads. The GitHub Copilot app runs the same agent runtime; if it doesn't list the server, add it in the app's MCP settings with the same command. For VS Code's Local agent harness, add the server to `mcp.json` under `servers`, with the binary's path as `"command"` and `["mcp"]` as `"args"`.

The server has five tools. All but `get_cache_efficiency` use the current repository and branch unless you pass `repository` and `branch`:

- `get_branch_ledger`: the branch's spend, top model, budget, circuit-breaker state, and prompt-cache hit ratio.
- `get_top_expensive_prompts`: the branch's most expensive prompt chains, with output tokens and failed tool calls; `limit` (1 to 20, default 5) and `orderBy` (`cost`, `outputTokens`, or `failedToolCalls`).
- `get_agent_breakdown`: the branch's spend and prompt-cache hit ratio by agent.
- `get_cache_efficiency`: prompt-cache efficiency in the current repository, or in `repository`, like `status --cache`. `by` groups rows by `model` (the default), `branch`, `pr`, or `agent`; `branch` and `pullRequest` keep one branch or pull request; `since` takes the same periods as `--since`; and `limit` (1 to 50, default 10) keeps the rows with the most input.
- `set_branch_budget`: sets the branch's budget in AI credits (`aiCredits`).

The first four are read-only. `set_branch_budget` changes only the budget, so Copilot asks before running it; in prompt mode, allow it with `--allow-tool 'ledger(set_branch_budget)'`. While the breaker is open for the branch or the session, or the branch is over budget, every result starts with a `WARNING:` line; for `get_cache_efficiency`, the branch is the current one. No tool can reset the breaker.

## Rates
```

Edit 1 of 1 in `docs/guide.md`, replace:

```markdown
  "cacheReadInputTokens": 1450000,
  "cacheWriteInputTokens": 0,
  "aiCredits": 617.75,
  "aiCreditsUsd": 6.1775,
```

with:

```markdown
  "cacheReadInputTokens": 1450000,
  "cacheWriteInputTokens": 0,
  "uncachedInputTokens": 1250000,
  "cacheReportedSpans": 6,
  "cacheHitRatio": 0.537037037037037037037037037,
  "aiCredits": 617.75,
  "aiCreditsUsd": 6.1775,
```

### Tests

- **MC1:** five tools; `get_cache_efficiency` is fourth, with its title, description, schema (`CacheSchema`), and read-only annotations. **MC11:** `gh ledger mcp` lists 5 tools.
- **MC2, MC4, MC6** and the guide's **GS7** (`mcp` sample): `get_branch_ledger` for `feature/ledger` adds `"uncachedInputTokens": 600000, "cacheReportedSpans": 1, "cacheHitRatio": 0.4`; an unreported branch and an empty one add `null`, `0`, `null`; agent rows add the same fields; the guide's `feature/search` adds 1,250,000, 6, and `0.537037037037037037037037037`.
- **MC8** (13 new cases): `since` on `get_branch_ledger`; on `get_cache_efficiency`, `orderBy`, an empty `repository`, a `null` `branch`, `pullRequest` 0, `"7"`, and 7.5, `since` `0d` and 30, `by` `Model` and `channel`, and `limit` 0 and 51, each with its message.
- **MC13:** with the MCP seed plus a pull request 7 span (`SeedPullRequestAsync`): the default call (four model rows, `claude-opus-5.5` first with ratio `0.672`, `qwen3.8-27b` and `claude-sonnet-5.5` with `null` observed values); `by` `pr` (the `(no PR)` rows, then PR 7); another repository by branch since `all` with `limit` 1; one branch by agent `today`, whose `(none)` row has `averageInputTokensPerTurn` `53333.333333333333333333333333`; and `pullRequest` 7 since `2026-11-21`.
- **MC14:** with a breaker trip on `feature/ledger`, a trip elsewhere, and an exceeded budget: inside the repository, asking about `contoso/api` still returns the current branch's trip and budget warnings, in that order; outside a repository without `repository`, the error above; outside a repository with `repository` and `COPILOT_AGENT_SESSION_ID` `conv-5`, only the session's trip (`currentSession: true`) and no budget warning.

### Acceptance criteria

- `get_cache_efficiency` validates its arguments, returns the documented fields, is read-only, and keeps the `WARNING:` prefix; the other tools gain the three fields; unknown arguments are rejected everywhere.
- `Period.TryParse` behaves as before (the existing `--since` tests pass unchanged).
- `dotnet test CopilotLedger.slnx` passes **801** tests (786 + 15).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 11 specification: Docs and the Context-Bloat Walkthrough

### Files

| File | Change |
|---|---|
| `docs/cli.md` | The `status` row and a `status --cache` row in "Reports", a bullet on the new JSON fields, and a new "Prompt-cache efficiency" section |
| `docs/configuration.md` | New "Cache tokens" section under "How usage is priced" |
| `docs/guide.md` | New "Find context bloat" section after "Compare agents", with samples `cache`, `cache-bloat`, and `cache-turns`, and its entry in the contents |
| `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs` | The breaker-tripping prompt's four requests grow; every Month span gets an `invoke_agent` parent and span links, back-filled |
| `tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs` | `Commands` adds the three samples, in the Month state |

### The example world

`GuideWorld`'s trace `…00b2` (the prompt that trips the credit-velocity breaker) keeps four `claude-opus-5.5` requests of 20,000 output tokens, each costing 126.00 AI credits, but its input now grows:

| Request | Input | Cache read | Uncached | Cost |
|---|---:|---:|---:|---:|
| c1, 16:20:00 UTC | 357,500 | 150,000 | 207,500 | 126.00 |
| c2, 16:21:30 | 452,500 | 250,000 | 202,500 | 126.00 |
| c3, 16:23:00 | 547,500 | 350,000 | 197,500 | 126.00 |
| c4, 16:24:30 | 642,500 | 450,000 | 192,500 | 126.00 |

(`claude-opus-5.5` costs $4.00 per million uncached input tokens, $0.20 per million cache reads, and $20.00 per million output tokens. For c1, (207,500 × $4.00 + 150,000 × $0.20) / 1,000,000 = $0.86; every request's input costs the same, and its 20,000 output tokens add $0.40.) The totals stay 2,000,000 input and 1,200,000 cache reads, so every existing sample, the receipt, and the breaker trip are unchanged.

Each Month span's parent is its agent's `invoke_agent` span: `…<trace>a0` for the default agent and `…<trace>e0` for a subagent, which is a child of `…<trace>a0`. After recording the spans, `GuideWorld` records an `invoke_agent` link for each root and subagent and a `chat` link for each span (links already recorded are kept), then calls `BackfillFromAgentSpansAsync` for every trace, as the daemon does. Agent names and conversations don't change, because every span already has them.

### Docs

The text is in the code below. In short: `docs/cli.md` explains `Input`, `Uncached`, what "reported" means and why a missing counter is 0 on a span that reports the other (D1), `n/a`, `0.0%`, and `*`, the `--by pr` source and its limits, `--turns` and `Growth`, `--bloat` and its estimate, the option rules, and every JSON field; `docs/configuration.md` says that a request without cache counts is priced as all uncached input, as before, and that cache reports treat it as unknown rather than as a miss; and the guide's walkthrough runs `status --cache`, `status --cache --bloat --limit 3`, and `status --cache --turns --trace 0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2`.

### Code (copy exactly)

Edit 1 of 1 in `tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs`, replace:

```csharp
        ["top-prompts"] = GuideState.Month,
        ["agents"] = GuideState.Month,
        ["budget-set"] = GuideState.Month,
        ["budget-list"] = GuideState.Month,
```

with:

```csharp
        ["top-prompts"] = GuideState.Month,
        ["agents"] = GuideState.Month,
        ["cache"] = GuideState.Month,
        ["cache-bloat"] = GuideState.Month,
        ["cache-turns"] = GuideState.Month,
        ["budget-set"] = GuideState.Month,
        ["budget-list"] = GuideState.Month,
```

Edit 1 of 5 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
            (Chat("a1", "c1", "2026-11-21T14:05:00Z", "claude-opus-5.5", null, null, 400_000, 15_000, 250_000), "feature/search"),
            (Chat("a1", "c2", "2026-11-21T14:07:00Z", "mai-code-2", null, "explore", 300_000, 15_000), "feature/search"),
            (Chat("b2", "c1", "2026-11-21T16:20:00Z", "claude-opus-5.5", null, null, 500_000, 20_000, 300_000), "feature/search"),
            (Chat("b2", "c2", "2026-11-21T16:21:30Z", "claude-opus-5.5", null, null, 500_000, 20_000, 300_000), "feature/search"),
            (Chat("b2", "c3", "2026-11-21T16:23:00Z", "claude-opus-5.5", null, null, 500_000, 20_000, 300_000), "feature/search"),
            (Chat("b2", "c4", "2026-11-21T16:24:30Z", "claude-opus-5.5", null, null, 500_000, 20_000, 300_000), "feature/search"),
            (Chat("c3", "c1", "2026-11-21T19:10:00Z", "gpt-6.1-sol", "contoso.openai.azure.com", null, 200_000, 10_000), "feature/search"),
            (Chat("d4", "c1", "2026-11-21T20:40:00Z", "qwen3.8-27b", "127.0.0.1", null, 300_000, 9_000), "feature/search"),
```

with:

```csharp
            (Chat("a1", "c1", "2026-11-21T14:05:00Z", "claude-opus-5.5", null, null, 400_000, 15_000, 250_000), "feature/search"),
            (Chat("a1", "c2", "2026-11-21T14:07:00Z", "mai-code-2", null, "explore", 300_000, 15_000), "feature/search"),
            (Chat("b2", "c1", "2026-11-21T16:20:00Z", "claude-opus-5.5", null, null, 357_500, 20_000, 150_000), "feature/search"),
            (Chat("b2", "c2", "2026-11-21T16:21:30Z", "claude-opus-5.5", null, null, 452_500, 20_000, 250_000), "feature/search"),
            (Chat("b2", "c3", "2026-11-21T16:23:00Z", "claude-opus-5.5", null, null, 547_500, 20_000, 350_000), "feature/search"),
            (Chat("b2", "c4", "2026-11-21T16:24:30Z", "claude-opus-5.5", null, null, 642_500, 20_000, 450_000), "feature/search"),
            (Chat("c3", "c1", "2026-11-21T19:10:00Z", "gpt-6.1-sol", "contoso.openai.azure.com", null, 200_000, 10_000), "feature/search"),
            (Chat("d4", "c1", "2026-11-21T20:40:00Z", "qwen3.8-27b", "127.0.0.1", null, 300_000, 9_000), "feature/search"),
```

Edit 2 of 5 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
        await store.RecordAsync(
            spans.Select(item => new LedgerEntry(item.Span, rates.Calculate(item.Span), Context(item.Branch))).ToArray());
        await new CircuitBreakerMonitor(store, new RecordingAlertSink()).EvaluateAsync(
            spans.Select(item => item.Span.TraceId));
```

with:

```csharp
        await store.RecordAsync(
            spans.Select(item => new LedgerEntry(item.Span, rates.Calculate(item.Span), Context(item.Branch))).ToArray());

        // Each prompt's span tree: the default agent's invoke_agent span (span ID ending a0), a subagent's invoke_agent
        // span (e0) below it, and each request's chat span below its agent's. Links already recorded are kept.
        List<GenAiSpanLink> links = [];
        foreach (NormalizedGenAiSpan span in spans.Select(item => item.Span))
        {
            string root = span.SpanId[..14] + "a0";
            links.Add(Link(span, root, null, null, "invoke_agent"));
            if (span.AgentName is not null)
            {
                links.Add(Link(span, span.ParentSpanId!, root, span.AgentName, "invoke_agent"));
            }

            links.Add(Link(span, span.SpanId, span.ParentSpanId, span.AgentName, "chat"));
        }

        await store.RecordSpanLinksAsync(links);
        await store.BackfillFromAgentSpansAsync(spans.Select(item => item.Span.TraceId));
        await new CircuitBreakerMonitor(store, new RecordingAlertSink()).EvaluateAsync(
            spans.Select(item => item.Span.TraceId));
```

Edit 3 of 5 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
        Slug);

    // trace and span are the last characters of 32- and 16-character IDs. GitHub-hosted models report cache counters, and
    // this example's Foundry BYOK and local endpoints (the spans with a serverAddress) don't.
    private static NormalizedGenAiSpan Chat(
        string trace,
```

with:

```csharp
        Slug);

    // trace and span are the last characters of 32- and 16-character IDs. GitHub-hosted models report cache counters, and
    // this example's Foundry BYOK and local endpoints (the spans with a serverAddress) don't. A request's parent is its
    // agent's invoke_agent span: the default agent's ends in a0, and a subagent's in e0.
    private static NormalizedGenAiSpan Chat(
        string trace,
```

Edit 4 of 5 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
        long cacheReadInputTokens = 0) =>
        TestSpans.Chat(
            startTime: Utc(start),
            requestModel: model,
```

with:

```csharp
        long cacheReadInputTokens = 0) =>
        TestSpans.Chat(
            parentSpanId: "5e8a1c3f7b9d" + trace.PadLeft(2, '0') + (agent is null ? "a0" : "e0"),
            startTime: Utc(start),
            requestModel: model,
```

Edit 5 of 5 in `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`, replace:

```csharp
            SpanId = "5e8a1c3f7b9d" + trace.PadLeft(2, '0') + span,
        };
}
```

with:

```csharp
            SpanId = "5e8a1c3f7b9d" + trace.PadLeft(2, '0') + span,
        };

    private static GenAiSpanLink Link(
        NormalizedGenAiSpan chat,
        string spanId,
        string? parentSpanId,
        string? agent,
        string operation) => new()
    {
        TraceId = chat.TraceId,
        SpanId = spanId,
        ParentSpanId = parentSpanId,
        OperationName = operation,
        AgentName = agent,
        ConversationId = chat.ConversationId,
        StartTime = chat.StartTime,
    };
}
```

Edit 1 of 2 in `docs/cli.md`, replace:

```markdown
| Command | Reports |
|---|---|
| `gh ledger status [--json]` | The daemon, ledger, rates, Copilot's export setting, warnings, and totals for today and the last 7 and 30 days |
| `gh ledger branch [--repo <key> \| --all-repos] [--since <period>] [--json]` | Spend by branch in the current repository, or in another or every repository; outside a Git repository, every repository |
| `gh ledger agents [--repo <key>] [--since <period>] [--json]` | Spend by agent in every repository, or in one with `--repo` |
| `gh ledger top-prompts [--repo <key>] [--since <period>] [--limit <n>] [--json]` | The most expensive prompt chains in every repository, or in one with `--repo`; `--limit` is 1 to 100 and defaults to 10 |

- A repository key is an `owner/repo` slug, a local repository root for a repository whose `origin` has no `owner/repo` path, or `(none)` for spend outside any repository.
```

with:

```markdown
| Command | Reports |
|---|---|
| `gh ledger status [--json]` | The daemon, ledger, rates, Copilot's export setting, warnings, and totals and cache hit ratios for today and the last 7 and 30 days |
| `gh ledger branch [--repo <key> \| --all-repos] [--since <period>] [--json]` | Spend by branch in the current repository, or in another or every repository; outside a Git repository, every repository |
| `gh ledger agents [--repo <key>] [--since <period>] [--json]` | Spend by agent in every repository, or in one with `--repo` |
| `gh ledger top-prompts [--repo <key>] [--since <period>] [--limit <n>] [--json]` | The most expensive prompt chains in every repository, or in one with `--repo`; `--limit` is 1 to 100 and defaults to 10 |
| `gh ledger status --cache ...` | Prompt-cache efficiency; see [Prompt-cache efficiency](#prompt-cache-efficiency) |

- A repository key is an `owner/repo` slug, a local repository root for a repository whose `origin` has no `owner/repo` path, or `(none)` for spend outside any repository.
```

Edit 2 of 2 in `docs/cli.md`, replace:

```markdown
- Spend is split into GitHub AI credits (`AI credits` and `GitHub USD`), Foundry BYOK (`BYOK USD`), and local models, whose `Local saved` is the cloud spend they avoided. `Unpriced` counts requests without a price; see [configuration.md](configuration.md).
- An agent shows as `(none)` when Copilot reports no agent name, as its default agent does. Built-in subagents such as `explore` report their own names, and a custom agent reports the `name` in its `.agent.md` file, or the file's name without `.agent.md`.

## Circuit breaker
```

with:

```markdown
- Spend is split into GitHub AI credits (`AI credits` and `GitHub USD`), Foundry BYOK (`BYOK USD`), and local models, whose `Local saved` is the cloud spend they avoided. `Unpriced` counts requests without a price; see [configuration.md](configuration.md).
- An agent shows as `(none)` when Copilot reports no agent name, as its default agent does. Built-in subagents such as `explore` report their own names, and a custom agent reports the `name` in its `.agent.md` file, or the file's name without `.agent.md`.
- `status --json` adds a `cache` object to each period, and each row of `branch --json` and `agents --json` adds `cacheReportedSpans`, `cacheUnreportedSpans`, and `cacheHitRatio`. See [Prompt-cache efficiency](#prompt-cache-efficiency).

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
```

Edit 1 of 1 in `docs/configuration.md`, replace:

```markdown
| `unrecognized_endpoint` | The endpoint isn't GitHub, Foundry, or local | Add its host to the matching `hostSuffixes` or `additionalHosts` list |
| `invalid_usage` | Token counts are inconsistent, for example more cached tokens than input tokens | Report it; it usually means a telemetry exporter bug |

## Why Foundry BYOK is unpriced by default
```

with:

```markdown
| `unrecognized_endpoint` | The endpoint isn't GitHub, Foundry, or local | Add its host to the matching `hostSuffixes` or `additionalHosts` list |
| `invalid_usage` | Token counts are inconsistent, for example more cached tokens than input tokens | Report it; it usually means a telemetry exporter bug |

### Cache tokens

A request's input tokens include the tokens the model read from its prompt cache and the tokens it wrote to it. Copilot reports the cache-read and cache-write counts only when they aren't zero. Cache reads are priced at `cachedInputPer1M`, cache writes at `cacheWritePer1M`, and the rest of the input at `inputPer1M`. When a request reports neither count, as many Foundry BYOK and local endpoints do, all of its input is priced as uncached input.

Cache-efficiency reports, such as `gh ledger status --cache`, don't treat such a request as a cache miss: its cache use is unknown, so hit ratios and observed cache totals leave it out, and a group of such requests shows `n/a` rather than `0.0%`. See [Prompt-cache efficiency](cli.md#prompt-cache-efficiency).

## Why Foundry BYOK is unpriced by default
```

Edit 1 of 2 in `docs/guide.md`, replace:

```markdown
- [Find the most expensive prompts](#find-the-most-expensive-prompts)
- [Compare agents](#compare-agents)
- [Set a budget for a branch](#set-a-budget-for-a-branch)
- [Catch a runaway loop](#catch-a-runaway-loop)
```

with:

```markdown
- [Find the most expensive prompts](#find-the-most-expensive-prompts)
- [Compare agents](#compare-agents)
- [Find context bloat](#find-context-bloat)
- [Set a budget for a branch](#set-a-budget-for-a-branch)
- [Catch a runaway loop](#catch-a-runaway-loop)
```

Edit 2 of 2 in `docs/guide.md`, replace:

```markdown
<!-- /sample -->

`(none)` is Copilot's default agent, which reports no name. Subagents such as `explore`, and your custom agents, report their own names.

## Set a budget for a branch
```

with:

````markdown
<!-- /sample -->

`(none)` is Copilot's default agent, which reports no name. Subagents such as `explore`, and your custom agents, report their own names.

## Find context bloat

**Goal:** find the prompts whose context grows on every request, and see how much of it the prompt cache absorbs.

Every model request sends the conversation so far, so an agent's input grows as it works. Input that the model's prompt cache already holds costs much less than uncached input. See how much of the input the cache served, by model:

<!-- sample: cache -->
```shell
gh ledger status --cache
```

```text
Cache efficiency by model in all repositories (last 30 days)

Model            Spans  Cache reported      Input  Cache read  Cache write   Uncached   Output  Hit %
claude-opus-5.5      7               7  2,900,000   1,450,000            0  1,450,000  127,000  50.0%
mai-code-2           2               2  1,100,000     500,000            0    600,000   55,000  45.5%
qwen3.8-27b          1               0    300,000         n/a          n/a        n/a    9,000    n/a
gpt-6.1-sol          1               0    200,000         n/a          n/a        n/a   10,000    n/a

2 of 11 spans reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.
```
<!-- /sample -->

`Hit %` is the share of input read from the cache. This example's Foundry BYOK and local endpoints report no cache counters, so their rows show `n/a`: unknown, not a 0% hit rate. Add `--by branch`, `--by pr`, or `--by agent` to group the rows another way.

Find the prompts that sent the most uncached input, and those whose input grew the most:

<!-- sample: cache-bloat -->
```shell
gh ledger status --cache --bloat --limit 3
```

```text
Context bloat in all repositories (last 30 days)

Prompt chains by uncached input
#  Started           Repository      Spans      Input  Uncached  Hit %  Trace
1  2026-11-21 11:20  contoso/webapp      4  2,000,000   800,000  60.0%  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2
2  2026-11-21 09:05  contoso/webapp      2    700,000   450,000  35.7%  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00a1
3  2026-11-03 10:30  contoso/webapp      1    300,000   300,000   0.0%  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00a7

Prompt chains by input growth within one agent invocation and model
#  Started           Repository      Agent   Model            Requests  First input  Peak input   Growth  Trace
1  2026-11-21 11:20  contoso/webapp  (none)  claude-opus-5.5         4      357,500     642,500  285,000  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2

Prompt chains with requests that reported no cache counters, by estimated uncached input
#  Started           Repository      Spans  Cache reported    Input  Est. uncached  Trace
1  2026-11-21 15:40  contoso/webapp      1               0  300,000        300,000  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00d4
2  2026-11-21 14:10  contoso/webapp      1               0  200,000        200,000  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00c3

Est. uncached is an estimate: it counts all input of a request without cache counters as uncached, as pricing does.
```
<!-- /sample -->

The prompt that tripped the circuit breaker leads both lists. See each of its requests:

<!-- sample: cache-turns -->
```shell
gh ledger status --cache --turns --trace 0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2
```

```text
Model requests in trace 0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2

#  Started              Agent   Model              Input  Cache read  Cache write  Uncached  Output  Hit %   Growth
1  2026-11-21 11:20:00  (none)  claude-opus-5.5  357,500     150,000            0   207,500  20,000  42.0%        -
2  2026-11-21 11:21:30  (none)  claude-opus-5.5  452,500     250,000            0   202,500  20,000  55.2%  +95,000
3  2026-11-21 11:23:00  (none)  claude-opus-5.5  547,500     350,000            0   197,500  20,000  63.9%  +95,000
4  2026-11-21 11:24:30  (none)  claude-opus-5.5  642,500     450,000            0   192,500  20,000  70.0%  +95,000

Growth is the change in input from the previous request by the same agent invocation and model; - marks its first request.
```
<!-- /sample -->

Each request added 95,000 tokens of context. The cache served more of it each time, but every request still sent about 200,000 uncached tokens. Growth compares a request only with earlier requests of the same agent invocation and model, so a subagent's requests never mix with its parent's. To see every request of a Copilot session, use `--conversation <id>` instead of `--trace`, with the session ID that `--json` output shows as `conversationId`. For the columns and the JSON fields, see [Prompt-cache efficiency](cli.md#prompt-cache-efficiency).

## Set a budget for a branch
````

### Tests

- **GS1:** the guide has exactly the checked samples, now including `cache`, `cache-bloat`, and `cache-turns`.
- **GS2** (3 new cases): the three samples are exactly what the commands print in the Month state.
- Every other guide sample (`status`, `branch`, `top-prompts`, `agents`, budgets, the breaker, `export-pr`, the receipt, and `mcp`) still passes unchanged.

### Acceptance criteria

- `docs/cli.md`, `docs/configuration.md`, and `docs/guide.md` are updated, and `GuideSamplesTests` passes.
- `docs/configuration.md` matches the pricing behavior: absent counters are priced as uncached and reported as unknown.
- `dotnet test CopilotLedger.slnx` passes **804** tests (801 + 3).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Test specifications

Every test is hermetic: it uses `TempDirectory` for files and databases, `ManualTimeProvider` for time, `FakeCliPlatform` and `CliHarness` for the CLI, and `McpTestHarness` or in-process pipes for MCP. No test starts a process, runs `git`, uses the network or Docker, or reads `~/.copilot-ledger`. Money and ratios are `decimal` (`TheoryData<…, decimal>` or `decimal` arguments, never `[InlineData(double)]`), and expected text is compared exactly, with `ReplaceLineEndings("\n")` only on the expected literal.

| Task | New tests (cases) | Changed tests | Total after |
|---|---|---|---:|
| 1 | NZ13 (7), NZ14, CM1 (8), CM2, CM3 (9) | — | 697 |
| 2 | Resolve_PullRequestCheckout_ReadsTheNumberFromTheMergeRef, Resolve_MergeConfig_YieldsOnlyExplicitPullRequestRefs (19), Resolve_DetachedHead_HasNoPullRequest, Resolve_LinkedWorktree_ReadsTheMergeRefFromTheCommonConfig, PullRequestNumber_DefaultsToNullAndTakesPartInEquality, CG4 | — | 721 |
| 3 | S12, S13 (7), SL11, SL12, IG10 | S1, SL1, SL8, SL10 (schema version 4) | 732 |
| 4 | S14, S15 | — | 734 |
| 5 | CE1 (5), CE2, CE3, CE4, CE5 | — | 743 |
| 6 | TB1, TB2, TB3, TB4, CB1, CB2 | — | 749 |
| 7 | RP14, RP15 | StatusText; GS2 `status-first` and `status` (guide text) | 751 |
| 8 | CR1 (5), CR2, CR3, CR4 (8), CR5, CR6 | — | 768 |
| 9 | CR7, CR8, CR9, CR10, CR11 (14) | — | 786 |
| 10 | MC8 (13 more cases), MC13, MC14 | MC1, MC2, MC4, MC6, MC11; GS7 (guide text) | 801 |
| 11 | GS2 (3 more cases: `cache`, `cache-bloat`, `cache-turns`) | GS1 (guide text) | 804 |

Each task's "Tests" subsection lists what its tests check, and its code gives every assertion. Decimal test vectors used across tasks, all exact `decimal` quotients in the invariant culture:

| Quotient | Value | Where |
|---|---|---|
| 3,662 / 7,429 | `0.4929331000134607618791223583` | CM1, CE1 |
| 1,450,000 / 2,600,000 | `0.5576923076923076923076923077` | CM1 |
| 1,450,000 / 2,700,000 | `0.537037037037037037037037037` | GS7 (the guide's `mcp` sample) |
| 1,200,000 / 2,200,000 | `0.5454545454545454545454545455` | S14, RP14 |
| 2 / 3 (800,000 / 1,200,000 or 10,000 / 15,000) | `0.6666666666666666666666666667` | CM1, S14, RP15, CR2, CR6, TB2, CB1, MC13, MC14 |
| 840,000 / 1,250,000 | `0.672` | MC13 |
| 1 / 6 | `0.1666666666666666666666666667` | CE1 |
| 900 / 1,300 | `0.6923076923076923076923076923` | CE3 |
| 1,600 / 3,500 | `0.4571428571428571428571428571` | CE4 |
| 3,500 / 6,000 | `0.5833333333333333333333333333` | CE4 |
| 4,500 / 7,000 | `0.6428571428571428571428571429` | CE4 |
| 22,000 / 38,000 | `0.5789473684210526315789473684` | CB1, CR10 |
| 8,000 / 30,000 | `0.2666666666666666666666666667` | CR3 |
| 160,000 / 3 | `53333.333333333333333333333333` | MC13 (`averageInputTokensPerTurn`) |

Percentages round half away from zero to one decimal place: `0.537037…` → `53.7%`, `0.527027…` → `52.7%`, `0.4875` → `48.8%` (the midpoint case, in the guide's `status` sample), `0.5454…` → `54.5%`, `0.6666…` → `66.7%`, `0.2666…` → `26.7%`, `0.16666…` → `16.7%`, and a reported `0` → `0.0%`.

## Final step: the pull request (orchestrator, not a builder task)

After Task 11, the orchestrator:

1. Runs `dotnet build CopilotLedger.slnx` (0 warnings, 0 errors) and `dotnet test CopilotLedger.slnx` (804 passed, 0 failed, 0 skipped), and checks with `git --no-pager diff --stat origin/main` that the branch changes only this plan, `.github/plans/archive/sprint-5.md`, and the files the tasks list.
2. Pushes `swigerb-prompt-cache-efficiency-per-turn-tokens` and opens one pull request, "Sprint 6: Prompt-cache efficiency & per-turn token breakdown", whose body says `Closes #62`, lists the eleven task commits, and states deviations D1–D5 from "Design decisions", each with a one-line rationale, and the bias that D1 leaves (see "Cache usage is per span").
3. Waits for CI on `ubuntu-latest` and `windows-latest`, including the smoke test of the published binary.
4. Optionally, with a real Copilot CLI session sending telemetry to a daemon whose `COPILOT_LEDGER_HOME` is a temporary directory, runs `gh ledger status --cache --turns --conversation <session id>` and checks that requests show `reported` states and that a first request shows a cache write with `0` cache reads. That re-checks D1's assumption on the installed Copilot CLI, which this sprint couldn't capture again.
5. Asks `ledger-architect` to review the branch.

This sprint doesn't bump the version, tag, or release. When the sprint closes, `ledger-architect` archives this plan with its amendments.

## Deferred to later sprints

- **Migration numbers for #61 and #63.** This sprint takes schema version 4. Dual-lens costing (#61) and `gh ledger import` (#63) take the next free numbers in merge order, 5 and up, and their plans must say so.
- **#61, dual-lens costing.** Retail cache-read and cache-write rates apply to the stored counters, which keep pricing an absent counter as 0 (all input uncached). Keep that pricing rule separate from the metrics rule (`CacheMetrics.IsReported`, `ReportedSql`).
- **#63, `gh ledger import`.** Map Copilot's `session.usage_record` `cacheDetailsReported` onto `CacheCounterState`: `Reported` for both counters when it's true (a 0 is then a reported 0) and `Absent` when it's false. `RecordAsync` rejects `LegacyUnknown`, so imported rows can't use it unless that rule changes.
- **#64, `gh ledger export`.** The public contracts it can chart are `GetCacheEfficiencyAsync` with `CacheEfficiencyRow` (including pull request rows), `GetTurnBreakdownAsync` with `TurnTokenRow`, and `GetContextBloatAsync` with `ContextBloatReport`.
- **#65, `gh ledger audit`.** Cache-miss streaks over `GetTurnBreakdownAsync` rows, counting only rows where `CacheMetrics.IsReported` (their `CacheReadInputTokens` isn't null) and keeping sequences apart by `AgentSpanId`.
- **User turns.** Group requests by `github.copilot.turn_id` or `interaction_id`, and mark `github.copilot.request.type = compaction` requests, which shrink input and could hide growth. Both need the attributes captured and stored first.
- **Agent definitions.** `gen_ai.agent.id`, which the reference calls a stable agent-definition identifier, could group the same agent across invocations, alongside `agent_span_id` for instances.
- **MCP tools for turns and bloat**, for example `get_turn_breakdown` and `get_context_bloat`, if agents need them; this sprint exposes them only in the CLI.
- **A fresh capture of cache attributes** from Copilot CLI, the GitHub Copilot app, and VS Code, to re-check that a zero counter is always left out (D1's assumption).
- **Carried from Sprint 5:** `gh ledger` in every message, workflow dependency lockfiles, the guide on macOS and Linux, reconciliation with `nano_aiu`, breaker state in `status`, MCP registration in `install`, `COPILOT_LEDGER_HOME` in sign-in items, headless Linux, a tool gate, webhooks, smarter retry churn, signed receipts and a pre-push hook, `export-pr --since`, upgrading without stopping everything by hand, build provenance, Arm64 smoke tests, checking for newer rates automatically, receipts for fork pull requests, and the Sprint 2 carry-overs, as described in `.github/plans/archive/sprint-5.md`.
