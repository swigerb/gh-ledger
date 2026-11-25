# gh-ledger: Copilot instructions

gh-ledger is a free, local-first .NET 10 utility. It attributes AI spend to repository, branch, model, and agent from OpenTelemetry `gen_ai.*` spans. Spend is tracked across three billing channels: GitHub AI Credits, Microsoft Foundry BYOK (USD), and local models such as Foundry Local and Ollama ($0.00 billed, with estimated cloud savings).

The `ledger-architect` agent plans work in `.github/plans/current-sprint.md`. The `ledger-builder` agent implements it one task at a time.

## Non-negotiable rules

### .NET 10 only
- Every project targets `net10.0`, inherited from `Directory.Build.props`. Don't set `TargetFramework` or `TargetFrameworks` in a `.csproj`, multi-target, or use preview frameworks.
- `global.json` pins the .NET 10 SDK. Don't change it.
- The solution is `CopilotLedger.slnx`. Don't add a `.sln`.

### Exact decimal money math
- Use `decimal` for every AI credit, USD amount, and token rate. 1 AI credit = $0.01 (`0.01m`). Token counts are `long`.
- Never use `float` or `double` for money: no double literals, no `Math` double overloads, no JSON-to-double conversion, no SQLite `REAL`. Parse and format decimals with `CultureInfo.InvariantCulture`.
- Rates are USD per 1,000,000 tokens with at most 6 decimal places. Cost is `tokens * ratePerMillion / 1_000_000m`, which is exact to 10^-12 USD.
- Don't round in Core or Storage. Round only for display, with an explicit `MidpointRounding`.
- Persist money in SQLite as `INTEGER` pico-USD (1 USD = 10^12 pico-USD). Aggregate with `SUM` over integer columns only, never over `TEXT` or `REAL`.
- In tests, write expected money as `decimal` (`TheoryData<..., decimal>` or `decimal.Parse(..., CultureInfo.InvariantCulture)`). `[InlineData(2.86)]` passes a `double`, so don't use it for money.

### Zero Docker, hermetic tests
- No Dockerfiles, compose files, container-based dev containers, Testcontainers, or anything else that needs a container runtime or external service.
- Tests use only temp directories and local SQLite files. No network access, no real `~/.copilot-ledger`, and no shelling out to `git`.

### Minimal diffs
- Change only what the current task requires. No drive-by refactors, renames, reformatting, or extra files.
- Public contracts in `.github/plans/current-sprint.md` are fixed. If one looks wrong, stop and report it for `ledger-architect` instead of redesigning it.
- Add NuGet packages only when the plan names them. Use the package sources already configured on the machine; don't add or edit package sources or `nuget.config`. If a restore or download is blocked by policy, stop and report it. Don't retry or work around the block.
- If NuGet audit warnings (`NU19xx`) fail the build, update the affected package to a patched version. Don't suppress the warning.
- Don't add assertion or mocking libraries unless the plan calls for them.

### Mandatory verification on every task
Run from the repository root:

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

- Both must exit 0. Warnings are errors (`TreatWarningsAsErrors`).
- Every task adds the tests its plan section lists. Behavior without tests isn't done.
- Never check off a task or claim success until both pass. Report the exact commands and the passed/failed/skipped counts.
- Never skip, delete, or weaken tests or analyzers to get a green build.

## Domain invariants
- `gen_ai.usage.input_tokens` includes cache-read and cache-write tokens, per the OTel GenAI semantic conventions, which Copilot follows. Uncached input = input - cache read - cache write.
- Copilot emits `gen_ai.usage.cache_write.input_tokens`; older semconv versions call it `gen_ai.usage.cache_creation.input_tokens`. Accept both when ingesting.
- Price only inference spans (`gen_ai.operation.name` = `chat`, `text_completion`, or `generate_content`). Copilot's `invoke_agent` spans carry totals across all turns, so never price them or add them to `chat` spans.
- Read Git context from `.git` files on disk. Never run `git`.
- Never persist prompt or response content. Strip credentials from Git remote URLs before storing them.

## Conventions
- C# 14 (the `net10.0` default; don't set `LangVersion`), file-scoped namespaces, nullable enabled, `sealed` records for data contracts, and `IReadOnlyList<T>` for exposed collections.
- Namespaces follow folders, for example `CopilotLedger.Core.Pricing`, `CopilotLedger.Core.Git`, `CopilotLedger.Core.Telemetry`, and `CopilotLedger.Storage`.
- Comment only non-obvious logic.
- Pricing configuration is the embedded `src/CopilotLedger.Core/Pricing/rates.json` plus the optional user overlay `~/.copilot-ledger/rates.json`. When either one's schema or behavior changes, update `docs/configuration.md` in the same task.
- Work lands on `dev`. `main` changes only through pull requests that pass CI; a release merges `dev` into `main` (see `docs/releasing.md`).
- Don't commit, push, rewrite history, or create branches unless the user asks.
