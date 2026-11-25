# Configuring gh-ledger

gh-ledger needs no configuration. This page describes where it keeps its files, how it prices requests, and how to change prices with an optional rates overlay.

## Where gh-ledger keeps its files

The data directory is `~/.copilot-ledger` (`%USERPROFILE%\.copilot-ledger` on Windows), or the directory that the `COPILOT_LEDGER_HOME` environment variable names. The daemon that starts at sign-in sees that variable only if your sign-in environment sets it.

| File | Purpose |
|---|---|
| `ledger.db` | The SQLite ledger, in WAL mode, so a `ledger.db-wal` file can hold its latest changes |
| `rates.json` | Your rates overlay, if you create one |
| `rates.downloaded.json` | Rates saved by `gh ledger rates update` |
| `daemon.json` | The running daemon's process ID, start time, endpoint, version, and database path |
| `daemon.lock` | Held while the daemon runs, so only one daemon runs at a time |
| `daemon.stop` | Written by `gh ledger daemon stop` to ask the daemon to stop |
| `daemon.log` | The log of a daemon started by `gh ledger daemon start` or at sign-in |
| `alert-toast.xml` | Windows only: the latest desktop notification, which the daemon shows through PowerShell |
| `install.json` | What `gh ledger install` changed, so `gh ledger uninstall` can undo it |

The ledger never stores prompt or response content. It records each repository's `owner/repo` name and local folder, never its remote URL.

## How usage is priced

gh-ledger prices model requests: spans whose `gen_ai.operation.name` is `chat`, `text_completion`, or `generate_content`, never other spans such as `invoke_agent`. Each request gets the first billing channel that matches, in this order:

