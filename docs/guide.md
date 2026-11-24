# CopilotLedger user guide

CopilotLedger records what GitHub Copilot spends and shows it by repository, branch, model, agent, and prompt. This guide walks through everyday tasks, one scenario at a time. For every command and option, see the [command reference](cli.md).

The samples come from one example: gh-ledger installed as the GitHub CLI extension on Windows, used in the `contoso/webapp` repository on the `feature/search` branch at 16:30 on 2026-11-21, local time (UTC-5). After setup and backfill, every sample starts from the same state: a month of spend, a rates overlay with Foundry BYOK prices, a budget for `feature/search`, and an open circuit-breaker trip, so early samples show things that later sections explain. The [weekly waste review](#weekly-waste-review) uses a day of agent activity instead. A test runs every sample, so each one is exactly what gh-ledger prints. Your numbers will differ. gh-ledger's messages name its commands `gh-ledger <command>`; with the extension, run them as `gh ledger <command>`.

- [Set up CopilotLedger](#set-up-copilotledger)
- [Backfill history after installing](#backfill-history-after-installing)
- [See what you spent](#see-what-you-spent)
- [Find the most expensive prompts](#find-the-most-expensive-prompts)
- [Compare agents](#compare-agents)
- [Find context bloat](#find-context-bloat)
- [Weekly waste review](#weekly-waste-review)
- [Set a budget for a branch](#set-a-budget-for-a-branch)
- [Catch a runaway loop](#catch-a-runaway-loop)
- [Post a receipt on a pull request](#post-a-receipt-on-a-pull-request)
- [Attach a cost report to a pull request](#attach-a-cost-report-to-a-pull-request)
- [Price Foundry BYOK and local models](#price-foundry-byok-and-local-models)
- [Ask Copilot about spend](#ask-copilot-about-spend)
- [Update rates](#update-rates)
- [Upgrade or uninstall](#upgrade-or-uninstall)

## Set up CopilotLedger

**Goal:** record what Copilot spends from now on.

```shell
gh extension install swigerb/gh-ledger
```

<!-- sample: install -->
```shell
gh ledger install
```

```text
Set COPILOT_OTEL_ENABLED=true for your user account.
gh ledger runs this GitHub CLI extension, so no alias is needed.
gh-ledger will start when you sign in.
gh-ledger daemon started (pid 4242) on http://localhost:4318.
CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.
```
<!-- /sample -->

On macOS and Linux, `install` sets the variable in your shell profile instead. Restart the apps and terminals as it says, send Copilot a prompt, and check that the prompt was recorded:

<!-- sample: status-first -->
```shell
gh ledger status
```

```text
gh-ledger 0.4.1 (GitHub CLI extension)
Daemon:  running (pid 4242) on http://localhost:4318 since 2026-11-21 16:20
Ledger:  C:\Users\you\.copilot-ledger\ledger.db
Rates:   2026-11-23
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
Today             66.7%  1 of 1 span
Last 7 days       66.7%  1 of 1 span
Last 30 days      66.7%  1 of 1 span
```
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
Superseded, unmatched            0
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
Superseded, unmatched            0
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

**Goal:** see today's spend, and each branch's spend.

<!-- sample: status -->
```shell
gh ledger status
```

```text
gh-ledger 0.4.1 (GitHub CLI extension)
Daemon:  running (pid 4242) on http://localhost:4318 since 2026-11-21 08:02
Ledger:  C:\Users\you\.copilot-ledger\ledger.db
Rates:   2026-11-23+contoso-2026-11
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
Today             53.7%  6 of 8 spans
Last 7 days       52.7%  8 of 10 spans
Last 30 days      48.8%  9 of 11 spans
```
<!-- /sample -->

<!-- sample: branch -->
```shell
gh ledger branch
```

```text
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
- To compare with other prices, such as a price list your company negotiated, add `--pricing <file>` to `status`, `branch`, `agents`, `top-prompts`, or `export-pr`. Only the API-equivalent amounts change; see [Compare with other prices](configuration.md#compare-with-other-prices---pricing).
- `branch` covers the current repository. Add `--all-repos` for every repository, `--since` for another period, such as `today`, `7d`, `all`, or a date, and `--json` for exact amounts.

## Find the most expensive prompts

**Goal:** find the prompts that cost the most.

<!-- sample: top-prompts -->
```shell
gh ledger top-prompts --limit 3
```

```text
Top 3 prompts by cost in all repositories (last 30 days)

#  Started           Repository      Agents       Models           Spans  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved  Trace
1  2026-11-21 11:20  contoso/webapp  -            claude-opus-5.5      4      504.00       $5.04     $0.00          $5.04        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2
2  2026-11-03 10:30  contoso/webapp  explore      claude-opus-5.5      1      144.00       $1.44     $0.00          $1.44        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00a7
3  2026-11-19 10:00  contoso/webapp  code-review  claude-opus-5.5      1      120.00       $1.20     $0.00          $1.20        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00e5
```
<!-- /sample -->

Each row is one prompt you sent, with every model request Copilot made for it (`Spans`). `Agents` lists the named agents that worked on it, or `-` when only Copilot's default agent did. `top-prompts` and `agents` cover every repository unless you add `--repo`, for example `--repo contoso/webapp`.

## Compare agents

**Goal:** see how much each agent spends.

<!-- sample: agents -->
```shell
gh ledger agents
```

```text
Agents in all repositories (last 30 days)

Agent        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
(none)           8         0      625.25       $6.25     $0.49        ~$6.49*        $0.17
explore          2         0      162.75       $1.63     $0.00         $1.44*        $0.00
code-review      1         0      120.00       $1.20     $0.00          $1.20        $0.00

3 of 11 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.
1 priced span reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.
```
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

## Weekly waste review

**Goal:** find the requests and tool calls that wasted AI credits this week, and what to change.

`gh ledger audit` reviews the last 7 days of every repository with fixed rules: bursts of requests, tool loops, retried failing commands, bloated or growing context, prompt-cache misses, premium models on short requests without tool calls, and prompts near the circuit breaker. This section's sample uses a day of agent activity instead of the shared state:

<!-- sample: audit -->
```shell
gh ledger audit
```

```text
Audit of all repositories (last 7 days)
6 findings: 1 high, 2 warning, 3 info.

Severity  Rule                        Scope                                                     Window                  Key metrics                                          Est. impact
High      failed_tool_retry           contoso/webapp @ feature/search, explore, bash            2026-11-21 10:00-10:01  4 failures in a row                                        $0.01
Warning   premium_model_trivial_turn  contoso/webapp @ feature/search, (none), claude-opus-5.5  2026-11-21 09:00-09:22  12 tool-free requests of 200 or fewer output tokens        $0.33
Warning   cache_miss_streak           contoso/webapp @ feature/search, (none), mai-code-2       2026-11-21 13:00-13:05  6 requests, 0.0% cache hits, 240,000 uncached              $0.11
Info      context_bloat               contoso/webapp @ feature/search, (none), gpt-6.2-sol      2026-11-21 12:00        260,000 in, 1,000 out (260:1)                                  -
Info      context_growth              contoso/webapp @ feature/search, (none), gpt-6.2-sol      2026-11-21 14:00-14:04  20,000 to 92,000 input in 10 requests (4.6x)                   -
Info      velocity_near_breaker       contoso/webapp @ feature/search                           2026-11-21 15:00-15:03  125.00 of 150.00 AI credits in 5 min (83.3%)                   -

Requests: 42 analyzed (42 live).
Tool calls: 6 analyzed.
Impacts: billed USD (GitHub AI credits and Foundry BYOK) at rates 2026-11-23, economy model mai-code-2. Findings can overlap, so don't add their impacts up.
Tool history: complete for these requests (tool calls are kept for 7 days, since 2026-11-14 16:30).
Rules: all 8.

Recommendations:
  failed_tool_retry: Fix the cause of the failing command, such as a missing dependency or a wrong path, before the agent retries it, because every retry costs another model request.
  premium_model_trivial_turn: Consider a cheaper model or a local model for these short requests without tool calls; they're only candidates, because a short answer can still need a strong model.
  cache_miss_streak: Keep the start of each prompt stable, such as instructions, tool definitions, and earlier messages, so the prompt cache can serve it, and avoid switching models in the middle of a task.
  context_bloat: Send less context with each request: attach smaller files and tool results, trim long instructions, or start a new session for a new task.
  context_growth: Start a new session or compact the conversation when the context keeps growing, and keep tool results short.
  velocity_near_breaker: Check whether this burst of spend was expected; if it wasn't, lower the circuit breaker's credit velocity with gh ledger breaker config so it trips next time.
```
<!-- /sample -->

Each finding is a review signal, not proof of waste. The explore subagent retried a failing `bash` command four times in a row, one less than the circuit breaker allows, and the three model requests between the failures cost about a cent; fixing the command's cause saves every retry. Twelve short questions without tool calls went to claude-opus-5.5, whose output price is in the top quarter of the rates; at mai-code-2's prices they would have cost $0.33 less. The estimates are billed USD at the rates in use, and findings can overlap, so they aren't added up.

To see one rule, use `--rule`, and repeat it for more; to see only the findings that matter most, use `--min-severity warning`. `--json` writes the findings with their metrics, scope, and thresholds. In a script, `--fail-on high` exits with code 2 when a High finding appears:

```shell
gh ledger audit --since 1d --fail-on high
```

To ask a local model for suggestions, pass the OpenAI-compatible endpoint of a server on this machine, such as Ollama or Foundry Local. gh-ledger sends it only a summary of the findings, with aliases instead of repository and branch names, and never stores the answer:

```shell
gh ledger audit --local-llm http://127.0.0.1:11434/v1 --local-llm-model llama3.3
```

Add `--show-llm-payload` to see exactly what it would send, without sending it. For the rules, the JSON fields, and what the local model receives, see [Audit for wasted spend](cli.md#audit-for-wasted-spend).

## Set a budget for a branch

**Goal:** get an alert when a branch spends more than you planned.

<!-- sample: budget-set -->
```shell
gh ledger budget set 750
```

```text
Budget for contoso/webapp @ feature/search: 750.00 AI credits. Spent so far: 666.75.
```
<!-- /sample -->

<!-- sample: budget-list -->
```shell
gh ledger budget list
```

```text
contoso/webapp @ feature/search: 666.75 of 750.00 AI credits
```
<!-- /sample -->

Budgets count Foundry BYOK spend at $0.01 per AI credit, so the branch's 617.75 GitHub AI credits and $0.49 of BYOK spend make 666.75. When the branch passes its budget, the daemon shows a desktop notification once and writes this line to `daemon.log`:

<!-- sample: budget-alert -->
```text
2026-11-21T21:30:00.0000000+00:00 ALERT CopilotLedger branch budget exceeded: contoso/webapp @ feature/search: 766.75 of 750.00 AI credits spent. Run: gh-ledger budget list
```
<!-- /sample -->

A budget doesn't block anything. Set it again to re-arm the alert, or remove it with `gh ledger budget clear`.

## Catch a runaway loop

**Goal:** notice when an agent burns credits in a loop.

The daemon trips the circuit breaker when one prompt spends more than 150 AI credits in 5 minutes, or makes 5 failed tool calls in a row. A trip shows a desktop notification and writes an `ALERT` line to `daemon.log`; it doesn't stop the agent. To see what tripped:

<!-- sample: breaker-status -->
```shell
gh ledger breaker status
```

```text
Circuit breaker: open (1 trip)
Rules: more than 150.00 AI credits in 5 minutes on one prompt; 5 failed tool calls in a row on one prompt
Flag file: off. Notifications: on.

Trip 1 at 2026-11-21 11:21: contoso/webapp @ feature/search: 252.00 AI credits in 5 minutes on one prompt
  rule credit_velocity, trace 0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2, session 7d4e1c2a-5b3f-4e8d-9a6c-0000000000b2
Reset a trip with: gh-ledger breaker reset --id <id>
```
<!-- /sample -->

When you've dealt with the loop, close the branch's open trips:

<!-- sample: breaker-reset -->
```shell
gh ledger breaker reset
```

```text
Reset 1 trip.
```
<!-- /sample -->

To change the limits, or to have the daemon write a flag file that a wrapper script can watch, see [Circuit breaker](cli.md#circuit-breaker).

## Post a receipt on a pull request

**Goal:** show a branch's AI spend on its pull request.

Add this workflow to the repository as `.github/workflows/copilot-ledger-receipt.yml`:

```yaml
name: CopilotLedger receipt

on:
  pull_request:
    types: [opened, synchronize, reopened]

permissions:
  contents: read
  pull-requests: write

concurrency:
  group: copilot-ledger-receipt-${{ github.event.pull_request.number }}
  cancel-in-progress: true

jobs:
  receipt:
    runs-on: ubuntu-latest
    steps:
      - uses: swigerb/copilot-ledger-action@v1
```

On the pull request's branch, write the branch's receipt, then commit and push it:

<!-- sample: export-pr -->
```shell
gh ledger export-pr
```

```text
Wrote .github/copilot-ledger/receipts/feature-search-38e7a3a2.md for feature/search.
Commit it on the branch and push; the CopilotLedger action posts it on the pull request.
```
<!-- /sample -->

```shell
git add .github/copilot-ledger/receipts
git commit -m "Add CopilotLedger receipt"
git push
```

The action posts the receipt as a comment on the pull request:

<!-- sample: receipt -->
> <!-- copilot-ledger-receipt -->
> ### CopilotLedger receipt for `feature/search`
>
> - **GitHub AI Credits:** 617.75 credits ($6.18) across 6 turns: `claude-opus-5.5` 599.00, `mai-code-2` 18.75
> - **Foundry BYOK:** $0.49 across 1 turn: `gpt-6.1-sol` $0.49
> - **Local offload:** 309,000 tokens across 1 turn ($0.00 billed • $0.17 cloud spend avoided): `qwen3.8-27b` 309,000
> - **API-equivalent (retail):** $6.49 at API list prices • 2 turns unpriced • estimated: 1 turn reported no cache counters
> - **Circuit breaker:** 1 trip • max single-prompt burn: 504.00 credits
>
> <details>
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
>
> To check these totals, run `gh ledger branch --since all --json` in the repository and compare the `feature/search` row.
>
> ```json
> {
>   "schemaVersion": 1,
>   "repository": "contoso/webapp",
>   "branch": "feature/search",
>   "commit": "4f9c2e1d8b7a6c5d4e3f2a1b0c9d8e7f6a5b4c3d",
>   "generatedAt": "2026-11-21T21:30:00.0000000+00:00",
>   "generator": "gh-ledger 0.4.1",
>   "totals": {
>     "spans": 8,
>     "unpricedSpans": 0,
>     "inputTokens": 3200000,
>     "outputTokens": 129000,
>     "cacheReadInputTokens": 1450000,
>     "cacheWriteInputTokens": 0,
>     "aiCredits": 617.75,
>     "aiCreditsUsd": 6.1775,
>     "foundryByokUsd": 0.49,
>     "localSavedUsd": 0.1725,
>     "apiEquivalentUsd": 6.49,
>     "apiEquivalentUnpricedSpans": 2,
>     "apiEquivalentEstimatedSpans": 1
>   },
>   "prompts": 4,
>   "maxPromptCredits": 504,
>   "breakerTrips": 1,
>   "models": [
>     {
>       "channel": "github_ai_credits",
>       "model": "claude-opus-5.5",
>       "spans": 5,
>       "unpricedSpans": 0,
>       "inputTokens": 2400000,
>       "outputTokens": 95000,
>       "cacheReadInputTokens": 1450000,
>       "cacheWriteInputTokens": 0,
>       "aiCredits": 599,
>       "aiCreditsUsd": 5.99,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0,
>       "apiEquivalentUsd": 5.99,
>       "apiEquivalentUnpricedSpans": 0,
>       "apiEquivalentEstimatedSpans": 0
>     },
>     {
>       "channel": "foundry_byok",
>       "model": "gpt-6.1-sol",
>       "spans": 1,
>       "unpricedSpans": 0,
>       "inputTokens": 200000,
>       "outputTokens": 10000,
>       "cacheReadInputTokens": 0,
>       "cacheWriteInputTokens": 0,
>       "aiCredits": 0,
>       "aiCreditsUsd": 0,
>       "foundryByokUsd": 0.49,
>       "localSavedUsd": 0,
>       "apiEquivalentUsd": 0.5,
>       "apiEquivalentUnpricedSpans": 0,
>       "apiEquivalentEstimatedSpans": 1
>     },
>     {
>       "channel": "github_ai_credits",
>       "model": "mai-code-2",
>       "spans": 1,
>       "unpricedSpans": 0,
>       "inputTokens": 300000,
>       "outputTokens": 15000,
>       "cacheReadInputTokens": 0,
>       "cacheWriteInputTokens": 0,
>       "aiCredits": 18.75,
>       "aiCreditsUsd": 0.1875,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0,
>       "apiEquivalentUsd": null,
>       "apiEquivalentUnpricedSpans": 1,
>       "apiEquivalentEstimatedSpans": 0
>     },
>     {
>       "channel": "local",
>       "model": "qwen3.8-27b",
>       "spans": 1,
>       "unpricedSpans": 0,
>       "inputTokens": 300000,
>       "outputTokens": 9000,
>       "cacheReadInputTokens": 0,
>       "cacheWriteInputTokens": 0,
>       "aiCredits": 0,
>       "aiCreditsUsd": 0,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0.1725,
>       "apiEquivalentUsd": null,
>       "apiEquivalentUnpricedSpans": 1,
>       "apiEquivalentEstimatedSpans": 0
>     }
>   ]
> }
> ```
>
> </details>
<!-- /sample -->

Before you merge, run `gh ledger export-pr` again and push, so the receipt includes the latest spend; the action updates the same comment. See the [action's README](https://github.com/swigerb/copilot-ledger-action) for its inputs and the checks it makes.

## Attach a cost report to a pull request

**Goal:** give reviewers a report of a branch's AI spend that opens anywhere, with the data behind it.

In the repository, export the branch's report and its bundle:

<!-- sample: export -->
```shell
gh ledger export --branch feature/search --bundle
```

```text
Wrote copilot-ledger-report.html: 8 requests in contoso/webapp, branch feature/search (last 30 days).
Wrote copilot-ledger-report.zip: report.html, manifest.json, and 5 telemetry files.
```
<!-- /sample -->

Open `copilot-ledger-report.html` in any browser. It works offline and without a server, and every chart has a table, so it reads well without JavaScript too. Attach both files to the pull request, or to an audit ticket. The ZIP holds the report, the data as JSON, and `manifest.json`, which lists each file's size and SHA-256 hash, so a reviewer can check that the files weren't changed after you wrote them.

- `--time-zone America/New_York` shows dates, times, and nights in another time zone than this computer's.
- `--since all` covers the branch's whole history instead of the last 30 days.
- `--include-local-paths` shows the local roots of repositories without a GitHub remote, which otherwise show as their folder names.
- `--generated-at` with the manifest's `generatedAtUtc` writes the same files again from an unchanged ledger.

See [Export a report](cli.md#export-a-report) for every option and the bundle's format.

## Price Foundry BYOK and local models

**Goal:** see what your Microsoft Foundry BYOK models cost, and what local models save you.

CopilotLedger records requests to local runtimes, such as Foundry Local and Ollama, at $0.00 and estimates what they saved. Foundry bills BYOK requests to you directly, not through GitHub, so CopilotLedger records them unpriced until you add your prices. Create a rates overlay:

```shell
gh ledger rates init
```

Then edit `rates.json` in your data directory, `C:\Users\you\.copilot-ledger` here or `~/.copilot-ledger` on macOS and Linux, and list your models with the prices from your Azure pricing or invoice:

<!-- sample: rates-overlay -->
```json
{
  "schemaVersion": 1,
  "ratesVersion": "contoso-2026-11",
  "foundryByok": {
    "models": [
      {
        "id": "gpt-6.1-sol",
        "aliases": ["contoso-gpt61-prod"],
        "windows": [
          { "tiers": [ { "name": "default", "inputPer1M": 1.75, "cachedInputPer1M": 0.175, "outputPer1M": 14.00 } ] }
        ]
      }
    ]
  }
}
```
<!-- /sample -->

List your deployment names as `aliases`, because Foundry requests often report them instead of the model name. Check the file:

<!-- sample: rates-validate -->
```shell
gh ledger rates validate
```

```text
C:\Users\you\.copilot-ledger\rates.json is valid. Rates version: 2026-11-23+contoso-2026-11.
```
<!-- /sample -->

Restart the daemon, with `gh ledger daemon stop` and then `gh ledger daemon start`, so it prices new requests with your rates. BYOK spend then shows under `BYOK USD`, as in [See what you spent](#see-what-you-spent). For custom endpoints and local model names, see [configuration.md](configuration.md).

## Ask Copilot about spend

**Goal:** let Copilot check the branch's spend, budget, and circuit breaker while it works.

Register gh-ledger's MCP server with Copilot CLI. In PowerShell on Windows:

```powershell
copilot mcp add ledger -- "$env:LOCALAPPDATA\GitHub CLI\extensions\gh-ledger\gh-ledger.exe" mcp
```

On macOS and Linux:

```shell
copilot mcp add ledger -- ~/.local/share/gh/extensions/gh-ledger/gh-ledger mcp
```

Then ask Copilot, for example, "How much has this branch cost so far?" Copilot calls the `get_branch_ledger` tool. While the breaker is open or the branch is over its budget, the result starts with a `WARNING:` line; the data follows as JSON, indented here:

<!-- sample: mcp -->
```text
WARNING: CopilotLedger circuit breaker is open for contoso/webapp @ feature/search: 252.00 AI credits in 5 minutes on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1
{
  "repository": "contoso/webapp",
  "branch": "feature/search",
  "spans": 8,
  "unpricedSpans": 0,
  "inputTokens": 3200000,
  "outputTokens": 129000,
  "cacheReadInputTokens": 1450000,
  "cacheWriteInputTokens": 0,
  "uncachedInputTokens": 1250000,
  "cacheReportedSpans": 6,
  "cacheHitRatio": 0.537037037037037037037037037,
  "aiCredits": 617.75,
  "aiCreditsUsd": 6.1775,
  "foundryByokUsd": 0.49,
  "localSavedUsd": 0.1725,
  "apiEquivalentUsd": 6.49,
  "apiEquivalentUnpricedSpans": 2,
  "apiEquivalentEstimatedSpans": 1,
  "billedAiCredits": 666.75,
  "topModel": "claude-opus-5.5",
  "budget": {
    "aiCredits": 750,
    "spentAiCredits": 666.75,
    "exceeded": false
  },
  "circuitBreaker": {
    "state": "open",
    "openTrips": [
      {
        "id": 1,
        "rule": "credit_velocity",
        "repository": "contoso/webapp",
        "branch": "feature/search",
        "traceId": "0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2",
        "conversationId": "7d4e1c2a-5b3f-4e8d-9a6c-0000000000b2",
        "currentSession": false,
        "trippedAt": "2026-11-21T16:21:32.0000000+00:00",
        "detail": "252.00 AI credits in 5 minutes",
        "resetCommand": "gh-ledger breaker reset --id 1"
      }
    ]
  },
  "warnings": [
    "WARNING: CopilotLedger circuit breaker is open for contoso/webapp @ feature/search: 252.00 AI credits in 5 minutes on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1"
  ]
}
```
<!-- /sample -->

The warning tells Copilot to stop and ask you before it continues. No tool can reset the breaker, and Copilot asks before it changes a budget. For the other tools, and for VS Code, see [MCP server](cli.md#mcp-server).

## Update rates

**Goal:** price new requests with GitHub's latest prices before the next gh-ledger release.

<!-- sample: rates-update -->
```shell
gh ledger rates update
```

```text
Updated rates from 2026-11-23 to 2026-12-15.
Restart the daemon to price new spans with them: gh ledger daemon stop, then gh ledger daemon start.
```
<!-- /sample -->

Restart the daemon as it says. Requests already recorded keep the prices they were recorded with, and your overlay applies on top of the new rates.

## Upgrade or uninstall

**Goal:** move to a new gh-ledger release, or remove CopilotLedger.

The daemon runs the extension's binary, and so does every Copilot session that uses the `ledger` MCP server. Windows can't replace that file while either runs it, so before you upgrade, close the Copilot CLI sessions, the GitHub Copilot app, and VS Code if they use the MCP server, and stop the daemon:

<!-- sample: daemon-stop -->
```shell
gh ledger daemon stop
```

```text
gh-ledger daemon stopped.
```
<!-- /sample -->

```shell
gh extension upgrade ledger
```

Then start the daemon again, and reopen your Copilot apps:

<!-- sample: daemon-start -->
```shell
gh ledger daemon start
```

```text
gh-ledger daemon started (pid 4242) on http://localhost:4318.
```
<!-- /sample -->

To remove CopilotLedger, undo `install`:

<!-- sample: uninstall -->
```shell
gh ledger uninstall
```

```text
gh-ledger daemon stopped.
gh-ledger will no longer start when you sign in.
Removed COPILOT_OTEL_ENABLED from your user account.
CopilotLedger is uninstalled. Your ledger data is still in C:\Users\you\.copilot-ledger.
```
<!-- /sample -->

If you registered the MCP server, remove it, and remove the `ledger` entry from VS Code's `mcp.json` if you added one:

```shell
copilot mcp remove ledger
```

Then close the Copilot sessions and apps that still run it, and remove the extension:

```shell
gh extension remove ledger
```

Your ledger stays in your data directory; delete the directory to remove it too.
