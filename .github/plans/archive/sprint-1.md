# Sprint 1: Core Accounting & SQLite Engine

| | |
|---|---|
| Architect | `ledger-architect` (Claude Opus 5.5) |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET 10 (`net10.0`), SDK 10.0.x, zero Docker |
| Plan written | 2026-11-20 |
| Status | Ready for Task 1 |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification section, runs the verification commands, changes the line to `- [x]`, and stops. Tasks are sequential, and each builds on the ones before it.

Every task ships with its tests. Task 1 creates `CopilotLedger.Tests`, and each task adds the cases listed for it under "Test specifications". A task is done only when `dotnet test` runs the whole suite, including its new cases, with 0 failed and 0 skipped.

## Tasks

- [x] Task 1: Solution & Core Contracts (CopilotLedger.Core). Scaffold `global.json`, `Directory.Build.props`, `CopilotLedger.slnx`, `src/CopilotLedger.Core` (`net10.0`), and `tests/CopilotLedger.Tests` (xUnit). Add the immutable records and interfaces (`NormalizedGenAiSpan`, `ModelRateCard`, `CreditCalculationResult`, `GitWorkspaceContext`, `ICreditCalculator`, `IGitContextResolver`) and their supporting types, with tests for their logic. No pricing or Git logic yet.
- [x] Task 2: AI Credit Rate Engine & Zero-Config Git Resolver (CopilotLedger.Core). Add `JsonRateCardCalculator` with an embedded `rates.json`: November 2026 GitHub pricing for `claude-opus-5.5`, `gpt-6.1-sol`, `gpt-6-astra`, `claude-sonnet-5.5`, and `mai-code-2`; Foundry BYOK endpoint detection; and $0.00 local offload for Qwen3.8-27B and other local models, with savings measured against MAI Code 2. Add the user rates overlay (`~/.copilot-ledger/rates.json`) that makes Foundry BYOK prices easy to configure. Add `FileSystemGitContextResolver`, which reads `.git/HEAD`, refs, and `remote.origin.url` straight from disk. Tests cover decimal rate-card math, cache discounts, the overlay, and Git context resolution.
- [x] Task 3: SQLite WAL Ledger & xUnit Suite (CopilotLedger.Storage & CopilotLedger.Tests). Add `src/CopilotLedger.Storage`: Microsoft.Data.Sqlite in WAL mode at `~/.copilot-ledger/ledger.db`, with schema migrations and rollups by repository, branch, model, and `gen_ai.agent.name`. Complete the xUnit suite with storage and SQLite rollup tests.

## Verified facts (retrieved 2026-11-20)

### GitHub AI Credit pricing

Source: [Models and pricing for GitHub Copilot](https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing). Prices are USD per 1M tokens; 1 AI credit = $0.01.

| Model ID | Tier | Input | Cached input | Cache write | Output |
|---|---|---|---|---|---|
| `claude-opus-5.5` | single | 4.00 | 0.20 | 5.00 | 20.00 |
| `claude-sonnet-5.5` | single | 2.00 | 0.10 | 2.50 | 10.00 |
| `gpt-6.1-sol` | default, input ≤ 272K | 2.00 | 0.10 | 2.50 | 10.00 |
| `gpt-6.1-sol` | long context, input > 272K | 4.00 | 0.20 | 5.00 | 15.00 |
| `gpt-6-astra` | default, input ≤ 272K | 10.00 | 1.00 | 12.50 | 50.00 |
| `gpt-6-astra` | long context, input > 272K | 20.00 | 2.00 | 25.00 | 75.00 |
| `mai-code-2` | single, before 2027-05-17 (promotional) | 0.50 | 0.025 | none listed | 2.50 |
| `mai-code-2` | single, from 2027-05-17 | 1.00 | 0.05 | none listed | 5.00 |

- The original project plan listed Claude Sonnet 5.5 at $3.00 / $0.30 / $15.00. That doesn't match the published November 2026 table, so this plan uses the GitHub Docs values above.
- Anthropic models and GPT-5.6+/GPT-6.x models bill cache writes in addition to cached input. Earlier OpenAI models and the Microsoft models list no cache-write price.
- Code completions and next edit suggestions aren't billed in AI credits.

### OpenTelemetry token semantics

