<!-- copilot-ledger-receipt -->
### CopilotLedger receipt for `smoke/e2e-0.4.1`

- **GitHub AI Credits:** 301.53 credits ($3.02) across 5 turns: `claude-opus-5.5` 300.00, `mai-code-2` 1.53
- **Foundry BYOK:** none
- **Local offload:** none
- **Circuit breaker:** 1 trip • max single-prompt burn: 300.00 credits

<details>
<summary>Details and data</summary>

| Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | Cloud spend avoided |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| GitHub AI Credits | `claude-opus-5.5` | 2 | 0 | 600,000 | 30,000 | 300.00 credits ($3.00) | $0.00 |
| GitHub AI Credits | `mai-code-2` | 3 | 0 | 58,748 | 115 | 1.53 credits ($0.02) | $0.00 |

Repository `swigerb/gh-ledger` • 3 prompts • commit `15003fbc7123` • generated 2026-11-22 04:17 UTC by gh-ledger 0.4.1

To check these totals, run `gh ledger branch --since all --json` in the repository and compare the `smoke/e2e-0.4.1` row.

```json
{
  "schemaVersion": 1,
  "repository": "swigerb/gh-ledger",
  "branch": "smoke/e2e-0.4.1",
  "commit": "15003fbc7123ce05803a70d6b0128bb764c0b41a",
  "generatedAt": "2026-11-22T04:17:19.2320939+00:00",
  "generator": "gh-ledger 0.4.1",
  "totals": {
    "spans": 5,
    "unpricedSpans": 0,
    "inputTokens": 658748,
    "outputTokens": 30115,
    "cacheReadInputTokens": 30208,
    "cacheWriteInputTokens": 0,
    "aiCredits": 301.53127,
    "aiCreditsUsd": 3.0153127,
    "foundryByokUsd": 0,
    "localSavedUsd": 0
  },
  "prompts": 3,
  "maxPromptCredits": 300,
  "breakerTrips": 1,
  "models": [
    {
      "channel": "github_ai_credits",
      "model": "claude-opus-5.5",
      "spans": 2,
      "unpricedSpans": 0,
      "inputTokens": 600000,
      "outputTokens": 30000,
      "cacheReadInputTokens": 0,
      "cacheWriteInputTokens": 0,
      "aiCredits": 300,
      "aiCreditsUsd": 3,
      "foundryByokUsd": 0,
      "localSavedUsd": 0
    },
    {
      "channel": "github_ai_credits",
      "model": "mai-code-2",
      "spans": 3,
      "unpricedSpans": 0,
      "inputTokens": 58748,
      "outputTokens": 115,
      "cacheReadInputTokens": 30208,
      "cacheWriteInputTokens": 0,
      "aiCredits": 1.53127,
      "aiCreditsUsd": 0.0153127,
      "foundryByokUsd": 0,
      "localSavedUsd": 0
    }
  ]
}
```

</details>
