# Configuring CopilotLedger

CopilotLedger works with no configuration. This page explains how it prices usage and how to change that with one optional file.

## Where CopilotLedger keeps its files

| Path | Purpose |
|---|---|
| `~/.copilot-ledger/` | Data directory (`%USERPROFILE%\.copilot-ledger\` on Windows) |
| `~/.copilot-ledger/ledger.db` | The local SQLite ledger (WAL mode) |
| `~/.copilot-ledger/rates.json` | Optional rates overlay, which you create |
| `~/.copilot-ledger/rates.downloaded.json` | Newer built-in rates saved by `gh ledger rates update` |
| `~/.copilot-ledger/daemon.json` | Written while the daemon runs: process ID, endpoint, start time, and database path |
| `~/.copilot-ledger/daemon.lock` | Held while the daemon runs, so only one daemon runs at a time |
| `~/.copilot-ledger/daemon.stop` | Written by `gh ledger daemon stop` to ask the daemon to stop; the daemon deletes it |
| `~/.copilot-ledger/daemon.log` | The daemon's log when it's started with `gh ledger daemon start` or at sign-in |
| `~/.copilot-ledger/alert-toast.xml` | Windows only: the latest desktop notification, which the daemon passes to PowerShell to show it |
| `~/.copilot-ledger/install.json` | What `gh ledger install` changed, so `gh ledger uninstall` can undo it |

Set the `COPILOT_LEDGER_HOME` environment variable to keep these files in another directory.

CopilotLedger never stores prompt or response content, and it strips credentials from Git remote URLs before saving them.

## How usage is priced

Each LLM request (an OpenTelemetry `chat` span) is attributed to one billing channel:

| Channel | How it's detected | What's recorded |
|---|---|---|
| GitHub AI Credits | No endpoint on the span, or an endpoint under `githubcopilot.com` | AI credits and USD from GitHub's published prices (1 AI credit = $0.01) |
| Microsoft Foundry BYOK | An endpoint under `services.ai.azure.com`, `openai.azure.com`, or `cognitiveservices.azure.com`, or provider `azure.ai.inference` | USD from **your** prices in `rates.json`, and 0 AI credits. Unpriced until you add prices. |
| Local | An endpoint of `localhost`, `127.x.x.x`, `::1`, or a host you list; a local provider name (`ollama`, `lm_studio`, `foundry_local`, or `llama_cpp`, which Copilot CLI reports for models you bring or run yourself); or a known local model, such as Qwen3.8-27B, with no endpoint | $0.00 billed, plus estimated savings: the same tokens priced at MAI Code 2's GitHub rates |
| Unpriced | Anything else | Tokens only, with a status that explains why |

The built-in prices are GitHub's rates for every text model on [Models and pricing for GitHub Copilot](https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing) as of 2026-11-20: 34 models, with their long-context tiers and promotional prices. A promotional price ends at 00:00 UTC on the date GitHub gives. Two models need your attention:

- Claude Opus 4.8 (fast mode) isn't built in, because the model name Copilot reports for it isn't known yet. If you use it, add it under `githubAiCredits.models` with the model name Copilot reports.
- GitHub hasn't published the prices that follow the promotions for Gemini 4 Argon (ending 2026-12-01) and Gemini 3.8 Flash (ending after 2026-12-31). Later requests for them are recorded with status `no_rate_for_date` until you add a price window or a CopilotLedger update adds the price.

### Pricing statuses

| Status | Meaning | Fix |
|---|---|---|
| `priced` | Priced from a rate card | — |
| `local_offload` | Local model: $0.00 billed, savings estimated | — |
| `byok_unpriced` | Foundry BYOK endpoint, but no price for the model | Add the model under `foundryByok.models` |
| `unknown_model` | No model on the request, or the model isn't in the price list | Add the model, or an alias for it, under `githubAiCredits.models` or `foundryByok.models` |
| `no_rate_for_date` | The model has no price for the request's date | Add a price window that covers the date |
| `unrecognized_endpoint` | The endpoint isn't GitHub, Foundry, or local | Add its host to the matching `hostSuffixes` or `additionalHosts` list |
| `invalid_usage` | Token counts are inconsistent, for example more cached tokens than input tokens | Report it; it usually means a telemetry exporter bug |

### Cache tokens

A request's input tokens include the tokens the model read from its prompt cache and the tokens it wrote to it. Copilot reports the cache-read and cache-write counts only when they aren't zero. Cache reads are priced at `cachedInputPer1M`, cache writes at `cacheWritePer1M`, and the rest of the input at `inputPer1M`. When a request reports neither count, as many Foundry BYOK and local endpoints do, all of its input is priced as uncached input.

Cache-efficiency reports, such as `gh ledger status --cache`, don't treat such a request as a cache miss: its cache use is unknown, so hit ratios and observed cache totals leave it out, and a group of such requests shows `n/a` rather than `0.0%`. A cache miss that writes nothing to the cache also reports neither count, so it's left out too, and a hit ratio can be higher than the real one. See [Prompt-cache efficiency](cli.md#prompt-cache-efficiency).

## Why Foundry BYOK is unpriced by default

With bring-your-own-key (BYOK), Microsoft Foundry bills you directly, not GitHub. Azure prices depend on region, deployment type, and your agreement, so CopilotLedger can't know yours.

Until you add prices, BYOK requests are still counted and attributed to repository, branch, model, and agent, with status `byok_unpriced` and $0.00 cost.

## Newer rates between releases: `gh ledger rates update`

Each gh-ledger release attaches the `rates.json` it was built with. `gh ledger rates update` downloads the latest release's copy from `https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json`, through your system proxy, and checks it with the same rules as the built-in rates.

Rates versions are dates. When the download is newer than the rates in use, `rates update` saves it as `rates.downloaded.json`; when it's the same or older, nothing changes. If your overlay doesn't merge with the newer rates, nothing is saved, and the error names the problem in your overlay.

CopilotLedger's base rates are `rates.downloaded.json` when that file is valid and newer than the built-in rates, and the built-in rates otherwise. So after you upgrade gh-ledger to newer built-in rates, an older download is ignored. A download that can't be read is ignored with a warning in `gh ledger status`, `gh ledger rates validate`, and the daemon log. Your overlay applies on top of either. `gh ledger status` shows `(downloaded)` after the rates version when the download is in use.

The daemon reads rates when it starts, so restart it after an update. Spans already recorded keep the prices they were recorded with.

## The rates overlay: `~/.copilot-ledger/rates.json`

To change pricing, create `~/.copilot-ledger/rates.json`. CopilotLedger merges it over the built-in rates when it loads them, so you only write what you want to change.

Run `gh ledger rates init` to create a starter file, and `gh ledger rates validate` to check it against the same rules the daemon uses. The daemon reads the file when it starts, so restart it after a change: `gh ledger daemon stop`, then `gh ledger daemon start`.

- Every section is optional except `"schemaVersion": 1`.
- A model with a new `id` is added. A model with the same `id` as a built-in one replaces it.
- Host and provider lists are added to the built-in lists.
- `localOffload.baselineModel` replaces the built-in value.
- `ratesVersion` is an optional label. Each request in the ledger records which rates priced it, for example `2026-11-20+contoso-2026-11`, or `2026-11-20+user` if you leave the label out.
- JSON doesn't allow comments, so put your own notes in the optional `notes` array.

If the file is invalid, CopilotLedger refuses to load it and reports the first problem it finds. It never silently ignores a broken file. Each error starts with the file's path, then says where the problem is and what's wrong:

- Most errors give a path into your file, such as `foundryByok.models[0].windows[0].tiers[0].outputPer1M`. Indexes count from 0, and they count only the entries in your file.
- If the file isn't valid JSON, the error gives the line and column where reading stopped, counting from 1.
- Two checks compare your file with the built-in rates: a model `id` or alias that a built-in model already uses, and a `localOffload.baselineModel` that isn't a GitHub model. These errors say `in the merged rates`.
- Property names are case-sensitive, and each property can appear only once in its object.

For example, this file leaves out a tier's `outputPer1M`:

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

CopilotLedger reports:

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

CopilotLedger reports:

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

CopilotLedger reports:

```text
/home/me/.copilot-ledger/rates.json: line 4, column 1: invalid JSON
```
<!-- /rates-error-example -->

### Price your Foundry BYOK models

Prices are USD per 1,000,000 tokens, from 0 to 1,000,000, with at most 6 decimal places. Take them from your Azure pricing or invoice. The rates below are an example, not real Azure prices.

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

- `id` is the model name, and `aliases` lists other names that mean the same model. Foundry requests often report your **deployment name** (`contoso-gpt61-prod` here), so list your deployment names as aliases.
- `cachedInputPer1M` and `cacheWritePer1M` are optional. If you leave one out, those tokens are billed at `inputPer1M`.
- Model names match case-insensitively, with spaces treated as `-`, so `Claude Opus 5.5` matches `claude-opus-5.5`.

### Use a custom Foundry endpoint host

Requests to `*.services.ai.azure.com`, `*.openai.azure.com`, and `*.cognitiveservices.azure.com` are recognized automatically. To add another host, such as a gateway in front of Foundry:

```json
{
  "schemaVersion": 1,
  "foundryByok": { "hostSuffixes": ["ai-gateway.contoso.com"] }
}
```

A suffix matches that host and any subdomain of it.

### Local models and savings

Local runtimes such as Foundry Local, Ollama, and LM Studio run models on your own GPU, so local requests cost $0.00. CopilotLedger recognizes them by their `localhost` endpoint.

**Qwen3.8-27B** is the reference local model. Its runtime names all roll up as `qwen3.8-27b`: `qwen3.8:27b`, `qwen3.8:27b-q4_K_M`, `qwen3.8-27b-cuda-gpu:1`, and `Qwen 3.8 27B`.

To show what running locally is worth, CopilotLedger prices the same tokens at **MAI Code 2**'s GitHub rates, a deliberately conservative comparison. That amount is reported as local savings and is never billed.

To add names for a local model, use an entry like this. An entry with the same `id` replaces the built-in one, so include the built-in aliases you still want.

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

To treat a model server on another machine on your network as local:

```json
{
  "schemaVersion": 1,
  "localOffload": { "additionalHosts": ["gpu-box.lan"] }
}
```

### Add or correct a GitHub price

If GitHub adds a model or changes a price before CopilotLedger ships updated rates, add or replace the model under `githubAiCredits.models`, using the same shape as the Foundry example.

For tiered or time-limited prices:

- `maxInputTokens` is an inclusive input-token limit. Set it on every tier except the last.
- `effectiveFrom` (inclusive) and `effectiveUntil` (exclusive) bound a price window. Use UTC timestamps such as `2027-05-17T00:00:00Z`.

## Field reference

Overlay sections:

| Field | Required | Notes |
|---|---|---|
| `schemaVersion` | Yes | Must be `1` |
| `ratesVersion` | No | Label recorded with each priced request |
| `notes` | No | Strings for your own notes |
| `githubAiCredits.hostSuffixes`, `foundryByok.hostSuffixes` | No | Added to the built-in lists |
| `foundryByok.providerNames`, `localOffload.providerNames` | No | Matched against `gen_ai.provider.name` |
| `githubAiCredits.models`, `foundryByok.models` | No | Priced models (see the next table) |
| `localOffload.models` | No | Local model entries: `{ "id": "...", "aliases": ["..."] }` |
| `localOffload.baselineModel` | No | A GitHub model id or alias used to estimate savings |
| `localOffload.additionalHosts` | No | Hosts treated as local |

Priced model entries:

| Field | Required | Default |
|---|---|---|
| `id` | Yes | — |
| `aliases` | No | `[]` |
| `windows` | Yes, at least one | — |
| `windows[].effectiveFrom`, `windows[].effectiveUntil` | No | Unbounded |
| `windows[].tiers` | Yes, at least one | — |
| `tiers[].name` | Yes | — |
| `tiers[].maxInputTokens` | Only for tiered prices: on every tier except the last | Unbounded |
| `tiers[].inputPer1M`, `tiers[].outputPer1M` | Yes | — |
| `tiers[].cachedInputPer1M`, `tiers[].cacheWritePer1M` | No | Billed at `inputPer1M` |