- OTel GenAI semantic conventions (`open-telemetry/semantic-conventions`, `docs/registry/attributes/gen-ai.md`): `gen_ai.usage.input_tokens` "SHOULD include all types of input tokens, including cached tokens", and `gen_ai.usage.cache_read.input_tokens` and `gen_ai.usage.cache_creation.input_tokens` each "SHOULD be included in `gen_ai.usage.input_tokens`".
- The GenAI conventions have moved to `open-telemetry/semantic-conventions-genai`, which uses cache-write naming: "The cache_read, cache_write, and reasoning counters report subsets of the input_tokens and output_tokens counters."
- GitHub Docs, [OpenTelemetry reference](https://docs.github.com/en/copilot/reference/enterprise-administrators/opentelemetry-reference) (Copilot runtime):
  - A `chat` span is "one span per LLM request", with `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, `gen_ai.usage.cache_read.input_tokens`, and `gen_ai.usage.cache_write.input_tokens`.
  - The root `invoke_agent` span wraps all LLM calls and tool executions for one user message and reports token totals across all turns.
  - `gen_ai.conversation.id` is the session identifier, and `gen_ai.agent.name` is set when available.
  - The input-token metric is described as "Input tokens used, including cached tokens".
- VS Code, [Monitor agent usage with OpenTelemetry](https://code.visualstudio.com/docs/agents/guides/monitoring-agents), documents `gen_ai.usage.input_tokens` and `gen_ai.usage.cache_read.input_tokens` on chat spans, following the same conventions.

**Settled:** `input_tokens` includes cache-read and cache-write tokens, so uncached input = `InputTokens - CacheReadInputTokens - CacheWriteInputTokens`. Copilot emits the cache-creation count as `gen_ai.usage.cache_write.input_tokens`; `gen_ai.usage.cache_creation.input_tokens` is the legacy name.

### Endpoints and SDK

- Microsoft Foundry endpoints (Microsoft Learn): `https://<resource>.services.ai.azure.com` (Foundry Models, including its `/openai` and `/anthropic` paths), `https://<resource>.openai.azure.com`, and `https://<resource>.cognitiveservices.azure.com`.
- The OTel Azure AI Inference convention sets `gen_ai.provider.name` to `azure.ai.inference`.
- In .NET 10, `dotnet new sln` creates an SLNX solution by default (Microsoft Learn breaking-change notice). The local SDK is 10.0.403.

### Local models

- Qwen3.8-27B is an Apache-2.0, 27B dense model with a 262K native context. Its Q4_K_M GGUF (about 17 GB) fits on a single 24 GB consumer GPU.
- Ollama tags include `qwen3.8:27b` and `qwen3.8:27b-q4_K_M`. That's from third-party docs (docs.tokios.com) and isn't verified against Ollama's library.
- The Foundry Local id `qwen3.8-27b-cuda-gpu:1` comes from the project's original plan.

### User decisions (2026-11-20)

- Every task ships with real tests, so `CopilotLedger.Tests` is created in Task 1.
- Foundry BYOK stays unpriced by default, but that has to be documented and easy to configure. The rates overlay (Design decision 11) and `docs/configuration.md` cover this.
- Local models run on the user's own GPU. Qwen3.8-27B is the reference local model, and savings are measured against MAI Code 2's GitHub rates.
- Confirmed:
  - the MAI Code 2 promotion ends at `2027-05-17T00:00:00Z`;
  - 272K means 272,000 input tokens and selects the tier for the whole request;
  - models with no listed cache-write price bill cache writes at their input rate;
  - spans to unrecognized hosts stay unpriced.
- Use GitHub's published prices, including Claude Sonnet 5.5 at $2.00 / $0.10 / $2.50 / $10.00.

## Design decisions

1. **Exact units.** Rates are `decimal` USD per 1M tokens with at most 6 decimal places, which is enforced when rates load. Each component costs `AiCreditMath.CostUsd(tokens, rate)` = `tokens * rate / 1_000_000m`, exact to 10^-12 USD. Nothing in Core or Storage rounds. `AiCredits = UsdCost / 0.01m`, for the GitHub channel only.
2. **Billing channel.** The calculator resolves the channel from the span's host (`ServerAddress`) and `ProviderName`. It takes the first rule that matches:
   - **Local**: any one of these:
     - the host is `localhost`, a loopback IP literal (`127.0.0.0/8` or `::1`, with or without brackets), or an entry in `localOffload.additionalHosts`;
     - the provider name is in `localOffload.providerNames`;
     - the host is null or blank and the model matches `localOffload.models`, for example Qwen3.8-27B.

     Billed $0, with savings priced at the GitHub rates of `localOffload.baselineModel` (default `mai-code-2`).
   - **FoundryByok**: the host equals an entry in `foundryByok.hostSuffixes`, or ends with `.` plus one; or the provider name is in `foundryByok.providerNames`. Billed in USD from `foundryByok.models`, with 0 AI credits.
   - **GitHubAiCredits**: the host is null or blank, or equals an entry in `githubAiCredits.hostSuffixes`, or ends with `.` plus one.
   - Anything else is **Unpriced**, with status `UnrecognizedEndpoint`. GitHub pricing is never applied to an unknown endpoint.
3. **Tiers.** The tier is chosen by the inclusive `InputTokens`. The first tier whose `maxInputTokens` is null or ≥ `InputTokens` prices the whole request. 272K means 272,000.
4. **Missing cache prices.** A null `cachedInputPer1M` or `cacheWritePer1M` bills those tokens at `inputPer1M`, as ordinary input with no discount or surcharge.
5. **Effective windows** are `[effectiveFrom, effectiveUntil)`, compared as instants against the span's `StartTime`; null means unbounded. The `mai-code-2` boundary is `2027-05-17T00:00:00Z`. The docs give only a date; the user confirmed UTC midnight.
6. **Model identity.** The effective model is `ResponseModel`, falling back to `RequestModel`. It's normalized by trimming, lower-casing (invariant), and collapsing each whitespace run to `-`, so `Claude Opus 5.5` becomes `claude-opus-5.5`. Matching is exact against normalized ids and aliases. There's no prefix or fuzzy matching.
7. **Billable operations** are `chat`, `text_completion`, and `generate_content`. Anything else, such as `invoke_agent` or `execute_tool`, gets `NotBillableOperation`, and the ledger refuses to store it.
8. **Foundry BYOK prices** depend on region, deployment type, and agreement, and aren't verified here, so the embedded `foundryByok.models` is empty. BYOK spans are still attributed (channel and tokens) with status `ByokUnpriced` until the user adds prices to the rates overlay (decision 11). `docs/configuration.md` documents this.
9. **Local models and savings.**
   - Local runtimes such as Foundry Local, Ollama, and LM Studio run models on the user's own GPU, so local spans bill $0.00.
   - Qwen3.8-27B is the reference local model. `localOffload.models` maps its runtime ids to the canonical `qwen3.8-27b`, so rollups group them.
   - Savings price the same tokens at `mai-code-2`'s GitHub rates, a deliberately conservative estimate that follows MAI Code 2's effective windows. Change it with `localOffload.baselineModel`.
10. **Storage** keeps one row per inference span, made idempotent by `UNIQUE (trace_id, span_id)`. Money is `INTEGER` pico-USD in a `STRICT` table, the database runs in WAL mode, and the schema version lives in `PRAGMA user_version`.
11. **Rates overlay.** An optional user file, `~/.copilot-ledger/rates.json` (`LedgerPaths.DefaultRatesOverlayPath`), is merged over the embedded rates when CopilotLedger loads them.
    - It uses the same schema with every section optional, so users can make one change without copying the whole file.
    - It can add Foundry BYOK prices, deployment-name aliases, local models and hosts, or GitHub price corrections, or set a different savings baseline.
    - The merge rules are in Task 2.
    - A broken overlay fails loudly, naming the file and the JSON path. It's never silently ignored.

## Task 1 specification: Solution & Core Contracts

### Files

```text
global.json
Directory.Build.props
CopilotLedger.slnx
src/CopilotLedger.Core/CopilotLedger.Core.csproj
src/CopilotLedger.Core/Telemetry/NormalizedGenAiSpan.cs
src/CopilotLedger.Core/Telemetry/GenAiOperationNames.cs
src/CopilotLedger.Core/Pricing/AiCreditMath.cs
src/CopilotLedger.Core/Pricing/BillingChannel.cs
src/CopilotLedger.Core/Pricing/PricingStatus.cs
src/CopilotLedger.Core/Pricing/ModelRateCard.cs
src/CopilotLedger.Core/Pricing/CreditCalculationResult.cs
src/CopilotLedger.Core/Pricing/ICreditCalculator.cs
src/CopilotLedger.Core/Git/GitWorkspaceContext.cs
src/CopilotLedger.Core/Git/IGitContextResolver.cs
tests/CopilotLedger.Tests/CopilotLedger.Tests.csproj
tests/CopilotLedger.Tests/Telemetry/GenAiOperationNamesTests.cs
tests/CopilotLedger.Tests/Pricing/AiCreditMathTests.cs
tests/CopilotLedger.Tests/Git/GitWorkspaceContextTests.cs
```

### Steps

**Step 1.** Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

**Step 2.** Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

**Step 3.** From the repository root, run:

```shell
dotnet new sln -n CopilotLedger
dotnet new classlib -n CopilotLedger.Core -o src/CopilotLedger.Core
dotnet sln CopilotLedger.slnx add src/CopilotLedger.Core/CopilotLedger.Core.csproj
dotnet new xunit -n CopilotLedger.Tests -o tests/CopilotLedger.Tests
dotnet add tests/CopilotLedger.Tests/CopilotLedger.Tests.csproj reference src/CopilotLedger.Core/CopilotLedger.Core.csproj
dotnet sln CopilotLedger.slnx add tests/CopilotLedger.Tests/CopilotLedger.Tests.csproj
```

`dotnet new sln` must create `CopilotLedger.slnx`. If it creates a `.sln` instead, delete it and re-run with `--format slnx`.

**Step 4.** Delete the templates' `Class1.cs` and `UnitTest1.cs`.

In `tests/CopilotLedger.Tests/CopilotLedger.Tests.csproj`, remove `TargetFramework`, `ImplicitUsings`, and `Nullable`, since they're inherited from `Directory.Build.props`, and remove any `PropertyGroup` left empty. Keep everything else the template generated, including package versions. If the template produced xUnit v3, pass `TestContext.Current.CancellationToken` to async APIs so analyzer `xUnit1051` doesn't fail the build.

Replace `src/CopilotLedger.Core/CopilotLedger.Core.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

**Step 5.** Add the contracts below exactly as written: same file paths, namespaces, type names, member names, types, and member order. Keep the trailing comments, which document the OTel mapping and semantics. Members ending in `;` instead of a body are implemented in this task as their comment describes. Apart from those members and `TokenCostBreakdown.TotalUsd`, these types contain no logic.

**Step 6.** Write the cases in "Task 1 tests" under "Test specifications".

### Contracts

```csharp
// ---- src/CopilotLedger.Core/Telemetry/NormalizedGenAiSpan.cs
namespace CopilotLedger.Core.Telemetry;

public sealed record NormalizedGenAiSpan
{
    public required string TraceId { get; init; }            // 32 lowercase hex chars
    public required string SpanId { get; init; }             // 16 lowercase hex chars
    public string? ParentSpanId { get; init; }
    public required string OperationName { get; init; }      // gen_ai.operation.name
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public string? ProviderName { get; init; }               // gen_ai.provider.name
    public string? RequestModel { get; init; }               // gen_ai.request.model
    public string? ResponseModel { get; init; }              // gen_ai.response.model
    public string? AgentName { get; init; }                  // gen_ai.agent.name
    public string? ConversationId { get; init; }             // gen_ai.conversation.id (Copilot session id)
    public string? ServerAddress { get; init; }              // server.address
    public int? ServerPort { get; init; }                    // server.port
    public string? WorkingDirectory { get; init; }           // set by ingestion in Sprint 2
    public long InputTokens { get; init; }                   // gen_ai.usage.input_tokens; INCLUDES cache read and cache write
    public long OutputTokens { get; init; }                  // gen_ai.usage.output_tokens
    public long CacheReadInputTokens { get; init; }          // gen_ai.usage.cache_read.input_tokens
    public long CacheWriteInputTokens { get; init; }         // gen_ai.usage.cache_write.input_tokens (legacy: cache_creation)
}

// ---- src/CopilotLedger.Core/Telemetry/GenAiOperationNames.cs
namespace CopilotLedger.Core.Telemetry;

public static class GenAiOperationNames
{
    public const string Chat = "chat";
    public const string TextCompletion = "text_completion";
    public const string GenerateContent = "generate_content";
    public const string InvokeAgent = "invoke_agent";
    public const string ExecuteTool = "execute_tool";

    // True only for Chat, TextCompletion, and GenerateContent (ordinal, case-insensitive). Null or blank returns false.
    public static bool IsInference(string? operationName);
}

// ---- src/CopilotLedger.Core/Pricing/AiCreditMath.cs
namespace CopilotLedger.Core.Pricing;

public static class AiCreditMath
{
    public const decimal UsdPerAiCredit = 0.01m;
    public const decimal TokensPerRateUnit = 1_000_000m;

    // tokens * ratePerMillion / TokensPerRateUnit. Throws ArgumentOutOfRangeException if either argument is negative.
    public static decimal CostUsd(long tokens, decimal ratePerMillion);

    // usd / UsdPerAiCredit
    public static decimal UsdToAiCredits(decimal usd);
}

// ---- src/CopilotLedger.Core/Pricing/BillingChannel.cs
namespace CopilotLedger.Core.Pricing;

public enum BillingChannel
{
    GitHubAiCredits,
    FoundryByok,
    Local,
    Unpriced,
}

// ---- src/CopilotLedger.Core/Pricing/PricingStatus.cs
namespace CopilotLedger.Core.Pricing;

public enum PricingStatus
{
    Priced,                 // GitHubAiCredits or FoundryByok with a matching rate
    LocalOffload,           // Local: billed 0, savings priced at the baseline model
    ByokUnpriced,           // FoundryByok endpoint, but no BYOK rate for the model
    UnknownModel,           // no model on the span, or the model isn't in the channel's table
    NoRateForDate,          // model known, but no window covers StartTime
    UnrecognizedEndpoint,   // host isn't local, Foundry, or GitHub
    InvalidUsage,           // a negative token count, or cache read + cache write > input
    NotBillableOperation,   // not an inference operation (for example invoke_agent)
}

// ---- src/CopilotLedger.Core/Pricing/ModelRateCard.cs
namespace CopilotLedger.Core.Pricing;

public sealed record RateTier(
    string Name,
    long? MaxInputTokens,              // inclusive bound on InputTokens; null = unbounded (last tier only)
    decimal InputPer1M,
    decimal? CachedInputPer1M,         // null bills cache reads at InputPer1M
    decimal? CacheWritePer1M,          // null bills cache writes at InputPer1M
    decimal OutputPer1M);

public sealed record RateWindow(
    DateTimeOffset? EffectiveFrom,     // inclusive; null = unbounded
    DateTimeOffset? EffectiveUntil,    // exclusive; null = unbounded
    IReadOnlyList<RateTier> Tiers);

public sealed record ModelRateCard(
    string ModelId,                    // canonical normalized id
    BillingChannel Channel,            // GitHubAiCredits or FoundryByok
    IReadOnlyList<string> Aliases,     // normalized
    IReadOnlyList<RateWindow> Windows);

// ---- src/CopilotLedger.Core/Pricing/CreditCalculationResult.cs
namespace CopilotLedger.Core.Pricing;

public sealed record TokenCostBreakdown(
    decimal UncachedInputUsd,
    decimal CacheReadUsd,
    decimal CacheWriteUsd,
    decimal OutputUsd)
{
    public decimal TotalUsd => UncachedInputUsd + CacheReadUsd + CacheWriteUsd + OutputUsd;
}

public sealed record CreditCalculationResult
{
    public required BillingChannel Channel { get; init; }
    public required PricingStatus Status { get; init; }
    public required string RatesVersion { get; init; }
    public string? ModelId { get; init; }                 // canonical id if a rate card matched, else the normalized effective model
    public string? RateCardModelId { get; init; }         // rate card applied (the baseline for Local); null if none
    public string? TierName { get; init; }                // set only when a tier was applied
    public long UncachedInputTokens { get; init; }
    public TokenCostBreakdown? Breakdown { get; init; }   // priced at RateCardModelId's rates; null if nothing was priced
    public decimal UsdCost { get; init; }                 // billed USD (GitHub AI credits or Foundry BYOK); otherwise 0
    public decimal AiCredits { get; init; }               // UsdCost / 0.01m when Channel is GitHubAiCredits; otherwise 0
    public decimal EstimatedCloudSavedUsd { get; init; }  // Local only: Breakdown.TotalUsd; otherwise 0
}

// ---- src/CopilotLedger.Core/Pricing/ICreditCalculator.cs
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Pricing;

public interface ICreditCalculator
{
    string RatesVersion { get; }

    // Pure and thread-safe. Throws only ArgumentNullException; every span problem is reported through PricingStatus.
    CreditCalculationResult Calculate(NormalizedGenAiSpan span);
}

// ---- src/CopilotLedger.Core/Git/GitWorkspaceContext.cs
namespace CopilotLedger.Core.Git;

public sealed record GitWorkspaceContext(
    string RepositoryRoot,      // absolute working-tree root (the directory that contains .git)
    string GitDirectory,        // absolute per-worktree git directory (contains HEAD)
    string? BranchName,         // for example "feature/x"; null when HEAD is detached
    string? HeadCommitSha,      // lowercase hex (40 or 64 chars); null for an unborn branch
    string? RemoteOriginUrl,    // remote.origin.url with userinfo removed; null if absent
    string? RepositorySlug)     // "owner/repo"; null if not derivable
{
    public bool IsDetachedHead => BranchName is null && HeadCommitSha is not null;
}

// ---- src/CopilotLedger.Core/Git/IGitContextResolver.cs
namespace CopilotLedger.Core.Git;

public interface IGitContextResolver
{
    // Returns null when workingDirectory doesn't exist or isn't inside a Git working tree.
    GitWorkspaceContext? Resolve(string workingDirectory);
}
```

### Acceptance criteria

- `CopilotLedger.slnx` is at the repository root and contains `src/CopilotLedger.Core/CopilotLedger.Core.csproj` and `tests/CopilotLedger.Tests/CopilotLedger.Tests.csproj`. There's no `.sln` file.
- `global.json`, `Directory.Build.props`, and `CopilotLedger.Core.csproj` match this section exactly.
- Every contract above exists exactly as specified, and `CopilotLedger.Core` has no other public types.
- `src/` contains no `double` or `float`.
- `dotnet build CopilotLedger.slnx` succeeds with 0 warnings, and `dotnet test CopilotLedger.slnx` passes C1–C7 with 0 failed and 0 skipped.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

Report the passed, failed, and skipped counts.

## Task 2 specification: AI Credit Rate Engine & Zero-Config Git Resolver

### Files

Create:

```text
src/CopilotLedger.Core/Pricing/rates.json
src/CopilotLedger.Core/Pricing/RateCardDocument.cs
src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs
src/CopilotLedger.Core/Git/GitRemoteUrl.cs
src/CopilotLedger.Core/Git/FileSystemGitContextResolver.cs
src/CopilotLedger.Core/LedgerPaths.cs
tests/CopilotLedger.Tests/TestSupport/TempDirectory.cs
tests/CopilotLedger.Tests/TestSupport/TestSpans.cs
tests/CopilotLedger.Tests/TestSupport/RateFixtures.cs
tests/CopilotLedger.Tests/Pricing/JsonRateCardCalculatorTests.cs
tests/CopilotLedger.Tests/Pricing/RateCardLoadingTests.cs
tests/CopilotLedger.Tests/Pricing/RateOverlayTests.cs
tests/CopilotLedger.Tests/Git/GitRemoteUrlTests.cs
tests/CopilotLedger.Tests/Git/FileSystemGitContextResolverTests.cs
```

Also modify `docs/configuration.md` to remove its status line (see "Tests" below).

Replace `src/CopilotLedger.Core/CopilotLedger.Core.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <EmbeddedResource Include="Pricing\rates.json" LogicalName="CopilotLedger.Core.Pricing.rates.json" />
  </ItemGroup>
</Project>
```

### rates.json (embedded; copy exactly)

```json
{
  "schemaVersion": 1,
  "ratesVersion": "2026-11-20",
  "source": {
    "url": "https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing",
    "retrievedOn": "2026-11-20"
  },
  "notes": [
    "Rates are USD per 1,000,000 tokens. 1 GitHub AI credit = 0.01 USD.",
    "gen_ai.usage.input_tokens includes cache-read and cache-write tokens; uncached input = input - cache read - cache write.",
    "A null cachedInputPer1M or cacheWritePer1M bills those tokens at inputPer1M.",
    "maxInputTokens is an inclusive bound on total input tokens and selects the tier for the whole request; 272K = 272000.",
    "Windows are [effectiveFrom, effectiveUntil) and match the span start time.",
    "mai-code-2 promotional pricing ends 2027-05-17T00:00:00Z.",
    "foundryByok.models is empty because Azure prices vary by region, deployment type, and agreement; add yours in ~/.copilot-ledger/rates.json (see docs/configuration.md).",
    "Local spans bill 0 USD; savings price the same tokens at the GitHub rates of localOffload.baselineModel. localOffload.models maps local runtime ids to one canonical model."
  ],
  "githubAiCredits": {
    "hostSuffixes": ["githubcopilot.com"],
    "models": [
      {
        "id": "claude-opus-5.5",
        "aliases": [],
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
        "aliases": [],
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
        "id": "mai-code-2",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": "2027-05-17T00:00:00Z",
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 0.50, "cachedInputPer1M": 0.025, "cacheWritePer1M": null, "outputPer1M": 2.50 }
            ]
          },
          {
            "effectiveFrom": "2027-05-17T00:00:00Z",
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 1.00, "cachedInputPer1M": 0.05, "cacheWritePer1M": null, "outputPer1M": 5.00 }
            ]
          }
        ]
      }
    ]
  },
  "foundryByok": {
    "hostSuffixes": ["services.ai.azure.com", "openai.azure.com", "cognitiveservices.azure.com"],
    "providerNames": ["azure.ai.inference"],
    "models": []
  },
  "localOffload": {
    "baselineModel": "mai-code-2",
    "models": [
      { "id": "qwen3.8-27b", "aliases": ["qwen-3.8-27b", "qwen3.8:27b", "qwen3.8:27b-q4_k_m", "qwen3.8-27b-cuda-gpu:1"] }
    ],
    "additionalHosts": [],
    "providerNames": []
  }
}
```

### RateCardDocument (internal DTOs)

`RateCardDocument.cs` holds `internal sealed` DTO classes that mirror the JSON. The same DTOs read both the embedded file and the user overlay.

- Every property has `[JsonPropertyName("...")]`. Rates are `decimal`, `maxInputTokens` is `long?`, window bounds are `DateTimeOffset?`, `retrievedOn` is `string`, and lists are `IReadOnlyList<T>`.
- In priced model entries, these are required (use the `required` modifier): `id`, `windows`, and each tier's `name`, `inputPer1M`, and `outputPer1M`. These are optional: `aliases` (default `[]`) and `effectiveFrom`, `effectiveUntil`, `maxInputTokens`, `cachedInputPer1M`, and `cacheWritePer1M` (default null). Local model entries require `id`; `aliases` defaults to `[]`.
- Top-level and section properties are optional in the JSON and are checked after deserializing.
  - The **base** document (the embedded file, or `Parse`'s first argument) must contain `schemaVersion`, `ratesVersion`, `source.url`, `source.retrievedOn`, `githubAiCredits.hostSuffixes`, `githubAiCredits.models`, `foundryByok.hostSuffixes`, `foundryByok.providerNames`, `foundryByok.models`, `localOffload.baselineModel`, `localOffload.models`, `localOffload.additionalHosts`, and `localOffload.providerNames`.
  - An **overlay** must contain `schemaVersion`.
  - `notes` is optional in both. A missing required property throws `InvalidDataException` naming its JSON path.

Deserialize with these options:

```csharp
new JsonSerializerOptions
{
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    AllowTrailingCommas = false,
    NumberHandling = JsonNumberHandling.Strict,
}
```

### JsonRateCardCalculator

```csharp
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Pricing;

