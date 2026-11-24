# Sprint 13: Scoped-Query Performance, the Windows Daemon, and Audit Quality

| | |
|---|---|
| Architect | `ledger-architect` |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET 10 (`net10.0`), SDK 10.0.x, zero Docker |
| Plan written | 2026-11-24, against `main` at `69ae671` (branch `swigerb-sprint-13-perf-windows-audit`) |
| Requirements | Issues [#83](https://github.com/swigerb/gh-ledger/issues/83) (scoped export, audit, and MCP audit are slow), [#78](https://github.com/swigerb/gh-ledger/issues/78) (the Windows daemon dies with a job-object shell), [#80](https://github.com/swigerb/gh-ledger/issues/80) (audit noise), and [#81](https://github.com/swigerb/gh-ledger/issues/81) (alerts name `gh-ledger`). None of them has comments. The live test report `docs/test-reports/2026-11-24-live-e2e-windows.md` found all four. |
| Status | Current sprint. Sprint 11 is complete and archived verbatim at `.github/plans/archive/sprint-11.md`. Sprint 12 runs at the same time on its own branch. |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification section, runs the verification commands, changes the line to `- [x]`, and stops. Tasks 1–8 are sequential, and each one builds on the ones before it.

**How this plan differs from Sprint 11's, archived at `.github/plans/archive/sprint-11.md`.** Sprint 11 gave every change as copy-exact code blocks generated from the files. This plan gives fixed **contracts** instead: exact public signatures, SQL, user-visible strings, documentation text, algorithms, and test vectors with exact expected values. You write the code that meets them.
- The strings, signatures, and expected values below are fixed. Copy them character for character.
- How you write a method body is up to you, within "Minimal diffs" in `.github/copilot-instructions.md`.
- If a contract looks wrong, or a test vector can't hold, stop and report it. Don't redesign it.
- Sprint 11 had a rule that no test opens a socket or starts a process. This plan has one exception, Task 4's DJ7, which is Windows-only (see Decision D4). Every other test stays in-process.

**Ground rules for every task:**
- **No ledger changes.** No schema or ledger-format change: no migration, no `user_version` bump, no new table, column, or index, and no `PRAGMA` change.
- **No version, rates, or package changes.** No change to `rates.json`, `docs/configuration.md`, the product version (0.5.0), `global.json`, `Directory.Build.props`, or any `.csproj`. No new NuGet package.
- **Money is `decimal`.** Never `double` or `float`.
- **Hermetic tests.** No Docker, no network outside loopback (and loopback only in DJ7), no `git`, and no real `~/.copilot-ledger`.
- **Plans and agents are off-limits.** No task edits `.github/` except its own checkbox.
- **Stay out of Sprint 12's areas.** Sprint 12 owns import, pricing, OTLP normalization, the schema, and the import and pricing sections of the docs. Don't touch:
  - `src/CopilotLedger.Core/Import/`, `src/CopilotLedger.Core/Pricing/`, `src/CopilotLedger.Otlp/Ingestion/`, or `src/CopilotLedger.Otlp/Receiver/`;
  - `LedgerMigrations.cs`, `SqliteLedgerStore.Imports.cs`, or `UsageAuthority.cs`;
  - these sections of `docs/cli.md`: "Import past sessions" and its subsections, "Reports", "API-equivalent USD", and "Rates".
- **Verify after every task.** Run `dotnet build CopilotLedger.slnx`, which must report 0 warnings and 0 errors (warnings are errors), and `dotnet test CopilotLedger.slnx`, which must pass exactly the task's test count with 0 failed and 0 skipped. Report the exact commands and counts.

If a vector fails, the build fails, or the test count differs from the task's, stop and report it. Don't change an expected value to make it pass.

Sprint 10's P28 race still exists. If a test fails only with `Dry run stopped: the ledger has a -wal or -shm file without the other, …`, run `dotnet test CopilotLedger.slnx` again. If it fails again, or anything else fails, stop and report it.

The final section, "Final step: the pull request", isn't a builder task. It has no checkbox, and `ledger-builder` never runs it.

## Tasks

- [x] Task 1: A Generated Large Ledger and an Opt-In Timing Harness, with "Before" Numbers (tests). #83. `LargeLedger` writes a 50,000-span, 75,000-tool-call ledger in two repositories. `ScopedQueryPerfTests.SQ1` is opt-in: it runs only when `GH_LEDGER_PERF=1` and otherwise returns at once. It times scoped and unscoped `audit`, `export --bundle`, and MCP `get_audit_findings`. Report the "before" numbers.
- [x] Task 2: Scoped Tool-Call Reads Select the Chains Once (CopilotLedger.Storage & tests). #83. `ExportToolCallWhereSql` replaces its correlated `EXISTS` with an uncorrelated `trace_id IN (SELECT trace_id FROM spans WHERE …)`, with no index or schema change. SQ2 is an always-on, bounded-time guard with exact counts. SQ1 gains its ratio assertions. Report the "after" numbers.
- [x] Task 3: Detecting the Windows Job, and Breaking Away When It Allows (CopilotLedger.Cli & tests). #78.
  - New types: `JobState`, `ICliPlatform.GetJobState()`, `DetachedLaunch`, and `DetachedLaunchPolicy`.
  - `WindowsJobObjects` uses `IsProcessInJob` and `QueryInformationJobObject` through `DllImport`.
  - `StartDetached` adds `CREATE_BREAKAWAY_FROM_JOB` when the immediate job sets `JOB_OBJECT_LIMIT_BREAKAWAY_OK`.
- [x] Task 4: `daemon start` and `install` Refuse Instead of Starting a Daemon That Would Die (CopilotLedger.Cli, docs & tests). #78.
  - The refusal message covers both commands.
  - The daemon logs a warning when its own job would kill it.
  - DJ7 is a Windows-only real-process test of a `KILL_ON_JOB_CLOSE` job, with and without breakaway.
  - Update `docs/cli.md`: "Install and uninstall", "The daemon", and "Troubleshooting".
- [ ] Task 5: Alerts Name the Command the Way gh-ledger Runs (CopilotLedger.Otlp, CopilotLedger.Cli, docs & tests). #81. Breaker and budget alerts say `Run: gh ledger …` when the daemon runs as the GitHub CLI extension, and `Run: gh-ledger …` otherwise, through the existing `GhExtension.CommandName`. Update the guide's `budget-alert` sample and the "Circuit breaker" section of `docs/cli.md`.
- [ ] Task 6: `context_bloat` Reports Once per Invocation, and `tool_loop` Is High Only When It Costs Something (CopilotLedger.Core, CopilotLedger.Cli & tests). #80.
  - `context_bloat` keeps its per-request trigger but reports one finding per agent invocation, model, and output kind, and gains a `requests` metric.
  - `tool_loop` is High only when the run is long enough and its known impact is above $0.
- [ ] Task 7: Per-Rule Totals in the Audit Table, and the Docs (CopilotLedger.Cli, docs & tests). #80. The text table gains a `By rule:` line. Update the `docs/cli.md` rule table and "Output" paragraph, and the guide's `audit` sample.
- [ ] Task 8: Docs Consistency and Final Verification (tests). All four issues.
  - DC1 checks the docs that this sprint changed.
  - Run the opt-in timing harness once more.
  - Publish a single-file `win-x64` binary and smoke-test the job check.
  - Run the full build and test suite.

## Starting point

- **Repository state.** `main` is at `69ae671`, "Correct and extend the live end-to-end test report (#86)", and this branch, `swigerb-sprint-13-perf-windows-audit`, starts there. At `69ae671`, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors, and `dotnet test CopilotLedger.slnx` passes **1740** tests (0 failed, 0 skipped). The architect measured both on Windows with SDK 10.0.115.
- **Version and rates.** The product version is 0.5.0 (`src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`) and stays 0.5.0. `rates.json` doesn't change.
- **Sprint 12** (#85, #76, #82, #79, #75) runs concurrently on its own branch, with no commits beyond `main` when this plan was written. Where this sprint depends on it:
  - **#85 (link imported tool calls by `apiCallId`).** #85 changes which request an imported tool call attaches to (`owner_span_row_id`). Task 2's SQL keeps `EffectiveLinkSql` and the owner join unchanged, so either sprint can merge first. Task 6's `tool_loop` cap doesn't read owners: it reads the tool-call sequence and the requests between the calls by time (`ToolSequences.Between`).
  - **#76 (record `nano_aiu`/`totalNanoAiu` as the charge).** #76 can turn some imported impacts from unknown or $0 into real amounts. After both sprints merge, `tool_loop` is High again exactly when such a run is long and costs something, which is the intent.
  - **Schema.** If Sprint 12 adds columns to `spans` or `span_links`, `LargeLedger`'s raw `INSERT`s still work as long as the new columns are nullable or have defaults. If a merge breaks them, the fix is to add the new column to the `INSERT`, not to change this plan's queries.
- **What this plan relies on, read from the code at `69ae671`:**
  - **Tool-call reads (#83).** `SqliteLedgerStore.Export.cs:28–34` (`ExportToolCallWhereSql`) is the only tool-call `WHERE` clause with the correlated `EXISTS`. Four reads use it:
    - export's page read (`:97`);
    - its latest-first read (`:118`);
    - its count (`:135`);
    - audit's tool-call read (`SqliteLedgerStore.Audit.cs:206`).

    Audit's agent-link reads (`Audit.cs:82` and `:269`) and the export coverage read (`Export.cs:294`) already use the uncorrelated `trace_id IN (SELECT trace_id FROM spans WHERE {FilterSql})`. MCP `get_audit_findings` calls the same snapshot reads as `audit`.
  - **Detached start (#78).** `WindowsDetachedProcess.Start` passes `CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT` (`WindowsDetachedProcess.cs:82`).
    - `DaemonCommands.StartAsync` (`DaemonCommands.cs:58–119`) runs these steps in order: the `IsHeld` check, the executable-name check, `Directory.CreateDirectory`, `StartDetached`, then a poll for `daemon.json`.
    - `install` calls `DaemonCommands.StartAsync` (`InstallCommands.cs:309–312`). It then always prints `gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.` and returns `StartAsync`'s exit code.
  - **Alert text (#81).** `BreakerMessages.ForTrip` and `ForBudget` (`src/CopilotLedger.Otlp/Breaker/BreakerMessages.cs:43` and `:56`) hard-code `Run: gh-ledger …`.
    - `GhExtension.CommandName(ICliPlatform)` (`src/CopilotLedger.Cli/Install/GhExtension.cs:39`) already returns `"gh ledger"` for the extension's binary and `"gh-ledger"` otherwise.
    - `DaemonHost.RunAsync` is the only code that constructs `AlertDispatcher` (`DaemonHost.cs:94`) and `CircuitBreakerMonitor` (`:98`).
    - `AlertDispatcher` builds alerts in `EnqueuePendingAsync` (`AlertDispatcher.cs:54`, `:67`), and `CircuitBreakerMonitor` builds them at `:131` and `:146`.
  - **Audit rules (#80).** `ContextBloatRule` emits one finding per request. `ToolLoopRule`'s severity is `AuditMath.Severity(run.Count, trigger)`, which is High at 2 × trigger whatever the impact. `AuditTable.Write` prints the totals line from `report.Findings`, which holds every finding, not only the ones shown.
  - **Tests.** xunit 2.9.3 can't skip a test dynamically, so platform-only tests return early, the way `StartDetachedTests.SD1` does. The test project already uses `Microsoft.Data.Sqlite` directly (for example in `Cli/AuditPrivacyTests.cs`). The test output directory contains the framework-dependent `gh-ledger.exe` apphost that the CLI project builds.

## Verified facts (retrieved 2026-11-24)

| Fact | Source | Used by |
|---|---|---|
| A correlated subquery "is reevaluated each time its result is required. An uncorrelated subquery is evaluated only once and the result reused as necessary." | [SQLite: SQL Language Expressions, §12 Correlated Subqueries](https://www.sqlite.org/lang_expr.html#correlated_subqueries) | Task 2 |
| `CREATE_BREAKAWAY_FROM_JOB` is `0x01000000`: "The child processes of a process associated with a job are not associated with the job. If the calling process is not associated with a job, this constant has no effect. If the calling process is associated with a job, the job must set the JOB_OBJECT_LIMIT_BREAKAWAY_OK limit." | [Process Creation Flags](https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags) | Tasks 3, 4 |
| With `JOB_OBJECT_LIMIT_BREAKAWAY_OK`, a child created with `CREATE_BREAKAWAY_FROM_JOB` isn't in the job. With `JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK`, every child is outside the job without the flag. "Child processes created using Win32_Process.Create are not associated with the job." For nested jobs, the parents' breakaway settings decide which job of the hierarchy the child joins. | [Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects) | Tasks 3, 4, D3 |
| In a nested hierarchy, a breakaway leaves the job and its parents up the hierarchy until it reaches a job that doesn't allow breakaway, and the process stays in that job. | [Nested Jobs](https://learn.microsoft.com/en-us/windows/win32/procthread/nested-jobs) | D3 |
| `JOB_OBJECT_LIMIT_BREAKAWAY_OK` = `0x00000800`, `JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK` = `0x00001000`, `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` = `0x00002000`. | [JOBOBJECT_BASIC_LIMIT_INFORMATION](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_limit_information) | Tasks 3, 4 |
| `JOBOBJECT_BASIC_LIMIT_INFORMATION` is, in order: `LARGE_INTEGER PerProcessUserTimeLimit`, `LARGE_INTEGER PerJobUserTimeLimit`, `DWORD LimitFlags`, `SIZE_T MinimumWorkingSetSize`, `SIZE_T MaximumWorkingSetSize`, `DWORD ActiveProcessLimit`, `ULONG_PTR Affinity`, `DWORD PriorityClass`, `DWORD SchedulingClass`. | same page | Task 3 |
| `JOBOBJECT_EXTENDED_LIMIT_INFORMATION` is, in order: `BasicLimitInformation`, `IoInfo` (`IO_COUNTERS`, six `ULONGLONG`), `SIZE_T ProcessMemoryLimit`, `SIZE_T JobMemoryLimit`, `SIZE_T PeakProcessMemoryUsed`, `SIZE_T PeakJobMemoryUsed`. | [JOBOBJECT_EXTENDED_LIMIT_INFORMATION](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_extended_limit_information) | Task 3 |
| `QueryInformationJobObject(hJob, …)`: if `hJob` is NULL and the calling process is associated with a job, the job associated with the calling process is used; if the job is nested, the immediate job of the calling process is used. `JobObjectExtendedLimitInformation` is class 9. | [QueryInformationJobObject](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-queryinformationjobobject) | Task 3 |
| `IsProcessInJob(ProcessHandle, NULL, &result)` tests whether the process runs under any job. A process can't open its own job without its name, but `QueryInformationJobObject` with NULL works on it. | [IsProcessInJob](https://learn.microsoft.com/en-us/windows/win32/api/jobapi/nf-jobapi-isprocessinjob) | Task 3 |
| `PublishSingleFile` is on for every RID build, with `EnableSingleFileAnalyzer` (`src/CopilotLedger.Cli/CopilotLedger.Cli.csproj:10–17`). The existing `kernel32.dll` `DllImport`s in `WindowsDetachedProcess` already ship in the single-file binary, so new `kernel32.dll` imports need nothing more. | the repository at `69ae671` | Task 3, Task 8 |

**Assumptions (not verified; the design doesn't depend on them):**

| # | Assumption | Why it doesn't matter |
|---|---|---|
| A1 | `CreateProcess` with `CREATE_BREAKAWAY_FROM_JOB` fails (access denied) when the immediate job doesn't allow breakaway. | gh-ledger adds the flag only when the immediate job sets `BREAKAWAY_OK` (Task 3, rule 3). |
| A2 | The GitHub Copilot app's agent shells run in a job with `KILL_ON_JOB_CLOSE` and without breakaway. | This comes from #78's evidence, measured on one machine. The design reads the job's flags at run time and doesn't name any host. |
| A3 | Sprint 12 adds only nullable or defaulted columns. | See "Starting point". If it doesn't, `LargeLedger`'s `INSERT`s need the new column, and nothing else changes. |

## Decisions

- **D1: #83 without an index.** Fix #83 by making the subquery uncorrelated, not with a `TEMP` table and not with an index.
  - SQLite evaluates an uncorrelated `IN (SELECT …)` once per statement (see Verified facts). On SQLite 3.53.3, the version that the repository's `Microsoft.Data.Sqlite` bundles, the architect's `EXPLAIN QUERY PLAN` of the new count shows `SEARCH span_links USING PRIMARY KEY (trace_id=?)` driven by `LIST SUBQUERY 2` (one `SCAN spans` for the selected traces), and no correlated scan of `spans` by trace.
  - The architect's prototype ran the same SQL against ledgers created by this repository's migrations:

    | Ledger | Old SQL | New SQL |
    |---|---|---|
    | 50,000 spans and 75,000 tool calls in 192 traces (Task 1's shape) | repository- and conversation-scoped count, page, and latest reads took **4.4–9.6 s** each | **8–69 ms** |
    | SQ2's guard shape | **≈12.3 s** per scoped call | **under 10 ms** |

  - Results were identical.
  - A `TEMP` table would write to the temp schema inside a read transaction, and an index would change the ledger. D1 needs neither.
- **D2: SQ1 is opt-in, SQ2 is always on.** A 50,000-span ledger takes seconds to build, so the full-size timing harness runs only with `GH_LEDGER_PERF=1`.
  - SQ2 is small enough for every run: about 16,000 spans and 16,000 links in one transaction.
  - SQ2 asserts exact counts, and a 3-second bound per scoped call. The old SQL needs ≈12 s and the new SQL needs <10 ms, so the bound is far from both and isn't flaky.
  - Reading the environment variable `GH_LEDGER_PERF` keeps the suite hermetic: it touches no file outside the test's temp directory.
- **D3: #78 refuses instead of using a broker.** When gh-ledger can't break away, it refuses with a message. Brokers were considered and rejected:
  - WMI `Win32_Process.Create` (whose children aren't in the caller's job) needs COM interop through `System.Management`, a NuGet package, or hand-written COM.
  - A Task Scheduler task changes what `install` registers.

  Both are larger than #78's acceptance criteria require, and the issue offers refusal as an option.
  - **Residual risk.** In a nested hierarchy, a breakaway can land the daemon in an outer job that kills on close (see Nested Jobs). The daemon can't prevent that, so it logs a `Warning:` line at start when its own job would kill it.
  - **Deferred.** `status` doesn't show that warning yet; see Deferred.
- **D4: DJ7 starts real processes and listens on loopback.** #78's first acceptance criterion requires it.
  - **Scope.** DJ7 runs only on Windows and returns at once elsewhere.
  - **Processes.** It starts the test output's own `gh-ledger.exe`, with `COPILOT_LEDGER_HOME` set to a temp directory, in a job that the test creates. The daemon that it starts listens on a free `127.0.0.1` port and receives no traffic.
  - **Cleanup.** The test stops the daemon in a `finally` block.
  - **Rules.** This is the only exception to "no test opens a socket or starts a process" in this plan. `.github/copilot-instructions.md` forbids network access and the real `~/.copilot-ledger`, and DJ7 uses neither.
- **D5: `context_bloat` aggregates, and keeps its trigger.** The per-request trigger stays: at least 50,000 input tokens, and at least 200 times as much input as output, or no output.
  - **Uncached weighting rejected.** Weighting uncached input would stop seed B (260,000 input, 200,000 cache read, 1,000 output) from triggering, and would change several pinned samples. That's deferred.
  - **Adding `requests` isn't a format change.** The `requests` metric is a new entry in the open `metrics` list. `telemetry/audit-findings.json`'s documented fields (`name`, `value`, `unit`) don't change, and neither does its format version.
- **D6: The `tool_loop` cap.** `tool_loop` is High only when `run.Count >= 2 × trigger` **and** the impact is known and greater than $0. Every other triggered run is a Warning. `failed_tool_retry` doesn't change: its High is tied to the circuit breaker's churn setting.
- **D7: The `premium_model_trivial_turn` routed exemption is deferred.** The ledger has no routed-model signal. No column records `isAuto` or `fusion.*`, and `src` has no handling for either. Adding one is a schema change, and Sprint 12's #76 may add the signal. See Deferred.
- **D8: Per-rule totals appear in text only.** `--json`, MCP, and the bundle's `audit-findings.json` are formats, so they don't change. JSON per-rule totals are deferred.
- **D9: #81 uses `GhExtension.CommandName`.** The daemon's `ProcessPath` is the extension's binary when the extension starts it, so the name matches how the user installed gh-ledger. `BreakerMessages` keeps `gh-ledger` as the default, so code outside the daemon, and the tests that pin it, don't change.

## Task 1: A Generated Large Ledger and an Opt-In Timing Harness, with "Before" Numbers

**Files:**
- New: `tests/CopilotLedger.Tests/TestSupport/LargeLedger.cs`
- New: `tests/CopilotLedger.Tests/Storage/ScopedQueryPerfTests.cs`

No `src` change.

### Contract: `LargeLedger`

```csharp
namespace CopilotLedger.Tests.TestSupport;

public static class LargeLedger
{
    public const string BigRepository = "contoso/big";
    public const string GhLedgerRepository = "swigerb/copilot-ledger";   // GitFixture's repository, so MCP's default scope selects it
    public const string Conversation = "conv-0003";                     // one 200-span trace in GhLedgerRepository
    public static readonly DateTimeOffset Base = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    // Creates the ledger at databasePath (via SqliteLedgerStore.InitializeAsync, then raw INSERTs in one transaction) and
    // returns the store. Deterministic: the same file contents on every run.
    public static Task<SqliteLedgerStore> CreateAsync(string databasePath);
}
```

**Generation algorithm** (copy the SQL exactly; it's the prototype that D1 measured):

1. `SqliteLedgerStore store = new(databasePath); await store.InitializeAsync();`
2. Open `new SqliteConnection($"Data Source={databasePath}")` and begin one transaction.
3. For each trace `t` in `0..191`:
   - **Shape.** `spans = t < 2 ? 6000 : 200` and `calls = t < 2 ? 9000 : 300`.
   - **IDs and scope.** `trace = t.ToString("x32")`, `conv = $"conv-{t:D4}"`, and `repo = t % 2 == 0 ? BigRepository : GhLedgerRepository`.
   - **Source.** `imported = t % 2 == 0`.
   - **Start.** `start = Base.ToUnixTimeMilliseconds() + t * 1_800_000L` (traces 30 minutes apart; all within 4 days of `Base`, so inside the 7-day tool history at `Now`).
   - **Spans.** For `i` in `0..spans-1`, run the span `INSERT` with:

     | Parameter | Value |
     |---|---|
     | `$t` | `trace` |
     | `$s` | `(i + 1).ToString("x16")` |
     | `$c` | `conv` |
     | `$st` | `start + i * 100L` |
     | `$r` | `repo` |
     | `$src` | `imported ? "import:copilot" : "otlp"` |
     | `$n` | `imported ? (i % 2 == 0 ? 1 : 2) : DBNull.Value` |

     Keep the returned `id` in `ids[i]`.
   - **Tool calls.** For `j` in `0..calls-1`, run the link `INSERT` with:

     | Parameter | Value |
     |---|---|
     | `$t` | `trace` |
     | `$s` | `"a" + j.ToString("x15")` |
     | `$c` | `conv` |
     | `$st` | `start + j * 60L + 30` |
     | `$o` | `imported ? ids[(int)((long)j * spans / calls)] : DBNull.Value` |
4. Commit. Totals: 50,000 spans and 75,000 tool calls in 192 traces. The two largest traces, `t` = 0 in `contoso/big` and `t` = 1 in `swigerb/copilot-ledger`, each have 6,000 spans and 9,000 tool calls.

```sql
INSERT INTO spans (trace_id, span_id, operation_name, conversation_id, request_model, model_id, start_time_unix_ms, end_time_unix_ms,
    input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens, billing_channel, pricing_status, rates_version,
    cost_usd_pico, saved_usd_pico, repo_slug, branch_name, recorded_at_unix_ms, cache_read_state, cache_write_state,
    ingest_source, imported_tool_call_count)
VALUES ($t, $s, 'chat', $c, 'claude-sonnet-4.6', 'claude-sonnet-4.6', $st, $st + 50, 60000, 200, 57000, 0, 'github_ai_credits', 'priced', '2026-11-23',
    1000000000, 0, $r, 'main', $st, 'reported', 'absent', $src, $n)
RETURNING id;
```

```sql
INSERT INTO span_links (trace_id, span_id, operation_name, conversation_id, start_time_unix_ms, recorded_at_unix_ms, tool_name, tool_exit_code, status_code, owner_span_row_id)
VALUES ($t, $s, 'execute_tool', $c, $st, $st, 'view', 0, 0, $o);
```

Prepare each command once and reuse it, setting parameter values per row (`SqliteType.Text` and `SqliteType.Integer`).

### Contract: `ScopedQueryPerfTests.SQ1_LargeLedger_ScopedReadsTakeAtMostTwiceTheUnscoped`

`public sealed class ScopedQueryPerfTests(ITestOutputHelper output)` in namespace `CopilotLedger.Tests.Storage`, with one `[Fact]`:

1. If `Environment.GetEnvironmentVariable("GH_LEDGER_PERF") != "1"`, return at once. The test then passes and touches nothing.
2. **Setup.**
   - `using TempDirectory temp = new();` then `GitFixture.Create(temp.Path)`.
   - `await LargeLedger.CreateAsync(Path.Combine(temp.Path, "data", "ledger.db"))`. Create the `data` directory first.
   - `ManualTimeProvider clock = new(LargeLedger.Now, TimeZoneInfo.Utc)`, and `await using CliHarness cli = new(temp.Path, new FakeCliPlatform(), clock)`.
3. **Measure.** Time each of these with `Stopwatch` and write one line per run through `output.WriteLine`, formatted exactly `"{label,-28} {exitCode} {elapsedMs} ms"`. Each command must exit 0.

   | Label | Command |
   |---|---|
   | `audit unscoped` | `cli.RunAsync("audit", "--since", "all", "--json")` |
   | `audit repo` | `cli.RunAsync("audit", "--since", "all", "--json", "--repo", LargeLedger.GhLedgerRepository)` |
   | `export unscoped` | `cli.RunAsync("export", "--all-repos", "--since", "all", "--bundle", "--out", "all.html")` |
   | `export repo` | `cli.RunAsync("export", "--repo", LargeLedger.GhLedgerRepository, "--since", "all", "--bundle", "--out", "repo.html")` |
   | `export conversation` | `cli.RunAsync("export", "--all-repos", "--conversation", LargeLedger.Conversation, "--since", "all", "--bundle", "--out", "conv.html")` |
   | `mcp get_audit_findings default` | an `McpTestHarness` built the way `Mcp/AuditMcpTests.cs`'s private `Context(root)` builds its `LedgerMcpContext`, but with `TimeProvider = clock`, calling `get_audit_findings` with arguments `{}`. The default scope is `swigerb/copilot-ledger` (GitFixture). The result's `IsError` must not be `true`; treat that as exit 0. |
4. **Task 1 makes no timing assertion.** Run it once, before Task 2 changes anything:

   ```shell
   $env:GH_LEDGER_PERF='1'; dotnet test CopilotLedger.slnx --filter "FullyQualifiedName~ScopedQueryPerfTests" --logger "console;verbosity=detailed"
   ```

   Report the six printed lines as the **"before"** numbers in your task report. Expected: the scoped runs are many times slower than the unscoped ones. The architect's prototype took seconds per scoped tool-call read.

**AC:**
- `LargeLedger` and SQ1 exist as specified, and no `src` file changes.
- Without `GH_LEDGER_PERF`, SQ1 passes at once.
- The task report includes the six "before" lines.

**Verify:** `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors. `dotnet test CopilotLedger.slnx` passes **1741** tests (0 failed, 0 skipped), without `GH_LEDGER_PERF` set.

## Task 2: Scoped Tool-Call Reads Select the Chains Once

**Files:**
- `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`
- `tests/CopilotLedger.Tests/Storage/ScopedQueryPerfTests.cs`

### Change (exact)

In `SqliteLedgerStore.Export.cs`, replace the comment and the constant at lines 25–34 with:

```csharp
    // The execute_tool links that count, start in the filter's period, and belong to a prompt chain with a selected span. The
    // subquery is uncorrelated, so SQLite evaluates it once per statement and looks each link's trace up in its result; a
    // correlated EXISTS tested FilterSql on every span of every unselected trace, once per link (#83). Unqualified columns
    // are span_links' outside the subquery and spans' inside it.
    private const string ExportToolCallWhereSql = $"""
        operation_name = 'execute_tool'
          AND {EffectiveLinkSql}
          AND (@from IS NULL OR start_time_unix_ms >= @from)
          AND (@to IS NULL OR start_time_unix_ms < @to)
          AND span_links.trace_id IN (SELECT trace_id FROM spans WHERE {FilterSql})
        """;
```

Change nothing else in Storage. No index, no `TEMP` table, no migration.

### Tests

**SQ2_ScopedToolCallReads_AreExactAndBounded** (`[Fact]`, always runs):

1. **Setup.** `using TempDirectory temp = new();` Create `<temp>/ledger.db` with `SqliteLedgerStore.InitializeAsync()`.
2. **Insert, in one transaction,** with the same two `INSERT` statements as `LargeLedger`, but with `imported_tool_call_count` NULL, `ingest_source` `'otlp'`, and `owner_span_row_id` NULL. Use `base = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()`.

   | Trace | `trace_id` | Spans | Tool calls | Conversation | Repository |
   |---|---|---|---|---|---|
   | 0 | `0.ToString("x32")` | 8,000 | 8,000 | `conv-big` | `contoso/big` |
   | 1 | `1.ToString("x32")` | 10 | 15 | `conv-small` | `swigerb/gh-ledger` |

   - Span `i` (0-based) of trace `t` has `span_id = (i + 1).ToString("x16")` and starts at `base + t * 3_600_000L + i * 100L`.
   - Link `j` of trace `t` has `span_id = "a" + j.ToString("x15")` and starts at `base + t * 3_600_000L + j * 60L + 30`.
3. **Read.** Open `await store.OpenExportSnapshotAsync()` and, for each filter below, time two calls with `Stopwatch`:
   - `CountToolCallsAsync(filter)`;
   - `GetAuditInputAsync(filter, new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero))`.
4. **Expected values:**

   | Filter | Count | Audit spans | Audit tool calls |
   |---|---|---|---|
   | `new LedgerFilter { ConversationId = "conv-small" }` | 15 | 10 | 15 |
   | `new LedgerFilter { Repository = "swigerb/gh-ledger" }` | 15 | 10 | 15 |
   | `new LedgerFilter()` | 8015 | 8010 | 8015 |
5. **Time bound.** Each of the two scoped filters' calls takes **less than 3 seconds**. Assert `elapsed < TimeSpan.FromSeconds(3)` per call. The unfiltered row has no time bound.

Before the fix, the scoped calls took about 12 s each, so SQ2 fails against the old SQL. Check that once: revert the change locally, run `dotnet test --filter SQ2`, then restore the change. Say in your report that you did.

**SQ1 gains its assertions.** After writing the six lines, assert:
- `audit repo` ≤ 2 × `audit unscoped` + 1,000 ms;
- `export repo` ≤ 2 × `export unscoped` + 1,000 ms;
- `export conversation` ≤ 2 × `export unscoped` + 1,000 ms;
- `mcp get_audit_findings default` < 5,000 ms.

These are #83's acceptance criteria. The +1,000 ms slack absorbs the fixed costs of process-level work on a small result. Then run the opt-in command from Task 1 again, and report the six lines as the **"after"** numbers next to Task 1's "before" numbers.

**AC:**
- The SQL is exactly as above.
- No existing test changes, and every existing export, audit, and MCP test passes unchanged (#83's second acceptance criterion).
- SQ2 passes, and it failed against the old SQL.
- With `GH_LEDGER_PERF=1`, SQ1 passes and shows the scoped runs within 2× (+1 s) of the unscoped ones, and MCP's default scope answers in under 5 s.

**Verify:** `dotnet build CopilotLedger.slnx` reports 0 warnings. `dotnet test CopilotLedger.slnx` passes **1742** tests (0 failed, 0 skipped).

## Task 3: Detecting the Windows Job, and Breaking Away When It Allows

**Files:**
- New: `src/CopilotLedger.Cli/JobState.cs`
- New: `src/CopilotLedger.Cli/DetachedLaunchPolicy.cs`
- New: `src/CopilotLedger.Cli/WindowsJobObjects.cs`
- Edit: `src/CopilotLedger.Cli/ICliPlatform.cs`, `src/CopilotLedger.Cli/SystemCliPlatform.cs`, `src/CopilotLedger.Cli/WindowsDetachedProcess.cs`, and `tests/CopilotLedger.Tests/TestSupport/FakeCliPlatform.cs`
- New: `tests/CopilotLedger.Tests/Cli/DetachedLaunchPolicyTests.cs`

### Contracts (exact)

```csharp
// src/CopilotLedger.Cli/JobState.cs
namespace CopilotLedger.Cli;

// The Windows job object that this process runs in: whether it's in one, and its immediate job's LimitFlags
// (JOBOBJECT_BASIC_LIMIT_INFORMATION). (false, 0) outside a job and on other platforms.
public readonly record struct JobState(bool InJob, uint LimitFlags);
```

```csharp
// src/CopilotLedger.Cli/DetachedLaunchPolicy.cs
namespace CopilotLedger.Cli;

public enum DetachedLaunch
{
    Independent,        // start it normally: no job, a job that lets every child leave, or one that doesn't kill on close
    BreakawayFromJob,   // add CREATE_BREAKAWAY_FROM_JOB: the job allows breakaway when asked
    Blocked,            // the job kills its processes when it closes and won't let the child leave
}

public static class DetachedLaunchPolicy
{
    public const uint BreakawayOk = 0x00000800;         // JOB_OBJECT_LIMIT_BREAKAWAY_OK
    public const uint SilentBreakawayOk = 0x00001000;   // JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK
    public const uint KillOnJobClose = 0x00002000;      // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE

    public static DetachedLaunch Decide(JobState state);
    public static bool DiesWithJob(JobState state);     // state.InJob && (state.LimitFlags & KillOnJobClose) != 0
}
```

`Decide` applies these rules in order; the first that matches wins:
1. `!state.InJob` → `Independent`.
2. `(flags & SilentBreakawayOk) != 0` → `Independent`, because children leave without the flag.
3. `(flags & BreakawayOk) != 0` → `BreakawayFromJob`.
4. `(flags & KillOnJobClose) != 0` → `Blocked`.
5. Otherwise → `Independent`, because the job doesn't kill its processes when it closes.

**`ICliPlatform`.** Add this member after `StartDetached`:

```csharp
    JobState GetJobState();                                                // Windows: this process's job (IsProcessInJob, then QueryInformationJobObject with a NULL job); (false, 0) on other platforms; throws Win32Exception when a call fails
```

**`WindowsJobObjects`.** In `src/CopilotLedger.Cli/WindowsJobObjects.cs`:
- `[SupportedOSPlatform("windows")] internal static class WindowsJobObjects` with `public static JobState GetCurrent()`.
- Declare the imports in the same style as `WindowsDetachedProcess`: `[DllImport("kernel32.dll", SetLastError = true)]` and `[return: MarshalAs(UnmanagedType.Bool)]`.
  - `GetCurrentProcess()`, which returns `IntPtr`.
  - `IsProcessInJob(IntPtr process, IntPtr job, [MarshalAs(UnmanagedType.Bool)] out bool result)`.
  - `QueryInformationJobObject(IntPtr job, int informationClass, out JobObjectExtendedLimitInformation information, int length, IntPtr returnLength)`.
- Define private `LayoutKind.Sequential` structs that mirror the verified layouts:
  - `long`, `long`, `uint`, `nuint`, `nuint`, `uint`, `nuint`, `uint`, `uint` for the basic struct;
  - six `ulong`s for `IO_COUNTERS`;
  - four `nuint`s after `IoInfo` in the extended struct.

`GetCurrent()` follows these steps:
1. If `!IsProcessInJob(GetCurrentProcess(), IntPtr.Zero, out bool inJob)`, throw `new Win32Exception()`.
2. If `!inJob`, return `new JobState(false, 0)`.
3. If `!QueryInformationJobObject(IntPtr.Zero, 9, out var info, Marshal.SizeOf<JobObjectExtendedLimitInformation>(), IntPtr.Zero)`, throw `new Win32Exception()`.
4. Return `new JobState(true, info.BasicLimitInformation.LimitFlags)`.

**`SystemCliPlatform`.**
- `GetJobState()` returns `OperatingSystem.IsWindows() ? WindowsJobObjects.GetCurrent() : default`.
- `StartDetached` on Windows becomes `WindowsDetachedProcess.Start(fileName, arguments, workingDirectory, DetachedLaunchPolicy.Decide(GetJobState()) == DetachedLaunch.BreakawayFromJob)`.
- `StartDetached` doesn't refuse. Task 4 adds the refusal in `DaemonCommands`. `StartDetached`'s `ICliPlatform` signature doesn't change.

**`WindowsDetachedProcess.Start`** gains a fourth parameter, `bool breakawayFromJob`. Add `private const uint CreateBreakawayFromJob = 0x01000000;`. Then:
- Pass `CreateNoWindow | ExtendedStartupInfoPresent | (breakawayFromJob ? CreateBreakawayFromJob : 0)` as the creation flags.
- Add one comment line saying that a job that kills on close would otherwise stop the daemon with the caller (#78).

**`FakeCliPlatform`** gains these members, and `GetJobState()` throws `JobStateException` when it's set, otherwise it returns `JobState`:

```csharp
    public JobState JobState { get; set; }                 // default (false, 0)
    public Exception? JobStateException { get; set; }
    public JobState GetJobState() => JobStateException is { } exception ? throw exception : JobState;
```

### Tests (`DetachedLaunchPolicyTests`)

**DL1_Decide** (`[Theory]`, 8 cases, `[InlineData(bool inJob, uint flags, DetachedLaunch expected)]`):

| Case | `inJob` | `flags` | Expected |
|---|---|---|---|
| 1 | `false` | `0x0` | `Independent` |
| 2 | `false` | `0x2000` | `Independent` |
| 3 | `true` | `0x0` | `Independent` |
| 4 | `true` | `0x2000` | `Blocked` |
| 5 | `true` | `0x2800` | `BreakawayFromJob` |
| 6 | `true` | `0x3000` | `Independent` |
| 7 | `true` | `0x3800` | `Independent` |
| 8 | `true` | `0x0800` | `BreakawayFromJob` |

**DL2_DiesWithJob** (`[Theory]`, 4 cases):

| Case | `inJob` | `flags` | Expected |
|---|---|---|---|
| 1 | `false` | `0x2000` | `false` |
| 2 | `true` | `0x2000` | `true` |
| 3 | `true` | `0x2800` | `true` |
| 4 | `true` | `0x0` | `false` |

**DL3_SystemPlatform_ReadsThisProcessesJob** (`[Fact]`): `JobState state = new SystemCliPlatform().GetJobState();` must not throw.
- Off Windows, assert `state == default`.
- On Windows, if `!state.InJob`, assert `state.LimitFlags == 0u`. The test host may or may not be in a job, so assert nothing more.

**AC:**
- The contracts are exactly as above.
- No NuGet package is added.
- The existing `StartDetachedTests.SD1` passes unchanged.
- The `ICliPlatform.StartDetached` signature is unchanged.

**Verify:** `dotnet build CopilotLedger.slnx` reports 0 warnings, including the single-file analyzer. `dotnet test CopilotLedger.slnx` passes **1755** tests: 1742 + 8 + 4 + 1.

## Task 4: `daemon start` and `install` Refuse Instead of Starting a Daemon That Would Die

**Files:**
- Edit: `src/CopilotLedger.Cli/Daemon/DaemonCommands.cs`, `src/CopilotLedger.Cli/Daemon/DaemonHost.cs`, and `docs/cli.md`
- New: `tests/CopilotLedger.Tests/Cli/DaemonJobTests.cs`
- New: `tests/CopilotLedger.Tests/TestSupport/WindowsJobHarness.cs`

`InstallCommands.cs` doesn't change: `install` already goes through `DaemonCommands.StartAsync`.

### Strings (exact)

`{cmd}` is `GhExtension.CommandName(context.Platform)`.

| Name | Text |
|---|---|
| R1 | `gh-ledger daemon not started: this terminal runs in a Windows job that stops the processes it started when it closes, so the daemon would stop with it.` |
| R1e | `gh-ledger daemon not started: can't read this terminal's Windows job, so the daemon might stop when the terminal closes ({ex.Message}).` |
| R2a | `Run {cmd} daemon start from a regular terminal.` |
| R2b | `Run {cmd} daemon start from a regular terminal, or sign out and back in to start it from the sign-in item.` |
| W1 | `Warning: this daemon runs in a Windows job that stops its processes when the job closes, so it stops when the program that started it closes. Run {cmd} daemon start from a regular terminal.` |

### `DaemonCommands.StartAsync`

Insert the refusal after the executable-name check and before `Directory.CreateDirectory(context.DataDirectory)`:

1. Read the job state:

   ```csharp
   JobState job;
   string? jobError = null;
   try { job = context.Platform.GetJobState(); }
   catch (Win32Exception ex) { job = default; jobError = ex.Message; }
   ```

2. If `jobError is not null || DetachedLaunchPolicy.Decide(job) == DetachedLaunch.Blocked`:
   - Write two lines to `context.Error`: first R1, or R1e when `jobError` isn't null; then R2b if `context.Platform.GetStartupCommand("CopilotLedger") is not null`, otherwise R2a.
   - Return `1`.
   - Don't create the data directory, delete `daemon.json`, or call `StartDetached`.

Everything else in `StartAsync` is unchanged. The `"CopilotLedger"` sign-in item name is the one `install` already uses (`docs/cli.md`, "Install and uninstall").

### `DaemonHost.RunAsync`

After `DaemonFiles.AcquireLock` succeeds, and before `RateCardFiles.LoadBase`, add:

```csharp
try
{
    if (DetachedLaunchPolicy.DiesWithJob(context.Platform.GetJobState()))
    {
        WriteLog(writer, context, $"Warning: this daemon runs in a Windows job that stops its processes when the job closes, so it stops when the program that started it closes. Run {GhExtension.CommandName(context.Platform)} daemon start from a regular terminal.");
    }
}
catch (Win32Exception)
{
}
```

Add a one-line comment above the `catch`: the job state is advisory here, and the daemon keeps running.

### Docs: `docs/cli.md` (exact text)

**"Install and uninstall."** Replace the bullet `- It starts the daemon.` with:

```markdown
- It starts the daemon, as `gh ledger daemon start` does. On Windows, in a terminal whose job would stop the daemon, it doesn't start it; see [The daemon](#the-daemon). The other changes stay, and `install` exits with code 1.
```

**"The daemon."** After the bullet list and before `The port defaults to 4318.`, insert:

````markdown
On Windows, some programs run their terminals in a job that stops every process the terminal started when it closes; the GitHub Copilot app's agent terminals can. `daemon start` reads the job it runs in. When the job lets processes leave it, the daemon leaves it. When the job would stop the daemon and doesn't let it leave, `daemon start` starts nothing, exits with code 1, and says:

```text
gh-ledger daemon not started: this terminal runs in a Windows job that stops the processes it started when it closes, so the daemon would stop with it.
Run gh ledger daemon start from a regular terminal, or sign out and back in to start it from the sign-in item.
```

The second line leaves out the sign-in part when there's no sign-in item, and says `gh-ledger` for a binary you built. If the job can't be read, `daemon start` refuses the same way. A job inside another job can still pass the daemon to an outer job that stops it; the daemon then writes a `Warning:` line to `daemon.log` when it starts, as `daemon run` does in such a terminal.
````

**"Troubleshooting."** After the bullet that starts `- **`gh ledger daemon start` says that the daemon did not start.**`, insert:

```markdown
- **`gh ledger daemon start` or `install` says `gh-ledger daemon not started: this terminal runs in a Windows job …`.** The terminal, such as an agent terminal of the GitHub Copilot app, would stop the daemon when it closes. Run `gh ledger daemon start` from a regular terminal, or sign out and back in.
```

### Tests

`DaemonJobTests` covers DJ1–DJ6 through `CliHarness` with `FakeCliPlatform`. Its default `ProcessPath`, `/opt/gh-ledger/gh-ledger`, isn't an extension, so `{cmd}` is `gh-ledger`.

**DJ1_Start_Refuses_InAKillOnCloseJob.**
- **Setup:** `Platform.JobState = new(true, 0x2000)`, with no startup command.
- **Run:** `daemon start`.
- **Expect:** exit 1 and `Out` empty. `Error` is exactly:

  ```text
  gh-ledger daemon not started: this terminal runs in a Windows job that stops the processes it started when it closes, so the daemon would stop with it.
  Run gh-ledger daemon start from a regular terminal.
  ```

  followed by `"\n"`. `Platform.DetachedStarts` is empty, and `daemon.json` doesn't exist.

**DJ2_Start_Refusal_MentionsTheSignInItem.**
- **Setup:** as DJ1, plus `Platform.StartupCommands["CopilotLedger"] = "x"`.
- **Expect:** `Error`'s second line is `Run gh-ledger daemon start from a regular terminal, or sign out and back in to start it from the sign-in item.`

**DJ3_Start_BreaksAway_WhenTheJobAllowsIt.**
- **Setup:** `Platform.JobState = new(true, 0x2800)`.
- **Run:** `daemon start`.
- **Expect:** exit 0, `Out` starts with `gh-ledger daemon started (pid `, and `Error` empty. Stop the in-process daemon as the existing daemon tests do.

**DJ4_Start_Refuses_WhenTheJobCantBeRead.**
- **Setup:** `Platform.JobStateException = new Win32Exception(5, "Access is denied.")`.
- **Expect:** exit 1, and `Error` is exactly:

  ```text
  gh-ledger daemon not started: can't read this terminal's Windows job, so the daemon might stop when the terminal closes (Access is denied.).
  Run gh-ledger daemon start from a regular terminal.
  ```

  followed by `"\n"`. `DetachedStarts` is empty.

**DJ5_Install_InAKillOnCloseJob_EndsWithTheMessage.**
- **Setup:** a Windows `FakeCliPlatform` (`IsWindows = true`), set up the way the existing Windows install tests set one up, with `JobState = new(true, 0x2000)`.
- **Run:** `install`.
- **Expect:**
  - exit 1;
  - `Error` equals R1 + `"\n"` + R2b (with `gh-ledger`) + `"\n"`, because `install` registered `CopilotLedger` first;
  - `Out` ends with `gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n`;
  - `DetachedStarts` is empty;
  - `StartupCommands` contains `CopilotLedger`.

**DJ6_DaemonRun_WarnsWhenItsJobKillsOnClose.**
- **Setup:** `Platform.JobState = new(true, 0x2800)`. Breakaway doesn't save a process that's already in the job.
- **Run:** `StartInProcessDaemonAsync()`, then stop the daemon the way `AlertDaemonTests` does.
- **Expect:** exactly one line of `DaemonOutput` ends with ` Warning: this daemon runs in a Windows job that stops its processes when the job closes, so it stops when the program that started it closes. Run gh-ledger daemon start from a regular terminal.`
- **Control:** with `JobState` left at its default, no line contains `Windows job`.

**DJ7_RealJob_TheDaemonSurvivesOrStartNeverRan** (`[Theory]`, 3 cases, `[InlineData(uint flags, bool expectStarted)]`): `(0x2000, false)`, `(0x2800, true)`, and `(0x3000, true)`. On non-Windows, return at once.

**The harness: `WindowsJobHarness`.** This is test-only code, `[SupportedOSPlatform("windows")] internal static class`, with:

```csharp
// Runs fileName with arguments, suspended, in a new job whose extended limit flags are limitFlags; resumes it; waits
// up to timeout for it to exit; then closes the job handle (which kills what's left in a KILL_ON_JOB_CLOSE job).
// environment is the child's whole environment. Its standard output and error go to outputPath. Returns its exit code.
public static int RunInJobThenCloseJob(
    string fileName, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment,
    uint limitFlags, string outputPath, TimeSpan timeout);
```

Implement it with `kernel32.dll` imports private to the class:
- `CreateJobObjectW(IntPtr, string?)`;
- `SetInformationJobObject(job, 9, ref extended, size)`, using the same struct layouts as Task 3, declared again in the test;
- `CreateProcessW`, with `CREATE_SUSPENDED` (`0x4`), `CREATE_NO_WINDOW` (`0x08000000`), and `CREATE_UNICODE_ENVIRONMENT` (`0x400`), and `STARTF_USESTDHANDLES`. Use an inheritable output handle from `File.OpenHandle(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Inheritable)` for standard output and standard error, and `bInheritHandles: true`;
- `AssignProcessToJobObject`, `ResumeThread`, `WaitForSingleObject`, `GetExitCodeProcess`, and `CloseHandle`.

**Building the arguments.**
- **Command line:** `"\"" + fileName + "\" " + string.Join(' ', arguments)`. The arguments have no spaces.
- **Environment block:** each `key=value` followed by `\0`, sorted ordinally, then a final `\0`.

**DJ7's steps:**
1. **Setup.**
   - `using TempDirectory temp = new();`
   - `string exe = Path.Combine(AppContext.BaseDirectory, "gh-ledger.exe")`.
   - `int port` comes from `new TcpListener(IPAddress.Loopback, 0)`: start it, read its `LocalEndpoint` port, then stop it.
   - The environment is a copy of `Environment.GetEnvironmentVariables()` with `COPILOT_LEDGER_HOME` set to `Path.Combine(temp.Path, "data")`.
2. **Run.** Call `RunInJobThenCloseJob(exe, ["daemon", "start", "--port", port], env, flags, Path.Combine(temp.Path, "out.txt"), TimeSpan.FromSeconds(60))`.
3. **When `expectStarted` is false:**
   - the exit code is 1;
   - `out.txt`'s first line equals R1;
   - its second line starts with `Run gh-ledger daemon start from a regular terminal`. Whether the sign-in clause appears depends on the machine's Run value, so don't assert the rest;
   - `data/daemon.json` doesn't exist.
4. **When `expectStarted` is true:**
   - the exit code is 0;
   - `out.txt` starts with `gh-ledger daemon started (pid `;
   - parse the pid. After the job handle is closed, wait 1 second, then assert that `Process.GetProcessById(pid)` doesn't throw and `HasExited` is false;
   - `data/daemon.json` names that pid.
5. **Cleanup,** in `finally`:
   - Run `exe daemon stop` through `Process.Start` with `UseShellExecute = false`, the same `COPILOT_LEDGER_HOME`, and redirected output, and wait up to 30 s.
   - If the pid still runs, `Kill()` it.

This is the test that #78's first acceptance criterion asks for. With the old code, the `0x2000` case would print `started` and the daemon would be gone after the job closed.

**AC:**
- Every string is exact.
- `install` ends with either a running daemon or the message (#78's second acceptance criterion), with exit code 1 in the refused case.
- `docs/cli.md` "Install and uninstall", "The daemon", and "Troubleshooting" contain the exact text above (#78's third acceptance criterion).
- DJ7 passes on Windows.
- No other test changes.

**Verify:** `dotnet build CopilotLedger.slnx` reports 0 warnings. `dotnet test CopilotLedger.slnx` passes **1764** tests: 1755 + 6 + 3.

## Task 5: Alerts Name the Command the Way gh-ledger Runs

**Files:**
- `src/CopilotLedger.Otlp/Breaker/BreakerMessages.cs`
- `src/CopilotLedger.Otlp/Breaker/CircuitBreakerMonitor.cs`
- `src/CopilotLedger.Cli/Alerts/AlertDispatcher.cs`
- `src/CopilotLedger.Cli/Daemon/DaemonHost.cs`
- `docs/guide.md`
- `docs/cli.md`
- the tests listed below

### Contracts (exact)

`BreakerMessages`:

```csharp
    public const string DefaultCommand = "gh-ledger";   // how alerts name gh-ledger unless the caller knows better; the daemon passes GhExtension.CommandName

    public static BreakerAlert ForTrip(long tripId, string repository, string branch, string detail, bool showNotification, string command = DefaultCommand)
        // Body = $"{repository} @ {branch}: {detail} on one prompt. Run: {command} breaker status"
    public static BreakerAlert ForBudget(BranchBudgetRow budget, bool showNotification, string command = DefaultCommand)
        // Body = $"{budget.Repository} @ {budget.Branch}: {Credits(budget.SpentAiCredits)} of {Credits(budget.BudgetAiCredits)} AI credits spent. Run: {command} budget list"
```

**`CircuitBreakerMonitor`.**
- The constructor becomes `public CircuitBreakerMonitor(SqliteLedgerStore store, IBreakerAlertSink sink, string command = BreakerMessages.DefaultCommand)`.
- Store `command` in a private readonly field, and pass it to both `ForTrip` and `ForBudget` (`:131` and `:146`).

**`AlertDispatcher`.**
- In the constructor, store `GhExtension.CommandName(context.Platform)` in a private readonly field `_command`.
- Pass `_command` to `ForTrip` and `ForBudget` in `EnqueuePendingAsync` (`:54` and `:67`).
- Add `public string Command => _command;` so `DaemonHost` can pass the same name to the monitor.

**`DaemonHost`.** Replace `CircuitBreakerMonitor monitor = new(store, dispatcher);` with `CircuitBreakerMonitor monitor = new(store, dispatcher, dispatcher.Command);`.

The titles (`TripTitle` and `BudgetTitle`) don't change. Existing tests whose platform isn't the extension keep expecting `Run: gh-ledger …`, so they don't change. Don't edit `docs/test-reports/2026-11-24-live-e2e-windows.md`: it records what 0.5.0 printed.

### Docs (exact)

**`docs/guide.md`.** In the `budget-alert` sample (line 416), replace `Run: gh-ledger budget list` with `Run: gh ledger budget list`. The guide's world runs as the extension (`GuideWorld`'s `ExtensionPath` with a `manifest.yml`), so `GuideSamplesTests.GS5` checks the new text. Change GS5 only if it builds the expected text itself, and not from the sample.

**`docs/cli.md`, "Circuit breaker."** After the sentence `It doesn't stop the agent.`, insert this sentence inside the same paragraph:

```text
The alert names the command the way the daemon runs: `gh ledger breaker status` from the extension, or `gh-ledger breaker status` for a binary you built.
```

### Tests

**BA1_Alerts_NameTheGivenCommand** (`[Theory]`, 2 cases), in `tests/CopilotLedger.Tests/Breaker/BreakerRulesTests.cs`. Both cases use the inputs of the existing BM vectors (`BreakerRulesTests.cs:90–121`) with `command: "gh ledger"`. Expected bodies, exactly:
- **`trip`:** `swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt. Run: gh ledger breaker status`
- **`budget`:** `contoso/api @ main: 31.50 of 40.50 AI credits spent. Run: gh ledger budget list`

**BA2_Monitor_PassesItsCommand** (`[Fact]`), in `tests/CopilotLedger.Tests/Otlp/CircuitBreakerMonitorTests.cs`.
- **Setup:** repeat the existing churn-trip test that expects `… 5 failed tool calls in a row on one prompt. Run: gh-ledger breaker status` (`:51`), but construct the monitor with `command: "gh ledger"`.
- **Expect:** the published body ends with `Run: gh ledger breaker status`.

**BA3_Daemon_AsTheExtension_SaysGhLedger** (`[Fact]`), in `tests/CopilotLedger.Tests/Cli/AlertDaemonTests.cs`.
- **Setup:** repeat the existing budget-alert daemon test (the one asserting `:194`'s `ALERT gh-ledger branch budget exceeded: … Run: gh-ledger budget list`), with the platform set up as the extension:
  - `ProcessPath = <root>/gh/extensions/gh-ledger/gh-ledger.exe` on Windows, or `…/gh-ledger` otherwise;
  - `ExistingFiles` contains `<root>/gh/extensions/gh-ledger/manifest.yml`.
- **Expect:** the `ALERT` line ends with `Run: gh ledger budget list`.

**AC:**
- #81's first acceptance criterion: BA1 and BA3 show `gh ledger budget list` and `gh ledger breaker status` for the extension.
- #81's second acceptance criterion: the guide's sample and `docs/cli.md` match.
- Every existing alert test passes unchanged.

**Verify:** `dotnet build CopilotLedger.slnx` reports 0 warnings. `dotnet test CopilotLedger.slnx` passes **1768** tests: 1764 + 2 + 1 + 1.

## Task 6: `context_bloat` Reports Once per Invocation, and `tool_loop` Is High Only When It Costs Something

**Files:**
- `src/CopilotLedger.Core/Audit/ContextBloatRule.cs`
- `src/CopilotLedger.Core/Audit/ToolLoopRule.cs`
- `src/CopilotLedger.Cli/Audit/AuditText.cs`
- `tests/CopilotLedger.Tests/Audit/TokenRuleTests.cs`
- `tests/CopilotLedger.Tests/Audit/ToolRuleTests.cs`
- `tests/CopilotLedger.Tests/Cli/AuditOutputTests.cs`
- `tests/CopilotLedger.Tests/TestSupport/AuditOutputs.cs`
- any other test whose pinned JSON holds a `context_bloat` finding's `metrics`. Find them with `rg -n "inputOutputRatio|zeroOutput" tests`.

### `ContextBloatRule` (algorithm)

1. **Qualify.** A span qualifies with the same trigger as today: `InputTokens >= ContextBloatInputTokens`, and either `OutputTokens == 0` or `InputTokens >= ContextBloatRatio * OutputTokens`.
2. **Group.** Group the qualifying spans, in `AuditMath.ByStart` order, by this key:

   `(Repository, Branch, PullRequestNumber, TraceId, ConversationId, AgentInvocationId, AuditMath.Agent(AgentName), Model, zeroOutput)`

   Use a `record struct` or tuple key with ordinal string equality. A null `AgentInvocationId` is a key value of its own, so the trace's unknown-invocation requests of one agent and model group together.
3. **Emit.** For each group, in the order of its first span's start, emit one finding:

   | Field | Value |
   |---|---|
   | `RuleId` | `context_bloat` |
   | `Severity` | `Info` |
   | `Scope` | `AuditMath.Scope(first, withModel: true)` |
   | `WindowStart` | `first.StartTime` |
   | `WindowEnd` | `AuditMath.LatestEnd(group)` |
   | `EstimatedImpactUsd` | `null` |
   | `RecommendationKey` | `AuditRecommendations.ContextBloatZeroOutput` for the zero-output group, otherwise `context_bloat` |

   `Metrics`, in this order:
   1. `requests` = the group's count (`AuditUnits.Requests`);
   2. `inputTokens` = the sum (`Tokens`);
   3. `outputTokens` = the sum (`Tokens`);
   4. for the zero-output group, `zeroOutput` = 1 (`Flag`); otherwise `inputOutputRatio` = `(decimal)sumInput / sumOutput` (`Ratio`).

   Update the class comment to say it reports per agent invocation and model.

### `ToolLoopRule`

Replace `Severity = AuditMath.Severity(run.Count, trigger)` with:

```csharp
decimal? impact = AuditMath.SumBilled(requests);
// High only when the run is long and costs something known; a free or unpriced loop is routine exploration (#80).
AuditSeverity severity = run.Count >= 2L * trigger && impact is > 0m ? AuditSeverity.High : AuditSeverity.Warning;
```

Use `impact` for `EstimatedImpactUsd`. Nothing else changes.

### `AuditText.KeyMetrics` for `context_bloat`

Let `n` be the `requests` metric's value, or 1 when the finding has no `requests` metric. Then:
- **`n == 1`:** the text is unchanged: `"{in} in, no output"` or `"{in} in, {out} out ({ratio}:1)"`.
- **`n > 1`:** prefix `"{LedgerFormat.Count(n)} requests, "`, giving `"20 requests, 2,400,000 in, 3,000 out (800:1)"` or `"3 requests, 240,000 in, no output"`.

### Test vectors (exact)

**AR1 (existing, 6 cases).** Every expected string that isn't `none` gains the prefix `requests=1 requests, ` after `[`. For example, `at` becomes `info [requests=1 requests, inputTokens=50000 tokens, outputTokens=250 tokens, inputOutputRatio=200 ratio] null context_bloat`.

**AR4 (existing).** Update only its assertions on the bloat finding's metrics, if it has any. Its scope and window stay.

**AR13_ContextBloat_ReportsOncePerInvocationModelAndKind** (`[Theory]`, 4 cases). Use `AuditFixtures.Span(k, TimeSpan.FromMinutes(k))`. Agentic requests are `InputTokens = 120_000`, `CacheReadInputTokens = 114_000`, `CacheReadState = Reported`, and `OutputTokens = 150`. Assert `AuditFixtures.Describe(AuditFixtures.Run(AuditRuleIds.ContextBloat, input))`.

| Case | Input | Expected |
|---|---|---|
| `agent session` | spans 1–20 agentic, plus span 21 with `InputTokens = 90_000`, `CacheReadInputTokens = 0`, `OutputTokens = 0` | `info [requests=20 requests, inputTokens=2400000 tokens, outputTokens=3000 tokens, inputOutputRatio=800 ratio] null context_bloat \| info [requests=1 requests, inputTokens=90000 tokens, outputTokens=0 tokens, zeroOutput=1 flag] null context_bloat_zero_output` |
| `two invocations` | spans 1–20 agentic; even-numbered spans `with { AgentInvocationId = "b0000000000000b0" }` | `info [requests=10 requests, inputTokens=1200000 tokens, outputTokens=1500 tokens, inputOutputRatio=800 ratio] null context_bloat \| info [requests=10 requests, inputTokens=1200000 tokens, outputTokens=1500 tokens, inputOutputRatio=800 ratio] null context_bloat` |
| `two models` | spans 1–10 agentic; even-numbered spans `with { Model = "claude-sonnet-5.5" }` | `info [requests=5 requests, inputTokens=600000 tokens, outputTokens=750 tokens, inputOutputRatio=800 ratio] null context_bloat \| info [requests=5 requests, inputTokens=600000 tokens, outputTokens=750 tokens, inputOutputRatio=800 ratio] null context_bloat` |
| `one below` | spans 1–20 agentic; span 7 `with { OutputTokens = 1_000 }` (ratio 120, below 200) | `info [requests=19 requests, inputTokens=2280000 tokens, outputTokens=2850 tokens, inputOutputRatio=800 ratio] null context_bloat` |

The `\|` in the table is a literal `|` in the expected string.

`agent session` is #80's first acceptance criterion: 21 agentic requests give 2 findings, not 21.

**AR14_ContextBloat_GroupScopeAndWindow** (`[Fact]`). Spans 1–3 are agentic at offsets 0, 1, and 2 minutes, and span 3 has `EndTime = span 3's StartTime + 90 s`. Assert one finding:
- `Scope` equals `AuditMath.Scope(span 1, withModel: true)`, compared field by field with the expected record;
- `WindowStart == span1.StartTime`;
- `WindowEnd == span3.StartTime + 90 s`.

**AR9 (existing theory, 3 new cases).**

| Case | Input | Expected |
|---|---|---|
| `high local` | `MapSpans(Interleaved(16, _ => ("view", null, 1)), span => span with { Channel = BillingChannel.Local, PricingStatus = PricingStatus.LocalOffload, CostUsd = 0m, AiCredits = 0m })` | `warning [calls=16 calls, requests=15 requests] 0 tool_loop` |
| `high unpriced` | `MapSpans(Interleaved(16, _ => ("view", null, 1)), span => span.SpanId.EndsWith('4') ? span with { PricingStatus = PricingStatus.UnknownModel } : span)` | `warning [calls=16 calls, requests=15 requests] null tool_loop` |
| `high parallel batch` | `AuditFixtures.Input([AuditFixtures.Span(1)], Calls(16, _ => 5, _ => ("view", null, 1)))` | `warning [calls=16 calls, requests=0 requests] 0 tool_loop` |

The existing `high` case (`high [calls=16 calls, requests=15 requests] 0.15 tool_loop`) stays High. `high local` is #80's second acceptance criterion.

**AO7 (existing theory, 3 new cases).**

| Case | Metrics | Expected |
|---|---|---|
| `context_bloat aggregated` | `("requests", 20m), ("inputTokens", 2_400_000m), ("outputTokens", 3_000m), ("inputOutputRatio", 800m)` | `20 requests, 2,400,000 in, 3,000 out (800:1)` |
| `context_bloat aggregated zero output` | `("requests", 3m), ("inputTokens", 240_000m), ("outputTokens", 0m), ("zeroOutput", 1m)` | `3 requests, 240,000 in, no output` |
| `context_bloat one request` | `("requests", 1m), ("inputTokens", 260_000m), ("outputTokens", 1_000m), ("inputOutputRatio", 260m)` | `260,000 in, 1,000 out (260:1)` |

**Pinned outputs.**
- In `AuditOutputs.Json`, insert into the `context_bloat` finding's `metrics` array, first:

  ```json
  {
    "name": "requests",
    "value": 1,
    "unit": "requests"
  },
  ```

  at the same indentation as its neighbors.
- Make the same insertion in every other pinned JSON that holds a `context_bloat` finding: export bundle tests and LLM-payload tests, if any.
- `AuditOutputs.Table` and the guide's `audit` sample don't change in this task: their single-request finding still reads `260,000 in, 1,000 out (260:1)`.

**AC:**
- #80's first two acceptance criteria hold: `agent session` and `high local`.
- No other rule's output changes.
- No JSON field is added, other than the `requests` metric entry.

**Verify:** `dotnet build CopilotLedger.slnx` reports 0 warnings. `dotnet test CopilotLedger.slnx` passes **1779** tests: 1768 + 4 + 1 + 3 + 3.

## Task 7: Per-Rule Totals in the Audit Table, and the Docs

**Files:**
- `src/CopilotLedger.Cli/Audit/AuditTable.cs`
- `tests/CopilotLedger.Tests/TestSupport/AuditOutputs.cs`
- `tests/CopilotLedger.Tests/Cli/AuditOutputTests.cs`
- `docs/cli.md`
- `docs/guide.md`

### `AuditTable.Write`

Directly after the totals line, inside the `else` branch that prints it, write:

```csharp
output.WriteLine(
    "By rule: " +
    string.Join(", ", AuditRuleIds.All
        .Select(id => (Id: id, Count: all.Count(finding => finding.RuleId == id)))
        .Where(rule => rule.Count > 0)
        .Select(rule => $"{rule.Id} {rule.Count.ToString(CultureInfo.InvariantCulture)}")) +
    ".");
```

It counts `all`, the same findings as the totals line, not only the ones shown. `No findings.` gets no `By rule:` line.

### Pinned text (exact)

**`AuditOutputs.Table`.** After `4 findings: 1 high, 2 warning, 1 info.`, insert the line:

```text
By rule: failed_tool_retry 1, context_bloat 1, cache_miss_streak 1, premium_model_trivial_turn 1.
```

**`AuditOutputTests.AO5`.** After `4 findings: 1 high, 2 warning, 1 info. Showing 1.`, insert the same line.

**`docs/guide.md` `audit` sample.** After `6 findings: 1 high, 2 warning, 3 info.`, insert:

```text
By rule: failed_tool_retry 1, context_bloat 1, context_growth 1, cache_miss_streak 1, premium_model_trivial_turn 1, velocity_near_breaker 1.
```

GS checks it.

**New AO8_ByRule_CountsEveryFindingInRuleOrder** (`[Fact]`).
- **Input:** build an `AuditInput` with `Rules = [AuditRuleIds.ContextBloat, AuditRuleIds.ToolLoop]` from Task 6's `two invocations` spans, plus `Interleaved`-style tool calls. Eight `view` calls in a row of `AuditFixtures.Invocation` give one `tool_loop` Warning. Get the report with `AuditAnalyzer.Report(…, AuditSeverity.Info, 1)`.
- **Expect:** the second line of `AuditTable.Write`'s output is exactly `By rule: tool_loop 1, context_bloat 2.`. Note the order: `AuditRuleIds.All` order, not severity order, and every finding is counted even though `limit` is 1.

### `docs/cli.md` (exact)

**Rule table.** Replace the `context_bloat` row with:

```markdown
| `context_bloat` | Requests of at least 50,000 input tokens with at least 200 times as much input as output, or no output: one finding for each agent invocation and model (or prompt chain, agent, and model when the invocation is unknown), and a separate one for its requests without output | Info only | | None |
```

Replace the `tool_loop` row with:

```markdown
| `tool_loop` | One agent invocation calling the same tool again and again with the same outcome | 8 calls in a row | 16 calls, when the estimated impact is known and more than $0 | The billed cost of the invocation's requests between the first call and the last |
```

**"Output and exit codes."** In the first paragraph, replace `The table lists the findings, most severe first, then by estimated impact;` with:

```text
The table counts the findings by severity and, on the line `By rule:`, by rule, in the rule table's order; then it lists them, most severe first, then by estimated impact;
```

Leave the rest of the paragraph as is.

**AC:**
- #80's third acceptance criterion: the rule table is updated.
- Per-rule totals show in text only. `--json`, MCP, and the bundle are unchanged (D8).
- `AuditDocsTests` passes. If it pins the rows, update it to the new text exactly as above.

**Verify:** `dotnet build CopilotLedger.slnx` reports 0 warnings. `dotnet test CopilotLedger.slnx` passes **1780** tests: 1779 + 1.

## Task 8: Docs Consistency and Final Verification

**Files:** new `tests/CopilotLedger.Tests/Docs/SprintThirteenDocsTests.cs`, next to `AuditDocsTests.cs`, in the same namespace as `AuditDocsTests`.

**DC1_TheDocsMatchTheSprintsBehavior** (`[Fact]`). Read `docs/cli.md` and `docs/guide.md` from the repository root. Find the root the way `Cli/AuditPrivacyTests.cs`'s `RepositoryRoot()` does: walk up from `AppContext.BaseDirectory` to the directory that contains `CopilotLedger.slnx`. Normalize the line endings with `ReplaceLineEndings("\n")`. Assert:
1. Neither file contains `Run: gh-ledger `.
2. `docs/cli.md` contains the two-line refusal block from Task 4, exactly as written there.
3. `docs/cli.md` contains the `context_bloat` and `tool_loop` rows from Task 7, exactly.
4. `docs/guide.md` contains `By rule: failed_tool_retry 1, context_bloat 1, context_growth 1, cache_miss_streak 1, premium_model_trivial_turn 1, velocity_near_breaker 1.`
5. `docs/guide.md` contains `Run: gh ledger budget list`.

**Final verification** (report every command and its result):
1. Run `dotnet build CopilotLedger.slnx`: 0 warnings and 0 errors.
2. Run `dotnet test CopilotLedger.slnx`: **1781** passed, 0 failed, 0 skipped.
3. Run `$env:GH_LEDGER_PERF='1'; dotnet test CopilotLedger.slnx --filter "FullyQualifiedName~ScopedQueryPerfTests" --logger "console;verbosity=detailed"`. It passes. Report the six lines next to Task 1's "before" and Task 2's "after" numbers.
4. **Single-file publish smoke** (Windows):
   - Run `dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r win-x64 -o <temp>\publish`. It must finish with 0 warnings, the same command CI and Release use. Don't commit the output.
   - Then `$env:COPILOT_LEDGER_HOME='<temp>\home'; & '<temp>\publish\gh-ledger.exe' --version` prints `0.5.0`.
   - `& '<temp>\publish\gh-ledger.exe' daemon start --port <free port>` either starts the daemon or prints the refusal, depending on the terminal's job. Report which one happened, then run `daemon stop`.
   - Delete `<temp>`.
5. **Repository checks:**
   - `git --no-pager diff --stat 69ae671 -- src/CopilotLedger.Core/Pricing src/CopilotLedger.Storage/LedgerMigrations.cs docs/configuration.md global.json Directory.Build.props '*.csproj'` is empty: no rates, schema, version, or package change.
   - `rg -n "double|float|REAL" <the files this sprint changed in src>` shows no new money use.

**AC:**
- DC1 passes.
- Every final-verification step passes, and each one is reported.

**Verify:** `dotnet build CopilotLedger.slnx` reports 0 warnings. `dotnet test CopilotLedger.slnx` passes **1781** tests: 1780 + 1.

## Test specifications

| Task | New tests | Running total |
|---|---|---|
| Start (`69ae671`) | – | 1740 |
| 1 | SQ1 (opt-in; passes at once without `GH_LEDGER_PERF`) | 1741 |
| 2 | SQ2 | 1742 |
| 3 | DL1 ×8, DL2 ×4, DL3 | 1755 |
| 4 | DJ1–DJ6, DJ7 ×3 | 1764 |
| 5 | BA1 ×2, BA2, BA3 | 1768 |
| 6 | AR13 ×4, AR14, AR9 +3 cases, AO7 +3 cases | 1779 |
| 7 | AO8 | 1780 |
| 8 | DC1 | 1781 |

Changed expectations, with no change to the count:
- **Task 6:** AR1, AR4 (if it pins metrics), `AuditOutputs.Json`, and the other pinned `context_bloat` JSON.
- **Task 7:** `AuditOutputs.Table`, AO5, the guide's `audit` sample, and `AuditDocsTests` (if it pins the rows).
- **Task 5:** the guide's `budget-alert` sample.

## Final step: the pull request (orchestrator only; not a builder task)

**Commits.** The orchestrator commits this plan first, together with the verbatim archive `.github/plans/archive/sprint-11.md`. It then commits each task as its own commit, `Sprint 13 Task N: <Title>`, each with the trailer `Co-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>`, on branch `swigerb-sprint-13-perf-windows-audit`.

**After Task 8, before opening the pull request:**
1. Ask `ledger-architect` to review, per its review mode.
2. Merge or rebase onto `main` once Sprint 12 has merged, and resolve any `SqliteLedgerStore.Export.cs` or `LargeLedger` `INSERT` conflicts as "Starting point" describes.
3. Run `dotnet build CopilotLedger.slnx` and `dotnet test CopilotLedger.slnx` again.

**The pull request.**
- **Title:** exactly "Sprint 13: Scoped-query performance, the Windows daemon, and audit quality".
- **Body:**
  - `Fixes #83`, `Fixes #78`, `Fixes #80`, and `Fixes #81`.
  - The "before" and "after" timings.
  - D3's nested-job residual risk.
  - D4's real-process test.
  - D7's deferred routed-model exemption. File it as a follow-up issue that depends on Sprint 12's #76.
- **No release.** Don't tag or release. The version stays 0.5.0, and cutting a release is the coordinator's call after merge.

## Deferred

- **`status` warning (#78 proposal 4).** Have `status` warn when the running daemon's job kills on close. The daemon can't share its job flags with `status` without a new `daemon.json` field, which is a format change. The `daemon.log` `Warning:` line covers it for now.
- **Broker launch (#78).** Start the daemon outside a non-breakaway job through WMI `Win32_Process.Create` or a Task Scheduler task (D3).
- **`premium_model_trivial_turn` routed exemption (#80).** Skip requests that Auto or Copilot Fusion routed. This needs a routed signal in the ledger, which is a schema change. Revisit after Sprint 12's #76.
- **`context_bloat` uncached weighting (#80).** Trigger on uncached input instead of total input, or exempt tool-call responses (D5).
- **`tool_loop` read-only tool exemptions by name (#80).**
- **Per-rule totals in `--json`, MCP, and the bundle (D8).** This changes the format.
- **Other `gh-ledger` command names in daemon messages.** For example, the "already running" and port messages, and the sign-in item's warnings, could use `GhExtension.CommandName` the way the alerts now do.
- **Query-plan test for #83.** Pin the plan (`LIST SUBQUERY 2`) through a store-level hook. The store has no `InternalsVisibleTo` today, and SQ2's bound guards the behavior.
