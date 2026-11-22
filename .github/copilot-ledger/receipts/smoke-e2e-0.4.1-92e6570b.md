<!-- copilot-ledger-receipt -->
### CopilotLedger receipt for `smoke/e2e-0.4.1`

- **GitHub AI Credits:** 304.53 credits ($3.05) across 6 turns: `claude-opus-5.5` 300.00, `mai-code-2` 4.53
- **Foundry BYOK:** none
- **Local offload:** none
- **Circuit breaker:** 1 trip • max single-prompt burn: 300.00 credits

<details>
<summary>Details and data</summary>

| Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | Cloud spend avoided |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| GitHub AI Credits | `claude-opus-5.5` | 2 | 0 | 600,000 | 30,000 | 300.00 credits ($3.00) | $0.00 |
| GitHub AI Credits | `mai-code-2` | 4 | 0 | 98,748 | 4,115 | 4.53 credits ($0.05) | $0.00 |

Repository `swigerb/gh-ledger` • 4 prompts • commit `0fa029b61050` • generated 2026-11-22 04:23 UTC by gh-ledger 0.4.1

To check these totals, run `gh ledger branch --since all --json` in the repository and compare the `smoke/e2e-0.4.1` row.

```json
{
  "schemaVersion": 1,
  "repository": "swigerb/gh-ledger",
  "branch": "smoke/e2e-0.4.1",
  "commit": "0fa029b610503f5f7bd9cb3006d1e00f11a11a27",
  "generatedAt": "2026-11-22T04:23:14.5645858+00:00",
  "generator": "gh-ledger 0.4.1",
  "totals": {
    "spans": 6,
    "unpricedSpans": 0,
    "inputTokens": 698748,
    "outputTokens": 34115,
    "cacheReadInputTokens": 30208,
    "cacheWriteInputTokens": 0,
    "aiCredits": 304.53127,
    "aiCreditsUsd": 3.0453127,
    "foundryByokUsd": 0,
    "localSavedUsd": 0
  },
  "prompts": 4,
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
      "spans": 4,
      "unpricedSpans": 0,
      "inputTokens": 98748,
      "outputTokens": 4115,
      "cacheReadInputTokens": 30208,
      "cacheWriteInputTokens": 0,
      "aiCredits": 4.53127,
      "aiCreditsUsd": 0.0453127,
      "foundryByokUsd": 0,
      "localSavedUsd": 0
    }
  ]
}
```

</details>