public sealed class JsonRateCardCalculator : ICreditCalculator
{
    public const string EmbeddedResourceName = "CopilotLedger.Core.Pricing.rates.json";

    public static JsonRateCardCalculator LoadEmbedded();
    public static JsonRateCardCalculator LoadEmbeddedWithOverlay(string overlayPath);   // embedded + the overlay file at overlayPath
    public static JsonRateCardCalculator LoadDefault();                                 // embedded + LedgerPaths.DefaultRatesOverlayPath when that file exists
    public static JsonRateCardCalculator Parse(string json, string? overlayJson = null);

    public string RatesVersion { get; }
    public CreditCalculationResult Calculate(NormalizedGenAiSpan span);
}
```

- The constructor is private. Instances are immutable, and `Calculate` is pure and thread-safe.
- Every load failure throws `InvalidDataException`: malformed JSON, a schema mismatch, or a broken validation rule. Wrap any `JsonException` as the inner exception. Messages name the JSON path, for example `githubAiCredits.models[2].windows[0].tiers[1].maxInputTokens`. Messages about overlay problems start with the overlay's file path, or with `overlay` when it came from `Parse`. `LoadEmbeddedWithOverlay` throws `FileNotFoundException` if the file doesn't exist.

**Load validation rules**

1. `schemaVersion` is 1, and `ratesVersion` isn't blank.
2. Within each table (`githubAiCredits.models`, `foundryByok.models`, and `localOffload.models`), ids aren't blank, and the normalized ids and aliases are all unique.
3. Each priced model (in `githubAiCredits.models` or `foundryByok.models`) has at least one window. When both bounds are set, `effectiveFrom < effectiveUntil`. Windows are sorted by `effectiveFrom` (null first) and don't overlap: every window except the last has a non-null `effectiveUntil` that's ≤ the next window's non-null `effectiveFrom`.
4. Each window has at least one tier, and tier names aren't blank and are unique within the window. Every tier except the last has `maxInputTokens` > 0, strictly ascending. The last tier's `maxInputTokens` is null.
5. Every rate is ≥ 0 and a multiple of 0.000001 (`rate * 1_000_000m % 1m == 0m`).
6. Host suffixes are trimmed and lower-cased (invariant). They can't be blank, start with `.`, or contain `/` or `:`. Additional hosts and provider names are trimmed and matched case-insensitively.
7. `localOffload.baselineModel`, normalized, resolves to a `githubAiCredits` model id or alias.

**Rates overlay**

`LoadEmbeddedWithOverlay`, `LoadDefault` (when its file exists), and `Parse` with `overlayJson` all merge the overlay into the base document before validating:

1. `RatesVersion` becomes `<base ratesVersion>+<overlay ratesVersion>`, or `<base ratesVersion>+user` when the overlay's `ratesVersion` is missing or blank. For example, `2026-11-20+contoso-2026-11`.
2. String lists (`hostSuffixes`, `providerNames`, `additionalHosts`) are combined: base entries first, then any overlay entries not already present (case-insensitive).
3. In the model lists (`githubAiCredits.models`, `foundryByok.models`, `localOffload.models`), an overlay entry replaces the base entry with the same normalized `id`, in place. Overlay entries with new ids are appended.
4. An overlay `localOffload.baselineModel` replaces the base value.
5. The overlay's `source` and `notes` are informational and ignored.
6. Validation rules 1–7 then run on the merged document. Rule 1 also requires the overlay's `schemaVersion` to be 1.

**Calculate algorithm**

1. If `span` is null, throw `ArgumentNullException`. Otherwise `Calculate` never throws.
2. `model` is the first non-blank of `ResponseModel` and `RequestModel`, normalized per Design decision 6, or null if both are blank.
3. If `!GenAiOperationNames.IsInference(span.OperationName)`, return `Unpriced` / `NotBillableOperation`.
4. If any token count is negative, or `CacheReadInputTokens + CacheWriteInputTokens > InputTokens`, return `Unpriced` / `InvalidUsage`.
5. `uncached = InputTokens - CacheReadInputTokens - CacheWriteInputTokens`.
6. Resolve the channel (Design decision 2). Normalize the host first: trim it and remove one surrounding `[` `]` pair. The host is loopback if `host.Equals("localhost", StringComparison.OrdinalIgnoreCase)`, or if `IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip)`. A suffix matches if `host.Equals(suffix, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase)`.
7. **Local:** price the usage with the baseline card, choosing the window by `StartTime` and the tier by `InputTokens`. If no window matches, return `Local` / `NoRateForDate`. Otherwise return `Local` / `LocalOffload` with `RateCardModelId` = the baseline id, `EstimatedCloudSavedUsd = Breakdown.TotalUsd`, and `UsdCost = AiCredits = 0`.
8. **FoundryByok:** if `model` is null, return `UnknownModel`. If there's no match in `foundryByok.models`, return `ByokUnpriced`. If no window matches, return `NoRateForDate`. Otherwise return `Priced` with `UsdCost = Breakdown.TotalUsd` and `AiCredits = 0`.
9. **GitHubAiCredits:** if `model` is null or has no match in `githubAiCredits.models`, return `UnknownModel`. If no window matches, return `NoRateForDate`. Otherwise return `Priced` with `UsdCost = Breakdown.TotalUsd` and `AiCredits = AiCreditMath.UsdToAiCredits(UsdCost)`.
10. **Unrecognized:** return `Unpriced` / `UnrecognizedEndpoint`.

These rules apply to every result:

- `RatesVersion` is always set.
- `ModelId` is the matched rate card's canonical id when a card matched. In Local results it's the canonical `localOffload.models` id when the model matches one, so `qwen3.8:27b` becomes `qwen3.8-27b`. Otherwise it's `model`.
- `UncachedInputTokens` is 0 for steps 3 and 4, and `uncached` for every other result.
- When the status isn't `Priced` or `LocalOffload`, `Breakdown` and `TierName` are null, and every amount is 0.

The breakdown uses the selected tier:

- `UncachedInputUsd = AiCreditMath.CostUsd(uncached, InputPer1M)`
- `CacheReadUsd = AiCreditMath.CostUsd(CacheReadInputTokens, CachedInputPer1M ?? InputPer1M)`
- `CacheWriteUsd = AiCreditMath.CostUsd(CacheWriteInputTokens, CacheWritePer1M ?? InputPer1M)`
- `OutputUsd = AiCreditMath.CostUsd(OutputTokens, OutputPer1M)`

### LedgerPaths

```csharp
namespace CopilotLedger.Core;

