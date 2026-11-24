<!-- copilot-ledger-receipt -->
### gh-ledger receipt for `smoke/e2e-0.5.0`

- **GitHub AI Credits:** 180.83 credits ($1.81) across 6 turns: `claude-opus-5.5` 177.20, `claude-haiku-4.5` 3.63
- **Foundry BYOK:** none
- **Local offload:** none
- **API-equivalent (retail):** $1.81 at API list prices • estimated: 3 turns reported no cache counters
- **Circuit breaker:** 1 trip • max single-prompt burn: 177.20 credits

<details>
<summary>Details and data</summary>

| Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | API-equivalent | Cloud spend avoided |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| GitHub AI Credits | `claude-opus-5.5` | 4 | 0 | 400,000 | 20,000 | 177.20 credits ($1.77) | $1.77 (estimate) | $0.00 |
| GitHub AI Credits | `claude-haiku-4.5` | 2 | 0 | 41,396 | 90 | 3.63 credits ($0.04) | $0.04 | $0.00 |

Repository `swigerb/gh-ledger` • 3 prompts • commit `ba399c34f4f1` • generated 2026-11-24 16:17 UTC by gh-ledger 0.5.0

To check these totals, run `gh ledger branch --since all --json` in the repository and compare the `smoke/e2e-0.5.0` row.

```json
{
  "schemaVersion": 1,
  "repository": "swigerb/gh-ledger",
  "branch": "smoke/e2e-0.5.0",
  "commit": "ba399c34f4f129f881e3c6a01314609664824856",
  "generatedAt": "2026-11-24T16:17:46.5381415+00:00",
  "generator": "gh-ledger 0.5.0",
  "totals": {
    "spans": 6,
    "unpricedSpans": 0,
    "inputTokens": 441396,
    "outputTokens": 20090,
    "cacheReadInputTokens": 73836,
    "cacheWriteInputTokens": 27540,
    "aiCredits": 180.82786,
    "aiCreditsUsd": 1.8082786,
    "foundryByokUsd": 0,
    "localSavedUsd": 0,
    "apiEquivalentUsd": 1.8082786,
    "apiEquivalentUnpricedSpans": 0,
    "apiEquivalentEstimatedSpans": 3
  },
  "prompts": 3,
  "maxPromptCredits": 177.2,
  "breakerTrips": 1,
  "models": [
    {
      "channel": "github_ai_credits",
      "model": "claude-opus-5.5",
      "spans": 4,
      "unpricedSpans": 0,
      "inputTokens": 400000,
      "outputTokens": 20000,
      "cacheReadInputTokens": 60000,
      "cacheWriteInputTokens": 0,
      "aiCredits": 177.2,
      "aiCreditsUsd": 1.772,
      "foundryByokUsd": 0,
      "localSavedUsd": 0,
      "apiEquivalentUsd": 1.772,
      "apiEquivalentUnpricedSpans": 0,
      "apiEquivalentEstimatedSpans": 3
    },
    {
      "channel": "github_ai_credits",
      "model": "claude-haiku-4.5",
      "spans": 2,
      "unpricedSpans": 0,
      "inputTokens": 41396,
      "outputTokens": 90,
      "cacheReadInputTokens": 13836,
      "cacheWriteInputTokens": 27540,
      "aiCredits": 3.62786,
      "aiCreditsUsd": 0.0362786,
      "foundryByokUsd": 0,
      "localSavedUsd": 0,
      "apiEquivalentUsd": 0.0362786,
      "apiEquivalentUnpricedSpans": 0,
      "apiEquivalentEstimatedSpans": 0
    }
  ]
}
```

</details>
