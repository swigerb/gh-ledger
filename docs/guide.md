# CopilotLedger user guide

CopilotLedger records what GitHub Copilot spends and shows it by repository, branch, model, agent, and prompt. This guide walks through everyday tasks, one scenario at a time. For every command and option, see the [command reference](cli.md).

The samples come from one example: gh-ledger installed as the GitHub CLI extension on Windows, used in the `contoso/webapp` repository on the `feature/search` branch at 16:30 on 2026-11-21, local time (UTC-5). After setup, every sample starts from the same state: a month of spend, a rates overlay with Foundry BYOK prices, a budget for `feature/search`, and an open circuit-breaker trip, so early samples show things that later sections explain. A test runs every sample, so each one is exactly what gh-ledger prints. Your numbers will differ. gh-ledger's messages name its commands `gh-ledger <command>`; with the extension, run them as `gh ledger <command>`.

- [Set up CopilotLedger](#set-up-copilotledger)
- [See what you spent](#see-what-you-spent)
- [Find the most expensive prompts](#find-the-most-expensive-prompts)
- [Compare agents](#compare-agents)
- [Set a budget for a branch](#set-a-budget-for-a-branch)
- [Catch a runaway loop](#catch-a-runaway-loop)
- [Post a receipt on a pull request](#post-a-receipt-on-a-pull-request)
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
Rates:   2026-11-20
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
Today             1         0       15.00       $0.15     $0.00        $0.00
Last 7 days       1         0       15.00       $0.15     $0.00        $0.00
Last 30 days      1         0       15.00       $0.15     $0.00        $0.00
```
<!-- /sample -->

If no spans appear, fix any warnings that `status` shows, and see [Troubleshooting](cli.md#troubleshooting).

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
Rates:   2026-11-20+contoso-2026-11
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
Today             8         0      617.75       $6.18     $0.49        $0.17
Last 7 days      10         0      764.00       $7.64     $0.49        $0.17
Last 30 days     11         0      908.00       $9.08     $0.49        $0.17
```
<!-- /sample -->

<!-- sample: branch -->
```shell
gh ledger branch
```

```text
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

**Goal:** find the prompts that cost the most.

<!-- sample: top-prompts -->
```shell
gh ledger top-prompts --limit 3
```

```text
Top 3 prompts by cost in all repositories (last 30 days)

#  Started           Repository      Agents       Models           Spans  AI credits  GitHub USD  BYOK USD  Local saved  Trace
1  2026-11-21 11:20  contoso/webapp  -            claude-opus-5.5      4      504.00       $5.04     $0.00        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2
2  2026-11-03 10:30  contoso/webapp  explore      claude-opus-5.5      1      144.00       $1.44     $0.00        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00a7
3  2026-11-19 10:00  contoso/webapp  code-review  claude-opus-5.5      1      120.00       $1.20     $0.00        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00e5
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

Agent        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
(none)           8         0      625.25       $6.25     $0.49        $0.17
explore          2         0      162.75       $1.63     $0.00        $0.00
code-review      1         0      120.00       $1.20     $0.00        $0.00
```
<!-- /sample -->

`(none)` is Copilot's default agent, which reports no name. Subagents such as `explore`, and your custom agents, report their own names.

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
> - **Circuit breaker:** 1 trip • max single-prompt burn: 504.00 credits
>
> <details>
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
>     "localSavedUsd": 0.1725
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
>       "localSavedUsd": 0
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
>       "localSavedUsd": 0
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
>       "localSavedUsd": 0
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
>       "localSavedUsd": 0.1725
>     }
>   ]
> }
> ```
>
> </details>
<!-- /sample -->

Before you merge, run `gh ledger export-pr` again and push, so the receipt includes the latest spend; the action updates the same comment. See the [action's README](https://github.com/swigerb/copilot-ledger-action) for its inputs and the checks it makes.

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
C:\Users\you\.copilot-ledger\rates.json is valid. Rates version: 2026-11-20+contoso-2026-11.
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
  "aiCredits": 617.75,
  "aiCreditsUsd": 6.1775,
  "foundryByokUsd": 0.49,
  "localSavedUsd": 0.1725,
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
Updated rates from 2026-11-20 to 2026-12-15.
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
