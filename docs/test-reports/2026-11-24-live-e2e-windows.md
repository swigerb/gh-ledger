# Live end-to-end test: gh-ledger 0.5.0 on Windows 11

On 2026-11-24, gh-ledger 0.5.0 was installed as a GitHub CLI extension on a working Windows 11 machine and tested end to end:
- live prompts through Copilot CLI;
- an import of the machine's real Copilot history;
- a reconciliation of the ledger with Copilot's own per-request charges;
- every report, the MCP server, audit, exports, and receipts;
- a privacy audit of everything gh-ledger wrote.

This report records what was run, what was measured, and the gaps found. Each gap was filed as an issue.

The machine owner's own history appears only as counts and percentages, and the only repository named is this one. The live test's own spend is shown in full: 22.44 AI credits ($0.22).

## Results

| Area | Result | Issues |
|---|---|---|
| Installation | Works, but the daemon dies with its shell when `install` runs inside a kill-on-close job, as in the GitHub Copilot app's agent shells. | [#78](https://github.com/swigerb/gh-ledger/issues/78) |
| Live capture | 11 prompts were recorded with the right repository, branch, agent, model, and tokens. 10 match Copilot's charge exactly; the Auto (Copilot Fusion) prompt is overstated by 43%. | [#76](https://github.com/swigerb/gh-ledger/issues/76) |
| MCP server | Works in a live prompt and over stdio, but `get_audit_findings` takes 27.6 s per call. | [#83](https://github.com/swigerb/gh-ledger/issues/83) |
| Budgets | The alert fired once, at the right total. Its hint names `gh-ledger`, which isn't on `PATH` for the extension. | [#81](https://github.com/swigerb/gh-ledger/issues/81) |
| Import | 61,323 requests and 79,932 tool calls recorded, and all 19 live spans recognized as overlaps. BYOK and failed compactions keep 17 logs incomplete, a rerun that records nothing takes 192 s, and about 13% of imported tool calls are attached to the wrong request. | [#82](https://github.com/swigerb/gh-ledger/issues/82), [#85](https://github.com/swigerb/gh-ledger/issues/85), [#79](https://github.com/swigerb/gh-ledger/issues/79), [#75](https://github.com/swigerb/gh-ledger/issues/75) |
| Pricing | 77 of 79 sessions equal Copilot's charge to the nano AIU. The other 2 differ only by the Auto and Copilot Fusion discounts, which gh-ledger doesn't apply: +9.9% overall. | [#76](https://github.com/swigerb/gh-ledger/issues/76) |
| Reports and cache views | Correct, and 0.1–0.4 s on 61,343 requests. | |
| Audit | Works, and 2 s for 7 days of every repository. Noisy: one rule makes 98% of the findings. Scoped to one repository, it takes 28 s. | [#80](https://github.com/swigerb/gh-ledger/issues/80), [#83](https://github.com/swigerb/gh-ledger/issues/83) |
| Export and receipts | Correct, offline, and verifiable, but scoped exports take 84–135 s. | [#83](https://github.com/swigerb/gh-ledger/issues/83) |
| Privacy | None of 900 content samples, 500 of them from the sessions gh-ledger imported or recorded, appear in anything it wrote. | |
| Supply chain | The binary's SHA-256 matched the release's, but it's unsigned, and the release has no provenance attestation. | [#77](https://github.com/swigerb/gh-ledger/issues/77) |

## Environment

- **Machine:** Windows 11 Pro 10.0.26300, x64, with GitHub CLI 2.103.3.
- **Copilot CLI:** the WinGet package, auto-updated to 1.0.115. The GitHub Copilot app was also in use on the same machine.
  - Started with `--no-auto-update`, the launcher runs the version built into it, 1.0.103: `copilot --version --no-auto-update` prints 1.0.103.
  - Prompts p1–p10 used that flag, so they ran 1.0.103; p11 ran 1.0.115.
- **gh-ledger 0.5.0:** installed with `gh extension install swigerb/gh-ledger`, at `%LOCALAPPDATA%\GitHub CLI\extensions\gh-ledger\gh-ledger.exe`.
- **Before the install:** there was no `~/.copilot-ledger`, port 4318 was free, and there were no `OTEL_*` or `COPILOT_*` user variables, `~/.codex`, or `~/.claude`.
- **Copilot's session store:** 463 session folders, 300 of them with an `events.jsonl`, 4.4 GB in all.

## Method

- **Live prompts:** each prompt ran `copilot -p <prompt> --model <model> --session-id <new GUID>` from this repository's working tree, as a new terminal would after `gh ledger install`:
  - no `COPILOT_*`, `OTEL_*`, `GH_TOKEN`, or `GITHUB_TOKEN` variables, except the user-scope `COPILOT_OTEL_ENABLED=true` that `install` set;
  - the real `~/.copilot` and the stored login.
- **Comparison:** each prompt's cost was compared in three places:
  - the CLI's `AI Credits` line;
  - the ledger's rows, read with SQLite in read-only mode;
  - the session log's `copilotUsage.totalNanoAiu`, where 1 AI credit is 10^9 nano AIU.
- **Scripts:** the reconciliation and privacy checks were small, read-only Python scripts. They read only the metadata fields named below and printed only aggregates, never session IDs or content. They aren't in the repository, which allows no Python.

## Installation

| Step | Result |
|---|---|
| `gh extension install swigerb/gh-ledger` | Installed 0.5.0. Its SHA-256 equals the release asset's. `Get-AuthenticodeSignature`: `NotSigned` ([#77](https://github.com/swigerb/gh-ledger/issues/77)). |
| `gh ledger install --dry-run` | Listed exactly the changes below, and made none. |
| `gh ledger install` (0.7 s) | Set the user variable `COPILOT_OTEL_ENABLED=true`, added the `CopilotLedger` Run value (`"…\gh-ledger.exe" daemon start`), wrote `install.json` and `daemon.json` (`importRules: 1`), and started the daemon on 127.0.0.1 and ::1, port 4318. |
| `gh ledger status` | Daemon running, OTel export enabled, rates 2026-11-23, no warnings. |

**Gap ([#78](https://github.com/swigerb/gh-ledger/issues/78)).** `install` ran in the GitHub Copilot app's agent shell. That shell runs in a job object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` and without breakaway, and the daemon started in the same job.
- **Repro:** a contained repro used a temporary `COPILOT_LEDGER_HOME`, port 4461, and a job of its own. `daemon start` succeeded. Closing the job killed the daemon without a `daemon.log` line, and `status` then said it wasn't running.
- **Workaround for this test:** run `gh ledger daemon stop`, then start the Run value's command outside the job with `Invoke-CimMethod Win32_Process -MethodName Create`.

## Live capture

| Run | Scenario | CLI | Model | Spans | Tool calls | gh-ledger AI credits | Copilot's charge |
|---|---|---|---|---:|---:|---:|---:|
| p1 | One prompt | 1.0.103 | `claude-haiku-4.5` | 1 | 0 | 3.3075 | 3.3075 |
| p2 | One prompt | 1.0.103 | `gpt-6-luna` | 1 | 0 | 0.2731425 | 0.2731425 |
| p3 | No cache counters | 1.0.103 | `gemini-3.8-flash` | 1 | 0 | 2.94165 | 2.94165 |
| p4 | No API list price | 1.0.103 | `mai-code-2-flash` | 1 | 0 | 0.186715 | 0.186715 |
| p5 | One prompt | 1.0.103 | `grok-4.7` | 1 | 0 | 4.4926 | 4.4926 |
| p6 | Tools, `--allow-tool 'shell(git:*)'` | 1.0.103 | `claude-haiku-4.5` | 5 | 5 | 3.52805 | 3.52805 |
| p7 | The `explore` sub-agent | 1.0.103 | `claude-haiku-4.5`, `gpt-5.6-luna` | 4 | 2 | 2.390773 | 2.390773 |
| p8 | gh-ledger's MCP server | 1.0.103 | `claude-haiku-4.5` | 4 | 3 | 3.10412 | 3.10412 |
| p9 | Budget alert | 1.0.103 | `claude-haiku-4.5` | 1 | 0 | 1.82113 | 1.82113 |
| p10 | `--model auto` (Copilot Fusion) | 1.0.103 | `mai-code-2-flash` | 1 | 0 | **0.18657** | **0.130599** |
| p11 | The current CLI | 1.0.115 | `mai-code-2-flash` | 1 | 0 | 0.172228 | 0.172228 |

**Attribution.** Every span was recorded with `swigerb/gh-ledger` and this branch. Copilot CLI's spans carry no Git attributes, so the daemon read them from the session's `workspace.yaml`. The `explore` sub-agent's spans were recorded with its name and model.

**The CLI's line.** It shows each prompt's charge rounded to cents: 3.31, 0.27, 2.94, 0.19, 4.49, 3.53, 2.39, 3.1, 1.82, 0.13, and 0.17.

**Runs with something to note.**
- **p3:** Gemini reported no cache counters. `status --cache` shows `n/a`, and its API-equivalent amount is marked `~`, as documented.
- **p4:** no API-equivalent price, because MAI models have no API list price. It's marked `*`.
- **p10 ([#76](https://github.com/swigerb/gh-ledger/issues/76)):** Copilot charged 0.7 times the rate card, the Copilot Fusion discount, but gh-ledger recorded the full list price.
  - Copilot reports the charge on the live `chat` span as `github.copilot.nano_aiu`, verified with the file exporter on 1.0.103 and 1.0.115. gh-ledger doesn't read it.
  - The two Auto prompts that captured span attributes ran in a temporary `COPILOT_HOME` with the file exporter, so the ledger didn't record them. Copilot charged 0.0882308 credits for them, which the total spend above includes.
- **p11:** confirms live capture on Copilot CLI 1.0.115, an open risk of the 0.5.0 release. `import` still skips that session as unsupported ([#75](https://github.com/swigerb/gh-ledger/issues/75)); its live span already counts.

### MCP server

**In a live prompt (p8).** The server was registered for one prompt with `--additional-mcp-config` and `--allow-tool ledger`, leaving `~/.copilot/mcp-config.json` untouched. The model called `get_branch_ledger` and answered `spans=14 aiCredits=17.1204305`, the ledger's exact values before that prompt.

**Over stdio, from this repository.** `initialize` took 0.18 s, and the server reported version 0.5.0. `tools/list` listed 6 tools, all marked read-only except `set_branch_budget`.

| Tool | Arguments | Time |
|---|---|---:|
| `get_branch_ledger` | None | 0.12 s |
| `get_top_expensive_prompts` | `limit` 3 | 0.05 s |
| `get_agent_breakdown` | None | 0.03 s |
| `get_cache_efficiency` | None; then `by` agent, `since` all | 0.03 s each |
| `get_audit_findings` | None; then `since` 30d, `minSeverity` warning | **27.6 s** and **27.5 s** ([#83](https://github.com/swigerb/gh-ledger/issues/83)) |

### Budget alert

For p9, the branch had spent 20.22 AI credits, and a budget of 20.33 was set on it. After the prompt, the daemon logged this once:

```text
ALERT gh-ledger branch budget exceeded: swigerb/gh-ledger @ swigerb-capability-gap-issues: 22.05 of 20.33 AI credits spent. Run: gh-ledger budget list
```

`budget list` showed the branch over budget, and `budget clear` removed the budget. The hint names `gh-ledger`, which isn't on `PATH` for the extension ([#81](https://github.com/swigerb/gh-ledger/issues/81)).

## Import

`gh ledger import --all` read the real session store with the daemon running. All three runs exited with code 2, because some coverage was incomplete.

| | Dry run | Import | Rerun |
|---|---:|---:|---:|
| Time | 8.7 s | 236.2 s | 192.3 s |
| Sessions scanned | 300 | 300 | 300 |
| Skipped, unchanged | 0 | 0 | 62 |
| Skipped, active | 7 | 7 | 7 |
| Unsupported, from before Copilot 1.0.103 | 214 | 214 | 214 |
| Records parsed | 61,459 | 61,459 | 18,662 |
| Recorded | 61,323 | 61,323 | 0 |
| Duplicate | 0 | 0 | 18,545 |
| Overlap-skipped | 19 | 19 | 0 |
| Malformed lines | 2 | 2 | 2 |
| Unsupported usage shapes | 117 | 117 | 117 |
| Unpriced | 4,412 | 4,412 | 0 |
| Tool calls recorded | 79,936 | 79,932 | 0 |

- **Overlap:** the 19 overlap-skipped records are exactly the 19 live spans recorded by then, matched by service request ID. Nothing was charged twice.
- **Malformed lines:** both are lines over 8 MiB, in one log.
- **Totals:** the dry run's AI credits equal the import's.
- **Tool calls:** the dry run counted 4 more because import keeps tool calls from the last 7 days, and that window moved between the runs. The earliest imported tool call starts less than a minute after the point 7 days before the import began.
- **Size:** the import grew the ledger from 0.83 MB to 96.4 MB.
- **Speed:** the dry run reads and prices every record in 8.7 s, so writing the ledger takes most of the import's time.

**Gaps ([#82](https://github.com/swigerb/gh-ledger/issues/82)).** The 117 unsupported usage shapes are compactions that import can't record:
- 104 successful BYOK compactions, which have no service request ID;
- 2 failed compactions that Copilot charged;
- 11 failed compactions that it didn't charge.

Every log with such a record stays incomplete. Those 17 logs make every `import` exit with code 2, make every export and audit report incomplete coverage, and are read again on every run. The rerun that recorded nothing took 192 s, resolving each of 18,545 records as a duplicate at about 10 ms each.

**Tool calls ([#85](https://github.com/swigerb/gh-ledger/issues/85)).** Import attaches each tool call to the latest usage record of its agent in file order. In these logs, a record that asked for tools is often followed directly by another record of the same agent before its tools start. So:
- of the 85,144 tool calls in these sessions, 6,597 (7.7%) are attached to another request of the same prompt, and 4,123 (4.8%) to a request of another prompt;
- in the 7-day tool history, 6,295 imported requests miss 11,130 tool calls, and 501 own 10,066 that they didn't ask for, although only about 1,064 requested tool calls never started.

Linking each tool call through the `apiCallId` of the `assistant.message` that asked for it reproduces each usage record's `numToolCalls` exactly for 97.6% of records.

**Local model ([#79](https://github.com/swigerb/gh-ledger/issues/79)).** 4,412 of the imported requests (7.2%) went to a BYOK model, `qwen3.8-27b-cuda-gpu:1`, which by its name runs on a local GPU.
- Copilot logs that provider as `openai`, so import records the requests as `other_provider`, as `docs/configuration.md` documents.
- So the history shows no local savings, and no overlay setting can safely mark them local.

### Reconciliation with Copilot's charges

The ledger was compared with the 79 supported, inactive sessions' own accounting:
- `data.usage.copilotUsage.totalNanoAiu` of each `session.usage_record`;
- `data.compactionTokensUsed.copilotUsage.totalNanoAiu` of each compaction.

The sessions have 59,720 usage records and 1,741 compactions. 4,412 usage records, those of the BYOK local model, have no charge, and neither do 117 compactions.

| Check | Result |
|---|---|
| Sessions whose gh-ledger total equals Copilot's charge, to the nano AIU | **77 of 79** |
| The other 2 | Exactly the sessions with Auto or Copilot Fusion requests |
| Usage records charged at 1.00 times the rate card | 46,289 |
| Usage records charged at 0.70 times, all Auto or Fusion | 9,019 |
| Compactions charged at 1.00 times | 1,447 |
| Compactions charged at 0.70 times | 177 |
| Records charged at any other ratio | 0 |
| gh-ledger's total compared with Copilot's | **+9.9%** |
| Where the difference comes from | Exactly the discount on the imported Auto and Fusion records (+9.905%), less the charge of the 2 failed compactions that Copilot charged and import skips (−0.005%) |

**What matches.** gh-ledger prices every record that Copilot charged at the rate card's price exactly. Rebuilding each record's amount from the logs at list price reproduces the ledger to the nano AIU. The 77 sessions' totals match Copilot's charge to the nano AIU, and in the other 2, every record matches individually except the discounted ones. That covers every GitHub-billed model, long-context tier, cache-write price, and promotional price window in this history, and vendor model IDs such as `claude-opus-5-5`.

**What doesn't ([#76](https://github.com/swigerb/gh-ledger/issues/76)).**
- **The discounts:** GitHub documents a 10% discount for auto model selection and a limited-time 30% discount for Copilot Fusion.
- **Auto runs as Fusion:** Auto selections now run as Fusion, so every Auto request here was charged at 0.70 times the rate card.
- **gh-ledger charges list price:** it prices those requests at list price, although the span's `github.copilot.nano_aiu` and the log's `totalNanoAiu` already give the exact charge.

## Reports, audit, export, and receipts

Every command ran from this repository against the real ledger, with the daemon running. The ledger held 61,343 requests and 79,942 tool calls. Times are wall-clock, including the process start.

| Command | Exit | Time |
|---|---:|---:|
| `status`, `status --json` | 0 | 0.4 s |
| `branch`, `branch --all-repos --since all` | 0 | 0.2–0.3 s |
| `agents --since all`, `top-prompts --since all --limit 5` | 0 | 0.2 s |
| `status --cache --since all`, with `--by agent` or `--by pr` | 0 | 0.2 s |
| `status --cache --bloat --since all --limit 5` | 0 | 0.3 s |
| `status --cache --turns --conversation <p10>` | 0 | 0.2 s |
| `audit` (7 days, every repository) | 0 | 2.0 s |
| `audit --since 30d --json --limit 200` | 0 | 2.1 s |
| `audit --repo swigerb/gh-ledger --since 30d` | 0 | **27.9 s** |
| `audit --fail-on high --min-severity high` | 2 | 1.9 s |
| `audit --local-llm http://localhost:11434/v1 … --show-llm-payload` | 0 | 1.9 s |
| `audit --local-llm http://10.0.0.5:11434/v1 …` | 1 | 0.1 s |
| `audit --local-llm http://127.0.0.1:11434/v1 …`, with no server running | 0 | 4.0 s |
| `export --all-repos --since 30d` | 0 | 4.0 s |
| `export --all-repos --since all --bundle` | 0 | 6.6 s |
| `export --since all --bundle --time-zone America/New_York` (this repository) | 0 | **134.9 s** |
| `export --all-repos --conversation <p10>` | 0 | **83.9 s** |
| `export-pr --stdout`, `export-pr --json` | 0 | 0.3 s |
| `rates validate`, `rates backfill-api-equivalent --dry-run` | 0 | 0.1–0.2 s |
| `breaker status`, `budget list` | 0 | 0.1 s |

### Results

- **Totals agree:** the reports, the receipt, and the export agree with each other and with the ledger.
  - The all-time export's AI credits equal the ledger's to the last digit, less p11's 0.172228, which ran after it.
  - The receipt's 20 turns and 22.2322505 AI credits equal the branch's row in `branch --since all --json` before p11.
- **Cache hit ratio:** 95.2% over 30 days, with 61,340 of 61,343 requests reporting cache counters.
- **`--local-llm`:**
  - **Remote hosts:** a host that isn't on loopback is refused: `use localhost, an address in 127.0.0.0/8, or [::1]`.
  - **No server running:** the audit warns that it couldn't reach the model and still exits with 0.
  - **The payload:** the one that `--show-llm-payload` prints uses `repo-1` and `branch-1` aliases, and holds no owner name, session ID, or trace ID.
- **`--fail-on high`:** exited with 2, as documented, because high findings existed.
- **Exports:**
  - The all-repositories export drew the latest 20,000 requests and tool calls on its timeline, and said so. Its totals include all 61,343 requests.
  - The HTML reports ran from 46 KB (one request) to 6.8 MB, and the bundles from 1.3 to 8.4 MB.
  - Each bundle had `report.html`, `manifest.json`, and 5 telemetry files. Every SHA-256 in the repository bundle's manifest verified, and so did those of an all-repositories bundle exported again later.
  - The `America/New_York` time zone resolved under invariant globalization.
- **In a browser:** this repository's 3.5 MB report, with 7,230 requests and 10,884 tool calls, rendered in about 0.2 s, with all nine sections and 22,930 SVG marks.

### Scoped queries ([#83](https://github.com/swigerb/gh-ledger/issues/83))

A repository or conversation scope is 13–21 times slower than the same command for every repository over the same period:
- **Why:** export's and audit's tool-call condition is a correlated `EXISTS` over the spans of the tool call's trace. When a trace has no selected span, every one of its spans is tested.
- **Trace size:** imported traces here reach 6,044 spans and 9,030 tool calls.
- **One count query:** 0.10 s unscoped, 27.5 s for one repository, and 24.7 s for one session.

### Audit findings ([#80](https://github.com/swigerb/gh-ledger/issues/80))

Over 7 days, 53,854 requests produced 34,026 findings:
- **`context_bloat` (info):** 33,446 of them, on 62% of all requests. Agent turns send large, mostly cached contexts and get short tool calls back, which matches the rule's trigger.
- **`tool_loop` (high):** over 30 days, 8 of the 10 high findings have a $0.00 or unknown impact, such as successful runs of `view` 17–24 times and of `grep` 95 times.
- **`premium_model_trivial_turn`:** over 30 days, 4 of its 7 findings (71 of 130 requests), including 1 of the 2 high ones, are requests whose model Auto or Copilot Fusion chose, not the user.
- **Real signals:** the rest look like real review signals: bursts of 64–71 requests in 5 minutes, failing tool calls retried 4 times in a row, and a cache-miss streak on the BYOK model.

### Documented limits, as observed

- **Sequence rules:** they skipped 32,341 requests (60%): imported sub-agent requests, which have no agent invocation.
- **Tool rules:** they reported the tool history as insufficient: tool calls are kept for 7 days, and 10,006 imported tool calls had no detail. Most of those 10,006 come from the attribution problem in [#85](https://github.com/swigerb/gh-ledger/issues/85).

Both rules' limits are described in [Audit for wasted spend](../cli.md#audit-for-wasted-spend).

## Privacy

**Content samples.** Two passes searched everything gh-ledger wrote for excerpts of the session logs' content. Each sample was a 40-character excerpt with at least two spaces, searched for as UTF-8, as escaped JSON, and as UTF-16. **Neither pass found any.**
- **Pass 1:** 400 samples, 100 each from user messages, assistant messages, tool results, and compaction summaries, taken from 120 randomly chosen session folders. It searched 81 files and archive entries, 302 MB in all:
  - `ledger.db`, `ledger.db-wal`, `daemon.log`, `install.json`, and `daemon.json`;
  - the 4 HTML reports and every entry of the 2 bundles;
  - the saved output of every command and MCP tool above.
- **Pass 2:** 500 samples, 100 each from user messages, assistant messages and reasoning, tool arguments, tool results, and compaction summaries. All came from the 81 sessions that gh-ledger imported or recorded live, whose metadata the ledger holds. It searched 71 files and archive entries, 269 MB in all: the same data files and `ledger.db-shm`, a new all-repositories export with every entry of its bundle, and the saved outputs.

**Ledger columns.** No text value in `spans` or `span_links` has two or more spaces. The longest are local repository roots, as documented, and a 129-character tool name made of two hexadecimal hashes.

**Reports.** Every HTML report has its Content Security Policy. None contains `http://`, `https://`, `<script src`, or `<link`.

**Local paths.** No export, bundle entry, receipt, audit output, or MCP result contains the user's profile path. Only `rates validate` printed it, in the terminal, as the overlay's path.

**Live spans.** Copilot's content capture was off, its default.

## Resource use

| Component | Measurement |
|---|---|
| Daemon | 61 MB private memory (127 MB working set), 13 threads, and 1.9 s of CPU time over its first 32 minutes. Those minutes included 20 live spans and the import. |
| Ledger | 101 MB for 61,343 requests and 79,942 tool calls |
| Reports | 0.1–0.4 s each; audit took 2 s for 7 days, and an unscoped export 4–7 s |

## Gaps found

| Issue | Severity | Gap |
|---|---|---|
| [#76](https://github.com/swigerb/gh-ledger/issues/76) | High | Auto and Copilot Fusion discounts aren't applied, so their requests' billed AI credits are 11% or 43% too high, +9.9% over this history. Copilot reports the exact charge. |
| [#83](https://github.com/swigerb/gh-ledger/issues/83) | Medium | Repository- and conversation-scoped export, audit, and MCP `get_audit_findings` take 25–135 s. |
| [#78](https://github.com/swigerb/gh-ledger/issues/78) | Medium | A daemon started from a kill-on-close job, such as a Copilot app agent shell, dies with it. |
| [#82](https://github.com/swigerb/gh-ledger/issues/82) | Medium | BYOK and failed compactions keep 17 logs incomplete: every import exits with code 2, every report shows incomplete coverage, and reruns that record nothing take 192 s. |
| [#85](https://github.com/swigerb/gh-ledger/issues/85) | Medium | Import attaches 13% of tool calls to the wrong request and 5% to the wrong prompt, which inflates the count of imported tool calls without detail about tenfold. |
| [#80](https://github.com/swigerb/gh-ledger/issues/80) | Low | The audit is noisy on agentic history. |
| [#79](https://github.com/swigerb/gh-ledger/issues/79) | Low | BYOK requests to a local OpenAI-compatible server import as `other_provider`, with no local savings. |
| [#81](https://github.com/swigerb/gh-ledger/issues/81) | Low | Alert hints name `gh-ledger` instead of `gh ledger`. |
| [#77](https://github.com/swigerb/gh-ledger/issues/77) | Low | The binaries are unsigned and have no build provenance attestations. |
| [#75](https://github.com/swigerb/gh-ledger/issues/75) | Existing | Import doesn't read Copilot CLI 1.0.115 logs. Live capture on 1.0.115 is verified; see the comment on the issue. |

## Reproduce

For more on these commands, see [Install and uninstall](../cli.md#install-and-uninstall) and [Import past sessions](../cli.md#import-past-sessions).

1. Install and start capturing:

   ```shell
   gh extension install swigerb/gh-ledger
   gh ledger install
   gh ledger status
   ```

   Run `install` from a regular terminal, not an agent's shell ([#78](https://github.com/swigerb/gh-ledger/issues/78)). Then restart the GitHub Copilot app, VS Code, and open terminals so they see `COPILOT_OTEL_ENABLED`.
2. Run a prompt and compare its charge. Without `--no-auto-update`, the CLI runs its updated version:

   ```shell
   copilot -p "Reply with exactly one word: ledger" --model mai-code-2-flash
   gh ledger status --cache --turns --conversation <session ID>
   ```

   Copilot's charge is `copilotUsage.totalNanoAiu` in the session's `events.jsonl`, under `~/.copilot/session-state/<session ID>/`.
3. Backfill, checking first what the import would record:

   ```shell
   gh ledger import --all --dry-run
   gh ledger import --all
   ```

4. To see what a live span carries, run one prompt with `COPILOT_HOME` set to a temporary folder and `COPILOT_OTEL_FILE_EXPORTER_PATH` set to a file in it. Copilot then writes its spans to that file instead of sending them to the daemon, and the stored login still works.

To undo the install, run `gh ledger uninstall`, then `gh extension remove ledger`. The ledger stays in `%USERPROFILE%\.copilot-ledger` until you delete it.