public static class LedgerPaths
{
    // Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot-ledger")
    public static string DataDirectory { get; }

    // Path.Combine(DataDirectory, "ledger.db")
    public static string DefaultDatabasePath { get; }

    // Path.Combine(DataDirectory, "rates.json"), the optional user rates overlay
    public static string DefaultRatesOverlayPath { get; }
}
```

`LedgerPaths` only computes paths; it never creates directories. Tests never read or write these paths.

### GitRemoteUrl

```csharp
namespace CopilotLedger.Core.Git;

public static class GitRemoteUrl
{
    // Trims. For http:// and https:// URLs, removes the userinfo: everything after "://" through the last "@"
    // before the first "/" of the path. Every other form comes back trimmed but otherwise unchanged.
    public static string Sanitize(string remoteUrl);

    // "owner/repo", or null. See the slug rules.
    public static string? GetRepositorySlug(string remoteUrl);
}
```

**Slug rules**

1. Trim the URL, remove one trailing `/`, then remove one trailing `.git` (ordinal, case-insensitive).
2. **URL form** (contains `://`): if the scheme is `file`, return null. Otherwise the path is everything after the authority, starting at the first `/` after `://`.
3. **scp form**: no `://`, a `:` before any `/`, and not a Windows drive path such as `C:\` or `C:/`. The path is everything after the first `:`.
4. Anything else, such as a local path, returns null.
5. Split the path on `/` and drop empty segments. If exactly two segments remain, return `"{segment0}/{segment1}"` with the original casing. Otherwise return null.

### FileSystemGitContextResolver

```csharp
namespace CopilotLedger.Core.Git;

public sealed class FileSystemGitContextResolver : IGitContextResolver
{
    // When ceilingDirectory is set, the upward search examines the ceiling itself but never goes above it.
    public FileSystemGitContextResolver(string? ceilingDirectory = null);

    public GitWorkspaceContext? Resolve(string workingDirectory);
}
```

**Resolve algorithm**

1. Return null if `workingDirectory` is blank or `!Directory.Exists(workingDirectory)`. Otherwise start at `Path.GetFullPath(workingDirectory)` with trailing separators trimmed.
2. Walk up one directory at a time. In each directory `D`, look at `D/.git`:
   - If it's a directory, it's `gitDir`.
   - If it's a file, its first line must start with `gitdir:`. The rest of that line, trimmed, is `gitDir`, resolved against `D` if it isn't rooted. Any other content returns null.
   - At the first `.git` found, stop: `repoRoot = D`. If `gitDir/HEAD` doesn't exist, return null.
   - If no `.git` turns up by the time the ceiling (compared case-insensitively on Windows) or the filesystem root has been examined, return null.
3. If `gitDir/commondir` exists, `commonDir` is its first line, trimmed, resolved against `gitDir`, and passed through `Path.GetFullPath`. Otherwise `commonDir = gitDir`.
4. Read `gitDir/HEAD` and trim it.
   - `ref: <refName>` sets `BranchName` to `refName` without a leading `refs/heads/`, and `HeadCommitSha = ResolveRef(refName)`.
   - 40 or 64 hex characters is a detached HEAD: `BranchName = null`, and `HeadCommitSha` is the value lower-cased.
   - Anything else leaves both null.
5. `ResolveRef(refName)` follows at most 5 levels:
   - Look for the loose file `gitDir/<refName>`, then `commonDir/<refName>`, mapping `/` to the platform separator. In the first one that exists, read the first line and trim it. `ref: X` recurses on `X`; 40 or 64 hex characters returns the SHA lower-cased.
   - If neither loose file exists, scan `commonDir/packed-refs`. Skip blank lines and lines that start with `#` or `^`. Split each remaining line at the first space into `<sha> <name>`, and return the SHA lower-cased when `name` equals `refName` (ordinal).
   - If nothing matches, return null. That's an unborn branch.
6. If `commonDir/config` exists, parse it:
   - Trim each line. Skip blank lines and lines that start with `#` or `;`.
   - A section header is `[name]` or `[name "sub"]`. Compare `name` case-insensitively and `sub` case-sensitively, after unescaping `\"` and `\\` in `sub`.
   - A key line is `key = value` or `key=value`, and keys compare case-insensitively. Ignore a bare `key` with no `=`. To read a value, trim it; if it starts with `"`, read up to the closing unescaped `"`; otherwise cut it at the first `#` or `;` and trim again.
   - `RemoteOriginUrl` is `GitRemoteUrl.Sanitize` applied to the first `url` key in section `remote`, subsection `origin`. `RepositorySlug = GitRemoteUrl.GetRepositorySlug(RemoteOriginUrl)`.
   - These aren't supported and are ignored: `include`/`includeIf`, `url.<base>.insteadOf`, and `config.worktree`.
7. Return `new GitWorkspaceContext(repoRoot, Path.GetFullPath(gitDir), BranchName, HeadCommitSha, RemoteOriginUrl, RepositorySlug)`.

Never start a process or run `git`. If reading a file throws `IOException` or `UnauthorizedAccessException`, treat that file as missing. Don't cache anything; Sprint 2 adds caching and a `FileSystemWatcher`. The resolver is stateless and thread-safe.

### Tests

Write the cases in "Task 2 tests" under "Test specifications". When they pass, delete the `> Status:` line at the top of `docs/configuration.md`. The rest of that file describes this task's behavior and must stay accurate.

### Acceptance criteria

