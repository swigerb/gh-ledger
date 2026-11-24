# Sprint 12: Billing & Import Fidelity

| | |
|---|---|
| Architect | `ledger-architect` |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET (`global.json`'s SDK), zero Docker |
| Plan written | 2026-11-24, against `main` at `69ae671` |
| Branch | `swigerb-sprint-12-billing-import-fidelity` |
| Requirements | Issues [#76](https://github.com/swigerb/gh-ledger/issues/76) (reported charge), [#82](https://github.com/swigerb/gh-ledger/issues/82) (BYOK and failed compactions), [#85](https://github.com/swigerb/gh-ledger/issues/85) (tool links by `apiCallId`), [#79](https://github.com/swigerb/gh-ledger/issues/79) (local BYOK by model), [#75](https://github.com/swigerb/gh-ledger/issues/75) (Copilot CLI 1.0.115) |
| Status | Current sprint. Sprint 11 is complete and archived verbatim at `.github/plans/archive/sprint-11.md`. A parallel Sprint 13 owns the audit, export tool-call queries, daemon start, and alert text (see "Integration boundaries"). |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification, runs the verification commands, changes the line to `- [x]`, and stops. Tasks are sequential; each builds on the ones before it, and each is one commit, `Sprint 12 Task N: <Title>`, with the repository's `Co-authored-by` trailer.

This plan is a **specification**, not copy-exact code. Each task gives the files, the public and internal contracts (names, types, and members are fixed), schemas, numbered algorithms, exact test vectors, acceptance criteria, and verification commands. Implement exactly those contracts. Where a task says "exactly", the value, string, name, or order is fixed. Where it leaves an implementation detail open (a private helper's name, a loop's shape), choose the simplest one that matches the surrounding code's style. Don't add features, options, logging, caching, or fast paths the task doesn't ask for, and don't refactor code outside the task's files.

After **every** task:

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

The build must report 0 warnings and 0 errors (`TreatWarningsAsErrors` is on in `Directory.Build.props`), and the test run must report 0 failed and 0 skipped. Record the passed count in the commit message body (`Tests: <n> passed`). Before Task 1, run the suite once on the unchanged tree and record the baseline (Sprint 11 ended at 1,740; docs-only commits since then add none).

Global rules for every task:

- **Money is `decimal` only.** No `double`, `float`, or `Math.Round` on money; no SQLite `REAL`. The one place a binary double appears is the IEEE-754 bit pattern of an OTLP `doubleValue`, which `NanoAiu.TryFromIeee754Bits` decodes with integer arithmetic only (no `BitConverter.Int64BitsToDouble`, no `double` local).
- **Hermetic tests.** Temporary directories, `ManualTimeProvider`, `FakeCliPlatform`, `CliHarness`, synthetic `CopilotSessionLogs` fixtures. No test opens a socket, starts a process, runs `git`, uses Docker or the network, or reads `~/.copilot`, `~/.codex`, or `~/.copilot-ledger`.
- **Privacy.** Nothing new is stored except the columns this plan lists. No prompt, response, reasoning, tool argument, tool result, or summary text is read; the new session-log paths are only those this plan allowlists.
- **Expected money in tests is written as `decimal` literals** (`0.0473886m`), never computed from the code under test unless the task says "equals the rate-card result of the same span".
- **No version bump.** `<Version>` stays `0.5.0`, and `src/CopilotLedger.Core/Pricing/rates.json` is unchanged (no rate changed; see "Verified facts").
- **Stay inside the boundaries** in "Integration boundaries". If a task can't be done without touching a Sprint 13 file other than as this plan allows, stop and report it.
- If a contract here doesn't compile against the code, a referenced line has moved, or a test vector disagrees with the code's arithmetic, **stop and report it**. Don't redesign.

## Tasks

- [x] Task 1: Reported-Charge Primitives (CopilotLedger.Core & tests). `ReportedChargeState`, `ReportedCharge`, and `NanoAiu`: exact validation of a nano-AIU count from a JSON number or an IEEE-754 double's bits, and its exact conversion to USD, AI credits, and pico-USD.
- [x] Task 2: The Calculator Bills the Reported Charge, Keeps the List Price, and Prices Local BYOK by Model (CopilotLedger.Core, docs & tests). `NormalizedGenAiSpan.ReportedCharge`; `CreditCalculationResult.ListUsdCost` and `ChargeSource`; the reported charge replaces the rate-card charge for priced GitHub AI-credit requests, and a malformed or inapplicable one is rejected; issue #79's `localOffload.models` rule for provider-billed requests.
- [x] Task 3: OTLP Reads `github.copilot.nano_aiu` (CopilotLedger.Otlp & tests). An exact integer from `doubleValue` in JSON and protobuf, and the normalizer's reported charge on inference spans only.
- [ ] Task 4: Ledger Format 7 (CopilotLedger.Storage, docs & tests). `spans.list_cost_usd_pico`, `spans.charge_source`, `import_sessions.import_revision`, and a partial index on imported tool links; the store writes and validates the new columns, and `docs/cli.md`'s import allowlist lists them.
- [ ] Task 5: Live Spans Record the Charged Amount (CopilotLedger.Otlp, CopilotLedger.Storage, docs & tests). End to end from OTLP to the ledger; `IngestResult.RejectedReportedCharges`; budgets, the breaker, and receipts use the charged amount.
- [ ] Task 6: Import Reads `totalNanoAiu`, and the Report Gains Its Sprint 12 Counts (CopilotLedger.Core, CopilotLedger.Storage, CopilotLedger.Cli, docs & tests). Usage records and compactions bill their reported charge; `ReportedChargeRejected` diagnostics; six new report counts, table rows, and JSON fields.
- [ ] Task 7: Import BYOK and Failed Compactions (CopilotLedger.Core, docs & tests). Issue #82's seven classification rules and `compactionAttemptId` as a BYOK compaction's record key.
- [ ] Task 8: Link Imported Tool Calls by `apiCallId` (CopilotLedger.Core, CopilotLedger.Storage, docs & tests). `assistant.message` IDs only, parent-chain resolution at the end of the log, the fallback count, and canaries.
- [ ] Task 9: Import Copilot CLI 1.0.115 Sessions (CopilotLedger.Core, docs & tests). The gate's maximum becomes 1.0.115, with 1.0.115's missing-field rules; 1.0.116 still fails closed.
- [ ] Task 10: Re-Import Repairs Billing in Place (CopilotLedger.Storage, docs & tests). Import revision 1 re-reads every log once; imported rows are repriced, and live GitHub rows take the log's reported charge, in the real run and the dry run alike.
- [ ] Task 11: Re-Import Repairs Tool Links in Place (CopilotLedger.Storage, docs & tests). Imported links move to the request that asked for them, or are removed when that request has no imported row, in the real run and the dry run alike.
- [ ] Task 12: Upgrade Guidance and Reference Consistency (docs & tests). Format 7 in the upgrade steps, "run `gh ledger import` once after upgrading", and the reference's billing and repair notes.
- [ ] Task 13: Final Verification (no product change). Full build and test gates, money and privacy sweeps, boundary and version checks.

## Starting point

- `main` at `69ae671` ("Correct and extend the live end-to-end test report (#86)"), after Sprint 11's PR #73 (`ba399c3`) and two documentation commits. Product version `0.5.0` (`src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`). Ledger format 6 (`LedgerMigrations.cs:166–210`). Copilot import window 1.0.103–1.0.114 (`CopilotSessionLogSource.cs:11–12`).
- What this plan depends on, read from the code at `69ae671`:
  - **Calculator.** `JsonRateCardCalculator.Calculate` (`JsonRateCardCalculator.cs:122–300`): the provider-billed guard returns `OtherProvider` before local models are consulted (`:152–158`); `localByKnownModel` requires `BillingSource.Endpoint` (`:160–163`). `CreditCalculationResult.UsdCost` is the billed USD and `AiCredits = UsdCost / 0.01m` for GitHub AI credits.
  - **OTLP.** `OtlpAnyValue` keeps no double (`OtlpModel.cs`); the JSON reader turns `doubleValue` into `Kind = Double` with no value (`OtlpJsonTraceReader.cs:157–160`); the protobuf reader discards field 4's fixed64 (`OtlpProtobufTraceReader.cs:239–243`). The normalizer builds the inference span around `GenAiSpanNormalizer.cs:190–215`.
  - **Storage.** `cost_usd_pico` is what budgets, the breaker, receipts, export, and audit sum. `InsertSql` (`SqliteLedgerStore.cs:10–36`) and `AddInsertParameters` write a span. Import resolution is `SqliteLedgerStore.Imports.cs` with `UsageAuthority.cs`; the dry run is `LedgerSnapshot.cs`, which must decide exactly as the store does.
  - **Import.** `CopilotSessionLogSource` reads allowlisted paths only (`PathTree`, `JsonFields`); `JsonFields.Integer` keeps only an `Int64` (`JsonFields.cs:98, 177`). Tool calls are owned by "the latest usage record of the tool's agent" (`ToolStart`, `:556–573`). A compaction must be successful and carry a service request ID (`:477–540`). `LedgerImporter` skips a complete, unchanged log (`LedgerImporter.cs:143–153`) and writes the checkpoint after the last batch (`:282–305`).
  - **Fixtures.** `tests/CopilotLedger.Tests/TestSupport/CopilotSessionLogs.cs` writes 1.0.114 shapes. Its `Compaction` already writes `compactionTokensUsed.copilotUsage.totalNanoAiu = 11_855_545_000`, and its `Event` gives every line an `id` and a `parentId` equal to the previous line's `id`.

## Verified facts (retrieved 2026-11-24)

| # | Fact | Source |
|---|---|---|
| F1 | On a `chat` span, `github.copilot.nano_aiu` is the AI units this turn consumed; 1 AIU = 10⁹ nano AIU. On `invoke_agent` it's the session total, and GitHub says to read the total from the root `invoke_agent` only because summing every span double-counts. gh-ledger sums `chat` spans only and never prices `invoke_agent` (a non-billable operation), so it counts each turn once. | [OpenTelemetry reference for Copilot](https://docs.github.com/en/copilot/reference/enterprise-administrators/opentelemetry-reference) |
| F2 | `github.copilot.cost` is a multiplier, not money; it isn't read. `github.copilot.request.type = compaction` marks compaction spans (already chat spans). | Same page |
| F3 | 1 AI credit = $0.01. | [Models and pricing for GitHub Copilot](https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing) |
| F4 | Auto model selection bills 10% less than the selected model's rate. | [Changelog 2026-07-01](https://github.blog/changelog/2026-07-01-copilot-cli-auto-model-selection-routes-based-on-task/) |
| F5 | Copilot Fusion bills 30% less "for a limited time". | [Changelog 2026-10-28](https://github.blog/changelog/2026-10-28-route-and-orchestrate-models-with-github-copilot-fusion/) |
| F6 | OTLP `AnyValue`: `int_value = 3` (int64, varint), `double_value = 4` (IEEE-754 double, wire type 1 fixed64, little-endian). Proto3's JSON mapping allows a double as a number or a string (`"NaN"`, `"Infinity"`, `"1.5"`). | [opentelemetry-proto `common.proto`](https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/common/v1/common.proto); [Proto3 JSON mapping](https://protobuf.dev/programming-guides/json/) |
| F7 | `Utf8JsonReader.TryGetDecimal` (probed with the repository's SDK): `47388600`, `47388600.0`, `4.73886E7`, and `4.73886e+7` → 47388600; `1e21` → true; `9007199254740993` → true (so a range check is needed); `-0` → 0; `0.5` → 0.5; `1E-400` → **true with value 0** (underflow); `1e400` → false; `79228162514264337593543950336` → false. | Local probe, 2026-11-24 |
| F8 | Copilot CLI 1.0.115 has been the latest release since 2026-11-24T13:03:50Z; 1.0.116-0 is a prerelease (2026-11-24T16:43Z). 1.0.115's notes list no session-log schema change. `ProducerVersion` parses `1.0.116-0` as 1.0.116 (fails closed) and `1.0.115-8` as 1.0.115. | `gh release list --repo github/copilot-cli`; release notes of v1.0.115 |
| F9 | GitHub's published rates are unchanged since 2026-11-20, so `rates.json` doesn't change. | F3's page, compared with `rates.json`'s `ratesVersion` |

Local session logs (304 logs on the architect's machine) were inspected **for key names, JSON types, enum values, and counts only**, with scripts that printed nothing else:

| # | Observation |
|---|---|
| L1 | In 1.0.103–1.0.114 usage records, `data.usage.apiCallId` equals `data.usage.accounting.usageId` in all 69,286 records that have both; one record has no `apiCallId`. No `apiCallId` repeats within a log. |
| L2 | From `tool.execution_start`, following `parentId` reaches an `assistant.message` directly in 66,474 cases and through earlier `tool.execution_start`/`tool.execution_complete` events in 30,369; about 100 pass through `hook.*`, `assistant.turn_*`, `subagent.started`, or `user.message`. 96,302 reach a message whose `data.apiCallId` matches a usage record; 631 reach a message without `apiCallId`. |
| L3 | 1.0.115 (one session): `session.usage_record` omits `data.usage.isByok`, `cacheDetailsReported`, `numToolCalls`, `isAuto`, and `apiCallId`, and adds `data.apiCallIdFromUsageId: true`. It keeps `accounting` (`usageId`, `sequence`, `sourceSessionId`), `provider` (`kind` = `copilot`), `serviceRequestId`, `copilotUsage.totalNanoAiu` (integer), and the token fields. Its `assistant.message` has `data.apiCallId`. |
| L4 | In the 1.0.103–1.0.115 window, every compaction with `compactionTokensUsed` has `requestCount` 1, `model`, the four token counts, and `duration`. Shapes: successful with `serviceRequestId`, `copilotUsage`, and `provider` (1,665); successful with none of the three (BYOK, 104); failed with all three (2); failed with none (11). `compactionAttemptId` and `clientRequestId` are always present, distinct, and unique across all logs. |

### Assumptions (not verifiable from a primary source)

- **A1.** A session log's `copilotUsage.totalNanoAiu` is the same charge, in the same unit, as the live span's `github.copilot.nano_aiu` for that request, and 1 AIU = 1 AI credit. Evidence: issue #76's measurements (Auto and Fusion requests whose reported AIU equal 0.9× and 0.7× the rate card), and Sprint 11's live check that rate-card pricing matched Copilot's own display. GitHub's pages name the unit "AI units" and price "AI credits" without stating the equality.
- **A2.** 1.0.115's usage shape (L3) is from one session. BYOK usage records, compactions, and `tool.execution_complete` written by 1.0.115 weren't observed; Task 9's rules fail closed for any 1.0.115 shape they don't cover.
- **A3.** Issue #76 lists "a string" as malformed. Proto3 JSON allows a double as a string (F6); this plan follows #76 and rejects any string `doubleValue` for `nano_aiu`, conservatively. An `intValue` string such as `"47388600"` is the int64 JSON mapping and is accepted (the JSON reader already reads it as `Int`).

## Coordinator decisions (fixed)

- **D1, repair in place.** Re-running `gh ledger import` updates each existing imported row's billed charge (and every other pricing column), its tool links, and logs that #82 left incomplete. Import revision 1 (`LedgerImporter.CurrentImportRevision`) makes every log that an earlier gh-ledger imported be read once more after the upgrade, even if unchanged (Task 10).
- **D2, live spans recorded before #76.** A live row can be corrected from its session log when, and only when, the row has a canonical usage ID (recorded by a format-6 daemon with a service request ID), is `github_ai_credits`/`priced`, isn't already `reported`, and its log's record carries a valid `totalNanoAiu`. Import then sets its `cost_usd_pico` to the reported charge and `charge_source` to `reported`, and keeps its list price. A live row without a canonical ID, whose log is gone or unsupported, or whose record has no valid charge keeps its rate-card charge. This is documented in `docs/cli.md` (Task 12).
- **D3, list price vs billed spend.** `spans.list_cost_usd_pico` keeps the rate-card price; `spans.cost_usd_pico` remains the charged amount, which budgets, the circuit breaker, receipts, export, and audit already sum. A reported charge that's malformed, or that can't apply to the request, falls back to the rate card, sets `charge_source = 'rejected'`, and is counted (`IngestResult.RejectedReportedCharges`; the import report's `reportedChargesRejected` and the `reportedChargeRejected` diagnostic).
- **D4, #85 privacy.** From `assistant.message`, import reads only the envelope `id` and `parentId` and `data.apiCallId`; from `tool.execution_start` and `tool.execution_complete`, it adds only the envelope `id` and `parentId`. It never reads `content`, `reasoningText`, `reasoningOpaque`, `encryptedContent`, `toolRequests`, `reasoningBlocks`, or tool arguments or results. Canary tests cover the ledger, the report, and the diagnostics, and `docs/cli.md`'s allowlist and "Never read" list change in the same task.
- **D5, schema.** This sprint owns ledger format 7 and its upgrade guidance. Sprint 13 adds no schema change.
- **D6, version.** Product version stays `0.5.0`; `rates.json` doesn't change.

## Integration boundaries (Sprint 13)

Sprint 13 runs in parallel and owns these. **Don't edit them**:

- `src/CopilotLedger.Core/Audit/**`.
- The tool-call queries in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs` and `src/CopilotLedger.Storage/SqliteLedgerStore.Audit.cs`.
- `WindowsDetachedProcess.cs` and daemon start (`DaemonCommands`, `DaemonHost` start path).
- Breaker and budget alert text.
- The audit, export, daemon, and install sections of the docs, **except** the one sentence of `docs/cli.md`'s "**Ledger format.**" paragraph that names the format number, which Task 12 changes from 6 to 7 (D5 makes it ours; it's a one-word change plus one added sentence).

Shared semantics, without edits: Sprint 13's audit and export read `cost_usd_pico` as billed spend, which is exactly what D3 keeps, and `span_links.owner_span_row_id`, whose meaning Task 11 keeps (an imported link's owner is the imported row of the request that asked for the tool). No query in a Sprint 13 file needs to change for this sprint.

---

## Task 1 specification: Reported-Charge Primitives

**Files**

- New `src/CopilotLedger.Core/Pricing/ReportedCharge.cs`
- New `src/CopilotLedger.Core/Pricing/NanoAiu.cs`
- New `tests/CopilotLedger.Tests/Pricing/NanoAiuTests.cs`

**Contracts**

```csharp
namespace CopilotLedger.Core.Pricing;

// What a request's own billing report said it was charged, in nano AI units (1 AIU = 1 AI credit = 10^9 nano AIU).
public enum ReportedChargeState
{
    Absent = 0,      // no reported charge
    Valid,           // NanoAiu holds an exact, non-negative integer no greater than NanoAiu.MaxExact
    Malformed,       // present but unusable: negative, fractional, NaN, infinite, above 2^53, or not a number
}

public readonly record struct ReportedCharge(ReportedChargeState State, long NanoAiu)
{
    public static ReportedCharge Absent => default;
    public static ReportedCharge Malformed => new(ReportedChargeState.Malformed, 0);
    public static ReportedCharge Valid(long nanoAiu);   // ArgumentOutOfRangeException unless 0 <= nanoAiu <= NanoAiu.MaxExact
}

public static class NanoAiu
{
    public const long MaxExact = 9_007_199_254_740_992;            // 2^53
    public static bool TryFromJsonNumber(ReadOnlySpan<byte> utf8Json, out long nanoAiu);
    public static bool TryFromIeee754Bits(ulong bits, out long nanoAiu);
    public static decimal ToUsd(long nanoAiu);        // nanoAiu * 0.00000000001m  (1 nano AIU = $10^-11)
    public static decimal ToAiCredits(long nanoAiu);  // nanoAiu / 1_000_000_000m
    public static long ToPicoUsd(long nanoAiu);       // checked(nanoAiu * 10)
}
```

`ToUsd`, `ToAiCredits`, and `ToPicoUsd` throw `ArgumentOutOfRangeException` for a value outside `0..MaxExact`.

**Algorithm, `TryFromJsonNumber(utf8Json)`** (one JSON value, such as a property's raw text):

1. Create a `Utf8JsonReader` over `utf8Json`. If `Read()` is false or `TokenType != JsonTokenType.Number`, return false. (Strings, `true`, `null`, objects, and arrays are false.)
2. If `!reader.TryGetDecimal(out decimal value)`, return false (F7: `1e400`, values above `decimal.MaxValue`).
3. If `value < 0` return false. If `value != decimal.Truncate(value)` return false. If `value > MaxExact` return false.
4. If `value == 0`: the token's mantissa (the bytes of `reader.ValueSpan` before the first `e` or `E`) must contain no digit other than `0`; otherwise return false. (F7: `1E-400` reads as 0.)
5. `nanoAiu = (long)value`; return true.

**Algorithm, `TryFromIeee754Bits(bits)`** (integer arithmetic only):

1. `exponent = (int)((bits >> 52) & 0x7FF)`, `mantissa = bits & 0x000F_FFFF_FFFF_FFFF`, `negative = (bits >> 63) != 0`.
2. If `exponent == 0x7FF`: return false (NaN or infinity).
3. If `exponent == 0 && mantissa == 0`: `nanoAiu = 0`, return true (+0 and −0).
4. If `exponent == 0`: return false (subnormal: never an integer ≥ 1).
5. If `negative`: return false.
6. `significand = mantissa | (1UL << 52)`, `shift = exponent - 1075`.
7. If `shift >= 0`: if `shift > 1` return false; `value = significand << shift`; return `value <= MaxExact` (with `nanoAiu = (long)value`). (`shift` 0 or 1 covers every integer up to 2^54; anything larger exceeds 2^53.)
8. If `shift <= -53`: return false (a positive value below 1).
9. `low = significand & ((1UL << -shift) - 1)`; if `low != 0` return false (fractional). `nanoAiu = (long)(significand >> -shift)`; return true.

**Tests** (`NanoAiuTests`, xUnit `[Theory]` rows count as one test each):

| Test | Input | Expected |
|---|---|---|
| NA1 `TryFromJsonNumber` accepts | `47388600`, `47388600.0`, `4.73886E7`, `4.73886e+7`, `0`, `-0`, `0.0e5`, `9007199254740992` | true; 47388600, 47388600, 47388600, 47388600, 0, 0, 0, 9007199254740992 |
| NA2 `TryFromJsonNumber` rejects | `9007199254740993`, `1e21`, `0.5`, `-1`, `1E-400`, `1e400`, `79228162514264337593543950336`, `"47388600"`, `true`, `null`, `{}`, `[]` | false |
| NA3 `TryFromIeee754Bits` accepts | `0x418698BDC0000000` (47388600.0), `0x4340000000000000` (2^53), `0x3FF0000000000000` (1.0), `0x41CDCD6500000000` (1e9), `0x0000000000000000` (+0), `0x8000000000000000` (−0) | true; 47388600, 9007199254740992, 1, 1000000000, 0, 0 |
| NA4 `TryFromIeee754Bits` rejects | `0x4340000000000001` (2^53+2), `0x3FE0000000000000` (0.5), `0x7FF8000000000000` (NaN), `0x7FF0000000000000` (+∞), `0xFFF0000000000000` (−∞), `0xBFF0000000000000` (−1.0), `0x0000000000000001` (subnormal), `0x4440000000000000` (2^69) | false |
| NA5 conversions | 47388600 | `ToUsd` = `0.000473886m`; `ToAiCredits` = `0.0473886m`; `ToPicoUsd` = `473886000` |
| NA6 conversions at the bounds | 0; 9007199254740992 | 0m, 0m, 0; `90071.99254740992m`, `9007199.254740992m`, `90071992547409920` |
| NA7 out of range | −1; 9007199254740993 | each of `ToUsd`, `ToAiCredits`, `ToPicoUsd`, and `ReportedCharge.Valid` throws `ArgumentOutOfRangeException` |
| NA8 defaults | `default(ReportedCharge)` | `State == Absent`, `NanoAiu == 0`; `ReportedCharge.Absent == default` |
| NA9 no binary floating point | the text of `NanoAiu.cs` and `ReportedCharge.cs` | contains none of `double`, `float`, `Double`, `Single`, `BitConverter` (read the files from the repository root, as `ExportPrivacyTests.EP6` does) |

**Acceptance:** the nine tests pass; no other file changes.

---

## Task 2 specification: The Calculator Bills the Reported Charge, Keeps the List Price, and Prices Local BYOK by Model

**Files**

- `src/CopilotLedger.Core/Telemetry/NormalizedGenAiSpan.cs`
- `src/CopilotLedger.Core/Pricing/CreditCalculationResult.cs`
- New `src/CopilotLedger.Core/Pricing/ChargeSource.cs`
- `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`
- `tests/CopilotLedger.Tests/Pricing/BillingSourceTests.cs` (BI8's `openai` row)
- New `tests/CopilotLedger.Tests/Pricing/ReportedChargePricingTests.cs`
- `docs/configuration.md` ("How usage is priced", "Imported requests", "Local models and savings")

**Contracts**

```csharp
// NormalizedGenAiSpan gains:
public ReportedCharge ReportedCharge { get; init; }     // github.copilot.nano_aiu, or an imported totalNanoAiu; Absent by default

// New file ChargeSource.cs
namespace CopilotLedger.Core.Pricing;
public enum ChargeSource
{
    RateCard = 0,   // UsdCost is the rate card's price
    Reported,       // UsdCost is the request's valid reported charge
    Rejected,       // a reported charge was present but malformed or inapplicable; UsdCost is the rate card's price
}

// CreditCalculationResult gains:
public decimal ListUsdCost { get; init; }               // the rate card's billed USD for this request, whatever was charged
public ChargeSource ChargeSource { get; init; }         // where UsdCost came from
```

The existing comment on `UsdCost` becomes `// charged USD: the reported charge when ChargeSource is Reported, else the rate card's`. `AiCredits` keeps its definition (`UsdCost / 0.01m` for GitHub AI credits).

**Algorithm, `Calculate(span)`:**

1. Rename today's body to `private CreditCalculationResult CalculateAtRateCard(NormalizedGenAiSpan span)` and make every `Result(...)` it returns set `ListUsdCost = UsdCost` and `ChargeSource = ChargeSource.RateCard`.
2. Apply issue #79 inside `CalculateAtRateCard`:
   1. Compute `localModel` (today's `_localModels.FirstOrDefault(item => item.Matches(model))`, null when `model` is null) **before** the provider-billed guard.
   2. The guard returns `OtherProvider` only when `span.BillingSource == BillingSource.Provider && !MatchesValue(span.ProviderName, _localProviders) && !MatchesValue(span.ProviderName, _foundryProviders) && localModel is null`.
   3. The local condition gains a fifth alternative: `span.BillingSource == BillingSource.Provider && localModel is not null && !MatchesValue(span.ProviderName, _foundryProviders)`. (A provider listed under `foundryByok.providerNames` stays Foundry BYOK even when its model is a local model.)
3. `Calculate(span)`: `rateCard = CalculateAtRateCard(span)`, then by `span.ReportedCharge.State`:
   1. `Absent` → return `rateCard`.
   2. `Malformed` → return `rateCard with { ChargeSource = ChargeSource.Rejected }`, unless `rateCard.Status == PricingStatus.NotBillableOperation`, which returns `rateCard` unchanged (non-inference spans never count a charge).
   3. `Valid(n)` and `rateCard.Channel == BillingChannel.GitHubAiCredits && rateCard.Status == PricingStatus.Priced` → return `rateCard with { UsdCost = NanoAiu.ToUsd(n), AiCredits = NanoAiu.ToAiCredits(n), ChargeSource = ChargeSource.Reported }`. `ListUsdCost`, `Breakdown`, `TierName`, `EstimatedCloudSavedUsd`, and `ApiEquivalent` stay the rate card's.
   4. `Valid(0)` on any other channel or status → `rateCard` unchanged (a zero charge doesn't contradict a request GitHub didn't bill, such as BYOK or local).
   5. `Valid(n > 0)` on any other channel or status, except `NotBillableOperation` → `rateCard with { ChargeSource = ChargeSource.Rejected }`. (`NotBillableOperation` → `rateCard` unchanged.) Using a charge for `unknown_model` or `no_rate_for_date` requests is deferred.

**Tests** (`ReportedChargePricingTests`, using the embedded rates and a fixed `StartTime` of `2026-11-20T00:00:00Z`; the "chat span" is `OperationName = chat`, no endpoint, `RequestModel = ResponseModel = "claude-haiku-4.5"`, `InputTokens = 1000`, `OutputTokens = 100`, `BillingSource.Endpoint`):

| Test | Span | Expected |
|---|---|---|
| RCP1 | chat span, `ReportedCharge.Valid(47388600)` | `Channel` GitHubAiCredits, `Status` Priced, `UsdCost == 0.000473886m`, `AiCredits == 0.0473886m`, `ChargeSource == Reported`, `ListUsdCost` equals `UsdCost` of the same span with `ReportedCharge.Absent`, and `Breakdown` equals that span's `Breakdown` |
| RCP2 | the same span without a charge | `ChargeSource == RateCard`, `ListUsdCost == UsdCost` |
| RCP3 | `Malformed` | the rate-card result with `ChargeSource == Rejected` |
| RCP4 | `OperationName = invoke_agent`, `Valid(47388600)` | `Status == NotBillableOperation`, `UsdCost == 0m`, `ChargeSource == RateCard` |
| RCP5 | `ServerAddress = "localhost"` (local), `Valid(0)` then `Valid(5)` | `ChargeSource == RateCard`, then `Rejected`; `UsdCost == 0m` both times |
| RCP6 | an unknown model `zz-unknown`, `Valid(47388600)` | `Status == UnknownModel`, `ChargeSource == Rejected`, `UsdCost == 0m` |
| RCP7 | `Valid(9007199254740992)` | `UsdCost == 90071.99254740992m`, `AiCredits == 9007199.254740992m` |
| RCP8 (#79) | `BillingSource.Provider`, `ProviderName = "openai"`, `RequestModel = "qwen3.8-27b-cuda-gpu:1"` | `Channel == Local`, `Status == LocalOffload`, `ModelId == "qwen3.8-27b"`, `EstimatedCloudSavedUsd > 0m`, `UsdCost == 0m` |
| RCP9 (#79) | the same with `RequestModel = "gpt-oss-unlisted"` | `Status == OtherProvider` |
| RCP10 (#79) | an overlay calculator whose `foundryByok.providerNames` lists `"contoso"`, `ProviderName = "contoso"`, local model `qwen3.8-27b` | `Channel == FoundryByok` (not Local) |

`BillingSourceTests.BI8`: the `openai` row (a provider-billed `openai` request whose model is a local model) now expects `(BillingChannel.Local, PricingStatus.LocalOffload)`; the Foundry row is unchanged. If BI8's `openai` row uses a model that isn't a local model, leave the row and add RCP8 only; report which applied.

**Docs (`docs/configuration.md`)**

- "How usage is priced", GitHub AI Credits row, "What's recorded": `AI credits and USD: the charge Copilot reports for the request (github.copilot.nano_aiu, or totalNanoAiu in a session log) when it's valid, which includes discounts such as Auto's 10% and Copilot Fusion's; otherwise GitHub's published prices (1 AI credit = $0.01)`.
- After that table, add a paragraph: "**Charged amount and list price.** gh-ledger records both. The charged amount is what budgets, the circuit breaker, receipts, reports, export, and audit use. The list price is the rate card's price for the same tokens. A reported charge that isn't a whole number of nano AI units from 0 to 2^53 (negative, fractional, NaN, infinite, too large, or text), or that's reported for a request GitHub didn't bill at a known price, isn't trusted: the request is charged at the rate card and counted as a rejected charge."
- "Imported requests": replace the BYOK bullet and the closing paragraph about `openai` with: "A Copilot BYOK request is local when its `providerType` is in `localOffload.providerNames`, or when its model is one of `localOffload.models` and its `providerType` isn't in `foundryByok.providerNames`; Foundry BYOK when its `providerType` is in `foundryByok.providerNames`; and `other_provider` otherwise. So a BYOK request through a local server that speaks OpenAI's API, which Copilot logs with provider type `openai`, is local when its model is listed in `localOffload.models`, such as Qwen3.8-27B's `qwen3.8-27b-cuda-gpu:1`, and `other_provider` when it isn't." Keep the Codex bullet; append "with the same model rule" to it.
- "Local models and savings": add one sentence after the first paragraph: "A request that its provider billed, such as an imported BYOK request, is local too when its model is one of `localOffload.models`."

**Acceptance:** RCP1–RCP10 pass; BI8 updated as above; every existing pricing test passes unchanged otherwise; the docs say what the code does.

---

## Task 3 specification: OTLP Reads `github.copilot.nano_aiu`

**Files**

- `src/CopilotLedger.Otlp/Decoding/OtlpModel.cs`
- `src/CopilotLedger.Otlp/Decoding/OtlpJsonTraceReader.cs`
- `src/CopilotLedger.Otlp/Decoding/OtlpProtobufTraceReader.cs`
- `src/CopilotLedger.Otlp/Normalization/GenAiAttributeNames.cs`
- `src/CopilotLedger.Otlp/Normalization/GenAiSpanNormalizer.cs`
- New `tests/CopilotLedger.Tests/Otlp/NanoAiuAttributeTests.cs`

**Contracts**

```csharp
// OtlpValueKind.Double's comment becomes: // recognized; only an exact non-negative integer value is kept (DoubleAsNanoAiu)
// OtlpAnyValue gains:
public long? DoubleAsNanoAiu { get; init; }   // set when Kind is Double and the value is an integer from 0 to 2^53 (NanoAiu)

// GenAiAttributeNames gains:
public const string NanoAiu = "github.copilot.nano_aiu";
```

**Algorithm**

1. JSON (`doubleValue`): when `value.ValueKind == JsonValueKind.Number` and `NanoAiu.TryFromJsonNumber(Encoding.UTF8.GetBytes(value.GetRawText()), out long n)`, return `{ Kind = Double, DoubleAsNanoAiu = n }`; otherwise `{ Kind = Double }` (a string, `null`, or an invalid number keeps `DoubleAsNanoAiu` null).
2. Protobuf (field 4, wire type 1): `ulong bits = reader.ReadFixed64()` (adapt to its existing return type without a `double`); when `NanoAiu.TryFromIeee754Bits(bits, out long n)`, `{ Kind = Double, DoubleAsNanoAiu = n }`; otherwise `{ Kind = Double }`.
3. Normalizer: only where it builds an **inference** span, set `ReportedCharge` from the attribute `github.copilot.nano_aiu` (`FindAttribute`):
   1. Absent attribute → `ReportedCharge.Absent`.
   2. `Kind == Int` and `0 <= IntValue <= NanoAiu.MaxExact` → `ReportedCharge.Valid(IntValue)`.
   3. `Kind == Double` and `DoubleAsNanoAiu is long n` → `ReportedCharge.Valid(n)`.
   4. Anything else present (a negative or too-large `Int`, an invalid `Double`, `String`, `Bool`, `Bytes`, `Array`, `KeyValueList`, or `Empty`) → `ReportedCharge.Malformed`.
   Non-inference spans (such as `invoke_agent` and `execute_tool`) never read the attribute.

**Tests** (`NanoAiuAttributeTests`; build OTLP JSON and protobuf payloads with the existing decoding tests' helpers):

| Test | Input | Expected |
|---|---|---|
| NO1 | JSON chat span, `nano_aiu` as `{"doubleValue": 47388600}` | normalized `ReportedCharge == Valid(47388600)` |
| NO2 | JSON chat span, `{"intValue": "47388600"}` and `{"intValue": 47388600}` | `Valid(47388600)` both |
| NO3 | JSON chat span, `doubleValue` of `0.5`, `-1`, `9007199254740993`, `"47388600"`, `"NaN"`, `"Infinity"` | `Malformed` each |
| NO4 | JSON chat span, `{"stringValue": "47388600"}`, `{"boolValue": true}` | `Malformed` |
| NO5 | protobuf chat span, field 4 bits `0x418698BDC0000000` | `Valid(47388600)` |
| NO6 | protobuf chat span, bits NaN `0x7FF8000000000000`, 0.5 `0x3FE0000000000000`, −1 `0xBFF0000000000000`, 2^53+2 `0x4340000000000001` | `Malformed` each |
| NO7 | protobuf chat span, `int_value` −5 | `Malformed` |
| NO8 | `invoke_agent` span with `doubleValue` 47388600 | its normalized span (if the normalizer returns one) has `ReportedCharge.Absent`, and the ingest test in Task 5 shows it's never priced |
| NO9 | chat span without the attribute | `Absent` |
| NO10 | `OtlpAnyValue` of a JSON `doubleValue` 1.5 | `Kind == Double`, `DoubleAsNanoAiu == null` |

**Acceptance:** NO1–NO10 pass; every existing OTLP decoding and normalizer test passes unchanged.

---

## Task 4 specification: Ledger Format 7

**Files**

- `src/CopilotLedger.Storage/LedgerMigrations.cs`
- `src/CopilotLedger.Storage/SqliteLedgerStore.cs` (`InsertSql`, `AddInsertParameters`, `ValidateEntry`, and a new `ChargeSourceName` mapping beside the existing channel and status mappings)
- `src/CopilotLedger.Storage/ImportSessionCheckpoint.cs`, `src/CopilotLedger.Storage/UsageAuthority.cs` (checkpoint columns only)
- `src/CopilotLedger.Storage/LedgerSnapshot.cs` (checkpoint read on format < 7)
- New `tests/CopilotLedger.Tests/Storage/SchemaVersion7Tests.cs`
- Tests that pin `user_version` 6 or "the supported version 6": `SchemaVersion6Tests`, `ApiEquivalentStoreTests`, `SpanLinkStoreTests`, `LedgerSnapshotTests`, `SqliteLedgerStoreTests` (change 6 to 7 where they assert the latest version; a test that **sets** `user_version = 6` to simulate a format-6 ledger keeps 6)
- `docs/cli.md` (the import allowlist between `<!-- import-allowlist -->` and `<!-- /import-allowlist -->` only; see "Docs" below)

> **The import allowlist changes with the schema.** `ImportPrivacyTests.IP2` requires every column of the created schema to be listed in `docs/cli.md`'s import allowlist (or in its "left empty" set), and `IP3` requires every column an import fills to be listed. Import fills all three new columns as soon as this task's store writes them (`list_cost_usd_pico` and `charge_source` on every insert, `import_revision` on every checkpoint, all non-NULL). So this task adds them to the allowlist in the same commit as the migration; a schema column is never added in one task and documented in a later one. The wording below is true at this task (every imported row is charged at the rate card until Task 6 reads `totalNanoAiu`); Task 6 replaces the `spans.charge_source` Copilot cell.

**DDL (migration 7, exactly):**

```sql
ALTER TABLE spans ADD COLUMN list_cost_usd_pico INTEGER
    CHECK (list_cost_usd_pico IS NULL OR list_cost_usd_pico >= 0);
ALTER TABLE spans ADD COLUMN charge_source TEXT NOT NULL DEFAULT 'rate_card'
    CHECK (charge_source IN ('rate_card', 'reported', 'rejected')
           AND (charge_source <> 'reported' OR (billing_channel = 'github_ai_credits' AND pricing_status = 'priced')));
ALTER TABLE import_sessions ADD COLUMN import_revision INTEGER NOT NULL DEFAULT 0 CHECK (import_revision >= 0);
CREATE INDEX ix_span_links_import_span ON span_links (span_id) WHERE owner_span_row_id IS NOT NULL;
```

with the comment above it: `// Charged amounts (Sprint 12). cost_usd_pico stays the charged amount that every report sums; list_cost_usd_pico is the rate card's price, and NULL means "the same as cost_usd_pico" (every row recorded before format 7, and any row a running format-6 daemon writes after the upgrade, was charged at the rate card). import_revision 0 marks logs imported before Sprint 12's import fixes, which the next import reads again.`

- The migration writes no row and never changes `import_sessions.complete`. `list_cost_usd_pico` is nullable on purpose: a gh-ledger 0.5.0 daemon still running after the binary is upgraded inserts without the new columns, and NULL then reads correctly as "list = charged".
- **Readers that need the list price** use `COALESCE(list_cost_usd_pico, cost_usd_pico)`. No report reads it in this sprint.

**Store**

- `InsertSql` adds `list_cost_usd_pico` and `charge_source` after `imported_tool_call_count` (columns and `@list_cost_usd_pico`, `@charge_source` values). `AddInsertParameters` writes `PicoUsd.FromUsd(calculation.ListUsdCost)` and `ChargeSourceName(calculation.ChargeSource)`.
- `ChargeSourceName`: `RateCard → "rate_card"`, `Reported → "reported"`, `Rejected → "rejected"`; any other value throws `ArgumentOutOfRangeException`.
- `ValidateEntry` also checks `ListUsdCost` as it checks `UsdCost` (non-negative and convertible by `PicoUsd.FromUsd`), and rejects `ChargeSource == Reported` unless `Channel == GitHubAiCredits && Status == Priced`, with `ArgumentException("A reported charge applies only to priced GitHub AI-credit requests.")`.
- `ImportSessionCheckpoint` gains `public int ImportRevision { get; init; }` with the comment `// LedgerImporter.CurrentImportRevision when written; 0 for checkpoints written before format 7`. `UpsertCheckpointAsync` writes it (`import_revision`, in the insert and the `DO UPDATE SET`); `ReadCheckpointAsync` gains a `bool hasImportRevision` parameter and selects `import_revision` when it's true and `0` otherwise. The store passes `true`; `LedgerSnapshot` passes `SchemaVersion >= 7`. `ValidateCheckpoint` rejects a negative `ImportRevision`.

**Tests** (`SchemaVersion7Tests`):

| Test | Steps | Expected |
|---|---|---|
| SV7-1 | create a format-6 ledger as `SchemaVersion6Tests` does (format-6 DDL by migrations 1–6, `user_version = 6`) with one live span (`cost_usd_pico` 473886000) and one checkpoint with `complete = 1`; open it with `SqliteLedgerStore.InitializeAsync` | `user_version` 7; the span's `list_cost_usd_pico` IS NULL, `charge_source` `rate_card`, `cost_usd_pico` 473886000; the checkpoint's `complete` 1 and `import_revision` 0; `ix_span_links_import_span` exists in `sqlite_master` |
| SV7-2 | record an entry whose calculation is `UsdCost 0.000473886m`, `ListUsdCost 0.000527m`, `ChargeSource Reported`, GitHub/priced | `cost_usd_pico` 473886000, `list_cost_usd_pico` 527000000, `charge_source` `reported` |
| SV7-3 | insert by SQL a span with `charge_source = 'reported'` and `billing_channel = 'local'` | `SqliteException` (CHECK) |
| SV7-4 | `ValidateEntry` with `ChargeSource Reported` and `Channel Local` | `ArgumentException` with the message above |
| SV7-5 | upsert then read a checkpoint with `ImportRevision = 1` | read back 1 |
| SV7-6 | `LedgerSnapshot` on a format-6 ledger with a checkpoint | `GetImportSessionAsync` returns it with `ImportRevision == 0`; the ledger's `user_version` stays 6 |
| SV7-7 | insert by SQL, as a format-6 daemon would, a span without the two new columns | the row has `list_cost_usd_pico` NULL and `charge_source` `rate_card` |

**Docs (`docs/cli.md` import allowlist, exactly):**

1. Insert these two rows immediately after the row whose first cell begins `` `spans.model_id` `` ("Priced from the fields above, as for live spans"):

   ```markdown
   | `spans.list_cost_usd_pico` | The rate card's price, from the fields above | Priced from the fields above |
   | `spans.charge_source` | `rate_card` | `rate_card` |
   ```

2. In the row whose cells read "The import's own results", append `` , `import_sessions.import_revision` `` to the first cell, after `` `import_sessions.imported_at_unix_ms` ``.

Change nothing else in `docs/cli.md`. No test is added for the docs; `ImportPrivacyTests` already enforces them.

**Acceptance:** SV7-1–SV7-7 pass; tests that assert the latest version now say 7 (`Database user_version 99 is newer than the supported version 7.`); `ImportPrivacyTests` (including `IP2` and `IP3`) passes with the two new rows and the added `import_revision` column; no Sprint 13 file is edited.

---

## Task 5 specification: Live Spans Record the Charged Amount

**Files**

- `src/CopilotLedger.Otlp/Ingestion/IngestResult.cs`, `src/CopilotLedger.Otlp/Ingestion/LedgerIngestor.cs`
- New `tests/CopilotLedger.Tests/Otlp/ReportedChargeIngestTests.cs`
- `tests/CopilotLedger.Tests/Otlp/LedgerIngestorTests.cs` (only if a helper calculator, such as `OverflowingCalculator`, needs `ListUsdCost`)
- `docs/configuration.md` (one sentence in "Charged amount and list price")

**Contracts**

```csharp
// IngestResult gains:
public int RejectedReportedCharges { get; init; }   // inference spans recorded at the rate card because their reported charge was rejected
```

**Algorithm (`LedgerIngestor.IngestAsync`)**

1. After `calculation = _calculator.Calculate(attributed)`, also check `PicoUsd.FromUsd(calculation.ListUsdCost)` inside the existing cost-out-of-range `try` (same `catch`, same `costRejected++`).
2. Count `calculation.ChargeSource == ChargeSource.Rejected` for every entry passed to `RecordAsync` into `RejectedReportedCharges`. A rejected charge isn't a rejected span: `RejectedSpans`, `FirstRejectionReason`, and the OTLP partial-success response are unchanged.

**Tests** (`ReportedChargeIngestTests`, using `LedgerIngestorTests`' in-memory OTLP JSON helpers and a temporary ledger):

| Test | Steps | Expected |
|---|---|---|
| RI1 (#76 AC1) | ingest one `chat` span (model `claude-haiku-4.5`, 1000 input, 100 output, no endpoint) with `github.copilot.nano_aiu` `{"doubleValue": 47388600}` | the row's `cost_usd_pico` 473886000 (0.0473886 AI credits), `charge_source` `reported`, `list_cost_usd_pico` = the rate card's price of the same span in pico-USD (from `PicoUsd.FromUsd(calculator.Calculate(spanWithoutCharge).UsdCost)`); `RejectedReportedCharges` 0 |
| RI2 (#76 AC1) | the same span without the attribute | `cost_usd_pico` = that rate-card price, `charge_source` `rate_card`, `list_cost_usd_pico` = `cost_usd_pico` |
| RI3 (#76 AC2) | an `invoke_agent` span with `nano_aiu` 11855545000 and one chat span with 47388600 in the same trace | the ledger sums 473886000 pico-USD for the trace; the `invoke_agent` span has no `spans` row |
| RI4 (#76 AC3) | chat spans with `doubleValue` `0.5`, `-1`, `"NaN"`, and `9007199254740993` | four rows at the rate card with `charge_source` `rejected`; `RejectedReportedCharges` 4; `RejectedSpans` 0 |
| RI5 (#76 AC5) | after RI1's span, query branch spend as `branch --since all --json` does and build the receipt as `ReceiptCommandTests` does | the receipt's total AI credits and USD equal the branch row's: `0.0473886` and `0.000473886` |
| RI6 (#76 AC5) | a branch budget of $0.0005 with RI1's span (`0.000473886`), then a second identical span | budget status before the second: under budget; after: over (the budget sums the charged amount, not the list price) |
| RI7 (#76 AC5) | the circuit breaker's credit-velocity evaluation over RI1's span with a threshold between the charged and the list price | evaluates against the charged amount (use `CircuitBreakerMonitorTests`' setup; pick a threshold strictly between the two amounts computed in the test) |

RI5–RI7 assert behavior through existing public APIs only; **no** budget, breaker, or receipt code changes (they already sum `cost_usd_pico`). If one of them fails, stop and report it: it means a reader uses something other than `cost_usd_pico`.

**Docs:** append to the "Charged amount and list price" paragraph: "The daemon counts rejected charges per request and records each as charged at the rate card."

**Acceptance:** RI1–RI7 pass; no file under `src/CopilotLedger.Core/Breaker`, `src/CopilotLedger.Core/Audit`, or alert text changes.

---

## Task 6 specification: Import Reads `totalNanoAiu`, and the Report Gains Its Sprint 12 Counts

**Files**

- `src/CopilotLedger.Core/Import/JsonFields.cs`
- `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs` (`UsagePaths`, `CompactionPaths`, `Usage`, `Compaction`)
- `src/CopilotLedger.Core/Import/ImportedUsageRecord.cs`, `src/CopilotLedger.Core/Import/SessionImportMapper.cs`
- `src/CopilotLedger.Core/Import/ImportDiagnosticCode.cs`
- `src/CopilotLedger.Storage/ImportRecordResult.cs`
- `src/CopilotLedger.Storage/Import/ImportReport.cs`, `src/CopilotLedger.Storage/Import/LedgerImporter.cs` (`Tally`, pricing loop, per-log diagnostics)
- `src/CopilotLedger.Cli/Import/ImportCommands.cs` (`Rows`, `WriteJson`, `Message`)
- `tests/CopilotLedger.Tests/TestSupport/CopilotSessionLogs.cs`
- New `tests/CopilotLedger.Tests/Import/CopilotReportedChargeTests.cs`
- Exact-output tests that change: `ImportCommandTests` (table rows and JSON), `GuideSamplesTests` samples via `docs/guide.md`, and any compaction amount assertions (see "Fixture default" below)
- `docs/cli.md` (import report table, JSON fields, allowlist), `docs/guide.md` (import samples)

**Contracts**

```csharp
// JsonFields gains:
// The value at path as a reported charge: Absent when the path is missing or JSON null; Valid for a JSON number that
// NanoAiu.TryFromJsonNumber accepts; Malformed for any other value (another number, a string, a bool, an object, an array).
internal ReportedCharge NanoAiu(string path);
// Value gains a field set only for JSON numbers: the NanoAiu.TryFromJsonNumber result over reader.ValueSpan (null if false).

// ImportedUsageRecord gains:
public ReportedCharge ReportedCharge { get; init; }   // totalNanoAiu; Absent when the log has none

// SessionImportMapper.ToSpan copies it: ReportedCharge = record.ReportedCharge.

// ImportDiagnosticCode gains, appended after SupersededUnmatched:
ReportedChargeRejected,       // set by the importer: requests of the log charged at the rate card because their reported charge was rejected

// ImportRecordResult gains (all default 0; Tasks 10 and 11 set them):
public int Repriced { get; init; }
public int LiveRepriced { get; init; }
public int ToolLinksRelinked { get; init; }
public int ToolLinksRemoved { get; init; }

// SessionLogReadResult gains (Task 8 sets it):
public long ToolCallsFallback { get; init; }          // tool calls linked by file order because their apiCallId chain didn't resolve

// ImportSourceReport gains (required, after ToolLinksRecorded):
public required long ToolLinksRelinked { get; init; }        // imported tool links moved to the request that asked for them
public required long ToolLinksRemoved { get; init; }         // imported tool links removed: their request has no imported row
public required long ToolLinksFallback { get; init; }        // tool calls linked by file order (no resolvable apiCallId)
public required long Repriced { get; init; }                 // imported rows whose pricing was rewritten
public required long LiveRepriced { get; init; }             // live rows given their log's reported charge
public required long ReportedChargesRejected { get; init; }  // requests priced at the rate card because their reported charge was rejected
```

`ImportSourceReport.Complete` is unchanged: none of the six makes coverage incomplete.

**Session-log paths (allowlist additions):** `UsagePaths` gains `("data.usage.copilotUsage.totalNanoAiu", 0)`; `CompactionPaths` gains `("data.compactionTokensUsed.copilotUsage.totalNanoAiu", 0)`. `Usage` sets `ReportedCharge = fields.NanoAiu("data.usage.copilotUsage.totalNanoAiu")` for every record; `Compaction` sets `ReportedCharge = fields.NanoAiu("data.compactionTokensUsed.copilotUsage.totalNanoAiu")`. A malformed charge never makes a record unsupported: it's priced at the rate card and counted.

**Importer:** for every in-scope record priced in this run (whatever its resolution), count `calculation.ChargeSource == Rejected` per log; add it to `tally.ReportedChargesRejected`, and when it's above 0, add a diagnostic `{ Source, LogId, Code = ReportedChargeRejected, Count }` after the `CostOutOfRange` one. Also check `PicoUsd.FromUsd(calculation.ListUsdCost)` inside the existing cost-out-of-range `try`. `tally.ToolLinksFallback += result.ToolCallsFallback` for each log read; `Repriced`, `LiveRepriced`, `ToolLinksRelinked`, and `ToolLinksRemoved` sum the batch results' fields.

**CLI (exact):**

- `Rows` gains, after `("Unpriced", …)` and in this order: `("Charges rejected", r => r.ReportedChargesRejected)`, `("Repriced", r => r.Repriced)`, `("Live repriced", r => r.LiveRepriced)`, `("Tool links relinked", r => r.ToolLinksRelinked)`.
- `WriteJson`'s per-source fields gain, right after `toolLinksRecorded` and in this order: `toolLinksRelinked`, `toolLinksRemoved`, `toolLinksFallback`, `repriced`, `liveRepriced`, `reportedChargesRejected`.
- `Message`: `ImportDiagnosticCode.ReportedChargeRejected => $"{Plural(count, "reported charge")} couldn't be used; charged at the rate card"`.

**Fixture support (`CopilotSessionLogs`):**

- `Usage(...)` gains `long? totalNanoAiu = null`; when not null it writes `usage["copilotUsage"] = { "totalNanoAiu": totalNanoAiu, "tokenDetails": [ { "tokenType": "input", "tokenCount": input } ] }` before `accounting`.
- `Compaction(...)` gains `long? totalNanoAiu = 11_855_545_000` (today's value); null omits `copilotUsage`.
- **Fixture default:** compactions in existing tests now carry a valid charge of 11,855,545,000 nano AIU, so they're charged **11.855545 AI credits = $0.11855545** instead of the rate card. Update each existing assertion that pins a compaction's billed amount to those exact values, or pass `totalNanoAiu: null` where the test is about rate-card pricing. Report the list of tests changed in the commit body.

**Tests** (`CopilotReportedChargeTests`, through `LedgerImporter` on a temporary ledger):

| Test | Log | Expected |
|---|---|---|
| IRC1 (#76 AC4) | one usage record `totalNanoAiu: 47388600` | its row: `cost_usd_pico` 473886000, `charge_source` `reported`, `list_cost_usd_pico` = the rate-card price; report `AiCredits == 0.0473886m`, `ReportedChargesRejected == 0` |
| IRC2 | the same without `copilotUsage` | rate card, `charge_source` `rate_card` |
| IRC3 | `totalNanoAiu` written as `0.5`, `-1`, `"47388600"`, `9007199254740993`, and `{}` (five records) | five rows at the rate card, `charge_source` `rejected`; `ReportedChargesRejected == 5`; one diagnostic `reportedChargeRejected` with `Count == 5`; coverage complete (exit 0 through the CLI) |
| IRC4 (#76 AC4) | a compaction with the fixture default | 11.855545 AI credits ($0.11855545), `reported` |
| IRC5 | a BYOK usage record (`providerType` `anthropic`) with `totalNanoAiu: 5` | `other_provider`, `charge_source` `rejected` (D3: a positive charge for a request GitHub didn't bill) |
| IRC6 | `ImportCommandTests`' table and JSON fixtures, updated | the four new rows appear after `Unpriced` with 0s; the six JSON fields appear after `toolLinksRecorded` with 0s |
| IRC7 | the `--dry-run` of IRC3 | the same counts and diagnostic; nothing written |

**Docs:**

- `docs/cli.md` report table: after `Unpriced`, rows `Charges rejected` ("Requests charged at the rate card because the charge their log reported isn't valid or can't apply"), `Repriced` ("Requests imported before whose pricing changed, which this import rewrote; see [Repairs](#repairs)"), `Live repriced` ("Live requests that this import gave the charge their log reported; see [Repairs](#repairs)"), `Tool links relinked` ("Imported tool calls moved to the request that asked for them; see [Repairs](#repairs)"). (Task 12 adds the "Repairs" heading; until then the anchor is a forward reference, which is fine.)
- JSON field list: after `toolLinksRecorded`, add `toolLinksRelinked`, `toolLinksRemoved`, `toolLinksFallback` (tool calls linked to the latest request of their agent because their `apiCallId` couldn't be followed), `repriced`, `liveRepriced`, `reportedChargesRejected`; the diagnostic codes gain `reportedChargeRejected`.
- Allowlist table (Task 4 already added the `spans.list_cost_usd_pico` and `spans.charge_source` rows and `import_sessions.import_revision`; don't add them again, or `DocumentedColumns` fails with "listed twice"): replace the `spans.charge_source` row's Copilot cell, `` `rate_card` ``, with: `reported` when `data.usage.copilotUsage.totalNanoAiu`, or a compaction's `data.compactionTokensUsed.copilotUsage.totalNanoAiu`, is a valid charge for a priced GitHub request; `rejected` when it's present but not; else `rate_card`. Its Codex cell stays `` `rate_card` ``, and the `spans.list_cost_usd_pico` row is unchanged. Add `spans.cost_usd_pico`'s note: "the reported charge when `charge_source` is `reported`".
- `docs/guide.md`: update the import samples' tables (the four new rows) so `GuideSamplesTests` passes.

**Acceptance:** IRC1–IRC7 and every updated exact-output test pass; `ImportPrivacyTests` passes with the updated allowlist.

---

## Task 7 specification: Import BYOK and Failed Compactions

**Files**

- `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs` (`CompactionPaths`, `Compaction`, and the latest BYOK provider by model)
- `src/CopilotLedger.Core/Import/ImportedUsageRecord.cs` (comment on `RecordKey` only)
- `tests/CopilotLedger.Tests/Import/CopilotCompactionTests.cs` (tests that asserted "failed" or "no service request ID" is unsupported change to the rules below)
- New `tests/CopilotLedger.Tests/Import/CopilotCompactionShapeTests.cs`
- `docs/cli.md` (allowlist rows for `span_id` and `import_record_aliases.record_key_sha256`; "Read, never stored")

**Paths:** `CompactionPaths` gains `("data.compactionAttemptId", 2048)` and `("data.compactionTokensUsed.provider.id", 64)` (only so that `provider` is recorded as an object; `provider.id` is never used). `IsObject("data.compactionTokensUsed.provider")` and `IsObject("data.compactionTokensUsed.copilotUsage")` decide presence. `summaryContent`, `clientRequestId`, `requestId`, `checkpointPath`, and `attemptMetrics` stay unread.

**Session state:** `Session` keeps `_latestByokProviderByModel` (`Dictionary<string, string>`, `StringComparer.OrdinalIgnoreCase`): each valid BYOK usage record sets `[model] = providerType.ToLowerInvariant()` when `providerType` is a string.

**Algorithm (`Compaction`), the first rule that matches decides** (`srid` = normalized `data.serviceRequestId`; `success` = `fields.Boolean("data.success")`; `charge` = `fields.NanoAiu("data.compactionTokensUsed.copilotUsage.totalNanoAiu")`):

0. Unchanged: no `compactionTokensUsed` (and no duplicate) → skipped event. A duplicate property → parsed, unsupported, field = the duplicate.
1. `success` isn't a boolean → parsed, unsupported, field `data.success`.
2. `success == false && srid is null && !IsObject(copilotUsage)` → **skipped event** (`_skipped++`, not parsed): a failed attempt nobody billed.
3. `success == false && srid is not null && charge.State == Valid` → a charged GitHub compaction: the success path's checks and record (rule 5), with its `ReportedCharge`.
4. `success == false` (anything else) → parsed, unsupported, field `data.success`.
5. `success == true && srid is not null` → today's path, unchanged except `ReportedCharge = charge` (Task 6).
6. `success == true && srid is null && !IsObject(provider) && !IsObject(copilotUsage) && compactionAttemptId is not null` → a **BYOK compaction**: today's checks for `timestamp`, `compactionTokensUsed` object, `requestCount == 1`, `model`, tokens, and duration (not `provider.kind`), then a record with `RecordKey = compactionAttemptId`, `ServiceRequestId = null`, `ProviderName = _latestByokProviderByModel.GetValueOrDefault(model)` (null when none), `BillingSource = BillingSource.Provider`, `ReportedCharge = Absent`, cache states `Reported`, `ToolCallCount = 0`, and the agent and turn as today.
7. Otherwise (`success == true` with no `srid` and a provider, a `copilotUsage`, or no `compactionAttemptId`) → parsed, unsupported, field `data.serviceRequestId`.

A BYOK compaction has no canonical ID, so it's recorded only from sessions without live spans (`Ambiguous-overlap` otherwise), as other BYOK requests are. Its record key is `compactionAttemptId`, stored only as `ImportIds.RecordKeySha256(Copilot, Compaction, attemptId)` and in `span_id`'s hash.

**Tests** (`CopilotCompactionShapeTests`; fixtures change `Compaction(...)` with `change:`):

| Test | Log | Expected |
|---|---|---|
| CC1 (#82 AC1) | `session.start` 1.0.114; one BYOK usage record (`providerType` `anthropic`, model `claude-haiku-4.5`); a successful BYOK compaction (no `serviceRequestId`, no `provider`, no `copilotUsage`, `compactionAttemptId` `c0000000-0000-4000-8000-0000000000b1`); a failed charged compaction (`success` false, `serviceRequestId` `srv-failed-1`, the default charge); a failed uncharged compaction (`success` false, no `serviceRequestId`, no `copilotUsage`) | through the CLI, `import --all` exits 0 with coverage complete; recorded: the BYOK usage record, the BYOK compaction (provider `anthropic`, `other_provider`), and the failed charged compaction (11.855545 AI credits, `reported`); `SkippedEvents` includes the uncharged one; the second run reports `Skipped, unchanged` 1 |
| CC2 (#82 AC2) | a successful GitHub compaction missing `inputTokens` | unsupported, coverage incomplete (exit 2), the log isn't skipped next run |
| CC3 | `success` is `"true"` (a string) | unsupported, field `data.success` |
| CC4 | failed with `serviceRequestId` and no `copilotUsage` | unsupported, field `data.success` |
| CC5 | failed with `serviceRequestId` and `totalNanoAiu` `0.5` | unsupported, field `data.success` (a failed compaction is recorded only on a valid charge) |
| CC6 | successful, no `serviceRequestId`, but a `copilotUsage` | unsupported, field `data.serviceRequestId` |
| CC7 | successful BYOK compaction with no earlier BYOK record of its model | recorded with `provider_name` NULL, `other_provider` |
| CC8 | CC1's log in a session that has a live span | the BYOK compaction is `Ambiguous-overlap`; the failed charged one is recorded (or overlap-skipped when the live span carries its service request ID) |
| CC9 (privacy) | CC1's log with `summaryContent`, `clientRequestId`, `requestId`, `checkpointPath` set to the fixture `Canary` | no canary in any ledger table, the report's text or JSON, or diagnostics; the raw `compactionAttemptId` appears nowhere in the ledger |

**Docs (`docs/cli.md`):** `spans.span_id` and `import_record_aliases.record_key_sha256` rows: "…or a compaction's `data.serviceRequestId`, or a BYOK compaction's `data.compactionAttemptId`". "Read, never stored": add "`data.compactionAttemptId`, stored only as the SHA-256 hashes above, and whether `data.compactionTokensUsed.provider` and `copilotUsage` are present". "Imported requests" in `docs/configuration.md` gains: "A compaction that Copilot billed is recorded even when it failed; a failed compaction that nobody billed is skipped. A compaction through your own provider (BYOK) is priced as that provider's requests are, by the provider of the latest BYOK request with the same model."

**Acceptance:** CC1–CC9 pass; `summaryContent` is never read.

---

## Task 8 specification: Link Imported Tool Calls by `apiCallId`

**Files**

- `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs` (`Types`, `Kinds`, new `AssistantMessagePaths`, `ToolStartPaths`, `ToolCompletePaths`, `UsagePaths`, `OnLine`, `Usage`, `ToolStart`, `ToolComplete`, `Finish`, `PendingTool`)
- `src/CopilotLedger.Core/Import/ImportEventKind.cs` (append `AssistantMessage`)
- `src/CopilotLedger.Core/Import/SessionLogReadResult.cs` (`ToolCallsFallback`, added in Task 6, now set)
- `tests/CopilotLedger.Tests/TestSupport/CopilotSessionLogs.cs`
- New `tests/CopilotLedger.Tests/Import/CopilotToolLinkTests.cs`
- `tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs` (one new test)
- `docs/cli.md` (allowlist rows for `span_links`, "Read, never stored", "Never read")

**Paths (exact):**

- `Types` gains `"assistant.message"` (and `Kinds` gains `ImportEventKind.AssistantMessage`, appended to the enum).
- `AssistantMessagePaths = new(("id", 2048), ("parentId", 2048), ("data.apiCallId", 2048))`.
- `ToolStartPaths` and `ToolCompletePaths` each gain `("id", 2048)` and `("parentId", 2048)`.
- `UsagePaths` gains `("data.usage.apiCallId", 2048)`.

Nothing else is read from `assistant.message`. An `assistant.message` still counts as a skipped event (`_skipped++`), so `SkippedEvents` doesn't change for existing logs.

**Session state:**

- `_chain`: `Dictionary<string, (ImportEventKind Kind, string? ParentId, string? ApiCallId)>` (ordinal), keyed by envelope `id`, for `assistant.message`, `tool.execution_start`, and `tool.execution_complete` events with no duplicate property and a string `id`. A repeated `id` keeps the first entry.
- `_usageKeyByApiCallId`: `Dictionary<string, string>` (ordinal): each **valid** usage record sets `[apiCallId ?? usageId] = usageId`, where `apiCallId = fields.String("data.usage.apiCallId")`. A repeated key keeps the first.
- `PendingTool` gains `string? ParentId` (the start event's `parentId`). Completed calls are kept as pending results (fallback owner, parent ID, and the fields `ImportedToolCall` needs) and turned into `ImportedToolCall`s in `Finish`.

**Algorithm (`Finish`, for each completed tool call, in completion order):**

1. `id = call.ParentId`; `hops = 0`.
2. While `id is not null && hops < 100`: `hops++`; if `!_chain.TryGetValue(id, out entry)` → stop (unresolved). If `entry.Kind == AssistantMessage` → if `entry.ApiCallId is not null && _usageKeyByApiCallId.TryGetValue(entry.ApiCallId, out owner)` → resolved with `owner`; otherwise unresolved; stop. Else (`ToolExecutionStart` or `ToolExecutionComplete`) → `id = entry.ParentId`.
3. Resolved → `OwnerRecordKey = owner`. Unresolved → `OwnerRecordKey = call.FallbackOwner` (today's rule: the latest usage record of the tool's agent at its start); when that's null, the call is dropped as today. Count `ToolCallsFallback` for every call whose owner came from the fallback and wasn't null.

**Fixture support (`CopilotSessionLogs`):**

- `Event(...)` gains `string? parentId = null`; when given, it replaces the automatic `parentId`. Add `public string LastId` (the last event's `id`).
- `AssistantMessage(string apiCallId, string? parentId = null)` writes `assistant.message` with `data`: `messageId`, `apiCallId`, `content = Canary`, `reasoningText = Canary`, `reasoningOpaque = Canary`, `encryptedContent = Canary`, `toolRequests = [ { "toolCallId": "<id>", "name": Canary, "arguments": { "command": Canary } } ]`, `reasoningBlocks = [ Canary ]`.
- `ToolStart(...)` and `ToolComplete(...)` gain `string? parentId = null`.

**Tests** (`CopilotToolLinkTests`):

| Test | Log (same agent throughout) | Expected |
|---|---|---|
| TL1 (#85 AC1) | `AssistantMessage("api-A")` → id `mA`; `Usage("api-A", "srv-A", numToolCalls: 2)`; `AssistantMessage("api-B")`; `Usage("api-B", "srv-B")`; `ToolStart("t1", "bash", parentId: mA)` → `s1`; `ToolStart("t2", "bash", parentId: s1)`; `ToolComplete("t1", true, parentId: s1)`; `ToolComplete("t2", true)` | both links' `owner_span_row_id` is A's row and their `trace_id` A's trace; none is B's; `ToolLinksFallback == 0` |
| TL2 (#85 AC2) | TL1, but the tool start's `parentId` names an unknown ID | the call falls back to B (today's rule) and `ToolLinksFallback == 1` |
| TL3 | the chain passes through a `hook.start` event | fallback, counted |
| TL4 | the message's `apiCallId` matches no usage record | fallback, counted |
| TL5 | a usage record without `data.usage.apiCallId`, its message's `apiCallId` equals its `usageId` | resolved (1.0.115's shape, L3) |
| TL6 | a `parentId` cycle of two tool events | fallback after at most 100 hops, counted; no exception |
| TL7 | a usage record that arrives **after** its tool starts and completes | resolved to it (resolution is at the end of the log) |
| TL8 | 1.0.114 fixtures used by existing tool tests | every existing tool-link test passes unchanged |
| TL9 | `--dry-run` of TL1 | `toolLinksRecorded` 2, `toolLinksFallback` 0 |

`ImportPrivacyTests` gains **IP-AM1 (#85 AC3):** a log with `AssistantMessage` canaries (all six fields above) and tool arguments and results holding `Canary`; after import, no table of the ledger, the text report, the JSON report, or any diagnostic contains `Canary`. Use the existing privacy tests' whole-database scan.

**Docs (`docs/cli.md`):**

- Allowlist row `span_links.trace_id`, `span_links.conversation_id`, `span_links.owner_span_row_id`: "The usage record whose response asked for the tool: the one whose `data.usage.apiCallId` (or usage ID) equals `data.apiCallId` of the `assistant.message` that the tool's `parentId` chain reaches through tool events; when the chain doesn't reach one, the latest usage record of the tool's agent".
- "Read, never stored": add "the envelope `id` and `parentId` of `assistant.message`, `tool.execution_start`, and `tool.execution_complete`, `data.apiCallId` of `assistant.message`, and `data.usage.apiCallId` of usage records, which tie a tool call to the request that asked for it".
- "Never read": replace "user and assistant messages" with "user and assistant message content (`content`, `transformedContent`), reasoning (`reasoningText`, `reasoningOpaque`, `reasoningBlocks`, `encryptedContent`), the tool requests a message makes (`toolRequests`)", keeping the rest of the list.

**Acceptance:** TL1–TL9 and IP-AM1 pass; the only new paths are those listed.

---

## Task 9 specification: Import Copilot CLI 1.0.115 Sessions

**Files**

- `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs` (`MaximumVersion`, `Usage`, the gate's comment)
- `src/CopilotLedger.Cli/Import/ImportCommands.cs` (only if the supported-range text is a literal rather than built from `MaximumVersion`)
- `tests/CopilotLedger.Tests/TestSupport/CopilotSessionLogs.cs` (`Usage115`)
- New `tests/CopilotLedger.Tests/Import/Copilot115Tests.cs`
- `tests/CopilotLedger.Tests/Import/CopilotSessionGateTests.cs`, `tests/CopilotLedger.Tests/Cli/ImportCommandTests.cs` (the "1.0.103 to 1.0.114 are supported" text)
- `docs/cli.md` (versions table), `docs/guide.md` (any version range it states)

**Contract:** `MaximumVersion = new(1, 0, 115)`. The gate's comment says "Copilot 1.0.103 to 1.0.115".

**Algorithm (`Usage`), only when the session's `copilotVersion` ≥ 1.0.115** (keep the parsed `ProducerVersion` from `Start`; earlier versions keep today's rules exactly):

1. `cacheDetailsReported` **absent** (`!IsPresent`) → treated as `false`. Present but not a boolean → unsupported as today.
2. `isByok` **absent** → treated as `false` only when `provider.kind == "copilot"`, `serviceRequestId` is not null, and `IsPresent("data.usage.copilotUsage.totalNanoAiu")`; otherwise unsupported, field `data.usage.isByok`. Present but not a boolean → unsupported as today.
3. `numToolCalls` absent → `ToolCallCount = null` (today's behavior).
4. `apiCallId` absent → the usage key is `usageId` (Task 8 already does this).
5. `data.apiCallIdFromUsageId` and `isAuto` aren't read.

**Fixture (`Usage115`)**: writes `session.usage_record` with `data.usage` = `model`, `responseModel` (optional), `provider { id: "copilot", kind: "copilot", label: "GitHub Copilot" }`, `modelDisplayName`, the four token counts, `reasoningTokens` 0, `duration`, `initiator`, `serviceRequestId`, `copilotUsage { totalNanoAiu, tokenDetails: [] }` (when `totalNanoAiu` isn't null), and `accounting { sourceSessionId, sequence, usageId }`; and `data.apiCallIdFromUsageId = true`. No `isByok`, `cacheDetailsReported`, `numToolCalls`, `isAuto`, or `apiCallId`.

**Tests** (`Copilot115Tests`):

| Test | Log | Expected |
|---|---|---|
| V115-1 (#75 AC1) | `session.start` 1.0.115; two `Usage115` records (`totalNanoAiu` 47388600 and 11855545); an `AssistantMessage` per record and one tool call per record linked by chain | both recorded (0.0473886 and 0.011855545 AI credits, `reported`); `imported_tool_call_count` NULL; both tool links resolved; coverage complete, exit 0 |
| V115-2 (#75 AC1) | the same, `--dry-run` then real run on a fresh ledger | dry-run and real-run reports have identical counts (every `ImportSourceReport` field) |
| V115-3 (#75 AC1) | a live span carrying `srv-115-a` recorded first, then the log | that record is `Overlap-skipped`; the other is recorded |
| V115-4 | a `Usage115` without `copilotUsage` | unsupported, field `data.usage.isByok` (fail closed) |
| V115-5 | a `Usage115` whose `provider.kind` is `byok-custom` | unsupported, field `data.usage.isByok` |
| V115-6 | a 1.0.114 log with a usage record missing `cacheDetailsReported` | unsupported, field `data.usage.cacheDetailsReported` (old rules unchanged) |
| V115-7 (#75 AC2) | `session.start` with `copilotVersion` `1.0.116` and `1.0.116-0` | `UnsupportedProducerVersion`, not read; the CLI warning is exactly the existing content-free text with "1.0.103 to 1.0.115 are supported" |
| V115-8 (#75 AC3) | V115-1's log with canaries in `Usage115`'s unread places (`modelDisplayName`, a `toolCounts` key) and the messages | no canary in the ledger, report, or diagnostics |

`CopilotSessionGateTests`: the `1.0.115` row becomes `Read`, `1.0.116` stays unsupported.

**Docs:** `docs/cli.md` versions table: "1.0.103 to 1.0.115". Add after the table: "A new Copilot version is read only after gh-ledger has been checked against its logs; until then its sessions are reported as unsupported, and nothing is guessed."

**Acceptance:** V115-1–V115-8 pass; no 1.0.114 behavior changes.

---

## Task 10 specification: Re-Import Repairs Billing in Place

**Files**

- `src/CopilotLedger.Storage/Import/LedgerImporter.cs` (`CurrentImportRevision`, the skip check, the checkpoint)
- `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs`, `src/CopilotLedger.Storage/UsageAuthority.cs`, `src/CopilotLedger.Storage/SqliteLedgerStore.cs` (a shared pricing-columns helper)
- `src/CopilotLedger.Storage/LedgerSnapshot.cs`
- New `tests/CopilotLedger.Tests/Import/ImportRepairTests.cs`

**Contracts**

```csharp
// LedgerImporter gains:
public const int CurrentImportRevision = 1;   // raise it when a fix changes what import records from an unchanged log

// UsageAuthority gains:
// A row's pricing columns, as AddInsertParameters writes them. ListCostPico and ChargeSource read as COALESCE(list, cost)
// and 'rate_card' on format < 7.
internal readonly record struct StoredPricing(
    string BillingChannel, string PricingStatus, string RatesVersion, string? ModelId, string? RateCardModelId,
    string? TierName, long CostPico, long SavedPico, long ListCostPico, string ChargeSource,
    long? ApiEquivalentPico, string? ApiEquivalentStatus, string? ApiRateCardModelId, string? ApiRatesVersion,
    string? ApiRateCardSha256);
internal static StoredPricing PricingOf(CreditCalculationResult calculation);   // the values AddInsertParameters would write
internal static Task<StoredPricing> ReadPricingAsync(SqliteConnection, SqliteTransaction?, long rowId, int schemaVersion, CancellationToken);
internal static Task<int> RewritePricingAsync(SqliteTransaction, long rowId, StoredPricing pricing, CancellationToken); // imported rows only
internal static Task<int> ApplyReportedToLiveAsync(SqliteTransaction, long rowId, long costPico, CancellationToken);
```

Refactor `AddInsertParameters` so it takes the 15 pricing values from `PricingOf(calculation)`; insert, compare, and rewrite then use identical values.

**Algorithm**

1. **Skip check.** A log is skipped as unchanged only when its checkpoint is complete, `checkpoint.ImportRevision >= CurrentImportRevision`, and size, last-write time, and SHA-256 match. The checkpoint the importer writes sets `ImportRevision = CurrentImportRevision`. So after the upgrade every log is read once more.
2. **Own imported row (`RecordImportedAsync`).** For an entry resolved `Duplicate` whose own row exists (`UsageAuthority.RecordedRowAsync` returns a row; today's call is no longer limited to entries with tool links): `stored = ReadPricingAsync(row)`; `wanted = PricingOf(entry.Calculation)`; if `stored != wanted`, `RewritePricingAsync` (`UPDATE spans SET` the 15 columns `WHERE id = @id AND ingest_source <> 'otlp'`) and `Repriced++`.
3. **Live row (D2).** After deciding, when `canonical is not null`, `facts.Owner == CanonicalOwner.Live`, and `entry.Calculation.ChargeSource == ChargeSource.Reported`: `ApplyReportedToLiveAsync(facts.OwnerRowId, PicoUsd.FromUsd(entry.Calculation.UsdCost))` runs
   ```sql
   UPDATE spans
      SET list_cost_usd_pico = COALESCE(list_cost_usd_pico, cost_usd_pico),
          cost_usd_pico = @cost,
          charge_source = 'reported'
    WHERE id = @id AND ingest_source = 'otlp'
      AND billing_channel = 'github_ai_credits' AND pricing_status = 'priced'
      AND charge_source <> 'reported';
   ```
   and `LiveRepriced` adds the rows it changed (0 or 1). This applies whether the resolution is `OverlapSkipped` or `Duplicate` (an alias written by an earlier import). A live row already `reported` is never changed.
4. **Rows another record owns** (a `Duplicate` without its own row, such as a fork's copy) aren't repriced; their own log's import reprices them.
5. **Dry run (`LedgerSnapshot.EvaluateAsync`).** Decide the same: for an own-row `Duplicate`, compare `ReadPricingAsync(row, SchemaVersion)` with `PricingOf(calculation)` and count `Repriced` when different; for a live owner, count `LiveRepriced` when the row is `github_ai_credits`/`priced` and its charge source (read as `rate_card` on format < 7) isn't `reported`. Remember each row ID counted (`HashSet<long>`) so the same row counts once per run, as the real run's second update would change nothing. Write nothing.
6. `RecordImportedAsync` returns `Repriced` and `LiveRepriced` in `ImportRecordResult`; the importer sums them (Task 6).

**Tests** (`ImportRepairTests`; "earlier import" = build the state by recording entries through the store with the old pricing, then run the importer):

| Test | Setup | Expected |
|---|---|---|
| RR1 (D1) | an imported row recorded at the rate card (`charge_source` `rate_card`) and a complete checkpoint with `import_revision` 0 for the unchanged log; the log's record has `totalNanoAiu` 47388600 | the log isn't skipped; `Repriced == 1`; the row's `cost_usd_pico` 473886000, `charge_source` `reported`, `list_cost_usd_pico` the rate-card price; the checkpoint's `import_revision` 1 |
| RR2 | run RR1's import again | `Skipped, unchanged` 1; `Repriced == 0` |
| RR3 (D2) | a live row (`otlp`, canonical ID of `srv-L`, rate card, `charge_source` `rate_card`, `list_cost_usd_pico` NULL); the log has `srv-L` with `totalNanoAiu` 47388600 | `Overlap-skipped` 1, `LiveRepriced == 1`; the live row's `cost_usd_pico` 473886000, `list_cost_usd_pico` = its old cost, `charge_source` `reported` |
| RR4 (D2) | RR3 with the alias already written by an earlier import (`live`) | `Duplicate` 1, `LiveRepriced == 1`, same row values |
| RR5 (D2) | a live row already `reported` at 400000000 pico; the log says 47388600 | unchanged; `LiveRepriced == 0` |
| RR6 (D2) | a live row without a canonical ID in the same conversation | unchanged; the record is `Ambiguous-overlap` |
| RR7 (#79 repair) | an imported BYOK row recorded `other_provider` before the overlay listed its model; the overlay now lists it | `Repriced == 1`; the row is `local`/`local_offload` with savings |
| RR8 (#82 repair) | a checkpoint `complete = 0` left by a BYOK compaction that was unsupported | the compaction is recorded; the checkpoint becomes complete |
| RR9 (dry run) | RR1, RR3, and RR7's states, `--dry-run` | the same `Repriced` and `LiveRepriced` as the real run, and the ledger file's bytes are unchanged |
| RR10 (dry run on format 6) | RR1's state in a format-6 ledger (built by SQL) | dry run reports `Repriced == 1`; the ledger stays format 6 |
| RR11 (budgets) | after RR3, the branch total from the store | equals the reported charge, not the old rate-card cost |

**Docs:** none here (Task 12 writes "Repairs").

**Acceptance:** RR1–RR11 pass; every existing import, snapshot, and authority test passes.

---

## Task 11 specification: Re-Import Repairs Tool Links in Place

**Files**

- `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs` (`InsertOwnedLinkAsync` becomes `UpsertOwnedLinkAsync`; removal)
- `src/CopilotLedger.Storage/LedgerSnapshot.cs`
- `tests/CopilotLedger.Tests/Import/ImportRepairTests.cs` (new tests)

**Contracts**

```csharp
private enum LinkWrite { Unchanged, Inserted, Relinked }
// The imported link for link.SpanId in link.ConversationId (owner_span_row_id IS NOT NULL), made to belong to ownerRowId and
// link.TraceId: inserted when there's none, updated in place when its owner or trace differs.
private static Task<LinkWrite> UpsertOwnedLinkAsync(SqliteTransaction, GenAiSpanLink link, long ownerRowId, long recordedAtUnixMs, CancellationToken);
// Removes this conversation's imported links with these span IDs; returns how many.
private static Task<int> RemoveOwnedLinksAsync(SqliteTransaction, IReadOnlyList<GenAiSpanLink> links, CancellationToken);
```

**Algorithm**

1. `UpsertOwnedLinkAsync`: `SELECT trace_id, owner_span_row_id FROM span_links WHERE span_id = @span AND conversation_id = @conversation AND owner_span_row_id IS NOT NULL ORDER BY trace_id` (uses `ix_span_links_import_span`).
   1. No row → today's `INSERT … ON CONFLICT (trace_id, span_id) DO NOTHING`; `Inserted` when it inserted, else `Unchanged`.
   2. The first row has `trace_id = link.TraceId` and `owner_span_row_id = ownerRowId` → `Unchanged`.
   3. Otherwise → `UPDATE span_links SET trace_id = @trace, owner_span_row_id = @owner WHERE trace_id = @old_trace AND span_id = @span` → `Relinked`.
   4. Any further rows (more than one imported link with that span ID in the conversation) are deleted and counted as removed.
2. `RecordImportedAsync`:
   - `Recorded` → `UpsertOwnedLinkAsync` for each link with the new row: `Inserted` adds to `ToolLinksRecorded`, `Relinked` to `ToolLinksRelinked`.
   - `Duplicate` with its own row → the same with the own row (today only inserted new links).
   - `OverlapSkipped`, `AmbiguousOverlap`, and `Duplicate` without its own row → `RemoveOwnedLinksAsync(entry.ToolLinks)` (`DELETE FROM span_links WHERE span_id = @span AND conversation_id = @conversation AND owner_span_row_id IS NOT NULL`), added to `ToolLinksRemoved`. These requests have no imported row, so an imported link left there by an earlier import belongs to the wrong request.
   - Live links (`owner_span_row_id IS NULL`) are never changed or removed.
3. `LedgerSnapshot.EvaluateAsync` decides the same with an in-memory overlay `Dictionary<(string Conversation, string SpanId), (string TraceId, long? Owner)?>` (null value = removed in this run) over the ledger's rows; for a `Recorded` entry, any existing imported link with the span ID counts as `Relinked` (the new row is necessarily a different owner). It writes nothing.

**Tests**

| Test | Setup | Expected |
|---|---|---|
| RL1 (D1, #85 repair) | TL1's log; the ledger holds the state an earlier import left: both rows recorded, both tool links owned by B's row with B's trace, checkpoint revision 0 | `ToolLinksRelinked == 2`; both links now have A's row and A's trace; `span_links` has exactly 2 imported rows for the conversation |
| RL2 | RL1 again | `Skipped, unchanged` 1 |
| RL3 | an imported link owned by an imported row, whose correct owner (by chain) is a record that's `Overlap-skipped` | `ToolLinksRemoved == 1`; no imported link with that span ID remains |
| RL4 | a live link (`owner_span_row_id` NULL) with the same `span_id` text | untouched |
| RL5 (dry run) | RL1 and RL3, `--dry-run` | the same `ToolLinksRelinked` and `ToolLinksRemoved`; ledger bytes unchanged |
| RL6 | a new tool call completed since the last import (today's behavior) | `ToolLinksRecorded == 1`, `ToolLinksRelinked == 0` |

**Acceptance:** RL1–RL6 pass; every existing tool-link and snapshot test passes; nothing outside `span_links` changes.

---

## Task 12 specification: Upgrade Guidance and Reference Consistency

**Files**

- `docs/cli.md` (the **Ledger format** paragraph's format number and one sentence; the import section's "Repairs" subsection)
- `docs/guide.md` (the upgrade section)
- `docs/configuration.md` (cross-reference only)
- `tests/CopilotLedger.Tests/Docs/UpgradeDocsTests.cs` (new test)

**Docs (exact wording to add):**

1. `docs/cli.md`, "**Ledger format.**": "This version upgrades the ledger to format 6" → "This version upgrades the ledger to format 7", and append after that sentence: "Format 7 keeps each request's list price beside the amount it was charged. After upgrading, run `gh ledger import --all` once so that requests imported before, and live requests whose session logs report their charge, are repaired (see [Repairs](#repairs))." Change nothing else in that paragraph or section.
2. `docs/cli.md`, new subsection `### Repairs` after "Overlap with live spans":
   - "An import after upgrading reads every session log again once, even one it imported completely before, and repairs what it recorded from it in place:"
   - "- **Charges.** A request imported before is priced again with the rates in use now; when anything about its price changed, such as the charge its log reports, its row is rewritten (`Repriced`). Its list price is kept beside the charge."
   - "- **Live requests.** A live request that the daemon recorded at the rate card, with a service request ID, gets the charge its session log reports, when the log reports a valid one (`Live repriced`). Its list price is kept. Live requests without a service request ID, such as those an earlier gh-ledger recorded, and those whose logs are gone or unsupported, keep the rate card's price."
   - "- **Tool calls.** An imported tool call that an earlier import attached to the wrong request moves to the request that asked for it (`Tool links relinked`); one whose request has no imported row, such as a request that a live span recorded, is removed (`toolLinksRemoved` in `--json`). Tool calls older than 7 days aren't kept, so they aren't repaired."
   - "- **Compactions.** Logs whose BYOK or failed compactions earlier versions couldn't read are read completely now."
   - "A dry run reports the same counts and changes nothing. Repairs never change which request counts: the rules of [Overlap with live spans](#overlap-with-live-spans) still decide that. Budgets, the circuit breaker, receipts, reports, export, and audit use the repaired charges from then on; circuit-breaker trips recorded before aren't re-evaluated."
3. `docs/guide.md` upgrade section: after the `gh extension upgrade ledger` step and the daemon restart, add a step "Repair what earlier versions imported:" with a `shell` block `gh ledger import --all` and the sentence "It reads each session log again once and fixes imported charges and tool calls; see [Repairs](cli.md#repairs)." Don't add a `<!-- sample -->` marker.
4. `docs/configuration.md`, "Charged amount and list price": append "Upgrading from 0.5.0 or earlier keeps the rows you have; [Repairs](cli.md#repairs) says how an import corrects them."

**Tests**

- **UD3** (`UpgradeDocsTests`): `docs/cli.md`'s "**Ledger format.**" line contains `format 7` and not `format 6`; `docs/guide.md`'s upgrade section contains `gh ledger import --all` after `gh extension upgrade ledger`; `docs/cli.md` contains `### Repairs`.
- **UD4**: the latest migration's version (`LedgerMigrations.Migrations[^1].Version`) is 7 and `docs/cli.md` names `format {that version}` (ties the docs to the DDL).

**Acceptance:** UD1–UD4 pass; `GuideSamplesTests` passes; no other section of the install, daemon, export, or audit docs changes.

---

## Task 13 specification: Final Verification

No product change. Run and record each result in the task commit's body (the commit changes only this plan's checkbox).

1. `dotnet build CopilotLedger.slnx` → 0 warnings, 0 errors.
2. `dotnet test CopilotLedger.slnx` → 0 failed, 0 skipped; record the total and the increase over the baseline.
3. `git --no-pager diff --stat 69ae671 -- src/CopilotLedger.Cli/CopilotLedger.Cli.csproj src/CopilotLedger.Core/Pricing/rates.json Directory.Build.props global.json` → empty (no version, rates, or SDK change).
4. `git --no-pager diff --stat 69ae671 -- src/CopilotLedger.Core/Audit src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs src/CopilotLedger.Storage/SqliteLedgerStore.Audit.cs` and any `WindowsDetachedProcess.cs` → empty (Sprint 13's files).
5. `git --no-pager diff 69ae671 -- src | Select-String -Pattern '^\+.*\b(double|float|Double|Single|REAL)\b'` → no money code; any hit must be a comment or the OTLP `Kind = Double` enum name, and is listed in the commit body with its reason.
6. `git --no-pager diff 69ae671 -- tests | Select-String -Pattern 'Environment\.GetFolderPath|UserProfile|\.copilot-ledger|Process\.Start|HttpClient|docker'` → no new hits outside existing harness helpers.
7. `docs/cli.md`'s allowlist lists `spans.list_cost_usd_pico`, `spans.charge_source`, and `import_sessions.import_revision`, and its "Read, never stored" list names every new path: `data.usage.copilotUsage.totalNanoAiu`, `data.compactionTokensUsed.copilotUsage.totalNanoAiu`, `data.compactionAttemptId`, `data.usage.apiCallId`, `assistant.message`'s `id`, `parentId`, `data.apiCallId`, and the tool events' `id` and `parentId` (`ImportPrivacyTests` enforces the stored columns; check the read list by eye).
8. `docs/configuration.md` describes the charged amount, the list price, rejected charges, and #79's rule as Tasks 2, 5, and 12 implemented them.

**Acceptance:** all eight checks hold. Then stop; the orchestrator opens the pull request.

---

## Test specifications (summary)

| Task | New tests | Changed tests |
|---|---|---|
| 1 | NA1–NA9 | — |
| 2 | RCP1–RCP10 | BI8 (`openai` row) |
| 3 | NO1–NO10 | — |
| 4 | SV7-1–SV7-7 | tests asserting the latest `user_version` (6 → 7) and "supported version 6" |
| 5 | RI1–RI7 | `LedgerIngestorTests` helper calculators, if needed |
| 6 | IRC1–IRC7 | `ImportCommandTests` table and JSON; guide samples; compaction amount assertions (fixture default charge) |
| 7 | CC1–CC9 | `CopilotCompactionTests` cases that expected failed or ID-less compactions to be unsupported |
| 8 | TL1–TL9, IP-AM1 | — |
| 9 | V115-1–V115-8 | gate row 1.0.115; "1.0.103 to 1.0.114" texts |
| 10 | RR1–RR11 | — |
| 11 | RL1–RL6 | tool-link store tests whose assertions name `InsertOwnedLinkAsync` behavior, if any |
| 12 | UD3, UD4 | — |

Exact values used across tasks:

| Value | What |
|---|---|
| 47,388,600 nano AIU | 0.0473886 AI credits = $0.000473886 = 473,886,000 pico-USD |
| 11,855,545,000 nano AIU | the fixture compaction's charge: 11.855545 AI credits = $0.11855545 = 118,555,450,000 pico-USD |
| 9,007,199,254,740,992 | 2^53, `NanoAiu.MaxExact`: $90,071.99254740992 |
| `0x418698BDC0000000` | 47388600.0 as IEEE-754 bits (wire bytes `00 00 00 C0 BD 98 86 41`) |
| 1 | `LedgerImporter.CurrentImportRevision` |
| 7 | the ledger format |
| 1.0.115 | `CopilotSessionLogSource.MaximumVersion` |

## Final step: the pull request (orchestrator, not a builder task)

After Task 13, the orchestrator pushes `swigerb-sprint-12-billing-import-fidelity` and opens one pull request titled "Sprint 12: Billing & import fidelity" with `Closes #76`, `Closes #82`, `Closes #85`, `Closes #79`, and `Closes #75`, and sections `## Changes` (the plan commit and each task commit), `## Verification` (build, test totals per task, Task 13's checks), `## Upgrade` (format 7, run `gh ledger import --all` once), and `## Risks` (A1–A3; a 0.5.0 daemon left running after the upgrade writes `list_cost_usd_pico` NULL, which reads as the rate card; circuit-breaker trips aren't re-evaluated after repricing). It then asks `ledger-architect` to review. Nobody bumps the version, tags, or releases in this sprint.

## Deferred to later sprints

- **Billing:** using a reported charge for `unknown_model` and `no_rate_for_date` GitHub requests (S12-1); showing list price and discount (Auto, Fusion) in reports, receipts, and MCP (S12-2); a `status` line for rejected charges, and logging them in the daemon's log (S12-3); re-evaluating breaker trips after repricing (S12-4); repricing live rows from `invoke_agent` totals (never; F1).
- **Import:** 1.0.115 BYOK usage, compaction, and `tool.execution_complete` shapes once observed (S12-5, A2); a parent chain that passes through `hook.*` or `assistant.turn_*` events (S12-6, L2); a stale imported link whose correct owner is out of scope or out of range in this run (S12-7); automatic acceptance of new Copilot versions (S12-8: a documented gate-update procedure stays manual); profiling the duplicate path on large stores (S12-9); live custom-provider spans of the GitHub Copilot app (S12-10).
- Everything Sprint 11 deferred, unchanged (see `.github/plans/archive/sprint-11.md`, "Deferred to later sprints").
