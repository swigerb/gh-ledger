# Sprint 15: MCP Tool Names, the Install Dry Run, `gh ledger` in Help & 0.7.0

| | |
|---|---|
| Architect | `ledger-architect` |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET (`global.json`'s SDK, unchanged), zero Docker, no new packages |
| Plan written | 2026-11-24, against `main` at `c6dc935` ("Docs: a quickstart, report screenshots, and a sweep for accuracy and brevity (#94)") |
| Branch | `swigerb-sprint-15-mcp-names-dry-run-help` |
| Requirements | Issue [#90](https://github.com/swigerb/gh-ledger/issues/90) (live MCP tool calls are named by 129-character hashes), issue [#91](https://github.com/swigerb/gh-ledger/issues/91) (`install --dry-run` always says it would start the daemon), issue [#92](https://github.com/swigerb/gh-ledger/issues/92) and its comment (help usage lines and command hints say `gh-ledger` under the extension), docs that no longer describe these as limitations, version 0.7.0, and its 0.6.0 → 0.7.0 upgrade docs |
| Status | Current sprint. Sprint 14's plan is archived verbatim at `.github/plans/archive/sprint-14.md` (SHA-256 `4F2DA37E44AC0BC0E3719151AC626912BA03CF87B3423EFD9D3472FF485413D5`). |
| Amended | 2026-11-24, after Task 2 (`75d4131`): Task 3's ML5 becomes a two-row theory, so every count from Task 3 on is one higher (PA1). 2026-11-25, after Task 3 (`1744ecd`): Task 4's MD1 reads only H's tool calls in its last query, and Task 5's `DecideStart` sets `job` in its `catch`; no count changes (PA2). 2026-11-25, after PA2 (`6896ad2`), with Task 4 in progress: a new Task 10 makes Task 3's IP6 read the ledger's files as IP1 does, so it passes alone and serially, and the final verification becomes Task 11; Task 5's IN20 says what its `withState: false` row asserts about `daemon.json`; no count changes (PA3). See "Plan amendments". |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification, runs the verification commands, changes the line to `- [x]`, and stops. Tasks are sequential, and each builds on the ones before it. Each task is one commit, titled `Sprint 15 Task N: <Title>`, with the repository's `Co-authored-by` trailer.

This plan is a **specification**, not copy-exact code. Each task gives:

- the files;
- the public and internal contracts (names, types, and members are fixed);
- schemas and DDL;
- numbered algorithms;
- exact test vectors;
- docs text;
- acceptance criteria;
- verification commands.

Implement exactly those contracts. Where a task says "exactly", the value, string, name, or order is fixed. Where it leaves a detail open (a private helper's name, a loop's shape), choose the simplest option that matches the surrounding code's style. Don't add features, options, logging, caching, or fast paths the task doesn't ask for. Don't refactor code outside the task's files. Inside a table cell, `\|` stands for `|`.

After **every** task:

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

The build must report 0 warnings and 0 errors (`TreatWarningsAsErrors` is on in `Directory.Build.props`). The test run must report 0 failed and 0 skipped. Record the passed count in the commit message body as `Tests: <n> passed`. The baseline at `c6dc935` is **1,960 passed, 0 failed, 0 skipped** (measured 2026-11-24 while planning, SDK 10.0.115). Each task says exactly how many tests it adds, counting each theory row; if the count differs, stop and report it.

Global rules for every task:

- **Money is `decimal` only.** No `double`, `float`, or `Math.Round` on money, and no SQLite `REAL`. Tests compare money as `decimal`, for example `GetDecimal()` against `0.0086m`.
- **Hermetic tests.** Use temporary directories, `ManualTimeProvider`, `FakeCliPlatform`, `CliHarness`, `LedgerSql`, `McpTestHarness`, and synthetic `CopilotSessionLogs` fixtures. No test may:
  - open a socket, start a process, or run `git`;
  - use Docker or the network;
  - read `~/.copilot`, `~/.codex`, or `~/.copilot-ledger`.

  A test that drops or alters schema, or reads `ledger.db`, `-wal`, or `-shm` bytes, calls `SqliteConnection.ClearAllPools()` first. Every test must also pass with `dotnet test CopilotLedger.slnx -- xUnit.ParallelizeTestCollections=false`. A test that compares System.CommandLine's own words ("Usage:", "Description:", "Show help and usage information") sets `CultureInfo.CurrentCulture` and `CultureInfo.CurrentUICulture` to `CultureInfo.InvariantCulture` and restores both in `finally`, as `CacheReportTests` does.
- **Privacy.** The only new stored data is the `mcp_tool_names` table (Task 2): an MCP tool's hashed name, which live spans already store as `span_links.tool_name`, and Copilot's `data.toolName` for it, which imported tool calls already store. Import newly reads `data.mcpServerName` and `data.mcpToolName` of `tool.execution_start` and keeps them only inside that hash. It never reads `arguments`, results, titles, or errors.
- **No new packages, and `global.json` is unchanged.** `src/CopilotLedger.Core/Pricing/rates.json` is unchanged in every task, including `ratesVersion`.
- **The ledger format stays 7.** No migration is added or changed. `DaemonState.CurrentImportRules` stays 1: the daemon's live-span rules don't change, so a running 0.6.0 daemon doesn't block this version's `import`.
- **Public contracts change only as listed.** These are the only public or internal contract changes this sprint:
  - `McpToolNames` (Task 1);
  - `LearnedToolName`, `LedgerMigrations.Tables`, `SqliteLedgerStore.RecordMcpToolNamesAsync`, `SqliteLedgerStore.ReadMcpToolNamesAsync`, and `LedgerExportSnapshot.GetMcpToolNamesAsync` (Task 2);
  - `SessionLogReadResult.LearnedToolNames` and `LedgerImporter.CurrentImportRevision` = 2 (Task 3);
  - `AuditReport.WithToolNames`, `ExportRecords.ToolCall`'s second parameter, `ExportJson.WriteToolCallsAsync`'s new parameter, and `ExportReportModel.ToolNames` (Task 4);
  - `DaemonStartOutcome`, `DaemonStartDecision`, `DaemonCommands.DecideStart`, and `DaemonCommands.WriteStartDecision` (Task 5);
  - `ExtensionHelpAction` (Task 6);
  - `CircuitFlagFile.Render`'s and `CircuitFlagFile.SyncAsync`'s `command` parameters, and `LedgerMcpContext.Command` (Task 7);
  - `<Version>` 0.7.0 (Task 8).

  CLI options, JSON field names, MCP tool names and schemas, and OTLP handling stay as they are. Output values change only where a task says so.
- **Version strings in top-level docs.** Until Task 8, no new line in `README.md`, `docs/*.md`, `.github/workflows/*.yml`, `src/`, or `tests/` may name `0.6.0`, the current version then: `VersionPlacesTests.VR3` fails on it. Test fixtures that need a version use `9.9.9`. From Task 8 on, the same holds for `0.7.0`, except the places `VersionPlacesTests` lists. Naming `0.5.0` is always allowed, and naming `0.6.0` is allowed from Task 8 on, so docs text that names it waits for Task 9 (D10).
- If a contract here doesn't compile against the code, a referenced line has moved, or a test vector disagrees with the code, **stop and report it**. Don't redesign.

## Tasks

- [x] Task 1: MCP Tool Names: Hash, Shape, Short Form, and Display (CopilotLedger.Core & tests). `McpToolNames` computes Copilot CLI's hashed name of an MCP tool, recognizes the 129-character shape, writes the short form `MCP tool 10be6bee/8453dc19`, and turns a stored tool name into the name reports show.
- [x] Task 2: The `mcp_tool_names` Table, Ensured on Every Open (CopilotLedger.Core, CopilotLedger.Storage & tests). A two-column table that opening any format-7 ledger adds when it's missing, as Sprint 14 did for its index, with an upsert that keeps the ordinally smallest name and a read for the export snapshot. The format number stays 7.
- [x] Task 3: Import Learns MCP Tool Names (CopilotLedger.Core, CopilotLedger.Storage, docs & tests). The Copilot source reads `data.mcpServerName` and `data.mcpToolName` beside `data.toolName`, and the importer records the learned names of every log it reads, before its checkpoint. `CurrentImportRevision` 2 reads each log once more. The import allowlist documents the table.
- [x] Task 4: Reports Show Readable MCP Tool Names (CopilotLedger.Core, CopilotLedger.Cli, CopilotLedger.Mcp, docs & tests). Export's timeline, `tool-calls.json`, and findings; `audit`'s table, `--json`, and local-model payload; and MCP `get_audit_findings` show the learned name or the short form. Findings are computed from the stored names first. The troubleshooting entry about 129-character names becomes "MCP tool names".
- [ ] Task 5: `install --dry-run` Decides the Daemon Step as `install` Does (CopilotLedger.Cli, docs & tests). One side-effect-free decision, shared by `daemon start`, `install`, and its dry run: already running, with its pid; wouldn't start, with the reason, hint, and exit code 1; or would start.
- [ ] Task 6: Help Names `gh ledger` Under the Extension (CopilotLedger.Cli, docs & tests). The root help option's action is wrapped, so the usage line of every help, including the help after a parse error, starts with `gh ledger` under the extension. A built binary's help is unchanged.
- [ ] Task 7: Command Hints Name `gh ledger` Under the Extension (CopilotLedger.Cli, CopilotLedger.Otlp, CopilotLedger.Mcp, docs & tests). The eight hard-coded hints of #92's comment use `GhExtension.CommandName`, `BreakerMessages.DefaultCommand`, or `LedgerMcpContext.Command`. The guide's `breaker-status` and `mcp` samples follow.
- [ ] Task 8: Version 0.7.0 (all version places, docs & tests). Step 1 of `docs/releasing.md`, exactly. Rates unchanged.
- [ ] Task 9: 0.6.0 → 0.7.0 Upgrade Docs and the 0.6.0 Report's Update (docs & tests). The format stays 7, the first import reads every log again once, and going back is a pinned install without the copy. `UpgradeDocsTests.UD6`. An `**Update:**` line in the 0.6.0 test report's "Gaps found".
- [ ] Task 10: IP6 Reads the Ledger as IP1 Does (tests only). PA3: IP6 clears the connection pools after its last query and reads `ledger.db`, `-wal`, and `-shm` through a `FileShare.ReadWrite | FileShare.Delete` stream, as IP1 does, so it passes alone and in a serial run. Its name, setup, and assertions are unchanged.
- [ ] Task 11: Final Verification (no product change). Build and test gates, serial tests, a `publish` whose `--version` is `0.7.0`, smoke tests with temporary homes, and money, privacy, boundary, and docs sweeps.

## Starting point

- `main` at `c6dc935`, after Sprint 14 (`ee4d3a1`, PR #89), its 0.6.0 live test report (#93), and the docs sweep (#94).
- Product version: `0.6.0`. Ledger format: 7. Copilot import window: 1.0.103–1.0.115.
- Tests: 1,960 passed.
- What this plan depends on, read from the code at `c6dc935`:
  - **Live tool names.** `GenAiSpanNormalizer.cs:180` stores `gen_ai.tool.name` as it arrives, in `span_links.tool_name`. Tool names reach users only through:
    - export: `ExportRecords.ToolCall` (`ExportRecords.cs:143`), which feeds the SVG timeline's `<title>` (`ExportSvg.cs:64`) through `ExportModelBuilder.cs:75`, and the bundle's `toolName` (`ExportJson.cs:357`, `:487`);
    - audit findings' `Scope.Tool` (`ToolSequences.cs:83`), which `AuditText.Scope` (the CLI table and export's findings), `AuditJson` (`audit --json`, `telemetry/audit-findings.json`, MCP `get_audit_findings`), and `AuditLlmPayload` (`--local-llm`, `--show-llm-payload`) write.

    `BreakerEvents.ToolCallEvent.ToolName` is read only for retry churn's failure test; no alert, flag file, trip detail, MCP text, report, or receipt displays a tool name. `AuditAnalyzer.Order` sorts findings by `Scope.Tool` as its last key but one.
  - **Import of tool events.** `CopilotSessionLogSource.ToolStartPaths` (`:109`) allowlists `id`, `parentId`, `timestamp`, `agentId`, `data.toolCallId`, `data.toolName` (256), and `data.parentToolCallId`. `ToolStart` (`:696`) keeps a pending call. `Finish` (`:332`) builds the `SessionLogReadResult` (`:379`). `LedgerImporter.ImportLogAsync` reads every discovered log that isn't active, out of `--since`, or unchanged (`:144–155`: `Complete`, `ImportRevision >= CurrentImportRevision` (1), size, write time, and SHA-256), keeps tool calls of the last 7 days only, and writes the checkpoint last (`:297–324`).
  - **Store open.** `SqliteLedgerStore.InitializeAsync` (`SqliteLedgerStore.cs:102`) runs the migrations, then, for each of `LedgerMigrations.Indexes` (`:171–205`), checks `sqlite_master`, takes `BEGIN IMMEDIATE`, checks again, and creates the index. `LedgerSnapshot`, which `import --dry-run` uses, opens read-only and never migrates. `audit`, `export`, and MCP `get_audit_findings` initialize the store and then read one `LedgerExportSnapshot`.
  - **Daemon start and install.** `DaemonCommands.StartAsync` (`DaemonCommands.cs:60`) decides, in order: the daemon holds its lock (`DaemonFiles.IsHeld`: already running, pid from `daemon.json`, exit 0); the process isn't named `gh-ledger` (exit 1); the Windows job blocks or can't be read (exit 1, two lines, the second with a sign-in part when `GetStartupCommand("CopilotLedger")` is set); otherwise it starts. `InstallAsync` refuses a process not named `gh-ledger` before any output or change, dry run included (`InstallCommands.cs:51–57`, `IN8`), registers the sign-in item, and then, with `--dry-run`, always prints `Would start the gh-ledger daemon.` and exits 0 (`:305–316`).
  - **Help.** `GhLedgerCli.InvokeAsync` (`GhLedgerCli.cs:34–64`) builds a `RootCommand`, whose name is `RootCommand.ExecutableName`. No command adds its own help option.
  - **Command hints.** #81 added `GhExtension.CommandName` (`"gh ledger"` for the extension layout, else `"gh-ledger"`), `BreakerMessages.DefaultCommand`, `AlertDispatcher.Command`, and `CircuitBreakerMonitor`'s `command` parameter. These still hard-code `gh-ledger`: `BreakerCommands.cs:282`; `LedgerMcpTools.cs:953` and `:956`; `CircuitFlagFile.cs:58–59`; `DaemonHost.cs:149`; `RatesCommands.cs:106`; `StartupItem.cs:17` and `:83`.

## Verified facts

| # | Fact | Source (retrieved 2026-11-24) |
|---|---|---|
| F1 | For a tool from an MCP server, Copilot CLI's live `execute_tool` span sets `gen_ai.tool.name` to the lowercase hexadecimal SHA-256 of the server's name in UTF-8, `/`, and the SHA-256 of the tool's name in UTF-8: 129 characters. It matched exactly on CLI 1.0.103 (`ledger`/`get_branch_ledger`) and 1.0.115 (`computer-use`/`exec_js`). Built-in tools such as `powershell`, `view`, `task`, and `tool_search_tool` arrive in clear. | The coordinator's check against real spans and session logs, recorded in issue #90. Cited as given: no real span or session log was opened while planning, by design. |
| F2 | The same call's `tool.execution_start` in the session log has `data.toolName` `computer-use-exec_js`, `data.mcpServerName` `computer-use`, and `data.mcpToolName` `exec_js`. | Same |
| F3 | Copilot's OpenTelemetry reference describes `gen_ai.tool.name` on `execute_tool` spans only as "Tool name (for example, `readFile`)" and doesn't mention hashing. Tool arguments and results are attributes only with content capture. | https://docs.github.com/en/copilot/reference/enterprise-administrators/opentelemetry-reference, "`execute_tool` span attributes" |
| F4 | SHA-256 of the UTF-8 bytes, lowercase hexadecimal: `ledger` → `fe14010b4fe83303852f0467c919ef9a7ca089b91e96e3aad7d426dd87079297`; `get_branch_ledger` → `eb20c46435d60871da0eb97ce23417c04835e272725c59a3a649c7ce8e521d97`; `computer-use` → `10be6beef496b89dae799c37bd04e5eba2a6264a0a4d605f5661b50d7dc4d1ea`; `exec_js` → `8453dc19fda259c463d7aaffb07becf26b7ce2cfc690a2b0a957e201653dc85a`; `café` (`63 61 66 c3 a9`) → `850f7dc43910ff890f8879c0ed26fe697c93a067ad93a7d50f466a7028a9bf4e`; the empty string → `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`; `CANARY-SERVER-7c1d` → `f3f23e35c9fc32bd46be8fb3989b4952f3dccaf5dbc4721da91359972275a9d5`; `CANARY-TOOL-7c1d` → `74ef83b0e3a641bf9d96ddd6e6dcf473f87891e975cbc4782d0be0a03f8c8cb4`. The UTF-16LE hash of `café` differs (`8c9f3eed…`). The issue's `10be6beef496…/8453dc19fda2…` is `computer-use`/`exec_js`. | Computed while planning with .NET 10.0.15 (`SHA256`) and Python `hashlib`; both agree |
| F5 | In System.CommandLine 2.0.15 as restored here, `HelpBuilder` and `HelpContext` are **internal**, and so are `HelpAction.Builder` and `LocalizationResources`. Public: `HelpAction` (sealed; `MaxWidth`, `Invoke(ParseResult)`, `ClearsParseErrors` true), `HelpOption.Action` (settable), `ParseErrorAction` (sealed), `SynchronousCommandLineAction` (protected constructor), virtual `CommandLineAction.Terminating` and `ClearsParseErrors`, `ParseResult.Action` and `ParseResult.InvocationConfiguration`, a settable `InvocationConfiguration.Output`, and static `RootCommand.ExecutableName`. `RootCommand` adds a recursive `HelpOption` and a `VersionOption`. | Reflection over `%USERPROFILE%\.nuget\packages\system.commandline\2.0.15\lib\net8.0\System.CommandLine.dll` |
| F6 | Replacing the root `HelpOption`'s action with a `SynchronousCommandLineAction` that runs the original `HelpAction` into a buffer and rewrites the usage line works, unchanged in exit code and in every other byte (CRLF kept), for: root `--help`; `import --help`; `budget set --help`; help after a parse error (`budget set` without its argument, `daemon` without a subcommand, the typo `imprt`); and `--help` beside an unknown option. `ParseErrorAction` writes its errors to `Error`, its typo suggestions to `Output` before the help, and then invokes the nearest `HelpOption`'s action. | Scratch probe outside the repository, deleted afterward |
| F7 | gh-ledger's help at `c6dc935` (Debug build, with temporary `COPILOT_LEDGER_HOME` and `COPILOT_HOME`; nothing was created): 29 help outputs, the root and 28 subcommands. Usage lines read `gh-ledger [command] [options]`, `gh-ledger budget set <credits> [options]`, and so on. A Commands row is `  <name>` followed by a space, such as `  set <credits>  Set the current branch's budget.` `daemon stop --help` is `Description:`, `  Stop the running daemon.`, a blank line, `Usage:`, `  gh-ledger daemon stop [options]`, a blank line, `Options:`, `  -?, -h, --help  Show help and usage information`, and a blank line. | Planning run |
| F8 | An `IOException` that a hosted service throws from `StartAsync` comes out of `WebApplication.StartAsync` unwrapped, with `UseTestServer()` (ASP.NET Core 10, `Microsoft.AspNetCore.TestHost` 10.0.15). `OtlpReceiverApp` uses `WebApplication.CreateEmptyBuilder`, which registers no logger. | Scratch probe, deleted |
| F9 | Microsoft.Data.Sqlite 10.0.15 bundles SQLite 3.53.3. Task 2's DDL creates the table; `pragma_table_list('mcp_tool_names')` gives `table\|2\|1\|1` (type, ncol, wr, strict); `pragma_table_info` gives `hashed_name\|TEXT\|1\|1` and `tool_name\|TEXT\|0\|1` (name, type, pk, notnull); the upsert keeps the smaller name by BINARY collation; the CHECKs reject a 128-character key and empty or 257-character names. | Scratch probes (Microsoft.Data.Sqlite 10.0.15, and Python `sqlite3` 3.50.4), deleted |
| F10 | 0.6.0 (`c6dc935`) skips a complete, unchanged log whose checkpoint has `ImportRevision >= 1`. | `LedgerImporter.cs:13`, `:149` |
| F11 | `claude-haiku-4.5` costs 1.00, 0.10 cached, and 5.00 USD per million input, cached input, and output tokens (rates `2026-11-23`), so a request of 10,000 input tokens, 8,000 of them read from the cache, and 300 output tokens costs $0.0043. | `src/CopilotLedger.Core/Pricing/rates.json`; `AuditSeeds` says the same |

## Assumptions (not verified against a primary source)

- **A1.** The GitHub Copilot app and VS Code's Agent Host, which share Copilot's runtime, name MCP tools the same way. Nothing here depends on it: any stored name of the hashed shape is resolved, whatever sent it.
- **A2.** `data.toolName` for one server and tool is the same in every log. When logs disagree, the ordinally smallest name is shown (D4).
- **A3.** Copilot keeps a session log for every session, so an import after a session ends learns its MCP tools' names. A tool whose only log is gone, or whose session is still open, shows the short form until an import reads a log that ran it (`--include-active` reads open sessions).
- **A4.** In the test host, standard output is redirected, so System.CommandLine doesn't wrap help (`HelpAction.MaxWidth` is `int.MaxValue`). The tests don't depend on it: the lines they compare are under 60 characters, and the Commands-row pattern skips continuation lines.
- **A5.** Rereading every log once (Task 3) takes about as long as a first import: the 0.6.0 live test's repair import of 308 sessions took 13.6 s. A reread also applies the documented repairs, so imported requests are repriced with the rates in use if the overlay or downloaded rates changed since they were imported.

## Decisions

- **D1. A table ensured on open; the format stays 7 (#90).** Learned names go in a new `mcp_tool_names` table that `InitializeAsync` creates whenever it's missing, after the migrations and before the indexes, under the same check, `BEGIN IMMEDIATE`, and check-again pattern as Sprint 14's index. `user_version` stays 7, so 0.6.0 still opens an upgraded ledger: it ignores the table. Going back is a pinned install, without the copy. A migration 8 was rejected: it would make the upgrade one-way for nothing that 0.6.0 can't ignore. Storing the readable name in `span_links.tool_name` was rejected: the stored value must not change (#90).
- **D2. Resolve for display only.** Rules, grouping, ordering, limits, and the stored values all use `span_links.tool_name` as stored. Each display maps a name through `McpToolNames.Display` at the end: an audit report through `AuditReport.WithToolNames` after `AuditAnalyzer.Report`, and export's tool calls in `ExportRecords.ToolCall`. Findings keep the order and selection that the stored names gave them, so the same runs, counts, and findings are shown before and after a name is learned.
- **D3. What's shown.** A learned name is Copilot's own `data.toolName`, such as `computer-use-exec_js`, the same name an imported call of that tool shows. An unknown hashed name shows as `MCP tool ` plus its first 8 digits, `/`, and the first 8 digits after the `/`: `MCP tool 10be6bee/8453dc19`. Only exactly 64 lowercase hexadecimal digits, `/`, and 64 more count as hashed. Every other name, and null, is shown as stored.
- **D4. The ordinally smallest name wins.** When logs give one hashed name different tool names (A2), the table keeps the smallest by SQLite's BINARY collation, whatever the order of the logs and imports. "First learned wins" was rejected: the result would depend on import history. "Latest wins" was rejected: it needs a timestamp column, and a reread could flip a name back and forth.
- **D5. Learn from every log import reads.** Every Copilot log whose status is `Read` teaches its names: whatever its records' `--repo` or `--since` scope, whether its session has live spans (whose tool calls aren't imported), and whatever its tool calls' age (the 7-day window applies to links, not names). A tool start teaches a name when `data.toolName`, `data.mcpServerName`, and `data.mcpToolName` are all valid strings and no allowlisted property repeats, with or without a tool call ID or completion. The importer records them before the log's checkpoint. A dry run records nothing. The import report doesn't change. Codex rollouts teach nothing.
- **D6. One more read of each log: `CurrentImportRevision` 2.** Logs imported earlier must be read again to learn their names, so a checkpoint below revision 2 is read again once, as revision 0 was for Sprint 12. 0.6.0 accepts revision 2 as current (F10), so after going back it doesn't read them again. Codex rollouts are read again once too, which only resolves duplicates. A per-source revision was rejected as more machinery than one reread costs.
- **D7. One daemon decision for `daemon start`, `install`, and the dry run (#91).** `DaemonCommands.DecideStart` decides without side effects. `DaemonFiles.IsHeld` opens and closes the lock file only when it exists; `daemon.json`, the process path, the job, and the sign-in item are only read. `StartAsync` and the dry run report a decision through one writer, with the same streams and exit codes. For an already running daemon, the dry run prints `daemon start`'s line. For a blocked start, it prints `Wouldn't start the gh-ledger daemon: ` and `daemon start`'s reason, then `daemon start`'s hint line, to standard error, and exits 1. The hint's sign-in part counts a Windows sign-in item that the dry run would register, as the real `install` registers it before starting the daemon. A registration that would fail can't be foreseen. The "not the gh-ledger executable" outcome is part of the decision for `daemon start`, but `install` never reaches it: it refuses that process first, dry run included (`IN8`).
- **D8. Help: wrap the help option's action (#92).** System.CommandLine 2.0.15 keeps its help builder internal (F5), names the root command after the executable, and doesn't allow spaces in command names. So under the extension, `GhLedgerCli` replaces the root `HelpOption`'s action with `ExtensionHelpAction`. It runs the original `HelpAction` into a buffer and replaces the root's name with `gh ledger` in the usage line only: the first line after the first blank line that starts with two spaces and the root's name. Because `ParseErrorAction` invokes the same option's action (F6), help after a parse error is covered too. For a binary the user built, nothing is installed, so help is byte-for-byte as before. Rejected: reflection into the internal builder (breaks silently on a package update), rewriting all output (could touch more than help), and a root command named `gh ledger` (not allowed).
- **D9. Command hints (#92).** The eight hints in #92's comment name the command through `GhExtension.CommandName` where the CLI's platform is known, through a `command` parameter that defaults to `BreakerMessages.DefaultCommand` in Otlp (the flag file, as #81 did for alerts), and through `LedgerMcpContext.Command` in Mcp, which `gh ledger mcp` sets. Every other `gh-ledger` in `src` names the product, the executable, or a built binary, such as `Run gh-ledger directly.` when `gh` isn't on `PATH`, and stays. Texts that say `gh ledger` for everyone (the audit recommendation `set with gh ledger breaker config`, receipts, and export's report text) stay: they're the documented form, and `AuditDocsTests` pins the first.
- **D10. Bump before the docs that name the previous version.** Task 8 bumps to 0.7.0 before Task 9 writes docs that name 0.6.0. Until then, 0.6.0 is the current version, and `VR3` fails on any new mention of it (Sprint 14's D9).
- **D11. Upgrade docs for 0.6.0 → 0.7.0.** Users of 0.6.0 need three facts: the format is unchanged, so going back is a pinned install without the copy; the first `import --all` reads every log again (A5); and the existing steps still apply. A short `**Upgrading from 0.6.0.**` paragraph in `docs/cli.md`, a sentence under `### Repairs` that names the logs read once more (those that gh-ledger 0.6.0 or earlier imported), one sentence in the guide, and `UD6` pin them. `UD5` keeps pinning 0.5.0's.
- **D12. The 0.6.0 report gets one `**Update:**` line.** It goes under "Gaps found", after its table, as the brief asks. The 0.5.0 report has its update line near its top instead. Nothing else in a dated report changes.

## Plan amendments

- **PA1 (2026-11-24, after Task 2 at `75d4131`; plan-text repair, no behavior change).** Task 3's ML5 paired a Copilot `1.0.102` log with `UnsupportedProducerVersion`, but the gate (`CopilotSessionLogSource`'s `Start`) gives a version below 1.0.103 `AggregateOnly` and one above 1.0.115 `UnsupportedProducerVersion`, as `CopilotSessionGateTests.CS2` pins. `ledger-builder` stopped on it, as the global rules say: 2,010 passed and 1 failed (ML5 only). ML5 is now a theory of `(string version, SessionLogStatus status)` with the rows `("1.0.102", SessionLogStatus.AggregateOnly)` and `("1.0.116", SessionLogStatus.UnsupportedProducerVersion)`, each asserting its status and an empty `LearnedToolNames`. That covers logs on both sides of the window: an older Copilot's, and a newer one's, which users get when Copilot updates past the window before gh-ledger does. Its name and claim, that a log whose status isn't `Read` teaches nothing, are unchanged. The second row adds one test, so Task 3 adds 17, and every later running total is one higher: 2,012 after Task 3, 2,019 after Task 4, 2,030 after Task 5, 2,041 after Task 6, 2,055 after Tasks 7 and 8, and 2,056 after Task 9 and in Task 10 (now Task 11, PA3). "Tests added this sprint" counts ML1–ML10 as 16, for a total of 96, from 1,960 to 2,056. Checked on a scratch copy of the working tree with ML5 as this theory, outside the repository and deleted afterward: 0 warnings, 0 errors, and 2,012 passed, 0 failed, 0 skipped; both rows also pass with `xUnit.ParallelizeTestCollections=false`. Changed: the Amended row; Task 3's ML5 row and acceptance; the counts in Tasks 4–9's acceptance and Task 10's gates (now Task 11, PA3); and "Tests added this sprint". Unchanged: Tasks 1 and 2 and their text; every contract, algorithm, fixture, and docs text; ML1–ML4, ML6–ML10, and IP6; the decisions; and the task order.
- **PA2 (2026-11-25, after Task 3 at `1744ecd`; plan-text repair, no behavior change).** Task 4's MD1 ended with `SELECT DISTINCT tool_name FROM span_links WHERE trace_id = '<H>'` is `[ExecJs]`. But `AuditSeeds.LiveTrace` also stores H's `invoke_agent` link (`Agent`) and its three `chat` links (`Request` under an agent), whose `tool_name` is NULL, so the query gives `["null", ExecJs]` (`LedgerSql.RowsAsync` writes NULL as `null`). `ledger-builder` stopped on it, as the global rules say: 2,018 passed and 1 failed (MD1 only). The query is now `SELECT DISTINCT tool_name FROM span_links WHERE trace_id = '<H>' AND operation_name = 'execute_tool'`, and it must give exactly `[ExecJs]`. `operation_name` is the column (`LedgerMigrations.cs:77`, migration 2), and tool links store `GenAiOperationNames.ExecuteTool`, `execute_tool`, the value the store's own tool-call queries filter on (`SqliteLedgerStore.cs:705`, `SqliteLedgerStore.Export.cs:50`; PA2's line numbers are at `1744ecd`). MD1's name and claim, that the stored names of H's tool calls are still the hash after a name is learned, are unchanged. Checking Tasks 5–9 against the code found one more slip. Task 5's `DecideStart` step 3 left `job` unassigned in its `catch`, so with `StartAsync`'s `JobState job;` (`DaemonCommands.cs:83`) the call `DetachedLaunchPolicy.Decide(job)` doesn't compile (CS0165, confirmed in a scratch build). The `catch` now sets `job = default;`, as `StartAsync` does today (`DaemonCommands.cs:86`); the steps, their order, and every outcome are the same. The rest of Tasks 5–9 was checked against the code it depends on, and where possible against today's output; nothing else needed a change. That covers: `install --dry-run` and `install` with `InstallCommandTests.WindowsPlatform()` and IN20–IN22's platforms, every line except the daemon step that Task 5 changes; the help of the built binary (29 runs, breadth-first as HL3 walks it, and `daemon stop --help` byte for byte as HL2 gives it); XH1–XH6's texts and helpers (`NewBreakerTrip`, `RecordBreakerTripAsync`, `CliContext.ConfigureWebHost`, `McpTestHarness`, and `AuditMcpTests`' context), every byte except the command that Task 7 names; BM13's `Harness.CreateAsync(…, command:)`; the guide samples and sentences that Task 7 edits; Task 8's 15 lines; and the docs anchors of Tasks 5, 6, and 9. Checked on a scratch copy of the working tree with MD1's new query, outside the repository and deleted afterward: 0 warnings, 0 errors, and 2,019 passed, 0 failed, 0 skipped; MD1–MD5 also pass with `xUnit.ParallelizeTestCollections=false`. No test or theory row is added or removed, so every count is PA1's: 2,019 after Task 4, 2,030 after Task 5, 2,041 after Task 6, 2,055 after Tasks 7 and 8, and 2,056 after Task 9 and in Task 10 (now Task 11, PA3). Changed: the Amended row; the last sentence of MD1's Expected cell; and the `catch` of Task 5's `DecideStart` step 3. Unchanged: Tasks 1–3 and their text; every contract, decision, fixture, test vector other than MD1's last query, docs text, and count; MD2–MD6 and DF1; Tasks 6–10; and the task order.
- **PA3 (2026-11-25, after PA2 at `6896ad2`, with Task 4 in progress; a test fix in a new task, and a plan-text clarification; no behavior change).** Task 3 said IP6 reads `ledger.db`, `-wal`, and `-shm` "as IP1 reads them", but the committed IP6 (`ImportPrivacyTests.IP6_AnMcpToolCall_KeepsOnlyItsHashedNameAndToolName`) doesn't. It calls `SqliteConnection.ClearAllPools()` right after its import, then runs two `LedgerSql.RowsAsync` queries, whose connection goes back to the pool and keeps the ledger open. It then reads each file with `FileRetry.ReadAllBytesAsync`, which opens it with `FileShare.Read`. Windows refuses that while SQLite has the file open for writing, so `FileRetry` retries for 10 seconds and throws. In a parallel run, another test's `ClearAllPools()` closes the pooled connection within those 10 seconds, which is why Task 3's run passed (`Tests: 2012 passed`); 2 of 2 parallel runs of `6896ad2` also passed while planning. Without that rescue, IP6 fails: `dotnet test CopilotLedger.slnx --filter "FullyQualifiedName~ImportPrivacyTests.IP6"` fails after 10 s with `IOException: The process cannot access the file '…\data\ledger.db' because it is being used by another process.` (`ImportPrivacyTests.cs:282`; PA3's line numbers are at `1744ecd`), and the serial run of `6896ad2` gives 2,011 passed and 1 failed (IP6 only). That breaks the global rule that every test passes with `xUnit.ParallelizeTestCollections=false`, and the final verification's serial gate. IP1 calls `ClearAllPools()` after its last `LedgerSql` query, right before its reads (`:63`), and reads each file through `new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)` copied into a `MemoryStream` (`:70–73`). It has no helper. A new Task 10, tests only, right before the final verification, makes IP6 do both. Either change alone made IP6 pass alone (3 runs each); Task 10 makes both, as IP1 does. IP6's name, setup, and every assertion are unchanged. Sprint 12's IP_AM1 also reads with `FileRetry`, but it calls `ClearAllPools()` after its runs and queries the ledger only after its reads, so it passes serially and stays as it is. The final verification becomes Task 11 and runs at the commit after Task 10. In PA1 and PA2, "Task 10" is the final verification, including at the end of PA2's "Tasks 6–10". Checked on a scratch copy of `6896ad2` with Task 10's change, outside the repository and deleted afterward: 0 warnings, 0 errors; IP6 passed alone, alone with `xUnit.ParallelizeTestCollections=false`, and serially with the rest of `ImportPrivacyTests` (7 passed); and the whole suite gave 2,012 passed, 0 failed, 0 skipped, both in parallel and with `xUnit.ParallelizeTestCollections=false`. Task 5 gets one clarification. IN20's `withState: false` row writes no `daemon.json`, so for that row, "`daemon.json`'s text is unchanged" means that `<temp>/data/daemon.json` still doesn't exist after the dry run. `DaemonFiles.IsHeld` and `DaemonFiles.TryReadState` only read, and a dry run writes nothing to the data directory (`InstallCommands.cs:279`). The cell now says so. No test or theory row is added or removed, so the counts stay: 2,019 after Task 4, 2,030 after Task 5, 2,041 after Task 6, 2,055 after Tasks 7 and 8, 2,056 after Task 9, and 2,056 in Tasks 10 and 11. Task 11's serial run must show 2,056 passed. Changed: the Amended row; the Tasks list (the new Task 10, and the final verification as Task 11); "(now Task 11, PA3)" after PA1's two mentions of Task 10 and PA2's one; IN20's Expected cell; the new Task 10 section; the final verification's heading, its first line, and its serial gate, which now states its counts; "Changed tests" under "Tests added this sprint"; and "After Task 11 (orchestrator)". Unchanged: Tasks 1–3 and their text, including Task 3's IP6 specification; Task 4, which is in progress; Tasks 5–9 apart from IN20's Expected cell; every contract, decision, fixture, other test vector, docs text, and count; what the final verification checks; and the order of Tasks 1–9.

## Task 1: MCP Tool Names: Hash, Shape, Short Form, and Display

**Files:** new `src/CopilotLedger.Core/Telemetry/McpToolNames.cs`; new `tests/CopilotLedger.Tests/Telemetry/McpToolNamesTests.cs`.

**Contract:**

```csharp
namespace CopilotLedger.Core.Telemetry;

// Copilot CLI's live execute_tool span names a tool from an MCP server by gen_ai.tool.name = the lowercase hexadecimal
// SHA-256 of the server's name in UTF-8, "/", and the SHA-256 of the tool's name in UTF-8: 129 characters. The session log
// names the same call in clear, so import learns the readable name of each hashed one, and reports show it.
public static class McpToolNames
{
    public const int HashedLength = 129;

    // serverName's tool toolName as Copilot CLI's live spans name it. Throws ArgumentNullException for null.
    public static string Hash(string serverName, string toolName);

    // 64 lowercase hexadecimal digits, "/", and 64 more.
    public static bool IsHashed([NotNullWhen(true)] string? name);

    // "MCP tool " + the first 8 digits + "/" + the first 8 digits after the "/". Throws ArgumentException unless IsHashed.
    public static string ShortForm(string hashedName);

    // What reports show for a stored tool name: for a hashed name, the name a session log gave it (learned, by hashed name),
    // else its ShortForm; any other name, and null, as it is. Throws ArgumentNullException for a null learned.
    public static string? Display(string? name, IReadOnlyDictionary<string, string> learned);
}
```

**Algorithm:**

1. `Hash`: `Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(serverName))) + "/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(toolName)))`.
2. `IsHashed`: `name` has length 129, `name[64] == '/'`, and every other character is `0`–`9` or `a`–`f`.
3. `ShortForm`: throw `new ArgumentException("The name isn't a hashed MCP tool name.", nameof(hashedName))` unless `IsHashed`; return `"MCP tool " + hashedName[..8] + "/" + hashedName[65..73]`.
4. `Display`: `ArgumentNullException.ThrowIfNull(learned)`. If `!IsHashed(name)`, return `name`. Return `learned.TryGetValue(name, out string? readable) ? readable : ShortForm(name)`.

**Tests (`McpToolNamesTests`).** Private constants:

- `LedgerHash` = `"fe14010b4fe83303852f0467c919ef9a7ca089b91e96e3aad7d426dd87079297/eb20c46435d60871da0eb97ce23417c04835e272725c59a3a649c7ce8e521d97"`;
- `ExecJs` = `"10be6beef496b89dae799c37bd04e5eba2a6264a0a4d605f5661b50d7dc4d1ea/8453dc19fda259c463d7aaffb07becf26b7ce2cfc690a2b0a957e201653dc85a"`.

| ID | Name | Vectors | Expected |
|---|---|---|---|
| MN1 | `MN1_Hash_IsTheSha256OfEachNameInUtf8` (theory, 4 rows) | (`ledger`, `get_branch_ledger`); (`computer-use`, `exec_js`); (`"caf\u00e9"`, `exec_js`); (`""`, `""`) | `LedgerHash`; `ExecJs`; `850f7dc43910ff890f8879c0ed26fe697c93a067ad93a7d50f466a7028a9bf4e/8453dc19fda259c463d7aaffb07becf26b7ce2cfc690a2b0a957e201653dc85a`; `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855/e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| MN2 | `MN2_IsHashed_AcceptsOnlyTheHashedShape` (theory, 11 rows of `(string? name, bool expected)`) | `LedgerHash`, `ExecJs`; then `null`, `""`, `"powershell"`, `ExecJs.ToUpperInvariant()`, `ExecJs[..128]`, `ExecJs + "0"`, `ExecJs.Replace('/', '-')`, `"g" + ExecJs[1..]`, `ExecJs[..63] + "/0" + ExecJs[65..]` | `true` for the first two; `false` for the other nine |
| MN3 | `MN3_ShortForm_IsTheFirst8DigitsOfEachHash` (theory, 2 rows) | `LedgerHash`; `ExecJs` | `"MCP tool fe14010b/eb20c464"`; `"MCP tool 10be6bee/8453dc19"` |
| MN4 | `MN4_ShortForm_RefusesAnotherName` | `ShortForm("powershell")` | throws `ArgumentException` |
| MN5 | `MN5_Display_ResolvesOnlyHashedNames` (theory, 5 rows of `(string? name, string? expected)`), with `learned` = `{ [ExecJs] = "computer-use-exec_js", ["powershell"] = "never shown" }` (ordinal) | `null`; `"powershell"`; `ExecJs`; `LedgerHash`; `ExecJs.ToUpperInvariant()` | `null`; `"powershell"`; `"computer-use-exec_js"`; `"MCP tool fe14010b/eb20c464"`; `ExecJs.ToUpperInvariant()` |
| MN6 | `MN6_Display_RequiresTheLearnedNames` | `Display("powershell", null!)` | throws `ArgumentNullException` |

**Acceptance:** MN1–MN6 pass: **24 tests**, so 1,984 passed. No other file changes.

**Verification:** `dotnet build CopilotLedger.slnx`; `dotnet test CopilotLedger.slnx`.

## Task 2: The `mcp_tool_names` Table, Ensured on Every Open

**Files:**

- new `src/CopilotLedger.Core/Import/LearnedToolName.cs`
- `src/CopilotLedger.Storage/LedgerMigrations.cs`
- `src/CopilotLedger.Storage/SqliteLedgerStore.cs`
- `src/CopilotLedger.Storage/SqliteLedgerStore.Imports.cs`
- `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`
- `src/CopilotLedger.Storage/LedgerExportSnapshot.cs`
- new `tests/CopilotLedger.Tests/Storage/McpToolNameStoreTests.cs`

`LedgerMigrations.Migrations`, every migration's text, and `Indexes` are unchanged.

**Contracts:**

```csharp
// CopilotLedger.Core.Import
// An MCP tool's hashed name, as Copilot CLI's live spans give it (McpToolNames.Hash of a tool.execution_start's
// data.mcpServerName and data.mcpToolName), and that event's data.toolName.
public sealed record LearnedToolName(string HashedName, string ToolName);

// LedgerMigrations, before Indexes
// Tables that every ledger of the latest format has. Opening the ledger adds one that's missing, without changing
// user_version: an earlier build of the same format ignores it.
// mcp_tool_names: the names that import learned for MCP tools whose live spans Copilot names by hashes (McpToolNames).
public static IReadOnlyList<(string Name, string Sql)> Tables { get; } =
[
    ("mcp_tool_names",
     """
     CREATE TABLE IF NOT EXISTS mcp_tool_names (
         hashed_name TEXT NOT NULL PRIMARY KEY CHECK (length(hashed_name) = 129),
         tool_name   TEXT NOT NULL CHECK (length(tool_name) BETWEEN 1 AND 256)
     ) STRICT, WITHOUT ROWID;
     """),
];

// SqliteLedgerStore (SqliteLedgerStore.Imports.cs)
// Records names that import learned, under one BEGIN IMMEDIATE transaction: adds each new hashed name, and for one it knows
// keeps the smaller tool name by SQLite's BINARY collation. Validates every name first: an ArgumentException when one's
// HashedName isn't McpToolNames.IsHashed or its ToolName isn't 1 to 256 characters long, and nothing is written. Returns
// without opening the ledger when names is empty.
public async Task RecordMcpToolNamesAsync(IReadOnlyList<LearnedToolName> names, CancellationToken cancellationToken = default);

// SqliteLedgerStore (SqliteLedgerStore.Export.cs)
// Every learned MCP tool name, by hashed name (ordinal).
internal static async Task<IReadOnlyDictionary<string, string>> ReadMcpToolNamesAsync(
    SqliteTransaction snapshot,
    CancellationToken cancellationToken);

// LedgerExportSnapshot
// The MCP tool names that import learned, by hashed name, for McpToolNames.Display.
public Task<IReadOnlyDictionary<string, string>> GetMcpToolNamesAsync(CancellationToken cancellationToken = default) =>
    SqliteLedgerStore.ReadMcpToolNamesAsync(_transaction, cancellationToken);
```

**Algorithm (`InitializeAsync`).** Move the body of today's `Indexes` loop (`SqliteLedgerStore.cs:171–205`) into a private static helper, `EnsureSchemaObjectAsync(SqliteConnection connection, string type, string name, string sql, CancellationToken cancellationToken)`, whose two queries become `SELECT COUNT(*) FROM sqlite_master WHERE type = @type AND name = @name;`:

1. If the count, outside a transaction, is 1, return.
2. `BEGIN IMMEDIATE` (`connection.BeginTransaction(deferred: false)`).
3. If the count inside the transaction is 0, execute `sql` in the transaction.
4. Commit.

After the migration loop, call it for each of `LedgerMigrations.Tables` with `"table"`, then for each of `LedgerMigrations.Indexes` with `"index"`, then set `_initialized = true`. The index's behavior is unchanged. Give the helper a one-line comment: it adds a table or index of the latest format that the ledger lacks, without changing `user_version`.

**Algorithm (`RecordMcpToolNamesAsync`):**

1. `ArgumentNullException.ThrowIfNull(names)`; `EnsureInitialized()`.
2. For each name: null, `!McpToolNames.IsHashed(name.HashedName)`, or `name.ToolName is not { Length: >= 1 and <= 256 }` throws `new ArgumentException("Learned MCP tool names need a hashed name and a tool name of 1 to 256 characters.", nameof(names))`.
3. If `names.Count == 0`, return.
4. One connection, `BEGIN IMMEDIATE`, and for each name:

   ```sql
   INSERT INTO mcp_tool_names (hashed_name, tool_name) VALUES (@hashed_name, @tool_name)
   ON CONFLICT (hashed_name) DO UPDATE SET tool_name = excluded.tool_name
   WHERE excluded.tool_name < mcp_tool_names.tool_name;
   ```

5. Commit.

**Algorithm (`ReadMcpToolNamesAsync`):** `SELECT hashed_name, tool_name FROM mcp_tool_names;` in the snapshot transaction, into a `Dictionary<string, string>(StringComparer.Ordinal)`.

**Tests (`McpToolNameStoreTests`).** Constants `ExecJs` and `LedgerHash` as in Task 1. `Rows(path)` is `LedgerSql.RowsAsync(path, "SELECT hashed_name || '|' || tool_name FROM mcp_tool_names ORDER BY hashed_name;")`. `TableCount(path)` is `LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'mcp_tool_names';")`.

| ID | Name | Vector | Expected |
|---|---|---|---|
| MT1 | `MT1_ANewLedger_HasTheTable` | `new SqliteLedgerStore(path).InitializeAsync()` | `RowsAsync(path, "SELECT name \|\| '\|' \|\| type \|\| '\|' \|\| pk \|\| '\|' \|\| \"notnull\" FROM pragma_table_info('mcp_tool_names') ORDER BY cid;")` is `["hashed_name\|TEXT\|1\|1", "tool_name\|TEXT\|0\|1"]`. `ScalarAsync(path, "SELECT type \|\| '\|' \|\| ncol \|\| '\|' \|\| wr \|\| '\|' \|\| strict FROM pragma_table_list('mcp_tool_names');")` is `"table\|2\|1\|1"`. `PRAGMA user_version` is 7. |
| MT2 | `MT2_AFormat7LedgerWithoutTheTable_GainsIt` | Initialize; record `ImportEntries.Live("c-1", null)`; `ClearAllPools()`; `DROP TABLE mcp_tool_names;`; `ClearAllPools()`; a new store on the path, initialized | `TableCount` 1; `user_version` 7; `SELECT COUNT(*) FROM spans` 1 |
| MT3 | `MT3_AFormat6Ledger_UpgradesAndGainsTheTable` | Initialize; record the same span; `ClearAllPools()`; run `DROP TABLE mcp_tool_names; DROP INDEX ix_spans_conversation_live; DROP INDEX ix_span_links_import_span; ALTER TABLE spans DROP COLUMN list_cost_usd_pico; ALTER TABLE spans DROP COLUMN charge_source; ALTER TABLE import_sessions DROP COLUMN import_revision; PRAGMA user_version = 6;`; `ClearAllPools()`; a new store, initialized | `user_version` 7; `TableCount` 1; the span count 1; its `charge_source` is `rate_card` |
| MT4 | `MT4_ConcurrentOpens_CreateOneTable` | `Task.WhenAll` of two `InitializeAsync` calls on two stores of one new path, then a third | No exception; `TableCount` 1; `user_version` 7 |
| MT5 | `MT5_LearnedNames_KeepTheOrdinallySmallestName` | On an initialized store: record `[(ExecJs, "computer-use-exec_js"), (LedgerHash, "ledger-get_branch_ledger")]` (A); record `[(ExecJs, "zzz")]` (B); record `[(ExecJs, "computer-use/exec_js")]` (C); record A again (D); record `[(ExecJs, "computer-use-exec")]` (E) | After A, B, C, and D, `Rows` is `[ExecJs + "\|computer-use-exec_js", LedgerHash + "\|ledger-get_branch_ledger"]`. After E, `[ExecJs + "\|computer-use-exec", LedgerHash + "\|ledger-get_branch_ledger"]`. |
| MT6 | `MT6_AnInvalidName_WritesNothing` (theory, 4 rows of `(string hashedName, string toolName)`) | `("powershell", "x")`; `(ExecJs.ToUpperInvariant(), "x")`; `(ExecJs, "")`; `(ExecJs, new string('x', 257))`. Each records `[new(LedgerHash, "ledger-get_branch_ledger"), new(hashedName, toolName)]` | `ArgumentException`; `SELECT COUNT(*) FROM mcp_tool_names` is 0 |
| MT7 | `MT7_TheExportSnapshot_ReadsTheNames` | On a new initialized store, `GetMcpToolNamesAsync()` of an export snapshot (N0); record A of MT5; a new snapshot's names (N1) | N0 is empty. N1 has exactly 2 entries: `N1[ExecJs]` is `computer-use-exec_js` and `N1[LedgerHash]` is `ledger-get_branch_ledger`. |
| MT8 | `MT8_ADryRunImport_NeverCreatesTheTable` | `CliHarness` (the default constructor); `new CopilotSessionLogs("s-a").Usage("u-1", R1)` written to `harness.Context.CopilotHome`, with `R1` = `"11111111-1111-4111-8111-111111111111"`; initialize the harness's ledger; `ClearAllPools()`; `DROP TABLE mcp_tool_names;`; `ClearAllPools()`; run `import --all --dry-run --json`; then `import --all --json` | After the dry run: exit code 0 and `TableCount` 0. After the import: exit code 0 and `TableCount` 1. |

**Acceptance:**

- MT1–MT8 pass: **11 tests**, so 1,995 passed. Sprint 14's `ConversationLiveIndexTests` and every existing test pass unchanged.
- `git diff c6dc935 -- src/CopilotLedger.Storage/LedgerMigrations.cs` adds only `Tables` and its comment.
- No `CopilotSessionLogSource`, importer, CLI, or docs change.

**Verification:** `dotnet build CopilotLedger.slnx`; `dotnet test CopilotLedger.slnx`.

## Task 3: Import Learns MCP Tool Names

**Files:**

- `src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs`
- `src/CopilotLedger.Core/Import/SessionLogReadResult.cs`
- `src/CopilotLedger.Storage/Import/LedgerImporter.cs`
- `docs/cli.md`
- `tests/CopilotLedger.Tests/TestSupport/CopilotSessionLogs.cs`
- `tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs`
- `tests/CopilotLedger.Tests/Import/ImportRepairTests.cs`
- new `tests/CopilotLedger.Tests/Import/McpToolNameImportTests.cs`

**Contracts:**

```csharp
// SessionLogReadResult, after ToolCalls
public IReadOnlyList<LearnedToolName> LearnedToolNames { get; init; } = [];   // a Read Copilot log's MCP tools: distinct, ordinal by HashedName, then ToolName

// LedgerImporter
public const int CurrentImportRevision = 2;   // 2: every log is read once more, to learn its MCP tools' names
```

**Algorithm (Copilot source):**

1. Add `("data.mcpServerName", 256)` and `("data.mcpToolName", 256)` to `ToolStartPaths`, after `("data.toolName", 256)`. Nothing else is added to any path tree.
2. `Session` gets `private readonly HashSet<LearnedToolName> _learned = [];`.
3. In `ToolStart`, after `Chain(...)` and the reads of `toolCallId` and `toolName`, and before the existing early `return`: if `fields.Duplicate is null && toolName is not null && fields.String("data.mcpServerName") is string server && fields.String("data.mcpToolName") is string tool`, add `new LearnedToolName(McpToolNames.Hash(server, tool), toolName)`. The rest of `ToolStart` is unchanged.
4. In `Finish`, set `LearnedToolNames = [.. _learned.OrderBy(name => name.HashedName, StringComparer.Ordinal).ThenBy(name => name.ToolName, StringComparer.Ordinal)]` on the `Read` result only. `SessionLogs.NotRead` and Codex are unchanged, so their results keep the empty default.

**Algorithm (importer).** In `ImportLogAsync`, inside the existing `if (store is not null)` block that writes the checkpoint and before that write, add `if (result.LearnedToolNames.Count > 0) { await store.RecordMcpToolNamesAsync(result.LearnedToolNames, cancellationToken); }`. Update the comment above the block to start "The log's learned MCP tool names, then its checkpoint, follow its last batch." This runs for every log whose status is `Read`, whatever its records' scope, resolutions, or tool calls' age. Nothing else changes, including the report and the skip check, which now compares with revision 2.

**Fixture (`CopilotSessionLogs.ToolStart`):** add optional parameters `string? mcpServerName = null, string? mcpToolName = null` after `parentId`. When one is given, set `data["mcpServerName"]` or `data["mcpToolName"]` to it before the `Event(...)` call; their position in `data` doesn't matter, because the reader doesn't depend on order. Existing calls are unchanged.

**Changed tests:**

- `ImportRepairTests.RR1` (`Assert.Equal(1, …ImportRevision)` at line 35–37) and `RR8` (`Assert.Equal(1, checkpoint.ImportRevision)` at line 167): expect `2`. Nothing else in them changes.
- `ImportPrivacyTests`:
  - `LeftEmpty` gains `["mcp_tool_names"] = [],`;
  - IP3's array gains `("mcp_tool_names", "1"),`;
  - in `WriteFixtures`, `.ToolStart("toolu-3", "grep", agentId: "agent-1")` becomes `.ToolStart("toolu-3", "computer-use-exec_js", agentId: "agent-1", mcpServerName: "computer-use", mcpToolName: "exec_js")`.

  IP1–IP5's assertions are unchanged, and so are IP1's counts (6 spans and 3 links).

**New test in `ImportPrivacyTests`:**

`IP6_AnMcpToolCall_KeepsOnlyItsHashedNameAndToolName`:

1. `Harness(temp)`. Write `new CopilotSessionLogs("s-mcp")`, then:
   - `.Usage("u1", S1, numToolCalls: 1)`;
   - `.Event("tool.execution_start", { toolCallId: "toolu-m1", toolName: "srv-tool", mcpServerName: Canary("SERVER"), mcpToolName: Canary("TOOL"), arguments: { query: Canary("ARGS") }, toolTitle: Canary("PROMPT") })`;
   - `.Event("tool.execution_complete", { toolCallId: "toolu-m1", success: false, result: { content: Canary("RESULT") }, error: { code: "failure", message: Canary("ERRMSG") } })`;

   to `harness.Context.CopilotHome`.
2. Run `import --all --source copilot --json`, then `SqliteConnection.ClearAllPools()`.
3. Expect:
   - exit code 0;
   - `SELECT hashed_name || '|' || tool_name FROM mcp_tool_names;` is exactly `["f3f23e35c9fc32bd46be8fb3989b4952f3dccaf5dbc4721da91359972275a9d5/74ef83b0e3a641bf9d96ddd6e6dcf473f87891e975cbc4782d0be0a03f8c8cb4|srv-tool"]`;
   - `SELECT tool_name FROM span_links;` is exactly `["srv-tool"]`;
   - none of `Canary("SERVER")`, `Canary("TOOL")`, `Canary("ARGS")`, `Canary("PROMPT")`, `Canary("RESULT")`, `Canary("ERRMSG")`, and `CopilotSessionLogs.Canary` is in the run's output or error, or, as UTF-8 bytes, in `ledger.db`, `ledger.db-wal`, or `ledger.db-shm` when they exist (read as IP1 reads them).

**Tests (`McpToolNameImportTests`).** Constants: `ExecJs` and `LedgerHash` as in Task 1; `SessionId` = `"0f8fad5b-d9cb-469f-a165-70867728950e"`; `S1` = `"11111111-1111-4111-8111-111111111111"`; `S2` = `"22222222-2222-4222-8222-222222222222"`; `Now` = 2026-11-23 12:00:00 UTC. `Mcp(log, id)` is `log.ToolStart(id, "computer-use-exec_js", mcpServerName: "computer-use", mcpToolName: "exec_js")`. Source-level tests read with `CopilotRecordTests.ReadAsync(home)`. Importer-level tests run `new LedgerImporter(JsonRateCardCalculator.LoadEmbedded(), new ManualTimeProvider(Now)).RunAsync(...)` with `Sources = [new ImportSourceRequest(new CopilotSessionLogSource(), home)]`, as `CopilotToolLinkTests.ImportAsync` does, and read rows with `LedgerSql`. `Names(db)` is `SELECT hashed_name || '|' || tool_name FROM mcp_tool_names ORDER BY hashed_name;`.

| ID | Name | Log or setup | Expected |
|---|---|---|---|
| ML1 | `ML1_AnMcpToolStart_TeachesItsName` | `new CopilotSessionLogs(SessionId).Usage("u1", S1, numToolCalls: 1)`, `Mcp(…, "t1")`, `.ToolComplete("t1", true)` | Status `Read`; `LearnedToolNames` is `[new LearnedToolName(ExecJs, "computer-use-exec_js")]`; one tool call, named `computer-use-exec_js`; `CopilotRecordTests.AssertNoCanary(result)` |
| ML2 | `ML2_OnlyThreeValidStringsTeachAName` (theory, 6 rows) | After `session.start`, one `tool.execution_start` whose data is, by row: `no tool name`: `{toolCallId "t1", mcpServerName "computer-use", mcpToolName "exec_js"}`; `no MCP tool name`: `{toolCallId, toolName "computer-use-exec_js", mcpServerName "computer-use"}`; `a numeric server name`: `{toolCallId, toolName, mcpServerName 7, mcpToolName "exec_js"}`; `an empty MCP tool name`: `{toolCallId, toolName, mcpServerName "computer-use", mcpToolName ""}`; `a server name over 256 characters`: `{toolCallId, toolName, mcpServerName new string('s', 257), mcpToolName "exec_js"}`; `a repeated property`: the raw line `{"type":"tool.execution_start","data":{"toolCallId":"t1","toolName":"computer-use-exec_js","mcpServerName":"computer-use","mcpServerName":"other","mcpToolName":"exec_js"},"id":"00000000-0000-4000-8000-0000000000ff","timestamp":"2026-11-20T12:00:01.000Z","parentId":null}` | Each: status `Read`, `LearnedToolNames` empty |
| ML3 | `ML3_EachNameIsLearnedOnce_InOrder` | `Mcp(log, "t1")`; `Mcp(log, "t2")`; `.ToolStart("t3", "ledger-get_branch_ledger", mcpServerName: "ledger", mcpToolName: "get_branch_ledger")`; `.ToolStart("t4", "computer-use-exec_js-v2", mcpServerName: "computer-use", mcpToolName: "exec_js")`; `.ToolStart("t5", "powershell")` | `LearnedToolNames` is exactly `[(ExecJs, "computer-use-exec_js"), (ExecJs, "computer-use-exec_js-v2"), (LedgerHash, "ledger-get_branch_ledger")]` |
| ML4 | `ML4_AStartWithoutACallIdOrCompletion_StillTeachesItsName` | `.Event("tool.execution_start", {toolName "computer-use-exec_js", mcpServerName "computer-use", mcpToolName "exec_js"})` only | `LearnedToolNames` is `[(ExecJs, "computer-use-exec_js")]`; `ToolCalls` empty |
| ML5 | `ML5_ALogThatIsntRead_TeachesNothing` (theory, 2 rows of `(string version, SessionLogStatus status)`; PA1) | `("1.0.102", SessionLogStatus.AggregateOnly)`, below the window; `("1.0.116", SessionLogStatus.UnsupportedProducerVersion)`, above it. Each writes `new CopilotSessionLogs(SessionId, version)` with `Mcp(…, "t1")` | Each: status is the row's `status`; `LearnedToolNames` empty |
| ML6 | `ML6_ImportRecordsTheNamesOfLiveSessionsAndOldToolCalls` | An initialized store at `<temp>/ledger.db` with `RecordAsync([ImportEntries.Live("s-live", S1)])`. Log `s-live`: `.Usage("u1", S1, numToolCalls: 1)`, `Mcp(…, "t1")`, `.ToolComplete("t1", true)`. Log `s-old`, started 2026-11-01 12:00:00 UTC: `.Usage("u2", S2, numToolCalls: 1)`, `.ToolStart("t2", "ledger-get_branch_ledger", mcpServerName: "ledger", mcpToolName: "get_branch_ledger")`, `.ToolComplete("t2", true)`. Import both. | The Copilot report has `OverlapSkipped` 1, `Recorded` 1, and `ToolLinksRecorded` 0. `Names` is `[ExecJs + "\|computer-use-exec_js", LedgerHash + "\|ledger-get_branch_ledger"]`. `SELECT COUNT(*) FROM span_links` is 0. |
| ML7 | `ML7_AScopedImport_LearnsFromLogsOutOfScope` | Log `s-elsewhere` with `context: CopilotSessionLogs.Context(api, api, "main", "contoso/api", GitFixture.Sha1)`, where `api` = `<temp>/api`: `.Usage("u1", S1)`, `Mcp(…, "t1")`. Import with `RepositoryRoot = <temp>/repo` and `RepositorySlug = "swigerb/copilot-ledger"`. | `RecordsOutOfScope` 1 and `Recorded` 0; `Names` is `[ExecJs + "\|computer-use-exec_js"]` |
| ML8 | `ML8_ADryRun_LearnsNothing` | Initialize the ledger; log `s-a` with `Mcp(…, "t1")`; import with `DryRun = true`, then without | 0 rows after the dry run; `[ExecJs + "\|computer-use-exec_js"]` after the import |
| ML9 | `ML9_TheSmallestNameWins_WhateverTheOrder` | Two ledgers, each with its own home holding logs `s-a` and `s-b`, each log with one `tool.execution_start` of `computer-use`/`exec_js`. In ledger 1, `s-a`'s `toolName` is `computer-use-exec_js-v2` and `s-b`'s is `computer-use-exec_js`. In ledger 2, they're swapped. Import each. | `Names` is `[ExecJs + "\|computer-use-exec_js"]` in both |
| ML10 | `ML10_ALogOfAnEarlierRevision_IsReadOnceMore` | Log `s-a`: `.Usage("u1", S1, numToolCalls: 1)`, `Mcp(…, "t1")`, `.ToolComplete("t1", true)`. Import (run 1). Run `DELETE FROM mcp_tool_names; UPDATE import_sessions SET import_revision = 1;` with `LedgerSql`. Import (run 2), then import again (run 3). | Run 1: `Recorded` 1 and `ToolLinksRecorded` 1. Run 2: `SessionsSkippedUnchanged` 0, `Duplicates` 1, `Recorded` 0; `Names` is `[ExecJs + "\|computer-use-exec_js"]`; `GetImportSessionAsync(Copilot, "s-a").ImportRevision` is 2. Run 3: `SessionsSkippedUnchanged` 1, `Recorded` 0. |

**Docs (`docs/cli.md`):**

- **Import allowlist.** Add these two rows right before `<!-- /import-allowlist -->`:

  ```markdown
  | `mcp_tool_names.hashed_name` | The name that Copilot CLI's live spans give an MCP tool: the SHA-256 of `data.mcpServerName` of `tool.execution_start`, `/`, and the SHA-256 of its `data.mcpToolName`, in lowercase hexadecimal | — |
  | `mcp_tool_names.tool_name` | `data.toolName` of the same `tool.execution_start`; when logs name one tool differently, the first in ordinal order | — |
  ```

- **Read, never stored.** In the `- **Read, never stored:**` line, replace `Usage IDs, service request IDs, and tool call IDs are stored only as the SHA-256 hashes above.` with `Usage IDs, service request IDs, tool call IDs, and \`data.mcpServerName\` and \`data.mcpToolName\` of \`tool.execution_start\` are stored only as the SHA-256 hashes above.` Every phrase that IP5 pins stays.

**Acceptance:**

- ML1–ML10 and IP6 pass: **17 tests** (ML5 is 2 rows, PA1), so 2,012 passed.
- RR1 and RR8 pass with revision 2. IP1–IP5 pass with the changes above, which add assertions and weaken none.
- `git grep -n -E 'arguments|toolTitle|"result"' -- src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs` (single quotes, in PowerShell) finds nothing and exits 1, as at `c6dc935`. `git diff HEAD~1 -- src/CopilotLedger.Core/Import/CopilotSessionLogSource.cs` adds no path to any path tree other than `data.mcpServerName` and `data.mcpToolName`.
- No CLI, export, audit, or MCP file changes.

**Verification:** `dotnet build CopilotLedger.slnx`; `dotnet test CopilotLedger.slnx`.

## Task 4: Reports Show Readable MCP Tool Names

**Files:**

- `src/CopilotLedger.Core/Audit/AuditReport.cs`
- `src/CopilotLedger.Cli/Audit/AuditCommands.cs`
- `src/CopilotLedger.Mcp/LedgerMcpTools.cs`
- `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`
- `src/CopilotLedger.Cli/Export/ExportRecords.cs`
- `src/CopilotLedger.Cli/Export/ExportJson.cs`
- `src/CopilotLedger.Cli/Export/ExportCommands.cs`
- `src/CopilotLedger.Cli/Export/ExportReportModel.cs`
- `docs/cli.md`
- new `tests/CopilotLedger.Tests/Cli/McpToolNameDisplayTests.cs`
- new `tests/CopilotLedger.Tests/Audit/AuditToolNameTests.cs`
- new `tests/CopilotLedger.Tests/Docs/SprintFifteenDocsTests.cs`

**Contracts:**

```csharp
// AuditReport
// The report with each finding's Scope.Tool as McpToolNames.Display shows it, in the same order: the rules grouped and
// ordered the stored names, so the same findings are shown. Throws ArgumentNullException for a null learned.
public AuditReport WithToolNames(IReadOnlyDictionary<string, string> learned);

// ExportRecords
public static ExportToolCallRecord ToolCall(ExportToolCallRow row, IReadOnlyDictionary<string, string> toolNames);   // ToolName = McpToolNames.Display(row.ToolName, toolNames)

// ExportJson
public static async Task WriteToolCallsAsync(
    Stream stream,
    LedgerExportSnapshot snapshot,
    ExportInputs inputs,
    IReadOnlyDictionary<string, string> toolNames,
    CancellationToken cancellationToken);

// ExportReportModel, after ChainTurns
public required IReadOnlyDictionary<string, string> ToolNames { get; init; }   // learned MCP tool names by hashed name (LedgerExportSnapshot.GetMcpToolNamesAsync)
```

**Algorithm:**

1. **`WithToolNames`.** `ArgumentNullException.ThrowIfNull(learned)`. Return `this with { Findings = [.. Findings.Select(finding => finding.Scope.Tool is null ? finding : finding with { Scope = finding.Scope with { Tool = McpToolNames.Display(finding.Scope.Tool, learned) } })] }`.
2. **`audit`** (`AuditCommands.RunAsync`). Declare `IReadOnlyDictionary<string, string> toolNames;` beside `input`. Inside the existing `await using (LedgerExportSnapshot snapshot …)` block, after `GetAuditInputAsync`, set `toolNames = await snapshot.GetMcpToolNamesAsync(cancellationToken);`. Append `.WithToolNames(toolNames)` to the `AuditAnalyzer.Report(...)` expression. The table, `--json`, `--local-llm`, `--show-llm-payload`, and `--fail-on` all use that report, so they all show the names, and `--fail-on` decides as before.
3. **MCP `get_audit_findings`** (`LedgerMcpTools.AuditFindingsAsync`). The same as step 2, in its snapshot block and on its `AuditAnalyzer.Report(...)`.
4. **Export** (`ExportModelBuilder.BuildAsync`):
   1. After `LedgerFilter filter = inputs.Filter;`, read `IReadOnlyDictionary<string, string> toolNames = await snapshot.GetMcpToolNamesAsync(cancellationToken);`.
   2. Right after `AuditReport audit = await AuditAsync(...)`, add `audit = audit.WithToolNames(toolNames);`. The existing repository rewrite stays after it.
   3. Change `.Select(ExportRecords.ToolCall)` to `.Select(row => ExportRecords.ToolCall(row, toolNames))`.
   4. Set `ToolNames = toolNames` in the model.
5. **Bundle.** `ExportJson.WriteToolCallsAsync` passes its `toolNames` to `ExportRecords.ToolCall(row, toolNames)`. `ExportCommands.BundleEntries` passes `model.ToolNames`.

Nothing else changes. In particular, `AuditAnalyzer`, the rules, the store's queries, `AuditJson`, `AuditText`, `ExportSvg`, and `HtmlReportWriter` are unchanged: they display what they're given.

**Display tests (`McpToolNameDisplayTests`).** Constants:

- `H` = `"48484848484848484848484848484848"`;
- `ExecJs` as in Task 1;
- `Short` = `"MCP tool 10be6bee/8453dc19"`;
- `Learned` = `"computer-use-exec_js"`.

`Harness(root)` is `new CliHarness(root, new FakeCliPlatform(), new ManualTimeProvider(AuditSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5))`.

`SeedAsync(root)`:

1. `SqliteLedgerStore store = await AuditSeeds.CreateAsync(root);`.
2. `AuditSeeds.LiveTrace h = new(H, "conv-h", GitFixture.Feature(root) with { PullRequestNumber = 42 });` and `string h0 = h.Agent(0, AuditSeeds.At("19:00:00"));`.
3. `h.Request(1, h0, At("19:00:00"), "claude-haiku-4.5", 10_000, 8_000, 300)`.
4. `h.Tool(1, h0, At("19:00:10"), ExecJs, 1, 2)`.
5. `h.Request(2, h0, At("19:00:15"), …)`, with the same model and token counts.
6. `h.Tool(2, h0, At("19:00:20"), ExecJs, 1, 2)`.
7. `h.Request(3, h0, At("19:00:25"), …)`, with the same model and token counts.
8. `h.Tool(3, h0, At("19:00:30"), ExecJs, 1, 2)`.
9. `await h.RecordAsync(store)`.

This adds one `failed_tool_retry` finding for H: warning; 3 failures, 2 requests, and `breakerChurnFailures` 5; an impact of 2 × $0.0043 = $0.0086 (F11).

`LearnAsync(CliHarness cli)` writes `new CopilotSessionLogs("s-mcp").ToolStart("toolu-1", Learned, mcpServerName: "computer-use", mcpToolName: "exec_js").ToolComplete("toolu-1", true)` to `cli.Context.CopilotHome`, runs `import --all`, and asserts exit code 0.

`HFinding(JsonElement findings)` is the single element whose `scope.traceId` is H.

| ID | Name | Steps | Expected |
|---|---|---|---|
| MD1 | `MD1_AuditJson_ShowsTheShortForm_ThenTheLearnedName_WithTheSameFindings` | `SeedAsync`; `audit --json` (before); `LearnAsync`; `audit --json` (after) | Both exit 0. The H finding before and after: `ruleId` `failed_tool_retry`; `severity` `warning`; `scope.repository` `swigerb/copilot-ledger`; `scope.branch` `feature/ledger`; `scope.pullRequest` 42; `scope.agent` `(none)`; `scope.tool` `Short` before and `Learned` after; metrics, as `name`/`value`/`unit`: `failures` 3 `calls`, `requests` 2 `requests`, and `breakerChurnFailures` 5 `calls`; `estimatedImpactUsd`'s `GetDecimal()` is `0.0086m`; `windowStart` `2026-11-21T19:00:10.0000000+00:00`; `windowEnd` `2026-11-21T19:00:30.0000000+00:00`. `before.Out.Replace("\"tool\": \"" + Short + "\"", "\"tool\": \"" + Learned + "\"")` equals `after.Out` exactly. Neither output contains `ExecJs`. After learning, `SELECT DISTINCT tool_name FROM span_links WHERE trace_id = '<H>' AND operation_name = 'execute_tool'` is exactly `[ExecJs]`: H's tool calls keep their hashed name (PA2). |
| MD2 | `MD2_TheAuditTable_NamesTheTool` | `SeedAsync`; `audit` (before); `LearnAsync`; `audit` (after) | Before contains `swigerb/copilot-ledger @ feature/ledger, (none), MCP tool 10be6bee/8453dc19`. After contains `swigerb/copilot-ledger @ feature/ledger, (none), computer-use-exec_js`, and not `Short`. Neither contains `ExecJs` or `ExecJs[..64]`. |
| MD3 | `MD3_TheLocalModelPayload_NamesTheTool` | `SeedAsync`; `audit --local-llm http://127.0.0.1:11434/v1 --local-llm-model llama3.3 --show-llm-payload` before and after `LearnAsync`. Parse the body, then `messages[1].content` as the summary, as `AuditDocsTests.AD2` does | The summary's non-null `findings[].scope.tool` values, ordinal: `["MCP tool 10be6bee/8453dc19", "bash"]` before and `["bash", "computer-use-exec_js"]` after. Neither output contains `ExecJs[..64]`. |
| MD4 | `MD4_TheExport_NamesTheTool_OnTheTimelineInTheBundleAndInTheFindings` | `SeedAsync`; `export --all-repos --since 7d --bundle` (before); `LearnAsync`; the same with `--force` (after). Each time, read the report with `ExportFiles.ReadReport` and `ExportFiles.Parse`, and the bundle with `ExportFiles.ReadZip`. | With `N` = `Short` before and `Learned` after: the report's `title` elements (by local name) include `14:00:10 · N · exit code 1`, `14:00:20 · N · exit code 1`, and `14:00:30 · N · exit code 1`; some `td` element's text starts with `swigerb/copilot-ledger @ feature/ledger, (none), N; `; `telemetry/tool-calls.json`'s `toolCalls` whose `traceId` is H are 3, each with `toolName` N; `telemetry/audit-findings.json`'s H finding has `scope.tool` N. Neither the report nor any bundle entry contains `ExecJs`. |
| MD5 | `MD5_GetAuditFindings_NamesTheTool` | `GitFixture.Create(root)`; `SeedAsync`; an `McpTestHarness` with `AuditMcpTests`' context: `DatabasePath` `<root>/data/ledger.db`, `WorkingDirectory` `<root>/repo`, `GetEnvironmentVariable` `_ => null`, `GitResolver` `new FileSystemGitContextResolver(ceilingDirectory: root)`, the harness's clock, and `Version` `"9.9.9"`. Call `get_audit_findings` with `{}`; `LearnAsync`; call again. | The H finding's `scope.tool` is `Short`, then `Learned`. Neither text contains `ExecJs`. |

**Core test (`AuditToolNameTests`).**

`MD6_WithToolNames_ChangesOnlyTheToolOfEachFinding`:

1. Build `input` = `AuditFixtures.Input([AuditFixtures.Span(1)], calls)` with `Rules = [AuditRuleIds.FailedToolRetry]`. `calls` are `AuditFixtures.Tool(n, TimeSpan.FromSeconds(10 * n), name, exitCode: 1, statusCode: 2)` for `n` = 1…9, where `name` is `ExecJs` for 1–3, `LedgerHash` for 4–6, and `"view"` for 7–9.
2. `report` = `AuditAnalyzer.Report(input, AuditFixtures.Start, new AuditReportFilter { Since = "7d", Period = "last 7 days", ToUtc = AuditFixtures.Start }, AuditSeverity.Info, null)`.
3. `resolved` = `report.WithToolNames(new Dictionary<string, string>(StringComparer.Ordinal) { [ExecJs] = "computer-use-exec_js" })`.

Expected:

- `report.Findings`' tools are `[ExecJs, LedgerHash, "view"]`, and `resolved.Findings`' are `["computer-use-exec_js", "MCP tool fe14010b/eb20c464", "view"]`.
- With `Scope.Tool` set to null in each, the two lists of findings are equal (record equality, in order).
- `resolved.Shown.Count` equals `report.Shown.Count`.
- `report.WithToolNames(null!)` throws `ArgumentNullException`.

**Docs (`docs/cli.md`):**

- **New subsection.** Right after the `### Repairs` paragraph, before `### The import report`:

  ```markdown
  ### MCP tool names

  Copilot CLI's live spans name a tool from an MCP server by two lowercase hexadecimal SHA-256 hashes, of the server's name and of the tool's name, joined by `/`. Its session log names the tool in clear, so each import learns the name from the logs it reads: `data.toolName` of `tool.execution_start`, such as `computer-use-exec_js`, the name that imported tool calls show. Export, audit, and the MCP server show it for live calls too. A tool that no log has named yet shows as `MCP tool` and the first 8 digits of each hash, such as `MCP tool 10be6bee/8453dc19`. The ledger keeps the hashes, and the audit's tool rules group calls by them.
  ```

- **Export.** In the paragraph that starts `The report shows summary cards`, after `It loads nothing and works without JavaScript.`, insert: `Tool calls and findings name MCP tools as [MCP tool names](#mcp-tool-names) describes.`
- **Audit.** Right after the bullet that starts `- Tool calls are kept for 7 days`, add: `- The tool rules group calls by the tool name that the ledger keeps, and findings name MCP tools as [MCP tool names](#mcp-tool-names) describes.`
- **Troubleshooting.** Replace the bullet that starts `- **Tool names are 129-character hashes.**` with: `- **A tool shows as \`MCP tool 10be6bee/8453dc19\`.** Copilot CLI's live spans name an MCP tool by hashes, and no session log that import has read has named that tool yet. After its session ends, run \`gh ledger import --all\`; see [MCP tool names](#mcp-tool-names).`

**Docs test (`SprintFifteenDocsTests`, new).** A `RepositoryFile` helper as in `UpgradeDocsTests`, and `Text(params string[] parts)` reading a file with `\n` line endings.

`DF1_TheReferenceDescribesMcpToolNames`:

- `docs/cli.md` contains `### MCP tool names`, `McpToolNames.ShortForm(McpToolNames.Hash("computer-use", "exec_js"))`, and `[MCP tool names](#mcp-tool-names)`;
- it doesn't contain `129-character`.

**Acceptance:**

- MD1–MD6 and DF1 pass: **7 tests**, so 2,019 passed.
- Every existing audit, export, MCP, guide, and docs test passes unchanged: `AuditDocsTests`, `ExportAllowlistTests`, `GuideSamplesTests`, `SprintThirteenDocsTests`, `AuditMcpTests.AM1`, and `ExportAuditTests.EF2`.
- `git diff HEAD~1 -- src/CopilotLedger.Core/Audit` changes only `AuditReport.cs`.

**Verification:** `dotnet build CopilotLedger.slnx`; `dotnet test CopilotLedger.slnx`.

## Task 5: `install --dry-run` Decides the Daemon Step as `install` Does

**Files:**

- `src/CopilotLedger.Cli/Daemon/DaemonCommands.cs`
- `src/CopilotLedger.Cli/Install/InstallCommands.cs`
- `docs/cli.md`
- `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs`
- `tests/CopilotLedger.Tests/Docs/SprintFifteenDocsTests.cs`

**Contracts (all internal, in `DaemonCommands.cs`, namespace `CopilotLedger.Cli.Daemon`):**

```csharp
// What daemon start decides before it starts anything.
internal enum DaemonStartOutcome
{
    AlreadyRunning,   // the daemon holds its lock
    NotExecutable,    // this process isn't the gh-ledger executable
    Blocked,          // the terminal's Windows job would stop the daemon, or can't be read
    Start,
}

internal sealed record DaemonStartDecision
{
    public required DaemonStartOutcome Outcome { get; init; }
    public int? ProcessId { get; init; }      // AlreadyRunning: daemon.json's processId; null when it can't be read
    public string? Executable { get; init; }  // NotExecutable: the process's path; null when unknown
    public string? JobError { get; init; }    // Blocked: why the job can't be read; null when the job blocks
    public bool SignInItem { get; init; }     // Blocked: a sign-in item exists, or signInItemPlanned
}

// DaemonCommands
// Decides daemon start without side effects: DaemonFiles.IsHeld opens and closes the lock file only when it exists, and
// daemon.json, the process path, the job, and the sign-in item are only read. signInItemPlanned: install's dry run would
// have registered the Windows sign-in item before starting the daemon.
internal static DaemonStartDecision DecideStart(CliContext context, bool signInItemPlanned = false);

// Writes what daemon start says for a decision other than Start, and returns its exit code: AlreadyRunning to standard
// output, 0; NotExecutable and Blocked to standard error, 1. With dryRun, Blocked's first line starts "Wouldn't start the
// gh-ledger daemon: " instead of "gh-ledger daemon not started: ". Throws ArgumentException for Start.
internal static int WriteStartDecision(CliContext context, DaemonStartDecision decision, bool dryRun);
```

**Algorithm (`DecideStart`)** keeps `StartAsync`'s order:

1. If `DaemonFiles.IsHeld(context)`, return `AlreadyRunning` with `ProcessId = DaemonFiles.TryReadState(context)?.ProcessId`.
2. `executable = context.Platform.ProcessPath`. If `!GetExecutableName(executable).Equals("gh-ledger", StringComparison.OrdinalIgnoreCase)`, return `NotExecutable` with `Executable = executable`.
3. `try { job = context.Platform.GetJobState(); } catch (Win32Exception ex) { job = default; jobError = ex.Message; }`, as `StartAsync` does (PA2). If `jobError is not null || DetachedLaunchPolicy.Decide(job) == DetachedLaunch.Blocked`, return `Blocked` with `JobError = jobError` and `SignInItem = signInItemPlanned || context.Platform.GetStartupCommand("CopilotLedger") is not null`.
4. Return `Start`.

**Algorithm (`WriteStartDecision`):**

1. `AlreadyRunning`: write to `context.Out` `gh-ledger daemon is already running (pid <n>).`, or `gh-ledger daemon is already running.` without a pid. Return 0.
2. `NotExecutable`: write to `context.Error` `Run this command from the gh-ledger executable (current process: <executable, or unknown>).` Return 1.
3. `Blocked`. Let `reason` be `this terminal runs in a Windows job that stops the processes it started when it closes, so the daemon would stop with it.` when `JobError` is null, else `can't read this terminal's Windows job, so the daemon might stop when the terminal closes (<JobError>).`. Write to `context.Error`:
   1. `(dryRun ? "Wouldn't start the gh-ledger daemon: " : "gh-ledger daemon not started: ") + reason`;
   2. with `command = GhExtension.CommandName(context.Platform)`: `Run <command> daemon start from a regular terminal, or sign out and back in to start it from the sign-in item.` when `SignInItem`, else `Run <command> daemon start from a regular terminal.`

   Return 1.
4. `Start`: throw `new ArgumentException("Start has nothing to report.", nameof(decision))`.

**Algorithm (`StartAsync`).** Replace its three checks (`DaemonCommands.cs:65–98`) with `DaemonStartDecision decision = DecideStart(context); if (decision.Outcome != DaemonStartOutcome.Start) { return WriteStartDecision(context, decision, dryRun: false); }`. Then `string executable = context.Platform.ProcessPath!;`; the rest, from `Directory.CreateDirectory(context.DataDirectory)`, is unchanged. Every message, stream, and exit code of `daemon start` stays as it is.

**Algorithm (`InstallAsync`):**

1. Declare `bool signInItemPlanned = false;` before the sign-in item block. Where the dry run writes `Would start gh-ledger when you sign in.`, also set `signInItemPlanned = context.Platform.IsWindows;`. Only Windows, because only there does `daemon start` read the sign-in item (`GetStartupCommand`).
2. Replace the `if (dryRun) { … "Would start the gh-ledger daemon." … }` branch with:
   1. `DaemonStartDecision decision = DaemonCommands.DecideStart(context, signInItemPlanned);`.
   2. If `decision.Outcome == DaemonStartOutcome.Start`, write `Would start the gh-ledger daemon.` to `context.Out` and set `exitCode = 0`.
   3. Otherwise, `exitCode = DaemonCommands.WriteStartDecision(context, decision, dryRun: true);`.

   The `else` branch (`StartAsync`) and the final line are unchanged.

**Tests (`InstallCommandTests`, reusing its `WindowsPlatform()`, `WindowsExtension`, `ManifestPath`, and `Normalize`).** Let:

- `Job` = `Wouldn't start the gh-ledger daemon: this terminal runs in a Windows job that stops the processes it started when it closes, so the daemon would stop with it.`
- `Unreadable` = `Wouldn't start the gh-ledger daemon: can't read this terminal's Windows job, so the daemon might stop when the terminal closes (Access is denied.).`
- `SignIn(c)` = `Run <c> daemon start from a regular terminal, or sign out and back in to start it from the sign-in item.`
- `Regular(c)` = `Run <c> daemon start from a regular terminal.`
- `Sh` = `Warning: GitHub CLI needs sh.exe from Git for Windows to run shell aliases, so gh ledger won't work until Git for Windows is installed. Run gh-ledger directly.`

The four blocked cases, as `TheoryData<string>`, build their platform as follows:

- `job`: `WindowsPlatform()` with `JobState = new(true, 0x2000)`;
- `unreadable`: `WindowsPlatform()` with `JobStateException = new Win32Exception(5, "Access is denied.")`;
- `extension`: `WindowsPlatform()` with `ProcessPath = WindowsExtension`, `@"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\manifest.yml"` in `ExistingFiles`, and `JobState = new(true, 0x2000)`;
- `no sign-in item`: `WindowsPlatform()` with `ProcessPath = Long`, where `Long` is `@"C:\" + new string('a', 250) + @"\gh-ledger.exe"`, and `JobState = new(true, 0x2000)`.

| ID | Name | Vector | Expected |
|---|---|---|---|
| IN20 | `IN20_DryRun_ReportsARunningDaemon` (theory: `withState` true, false) | `WindowsPlatform()`; create `<temp>/data`; when `withState`, write `<temp>/data/daemon.json` as `{"schemaVersion":1,"processId":4242,"startedAt":"2026-11-24T12:00:00+00:00","endpoint":"http://localhost:4318","version":"9.9.9","databasePath":"ledger.db","importRules":1}`; hold `<temp>/data/daemon.lock` open with `FileMode.OpenOrCreate`, `FileAccess.ReadWrite`, and `FileShare.None` while running `install --dry-run` and then `install` | Let `line` be `gh-ledger daemon is already running (pid 4242).` with `withState`, else `gh-ledger daemon is already running.` The dry run: exit 0; `Error` empty; `Out` is exactly these lines, each followed by `\n`: `Would set COPILOT_OTEL_ENABLED=true for your user account.`, `Would set gh alias: gh ledger runs C:\tools\gh-ledger.exe.`, `Sh`, `Would start gh-ledger when you sign in.`, `line`, and `Dry run: no changes made.` After the dry run, `UserVariableWrites`, `StartupCommandWrites`, `ProcessRuns`, and `DetachedStarts` are empty, there's no `install.json`, and `<temp>/data/daemon.json` is as it was: with `withState`, its text is unchanged; without it, it still doesn't exist (PA3). The install: exit 0; `Out` contains `line + "\n"` and ends with `gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n`; `DetachedStarts` empty. |
| IN21 | `IN21_DryRun_InABlockingJob_SaysItWouldntStartTheDaemon` (theory, the 4 cases) | `install --dry-run` | Exit 1. `Out` and `Error`, by case, below. In every case `UserVariableWrites`, `StartupCommandWrites`, `ProcessRuns`, and `DetachedStarts` are empty, and there's no `install.json`. |
| IN22 | `IN22_DryRun_DecidesTheDaemonLikeInstall` (theory, the 4 cases) | `install --dry-run` in one temporary directory, and `install` in another, each with a new platform of the case | Both exit 1. Splitting each `Error` on `\n` gives 3 parts, the last empty. The second lines are equal. The real first line starts with `gh-ledger daemon not started: `, and the dry first line is `Wouldn't start the gh-ledger daemon: ` plus the rest of the real one. The real run's `DetachedStarts` is empty. |

IN21's expected output, each line followed by `\n`:

- `job`: `Out` is `Would set COPILOT_OTEL_ENABLED=true for your user account.`, `Would set gh alias: gh ledger runs C:\tools\gh-ledger.exe.`, `Sh`, `Would start gh-ledger when you sign in.`, and `Dry run: no changes made.`. `Error` is `Job` and `SignIn("gh-ledger")`.
- `unreadable`: `Out` as `job`. `Error` is `Unreadable` and `SignIn("gh-ledger")`.
- `extension`: `Out` is `Would set COPILOT_OTEL_ENABLED=true for your user account.`, `gh ledger runs this GitHub CLI extension, so no alias is needed.`, `Would start gh-ledger when you sign in.`, and `Dry run: no changes made.`. `Error` is `Job` and `SignIn("gh ledger")`.
- `no sign-in item`: `Out` is `Would set COPILOT_OTEL_ENABLED=true for your user account.`, `Would set gh alias: gh ledger runs <Long>.`, `Sh`, `Warning: the gh-ledger path is too long for a sign-in command, so gh-ledger won't start when you sign in. Run gh-ledger daemon start after you sign in.`, and `Dry run: no changes made.`. `Error` is `Job` and `Regular("gh-ledger")`.

**Docs (`docs/cli.md`).** Replace the paragraph that starts `Then restart the GitHub Copilot app, VS Code, and open terminals. \`--dry-run\`` with exactly:

`Then restart the GitHub Copilot app, VS Code, and open terminals. \`--dry-run\` prints the changes without making them, and exits with the code that \`install\` would. For the daemon, it says that it's already running, with its pid; or, in a terminal whose Windows job would stop it, \`Wouldn't start the gh-ledger daemon:\` with the reason and hint that \`daemon start\` gives; or \`Would start the gh-ledger daemon.\``

**Docs test (`SprintFifteenDocsTests`).**

`DF2_TheReferenceSaysWhatTheInstallDryRunReportsForTheDaemon`:

- `docs/cli.md` doesn't contain `but always says \`Would start the gh-ledger daemon.\``;
- it contains `` `Wouldn't start the gh-ledger daemon:` ``.

**Acceptance:**

- IN20–IN22 and DF2 pass: **11 tests**, so 2,030 passed.
- IN1–IN19, DJ1–DJ7, and DM1–DM7 pass unchanged; IN9 proves the unchanged case.
- `SprintThirteenDocsTests.DC1` passes unchanged: the refusal block it pins isn't edited.

**Verification:** `dotnet build CopilotLedger.slnx`; `dotnet test CopilotLedger.slnx`.

## Task 6: Help Names `gh ledger` Under the Extension

**Files:**

- new `src/CopilotLedger.Cli/Install/ExtensionHelpAction.cs`
- `src/CopilotLedger.Cli/GhLedgerCli.cs`
- `docs/cli.md`
- `docs/quickstart.md`
- new `tests/CopilotLedger.Tests/Cli/ExtensionHelpTests.cs`
- `tests/CopilotLedger.Tests/Docs/SprintFifteenDocsTests.cs`

**Contract:**

```csharp
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

namespace CopilotLedger.Cli.Install;

// --help, and the help after a parse error, of the GitHub CLI extension: System.CommandLine's help, whose usage line names
// the root command by the executable's name (rootName), with "gh ledger" in its place. Everything else is unchanged.
internal sealed class ExtensionHelpAction(HelpAction help, string rootName) : SynchronousCommandLineAction
{
    public override bool ClearsParseErrors => help.ClearsParseErrors;

    public override int Invoke(ParseResult parseResult);

    // help with "gh ledger" in place of rootName in its usage line: the first line after the first empty line that equals
    // "  " + rootName or starts with "  " + rootName + " ". The help's other lines, and its line breaks (newLine), are kept.
    internal static string Rewrite(string help, string rootName, string newLine);
}
```

**Algorithm (`Invoke`):**

1. `InvocationConfiguration configuration = parseResult.InvocationConfiguration;` and `TextWriter output = configuration.Output;`.
2. `StringWriter buffer = new(CultureInfo.InvariantCulture) { NewLine = output.NewLine };`.
3. `configuration.Output = buffer;`. Then `int exitCode; try { exitCode = help.Invoke(parseResult); } finally { configuration.Output = output; }`.
4. `output.Write(Rewrite(buffer.ToString(), rootName, output.NewLine));`, then return `exitCode`.

**Algorithm (`Rewrite`):**

1. `string[] lines = help.Split(newLine);`.
2. `int blank = Array.IndexOf(lines, string.Empty);` If it's negative, return `help`.
3. `string prefix = "  " + rootName;`. For `i` from `blank + 1` to the last line, at the first `lines[i]` that equals `prefix` or starts with `prefix + " "` (ordinal), set `lines[i] = "  gh ledger" + lines[i][prefix.Length..]` and stop.
4. Return `string.Join(newLine, lines)`.

**Algorithm (`GhLedgerCli.InvokeAsync`).** After the last `root.Add(...)` and before `root.Parse(args)`:

```csharp
if (GhExtension.IsInstalled(context.Platform))
{
    HelpOption help = root.Options.OfType<HelpOption>().Single();
    help.Action = new ExtensionHelpAction((HelpAction)help.Action!, root.Name);
}
```

Add `using System.CommandLine.Help;`. Nothing else in `GhLedgerCli` changes. A binary the user built gets no wrapper, so its help is byte-for-byte as before.

**Tests (`ExtensionHelpTests`).** Every test sets `CultureInfo.CurrentCulture` and `CultureInfo.CurrentUICulture` to `CultureInfo.InvariantCulture` and restores them in `finally`. Definitions:

- `Extension()` is a `FakeCliPlatform` with `IsWindows = true` and `ProcessPath = @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe"`, and `@"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\manifest.yml"` in `ExistingFiles`;
- `Built()` is `new FakeCliPlatform()`;
- `root` is `System.CommandLine.RootCommand.ExecutableName`.

Each run uses its own `CliHarness` over the test's temporary directory.

| ID | Name | Vector | Expected |
|---|---|---|---|
| HL1 | `HL1_UnderTheExtension_OnlyTheUsageLineChanges` (theory, 7 rows of `(string[] args, int exitCode, string usage)`) | `(["--help"], 0, "[command] [options]")`; `(["import", "--help"], 0, "import [options]")`; `(["budget", "set", "--help"], 0, "budget set <credits> [options]")`; `(["daemon", "stop", "-h"], 0, "daemon stop [options]")`; `(["budget", "set"], 1, "budget set <credits> [options]")`; `(["daemon"], 1, "daemon [command] [options]")`; `(["imprt"], 1, "[command] [options]")`. Run `args` with `Built()` and with `Extension()`. | Both exit with `exitCode`. Exactly one line of the built `Out` is `"  " + root + " " + usage`. The extension's `Out` equals the built `Out` with `"\n  " + root + " " + usage + "\n"` replaced by `"\n  gh ledger " + usage + "\n"`. The two `Error`s are equal. |
| HL2 | `HL2_DaemonStopsHelp_IsTheDefaultExceptForTheName` (theory: `extension` false, true) | `daemon stop --help` with `Built()` or `Extension()` | Exit 0; `Error` empty; `Out` is exactly `"Description:\n  Stop the running daemon.\n\nUsage:\n  " + name + " daemon stop [options]\n\nOptions:\n  -?, -h, --help  Show help and usage information\n\n"`, where `name` is `root` for the built binary and `gh ledger` for the extension |
| HL3 | `HL3_UnderTheExtension_EveryCommandsUsageLineStartsWithGhLedger` | With `Extension()`, start from the root (no path). For each path, run `[.. path, "--help"]`; the usage line is the line after the line `Usage:`. In the `Commands:` section, up to its first empty line, every line that matches `^  (?<name>[a-z][a-z-]*)( \|$)` adds the path `[.. path, name]`. Visit breadth-first. | Every run exits 0, and its usage line starts with `string.Join(' ', ["  gh ledger", .. path])` followed by a space. Exactly 29 runs. |

**Docs:**

- `docs/cli.md`: in the paragraph that starts `This page writes commands as`, delete the sentence `` `--help` usage lines say `gh-ledger`; with the extension, type `gh ledger`. `` (and the space before it). Nothing else in the paragraph changes.
- `docs/quickstart.md`: the last line, `Run any command with \`--help\` for its options. The help shows the program's own name, \`gh-ledger\`; with the extension, type \`gh ledger\`.`, becomes `Run any command with \`--help\` for its options.`
- `docs/guide.md` is unchanged in this task: Task 7 removes its sentence about messages and `--help` once the messages say `gh ledger` too.

**Docs test (`SprintFifteenDocsTests`).**

`DF3_HelpIsNoLongerDescribedAsNamingGhLedger`:

- `docs/cli.md` doesn't contain `` `--help` usage lines say ``;
- `docs/quickstart.md` doesn't contain `The help shows the program's own name`.

**Acceptance:**

- HL1–HL3 and DF3 pass: **11 tests**, so 2,041 passed.
- No test that existed before this task changes.
- `git diff HEAD~1 -- src` changes only `GhLedgerCli.cs` and adds `ExtensionHelpAction.cs`.

**Verification:** `dotnet build CopilotLedger.slnx`; `dotnet test CopilotLedger.slnx`.

## Task 7: Command Hints Name `gh ledger` Under the Extension

**Files:**

- `src/CopilotLedger.Cli/Breaker/BreakerCommands.cs`
- `src/CopilotLedger.Otlp/Breaker/CircuitFlagFile.cs`
- `src/CopilotLedger.Otlp/Breaker/CircuitBreakerMonitor.cs`
- `src/CopilotLedger.Mcp/LedgerMcpContext.cs`
- `src/CopilotLedger.Mcp/LedgerMcpTools.cs`
- `src/CopilotLedger.Cli/Mcp/McpCommand.cs`
- `src/CopilotLedger.Cli/Daemon/DaemonHost.cs`
- `src/CopilotLedger.Cli/Rates/RatesCommands.cs`
- `src/CopilotLedger.Cli/Install/StartupItem.cs`
- `docs/guide.md`
- new `tests/CopilotLedger.Tests/Cli/ExtensionHintTests.cs`
- `tests/CopilotLedger.Tests/Otlp/CircuitBreakerMonitorTests.cs`
- `tests/CopilotLedger.Tests/Docs/SprintFifteenDocsTests.cs`

**Contracts:**

```csharp
// CircuitFlagFile
public static string Render(
    string repositoryRoot,
    IReadOnlyList<BreakerTrip> openTrips,
    string command = BreakerMessages.DefaultCommand);   // the file's text; openTrips is non-empty; command starts each resetCommand

public static async Task SyncAsync(
    SqliteLedgerStore store,
    string repositoryRoot,
    string command = BreakerMessages.DefaultCommand,
    CancellationToken cancellationToken = default);     // Render(…, command) when it writes

// LedgerMcpContext
public string Command { get; init; } = "gh-ledger";     // how results name gh-ledger in reset commands; gh ledger mcp passes GhExtension.CommandName
```

**The eight hints.** In the `command` column, `CommandName` is `GhExtension.CommandName(context.Platform)`.

| # | Where (at `c6dc935`) | New text | `command` |
|---|---|---|---|
| 1 | `BreakerCommands.cs:282` | `Reset a trip with: <command> breaker reset --id <id>` | `CommandName` |
| 2 | `LedgerMcpTools.cs:953` | `resetCommand`: `<command> breaker reset --id <n>` | `_context.Command` |
| 3 | `LedgerMcpTools.cs:956` | `… The user can reset it with: <command> breaker reset --id <n>` | `_context.Command` |
| 4 | `CircuitFlagFile.cs:58–59` | `resetCommand`: `<command> breaker reset --id <n>` | the `command` parameter |
| 5 | `DaemonHost.cs:149` | `Port <port> is in use. Stop the other program, or run <command> daemon run --port <port>.` | `CommandName` |
| 6 | `RatesCommands.cs:106` | `Created <path>. Add your prices, then run: <command> rates validate` | `CommandName` |
| 7 | `StartupItem.cs:17` | `Warning: the gh-ledger path is too long for a sign-in command, so gh-ledger won't start when you sign in. Run <command> daemon start after you sign in.` | `CommandName` |
| 8 | `StartupItem.cs:83` | `Warning: the gh-ledger path has characters that a desktop autostart entry can't run, so gh-ledger won't start when you sign in. Run <command> daemon start after you sign in.` | `CommandName` |

**Callers that pass the command:**

- `BreakerCommands.SyncFlagFilesAsync`: `CircuitFlagFile.SyncAsync(store, root, GhExtension.CommandName(context.Platform), cancellationToken)`.
- `CircuitBreakerMonitor.RecordTripAsync`: `CircuitFlagFile.SyncAsync(_store, trip.RepositoryRoot, _command, CancellationToken.None)`. The daemon already passes `AlertDispatcher.Command`.
- `McpCommand`: add `Command = GhExtension.CommandName(context.Platform),` to the `LedgerMcpContext`.

Every other `gh-ledger` text in `src` stays (D9).

**Tests (`ExtensionHintTests`).** Definitions:

- `Platform(bool extension)`: `new FakeCliPlatform()` when false. When true, a `FakeCliPlatform` with `IsWindows = true`, `ProcessPath = @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe"`, and the manifest beside it in `ExistingFiles`.
- `Now` = 2026-11-24 12:00:00 UTC.
- `Trip()` = `NewBreakerTrip { Rule = CreditVelocity, TraceId = new string('1', 32), Repository = "swigerb/copilot-ledger", Branch = "feature/ledger", TrippedAt = Now.AddMinutes(-30), Detail = "180.00 AI credits in 5 minutes", WindowCostUsd = 1.80m, WindowMinutes = 5 }`.

The `(bool extension, string command)` theories have two rows: `(false, "gh-ledger")` and `(true, "gh ledger")`.

| ID | Name | Vector | Expected |
|---|---|---|---|
| XH1 | `XH1_BreakerStatus_NamesTheCommand` (theory) | Record `Trip()` through an initialized store; `breaker status` | Exit 0; `Out` ends with `"\nReset a trip with: " + command + " breaker reset --id <id>\n"` |
| XH2 | `XH2_TheFlagFile_NamesTheCommand` (theory) | `GitFixture.Create(temp.Path)`; record `Trip() with { RepositoryRoot = repo }`; `breaker config --flag-file on` | Exit 0; `<repo>/.copilot-ledger/CIRCUIT_OPEN`'s `openTrips[0].resetCommand` is `command + " breaker reset --id 1"` |
| XH3 | `XH3_McpWarnings_NameTheCommand` (theory, rows `(null, "gh-ledger")` and `("gh ledger", "gh ledger")`) | `GitFixture.Create(root)`; `AuditSeeds.CreateAsync(root)`; record `Trip() with { TraceId = AuditSeeds.R, ConversationId = "conv-r", TrippedAt = AuditSeeds.At("15:01:00") }`; an `McpTestHarness` with `AuditMcpTests`' context (Task 4's MD5), plus `Command = <the row's value>` when it isn't null; call `get_branch_ledger` with `{}` | The text's first line is exactly `WARNING: gh-ledger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: <command> breaker reset --id 1`. The structured `circuitBreaker.openTrips[0].resetCommand` is `<command> breaker reset --id 1`. |
| XH4 | `XH4_RatesInit_NamesTheCommand` (theory) | `rates init` | Exit 0; `Error` empty; `Out` is `"Created " + harness.Context.RatesOverlayPath + ". Add your prices, then run: " + command + " rates validate\n"` |
| XH5 | `XH5_UnregisterableSignInItems_NameTheExtensionsCommand` (theory, rows `windows` and `linux`) | `windows`: a `FakeCliPlatform` with `IsWindows = true`, `ProcessPath = @"C:\" + new string('a', 220) + @"\GitHub CLI\extensions\gh-ledger\gh-ledger.exe"`, the manifest beside it, and `gh` at `C:\gh\gh.exe`. `linux`: `IsWindows = false`, `HomeDirectory` a new `<temp>/home` holding `.zshrc` `export FOO=1`, `ShellPath = "/bin/zsh"`, `ProcessPath = "/home/me/my$data/gh/extensions/gh-ledger/gh-ledger"`, the manifest beside it, and `gh` at `/usr/bin/gh`. Run `install --dry-run`. | Exit 0. `Out` contains, followed by `\n`: for `windows`, `Warning: the gh-ledger path is too long for a sign-in command, so gh-ledger won't start when you sign in. Run gh ledger daemon start after you sign in.`; for `linux`, `Warning: the gh-ledger path has characters that a desktop autostart entry can't run, so gh-ledger won't start when you sign in. Run gh ledger daemon start after you sign in.` |
| XH6 | `XH6_APortInUse_NamesTheCommand` (theory) | A `CliHarness` with `Platform(extension)` and `new ManualTimeProvider(Now)`. Call `GhLedgerCli.InvokeAsync(["daemon", "run", "--port", "4319"], harness.Context with { Out = output, Error = error, ConfigureWebHost = builder => { builder.UseTestServer(); builder.ConfigureServices(services => services.AddHostedService(_ => new PortInUse())); } })`, with new `StringWriter`s. `PortInUse` is a private `IHostedService` whose `StartAsync` throws `new IOException("Address already in use")` and whose `StopAsync` completes (F8). | Returns 1. `output`, with `\r\n` as `\n`, is exactly `"2026-11-24T12:00:00.0000000+00:00 Port 4319 is in use. Stop the other program, or run " + command + " daemon run --port 4319.\n"`. `error` is empty. |

**Test in `CircuitBreakerMonitorTests`.**

`BM13_TheFlagFile_NamesTheDaemonsCommand`:

1. `Harness.CreateAsync(temp, BreakerSettings.Default with { FlagFile = true }, command: "gh ledger")`.
2. Ingest `TraceHRequest(1)` to `TraceHRequest(6)`, as BM7 does.

Expected: the flag file's `openTrips[0].resetCommand` is `gh ledger breaker reset --id 1`. BM7 still expects `gh-ledger` with the default command.

**Docs (`docs/guide.md`):**

- In the paragraph that starts `The samples come from one example`, delete the sentence `` Where a message or `--help` says `gh-ledger <command>`, run `gh ledger <command>`. `` (and the space before it).
- `breaker-status` sample: `Reset a trip with: gh-ledger breaker reset --id <id>` becomes `Reset a trip with: gh ledger breaker reset --id <id>`.
- `mcp` sample: in its first line, its `"resetCommand"`, and its `"warnings"` entry, `gh-ledger breaker reset --id 1` becomes `gh ledger breaker reset --id 1`. `WARNING: gh-ledger circuit breaker is open` stays: it names the product.

`GuideSamplesTests.GS2` (`breaker-status`) and `GS7` (`mcp`) check them, unchanged.

**Docs test (`SprintFifteenDocsTests`).**

`DF4_TheGuideShowsTheExtensionsCommandInHints`:

- `docs/guide.md` contains `Reset a trip with: gh ledger breaker reset --id <id>`;
- it contains neither `` Where a message or `--help` says `` nor `gh-ledger breaker reset`.

**Acceptance:**

- XH1–XH6, BM13, and DF4 pass: **14 tests**, so 2,055 passed.
- Every existing test passes unchanged, including:
  - `BreakerCommandTests` BC2 and BC5;
  - `InstallCommandTests` IN14;
  - `RatesCommandTests`;
  - `LedgerMcpServerTests`;
  - `CircuitBreakerMonitorTests` BM7;
  - `GuideSamplesTests`.

  The built binary's and the default context's texts are unchanged.
- `git grep -n -E "gh-ledger (breaker reset|daemon run --port|rates validate|daemon start after)" -- src` finds nothing.

**Verification:** `dotnet build CopilotLedger.slnx`; `dotnet test CopilotLedger.slnx`.

## Task 8: Version 0.7.0

Follow `docs/releasing.md` step 1 **exactly**, with `0.7.0`. Skip step 2: `rates.json` is unchanged, and so is its `ratesVersion`.

**Places to change (line numbers at `c6dc935`; Tasks 3–7 move some `docs/cli.md` lines):**

- `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`: `<Version>` (line 7).
- `src/CopilotLedger.Cli/GhLedgerCli.cs`: the comment (line 18) and the fallback after `??` (line 23).
- `.github/workflows/ci.yml`: the smoke test's comparison (line 46).
- Tests:
  - `DaemonCommandTests.DM1` (line 24);
  - `ReportCommandTests.StatusText` (line 648);
  - `ExportCommandTests.EC5` (line 118);
  - `ExportBundleTests.EB2` (line 56).
- `docs/guide.md`: the `status-first` and `status` samples (lines 55 and 173), and the receipt (lines 539 and 550).
- `docs/cli.md`: the manifest sample's `"toolVersion"`.
- `docs/releasing.md`: `git tag v0.7.0` and `git push origin v0.7.0` in step 4 (lines 24–25).

**Leave:**

- the versions that step 1 says to leave: `gh-ledger 0.4.1 and earlier` in the assets table, the prerelease tag `v0.5.0-rc.1`, and the other mentions to leave in `VersionPlacesTests.Places`;
- the comments in `GuideSamplesTests`, `ModelResolutionTests`, and `RatesUpdateTests`;
- the earlier daemon's `daemon.json` in `ImportCommandTests`;
- `docs/test-reports/` and `.github/plans/`.

Don't touch `VersionPlacesTests.cs` unless VR1–VR3 fail; if they do, stop and report it.

**Acceptance:**

- `git grep -n -E "(^|[^0-9.])0\.7\.0([^0-9]|$)" -- README.md ':(glob)docs/*.md' .github/workflows src tests` lists exactly the places above: 15 lines. The `:(glob)` pathspec keeps `docs/test-reports/` out, as VR3 does; a plain `docs/*.md` would match it too.
- `git grep -n -E "(^|[^0-9.])0\.6\.0([^0-9]|$)" -- README.md ':(glob)docs/*.md' .github/workflows src tests` finds nothing.
- `VersionPlacesTests`, `GuideSamplesTests`, and the four named tests pass. The count stays 2,055.
- `git diff c6dc935 -- src/CopilotLedger.Core/Pricing/rates.json global.json` is empty.

**Verification:** `dotnet build CopilotLedger.slnx`; `dotnet test CopilotLedger.slnx`.

## Task 9: 0.6.0 → 0.7.0 Upgrade Docs and the 0.6.0 Report's Update

**Files:**

- `docs/cli.md`
- `docs/guide.md`
- `docs/test-reports/2026-11-24-live-e2e-0.6.0-windows.md`
- `tests/CopilotLedger.Tests/Docs/UpgradeDocsTests.cs`

**Docs (`docs/cli.md`):**

- **Repairs.** Append to the paragraph under `### Repairs`: ` An import also reads once more each log that gh-ledger 0.6.0 or earlier imported, to learn the names of the MCP tools that ran in it; see [MCP tool names](#mcp-tool-names).` It names the versions, so it stays true after later releases; Task 3 couldn't name 0.6.0 before the bump (D10).
- **Ledger format.** In the `**Ledger format.**` paragraph:
  - replace `and an earlier gh-ledger can't open the ledger after that: it stops with` with `and gh-ledger 0.5.0 and earlier can't open the ledger after that: they stop with`;
  - replace `run \`gh ledger import --all\` once to repair earlier imports and charges;` with `run \`gh ledger import --all\` once to repair earlier imports and charges and to learn the names of MCP tools;`.

  The paragraph keeps `format 7`, never says `format 6`, and keeps everything else (UD2–UD4).
- **Upgrading from 0.6.0.** Insert this paragraph right after the `**Upgrading from 0.5.0.**` paragraph and before the first fenced block:

  `**Upgrading from 0.6.0.** gh-ledger 0.6.0 uses ledger format 7 too, so it can still open the ledger after you upgrade. Follow the steps below; the first \`gh ledger import --all\` reads every session log again to learn the names of MCP tools. To go back, stop the daemon, run \`gh extension remove ledger\` and \`gh extension install swigerb/gh-ledger --pin v0.6.0\`, and start the daemon; you don't need the copy.`

**Docs (`docs/guide.md`, "Upgrade or uninstall"):**

- Replace `Then repair what an earlier version recorded; see [Repairs](cli.md#repairs):` with `Then repair what an earlier version recorded, and learn the names of MCP tools; see [Repairs](cli.md#repairs):`.
- In the paragraph that starts `Keep the copy until you're sure you'll stay on the new release.`, insert after that first sentence: `To go back to gh-ledger 0.6.0, stop the daemon, run \`gh extension remove ledger\` and then \`gh extension install swigerb/gh-ledger --pin v0.6.0\`, and start the daemon; 0.6.0 opens the same ledger.` The rest of the paragraph, including its `--pin v0.5.0` sentence, is unchanged.

**Docs (`docs/test-reports/2026-11-24-live-e2e-0.6.0-windows.md`).** Under `## Gaps found`, after the gaps table and its following blank line, and before the paragraph that starts `**The test suite.**`, add one line and a blank line:

`**Update:** gh-ledger 0.7.0 fixes all three: [#90](https://github.com/swigerb/gh-ledger/issues/90), [#91](https://github.com/swigerb/gh-ledger/issues/91), and [#92](https://github.com/swigerb/gh-ledger/issues/92).`

Nothing else in the report changes. `docs/test-reports/` is outside VR3's and PN2's scans.

**Test (`UpgradeDocsTests`).**

`UD6_TheUpgradeFrom060_KeepsFormat7AndGoesBackWithThePin`:

1. In the cli.md steps (`**Upgrading or removing the extension.**` … `## The daemon`):
   - exactly one line starts with `**Upgrading from 0.6.0.**`, and its index is greater than the `**Upgrading from 0.5.0.**` line's and less than the first fenced line's;
   - it contains `format 7`, `gh ledger import --all`, `gh extension remove ledger`, `gh extension install swigerb/gh-ledger --pin v0.6.0`, and `you don't need the copy`;
   - it contains neither `format 6` nor `GhLedgerCli.Version`.
2. In the guide steps (`## Upgrade or uninstall` … `To remove gh-ledger, undo \`install\`:`):
   - a line containing `gh extension install swigerb/gh-ledger --pin v0.6.0` comes after the line `gh ledger import --all`;
   - it doesn't contain `GhLedgerCli.Version`.
3. In `docs/cli.md`, the first non-empty line after the line `### Repairs` contains `each log that gh-ledger 0.6.0 or earlier imported` and `[MCP tool names](#mcp-tool-names)`.

**Acceptance:**

- UD1–UD6, VR1–VR3, `ProductNameTests`, `GuideSamplesTests`, and `SprintFifteenDocsTests` pass: **1 test** added, so 2,056 passed.
- The cli.md steps still read stop → copy → upgrade → start → import (UD1).
- `git grep -n -E "(^|[^0-9.])0\.7\.0([^0-9]|$)" -- README.md ':(glob)docs/*.md' .github/workflows src tests` still lists exactly Task 8's 15 lines. The report's new line is under `docs/test-reports/`, which that pathspec and VR3 leave out.

**Verification:** `dotnet build CopilotLedger.slnx`; `dotnet test CopilotLedger.slnx`.

## Task 10: IP6 Reads the Ledger as IP1 Does

PA3's fix. IP6 runs two `LedgerSql.RowsAsync` queries after its `SqliteConnection.ClearAllPools()`, so a pooled connection still has the ledger open when it reads the files with `FileRetry.ReadAllBytesAsync`, which opens them with `FileShare.Read`. Windows refuses that while SQLite has a file open for writing, so IP6 fails after 10 seconds unless another test clears the pools in time. It always fails alone, and always with `xUnit.ParallelizeTestCollections=false`.

**Files:** `tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs` only. No product, docs, or other test change.

**Changes**, in `IP6_AnMcpToolCall_KeepsOnlyItsHashedNameAndToolName` only. Line numbers are at `1744ecd`; Tasks 4–9 don't edit this file.

1. IP6's last `LedgerSql` query is the statement on lines 258–260:

   ```csharp
   Assert.Equal(
       ["srv-tool"],
       await LedgerSql.RowsAsync(harness.Context.DatabasePath, "SELECT tool_name FROM span_links;"));
   ```

   On the line right after it, add the call that IP1 makes after its last query (line 63):

   ```csharp
   SqliteConnection.ClearAllPools();
   ```

   The `ClearAllPools()` right after the import (line 250) stays, and so does the blank line before `string output = run.Out + run.Error;`.

2. Replace line 282, `byte[] ledger = await FileRetry.ReadAllBytesAsync(path);`, with IP1's read (lines 70–73), inline. IP1 has no helper for it, so don't add one.

   ```csharp
   await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
   using MemoryStream copy = new();
   await stream.CopyToAsync(copy);
   byte[] ledger = copy.ToArray();
   ```

   The next line, `Assert.True(ledger.AsSpan().IndexOf(bytes) < 0, $"{canary} is in ledger.db{suffix}.");`, is unchanged.

Nothing else changes. Keep IP6's name, its log, its import run, and every assertion:

- exit code 0;
- exactly the one `mcp_tool_names` row;
- exactly `["srv-tool"]` from `span_links`;
- for each of the 7 canaries, that it isn't in the run's output, or in `ledger.db`, `ledger.db-wal`, or `ledger.db-shm` when that file exists.

Don't change IP1–IP5, IP_AM1, `FileRetry`, or `LedgerSql`. IP_AM1 keeps `FileRetry.ReadAllBytesAsync`: it calls `ClearAllPools()` after its runs and queries the ledger only after its reads, so nothing has the ledger open when it reads.

If IP6 still fails, stop and report it. Don't change its setup or assertions to make it pass. If a run fails only in tests other than IP6, stop and report those too: they aren't this task's to fix.

**Acceptance:**

- `dotnet test CopilotLedger.slnx --filter "FullyQualifiedName~ImportPrivacyTests.IP6"` reports 1 passed, 0 failed, 0 skipped, well under 10 seconds (about 0.2 s while planning). So does the same command with `-- xUnit.ParallelizeTestCollections=false`.
- `dotnet test CopilotLedger.slnx` and `dotnet test CopilotLedger.slnx -- xUnit.ParallelizeTestCollections=false` each report 0 failed, 0 skipped, and **2,056** passed. No test or theory row is added or removed, so the count stays 2,056.
- In `ImportPrivacyTests.cs`, `FileShare.ReadWrite | FileShare.Delete` is on exactly 2 lines (IP1's and IP6's), and `FileRetry.ReadAllBytesAsync` is on exactly 1 (IP_AM1's).
- `git diff HEAD~1 --stat` lists only `tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs` and this plan file. The plan file changes only to check off this task. The test file's diff adds 5 lines and deletes 1, all in IP6.

**Verification:**

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx --filter "FullyQualifiedName~ImportPrivacyTests.IP6"
dotnet test CopilotLedger.slnx --filter "FullyQualifiedName~ImportPrivacyTests.IP6" -- xUnit.ParallelizeTestCollections=false
dotnet test CopilotLedger.slnx
dotnet test CopilotLedger.slnx -- xUnit.ParallelizeTestCollections=false
git --no-pager grep -n -F "FileShare.ReadWrite | FileShare.Delete" -- tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs
git --no-pager grep -n -F "FileRetry.ReadAllBytesAsync" -- tests/CopilotLedger.Tests/Cli/ImportPrivacyTests.cs
git --no-pager diff --stat
```

The build reports 0 warnings and 0 errors. Before the commit, `git --no-pager diff --stat` (the working tree against Task 9's commit) lists the two files that `git diff HEAD~1 --stat` lists after it. Record `Tests: <n> passed` and the serial run's counts in the commit body.

## Task 11: Final Verification (no product change)

In a clean tree, at the commit after Task 10:

1. **Gates.**
   - `dotnet build CopilotLedger.slnx`: 0 warnings, 0 errors.
   - `dotnet test CopilotLedger.slnx`: 0 failed, 0 skipped, **2,056** passed.
   - `dotnet test CopilotLedger.slnx -- xUnit.ParallelizeTestCollections=false`: the same counts: 0 failed, 0 skipped, **2,056** passed (PA3).

   Record both.
2. **Publish.** Run `dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r win-x64 -o "$env:TEMP\gh-ledger-final"`; on another OS, use its RID. Then `& "$env:TEMP\gh-ledger-final\gh-ledger.exe" --version` must print exactly `0.7.0`.
3. **Smoke test.** Set `COPILOT_LEDGER_HOME` and `COPILOT_HOME` to new folders under a new temporary directory, and never use port 4318 or start the daemon. Run and check:
   1. `rates validate` exits 0.
   2. `--help`'s usage line is `  gh-ledger [command] [options]`.
   3. `breaker status` exits 0 and prints `Circuit breaker: closed`.
   4. `import --all --json` exits 0.
   5. Copy the binary to `<temp>\gh\extensions\gh-ledger\gh-ledger.exe`, and create an empty `<temp>\gh\extensions\gh-ledger\manifest.yml`. From that copy, `--help`'s usage line is `  gh ledger [command] [options]`, and `daemon stop --help`'s is `  gh ledger daemon stop [options]`.
   6. Optionally, from that copy, run `install --dry-run` and record its output and exit code without making them a gate: 0 in a regular terminal, and 1 with `Wouldn't start the gh-ledger daemon:` in a terminal whose job would stop the daemon. It only reads the environment.

   Delete the temporary directory afterward.
4. **Money.** `git diff c6dc935 -- src | Select-String -Pattern '\b(double|float)\b|REAL|Math\.Round'` finds no added line about money.
5. **Privacy.**
   - `git diff c6dc935 -- src` adds one table (`mcp_tool_names`, two columns) and no column to any other table.
   - `ToolStartPaths` gains only `data.mcpServerName` and `data.mcpToolName`.
   - IP1, IP3, and IP6 assert that no canary reaches the ledger or the output.
6. **Boundaries.**
   - `git diff c6dc935 --stat -- global.json src/CopilotLedger.Core/Pricing/rates.json` is empty.
   - `git diff c6dc935 -- src/CopilotLedger.Storage/LedgerMigrations.cs` adds only `Tables`.
   - `LedgerMigrations.Migrations` still ends at 7 (UD4).
   - No `.csproj` gains a `PackageReference`.
   - `DaemonState.CurrentImportRules` is still 1.
7. **Contracts.** The only public or internal contract changes are those in the global rules; compare `git diff c6dc935 -- src`.
8. **Docs.**
   - `git grep -n -E "129-character|but always says .Would start|usage lines say .gh-ledger.|program's own name|Where a message or .--help. says" -- README.md ':(glob)docs/*.md'` finds nothing. The dated reports under `docs/test-reports/` keep their text, so the pathspec leaves them out.
   - `docs/cli.md` describes MCP tool names, the reread, the dry run's daemon step, and the 0.6.0 upgrade.
   - `docs/configuration.md` is unchanged: no pricing configuration changed.
   - No release notes file was added.
9. Check off this task, and commit with the results in the body.

## Tests added this sprint

| Task | File | Tests (theory rows counted) |
|---|---|---|
| 1 | `Telemetry/McpToolNamesTests.cs` | MN1–MN6: 24 |
| 2 | `Storage/McpToolNameStoreTests.cs` | MT1–MT8: 11 |
| 3 | `Import/McpToolNameImportTests.cs` | ML1–ML10: 16 |
| 3 | `Cli/ImportPrivacyTests.cs` | IP6: 1 |
| 4 | `Cli/McpToolNameDisplayTests.cs` | MD1–MD5: 5 |
| 4 | `Audit/AuditToolNameTests.cs` | MD6: 1 |
| 4 | `Docs/SprintFifteenDocsTests.cs` | DF1: 1 |
| 5 | `Cli/InstallCommandTests.cs` | IN20–IN22: 10 |
| 5 | `Docs/SprintFifteenDocsTests.cs` | DF2: 1 |
| 6 | `Cli/ExtensionHelpTests.cs` | HL1–HL3: 10 |
| 6 | `Docs/SprintFifteenDocsTests.cs` | DF3: 1 |
| 7 | `Cli/ExtensionHintTests.cs` | XH1–XH6: 12 |
| 7 | `Otlp/CircuitBreakerMonitorTests.cs` | BM13: 1 |
| 7 | `Docs/SprintFifteenDocsTests.cs` | DF4: 1 |
| 9 | `Docs/UpgradeDocsTests.cs` | UD6: 1 |

Total: 96, from 1,960 to 2,056.

Changed tests, each as strong as before:

- `ImportRepairTests` RR1 and RR8 expect import revision 2 (Task 3).
- `ImportPrivacyTests` (Task 3): `LeftEmpty` and IP3 include `mcp_tool_names`, and the fixture's `toolu-3` is an MCP tool call.
- `CopilotSessionLogs.ToolStart` takes optional `mcpServerName` and `mcpToolName` (Task 3).
- The four version tests (Task 8).
- `ImportPrivacyTests.IP6` (Task 10, PA3): reads `ledger.db`, `-wal`, and `-shm` as IP1 does, after `ClearAllPools()`, so it passes alone and serially. Its assertions are unchanged.

The guide's `breaker-status` and `mcp` samples change, and `GuideSamplesTests` checks them unchanged (Task 7).

## After Task 11 (orchestrator)

Hand back to `ledger-architect` for review mode. Release notes for 0.7.0 aren't committed to the repository: the architect's planning report drafts them, and the coordinator publishes them with the release. Don't push, tag, open a PR, or create a release from a builder run.

## Deferred to later sprints

- **S15-1.** Learning MCP tool names from open sessions without `--include-active`, or from a source other than session logs, so a live call shows its name before its session ends (A3).
- **S15-2.** Naming the command per platform in shared artifacts that always say `gh ledger`: the audit recommendation for `velocity_near_breaker`, receipts, and export's report text (D9).
- **S15-3.** Replacing `ExtensionHelpAction` with System.CommandLine's own usage name for the root command (`RootCommand.HelpName` on the project's `main`; not in 2.0.15) once a release that has it is adopted.
- **S15-4.** Confirming that the GitHub Copilot app and VS Code's Agent Host hash MCP tool names as Copilot CLI does (A1).
- **S15-5.** Per-source import revisions, so a change that concerns only Copilot logs doesn't read Codex rollouts again (D6).
- **S15-6.** `install --dry-run` can't foresee a sign-in item registration that would fail (D7).
- **Carried from Sprint 14** (see `.github/plans/archive/sprint-14.md`): S14-1 to S14-5, unchanged; and from Sprint 12, S12-1 to S12-8 and S12-10 to S12-12, unchanged (S12-9 was done by Sprint 14).
- Everything Sprint 11 deferred, unchanged (see `.github/plans/archive/sprint-11.md`, "Deferred to later sprints"), except Sprint 5's "`gh ledger` in every message", which Tasks 6 and 7 finish for help and command hints. Product and executable names stay `gh-ledger`, and S15-2 tracks the shared artifacts.