- `rates.json` matches this section exactly and is embedded as `CopilotLedger.Core.Pricing.rates.json`.
- The public surfaces of `JsonRateCardCalculator`, `LedgerPaths`, `GitRemoteUrl`, and `FileSystemGitContextResolver` match this section exactly. DTOs and helpers are `internal`.
- `docs/configuration.md` matches the implemented behavior, and its `> Status:` line is gone.
- `src/` contains no `double`, `float`, or `System.Diagnostics.Process`.
- `dotnet build CopilotLedger.slnx` succeeds with 0 warnings, and `dotnet test CopilotLedger.slnx` passes the Task 1 and Task 2 cases with 0 failed and 0 skipped.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

Report the passed, failed, and skipped counts.

## Task 3 specification: SQLite WAL Ledger & xUnit Suite

### Files

```text
src/CopilotLedger.Storage/CopilotLedger.Storage.csproj
src/CopilotLedger.Storage/PicoUsd.cs
src/CopilotLedger.Storage/LedgerEntry.cs
src/CopilotLedger.Storage/LedgerGrouping.cs
src/CopilotLedger.Storage/LedgerFilter.cs
src/CopilotLedger.Storage/LedgerRollupRow.cs
src/CopilotLedger.Storage/LedgerMigrations.cs            (internal)
src/CopilotLedger.Storage/SqliteLedgerStore.cs
tests/CopilotLedger.Tests/Storage/PicoUsdTests.cs
tests/CopilotLedger.Tests/Storage/SqliteLedgerStoreTests.cs
```

Also update `CopilotLedger.slnx`, `tests/CopilotLedger.Tests/CopilotLedger.Tests.csproj` (add the Storage reference), and `tests/CopilotLedger.Tests/TestSupport/TempDirectory.cs` (clear SQLite pools).

### Steps

From the repository root, run:

```shell
dotnet new classlib -n CopilotLedger.Storage -o src/CopilotLedger.Storage
dotnet add src/CopilotLedger.Storage/CopilotLedger.Storage.csproj package Microsoft.Data.Sqlite
dotnet add src/CopilotLedger.Storage/CopilotLedger.Storage.csproj reference src/CopilotLedger.Core/CopilotLedger.Core.csproj
dotnet add tests/CopilotLedger.Tests/CopilotLedger.Tests.csproj reference src/CopilotLedger.Storage/CopilotLedger.Storage.csproj
dotnet sln CopilotLedger.slnx add src/CopilotLedger.Storage/CopilotLedger.Storage.csproj
```

Then:

- Delete the template's `Class1.cs`.
- In `CopilotLedger.Storage.csproj`, remove `TargetFramework`, `ImplicitUsings`, and `Nullable`, because they're inherited from `Directory.Build.props`. Also remove any `PropertyGroup` left empty. Keep everything else that the template and `dotnet add` generated, including package versions.
- `Microsoft.Data.Sqlite` must resolve to a stable 10.0.x version. If `dotnet add package` chose another major version, re-run it with `--version` set to the latest stable 10.0.x.
- Don't reference `Microsoft.Data.Sqlite.Core` or the SQLitePCLRaw packages directly. Don't add assertion or mocking libraries.
- In `TempDirectory.Dispose()`, call `SqliteConnection.ClearAllPools()` before deleting, so pooled connections release the database files on Windows.
- Write the cases in "Task 3 tests" under "Test specifications".

### Storage API

```csharp
// All types: namespace CopilotLedger.Storage. The default database path is CopilotLedger.Core.LedgerPaths.DefaultDatabasePath.

public static class PicoUsd
{
    public const long PerUsd = 1_000_000_000_000L;

    // Throws ArgumentOutOfRangeException if usd < 0, if usd * 10^12 isn't an integer, or if the result exceeds long.MaxValue.
    public static long FromUsd(decimal usd);

    // picoUsd / 1_000_000_000_000m
    public static decimal ToUsd(long picoUsd);
}

public sealed record LedgerEntry(NormalizedGenAiSpan Span, CreditCalculationResult Calculation, GitWorkspaceContext? Git);

public enum LedgerGrouping
{
    Repository,
    Branch,      // grouped by repository and branch
    Model,
    Agent,       // gen_ai.agent.name
}

public sealed record LedgerFilter
{
    public static LedgerFilter All { get; } = new();
    public DateTimeOffset? From { get; init; }   // inclusive, on span StartTime
    public DateTimeOffset? To { get; init; }     // exclusive
    public string? Repository { get; init; }     // equals the repository key
    public string? Branch { get; init; }         // equals the branch key
}

public sealed record LedgerRollupRow
{
    public string? Repository { get; init; }                 // set for the Repository and Branch groupings
    public string? Branch { get; init; }                     // set for the Branch grouping
    public string? Model { get; init; }                      // set for the Model grouping
    public string? Agent { get; init; }                      // set for the Agent grouping
    public required long SpanCount { get; init; }
    public required long UnpricedSpanCount { get; init; }    // status other than Priced or LocalOffload
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required decimal AiCredits { get; init; }         // AiCreditMath.UsdToAiCredits(AiCreditsUsd)
    public required decimal AiCreditsUsd { get; init; }      // GitHubAiCredits channel
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
}

public sealed class SqliteLedgerStore
{
    public SqliteLedgerStore(string databasePath, TimeProvider? timeProvider = null);   // null uses TimeProvider.System
    public string DatabasePath { get; }

    // Creates the directory and database, enables WAL, and applies migrations. Idempotent.
    // Throws InvalidOperationException if the database's user_version is newer than this build supports.
    public Task InitializeAsync(CancellationToken cancellationToken = default);

    // Writes in one transaction and returns the number of rows inserted. Existing (trace_id, span_id) rows are skipped.
    // Throws InvalidOperationException if InitializeAsync hasn't run.
    // Throws ArgumentException, writing nothing, if any entry has Status NotBillableOperation or a negative token count.
    public Task<int> RecordAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default);

    // Throws InvalidOperationException if InitializeAsync hasn't run.
    public Task<IReadOnlyList<LedgerRollupRow>> GetRollupAsync(LedgerGrouping grouping, LedgerFilter filter, CancellationToken cancellationToken = default);
}
```

### Connection and migration rules

- Build the connection string with `new SqliteConnectionStringBuilder { DataSource = DatabasePath, Mode = SqliteOpenMode.ReadWriteCreate }`, leaving pooling at its default. Each operation opens its own connection, so the store is safe to use concurrently.
- Every connection runs `PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000;` after it opens.
- `InitializeAsync` calls `Directory.CreateDirectory` on the database's folder, then runs `PRAGMA journal_mode = WAL;`. If the returned value isn't `wal` (case-insensitive), it throws `InvalidOperationException`. Then it migrates.
- `internal static class LedgerMigrations` holds an ordered list of `(int Version, string Sql)`. If `PRAGMA user_version` is greater than the latest version, throw `InvalidOperationException`. Otherwise apply each pending migration in its own transaction, together with `PRAGMA user_version = <Version>` (an integer literal from code), and commit.
- All SQL is parameterized. Grouping SQL comes from a fixed `switch` on `LedgerGrouping`; never interpolate caller strings into SQL.
- `recorded_at_unix_ms` comes from `timeProvider.GetUtcNow()`, and all times are stored as `ToUnixTimeMilliseconds()`.

### Migration 1 (copy exactly)

```sql
CREATE TABLE spans (
    id                       INTEGER PRIMARY KEY,
    trace_id                 TEXT    NOT NULL,
    span_id                  TEXT    NOT NULL,
    parent_span_id           TEXT,
    operation_name           TEXT    NOT NULL,
    conversation_id          TEXT,
    agent_name               TEXT,
    provider_name            TEXT,
    request_model            TEXT,
    response_model           TEXT,
    model_id                 TEXT,
    rate_card_model_id       TEXT,
    tier_name                TEXT,
    server_address           TEXT,
    server_port              INTEGER,
    start_time_unix_ms       INTEGER NOT NULL,
    end_time_unix_ms         INTEGER NOT NULL,
    input_tokens             INTEGER NOT NULL CHECK (input_tokens >= 0),
    output_tokens            INTEGER NOT NULL CHECK (output_tokens >= 0),
    cache_read_input_tokens  INTEGER NOT NULL CHECK (cache_read_input_tokens >= 0),
    cache_write_input_tokens INTEGER NOT NULL CHECK (cache_write_input_tokens >= 0),
    billing_channel          TEXT    NOT NULL,
    pricing_status           TEXT    NOT NULL,
    rates_version            TEXT    NOT NULL,
    cost_usd_pico            INTEGER NOT NULL CHECK (cost_usd_pico >= 0),
    saved_usd_pico           INTEGER NOT NULL CHECK (saved_usd_pico >= 0),
    repo_root                TEXT,
    repo_slug                TEXT,
    branch_name              TEXT,
    head_commit_sha          TEXT,
    recorded_at_unix_ms      INTEGER NOT NULL,
    UNIQUE (trace_id, span_id)
) STRICT;

CREATE INDEX ix_spans_start_time  ON spans (start_time_unix_ms);
CREATE INDEX ix_spans_repo_branch ON spans (repo_slug, branch_name);
CREATE INDEX ix_spans_model       ON spans (model_id);
CREATE INDEX ix_spans_agent       ON spans (agent_name);
```

### Column mapping

- Span fields map to the columns of the same name. `model_id`, `rate_card_model_id`, `tier_name`, and `rates_version` come from `Calculation`.
- `cost_usd_pico = PicoUsd.FromUsd(Calculation.UsdCost)` and `saved_usd_pico = PicoUsd.FromUsd(Calculation.EstimatedCloudSavedUsd)`.
- `repo_root`, `repo_slug`, `branch_name`, and `head_commit_sha` come from `Git`, and are null when `Git` is null.
- Enum columns use these fixed snake_case strings. Never use `Enum.ToString()`.
  - `billing_channel`: `github_ai_credits`, `foundry_byok`, `local`, `unpriced`.
  - `pricing_status`: `priced`, `local_offload`, `byok_unpriced`, `unknown_model`, `no_rate_for_date`, `unrecognized_endpoint`, `invalid_usage`, `not_billable_operation`.
- Insert with `INSERT INTO spans (...) VALUES (...) ON CONFLICT (trace_id, span_id) DO NOTHING;` and count the rows affected.

### Rollup query

These are the group keys:

- Repository: `COALESCE(repo_slug, repo_root, '(none)') AS repository`
- Branch: `repository`, plus `COALESCE(branch_name, '(none)') AS branch`
- Model: `COALESCE(model_id, '(none)') AS model`
- Agent: `COALESCE(agent_name, '(none)') AS agent`