| Channel | Detected by | Recorded |
|---|---|---|
| Local | An endpoint of `localhost`, a loopback address, or a host in `localOffload.additionalHosts`; the provider name `ollama`, `lm_studio`, `foundry_local`, or `llama_cpp`; or no endpoint and a model in `localOffload.models`, such as Qwen3.8-27B | $0.00 billed, and estimated savings: the same tokens at the GitHub rates of `localOffload.baselineModel`, MAI Code 2 by default |
| Microsoft Foundry BYOK | An endpoint under `services.ai.azure.com`, `openai.azure.com`, or `cognitiveservices.azure.com`, or the provider name `azure.ai.inference` | USD at your prices in `rates.json`, and 0 AI credits; unpriced until you add prices |
| GitHub AI credits | No endpoint, or an endpoint under `githubcopilot.com` | AI credits and USD, at $0.01 per AI credit |
| Unpriced | Anything else | Tokens only, with a [pricing status](#pricing-statuses) that says why |

**Charged amount and list price.** For a GitHub request, gh-ledger records the charge that Copilot reports, `github.copilot.nano_aiu` on a live span or `copilotUsage.totalNanoAiu` in a session log, where 1,000,000,000 nano AI units are 1 AI credit. The charge includes discounts, such as those for Auto model selection and Copilot Fusion. gh-ledger also records the list price: the same tokens at GitHub's published rates, which it charges when Copilot reports no charge. A reported charge that isn't a whole number of nano AI units from 0 to 2^53, or a nonzero charge on a request that GitHub didn't bill at a known price, is rejected, and the request is charged at the rate card. Budgets, the circuit breaker, reports, receipts, export, and audit use the charge.

### Built-in prices

The built-in GitHub prices cover 34 models, with their long-context tiers and promotional prices, from [Models and pricing for GitHub Copilot](https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing) as of 2026-11-20. Promotional prices end at 00:00 UTC.

- Claude Opus 4.8 (fast mode) isn't built in, because the model name that Copilot reports for it isn't known. If you use it, add it under `githubAiCredits.models`.
- GitHub hasn't published the prices that follow the promotions of Gemini 4 Argon, which ends on 2026-12-01, and Gemini 3.8 Flash, which ends on 2027-01-01. Later requests for them are `no_rate_for_date` until you add a price window or a gh-ledger update adds the price.

### Which model prices a request

A request can report the model that answered it (`gen_ai.response.model`) and the model it asked for (`gen_ai.request.model`). Copilot CLI, for example, can report a Claude Haiku 4.5 request's response model as `claude-haiku-4-5-20251001`. A name matches a model's `id` or one of its `aliases`, ignoring case, with spaces read as `-`. Built-in GitHub and API-equivalent models list their vendors' API ids and dated snapshot ids as aliases.

- A GitHub or Foundry BYOK request is priced as its response model when the channel has that model, and otherwise as its request model. It's recorded with that model's `id`.
- When the channel has neither model, the request is `unknown_model` on GitHub or `byok_unpriced` on Foundry BYOK, recorded with its response model, or with its request model when it reports none.
- A local request matches `localOffload.models` by its response model, or by its request model when it reports none. Its savings don't depend on the model.

### Pricing statuses

| Status | Meaning | Fix |
|---|---|---|
| `priced` | Priced from a rate card | — |
| `local_offload` | Local: $0.00 billed, savings estimated | — |
| `byok_unpriced` | Foundry BYOK, but neither the response model nor the request model has a price | Add the model, or an alias, under `foundryByok.models` |
| `unknown_model` | GitHub, but neither model has a price, or the request names no model | Add the model, or an alias, under `githubAiCredits.models` |
| `no_rate_for_date` | The model has no price for the request's date | Add a price window that covers the date |
| `unrecognized_endpoint` | The endpoint isn't GitHub, Foundry, or local | Add its host to the matching `hostSuffixes` or `additionalHosts` list |
| `other_provider` | An imported request billed by a provider that isn't Foundry BYOK or local, such as a Codex request to OpenAI; it bills nothing | If the provider is local or Foundry, add its name to `localOffload.providerNames` or `foundryByok.providerNames` |
| `invalid_usage` | Inconsistent token counts, such as more cached tokens than input tokens | Report it; it usually means a telemetry exporter bug |

### Cache tokens

A request's input tokens include those it read from the prompt cache and those it wrote to it. Cache reads are priced at `cachedInputPer1M`, cache writes at `cacheWritePer1M`, and the rest at `inputPer1M`.

Copilot reports cache counts only when they aren't zero. A request that reports neither, as many Foundry BYOK and local endpoints do, has unknown cache use: its input is priced as uncached, and cache reports such as `gh ledger status --cache` leave it out of hit ratios, showing `n/a` rather than `0.0%`. A cache miss that writes nothing reports neither count either, so a hit ratio can read higher than the real one. See [Prompt-cache efficiency](cli.md#prompt-cache-efficiency).

### API-equivalent prices

gh-ledger also records what each request would cost at its model's API list price, from the `apiEquivalent` section of the rates (see [API list prices](#api-list-prices)). This API-equivalent cost is a comparison only: it's never billed, and budgets and the circuit breaker ignore it.

- A request is priced as the model it's recorded with, or as its request model when no section of the rates has that name; a Foundry BYOK request that reports `gpt-6.1-sol-2026-10-01` is priced as `gpt-6.1-sol`. A name that the rates have is never replaced, so a request that MAI Code 2 answered is unpriced here.
- A local request is priced as the local baseline. Nothing else depends on the channel: an `other_provider` request, or one to an unrecognized endpoint, is still priced when its model has a list price.
- A request without a list price for its date, or with inconsistent token counts, has no API-equivalent cost, never $0.00.
- Cache reads and writes are priced at their list prices. A request without cache counters is priced as all uncached input, and reports mark its cost as an estimate.
- Each request keeps its result, with the rates version and the SHA-256 of the list prices that gave it. A request recorded without one counts as unpriced until `gh ledger rates backfill-api-equivalent` prices it with the rates in use, as of its start time; the backfill changes nothing else, and `--dry-run` only counts.
- A cost above the ledger's limit of about $9.2 million is never stored: such a request isn't recorded, and the backfill leaves it without a result.

The built-in list prices are each vendor's public prices on 2026-11-23 for the 31 GitHub models that have them: standard processing on the vendor's global endpoint, not batch, flex, priority, fast mode, or regional prices. Each model's `sourceUrl` is its price page. Where a list price differs from GitHub's rate, as Grok 4.5's cached input and GPT-5.6 Luna's long-context threshold do, the list price is used.

- MAI Code 2, MAI Code 2 Flash, and MAI Code 1.1 Flash have no public list price, so they're unpriced here. So are local requests, whose baseline is MAI Code 2, unless you set `apiEquivalent.localBaselineModel`.
- Cache writes are priced for the vendor's default cache lifetime: 5 minutes at Anthropic and Moonshot, and 30 minutes at OpenAI. Where the vendor lists no cache-write price, as for Google's and xAI's models and OpenAI's GPT-5.3-Codex and GPT-5.4 nano, cache writes are priced as input.
- Prices start and end at 00:00 UTC, and a promotion ends the day after its last day. Google publishes the prices that follow its promotions; OpenAI publishes none after GPT-5.6 Sol's, which ends on 2026-12-01. A retired model has no list price from its retirement date: Claude Sonnet 4 from 2026-06-15, and GPT-5.3-Codex and GPT-5.4 nano from 2027-04-01. Current prices also apply to earlier dates, except before a documented change: GPT-5.6 Luna's prices start on 2026-07-30, and GPT-5.6 Sol's on 2026-08-21.

### Imported requests

`gh ledger import` prices session-log requests as the daemon prices live ones, with the rates in use when you run it, as of each request's start time. A request outside every price window of its model is `no_rate_for_date`, never priced at today's price. A session log has no endpoint, so the channel depends on who billed the request:

- A Copilot request or compaction that GitHub billed gets GitHub AI credits, or is local when its model is in `localOffload.models`.
- A Copilot BYOK request is classified by its `providerType`, and a Codex request by its `model_provider`, with Codex's `lmstudio` read as `lm_studio`. It's local when the provider is in `localOffload.providerNames`, or when its model is in `localOffload.models` and the provider isn't in `foundryByok.providerNames`. It's Foundry BYOK when the provider is in `foundryByok.providerNames`, and `other_provider` otherwise.
- Copilot logs a local server that speaks OpenAI's API as the provider `openai`, so its requests are local only when their model is in `localOffload.models`, as Qwen3.8-27B's `qwen3.8-27b-cuda-gpu:1` is.
- A compaction that Copilot billed is recorded even when it failed. A BYOK compaction is priced by the provider of the latest BYOK request with the same model.

## Update the rates: `gh ledger rates update`

`gh ledger rates update` downloads `rates.v2.json` from the latest gh-ledger release, or that release's `rates.json` if it has none, through your system proxy. It saves the file as `rates.downloaded.json` only when the file passes the built-in rates' checks, its rates version (a date) is newer than the rates in use, it has API list prices (`schemaVersion` 2), and your overlay merges with it. Otherwise nothing changes, and the command says why.

The base rates are `rates.downloaded.json` when it's valid, newer than the built-in rates, and `schemaVersion` 2; otherwise they're the built-in rates, so an older download is ignored after you upgrade gh-ledger. An unreadable download, or a newer one with `schemaVersion` 1, is ignored with a warning, for example in `gh ledger status` and the daemon log. `gh ledger status` shows `(downloaded)` after the rates version while the download is in use. Your overlay applies on top of the base rates. Restart the daemon after an update; requests already recorded keep their prices.

## The rates overlay: `~/.copilot-ledger/rates.json`

To change pricing, create `rates.json` in the data directory. gh-ledger merges it over the base rates, so you write only what you change. `gh ledger rates init` creates a starter file, and `gh ledger rates validate` checks it. The daemon reads the file when it starts, so restart the daemon after a change.

| In the overlay | Effect |
|---|---|
| `schemaVersion` | Required: `1`, or `2` when the file has an `apiEquivalent` section. Every other section is optional. |
| A model with a new `id` | Added to its section |
| A model with a built-in model's `id` | Replaces that model, aliases included |
| A model `id` or alias that another built-in model of the same section uses | Invalid |
| Host and provider lists | Added to the built-in lists |
| `localOffload.baselineModel`, `apiEquivalent.localBaselineModel` | Replace the built-in values |
| `ratesVersion` | An optional label. Each request records the rates that priced it, such as `2026-11-23+contoso-2026-11`, or `2026-11-23+user` without a label. |
| `notes` | Your own notes, because JSON doesn't allow comments |

gh-ledger never ignores an invalid overlay. The daemon doesn't start; `import`, `audit`, `rates backfill-api-equivalent`, and the MCP tool `get_audit_findings` stop; `status` shows the problem; and `export` warns and leaves `premium_model_trivial_turn` and repriced estimates out of its audit findings. The error starts with the file's path and describes the first problem:

- Most errors give a path into your file, such as `foundryByok.models[0].windows[0].tiers[0].outputPer1M`. Indexes count from 0, over the entries in your file.
- If the file isn't valid JSON, the error gives the line and column where reading stopped, counting from 1.
- Errors that compare your file with the built-in rates say `in the merged rates`: a model `id` or alias that a built-in model of the same section already uses, a `localOffload.baselineModel` that isn't a GitHub model, or an `apiEquivalent.localBaselineModel` that isn't an API-equivalent model.
- Property names are case-sensitive, an unknown property is an error, and each property can appear only once in its object.

This file leaves out a tier's `outputPer1M`:

<!-- rates-error-example -->
```json
{
  "schemaVersion": 1,
  "foundryByok": {
    "models": [
      { "id": "gpt-6.1-sol", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1.75 } ] } ] }
    ]
  }
}
```

gh-ledger reports:

```text
/home/me/.copilot-ledger/rates.json: foundryByok.models[0].windows[0].tiers[0].outputPer1M: is required
```
<!-- /rates-error-example -->

This file names a baseline model that doesn't exist:

<!-- rates-error-example -->
```json
{
  "schemaVersion": 1,
  "localOffload": { "baselineModel": "gpt-missing" }
}
```

gh-ledger reports:

```text
/home/me/.copilot-ledger/rates.json: localOffload.baselineModel: does not resolve to a githubAiCredits model in the merged rates: gpt-missing
```
<!-- /rates-error-example -->

This file has a comma after its last property, which JSON doesn't allow:

<!-- rates-error-example -->
```json
{
  "schemaVersion": 1,
  "ratesVersion": "contoso-2026-11",
}
```

gh-ledger reports:

```text
/home/me/.copilot-ledger/rates.json: line 4, column 1: invalid JSON
```
<!-- /rates-error-example -->

### Price your Foundry BYOK models

With bring-your-own-key (BYOK), Microsoft Foundry bills you directly, at prices that depend on region, deployment type, and agreement, so gh-ledger has no built-in Foundry prices. Until you add yours, BYOK requests are counted at $0.00 with status `byok_unpriced`. Take your prices from your Azure pricing or invoice; these rates are only an example:

```json
{
  "schemaVersion": 1,
  "ratesVersion": "contoso-2026-11",
  "notes": ["Azure prices from our November 2026 invoice."],
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

- Foundry requests often report your deployment name, `contoso-gpt61-prod` here, so list your deployment names as `aliases`.
- Prices are USD per 1,000,000 tokens, from 0 to 1,000,000, with at most 6 decimal places. Without `cachedInputPer1M` or `cacheWritePer1M`, those tokens are priced at `inputPer1M`.

### Use a custom Foundry endpoint host

To treat another host as Foundry BYOK, such as a gateway in front of Foundry, add it as a host suffix, without a scheme, port, path, or leading `.`. A suffix matches that host and its subdomains.

```json
{
  "schemaVersion": 1,
  "foundryByok": { "hostSuffixes": ["ai-gateway.contoso.com"] }
}
```

### Local models and savings

Requests to local runtimes, such as Foundry Local, Ollama, and LM Studio, bill $0.00. gh-ledger reports the same tokens at the GitHub rates of `localOffload.baselineModel`, MAI Code 2 by default, as local savings, which are never billed. The built-in local model, Qwen3.8-27B, is recorded as `qwen3.8-27b` under runtime names such as `qwen3.8:27b`, `qwen3.8:27b-q4_K_M`, `qwen3.8-27b-cuda-gpu:1`, and `Qwen 3.8 27B`.

To add names for a local model, add an entry. An entry with a built-in model's `id` replaces it, so include the built-in aliases you still want:

```json
{
  "schemaVersion": 1,
  "localOffload": {
    "models": [
      { "id": "qwen3.8-27b", "aliases": ["qwen-3.8-27b", "qwen3.8:27b", "qwen3.8:27b-q4_k_m", "qwen3.8-27b-cuda-gpu:1", "my-qwen-build"] }
    ]
  }
}
```

To measure savings against a different GitHub model:

```json
{
  "schemaVersion": 1,
  "localOffload": { "baselineModel": "claude-sonnet-5.5" }
}
```

To treat a model server on another machine as local, add its exact host name:

```json
{
  "schemaVersion": 1,
  "localOffload": { "additionalHosts": ["gpu-box.lan"] }
}
```

### Add or correct a GitHub price

If GitHub adds a model or changes a price before gh-ledger's rates do, add the model under `githubAiCredits.models` in the same shape as the Foundry example, or replace a built-in model by giving the same `id`. A replacement drops the built-in model's aliases, so list the ones you still need.

- `maxInputTokens` is an inclusive limit on a request's input tokens, cache reads and writes included. Set it on every tier except the last, in ascending order.
- `effectiveFrom` (inclusive) and `effectiveUntil` (exclusive) bound a price window by the request's start time. Use UTC timestamps such as `2027-05-17T00:00:00Z`, and list windows in order, without overlaps.

### API list prices

The `apiEquivalent` section holds API list prices: what a model costs from its vendor's API, in USD per 1,000,000 tokens. They never change billed amounts, local savings, or budgets, and the section needs `"schemaVersion": 2`. For example, to compare with prices your company negotiated:

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

- These models have the same fields and rules as other priced models, plus the optional notes `vendor` and `sourceUrl`. Their ids and aliases must be unique within `apiEquivalent`, but a GitHub or Foundry BYOK model can have the same id.
- `localBaselineModel`, optional, names the API-equivalent model whose list prices value local requests. Without it, local requests use the list prices of `localOffload.baselineModel`, if it has any.

Overlay list prices apply to requests recorded from then on. To see what requests already recorded would cost at other prices, use `--pricing`.

### Compare with other prices: `--pricing`

`gh ledger status`, `branch`, `agents`, `top-prompts`, and `export-pr` take `--pricing <path>`: a rates file whose API list prices replace the recorded API-equivalent amounts in that report. Nothing else changes, and nothing is written to the ledger.

- The file is a complete rates document with `"schemaVersion": 2`, not an overlay; a release's `rates.v2.json` is a good start. Neither your overlay nor downloaded rates apply to it.
- Each request keeps the channel and model id it was recorded with. The file prices that model id, by id or alias, as of the request's start time, or the request model when no section of the file has the id. A local request is priced as the file's local baseline. Requests recorded without an API-equivalent result are priced too.
- Reports name the file in a `Pricing:` line, and their JSON adds `pricingSource`. A receipt names the file without its folder.
- `gh ledger rates validate --pricing <path>` checks a file with the same rules and messages.

## Field reference

Overlay sections:

| Field | Required | Notes |
|---|---|---|
| `schemaVersion` | Yes | `1`, or `2` when the file has `apiEquivalent` |
| `ratesVersion` | No | Label recorded with each priced request |
| `notes` | No | Strings for your own notes |
| `source` | No | Ignored in an overlay |
| `githubAiCredits.hostSuffixes`, `foundryByok.hostSuffixes` | No | Host suffixes, added to the built-in lists |
| `foundryByok.providerNames`, `localOffload.providerNames` | No | Matched, ignoring case, against `gen_ai.provider.name` and the provider of an imported BYOK or Codex request (see [Imported requests](#imported-requests)) |
| `githubAiCredits.models`, `foundryByok.models` | No | Priced models (see the next table) |
| `localOffload.models` | No | Local model entries: `{ "id": "...", "aliases": ["..."] }` |
| `localOffload.baselineModel` | No | A GitHub model id or alias whose rates estimate local savings; also the default economy model of `gh ledger audit` |
| `localOffload.additionalHosts` | No | Exact host names treated as local, for pricing only; never a destination for `gh ledger audit --local-llm` |
| `apiEquivalent.models` | No | API list prices, as priced models (see the next table) |
| `apiEquivalent.localBaselineModel` | No | An API-equivalent model id or alias that values local requests |
| `apiEquivalent.description` | No | Text for your own notes |

Priced model entries:

| Field | Required | Default |
|---|---|---|
| `id` | Yes | — |
| `vendor`, `sourceUrl` | No; only in `apiEquivalent.models` | None |
| `aliases` | No | `[]` |
| `windows` | Yes, at least one, in order and without overlaps | — |
| `windows[].effectiveFrom`, `windows[].effectiveUntil` | No | Unbounded |
| `windows[].tiers` | Yes, at least one | — |
| `tiers[].name` | Yes, unique within its window | — |
| `tiers[].maxInputTokens` | On every tier except the last, in ascending order | Unbounded |
| `tiers[].inputPer1M`, `tiers[].outputPer1M` | Yes | — |
| `tiers[].cachedInputPer1M`, `tiers[].cacheWritePer1M` | No | Priced at `inputPer1M` |