```sql
SELECT <key columns>,
       COUNT(*)                                                                          AS span_count,
       SUM(CASE WHEN pricing_status IN ('priced', 'local_offload') THEN 0 ELSE 1 END)    AS unpriced_span_count,
       SUM(input_tokens)                                                                 AS input_tokens,
       SUM(output_tokens)                                                                AS output_tokens,
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
GROUP BY <key aliases>
ORDER BY github_usd_pico + byok_usd_pico DESC, saved_usd_pico DESC, <key aliases ascending>;
```

Convert the pico sums with `PicoUsd.ToUsd`, and compute `AiCredits = AiCreditMath.UsdToAiCredits(AiCreditsUsd)`. Bind null filter values as `DBNull.Value`.

### Acceptance criteria

- `CopilotLedger.slnx` contains `CopilotLedger.Core`, `CopilotLedger.Storage`, and `CopilotLedger.Tests`.
- Migration 1 matches this plan exactly. Money columns are `INTEGER`, and there's no `REAL` anywhere.
- The Task 3 cases (the `PicoUsd` cases and S1–S11) exist and pass, and the whole suite for Tasks 1–3 runs with 0 failed and 0 skipped.
- No test touches the network, anything under `LedgerPaths.DataDirectory`, or `git`. `src/` contains no `double`, `float`, or `System.Diagnostics.Process`.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

Report the passed, failed, and skipped counts.

## Test specifications

Shared rules for every task:

- Money assertions use `decimal` values, through `TheoryData<..., decimal>` or `decimal.Parse(s, CultureInfo.InvariantCulture)`. Decimal equality ignores trailing zeros, so `286m == 286.00m`.
- Tests use the public API only. No `InternalsVisibleTo`.
- Each test that writes files gets its own `TempDirectory`. Nothing touches `LedgerPaths.DataDirectory` (the real database or rates overlay), the network, or `git`.
- Unless a case says otherwise, a span has `OperationName = "chat"`, `StartTime = 2026-11-20T12:00:00Z`, `EndTime = StartTime + 2s`, and null `ServerAddress` (the GitHub channel).

### Task 1 tests

| # | Class | Case | Expected |
|---|---|---|---|
| C1 | `GenAiOperationNamesTests` | `IsInference` for `chat`, `CHAT`, `text_completion`, and `generate_content` (theory) | true |
| C2 | `GenAiOperationNamesTests` | `IsInference` for `invoke_agent`, `execute_tool`, `embeddings`, empty, whitespace, and null (theory) | false |
| C3 | `AiCreditMathTests` | `CostUsd` for (1, 0.025), (272,001, 4.00), (1,000,000, 20.00), and (0, 4.00) | 0.000000025, 1.088004, 20, and 0 |
| C4 | `AiCreditMathTests` | `CostUsd` with tokens -1, or with rate -0.01 | throws `ArgumentOutOfRangeException` |
| C5 | `AiCreditMathTests` | `UsdToAiCredits` for 2.86, 1.238004, 0.000000025, and 0 | 286, 123.8004, 0.0000025, and 0 |
| C6 | `AiCreditMathTests` | `new TokenCostBreakdown(1.20m, 0.16m, 0.50m, 1.00m).TotalUsd` | 2.86 |
| C7 | `GitWorkspaceContextTests` | `IsDetachedHead` for (branch null, a 40-hex SHA), (`main`, a SHA), (`main`, null), and (null, null) | true, false, false, and false |

### Task 2 tests

#### Test support

- `TempDirectory : IDisposable` creates `Path.Combine(Path.GetTempPath(), "copilot-ledger-tests", Guid.NewGuid().ToString("N"))`. `Dispose()` deletes the directory recursively, ignoring `IOException` and `UnauthorizedAccessException`. Task 3 adds a `SqliteConnection.ClearAllPools()` call before the delete.
- `TestSpans.Chat(...)` builds a `NormalizedGenAiSpan` with the defaults above. It sets `TraceId = Guid.NewGuid().ToString("N")`, `SpanId` to the first 16 characters of another `Guid.NewGuid().ToString("N")`, and takes optional parameters for every other field.
- `RateFixtures.ByokTestRatesJson` holds this exact JSON, with fictional rates for tests only:

```json
{
  "schemaVersion": 1,
  "ratesVersion": "test-fixture",
  "source": { "url": "https://example.invalid/rates", "retrievedOn": "2026-11-20" },
  "notes": ["Fictional rates for tests only."],
  "githubAiCredits": {
    "hostSuffixes": ["githubcopilot.com"],
    "models": [
      {
        "id": "claude-sonnet-5.5",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 2.50, "outputPer1M": 10.00 }
            ]
          }
        ]
      }
    ]
  },
  "foundryByok": {
    "hostSuffixes": ["services.ai.azure.com", "openai.azure.com", "cognitiveservices.azure.com"],
    "providerNames": ["azure.ai.inference"],
    "models": [
      {
        "id": "gpt-6.1-sol",
        "aliases": [],
        "windows": [
          {
            "effectiveFrom": null,
            "effectiveUntil": null,
            "tiers": [
              { "name": "default", "maxInputTokens": null, "inputPer1M": 1.75, "cachedInputPer1M": 0.175, "cacheWritePer1M": null, "outputPer1M": 14.00 }
            ]
          }
        ]
      }
    ]
  },
  "localOffload": {
    "baselineModel": "claude-sonnet-5.5",
    "models": [],
    "additionalHosts": [],
    "providerNames": []
  }
}
```

#### Pricing: `JsonRateCardCalculatorTests` (embedded rates unless noted)

`LoadEmbedded()` succeeds and `RatesVersion == "2026-11-20"`.

GitHub vectors. Each expects `Channel = GitHubAiCredits`, `Status = Priced`, `ModelId = RateCardModelId = model`, and `EstimatedCloudSavedUsd = 0`.

| # | Model | Start (UTC) | Input | Cache read | Cache write | Output | Tier | UsdCost | AiCredits |
|---|---|---|---|---|---|---|---|---|---|
| V1 | `claude-opus-5.5` | 2026-11-20T12:00:00Z | 1,000,000 | 0 | 0 | 1,000,000 | default | 24.00 | 2400 |
| V2 | `claude-opus-5.5` | 2026-11-20T12:00:00Z | 1,200,000 | 800,000 | 100,000 | 50,000 | default | 2.86 | 286 |
| V3 | `claude-sonnet-5.5` | 2026-11-20T12:00:00Z | 10,000 | 0 | 0 | 2,000 | default | 0.04 | 4 |
| V4 | `gpt-6.1-sol` | 2026-11-20T12:00:00Z | 272,000 | 0 | 0 | 10,000 | default | 0.644 | 64.4 |
| V5 | `gpt-6.1-sol` | 2026-11-20T12:00:00Z | 272,001 | 0 | 0 | 10,000 | long_context | 1.238004 | 123.8004 |
| V6 | `gpt-6.1-sol` | 2026-11-20T12:00:00Z | 400,000 | 300,000 | 50,000 | 20,000 | long_context | 0.81 | 81 |
| V7 | `gpt-6-astra` | 2026-11-20T12:00:00Z | 100,000 | 40,000 | 10,000 | 5,000 | default | 0.915 | 91.5 |
| V8 | `mai-code-2` | 2026-11-20T12:00:00Z | 1,000,000 | 400,000 | 0 | 100,000 | default | 0.56 | 56 |
| V9 | `mai-code-2` | 2027-05-16T23:59:59Z | 1,000,000 | 400,000 | 0 | 100,000 | default | 0.56 | 56 |
| V10 | `mai-code-2` | 2027-05-17T00:00:00Z | 1,000,000 | 400,000 | 0 | 100,000 | default | 1.12 | 112 |
| V11 | `mai-code-2` | 2026-11-20T12:00:00Z | 1,000 | 0 | 1,000 | 0 | default | 0.0005 | 0.05 |
| V12 | `mai-code-2` | 2026-11-20T12:00:00Z | 1 | 1 | 0 | 0 | default | 0.000000025 | 0.0000025 |

Cache-discount checks:

- V2's breakdown is exactly `UncachedInputUsd 1.20`, `CacheReadUsd 0.16`, `CacheWriteUsd 0.50`, and `OutputUsd 1.00`, with `UncachedInputTokens 300,000`. The same usage with no cache tokens (input 1,200,000, output 50,000) costs 5.80, so caching saves exactly 2.94.
- V11 bills cache writes at the input rate because `cacheWritePer1M` is null: `CacheWriteUsd 0.0005`.
- V12 doesn't round: `CacheReadUsd 0.000000025`.

Other pricing cases:

| # | Span | Expected |
|---|---|---|
| P1 | `RequestModel "auto"`, `ResponseModel "claude-sonnet-5.5"`, V3 usage | Priced, `ModelId "claude-sonnet-5.5"`, UsdCost 0.04 |
| P2 | `ResponseModel "Claude Opus 5.5"`, V1 usage | Priced, `ModelId "claude-opus-5.5"`, UsdCost 24.00 |
| P3 | `OperationName` `invoke_agent` and `execute_tool` (theory), V1 usage | Unpriced, NotBillableOperation, every amount 0 |
| P4 | input 100, cache read 80, cache write 30 | Unpriced, InvalidUsage, every amount 0 |
| P5 | output -1 | Unpriced, InvalidUsage |
| P6 | model `gpt-unknown` | GitHubAiCredits, UnknownModel, every amount 0 |
| P7 | no request or response model | GitHubAiCredits, UnknownModel |
| P8 | host `api.githubcopilot.com`, `API.Enterprise.GitHubCopilot.com`, empty, or null (theory), V3 usage | GitHubAiCredits, Priced, UsdCost 0.04 |
| P9 | host `api.example.com`, `evilgithubcopilot.com`, or `evilopenai.azure.com` (theory), V3 usage | Unpriced, UnrecognizedEndpoint, every amount 0 |
| P10 | host `127.0.0.1`, `127.0.0.2`, `localhost`, `LOCALHOST`, `::1`, or `[::1]` (theory); model `qwen3.8-27b-cuda-gpu:1`; input 400,000; output 12,000 | Local, LocalOffload, UsdCost 0, AiCredits 0, EstimatedCloudSavedUsd 0.23, `RateCardModelId "mai-code-2"`, `TierName "default"`, `ModelId "qwen3.8-27b"` |
| P11 | host `contoso.openai.azure.com`, `contoso.services.ai.azure.com`, or `contoso.cognitiveservices.azure.com` (theory); `gpt-6.1-sol`; input 100,000; output 10,000 | FoundryByok, ByokUnpriced, every amount 0 |
| P12 | null host, `ProviderName "azure.ai.inference"`, same usage as P11 | FoundryByok, ByokUnpriced |
| P13 | `ByokTestRatesJson`; host `contoso.openai.azure.com`; `gpt-6.1-sol`; input 100,000; output 10,000 | FoundryByok, Priced, UsdCost 0.315, AiCredits 0, `TierName "default"` |
| P14 | as P10, with host `127.0.0.1` and start `2027-05-17T00:00:00Z` | LocalOffload, EstimatedCloudSavedUsd 0.46 (MAI Code 2's standard rates) |
| P15 | null host; model `qwen3.8:27b`, `Qwen 3.8 27B`, or `qwen3.8:27b-q4_K_M` (theory); P10 usage | Local, LocalOffload, EstimatedCloudSavedUsd 0.23, `ModelId "qwen3.8-27b"` |
| P16 | host `127.0.0.1`; model `phi-local-test`; P10 usage | Local, LocalOffload, EstimatedCloudSavedUsd 0.23, `ModelId "phi-local-test"` (an unlisted local model is still local) |

#### Pricing: `RateCardLoadingTests`

`Parse(ByokTestRatesJson)` succeeds with `RatesVersion == "test-fixture"`.

Each case below replaces the first occurrence of **Find** in `ByokTestRatesJson`, asserts the text changed, and expects `Parse` to throw `InvalidDataException`.

| # | Find | Replace with | Rule broken |
|---|---|---|---|
| M1 | `"schemaVersion": 1,` | `"schemaVersion": 2,` | schemaVersion must be 1 |
| M2 | `"inputPer1M": 2.00,` | `"inputPer1M": 2.0000001,` | more than 6 decimal places |
| M3 | `"baselineModel": "claude-sonnet-5.5"` | `"baselineModel": "gpt-missing"` | baseline must exist |
| M4 | `"schemaVersion": 1,` | `"schemaVersion": 1, "currency": "USD",` | unknown property |
| M5 | `"maxInputTokens": null, "inputPer1M": 2.00` | `"maxInputTokens": 1000, "inputPer1M": 2.00` | last tier must be unbounded |
| M6 | `"inputPer1M": 2.00,` | `"inputPer1M": -2.00,` | negative rate |
| M7 | `"ratesVersion": "test-fixture",` | nothing (delete it) | the base document must have `ratesVersion` |

Malformed JSON (the fixture with its final `}` removed) also throws `InvalidDataException`.

#### Pricing: `RateOverlayTests`

Unless a case says otherwise, each case writes the overlay to `<TempDirectory>/rates.json` and calls `JsonRateCardCalculator.LoadEmbeddedWithOverlay(path)`. Overlay A is the Foundry BYOK example from `docs/configuration.md`, with fictional rates:

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

| # | Overlay | Span | Expected |
|---|---|---|---|
| O1 | A | host `contoso.openai.azure.com`; `gpt-6.1-sol`; input 100,000; output 10,000 | FoundryByok, Priced, UsdCost 0.315, AiCredits 0, `RatesVersion "2026-11-20+contoso-2026-11"` |
| O2 | A | as O1, but model `contoso-gpt61-prod` (a Foundry deployment name) | Priced, `ModelId "gpt-6.1-sol"`, UsdCost 0.315 |
| O3 | A | V3 (GitHub) | GitHubAiCredits, Priced, UsdCost 0.04 (GitHub prices unaffected) |
| O4 | `{ "schemaVersion": 1, "localOffload": { "baselineModel": "claude-sonnet-5.5" } }` | P10 usage at host `127.0.0.1` | LocalOffload, EstimatedCloudSavedUsd 0.92, `RateCardModelId "claude-sonnet-5.5"`, `RatesVersion "2026-11-20+user"` |
| O5 | `{ "schemaVersion": 1, "localOffload": { "additionalHosts": ["gpu-box.lan"] } }` | host `gpu-box.lan`; model `qwen3.8:27b`; P10 usage | Local, LocalOffload, EstimatedCloudSavedUsd 0.23 |
| O6 | `{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "example-new-model", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1.00, "outputPer1M": 4.00 } ] } ] } ] } }` | model `example-new-model`; input 1,000,000; output 1,000,000 | GitHubAiCredits, Priced, UsdCost 5.00, AiCredits 500 |
| O7 | `{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "claude-opus-5.5", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 3.00, "cachedInputPer1M": 0.30, "cacheWritePer1M": 3.75, "outputPer1M": 15.00 } ] } ] } ] } }` | V1 usage | UsdCost 18.00, AiCredits 1800 (the overlay entry replaced the embedded one) |
| O8 | `{ "schemaVersion": 1, "localOffload": { "models": [ { "id": "qwen3.8-27b", "aliases": ["my-qwen-build"] } ] } }` | null host; model `my-qwen-build`; P10 usage | Local, `ModelId "qwen3.8-27b"`, EstimatedCloudSavedUsd 0.23 |

Each of these invalid overlays throws `InvalidDataException`, and the message contains the overlay's file path (theory):

| # | Overlay | Problem |
|---|---|---|
| O9 | `{ "schemaVersion": 2 }` | unsupported schema version |
| O10 | `{ "schemaVersion": 1, "foundryBYOK": { } }` | unknown property (wrong casing) |
| O11 | `{ "schemaVersion": 1, "localOffload": { "baselineModel": "gpt-missing" } }` | baseline model doesn't exist |
| O12 | `{ "schemaVersion": 1, "foundryByok": { "models": [ { "id": "gpt-6.1-sol", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1.75 } ] } ] } ] } }` | missing `outputPer1M` |
| O13 | `{ }` | missing `schemaVersion` |
| O14 | `{ "schemaVersion": 1, "localOffload": { "models": [ { "id": "my-local", "aliases": ["qwen3.8:27b"] } ] } }` | alias collides with the embedded Qwen entry |

O15: `LoadEmbeddedWithOverlay(<TempDirectory>/missing.json)` throws `FileNotFoundException`.

#### Git: `GitRemoteUrlTests` (theory)

| remote URL | `Sanitize` | `GetRepositorySlug` |
|---|---|---|
| `https://github.com/swigerb/copilot-ledger.git` | unchanged | `swigerb/copilot-ledger` |
| `https://github.com/swigerb/copilot-ledger` | unchanged | `swigerb/copilot-ledger` |
| `https://github.com/swigerb/copilot-ledger/` | unchanged | `swigerb/copilot-ledger` |
| `https://x-access-token:s3cr3t@github.com/swigerb/copilot-ledger.git` | `https://github.com/swigerb/copilot-ledger.git` | `swigerb/copilot-ledger` |
| `git@github.com:swigerb/copilot-ledger.git` | unchanged | `swigerb/copilot-ledger` |
| `ssh://git@github.com/swigerb/copilot-ledger.git` | unchanged | `swigerb/copilot-ledger` |
| `https://ghe.contoso.com/platform/billing.git` | unchanged | `platform/billing` |
| `https://dev.azure.com/contoso/project/_git/repo` | unchanged | null |
| `C:\src\mirror.git` | unchanged | null |
| `file:///srv/git/mirror.git` | unchanged | null |
| `/srv/git/mirror.git` | unchanged | null |

#### Git: `FileSystemGitContextResolverTests`

Each test builds a fixture under a fresh `TempDirectory` (called `<root>`) and uses `new FileSystemGitContextResolver(ceilingDirectory: <root>)`. File contents end with `\n`.

- `SHA1 = 0123456789abcdef0123456789abcdef01234567`
- `SHA256 = 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef`
- An "origin config" is a `config` file with `[remote "origin"]` and an indented `url = https://github.com/swigerb/copilot-ledger.git` on the next line.

| # | Fixture | Resolve from | Expected |
|---|---|---|---|
| G1 | `<root>/repo/.git/HEAD` = `ref: refs/heads/feature/ledger`; `<root>/repo/.git/refs/heads/feature/ledger` = SHA1; origin config at `<root>/repo/.git/config`; empty directory `<root>/repo/src/deep` | `<root>/repo/src/deep` | `RepositoryRoot = <root>/repo`, `GitDirectory = <root>/repo/.git`, `BranchName "feature/ledger"`, SHA1, `RemoteOriginUrl` = the origin URL, slug `swigerb/copilot-ledger`, `IsDetachedHead false` |
| G2 | HEAD = `ref: refs/heads/main`; no loose ref; `packed-refs` lines: `# pack-refs with: peeled fully-peeled sorted`, `fedcba9876543210fedcba9876543210fedcba98 refs/heads/develop`, `<SHA1> refs/heads/main`, `1111111111111111111111111111111111111111 refs/tags/v1.0.0`, `^2222222222222222222222222222222222222222` | repo root | `BranchName "main"`, SHA1 |
| G3 | HEAD = SHA1, or HEAD = SHA256 (theory) | repo root | `BranchName null`, that SHA, `IsDetachedHead true` |
| G4 | HEAD = `ref: refs/heads/main`; no refs and no `packed-refs` | repo root | `BranchName "main"`, `HeadCommitSha null`, `IsDetachedHead false` |
| G5 | Main repo at `<root>/main/.git` (HEAD = `ref: refs/heads/main`, origin config, `refs/heads/wt-branch` = SHA1). Worktree git dir `<root>/main/.git/worktrees/wt` with `HEAD` = `ref: refs/heads/wt-branch` and `commondir` = `../..`. File `<root>/wt/.git` = `gitdir: ` + the absolute path of `<root>/main/.git/worktrees/wt` | `<root>/wt` | `RepositoryRoot = <root>/wt`, `GitDirectory = <root>/main/.git/worktrees/wt`, `BranchName "wt-branch"`, SHA1, slug `swigerb/copilot-ledger` |
| G6 | Config lines: `# comment`, `; comment`, `[core]`, `repositoryformatversion = 0`, `[remote "upstream"]`, `url = https://github.com/contoso/upstream.git`, `[Remote "origin"]`, `URL = "https://github.com/swigerb/copilot-ledger.git" ; trailing comment`, `fetch = +refs/heads/*:refs/remotes/origin/*` (key lines indented with a tab) | repo root | `RemoteOriginUrl "https://github.com/swigerb/copilot-ledger.git"`, slug `swigerb/copilot-ledger` |
| G7 | Origin url `https://user:s3cr3t@github.com/swigerb/copilot-ledger.git` | repo root | `RemoteOriginUrl "https://github.com/swigerb/copilot-ledger.git"`, which doesn't contain `s3cr3t` |
| G8 | `<root>/plain/sub`, with no `.git` anywhere up to `<root>` | `<root>/plain/sub` | null |
| G9 | nothing | `<root>/does-not-exist` | null |
| G10 | Config contains only `[core]` | repo root | `RemoteOriginUrl null`, `RepositorySlug null`; branch and SHA still resolved |

Compare paths after `Path.GetFullPath`.

### Task 3 tests

#### Storage: `PicoUsdTests`

- These round-trip exactly through `FromUsd` and `ToUsd`: `0` ↔ `0`, `0.000000025` ↔ `25_000`, `2.86` ↔ `2_860_000_000_000`, and `1.238004` ↔ `1_238_004_000_000`.
- `FromUsd` throws `ArgumentOutOfRangeException` for `-0.01`, for `0.0000000000001`, and for `10_000_000`, which overflows a `long`.

#### Storage: `SqliteLedgerStoreTests`

Each test uses its own database at `<TempDirectory>/nested/ledger.db`.

Seed entries. Start times are `2026-11-20T12:00:00Z` except E5. Entries use the embedded calculator except E4, which uses `Parse(ByokTestRatesJson)`. Git contexts are built directly; `RepositoryRoot` can be any absolute path.

| Entry | Span | Git (slug / branch) | Agent | Expected calculation |
|---|---|---|---|---|
| E1 | `claude-opus-5.5`, V2 usage | `swigerb/copilot-ledger` / `main` | `ledger-architect` | GitHub, Priced, 2.86 USD, 286 credits |
| E2 | `mai-code-2`, V8 usage | `swigerb/copilot-ledger` / `feature/ledger` | `ledger-builder` | GitHub, Priced, 0.56 USD, 56 credits |
| E3 | `qwen3.8-27b-cuda-gpu:1` at `127.0.0.1:11434`; input 400,000; output 12,000 | `swigerb/copilot-ledger` / `feature/ledger` | `ledger-builder` | Local, saved 0.23 |
| E4 | `gpt-6.1-sol` at `contoso.openai.azure.com`; input 100,000; output 10,000 | `contoso/api` / `main` | null | FoundryByok, Priced, 0.315 USD |
| E5 | `gpt-unknown`; input 1,000; output 100; start `2026-11-21T09:00:00Z` | no Git context (null) | `ledger-builder` | GitHub, UnknownModel, 0 |
| E6 | Same span and calculation as E1 (same `TraceId` and `SpanId`) | same as E1 | same as E1 | skipped as a duplicate |

Behavior tests:

- **S1.** `InitializeAsync` creates the `nested` folder and the database. A separate `SqliteConnection` reads `PRAGMA journal_mode` = `wal` and `PRAGMA user_version` = 1. Calling `InitializeAsync` a second time succeeds.
- **S2.** After a separate connection sets `PRAGMA user_version = 99`, a new store's `InitializeAsync` throws `InvalidOperationException`.
- **S3.** `RecordAsync` before `InitializeAsync` throws `InvalidOperationException`.
- **S4.** Recording `[E1..E6]` returns 5. Recording the same six again returns 0.
- **S5.** Each of these batches throws `ArgumentException`, and afterward the Repository rollup is empty: `[E1, an invoke_agent entry]` and `[E1, an entry whose span has OutputTokens = -1]`.

**S6.** Repository rollup of the seed, in order:

| Repository | Spans | Unpriced | Input | Output | Cache read | Cache write | AiCredits | AiCreditsUsd | FoundryByokUsd | LocalSavedUsd |
|---|---|---|---|---|---|---|---|---|---|---|
| `swigerb/copilot-ledger` | 3 | 0 | 2,600,000 | 162,000 | 1,200,000 | 100,000 | 342 | 3.42 | 0 | 0.23 |
| `contoso/api` | 1 | 0 | 100,000 | 10,000 | 0 | 0 | 0 | 0 | 0.315 | 0 |
| `(none)` | 1 | 1 | 1,000 | 100 | 0 | 0 | 0 | 0 | 0 | 0 |

**S7.** Branch rollup, in order:

| Repository | Branch | Spans | AiCredits | AiCreditsUsd | FoundryByokUsd | LocalSavedUsd |
|---|---|---|---|---|---|---|
| `swigerb/copilot-ledger` | `main` | 1 | 286 | 2.86 | 0 | 0 |
| `swigerb/copilot-ledger` | `feature/ledger` | 2 | 56 | 0.56 | 0 | 0.23 |
| `contoso/api` | `main` | 1 | 0 | 0 | 0.315 | 0 |
| `(none)` | `(none)` | 1 | 0 | 0 | 0 | 0 |

**S8.** Model rollup, in order:

| Model | Spans | Unpriced | AiCredits | AiCreditsUsd | FoundryByokUsd | LocalSavedUsd |
|---|---|---|---|---|---|---|
| `claude-opus-5.5` | 1 | 0 | 286 | 2.86 | 0 | 0 |
| `mai-code-2` | 1 | 0 | 56 | 0.56 | 0 | 0 |
| `gpt-6.1-sol` | 1 | 0 | 0 | 0 | 0.315 | 0 |
| `qwen3.8-27b` | 1 | 0 | 0 | 0 | 0 | 0.23 |
| `gpt-unknown` | 1 | 1 | 0 | 0 | 0 | 0 |

**S9.** Agent rollup, in order:

| Agent | Spans | Unpriced | Input | Output | AiCredits | AiCreditsUsd | FoundryByokUsd | LocalSavedUsd |
|---|---|---|---|---|---|---|---|---|
| `ledger-architect` | 1 | 0 | 1,200,000 | 50,000 | 286 | 2.86 | 0 | 0 |
| `ledger-builder` | 3 | 1 | 1,401,000 | 112,100 | 56 | 0.56 | 0 | 0.23 |
| `(none)` | 1 | 0 | 100,000 | 10,000 | 0 | 0 | 0.315 | 0 |

**S10.** Filters on the seed:

| Grouping | Filter | Expected rows |
|---|---|---|
| Branch | `Repository = "swigerb/copilot-ledger"` | `main` (2.86), then `feature/ledger` (0.56) |
| Repository | `Branch = "main"` | `swigerb/copilot-ledger` (1 span, 2.86), then `contoso/api` (1 span, FoundryByokUsd 0.315) |
| Repository | `To = 2026-11-21T00:00:00Z` | `swigerb/copilot-ledger`, then `contoso/api` (no `(none)` row) |
| Repository | `From = 2026-11-21T00:00:00Z` | only `(none)`, with 1 span |

**S11.** Exactness. In a fresh database containing only a V5 entry, the Model rollup gives `AiCreditsUsd == 1.238004m` and `AiCredits == 123.8004m`. In another containing only a V12 entry, it gives `AiCreditsUsd == 0.000000025m` and `AiCredits == 0.0000025m`.

## Deferred to later sprints

- **Sprint 2 (OTLP receiver and CLI)**
  - Map Copilot `chat` spans to `NormalizedGenAiSpan`. Read `gen_ai.usage.cache_write.input_tokens`, falling back to `gen_ai.usage.cache_creation.input_tokens`.
  - When a `chat` span lacks `gen_ai.agent.name` or `gen_ai.conversation.id`, copy it from the nearest `invoke_agent` ancestor.
  - Normalize emitters whose `input_tokens` excludes cache tokens: if cache read + cache write > input, set `InputTokens = input + cache read + cache write` before pricing.
  - Find which attribute carries the working directory in real Copilot traces. The original plan's `github.copilot.workspace` / `cwd` isn't verified.
  - Record the real `server.address` values for Copilot-hosted, Foundry BYOK, and local spans, and update the `rates.json` host lists to match.
  - Wrap `FileSystemGitContextResolver` with caching and a `FileSystemWatcher` on `HEAD`.
  - Add `gh ledger rates` commands that scaffold `~/.copilot-ledger/rates.json` from `docs/configuration.md`'s examples and validate it, printing the same errors the loader would.
- **Reconciliation.** GitHub Docs say to read AI-unit consumption from `github.copilot.nano_aiu` on the root `invoke_agent` span only. Third-party write-ups say dividing it by 10^9 gives AI credits, but GitHub doesn't document that. Consider storing it per invocation and comparing it with computed credits.
- **Sprints 3 and 4.** The runaway-loop circuit breaker, the MCP server, PR receipts with the GitHub Action, and a `rates.json` update command, per the roadmap.

## Amendments during implementation

Recorded by `ledger-architect` on 2026-11-21, during Sprint 5 (audit finding A12); Sprint 1 was archived without this section. Line numbers refer to this file.

1. **PR #4 (Task 1), CI.** The architect added `.github/workflows/ci.yml`, which this plan didn't list. It runs `dotnet build CopilotLedger.slnx` and `dotnet test CopilotLedger.slnx` on `ubuntu-latest` and `windows-latest` for pushes to `main`, pull requests, and manual runs. Sprint 2's plan recorded it (`.github/plans/archive/sprint-2.md`, line 36).
2. **Found later: `Calculate` could throw.** The contract at L333 ("Throws only ArgumentNullException") and L599 ("Otherwise `Calculate` never throws") didn't hold. `JsonRateCardCalculator.Calculate` threw `OverflowException` for an extreme overlay rate, such as 10^22 USD per 1M tokens, because Task 2 bounded a rate only by requiring `rate × 1,000,000` to fit in a `decimal`. Sprint 2 restored the contract: its Task 3 (PR #15) rejects rates above 1,000,000 USD per 1M tokens, and its Task 2 ingestor (PR #14) also catches `OverflowException`.
