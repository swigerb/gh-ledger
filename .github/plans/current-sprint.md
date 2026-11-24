# Sprint 9: `gh ledger export`: Self-Contained Offline HTML Report and Metadata-Only Bundle

| | |
|---|---|
| Architect | `ledger-architect` |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET 10 (`net10.0`), SDK 10.0.x, zero Docker |
| Plan written | 2026-11-23, against merged Sprint 8 (`main` at `7620e55`) |
| Requirements | Issue [#64](https://github.com/swigerb/gh-ledger/issues/64), "`gh ledger export`: self-contained offline HTML report & metadata-only bundle" |
| Status | Current sprint. Sprint 8 is complete and archived at `.github/plans/archive/sprint-8.md`, with its amendments. |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification section, runs the verification commands, changes the line to `- [x]`, and stops. Tasks 1–23 are sequential, and each builds on the ones before it.

This sprint ships as one pull request. The orchestrator commits this plan first, together with `.github/plans/archive/sprint-8.md`, then each task as its own commit, `Sprint 9 Task N: <Title>`, on branch `swigerb-sprint-9-ledger-export-html-report`, and opens one pull request titled exactly "Sprint 9: `gh ledger export`: self-contained offline HTML report & metadata-only bundle", with `Closes #64`. After every task, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors (warnings are errors), and `dotnet test CopilotLedger.slnx` passes exactly the number of tests the task gives, with 0 failed and 0 skipped.

All of this plan's code, tests, and docs are **copy exactly**. The architect built every task in a scratch repository made from `git archive 7620e55`, outside this repository's worktree. Each task compiled with `TreatWarningsAsErrors` and passed the whole suite: 1288, 1296, 1309, 1331, 1338, 1348, 1356, 1359, 1368, 1374, 1377, and 1378 tests after Tasks 1–12. The architect then applied this plan's blocks with a script, task by task and in order, to a fresh `git archive 7620e55` with Windows line endings (as a `core.autocrlf=true` checkout has them), built and tested after each task, and got the same counts and, line endings aside, the same files. Copy each block character for character, including every comment line, `using` directive, blank line, and line of Markdown. Don't add argument checks (`ThrowIfNull` and the like), cancellation tokens, `ConfigureAwait`, logging, caching, or fast paths that the code doesn't show, and don't tidy code or reword docs.

Each task's specification lists its files, summarizes its contracts and algorithms for review, and then gives its code under "Code (copy exactly)". The summaries aren't code to write; the code section is. It has two kinds of blocks:

- **New file `<path>`:** write the block as the whole file, with `\n` line breaks, one `\n` at the end, and no byte order mark.
- **Edit k of m in `<path>`, replace: … with: …:** make a file's edits in the order given. Each "replace" block occurs exactly once in the file at the moment you make that edit, so search for it as an exact string. None of the files this sprint edits has a byte order mark; keep each file's line endings as they are. That includes `.github/workflows/ci.yml` (Task 12), which the repository itself stores with Windows line endings (CRLF): its blocks are shown with `\n`, like every block, so match and write them with the file's CRLF. Tasks 5 and 8 each add one item to `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`, which has no byte order mark; no task edits `CopilotLedger.Storage.csproj` or `CopilotLedger.Tests.csproj`, which start with one.

Tests are code too: write every test file and test edit exactly as given, in the order given. Don't drop an assertion because it looks redundant, and don't turn an exact comparison into `Contains` or `StartsWith`. Never normalize the product's output in a test (no `ReplaceLineEndings`, `Replace("\r\n", …)`, or `Trim` on an actual value); `CliHarness` already turns `\r\n` into `\n` in `Out` and `Error`, and the report's bytes are compared as written. The three `ReplaceLineEndings` calls in this sprint's tests apply to expected values only: the stylesheet and the script as embedded in the assembly, and `docs/cli.md`, which a `core.autocrlf=true` checkout holds with CRLF.

If a block doesn't apply, the build fails, or the test count differs from the task's, stop and report it. Don't change the plan's code to make it fit.

The final section, "Final step: the pull request", isn't a builder task. It has no checkbox, and `ledger-builder` never runs it.

## Tasks

- [x] Task 1: Time Zones and the Export's Time Rules (CopilotLedger.Cli & tests). `ICliPlatform.FindTimeZone` resolves IANA time zone IDs on every OS, on Windows through its own `icu.dll` because the CLI runs with invariant globalization, and Windows IDs on Windows. `ExportTime` turns local times into UTC across daylight-saving gaps and overlaps, lists the 18:00–06:00 night bands, and parses `--generated-at` (UTC only, to the millisecond).
- [x] Task 2: The Export's Read Snapshot and Conversation Filter (CopilotLedger.Storage & tests). `LedgerFilter.ConversationId` limits every query to one Copilot session. `OpenExportSnapshotAsync` opens one connection and one deferred read transaction, pinned by a first read, in which `LedgerExportSnapshot` runs the reports' own rollup, cache, prompt-chain, prompt-count, and turn queries, moved unchanged into shared helpers.
- [x] Task 3: Export Rows (CopilotLedger.Storage & tests). The snapshot pages effective requests and tool calls by (start, trace, span), lists the latest N for the timeline, and reads coverage (sources, superseded imports with and without a match, ledger-wide import gaps, tool history, cache counters), rate-card provenance, breaker trips, budgets, and repository roots, all through the export's filter. `ExportSeeds` is the export tests' shared ledger.
- [x] Task 4: `gh ledger export`: Command, Inputs, Header, Summary, and Coverage (CopilotLedger.Cli & tests). The command, its options and validation, the window evaluated at `--generated-at` in `--time-zone`, refusing existing files without `--force`, and a well-formed, LF-only page with exactly the issue's Content Security Policy, a header, summary cards, and coverage that states requests and tool calls separately.
- [x] Task 5: Tables and the Stylesheet (CopilotLedger.Cli & tests). Sortable tables of rollups, prompt chains, cache efficiency, breaker trips, and budgets, whose sort keys are integer ranks of the exact values; the breaker and budget cards; and the embedded stylesheet with light and dark schemes.
- [x] Task 6: The Timeline and the `ledger-data` Block (CopilotLedger.Cli & tests). A server-rendered SVG timeline: a row per Copilot session, labeled with its number and local start time, a bar per request colored by model, a tick per tool call, night bands, an axis, and an overview, capped by `--max-timeline-spans` with a note when it leaves requests out; and the `ledger-data` JSON block that the script reads.
- [x] Task 7: Cost Split, Cache, and Context-Growth Charts (CopilotLedger.Cli & tests). Billed, API-equivalent, and local-savings figures, each on its own scale and with a key, in colors outside the model palette; cache hit-ratio bars that never show n/a as 0%; and context-growth charts of the costliest prompt chains that have more than one request, with captions and sequence keys.
- [x] Task 8: The Script (CopilotLedger.Cli & tests). An embedded script that adds the timeline's zoom, pan, and range selection, the lens switch, and table sorting, through `textContent` and attributes only, with no network, storage, or code-evaluation API.
- [x] Task 9: The Bundle (CopilotLedger.Cli & tests). `--bundle` writes a deterministic ZIP of `manifest.json`, `report.html`, and four telemetry JSON files from the same snapshot, with SHA-256 hashes, replay inputs, and rate provenance; requests and tool calls stream in pages, so `spans.json` has every request whatever the timeline cap.
- [x] Task 10: Privacy, XSS, Paths, Determinism, and Culture (tests). Canary content through the real ingest path and the daemon's failure log, hostile branch and agent names, local paths, byte-identical repeats and replays, the `de-DE` culture, and a check that the repository gains no packages or tooling.
- [x] Task 11: The Reference and the Allowlist (docs & tests). `docs/cli.md` documents the command, the report, the bundle, and the allowlist of every JSON object export writes, which tests compare with the files written.
- [x] Task 12: The Walkthrough, README, and CI (docs, CI & tests). `docs/guide.md` gains "Attach a cost report to a pull request" with a checked sample, the README a feature line and a privacy sentence, and CI a smoke test of `export --bundle` with an IANA time zone on Linux and Windows.
- [x] Task 13: A Repository Path Shows Only Its Folder Name (CopilotLedger.Cli, docs & tests). Review findings R1 and R6: a local path shows as its last folder's name once trailing separators are removed, and a drive, share, or file-system root as `(root folder)`, so no part of a `--repo` path reaches a file or the console; `docs/cli.md` says how to reproduce an export of a repository without a remote.
- [x] Task 14: A Failed Write Names No Path and Leaves Both Files As They Were (CopilotLedger.Cli, docs & tests). Review findings R2 and R3: a failed write says why without the exception's message, which names an absolute path, and a ZIP that `--force` replaces waits beside it until the report is in place, so a failed export leaves both files as they were.
- [x] Task 15: Tool History Counts Every Missing Tool Call and Every Deleted One (CopilotLedger.Storage, CopilotLedger.Cli, docs & tests). Review findings A1 (R4) and R5: each imported request lacks the tool calls it reported beyond the links it owns, and tool history starts no earlier than the earliest tool call the ledger keeps, so an export with a past `--generated-at` doesn't count on tool calls that the daemon deleted.
- [x] Task 16: Pages Seek the Start-Time Indexes (CopilotLedger.Storage & tests). Review finding C2: a plain lower bound on the start time, and a first cursor at the period's start, let each page of `spans.json` and `tool-calls.json` seek its index instead of scanning and sorting the table, and tool calls find their prompt chains through `ix_spans_trace`; 200,000 requests page in 0.3 s instead of 21 s.
- [x] Task 17: The Timeline Draws Every Request, Labels Only Real Times, and Lets the Page Scroll (CopilotLedger.Cli, docs & tests). Review findings C1, R7 (C3), R8, and A2: a bar is at least 0.001 wide, a local time that a transition skips is no tick and one it repeats is two, `ledger-data`'s `minWidth` stops zooming where the axis still has two labels, and only Ctrl or Command with the wheel, or a pinch, zooms, so a plain scroll or a swipe scrolls the page.
- [x] Task 18: An Encoded Path Shows Only Its Folder Name (CopilotLedger.Cli & tests). Review finding N1: a key's percent-encoded bytes are decoded before it's judged a path and before its folder name is taken, so no `file:` URI or other encoding of a `--repo` path reaches a file or the console.
- [x] Task 19: The Report and the Bundle Are Moved Into Place by One Rule (CopilotLedger.Cli, docs & tests). Review findings N2, N3, D1, N6, and A4: without `--force` nothing is replaced, a read-only report or ZIP is refused even with it, the replaced ZIP is put back when the report can't be moved into place, and an export that leaves a backup or a mismatched ZIP behind exits with 1 and says so; export writes the report's temporary file, and moves and deletes both files, through `ICliPlatform`, so tests act at each step on any OS, including between the checks and the moves.
- [x] Task 20: Tool History Starts at the Earliest Link Kept, and Without Links There Is None (CopilotLedger.Storage, CopilotLedger.Cli, docs & tests). Review finding N4: tool history starts no earlier than the earliest span link of any kind that the ledger keeps, and a ledger that keeps none has no tool history, so a past `--generated-at` never counts on tool calls the daemon deleted.
- [x] Task 21: The Coverage Query Counts Each Request's Tool Links Once (CopilotLedger.Storage & tests). Review finding D2: a grouped join replaces a subquery that scanned all of `span_links` for each imported request, so the coverage query takes 0.13 s instead of 179 s for 30,000 imported requests.
- [ ] Task 22: A Range That Ends in a Repeated Hour Keeps Its Ticks (CopilotLedger.Cli & tests). Review finding N5: ticks are found over whole local days around the range, so a range that ends inside the hour that fall-back repeats keeps the ticks of the hour's first pass, and `minWidth` never exceeds the axis.
- [ ] Task 23: Imported Requests Without Tool Data Leave Tool History Incomplete (CopilotLedger.Storage, CopilotLedger.Cli, docs & tests). Review finding E1: the selected imported requests whose logs record no tool calls (every Codex request) are counted as `coverage.toolHistory.importedRequestsWithoutToolData`, and while there are any, tool history isn't complete; Copilot requests, compactions included, and live spans keep their coverage.

## Starting point

- Sprint 8 is complete. Its plan and Tasks 1–19 merged in PR #69, squash-merged on 2026-11-24 as `7620e55` from branch head `fbffe98`, which has the same tree. At `7620e55`, `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors and `dotnet test CopilotLedger.slnx` passes 1269 tests (0 failed, 0 skipped), with SDK 10.0.115. Sprint 8's plan is archived at `.github/plans/archive/sprint-8.md`, with its amendments; this plan's commit adds it. The latest release is still v0.4.1.
- Issue #64 is the requirements source. Its 16 acceptance criteria are implemented here, with the deviations listed under "Design decisions", as is #63's requirement that export leave out superseded rows and report coverage limitations (Sprint 8's deviation D26). The coordinating session settled these questions on 2026-11-23: the default scope is the current repository, with `--all-repos` (D1); coverage has three indicators and no overall "complete" (D9); label cuts never split a surrogate pair; and, after a browser review of sample reports, timeline rows are numbered and the charts have keys, headings, and captions (P8–P11).
- Merged behavior this plan depends on (read from the code at `7620e55`):
  - **Store.** `FilterSql` holds `@from` (inclusive), `@to` (exclusive), `@repository` (`COALESCE(repo_slug, repo_root, '(none)')`), `@branch` (`COALESCE(branch_name, '(none)')`), `@pull_request`, `@agent`, and `accounting_state = 'effective'`; `AddFilterParameters` binds them from `LedgerFilter`. `EffectiveLinkSql` keeps a link unless a superseded row owns it. `GetRollupAsync` and `GetTopPromptChainsAsync` read in one deferred transaction each; `GetCacheEfficiencyAsync`, `GetPromptCountAsync`, and `GetTurnBreakdownAsync` use a bare connection. `GetTurnBreakdownAsync` already binds `@conversation` (its selector), so the new filter parameter is `@conversation_id`. Microsoft.Data.Sqlite's `BeginTransaction()` is `BEGIN IMMEDIATE`, and `BeginTransaction(deferred: true)` takes no write lock.
  - **Imports and supersession.** The ledger is at schema version 6, whose migration Sprint 8's Task 14 edited in place to add `import_sessions.log_sha256` (`NOT NULL`, 64 lowercase hex characters); `ImportSessionCheckpoint.LogSha256` is required. A live span supersedes the imported row with its canonical usage ID (I1), and the imported rows of its session that it can't match (I2, I3), which `gh ledger import` reports as `Superseded, unmatched`. Either way `superseded_by` names a live row of the same session: the arriving one (`SupersedeCanonicalAsync`, `ReconcileConversationAsync`), or the session's first when the backfill reconciles it. `import_sessions` has no repository column.
  - **Daemon failure log.** `IngestFailureLog` keeps only a `SqliteException`'s message (Sprint 8 D21, narrowed by Task 16); any other exception is written as its type name alone.
  - **Repository keys can be local paths.** Rollups, `--repo`, `branch_budgets.repository`, and `breaker_trips.repository` use `COALESCE(repo_slug, repo_root, '(none)')`, and `breaker_trips.repo_root` holds a root. The ledger stores no working directory and no remote URL; `GitRemoteUrl` keeps only a strict `owner/repo` slug.
  - **Tool history.** The daemon prunes `span_links` older than 7 days (`LedgerImporter.ToolLinkRetention`). Links have a start time and no end time.
  - **Reports.** `branch` defaults to the current repository (`RepositorySlug ?? RepositoryRoot`), falls back to every repository outside a Git repository, and has `--all-repos`. `--since` defaults to `30d`. Report JSON uses `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`. `ReportCommands.Sum` and `StateName` and `ReceiptMarkdown.ChannelId` are private. `LedgerFormat` has `Credits`, `Usd`, `Count`, `ApiEquivalent`, `Percent`, `LocalTime`, `WriteMoney` (`0.############`), `WriteTime`, and the other JSON writers the reports share.
  - **Platform.** The CLI sets `InvariantGlobalization`. `ICliPlatform` has no time zone member; `SystemCliPlatform` and the tests' `FakeCliPlatform` implement it. `WindowsDetachedProcess` already uses `[DllImport]`. The CLI project has `EnableSingleFileAnalyzer`, no trimming, and no byte order mark; Core embeds `rates.json` with an explicit `LogicalName`.
  - **Tests.** xUnit 2.9.3 and no `InternalsVisibleTo`, so tests drive the CLI through `CliHarness` and call only public types. `GuideSamplesTests` runs every `docs/guide.md` sample in a `GuideWorld` state and requires exit code 0, empty standard error, and exact output; the test project copies only `docs/configuration.md` and `docs/guide.md` to its output, so tests find `docs/cli.md` from the repository root, as `ImportPrivacyTests` does.
  - **CI.** `.github/workflows/ci.yml` publishes the single-file binary for `linux-x64` and `win-x64` and smoke-tests it in bash: one OTLP span starting at 2026-11-21T00:00:00Z, `branch --all-repos --since all --json`, `status`, `breaker status`, `budget list`, and MCP `tools/list`.

## Verified facts (retrieved 2026-11-23)

### Time zones under invariant globalization (probed with .NET 10.0.15, SDK 10.0.115, Windows NT 10.0.26300)

- Under `InvariantGlobalization=true` on Windows, `TimeZoneInfo.TryFindSystemTimeZoneById("America/New_York")` and `("Etc/UTC")` fail and `TryConvertIanaIdToWindowsId` returns false; Windows IDs (`Eastern Standard Time`) and `UTC` resolve.
- Windows' own ICU (`C:\Windows\System32\icu.dll`, file version 72.1.0.4) exports `ucal_getWindowsTimeZoneID`. Through `[DllImport("icu.dll", EntryPoint = "ucal_getWindowsTimeZoneID", CharSet = CharSet.Unicode, ExactSpelling = true)]` with `[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]`, it maps `America/New_York`, `US/Eastern`, and `EST5EDT` to `Eastern Standard Time` (−05:00 in January, −04:00 in July), `Europe/Berlin` to `W. Europe Standard Time`, `Asia/Kolkata` to `India Standard Time`, and `Etc/UTC` to `UTC`; `america/new_york`, `Invalid/Zone`, and the empty string give an empty result with status 0. A published single-file, self-contained `win-x64` binary behaves identically, and so did this plan's build (`export --time-zone America/New_York` recorded `Eastern Standard Time`).
- ICU's contract ([`ucal.h`](https://github.com/unicode-org/icu/blob/main/icu4c/source/i18n/unicode/ucal.h)): `int32_t ucal_getWindowsTimeZoneID(const UChar* id, int32_t len, UChar* winid, int32_t winidCapacity, UErrorCode* status)`, stable since ICU 52; an unknown ID returns length 0 without an error status.
- [International Components for Unicode (ICU)](https://learn.microsoft.com/en-us/windows/win32/intl/international-components-for-unicode--icu-) (Microsoft Learn): Windows 10 1903 added the combined `icu.dll`, "present only starting with version 1903".
- [Globalization and ICU](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu) (Microsoft Learn): .NET's IANA-to-Windows conversion "isn't available when using NLS mode or globalization invariant mode". [dotnet/runtime's invariant-mode design](https://github.com/dotnet/runtime/blob/main/docs/design/features/globalization-invariant-mode.md): on Linux, ICU is used only for time zone display names.
- The test host isn't invariant, so in-process tests resolve IANA IDs through .NET's own ICU; only the published binary in CI exercises the `icu.dll` call. `new CultureInfo("de-DE")` throws under invariant globalization, so the culture test runs in the test host.

### Encoders and JSON

- `HtmlEncoder.Default.Encode("<script>alert(1)</script>")` is `&lt;script&gt;alert(1)&lt;/script&gt;`, and `"\"></svg><img src=x onerror=alert(1)>"` becomes `&quot;&gt;&lt;/svg&gt;&lt;img src=x onerror=alert(1)&gt;`. It also encodes `'`, `+`, every non-ASCII character (`&#x1F600;`), and control characters, using only `&lt;`, `&gt;`, `&amp;`, `&quot;`, and numeric references, so its output is well-formed XML text. A lone surrogate becomes U+FFFD.
- `Utf8JsonWriter` with the default encoder escapes `<`, `>`, `&`, `'`, `+`, and `` ` `` as `\u003C`, `\u003E`, `\u0026`, `\u0027`, `\u002B`, and `\u0060`, so `</script>` and `<!--` can't occur in its output. `JsonWriterOptions.NewLine` defaults to `"\r\n"` on Windows; with `"\n"`, indented output has no CR.

### ZIP

- Two `ZipArchive` writes (`CompressionLevel.Optimal`) of the same entries with `LastWriteTime` 1980-01-01T00:00:00+00:00 and the same write calls are byte-identical: DOS time 0x0000, date 0x0021, method 8, no extra field, no data descriptor on a seekable stream.
- Deflate's output depends on how data is written: the same 100,000 bytes in one call, in 4,096-byte chunks, and in 7-byte chunks compress to 2,225, 2,228, and 2,441 bytes. The bundle therefore feeds every entry to the archive in fixed 65,536-byte chunks.

### Daylight saving and parsing

- For a custom zone with US rules, `IsInvalidTime(2026-03-08 02:30)` is true and `ConvertTimeToUtc` throws for it; `IsAmbiguousTime(2026-11-01 01:30)` is true, `GetAmbiguousTimeOffsets` returns [−05:00, −04:00], and `ConvertTimeToUtc` picks the later instant. `ExportTime.ToUtc` defines its own rule instead.
- `DateTimeOffset.TryParseExact` with a literal `'Z'` parses `2026-11-21T21:30:00Z` as local time, and with `K` also accepts a missing zone and other offsets, so `--generated-at` checks the text's shape first.

### SQLite (Microsoft.Data.Sqlite 10.0.15, SQLite 3.53.3)

- In WAL mode, `BeginTransaction(deferred: true)` plus a first read pins a snapshot: another connection's insert commits meanwhile, and the snapshot still reads the old rows until it ends.
- Row-value keyset paging, `(start, trace, span) > (@s, @t, @p) ORDER BY start, trace, span LIMIT n`, returns every row once, in order.

### Web platform and GitHub

- [Content Security Policy](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/CSP) (MDN): a policy can be given in a `<meta http-equiv>` element; `'unsafe-inline'` allows inline scripts and styles.
- [SVG in HTML](https://developer.mozilla.org/en-US/docs/Web/SVG/Guides/SVG_in_HTML) (MDN): an inline `<svg>` and its children are in the SVG namespace without `xmlns`.
- [Attaching files](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files) (GitHub Docs): pull request comments accept `.html`, `.htm`, `.json`, and `.zip` attachments, up to 25 MB for files other than images and videos.
- [OpenTelemetry GenAI attribute registry](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/registry/attributes/gen-ai.md): `gen_ai.input.messages`, `gen_ai.output.messages`, `gen_ai.system_instructions`, `gen_ai.tool.call.arguments`, and `gen_ai.tool.call.result` are content attributes; the normalizer reads none of them.
- WCAG 2.2's relative-luminance formula gives the channel colors (Task 7) a contrast of 4.6:1 or more against the figure's panel in both color schemes.

### Assumptions (not verified)

| # | Assumption | If it's wrong |
|---|---|---|
| A1 | On Linux and macOS, the published binary resolves IANA IDs under invariant globalization from the system's tz database | The CI smoke step (`--time-zone America/New_York` on `ubuntu-latest`) fails at pull-request time |
| A2 | Windows releases without `icu.dll` (before Windows 10 1903 and Windows Server 2019) can't map IANA IDs | Users there pass a Windows ID; `docs/cli.md` says so |
| A3 | The ZIP container is byte-reproducible per OS and runtime, not across OSes; the entries' bytes are identical everywhere | Nothing: the acceptance criterion asks only for the same tool and runtime |
| A4 | Current Chromium, Firefox, and Safari run the script and render the SVG as specified; no test runs a browser | The page is complete without the script (static SVG and tables); the orchestrator checks the script in a browser before merging |
| A5 | `report.html` stays far below 25 MB at the default timeline cap: about 150 bytes per drawn request and 120 per tool call, about 6 MB at 20,000 of each | Users lower `--max-timeline-spans`; `docs/cli.md` gives the estimate |

## Design decisions

### Scope, window, and output (D1, D2, D11–D14)

`gh ledger export [--format html] [--out <path>] [--bundle] [--repo <key> | --all-repos] [--branch <name>] [--pr <n>] [--conversation <id>] [--since <period>] [--time-zone <IANA id>] [--include-local-paths] [--max-timeline-spans <n>] [--generated-at <UTC time>] [--force]`. The scope defaults to the current repository, as `branch` does, or every repository outside a Git repository; `--all-repos` exports every repository from inside one. `--since` (default `30d`) is evaluated at the generation time in the export's time zone, and the window is `[from, generatedAt)` in whole milliseconds, so a replay with `--generated-at` and `--time-zone` selects the same requests from an unchanged ledger on any machine. `--out` must end in `.html` or `.htm`; the bundle is `<stem>.zip` beside it. Existing targets are refused without `--force`; both files are written to temporary names and moved into place. Standard output names the files without folders, the request count, the scope, and the period, and says when the timeline leaves requests out; it never shows a path.

### Time zones (D3)

`ICliPlatform.FindTimeZone` tries `TimeZoneInfo.TryFindSystemTimeZoneById`, then on Windows maps an IANA ID through `icu.dll` (`SystemCliPlatform.WindowsTimeZoneId`), because the CLI's invariant globalization disables .NET's own mapping. The report and the manifest record the requested ID, the resolved `TimeZoneInfo.Id`, the night hours, and every night band in UTC. Night is 18:00 to 06:00 local; a start time that a spring-forward transition skips moves to the end of the gap, and a repeated end time takes its first occurrence.

### One snapshot and effective rows (D6, #63)

Export calls `InitializeAsync`, then reads everything in one `LedgerExportSnapshot`: one connection and one deferred transaction, pinned by a first read, so the cards, tables, charts, and every bundle file describe the same ledger state. The reports' own SQL runs in it (moved into internal static helpers that take a connection and transaction), so export's numbers equal `branch`, `agents`, `top-prompts`, and `status --cache`, and every query keeps only effective rows. Tool calls and breaker trips, which have no repository or branch columns, are selected through their prompt chains: a tool call counts when its trace has a selected request and it starts in the window. Budgets are those of the selected requests' repository and branch, with all-time spend, as `budget list` shows them.

### Coverage (D9)

The report, `manifest.json`, and `telemetry/summary.json` carry a `coverage` object with three separate indicators and no overall "complete": `requestsComplete`, `toolHistory.complete`, and `timeline.truncated`. Requests may be incomplete when the ledger has deferred imports or incompletely imported logs (`import_sessions` can't be filtered by repository, so these counts are ledger-wide and labeled so), or when the selection holds imported requests that live spans of their sessions replaced without matching them, since those spans may not cover them. Superseded imports are counted apart: `supersededMatchedImportSpans` (a selected live span has the same canonical usage ID, so it's the same call, counted once) and `supersededUnmatchedImportSpans` (selected by the imported row's own columns, as `gh ledger import` scopes its `Superseded, unmatched` count). Tool history is incomplete when a selected request is older than the 7 days of tool history, or an imported request's tool calls have no detail. The page states requests and tool calls separately (`Requests: complete.`, `Tool calls: partial; see the notes.`) and lists the notes.

### The page (D8, D16, D17, D19)

One UTF-8 file without a byte order mark or a carriage return, well-formed as XML apart from the contents of its `<style>` and two `<script>` elements, so tests parse it without a browser. The `<meta http-equiv="Content-Security-Policy">` holds exactly the issue's policy. Every ledger string goes through `HtmlEncoder.Default`; every number is invariant. Charts are server-rendered SVG with `role="img"`, a `<title>`, and a `<desc>`, each with a table, so the page is complete without JavaScript. The `ledger-data` block holds only the filter, the time zone with its night bands, the timeline's axis, and the coverage; per-request data is in the bundle. An audit-findings section is rendered only when #65 provides findings.

### The timeline (D5, D15)

A row per Copilot session (at most 200; the rest share the last row), labeled with its number and the local time of its first drawn request or tool call, such as `3 · 11-21 09:00`, because session IDs can share long prefixes; the label's `<title>` and the table hold the whole ID. A bar per request colored by model, a tick per tool call at its start (red when it failed; the ledger keeps no end time, so there's no duration), night bands, an axis whose labels follow the zoom, and an overview. `--max-timeline-spans` (0 to 100,000, default 20,000) caps the drawn requests and, separately, the drawn tool calls, keeping the latest; the totals and tables include everything.

### Cost, cache, and context growth (D7)

Three figures, never stacked or summed: billed USD (GitHub AI credits, then Foundry BYOK), API-equivalent USD, and local savings, each scaled to its own largest bar, each with a key, and each with a chart by model and a chart by agent under visible headings. The channel colors lie outside the timeline's model palette. A hatched mark flags requests without a price, and cache bars show `n/a`, never `0.0%`, for rows without cache counters. Context-growth charts show the input tokens of each request of the first 10 prompt chains, in cost order, that have more than one request, each with a caption (rank, local start, requests, peak input) and a key of its agent and model sequences.

### The script

One ES2020 `'use strict'` function in the page: timeline zoom (wheel, pinch, `+`/`-`), pan (drag, arrow keys), reset, and range selection on the overview; the lens switch; and table sorting by integer ranks. It writes only `textContent` and fixed attributes, creates only `span` and `button` elements, and uses no `innerHTML`, `eval`, network, storage, or navigation API.

### The bundle (D4, D18)

A ZIP of exactly six entries in ordinal order: `manifest.json`, `report.html` (the same bytes as the report), and `telemetry/prompt-chains.json`, `telemetry/spans.json`, `telemetry/summary.json`, and `telemetry/tool-calls.json`. Two passes over the same snapshot: the first hashes each entry (SHA-256 and size), and the second writes them with a fixed entry time and fixed 64 KiB deflate chunks and checks the hashes again. The manifest holds the hashes, the replay inputs (`generatedAtUtc`, the filter, the time zone with its bands, and the options), the rate versions and API rate-card hashes that priced the selection, the privacy flag, and the coverage. Money is a JSON number whose text is the exact decimal that `LedgerFormat.WriteMoney` writes, as in every gh-ledger JSON.

### Privacy and paths (D10)

Export reads only the ledger. By default no absolute path appears anywhere: repositories are slugs, `(none)`, or a local root's folder name. `--include-local-paths` shows full roots and adds `repositoryRoot` to span and breaker-trip records; standard output never shows a path. Prompts, responses, tool arguments and results, and file contents were never stored, and the allowlist in `docs/cli.md` lists every property export writes.

### Docs and CI

`docs/cli.md` gains "Export a report" with the bundle layout and the allowlist between `<!-- export-allowlist -->` markers that a test parses; `docs/guide.md` gains "Attach a cost report to a pull request", whose sample runs in `GuideState.Month`; the README gains a feature line and a privacy sentence. CI smoke-tests `export --bundle --time-zone America/New_York` on Linux and Windows. `docs/configuration.md` doesn't change; no package, version, MCP tool, or schema change.

### Deviations from issue #64

| # | Issue | This plan | Why |
|---|---|---|---|
| D1 | `[--repo <key>]`, "match the existing semantics" | The default scope is the current repository, or every repository outside one; **`--all-repos`** exports every repository from inside one, and with `--repo` it's an error | A report for a pull request is about one repository, as `branch` and `export-pr` assume |
| D2 | `--since` "matches the existing semantics" | Evaluated at `generatedAt`, not the wall clock, in the export's time zone; the window is `[from, generatedAt)` in whole milliseconds | A replay must select the same window from an unchanged ledger, on any machine |
| D3 | "Accept a supported IANA ID" | IANA IDs on every OS, through `icu.dll` on Windows 10 1903 and later; Windows IDs also work on Windows | The CLI's invariant globalization disables .NET's IANA mapping on Windows |
| D4 | Money "as decimal strings" | A JSON number whose text is the exact decimal of `LedgerFormat.WriteMoney`, as in every gh-ledger JSON | The criterion requires equality with `WriteMoney`'s output; quoted strings would differ from every other gh-ledger JSON |
| D5 | Timeline "rows are conversations or agents" | Rows are Copilot sessions, at most 200, numbered and labeled with their local start; the caps keep the latest N requests and, separately, the latest N tool calls | "Session timeline"; prefixes of session IDs can't tell rows apart; the caps bound the SVG |
| D6 | Filters "applied identically to all result sets" | Tool calls and breaker trips are selected through their prompt chains; budgets through the selected requests' keys, with all-time spend | Trace membership applies every filter, including `--pr` and `--conversation`, to tables without those columns |
| D7 | Top prompt chains | `prompt-chains.json` holds the top 100 by billed cost (the `top-prompts` query) with `total`; the page's table the same 100; the growth charts the first 10 of them with more than one request | Reuses `top-prompts` exactly; `spans.json` has every request, so any chain can be recomputed |
| D8 | — | `ledger-data` holds only the filter, the time zone, the timeline's axis, and the coverage | Keeps the page bounded and the script free of money values |
| D9 | "Report coverage limitations" (#63 D26) | `requestsComplete`, `toolHistory.complete`, and `timeline.truncated`, with no overall "complete"; superseded imports counted as matched or unmatched; import gaps are ledger-wide and labeled so; reports, receipts, and MCP still carry no notes | Tool history alone would call nearly every 30-day export incomplete and hide whether the requests are complete; an unmatched supersession can retire a call that no live span recorded |
| D10 | Paths | No absolute path by default; `--include-local-paths` shows roots in the files, never on standard output | The ledger stores only repository roots as local paths |
| D11 | `--out` default `./copilot-ledger-report.html` | `--out` ends in `.html` or `.htm`; the bundle is `<stem>.zip`; the folder must exist | The report and the ZIP can't be the same file |
| D12 | Success output | File names without folders, the count, the scope, and the period, and a line when the timeline is truncated | Path-free, deterministic lines that the guide checks exactly |
| D13 | `--max-timeline-spans` | 0 to 100,000; default 20,000 | 0 makes an aggregate-only report; 100,000 bounds the page |
| D14 | `--generated-at <UTC timestamp>` | `yyyy-MM-ddTHH:mm:ss`, an optional fraction of 1–7 digits, then `Z` or `+00:00`, truncated to milliseconds | The manifest's `generatedAtUtc` can be pasted back; only UTC is accepted, as the issue says |
| D15 | Tool calls | Ticks at their start; no end time or duration anywhere | `span_links` keeps no end time; the issue forbids invented durations |
| D16 | Keyboard access | Bars and growth lines are focusable; the timeline is one focusable control with arrow keys, `+`/`-`, and `0` | Thousands of tab stops would make the page unusable from the keyboard |
| D17 | "Audit findings: an optional section" | A model list (always empty now) and a section rendered only when it isn't empty; no bundle entry | #65 owns audit; the six-entry allowlist stays fixed until it extends it |
| D18 | `ExportSpanRecord` fields | The issue's fields plus `model`, `apiEquivalentStatus`, `apiRatesVersion`, `apiRateCardSha256`, and `repository`; tool calls add `conversationId`, `agent`, `failed`, and `ingestSource` | A consumer can reproduce every rollup and verify pricing provenance from `spans.json`; none is content |
| D19 | CSP | Exactly the issue's policy; a hash-based policy is deferred | The issue fixes the policy; encoding is the XSS defense |

### Changes from the design, made while building

| # | Change | Task | Why |
|---|---|---|---|
| P1 | `LedgerFormat.Exact` arrives with the cards | 4 | The cards' titles need it first |
| P2 | The model keeps the storage rows `PromptChainRow`, `BreakerTrip`, and `BranchBudgetRow`, with shown repository keys, instead of mirror records | 5 | The mirrors only copied fields |
| P3 | Bar charts are 860 units wide | 7 | A full bar, a mark, and a value such as `$0.82 + 1 unpriced` need the room |
| P4 | The bundle's files use `UnsafeRelaxedJsonEscaping`, as the reports do; `ledger-data` keeps the default encoder | 9 | `generatedAtUtc` must read `+00:00` to be pasted into `--generated-at`; only the block inside `<script>` needs `<` escaped |
| P5 | The CI step passes `--generated-at 2030-01-01T00:00:00Z` | 12 | The smoke span starts at 2026-11-21T00:00:00Z, which a runner's clock may not have reached |
| P6 | A drag on the overview doesn't also pan the timeline | 8 | Found in the first sample review |
| P7 | `LedgerFormat.WritePromptChain` and `WriteCacheFields` are extracted from `ReportCommands`, which calls them | 9 | The bundle writes exactly the reports' JSON fields |
| P8 | Timeline rows are numbered and labeled with their local start (`3 · 11-21 09:00`); the timeline's table gains a `#` column | 6 | The browser review: every row of a sample read `7d4e1c2a…` |
| P9 | Channel colors outside the model palette, and a key in every cost and cache figure | 7 | The browser review: orange meant a model in one figure and Foundry BYOK in the next, and the billed chart had no legend |
| P10 | `By model` and `By agent` headings above each chart | 7 | The browser review: nothing visible separated the two charts |
| P11 | Growth charts only for chains of two or more requests, with captions and sequence keys, or a sentence when there's none | 7 | The browser review: the charts weren't identifiable, and one-request chains drew a lone dot |
| P12 | Allowlist rows list several files when the object is the same in each | 11 | Every (file, object) pair is still documented exactly once |
| P13 | Superseded imports are counted as matched or unmatched; unmatched ones make `requestsComplete` false and add the warning `import-superseded-unmatched`; `FilterSql` is split into `SpanFilterSql` and the effective-row predicate | 3, 4, 6, 11 | Merged Sprint 8 (Task 15) supersedes imported rows that live spans can't match; "live spans recorded the same calls" isn't true of those |
| P14 | EP1 puts its canary in `daemon.log` through a `SqliteException` and checks the file before exporting | 10 | `IngestFailureLog` now keeps only a `SqliteException`'s message (Sprint 8 Task 16), so an `IOException` canary never reached the log |
| P15 | Test checkpoints set `LogSha256` | 3, 4 | Sprint 8 Task 14 added `import_sessions.log_sha256` |

### Acceptance criteria traceability

| #64 criterion | Tasks | Tests or deviation |
|---|---|---|
| One `.html` with no external references and the CSP `<meta>`, enforced by a scan | 4, 8 | EC9, EA1, EA2 |
| Privacy canaries absent from `report.html`, every ZIP entry, standard output, and standard error | 10 | EP1 |
| Allowlist tests for `ExportSpanRecord` JSON and `manifest.json` | 11 | EL1, EL2, EL3 |
| XSS: branch and agent strings only HTML-encoded; no literal `</script>` in the embedded JSON | 10 | EP2, TL5 |
| `repo_root` and other absolute paths only with `--include-local-paths` | 4, 5, 10, 13, 14, 18 | EC10, EH7, EP3, EP7, EC3; D10 |
| Byte-identical HTML and ZIP for the same database, arguments, and time | 9, 10 | EP4; D14 |
| Replay inputs and rate provenance in the manifest; `--generated-at` replays; a cap below the dataset still exports every request, whose exact sums reproduce the summary from the same snapshot | 2, 9, 10, 16 | XS2, EB3, EB5, EP4, XR13; D2, D14 |
| Bundle integrity: exactly the allowlisted entries; `sha256` and `bytes` match; money equals `WriteMoney` output | 9 | EB1, EB2, EB3; D4 |
| `Unpriced` and `n/a`, never `$0.00` or `0%` | 4, 7 | EC7, CH3, CH4 |
| API-equivalent and local savings never added to billed spend; tool ticks at known starts without invented durations | 6, 7, 9 | CH1, CH2, TL6, EB4; D15 |
| Timeline: daylight-saving bands for a fixed `--time-zone`; truncation noted; aggregates complete | 1, 6, 17, 22 | ET3–ET7, TL4, TL12, TL14, TL2, TL3, TL11 |
| `--out` refused without `--force`; clear `--time-zone` and `--since` errors | 4, 9, 14, 19 | EC1, EC2, EC3, EC12, EC14, EC15, EB6 |
| Meaningful without JavaScript (structural assertions, no browser) | 4–8 | CH6, EA3, EC9 |
| Invariant formatting under `de-DE` | 10 | EP5 |
| No new NuGet packages, no browser, Node, or Python tooling, no CDN | 8, 10 | EP6, EA1 |
| `docs/cli.md` and `docs/guide.md` updated; `GuideSamplesTests` passes | 11, 12 | EL1–EL3, GS1, GS2 |
| Build and tests green with warnings as errors; hermetic | all | Every task |
| #63: every export query leaves out superseded rows | 2, 3 | XR1, XR3, XR4 |
| #63: export reports coverage limitations, with retained tool coverage marked separately and a truncated timeline never called complete | 3, 4, 6, 9, 15, 20, 21, 23 | XR4, XR10, XR11, XR14, EC8, EC11, EC13, EC18, EC19, EC20, TL2, TL10, EB5; D9 |

### Review fixes (Tasks 13–17)

Three reviews of Tasks 1–12, as committed on `swigerb-sprint-9-ledger-export-html-report` at `ae05799`, found the problems below: the architect's (A1, A2), an independent reviewer's (R1–R8), and the coordinator's (C1–C3, where C3 is R7). Tasks 13–17 fix them on top of Task 12. Each fix has tests that fail without it, except Task 16's, which changes only how fast pages are read (XR13 guards its results). Where these tasks and the design above differ, the tasks win. They were built like Tasks 1–12: in the scratch repository, whose Task 12 tree is the same as `ae05799`'s apart from the plans, with each task's blocks generated from its commit there, then applied by script, in order, to `git archive ae05799` with Windows line endings, with a build and the whole suite after each task: 1388, 1389, 1392, 1393, and 1398 tests, and, line endings aside, the same files.

| Finding | Problem | Fix | Task |
|---|---|---|---|
| R1 | `FolderName` returned a path whole when it ended with `/` or `\`, so `--repo C:/Users/alice/project/` (as shells complete folder names) put an absolute path in standard output, the page, and `manifest.filter.repo`, even when it selected nothing; so did a root such as `C:\` or `/` | A local path shows as its last segment once trailing separators are removed, a drive, share, or file-system root as `(root folder)`, and never as text with a separator; `file:` URIs count as local paths | 13 |
| R6 | A repository without a remote is recorded by its folder name, which `--repo` can't select in a replay; two folders with the same name look the same | Documented in `docs/cli.md`; telling same-named folders apart is deferred | 13 |
| R2 | A failed write printed the exception's message, which names the temporary file's absolute path: `--out missing/report.html` showed the working directory and the process ID | The reason, without a path: `its folder doesn't exist.`, `access is denied.`, or `the file system reported error 0x<HRESULT>.` | 14 |
| R3 | The ZIP was moved into place before the report and never put back, so a report that couldn't be replaced left the new ZIP beside the old report, and the old ZIP was lost | A ZIP that `--force` replaces waits beside it until the report is in place, and is put back if the report can't be; with no old ZIP, a new one is deleted | 14 |
| A1, R4 | `importedToolCallsWithoutDetail` counted an imported request's tool calls only when it owned no link, but import links only the tool calls that started within the 7 days and completed, so a request with some links counted as complete | Each imported request lacks its `imported_tool_call_count` less the `execute_tool` links it owns, at least 0 | 15 |
| R5 | Tool history was the 7 days before `generatedAt`, but the daemon deletes tool calls by its own clock, so an export with a past `--generated-at` counted on tool calls that were already deleted | Tool history starts no earlier than the earliest tool call the ledger keeps, read from the snapshot, so an unchanged ledger still replays byte for byte | 15 |
| C2 | `(@after_start IS NULL OR …)` kept SQLite from using `ix_spans_start_time`, so each 1,000-row page scanned and sorted the table, and each tool-call page built an `IN` list of every selected trace again: one pass over 200,000 requests took 21 s, and over their tool calls 56 s; a bundle makes two of each | A plain lower bound on the start time and a first cursor at the period's start; a correlated `EXISTS` through `ix_spans_trace`: 0.3 s and 0.4 s | 16 |
| C1 | A request without a duration (every Codex import, and Copilot imports without one) or shorter than about 1.2 s on a 30-day axis was a bar `width="0"`, which SVG never draws | Bars are at least 0.001 wide, and their 1-pixel stroke shows them | 17 |
| R7, C3 | A local time that spring-forward skips mapped to the gap's end and kept its label, and the real tick there was dropped: `02:00` where it was 03:00 | A skipped time is no tick, except midnight, whose tick is the day's first instant; a repeated time is a tick each time it occurs | 17 |
| R8 | Ticks were as fine as the whole range needed (6 hours over 30 days), but the script zoomed down to 0.5 units (22 minutes there), where a view can hold no tick and no label | Ticks one step finer than the axis's lines, and `ledger-data`'s `timeline.minWidth`, twice the widest gap between ticks, below which the script doesn't zoom | 17 |
| A2 | The wheel always zoomed and `#timeline-plot` had `touch-action: none`, so a timeline of many rows, up to 3,200 units tall, trapped the page's scrolling under the pointer and under a finger | Only Ctrl or Command with the wheel, or a pinch, zooms; a plain scroll and a vertical swipe scroll the page | 17 |

Measured on 2026-11-23 for Task 16, on SQLite 3.53.3 (Microsoft.Data.Sqlite 10.0.15), in a ledger of 200,000 requests and 200,000 tool calls, with and without `ANALYZE`:

| Page query | `EXPLAIN QUERY PLAN` | One pass, 1,000 rows a page |
|---|---|---|
| Requests, before | `SCAN spans`; `USE TEMP B-TREE FOR ORDER BY` | 21.2 s |
| Requests, after | `SEARCH spans USING INDEX ix_spans_start_time (start_time_unix_ms>?)`; `USE TEMP B-TREE FOR LAST 2 TERMS OF ORDER BY` | 0.3 s |
| Tool calls, before | `SEARCH span_links USING PRIMARY KEY (trace_id=?)`; `LIST SUBQUERY` with `SCAN spans`; `USE TEMP B-TREE FOR ORDER BY` | 55.8 s |
| Tool calls, after | `SEARCH span_links USING INDEX ix_span_links_start (start_time_unix_ms>?)`; `SEARCH chain EXISTS USING INDEX ix_spans_trace (trace_id=?)` | 0.4 s |

Through the public snapshot after Task 17, the same ledger's requests page in 0.97 s and its tool calls in 0.45 s, and `export --all-repos --since all --bundle --max-timeline-spans 0` takes 9.2 s. Assumption A5, measured after Task 17: 25,000 requests and 25,000 tool calls over 25 days make a 6,067,672-byte report and a 1,716,559-byte ZIP at the default cap.

### Review fixes, rounds 2 and 3 (Tasks 18–23)

A second round of reviews, of Tasks 13–17 as committed at `ecc91f6`, found the problems below: the independent reviewer's N1–N5, and the coordinator's D1 and D2 (D2 is also the architect's own finding from the same round). A third round, of Tasks 18–22 as built in the scratch repository, found that EC15 didn't guard N2's fix (the independent reviewer's N6). With it, the architect found that no test made a report appear during an export (A4), and the coordinator added E1, which Sprint 10's session found. Tasks 18–23 fix them on top of Task 17; N6 and A4 are fixed in Task 19, where EC15 is written. Each fix has tests that fail without it, except Task 21's, which changes only how fast the coverage query runs (XR14 pins counts that the old query gave too). Where these tasks and the earlier ones differ, these win. They were built like Tasks 13–17, in the scratch repository, whose Task 17 tree is the same as `ecc91f6`'s apart from the plans, and applied by script, in order, to `git archive ecc91f6` with Windows line endings, with a build and the whole suite after each task: 1403, 1409, 1410, 1411, 1412, and 1414 tests, and, line endings aside, the same files.

| Finding | Problem | Fix | Task |
|---|---|---|---|
| N1 | A `file:` URI with percent-encoded separators (`file:///C:%5CUsers%5C…%5Crepo`) kept them in its last segment, so the encoded path reached standard output and the files | A key's percent-encoded bytes are decoded, however deep the encoding, before it's judged a path and before its folder name is taken | 18 |
| N2 | Task 14 moved aside any ZIP that existed at the move, so a ZIP that another program wrote during an export without `--force` was replaced, and its backup deleted | Without `--force`, no move replaces a file, and a ZIP or report that appeared since the check is refused as already existing | 19 |
| N3 | When the report couldn't be moved into place and the new ZIP couldn't be deleted, the failure was swallowed | Standard error says `<zip> was written, but <report> wasn't, so they don't match.` | 19 |
| D1 | Windows renames a read-only file but won't delete it, so `--force` moved a read-only ZIP aside, replaced it, exited 0, and left the old ZIP behind as a read-only `.bak` | A read-only report or ZIP is refused before anything is written, even with `--force` (`access is denied.`); a replaced ZIP that can't be deleted ends the export with exit code 1, and the message says where it's kept | 19 |
| N4 | With every link deleted, tool history fell back to the 7 days before `generatedAt`, so a past `--generated-at` could still call it complete | Tool history starts no earlier than the earliest link of any kind (the daemon deletes agent invocations' links with tool calls'), and a ledger that keeps no link has no tool history | 20 |
| D2 | The coverage query counted each imported request's links with a subquery on `owner_span_row_id`, which has no index, so it scanned all of `span_links` for every such request: 33 s for 10,000 imported requests and 30,000 links, and 179 s for 30,000 and 50,000 | A grouped join counts each owner's links once: 65 ms and 133 ms for the whole coverage query | 21 |
| N5 | A range that ended inside the hour that fall-back repeats lost the ticks of the hour's first pass, because the loop stopped at the end's local time, and `minWidth` could exceed the axis | The tick loop walks whole local days around the range and keeps the instants inside it; `minWidth` is at most the axis, in C# and in the script | 22 |
| N6 | EC15 wrote the other program's ZIP just before the export moved its own ZIP into place, after the decision to move a ZIP aside, so it passed even when that decision ignored `--force`; no platform call came between the checks and that decision | Export writes its report's temporary file through `ICliPlatform.WriteFileAsync`, after the checks and before that decision, and EC15 writes the other program's file there | 19 |
| A4 | No test made a report appear during an export without `--force`, so a report move that overwrote would have passed | EC15's second case writes the report there | 19 |
| E1 | A Codex rollout records no tool calls, so a Codex request owns no link and has no count, and an export of recent Codex imports said "Tool calls: complete for this window." | `coverage.toolHistory.importedRequestsWithoutToolData` counts the selected imported requests, other than Copilot's, without a tool-call count; above 0, tool history isn't complete, with the warning `imported-requests-without-tool-data` and a note | 23 |

E1's figure leaves Copilot rows out. Copilot logs count each usage record's tool calls, and Copilot's importer records a compaction, which makes no tool calls, without a count; nothing stored tells the two apart, so a Copilot row without a count is taken for a compaction. Counting every imported row without a count would have called every export with a compaction incomplete.

After Task 19, one rule set moves the two files into place:

- `CanWrite` refuses a directory, an existing file without `--force`, and a read-only file even with it.
- Without `--force`, no move overwrites.
- With `--force`, an existing ZIP waits as `<zip>.<process ID>.bak` until the report is in place, and is put back if the report can't be.
- An export that leaves a backup behind, or can't take a new ZIP back, exits with 1 and says so on standard error, without a path.
- Export writes its report's temporary file through `ICliPlatform.WriteFileAsync`, and moves and deletes both files through `MoveFile` and `DeleteFile`, so a test can make another program's file appear, or a move or deletion fail, at an exact step on any OS, including between the checks and the moves.

Not fixed (Low): a share path without a share (`//server`) shows the server's name; and two items deferred below.

## Task 1 specification: Time Zones and the Export's Time Rules

An export's dates, times, and night shading are in one time zone, which a replay must resolve the same way on any machine. This task adds the platform lookup and the time rules; nothing calls them yet.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/ICliPlatform.cs` | New `FindTimeZone(string id)` |
| `src/CopilotLedger.Cli/SystemCliPlatform.cs` | `FindTimeZone`; public static `WindowsTimeZoneId` and its `icu.dll` import |
| `src/CopilotLedger.Cli/Export/ExportTime.cs` | New: `UtcBand` and `ExportTime` (`NightStart`, `NightEnd`, `ToUtc`, `NightBands`, `TruncateToMilliseconds`, `TryParseGeneratedAt`), public so tests can call them |
| `tests/CopilotLedger.Tests/TestSupport/FakeCliPlatform.cs` | `TimeZones`, which `FindTimeZone` reads |
| `tests/CopilotLedger.Tests/TestSupport/TestZones.cs` | New: `Test/New_York` (US rules) and `Test/Gap` (18:00–18:59 skipped on 2026-03-08, 06:00–06:59 repeated on 2026-11-01) |
| `tests/CopilotLedger.Tests/Cli/ExportTimeTests.cs` | New: ET1–ET8 |

### Contracts and algorithms

- `FindTimeZone(id)`: null unless `id` is 1 to 128 characters without leading or trailing white space; then `TimeZoneInfo.TryFindSystemTimeZoneById(id)`; then, on Windows, `WindowsTimeZoneId(id)` and the zone with that ID; otherwise null. `InvalidTimeZoneException` gives null. `WindowsTimeZoneId` calls `ucal_getWindowsTimeZoneID` with a 128-character buffer and returns null for an error status, a length of 0 or above 128, or a missing `icu.dll` or entry point.
- `ToUtc(local, zone)`: a time that a spring-forward transition skips moves a minute at a time to the first valid one, the end of the gap; a repeated time takes the larger offset, its first occurrence; the result is truncated to milliseconds.
- `NightBands(zone, fromUtc, toUtc)`: for each local date from the day before `fromUtc`'s to `toUtc`'s, the band from that date's 18:00 to the next date's 06:00, both through `ToUtc`, kept when it overlaps `[fromUtc, toUtc)`; unclipped and in order.
- `TryParseGeneratedAt(value)`: `yyyy-MM-ddTHH:mm:ss`, an optional fraction of 1 to 7 digits, then `Z` or `+00:00`, and nothing else; the instant, truncated to milliseconds.

### Code (copy exactly)

New file `src/CopilotLedger.Cli/Export/ExportTime.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace CopilotLedger.Cli.Export;

// One night of the timeline's shading, from a local NightStart to the next day's NightEnd, in UTC.
public sealed record UtcBand(DateTimeOffset FromUtc, DateTimeOffset ToUtc);

// The time rules of gh ledger export.
public static partial class ExportTime
{
    public static readonly TimeOnly NightStart = new(18, 0);
    public static readonly TimeOnly NightEnd = new(6, 0);

    private static readonly string[] GeneratedAtFormats = ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"];

    // A local date and time in zone as a UTC instant, to the millisecond. A time that a spring-forward transition skips moves to
    // the end of the gap (the transition instant); a time that a fall-back transition repeats takes its first, earlier occurrence.
    public static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        DateTime value = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        for (int step = 0; zone.IsInvalidTime(value); step++)
        {
            if (step == 1440)
            {
                throw new InvalidOperationException($"The time zone {zone.Id} skips more than a day.");
            }

            value = value.AddMinutes(1);
        }

        TimeSpan offset = zone.IsAmbiguousTime(value) ? zone.GetAmbiguousTimeOffsets(value).Max() : zone.GetUtcOffset(value);
        return TruncateToMilliseconds(new DateTimeOffset(value.Ticks - offset.Ticks, TimeSpan.Zero));
    }

    // The nights, from local NightStart to the next day's NightEnd in zone, that overlap [fromUtc, toUtc): unclipped, in order.
    public static IReadOnlyList<UtcBand> NightBands(TimeZoneInfo zone, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        List<UtcBand> bands = [];
        DateOnly last = LocalDate(toUtc, zone);
        for (DateOnly date = LocalDate(fromUtc, zone).AddDays(-1); date <= last; date = date.AddDays(1))
        {
            DateTimeOffset start = ToUtc(date.ToDateTime(NightStart), zone);
            DateTimeOffset end = ToUtc(date.AddDays(1).ToDateTime(NightEnd), zone);
            if (end > fromUtc && start < toUtc)
            {
                bands.Add(new UtcBand(start, end));
            }
        }

        return bands;
    }

    // value in UTC, truncated to whole milliseconds.
    public static DateTimeOffset TruncateToMilliseconds(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

    // --generated-at: yyyy-MM-ddTHH:mm:ss, an optional fraction of 1 to 7 digits, then Z or +00:00. The instant, truncated to
    // milliseconds; false for anything else. The shape is checked first because the K specifier also accepts a missing zone.
    public static bool TryParseGeneratedAt(string? value, out DateTimeOffset instant)
    {
        if (value is not null
            && GeneratedAtShape().IsMatch(value)
            && DateTimeOffset.TryParseExact(
                value,
                GeneratedAtFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTimeOffset parsed)
            && parsed.Offset == TimeSpan.Zero)
        {
            instant = TruncateToMilliseconds(parsed);
            return true;
        }

        instant = default;
        return false;
    }

    // The local date of an instant in zone.
    internal static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    [GeneratedRegex(@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?(Z|\+00:00)\z")]
    private static partial Regex GeneratedAtShape();
}
```

Edit 1 of 1 in `src/CopilotLedger.Cli/ICliPlatform.cs`, replace:

```csharp
    bool FileExists(string path);
    string? FindOnPath(string fileName);                                   // full path of an executable on PATH, or null
    Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    int StartDetached(string fileName, IReadOnlyList<string> arguments, string workingDirectory);   // returns the new process id; its standard input, output, and error are the null device; on Windows it inherits no other handle
```

with:

```csharp
    bool FileExists(string path);
    string? FindOnPath(string fileName);                                   // full path of an executable on PATH, or null
    TimeZoneInfo? FindTimeZone(string id);                                 // a time zone by IANA ID (on Windows also by Windows ID), or null
    Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    int StartDetached(string fileName, IReadOnlyList<string> arguments, string workingDirectory);   // returns the new process id; its standard input, output, and error are the null device; on Windows it inherits no other handle
```

Edit 1 of 3 in `src/CopilotLedger.Cli/SystemCliPlatform.cs`, replace:

```csharp
using System.Diagnostics;
using System.Text;
using Microsoft.Win32;
```

with:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
```

Edit 2 of 3 in `src/CopilotLedger.Cli/SystemCliPlatform.cs`, replace:

```csharp
        }

        return null;
    }

    public async Task<ProcessRunResult> RunAsync(
```

with:

```csharp
        }

        return null;
    }

    // gh-ledger runs with invariant globalization, which turns off .NET's IANA-to-Windows mapping on Windows, so an IANA ID that
    // Windows doesn't know is mapped through Windows' own ICU.
    public TimeZoneInfo? FindTimeZone(string id)
    {
        if (id.Length is 0 or > 128 || id.Trim().Length != id.Length)
        {
            return null;
        }

        try
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out TimeZoneInfo? zone))
            {
                return zone;
            }

            return OperatingSystem.IsWindows()
                && WindowsTimeZoneId(id) is string windowsId
                && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out zone)
                    ? zone
                    : null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    // The Windows time zone ID that Windows' ICU (icu.dll, Windows 10 1903 and later) maps an IANA ID to; null when it maps to
    // none or icu.dll isn't there.
    [SupportedOSPlatform("windows")]
    public static string? WindowsTimeZoneId(string ianaId)
    {
        char[] buffer = new char[128];
        int status = 0;
        try
        {
            int length = GetWindowsTimeZoneId(ianaId, ianaId.Length, buffer, buffer.Length, ref status);
            return status > 0 || length is <= 0 or > 128 ? null : new string(buffer, 0, length);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    public async Task<ProcessRunResult> RunAsync(
```

Edit 3 of 3 in `src/CopilotLedger.Cli/SystemCliPlatform.cs`, replace:

```csharp
        }
    }
}
```

with:

```csharp
        }
    }

    // ICU's int32_t ucal_getWindowsTimeZoneID(const UChar* id, int32_t len, UChar* winid, int32_t winidCapacity, UErrorCode* status):
    // the result's length, 0 for an unknown or unmappable ID; a status above 0 is an error.
    [DllImport("icu.dll", EntryPoint = "ucal_getWindowsTimeZoneID", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetWindowsTimeZoneId(string id, int length, [Out] char[] windowsId, int capacity, ref int status);
}
```

New file `tests/CopilotLedger.Tests/Cli/ExportTimeTests.cs`:

```csharp
using System.Globalization;
using CopilotLedger.Cli;
using CopilotLedger.Cli.Export;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The export's time rules: time zones by IANA ID, nights across daylight-saving transitions, and --generated-at.
public sealed class ExportTimeTests
{
    [Fact]
    public void ET1_SystemTimeZonesResolveByIanaId()
    {
        SystemCliPlatform platform = new();
        TimeZoneInfo utc = platform.FindTimeZone("UTC")!;
        TimeZoneInfo newYork = platform.FindTimeZone("America/New_York")!;

        Assert.Equal(TimeSpan.Zero, utc.BaseUtcOffset);
        Assert.Equal(TimeSpan.FromHours(-5), newYork.GetUtcOffset(Utc("2026-01-15T12:00:00Z")));
        Assert.Equal(TimeSpan.FromHours(-4), newYork.GetUtcOffset(Utc("2026-07-01T12:00:00Z")));
        Assert.Null(platform.FindTimeZone("Invalid/Zone"));
        Assert.Null(platform.FindTimeZone(""));
        Assert.Null(platform.FindTimeZone(" UTC"));
    }

    // gh-ledger runs with invariant globalization, so on Windows an IANA ID reaches its time zone only through Windows' ICU.
    [Fact]
    public void ET2_WindowsMapsIanaIdsThroughItsIcu()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("Eastern Standard Time", SystemCliPlatform.WindowsTimeZoneId("America/New_York"));
            Assert.Equal("UTC", SystemCliPlatform.WindowsTimeZoneId("Etc/UTC"));
            Assert.Null(SystemCliPlatform.WindowsTimeZoneId("Invalid/Zone"));
            return;
        }

        TimeZoneInfo berlin = new SystemCliPlatform().FindTimeZone("Europe/Berlin")!;
        Assert.Equal(TimeSpan.FromHours(1), berlin.GetUtcOffset(Utc("2026-01-15T12:00:00Z")));
        Assert.Equal(TimeSpan.FromHours(2), berlin.GetUtcOffset(Utc("2026-07-01T12:00:00Z")));
    }

    [Fact]
    public void ET3_UtcNightsAreSixPmToSixAm()
    {
        Assert.Equal(
            [Band("2026-11-20T18:00:00Z", "2026-11-21T06:00:00Z")],
            ExportTime.NightBands(TimeZoneInfo.Utc, Utc("2026-11-20T12:00:00Z"), Utc("2026-11-21T12:00:00Z")));
    }

    [Fact]
    public void ET4_TheSpringForwardNightIsElevenHours()
    {
        IReadOnlyList<UtcBand> bands =
            ExportTime.NightBands(TestZones.NewYork, Utc("2026-03-07T12:00:00Z"), Utc("2026-03-09T12:00:00Z"));

        Assert.Equal(
            [Band("2026-03-07T23:00:00Z", "2026-03-08T10:00:00Z"), Band("2026-03-08T22:00:00Z", "2026-03-09T10:00:00Z")],
            bands);
        Assert.Equal(TimeSpan.FromHours(11), bands[0].ToUtc - bands[0].FromUtc);
    }

    [Fact]
    public void ET5_TheFallBackNightIsThirteenHours()
    {
        IReadOnlyList<UtcBand> bands =
            ExportTime.NightBands(TestZones.NewYork, Utc("2026-10-31T12:00:00Z"), Utc("2026-11-02T12:00:00Z"));

        Assert.Equal(
            [Band("2026-10-31T22:00:00Z", "2026-11-01T11:00:00Z"), Band("2026-11-01T23:00:00Z", "2026-11-02T11:00:00Z")],
            bands);
        Assert.Equal(TimeSpan.FromHours(13), bands[0].ToUtc - bands[0].FromUtc);
    }

    // On 2026-03-08, Test/Gap skips 18:00 to 18:59, so that night starts when the clocks jump, at 19:00 UTC-04:00.
    [Fact]
    public void ET6_ASkippedStartMovesToTheEndOfTheGap()
    {
        Assert.Equal(
            [Band("2026-03-08T23:00:00Z", "2026-03-09T10:00:00Z")],
            ExportTime.NightBands(TestZones.Gap, Utc("2026-03-08T12:00:00Z"), Utc("2026-03-09T12:00:00Z")));
        Assert.Equal(Utc("2026-03-08T23:00:00Z"), ExportTime.ToUtc(new DateTime(2026, 3, 8, 18, 30, 0), TestZones.Gap));
    }

    // On 2026-11-01, Test/Gap has 06:00 to 06:59 twice, so that night ends at the first 06:00, at UTC-04:00.
    [Fact]
    public void ET7_ARepeatedEndTakesItsFirstOccurrence()
    {
        Assert.Equal(
            [Band("2026-10-31T22:00:00Z", "2026-11-01T10:00:00Z")],
            ExportTime.NightBands(TestZones.Gap, Utc("2026-10-31T12:00:00Z"), Utc("2026-11-01T12:00:00Z")));
        Assert.Equal(Utc("2026-11-01T10:30:00Z"), ExportTime.ToUtc(new DateTime(2026, 11, 1, 6, 30, 0), TestZones.Gap));
    }

    [Theory]
    [InlineData("2026-11-21T21:30:00Z", "2026-11-21T21:30:00.0000000+00:00")]
    [InlineData("2026-11-21T21:30:00.1234567Z", "2026-11-21T21:30:00.1230000+00:00")]
    [InlineData("2026-11-21T21:30:00.0000000+00:00", "2026-11-21T21:30:00.0000000+00:00")]
    [InlineData("2026-11-21T21:30:00.5+00:00", "2026-11-21T21:30:00.5000000+00:00")]
    [InlineData("2026-11-21T21:30:00", null)]
    [InlineData("2026-11-21T21:30:00-05:00", null)]
    [InlineData("2026-11-21T21:30:00.Z", null)]
    [InlineData("2026-11-21T21:30:00.12345678Z", null)]
    [InlineData("2026-11-21 21:30:00Z", null)]
    [InlineData("2026-02-30T21:30:00Z", null)]
    [InlineData("", null)]
    [InlineData(" 2026-11-21T21:30:00Z", null)]
    public void ET8_GeneratedAtIsUtcToTheMillisecond(string value, string? expected)
    {
        bool parsed = ExportTime.TryParseGeneratedAt(value, out DateTimeOffset instant);

        Assert.Equal(expected is not null, parsed);
        if (expected is not null)
        {
            Assert.Equal(expected, instant.ToString("O", CultureInfo.InvariantCulture));
        }
    }

    private static UtcBand Band(string from, string to) => new(Utc(from), Utc(to));

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/TestSupport/FakeCliPlatform.cs`, replace:

```csharp
    public Dictionary<string, string> PathExecutables { get; } =
        new(StringComparer.Ordinal);
    public HashSet<string> ExistingFiles { get; } = new(StringComparer.Ordinal);
    public List<(string FileName, IReadOnlyList<string> Arguments)> ProcessRuns { get; } = [];
```

with:

```csharp
    public Dictionary<string, string> PathExecutables { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, TimeZoneInfo> TimeZones { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ExistingFiles { get; } = new(StringComparer.Ordinal);
    public List<(string FileName, IReadOnlyList<string> Arguments)> ProcessRuns { get; } = [];
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/TestSupport/FakeCliPlatform.cs`, replace:

```csharp
        PathExecutables.GetValueOrDefault(fileName);

    public Task<ProcessRunResult> RunAsync(
        string fileName,
```

with:

```csharp
        PathExecutables.GetValueOrDefault(fileName);

    public TimeZoneInfo? FindTimeZone(string id) =>
        TimeZones.GetValueOrDefault(id);

    public Task<ProcessRunResult> RunAsync(
        string fileName,
```

New file `tests/CopilotLedger.Tests/TestSupport/TestZones.cs`:

```csharp
namespace CopilotLedger.Tests.TestSupport;

// Time zones with fixed daylight-saving rules, so tests don't depend on the operating system's time zone database.
public static class TestZones
{
    // UTC-05:00, and UTC-04:00 from the second Sunday of March at 02:00 to the first Sunday of November at 02:00, as New York
    // since 2007.
    public static readonly TimeZoneInfo NewYork = Create("Test/New_York", 2, 2);

    // UTC-05:00, and UTC-04:00 from the second Sunday of March at 18:00 to the first Sunday of November at 07:00: 18:00 to 18:59
    // doesn't exist on the first day, and 06:00 to 06:59 happens twice on the last.
    public static readonly TimeZoneInfo Gap = Create("Test/Gap", 18, 7);

    private static TimeZoneInfo Create(string id, int startHour, int endHour) =>
        TimeZoneInfo.CreateCustomTimeZone(
            id,
            TimeSpan.FromHours(-5),
            id,
            id,
            id + " daylight",
            [
                TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                    DateTime.MinValue.Date,
                    DateTime.MaxValue.Date,
                    TimeSpan.FromHours(1),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, startHour, 0, 0), 3, 2, DayOfWeek.Sunday),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, endHour, 0, 0), 11, 1, DayOfWeek.Sunday)),
            ]);
}
```

### Tests

- **ET1** `SystemCliPlatform.FindTimeZone`: `UTC` (offset 0) and `America/New_York` (−05:00 at 2026-01-15T12:00Z, −04:00 at 2026-07-01T12:00Z) resolve; `Invalid/Zone`, the empty string, and ` UTC` give null.
- **ET2** On Windows, `WindowsTimeZoneId`: `America/New_York` → `Eastern Standard Time`, `Etc/UTC` → `UTC`, `Invalid/Zone` → null. Elsewhere, `FindTimeZone("Europe/Berlin")` is +01:00 in January and +02:00 in July.
- **ET3–ET7** Night bands. UTC: [2026-11-20T18:00Z, 2026-11-21T06:00Z). `Test/New_York` across spring forward: [2026-03-07T23:00Z, 2026-03-08T10:00Z), 11 hours, then [2026-03-08T22:00Z, 2026-03-09T10:00Z); across fall back: [2026-10-31T22:00Z, 2026-11-01T11:00Z), 13 hours, then [2026-11-01T23:00Z, 2026-11-02T11:00Z). `Test/Gap`'s skipped start: [2026-03-08T23:00Z, 2026-03-09T10:00Z), and `ToUtc(2026-03-08 18:30)` is 23:00Z; its repeated end: [2026-10-31T22:00Z, 2026-11-01T10:00Z), and `ToUtc(2026-11-01 06:30)` is 10:30Z.
- **ET8** (12 cases) `TryParseGeneratedAt` accepts `2026-11-21T21:30:00Z`, `2026-11-21T21:30:00.1234567Z` (→ .123), `2026-11-21T21:30:00.0000000+00:00`, and `2026-11-21T21:30:00.5+00:00` (→ .500); it rejects `2026-11-21T21:30:00`, `2026-11-21T21:30:00-05:00`, `2026-11-21T21:30:00.Z`, `2026-11-21T21:30:00.12345678Z`, `2026-11-21 21:30:00Z`, `2026-02-30T21:30:00Z`, the empty string, and ` 2026-11-21T21:30:00Z`.

### Acceptance criteria

- IANA IDs resolve on every OS, and Windows IDs on Windows; night bands follow daylight saving with the gap and repeat rules above.
- `dotnet test CopilotLedger.slnx` passes **1288** tests (1269 + 19).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 2 specification: The Export's Read Snapshot and Conversation Filter

Every number in an export must describe one ledger state, and the reports' numbers must be the export's. This task moves the bodies of five report queries into internal static helpers that take a connection and a transaction, runs them in one read snapshot, and adds the conversation filter behind `--conversation`.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/LedgerFilter.cs` | New `ConversationId` |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | `FilterSql` and `AddFilterParameters` gain `@conversation_id`; the bodies of `GetRollupAsync`, `GetCacheEfficiencyAsync`, `GetTopPromptChainsAsync`, `GetPromptCountAsync`, and `GetTurnBreakdownAsync` move into `ReadRollupAsync`, `ReadCacheEfficiencyAsync`, `ReadTopPromptChainsAsync`, `ReadPromptCountAsync`, and `ReadTurnBreakdownAsync`; new `OpenExportSnapshotAsync` |
| `src/CopilotLedger.Storage/LedgerExportSnapshot.cs` | New: the snapshot and its five report queries |
| `tests/CopilotLedger.Tests/Storage/ExportSnapshotTests.cs` | New: XS1–XS4 |

### Contracts

- `LedgerFilter.ConversationId`: only spans of this `gen_ai.conversation.id`, a Copilot session.
- `OpenExportSnapshotAsync()`: one connection and one deferred transaction (`BeginTransaction(deferred: true)`), pinned by a first read; `InvalidOperationException` before `InitializeAsync`. `LedgerExportSnapshot.DisposeAsync` ends the transaction, which never wrote, and closes the connection.
- The public methods keep their signatures, argument checks, and behavior. The snapshot's methods take the same arguments and throw the same exceptions for the same arguments.

### Code (copy exactly)

New file `src/CopilotLedger.Storage/LedgerExportSnapshot.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// One read transaction over the ledger that every query of gh ledger export shares, so its summaries and its details see the
// same rows (SqliteLedgerStore.OpenExportSnapshotAsync). It never writes. Unlike LedgerSnapshot, import's dry run, it reads an
// initialized ledger at the current schema version.
public sealed class LedgerExportSnapshot : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;

    internal LedgerExportSnapshot(SqliteConnection connection, SqliteTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    // As SqliteLedgerStore.GetRollupAsync with the recorded API-equivalent results.
    public Task<IReadOnlyList<LedgerRollupRow>> GetRollupAsync(
        LedgerGrouping grouping,
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadRollupAsync(_transaction, grouping, filter, null, cancellationToken);

    // As SqliteLedgerStore.GetCacheEfficiencyAsync.
    public Task<IReadOnlyList<CacheEfficiencyRow>> GetCacheEfficiencyAsync(
        LedgerGrouping grouping,
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadCacheEfficiencyAsync(_connection, _transaction, grouping, filter, cancellationToken);

    // As SqliteLedgerStore.GetTopPromptChainsAsync with the recorded API-equivalent results. Throws ArgumentOutOfRangeException
    // unless 1 <= limit <= 1000.
    public Task<IReadOnlyList<PromptChainRow>> GetTopPromptChainsAsync(
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return SqliteLedgerStore.ReadTopPromptChainsAsync(_transaction, filter, limit, null, cancellationToken);
    }

    // As SqliteLedgerStore.GetPromptCountAsync.
    public Task<long> GetPromptCountAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadPromptCountAsync(_connection, _transaction, filter, cancellationToken);

    // As SqliteLedgerStore.GetTurnBreakdownAsync. Throws ArgumentNullException for a null selector or filter.
    public Task<IReadOnlyList<TurnTokenRow>> GetTurnBreakdownAsync(
        TurnSelector selector,
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(filter);
        return SqliteLedgerStore.ReadTurnBreakdownAsync(_connection, _transaction, selector, filter, cancellationToken);
    }

    // Ends the read transaction, which never wrote, and closes the connection.
    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
```

Edit 1 of 1 in `src/CopilotLedger.Storage/LedgerFilter.cs`, replace:

```csharp
    public int? PullRequestNumber { get; init; }   // spans recorded with this pull request number
    public string? Agent { get; init; }            // agent name, or (none) for spans without one
}
```

with:

```csharp
    public int? PullRequestNumber { get; init; }   // spans recorded with this pull request number
    public string? Agent { get; init; }            // agent name, or (none) for spans without one
    public string? ConversationId { get; init; }   // spans of this conversation (gen_ai.conversation.id, a Copilot session)
}
```

Edit 1 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
          AND (@pull_request IS NULL OR pull_request_number = @pull_request)
          AND (@agent IS NULL OR COALESCE(agent_name, '(none)') = @agent)
          AND accounting_state = 'effective'
        """;
```

with:

```csharp
          AND (@pull_request IS NULL OR pull_request_number = @pull_request)
          AND (@agent IS NULL OR COALESCE(agent_name, '(none)') = @agent)
          AND (@conversation_id IS NULL OR conversation_id = @conversation_id)
          AND accounting_state = 'effective'
        """;
```

Edit 2 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        // One read transaction, so both queries see the same spans.
        await using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
        (string keyColumns, string keyAliases) = RollupKey(grouping);
        Dictionary<string, RetailTotals>? repriced = whatIf is null
```

with:

```csharp
        // One read transaction, so both queries see the same spans.
        await using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
        return await ReadRollupAsync(snapshot, grouping, filter, whatIf, cancellationToken);
    }

    // GetRollupAsync's queries, in a read transaction that the caller owns.
    internal static async Task<IReadOnlyList<LedgerRollupRow>> ReadRollupAsync(
        SqliteTransaction snapshot,
        LedgerGrouping grouping,
        LedgerFilter filter,
        JsonRateCardCalculator? whatIf,
        CancellationToken cancellationToken)
    {
        SqliteConnection connection = snapshot.Connection!;
        (string keyColumns, string keyAliases) = RollupKey(grouping);
        Dictionary<string, RetailTotals>? repriced = whatIf is null
```

Edit 3 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
    {
        EnsureInitialized();
        (string keyColumns, string keyAliases) = grouping switch
        {
```

with:

```csharp
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await ReadCacheEfficiencyAsync(connection, null, grouping, filter, cancellationToken);
    }

    // GetCacheEfficiencyAsync's query on connection, in transaction when it isn't null.
    internal static async Task<IReadOnlyList<CacheEfficiencyRow>> ReadCacheEfficiencyAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        LedgerGrouping grouping,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        (string keyColumns, string keyAliases) = grouping switch
        {
```

Edit 4 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        };

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {keyColumns},
```

with:

```csharp
        };

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {keyColumns},
```

Edit 5 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        // One read transaction, so every query sees the same spans.
        await using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
        List<PromptChainData> data = [];
        await using (SqliteCommand query = connection.CreateCommand())
```

with:

```csharp
        // One read transaction, so every query sees the same spans.
        await using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
        return await ReadTopPromptChainsAsync(snapshot, filter, limit, whatIf, cancellationToken);
    }

    // GetTopPromptChainsAsync's queries, in a read transaction that the caller owns.
    internal static async Task<IReadOnlyList<PromptChainRow>> ReadTopPromptChainsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        int limit,
        JsonRateCardCalculator? whatIf,
        CancellationToken cancellationToken)
    {
        SqliteConnection connection = snapshot.Connection!;
        List<PromptChainData> data = [];
        await using (SqliteCommand query = connection.CreateCommand())
```

Edit 6 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT COUNT(DISTINCT trace_id)
```

with:

```csharp
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await ReadPromptCountAsync(connection, null, filter, cancellationToken);
    }

    // GetPromptCountAsync's query on connection, in transaction when it isn't null.
    internal static async Task<long> ReadPromptCountAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT COUNT(DISTINCT trace_id)
```

Edit 7 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        ArgumentNullException.ThrowIfNull(filter);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            WITH selected AS (
```

with:

```csharp
        ArgumentNullException.ThrowIfNull(filter);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await ReadTurnBreakdownAsync(connection, null, selector, filter, cancellationToken);
    }

    // GetTurnBreakdownAsync's query on connection, in transaction when it isn't null.
    internal static async Task<IReadOnlyList<TurnTokenRow>> ReadTurnBreakdownAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        TurnSelector selector,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            WITH selected AS (
```

Edit 8 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
```

with:

```csharp
    }

    // A read snapshot for gh ledger export: one connection and one deferred transaction that every query of the export shares.
    // Its first read starts the snapshot, so rows recorded afterward never appear in it. Throws InvalidOperationException if
    // InitializeAsync hasn't run.
    public async Task<LedgerExportSnapshot> OpenExportSnapshotAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        try
        {
            SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
            await using (SqliteCommand pin = connection.CreateCommand())
            {
                pin.Transaction = transaction;
                pin.CommandText = "SELECT COUNT(*) FROM sqlite_schema;";
                await pin.ExecuteScalarAsync(cancellationToken);
            }

            return new LedgerExportSnapshot(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
```

Edit 9 of 9 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        command.Parameters.AddWithValue("@pull_request", OrDbNull(filter.PullRequestNumber));
        command.Parameters.AddWithValue("@agent", OrDbNull(filter.Agent));
    }

    private static async Task<IReadOnlyList<BreakerTrip>> ReadTripsAsync(
```

with:

```csharp
        command.Parameters.AddWithValue("@pull_request", OrDbNull(filter.PullRequestNumber));
        command.Parameters.AddWithValue("@agent", OrDbNull(filter.Agent));
        command.Parameters.AddWithValue("@conversation_id", OrDbNull(filter.ConversationId));
    }

    private static async Task<IReadOnlyList<BreakerTrip>> ReadTripsAsync(
```

New file `tests/CopilotLedger.Tests/Storage/ExportSnapshotTests.cs`:

```csharp
using System.Text.Json;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

// gh ledger export's read snapshot: the reports' queries in one read transaction, and the conversation filter.
public sealed class ExportSnapshotTests
{
    [Theory]
    [InlineData("rollup")]
    [InlineData("cache")]
    [InlineData("chains")]
    [InlineData("count")]
    [InlineData("turns")]
    public async Task XS1_TheConversationFilterLimitsEveryQuery(string query)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);
        LedgerFilter conversation = new() { ConversationId = "conv-c" };
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        switch (query)
        {
            case "rollup":
                foreach (IReadOnlyList<LedgerRollupRow> rows in new[]
                         {
                             await store.GetRollupAsync(LedgerGrouping.Model, conversation),
                             await snapshot.GetRollupAsync(LedgerGrouping.Model, conversation),
                         })
                {
                    LedgerRollupRow row = Assert.Single(rows);
                    Assert.Equal(("claude-opus-5.5", 2L, 50_000L), (row.Model, row.SpanCount, row.InputTokens));
                }

                break;
            case "cache":
                foreach (IReadOnlyList<CacheEfficiencyRow> rows in new[]
                         {
                             await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, conversation),
                             await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Model, conversation),
                         })
                {
                    CacheEfficiencyRow row = Assert.Single(rows);
                    Assert.Equal(("claude-opus-5.5", 2L, 50_000L), (row.Model, row.SpanCount, row.InputTokens));
                }

                break;
            case "chains":
                Assert.Equal(TurnSeeds.B3, Assert.Single(await store.GetTopPromptChainsAsync(conversation, 10)).TraceId);
                Assert.Equal(TurnSeeds.B3, Assert.Single(await snapshot.GetTopPromptChainsAsync(conversation, 10)).TraceId);
                break;
            case "count":
                Assert.Equal(1, await store.GetPromptCountAsync(conversation));
                Assert.Equal(1, await snapshot.GetPromptCountAsync(conversation));
                break;
            default:
                Assert.Empty(await store.GetTurnBreakdownAsync(TurnSelector.ForTrace(TurnSeeds.B1), conversation));
                Assert.Empty(await snapshot.GetTurnBreakdownAsync(TurnSelector.ForTrace(TurnSeeds.B1), conversation));
                Assert.Equal(2, (await store.GetTurnBreakdownAsync(TurnSelector.ForTrace(TurnSeeds.B3), conversation)).Count);
                Assert.Equal(2, (await snapshot.GetTurnBreakdownAsync(TurnSelector.ForTrace(TurnSeeds.B3), conversation)).Count);
                break;
        }
    }

    [Fact]
    public async Task XS2_ASnapshotDoesntSeeRequestsRecordedAfterItStarted()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        Assert.Equal(1, await store.RecordAsync([Entry()]));

        Assert.Equal(4, await snapshot.GetPromptCountAsync(LedgerFilter.All));
        Assert.Equal(10, (await snapshot.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All)).Sum(row => row.SpanCount));
        Assert.Equal(4, (await snapshot.GetTopPromptChainsAsync(LedgerFilter.All, 10)).Count);
        Assert.Equal(5, await store.GetPromptCountAsync(LedgerFilter.All));
        await using LedgerExportSnapshot later = await store.OpenExportSnapshotAsync();
        Assert.Equal(5, await later.GetPromptCountAsync(LedgerFilter.All));
    }

    [Fact]
    public async Task XS3_TheSnapshotAnswersAsTheStoreDoes()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        foreach (LedgerFilter filter in new[] { LedgerFilter.All, new LedgerFilter { Repository = "swigerb/copilot-ledger" } })
        {
            foreach (LedgerGrouping grouping in new[]
                     {
                         LedgerGrouping.Repository, LedgerGrouping.Branch, LedgerGrouping.Model, LedgerGrouping.Agent,
                         LedgerGrouping.ChannelModel,
                     })
            {
                IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(grouping, filter);
                Assert.NotEmpty(rows);
                Assert.Equal(Json(rows), Json(await snapshot.GetRollupAsync(grouping, filter)));
            }

            foreach (LedgerGrouping grouping in new[]
                     {
                         LedgerGrouping.Repository, LedgerGrouping.Branch, LedgerGrouping.Model, LedgerGrouping.Agent,
                         LedgerGrouping.PullRequest,
                     })
            {
                IReadOnlyList<CacheEfficiencyRow> rows = await store.GetCacheEfficiencyAsync(grouping, filter);
                Assert.NotEmpty(rows);
                Assert.Equal(Json(rows), Json(await snapshot.GetCacheEfficiencyAsync(grouping, filter)));
            }

            Assert.Equal(
                Json(await store.GetTopPromptChainsAsync(filter, 10)),
                Json(await snapshot.GetTopPromptChainsAsync(filter, 10)));
            Assert.Equal(await store.GetPromptCountAsync(filter), await snapshot.GetPromptCountAsync(filter));
            Assert.Equal(
                Json(await store.GetTurnBreakdownAsync(TurnSelector.ForConversation("conv-b"), filter)),
                Json(await snapshot.GetTurnBreakdownAsync(TurnSelector.ForConversation("conv-b"), filter)));
        }
    }

    [Fact]
    public async Task XS4_ASnapshotNeedsAnInitializedLedger()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenExportSnapshotAsync());
        await store.InitializeAsync();
        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync())
        {
            Assert.Equal(0, await snapshot.GetPromptCountAsync(LedgerFilter.All));
        }

        Assert.Equal(1, await store.RecordAsync([Entry()]));
        Assert.Equal(1, await store.GetPromptCountAsync(LedgerFilter.All));
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        await TurnSeeds.SeedAsync(store, temp.Path);
        return store;
    }

    // One request in a new prompt chain and conversation.
    private static LedgerEntry Entry()
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: new DateTimeOffset(2026, 11, 21, 13, 0, 0, TimeSpan.Zero),
            requestModel: "claude-opus-5.5",
            conversationId: "conv-d",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "b4b4b4b4b4b4b4b4b4b4b4b4b4b4b4b4",
            SpanId = "f400000000000001",
        };
        return new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
}
```

### Tests

On `TurnSeeds` plus a request in another conversation:

- **XS1** (5 cases) `ConversationId = "conv-c"` limits the model rollup, cache efficiency by model, prompt chains (only B3), the prompt count (1), and the turn breakdown (B1's trace with the filter: no rows), on the store and on the snapshot.
- **XS2** Isolation: after the snapshot's first read, `RecordAsync` adds a request; the snapshot's rollup, prompt count, and chains don't change, the store's do, and a new snapshot sees the request.
- **XS3** For `LedgerFilter.All` and a repository filter, each snapshot method returns what the store's method returns, compared as JSON.
- **XS4** `OpenExportSnapshotAsync` before `InitializeAsync` throws `InvalidOperationException`; after a snapshot is disposed, `RecordAsync` succeeds.

### Acceptance criteria

- The reports return what they returned before; the snapshot returns the same rows in one read transaction.
- `dotnet test CopilotLedger.slnx` passes **1296** tests (1288 + 8).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 3 specification: Export Rows

The export needs every effective request and tool call in pages, the latest ones for the timeline, and what the ledger can't account for. This task adds those reads to the snapshot, with read models that list their columns explicitly, and the export tests' shared ledger.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/ExportCursor.cs`, `ExportSpanRow.cs`, `ExportToolCallRow.cs`, `ExportCoverageRow.cs` (with `ExportSourceCount`), `ExportRatesProvenance.cs` (with `ExportApiRateCard`) | New read models |
| `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs` | New partial class: the export's queries |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | `FilterSql` becomes `SpanFilterSql`, a filter's conditions on any row, plus the effective-row predicate; the budgets' `SELECT` becomes `BranchBudgetSelectSql`, shared with the export |
| `src/CopilotLedger.Storage/LedgerExportSnapshot.cs` | The row methods |
| `tests/CopilotLedger.Tests/TestSupport/ExportSeeds.cs` | New: the shared ledger, and `SupersedeUnmatchedAsync` |
| `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs` | New: XR1–XR10 |

### Contracts and queries

- `GetSpansAsync` and `GetToolCallsAsync` ascend by (start, trace ID, span ID) after an `ExportCursor`, with a limit of 1 to 10,000; `GetLatestSpansAsync` and `GetLatestToolCallsAsync` descend by the same key, with a limit of 0 to 100,000; `ArgumentOutOfRangeException` outside those ranges.
- A tool call is an `execute_tool` link that `EffectiveLinkSql` keeps, whose trace has a selected request and whose start is in the window; `CountToolCallsAsync` counts them. Breaker trips are those of the selected traces; budgets those of the selected requests' repository and branch, with all-time spend; repository roots are the `repo_root` values of selected rows without a slug.
- `GetCoverageAsync(filter, toolHistoryFrom)`: the selected requests by `ingest_source`; superseded imports whose live row is selected and has the same canonical usage ID (matched: the same call, counted once); superseded imports whose live row doesn't share their canonical usage ID and that the filter selects by their own columns (unmatched: they may be calls that no live span recorded); the whole ledger's deferred import records and incompletely imported logs (`import_sessions` has no repository); the selected requests that start before `toolHistoryFrom`; imported tool calls without detail; and requests with reported, absent, and legacy-unknown cache counters.
- `GetRatesProvenanceAsync`: the distinct rate versions, and API rate-card versions with their SHA-256 hashes, that priced the selection, ordinal.

### The shared ledger (`ExportSeeds`)

`GeneratedAt` is 2026-11-21T21:30:00Z, 16:30 at `ManualTimeProvider.UtcMinus5`, the export tests' default zone. `SeedAsync(store, root)` records, with the embedded rates unless noted:

| Row | Trace / conversation | Start (UTC) | Request | Git | Billing |
|---|---|---|---|---|---|
| A1 | `a1…a1` / `conv-a` | 11-21 14:00:00 | `claude-opus-5.5`, 100,000 in (40,000 cache reads, reported), 10,000 out | `GitFixture.Feature(root)`, pull request 42 | GitHub $0.448 (44.8 credits); API $0.448 |
| A2 | `a1…a1` / `conv-a`, agent `explore` | 11-21 14:01:00 | `mai-code-2`, 200,000 in, 20,000 out, no cache counters | the same | GitHub $0.15; API unpriced |
| B1 | `b1…b1` / `conv-b` | 11-21 15:00:00 | `gpt-6.1-sol` at `contoso.openai.azure.com`, priced with `RateFixtures.ByokTestRatesJson` | `Feature` | Foundry BYOK $0.315; API unpriced |
| C1 | `c1…c1` / `conv-c` | 11-21 16:00:00 | `qwen3.8-27b` at `127.0.0.1`, 300,000 in, 9,000 out | `Feature`, branch `main` | Local, saved $0.1725; API unpriced |
| C2 | `c1…c1` / `conv-c` | 11-21 16:05:00 | `gpt-unknown`, 1,000 in, 100 out | `main` | `unknown_model`; API unpriced |
| D1 | `d1…d1` / `conv-d` | 11-21 17:00:00 | `claude-opus-5.5`, 10,000 in, 1,000 out, no cache counters | `<root>/private-7c1d/tools`, no remote, `main` | GitHub $0.06; API $0.06, an estimate |
| I1 | imported, `conv-i` | 11-20 12:00:00 | `mai-code-2`, 1,000 in, 100 out, with a tool call that exited 0 | none | `import:copilot`, GitHub $0.00075 |
| I2 | imported, `conv-j`, service request ID `S` | 11-20 12:00:00 | the same, with a tool call that exited 1 | none | superseded by J1 (matched) |
| J1 | live `f1…f1` / `conv-j`, `S` | 11-20 12:00:00 | the same | none | GitHub $0.00075 |

Links: `execute_tool` T1 and T2 (trace `a1`, `bash`, exit 0 at 14:00:30 and exit 1 at 14:00:40), T3 (`b1`, `view`, status 2 at 15:00:10), T4 (trace `e1…e1`, which has no request, at 15:30), and T5, `a1`'s `invoke_agent`. Breaker trips: `credit_velocity` on `a1` (open), `retry_churn` on `c1` (reset), and `credit_velocity` on `e1` (open). Budgets: 50 credits on `swigerb/copilot-ledger` `feature/ledger` and 10 on `contoso/api` `main`. `SupersedeUnmatchedAsync(store)` adds an import in `conv-u` (11-20 12:00, no service request ID) and then a live span of `conv-u` without one (trace `7a…7a`, 11-21 12:00), which supersedes it without matching it.

Every repository, `--since 30d`: 8 requests (A1, A2, B1, C1, C2, D1, I1, J1), 1 unpriced (C2); GitHub $0.6595 (65.95 credits); BYOK $0.315; local saved $0.1725; API $0.508 with 6 unpriced and 1 estimated; 1 of 8 requests reported cache counters, 40.0%; 6 prompt chains; 4 tool calls (T1, T2, T3, and I1's), 2 failed; the trips on `c1` and `a1`; the 50-credit budget, spent 91.3, exceeded.

### Code (copy exactly)

New file `src/CopilotLedger.Storage/ExportCoverageRow.cs`:

```csharp
namespace CopilotLedger.Storage;

public sealed record ExportSourceCount(string Source, long Spans);

// What an export's selection covers, and what the ledger can't account for.
public sealed record ExportCoverageRow
{
    public required IReadOnlyList<ExportSourceCount> SpansBySource { get; init; }   // sources with spans, ordinal
    public required long SupersededMatchedImportSpans { get; init; }   // superseded by a selected live row of the same call
    public required long SupersededUnmatchedImportSpans { get; init; } // superseded without a match, selected by their own columns
    public required long LedgerAmbiguousImportRecords { get; init; } // import_sessions, the whole ledger
    public required long LedgerIncompleteImportLogs { get; init; }   // import_sessions with complete = 0, the whole ledger
    public required long SpansBeforeToolHistory { get; init; }       // selected spans that start before toolHistoryFrom
    public required long ImportedToolCallsWithoutDetail { get; init; }
    public required long CacheReportedSpans { get; init; }
    public required long CacheAbsentSpans { get; init; }
    public required long CacheLegacyUnknownSpans { get; init; }
}
```

New file `src/CopilotLedger.Storage/ExportCursor.cs`:

```csharp
namespace CopilotLedger.Storage;

// The last row of a page of export rows. The next page starts after it in (start time, trace ID, span ID) order.
public sealed record ExportCursor(long StartTimeUnixMs, string TraceId, string SpanId);
```

New file `src/CopilotLedger.Storage/ExportRatesProvenance.cs`:

```csharp
namespace CopilotLedger.Storage;

public sealed record ExportApiRateCard(string RatesVersion, string Sha256);

// The rate cards that priced an export's selection.
public sealed record ExportRatesProvenance
{
    public required IReadOnlyList<string> RatesVersions { get; init; }              // distinct rates_version, ordinal
    public required IReadOnlyList<ExportApiRateCard> ApiRateCards { get; init; }    // distinct (api_rates_version, api_rate_card_sha256), ordinal
}
```

New file `src/CopilotLedger.Storage/ExportSpanRow.cs`:

```csharp
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

// One effective inference span, with exactly the columns that gh ledger export may use.
public sealed record ExportSpanRow
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string OperationName { get; init; }
    public string? ConversationId { get; init; }
    public string? AgentName { get; init; }
    public string? ProviderName { get; init; }
    public string? RequestModel { get; init; }
    public string? ResponseModel { get; init; }
    public string? ModelId { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required CacheCounterState CacheReadState { get; init; }
    public required CacheCounterState CacheWriteState { get; init; }
    public required BillingChannel BillingChannel { get; init; }
    public required string PricingStatus { get; init; }           // as stored: priced, local_offload, byok_unpriced, …
    public required string RatesVersion { get; init; }
    public required decimal CostUsd { get; init; }
    public required decimal SavedUsd { get; init; }
    public decimal? ApiEquivalentUsd { get; init; }
    public string? ApiEquivalentStatus { get; init; }             // priced, unpriced, not_applicable, or null (recorded before version 5)
    public string? ApiRatesVersion { get; init; }
    public string? ApiRateCardSha256 { get; init; }
    public string? RepositorySlug { get; init; }
    public string? RepositoryRoot { get; init; }
    public string? BranchName { get; init; }
    public string? HeadCommitSha { get; init; }
    public int? PullRequestNumber { get; init; }
    public required string IngestSource { get; init; }            // otlp, import:copilot, import:codex
}
```

New file `src/CopilotLedger.Storage/ExportToolCallRow.cs`:

```csharp
namespace CopilotLedger.Storage;

// One retained execute_tool link of a selected prompt chain.
public sealed record ExportToolCallRow
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ConversationId { get; init; }
    public string? AgentName { get; init; }
    public string? ToolName { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public long? ToolExitCode { get; init; }
    public required int StatusCode { get; init; }                 // OTLP status: 0 unset, 1 ok, 2 error
    public required string IngestSource { get; init; }            // its owner's ingest_source; otlp for a live link
}
```

Edit 1 of 2 in `src/CopilotLedger.Storage/LedgerExportSnapshot.cs`, replace:

```csharp
    }

    // Ends the read transaction, which never wrote, and closes the connection.
    public async ValueTask DisposeAsync()
```

with:

```csharp
    }

    // The filter's spans, ascending by (start time, trace ID, span ID), after `after` when it isn't null. Throws
    // ArgumentOutOfRangeException unless 1 <= limit <= 10,000.
    public Task<IReadOnlyList<ExportSpanRow>> GetSpansAsync(
        LedgerFilter filter,
        ExportCursor? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        CheckPageLimit(limit);
        return SqliteLedgerStore.ReadExportSpansAsync(_transaction, filter, after, limit, cancellationToken);
    }

    // The filter's latest spans, descending by (start time, trace ID, span ID). Throws ArgumentOutOfRangeException unless
    // 0 <= limit <= 100,000.
    public Task<IReadOnlyList<ExportSpanRow>> GetLatestSpansAsync(
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        CheckLatestLimit(limit);
        return SqliteLedgerStore.ReadLatestExportSpansAsync(_transaction, filter, limit, cancellationToken);
    }

    // The execute_tool links that count and start in the filter's period, of the prompt chains with a selected span, ascending
    // by (start time, trace ID, span ID), after `after` when it isn't null. Throws ArgumentOutOfRangeException unless
    // 1 <= limit <= 10,000.
    public Task<IReadOnlyList<ExportToolCallRow>> GetToolCallsAsync(
        LedgerFilter filter,
        ExportCursor? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        CheckPageLimit(limit);
        return SqliteLedgerStore.ReadExportToolCallsAsync(_transaction, filter, after, limit, cancellationToken);
    }

    // GetToolCallsAsync's latest rows, descending. Throws ArgumentOutOfRangeException unless 0 <= limit <= 100,000.
    public Task<IReadOnlyList<ExportToolCallRow>> GetLatestToolCallsAsync(
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        CheckLatestLimit(limit);
        return SqliteLedgerStore.ReadLatestExportToolCallsAsync(_transaction, filter, limit, cancellationToken);
    }

    // The number of rows GetToolCallsAsync pages through.
    public Task<long> CountToolCallsAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.CountExportToolCallsAsync(_transaction, filter, cancellationToken);

    // What the filter's spans cover. Spans that start before toolHistoryFrom may have lost their tool calls to retention.
    public Task<ExportCoverageRow> GetCoverageAsync(
        LedgerFilter filter,
        DateTimeOffset toolHistoryFrom,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadExportCoverageAsync(_transaction, filter, toolHistoryFrom, cancellationToken);

    public Task<ExportRatesProvenance> GetRatesProvenanceAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadExportRatesProvenanceAsync(_transaction, filter, cancellationToken);

    // The breaker trips of the prompt chains with a selected span, newest first.
    public Task<IReadOnlyList<BreakerTrip>> GetBreakerTripsAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadExportBreakerTripsAsync(_transaction, filter, cancellationToken);

    // The budgets of the selected spans' repository and branch keys, as budget list shows them.
    public Task<IReadOnlyList<BranchBudgetRow>> GetBudgetsAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadExportBudgetsAsync(_transaction, filter, cancellationToken);

    // The selected spans' repository roots that have no GitHub slug, ordinal.
    public Task<IReadOnlyList<string>> GetRepositoryRootsAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadExportRepositoryRootsAsync(_transaction, filter, cancellationToken);

    // Ends the read transaction, which never wrote, and closes the connection.
    public async ValueTask DisposeAsync()
```

Edit 2 of 2 in `src/CopilotLedger.Storage/LedgerExportSnapshot.cs`, replace:

```csharp
        await _connection.DisposeAsync();
    }
}
```

with:

```csharp
        await _connection.DisposeAsync();
    }

    private static void CheckPageLimit(int limit)
    {
        if (limit is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
    }

    private static void CheckLatestLimit(int limit)
    {
        if (limit is < 0 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
    }
}
```

New file `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// The row queries of gh ledger export, which LedgerExportSnapshot runs in its read transaction. Spans, coverage counts,
// provenance, and roots read effective rows (FilterSql); tool calls, breaker trips, and budgets belong to them.
public sealed partial class SqliteLedgerStore
{
    // ExportSpanRow's columns, read by ordinal in ReadExportSpan.
    private const string ExportSpanColumnsSql = """
        trace_id, span_id, parent_span_id, operation_name, conversation_id, agent_name, provider_name, request_model,
               response_model, model_id, start_time_unix_ms, end_time_unix_ms, input_tokens, output_tokens,
               cache_read_input_tokens, cache_write_input_tokens, cache_read_state, cache_write_state, billing_channel,
               pricing_status, rates_version, cost_usd_pico, saved_usd_pico, api_equivalent_usd_pico, api_equivalent_status,
               api_rates_version, api_rate_card_sha256, repo_slug, repo_root, branch_name, head_commit_sha, pull_request_number,
               ingest_source
        """;

    // ExportToolCallRow's columns, read by ordinal in ReadExportToolCall. A live link has no owner row.
    private const string ExportToolCallColumnsSql = """
        trace_id, span_id, conversation_id, agent_name, tool_name, start_time_unix_ms, tool_exit_code, status_code,
               COALESCE((SELECT owner.ingest_source FROM spans AS owner WHERE owner.id = span_links.owner_span_row_id), 'otlp')
        """;

    // The execute_tool links that count, start in the filter's period, and belong to a prompt chain with a selected span.
    // Unqualified columns are span_links' outside the subquery and spans' inside it.
    private const string ExportToolCallWhereSql = $"""
        operation_name = 'execute_tool'
          AND {EffectiveLinkSql}
          AND (@from IS NULL OR start_time_unix_ms >= @from)
          AND (@to IS NULL OR start_time_unix_ms < @to)
          AND trace_id IN (SELECT trace_id FROM spans WHERE {FilterSql})
        """;

    // Rows after the cursor in (start time, trace ID, span ID) order, or every row when there's no cursor.
    private const string AfterCursorSql =
        "(@after_start IS NULL OR (start_time_unix_ms, trace_id, span_id) > (@after_start, @after_trace, @after_span))";

    internal static Task<IReadOnlyList<ExportSpanRow>> ReadExportSpansAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        ExportCursor? after,
        int limit,
        CancellationToken cancellationToken) =>
        ReadExportRowsAsync(
            snapshot,
            $"""
            SELECT {ExportSpanColumnsSql}
            FROM spans
            WHERE {FilterSql}
              AND {AfterCursorSql}
            ORDER BY start_time_unix_ms, trace_id, span_id
            LIMIT @limit;
            """,
            filter,
            after,
            limit,
            ReadExportSpan,
            cancellationToken);

    internal static Task<IReadOnlyList<ExportSpanRow>> ReadLatestExportSpansAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken) =>
        ReadExportRowsAsync(
            snapshot,
            $"""
            SELECT {ExportSpanColumnsSql}
            FROM spans
            WHERE {FilterSql}
            ORDER BY start_time_unix_ms DESC, trace_id DESC, span_id DESC
            LIMIT @limit;
            """,
            filter,
            null,
            limit,
            ReadExportSpan,
            cancellationToken);

    internal static Task<IReadOnlyList<ExportToolCallRow>> ReadExportToolCallsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        ExportCursor? after,
        int limit,
        CancellationToken cancellationToken) =>
        ReadExportRowsAsync(
            snapshot,
            $"""
            SELECT {ExportToolCallColumnsSql}
            FROM span_links
            WHERE {ExportToolCallWhereSql}
              AND {AfterCursorSql}
            ORDER BY start_time_unix_ms, trace_id, span_id
            LIMIT @limit;
            """,
            filter,
            after,
            limit,
            ReadExportToolCall,
            cancellationToken);

    internal static Task<IReadOnlyList<ExportToolCallRow>> ReadLatestExportToolCallsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken) =>
        ReadExportRowsAsync(
            snapshot,
            $"""
            SELECT {ExportToolCallColumnsSql}
            FROM span_links
            WHERE {ExportToolCallWhereSql}
            ORDER BY start_time_unix_ms DESC, trace_id DESC, span_id DESC
            LIMIT @limit;
            """,
            filter,
            null,
            limit,
            ReadExportToolCall,
            cancellationToken);

    internal static async Task<long> CountExportToolCallsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"SELECT COUNT(*) FROM span_links WHERE {ExportToolCallWhereSql};";
        AddFilterParameters(command, filter);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    // A superseded row that a live span of the same call replaced (the same canonical usage ID) counts when that live row is
    // selected: it's the same request, counted once. One that live spans of its session replaced without matching it may be a
    // call that no live span covers, so it counts when the filter selects it by its own columns, as gh ledger import reports
    // it. import_sessions has no repository, so its two counts are the whole ledger's.
    internal static async Task<ExportCoverageRow> ReadExportCoverageAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        DateTimeOffset toolHistoryFrom,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT ingest_source, COUNT(*) FROM spans WHERE {FilterSql} GROUP BY ingest_source ORDER BY ingest_source;

            SELECT COUNT(*) FROM spans AS superseded JOIN spans AS live ON live.id = superseded.superseded_by
            WHERE superseded.accounting_state = 'superseded'
              AND live.canonical_usage_id = superseded.canonical_usage_id
              AND live.id IN (SELECT id FROM spans WHERE {FilterSql});

            SELECT COUNT(*) FROM spans
            WHERE {SpanFilterSql}
              AND accounting_state = 'superseded'
              AND NOT EXISTS (SELECT 1 FROM spans AS live
                              WHERE live.id = spans.superseded_by AND live.canonical_usage_id = spans.canonical_usage_id);

            SELECT COALESCE(SUM(CASE WHEN start_time_unix_ms < @tool_history_from THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN imported_tool_call_count > 0
                                      AND NOT EXISTS (SELECT 1 FROM span_links WHERE span_links.owner_span_row_id = spans.id)
                                     THEN imported_tool_call_count ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN NOT {ReportedSql}
                                      AND cache_read_state <> 'legacy_unknown' AND cache_write_state <> 'legacy_unknown'
                                     THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN NOT {ReportedSql}
                                      AND (cache_read_state = 'legacy_unknown' OR cache_write_state = 'legacy_unknown')
                                     THEN 1 ELSE 0 END), 0)
            FROM spans WHERE {FilterSql};

            SELECT COALESCE(SUM(records_ambiguous), 0), COALESCE(SUM(CASE WHEN complete = 0 THEN 1 ELSE 0 END), 0)
            FROM import_sessions;
            """;
        AddFilterParameters(command, filter);
        command.Parameters.AddWithValue("@tool_history_from", toolHistoryFrom.ToUnixTimeMilliseconds());
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<ExportSourceCount> sources = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            sources.Add(new ExportSourceCount(reader.GetString(0), reader.GetInt64(1)));
        }

        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        long matched = reader.GetInt64(0);
        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        long unmatched = reader.GetInt64(0);
        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        (long before, long withoutDetail, long reported, long absent, long legacy) =
            (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4));
        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        sources.Sort((left, right) => string.CompareOrdinal(left.Source, right.Source));
        return new ExportCoverageRow
        {
            SpansBySource = sources,
            SupersededMatchedImportSpans = matched,
            SupersededUnmatchedImportSpans = unmatched,
            LedgerAmbiguousImportRecords = reader.GetInt64(0),
            LedgerIncompleteImportLogs = reader.GetInt64(1),
            SpansBeforeToolHistory = before,
            ImportedToolCallsWithoutDetail = withoutDetail,
            CacheReportedSpans = reported,
            CacheAbsentSpans = absent,
            CacheLegacyUnknownSpans = legacy,
        };
    }

    internal static async Task<ExportRatesProvenance> ReadExportRatesProvenanceAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT DISTINCT rates_version FROM spans WHERE {FilterSql} ORDER BY rates_version;

            SELECT DISTINCT api_rates_version, api_rate_card_sha256 FROM spans
            WHERE {FilterSql} AND api_rates_version IS NOT NULL AND api_rate_card_sha256 IS NOT NULL
            ORDER BY api_rates_version, api_rate_card_sha256;
            """;
        AddFilterParameters(command, filter);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<string> versions = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(reader.GetString(0));
        }

        await reader.NextResultAsync(cancellationToken);
        List<ExportApiRateCard> cards = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            cards.Add(new ExportApiRateCard(reader.GetString(0), reader.GetString(1)));
        }

        versions.Sort(StringComparer.Ordinal);
        cards.Sort((left, right) =>
        {
            int order = string.CompareOrdinal(left.RatesVersion, right.RatesVersion);
            return order != 0 ? order : string.CompareOrdinal(left.Sha256, right.Sha256);
        });
        return new ExportRatesProvenance { RatesVersions = versions, ApiRateCards = cards };
    }

    // The trips of the selected prompt chains, newest first.
    internal static async Task<IReadOnlyList<BreakerTrip>> ReadExportBreakerTripsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT id, rule, trace_id, conversation_id, repository, branch, repo_root, tripped_at_unix_ms, detail,
                   window_cost_usd_pico, window_minutes, failure_count, recorded_at_unix_ms, notified_at_unix_ms, reset_at_unix_ms
            FROM breaker_trips
            WHERE trace_id IN (SELECT trace_id FROM spans WHERE {FilterSql})
            ORDER BY tripped_at_unix_ms DESC, id DESC;
            """;
        AddFilterParameters(command, filter);
        return await ReadTripsAsync(command, cancellationToken);
    }

    // The budgets of the selected spans' repository and branch keys, with their all-time spend as budget list shows it.
    internal static async Task<IReadOnlyList<BranchBudgetRow>> ReadExportBudgetsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            {BranchBudgetSelectSql}
            WHERE EXISTS (SELECT 1 FROM spans
                           WHERE {FilterSql}
                             AND COALESCE(repo_slug, repo_root, '(none)') = b.repository
                             AND COALESCE(branch_name, '(none)') = b.branch)
            ORDER BY b.repository, b.branch;
            """;
        AddFilterParameters(command, filter);
        return await ReadBranchBudgetsAsync(command, cancellationToken);
    }

    // The selected spans' repository keys that are local paths: the roots of repositories without a GitHub slug.
    internal static async Task<IReadOnlyList<string>> ReadExportRepositoryRootsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT DISTINCT repo_root FROM spans
            WHERE {FilterSql} AND repo_slug IS NULL AND repo_root IS NOT NULL
            ORDER BY repo_root;
            """;
        AddFilterParameters(command, filter);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<string> roots = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            roots.Add(reader.GetString(0));
        }

        roots.Sort(StringComparer.Ordinal);
        return roots;
    }

    private static async Task<IReadOnlyList<T>> ReadExportRowsAsync<T>(
        SqliteTransaction snapshot,
        string sql,
        LedgerFilter filter,
        ExportCursor? after,
        int limit,
        Func<SqliteDataReader, T> read,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = sql;
        AddFilterParameters(command, filter);
        command.Parameters.AddWithValue("@after_start", OrDbNull(after?.StartTimeUnixMs));
        command.Parameters.AddWithValue("@after_trace", OrDbNull(after?.TraceId));
        command.Parameters.AddWithValue("@after_span", OrDbNull(after?.SpanId));
        command.Parameters.AddWithValue("@limit", limit);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<T> rows = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    private static ExportSpanRow ReadExportSpan(SqliteDataReader reader) => new()
    {
        TraceId = reader.GetString(0),
        SpanId = reader.GetString(1),
        ParentSpanId = TextOrNull(reader, 2),
        OperationName = reader.GetString(3),
        ConversationId = TextOrNull(reader, 4),
        AgentName = TextOrNull(reader, 5),
        ProviderName = TextOrNull(reader, 6),
        RequestModel = TextOrNull(reader, 7),
        ResponseModel = TextOrNull(reader, 8),
        ModelId = TextOrNull(reader, 9),
        StartTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10)),
        EndTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(11)),
        InputTokens = reader.GetInt64(12),
        OutputTokens = reader.GetInt64(13),
        CacheReadInputTokens = reader.GetInt64(14),
        CacheWriteInputTokens = reader.GetInt64(15),
        CacheReadState = ToCacheCounterState(reader.GetString(16)),
        CacheWriteState = ToCacheCounterState(reader.GetString(17)),
        BillingChannel = ToBillingChannel(reader.GetString(18)),
        PricingStatus = reader.GetString(19),
        RatesVersion = reader.GetString(20),
        CostUsd = PicoUsd.ToUsd(reader.GetInt64(21)),
        SavedUsd = PicoUsd.ToUsd(reader.GetInt64(22)),
        ApiEquivalentUsd = reader.IsDBNull(23) ? null : PicoUsd.ToUsd(reader.GetInt64(23)),
        ApiEquivalentStatus = TextOrNull(reader, 24),
        ApiRatesVersion = TextOrNull(reader, 25),
        ApiRateCardSha256 = TextOrNull(reader, 26),
        RepositorySlug = TextOrNull(reader, 27),
        RepositoryRoot = TextOrNull(reader, 28),
        BranchName = TextOrNull(reader, 29),
        HeadCommitSha = TextOrNull(reader, 30),
        PullRequestNumber = reader.IsDBNull(31) ? null : reader.GetInt32(31),
        IngestSource = reader.GetString(32),
    };

    private static ExportToolCallRow ReadExportToolCall(SqliteDataReader reader) => new()
    {
        TraceId = reader.GetString(0),
        SpanId = reader.GetString(1),
        ConversationId = TextOrNull(reader, 2),
        AgentName = TextOrNull(reader, 3),
        ToolName = TextOrNull(reader, 4),
        StartTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
        ToolExitCode = reader.IsDBNull(6) ? null : reader.GetInt64(6),
        StatusCode = reader.GetInt32(7),
        IngestSource = reader.GetString(8),
    };

    private static string? TextOrNull(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
```

Edit 1 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        """;

    // The conditions of a LedgerFilter, with parameters from AddFilterParameters. A null filter property matches every span.
    // Only effective rows count: an imported row that a live row superseded is kept for diagnosis and never reported.
    private const string FilterSql = """
        (@from IS NULL OR start_time_unix_ms >= @from)
          AND (@to IS NULL OR start_time_unix_ms < @to)
```

with:

```csharp
        """;

    // The conditions of a LedgerFilter on a span row, whatever its accounting state, with parameters from AddFilterParameters.
    // A null filter property matches every span.
    private const string SpanFilterSql = """
        (@from IS NULL OR start_time_unix_ms >= @from)
          AND (@to IS NULL OR start_time_unix_ms < @to)
```

Edit 2 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
          AND (@agent IS NULL OR COALESCE(agent_name, '(none)') = @agent)
          AND (@conversation_id IS NULL OR conversation_id = @conversation_id)
          AND accounting_state = 'effective'
        """;

    // An execute_tool link counts unless an imported row that was superseded owns it (its live twin's links count instead).
```

with:

```csharp
          AND (@agent IS NULL OR COALESCE(agent_name, '(none)') = @agent)
          AND (@conversation_id IS NULL OR conversation_id = @conversation_id)
        """;

    // The conditions of a LedgerFilter, with parameters from AddFilterParameters. A null filter property matches every span.
    // Only effective rows count: an imported row that a live row superseded is kept for diagnosis and never reported.
    private const string FilterSql = SpanFilterSql + "\n  AND accounting_state = 'effective'";

    // An execute_tool link counts unless an imported row that was superseded owns it (its live twin's links count instead).
```

Edit 3 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        trace_id, agent_span_id, COALESCE(repo_slug, repo_root, '(none)'), COALESCE(branch_name, '(none)'),
                                 COALESCE(conversation_id, '(none)'), COALESCE(model_id, '(none)')
        """;

    private readonly TimeProvider _timeProvider;
```

with:

```csharp
        trace_id, agent_span_id, COALESCE(repo_slug, repo_root, '(none)'), COALESCE(branch_name, '(none)'),
                                 COALESCE(conversation_id, '(none)'), COALESCE(model_id, '(none)')
        """;

    // Budgets with their key's all-time effective spend, in ReadBranchBudgetsAsync's column order, from branch_budgets AS b.
    private const string BranchBudgetSelectSql = """
        SELECT b.repository, b.branch, b.budget_usd_pico, b.set_at_unix_ms, b.alerted_at_unix_ms, b.notified_at_unix_ms,
               (SELECT COALESCE(SUM(s.cost_usd_pico), 0) FROM spans AS s
                 WHERE COALESCE(s.repo_slug, s.repo_root, '(none)') = b.repository
                   AND COALESCE(s.branch_name, '(none)') = b.branch
                   AND s.accounting_state = 'effective') AS spent_usd_pico
        FROM branch_budgets AS b
        """;

    private readonly TimeProvider _timeProvider;
```

Edit 4 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.cs`, replace:

```csharp
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.repository, b.branch, b.budget_usd_pico, b.set_at_unix_ms, b.alerted_at_unix_ms, b.notified_at_unix_ms,
                   (SELECT COALESCE(SUM(s.cost_usd_pico), 0) FROM spans AS s
                     WHERE COALESCE(s.repo_slug, s.repo_root, '(none)') = b.repository
                       AND COALESCE(s.branch_name, '(none)') = b.branch
                       AND s.accounting_state = 'effective') AS spent_usd_pico
            FROM branch_budgets AS b
            WHERE (@repository IS NULL OR b.repository = @repository) AND (@branch IS NULL OR b.branch = @branch) ORDER BY b.repository, b.branch;
            """;
```

with:

```csharp
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            {BranchBudgetSelectSql}
            WHERE (@repository IS NULL OR b.repository = @repository) AND (@branch IS NULL OR b.branch = @branch) ORDER BY b.repository, b.branch;
            """;
```

New file `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs`:

```csharp
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

// gh ledger export's row queries, on ExportSeeds.
public sealed class ExportRowTests
{
    private static readonly LedgerFilter InRepository = new() { Repository = "swigerb/copilot-ledger" };

    [Fact]
    public async Task XR1_PagesReturnEachEffectiveSpanOnceInOrder()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        List<int> pages = [];
        List<ExportSpanRow> rows = [];
        ExportCursor? after = null;
        IReadOnlyList<ExportSpanRow> page;
        while ((page = await snapshot.GetSpansAsync(LedgerFilter.All, after, 3)).Count > 0)
        {
            pages.Add(page.Count);
            rows.AddRange(page);
            after = new ExportCursor(page[^1].StartTime.ToUnixTimeMilliseconds(), page[^1].TraceId, page[^1].SpanId);
        }

        Assert.Equal([3, 3, 2], pages);
        Assert.Equal(
            [
                ImportEntries.Copilot("k1", "conv-i", null).Span.SpanId, "f100000000000001", "a100000000000001",
                "a100000000000002", "b100000000000001", "c100000000000001", "c100000000000002", "d100000000000001",
            ],
            rows.Select(row => row.SpanId));
        Assert.Equal(
            ["import:copilot", "otlp", "otlp", "otlp", "otlp", "otlp", "otlp", "otlp"],
            rows.Select(row => row.IngestSource));
        Assert.Equal(
            new ExportSpanRow
            {
                TraceId = ExportSeeds.A,
                SpanId = "a100000000000001",
                OperationName = "chat",
                ConversationId = "conv-a",
                RequestModel = "claude-opus-5.5",
                ModelId = "claude-opus-5.5",
                StartTime = new DateTimeOffset(2026, 11, 21, 14, 0, 0, TimeSpan.Zero),
                EndTime = new DateTimeOffset(2026, 11, 21, 14, 0, 2, TimeSpan.Zero),
                InputTokens = 100_000,
                OutputTokens = 10_000,
                CacheReadInputTokens = 40_000,
                CacheWriteInputTokens = 0,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Absent,
                BillingChannel = BillingChannel.GitHubAiCredits,
                PricingStatus = "priced",
                RatesVersion = "2026-11-23",
                CostUsd = 0.448m,
                SavedUsd = 0m,
                ApiEquivalentUsd = 0.448m,
                ApiEquivalentStatus = "priced",
                ApiRatesVersion = "2026-11-23",
                ApiRateCardSha256 = JsonRateCardCalculator.LoadEmbedded().ApiRateCardSha256,
                RepositorySlug = "swigerb/copilot-ledger",
                RepositoryRoot = Path.Combine(temp.Path, "repo"),
                BranchName = "feature/ledger",
                HeadCommitSha = GitFixture.Sha1,
                PullRequestNumber = 42,
                IngestSource = "otlp",
            },
            rows[2]);
    }

    [Fact]
    public async Task XR2_TheLatestSpansComeNewestFirstWithinTheLimits()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        Assert.Equal(
            ["d100000000000001", "c100000000000002", "c100000000000001"],
            (await snapshot.GetLatestSpansAsync(LedgerFilter.All, 3)).Select(row => row.SpanId));
        Assert.Empty(await snapshot.GetLatestSpansAsync(LedgerFilter.All, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => snapshot.GetLatestSpansAsync(LedgerFilter.All, 100_001));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => snapshot.GetLatestToolCallsAsync(LedgerFilter.All, -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => snapshot.GetSpansAsync(LedgerFilter.All, null, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => snapshot.GetToolCallsAsync(LedgerFilter.All, null, 10_001));
    }

    [Fact]
    public async Task XR3_ToolCallsAreTheEffectiveLinksOfSelectedChains()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        string imported = ImportEntries.ToolLink(ImportEntries.Copilot("k1", "conv-i", null), "call-1", 0).SpanId;

        IReadOnlyList<ExportToolCallRow> first = await snapshot.GetToolCallsAsync(LedgerFilter.All, null, 3);
        ExportCursor after = new(first[^1].StartTime.ToUnixTimeMilliseconds(), first[^1].TraceId, first[^1].SpanId);
        IReadOnlyList<ExportToolCallRow> rows = [.. first, .. await snapshot.GetToolCallsAsync(LedgerFilter.All, after, 3)];

        Assert.Equal([imported, "7100000000000001", "7100000000000002", "7100000000000003"], rows.Select(row => row.SpanId));
        Assert.Equal(["import:copilot", "otlp", "otlp", "otlp"], rows.Select(row => row.IngestSource));
        Assert.Equal(
            new ExportToolCallRow
            {
                TraceId = ExportSeeds.B,
                SpanId = "7100000000000003",
                ConversationId = "conv-b",
                ToolName = "view",
                StartTime = new DateTimeOffset(2026, 11, 21, 15, 0, 10, TimeSpan.Zero),
                StatusCode = 2,
                IngestSource = "otlp",
            },
            rows[3]);
        Assert.Equal([0L, 0L, 1L, null], rows.Select(row => row.ToolExitCode));
        Assert.Equal(4, await snapshot.CountToolCallsAsync(LedgerFilter.All));
        Assert.Equal(
            ["7100000000000003", "7100000000000002"],
            (await snapshot.GetLatestToolCallsAsync(LedgerFilter.All, 2)).Select(row => row.SpanId));
        Assert.Equal(
            ["7100000000000001", "7100000000000002", "7100000000000003"],
            (await snapshot.GetToolCallsAsync(InRepository, null, 10)).Select(row => row.SpanId));
        Assert.Equal(3, await snapshot.CountToolCallsAsync(InRepository));
    }

    [Fact]
    public async Task XR4_CoverageCountsSourcesSupersededRowsImportsRetentionAndCache()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        DateTimeOffset toolHistoryFrom = new(2026, 11, 21, 0, 0, 0, TimeSpan.Zero);
        string i1 = ImportEntries.Copilot("k1", "conv-i", null).Span.TraceId;

        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync())
        {
            ExportCoverageRow all = await snapshot.GetCoverageAsync(LedgerFilter.All, toolHistoryFrom);
            Assert.Equal([new ExportSourceCount("import:copilot", 1), new ExportSourceCount("otlp", 7)], all.SpansBySource);
            Assert.Equal(
                (1L, 0L, 0L, 0L, 2L, 0L),
                (all.SupersededMatchedImportSpans, all.SupersededUnmatchedImportSpans, all.LedgerAmbiguousImportRecords,
                    all.LedgerIncompleteImportLogs, all.SpansBeforeToolHistory, all.ImportedToolCallsWithoutDetail));
            Assert.Equal((1L, 7L, 0L), (all.CacheReportedSpans, all.CacheAbsentSpans, all.CacheLegacyUnknownSpans));
            ExportCoverageRow repository = await snapshot.GetCoverageAsync(InRepository, toolHistoryFrom);
            Assert.Equal([new ExportSourceCount("otlp", 5)], repository.SpansBySource);
            Assert.Equal((0L, 0L), (repository.SupersededMatchedImportSpans, repository.SpansBeforeToolHistory));
        }

        await store.RecordImportedAsync(
            [],
            new ImportSessionCheckpoint
            {
                Source = ImportSourceKind.Copilot,
                LogId = "session-log-1",
                LogSizeBytes = 10,
                LogLastWriteTime = ExportSeeds.GeneratedAt,
                LogSha256 = new string('a', 64),
                Complete = false,
                Recorded = 0,
                Duplicates = 0,
                OverlapSkipped = 0,
                AmbiguousOverlap = 3,
            });
        await LedgerSql.ExecuteAsync(
            store.DatabasePath,
            "UPDATE spans SET cache_read_state = 'legacy_unknown', cache_write_state = 'legacy_unknown' WHERE span_id = @p0;",
            "d100000000000001");
        await LedgerSql.ExecuteAsync(store.DatabasePath, "UPDATE spans SET imported_tool_call_count = 3 WHERE trace_id = @p0;", i1);
        await LedgerSql.ExecuteAsync(store.DatabasePath, "DELETE FROM span_links WHERE trace_id = @p0;", i1);

        await using LedgerExportSnapshot later = await store.OpenExportSnapshotAsync();
        ExportCoverageRow coverage = await later.GetCoverageAsync(LedgerFilter.All, toolHistoryFrom);
        Assert.Equal((3L, 1L), (coverage.LedgerAmbiguousImportRecords, coverage.LedgerIncompleteImportLogs));
        Assert.Equal((1L, 6L, 1L), (coverage.CacheReportedSpans, coverage.CacheAbsentSpans, coverage.CacheLegacyUnknownSpans));
        Assert.Equal(3, coverage.ImportedToolCallsWithoutDetail);
        ExportCoverageRow scoped = await later.GetCoverageAsync(InRepository, toolHistoryFrom);
        Assert.Equal(
            (3L, 1L, 0L),
            (scoped.LedgerAmbiguousImportRecords, scoped.LedgerIncompleteImportLogs, scoped.ImportedToolCallsWithoutDetail));
    }

    [Fact]
    public async Task XR10_UnmatchedSupersessionsCountByTheirOwnColumns()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        DateTimeOffset toolHistoryFrom = new(2026, 11, 21, 0, 0, 0, TimeSpan.Zero);

        ImportedLedgerEntry imported = await ExportSeeds.SupersedeUnmatchedAsync(store);

        Assert.Equal(
            ["superseded|" + ExportSeeds.U],
            await LedgerSql.RowsAsync(
                store.DatabasePath,
                "SELECT imported.accounting_state, live.trace_id FROM spans AS imported JOIN spans AS live ON live.id = imported.superseded_by WHERE imported.trace_id = @p0;",
                imported.Span.TraceId));
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        ExportCoverageRow all = await snapshot.GetCoverageAsync(LedgerFilter.All, toolHistoryFrom);
        Assert.Equal((1L, 1L), (all.SupersededMatchedImportSpans, all.SupersededUnmatchedImportSpans));
        List<long> scoped = [];
        foreach (LedgerFilter filter in new[]
                 {
                     LedgerFilter.All with { ConversationId = "conv-u" },
                     LedgerFilter.All with { ConversationId = "conv-a" },
                     LedgerFilter.All with { From = toolHistoryFrom },
                     InRepository,
                 })
        {
            scoped.Add((await snapshot.GetCoverageAsync(filter, toolHistoryFrom)).SupersededUnmatchedImportSpans);
        }

        Assert.Equal([1L, 0L, 0L, 0L], scoped);
    }

    [Fact]
    public async Task XR5_ProvenanceListsTheRateCardsThatPricedTheSelection()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        ExportRatesProvenance provenance = await snapshot.GetRatesProvenanceAsync(LedgerFilter.All);

        Assert.Equal(["2026-11-23", "test-fixture"], provenance.RatesVersions);
        Assert.Equal(
            [
                new ExportApiRateCard("2026-11-23", JsonRateCardCalculator.LoadEmbedded().ApiRateCardSha256),
                new ExportApiRateCard("test-fixture", JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson).ApiRateCardSha256),
            ],
            provenance.ApiRateCards);
        Assert.Equal(
            ["2026-11-23"],
            (await snapshot.GetRatesProvenanceAsync(new LedgerFilter { Branch = "main" })).RatesVersions);
    }

    [Fact]
    public async Task XR6_BreakerTripsAreTheSelectedChainsNewestFirst()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        IReadOnlyList<BreakerTrip> trips = await snapshot.GetBreakerTripsAsync(LedgerFilter.All);

        Assert.Equal([ExportSeeds.C, ExportSeeds.A], trips.Select(trip => trip.TraceId));
        Assert.Equal([BreakerRule.RetryChurn, BreakerRule.CreditVelocity], trips.Select(trip => trip.Rule));
        Assert.Equal([ExportSeeds.GeneratedAt, null], trips.Select(trip => trip.ResetAt));
        Assert.Equal(
            [ExportSeeds.A],
            (await snapshot.GetBreakerTripsAsync(new LedgerFilter { Branch = "feature/ledger" })).Select(trip => trip.TraceId));
    }

    [Fact]
    public async Task XR7_BudgetsAreTheSelectedKeysAsBudgetListShowsThem()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        BranchBudgetRow budget = Assert.Single(await snapshot.GetBudgetsAsync(LedgerFilter.All));

        Assert.Equal(
            ("swigerb/copilot-ledger", "feature/ledger", 50m, 91.3m, true),
            (budget.Repository, budget.Branch, budget.BudgetAiCredits, budget.SpentAiCredits, budget.Exceeded));
        Assert.Equal(Assert.Single(await store.GetBranchBudgetsAsync("swigerb/copilot-ledger", "feature/ledger")), budget);
        Assert.Empty(await snapshot.GetBudgetsAsync(new LedgerFilter { Branch = "main" }));
    }

    [Fact]
    public async Task XR8_RootsAreTheSelectedRepositoriesWithoutASlug()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        Assert.Equal([ExportSeeds.LocalRoot(temp.Path)], await snapshot.GetRepositoryRootsAsync(LedgerFilter.All));
        Assert.Empty(await snapshot.GetRepositoryRootsAsync(InRepository));
    }

    [Theory]
    [InlineData("repository", 5, 3, 2)]
    [InlineData("branch", 3, 0, 1)]
    [InlineData("pull request", 2, 2, 1)]
    [InlineData("conversation", 2, 0, 1)]
    public async Task XR9_EachFilterSelectsSpansToolCallsAndTripsTogether(string by, int spans, int toolCalls, int trips)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        LedgerFilter filter = by switch
        {
            "repository" => InRepository,
            "branch" => new LedgerFilter { Branch = "main" },
            "pull request" => new LedgerFilter { PullRequestNumber = 42 },
            _ => new LedgerFilter { ConversationId = "conv-c" },
        };

        Assert.Equal(spans, (await snapshot.GetSpansAsync(filter, null, 100)).Count);
        Assert.Equal(toolCalls, (await snapshot.GetToolCallsAsync(filter, null, 100)).Count);
        Assert.Equal(trips, (await snapshot.GetBreakerTripsAsync(filter)).Count);
    }
}
```

New file `tests/CopilotLedger.Tests/TestSupport/ExportSeeds.cs`:

```csharp
using System.Globalization;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.TestSupport;

// The ledger of gh ledger export's tests. Times are UTC; GeneratedAt is 16:30 at ManualTimeProvider.UtcMinus5.
// A1, A2  trace A, conv-a, swigerb/copilot-ledger feature/ledger, pull request 42: claude-opus-5.5 at 11-21 14:00 with
//         40,000 reported cache reads, then an explore subagent's mai-code-2 at 14:01 without cache counters.
// B1      trace B, conv-b, feature/ledger: Foundry BYOK gpt-6.1-sol at 15:00, priced with RateFixtures.ByokTestRatesJson.
// C1, C2  trace C, conv-c, main: a local qwen3.8-27b request at 16:00 and an unpriced gpt-unknown one at 16:05.
// D1      trace D, conv-d: claude-opus-5.5 at 17:00 in LocalRoot(root), a repository without a remote, on main.
// I1      an imported Copilot record (conv-i, 11-20 12:00) whose bash call exited 0.
// I2, J1  an imported record (conv-j, 11-20 12:00) whose bash call exited 1, superseded by J1, its live twin (trace J).
// SupersedeUnmatchedAsync adds U: an imported record that a live span of its session replaces without matching it.
// Links: execute_tool T1 and T2 (trace A, bash, exit 0 at 14:00:30, exit 1 at 14:00:40), T3 (trace B, view, status 2 at
// 15:00:10), T4 (trace E, which has no request, at 15:30), and T5, A's invoke_agent link. Breaker trips: credit_velocity on
// A at 14:02 (open), retry_churn on C at 16:06 (reset), credit_velocity on E (open). Budgets: 50 AI credits on
// swigerb/copilot-ledger feature/ledger and 10 on contoso/api main.
public static class ExportSeeds
{
    public const string A = "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1";
    public const string B = "b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1";
    public const string C = "c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1";
    public const string D = "d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1";
    public const string E = "e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1";
    public const string J = "f1f1f1f1f1f1f1f1f1f1f1f1f1f1f1f1";
    public const string U = "7a7a7a7a7a7a7a7a7a7a7a7a7a7a7a7a";
    public const string ServiceRequestId = "22222222-2222-4222-8222-222222222222";

    public static readonly DateTimeOffset GeneratedAt = new(2026, 11, 21, 21, 30, 0, TimeSpan.Zero);

    // D1's repository.
    public static string LocalRoot(string root) => Path.Combine(root, "private-7c1d", "tools");

    // A ledger at <root>/data/ledger.db, where CliHarness(root) looks, whose clock stays at GeneratedAt; seeded.
    public static async Task<SqliteLedgerStore> CreateAsync(string root)
    {
        SqliteLedgerStore store = new(
            Path.Combine(root, "data", "ledger.db"),
            new ManualTimeProvider(GeneratedAt));
        await store.InitializeAsync();
        await SeedAsync(store, root);
        return store;
    }

    public static async Task SeedAsync(SqliteLedgerStore store, string root)
    {
        JsonRateCardCalculator embedded = JsonRateCardCalculator.LoadEmbedded();
        JsonRateCardCalculator byok = JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson);
        GitWorkspaceContext feature = GitFixture.Feature(root);
        GitWorkspaceContext main = feature with { BranchName = "main" };
        GitWorkspaceContext local = new(LocalRoot(root), Path.Combine(LocalRoot(root), ".git"), "main", GitFixture.Sha2, null, null);

        ImportedLedgerEntry i1 = ImportEntries.Copilot("k1", "conv-i", null);
        ImportedLedgerEntry i2 = ImportEntries.Copilot("k2", "conv-j", ServiceRequestId);
        await store.RecordImportedAsync(
        [
            i1 with { ToolLinks = [ImportEntries.ToolLink(i1, "call-1", 0)] },
            i2 with { ToolLinks = [ImportEntries.ToolLink(i2, "call-2", 1)] },
        ],
            null);

        LedgerEntry j1 = ImportEntries.Live("conv-j", ServiceRequestId);
        (NormalizedGenAiSpan Span, JsonRateCardCalculator Rates, GitWorkspaceContext Git)[] spans =
        [
            (Chat(A, 1, "14:00:00", "claude-opus-5.5", "conv-a", 100_000, 10_000, cacheRead: 40_000), embedded, feature with { PullRequestNumber = 42 }),
            (Chat(A, 2, "14:01:00", "mai-code-2", "conv-a", 200_000, 20_000, agent: "explore"), embedded, feature with { PullRequestNumber = 42 }),
            (Chat(B, 1, "15:00:00", "gpt-6.1-sol", "conv-b", 100_000, 10_000, server: "contoso.openai.azure.com"), byok, feature),
            (Chat(C, 1, "16:00:00", "qwen3.8-27b", "conv-c", 300_000, 9_000, server: "127.0.0.1"), embedded, main),
            (Chat(C, 2, "16:05:00", "gpt-unknown", "conv-c", 1_000, 100), embedded, main),
            (Chat(D, 1, "17:00:00", "claude-opus-5.5", "conv-d", 10_000, 1_000), embedded, local),
        ];
        await store.RecordAsync(
        [
            .. spans.Select(item => new LedgerEntry(item.Span, item.Rates.Calculate(item.Span), item.Git)),
            j1 with { Span = j1.Span with { TraceId = J, SpanId = SpanId(J, 1) } },
        ]);

        await store.RecordSpanLinksAsync(
        [
            Tool(A, 1, "14:00:30", "conv-a", "bash", exitCode: 0),
            Tool(A, 2, "14:00:40", "conv-a", "bash", exitCode: 1),
            Tool(B, 3, "15:00:10", "conv-b", "view", status: 2),
            Tool(E, 4, "15:30:00", "conv-e", "bash", exitCode: 0),
            new GenAiSpanLink
            {
                TraceId = A,
                SpanId = "7100000000000005",
                OperationName = "invoke_agent",
                AgentName = "explore",
                ConversationId = "conv-a",
                StartTime = At("14:00:50"),
            },
        ]);

        await store.RecordBreakerTripAsync(Velocity(A, "conv-a", "14:02:00", feature.RepositoryRoot));
        long? churn = await store.RecordBreakerTripAsync(new NewBreakerTrip
        {
            Rule = BreakerRule.RetryChurn,
            TraceId = C,
            ConversationId = "conv-c",
            Repository = "swigerb/copilot-ledger",
            Branch = "main",
            RepositoryRoot = feature.RepositoryRoot,
            TrippedAt = At("16:06:00"),
            Detail = "5 failed tool calls in a row",
            FailureCount = 5,
        });
        await store.ResetBreakerTripsAsync(new BreakerTripFilter { Id = churn });
        await store.RecordBreakerTripAsync(Velocity(E, null, "15:31:00", feature.RepositoryRoot));
        await store.SetBranchBudgetAsync("swigerb/copilot-ledger", "feature/ledger", 50m);
        await store.SetBranchBudgetAsync("contoso/api", "main", 10m);
    }

    // Records an imported Copilot request (conv-u, 11-20 12:00) without a service request ID, then a live span of its session
    // without one (trace U, 11-21 12:00), which supersedes it without matching it (I3). Returns the imported request.
    public static async Task<ImportedLedgerEntry> SupersedeUnmatchedAsync(SqliteLedgerStore store)
    {
        ImportedLedgerEntry imported = ImportEntries.Copilot("k9", "conv-u", null);
        await store.RecordImportedAsync([imported], null);
        LedgerEntry live = ImportEntries.Live("conv-u", null, ImportEntries.Start.AddDays(1));
        await store.RecordAsync([live with { Span = live.Span with { TraceId = U, SpanId = SpanId(U, 1) } }]);
        return imported;
    }

    // The 16-character span ID of a trace's request or link: the trace's first two characters, zeros, and index.
    public static string SpanId(string trace, int index) =>
        trace[..2] + "000000000000" + index.ToString("D2", CultureInfo.InvariantCulture);

    private static NormalizedGenAiSpan Chat(
        string trace,
        int index,
        string time,
        string model,
        string conversation,
        long input,
        long output,
        long cacheRead = 0,
        string? agent = null,
        string? server = null) =>
        TestSpans.Chat(
            startTime: At(time),
            requestModel: model,
            agentName: agent,
            conversationId: conversation,
            serverAddress: server,
            inputTokens: input,
            outputTokens: output,
            cacheReadInputTokens: cacheRead) with
        {
            TraceId = trace,
            SpanId = SpanId(trace, index),
        };

    private static GenAiSpanLink Tool(
        string trace,
        int index,
        string time,
        string conversation,
        string tool,
        long? exitCode = null,
        int status = 0) => new()
    {
        TraceId = trace,
        SpanId = "71000000000000" + index.ToString("D2", CultureInfo.InvariantCulture),
        OperationName = "execute_tool",
        ConversationId = conversation,
        StartTime = At(time),
        ToolName = tool,
        ToolExitCode = exitCode,
        StatusCode = status,
    };

    private static NewBreakerTrip Velocity(string trace, string? conversation, string time, string root) => new()
    {
        Rule = BreakerRule.CreditVelocity,
        TraceId = trace,
        ConversationId = conversation,
        Repository = "swigerb/copilot-ledger",
        Branch = "feature/ledger",
        RepositoryRoot = root,
        TrippedAt = At(time),
        Detail = "180.00 AI credits in 5 minutes",
        WindowCostUsd = 1.8m,
        WindowMinutes = 5,
    };

    private static DateTimeOffset At(string time) =>
        DateTimeOffset.Parse($"2026-11-21T{time}Z", CultureInfo.InvariantCulture);
}
```

### Tests

- **XR1** Pages of 3 return the 8 effective rows in (start, trace, span) order, without repeats and without I2; A1's row has every field with the fixture's values.
- **XR2** `GetLatestSpansAsync(3)` is D1, C2, C1; a limit of 0 gives none; 100,001 and a page limit of 0 throw `ArgumentOutOfRangeException`.
- **XR3** Tool calls: I1's, T1, T2, and T3 in order, with `import:copilot`, `otlp`, `otlp`, `otlp`; not T4, T5, or I2's; a count of 4; the latest 2 are T3 and T2; in the repository, T1–T3.
- **XR4** Coverage: sources `import:copilot` 1 and `otlp` 7; matched 1 and unmatched 0; ledger-wide 0 and 0 (3 and 1 after a checkpoint with 3 deferred records that isn't complete); 2 requests before 2026-11-21T00:00Z; cache 1 reported, 7 absent, 0 legacy (6 and 1 after marking D1 `legacy_unknown`); imported tool calls without detail 0 (3 after giving I1 a count of 3 and deleting its link).
- **XR5** Provenance: `["2026-11-23", "test-fixture"]` and two API rate cards, ordinal, with the embedded card's and the BYOK fixture's hashes.
- **XR6** Breaker trips: `c1`'s, then `a1`'s; not `e1`'s; with branch `feature/ledger`, only `a1`'s.
- **XR7** Budgets: only `feature/ledger`'s (50, spent 91.3, exceeded); with branch `main`, none.
- **XR8** Roots: `[<root>/private-7c1d/tools]`; with the repository filter, none.
- **XR9** (4 cases) Each filter selects spans, tool calls, and trips together: the repository 5 / 3 / 2; `main` 3 / 0 / 1; pull request 42 2 / 2 / 1; `conv-c` 2 / 0 / 1.
- **XR10** After `SupersedeUnmatchedAsync`, the import is `superseded` and its `superseded_by` names the live span's row; every repository: matched 1, unmatched 1; unmatched by filter: `conv-u` 1, `conv-a` 0, from 2026-11-21T00:00Z 0 (the import started earlier, though its live span didn't), and the repository 0 (the import recorded none).

### Acceptance criteria

- Every export read keeps only effective rows and applies the whole filter; coverage tells matched and unmatched supersessions apart.
- `dotnet test CopilotLedger.slnx` passes **1309** tests (1296 + 13).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 4 specification: `gh ledger export`: Command, Inputs, Header, Summary, and Coverage

This task adds the command and the first part of the page: everything up to the coverage notes. Later tasks add sections to the same writer.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/ExportCommands.cs` | New: the command, its validation, resolution, and writing |
| `src/CopilotLedger.Cli/Export/ExportInputs.cs`, `ExportLimits.cs` | New: the resolved options and the limits |
| `src/CopilotLedger.Cli/Export/ExportReportModel.cs` | New: the model (header, filter, summary, coverage, audit findings) |
| `src/CopilotLedger.Cli/Export/ExportRecords.cs` | New: shown repository keys, folder names, the scope, plurals |
| `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs` | New: builds the model from one snapshot |
| `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs` | New: the page |
| `src/CopilotLedger.Cli/GhLedgerCli.cs` | Registers `export` after `export-pr` |
| `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs` | New `Exact`, the `WriteMoney` text, which `WriteMoney` now uses |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | `Sum` becomes `internal` |
| `tests/CopilotLedger.Tests/TestSupport/ExportFiles.cs` | New: reads the report, blanks its style and script, and parses it as XML |
| `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs` | New: EC1–EC11 |

### Usage and validation

`gh ledger export [--format html] [--out <path>] [--repo <key> | --all-repos] [--branch <name>] [--pr <n>] [--conversation <id>] [--since <period>] [--time-zone <IANA id>] [--include-local-paths] [--max-timeline-spans <n>] [--generated-at <UTC time>] [--force]`; Task 9 adds `--bundle`. Validation errors exit with 1, print the message and `\n` to standard error, and write nothing, not even a ledger:

| Condition | Message |
|---|---|
| `--format` other than `html` | `--format must be html.` |
| `--out` blank | `--out can't be blank.` |
| `--out` not ending in `.html` or `.htm`, ignoring case | `--out must end with .html or .htm.` |
| `--repo` with `--all-repos` | `Use either --repo or --all-repos, not both.` |
| `--pr` below 1 | `--pr must be a positive integer.` |
| `--conversation` blank | `--conversation can't be blank.` |
| `--since` invalid | `Invalid --since value <value>. Use today, all, <N>d (1-3650), or yyyy-MM-dd.` |
| `--time-zone` that `FindTimeZone` can't resolve | `Invalid --time-zone value <value>. Use an IANA time zone ID, such as America/New_York.` |
| `--generated-at` that `TryParseGeneratedAt` rejects | `Invalid --generated-at value <value>. Use a UTC time such as 2026-11-21T21:30:00Z.` |
| `--max-timeline-spans` outside 0–100,000 | `--max-timeline-spans must be from 0 to 100000.` |

Run-time errors exit with 1 and write nothing: `<out> is a directory.`, `<out> already exists. Use --force to replace it.`, and `Could not write <out>: <message>` for an `IOException` or `UnauthorizedAccessException` while writing a temporary file or moving it into place.

### Resolution and output

1. `generatedAt` is `--generated-at`, else the clock, truncated to milliseconds; the zone is `--time-zone` through `FindTimeZone`, else the clock's local zone.
2. `--since` is evaluated at `generatedAt` in that zone; the window is `[from, generatedAt)`.
3. The repository is null with `--all-repos`, else `--repo`, else the working directory's `RepositorySlug ?? RepositoryRoot`, else null (every repository).
4. Check the target, then `InitializeAsync`, `OpenExportSnapshotAsync`, `ExportModelBuilder.BuildAsync`, and `HtmlReportWriter.Write`; write a temporary file beside the target and move it into place.
5. Standard output: `Wrote <file name>: <N> request(s) in <scope> (<period>).`, where the scope is `all repositories` or the shown repository key, then `, branch <name>`, `, pull request #<n>`, and `, conversation <id>` for each filter given, and never a path.

### The page

UTF-8 without a byte order mark or a carriage return, well-formed as XML apart from the contents of `<style>` and `<script>`: `<!DOCTYPE html>`, `<html lang="en">`, `<meta charset="utf-8" />` first, then `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:" />`, the title `CopilotLedger report: <scope> (<period>)`, a header with the filter and the time zone, summary cards whose `title` holds the exact amount, and the coverage section. Coverage states `Requests: complete.` or `Requests: may be incomplete; see the notes.`, then `Tool calls: complete for this window.` or `Tool calls: partial; see the notes.`, then the notes that apply, in order: the sources; imports that live spans recorded too; imports that live spans of their sessions replaced without matching them; deferred imports and unfinished logs, across the whole ledger; requests older than the tool history; imported tool calls without detail; unpriced requests; the reports' API-equivalent footnotes; and requests without cache counters. `Unpriced` and `n/a` are never `$0.00` or `0.0%`.

### Code (copy exactly)

New file `src/CopilotLedger.Cli/Export/ExportCommands.cs`:

```csharp
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using CopilotLedger.Core.Git;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// gh ledger export: an offline HTML report of the ledger, written from one read snapshot.
internal static class ExportCommands
{
    public static Command Create(CliContext context)
    {
        Command command = new("export", "Write an offline HTML cost report, and optionally a bundle of its data.");
        Option<string> format = new("--format")
        {
            Description = "Report format: html.",
            DefaultValueFactory = _ => "html",
        };
        format.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<string>() != "html")
            {
                result.AddError("--format must be html.");
            }
        });
        Option<string> output = new("--out")
        {
            Description = "The report file (.html), relative to the current directory.",
            DefaultValueFactory = _ => ExportLimits.DefaultOut,
        };
        output.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError("--out can't be blank.");
            }
            else if (!value.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
                     !value.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("--out must end with .html or .htm.");
            }
        });
        Option<string?> repository = new("--repo") { Description = "Limit the report to this repository key." };
        Option<bool> allRepositories = new("--all-repos") { Description = "Report every repository." };
        Option<string?> branch = new("--branch") { Description = "Limit the report to this branch, or (none)." };
        Option<int?> pullRequest = new("--pr") { Description = "Limit the report to this pull request number." };
        pullRequest.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is < 1)
            {
                result.AddError("--pr must be a positive integer.");
            }
        });
        Option<string?> conversation = new("--conversation") { Description = "Limit the report to one Copilot session." };
        conversation.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string?>()))
            {
                result.AddError("--conversation can't be blank.");
            }
        });
        Option<string> since = new("--since")
        {
            Description = "Period: today, all, Nd, or yyyy-MM-dd.",
            DefaultValueFactory = _ => "30d",
        };
        since.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string>() ?? string.Empty;
            if (!LedgerPeriod.TryParse(value, context.TimeProvider, out _))
            {
                result.AddError($"Invalid --since value {value}. Use today, all, <N>d (1-3650), or yyyy-MM-dd.");
            }
        });
        Option<string?> timeZone = new("--time-zone")
        {
            Description = "IANA time zone for dates, times, and night shading; default: this computer's.",
        };
        timeZone.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string?>() ?? string.Empty;
            if (context.Platform.FindTimeZone(value) is null)
            {
                result.AddError($"Invalid --time-zone value {value}. Use an IANA time zone ID, such as America/New_York.");
            }
        });
        Option<bool> includeLocalPaths = new("--include-local-paths")
        {
            Description = "Show local repository paths, which are left out by default.",
        };
        Option<int> maxTimelineSpans = new("--max-timeline-spans")
        {
            Description = "Most requests, and tool calls, to draw on the timeline (0 to 100000).",
            DefaultValueFactory = _ => ExportLimits.DefaultMaxTimelineSpans,
        };
        maxTimelineSpans.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 0 or > ExportLimits.MaxTimelineSpans)
            {
                result.AddError("--max-timeline-spans must be from 0 to 100000.");
            }
        });
        Option<string?> generatedAt = new("--generated-at")
        {
            Description = "The generation time to record, in UTC, to reproduce an earlier export.",
        };
        generatedAt.Validators.Add(result =>
        {
            string? value = result.GetValueOrDefault<string?>();
            if (!ExportTime.TryParseGeneratedAt(value, out _))
            {
                result.AddError($"Invalid --generated-at value {value}. Use a UTC time such as 2026-11-21T21:30:00Z.");
            }
        });
        Option<bool> force = new("--force") { Description = "Replace existing files." };
        command.Add(format);
        command.Add(output);
        command.Add(repository);
        command.Add(allRepositories);
        command.Add(branch);
        command.Add(pullRequest);
        command.Add(conversation);
        command.Add(since);
        command.Add(timeZone);
        command.Add(includeLocalPaths);
        command.Add(maxTimelineSpans);
        command.Add(generatedAt);
        command.Add(force);
        command.Validators.Add(result =>
        {
            if (result.GetValue(repository) is not null && result.GetValue(allRepositories))
            {
                result.AddError("Use either --repo or --all-repos, not both.");
            }
        });
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                ExportAsync(
                    context,
                    Resolve(
                        context,
                        result.GetValue(generatedAt),
                        result.GetValue(timeZone),
                        result.GetValue(since)!,
                        result.GetValue(allRepositories) ? null : result.GetValue(repository),
                        result.GetValue(allRepositories),
                        new LedgerFilter
                        {
                            Branch = result.GetValue(branch),
                            PullRequestNumber = result.GetValue(pullRequest),
                            ConversationId = result.GetValue(conversation),
                        },
                        result.GetValue(maxTimelineSpans),
                        result.GetValue(includeLocalPaths)),
                    result.GetValue(output)!,
                    result.GetValue(force),
                    cancellationToken));
        return command;
    }

    // The options with generatedAt, the time zone, the period, and the repository resolved. Without --repo or --all-repos,
    // the working directory's repository (its slug, else its root), or every repository outside one.
    private static ExportInputs Resolve(
        CliContext context,
        string? generatedAtText,
        string? timeZoneId,
        string since,
        string? repository,
        bool allRepositories,
        LedgerFilter keys,
        int maxTimelineSpans,
        bool includeLocalPaths)
    {
        DateTimeOffset generatedAt = generatedAtText is null
            ? ExportTime.TruncateToMilliseconds(context.TimeProvider.GetUtcNow())
            : ExportTime.TryParseGeneratedAt(generatedAtText, out DateTimeOffset parsed)
                ? parsed
                : throw new InvalidOperationException();
        TimeZoneInfo zone = timeZoneId is null
            ? context.TimeProvider.LocalTimeZone
            : context.Platform.FindTimeZone(timeZoneId) ?? throw new InvalidOperationException();
        LedgerPeriod period = LedgerPeriod.TryParse(since, new ExportClock(generatedAt, zone), out LedgerPeriod? value)
            ? value
            : throw new InvalidOperationException();
        if (!allRepositories && repository is null)
        {
            GitWorkspaceContext? git = new FileSystemGitContextResolver().Resolve(context.WorkingDirectory);
            repository = git?.RepositorySlug ?? git?.RepositoryRoot;
        }

        return new ExportInputs
        {
            GeneratedAt = generatedAt,
            TimeZone = zone,
            RequestedTimeZone = timeZoneId,
            Since = since.Trim(),
            Period = period,
            Filter = keys with { From = period.From, To = generatedAt, Repository = repository },
            AllRepositories = repository is null,
            MaxTimelineSpans = maxTimelineSpans,
            IncludeLocalPaths = includeLocalPaths,
        };
    }

    private static async Task<int> ExportAsync(
        CliContext context,
        ExportInputs inputs,
        string output,
        bool force,
        CancellationToken cancellationToken)
    {
        string htmlPath = Path.GetFullPath(output, context.WorkingDirectory);
        if (!CanWrite(context, htmlPath, output, force))
        {
            return 1;
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        ExportReportModel model;
        byte[] html;
        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync(cancellationToken))
        {
            model = await ExportModelBuilder.BuildAsync(snapshot, inputs, cancellationToken);
            html = HtmlReportWriter.Write(model);
        }

        string htmlTemp = TemporaryPath(htmlPath);
        try
        {
            await File.WriteAllBytesAsync(htmlTemp, html, cancellationToken);
            File.Move(htmlTemp, htmlPath, overwrite: force);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine($"Could not write {output}: {ex.Message}");
            return 1;
        }
        finally
        {
            DeleteIfPresent(htmlTemp);
        }

        context.Out.WriteLine(
            $"Wrote {Path.GetFileName(htmlPath)}: {ExportRecords.Plural(model.Summary.Totals.SpanCount, "request", "requests")} in {model.Header.Scope} ({model.Header.Filter.Period}).");
        return 0;
    }

    // False, after writing why, when path is a directory, or a file that --force doesn't allow replacing.
    private static bool CanWrite(CliContext context, string path, string shown, bool force)
    {
        if (Directory.Exists(path))
        {
            context.Error.WriteLine($"{shown} is a directory.");
            return false;
        }

        if (!force && File.Exists(path))
        {
            context.Error.WriteLine($"{shown} already exists. Use --force to replace it.");
            return false;
        }

        return true;
    }

    // The temporary file beside target that a file is written to before it's moved into place.
    private static string TemporaryPath(string target) =>
        $"{target}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.tmp";

    private static void DeleteIfPresent(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // LedgerPeriod's clock for the export: generatedAt, in the export's time zone.
    private sealed class ExportClock(DateTimeOffset utcNow, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
```

New file `src/CopilotLedger.Cli/Export/ExportInputs.cs`:

```csharp
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// The export's options, resolved before the ledger is read.
internal sealed record ExportInputs
{
    public required DateTimeOffset GeneratedAt { get; init; }   // UTC, whole milliseconds
    public required TimeZoneInfo TimeZone { get; init; }
    public string? RequestedTimeZone { get; init; }             // --time-zone as given; null for the machine's zone
    public required string Since { get; init; }                 // --since, trimmed
    public required LedgerPeriod Period { get; init; }          // --since at GeneratedAt in TimeZone
    public required LedgerFilter Filter { get; init; }          // From = Period.From, To = GeneratedAt, and the key filters
    public required bool AllRepositories { get; init; }         // no repository key: --all-repos, or outside a Git repository
    public required int MaxTimelineSpans { get; init; }
    public required bool IncludeLocalPaths { get; init; }
}
```

New file `src/CopilotLedger.Cli/Export/ExportLimits.cs`:

```csharp
namespace CopilotLedger.Cli.Export;

// gh ledger export's fixed limits and defaults.
internal static class ExportLimits
{
    public const int SchemaVersion = 1;                 // of manifest.json, every telemetry file, and ledger-data
    public const string DefaultOut = "copilot-ledger-report.html";
    public const int DefaultMaxTimelineSpans = 20_000;
    public const int MaxTimelineSpans = 100_000;
    public const int PromptChains = 100;
    public const int GrowthChains = 10;
    public const int GrowthTurns = 500;                 // points drawn per chain
    public const int ChartRows = 40;                    // bars per cost or cache chart
    public const int PageSize = 1_000;                  // rows per query when streaming the bundle
}
```

New file `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`:

```csharp
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Storage;
using CopilotLedger.Storage.Import;

namespace CopilotLedger.Cli.Export;

// Reads everything the report shows from one snapshot, so its parts agree with each other and with the reports.
internal static class ExportModelBuilder
{
    public static async Task<ExportReportModel> BuildAsync(
        LedgerExportSnapshot snapshot,
        ExportInputs inputs,
        CancellationToken cancellationToken)
    {
        LedgerFilter filter = inputs.Filter;
        HashSet<string> roots = new(
            await snapshot.GetRepositoryRootsAsync(filter, cancellationToken),
            StringComparer.Ordinal);
        ExportFilter shown = new()
        {
            Repository = filter.Repository is null
                ? null
                : ExportRecords.Repository(filter.Repository, roots, inputs.IncludeLocalPaths),
            AllRepositories = inputs.AllRepositories,
            Branch = filter.Branch,
            PullRequest = filter.PullRequestNumber,
            Conversation = filter.ConversationId,
            Since = inputs.Since,
            Period = inputs.Period.Label,
            FromUtc = inputs.Period.From?.ToUniversalTime(),
            ToUtc = inputs.GeneratedAt,
        };
        ExportRatesProvenance provenance = await snapshot.GetRatesProvenanceAsync(filter, cancellationToken);
        LedgerRollupRow totals = ReportCommands.Sum(
            await snapshot.GetRollupAsync(LedgerGrouping.ChannelModel, filter, cancellationToken));
        DateTimeOffset toolHistoryFrom = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
        return new ExportReportModel
        {
            Header = new ExportHeader
            {
                ToolVersion = GhLedgerCli.Version,
                GeneratedAt = inputs.GeneratedAt,
                Filter = shown,
                TimeZone = inputs.TimeZone,
                RequestedTimeZone = inputs.RequestedTimeZone,
                RatesVersions = provenance.RatesVersions,
                ApiRateCards = provenance.ApiRateCards,
                Scope = ExportRecords.Scope(shown),
                MaxTimelineSpans = inputs.MaxTimelineSpans,
                IncludeLocalPaths = inputs.IncludeLocalPaths,
            },
            Summary = new ExportSummary
            {
                Totals = totals,
                PromptChains = await snapshot.GetPromptCountAsync(filter, cancellationToken),
            },
            Coverage = new ExportCoverage
            {
                Spans = totals.SpanCount,
                SpansBySource = coverage.SpansBySource,
                SupersededMatchedImportSpans = coverage.SupersededMatchedImportSpans,
                SupersededUnmatchedImportSpans = coverage.SupersededUnmatchedImportSpans,
                LedgerAmbiguousImportRecords = coverage.LedgerAmbiguousImportRecords,
                LedgerIncompleteImportLogs = coverage.LedgerIncompleteImportLogs,
                UnpricedSpans = totals.UnpricedSpanCount,
                ApiEquivalentUnpricedSpans = totals.ApiEquivalentUnpricedSpanCount,
                ApiEquivalentEstimatedSpans = totals.ApiEquivalentEstimatedSpanCount,
                CacheReportedSpans = coverage.CacheReportedSpans,
                CacheAbsentSpans = coverage.CacheAbsentSpans,
                CacheLegacyUnknownSpans = coverage.CacheLegacyUnknownSpans,
                ToolHistoryRetentionDays = LedgerImporter.ToolLinkRetention.Days,
                ToolHistoryFromUtc = toolHistoryFrom,
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
            },
            AuditFindings = [],
        };
    }
}
```

New file `src/CopilotLedger.Cli/Export/ExportRecords.cs`:

```csharp
using System.Globalization;
using CopilotLedger.Cli.Reporting;

namespace CopilotLedger.Cli.Export;

// How the export names repositories, scopes, and counts. Local paths are shown as their folder names unless the export
// includes them.
internal static class ExportRecords
{
    // A shown repository key: a local root (one of roots, or one that looks like a path) becomes its folder name unless
    // includeLocalPaths; a slug or (none) stays as it is.
    public static string Repository(string key, IReadOnlySet<string> roots, bool includeLocalPaths) =>
        !includeLocalPaths && (roots.Contains(key) || LooksLikeLocalPath(key)) ? FolderName(key) : key;

    // The last segment after / or \; the path itself when it has none.
    public static string FolderName(string path)
    {
        int separator = path.LastIndexOfAny(['/', '\\']);
        return separator < 0 || separator == path.Length - 1 ? path : path[(separator + 1)..];
    }

    // Starts with / or \, or with a drive letter and a colon.
    public static bool LooksLikeLocalPath(string key) =>
        key.StartsWith('/') || key.StartsWith('\\') || (key.Length >= 2 && char.IsAsciiLetter(key[0]) && key[1] == ':');

    // "<repository, or all repositories>[, branch <b>][, pull request #<n>][, conversation <id>]". A local root shows as
    // its folder name even when the export includes local paths, so standard output never shows a path.
    public static string Scope(ExportFilter filter) =>
        (filter.Repository is null
            ? "all repositories"
            : LooksLikeLocalPath(filter.Repository) ? FolderName(filter.Repository) : filter.Repository)
        + (filter.Branch is null ? string.Empty : $", branch {filter.Branch}")
        + (filter.PullRequest is int number
            ? ", pull request #" + number.ToString(CultureInfo.InvariantCulture)
            : string.Empty)
        + (filter.Conversation is null ? string.Empty : $", conversation {filter.Conversation}");

    // count with its noun: "1 request", "8 requests".
    public static string Plural(long count, string one, string many) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? one : many)}";
}
```

New file `src/CopilotLedger.Cli/Export/ExportReportModel.cs`:

```csharp
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// What the report shows. HtmlReportWriter renders it; the bundle's JSON files serialize it.
internal sealed record ExportReportModel
{
    public required ExportHeader Header { get; init; }
    public required ExportSummary Summary { get; init; }
    public required ExportCoverage Coverage { get; init; }
    public required IReadOnlyList<ExportAuditFinding> AuditFindings { get; init; }      // always empty until gh ledger audit (#65)
}

internal sealed record ExportHeader
{
    public required string ToolVersion { get; init; }                         // GhLedgerCli.Version
    public required DateTimeOffset GeneratedAt { get; init; }
    public required ExportFilter Filter { get; init; }
    public required TimeZoneInfo TimeZone { get; init; }
    public string? RequestedTimeZone { get; init; }
    public required IReadOnlyList<string> RatesVersions { get; init; }
    public required IReadOnlyList<ExportApiRateCard> ApiRateCards { get; init; }
    public required string Scope { get; init; }                                // "swigerb/copilot-ledger, branch feature/ledger", or "all repositories"
    public required int MaxTimelineSpans { get; init; }
    public required bool IncludeLocalPaths { get; init; }
}

internal sealed record ExportFilter
{
    public string? Repository { get; init; }      // the shown repository key; null for every repository
    public required bool AllRepositories { get; init; }
    public string? Branch { get; init; }
    public int? PullRequest { get; init; }
    public string? Conversation { get; init; }
    public required string Since { get; init; }
    public required string Period { get; init; }  // LedgerPeriod.Label: "last 30 days", "today", "since 2026-11-01", "all time"
    public DateTimeOffset? FromUtc { get; init; }
    public required DateTimeOffset ToUtc { get; init; }
}

internal sealed record ExportSummary
{
    public required LedgerRollupRow Totals { get; init; }     // ReportCommands.Sum of the ChannelModel rollup
    public required long PromptChains { get; init; }          // GetPromptCountAsync
}

internal sealed record ExportCoverage
{
    public required long Spans { get; init; }
    public required IReadOnlyList<ExportSourceCount> SpansBySource { get; init; }
    public required long SupersededMatchedImportSpans { get; init; }    // left out: a selected live span recorded the same call
    public required long SupersededUnmatchedImportSpans { get; init; }  // left out: live spans of their sessions replaced them unmatched
    public required long LedgerAmbiguousImportRecords { get; init; }
    public required long LedgerIncompleteImportLogs { get; init; }
    public required long UnpricedSpans { get; init; }
    public required long ApiEquivalentUnpricedSpans { get; init; }
    public required long ApiEquivalentEstimatedSpans { get; init; }
    public required long CacheReportedSpans { get; init; }
    public required long CacheAbsentSpans { get; init; }
    public required long CacheLegacyUnknownSpans { get; init; }
    public required int ToolHistoryRetentionDays { get; init; }        // LedgerImporter.ToolLinkRetention.Days (7)
    public required DateTimeOffset ToolHistoryFromUtc { get; init; }    // GeneratedAt less the retention
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
    // Imports that the ledger couldn't account for (deferred, unfinished, or replaced without a match) may hide requests.
    public bool RequestsComplete =>
        LedgerAmbiguousImportRecords == 0 && LedgerIncompleteImportLogs == 0 && SupersededUnmatchedImportSpans == 0;
    public bool ToolHistoryComplete => SpansBeforeToolHistory == 0 && ImportedToolCallsWithoutDetail == 0;
}

internal sealed record ExportAuditFinding(string Severity, string Title, string Detail);
```

New file `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`:

```csharp
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// The report page: UTF-8 without a byte order mark, "\n" line breaks only, and well-formed XML apart from the contents of
// its style and script elements. Text from the ledger, the options, or a time zone goes through HtmlEncoder.Default.
internal static class HtmlReportWriter
{
    private const string Policy =
        "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:";

    public static byte[] Write(ExportReportModel model)
    {
        ExportHeader header = model.Header;
        StringBuilder html = new();
        Line(html, "<!DOCTYPE html>");
        Line(html, "<html lang=\"en\">");
        Line(html, "<head>");
        Line(html, "<meta charset=\"utf-8\" />");
        Line(html, $"<meta http-equiv=\"Content-Security-Policy\" content=\"{Policy}\" />");
        Line(html, "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        Line(html, $"<meta name=\"generator\" content=\"gh-ledger {Text(header.ToolVersion)}\" />");
        Line(html, $"<title>CopilotLedger report: {Text(header.Scope)} ({Text(header.Filter.Period)})</title>");
        Line(html, "<style>");
        Line(html, "</style>");
        Line(html, "</head>");
        Line(html, "<body>");
        WriteHeader(html, model);
        Line(html, "<main>");
        WriteSummary(html, model);
        WriteCoverage(html, model.Coverage, header.TimeZone);
        WriteAuditFindings(html, model.AuditFindings);
        Line(html, "</main>");
        Line(
            html,
            $"<footer><p>Written by gh-ledger {Text(header.ToolVersion)}. This report holds no prompts, responses, tool arguments or results, or file contents{(header.IncludeLocalPaths ? string.Empty : ", and no local paths")}. Times are in {Text(header.TimeZone.Id)}.</p></footer>");
        Line(html, "</body>");
        Line(html, "</html>");
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(html.ToString());
    }

    // The coverage notes, in order, each only when it applies.
    internal static IEnumerable<string> CoverageNotes(ExportCoverage coverage, TimeZoneInfo zone)
    {
        yield return coverage.Spans == 0
            ? "No requests match this export."
            : $"{ExportRecords.Plural(coverage.Spans, "request", "requests")}: {string.Join(", ", Sources(coverage.SpansBySource))}.";
        long matched = coverage.SupersededMatchedImportSpans;
        if (matched > 0)
        {
            yield return matched == 1
                ? "1 imported request was left out because a live span recorded the same call."
                : $"{LedgerFormat.Count(matched)} imported requests were left out because live spans recorded the same calls.";
        }

        long unmatched = coverage.SupersededUnmatchedImportSpans;
        if (unmatched > 0)
        {
            yield return unmatched == 1
                ? "1 imported request was left out because live spans of its session replaced it without matching it; they may not cover it."
                : $"{LedgerFormat.Count(unmatched)} imported requests were left out because live spans of their sessions replaced them without matching them; they may not cover them all.";
        }

        long ambiguous = coverage.LedgerAmbiguousImportRecords;
        if (ambiguous > 0)
        {
            yield return ambiguous == 1
                ? "Across the whole ledger, gh ledger import deferred 1 request whose session has live spans it couldn't match; it may belong in this report."
                : $"Across the whole ledger, gh ledger import deferred {LedgerFormat.Count(ambiguous)} requests whose sessions have live spans it couldn't match; some may belong in this report.";
        }

        long incomplete = coverage.LedgerIncompleteImportLogs;
        if (incomplete > 0)
        {
            yield return incomplete == 1
                ? "Across the whole ledger, 1 session log wasn't imported completely."
                : $"Across the whole ledger, {LedgerFormat.Count(incomplete)} session logs weren't imported completely.";
        }

        long before = coverage.SpansBeforeToolHistory;
        if (before > 0)
        {
            yield return $"Tool calls are kept for {coverage.ToolHistoryRetentionDays.ToString(CultureInfo.InvariantCulture)} days, so the {ExportRecords.Plural(before, "request", "requests")} before {LedgerFormat.LocalTime(coverage.ToolHistoryFromUtc, zone)} may be missing {(before == 1 ? "its" : "their")} tool calls.";
        }

        long withoutDetail = coverage.ImportedToolCallsWithoutDetail;
        if (withoutDetail > 0)
        {
            yield return withoutDetail == 1
                ? "1 tool call of an imported request is counted without detail."
                : $"{LedgerFormat.Count(withoutDetail)} tool calls of imported requests are counted without detail.";
        }

        long unpriced = coverage.UnpricedSpans;
        if (unpriced > 0)
        {
            yield return unpriced == 1
                ? "1 request has no price, so billed totals leave it out."
                : $"{LedgerFormat.Count(unpriced)} requests have no price, so billed totals leave them out.";
        }

        if (coverage.ApiEquivalentUnpricedSpans > 0)
        {
            yield return $"{LedgerFormat.Count(coverage.ApiEquivalentUnpricedSpans)} of {ExportRecords.Plural(coverage.Spans, "span", "spans")} had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.";
        }

        long estimated = coverage.ApiEquivalentEstimatedSpans;
        if (estimated > 0)
        {
            yield return $"{ExportRecords.Plural(estimated, "priced span", "priced spans")} reported no cache counters, so all of {(estimated == 1 ? "its" : "their")} input is priced as uncached; ~ marks an estimate.";
        }

        long unreported = coverage.CacheAbsentSpans + coverage.CacheLegacyUnknownSpans;
        if (unreported > 0)
        {
            yield return unreported == 1
                ? "1 request reported no cache counters; cache hit ratios leave it out."
                : $"{LedgerFormat.Count(unreported)} requests reported no cache counters; cache hit ratios leave them out.";
        }
    }

    private static void WriteHeader(StringBuilder html, ExportReportModel model)
    {
        ExportHeader header = model.Header;
        ExportFilter filter = header.Filter;
        TimeZoneInfo zone = header.TimeZone;
        Line(html, "<header id=\"report-header\">");
        Line(html, "<h1>CopilotLedger report</h1>");
        Line(html, $"<p class=\"scope\">{Text(header.Scope)} · {Text(filter.Period)}</p>");
        Line(html, "<dl class=\"meta\">");
        Line(
            html,
            $"<dt>Window</dt><dd>{(filter.FromUtc is DateTimeOffset from ? Time(from, zone) : "All time")} to {Time(filter.ToUtc, zone)}</dd>");
        Line(html, $"<dt>Generated</dt><dd>{Time(header.GeneratedAt, zone)} by gh-ledger {Text(header.ToolVersion)}</dd>");
        Line(
            html,
            $"<dt>Time zone</dt><dd>{Text(zone.Id)}{(header.RequestedTimeZone is string requested && requested != zone.Id ? $" (requested {Text(requested)})" : string.Empty)}</dd>");
        if (header.IncludeLocalPaths && filter.Repository is not null)
        {
            Line(html, $"<dt>Repository</dt><dd>{Text(filter.Repository)}</dd>");
        }

        Line(
            html,
            $"<dt>Rates</dt><dd>{(header.RatesVersions.Count == 0 ? "None recorded" : Text(string.Join(", ", header.RatesVersions)))}</dd>");
        Line(
            html,
            "<dt>API list prices</dt><dd>" +
            (header.ApiRateCards.Count == 0
                ? "None recorded"
                : string.Join(
                    ", ",
                    header.ApiRateCards.Select(card =>
                        $"<span title=\"{Text(card.Sha256)}\">{Text(card.RatesVersion)} {Text(card.Sha256[..Math.Min(12, card.Sha256.Length)])}</span>"))) +
            "</dd>");
        Line(html, "</dl>");
        html.Append("<nav aria-label=\"Sections\"><ul>");
        foreach ((string id, string title) in Sections(model))
        {
            html.Append($"<li><a href=\"#{id}\">{title}</a></li>");
        }

        Line(html, "</ul></nav>");
        Line(html, "</header>");
    }

    // The sections present, in page order.
    private static IEnumerable<(string Id, string Title)> Sections(ExportReportModel model)
    {
        yield return ("summary", "Summary");
        yield return ("coverage", "Coverage");
        if (model.AuditFindings.Count > 0)
        {
            yield return ("audit-findings", "Audit findings");
        }
    }

    private static void WriteSummary(StringBuilder html, ExportReportModel model)
    {
        LedgerRollupRow totals = model.Summary.Totals;
        SectionStart(html, "summary", "Summary");
        Line(html, "<ul class=\"cards\">");
        Card(
            html,
            "requests",
            "Requests",
            LedgerFormat.Count(totals.SpanCount),
            totals.SpanCount.ToString(CultureInfo.InvariantCulture),
            $"{LedgerFormat.Count(totals.UnpricedSpanCount)} unpriced");
        Card(
            html,
            "credits",
            "GitHub AI credits",
            LedgerFormat.Credits(totals.AiCredits),
            LedgerFormat.Exact(totals.AiCredits),
            LedgerFormat.Usd(totals.AiCreditsUsd));
        Card(
            html,
            "byok",
            "Foundry BYOK",
            LedgerFormat.Usd(totals.FoundryByokUsd),
            LedgerFormat.Exact(totals.FoundryByokUsd),
            "billed by Microsoft Foundry");
        Card(
            html,
            "api",
            "API equivalent",
            LedgerFormat.ApiEquivalent(totals),
            totals.ApiEquivalentUsd is decimal api ? LedgerFormat.Exact(api) : "Unpriced",
            "a comparison, not billed");
        Card(
            html,
            "local",
            "Local saved",
            LedgerFormat.Usd(totals.LocalSavedUsd),
            LedgerFormat.Exact(totals.LocalSavedUsd),
            "cloud spend avoided");
        Card(
            html,
            "cache",
            "Cache hit",
            LedgerFormat.Percent(totals.CacheHitRatio),
            totals.CacheHitRatio?.ToString(CultureInfo.InvariantCulture) ?? "n/a",
            $"{LedgerFormat.Count(totals.CacheReportedSpanCount)} of {LedgerFormat.Count(totals.SpanCount)} reported");
        Card(
            html,
            "chains",
            "Prompt chains",
            LedgerFormat.Count(model.Summary.PromptChains),
            model.Summary.PromptChains.ToString(CultureInfo.InvariantCulture),
            "prompts sent");
        Line(html, "</ul>");
        Line(html, "</section>");
    }

    private static void WriteCoverage(StringBuilder html, ExportCoverage coverage, TimeZoneInfo zone)
    {
        SectionStart(html, "coverage", "Coverage");
        Line(
            html,
            $"<p id=\"coverage-requests\">{(coverage.RequestsComplete ? "Requests: complete." : "Requests: may be incomplete; see the notes.")}</p>");
        Line(
            html,
            $"<p id=\"coverage-tool-calls\">{(coverage.ToolHistoryComplete ? "Tool calls: complete for this window." : "Tool calls: partial; see the notes.")}</p>");
        Line(html, "<ul id=\"coverage-notes\">");
        foreach (string note in CoverageNotes(coverage, zone))
        {
            Line(html, $"<li>{Text(note)}</li>");
        }

        Line(html, "</ul>");
        Line(html, "</section>");
    }

    private static void WriteAuditFindings(StringBuilder html, IReadOnlyList<ExportAuditFinding> findings)
    {
        if (findings.Count == 0)
        {
            return;
        }

        SectionStart(html, "audit-findings", "Audit findings");
        Line(html, "<table>");
        Line(html, "<caption>Audit findings</caption>");
        Line(html, "<thead><tr><th scope=\"col\">Severity</th><th scope=\"col\">Finding</th><th scope=\"col\">Detail</th></tr></thead>");
        Line(html, "<tbody>");
        foreach (ExportAuditFinding finding in findings)
        {
            Line(html, $"<tr><td>{Text(finding.Severity)}</td><td>{Text(finding.Title)}</td><td>{Text(finding.Detail)}</td></tr>");
        }

        Line(html, "</tbody>");
        Line(html, "</table>");
        Line(html, "</section>");
    }

    // Each source's requests: live first, then Copilot session logs, Codex rollouts, and any other source (ordinal).
    private static IEnumerable<string> Sources(IReadOnlyList<ExportSourceCount> sources) =>
        sources
            .OrderBy(source => source.Source switch
            {
                "otlp" => 0,
                "import:copilot" => 1,
                "import:codex" => 2,
                _ => 3,
            })
            .ThenBy(source => source.Source, StringComparer.Ordinal)
            .Select(source => LedgerFormat.Count(source.Spans) + source.Source switch
            {
                "otlp" => " recorded live",
                "import:copilot" => " imported from Copilot session logs",
                "import:codex" => " imported from Codex rollouts",
                _ => $" from {source.Source}",
            });

    private static void Card(StringBuilder html, string id, string label, string value, string exact, string note) =>
        Line(
            html,
            $"<li class=\"card\" id=\"card-{id}\"><span class=\"card-label\">{label}</span> <span class=\"card-value\" title=\"{Text(exact)}\">{Text(value)}</span> <span class=\"card-note\">{Text(note)}</span></li>");

    private static void SectionStart(StringBuilder html, string id, string title)
    {
        Line(html, $"<section id=\"{id}\" aria-labelledby=\"{id}-heading\">");
        Line(html, $"<h2 id=\"{id}-heading\">{title}</h2>");
    }

    // <time datetime="{UTC, round trip}">{yyyy-MM-dd HH:mm in zone}</time>
    private static string Time(DateTimeOffset value, TimeZoneInfo zone) =>
        $"<time datetime=\"{value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}\">{LedgerFormat.LocalTime(value, zone)}</time>";

    private static string Text(string value) => HtmlEncoder.Default.Encode(value);

    private static void Line(StringBuilder html, string line) => html.Append(line).Append('\n');
}
```

Edit 1 of 2 in `src/CopilotLedger.Cli/GhLedgerCli.cs`, replace:

```csharp
using CopilotLedger.Cli.Breaker;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Import;
using CopilotLedger.Cli.Install;
```

with:

```csharp
using CopilotLedger.Cli.Breaker;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Export;
using CopilotLedger.Cli.Import;
using CopilotLedger.Cli.Install;
```

Edit 2 of 2 in `src/CopilotLedger.Cli/GhLedgerCli.cs`, replace:

```csharp
        }

        root.Add(ReceiptCommands.Create(context));
        root.Add(ImportCommands.Create(context));
        root.Add(McpCommand.Create(context));
```

with:

```csharp
        }

        root.Add(ReceiptCommands.Create(context));
        root.Add(ExportCommands.Create(context));
        root.Add(ImportCommands.Create(context));
        root.Add(McpCommand.Create(context));
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs`, replace:

```csharp
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static void WriteMoney(
        Utf8JsonWriter writer,
```

with:

```csharp
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    // value with every digit it has, as WriteMoney writes it.
    public static string Exact(decimal value) =>
        value.ToString("0.############", CultureInfo.InvariantCulture);

    public static void WriteMoney(
        Utf8JsonWriter writer,
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs`, replace:

```csharp
    {
        writer.WritePropertyName(name);
        writer.WriteRawValue(value.ToString("0.############", CultureInfo.InvariantCulture));
    }

    // The API-equivalent fields that report rows have after WriteTotals.
```

with:

```csharp
    {
        writer.WritePropertyName(name);
        writer.WriteRawValue(Exact(value));
    }

    // The API-equivalent fields that report rows have after WriteTotals.
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
    }

    private static LedgerRollupRow Sum(IReadOnlyList<LedgerRollupRow> rows) =>
        new()
        {
```

with:

```csharp
    }

    internal static LedgerRollupRow Sum(IReadOnlyList<LedgerRollupRow> rows) =>
        new()
        {
```

New file `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`:

```csharp
using System.Xml.Linq;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// gh ledger export: its options, files, standard output, header, summary cards, and coverage notes.
public sealed class ExportCommandTests
{
    private const string Report = "copilot-ledger-report.html";

    [Theory]
    [InlineData("--format|pdf", "--format must be html.")]
    [InlineData("--out|", "--out can't be blank.")]
    [InlineData("--out|report.txt", "--out must end with .html or .htm.")]
    [InlineData("--repo|x|--all-repos", "Use either --repo or --all-repos, not both.")]
    [InlineData("--pr|0", "--pr must be a positive integer.")]
    [InlineData("--conversation| ", "--conversation can't be blank.")]
    [InlineData("--since|0d", "Invalid --since value 0d. Use today, all, <N>d (1-3650), or yyyy-MM-dd.")]
    [InlineData("--time-zone|Mars/Base", "Invalid --time-zone value Mars/Base. Use an IANA time zone ID, such as America/New_York.")]
    [InlineData("--generated-at|2026-11-21T21:30:00", "Invalid --generated-at value 2026-11-21T21:30:00. Use a UTC time such as 2026-11-21T21:30:00Z.")]
    [InlineData("--generated-at|2026-11-21T21:30:00-05:00", "Invalid --generated-at value 2026-11-21T21:30:00-05:00. Use a UTC time such as 2026-11-21T21:30:00Z.")]
    [InlineData("--max-timeline-spans|100001", "--max-timeline-spans must be from 0 to 100000.")]
    [InlineData("--max-timeline-spans|-1", "--max-timeline-spans must be from 0 to 100000.")]
    public async Task EC1_InvalidOptionsWriteNothing(string args, string message)
    {
        using TempDirectory temp = new();
        CliHarness cli = Harness(temp.Path);

        CliRun run = await cli.RunAsync(["export", .. args.Split('|')]);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message + "\n", run.Error);
        Assert.False(File.Exists(Path.Combine(temp.Path, Report)));
        Assert.False(File.Exists(cli.Context.DatabasePath));
    }

    [Fact]
    public async Task EC2_AnExistingReportIsReplacedOnlyWithForce()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        string path = Path.Combine(temp.Path, Report);
        File.WriteAllText(path, "old");

        CliRun refused = await cli.RunAsync("export", "--all-repos");
        CliRun replaced = await cli.RunAsync("export", "--all-repos", "--force");

        Assert.Equal((1, "copilot-ledger-report.html already exists. Use --force to replace it.\n"), (refused.ExitCode, refused.Error));
        Assert.Equal((0, string.Empty), (replaced.ExitCode, replaced.Error));
        Assert.StartsWith("<!DOCTYPE html>\n", File.ReadAllText(path));
    }

    [Fact]
    public async Task EC3_ADirectoryOrAMissingFolderIsAnError()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        Directory.CreateDirectory(Path.Combine(temp.Path, "out.html"));

        CliRun directory = await cli.RunAsync("export", "--all-repos", "--out", "out.html", "--force");
        CliRun missing = await cli.RunAsync("export", "--all-repos", "--out", "missing/report.html");

        Assert.Equal((1, "out.html is a directory.\n"), (directory.ExitCode, directory.Error));
        Assert.Equal(1, missing.ExitCode);
        Assert.StartsWith("Could not write missing/report.html: ", missing.Error);
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task EC4_TheDefaultScopeIsTheCurrentRepository()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        string repo = GitFixture.Create(temp.Path);
        CliHarness cli = Harness(temp.Path);

        CliRun inside = await cli.RunInAsync(repo, "export");
        CliRun outside = await cli.RunAsync("export");
        CliRun all = await cli.RunInAsync(repo, "export", "--all-repos", "--force");

        Assert.Equal(
            (0, "Wrote copilot-ledger-report.html: 5 requests in swigerb/copilot-ledger (last 30 days).\n", string.Empty),
            (inside.ExitCode, inside.Out, inside.Error));
        Assert.Equal("Wrote copilot-ledger-report.html: 8 requests in all repositories (last 30 days).\n", outside.Out);
        Assert.Equal("Wrote copilot-ledger-report.html: 8 requests in all repositories (last 30 days).\n", all.Out);
        Assert.True(File.Exists(Path.Combine(repo, Report)));
        Assert.True(File.Exists(Path.Combine(temp.Path, Report)));
    }

    [Fact]
    public async Task EC5_SinceIsEvaluatedAtGeneratedAtInTheExportTimeZone()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        CliRun run = await cli.RunAsync(
            "export", "--all-repos", "--since", "today", "--generated-at", "2026-11-21T03:00:00Z", "--time-zone", "Test/New_York");

        Assert.Equal("Wrote copilot-ledger-report.html: 2 requests in all repositories (today).\n", run.Out);
        XDocument page = ExportFiles.Load(Path.Combine(temp.Path, Report));
        Assert.Equal("2026-11-20 00:00 to 2026-11-20 22:00", ExportFiles.Meta(page, "Window"));
        Assert.Equal("Test/New_York", ExportFiles.Meta(page, "Time zone"));
        Assert.Equal("2026-11-20 22:00 by gh-ledger 0.4.1", ExportFiles.Meta(page, "Generated"));
    }

    [Fact]
    public async Task EC6_TheCardsShowTheReportsTotals()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(("8", "8", "1 unpriced"), Card(page, "requests"));
        Assert.Equal(("65.95", "65.95", "$0.66"), Card(page, "credits"));
        Assert.Equal(("$0.32", "0.315", "billed by Microsoft Foundry"), Card(page, "byok"));
        Assert.Equal(("~$0.51*", "0.508", "a comparison, not billed"), Card(page, "api"));
        Assert.Equal(("$0.17", "0.1725", "cloud spend avoided"), Card(page, "local"));
        Assert.Equal(("40.0%", "0.4", "1 of 8 reported"), Card(page, "cache"));
        Assert.Equal(("6", "6", "prompts sent"), Card(page, "chains"));
    }

    [Fact]
    public async Task EC7_UnpricedAndUnreportedNeverShowAsZero()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(
            Path.Combine(temp.Path, "data", "ledger.db"),
            new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        await store.RecordAsync([Entry(ExportSeeds.GeneratedAt.AddHours(-1))]);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(("Unpriced", "Unpriced", "a comparison, not billed"), Card(page, "api"));
        Assert.Equal(("n/a", "n/a", "0 of 1 reported"), Card(page, "cache"));
        Assert.DoesNotContain("$0.00", ExportFiles.ById(page, "card-api").Value);
        Assert.DoesNotContain("0.0%", ExportFiles.ById(page, "card-cache").Value);
    }

    [Fact]
    public async Task EC8_CoverageStatesRequestsAndToolCallsSeparately()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        XDocument fixture = await ExportAsync(cli, temp.Path, "--all-repos");
        await store.RecordAsync([Entry(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero))]);
        XDocument older = await ExportAsync(cli, temp.Path, "--all-repos", "--force");
        await store.RecordImportedAsync([], Checkpoint(ambiguous: 2));
        XDocument deferred = await ExportAsync(cli, temp.Path, "--all-repos", "--force");

        Assert.Equal(
            ("Requests: complete.", "Tool calls: complete for this window."),
            (ExportFiles.ById(fixture, "coverage-requests").Value, ExportFiles.ById(fixture, "coverage-tool-calls").Value));
        Assert.Equal(
            [
                "8 requests: 7 recorded live, 1 imported from Copilot session logs.",
                "1 imported request was left out because a live span recorded the same call.",
                "1 request has no price, so billed totals leave it out.",
                "6 of 8 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.",
                "1 priced span reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.",
                "7 requests reported no cache counters; cache hit ratios leave them out.",
            ],
            Notes(fixture));
        Assert.Equal(
            ("Requests: complete.", "Tool calls: partial; see the notes."),
            (ExportFiles.ById(older, "coverage-requests").Value, ExportFiles.ById(older, "coverage-tool-calls").Value));
        Assert.Contains(
            "Tool calls are kept for 7 days, so the 1 request before 2026-11-14 16:30 may be missing its tool calls.",
            Notes(older));
        Assert.Equal("Requests: may be incomplete; see the notes.", ExportFiles.ById(deferred, "coverage-requests").Value);
        Assert.Contains(
            "Across the whole ledger, gh ledger import deferred 2 requests whose sessions have live spans it couldn't match; some may belong in this report.",
            Notes(deferred));
    }

    [Fact]
    public async Task EC9_TheDocumentIsSelfContainedWellFormedAndLfOnly()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        await cli.RunAsync("export", "--all-repos");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        XDocument page = ExportFiles.Parse(html);
        Assert.StartsWith("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\" />\n", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "Content-Security-Policy"));
        Assert.Contains(
            "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:\" />",
            html);
        Assert.Equal("CopilotLedger report: all repositories (last 30 days)", page.Root!.Element("head")!.Element("title")!.Value);
        Assert.Equal("section", ExportFiles.ById(page, "summary").Name.LocalName);
        Assert.Equal("section", ExportFiles.ById(page, "coverage").Name.LocalName);
        Assert.DoesNotContain(page.Descendants(), element => (string?)element.Attribute("id") == "audit-findings");
    }

    [Fact]
    public async Task EC10_LocalPathsAreShownOnlyWhenIncluded()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        string root = ExportSeeds.LocalRoot(temp.Path);

        CliRun included = await cli.RunAsync("export", "--repo", root, "--include-local-paths");
        XDocument page = ExportFiles.Load(Path.Combine(temp.Path, Report));
        CliRun hidden = await cli.RunAsync("export", "--repo", root, "--force");
        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));

        Assert.Equal("Wrote copilot-ledger-report.html: 1 request in tools (last 30 days).\n", included.Out);
        Assert.Equal(included.Out, hidden.Out);
        Assert.Equal(root, ExportFiles.Meta(page, "Repository"));
        Assert.Equal("tools · last 30 days", page.Descendants("p").Single(p => (string?)p.Attribute("class") == "scope").Value);
        Assert.DoesNotContain("private-7c1d", html);
        Assert.DoesNotContain(ExportFiles.Parse(html).Descendants("dt"), dt => dt.Value == "Repository");
    }

    [Fact]
    public async Task EC11_RequestsThatLiveSpansReplacedWithoutAMatchMayBeMissing()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await ExportSeeds.SupersedeUnmatchedAsync(store);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(
            ("Requests: may be incomplete; see the notes.", "Tool calls: complete for this window."),
            (ExportFiles.ById(page, "coverage-requests").Value, ExportFiles.ById(page, "coverage-tool-calls").Value));
        Assert.Equal(
            [
                "9 requests: 8 recorded live, 1 imported from Copilot session logs.",
                "1 imported request was left out because a live span recorded the same call.",
                "1 imported request was left out because live spans of its session replaced it without matching it; they may not cover it.",
                "1 request has no price, so billed totals leave it out.",
                "7 of 9 spans had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.",
                "1 priced span reported no cache counters, so all of its input is priced as uncached; ~ marks an estimate.",
                "8 requests reported no cache counters; cache hit ratios leave them out.",
            ],
            Notes(page));
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
    {
        FakeCliPlatform platform = new();
        platform.TimeZones["Test/New_York"] = TestZones.NewYork;
        return new CliHarness(root, platform, new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));
    }

    // Runs export in root, which must succeed, and loads the report.
    private static async Task<XDocument> ExportAsync(CliHarness cli, string root, params string[] args)
    {
        CliRun run = await cli.RunAsync(["export", .. args]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return ExportFiles.Load(Path.Combine(root, Report));
    }

    private static (string Value, string Title, string Note) Card(XDocument page, string id)
    {
        XElement card = ExportFiles.ById(page, "card-" + id);
        XElement value = card.Elements("span").Single(span => (string?)span.Attribute("class") == "card-value");
        XElement note = card.Elements("span").Single(span => (string?)span.Attribute("class") == "card-note");
        return (value.Value, (string)value.Attribute("title")!, note.Value);
    }

    private static IReadOnlyList<string> Notes(XDocument page) =>
        ExportFiles.ById(page, "coverage-notes").Elements("li").Select(item => item.Value).ToArray();

    // A mai-code-2 request without cache counters at start, which has no API-equivalent price.
    private static LedgerEntry Entry(DateTimeOffset start)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: start,
            requestModel: "mai-code-2",
            conversationId: "conv-x",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a",
            SpanId = "9a00000000000001",
        };
        return new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null);
    }

    private static ImportSessionCheckpoint Checkpoint(long ambiguous) => new()
    {
        Source = ImportSourceKind.Copilot,
        LogId = "session-log-1",
        LogSizeBytes = 10,
        LogLastWriteTime = ExportSeeds.GeneratedAt,
        LogSha256 = new string('a', 64),
        Complete = true,
        Recorded = 0,
        Duplicates = 0,
        OverlapSkipped = 0,
        AmbiguousOverlap = ambiguous,
    };
}
```

New file `tests/CopilotLedger.Tests/TestSupport/ExportFiles.cs`:

```csharp
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CopilotLedger.Tests.TestSupport;

// Reads what gh ledger export wrote.
public static partial class ExportFiles
{
    // The report's text. Fails when it has a byte order mark, a carriage return, or invalid UTF-8.
    public static string ReadReport(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "The report starts with a byte order mark.");
        string html = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        Assert.DoesNotContain('\r', html);
        return html;
    }

    // The page as XML, with the contents of its style and script elements removed. Fails when it isn't well formed.
    public static XDocument Parse(string html)
    {
        using XmlReader reader = XmlReader.Create(
            new StringReader(StyleOrScript().Replace(html, "$1$2")),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        return XDocument.Load(reader);
    }

    // ReadReport, then Parse.
    public static XDocument Load(string path) => Parse(ReadReport(path));

    // The element with this id.
    public static XElement ById(XDocument page, string id) =>
        page.Descendants().Single(element => (string?)element.Attribute("id") == id);

    // The header's description of term.
    public static string Meta(XDocument page, string term) =>
        page.Descendants("dt").Single(dt => dt.Value == term).ElementsAfterSelf("dd").First().Value;

    [GeneratedRegex(@"(<(?:style|script)\b[^>]*>).*?(</(?:style|script)>)", RegexOptions.Singleline)]
    private static partial Regex StyleOrScript();
}
```

### Tests

- **EC1** (12 cases) Each validation message above, with exit code 1, the message and `\n` on standard error, and neither a report nor a ledger.
- **EC2** An existing report: exit code 1 and `copilot-ledger-report.html already exists. Use --force to replace it.\n`, the file unchanged; with `--force`, replaced.
- **EC3** `--out out.html` naming a directory gives `out.html is a directory.\n`; `--out missing/report.html` gives an error starting `Could not write missing/report.html: `; no `.tmp` file is left.
- **EC4** In the fixture's repository: `Wrote copilot-ledger-report.html: 5 requests in swigerb/copilot-ledger (last 30 days).\n`; outside it, `… 8 requests in all repositories (last 30 days).\n`; `--all-repos` inside it, 8.
- **EC5** `--all-repos --since today --generated-at 2026-11-21T03:00:00Z --time-zone Test/New_York`: 2 requests, `(today)`, the window `2026-11-20 00:00 to 2026-11-20 22:00`, and the zone `Test/New_York`.
- **EC6** The cards: `8` and `1 unpriced`; `65.95` and `$0.66`; `$0.32` (title `0.315`); `~$0.51*` (title `0.508`); `$0.17` (title `0.1725`); `40.0%` and `1 of 8 reported`; `6`.
- **EC7** One `mai-code-2` request without cache counters: the API card shows `Unpriced` and the cache card `n/a`, never `$0.00` or `0.0%`.
- **EC8** Coverage of the fixture: `Requests: complete.`, `Tool calls: complete for this window.`, and its six notes; with a request on 2026-11-10, `Tool calls: partial; see the notes.` and `Tool calls are kept for 7 days, so the 1 request before 2026-11-14 16:30 may be missing its tool calls.`; with a checkpoint of 2 deferred records, `Requests: may be incomplete; see the notes.` and the ledger-wide note.
- **EC9** The document: its first lines, one CSP `<meta>` with exactly the issue's policy, the title `CopilotLedger report: all repositories (last 30 days)`, no CR or byte order mark, well-formed, `#summary` and `#coverage`, and no `#audit-findings`.
- **EC10** `--include-local-paths --repo <root>/private-7c1d/tools` shows the full root in the header and `tools` in the scope; without the option, no part of the root is in the page.
- **EC11** After `ExportSeeds.SupersedeUnmatchedAsync`: `Requests: may be incomplete; see the notes.`, `Tool calls: complete for this window.`, and exactly seven notes, the third `1 imported request was left out because live spans of its session replaced it without matching it; they may not cover it.`

### Acceptance criteria

- The page is self-contained and well-formed, with exactly the issue's CSP; invalid options and existing files write nothing.
- `dotnet test CopilotLedger.slnx` passes **1331** tests (1309 + 22).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 5 specification: Tables and the Stylesheet

This task adds the spend tables, the breaker and budget cards, and the embedded stylesheet. Every table can be sorted later by the script, using integer ranks that the server computes from the exact values, so the script never parses money.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/ExportReportModel.cs` | Rollups, prompt chains, breaker trips, and budgets: the storage rows themselves, with shown repository keys |
| `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs` | Reads them from the snapshot |
| `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs` | The tables, the breaker and budget cards, and the `<style>` element |
| `src/CopilotLedger.Cli/Export/ExportAssets.cs` | New: `Css`, the embedded stylesheet with `\n` line endings |
| `src/CopilotLedger.Cli/Export/Assets/report.css` | New: the stylesheet |
| `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj` | Embeds `report.css` with `LogicalName` `CopilotLedger.Cli.Export.Assets.report.css` |
| `tests/CopilotLedger.Tests/Cli/ExportTableTests.cs` | New: EH1–EH7 |

### Contracts

- Tables: rollups by model, agent, branch, and channel and model; the top 100 prompt chains; cache efficiency by model and agent with `status --cache`'s columns; breaker trips; and budgets. Money cells show the reports' display strings and hold the exact amount in `title`; `Unpriced` and `n/a` stay words.
- Each sortable cell's `data-sort` is the row's 0-based position when the rows are ordered by that column: values ascending, with null (Unpriced, n/a) first; strings ordinal; ties in table order.
- The stylesheet: light and dark schemes through custom properties, reduced motion, the model palette `m0`–`m7` (Okabe–Ito), the hatch pattern's classes, and no `@import`, web font, or `url(` other than `url(#hatch)`. Task 7 replaces its four channel colors.

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`, replace:

```xml
    <ProjectReference Include="..\CopilotLedger.Otlp\CopilotLedger.Otlp.csproj" />
  </ItemGroup>
</Project>
```

with:

```xml
    <ProjectReference Include="..\CopilotLedger.Otlp\CopilotLedger.Otlp.csproj" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="Export\Assets\report.css" LogicalName="CopilotLedger.Cli.Export.Assets.report.css" />
  </ItemGroup>
</Project>
```

New file `src/CopilotLedger.Cli/Export/Assets/report.css`:

```
:root {
  color-scheme: light dark;
  --fg: #1f2328;
  --muted: #59636e;
  --bg: #ffffff;
  --panel: #f6f8fa;
  --border: #d1d9e0;
  --accent: #0969da;
  --github: #0072b2;
  --byok: #e69f00;
  --api: #56b4e9;
  --local: #009e73;
  --fail: #d55e00;
  --night: rgba(0, 0, 0, 0.07);
  --window: rgba(9, 105, 218, 0.12);
  font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
  font-size: 15px;
  line-height: 1.45;
}
@media (prefers-color-scheme: dark) {
  :root {
    --fg: #e6edf3;
    --muted: #9198a1;
    --bg: #0d1117;
    --panel: #151b23;
    --border: #3d444d;
    --accent: #4493f8;
    --night: rgba(255, 255, 255, 0.08);
    --window: rgba(68, 147, 248, 0.18);
  }
}
* { box-sizing: border-box; }
[hidden] { display: none !important; }
html { scroll-behavior: smooth; }
body { margin: 0; color: var(--fg); background: var(--bg); }
#report-header, main, footer { max-width: 1200px; margin: 0 auto; padding: 0 16px; }
h1 { font-size: 1.6rem; margin: 24px 0 4px; }
h2 { font-size: 1.25rem; margin: 32px 0 8px; padding-bottom: 4px; border-bottom: 1px solid var(--border); }
h3 { font-size: 1rem; margin: 16px 0 6px; }
a { color: var(--accent); }
.scope { color: var(--muted); margin: 0 0 12px; }
dl.meta { display: grid; grid-template-columns: max-content 1fr; gap: 2px 16px; margin: 0 0 12px; }
dl.meta dt { color: var(--muted); }
dl.meta dd { margin: 0; overflow-wrap: anywhere; }
nav ul { list-style: none; display: flex; flex-wrap: wrap; gap: 4px 16px; padding: 0; margin: 0 0 8px; }
ul.cards { list-style: none; padding: 0; margin: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(170px, 1fr)); gap: 12px; }
li.card { display: flex; flex-direction: column; padding: 10px 12px; border: 1px solid var(--border); border-radius: 8px; background: var(--panel); }
.card-label, .card-note, .note, .empty { color: var(--muted); font-size: 0.85rem; }
.card-value { font-size: 1.4rem; font-weight: 600; font-variant-numeric: tabular-nums; }
#coverage p { margin: 4px 0; font-weight: 600; }
#coverage ul { margin: 6px 0; padding-left: 20px; }
.table-wrap { overflow-x: auto; margin: 8px 0 16px; }
table.data { border-collapse: collapse; font-size: 0.85rem; font-variant-numeric: tabular-nums; }
table.data caption { text-align: left; font-weight: 600; padding: 4px 0; }
table.data th, table.data td { padding: 4px 8px; border-bottom: 1px solid var(--border); text-align: left; white-space: nowrap; }
table.data th { background: var(--panel); }
table.data th button { font: inherit; color: inherit; background: none; border: 0; padding: 0; cursor: pointer; text-decoration: underline dotted; }
table.data th[aria-sort="ascending"] button::after { content: " \25B2"; }
table.data th[aria-sort="descending"] button::after { content: " \25BC"; }
code { font-family: ui-monospace, "Cascadia Mono", Consolas, monospace; font-size: 0.8rem; }
figure.chart { margin: 8px 0 16px; padding: 12px; border: 1px solid var(--border); border-radius: 8px; background: var(--panel); }
figure.chart > svg { display: block; width: 100%; height: auto; }
figcaption { font-weight: 600; margin-bottom: 6px; }
svg text { fill: var(--fg); font-size: 12px; }
.m0 { fill: #e69f00; stroke: #e69f00; background: #e69f00; }
.m1 { fill: #56b4e9; stroke: #56b4e9; background: #56b4e9; }
.m2 { fill: #009e73; stroke: #009e73; background: #009e73; }
.m3 { fill: #f0e442; stroke: #f0e442; background: #f0e442; }
.m4 { fill: #0072b2; stroke: #0072b2; background: #0072b2; }
.m5 { fill: #d55e00; stroke: #d55e00; background: #d55e00; }
.m6 { fill: #cc79a7; stroke: #cc79a7; background: #cc79a7; }
.m7 { fill: #999999; stroke: #999999; background: #999999; }
.seg-github { fill: var(--github); }
.seg-byok { fill: var(--byok); }
.seg-api { fill: var(--api); }
.seg-local { fill: var(--local); }
.hatch-back { fill: var(--panel); }
.hatch-line { stroke: var(--muted); stroke-width: 2; }
.unpriced, .na { stroke: var(--muted); stroke-width: 0.5; }
.span, .tool, .tick, .window { vector-effect: non-scaling-stroke; }
.span { stroke-width: 1; }
.tool { stroke: var(--muted); stroke-width: 1.5; }
.tool.fail { stroke: var(--fail); stroke-width: 2; }
.night { fill: var(--night); }
.density { fill: var(--muted); }
.window { fill: var(--window); stroke: var(--accent); stroke-width: 1; }
.tick { stroke: var(--muted); stroke-width: 1; }
.timeline-grid { display: grid; grid-template-columns: 200px minmax(0, 1fr); align-items: start; }
.timeline-labels { display: block; width: 200px; }
.timeline-viewport { min-width: 0; }
.timeline-viewport > svg { display: block; width: 100%; }
#timeline-plot { touch-action: none; cursor: grab; }
#timeline-overview { touch-action: none; cursor: crosshair; margin-top: 4px; }
.axis-labels { position: relative; height: 18px; font-size: 11px; color: var(--muted); }
.axis-label { position: absolute; top: 2px; transform: translateX(-50%); white-space: nowrap; }
.timeline-controls { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 16px; margin: 8px 0; }
.timeline-controls p { margin: 0; color: var(--muted); font-size: 0.85rem; }
ul.legend { list-style: none; display: flex; flex-wrap: wrap; gap: 4px 12px; padding: 0; margin: 6px 0 0; font-weight: 400; }
.swatch { display: inline-block; width: 10px; height: 10px; margin-right: 4px; border-radius: 2px; }
fieldset.lens-toggle { display: inline-flex; gap: 12px; margin: 8px 0; border: 1px solid var(--border); border-radius: 8px; }
details.chart-table summary { cursor: pointer; color: var(--accent); margin-top: 8px; }
.bar:focus, .sequence:focus { outline: none; }
.bar:focus-visible rect { stroke: var(--fg); stroke-width: 2; }
.sequence:focus-visible polyline { stroke-width: 3; }
[tabindex]:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
.sequence polyline { fill: none; stroke-width: 1.5; }
.sequence circle { stroke: none; }
.s0 { stroke: #0072b2; fill: #0072b2; }
.s1 { stroke: #e69f00; fill: #e69f00; }
.s2 { stroke: #009e73; fill: #009e73; }
.s3 { stroke: #cc79a7; fill: #cc79a7; }
.s4 { stroke: #56b4e9; fill: #56b4e9; }
.s5 { stroke: #d55e00; fill: #d55e00; }
.s6 { stroke: #999999; fill: #999999; }
.s7 { stroke: #f0e442; fill: #f0e442; }
.growth-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 12px; }
.axis { stroke: var(--border); }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
footer { color: var(--muted); font-size: 0.85rem; padding-top: 16px; padding-bottom: 32px; }
@media (prefers-reduced-motion: reduce) {
  html { scroll-behavior: auto; }
  *, *::before, *::after { transition: none !important; animation: none !important; }
}
@media print {
  nav, .timeline-controls, .lens-toggle { display: none !important; }
  body { color: #000; background: #fff; }
  figure.chart { break-inside: avoid; }
}
```

New file `src/CopilotLedger.Cli/Export/ExportAssets.cs`:

```csharp
namespace CopilotLedger.Cli.Export;

// The report's stylesheet, embedded in the assembly, with "\n" line endings whatever the checkout used.
internal static class ExportAssets
{
    public static string Css { get; } = Read("CopilotLedger.Cli.Export.Assets.report.css");

    private static string Read(string name)
    {
        using Stream stream = typeof(ExportAssets).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The resource {name} is missing.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }
}
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
        };
        ExportRatesProvenance provenance = await snapshot.GetRatesProvenanceAsync(filter, cancellationToken);
        LedgerRollupRow totals = ReportCommands.Sum(
            await snapshot.GetRollupAsync(LedgerGrouping.ChannelModel, filter, cancellationToken));
        DateTimeOffset toolHistoryFrom = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
```

with:

```csharp
        };
        ExportRatesProvenance provenance = await snapshot.GetRatesProvenanceAsync(filter, cancellationToken);
        IReadOnlyList<LedgerRollupRow> byChannelModel =
            await snapshot.GetRollupAsync(LedgerGrouping.ChannelModel, filter, cancellationToken);
        LedgerRollupRow totals = ReportCommands.Sum(byChannelModel);
        DateTimeOffset toolHistoryFrom = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
            },
            AuditFindings = [],
        };
    }
}
```

with:

```csharp
            },
            AuditFindings = [],
            ByModel = await snapshot.GetRollupAsync(LedgerGrouping.Model, filter, cancellationToken),
            ByAgent = await snapshot.GetRollupAsync(LedgerGrouping.Agent, filter, cancellationToken),
            ByBranch =
            [
                .. (await snapshot.GetRollupAsync(LedgerGrouping.Branch, filter, cancellationToken))
                    .Select(row => row with { Repository = row.Repository is null ? null : Shown(row.Repository) }),
            ],
            ByChannelModel = byChannelModel,
            CacheByModel = await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Model, filter, cancellationToken),
            CacheByAgent = await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Agent, filter, cancellationToken),
            PromptChains =
            [
                .. (await snapshot.GetTopPromptChainsAsync(filter, ExportLimits.PromptChains, cancellationToken))
                    .Select(row => row with { Repository = Shown(row.Repository) }),
            ],
            BreakerTrips =
            [
                .. (await snapshot.GetBreakerTripsAsync(filter, cancellationToken)).Select(trip => trip with
                {
                    Repository = Shown(trip.Repository),
                    RepositoryRoot = inputs.IncludeLocalPaths ? trip.RepositoryRoot : null,
                }),
            ],
            Budgets =
            [
                .. (await snapshot.GetBudgetsAsync(filter, cancellationToken))
                    .Select(row => row with { Repository = Shown(row.Repository) }),
            ],
        };

        string Shown(string key) => ExportRecords.Repository(key, roots, inputs.IncludeLocalPaths);
    }
}
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
    public required ExportCoverage Coverage { get; init; }
    public required IReadOnlyList<ExportAuditFinding> AuditFindings { get; init; }      // always empty until gh ledger audit (#65)
}

internal sealed record ExportHeader
```

with:

```csharp
    public required ExportCoverage Coverage { get; init; }
    public required IReadOnlyList<ExportAuditFinding> AuditFindings { get; init; }      // always empty until gh ledger audit (#65)
    public required IReadOnlyList<LedgerRollupRow> ByModel { get; init; }               // GetRollupAsync's order
    public required IReadOnlyList<LedgerRollupRow> ByAgent { get; init; }
    public required IReadOnlyList<LedgerRollupRow> ByBranch { get; init; }              // Repository is the shown key
    public required IReadOnlyList<LedgerRollupRow> ByChannelModel { get; init; }
    public required IReadOnlyList<CacheEfficiencyRow> CacheByModel { get; init; }
    public required IReadOnlyList<CacheEfficiencyRow> CacheByAgent { get; init; }
    public required IReadOnlyList<PromptChainRow> PromptChains { get; init; }           // the top ExportLimits.PromptChains; shown keys
    public required IReadOnlyList<BreakerTrip> BreakerTrips { get; init; }              // shown keys; RepositoryRoot only with local paths
    public required IReadOnlyList<BranchBudgetRow> Budgets { get; init; }               // shown keys
}

internal sealed record ExportHeader
```

Edit 1 of 6 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
using System.Text.Encodings.Web;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;
```

with:

```csharp
using System.Text.Encodings.Web;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;
```

Edit 2 of 6 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        Line(html, $"<title>CopilotLedger report: {Text(header.Scope)} ({Text(header.Filter.Period)})</title>");
        Line(html, "<style>");
        Line(html, "</style>");
        Line(html, "</head>");
```

with:

```csharp
        Line(html, $"<title>CopilotLedger report: {Text(header.Scope)} ({Text(header.Filter.Period)})</title>");
        Line(html, "<style>");
        Line(html, ExportAssets.Css.TrimEnd('\n'));
        Line(html, "</style>");
        Line(html, "</head>");
```

Edit 3 of 6 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        WriteSummary(html, model);
        WriteCoverage(html, model.Coverage, header.TimeZone);
        WriteAuditFindings(html, model.AuditFindings);
        Line(html, "</main>");
```

with:

```csharp
        WriteSummary(html, model);
        WriteCoverage(html, model.Coverage, header.TimeZone);
        WriteCache(html, model);
        WritePromptChains(html, model, header.TimeZone);
        WriteRollups(html, model);
        WriteBreaker(html, model, header.TimeZone);
        WriteAuditFindings(html, model.AuditFindings);
        Line(html, "</main>");
```

Edit 4 of 6 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        yield return ("summary", "Summary");
        yield return ("coverage", "Coverage");
        if (model.AuditFindings.Count > 0)
        {
```

with:

```csharp
        yield return ("summary", "Summary");
        yield return ("coverage", "Coverage");
        yield return ("cache", "Cache efficiency");
        yield return ("prompt-chains", "Prompt chains");
        yield return ("rollups", "Spend tables");
        yield return ("breaker", "Circuit breaker and budgets");
        if (model.AuditFindings.Count > 0)
        {
```

Edit 5 of 6 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
            model.Summary.PromptChains.ToString(CultureInfo.InvariantCulture),
            "prompts sent");
        Line(html, "</ul>");
        Line(html, "</section>");
```

with:

```csharp
            model.Summary.PromptChains.ToString(CultureInfo.InvariantCulture),
            "prompts sent");
        int open = model.BreakerTrips.Count(trip => trip.ResetAt is null);
        Card(
            html,
            "breaker",
            "Circuit breaker",
            $"{LedgerFormat.Count(open)} open",
            open.ToString(CultureInfo.InvariantCulture),
            ExportRecords.Plural(model.BreakerTrips.Count, "trip", "trips"));
        int exceeded = model.Budgets.Count(budget => budget.Exceeded);
        Card(
            html,
            "budgets",
            "Budgets",
            model.Budgets.Count == 0
                ? "None"
                : $"{LedgerFormat.Count(exceeded)} of {LedgerFormat.Count(model.Budgets.Count)} exceeded",
            model.Budgets.Count == 0 ? "None" : exceeded.ToString(CultureInfo.InvariantCulture),
            "all-time spend");
        Line(html, "</ul>");
        Line(html, "</section>");
```

Edit 6 of 6 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        Line(html, "</section>");
    }

    private static void WriteAuditFindings(StringBuilder html, IReadOnlyList<ExportAuditFinding> findings)
```

with:

```csharp
        Line(html, "</section>");
    }

    private static void WriteCache(StringBuilder html, ExportReportModel model)
    {
        SectionStart(html, "cache", "Cache efficiency");
        Table(
            html,
            "table-cache-model",
            "Prompt cache by model",
            ["Model", .. CacheHeaders],
            [.. model.CacheByModel.Select(row => (IReadOnlyList<Cell>)[Name(row.Model), .. CacheCells(row)])]);
        Table(
            html,
            "table-cache-agent",
            "Prompt cache by agent",
            ["Agent", .. CacheHeaders],
            [.. model.CacheByAgent.Select(row => (IReadOnlyList<Cell>)[Name(row.Agent), .. CacheCells(row)])]);
        Line(html, "</section>");
    }

    private static void WritePromptChains(StringBuilder html, ExportReportModel model, TimeZoneInfo zone)
    {
        SectionStart(html, "prompt-chains", "Prompt chains");
        Table(
            html,
            "table-prompt-chains",
            $"Top {LedgerFormat.Count(model.PromptChains.Count)} of {LedgerFormat.Count(model.Summary.PromptChains)} prompt chains by billed cost",
            [
                "#", "Started", "Repository", "Branch", "Agents", "Models", "Requests", "Input", "Output", "AI credits",
                "GitHub USD", "BYOK USD", "API equiv USD", "Local saved", "Trace",
            ],
            [
                .. model.PromptChains.Select((row, index) => (IReadOnlyList<Cell>)
                [
                    Count(index + 1),
                    TimeCell(row.StartTime, zone),
                    Name(row.Repository),
                    Name(row.Branch),
                    Name(row.Agents.Count == 0 ? "-" : string.Join(", ", row.Agents)),
                    Name(row.Models.Count == 0 ? "-" : string.Join(", ", row.Models)),
                    Count(row.SpanCount),
                    Count(row.InputTokens),
                    Count(row.OutputTokens),
                    Money(LedgerFormat.Credits(row.AiCredits), row.AiCredits),
                    Money(LedgerFormat.Usd(row.AiCreditsUsd), row.AiCreditsUsd),
                    Money(LedgerFormat.Usd(row.FoundryByokUsd), row.FoundryByokUsd),
                    Money(LedgerFormat.ApiEquivalent(row), row.ApiEquivalentUsd),
                    Money(LedgerFormat.Usd(row.LocalSavedUsd), row.LocalSavedUsd),
                    Code(row.TraceId),
                ]),
            ]);
        Line(html, "</section>");
    }

    private static void WriteRollups(StringBuilder html, ExportReportModel model)
    {
        SectionStart(html, "rollups", "Spend tables");
        Table(
            html,
            "table-by-model",
            "Spend by model",
            ["Model", .. RollupHeaders],
            [.. model.ByModel.Select(row => (IReadOnlyList<Cell>)[Name(row.Model), .. RollupCells(row)])]);
        Table(
            html,
            "table-by-agent",
            "Spend by agent",
            ["Agent", .. RollupHeaders],
            [.. model.ByAgent.Select(row => (IReadOnlyList<Cell>)[Name(row.Agent), .. RollupCells(row)])]);
        Table(
            html,
            "table-by-branch",
            "Spend by branch",
            ["Repository", "Branch", .. RollupHeaders],
            [.. model.ByBranch.Select(row => (IReadOnlyList<Cell>)[Name(row.Repository), Name(row.Branch), .. RollupCells(row)])]);
        Table(
            html,
            "table-by-channel-model",
            "Spend by billing channel and model",
            ["Channel", "Model", .. RollupHeaders],
            [.. model.ByChannelModel.Select(row => (IReadOnlyList<Cell>)[Name(ChannelName(row.Channel)), Name(row.Model), .. RollupCells(row)])]);
        Line(html, "</section>");
    }

    private static void WriteBreaker(StringBuilder html, ExportReportModel model, TimeZoneInfo zone)
    {
        SectionStart(html, "breaker", "Circuit breaker and budgets");
        Table(
            html,
            "table-breaker-trips",
            "Breaker trips of these prompt chains",
            ["Rule", "Tripped", "State", "Repository", "Branch", "Window cost", "Window", "Failures", "Trace"],
            [
                .. model.BreakerTrips.Select(trip => (IReadOnlyList<Cell>)
                [
                    Name(trip.Rule == BreakerRule.CreditVelocity ? "Credit velocity" : "Retry churn"),
                    TimeCell(trip.TrippedAt, zone),
                    trip.ResetAt is DateTimeOffset reset ? new Cell($"Reset {Time(reset, zone)}", reset) : new Cell("Open", null),
                    Name(trip.Repository),
                    Name(trip.Branch),
                    trip.WindowCostUsd is decimal cost ? Money(LedgerFormat.Usd(cost), cost) : new Cell("-", null),
                    trip.WindowMinutes is int minutes ? new Cell($"{LedgerFormat.Count(minutes)} min", (long)minutes) : new Cell("-", null),
                    trip.FailureCount is int failures ? Count(failures) : new Cell("-", null),
                    Code(trip.TraceId),
                ]),
            ]);
        Table(
            html,
            "table-budgets",
            "Budgets of these branches",
            ["Repository", "Branch", "Budget (AI credits)", "Spent, all time (AI credits)", "Status"],
            [
                .. model.Budgets.Select(budget => (IReadOnlyList<Cell>)
                [
                    Name(budget.Repository),
                    Name(budget.Branch),
                    Money(LedgerFormat.Credits(budget.BudgetAiCredits), budget.BudgetAiCredits),
                    Money(LedgerFormat.Credits(budget.SpentAiCredits), budget.SpentAiCredits),
                    Name(budget.Exceeded ? "Exceeded" : "Within budget"),
                ]),
            ]);
        Line(html, "</section>");
    }

    private static readonly string[] RollupHeaders =
        ["Requests", "Unpriced", "AI credits", "GitHub USD", "BYOK USD", "API equiv USD", "Local saved", "Cache hit"];

    private static readonly string[] CacheHeaders =
        ["Requests", "Cache reported", "Input", "Cache read", "Cache write", "Uncached", "Output", "Hit %"];

    private static IReadOnlyList<Cell> RollupCells(LedgerRollupRow row) =>
    [
        Count(row.SpanCount),
        Count(row.UnpricedSpanCount),
        Money(LedgerFormat.Credits(row.AiCredits), row.AiCredits),
        Money(LedgerFormat.Usd(row.AiCreditsUsd), row.AiCreditsUsd),
        Money(LedgerFormat.Usd(row.FoundryByokUsd), row.FoundryByokUsd),
        Money(LedgerFormat.ApiEquivalent(row), row.ApiEquivalentUsd),
        Money(LedgerFormat.Usd(row.LocalSavedUsd), row.LocalSavedUsd),
        Ratio(row.CacheHitRatio),
    ];

    // status --cache's cells: totals over the requests that reported cache usage are n/a when none did, and marked * when
    // only some did.
    private static IReadOnlyList<Cell> CacheCells(CacheEfficiencyRow row) =>
    [
        Count(row.SpanCount),
        Count(row.CacheReportedSpanCount),
        Count(row.InputTokens),
        Observed(row, row.ReportedCacheReadInputTokens),
        Observed(row, row.ReportedCacheWriteInputTokens),
        Observed(row, row.ReportedUncachedInputTokens),
        Count(row.OutputTokens),
        Ratio(row.CacheHitRatio),
    ];

    private static Cell Observed(CacheEfficiencyRow row, long value) =>
        row.CacheReportedSpanCount == 0
            ? new Cell("n/a", null)
            : new Cell(LedgerFormat.Count(value) + (row.CacheUnreportedSpanCount > 0 ? "*" : string.Empty), value);

    private static string ChannelName(BillingChannel? channel) => channel switch
    {
        BillingChannel.GitHubAiCredits => "GitHub AI credits",
        BillingChannel.FoundryByok => "Foundry BYOK",
        BillingChannel.Local => "Local",
        _ => "Unpriced",
    };

    // A sortable table. Each cell's data-sort is its row's 0-based position when the rows are ordered by that column: values
    // ascending with null first, strings ordinal, ties in table order. A table without rows has one "None." row instead.
    private static void Table(
        StringBuilder html,
        string id,
        string caption,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<Cell>> rows)
    {
        Line(html, $"<div class=\"table-wrap\"><table class=\"{(rows.Count == 0 ? "data" : "data sortable")}\" id=\"{id}\">");
        Line(html, $"<caption>{caption}</caption>");
        Line(html, "<thead><tr>" + string.Concat(headers.Select(header => $"<th scope=\"col\">{header}</th>")) + "</tr></thead>");
        Line(html, "<tbody>");
        if (rows.Count == 0)
        {
            Line(html, $"<tr><td colspan=\"{headers.Count.ToString(CultureInfo.InvariantCulture)}\">None.</td></tr>");
        }

        int[][] ranks = [.. Enumerable.Range(0, headers.Count).Select(column => Ranks(rows, column))];
        for (int row = 0; row < rows.Count; row++)
        {
            html.Append("<tr>");
            for (int column = 0; column < headers.Count; column++)
            {
                Cell cell = rows[row][column];
                html.Append($"<td data-sort=\"{ranks[column][row].ToString(CultureInfo.InvariantCulture)}\"");
                html.Append(cell.Title is null ? ">" : $" title=\"{Text(cell.Title)}\">");
                html.Append(cell.Html).Append("</td>");
            }

            Line(html, "</tr>");
        }

        Line(html, "</tbody>");
        Line(html, "</table></div>");
    }

    private static int[] Ranks(IReadOnlyList<IReadOnlyList<Cell>> rows, int column)
    {
        int[] order = [.. Enumerable.Range(0, rows.Count).Order(Comparer<int>.Create((left, right) =>
        {
            int compared = CompareKeys(rows[left][column].Key, rows[right][column].Key);
            return compared != 0 ? compared : left.CompareTo(right);
        }))];
        int[] ranks = new int[rows.Count];
        for (int position = 0; position < order.Length; position++)
        {
            ranks[order[position]] = position;
        }

        return ranks;
    }

    private static int CompareKeys(object? left, object? right) => (left, right) switch
    {
        (null, null) => 0,
        (null, _) => -1,
        (_, null) => 1,
        (string text, string other) => string.CompareOrdinal(text, other),
        (IComparable value, _) => value.CompareTo(right),
        _ => 0,
    };

    // A cell: its HTML, which the caller encoded; the value it sorts by; and the exact value for its title, if any.
    private sealed record Cell(string Html, object? Key, string? Title = null);

    private static Cell Name(string? value) => new(Text(value ?? "(none)"), value ?? "(none)");

    private static Cell Count(long value) => new(LedgerFormat.Count(value), value);

    // A money amount as shown, titled with its exact value, or Unpriced when there's none.
    private static Cell Money(string shown, decimal? exact) =>
        new(Text(shown), exact, exact is decimal value ? LedgerFormat.Exact(value) : "Unpriced");

    private static Cell Ratio(decimal? ratio) =>
        new(Text(LedgerFormat.Percent(ratio)), ratio, ratio?.ToString(CultureInfo.InvariantCulture) ?? "n/a");

    private static Cell TimeCell(DateTimeOffset value, TimeZoneInfo zone) => new(Time(value, zone), value);

    private static Cell Code(string value) => new($"<code>{Text(value)}</code>", value);

    private static void WriteAuditFindings(StringBuilder html, IReadOnlyList<ExportAuditFinding> findings)
```

New file `tests/CopilotLedger.Tests/Cli/ExportTableTests.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using CopilotLedger.Cli;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The export's tables, the breaker and budget cards, sorting keys, and stylesheet.
public sealed class ExportTableTests
{
    private const string Report = "copilot-ledger-report.html";

    [Fact]
    public async Task EH1_TheRollupTablesMatchTheReports()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        XDocument page = await ExportAsync(cli, temp.Path, "--all-repos");
        using JsonDocument branch = JsonDocument.Parse((await cli.RunAsync("branch", "--all-repos", "--json")).Out);
        using JsonDocument agents = JsonDocument.Parse((await cli.RunAsync("agents", "--json")).Out);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        Assert.Equal(
            [
                "swigerb/copilot-ledger|feature/ledger|3|0.598|0.315",
                "tools|main|1|0.06|0",
                "(none)|(none)|2|0.0015|0",
                "swigerb/copilot-ledger|main|2|0|0",
            ],
            Rows(page, "table-by-branch").Select(row => $"{row[0].Value}|{row[1].Value}|{row[2].Value}|{Title(row[5])}|{Title(row[6])}"));
        Assert.Equal(
            branch.RootElement.GetProperty("rows").EnumerateArray().Select(row =>
                $"{Shown(row.GetProperty("repository").GetString()!)}|{row.GetProperty("branch").GetString()}|{row.GetProperty("spans")}|{row.GetProperty("aiCreditsUsd").GetRawText()}|{row.GetProperty("foundryByokUsd").GetRawText()}"),
            Rows(page, "table-by-branch").Select(row => $"{row[0].Value}|{row[1].Value}|{row[2].Value}|{Title(row[5])}|{Title(row[6])}"));
        Assert.Equal(
            agents.RootElement.GetProperty("rows").EnumerateArray().Select(row =>
                $"{row.GetProperty("agent").GetString() ?? "(none)"}|{row.GetProperty("spans")}|{row.GetProperty("aiCreditsUsd").GetRawText()}|{row.GetProperty("localSavedUsd").GetRawText()}"),
            Rows(page, "table-by-agent").Select(row => $"{row[0].Value}|{row[1].Value}|{Title(row[4])}|{Title(row[7])}"));
        Assert.Equal(
            (await snapshot.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All)).Select(row =>
                $"{row.Model}|{row.SpanCount}|{Exact(row.AiCreditsUsd)}|{(row.ApiEquivalentUsd is decimal api ? Exact(api) : "Unpriced")}"),
            Rows(page, "table-by-model").Select(row => $"{row[0].Value}|{row[1].Value}|{Title(row[4])}|{Title(row[6])}"));
        Assert.Equal(
            [
                "GitHub AI credits|claude-opus-5.5|2|0.508",
                "Foundry BYOK|gpt-6.1-sol|1|0",
                "GitHub AI credits|mai-code-2|3|0.1515",
                "Local|qwen3.8-27b|1|0",
                "GitHub AI credits|gpt-unknown|1|0",
            ],
            Rows(page, "table-by-channel-model").Select(row => $"{row[0].Value}|{row[1].Value}|{row[2].Value}|{Title(row[5])}"));

        string Shown(string key) => key == ExportSeeds.LocalRoot(temp.Path) ? "tools" : key;
    }

    [Fact]
    public async Task EH2_PromptChainsAreTheTopChainsByBilledCost()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        XElement table = ExportFiles.ById(page, "table-prompt-chains");
        Assert.Equal("Top 6 of 6 prompt chains by billed cost", table.Element("caption")!.Value);
        Assert.Equal(
            [
                "1", "2026-11-21 09:00", "swigerb/copilot-ledger", "feature/ledger", "explore", "claude-opus-5.5, mai-code-2", "2",
                "300,000", "30,000", "59.80", "$0.60", "$0.00", "$0.45*", "$0.00", ExportSeeds.A,
            ],
            Rows(page, "table-prompt-chains")[0].Select(cell => cell.Value));
        Assert.Equal(
            [ExportSeeds.A, ExportSeeds.B, ExportSeeds.D, ImportEntries.Copilot("k1", "conv-i", null).Span.TraceId, ExportSeeds.J, ExportSeeds.C],
            Rows(page, "table-prompt-chains").Select(row => row[14].Value));
    }

    [Fact]
    public async Task EH3_CacheTablesShowNotAvailableAndPartialTotals()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        IReadOnlyList<string> opus = Row(page, "table-cache-model", "claude-opus-5.5");
        IReadOnlyList<string> mai = Row(page, "table-cache-model", "mai-code-2");
        Assert.Equal(["claude-opus-5.5", "2", "1", "110,000", "40,000*", "0*", "60,000*", "11,000", "40.0%"], opus);
        Assert.Equal(["mai-code-2", "3", "0", "202,000", "n/a", "n/a", "n/a", "20,200", "n/a"], mai);
        Assert.Equal(["(none)", "explore"], Rows(page, "table-cache-agent").Select(row => row[0].Value));
    }

    [Fact]
    public async Task EH4_BreakerTripsAndBudgetsHaveTablesAndCards()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(
            [
                "Retry churn|2026-11-21 11:06|Reset 2026-11-21 16:30|swigerb/copilot-ledger|main|-|-|5|" + ExportSeeds.C,
                "Credit velocity|2026-11-21 09:02|Open|swigerb/copilot-ledger|feature/ledger|$1.80|5 min|-|" + ExportSeeds.A,
            ],
            Rows(page, "table-breaker-trips").Select(row => string.Join('|', row.Select(cell => cell.Value))));
        Assert.Equal(
            ["swigerb/copilot-ledger|feature/ledger|50.00|91.30|Exceeded"],
            Rows(page, "table-budgets").Select(row => string.Join('|', row.Select(cell => cell.Value))));
        Assert.Equal(["91.3"], Rows(page, "table-budgets").Select(row => Title(row[3])));
        Assert.Equal("Circuit breaker 1 open 2 trips", ExportFiles.ById(page, "card-breaker").Value);
        Assert.Equal("Budgets 1 of 1 exceeded all-time spend", ExportFiles.ById(page, "card-budgets").Value);
    }

    [Fact]
    public async Task EH5_SortKeysAreRanksOfTheExactValues()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        foreach (XElement table in page.Descendants("table").Where(table => (string?)table.Attribute("class") == "data sortable"))
        {
            IReadOnlyList<IReadOnlyList<XElement>> rows = Rows(page, (string)table.Attribute("id")!);
            for (int column = 0; column < rows[0].Count; column++)
            {
                Assert.Equal(
                    Enumerable.Range(0, rows.Count),
                    rows.Select(row => int.Parse((string)row[column].Attribute("data-sort")!, NumberStyles.None, CultureInfo.InvariantCulture)).Order());
            }
        }

        IReadOnlyList<IReadOnlyList<XElement>> byModel = Rows(page, "table-by-model");
        Assert.Equal(
            ["0", "0", "0", "0.1515", "0.508"],
            byModel.OrderBy(row => Rank(row[4])).Select(row => Title(row[4])));
        Assert.Equal(
            ["Unpriced", "Unpriced", "Unpriced", "Unpriced", "0.508"],
            byModel.OrderBy(row => Rank(row[6])).Select(row => Title(row[6])));
        Assert.Equal(
            ["claude-opus-5.5", "gpt-6.1-sol", "gpt-unknown", "mai-code-2", "qwen3.8-27b"],
            byModel.OrderBy(row => Rank(row[0])).Select(row => row[0].Value));
    }

    [Fact]
    public async Task EH6_TheStyleElementHoldsTheStylesheetOnce()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");
        using Stream resource = typeof(GhLedgerCli).Assembly.GetManifestResourceStream("CopilotLedger.Cli.Export.Assets.report.css")!;
        using StreamReader reader = new(resource);
        string css = reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd('\n');

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));

        Assert.Contains("<style>\n" + css + "\n</style>\n", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<style"));
        Assert.True(css.Length > 1_000);
        Assert.DoesNotContain("</style", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("url(", css.Replace("url(#hatch)", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EH7_LocalRootsAreFolderNamesUnlessIncluded()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        string root = ExportSeeds.LocalRoot(temp.Path);

        XDocument hidden = await ExportAsync(cli, temp.Path, "--all-repos");
        string hiddenHtml = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        XDocument included = await ExportAsync(cli, temp.Path, "--all-repos", "--include-local-paths", "--force");

        Assert.Contains("tools", Rows(hidden, "table-by-branch").Select(row => row[0].Value));
        Assert.DoesNotContain("private-7c1d", hiddenHtml);
        Assert.Contains(root, Rows(included, "table-by-branch").Select(row => row[0].Value));
        Assert.Contains(root, Rows(included, "table-prompt-chains").Select(row => row[2].Value));
    }

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

    private static async Task<XDocument> ExportAsync(CliHarness cli, string root, params string[] args)
    {
        CliRun run = await cli.RunAsync(["export", .. args]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return ExportFiles.Load(Path.Combine(root, Report));
    }

    // The table's body rows, each as its cells.
    private static IReadOnlyList<IReadOnlyList<XElement>> Rows(XDocument page, string id) =>
        [.. ExportFiles.ById(page, id).Element("tbody")!.Elements("tr").Select(row => (IReadOnlyList<XElement>)[.. row.Elements("td")])];

    // The texts of the table's row whose first cell is key.
    private static IReadOnlyList<string> Row(XDocument page, string id, string key) =>
        [.. Rows(page, id).Single(row => row[0].Value == key).Select(cell => cell.Value)];

    private static string Title(XElement cell) => (string)cell.Attribute("title")!;

    private static int Rank(XElement cell) =>
        int.Parse((string)cell.Attribute("data-sort")!, NumberStyles.None, CultureInfo.InvariantCulture);

    private static string Exact(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);
}
```

### Tests

- **EH1** The four rollup tables equal `branch --all-repos --json`, `agents --json`, and the snapshot's model and channel rollups: keys, counts, and exact money titles.
- **EH2** Prompt chains: the caption `Top 6 of 6 prompt chains by billed cost`, and row 1: `1`, `2026-11-21 09:00`, `swigerb/copilot-ledger`, `feature/ledger`, `explore`, `claude-opus-5.5, mai-code-2`, `2`, `300,000`, `30,000`, `59.80`, `$0.60`, `$0.00`, `$0.45*`, `$0.00`, and the trace.
- **EH3** Cache tables: `claude-opus-5.5` at `40.0%` with 1 of 2 reported and `*` totals; `mai-code-2` `n/a`.
- **EH4** The breaker and budget tables and their cards: `1 open` of `2 trips`, and `1 of 1 exceeded` (`50.00`, `91.30`, `Exceeded`).
- **EH5** Every `data-sort` is an integer; `GitHub USD` ranks follow the exact amounts, and `API equiv USD` ranks `Unpriced` first.
- **EH6** The `<style>` element holds the stylesheet once, LF only, without `</style`, `@import`, or a `url(` other than `url(#hatch)`.
- **EH7** By default the by-branch table shows `tools` and the page has no `private-7c1d`; with `--include-local-paths`, the full root.

### Acceptance criteria

- The tables equal the reports, and sort keys are ranks, never money parsed in the browser.
- `dotnet test CopilotLedger.slnx` passes **1338** tests (1331 + 7).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 6 specification: The Timeline and the `ledger-data` Block

This task draws the timeline on the server, adds its table, and writes the one JSON block the script will read.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/TimelineLayout.cs` | New: rows, bars, ticks, night bands, and the axis |
| `src/CopilotLedger.Cli/Export/ExportSvg.cs` | New: the timeline's SVG |
| `src/CopilotLedger.Cli/Export/ExportJson.cs` | New: the JSON options and the filter, time zone, and coverage writers, and `LedgerData` |
| `src/CopilotLedger.Cli/Export/ExportReportModel.cs` | The span and tool-call records, the timeline, coverage's timeline fields, and its warnings |
| `src/CopilotLedger.Cli/Export/ExportRecords.cs` | `Span`, `ToolCall`, `SpanRepository`, and `TimelineTruncation` |
| `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs` | The latest requests and tool calls, and the timeline |
| `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs` | The timeline section and the `ledger-data` block |
| `src/CopilotLedger.Cli/Export/ExportCommands.cs` | The standard-output line when the timeline leaves requests out |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | `StateName` becomes `internal`, for the span records' cache states |
| `tests/CopilotLedger.Tests/TestSupport/ExportFiles.cs` | `LedgerData`, which parses the one `ledger-data` block |
| `tests/CopilotLedger.Tests/Cli/ExportTimelineTests.cs` | New: TL1–TL10 |

### Algorithms

- **Drawn glyphs.** The latest `--max-timeline-spans` requests and, separately, the latest `--max-timeline-spans` tool calls. The axis runs from the earliest start to the latest end or tool start, at least an hour; `x = round((t − t0) × 1000 / (t1 − t0), 3)`.
- **Rows.** A request's row is its conversation; a tool call's is the conversation of its trace's drawn requests, else its own, else none. Rows are ordered by their first glyph, then ID; requests without a conversation share a row after them. With more than 200 conversations, the 199 with the most glyphs keep rows and the rest share the last. Each row is labeled `<n> · <MM-dd HH:mm>`, its number and its first glyph's local time, or `<n> · no session`, or `<n> · <N> other sessions`; the whole ID is in the label's `<title>` and the table, whose first column is `#`.
- **Colors.** A bar's color is its model's position among the model rollup's first 7 rows (`m0`–`m6`), else `m7`, other models.
- **Ticks.** Local midnights (level 0), 06:00/12:00/18:00 within 62 days (1), other hours within 7 days (2), and 10-minute marks within a day (3); at most 12 labels show, of the finest level that fits.
- **`ledger-data`.** `schemaVersion`, `generatedAtUtc`, `toolVersion`, `filter`, `timeZone` (requested, ID, night hours, and night bands in UTC), `timeline` (`fromUtc`, `toUtc`, `width`, `ticks`), and `coverage`. It's written with the default encoder, which escapes `<`, `>`, and `&`, so it can't end its `<script>` element. Coverage: `requestsComplete`; `warnings`, ordinal codes; the counts, including `supersededMatchedImportSpans` and `supersededUnmatchedImportSpans`; `toolHistory`; and `timeline`.

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        context.Out.WriteLine(
            $"Wrote {Path.GetFileName(htmlPath)}: {ExportRecords.Plural(model.Summary.Totals.SpanCount, "request", "requests")} in {model.Header.Scope} ({model.Header.Filter.Period}).");
        return 0;
    }
```

with:

```csharp
        context.Out.WriteLine(
            $"Wrote {Path.GetFileName(htmlPath)}: {ExportRecords.Plural(model.Summary.Totals.SpanCount, "request", "requests")} in {model.Header.Scope} ({model.Header.Filter.Period}).");
        if (model.Coverage.TimelineTruncated)
        {
            context.Out.WriteLine(ExportRecords.TimelineTruncation(model.Coverage));
        }

        return 0;
    }
```

New file `src/CopilotLedger.Cli/Export/ExportJson.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// The export's JSON: indented with "\n" line breaks and the default encoder, which escapes <, >, and & so the JSON can sit
// inside a script element. Property order is part of the contract.
internal static class ExportJson
{
    public static readonly JsonWriterOptions Options = new() { Indented = true, NewLine = "\n" };

    // The report's ledger-data block: what the script needs, and what a reader of the page may want to check.
    public static byte[] LedgerData(ExportReportModel model)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, Options))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
            LedgerFormat.WriteTime(writer, "generatedAtUtc", model.Header.GeneratedAt);
            writer.WriteString("toolVersion", model.Header.ToolVersion);
            WriteFilter(writer, model.Header.Filter);
            WriteTimeZone(writer, model);
            ExportTimeline timeline = model.Timeline;
            if (timeline.IsEmpty)
            {
                writer.WriteNull("timeline");
            }
            else
            {
                writer.WriteStartObject("timeline");
                LedgerFormat.WriteTime(writer, "fromUtc", DateTimeOffset.FromUnixTimeMilliseconds(timeline.FromUnixMs));
                LedgerFormat.WriteTime(writer, "toUtc", DateTimeOffset.FromUnixTimeMilliseconds(timeline.ToUnixMs));
                writer.WriteNumber("width", TimelineLayout.Width);
                writer.WriteStartArray("ticks");
                foreach (ExportAxisTick tick in timeline.Ticks)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("x");
                    writer.WriteRawValue(Number(tick.X));
                    writer.WriteNumber("level", tick.Level);
                    writer.WriteString("label", tick.Label);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            WriteCoverage(writer, model.Coverage);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    // "filter": the selection, with the shown repository key.
    public static void WriteFilter(Utf8JsonWriter writer, ExportFilter filter)
    {
        writer.WriteStartObject("filter");
        LedgerFormat.WriteNullable(writer, "repo", filter.Repository);
        writer.WriteBoolean("allRepositories", filter.AllRepositories);
        LedgerFormat.WriteNullable(writer, "branch", filter.Branch);
        LedgerFormat.WriteNullable(writer, "pr", filter.PullRequest);
        LedgerFormat.WriteNullable(writer, "conversation", filter.Conversation);
        writer.WriteString("since", filter.Since);
        writer.WriteString("period", filter.Period);
        LedgerFormat.WriteTime(writer, "fromUtc", filter.FromUtc);
        LedgerFormat.WriteTime(writer, "toUtc", filter.ToUtc);
        writer.WriteEndObject();
    }

    // "timeZone": the requested and resolved zone, the night hours, and the timeline's nights in UTC.
    public static void WriteTimeZone(Utf8JsonWriter writer, ExportReportModel model)
    {
        writer.WriteStartObject("timeZone");
        LedgerFormat.WriteNullable(writer, "requested", model.Header.RequestedTimeZone);
        writer.WriteString("id", model.Header.TimeZone.Id);
        writer.WriteString("nightStartLocal", ExportTime.NightStart.ToString("HH:mm", CultureInfo.InvariantCulture));
        writer.WriteString("nightEndLocal", ExportTime.NightEnd.ToString("HH:mm", CultureInfo.InvariantCulture));
        writer.WriteStartArray("nightBandsUtc");
        foreach (UtcBand band in model.Timeline.NightBands)
        {
            writer.WriteStartObject();
            LedgerFormat.WriteTime(writer, "fromUtc", band.FromUtc);
            LedgerFormat.WriteTime(writer, "toUtc", band.ToUtc);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    // "coverage": requests, tool history, and the timeline, each with its own completeness.
    public static void WriteCoverage(Utf8JsonWriter writer, ExportCoverage coverage)
    {
        writer.WriteStartObject("coverage");
        writer.WriteBoolean("requestsComplete", coverage.RequestsComplete);
        writer.WriteStartArray("warnings");
        foreach (string warning in coverage.Warnings)
        {
            writer.WriteStringValue(warning);
        }

        writer.WriteEndArray();
        writer.WriteNumber("spans", coverage.Spans);
        writer.WriteStartArray("spansBySource");
        foreach (ExportSourceCount source in coverage.SpansBySource)
        {
            writer.WriteStartObject();
            writer.WriteString("source", source.Source);
            writer.WriteNumber("spans", source.Spans);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteNumber("supersededMatchedImportSpans", coverage.SupersededMatchedImportSpans);
        writer.WriteNumber("supersededUnmatchedImportSpans", coverage.SupersededUnmatchedImportSpans);
        writer.WriteNumber("ledgerAmbiguousImportRecords", coverage.LedgerAmbiguousImportRecords);
        writer.WriteNumber("ledgerIncompleteImportLogs", coverage.LedgerIncompleteImportLogs);
        writer.WriteNumber("unpricedSpans", coverage.UnpricedSpans);
        writer.WriteNumber("apiEquivalentUnpricedSpans", coverage.ApiEquivalentUnpricedSpans);
        writer.WriteNumber("apiEquivalentEstimatedSpans", coverage.ApiEquivalentEstimatedSpans);
        writer.WriteNumber("cacheReportedSpans", coverage.CacheReportedSpans);
        writer.WriteNumber("cacheAbsentSpans", coverage.CacheAbsentSpans);
        writer.WriteNumber("cacheLegacyUnknownSpans", coverage.CacheLegacyUnknownSpans);
        writer.WriteStartObject("toolHistory");
        writer.WriteBoolean("complete", coverage.ToolHistoryComplete);
        writer.WriteNumber("retentionDays", coverage.ToolHistoryRetentionDays);
        LedgerFormat.WriteTime(writer, "retainedFromUtc", coverage.ToolHistoryFromUtc);
        writer.WriteNumber("spansBeforeRetention", coverage.SpansBeforeToolHistory);
        writer.WriteNumber("importedToolCallsWithoutDetail", coverage.ImportedToolCallsWithoutDetail);
        writer.WriteEndObject();
        writer.WriteStartObject("timeline");
        writer.WriteBoolean("truncated", coverage.TimelineTruncated);
        writer.WriteNumber("maxSpans", coverage.MaxTimelineSpans);
        writer.WriteNumber("spans", coverage.Spans);
        writer.WriteNumber("spansDrawn", coverage.TimelineSpans);
        writer.WriteNumber("toolCalls", coverage.ToolCalls);
        writer.WriteNumber("toolCallsDrawn", coverage.TimelineToolCalls);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    // A coordinate with at most three decimal places, invariant.
    public static string Number(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
```

Edit 1 of 3 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
        DateTimeOffset toolHistoryFrom = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
        return new ExportReportModel
        {
```

with:

```csharp
        DateTimeOffset toolHistoryFrom = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
        IReadOnlyList<LedgerRollupRow> byModel = await snapshot.GetRollupAsync(LedgerGrouping.Model, filter, cancellationToken);

        // The timeline draws the latest MaxTimelineSpans requests and, separately, tool calls, in start order.
        ExportSpanRecord[] drawnSpans =
        [
            .. (await snapshot.GetLatestSpansAsync(filter, inputs.MaxTimelineSpans, cancellationToken))
                .Reverse()
                .Select(row => ExportRecords.Span(row, inputs.IncludeLocalPaths)),
        ];
        ExportToolCallRecord[] drawnToolCalls =
        [
            .. (await snapshot.GetLatestToolCallsAsync(filter, inputs.MaxTimelineSpans, cancellationToken))
                .Reverse()
                .Select(ExportRecords.ToolCall),
        ];
        return new ExportReportModel
        {
```

Edit 2 of 3 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
            },
            AuditFindings = [],
            ByModel = await snapshot.GetRollupAsync(LedgerGrouping.Model, filter, cancellationToken),
            ByAgent = await snapshot.GetRollupAsync(LedgerGrouping.Agent, filter, cancellationToken),
            ByBranch =
```

with:

```csharp
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
                ToolCalls = await snapshot.CountToolCallsAsync(filter, cancellationToken),
                MaxTimelineSpans = inputs.MaxTimelineSpans,
                TimelineSpans = drawnSpans.Length,
                TimelineToolCalls = drawnToolCalls.Length,
            },
            AuditFindings = [],
            ByModel = byModel,
            ByAgent = await snapshot.GetRollupAsync(LedgerGrouping.Agent, filter, cancellationToken),
            ByBranch =
```

Edit 3 of 3 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
                    .Select(row => row with { Repository = Shown(row.Repository) }),
            ],
        };

        string Shown(string key) => ExportRecords.Repository(key, roots, inputs.IncludeLocalPaths);
```

with:

```csharp
                    .Select(row => row with { Repository = Shown(row.Repository) }),
            ],
            Timeline = TimelineLayout.Build(
                drawnSpans,
                drawnToolCalls,
                [.. byModel.Select(row => row.Model ?? "(none)")],
                inputs.TimeZone),
        };

        string Shown(string key) => ExportRecords.Repository(key, roots, inputs.IncludeLocalPaths);
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Export/ExportRecords.cs`, replace:

```csharp
using System.Globalization;
using CopilotLedger.Cli.Reporting;

namespace CopilotLedger.Cli.Export;
```

with:

```csharp
using System.Globalization;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Export/ExportRecords.cs`, replace:

```csharp
    public static string Plural(long count, string one, string many) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? one : many)}";
}
```

with:

```csharp
    public static string Plural(long count, string one, string many) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? one : many)}";

    // The line that says the timeline doesn't draw every request or tool call.
    public static string TimelineTruncation(ExportCoverage coverage) =>
        $"The timeline draws the latest {LedgerFormat.Count(coverage.TimelineSpans)} of {Plural(coverage.Spans, "request", "requests")} and {LedgerFormat.Count(coverage.TimelineToolCalls)} of {Plural(coverage.ToolCalls, "tool call", "tool calls")}; the totals and tables include them all.";

    // A request's shown repository: its slug, else its root's folder name (the root with includeLocalPaths), else (none).
    public static string SpanRepository(string? slug, string? root, bool includeLocalPaths) =>
        slug ?? (root is null ? "(none)" : includeLocalPaths ? root : FolderName(root));

    public static ExportSpanRecord Span(ExportSpanRow row, bool includeLocalPaths) => new()
    {
        TraceId = row.TraceId,
        SpanId = row.SpanId,
        ParentSpanId = row.ParentSpanId,
        OperationName = row.OperationName,
        ConversationId = row.ConversationId,
        Agent = row.AgentName,
        Provider = row.ProviderName,
        RequestModel = row.RequestModel,
        ResponseModel = row.ResponseModel,
        Model = row.ModelId,
        StartTime = row.StartTime,
        EndTime = row.EndTime,
        InputTokens = row.InputTokens,
        CacheReadInputTokens = row.CacheReadInputTokens,
        CacheWriteInputTokens = row.CacheWriteInputTokens,
        UncachedInputTokens = Math.Max(row.InputTokens - row.CacheReadInputTokens - row.CacheWriteInputTokens, 0),
        OutputTokens = row.OutputTokens,
        CacheReadState = ReportCommands.StateName(row.CacheReadState),
        CacheWriteState = ReportCommands.StateName(row.CacheWriteState),
        BillingChannel = row.BillingChannel,
        PricingStatus = row.PricingStatus,
        RatesVersion = row.RatesVersion,
        CostUsd = row.CostUsd,
        AiCredits = row.BillingChannel == BillingChannel.GitHubAiCredits ? AiCreditMath.UsdToAiCredits(row.CostUsd) : 0m,
        SavedUsd = row.SavedUsd,
        ApiEquivalentUsd = row.ApiEquivalentUsd,
        ApiEquivalentStatus = row.ApiEquivalentStatus,
        ApiRatesVersion = row.ApiRatesVersion,
        ApiRateCardSha256 = row.ApiRateCardSha256,
        Repository = SpanRepository(row.RepositorySlug, row.RepositoryRoot, includeLocalPaths),
        RepositoryRoot = includeLocalPaths ? row.RepositoryRoot : null,
        Branch = row.BranchName,
        HeadCommit = row.HeadCommitSha,
        PullRequest = row.PullRequestNumber,
        IngestSource = row.IngestSource,
    };

    public static ExportToolCallRecord ToolCall(ExportToolCallRow row)
    {
        string status = row.StatusCode switch
        {
            1 => "ok",
            2 => "error",
            _ => "unset",
        };
        return new ExportToolCallRecord
        {
            TraceId = row.TraceId,
            SpanId = row.SpanId,
            ConversationId = row.ConversationId,
            Agent = row.AgentName,
            ToolName = row.ToolName,
            StartTime = row.StartTime,
            ExitCode = row.ToolExitCode,
            Status = status,
            Failed = row.ToolExitCode is not (null or 0) || status == "error",
            IngestSource = row.IngestSource,
        };
    }
}
```

Edit 1 of 3 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;
```

with:

```csharp
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;
```

Edit 2 of 3 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
    public required IReadOnlyList<BreakerTrip> BreakerTrips { get; init; }              // shown keys; RepositoryRoot only with local paths
    public required IReadOnlyList<BranchBudgetRow> Budgets { get; init; }               // shown keys
}

internal sealed record ExportHeader
```

with:

```csharp
    public required IReadOnlyList<BreakerTrip> BreakerTrips { get; init; }              // shown keys; RepositoryRoot only with local paths
    public required IReadOnlyList<BranchBudgetRow> Budgets { get; init; }               // shown keys
    public required ExportTimeline Timeline { get; init; }
}

internal sealed record ExportHeader
```

Edit 3 of 3 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
    // Imports that the ledger couldn't account for (deferred, unfinished, or replaced without a match) may hide requests.
    public bool RequestsComplete =>
        LedgerAmbiguousImportRecords == 0 && LedgerIncompleteImportLogs == 0 && SupersededUnmatchedImportSpans == 0;
    public bool ToolHistoryComplete => SpansBeforeToolHistory == 0 && ImportedToolCallsWithoutDetail == 0;
}

internal sealed record ExportAuditFinding(string Severity, string Title, string Detail);
```

with:

```csharp
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
    public required long ToolCalls { get; init; }                       // CountToolCallsAsync
    public required int MaxTimelineSpans { get; init; }
    public required long TimelineSpans { get; init; }                   // drawn requests
    public required long TimelineToolCalls { get; init; }               // drawn tool calls
    public bool TimelineTruncated => TimelineSpans < Spans || TimelineToolCalls < ToolCalls;
    // Imports that the ledger couldn't account for (deferred, unfinished, or replaced without a match) may hide requests.
    public bool RequestsComplete =>
        LedgerAmbiguousImportRecords == 0 && LedgerIncompleteImportLogs == 0 && SupersededUnmatchedImportSpans == 0;
    public bool ToolHistoryComplete => SpansBeforeToolHistory == 0 && ImportedToolCallsWithoutDetail == 0;

    // The limitations that apply, ordinal.
    public IReadOnlyList<string> Warnings =>
    [
        .. new (bool Applies, string Name)[]
        {
            (ApiEquivalentEstimatedSpans > 0, "api-equivalent-estimated"),
            (ApiEquivalentUnpricedSpans > 0, "api-equivalent-unpriced"),
            (CacheAbsentSpans + CacheLegacyUnknownSpans > 0, "cache-unreported"),
            (LedgerAmbiguousImportRecords > 0, "import-ambiguous"),
            (LedgerIncompleteImportLogs > 0, "import-incomplete"),
            (SupersededUnmatchedImportSpans > 0, "import-superseded-unmatched"),
            (ImportedToolCallsWithoutDetail > 0, "imported-tool-calls-without-detail"),
            (TimelineTruncated, "timeline-truncated"),
            (SpansBeforeToolHistory > 0, "tool-history-retention"),
            (UnpricedSpans > 0, "unpriced"),
        }.Where(warning => warning.Applies).Select(warning => warning.Name),
    ];
}

// One selected request, as the bundle and the timeline show it.
internal sealed record ExportSpanRecord
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string OperationName { get; init; }
    public string? ConversationId { get; init; }
    public string? Agent { get; init; }
    public string? Provider { get; init; }
    public string? RequestModel { get; init; }
    public string? ResponseModel { get; init; }
    public string? Model { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required long InputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required long UncachedInputTokens { get; init; }        // max(input - cache read - cache write, 0), as pricing counts it
    public required long OutputTokens { get; init; }
    public required string CacheReadState { get; init; }           // reported, absent, legacy_unknown
    public required string CacheWriteState { get; init; }
    public required BillingChannel BillingChannel { get; init; }
    public required string PricingStatus { get; init; }
    public required string RatesVersion { get; init; }
    public required decimal CostUsd { get; init; }
    public required decimal AiCredits { get; init; }               // CostUsd in AI credits on GitHub AI credits; 0 otherwise
    public required decimal SavedUsd { get; init; }
    public decimal? ApiEquivalentUsd { get; init; }
    public string? ApiEquivalentStatus { get; init; }
    public string? ApiRatesVersion { get; init; }
    public string? ApiRateCardSha256 { get; init; }
    public required string Repository { get; init; }               // slug, root's folder name (the root with local paths), or (none)
    public string? RepositoryRoot { get; init; }                   // set only with --include-local-paths
    public string? Branch { get; init; }
    public string? HeadCommit { get; init; }
    public int? PullRequest { get; init; }
    public required string IngestSource { get; init; }
}

// One retained tool call of a selected prompt chain.
internal sealed record ExportToolCallRecord
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ConversationId { get; init; }
    public string? Agent { get; init; }
    public string? ToolName { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public long? ExitCode { get; init; }
    public required string Status { get; init; }      // unset, ok, error
    public required bool Failed { get; init; }        // an exit code other than 0, or status error
    public required string IngestSource { get; init; }
}

// The timeline's drawing: x runs from 0 at FromUnixMs to 1000 at ToUnixMs, both 0 when nothing is drawn.
internal sealed record ExportTimeline
{
    public required long FromUnixMs { get; init; }
    public required long ToUnixMs { get; init; }
    public required IReadOnlyList<ExportTimelineRow> Rows { get; init; }
    public required IReadOnlyList<ExportTimelineSpan> Spans { get; init; }        // by start, trace, span
    public required IReadOnlyList<ExportTimelineTool> ToolCalls { get; init; }
    public required IReadOnlyList<UtcBand> NightBands { get; init; }
    public required IReadOnlyList<ExportAxisTick> Ticks { get; init; }
    public required IReadOnlyList<string> Models { get; init; }                   // the models of colors m0-m6: the model rollup's first 7
    public required bool OtherModels { get; init; }                               // some drawn request has color m7
    public bool IsEmpty => Spans.Count == 0 && ToolCalls.Count == 0;
}

internal sealed record ExportTimelineRow(
    int Index,
    string Label,
    string Title,
    long Requests,
    long ToolCalls,
    long FailedToolCalls,
    DateTimeOffset? First,
    DateTimeOffset? Last,
    IReadOnlyList<string> Models);

internal sealed record ExportTimelineSpan(ExportSpanRecord Span, int Row, decimal X, decimal Width, int Color);

internal sealed record ExportTimelineTool(ExportToolCallRecord Call, int Row, decimal X);

internal sealed record ExportAxisTick(decimal X, int Level, string Label);   // Level 0 days, 1 six hours, 2 hours, 3 ten minutes

internal sealed record ExportAuditFinding(string Severity, string Title, string Detail);
```

New file `src/CopilotLedger.Cli/Export/ExportSvg.cs`:

```csharp
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Export;

// The report's SVG. Text goes through HtmlEncoder.Default, and numbers are invariant.
internal static class ExportSvg
{
    // The timeline's session labels, plot, axis, axis labels, and overview.
    public static void Timeline(StringBuilder html, ExportTimeline timeline, TimeZoneInfo zone)
    {
        int height = Math.Max(timeline.Rows.Count, 1) * TimelineLayout.RowHeight;
        long from = timeline.FromUnixMs;
        long to = timeline.ToUnixMs;
        Line(html, "<div class=\"timeline-grid\">");
        Line(
            html,
            $"<svg class=\"timeline-labels\" role=\"img\" aria-labelledby=\"timeline-labels-title timeline-labels-desc\" width=\"200\" height=\"{Int(height)}\" viewBox=\"0 0 200 {Int(height)}\">");
        Line(
            html,
            "<title id=\"timeline-labels-title\">Sessions</title><desc id=\"timeline-labels-desc\">One row per Copilot session, in the order they started.</desc>");
        foreach (ExportTimelineRow row in timeline.Rows)
        {
            Line(
                html,
                $"<text x=\"196\" y=\"{Int(row.Index * TimelineLayout.RowHeight + 11)}\" text-anchor=\"end\"><title>{Text(row.Title)}</title>{Text(row.Label)}</text>");
        }

        Line(html, "</svg>");
        Line(html, "<div class=\"timeline-viewport\" id=\"timeline-viewport\" tabindex=\"0\" aria-describedby=\"timeline-help\">");
        Line(
            html,
            $"<svg id=\"timeline-plot\" role=\"img\" aria-labelledby=\"timeline-plot-title timeline-plot-desc\" viewBox=\"0 0 1000 {Int(height)}\" preserveAspectRatio=\"none\" height=\"{Int(height)}\">");
        Line(html, "<title id=\"timeline-plot-title\">Requests and tool calls over time</title>");
        Line(
            html,
            $"<desc id=\"timeline-plot-desc\">{Text($"{ExportRecords.Plural(timeline.Spans.Count, "request", "requests")} and {ExportRecords.Plural(timeline.ToolCalls.Count, "tool call", "tool calls")} from {LedgerFormat.LocalTime(DateTimeOffset.FromUnixTimeMilliseconds(from), zone)} to {LedgerFormat.LocalTime(DateTimeOffset.FromUnixTimeMilliseconds(to), zone)} ({zone.Id}). Bars are model requests, colored by model; ticks under them are tool calls, red when they failed. Shaded bands are nights, 18:00 to 06:00.")}</desc>");
        Nights(html, timeline, height);
        Line(html, "<g class=\"spans\">");
        foreach (ExportTimelineSpan bar in timeline.Spans)
        {
            ExportSpanRecord span = bar.Span;
            Line(
                html,
                $"<rect class=\"span m{Int(bar.Color)}\" x=\"{ExportJson.Number(bar.X)}\" y=\"{Int(bar.Row * TimelineLayout.RowHeight + 2)}\" width=\"{ExportJson.Number(bar.Width)}\" height=\"8\"><title>{Text($"{Clock(span.StartTime, zone)} · {span.Model ?? "(none)"} · {span.Agent ?? "(none)"} · {Cost(span)}")}</title></rect>");
        }

        Line(html, "</g>");
        Line(html, "<g class=\"tools\">");
        foreach (ExportTimelineTool tick in timeline.ToolCalls)
        {
            ExportToolCallRecord call = tick.Call;
            string y = Int(tick.Row * TimelineLayout.RowHeight + 11);
            string y2 = Int(tick.Row * TimelineLayout.RowHeight + 15);
            string outcome = call.ExitCode is long code
                ? "exit code " + code.ToString(CultureInfo.InvariantCulture)
                : call.Status == "error" ? "error" : "ok";
            Line(
                html,
                $"<line class=\"{(call.Failed ? "tool fail" : "tool")}\" x1=\"{ExportJson.Number(tick.X)}\" y1=\"{y}\" x2=\"{ExportJson.Number(tick.X)}\" y2=\"{y2}\"><title>{Text($"{Clock(call.StartTime, zone)} · {call.ToolName ?? "(unknown tool)"} · {outcome}")}</title></line>");
        }

        Line(html, "</g>");
        Line(html, "</svg>");
        Line(html, "<svg id=\"timeline-axis\" class=\"timeline-axis\" aria-hidden=\"true\" viewBox=\"0 0 1000 6\" preserveAspectRatio=\"none\" height=\"6\">");
        foreach (ExportAxisTick tick in timeline.Ticks)
        {
            Line(html, $"<line class=\"tick\" x1=\"{ExportJson.Number(tick.X)}\" y1=\"0\" x2=\"{ExportJson.Number(tick.X)}\" y2=\"6\" />");
        }

        Line(html, "</svg>");
        html.Append("<div class=\"axis-labels\" id=\"timeline-axis-labels\">");
        foreach (ExportAxisTick tick in TimelineLayout.Labels(timeline.Ticks, 0m, TimelineLayout.Width))
        {
            html.Append(
                $"<span class=\"axis-label\" style=\"left: {(tick.X / 10m).ToString("0.####", CultureInfo.InvariantCulture)}%\">{Text(tick.Label)}</span>");
        }

        Line(html, "</div>");
        Line(
            html,
            "<svg id=\"timeline-overview\" role=\"img\" aria-labelledby=\"timeline-overview-title timeline-overview-desc\" viewBox=\"0 0 1000 32\" preserveAspectRatio=\"none\" height=\"32\">");
        Line(
            html,
            "<title id=\"timeline-overview-title\">Overview</title><desc id=\"timeline-overview-desc\">How many requests started in each part of the timeline.</desc>");
        Nights(html, timeline, 32);
        int[] bins = new int[200];
        foreach (ExportTimelineSpan bar in timeline.Spans)
        {
            long offset = bar.Span.StartTime.ToUnixTimeMilliseconds() - from;
            bins[(int)Math.Min(offset * 200 / (to - from), 199)]++;
        }

        int most = bins.Max();
        for (int bin = 0; bin < bins.Length; bin++)
        {
            if (bins[bin] > 0)
            {
                decimal barHeight = decimal.Round(bins[bin] * 28m / most, 2, MidpointRounding.AwayFromZero);
                Line(
                    html,
                    $"<rect class=\"density\" x=\"{Int(bin * 5)}\" y=\"{(30m - barHeight).ToString("0.##", CultureInfo.InvariantCulture)}\" width=\"5\" height=\"{barHeight.ToString("0.##", CultureInfo.InvariantCulture)}\" />");
            }
        }

        Line(html, "<rect id=\"timeline-window\" class=\"window\" x=\"0\" y=\"0\" width=\"1000\" height=\"32\" />");
        Line(html, "</svg>");
        Line(html, "</div></div>");
    }

    // The night bands, clipped to the axis, as rects of the given height.
    private static void Nights(StringBuilder html, ExportTimeline timeline, int height)
    {
        Line(html, "<g class=\"nights\">");
        foreach (UtcBand band in timeline.NightBands)
        {
            long start = Math.Max(band.FromUtc.ToUnixTimeMilliseconds(), timeline.FromUnixMs);
            long end = Math.Min(band.ToUtc.ToUnixTimeMilliseconds(), timeline.ToUnixMs);
            decimal x = TimelineLayout.Position(start, timeline.FromUnixMs, timeline.ToUnixMs);
            decimal width = TimelineLayout.Position(end, timeline.FromUnixMs, timeline.ToUnixMs) - x;
            Line(
                html,
                $"<rect class=\"night\" x=\"{ExportJson.Number(x)}\" y=\"0\" width=\"{ExportJson.Number(width)}\" height=\"{Int(height)}\" />");
        }

        Line(html, "</g>");
    }

    // What a request cost, for its bar's title.
    private static string Cost(ExportSpanRecord span) =>
        span.PricingStatus is not ("priced" or "local_offload")
            ? $"Unpriced ({span.PricingStatus})"
            : span.BillingChannel switch
            {
                BillingChannel.GitHubAiCredits => $"{LedgerFormat.Credits(span.AiCredits)} AI credits",
                BillingChannel.FoundryByok => $"{LedgerFormat.Usd(span.CostUsd)} BYOK",
                BillingChannel.Local => $"local, {LedgerFormat.Usd(span.SavedUsd)} saved",
                _ => $"Unpriced ({span.PricingStatus})",
            };

    private static string Clock(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone).ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Text(string value) => HtmlEncoder.Default.Encode(value);

    private static void Line(StringBuilder html, string line) => html.Append(line).Append('\n');
}
```

Edit 1 of 5 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        WriteSummary(html, model);
        WriteCoverage(html, model.Coverage, header.TimeZone);
        WriteCache(html, model);
        WritePromptChains(html, model, header.TimeZone);
```

with:

```csharp
        WriteSummary(html, model);
        WriteCoverage(html, model.Coverage, header.TimeZone);
        WriteTimeline(html, model, header.TimeZone);
        WriteCache(html, model);
        WritePromptChains(html, model, header.TimeZone);
```

Edit 2 of 5 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
            html,
            $"<footer><p>Written by gh-ledger {Text(header.ToolVersion)}. This report holds no prompts, responses, tool arguments or results, or file contents{(header.IncludeLocalPaths ? string.Empty : ", and no local paths")}. Times are in {Text(header.TimeZone.Id)}.</p></footer>");
        Line(html, "</body>");
        Line(html, "</html>");
```

with:

```csharp
            html,
            $"<footer><p>Written by gh-ledger {Text(header.ToolVersion)}. This report holds no prompts, responses, tool arguments or results, or file contents{(header.IncludeLocalPaths ? string.Empty : ", and no local paths")}. Times are in {Text(header.TimeZone.Id)}.</p></footer>");
        html.Append("<script type=\"application/json\" id=\"ledger-data\">");
        html.Append(Encoding.UTF8.GetString(ExportJson.LedgerData(model)));
        Line(html, "</script>");
        Line(html, "</body>");
        Line(html, "</html>");
```

Edit 3 of 5 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
                : $"{LedgerFormat.Count(unreported)} requests reported no cache counters; cache hit ratios leave them out.";
        }
    }

    private static void WriteHeader(StringBuilder html, ExportReportModel model)
```

with:

```csharp
                : $"{LedgerFormat.Count(unreported)} requests reported no cache counters; cache hit ratios leave them out.";
        }

        if (coverage.TimelineTruncated)
        {
            yield return ExportRecords.TimelineTruncation(coverage);
        }
    }

    private static void WriteHeader(StringBuilder html, ExportReportModel model)
```

Edit 4 of 5 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        yield return ("summary", "Summary");
        yield return ("coverage", "Coverage");
        yield return ("cache", "Cache efficiency");
        yield return ("prompt-chains", "Prompt chains");
```

with:

```csharp
        yield return ("summary", "Summary");
        yield return ("coverage", "Coverage");
        yield return ("timeline", "Timeline");
        yield return ("cache", "Cache efficiency");
        yield return ("prompt-chains", "Prompt chains");
```

Edit 5 of 5 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        }

        Line(html, "</ul>");
        Line(html, "</section>");
    }
```

with:

```csharp
        }

        Line(html, "</ul>");
        Line(html, "</section>");
    }

    private static void WriteTimeline(StringBuilder html, ExportReportModel model, TimeZoneInfo zone)
    {
        ExportTimeline timeline = model.Timeline;
        ExportCoverage coverage = model.Coverage;
        SectionStart(html, "timeline", "Timeline");
        string note = timeline.IsEmpty
            ? "No requests or tool calls to draw."
            : coverage.TimelineTruncated
                ? ExportRecords.TimelineTruncation(coverage)
                : $"All {ExportRecords.Plural(coverage.Spans, "request", "requests")} and {ExportRecords.Plural(coverage.ToolCalls, "tool call", "tool calls")}.";
        Line(html, $"<p class=\"note\" id=\"timeline-note\">{Text(note)}</p>");
        if (!timeline.IsEmpty)
        {
            Line(
                html,
                "<div class=\"timeline-controls\" id=\"timeline-controls\" hidden=\"hidden\"><button type=\"button\" id=\"timeline-reset\">Reset zoom</button>");
            Line(
                html,
                "<p id=\"timeline-help\">Scroll or pinch to zoom and drag to pan, or focus the timeline and use the arrow keys, + and -, and 0. Drag across the overview to choose a range.</p></div>");
            Line(html, "<figure class=\"chart\" id=\"timeline-figure\">");
            ExportSvg.Timeline(html, timeline, zone);
            html.Append("<figcaption>Requests by session. <ul class=\"legend\">");
            for (int color = 0; color < timeline.Models.Count; color++)
            {
                html.Append($"<li><span class=\"swatch m{color.ToString(CultureInfo.InvariantCulture)}\"></span>{Text(timeline.Models[color])}</li>");
            }

            if (timeline.OtherModels)
            {
                html.Append("<li><span class=\"swatch m7\"></span>Other models</li>");
            }

            Line(html, "</ul></figcaption>");
            Line(html, "<details class=\"chart-table\"><summary>Data table</summary>");
            Table(
                html,
                "table-timeline",
                "Sessions on the timeline",
                ["#", "Session", "First request", "Last request", "Requests", "Models", "Tool calls", "Failed tool calls"],
                [
                    .. timeline.Rows.Select(row => (IReadOnlyList<Cell>)
                    [
                        Count(row.Index + 1),
                        new Cell(Text(row.Title), row.Title),
                        row.First is DateTimeOffset first ? TimeCell(first, zone) : new Cell("-", null),
                        row.Last is DateTimeOffset last ? TimeCell(last, zone) : new Cell("-", null),
                        Count(row.Requests),
                        Name(row.Models.Count == 0 ? "-" : string.Join(", ", row.Models)),
                        Count(row.ToolCalls),
                        Count(row.FailedToolCalls),
                    ]),
                ]);
            Line(html, "</details>");
            Line(html, "</figure>");
        }

        Line(html, "</section>");
    }
```

New file `src/CopilotLedger.Cli/Export/TimelineLayout.cs`:

```csharp
using System.Globalization;
using CopilotLedger.Cli.Reporting;

namespace CopilotLedger.Cli.Export;

// The timeline's geometry: rows, bars, ticks, night bands, and axis marks, with x from 0 to Width over the drawn range.
internal static class TimelineLayout
{
    public const int Width = 1000;
    public const int RowHeight = 16;
    public const int MaxRows = 200;
    private const long MinimumRangeMs = 3_600_000;
    private const long DayMs = 86_400_000;

    public static readonly ExportTimeline Empty = new()
    {
        FromUnixMs = 0,
        ToUnixMs = 0,
        Rows = [],
        Spans = [],
        ToolCalls = [],
        NightBands = [],
        Ticks = [],
        Models = [],
        OtherModels = false,
    };

    // spans and toolCalls are the drawn ones in start order; modelsByCost is the model rollup's order, which picks colors.
    public static ExportTimeline Build(
        IReadOnlyList<ExportSpanRecord> spans,
        IReadOnlyList<ExportToolCallRecord> toolCalls,
        IReadOnlyList<string> modelsByCost,
        TimeZoneInfo zone)
    {
        if (spans.Count == 0 && toolCalls.Count == 0)
        {
            return Empty;
        }

        long t0 = Math.Min(
            spans.Count == 0 ? long.MaxValue : spans.Min(span => span.StartTime.ToUnixTimeMilliseconds()),
            toolCalls.Count == 0 ? long.MaxValue : toolCalls.Min(call => call.StartTime.ToUnixTimeMilliseconds()));
        long t1 = Math.Max(
            spans.Count == 0 ? long.MinValue : spans.Max(span => span.EndTime.ToUnixTimeMilliseconds()),
            toolCalls.Count == 0 ? long.MinValue : toolCalls.Max(call => call.StartTime.ToUnixTimeMilliseconds()));
        if (t1 - t0 < MinimumRangeMs)
        {
            t0 -= (MinimumRangeMs - (t1 - t0)) / 2;
            t1 = t0 + MinimumRangeMs;
        }

        // A tool call's row is its prompt chain's: the conversation of the trace's first drawn request, else its own.
        Dictionary<string, string?> traces = new(StringComparer.Ordinal);
        foreach (ExportSpanRecord span in spans)
        {
            traces.TryAdd(span.TraceId, span.ConversationId);
        }

        string? ToolConversation(ExportToolCallRecord call) =>
            traces.TryGetValue(call.TraceId, out string? conversation) ? conversation : call.ConversationId;

        Dictionary<string, Group> conversations = new(StringComparer.Ordinal);
        Group? none = null;
        Group GroupOf(string? conversation)
        {
            if (conversation is null)
            {
                return none ??= new Group(null);
            }

            if (!conversations.TryGetValue(conversation, out Group? group))
            {
                group = new Group(conversation);
                conversations.Add(conversation, group);
            }

            return group;
        }

        foreach (ExportSpanRecord span in spans)
        {
            GroupOf(span.ConversationId).Add(span.StartTime.ToUnixTimeMilliseconds());
        }

        foreach (ExportToolCallRecord call in toolCalls)
        {
            GroupOf(ToolConversation(call)).Add(call.StartTime.ToUnixTimeMilliseconds());
        }

        // With more than MaxRows groups, the MaxRows - 1 with the most glyphs keep rows and the others share the last.
        List<Group> groups = [.. conversations.Values];
        if (none is not null)
        {
            groups.Add(none);
        }

        List<Group> kept = groups.Count <= MaxRows
            ? groups
            : [.. groups
                .OrderByDescending(group => group.Glyphs)
                .ThenBy(group => group.First)
                .ThenBy(group => group.Conversation is null ? 1 : 0)
                .ThenBy(group => group.Conversation, StringComparer.Ordinal)
                .Take(MaxRows - 1)];
        List<Group> ordered =
        [
            .. kept.Where(group => group.Conversation is not null)
                .OrderBy(group => group.First)
                .ThenBy(group => group.Conversation, StringComparer.Ordinal),
            .. kept.Where(group => group.Conversation is null),
        ];
        int others = groups.Count - kept.Count;
        for (int index = 0; index < ordered.Count; index++)
        {
            ordered[index].Row = index;
        }

        foreach (Group group in groups.Where(group => group.Row < 0))
        {
            group.Row = ordered.Count;
        }

        decimal X(long instant) => Position(instant, t0, t1);

        List<string> colors = [.. modelsByCost.Take(7)];
        ExportTimelineSpan[] bars =
        [
            .. spans.Select(span =>
            {
                decimal start = X(span.StartTime.ToUnixTimeMilliseconds());
                decimal end = X(span.EndTime.ToUnixTimeMilliseconds());
                int color = span.Model is null ? -1 : colors.IndexOf(span.Model);
                return new ExportTimelineSpan(span, GroupOf(span.ConversationId).Row, start, Math.Max(end - start, 0m), color < 0 ? 7 : color);
            }),
        ];
        ExportTimelineTool[] ticks =
        [
            .. toolCalls.Select(call =>
                new ExportTimelineTool(call, GroupOf(ToolConversation(call)).Row, X(call.StartTime.ToUnixTimeMilliseconds()))),
        ];
        ILookup<int, ExportTimelineSpan> barsByRow = bars.ToLookup(bar => bar.Row);
        ILookup<int, ExportTimelineTool> ticksByRow = ticks.ToLookup(tick => tick.Row);

        // A row's label is its number and the local time of its first request or tool call, which is unique and readable
        // even when session IDs share a long prefix; the title and the data table hold the whole ID.
        List<ExportTimelineRow> rows =
        [
            .. ordered.Select(group => Row(
                group.Row,
                group.Conversation is null
                    ? $"{Ordinal(group.Row)} · no session"
                    : $"{Ordinal(group.Row)} · {TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(group.First), zone).ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)}",
                group.Conversation ?? "(no session)",
                barsByRow[group.Row],
                ticksByRow[group.Row])),
        ];
        if (others > 0)
        {
            rows.Add(Row(
                ordered.Count,
                $"{Ordinal(ordered.Count)} · {LedgerFormat.Count(others)} other sessions",
                $"({LedgerFormat.Count(others)} other sessions)",
                barsByRow[ordered.Count],
                ticksByRow[ordered.Count]));
        }

        return new ExportTimeline
        {
            FromUnixMs = t0,
            ToUnixMs = t1,
            Rows = rows,
            Spans = bars,
            ToolCalls = ticks,
            NightBands = ExportTime.NightBands(
                zone,
                DateTimeOffset.FromUnixTimeMilliseconds(t0),
                DateTimeOffset.FromUnixTimeMilliseconds(t1)),
            Ticks = Ticks(t0, t1, zone, X),
            Models = colors,
            OtherModels = bars.Any(bar => bar.Color == 7),
        };
    }

    // An instant's x on the axis from `from` (0) to `to` (Width), to three decimal places.
    public static decimal Position(long instant, long from, long to) =>
        decimal.Round((instant - from) * (decimal)Width / (to - from), 3, MidpointRounding.AwayFromZero);

    // A row's number as shown: its index from 1.
    private static string Ordinal(int row) => (row + 1).ToString(CultureInfo.InvariantCulture);

    // The axis labels to show over [from, to] of x: the finest level whose ticks there, counting coarser levels, number at
    // most 12; at level 0, every ceil(n / 12)-th day.
    public static IReadOnlyList<ExportAxisTick> Labels(IReadOnlyList<ExportAxisTick> ticks, decimal from, decimal to)
    {
        ExportAxisTick[] inView = [.. ticks.Where(tick => tick.X >= from && tick.X <= to)];
        for (int level = 3; level >= 0; level--)
        {
            ExportAxisTick[] shown = [.. inView.Where(tick => tick.Level <= level)];
            if (shown.Length <= 12)
            {
                return shown;
            }
        }

        ExportAxisTick[] days = [.. inView.Where(tick => tick.Level == 0)];
        int every = (days.Length + 11) / 12;
        return [.. days.Where((_, index) => index % every == 0)];
    }

    // Local boundaries from t0 to t1 as ticks: level 0 at midnight, 1 at 06:00, 12:00, and 18:00 within 62 days, 2 at other
    // whole hours within 7 days, and 3 at other 10-minute marks within a day. A boundary that a transition skips maps to the
    // instant of the previous one and is dropped.
    private static IReadOnlyList<ExportAxisTick> Ticks(long t0, long t1, TimeZoneInfo zone, Func<long, decimal> x)
    {
        long range = t1 - t0;
        TimeSpan step = range <= DayMs
            ? TimeSpan.FromMinutes(10)
            : range <= 7 * DayMs
                ? TimeSpan.FromHours(1)
                : range <= 62 * DayMs ? TimeSpan.FromHours(6) : TimeSpan.FromDays(1);
        DateTime end = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(t1), zone).DateTime;
        List<ExportAxisTick> ticks = [];
        long previous = long.MinValue;
        for (DateTime local = ExportTime.LocalDate(DateTimeOffset.FromUnixTimeMilliseconds(t0), zone).ToDateTime(TimeOnly.MinValue);
             local <= end;
             local += step)
        {
            long instant = ExportTime.ToUtc(local, zone).ToUnixTimeMilliseconds();
            if (instant < t0 || instant > t1 || instant == previous)
            {
                continue;
            }

            previous = instant;
            int level = local.TimeOfDay == TimeSpan.Zero ? 0 : local.Minute != 0 ? 3 : local.Hour % 6 == 0 ? 1 : 2;
            ticks.Add(new ExportAxisTick(
                x(instant),
                level,
                local.ToString(level == 0 ? "yyyy-MM-dd" : "HH:mm", CultureInfo.InvariantCulture)));
        }

        int days = ticks.Count(tick => tick.Level == 0);
        if (days > 2_000)
        {
            int every = (days + 1_999) / 2_000;
            int day = -1;
            ticks = [.. ticks.Where(tick => tick.Level != 0 || ++day % every == 0)];
        }

        return ticks;
    }

    private static ExportTimelineRow Row(
        int index,
        string label,
        string title,
        IEnumerable<ExportTimelineSpan> bars,
        IEnumerable<ExportTimelineTool> ticks)
    {
        ExportTimelineSpan[] requests = [.. bars];
        ExportTimelineTool[] calls = [.. ticks];
        return new ExportTimelineRow(
            index,
            label,
            title,
            requests.Length,
            calls.Length,
            calls.Count(tick => tick.Call.Failed),
            requests.Length == 0 ? null : requests[0].Span.StartTime,
            requests.Length == 0 ? null : requests[^1].Span.StartTime,
            [.. requests.Select(bar => bar.Span.Model ?? "(none)").Distinct().Order(StringComparer.Ordinal)]);
    }

    // One row's glyphs before rows are assigned.
    private sealed class Group(string? conversation)
    {
        public string? Conversation { get; } = conversation;
        public long Glyphs { get; private set; }
        public long First { get; private set; } = long.MaxValue;
        public int Row { get; set; } = -1;

        public void Add(long start)
        {
            Glyphs++;
            First = Math.Min(First, start);
        }
    }
}
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
                    : LedgerFormat.Count(delta);

    private static string StateName(CacheCounterState state) => state switch
    {
        CacheCounterState.Reported => "reported",
```

with:

```csharp
                    : LedgerFormat.Count(delta);

    internal static string StateName(CacheCounterState state) => state switch
    {
        CacheCounterState.Reported => "reported",
```

New file `tests/CopilotLedger.Tests/Cli/ExportTimelineTests.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The export's timeline: rows, bars, tool ticks, night bands, the axis, truncation, and the ledger-data block.
public sealed partial class ExportTimelineTests
{
    private const string Report = "copilot-ledger-report.html";

    [Fact]
    public async Task TL1_TheTimelineDrawsEveryRequestAndToolCallBySession()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        XElement plot = ExportFiles.ById(page, "timeline-plot");
        Assert.Equal(8, Elements(plot, "rect", "span").Count());
        Assert.Equal(4, Elements(plot, "line", "tool").Count());
        Assert.Equal(2, Elements(plot, "line", "fail").Count());
        Assert.Single(Elements(plot, "rect", "night"));
        XElement[] labels = [.. page.Descendants("svg").Single(svg => (string?)svg.Attribute("class") == "timeline-labels").Elements("text")];
        Assert.Equal(
            ["1 · 11-20 07:00", "2 · 11-20 07:00", "3 · 11-21 09:00", "4 · 11-21 10:00", "5 · 11-21 11:00", "6 · 11-21 12:00"],
            labels.Select(text => text.Nodes().OfType<XText>().Single().Value));
        Assert.Equal(
            ["conv-i", "conv-j", "conv-a", "conv-b", "conv-c", "conv-d"],
            labels.Select(text => text.Element("title")!.Value));
        XElement opus = Elements(plot, "rect", "m0").First();
        Assert.Equal(("896.535", "34"), ((string)opus.Attribute("x")!, (string)opus.Attribute("y")!));
        Assert.Equal("09:00:00 · claude-opus-5.5 · (none) · 44.80 AI credits", opus.Element("title")!.Value);
        Assert.Equal("All 8 requests and 4 tool calls.", ExportFiles.ById(page, "timeline-note").Value);
    }

    [Fact]
    public async Task TL2_TheCapDrawsTheLatestRequestsAndToolCalls()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        const string Truncation =
            "The timeline draws the latest 2 of 8 requests and 2 of 4 tool calls; the totals and tables include them all.";

        XDocument full = await ExportAsync(cli, temp.Path, "--all-repos");
        CliRun run = await cli.RunAsync("export", "--all-repos", "--max-timeline-spans", "2", "--force");
        XDocument capped = ExportFiles.Load(Path.Combine(temp.Path, Report));

        Assert.Equal(
            $"Wrote copilot-ledger-report.html: 8 requests in all repositories (last 30 days).\n{Truncation}\n",
            run.Out);
        XElement plot = ExportFiles.ById(capped, "timeline-plot");
        Assert.Equal(
            ["11:05:00 · gpt-unknown · (none) · Unpriced (unknown_model)", "12:00:00 · claude-opus-5.5 · (none) · 6.00 AI credits"],
            Elements(plot, "rect", "span").Select(rect => rect.Element("title")!.Value));
        Assert.Equal(
            ["09:00:40 · bash · exit code 1", "10:00:10 · view · error"],
            plot.Descendants("line").Select(line => line.Element("title")!.Value));
        Assert.Equal(Truncation, ExportFiles.ById(capped, "timeline-note").Value);
        Assert.Equal(Truncation, ExportFiles.ById(capped, "coverage-notes").Elements("li").Last().Value);
        Assert.Equal(ExportFiles.ById(full, "summary").ToString(), ExportFiles.ById(capped, "summary").ToString());
    }

    [Fact]
    public async Task TL3_ACapOfZeroDrawsNothing()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--max-timeline-spans", "0");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        XDocument page = ExportFiles.Parse(html);
        using JsonDocument data = ExportFiles.LedgerData(html);
        Assert.EndsWith(
            "The timeline draws the latest 0 of 8 requests and 0 of 4 tool calls; the totals and tables include them all.\n",
            run.Out);
        Assert.Equal("No requests or tool calls to draw.", ExportFiles.ById(page, "timeline-note").Value);
        Assert.DoesNotContain(page.Descendants(), element => (string?)element.Attribute("id") is "timeline-plot" or "timeline-figure");
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("timeline").ValueKind);
        Assert.Equal(0, data.RootElement.GetProperty("timeZone").GetProperty("nightBandsUtc").GetArrayLength());
        Assert.True(data.RootElement.GetProperty("coverage").GetProperty("timeline").GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task TL4_NightBandsFollowDaylightSavingTime()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        await store.RecordAsync(
        [
            Entry("conv-dst", 1, new DateTimeOffset(2026, 3, 7, 20, 0, 0, TimeSpan.Zero)),
            Entry("conv-dst", 2, new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero)),
        ]);

        await ExportAsync(
            Harness(temp.Path),
            temp.Path,
            "--all-repos", "--since", "all", "--time-zone", "Test/New_York", "--generated-at", "2026-03-10T00:00:00Z");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        using JsonDocument data = ExportFiles.LedgerData(html);
        JsonElement zone = data.RootElement.GetProperty("timeZone");
        Assert.Equal(("Test/New_York", "Test/New_York"), (zone.GetProperty("requested").GetString(), zone.GetProperty("id").GetString()));
        Assert.Equal(
            [
                "2026-03-07T23:00:00.0000000+00:00|2026-03-08T10:00:00.0000000+00:00",
                "2026-03-08T22:00:00.0000000+00:00|2026-03-09T10:00:00.0000000+00:00",
            ],
            zone.GetProperty("nightBandsUtc").EnumerateArray().Select(band =>
                $"{band.GetProperty("fromUtc").GetString()}|{band.GetProperty("toUtc").GetString()}"));
        Assert.Equal(2, Elements(ExportFiles.ById(ExportFiles.Parse(html), "timeline-plot"), "rect", "night").Count());
    }

    [Fact]
    public async Task TL5_TheLedgerDataBlockHasTheFilterTimeZoneAndAxis()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        using JsonDocument data = ExportFiles.LedgerData(html);

        JsonElement root = data.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("2026-11-21T21:30:00.0000000+00:00", root.GetProperty("generatedAtUtc").GetString());
        Assert.Equal("30d", root.GetProperty("filter").GetProperty("since").GetString());
        Assert.Equal("2026-10-23T05:00:00.0000000+00:00", root.GetProperty("filter").GetProperty("fromUtc").GetString());
        Assert.Equal("2026-11-21T21:30:00.0000000+00:00", root.GetProperty("filter").GetProperty("toUtc").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("filter").GetProperty("repo").ValueKind);
        Assert.Equal("CopilotLedger-Test-0500", root.GetProperty("timeZone").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("timeZone").GetProperty("requested").ValueKind);
        Assert.Equal(1000, root.GetProperty("timeline").GetProperty("width").GetInt32());
        Assert.Equal("2026-11-20T12:00:00.0000000+00:00", root.GetProperty("timeline").GetProperty("fromUtc").GetString());
        Assert.Equal("2026-11-21T17:00:02.0000000+00:00", root.GetProperty("timeline").GetProperty("toUtc").GetString());
        Assert.Equal(
            ["api-equivalent-estimated", "api-equivalent-unpriced", "cache-unreported", "unpriced"],
            root.GetProperty("coverage").GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()));
        Assert.Equal(
            (true, 1, 0),
            (root.GetProperty("coverage").GetProperty("requestsComplete").GetBoolean(),
                root.GetProperty("coverage").GetProperty("supersededMatchedImportSpans").GetInt32(),
                root.GetProperty("coverage").GetProperty("supersededUnmatchedImportSpans").GetInt32()));
        Assert.True(root.GetProperty("coverage").GetProperty("toolHistory").GetProperty("complete").GetBoolean());
    }

    [Fact]
    public async Task TL10_LedgerDataFlagsImportsThatLiveSpansReplacedWithoutAMatch()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await ExportSeeds.SupersedeUnmatchedAsync(store);

        await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        JsonElement coverage = data.RootElement.GetProperty("coverage");
        Assert.Equal(
            (false, 1, 1),
            (coverage.GetProperty("requestsComplete").GetBoolean(),
                coverage.GetProperty("supersededMatchedImportSpans").GetInt32(),
                coverage.GetProperty("supersededUnmatchedImportSpans").GetInt32()));
        Assert.Equal(
            ["api-equivalent-estimated", "api-equivalent-unpriced", "cache-unreported", "import-superseded-unmatched", "unpriced"],
            coverage.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()));
    }

    [Fact]
    public async Task TL6_ToolCallsAreTicksWithoutDurations()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        XElement[] ticks = [.. ExportFiles.ById(page, "timeline-plot").Descendants("line")];
        Assert.Equal(4, ticks.Length);
        Assert.All(ticks, tick => Assert.Equal((string)tick.Attribute("x1")!, (string)tick.Attribute("x2")!));
        Assert.All(ticks, tick => Assert.Matches(ToolTitle(), tick.Element("title")!.Value));
    }

    [Fact]
    public async Task TL7_TheAxisShowsAtMostTwelveLabelsOfTheFinestLevel()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        using JsonDocument data = ExportFiles.LedgerData(html);

        JsonElement[] ticks = [.. data.RootElement.GetProperty("timeline").GetProperty("ticks").EnumerateArray()];
        Assert.Equal(30, ticks.Length);
        Assert.Equal([0, 1, 2], ticks.Select(tick => tick.GetProperty("level").GetInt32()).Distinct().Order());
        Assert.Equal(("0", 2, "07:00"), (ticks[0].GetProperty("x").GetRawText(), ticks[0].GetProperty("level").GetInt32(), ticks[0].GetProperty("label").GetString()));
        Assert.Equal(
            ["17.241%|12:00", "37.9303%|18:00", "58.6196%|2026-11-21", "79.3088%|06:00", "99.9981%|12:00"],
            ExportFiles.ById(ExportFiles.Parse(html), "timeline-axis-labels").Elements("span").Select(span =>
                $"{((string)span.Attribute("style")!)["left: ".Length..]}|{span.Value}"));
    }

    [Fact]
    public async Task TL8_MoreThanTwoHundredSessionsShareTheLastRow()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        DateTimeOffset start = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
        await store.RecordAsync(
        [
            .. Enumerable.Range(0, 201).Select(index =>
                Entry($"conv-{index.ToString("D3", CultureInfo.InvariantCulture)}", index, start.AddMinutes(index))),
        ]);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        string[] labels =
        [
            .. page.Descendants("svg").Single(svg => (string?)svg.Attribute("class") == "timeline-labels").Elements("text")
                .Select(text => text.Nodes().OfType<XText>().Single().Value),
        ];
        Assert.Equal(200, labels.Length);
        Assert.Equal(("1 · 11-21 05:00", "199 · 11-21 08:18", "200 · 2 other sessions"), (labels[0], labels[198], labels[199]));
        Assert.Equal("3200", (string)ExportFiles.ById(page, "timeline-plot").Attribute("height")!);
    }

    [Fact]
    public async Task TL9_RowLabelsStayDistinctWhenSessionIdsShareAPrefix()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        const string Prefix = "7d4e1c2a-9b8f-4e3d-a1b2-00000000000";
        DateTimeOffset start = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
        await store.RecordAsync(
        [
            Entry(Prefix + "1", 0, start),
            Entry(Prefix + "2", 1, start.AddSeconds(30)),
            Entry(Prefix + "3", 2, start.AddHours(1)),
        ]);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        XElement[] texts = [.. page.Descendants("svg").Single(svg => (string?)svg.Attribute("class") == "timeline-labels").Elements("text")];
        string[] labels = [.. texts.Select(text => text.Nodes().OfType<XText>().Single().Value)];
        Assert.Equal(["1 · 11-21 05:00", "2 · 11-21 05:00", "3 · 11-21 06:00"], labels);
        Assert.Equal(labels.Length, labels.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal([Prefix + "1", Prefix + "2", Prefix + "3"], texts.Select(text => text.Element("title")!.Value));
        Assert.Equal(
            ["1|" + Prefix + "1", "2|" + Prefix + "2", "3|" + Prefix + "3"],
            ExportFiles.ById(page, "table-timeline").Element("tbody")!.Elements("tr").Select(row =>
                string.Join('|', row.Elements("td").Take(2).Select(cell => cell.Value))));
    }

    private static CliHarness Harness(string root)
    {
        FakeCliPlatform platform = new();
        platform.TimeZones["Test/New_York"] = TestZones.NewYork;
        return new CliHarness(root, platform, new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));
    }

    private static async Task<XDocument> ExportAsync(CliHarness cli, string root, params string[] args)
    {
        CliRun run = await cli.RunAsync(["export", .. args]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return ExportFiles.Load(Path.Combine(root, Report));
    }

    // The elements named name under parent that have the class className.
    private static IEnumerable<XElement> Elements(XElement parent, string name, string className) =>
        parent.Descendants(name).Where(element =>
            ((string?)element.Attribute("class"))?.Split(' ').Contains(className, StringComparer.Ordinal) == true);

    // A mai-code-2 request in conversation at start.
    private static LedgerEntry Entry(string conversation, int index, DateTimeOffset start)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: start,
            requestModel: "mai-code-2",
            conversationId: conversation,
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "7e" + index.ToString("D30", CultureInfo.InvariantCulture),
            SpanId = "7e" + index.ToString("D14", CultureInfo.InvariantCulture),
        };
        return new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null);
    }

    [GeneratedRegex(@"\A[0-9]{2}:[0-9]{2}:[0-9]{2} · [^·]+ · (exit code -?[0-9]+|error|ok)\z")]
    private static partial Regex ToolTitle();
}
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/TestSupport/ExportFiles.cs`, replace:

```csharp
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
```

with:

```csharp
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/TestSupport/ExportFiles.cs`, replace:

```csharp
        page.Descendants("dt").Single(dt => dt.Value == term).ElementsAfterSelf("dd").First().Value;

    [GeneratedRegex(@"(<(?:style|script)\b[^>]*>).*?(</(?:style|script)>)", RegexOptions.Singleline)]
    private static partial Regex StyleOrScript();
```

with:

```csharp
        page.Descendants("dt").Single(dt => dt.Value == term).ElementsAfterSelf("dd").First().Value;

    // The report's one ledger-data block, parsed.
    public static JsonDocument LedgerData(string html)
    {
        const string Start = "<script type=\"application/json\" id=\"ledger-data\">";
        int start = html.IndexOf(Start, StringComparison.Ordinal);
        Assert.True(start >= 0, "The report has no ledger-data block.");
        Assert.Equal(start, html.LastIndexOf(Start, StringComparison.Ordinal));
        start += Start.Length;
        return JsonDocument.Parse(html[start..html.IndexOf("</script>", start, StringComparison.Ordinal)]);
    }

    [GeneratedRegex(@"(<(?:style|script)\b[^>]*>).*?(</(?:style|script)>)", RegexOptions.Singleline)]
    private static partial Regex StyleOrScript();
```

### Tests

- **TL1** 8 bars, 4 ticks (2 failed), and 6 rows labeled `1 · 11-20 07:00`, `2 · 11-20 07:00`, `3 · 11-21 09:00`, `4 · 11-21 10:00`, `5 · 11-21 11:00`, `6 · 11-21 12:00`, titled `conv-i`, `conv-j`, `conv-a`, `conv-b`, `conv-c`, `conv-d`; A1's bar at x 896.535; the note `All 8 requests and 4 tool calls.`; 1 night band.
- **TL2** `--max-timeline-spans 2`: bars D1 and C2, ticks T3 and T2; the note, the coverage item, and the standard-output line `The timeline draws the latest 2 of 8 requests and 2 of 4 tool calls; the totals and tables include them all.`; the cards equal the uncapped run's.
- **TL3** `--max-timeline-spans 0`: no bars or ticks, `No requests or tool calls to draw.`, and `ledger-data`'s `timeline` null.
- **TL4** Requests at 2026-03-07T20:00Z and 2026-03-09T12:00Z with `--time-zone Test/New_York`: the night bands [2026-03-07T23:00Z, 2026-03-08T10:00Z) and [2026-03-08T22:00Z, 2026-03-09T10:00Z), drawn in the plot.
- **TL5** One `ledger-data` block that parses: `schemaVersion` 1, the filter, `timeZone.id` `CopilotLedger-Test-0500`, the axis, the warnings, `requestsComplete` true, matched 1, unmatched 0, and complete tool history.
- **TL6** Every tick has `x1` equal to `x2`, and no tool title has a duration.
- **TL7** The 29-hour axis has ticks of levels 0, 1, and 2, none of level 3, and its shown labels and positions.
- **TL8** 201 requests in 201 conversations: 200 rows; `1 · 11-21 05:00`, `199 · 11-21 08:18`, and `200 · 2 other sessions`; the plot 3200 high.
- **TL9** Sessions `7d4e1c2a-9b8f-4e3d-a1b2-000000000001` to `…3`, two in the same minute: labels `1 · 11-21 05:00`, `2 · 11-21 05:00`, `3 · 11-21 06:00`, all distinct, titled with the whole IDs, and the table's `#` column 1 to 3.
- **TL10** After `SupersedeUnmatchedAsync`: `requestsComplete` false, matched 1, unmatched 1, and the warnings `api-equivalent-estimated`, `api-equivalent-unpriced`, `cache-unreported`, `import-superseded-unmatched`, `unpriced`.

### Acceptance criteria

- The timeline draws the latest requests and tool calls by session, with daylight-saving night bands, and says when it leaves some out; `ledger-data` holds no money and no `</script>`.
- `dotnet test CopilotLedger.slnx` passes **1348** tests (1338 + 10).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 7 specification: Cost Split, Cache, and Context-Growth Charts

This task draws the cost, cache, and context-growth charts. Each figure has a key, each chart a visible heading, and the channel colors lie outside the timeline's model palette, so a color never means a model in one figure and a billing channel in the next.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/ExportSvg.cs` | `CostSplit`, `CacheBars`, `Growth`, `HatchSwatch`, and the shared bar chart |
| `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs` | The cost-split section with its three lens figures, the cache charts, the context-growth section, the keys and headings, and the hatch pattern |
| `src/CopilotLedger.Cli/Export/ExportReportModel.cs` | `ChainTurns`, `ExportChainTurns`, and `ExportTurnPoint` |
| `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs` | The requests of the first 10 prompt chains with at least two requests |
| `src/CopilotLedger.Cli/Export/ExportRecords.cs` | `Shorten`, which cuts a chart label without splitting a surrogate pair |
| `src/CopilotLedger.Cli/Export/Assets/report.css` | Replaces Task 5's four channel colors; adds the key, heading, and growth styles |
| `tests/CopilotLedger.Tests/Cli/ExportChartTests.cs` | New: CH1–CH8 |

### Contracts

- **Lenses.** Three figures, never stacked or summed, each scaled to its own largest bar: billed USD (`seg-github` for GitHub AI credits, then `seg-byok` for Foundry BYOK), API-equivalent USD (`seg-api`), and local savings (`seg-local`). Each has a chart by model and a chart by agent, each under an `h3` heading `By model` or `By agent`, and a key in its `figcaption`. A row with unpriced requests gets a hatched mark after its bar; a row whose requests are all unpriced shows `Unpriced` and no bar. Local bars never carry a mark, so the local key has one entry.
- **Bars.** `viewBox="0 0 860 …"`, at most 40 rows; a bar starts at x 200 and the largest is 440 long; labels longer than 28 UTF-16 units become 27 and `…`, one fewer when the cut would split a surrogate pair; the whole name is in the bar's `title` and `aria-label`.
- **Colors** (light / dark): `--github` #8250df / #b392f0, `--byok` #7d4e24 / #a87b4f, `--ink` #424a53 / #c9d1d9 for API equivalent and cache, and `--local` #4d6b2a / #a3c46b, each at least 4.6:1 against the figure's panel; none is a model color.
- **Cache.** Hit-ratio bars by model and by agent under the same headings; a row without cache counters gets a hatched `n/a` mark, never `0.0%`; the key explains both.
- **Context growth.** The heading `Context growth`, then a chart for each of the first 10 prompt chains, in cost order, that have at least two requests: a caption `#<rank> · <yyyy-MM-dd HH:mm> · <N> requests · peak <X> input tokens` with the trace ID in its `title`, the input tokens of each request (at most 500), a line per agent invocation and model, and, with more than one, a key of `agent · model`. Without such a chain: `No prompt chain has more than one request.`

### Code (copy exactly)

Edit 1 of 5 in `src/CopilotLedger.Cli/Export/Assets/report.css`, replace:

```
  --border: #d1d9e0;
  --accent: #0969da;
  --github: #0072b2;
  --byok: #e69f00;
  --api: #56b4e9;
  --local: #009e73;
  --fail: #d55e00;
  --night: rgba(0, 0, 0, 0.07);
```

with:

```
  --border: #d1d9e0;
  --accent: #0969da;
  --github: #8250df;
  --byok: #7d4e24;
  --ink: #424a53;
  --local: #4d6b2a;
  --fail: #d55e00;
  --night: rgba(0, 0, 0, 0.07);
```

Edit 2 of 5 in `src/CopilotLedger.Cli/Export/Assets/report.css`, replace:

```
    --border: #3d444d;
    --accent: #4493f8;
    --night: rgba(255, 255, 255, 0.08);
    --window: rgba(68, 147, 248, 0.18);
```

with:

```
    --border: #3d444d;
    --accent: #4493f8;
    --github: #b392f0;
    --byok: #a87b4f;
    --ink: #c9d1d9;
    --local: #a3c46b;
    --night: rgba(255, 255, 255, 0.08);
    --window: rgba(68, 147, 248, 0.18);
```

Edit 3 of 5 in `src/CopilotLedger.Cli/Export/Assets/report.css`, replace:

```
.m6 { fill: #cc79a7; stroke: #cc79a7; background: #cc79a7; }
.m7 { fill: #999999; stroke: #999999; background: #999999; }
.seg-github { fill: var(--github); }
.seg-byok { fill: var(--byok); }
.seg-api { fill: var(--api); }
.seg-local { fill: var(--local); }
.hatch-back { fill: var(--panel); }
.hatch-line { stroke: var(--muted); stroke-width: 2; }
```

with:

```
.m6 { fill: #cc79a7; stroke: #cc79a7; background: #cc79a7; }
.m7 { fill: #999999; stroke: #999999; background: #999999; }
.seg-github { fill: var(--github); background: var(--github); stroke: var(--panel); stroke-width: 1; }
.seg-byok { fill: var(--byok); background: var(--byok); stroke: var(--panel); stroke-width: 1; }
.seg-api, .seg-cache { fill: var(--ink); background: var(--ink); }
.seg-local { fill: var(--local); background: var(--local); }
.defs { position: absolute; width: 0; height: 0; overflow: hidden; }
.hatch-back { fill: var(--panel); }
.hatch-line { stroke: var(--muted); stroke-width: 2; }
```

Edit 4 of 5 in `src/CopilotLedger.Cli/Export/Assets/report.css`, replace:

```
ul.legend { list-style: none; display: flex; flex-wrap: wrap; gap: 4px 12px; padding: 0; margin: 6px 0 0; font-weight: 400; }
.swatch { display: inline-block; width: 10px; height: 10px; margin-right: 4px; border-radius: 2px; }
fieldset.lens-toggle { display: inline-flex; gap: 12px; margin: 8px 0; border: 1px solid var(--border); border-radius: 8px; }
details.chart-table summary { cursor: pointer; color: var(--accent); margin-top: 8px; }
```

with:

```
ul.legend { list-style: none; display: flex; flex-wrap: wrap; gap: 4px 12px; padding: 0; margin: 6px 0 0; font-weight: 400; }
.swatch { display: inline-block; width: 10px; height: 10px; margin-right: 4px; border-radius: 2px; }
svg.swatch { vertical-align: -1px; }
h3.chart-heading { font-size: 0.85rem; color: var(--muted); margin: 10px 0 2px; }
fieldset.lens-toggle { display: inline-flex; gap: 12px; margin: 8px 0; border: 1px solid var(--border); border-radius: 8px; }
details.chart-table summary { cursor: pointer; color: var(--accent); margin-top: 8px; }
```

Edit 5 of 5 in `src/CopilotLedger.Cli/Export/Assets/report.css`, replace:

```
.sequence polyline { fill: none; stroke-width: 1.5; }
.sequence circle { stroke: none; }
.s0 { stroke: #0072b2; fill: #0072b2; }
.s1 { stroke: #e69f00; fill: #e69f00; }
.s2 { stroke: #009e73; fill: #009e73; }
.s3 { stroke: #cc79a7; fill: #cc79a7; }
.s4 { stroke: #56b4e9; fill: #56b4e9; }
.s5 { stroke: #d55e00; fill: #d55e00; }
.s6 { stroke: #999999; fill: #999999; }
.s7 { stroke: #f0e442; fill: #f0e442; }
.growth-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 12px; }
.axis { stroke: var(--border); }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
```

with:

```
.sequence polyline { fill: none; stroke-width: 1.5; }
.sequence circle { stroke: none; }
.s0 { stroke: #0072b2; fill: #0072b2; background: #0072b2; }
.s1 { stroke: #e69f00; fill: #e69f00; background: #e69f00; }
.s2 { stroke: #009e73; fill: #009e73; background: #009e73; }
.s3 { stroke: #cc79a7; fill: #cc79a7; background: #cc79a7; }
.s4 { stroke: #56b4e9; fill: #56b4e9; background: #56b4e9; }
.s5 { stroke: #d55e00; fill: #d55e00; background: #d55e00; }
.s6 { stroke: #999999; fill: #999999; background: #999999; }
.s7 { stroke: #f0e442; fill: #f0e442; background: #f0e442; }
.growth-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 12px; }
.growth-chart { min-width: 0; }
.growth-caption { margin: 0 0 4px; font-size: 0.85rem; font-weight: 600; font-variant-numeric: tabular-nums; }
.growth-grid svg.growth { display: block; width: 100%; height: auto; }
.axis { stroke: var(--border); }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
```

Edit 1 of 3 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
                .Select(ExportRecords.ToolCall),
        ];
        return new ExportReportModel
        {
```

with:

```csharp
                .Select(ExportRecords.ToolCall),
        ];
        IReadOnlyList<PromptChainRow> promptChains =
            await snapshot.GetTopPromptChainsAsync(filter, ExportLimits.PromptChains, cancellationToken);
        return new ExportReportModel
        {
```

Edit 2 of 3 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
            CacheByModel = await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Model, filter, cancellationToken),
            CacheByAgent = await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Agent, filter, cancellationToken),
            PromptChains =
            [
                .. (await snapshot.GetTopPromptChainsAsync(filter, ExportLimits.PromptChains, cancellationToken))
                    .Select(row => row with { Repository = Shown(row.Repository) }),
            ],
            BreakerTrips =
            [
```

with:

```csharp
            CacheByModel = await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Model, filter, cancellationToken),
            CacheByAgent = await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Agent, filter, cancellationToken),
            PromptChains = [.. promptChains.Select(row => row with { Repository = Shown(row.Repository) })],
            BreakerTrips =
            [
```

Edit 3 of 3 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
                [.. byModel.Select(row => row.Model ?? "(none)")],
                inputs.TimeZone),
        };

        string Shown(string key) => ExportRecords.Repository(key, roots, inputs.IncludeLocalPaths);
    }
}
```

with:

```csharp
                [.. byModel.Select(row => row.Model ?? "(none)")],
                inputs.TimeZone),
            ChainTurns = await ChainTurnsAsync(snapshot, filter, promptChains, cancellationToken),
        };

        string Shown(string key) => ExportRecords.Repository(key, roots, inputs.IncludeLocalPaths);
    }

    // The requests, in the export's window, of the first GrowthChains prompt chains, in cost order, that have at least two
    // requests: a single request shows no growth. A chain whose trace ID isn't 32 hexadecimal characters is skipped, as
    // --turns can't select it.
    private static async Task<IReadOnlyList<ExportChainTurns>> ChainTurnsAsync(
        LedgerExportSnapshot snapshot,
        LedgerFilter filter,
        IReadOnlyList<PromptChainRow> promptChains,
        CancellationToken cancellationToken)
    {
        List<ExportChainTurns> chains = [];
        for (int index = 0; index < promptChains.Count && chains.Count < ExportLimits.GrowthChains; index++)
        {
            string trace = promptChains[index].TraceId;
            if (promptChains[index].SpanCount < 2 || trace.Length != 32 || !trace.All(char.IsAsciiHexDigit))
            {
                continue;
            }

            IReadOnlyList<TurnTokenRow> turns =
                await snapshot.GetTurnBreakdownAsync(TurnSelector.ForTrace(trace), filter, cancellationToken);
            List<(string AgentSpan, string Model)> sequences = [];
            List<ExportTurnPoint> points = [];
            foreach (TurnTokenRow turn in turns.Take(ExportLimits.GrowthTurns))
            {
                (string, string) key = (turn.AgentSpanId ?? string.Empty, turn.Model);
                if (!sequences.Contains(key))
                {
                    sequences.Add(key);
                }

                points.Add(new ExportTurnPoint(
                    points.Count,
                    turn.StartTime,
                    turn.Agent,
                    turn.Model,
                    sequences.IndexOf(key),
                    turn.InputTokens));
            }

            chains.Add(new ExportChainTurns(
                index + 1,
                trace,
                promptChains[index].StartTime,
                turns.Count,
                turns.Count == 0 ? 0 : turns.Max(turn => turn.InputTokens),
                points));
        }

        return chains;
    }
}
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportRecords.cs`, replace:

```csharp
    public static string TimelineTruncation(ExportCoverage coverage) =>
        $"The timeline draws the latest {LedgerFormat.Count(coverage.TimelineSpans)} of {Plural(coverage.Spans, "request", "requests")} and {LedgerFormat.Count(coverage.TimelineToolCalls)} of {Plural(coverage.ToolCalls, "tool call", "tool calls")}; the totals and tables include them all.";

    // A request's shown repository: its slug, else its root's folder name (the root with includeLocalPaths), else (none).
```

with:

```csharp
    public static string TimelineTruncation(ExportCoverage coverage) =>
        $"The timeline draws the latest {LedgerFormat.Count(coverage.TimelineSpans)} of {Plural(coverage.Spans, "request", "requests")} and {LedgerFormat.Count(coverage.TimelineToolCalls)} of {Plural(coverage.ToolCalls, "tool call", "tool calls")}; the totals and tables include them all.";

    // value when it has at most length UTF-16 code units; else its first length - 1 units and "…", one fewer when the cut
    // would split a surrogate pair (HtmlEncoder would write a lone surrogate as U+FFFD).
    public static string Shorten(string value, int length)
    {
        if (value.Length <= length)
        {
            return value;
        }

        int keep = length - 1;
        if (keep > 0 && char.IsHighSurrogate(value[keep - 1]))
        {
            keep--;
        }

        return value[..keep] + "…";
    }

    // A request's shown repository: its slug, else its root's folder name (the root with includeLocalPaths), else (none).
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
    public required IReadOnlyList<BranchBudgetRow> Budgets { get; init; }               // shown keys
    public required ExportTimeline Timeline { get; init; }
}

internal sealed record ExportHeader
```

with:

```csharp
    public required IReadOnlyList<BranchBudgetRow> Budgets { get; init; }               // shown keys
    public required ExportTimeline Timeline { get; init; }
    public required IReadOnlyList<ExportChainTurns> ChainTurns { get; init; }           // the first GrowthChains chains with 2+ requests
}

internal sealed record ExportHeader
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
internal sealed record ExportAxisTick(decimal X, int Level, string Label);   // Level 0 days, 1 six hours, 2 hours, 3 ten minutes

internal sealed record ExportAuditFinding(string Severity, string Title, string Detail);
```

with:

```csharp
internal sealed record ExportAxisTick(decimal X, int Level, string Label);   // Level 0 days, 1 six hours, 2 hours, 3 ten minutes

// A prompt chain's requests for its context-growth chart: at most GrowthTurns of TotalTurns, in start order. StartTime is the
// chain's first request; PeakInputTokens is the most input of any of its requests.
internal sealed record ExportChainTurns(
    int Rank,
    string TraceId,
    DateTimeOffset StartTime,
    long TotalTurns,
    long PeakInputTokens,
    IReadOnlyList<ExportTurnPoint> Turns);

// Sequence numbers an (agent invocation, model) pair by its first request in the chain, from 0.
internal sealed record ExportTurnPoint(int Index, DateTimeOffset StartTime, string? Agent, string Model, int Sequence, long InputTokens);

internal sealed record ExportAuditFinding(string Severity, string Title, string Detail);
```

Edit 1 of 3 in `src/CopilotLedger.Cli/Export/ExportSvg.cs`, replace:

```csharp
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Export;
```

with:

```csharp
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;
```

Edit 2 of 3 in `src/CopilotLedger.Cli/Export/ExportSvg.cs`, replace:

```csharp
    }

    // The night bands, clipped to the axis, as rects of the given height.
    private static void Nights(StringBuilder html, ExportTimeline timeline, int height)
```

with:

```csharp
    }

    // A cost chart of one lens: billed (GitHub AI credits, then Foundry BYOK), api (API-equivalent), or local (savings). Each
    // chart scales to its own largest bar.
    public static void CostSplit(
        StringBuilder html,
        string id,
        string title,
        IReadOnlyList<LedgerRollupRow> rows,
        string lens,
        Func<LedgerRollupRow, string> name) =>
        Bars(
            html,
            id,
            title,
            lens switch
            {
                "billed" => "Billed USD: GitHub AI credits, then Foundry BYOK. A hatched mark means some requests have no price.",
                "api" => "USD at API list prices. A hatched mark means some requests have no API-equivalent price.",
                _ => "The cloud spend that local models avoided, in USD.",
            },
            [
                .. rows.Select(row => lens switch
                {
                    "billed" => row.UnpricedSpanCount == row.SpanCount
                        ? new Bar(name(row), [], "Unpriced", "unpriced", 12)
                        : new Bar(
                            name(row),
                            [("seg-github", row.AiCreditsUsd), ("seg-byok", row.FoundryByokUsd)],
                            LedgerFormat.Usd(row.AiCreditsUsd + row.FoundryByokUsd)
                                + (row.UnpricedSpanCount > 0 ? $" + {LedgerFormat.Count(row.UnpricedSpanCount)} unpriced" : string.Empty),
                            row.UnpricedSpanCount > 0 ? "unpriced" : null,
                            12),
                    "api" => new Bar(
                        name(row),
                        [("seg-api", row.ApiEquivalentUsd ?? 0m)],
                        LedgerFormat.ApiEquivalent(row),
                        row.ApiEquivalentUnpricedSpanCount > 0 ? "unpriced" : null,
                        12),
                    _ => new Bar(name(row), [("seg-local", row.LocalSavedUsd)], LedgerFormat.Usd(row.LocalSavedUsd), null, 0),
                }),
            ],
            null);

    // Cache hit ratios as bars of ratio × 440. A row that reported no cache counters gets a hatched mark and n/a, never 0%.
    public static void CacheBars(
        StringBuilder html,
        string id,
        string title,
        IReadOnlyList<CacheEfficiencyRow> rows,
        Func<CacheEfficiencyRow, string> name) =>
        Bars(
            html,
            id,
            title,
            "The share of input tokens read from the prompt cache, over the requests that reported cache counters.",
            [
                .. rows.Select(row => row.CacheHitRatio is decimal ratio
                    ? new Bar(
                        name(row),
                        [("seg-cache", ratio)],
                        $"{LedgerFormat.Percent(ratio)} · {LedgerFormat.Count(row.CacheReportedSpanCount)} of {LedgerFormat.Count(row.SpanCount)} reported",
                        null,
                        0)
                    : new Bar(name(row), [], row.CacheReportedSpanCount == 0 ? "n/a (no cache counters)" : "n/a", "na", 60)),
            ],
            1m);

    // A prompt chain's context growth: a caption (rank, local start, requests, peak input; the trace ID in its title), then
    // each request's input tokens in start order, a line per sequence (agent invocation and model), from x = 48 to 348 and
    // y = 130 (0) to 10 (the most input), then a key of the sequences when there's more than one.
    public static void Growth(StringBuilder html, ExportChainTurns chain, TimeZoneInfo zone)
    {
        string id = "growth-" + Int(chain.Rank);
        IReadOnlyList<ExportTurnPoint> turns = chain.Turns;
        long most = turns.Count == 0 ? 0 : turns.Max(turn => turn.InputTokens);
        int sequences = turns.Count == 0 ? 0 : turns.Max(turn => turn.Sequence) + 1;
        string X(int index) => ExportJson.Number(
            decimal.Round(48m + index * 300m / Math.Max(turns.Count - 1, 1), 2, MidpointRounding.AwayFromZero));
        string Y(long input) => ExportJson.Number(
            most == 0 ? 130m : decimal.Round(130m - input * 120m / most, 2, MidpointRounding.AwayFromZero));
        string description =
            $"{ExportRecords.Plural(chain.TotalTurns, "request", "requests")} of trace {chain.TraceId} in {ExportRecords.Plural(sequences, "sequence", "sequences")}, with at most {LedgerFormat.Count(most)} input tokens."
            + (chain.TotalTurns > turns.Count ? $" The chart draws the first {LedgerFormat.Count(turns.Count)}." : string.Empty);
        string caption =
            $"#{Int(chain.Rank)} · {TimeZoneInfo.ConvertTime(chain.StartTime, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} · {ExportRecords.Plural(chain.TotalTurns, "request", "requests")} · peak {LedgerFormat.Count(chain.PeakInputTokens)} input tokens";
        Line(html, "<div class=\"growth-chart\">");
        Line(html, $"<p class=\"growth-caption\" id=\"{id}-caption\" title=\"{Text(chain.TraceId)}\">{Text(caption)}</p>");
        Line(html, $"<svg id=\"{id}\" class=\"growth\" role=\"img\" aria-labelledby=\"{id}-title {id}-desc\" viewBox=\"0 0 360 160\">");
        Line(
            html,
            $"<title id=\"{id}-title\">Prompt chain {Int(chain.Rank)}: input tokens per request</title><desc id=\"{id}-desc\">{Text(description)}</desc>");
        Line(html, "<line class=\"axis\" x1=\"48\" y1=\"10\" x2=\"48\" y2=\"130\" /><line class=\"axis\" x1=\"48\" y1=\"130\" x2=\"348\" y2=\"130\" />");
        Line(
            html,
            $"<text x=\"44\" y=\"134\" text-anchor=\"end\">0</text><text x=\"44\" y=\"14\" text-anchor=\"end\">{LedgerFormat.Count(most)}</text><text x=\"48\" y=\"146\" text-anchor=\"middle\">1</text><text x=\"348\" y=\"146\" text-anchor=\"middle\">{LedgerFormat.Count(turns.Count)}</text>");
        List<string> names = [];
        for (int sequence = 0; sequence < sequences; sequence++)
        {
            ExportTurnPoint[] points = [.. turns.Where(turn => turn.Sequence == sequence)];
            string name = $"{points[0].Agent ?? "(none)"} · {points[0].Model}";
            names.Add(name);
            string label = $"{name}: {ExportRecords.Plural(points.Length, "request", "requests")}";
            html.Append($"<g class=\"sequence s{Int(sequence % 8)}\" tabindex=\"0\" role=\"img\" aria-label=\"{Text(label)}\">");
            html.Append($"<polyline points=\"{string.Join(' ', points.Select(point => $"{X(point.Index)},{Y(point.InputTokens)}"))}\" />");
            foreach (ExportTurnPoint point in points)
            {
                html.Append(
                    $"<circle cx=\"{X(point.Index)}\" cy=\"{Y(point.InputTokens)}\" r=\"2\"><title>{Text($"Request {Int(point.Index + 1)} · {Clock(point.StartTime, zone)} · {point.Agent ?? "(none)"} · {point.Model} · {LedgerFormat.Count(point.InputTokens)} input tokens")}</title></circle>");
            }

            Line(html, "</g>");
        }

        Line(html, "</svg>");
        if (names.Count > 1)
        {
            Line(
                html,
                "<ul class=\"legend\">"
                + string.Concat(names.Select((name, sequence) => $"<li><span class=\"swatch s{Int(sequence % 8)}\"></span>{Text(name)}</li>"))
                + "</ul>");
        }

        Line(html, "</div>");
    }

    // A key's swatch of the hatch pattern, drawn with the class of the hatched mark it explains.
    public static string HatchSwatch(string mark) =>
        $"<svg class=\"swatch\" width=\"10\" height=\"10\" aria-hidden=\"true\" focusable=\"false\"><rect class=\"{mark}\" width=\"10\" height=\"10\" fill=\"url(#hatch)\" /></svg>";

    // A horizontal bar chart of at most ExportLimits.ChartRows rows. Segments start at x = 200 and are scaled so that max
    // (by default, the longest bar) is 440 long; then come the hatched mark and the value text.
    private static void Bars(StringBuilder html, string id, string title, string description, IReadOnlyList<Bar> bars, decimal? max)
    {
        Bar[] shown = [.. bars.Take(ExportLimits.ChartRows)];
        decimal scale = max ?? (shown.Length == 0 ? 0m : shown.Max(bar => bar.Segments.Sum(segment => segment.Value)));
        string desc = bars.Count > shown.Length
            ? $"{description} Showing {LedgerFormat.Count(shown.Length)} of {LedgerFormat.Count(bars.Count)}; the table lists them all."
            : description;
        Line(
            html,
            $"<svg id=\"{id}\" class=\"bars\" role=\"img\" aria-labelledby=\"{id}-title {id}-desc\" viewBox=\"0 0 860 {Int(shown.Length * 22 + 8)}\">");
        Line(html, $"<title id=\"{id}-title\">{Text(title)}</title><desc id=\"{id}-desc\">{Text(desc)}</desc>");
        for (int row = 0; row < shown.Length; row++)
        {
            Bar bar = shown[row];
            string label = $"{bar.Name}: {bar.Value}";
            html.Append($"<g class=\"bar\" tabindex=\"0\" role=\"img\" aria-label=\"{Text(label)}\"><title>{Text(label)}</title>");
            html.Append(
                $"<text class=\"label\" x=\"192\" y=\"{Int(row * 22 + 19)}\" text-anchor=\"end\">{Text(ExportRecords.Shorten(bar.Name, 28))}</text>");
            decimal x = 200m;
            foreach ((string segment, decimal value) in bar.Segments)
            {
                decimal width = scale == 0m ? 0m : decimal.Round(value * 440m / scale, 2, MidpointRounding.AwayFromZero);
                if (width > 0m)
                {
                    html.Append(
                        $"<rect class=\"{segment}\" x=\"{ExportJson.Number(x)}\" y=\"{Int(row * 22 + 6)}\" width=\"{ExportJson.Number(width)}\" height=\"14\" />");
                    x += width;
                }
            }

            if (bar.Mark is not null)
            {
                html.Append(
                    $"<rect class=\"{bar.Mark}\" x=\"{ExportJson.Number(x)}\" y=\"{Int(row * 22 + 6)}\" width=\"{Int(bar.MarkWidth)}\" height=\"14\" fill=\"url(#hatch)\" />");
                x += bar.MarkWidth;
            }

            Line(html, $"<text class=\"value\" x=\"{ExportJson.Number(x + 6m)}\" y=\"{Int(row * 22 + 18)}\">{Text(bar.Value)}</text></g>");
        }

        Line(html, "</svg>");
    }

    // The night bands, clipped to the axis, as rects of the given height.
    private static void Nights(StringBuilder html, ExportTimeline timeline, int height)
```

Edit 3 of 3 in `src/CopilotLedger.Cli/Export/ExportSvg.cs`, replace:

```csharp
    private static string Text(string value) => HtmlEncoder.Default.Encode(value);

    private static void Line(StringBuilder html, string line) => html.Append(line).Append('\n');
}
```

with:

```csharp
    private static string Text(string value) => HtmlEncoder.Default.Encode(value);

    private static void Line(StringBuilder html, string line) => html.Append(line).Append('\n');

    // One bar: its row's name, its segments (CSS class and value), its value text, and its hatched mark's class and width.
    private sealed record Bar(string Name, IReadOnlyList<(string Class, decimal Value)> Segments, string Value, string? Mark, int MarkWidth);
}
```

Edit 1 of 5 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        Line(html, "</head>");
        Line(html, "<body>");
        WriteHeader(html, model);
        Line(html, "<main>");
```

with:

```csharp
        Line(html, "</head>");
        Line(html, "<body>");
        Line(
            html,
            "<svg class=\"defs\" width=\"0\" height=\"0\" aria-hidden=\"true\" focusable=\"false\"><defs><pattern id=\"hatch\" width=\"6\" height=\"6\" patternUnits=\"userSpaceOnUse\" patternTransform=\"rotate(45)\"><rect class=\"hatch-back\" width=\"6\" height=\"6\" /><line class=\"hatch-line\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" /></pattern></defs></svg>");
        WriteHeader(html, model);
        Line(html, "<main>");
```

Edit 2 of 5 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        WriteCoverage(html, model.Coverage, header.TimeZone);
        WriteTimeline(html, model, header.TimeZone);
        WriteCache(html, model);
        WritePromptChains(html, model, header.TimeZone);
        WriteRollups(html, model);
```

with:

```csharp
        WriteCoverage(html, model.Coverage, header.TimeZone);
        WriteTimeline(html, model, header.TimeZone);
        WriteCostSplit(html, model);
        WriteCache(html, model, header.TimeZone);
        WritePromptChains(html, model, header.TimeZone);
        WriteRollups(html, model);
```

Edit 3 of 5 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        yield return ("coverage", "Coverage");
        yield return ("timeline", "Timeline");
        yield return ("cache", "Cache efficiency");
        yield return ("prompt-chains", "Prompt chains");
```

with:

```csharp
        yield return ("coverage", "Coverage");
        yield return ("timeline", "Timeline");
        yield return ("cost-split", "Cost by model and agent");
        yield return ("cache", "Cache efficiency");
        yield return ("prompt-chains", "Prompt chains");
```

Edit 4 of 5 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
    }

    private static void WriteCache(StringBuilder html, ExportReportModel model)
    {
        SectionStart(html, "cache", "Cache efficiency");
        Table(
            html,
```

with:

```csharp
    }

    // The three lenses of cost. Each figure stands alone without the script; the script shows one at a time.
    private static void WriteCostSplit(StringBuilder html, ExportReportModel model)
    {
        SectionStart(html, "cost-split", "Cost by model and agent");
        Line(
            html,
            "<p>Billed is what GitHub (AI credits) and Microsoft Foundry (BYOK) charge. The API-equivalent and local-savings views compare the same requests with other prices; they're never added to billed spend.</p>");
        Line(html, "<fieldset class=\"lens-toggle\" id=\"lens-toggle\" hidden=\"hidden\"><legend>Show</legend>");
        Line(html, "<label><input type=\"radio\" name=\"lens\" value=\"billed\" checked=\"checked\" /> Billed</label>");
        Line(html, "<label><input type=\"radio\" name=\"lens\" value=\"api\" /> API equivalent</label>");
        Line(html, "<label><input type=\"radio\" name=\"lens\" value=\"local\" /> Local savings</label></fieldset>");
        Lens(
            html,
            "billed",
            "Billed USD: GitHub AI credits and Foundry BYOK",
            "Billed USD",
            model.ByModel,
            model.ByAgent,
            ["Requests", "Unpriced", "GitHub USD", "BYOK USD", "Billed USD"],
            row =>
            [
                Count(row.SpanCount),
                Count(row.UnpricedSpanCount),
                Money(LedgerFormat.Usd(row.AiCreditsUsd), row.AiCreditsUsd),
                Money(LedgerFormat.Usd(row.FoundryByokUsd), row.FoundryByokUsd),
                Money(LedgerFormat.Usd(row.AiCreditsUsd + row.FoundryByokUsd), row.AiCreditsUsd + row.FoundryByokUsd),
            ]);
        Lens(
            html,
            "api",
            "API-equivalent USD: the same requests at API list prices",
            "API-equivalent USD",
            model.ByModel,
            model.ByAgent,
            ["Requests", "API equiv USD", "No API price", "Estimated"],
            row =>
            [
                Count(row.SpanCount),
                Money(LedgerFormat.ApiEquivalent(row), row.ApiEquivalentUsd),
                Count(row.ApiEquivalentUnpricedSpanCount),
                Count(row.ApiEquivalentEstimatedSpanCount),
            ]);
        Lens(
            html,
            "local",
            "Local savings: the cloud spend that local models avoided",
            "Local savings",
            [.. model.ByChannelModel.Where(row => row.Channel == BillingChannel.Local)],
            [.. model.ByAgent.Where(row => row.LocalSavedUsd > 0m)],
            ["Requests", "Local saved"],
            row => [Count(row.SpanCount), Money(LedgerFormat.Usd(row.LocalSavedUsd), row.LocalSavedUsd)]);
        Line(html, "</section>");
    }

    // One lens's figure: its key, a chart by model, a chart by agent, and their tables. Local bars never carry a hatched mark,
    // so the local key has one entry.
    private static void Lens(
        StringBuilder html,
        string lens,
        string caption,
        string name,
        IReadOnlyList<LedgerRollupRow> byModel,
        IReadOnlyList<LedgerRollupRow> byAgent,
        IReadOnlyList<string> headers,
        Func<LedgerRollupRow, IReadOnlyList<Cell>> cells)
    {
        string key = lens switch
        {
            "billed" => Key("seg-github", "GitHub AI credits") + Key("seg-byok", "Foundry BYOK") + HatchKey("unpriced", "Unpriced requests"),
            "api" => Key("seg-api", "API-equivalent USD") + HatchKey("unpriced", "No API-equivalent price"),
            _ => Key("seg-local", "Cloud spend avoided"),
        };
        Line(
            html,
            $"<figure class=\"chart lens\" id=\"lens-{lens}\" data-lens=\"{lens}\"><figcaption>{caption}<ul class=\"legend\" id=\"legend-{lens}\">{key}</ul></figcaption>");
        Chart(byModel, "model", row => row.Model ?? "(none)");
        Chart(byAgent, "agent", row => row.Agent ?? "(none)");
        Line(html, "<details class=\"chart-table\"><summary>Data tables</summary>");
        Table(
            html,
            $"table-cost-{lens}-model",
            $"{name} by model",
            ["Model", .. headers],
            [.. byModel.Select(row => (IReadOnlyList<Cell>)[Name(row.Model), .. cells(row)])]);
        Table(
            html,
            $"table-cost-{lens}-agent",
            $"{name} by agent",
            ["Agent", .. headers],
            [.. byAgent.Select(row => (IReadOnlyList<Cell>)[Name(row.Agent), .. cells(row)])]);
        Line(html, "</details>");
        Line(html, "</figure>");

        void Chart(IReadOnlyList<LedgerRollupRow> rows, string by, Func<LedgerRollupRow, string> label)
        {
            Line(html, $"<h3 class=\"chart-heading\">By {by}</h3>");
            if (rows.Count == 0)
            {
                Line(html, $"<p class=\"empty\">{(lens == "local" ? "No local requests." : "No requests.")}</p>");
            }
            else
            {
                ExportSvg.CostSplit(html, $"cost-{lens}-{by}", $"{name} by {by}", rows, lens, label);
            }
        }
    }

    // A key's entry: a swatch of a CSS class, then what it means.
    private static string Key(string swatch, string meaning) => $"<li><span class=\"swatch {swatch}\"></span>{Text(meaning)}</li>";

    // A key's entry for a hatched mark of the given class.
    private static string HatchKey(string mark, string meaning) => $"<li>{ExportSvg.HatchSwatch(mark)}{Text(meaning)}</li>";

    private static void WriteCache(StringBuilder html, ExportReportModel model, TimeZoneInfo zone)
    {
        SectionStart(html, "cache", "Cache efficiency");
        Line(
            html,
            $"<figure class=\"chart\" id=\"cache-figure\"><figcaption>Prompt-cache hit ratio by model and agent<ul class=\"legend\" id=\"legend-cache\">{Key("seg-cache", "Cache hit ratio")}{HatchKey("na", "No hit ratio (n/a)")}</ul></figcaption>");
        Line(html, "<h3 class=\"chart-heading\">By model</h3>");
        ExportSvg.CacheBars(html, "cache-model", "Cache hit ratio by model", model.CacheByModel, row => row.Model ?? "(none)");
        Line(html, "<h3 class=\"chart-heading\">By agent</h3>");
        ExportSvg.CacheBars(html, "cache-agent", "Cache hit ratio by agent", model.CacheByAgent, row => row.Agent ?? "(none)");
        Table(
            html,
```

Edit 5 of 5 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
            ["Agent", .. CacheHeaders],
            [.. model.CacheByAgent.Select(row => (IReadOnlyList<Cell>)[Name(row.Agent), .. CacheCells(row)])]);
        Line(html, "</section>");
    }
```

with:

```csharp
            ["Agent", .. CacheHeaders],
            [.. model.CacheByAgent.Select(row => (IReadOnlyList<Cell>)[Name(row.Agent), .. CacheCells(row)])]);
        Line(html, "</figure>");
        Line(html, "<h3 id=\"growth-heading\">Context growth</h3>");
        if (model.ChainTurns.Count == 0)
        {
            Line(html, "<p class=\"empty\" id=\"growth-none\">No prompt chain has more than one request.</p>");
        }
        else
        {
            Line(
                html,
                "<figure class=\"chart\" id=\"growth-figure\"><figcaption>Input tokens per request in the costliest prompt chains that have more than one request</figcaption>");
            Line(html, "<div class=\"growth-grid\">");
            foreach (ExportChainTurns chain in model.ChainTurns)
            {
                ExportSvg.Growth(html, chain, zone);
            }

            Line(html, "</div>");
            Line(html, "<details class=\"chart-table\"><summary>Data table</summary>");
            Table(
                html,
                "table-growth",
                "Context growth of the costliest prompt chains",
                ["#", "Trace", "Requests", "First input", "Peak input", "Sequences"],
                [
                    .. model.ChainTurns.Select(chain => (IReadOnlyList<Cell>)
                    [
                        Count(chain.Rank),
                        Code(chain.TraceId),
                        Count(chain.TotalTurns),
                        Count(chain.Turns.Count == 0 ? 0 : chain.Turns[0].InputTokens),
                        Count(chain.PeakInputTokens),
                        Count(chain.Turns.Count == 0 ? 0 : chain.Turns.Max(turn => turn.Sequence) + 1),
                    ]),
                ]);
            Line(html, "</details>");
            Line(html, "</figure>");
        }

        Line(html, "</section>");
    }
```

New file `tests/CopilotLedger.Tests/Cli/ExportChartTests.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The export's cost lenses, cache bars, and context-growth charts, and how they read without JavaScript.
public sealed partial class ExportChartTests
{
    private const string Report = "copilot-ledger-report.html";

    [Fact]
    public async Task CH1_TheBilledLensStacksGitHubAndByokSpendWithAKey()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        Assert.Equal(
            [
                "claude-opus-5.5|seg-github 440|$0.51",
                "gpt-6.1-sol|seg-byok 272.83|$0.32",
                "mai-code-2|seg-github 131.22|$0.15",
                "qwen3.8-27b||$0.00",
                "gpt-unknown|unpriced 12|Unpriced",
            ],
            Bars(page, "cost-billed-model"));
        Assert.All(
            ExportFiles.ById(page, "lens-billed").Descendants("rect"),
            rect => Assert.Contains((string)rect.Attribute("class")!, new[] { "seg-github", "seg-byok", "unpriced" }));
        Assert.Equal("url(#hatch)", (string)ExportFiles.ById(page, "cost-billed-model").Descendants("rect").Single(rect => (string?)rect.Attribute("class") == "unpriced").Attribute("fill")!);
        XElement key = ExportFiles.ById(page, "legend-billed");
        Assert.Equal(
            ["swatch seg-github|GitHub AI credits", "swatch seg-byok|Foundry BYOK", "swatch|Unpriced requests"],
            key.Elements("li").Select(item => $"{(string)item.Elements().First().Attribute("class")!}|{item.Value}"));
        Assert.Equal(("unpriced", "url(#hatch)"), ((string)key.Descendants("rect").Single().Attribute("class")!, (string)key.Descendants("rect").Single().Attribute("fill")!));

        // The channel colors, light and dark, are none of the model colors.
        string css = StyleSheet().Match(ExportFiles.ReadReport(Path.Combine(temp.Path, Report))).Groups[1].Value;
        string[] models = [.. ModelColor().Matches(css).Select(match => match.Groups[1].Value)];
        string[] channels = [.. ChannelColor().Matches(css).Select(match => match.Groups[1].Value)];
        Assert.Equal((8, 8), (models.Length, channels.Length));
        Assert.DoesNotContain(channels, channel => models.Contains(channel));
    }

    [Fact]
    public async Task CH2_EachLensHasItsOwnScale()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        Assert.Equal(
            [
                "claude-opus-5.5|seg-api 440|~$0.51",
                "gpt-6.1-sol|unpriced 12|Unpriced",
                "mai-code-2|unpriced 12|Unpriced",
                "qwen3.8-27b|unpriced 12|Unpriced",
                "gpt-unknown|unpriced 12|Unpriced",
            ],
            Bars(page, "cost-api-model"));
        Assert.Equal(["qwen3.8-27b|seg-local 440|$0.17"], Bars(page, "cost-local-model"));
        Assert.Equal(["(none)|seg-local 440|$0.17"], Bars(page, "cost-local-agent"));
        Assert.Contains("gpt-6.1-sol|seg-byok 272.83|$0.32", Bars(page, "cost-billed-model"));
        Assert.Equal(["API-equivalent USD", "No API-equivalent price"], ExportFiles.ById(page, "legend-api").Elements("li").Select(item => item.Value));
        Assert.Equal(["Cloud spend avoided"], ExportFiles.ById(page, "legend-local").Elements("li").Select(item => item.Value));
    }

    [Fact]
    public async Task CH3_UnpricedRequestsGetAHatchedMark()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        Assert.Equal(
            ["(none)|seg-github 271.9 seg-byok 168.1 unpriced 12|$0.82 + 1 unpriced", "explore|seg-github 80.05|$0.15"],
            Bars(page, "cost-billed-agent"));
        Assert.Equal(
            ["(none)|seg-api 440 unpriced 12|~$0.51*", "explore|unpriced 12|Unpriced"],
            Bars(page, "cost-api-agent"));
    }

    [Fact]
    public async Task CH4_CacheBarsShowNotAvailableInsteadOfZero()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        Assert.Equal(
            [
                "qwen3.8-27b|na 60|n/a (no cache counters)",
                "mai-code-2|na 60|n/a (no cache counters)",
                "claude-opus-5.5|seg-cache 176|40.0% · 1 of 2 reported",
                "gpt-6.1-sol|na 60|n/a (no cache counters)",
                "gpt-unknown|na 60|n/a (no cache counters)",
            ],
            Bars(page, "cache-model"));
        Assert.DoesNotMatch(@"(?<![0-9])0\.0%", ExportFiles.ById(page, "cache-model").Value);
        Assert.Equal(["Cache hit ratio", "No hit ratio (n/a)"], ExportFiles.ById(page, "legend-cache").Elements("li").Select(item => item.Value));
        Assert.Equal("na", (string)ExportFiles.ById(page, "legend-cache").Descendants("rect").Single().Attribute("class")!);
    }

    [Fact]
    public async Task CH5_ContextGrowthChartsOnlyChainsOfMoreThanOneRequest()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        XElement figure = ExportFiles.ById(page, "growth-figure");
        Assert.Equal(
            ["growth-1", "growth-6"],
            figure.Descendants("svg").Where(svg => (string?)svg.Attribute("class") == "growth").Select(svg => (string)svg.Attribute("id")!));
        XElement[] captions = [.. figure.Descendants("p").Where(paragraph => (string?)paragraph.Attribute("class") == "growth-caption")];
        Assert.Equal(
            ["#1 · 2026-11-21 09:00 · 2 requests · peak 200,000 input tokens", "#6 · 2026-11-21 11:00 · 2 requests · peak 300,000 input tokens"],
            captions.Select(caption => caption.Value));
        Assert.Equal([ExportSeeds.A, ExportSeeds.C], captions.Select(caption => (string)caption.Attribute("title")!));
        XElement first = ExportFiles.ById(page, "growth-1");
        Assert.Equal(
            ["(none) · claude-opus-5.5: 1 request", "explore · mai-code-2: 1 request"],
            first.Elements("g").Select(sequence => (string)sequence.Attribute("aria-label")!));
        Assert.Equal(
            ["48,70", "348,10"],
            first.Descendants("circle").Select(circle => $"{(string)circle.Attribute("cx")!},{(string)circle.Attribute("cy")!}"));
        Assert.Equal(
            "Request 2 · 09:01:00 · explore · mai-code-2 · 200,000 input tokens",
            first.Descendants("circle").Last().Element("title")!.Value);
        Assert.Equal(
            ["swatch s0|(none) · claude-opus-5.5", "swatch s1|explore · mai-code-2"],
            first.ElementsAfterSelf("ul").Single().Elements("li").Select(item => $"{(string)item.Element("span")!.Attribute("class")!}|{item.Value}"));
        Assert.Equal(2, ExportFiles.ById(page, "table-growth").Element("tbody")!.Elements("tr").Count());
    }

    [Fact]
    public async Task CH6_ThePageReadsWithoutJavaScript()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        Assert.All(
            page.Descendants("svg").Where(svg => (string?)svg.Attribute("aria-hidden") != "true"),
            svg =>
            {
                Assert.Equal("img", (string?)svg.Attribute("role"));
                Assert.Equal(["title", "desc"], svg.Elements().Take(2).Select(element => element.Name.LocalName));
            });
        Assert.All(
            page.Descendants("figure"),
            figure =>
            {
                Assert.Single(figure.Elements("figcaption"));
                Assert.NotEmpty(figure.Descendants("table"));
            });
        Assert.Equal(
            ["timeline-controls", "lens-toggle"],
            page.Descendants().Where(element => element.Attribute("hidden") is not null).Select(element => (string)element.Attribute("id")!));
        Assert.Equal(3, page.Descendants("figure").Count(figure => figure.Attribute("data-lens") is not null));
        XElement[] headings = [.. page.Descendants("h3").Where(heading => (string?)heading.Attribute("class") == "chart-heading")];
        Assert.Equal(["By model", "By agent", "By model", "By agent", "By model", "By agent", "By model", "By agent"], headings.Select(heading => heading.Value));
        Assert.Equal(
            ["cost-billed-model", "cost-billed-agent", "cost-api-model", "cost-api-agent", "cost-local-model", "cost-local-agent", "cache-model", "cache-agent"],
            headings.Select(heading => (string)heading.ElementsAfterSelf().First().Attribute("id")!));
    }

    [Fact]
    public async Task CH7_LabelsAreCutBetweenCharactersNotInsideThem()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        string agent = new string('a', 26) + "😀tail";
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: ExportSeeds.GeneratedAt.AddHours(-1),
            requestModel: "claude-opus-5.5",
            agentName: agent,
            conversationId: "1234567😀xyz",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e",
            SpanId = "5e00000000000001",
        };
        await store.RecordAsync([new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null)]);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        XElement bar = ExportFiles.ById(page, "cost-billed-agent").Elements("g").Single();
        Assert.Equal(new string('a', 26) + "…", bar.Element("text")!.Value);
        Assert.Equal(agent + ": $0.01", bar.Element("title")!.Value);
        XElement label = page.Descendants("svg").Single(svg => (string?)svg.Attribute("class") == "timeline-labels").Elements("text").Single();
        Assert.Equal(("1 · 11-21 15:30", "1234567😀xyz"), (label.Nodes().OfType<XText>().Single().Value, label.Element("title")!.Value));
        Assert.Contains("&#x1F600;", html);
        Assert.DoesNotContain("&#xFFFD;", html);
        Assert.DoesNotContain('\uFFFD', html);
    }

    [Fact]
    public async Task CH8_WithoutAChainOfTwoRequestsTheReportSaysSo()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: ExportSeeds.GeneratedAt.AddHours(-1),
            requestModel: "claude-opus-5.5",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "5f5f5f5f5f5f5f5f5f5f5f5f5f5f5f5f",
            SpanId = "5f00000000000001",
        };
        await store.RecordAsync([new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null)]);

        XDocument page = await ExportAsync(temp.Path, "--all-repos");

        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        Assert.Equal(1, data.RootElement.GetProperty("coverage").GetProperty("spans").GetInt64());
        Assert.Equal("Context growth", ExportFiles.ById(page, "growth-heading").Value);
        Assert.Equal("No prompt chain has more than one request.", ExportFiles.ById(page, "growth-none").Value);
        Assert.DoesNotContain(page.Descendants(), element => (string?)element.Attribute("id") is "growth-figure" or "table-growth");
    }

    private static async Task<XDocument> ExportAsync(string root, params string[] args)
    {
        CliHarness cli = new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));
        CliRun run = await cli.RunAsync(["export", .. args]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return ExportFiles.Load(Path.Combine(root, Report));
    }

    // Each bar of the chart: "label|class width ...|value text".
    private static IReadOnlyList<string> Bars(XDocument page, string id) =>
    [
        .. ExportFiles.ById(page, id).Elements("g").Select(bar =>
            $"{bar.Elements("text").First().Value}|{string.Join(' ', bar.Elements("rect").Select(rect => $"{(string)rect.Attribute("class")!} {(string)rect.Attribute("width")!}"))}|{bar.Elements("text").Last().Value}"),
    ];

    [GeneratedRegex(@"<style>(.*?)</style>", RegexOptions.Singleline)]
    private static partial Regex StyleSheet();

    [GeneratedRegex(@"\.m[0-7] \{ fill: (#[0-9a-f]{6});")]
    private static partial Regex ModelColor();

    [GeneratedRegex(@"--(?:github|byok|ink|local): (#[0-9a-f]{6});")]
    private static partial Regex ChannelColor();
}
```

### Tests

- **CH1** The billed lens by model: `claude-opus-5.5` `seg-github` 440 `$0.51`, `gpt-6.1-sol` `seg-byok` 272.83 `$0.32`, `mai-code-2` `seg-github` 131.22 `$0.15`, `qwen3.8-27b` `$0.00`, and `gpt-unknown` `Unpriced` with a 12-unit hatched mark; the key `GitHub AI credits`, `Foundry BYOK`, and `Unpriced requests` with a hatch swatch; and the stylesheet's 8 channel colors include none of its 8 model colors.
- **CH2** The API and local lenses have their own scales (`claude-opus-5.5` 440 `~$0.51`; `qwen3.8-27b` 440 `$0.17`) and keys (`API-equivalent USD`, `No API-equivalent price`; `Cloud spend avoided`).
- **CH3** Hatched marks: the agent rows `(none)` (`$0.82 + 1 unpriced`) and, in the API lens, `~$0.51*` and `Unpriced`.
- **CH4** Cache bars by model: four rows `n/a (no cache counters)` with 60-unit hatched marks and `claude-opus-5.5` `40.0% · 1 of 2 reported`; no `0.0%`; the key `Cache hit ratio` and `No hit ratio (n/a)`.
- **CH5** Growth charts only for traces A and C (`growth-1`, `growth-6`), captioned `#1 · 2026-11-21 09:00 · 2 requests · peak 200,000 input tokens` and `#6 · 2026-11-21 11:00 · 2 requests · peak 300,000 input tokens`, titled with the trace IDs; `growth-1`'s two sequences, points, last title, and key; and 2 table rows.
- **CH6** Without JavaScript: every chart `svg` that isn't `aria-hidden` has `role="img"`, a `title`, and a `desc`; every figure has a `figcaption` and a table; only the controls and the lens switch are hidden; 8 headings `By model` and `By agent`, each followed by its chart.
- **CH7** An agent named 26 `a`s, `😀`, and `tail` gets the label of 26 `a`s and `…`, never half the emoji, and its whole name in the title; the timeline label `1 · 11-21 15:30` has the session ID `1234567😀xyz` in its title; the page has no U+FFFD.
- **CH8** A ledger whose one prompt chain has one request: the heading `Context growth`, the sentence `No prompt chain has more than one request.`, and no growth figure or table.

### Acceptance criteria

- The comparison lenses are never added to billed spend; `Unpriced` and `n/a` are hatched and labeled, never zero; every chart is identifiable without its tooltip.
- `dotnet test CopilotLedger.slnx` passes **1356** tests (1348 + 8).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 8 specification: The Script

The page is complete without JavaScript; the script adds zooming and panning on the timeline, the lens switch, and sorting. It reads only the `ledger-data` block and the page's own elements.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/Assets/report.js` | New: the script |
| `src/CopilotLedger.Cli/Export/ExportAssets.cs` | `Script`, with `\n` line endings |
| `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs` | The `<script>` element |
| `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj` | Embeds `report.js` with `LogicalName` `CopilotLedger.Cli.Export.Assets.report.js` |
| `tests/CopilotLedger.Tests/Cli/ExportAssetTests.cs` | New: EA1–EA3 |

### Features and safety rules

1. Read `ledger-data` with `JSON.parse` in `try`/`catch`; without it, or on any error, leave the page as it is.
2. **Timeline:** unhide the controls; the wheel zooms around the pointer, a drag pans, two pointers pinch, and the focused viewport takes the arrow keys, `+`, `-`, and `0`; a drag across the overview selects a range without also panning; each change sets the plot's and axis's `viewBox`, moves the overview's window, and re-renders the axis labels.
3. **Lens switch:** unhide it; on `change`, hide the figures of the other lenses.
4. **Sorting:** wrap each header of a `table.sortable` in a `button`; a click sorts the rows by the integer `data-sort` ranks, toggling the direction and `aria-sort`.

Text only through `textContent`; elements only `span` and `button`; attributes only fixed names with numbers; no `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `eval`, `Function(`, string timers, `fetch(`, `XMLHttpRequest`, `WebSocket`, `EventSource`, `sendBeacon`, `import(`, storage, cookies, `window.open`, `postMessage`, or `location`, not even in comments; no money arithmetic.

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`, replace:

```xml
  <ItemGroup>
    <EmbeddedResource Include="Export\Assets\report.css" LogicalName="CopilotLedger.Cli.Export.Assets.report.css" />
  </ItemGroup>
</Project>
```

with:

```xml
  <ItemGroup>
    <EmbeddedResource Include="Export\Assets\report.css" LogicalName="CopilotLedger.Cli.Export.Assets.report.css" />
    <EmbeddedResource Include="Export\Assets\report.js" LogicalName="CopilotLedger.Cli.Export.Assets.report.js" />
  </ItemGroup>
</Project>
```

New file `src/CopilotLedger.Cli/Export/Assets/report.js`:

```
(function () {
  'use strict';

  // gh ledger export's report script. The page is complete without it; it adds zooming and panning to the timeline, the
  // cost lens switch, and sortable tables. It reads only the ledger-data block and the page's own elements, writes text
  // only through textContent, and never contacts the network.

  var data = null;
  try {
    var block = document.getElementById('ledger-data');
    data = block ? JSON.parse(block.textContent) : null;
  } catch (error) {
    data = null;
  }

  if (data && data.timeline) {
    setUpTimeline(data.timeline);
  }

  setUpLenses();
  setUpSorting();

  function setUpTimeline(timeline) {
    var plot = document.getElementById('timeline-plot');
    var axis = document.getElementById('timeline-axis');
    var labels = document.getElementById('timeline-axis-labels');
    var overview = document.getElementById('timeline-overview');
    var frame = document.getElementById('timeline-window');
    var viewport = document.getElementById('timeline-viewport');
    var controls = document.getElementById('timeline-controls');
    var reset = document.getElementById('timeline-reset');
    if (!plot || !axis || !labels || !overview || !frame || !viewport || !controls || !reset) {
      return;
    }

    var width = timeline.width;
    var height = Number(plot.getAttribute('height'));
    var ticks = Array.isArray(timeline.ticks) ? timeline.ticks : [];
    var view = { x: 0, w: width };
    var pointers = new Map();
    var drag = null;
    var pinch = null;
    var brush = null;

    controls.hidden = false;

    function clamp(value, low, high) {
      return Math.min(Math.max(value, low), high);
    }

    function apply() {
      view.w = clamp(view.w, 0.5, width);
      view.x = clamp(view.x, 0, width - view.w);
      plot.setAttribute('viewBox', view.x + ' 0 ' + view.w + ' ' + height);
      axis.setAttribute('viewBox', view.x + ' 0 ' + view.w + ' 6');
      frame.setAttribute('x', String(view.x));
      frame.setAttribute('width', String(view.w));
      renderLabels();
    }

    // The finest level whose ticks in view, counting coarser levels, number at most 12; at level 0, every n-th day.
    function shownTicks() {
      var inView = ticks.filter(function (tick) {
        return tick.x >= view.x && tick.x <= view.x + view.w;
      });
      for (var level = 3; level >= 0; level--) {
        var shown = inView.filter(function (tick) {
          return tick.level <= level;
        });
        if (shown.length <= 12) {
          return shown;
        }
      }

      var days = inView.filter(function (tick) {
        return tick.level === 0;
      });
      var every = Math.ceil(days.length / 12);
      return days.filter(function (tick, index) {
        return index % every === 0;
      });
    }

    function renderLabels() {
      labels.textContent = '';
      shownTicks().forEach(function (tick) {
        var label = document.createElement('span');
        label.className = 'axis-label';
        label.textContent = String(tick.label);
        label.style.left = ((tick.x - view.x) / view.w * 100).toFixed(4) + '%';
        labels.appendChild(label);
      });
    }

    // The timeline unit under a client x coordinate.
    function unitAt(clientX) {
      var box = plot.getBoundingClientRect();
      return view.x + (clientX - box.left) / Math.max(box.width, 1) * view.w;
    }

    function zoom(factor, unit) {
      var ratio = (unit - view.x) / view.w;
      view.w = clamp(view.w * factor, 0.5, width);
      view.x = unit - ratio * view.w;
      apply();
    }

    function resetView() {
      view.x = 0;
      view.w = width;
      apply();
    }

    viewport.addEventListener('wheel', function (event) {
      event.preventDefault();
      zoom(event.deltaY < 0 ? 1 / 1.25 : 1.25, unitAt(event.clientX));
    }, { passive: false });

    viewport.addEventListener('pointerdown', function (event) {
      viewport.setPointerCapture(event.pointerId);
      pointers.set(event.pointerId, event.clientX);
      if (pointers.size === 1) {
        drag = { clientX: event.clientX, x: view.x };
        pinch = null;
      } else if (pointers.size === 2) {
        var positions = Array.from(pointers.values());
        drag = null;
        pinch = {
          distance: Math.max(Math.abs(positions[0] - positions[1]), 1),
          w: view.w,
          unit: unitAt((positions[0] + positions[1]) / 2)
        };
      }
    });

    viewport.addEventListener('pointermove', function (event) {
      if (!pointers.has(event.pointerId)) {
        return;
      }

      pointers.set(event.pointerId, event.clientX);
      if (pinch && pointers.size === 2) {
        var positions = Array.from(pointers.values());
        var distance = Math.max(Math.abs(positions[0] - positions[1]), 1);
        var box = plot.getBoundingClientRect();
        var middle = (positions[0] + positions[1]) / 2;
        view.w = clamp(pinch.w * pinch.distance / distance, 0.5, width);
        view.x = pinch.unit - (middle - box.left) / Math.max(box.width, 1) * view.w;
        apply();
      } else if (drag) {
        var bounds = plot.getBoundingClientRect();
        view.x = drag.x - (event.clientX - drag.clientX) / Math.max(bounds.width, 1) * view.w;
        apply();
      }
    });

    function release(event) {
      pointers.delete(event.pointerId);
      if (pointers.size < 2) {
        pinch = null;
      }

      if (pointers.size === 0) {
        drag = null;
      }
    }

    viewport.addEventListener('pointerup', release);
    viewport.addEventListener('pointercancel', release);

    viewport.addEventListener('keydown', function (event) {
      var handled = true;
      if (event.key === 'ArrowLeft') {
        view.x -= view.w * 0.1;
        apply();
      } else if (event.key === 'ArrowRight') {
        view.x += view.w * 0.1;
        apply();
      } else if (event.key === '+' || event.key === '=') {
        zoom(1 / 1.25, view.x + view.w / 2);
      } else if (event.key === '-') {
        zoom(1.25, view.x + view.w / 2);
      } else if (event.key === '0' || event.key === 'Home') {
        resetView();
      } else {
        handled = false;
      }

      if (handled) {
        event.preventDefault();
      }
    });

    reset.addEventListener('click', resetView);

    // The overview's unit under a client x coordinate.
    function overviewUnit(clientX) {
      var box = overview.getBoundingClientRect();
      return clamp((clientX - box.left) / Math.max(box.width, 1) * width, 0, width);
    }

    overview.addEventListener('pointerdown', function (event) {
      // The overview is inside the viewport: choosing a range mustn't also start a pan.
      event.stopPropagation();
      overview.setPointerCapture(event.pointerId);
      brush = overviewUnit(event.clientX);
    });

    overview.addEventListener('pointermove', function (event) {
      if (brush === null) {
        return;
      }

      var unit = overviewUnit(event.clientX);
      frame.setAttribute('x', String(Math.min(brush, unit)));
      frame.setAttribute('width', String(Math.abs(unit - brush)));
    });

    overview.addEventListener('pointerup', function (event) {
      if (brush === null) {
        return;
      }

      var unit = overviewUnit(event.clientX);
      var start = Math.min(brush, unit);
      var span = Math.abs(unit - brush);
      brush = null;
      if (span < 1) {
        view.x = unit - view.w / 2;
      } else {
        view.x = start;
        view.w = span;
      }

      apply();
    });

    overview.addEventListener('pointercancel', function () {
      brush = null;
      apply();
    });

    apply();
  }

  function setUpLenses() {
    var toggle = document.getElementById('lens-toggle');
    if (!toggle) {
      return;
    }

    var figures = Array.from(document.querySelectorAll('figure[data-lens]'));
    function show(lens) {
      figures.forEach(function (figure) {
        figure.hidden = figure.getAttribute('data-lens') !== lens;
      });
    }

    toggle.hidden = false;
    toggle.addEventListener('change', function (event) {
      if (event.target && event.target.name === 'lens') {
        show(event.target.value);
      }
    });
    var checked = toggle.querySelector('input[name="lens"]:checked');
    show(checked ? checked.value : 'billed');
  }

  // Each sortable table's headers become buttons that sort its rows by their integer data-sort ranks.
  function setUpSorting() {
    Array.from(document.querySelectorAll('table.sortable')).forEach(function (table) {
      var body = table.tBodies[0];
      var headers = Array.from(table.querySelectorAll('thead th'));
      if (!body) {
        return;
      }

      headers.forEach(function (header, column) {
        var button = document.createElement('button');
        button.type = 'button';
        while (header.firstChild) {
          button.appendChild(header.firstChild);
        }

        header.appendChild(button);
        button.addEventListener('click', function () {
          var ascending = header.getAttribute('aria-sort') !== 'ascending';
          headers.forEach(function (other) {
            other.removeAttribute('aria-sort');
          });
          header.setAttribute('aria-sort', ascending ? 'ascending' : 'descending');
          var rows = Array.from(body.rows);
          rows.sort(function (left, right) {
            var difference = Number(left.cells[column].getAttribute('data-sort')) -
              Number(right.cells[column].getAttribute('data-sort'));
            return ascending ? difference : -difference;
          });
          rows.forEach(function (row) {
            body.appendChild(row);
          });
        });
      });
    });
  }
}());
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportAssets.cs`, replace:

```csharp
namespace CopilotLedger.Cli.Export;

// The report's stylesheet, embedded in the assembly, with "\n" line endings whatever the checkout used.
internal static class ExportAssets
{
    public static string Css { get; } = Read("CopilotLedger.Cli.Export.Assets.report.css");

    private static string Read(string name)
```

with:

```csharp
namespace CopilotLedger.Cli.Export;

// The report's stylesheet and script, embedded in the assembly, with "\n" line endings whatever the checkout used.
internal static class ExportAssets
{
    public static string Css { get; } = Read("CopilotLedger.Cli.Export.Assets.report.css");

    public static string Script { get; } = Read("CopilotLedger.Cli.Export.Assets.report.js");

    private static string Read(string name)
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        html.Append("<script type=\"application/json\" id=\"ledger-data\">");
        html.Append(Encoding.UTF8.GetString(ExportJson.LedgerData(model)));
        Line(html, "</script>");
        Line(html, "</body>");
```

with:

```csharp
        html.Append("<script type=\"application/json\" id=\"ledger-data\">");
        html.Append(Encoding.UTF8.GetString(ExportJson.LedgerData(model)));
        Line(html, "</script>");
        Line(html, "<script>");
        Line(html, ExportAssets.Script.TrimEnd('\n'));
        Line(html, "</script>");
        Line(html, "</body>");
```

New file `tests/CopilotLedger.Tests/Cli/ExportAssetTests.cs`:

```csharp
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CopilotLedger.Cli;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The report is self-contained: no external references, and an inline script that only reads the page.
public sealed class ExportAssetTests
{
    private const string Report = "copilot-ledger-report.html";

    // The script may contain none of these, not even in a comment.
    private static readonly string[] Banned =
    [
        "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "eval", "Function(", "setTimeout", "setInterval",
        "fetch(", "XMLHttpRequest", "WebSocket", "EventSource", "sendBeacon", "import(", "localStorage", "sessionStorage",
        "document.cookie", "window.open", "postMessage", "location", "@import", "url(", "</script", "<!--",
    ];

    [Fact]
    public async Task EA1_ThePageReferencesNothingOutsideItself()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        string html = await ExportAsync(temp.Path);

        XDocument page = ExportFiles.Parse(html);
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xlink:href", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xmlns", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(page.Descendants().Attributes(), attribute => attribute.Name.LocalName == "src");
        Assert.All(
            page.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "href"),
            href => Assert.Matches("^#[a-z-]+$", href.Value));
        Assert.DoesNotContain(page.Descendants(), element =>
            element.Name.LocalName is "img" or "link" or "iframe" or "object" or "embed" or "base" or "form");
        foreach (string token in new[] { "@import", "fetch(", "XMLHttpRequest", "WebSocket" })
        {
            Assert.DoesNotContain(token, html, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("url(", html.Replace("url(#hatch)", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "Content-Security-Policy"));
    }

    [Fact]
    public async Task EA2_TheScriptIsTheAssetAndAvoidsUnsafeApis()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        using Stream resource = typeof(GhLedgerCli).Assembly.GetManifestResourceStream("CopilotLedger.Cli.Export.Assets.report.js")!;
        using StreamReader reader = new(resource);
        string script = reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd('\n');

        string html = await ExportAsync(temp.Path);

        Assert.EndsWith("</script>\n<script>\n" + script + "\n</script>\n</body>\n</html>\n", html);
        Assert.StartsWith("(function () {\n  'use strict';", script);
        Assert.Equal(2, Regex.Matches(html, "<script").Count);
        Assert.Equal(2, Regex.Matches(html, "</script>").Count);
        Assert.All(Banned, token => Assert.DoesNotContain(token, script, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EA3_TheElementsTheScriptUsesExistAndItsControlsStartHidden()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        string html = await ExportAsync(temp.Path);

        XDocument page = ExportFiles.Parse(html);
        string[] hooks =
        [
            "ledger-data", "timeline-plot", "timeline-axis", "timeline-axis-labels", "timeline-overview", "timeline-window",
            "timeline-viewport", "timeline-controls", "timeline-reset", "lens-toggle",
        ];
        Assert.All(hooks, id =>
        {
            Assert.Single(page.Descendants(), element => (string?)element.Attribute("id") == id);
            Assert.Contains($"'{id}'", html, StringComparison.Ordinal);
        });
        Assert.Contains(page.Descendants("table"), table => (string?)table.Attribute("class") == "data sortable");
        Assert.Equal("hidden", (string?)ExportFiles.ById(page, "timeline-controls").Attribute("hidden"));
        Assert.Equal("hidden", (string?)ExportFiles.ById(page, "lens-toggle").Attribute("hidden"));
        Assert.Equal(["billed", "api", "local"], page.Descendants("figure").Select(figure => (string?)figure.Attribute("data-lens")).OfType<string>());
    }

    private static async Task<string> ExportAsync(string root)
    {
        CliHarness cli = new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));
        CliRun run = await cli.RunAsync("export", "--all-repos");
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return ExportFiles.ReadReport(Path.Combine(root, Report));
    }
}
```

### Tests

- **EA1** No `http://`, `https://`, or protocol-relative reference anywhere, every `href` is `#…`, no `@import`, `fetch(`, `XMLHttpRequest`, or `WebSocket`, no `url(` but `url(#hatch)`, and the CSP `<meta>` exactly once.
- **EA2** The inline script is the asset, LF only, with none of the banned tokens above (compared ignoring case) and no `</script` or `<!--`.
- **EA3** The elements the script uses exist, and the controls and lens switch start hidden.

### Acceptance criteria

- The page references nothing outside itself, and the script can't load, send, or evaluate anything.
- `dotnet test CopilotLedger.slnx` passes **1359** tests (1356 + 3).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 9 specification: The Bundle

`--bundle` writes a ZIP beside the report, from the same snapshot, that a reviewer can verify and a tool can read.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/ExportBundleWriter.cs` | New: the two-pass ZIP writer |
| `src/CopilotLedger.Cli/Export/ExportJson.cs` | The manifest, the summary, prompt chains, streamed spans and tool calls, and `FileOptions` |
| `src/CopilotLedger.Cli/Export/ExportCommands.cs` | `--bundle`, its target checks, writing both files, and the second standard-output line |
| `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs` | `WritePromptChain` and `WriteCacheFields`, moved from `ReportCommands` |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | Calls them |
| `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs` | `ChannelId` becomes `internal` |
| `tests/CopilotLedger.Tests/TestSupport/ExportFiles.cs` | `ReadZip` and `LocalHeaders` |
| `tests/CopilotLedger.Tests/Cli/ExportBundleTests.cs` | New: EB1–EB9 |

### Contracts and algorithm

- **Entries**, in ordinal order and nothing else: `manifest.json`, `report.html` (the report's bytes), `telemetry/prompt-chains.json` (the top 100 chains, with `total`), `telemetry/spans.json` (every selected request, in pages of 1,000), `telemetry/summary.json`, and `telemetry/tool-calls.json` (every selected tool call, without an end time or duration).
- **Two passes.** Pass 1 runs each entry's writer into a hashing stream (SHA-256 and a byte count). The manifest lists them. Pass 2 writes `manifest.json` and the entries with `CompressionLevel.Optimal`, the entry time 1980-01-01T00:00:00+00:00, and their bytes fed to deflate in fixed 65,536-byte chunks, and checks each entry's hash again; a difference throws.
- **Files.** Both files are written to `<target>.<process ID>.tmp` beside their targets and moved into place, the ZIP first; temporary files are deleted on any error. Standard output adds `Wrote <zip name>: report.html, manifest.json, and 4 telemetry files.`
- **JSON.** Indented with `\n`, one object and one final `\n` per file, with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, as the reports' JSON, so `generatedAtUtc` reads `…+00:00` and can be pasted into `--generated-at`. Money is a JSON number written by `LedgerFormat.WriteMoney`. The manifest holds `schemaVersion`, `tool`, `toolVersion`, `generatedAtUtc`, `ratesVersions`, `apiRateCards`, `filter`, `timeZone`, `options`, `privacy`, `files`, and `coverage`.

### Code (copy exactly)

New file `src/CopilotLedger.Cli/Export/ExportBundleWriter.cs`:

```csharp
using System.IO.Compression;
using System.Security.Cryptography;

namespace CopilotLedger.Cli.Export;

// One file of the bundle and how to write it. It must write the same bytes each time it's called.
internal sealed record ExportBundleEntry(string Path, Func<Stream, CancellationToken, Task> WriteAsync);

// A bundle file's exact length and SHA-256 (64 lowercase hexadecimal characters).
internal sealed record ExportFileHash(string Path, long Bytes, string Sha256);

// The bundle: a ZIP whose entries, dates, order, and compression input don't depend on the machine or the clock.
internal static class ExportBundleWriter
{
    public static readonly DateTimeOffset EntryTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public const int ChunkBytes = 65_536;

    // Writes a new ZIP at path: manifest.json (from the other entries' hashes), then the entries in ordinal order of Path.
    // Each entry is written twice: once to hash it, then into the ZIP, where its hash is checked again. Returns the hashes.
    public static async Task<IReadOnlyList<ExportFileHash>> WriteAsync(
        string path,
        IReadOnlyList<ExportBundleEntry> entries,
        Func<IReadOnlyList<ExportFileHash>, byte[]> manifest,
        CancellationToken cancellationToken)
    {
        ExportBundleEntry[] ordered = [.. entries.OrderBy(entry => entry.Path, StringComparer.Ordinal)];
        List<ExportFileHash> hashes = [];
        foreach (ExportBundleEntry entry in ordered)
        {
            using HashingStream hashing = new(Stream.Null);
            await entry.WriteAsync(hashing, cancellationToken);
            hashes.Add(hashing.Hash(entry.Path));
        }

        byte[] manifestBytes = manifest(hashes);
        await using FileStream file = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using ZipArchive zip = new(file, ZipArchiveMode.Create, leaveOpen: true);
        await WriteEntryAsync(zip, "manifest.json", (stream, token) => stream.WriteAsync(manifestBytes, token).AsTask(), cancellationToken);
        for (int index = 0; index < ordered.Length; index++)
        {
            ExportFileHash written = await WriteEntryAsync(zip, ordered[index].Path, ordered[index].WriteAsync, cancellationToken);
            if (written != hashes[index])
            {
                throw new InvalidOperationException("An export file changed while the bundle was written.");
            }
        }

        return hashes;
    }

    private static async Task<ExportFileHash> WriteEntryAsync(
        ZipArchive zip,
        string path,
        Func<Stream, CancellationToken, Task> write,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = EntryTime;
        await using Stream compressed = entry.Open();
        ChunkingStream chunks = new(compressed, ChunkBytes);
        using HashingStream hashing = new(chunks);
        await write(hashing, cancellationToken);
        await chunks.CompleteAsync(cancellationToken);
        return hashing.Hash(path);
    }

    // A write-only stream that hashes and counts what it forwards to inner, which it doesn't own.
    private sealed class HashingStream(Stream inner) : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long _bytes;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public ExportFileHash Hash(string path) =>
            new(path, _bytes, Convert.ToHexStringLower(_hash.GetCurrentHash()));

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _hash.AppendData(buffer);
            _bytes += buffer.Length;
            inner.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _hash.AppendData(buffer.Span);
            _bytes += buffer.Length;
            await inner.WriteAsync(buffer, cancellationToken);
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hash.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    // Forwards writes to inner only in whole chunks of size bytes, and the rest at CompleteAsync, so deflate sees the same
    // calls for the same bytes however they were written. Flushing forwards nothing.
    private sealed class ChunkingStream(Stream inner, int size) : Stream
    {
        private readonly byte[] _buffer = new byte[size];
        private int _count;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            while (buffer.Length > 0)
            {
                int taken = Math.Min(size - _count, buffer.Length);
                buffer[..taken].CopyTo(_buffer.AsSpan(_count));
                _count += taken;
                buffer = buffer[taken..];
                if (_count == size)
                {
                    inner.Write(_buffer);
                    _count = 0;
                }
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (buffer.Length > 0)
            {
                int taken = Math.Min(size - _count, buffer.Length);
                buffer[..taken].CopyTo(_buffer.AsMemory(_count));
                _count += taken;
                buffer = buffer[taken..];
                if (_count == size)
                {
                    await inner.WriteAsync(_buffer, cancellationToken);
                    _count = 0;
                }
            }
        }

        // Writes the last partial chunk.
        public async Task CompleteAsync(CancellationToken cancellationToken)
        {
            if (_count > 0)
            {
                await inner.WriteAsync(_buffer.AsMemory(0, _count), cancellationToken);
                _count = 0;
            }
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
```

Edit 1 of 7 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
            }
        });
        Option<string?> repository = new("--repo") { Description = "Limit the report to this repository key." };
        Option<bool> allRepositories = new("--all-repos") { Description = "Report every repository." };
```

with:

```csharp
            }
        });
        Option<bool> bundle = new("--bundle")
        {
            Description = "Also write a ZIP with the report, a manifest, and its data as JSON, beside the report.",
        };
        Option<string?> repository = new("--repo") { Description = "Limit the report to this repository key." };
        Option<bool> allRepositories = new("--all-repos") { Description = "Report every repository." };
```

Edit 2 of 7 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        command.Add(format);
        command.Add(output);
        command.Add(repository);
        command.Add(allRepositories);
```

with:

```csharp
        command.Add(format);
        command.Add(output);
        command.Add(bundle);
        command.Add(repository);
        command.Add(allRepositories);
```

Edit 3 of 7 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
                        result.GetValue(includeLocalPaths)),
                    result.GetValue(output)!,
                    result.GetValue(force),
                    cancellationToken));
```

with:

```csharp
                        result.GetValue(includeLocalPaths)),
                    result.GetValue(output)!,
                    result.GetValue(bundle),
                    result.GetValue(force),
                    cancellationToken));
```

Edit 4 of 7 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        ExportInputs inputs,
        string output,
        bool force,
        CancellationToken cancellationToken)
    {
        string htmlPath = Path.GetFullPath(output, context.WorkingDirectory);
        if (!CanWrite(context, htmlPath, output, force))
        {
            return 1;
```

with:

```csharp
        ExportInputs inputs,
        string output,
        bool bundle,
        bool force,
        CancellationToken cancellationToken)
    {
        string htmlPath = Path.GetFullPath(output, context.WorkingDirectory);
        string? zipPath = bundle ? Path.ChangeExtension(htmlPath, ".zip") : null;
        string zipShown = Path.ChangeExtension(output, ".zip");
        if (!CanWrite(context, htmlPath, output, force) || (zipPath is not null && !CanWrite(context, zipPath, zipShown, force)))
        {
            return 1;
```

Edit 5 of 7 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        ExportReportModel model;
        byte[] html;
        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync(cancellationToken))
        {
            model = await ExportModelBuilder.BuildAsync(snapshot, inputs, cancellationToken);
            html = HtmlReportWriter.Write(model);
        }

        string htmlTemp = TemporaryPath(htmlPath);
        try
        {
            await File.WriteAllBytesAsync(htmlTemp, html, cancellationToken);
            File.Move(htmlTemp, htmlPath, overwrite: force);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine($"Could not write {output}: {ex.Message}");
            return 1;
        }
```

with:

```csharp
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        string htmlTemp = TemporaryPath(htmlPath);
        string? zipTemp = zipPath is null ? null : TemporaryPath(zipPath);
        string writing = output;
        ExportReportModel model;
        try
        {
            // Both files are written from one snapshot, then moved into place: the ZIP first, then the report.
            await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync(cancellationToken))
            {
                model = await ExportModelBuilder.BuildAsync(snapshot, inputs, cancellationToken);
                byte[] html = HtmlReportWriter.Write(model);
                await File.WriteAllBytesAsync(htmlTemp, html, cancellationToken);
                if (zipTemp is not null)
                {
                    writing = zipShown;
                    await ExportBundleWriter.WriteAsync(
                        zipTemp,
                        BundleEntries(snapshot, inputs, model, html),
                        hashes => ExportJson.Manifest(model, hashes),
                        cancellationToken);
                }
            }

            if (zipTemp is not null)
            {
                writing = zipShown;
                File.Move(zipTemp, zipPath!, overwrite: force);
            }

            writing = output;
            File.Move(htmlTemp, htmlPath, overwrite: force);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine($"Could not write {writing}: {ex.Message}");
            return 1;
        }
```

Edit 6 of 7 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        {
            DeleteIfPresent(htmlTemp);
        }

        context.Out.WriteLine(
            $"Wrote {Path.GetFileName(htmlPath)}: {ExportRecords.Plural(model.Summary.Totals.SpanCount, "request", "requests")} in {model.Header.Scope} ({model.Header.Filter.Period}).");
        if (model.Coverage.TimelineTruncated)
        {
```

with:

```csharp
        {
            DeleteIfPresent(htmlTemp);
            if (zipTemp is not null)
            {
                DeleteIfPresent(zipTemp);
            }
        }

        context.Out.WriteLine(
            $"Wrote {Path.GetFileName(htmlPath)}: {ExportRecords.Plural(model.Summary.Totals.SpanCount, "request", "requests")} in {model.Header.Scope} ({model.Header.Filter.Period}).");
        if (zipPath is not null)
        {
            context.Out.WriteLine($"Wrote {Path.GetFileName(zipPath)}: report.html, manifest.json, and 4 telemetry files.");
        }

        if (model.Coverage.TimelineTruncated)
        {
```

Edit 7 of 7 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        }

        return 0;
    }

    // False, after writing why, when path is a directory, or a file that --force doesn't allow replacing.
```

with:

```csharp
        }

        return 0;
    }

    // The bundle's files besides manifest.json. The report is the same bytes as the .html file.
    private static IReadOnlyList<ExportBundleEntry> BundleEntries(
        LedgerExportSnapshot snapshot,
        ExportInputs inputs,
        ExportReportModel model,
        byte[] html)
    {
        byte[] chains = ExportJson.PromptChains(model);
        byte[] summary = ExportJson.Summary(model);
        return
        [
            new("report.html", (stream, token) => stream.WriteAsync(html, token).AsTask()),
            new("telemetry/prompt-chains.json", (stream, token) => stream.WriteAsync(chains, token).AsTask()),
            new("telemetry/spans.json", (stream, token) => ExportJson.WriteSpansAsync(stream, snapshot, inputs, token)),
            new("telemetry/summary.json", (stream, token) => stream.WriteAsync(summary, token).AsTask()),
            new("telemetry/tool-calls.json", (stream, token) => ExportJson.WriteToolCallsAsync(stream, snapshot, inputs, token)),
        ];
    }

    // False, after writing why, when path is a directory, or a file that --force doesn't allow replacing.
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Export/ExportJson.cs`, replace:

```csharp
using System.Globalization;
using System.Text.Json;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// The export's JSON: indented with "\n" line breaks and the default encoder, which escapes <, >, and & so the JSON can sit
// inside a script element. Property order is part of the contract.
internal static class ExportJson
{
    public static readonly JsonWriterOptions Options = new() { Indented = true, NewLine = "\n" };

    // The report's ledger-data block: what the script needs, and what a reader of the page may want to check.
```

with:

```csharp
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Cli.Receipts;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// The export's JSON: indented, with "\n" line breaks. Property order is part of the contract.
internal static class ExportJson
{
    // ledger-data: the default encoder escapes <, >, and &, so the block can't end its script element.
    public static readonly JsonWriterOptions Options = new() { Indented = true, NewLine = "\n" };

    // The bundle's files, escaped only as JSON requires, as the reports' JSON is: values read as written, so generatedAtUtc
    // can be pasted into --generated-at.
    public static readonly JsonWriterOptions FileOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The report's ledger-data block: what the script needs, and what a reader of the page may want to check.
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Export/ExportJson.cs`, replace:

```csharp
    // A coordinate with at most three decimal places, invariant.
    public static string Number(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
```

with:

```csharp
    // A coordinate with at most three decimal places, invariant.
    public static string Number(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    // manifest.json: the replay inputs, provenance, privacy, the other entries' hashes, and the coverage.
    public static byte[] Manifest(ExportReportModel model, IReadOnlyList<ExportFileHash> files) => Document(writer =>
    {
        ExportHeader header = model.Header;
        writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
        writer.WriteString("tool", "gh-ledger");
        writer.WriteString("toolVersion", header.ToolVersion);
        LedgerFormat.WriteTime(writer, "generatedAtUtc", header.GeneratedAt);
        writer.WriteStartArray("ratesVersions");
        foreach (string version in header.RatesVersions)
        {
            writer.WriteStringValue(version);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("apiRateCards");
        foreach (ExportApiRateCard card in header.ApiRateCards)
        {
            writer.WriteStartObject();
            writer.WriteString("ratesVersion", card.RatesVersion);
            writer.WriteString("sha256", card.Sha256);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteFilter(writer, header.Filter);
        WriteTimeZone(writer, model);
        writer.WriteStartObject("options");
        writer.WriteNumber("maxTimelineSpans", header.MaxTimelineSpans);
        writer.WriteBoolean("includeLocalPaths", header.IncludeLocalPaths);
        writer.WriteEndObject();
        writer.WriteStartObject("privacy");
        writer.WriteBoolean("contentFree", true);
        writer.WriteBoolean("localPathsIncluded", header.IncludeLocalPaths);
        writer.WriteStartArray("excluded");
        foreach (string excluded in new[] { "prompts", "responses", "tool arguments", "tool results", "file contents", "remote URLs" })
        {
            writer.WriteStringValue(excluded);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteStartArray("files");
        foreach (ExportFileHash file in files)
        {
            writer.WriteStartObject();
            writer.WriteString("path", file.Path);
            writer.WriteNumber("bytes", file.Bytes);
            writer.WriteString("sha256", file.Sha256);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteCoverage(writer, model.Coverage);
    });

    // telemetry/summary.json: the totals and every table of the report, as the reports' --json rows.
    public static byte[] Summary(ExportReportModel model) => Document(writer =>
    {
        writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
        writer.WriteStartObject("totals");
        WriteRollupFields(writer, model.Summary.Totals);
        writer.WriteNumber("promptChains", model.Summary.PromptChains);
        writer.WriteEndObject();
        WriteRollups(writer, "byModel", model.ByModel, row => writer.WriteString("model", row.Model));
        WriteRollups(writer, "byAgent", model.ByAgent, row => writer.WriteString("agent", row.Agent));
        WriteRollups(writer, "byBranch", model.ByBranch, row =>
        {
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
        });
        WriteRollups(writer, "byChannelModel", model.ByChannelModel, row =>
        {
            writer.WriteString("channel", ReceiptMarkdown.ChannelId(row.Channel!.Value));
            writer.WriteString("model", row.Model);
        });
        WriteCaches(writer, "cacheByModel", model.CacheByModel, row => writer.WriteString("model", row.Model));
        WriteCaches(writer, "cacheByAgent", model.CacheByAgent, row => writer.WriteString("agent", row.Agent));
        writer.WriteStartArray("breakerTrips");
        foreach (BreakerTrip trip in model.BreakerTrips)
        {
            writer.WriteStartObject();
            writer.WriteString("rule", BreakerRuleNames.ToName(trip.Rule));
            writer.WriteString("traceId", trip.TraceId);
            LedgerFormat.WriteNullable(writer, "conversationId", trip.ConversationId);
            writer.WriteString("repository", trip.Repository);
            if (model.Header.IncludeLocalPaths)
            {
                LedgerFormat.WriteNullable(writer, "repositoryRoot", trip.RepositoryRoot);
            }

            writer.WriteString("branch", trip.Branch);
            LedgerFormat.WriteTime(writer, "trippedAt", trip.TrippedAt);
            writer.WriteBoolean("open", trip.ResetAt is null);
            LedgerFormat.WriteTime(writer, "resetAt", trip.ResetAt);
            WriteNullableMoney(writer, "windowCostUsd", trip.WindowCostUsd);
            LedgerFormat.WriteNullable(writer, "windowMinutes", trip.WindowMinutes);
            LedgerFormat.WriteNullable(writer, "failureCount", trip.FailureCount);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("budgets");
        foreach (BranchBudgetRow budget in model.Budgets)
        {
            writer.WriteStartObject();
            writer.WriteString("repository", budget.Repository);
            writer.WriteString("branch", budget.Branch);
            LedgerFormat.WriteMoney(writer, "budgetAiCredits", budget.BudgetAiCredits);
            LedgerFormat.WriteMoney(writer, "spentAiCredits", budget.SpentAiCredits);
            writer.WriteBoolean("exceeded", budget.Exceeded);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteCoverage(writer, model.Coverage);
    });

    // telemetry/prompt-chains.json: the top chains as top-prompts --json rows, and how many chains there are.
    public static byte[] PromptChains(ExportReportModel model) => Document(writer =>
    {
        writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
        writer.WriteNumber("limit", ExportLimits.PromptChains);
        writer.WriteNumber("total", model.Summary.PromptChains);
        writer.WriteStartArray("chains");
        for (int index = 0; index < model.PromptChains.Count; index++)
        {
            LedgerFormat.WritePromptChain(writer, model.PromptChains[index], index + 1);
        }

        writer.WriteEndArray();
    });

    // telemetry/spans.json: every selected request, read and written a page at a time.
    public static async Task WriteSpansAsync(
        Stream stream,
        LedgerExportSnapshot snapshot,
        ExportInputs inputs,
        CancellationToken cancellationToken)
    {
        await using (Utf8JsonWriter writer = new(stream, FileOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
            writer.WriteStartArray("spans");
            ExportCursor? after = null;
            IReadOnlyList<ExportSpanRow> page;
            do
            {
                page = await snapshot.GetSpansAsync(inputs.Filter, after, ExportLimits.PageSize, cancellationToken);
                foreach (ExportSpanRow row in page)
                {
                    WriteSpan(writer, ExportRecords.Span(row, inputs.IncludeLocalPaths), inputs.IncludeLocalPaths);
                }

                await writer.FlushAsync(cancellationToken);
                after = page.Count == 0
                    ? null
                    : new ExportCursor(page[^1].StartTime.ToUnixTimeMilliseconds(), page[^1].TraceId, page[^1].SpanId);
            }
            while (page.Count == ExportLimits.PageSize);

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        await stream.WriteAsync(NewLine, cancellationToken);
    }

    // telemetry/tool-calls.json: every tool call of the selected prompt chains, a page at a time.
    public static async Task WriteToolCallsAsync(
        Stream stream,
        LedgerExportSnapshot snapshot,
        ExportInputs inputs,
        CancellationToken cancellationToken)
    {
        await using (Utf8JsonWriter writer = new(stream, FileOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ExportLimits.SchemaVersion);
            writer.WriteStartArray("toolCalls");
            ExportCursor? after = null;
            IReadOnlyList<ExportToolCallRow> page;
            do
            {
                page = await snapshot.GetToolCallsAsync(inputs.Filter, after, ExportLimits.PageSize, cancellationToken);
                foreach (ExportToolCallRow row in page)
                {
                    WriteToolCall(writer, ExportRecords.ToolCall(row));
                }

                await writer.FlushAsync(cancellationToken);
                after = page.Count == 0
                    ? null
                    : new ExportCursor(page[^1].StartTime.ToUnixTimeMilliseconds(), page[^1].TraceId, page[^1].SpanId);
            }
            while (page.Count == ExportLimits.PageSize);

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        await stream.WriteAsync(NewLine, cancellationToken);
    }

    private static readonly byte[] NewLine = [(byte)'\n'];

    // One JSON object, then "\n".
    private static byte[] Document(Action<Utf8JsonWriter> write)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, FileOptions))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        stream.Write(NewLine);
        return stream.ToArray();
    }

    // What branch and agents --json rows have after their key.
    private static void WriteRollupFields(Utf8JsonWriter writer, LedgerRollupRow row)
    {
        LedgerFormat.WriteTotals(writer, row);
        LedgerFormat.WriteApiEquivalent(writer, row);
        LedgerFormat.WriteCacheCoverage(writer, row);
    }

    private static void WriteRollups(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyList<LedgerRollupRow> rows,
        Action<LedgerRollupRow> writeKey)
    {
        writer.WriteStartArray(name);
        foreach (LedgerRollupRow row in rows)
        {
            writer.WriteStartObject();
            writeKey(row);
            WriteRollupFields(writer, row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteCaches(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyList<CacheEfficiencyRow> rows,
        Action<CacheEfficiencyRow> writeKey)
    {
        writer.WriteStartArray(name);
        foreach (CacheEfficiencyRow row in rows)
        {
            writer.WriteStartObject();
            writeKey(row);
            LedgerFormat.WriteCacheFields(writer, row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteSpan(Utf8JsonWriter writer, ExportSpanRecord span, bool includeLocalPaths)
    {
        writer.WriteStartObject();
        writer.WriteString("traceId", span.TraceId);
        writer.WriteString("spanId", span.SpanId);
        LedgerFormat.WriteNullable(writer, "parentSpanId", span.ParentSpanId);
        writer.WriteString("operationName", span.OperationName);
        LedgerFormat.WriteNullable(writer, "conversationId", span.ConversationId);
        LedgerFormat.WriteNullable(writer, "agent", span.Agent);
        LedgerFormat.WriteNullable(writer, "provider", span.Provider);
        LedgerFormat.WriteNullable(writer, "requestModel", span.RequestModel);
        LedgerFormat.WriteNullable(writer, "responseModel", span.ResponseModel);
        LedgerFormat.WriteNullable(writer, "model", span.Model);
        LedgerFormat.WriteTime(writer, "startTime", span.StartTime);
        LedgerFormat.WriteTime(writer, "endTime", span.EndTime);
        writer.WriteNumber("inputTokens", span.InputTokens);
        writer.WriteNumber("cacheReadInputTokens", span.CacheReadInputTokens);
        writer.WriteNumber("cacheWriteInputTokens", span.CacheWriteInputTokens);
        writer.WriteNumber("uncachedInputTokens", span.UncachedInputTokens);
        writer.WriteNumber("outputTokens", span.OutputTokens);
        writer.WriteString("cacheReadState", span.CacheReadState);
        writer.WriteString("cacheWriteState", span.CacheWriteState);
        writer.WriteString("billingChannel", ReceiptMarkdown.ChannelId(span.BillingChannel));
        writer.WriteString("pricingStatus", span.PricingStatus);
        writer.WriteString("ratesVersion", span.RatesVersion);
        LedgerFormat.WriteMoney(writer, "costUsd", span.CostUsd);
        LedgerFormat.WriteMoney(writer, "aiCredits", span.AiCredits);
        LedgerFormat.WriteMoney(writer, "savedUsd", span.SavedUsd);
        WriteNullableMoney(writer, "apiEquivalentUsd", span.ApiEquivalentUsd);
        LedgerFormat.WriteNullable(writer, "apiEquivalentStatus", span.ApiEquivalentStatus);
        LedgerFormat.WriteNullable(writer, "apiRatesVersion", span.ApiRatesVersion);
        LedgerFormat.WriteNullable(writer, "apiRateCardSha256", span.ApiRateCardSha256);
        writer.WriteString("repository", span.Repository);
        if (includeLocalPaths)
        {
            LedgerFormat.WriteNullable(writer, "repositoryRoot", span.RepositoryRoot);
        }

        LedgerFormat.WriteNullable(writer, "branch", span.Branch);
        LedgerFormat.WriteNullable(writer, "headCommit", span.HeadCommit);
        LedgerFormat.WriteNullable(writer, "pullRequest", span.PullRequest);
        writer.WriteString("ingestSource", span.IngestSource);
        writer.WriteEndObject();
    }

    private static void WriteToolCall(Utf8JsonWriter writer, ExportToolCallRecord call)
    {
        writer.WriteStartObject();
        writer.WriteString("traceId", call.TraceId);
        writer.WriteString("spanId", call.SpanId);
        LedgerFormat.WriteNullable(writer, "conversationId", call.ConversationId);
        LedgerFormat.WriteNullable(writer, "agent", call.Agent);
        LedgerFormat.WriteNullable(writer, "toolName", call.ToolName);
        LedgerFormat.WriteTime(writer, "startTime", call.StartTime);
        LedgerFormat.WriteNullable(writer, "exitCode", call.ExitCode);
        writer.WriteString("status", call.Status);
        writer.WriteBoolean("failed", call.Failed);
        writer.WriteString("ingestSource", call.IngestSource);
        writer.WriteEndObject();
    }

    private static void WriteNullableMoney(Utf8JsonWriter writer, string name, decimal? value)
    {
        if (value is decimal amount)
        {
            LedgerFormat.WriteMoney(writer, name, amount);
        }
        else
        {
            writer.WriteNull(name);
        }
    }
}
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
    }

    private static string ChannelId(BillingChannel channel) => channel switch
    {
        BillingChannel.GitHubAiCredits => "github_ai_credits",
```

with:

```csharp
    }

    internal static string ChannelId(BillingChannel channel) => channel switch
    {
        BillingChannel.GitHubAiCredits => "github_ai_credits",
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Reporting/LedgerFormat.cs`, replace:

```csharp
    }

    public static string FinishJson(Utf8JsonWriter writer, MemoryStream stream)
    {
```

with:

```csharp
    }

    // A top-prompts --json row, which gh ledger export's prompt-chains.json shares.
    public static void WritePromptChain(Utf8JsonWriter writer, PromptChainRow row, int rank)
    {
        writer.WriteStartObject();
        writer.WriteNumber("rank", rank);
        writer.WriteString("traceId", row.TraceId);
        WriteNullable(writer, "conversationId", row.ConversationId);
        WriteTime(writer, "startTime", row.StartTime);
        WriteTime(writer, "endTime", row.EndTime);
        writer.WriteString("repository", row.Repository);
        writer.WriteString("branch", row.Branch);
        writer.WriteStartArray("agents");
        foreach (string agent in row.Agents)
        {
            writer.WriteStringValue(agent);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("models");
        foreach (string model in row.Models)
        {
            writer.WriteStringValue(model);
        }

        writer.WriteEndArray();
        WriteTotals(writer, row);
        WriteApiEquivalent(writer, row);
        writer.WriteEndObject();
    }

    // The fields of a status --cache --json row after its key, which gh ledger export's summary.json shares.
    public static void WriteCacheFields(Utf8JsonWriter writer, CacheEfficiencyRow row)
    {
        writer.WriteNumber("spans", row.SpanCount);
        writer.WriteNumber("cacheReportedSpans", row.CacheReportedSpanCount);
        writer.WriteNumber("cacheUnreportedSpans", row.CacheUnreportedSpanCount);
        writer.WriteNumber("inputTokens", row.InputTokens);
        writer.WriteNumber("outputTokens", row.OutputTokens);
        writer.WriteNumber("maxInputTokens", row.MaxInputTokens);
        WriteDecimal(writer, "averageInputTokensPerTurn", row.AverageInputTokensPerTurn);
        writer.WriteNumber("reportedInputTokens", row.ReportedInputTokens);
        writer.WriteNumber("reportedCacheReadInputTokens", row.ReportedCacheReadInputTokens);
        writer.WriteNumber("reportedCacheWriteInputTokens", row.ReportedCacheWriteInputTokens);
        WriteNullable(writer, "uncachedInputTokens", row.UncachedInputTokens);
        writer.WriteNumber("estimatedUncachedInputTokens", row.EstimatedUncachedInputTokens);
        WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
    }

    public static string FinishJson(Utf8JsonWriter writer, MemoryStream stream)
    {
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
        for (int index = 0; index < rows.Count; index++)
        {
            PromptChainRow row = rows[index];
            writer.WriteStartObject();
            writer.WriteNumber("rank", index + 1);
            writer.WriteString("traceId", row.TraceId);
            if (row.ConversationId is null)
            {
                writer.WriteNull("conversationId");
            }
            else
            {
                writer.WriteString("conversationId", row.ConversationId);
            }

            LedgerFormat.WriteTime(writer, "startTime", row.StartTime);
            LedgerFormat.WriteTime(writer, "endTime", row.EndTime);
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
            writer.WriteStartArray("agents");
            foreach (string agent in row.Agents)
            {
                writer.WriteStringValue(agent);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("models");
            foreach (string model in row.Models)
            {
                writer.WriteStringValue(model);
            }

            writer.WriteEndArray();
            LedgerFormat.WriteTotals(writer, row);
            LedgerFormat.WriteApiEquivalent(writer, row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
```

with:

```csharp
        for (int index = 0; index < rows.Count; index++)
        {
            LedgerFormat.WritePromptChain(writer, rows[index], index + 1);
        }

        writer.WriteEndArray();
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Reporting/ReportCommands.cs`, replace:

```csharp
            }

            writer.WriteNumber("spans", row.SpanCount);
            writer.WriteNumber("cacheReportedSpans", row.CacheReportedSpanCount);
            writer.WriteNumber("cacheUnreportedSpans", row.CacheUnreportedSpanCount);
            writer.WriteNumber("inputTokens", row.InputTokens);
            writer.WriteNumber("outputTokens", row.OutputTokens);
            writer.WriteNumber("maxInputTokens", row.MaxInputTokens);
            LedgerFormat.WriteDecimal(writer, "averageInputTokensPerTurn", row.AverageInputTokensPerTurn);
            writer.WriteNumber("reportedInputTokens", row.ReportedInputTokens);
            writer.WriteNumber("reportedCacheReadInputTokens", row.ReportedCacheReadInputTokens);
            writer.WriteNumber("reportedCacheWriteInputTokens", row.ReportedCacheWriteInputTokens);
            LedgerFormat.WriteNullable(writer, "uncachedInputTokens", row.UncachedInputTokens);
            writer.WriteNumber("estimatedUncachedInputTokens", row.EstimatedUncachedInputTokens);
            LedgerFormat.WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
            writer.WriteEndObject();
        }
```

with:

```csharp
            }

            LedgerFormat.WriteCacheFields(writer, row);
            writer.WriteEndObject();
        }
```

New file `tests/CopilotLedger.Tests/Cli/ExportBundleTests.cs`:

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The bundle: its entries, manifest, telemetry files, and how it's written.
public sealed class ExportBundleTests
{
    private const string Report = "copilot-ledger-report.html";
    private const string Bundle = "copilot-ledger-report.zip";

    private static readonly string[] Telemetry =
    [
        "report.html", "telemetry/prompt-chains.json", "telemetry/spans.json", "telemetry/summary.json", "telemetry/tool-calls.json",
    ];

    private static readonly HashSet<string> MoneyNames =
    [
        "aiCredits", "aiCreditsUsd", "foundryByokUsd", "localSavedUsd", "apiEquivalentUsd", "costUsd", "savedUsd", "windowCostUsd",
        "budgetAiCredits", "spentAiCredits",
    ];

    [Fact]
    public async Task EB1_TheZipHasSixEntriesWithFixedDatesInOrdinalOrder()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        CliRun run = await ExportAsync(temp.Path, "--all-repos", "--bundle");

        Assert.Equal(
            "Wrote copilot-ledger-report.html: 8 requests in all repositories (last 30 days).\n" +
            "Wrote copilot-ledger-report.zip: report.html, manifest.json, and 4 telemetry files.\n",
            run.Out);
        Assert.Equal(["manifest.json", .. Telemetry], ExportFiles.ReadZip(Path.Combine(temp.Path, Bundle)).Select(entry => entry.Name));
        Assert.All(
            ExportFiles.LocalHeaders(Path.Combine(temp.Path, Bundle)),
            header => Assert.Equal((0x0000, 0x0021, 8), (header.Time, header.Date, header.Method)));
        Assert.Equal(File.ReadAllBytes(Path.Combine(temp.Path, Report)), Entry(temp.Path, "report.html"));
    }

    [Fact]
    public async Task EB2_TheManifestHashesEveryOtherEntry()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle");

        using JsonDocument manifest = Json(temp.Path, "manifest.json");
        JsonElement root = manifest.RootElement;
        Assert.Equal((1, "gh-ledger", "0.4.1"), (root.GetProperty("schemaVersion").GetInt32(), root.GetProperty("tool").GetString(), root.GetProperty("toolVersion").GetString()));
        Assert.Equal(
            Telemetry.Select(name => $"{name}|{Entry(temp.Path, name).Length.ToString(CultureInfo.InvariantCulture)}|{Convert.ToHexStringLower(SHA256.HashData(Entry(temp.Path, name)))}"),
            root.GetProperty("files").EnumerateArray().Select(file =>
                $"{file.GetProperty("path").GetString()}|{file.GetProperty("bytes").GetInt64().ToString(CultureInfo.InvariantCulture)}|{file.GetProperty("sha256").GetString()}"));
        Assert.All(Telemetry.Skip(1), name => Assert.EndsWith("}\n", Encoding.UTF8.GetString(Entry(temp.Path, name))));
    }

    [Fact]
    public async Task EB3_MoneyIsExactAndSpansAddUpToTheTotalsWhateverTheCap()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle", "--max-timeline-spans", "1");

        using JsonDocument spans = Json(temp.Path, "telemetry/spans.json");
        using JsonDocument summary = Json(temp.Path, "telemetry/summary.json");
        foreach (string name in Telemetry.Skip(1))
        {
            using JsonDocument document = Json(temp.Path, name);
            Assert.All(MoneyValues(document.RootElement), raw =>
                Assert.Equal(decimal.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture).ToString("0.############", CultureInfo.InvariantCulture), raw));
        }

        JsonElement[] records = [.. spans.RootElement.GetProperty("spans").EnumerateArray()];
        JsonElement totals = summary.RootElement.GetProperty("totals");
        Assert.Equal(8, records.Length);
        Assert.Equal(8, totals.GetProperty("spans").GetInt64());
        Assert.Equal(1, records.Count(span => span.GetProperty("pricingStatus").GetString() is not ("priced" or "local_offload")));
        Assert.Equal(0.6595m, Sum(records, "costUsd", "github_ai_credits"));
        Assert.Equal(totals.GetProperty("aiCreditsUsd").GetDecimal(), Sum(records, "costUsd", "github_ai_credits"));
        Assert.Equal(65.95m, Sum(records, "aiCredits", null));
        Assert.Equal(totals.GetProperty("aiCredits").GetDecimal(), Sum(records, "aiCredits", null));
        Assert.Equal(0.315m, Sum(records, "costUsd", "foundry_byok"));
        Assert.Equal(totals.GetProperty("foundryByokUsd").GetDecimal(), Sum(records, "costUsd", "foundry_byok"));
        Assert.Equal(0.1725m, Sum(records, "savedUsd", null));
        Assert.Equal(totals.GetProperty("localSavedUsd").GetDecimal(), Sum(records, "savedUsd", null));
        Assert.Equal(0.508m, Sum(records, "apiEquivalentUsd", null));
        Assert.Equal(totals.GetProperty("apiEquivalentUsd").GetDecimal(), Sum(records, "apiEquivalentUsd", null));
    }

    [Fact]
    public async Task EB4_ToolCallsHaveNoEndOrDuration()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle");

        using JsonDocument document = Json(temp.Path, "telemetry/tool-calls.json");
        JsonElement[] calls = [.. document.RootElement.GetProperty("toolCalls").EnumerateArray()];
        Assert.Equal(4, calls.Length);
        Assert.All(calls, call => Assert.Equal(
            ["traceId", "spanId", "conversationId", "agent", "toolName", "startTime", "exitCode", "status", "failed", "ingestSource"],
            call.EnumerateObject().Select(property => property.Name)));
        Assert.Equal(
            ["bash|0|unset|False|import:copilot", "bash|0|unset|False|otlp", "bash|1|unset|True|otlp", "view||error|True|otlp"],
            calls.Select(call =>
                $"{call.GetProperty("toolName").GetString()}|{call.GetProperty("exitCode")}|{call.GetProperty("status").GetString()}|{call.GetProperty("failed").GetBoolean()}|{call.GetProperty("ingestSource").GetString()}"));
    }

    [Fact]
    public async Task EB5_TheManifestHoldsTheReplayInputsAndProvenance()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle");

        using JsonDocument manifest = Json(temp.Path, "manifest.json");
        using JsonDocument summary = Json(temp.Path, "telemetry/summary.json");
        JsonElement root = manifest.RootElement;
        Assert.Contains(
            "\n  \"generatedAtUtc\": \"2026-11-21T21:30:00.0000000+00:00\",\n",
            Encoding.UTF8.GetString(Entry(temp.Path, "manifest.json")),
            StringComparison.Ordinal);
        Assert.Equal(
            [
                "schemaVersion", "tool", "toolVersion", "generatedAtUtc", "ratesVersions", "apiRateCards", "filter", "timeZone", "options",
                "privacy", "files", "coverage",
            ],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("2026-11-21T21:30:00.0000000+00:00", root.GetProperty("generatedAtUtc").GetString());
        Assert.Equal(["2026-11-23", "test-fixture"], root.GetProperty("ratesVersions").EnumerateArray().Select(version => version.GetString()));
        Assert.Equal(["2026-11-23", "test-fixture"], root.GetProperty("apiRateCards").EnumerateArray().Select(card => card.GetProperty("ratesVersion").GetString()));
        JsonElement filter = root.GetProperty("filter");
        Assert.Equal(
            ("30d", "last 30 days", true, "2026-10-23T05:00:00.0000000+00:00"),
            (filter.GetProperty("since").GetString(), filter.GetProperty("period").GetString(), filter.GetProperty("allRepositories").GetBoolean(), filter.GetProperty("fromUtc").GetString()));
        JsonElement band = Assert.Single(root.GetProperty("timeZone").GetProperty("nightBandsUtc").EnumerateArray());
        Assert.Equal(
            ("2026-11-20T23:00:00.0000000+00:00", "2026-11-21T11:00:00.0000000+00:00"),
            (band.GetProperty("fromUtc").GetString(), band.GetProperty("toUtc").GetString()));
        Assert.Equal("{\n    \"maxTimelineSpans\": 20000,\n    \"includeLocalPaths\": false\n  }", root.GetProperty("options").GetRawText());
        JsonElement privacy = root.GetProperty("privacy");
        Assert.True(privacy.GetProperty("contentFree").GetBoolean());
        Assert.False(privacy.GetProperty("localPathsIncluded").GetBoolean());
        Assert.Equal(
            ["prompts", "responses", "tool arguments", "tool results", "file contents", "remote URLs"],
            privacy.GetProperty("excluded").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(summary.RootElement.GetProperty("coverage").GetRawText(), root.GetProperty("coverage").GetRawText());
        Assert.True(root.GetProperty("coverage").GetProperty("requestsComplete").GetBoolean());
    }

    [Fact]
    public async Task EB6_AnExistingBundleIsReplacedOnlyWithForce()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        File.WriteAllText(Path.Combine(temp.Path, Bundle), "old");
        CliHarness cli = Harness(temp.Path);

        CliRun refused = await cli.RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal((1, "copilot-ledger-report.zip already exists. Use --force to replace it.\n"), (refused.ExitCode, refused.Error));
        Assert.False(File.Exists(Path.Combine(temp.Path, Report)));
        Assert.Equal("old", File.ReadAllText(Path.Combine(temp.Path, Bundle)));
        CliRun replaced = await cli.RunAsync("export", "--all-repos", "--bundle", "--force");
        Assert.Equal((0, string.Empty), (replaced.ExitCode, replaced.Error));
        Assert.Equal(6, ExportFiles.ReadZip(Path.Combine(temp.Path, Bundle)).Count);
    }

    [Fact]
    public async Task EB7_AFailedBundleLeavesNeitherFileNorTemporaryFiles()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        Directory.CreateDirectory(Path.Combine(temp.Path, $"{Bundle}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.tmp"));

        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal(1, run.ExitCode);
        Assert.StartsWith("Could not write copilot-ledger-report.zip: ", run.Error);
        Assert.False(File.Exists(Path.Combine(temp.Path, Report)));
        Assert.False(File.Exists(Path.Combine(temp.Path, Bundle)));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task EB8_PromptChainsAreTheTopPromptsRows()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle");
        using JsonDocument topPrompts = JsonDocument.Parse((await cli.RunAsync("top-prompts", "--limit", "100", "--json")).Out);

        using JsonDocument chains = Json(temp.Path, "telemetry/prompt-chains.json");
        Assert.Equal((100, 6), (chains.RootElement.GetProperty("limit").GetInt32(), chains.RootElement.GetProperty("total").GetInt32()));
        Assert.Equal(
            topPrompts.RootElement.GetProperty("rows").EnumerateArray().Select(row => Shown(Flat(row), temp.Path)),
            chains.RootElement.GetProperty("chains").EnumerateArray().Select(Flat));
    }

    [Fact]
    public async Task EB9_TheSummaryRowsAreTheReportsRows()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        await ExportAsync(temp.Path, "--all-repos", "--bundle");
        using JsonDocument branch = JsonDocument.Parse((await cli.RunAsync("branch", "--all-repos", "--json")).Out);
        using JsonDocument agents = JsonDocument.Parse((await cli.RunAsync("agents", "--json")).Out);
        using JsonDocument cache = JsonDocument.Parse((await cli.RunAsync("status", "--cache", "--json")).Out);

        using JsonDocument summary = Json(temp.Path, "telemetry/summary.json");
        JsonElement root = summary.RootElement;
        Assert.Equal(branch.RootElement.GetProperty("rows").EnumerateArray().Select(row => Shown(Flat(row), temp.Path)), root.GetProperty("byBranch").EnumerateArray().Select(Flat));
        Assert.Equal(agents.RootElement.GetProperty("rows").EnumerateArray().Select(Flat), root.GetProperty("byAgent").EnumerateArray().Select(Flat));
        Assert.Equal(cache.RootElement.GetProperty("rows").EnumerateArray().Select(Flat), root.GetProperty("cacheByModel").EnumerateArray().Select(Flat));
        Assert.Equal(5, root.GetProperty("byChannelModel").GetArrayLength());
        Assert.Equal(6, root.GetProperty("totals").GetProperty("promptChains").GetInt32());
    }

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

    private static async Task<CliRun> ExportAsync(string root, params string[] args)
    {
        CliRun run = await Harness(root).RunAsync(["export", .. args]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        return run;
    }

    private static byte[] Entry(string root, string name) =>
        ExportFiles.ReadZip(Path.Combine(root, Bundle)).Single(entry => entry.Name == name).Bytes;

    private static JsonDocument Json(string root, string name) => JsonDocument.Parse(Entry(root, name));

    // The raw text of every number property with a money name, anywhere in element.
    private static IEnumerable<string> MoneyValues(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property =>
            MoneyNames.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.Number
                ? [property.Value.GetRawText()]
                : MoneyValues(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(MoneyValues),
        _ => [],
    };

    // The sum of a money field over the spans, of one billing channel when channel isn't null.
    private static decimal Sum(IEnumerable<JsonElement> spans, string name, string? channel) =>
        spans.Where(span => channel is null || span.GetProperty("billingChannel").GetString() == channel)
            .Where(span => span.GetProperty(name).ValueKind == JsonValueKind.Number)
            .Sum(span => span.GetProperty(name).GetDecimal());

    // An object's properties as "name=value" lines, values as raw JSON without layout, so files with different indentation
    // compare equal.
    private static string Flat(JsonElement row) =>
        string.Join('\n', row.EnumerateObject().Select(property => $"{property.Name}={JsonSerializer.Serialize(property.Value)}"));

    // The flat row with ExportSeeds' local root shown as its folder name, as the export shows it.
    private static string Shown(string flat, string root) =>
        flat.Replace(JsonSerializer.Serialize(ExportSeeds.LocalRoot(root)), "\"tools\"", StringComparison.Ordinal);
}
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/TestSupport/ExportFiles.cs`, replace:

```csharp
using System.Text;
using System.Text.Json;
```

with:

```csharp
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/TestSupport/ExportFiles.cs`, replace:

```csharp
    }

    [GeneratedRegex(@"(<(?:style|script)\b[^>]*>).*?(</(?:style|script)>)", RegexOptions.Singleline)]
    private static partial Regex StyleOrScript();
```

with:

```csharp
    }

    // The bundle's entries in archive order, decompressed.
    public static IReadOnlyList<(string Name, byte[] Bytes)> ReadZip(string path)
    {
        using ZipArchive zip = ZipFile.OpenRead(path);
        return
        [
            .. zip.Entries.Select(entry =>
            {
                using Stream stream = entry.Open();
                using MemoryStream bytes = new();
                stream.CopyTo(bytes);
                return (entry.FullName, bytes.ToArray());
            }),
        ];
    }

    // Each entry's local file header, in central-directory order: its name, DOS time, DOS date, and compression method.
    public static IReadOnlyList<(string Name, int Time, int Date, int Method)> LocalHeaders(string path)
    {
        byte[] zip = File.ReadAllBytes(path);
        int end = zip.Length - 22;
        Assert.Equal(0x06054b50u, BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(end)));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(end + 10));
        int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(end + 16));
        List<(string, int, int, int)> headers = [];
        for (int index = 0; index < count; index++)
        {
            Assert.Equal(0x02014b50u, BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(offset)));
            int local = (int)BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(offset + 42));
            Assert.Equal(0x04034b50u, BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(local)));
            headers.Add((
                Encoding.UTF8.GetString(zip, local + 30, BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 26))),
                BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 10)),
                BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 12)),
                BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 8))));
            offset += 46
                + BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 28))
                + BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 30))
                + BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 32));
        }

        return headers;
    }

    [GeneratedRegex(@"(<(?:style|script)\b[^>]*>).*?(</(?:style|script)>)", RegexOptions.Singleline)]
    private static partial Regex StyleOrScript();
```

### Tests

- **EB1** Exactly the six entries in ordinal order, every local header with DOS time 0x0000, date 0x0021, and method 8, and `report.html` equal to the report beside it.
- **EB2** `files` lists the five other entries in ordinal order, each with its byte count and lowercase SHA-256; no entry for the manifest.
- **EB3** Money is exact (each value's text is `WriteMoney`'s); with `--max-timeline-spans 1`, `spans.json` still has all 8 requests, and their sums equal the summary's totals: $0.6595 GitHub, 65.95 credits, $0.315 BYOK, $0.1725 saved, $0.508 API equivalent.
- **EB4** `tool-calls.json`: 4 records with exactly the documented fields, no end time or duration, and `failed` for T2 and T3.
- **EB5** The manifest's replay inputs and provenance: `generatedAtUtc` (its raw text `"2026-11-21T21:30:00.0000000+00:00"`), the filter, the time zone with its band, the options, the rate versions and cards, the privacy flags, and coverage equal to the summary's.
- **EB6** An existing ZIP is refused without `--force`, and the report isn't written either; with `--force`, both are replaced.
- **EB7** A directory at the ZIP's temporary name makes the ZIP fail: exit code 1, `Could not write copilot-ledger-report.zip: …`, neither target written, and no temporary file left.
- **EB8** `prompt-chains.json`: `limit` 100, `total` 6, and chains equal to `top-prompts --all-repos --limit 100 --json`.
- **EB9** `summary.json`'s `byBranch`, `byAgent`, and `cacheByModel` equal `branch --all-repos --json`, `agents --json`, and `status --cache --json`.

### Acceptance criteria

- The bundle holds exactly the allowlisted entries, whose hashes and sizes match, and every request whatever the timeline cap.
- `dotnet test CopilotLedger.slnx` passes **1368** tests (1359 + 9).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 10 specification: Privacy, XSS, Paths, Determinism, and Culture

This task only adds tests: they pass with Tasks 1–9 as written.

### Files

| File | Change |
|---|---|
| `tests/CopilotLedger.Tests/Cli/ExportPrivacyTests.cs` | New: EP1–EP6 |

### Code (copy exactly)

New file `tests/CopilotLedger.Tests/Cli/ExportPrivacyTests.cs`:

```csharp
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Ingestion;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Cli;

// What the export may contain: no content, no local paths unless asked, encoded text, and the same bytes for the same inputs.
public sealed class ExportPrivacyTests
{
    private const string Report = "copilot-ledger-report.html";
    private const string Bundle = "copilot-ledger-report.zip";
    private const string Conversation = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private static readonly string[] Canaries =
    [
        "CANARY-SESSION-5e2b", "CANARY-PROMPT-5e2b", "CANARY-RESPONSE-5e2b", "CANARY-SYSTEM-5e2b", "CANARY-ARGS-5e2b",
        "CANARY-RESULT-5e2b", "CANARY-PATH-5e2b", "CANARY-ERRMSG-5e2b",
    ];

    // A chat request whose span carries content attributes, and a tool call with arguments and a result.
    private const string Spans = """
        {
          "resourceSpans": [
            {
              "resource": { "attributes": [ { "key": "service.name", "value": { "stringValue": "github-copilot" } } ] },
              "scopeSpans": [
                {
                  "scope": { "name": "github.copilot" },
                  "spans": [
                    {
                      "traceId": "5b8efff798038103d269b633813fc60c",
                      "spanId": "eee19b7ec3c1b174",
                      "parentSpanId": "eee19b7ec3c1b173",
                      "name": "chat mai-code-2",
                      "startTimeUnixNano": "1795199604707000000",
                      "endTimeUnixNano": "1795199606429000000",
                      "attributes": [
                        { "key": "gen_ai.operation.name", "value": { "stringValue": "chat" } },
                        { "key": "gen_ai.provider.name", "value": { "stringValue": "github" } },
                        { "key": "gen_ai.request.model", "value": { "stringValue": "mai-code-2" } },
                        { "key": "gen_ai.conversation.id", "value": { "stringValue": "0f8fad5b-d9cb-469f-a165-70867728950e" } },
                        { "key": "gen_ai.usage.input_tokens", "value": { "intValue": "1000" } },
                        { "key": "gen_ai.usage.output_tokens", "value": { "intValue": "100" } },
                        { "key": "gen_ai.input.messages", "value": { "stringValue": "CANARY-PROMPT-5e2b" } },
                        { "key": "gen_ai.output.messages", "value": { "stringValue": "CANARY-RESPONSE-5e2b" } },
                        { "key": "gen_ai.system_instructions", "value": { "stringValue": "CANARY-SYSTEM-5e2b" } }
                      ]
                    },
                    {
                      "traceId": "5b8efff798038103d269b633813fc60c",
                      "spanId": "eee19b7ec3c1b175",
                      "parentSpanId": "eee19b7ec3c1b173",
                      "name": "execute_tool bash",
                      "startTimeUnixNano": "1795199606500000000",
                      "endTimeUnixNano": "1795199607000000000",
                      "attributes": [
                        { "key": "gen_ai.operation.name", "value": { "stringValue": "execute_tool" } },
                        { "key": "gen_ai.conversation.id", "value": { "stringValue": "0f8fad5b-d9cb-469f-a165-70867728950e" } },
                        { "key": "gen_ai.tool.name", "value": { "stringValue": "bash" } },
                        { "key": "gen_ai.tool.call.arguments", "value": { "stringValue": "CANARY-ARGS-5e2b" } },
                        { "key": "gen_ai.tool.call.result", "value": { "stringValue": "CANARY-RESULT-5e2b" } }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task EP1_NoContentOrLocalPathReachesTheExport()
    {
        using TempDirectory temp = new();
        string repo = Path.Combine(temp.Path, "CANARY-PATH-5e2b", "repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git", "refs", "heads"));
        File.WriteAllText(Path.Combine(repo, ".git", "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(Path.Combine(repo, ".git", "refs", "heads", "main"), GitFixture.Sha1 + "\n");
        string session = Path.Combine(temp.Path, "copilot-home", "session-state", Conversation);
        Directory.CreateDirectory(session);
        File.WriteAllText(Path.Combine(session, "workspace.yaml"), $"cwd: {repo}\n");
        File.WriteAllText(Path.Combine(session, "events.jsonl"), "{\"type\":\"user.message\",\"data\":{\"content\":\"CANARY-SESSION-5e2b\"}}\n");
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        LedgerIngestor ingestor = new(
            store,
            JsonRateCardCalculator.LoadEmbedded(),
            new FileSystemGitContextResolver(ceilingDirectory: temp.Path),
            new CopilotSessionWorkspaceLocator(Path.Combine(temp.Path, "copilot-home")));
        IngestResult ingested = await ingestor.IngestAsync(OtlpTraceDecoder.DecodeJson(Encoding.UTF8.GetBytes(Spans)));

        // IngestFailureLog keeps only a SqliteException's message, so this one is how a canary can reach daemon.log.
        new IngestFailureLog(line => File.AppendAllText(Path.Combine(temp.Path, "data", "daemon.log"), line + "\n"), TimeProvider.System)
            .Report(new SqliteException("CANARY-ERRMSG-5e2b", 1));
        Assert.Equal(
            "Warning: could not record spans: SqliteException: CANARY-ERRMSG-5e2b\n",
            File.ReadAllText(Path.Combine(temp.Path, "data", "daemon.log")));

        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal(1, ingested.RecordedSpans);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        Assert.StartsWith("Wrote copilot-ledger-report.html: 1 request in all repositories (last 30 days).\n", run.Out);
        using JsonDocument spans = JsonDocument.Parse(Entry(temp.Path, "telemetry/spans.json"));
        Assert.Equal("repo", Assert.Single(spans.RootElement.GetProperty("spans").EnumerateArray()).GetProperty("repository").GetString());
        string[] texts =
        [
            run.Out,
            run.Error,
            File.ReadAllText(Path.Combine(temp.Path, Report)),
            .. ExportFiles.ReadZip(Path.Combine(temp.Path, Bundle)).Select(entry => Encoding.UTF8.GetString(entry.Bytes)),
        ];
        Assert.Equal(9, texts.Length);
        Assert.All(Canaries, canary => Assert.All(texts, text => Assert.DoesNotContain(canary, text, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task EP2_LedgerTextIsEncodedEverywhere()
    {
        const string Branch = "<script>alert(1)</script>";
        const string Agent = "\"></svg><img src=x onerror=alert(1)>";
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: ExportSeeds.GeneratedAt.AddHours(-1),
            requestModel: "claude-opus-5.5",
            agentName: Agent,
            conversationId: "conv-xss",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c",
            SpanId = "3c00000000000001",
        };
        await store.RecordAsync(
        [
            new LedgerEntry(
                span,
                JsonRateCardCalculator.LoadEmbedded().Calculate(span),
                GitFixture.Feature(temp.Path) with { BranchName = Branch }),
        ]);

        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--branch", Branch, "--bundle");

        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        Assert.DoesNotContain(Branch, html, StringComparison.Ordinal);
        Assert.DoesNotContain(Agent, html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&quot;&gt;&lt;/svg&gt;&lt;img src=x onerror=alert(1)&gt;", html, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(html, "</script>").Count);
        Assert.Contains("\\u003Cscript\\u003Ealert(1)\\u003C/script\\u003E", html, StringComparison.Ordinal);
        using JsonDocument data = ExportFiles.LedgerData(html);
        Assert.Equal(Branch, data.RootElement.GetProperty("filter").GetProperty("branch").GetString());
        Assert.NotNull(ExportFiles.Parse(html).Root);
        foreach (byte[] bytes in ExportFiles.ReadZip(Path.Combine(temp.Path, Bundle))
                     .Where(entry => entry.Name.EndsWith(".json", StringComparison.Ordinal))
                     .Select(entry => entry.Bytes))
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
        }

        using JsonDocument summary = JsonDocument.Parse(
            ExportFiles.ReadZip(Path.Combine(temp.Path, Bundle)).Single(entry => entry.Name == "telemetry/summary.json").Bytes);
        Assert.Equal(Agent, summary.RootElement.GetProperty("byAgent")[0].GetProperty("agent").GetString());
        Assert.Equal(Branch, summary.RootElement.GetProperty("byBranch")[0].GetProperty("branch").GetString());
    }

    [Fact]
    public async Task EP3_LocalPathsAppearOnlyWhenIncluded()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        CliRun hidden = await cli.RunAsync("export", "--all-repos", "--bundle");
        string[] hiddenTexts = Texts(temp.Path);
        CliRun included = await cli.RunAsync("export", "--all-repos", "--bundle", "--include-local-paths", "--force");
        string[] includedTexts = Texts(temp.Path);

        Assert.Equal((0, 0), (hidden.ExitCode, included.ExitCode));
        Assert.Equal(hidden.Out, included.Out);
        Assert.All(hiddenTexts, text => Assert.DoesNotContain("private-7c1d", text, StringComparison.Ordinal));
        Assert.All(hiddenTexts, text => Assert.DoesNotContain("\"repositoryRoot\"", text, StringComparison.Ordinal));
        Assert.Contains("\"localPathsIncluded\": false", hiddenTexts[1], StringComparison.Ordinal);
        Assert.Contains("\"localPathsIncluded\": true", includedTexts[1], StringComparison.Ordinal);
        Assert.Contains("private-7c1d", includedTexts[0], StringComparison.Ordinal);
        using JsonDocument spans = JsonDocument.Parse(Entry(temp.Path, "telemetry/spans.json"));
        JsonElement d1 = spans.RootElement.GetProperty("spans").EnumerateArray().Single(span => span.GetProperty("spanId").GetString() == "d100000000000001");
        Assert.Equal(ExportSeeds.LocalRoot(temp.Path), d1.GetProperty("repositoryRoot").GetString());
        Assert.Equal(ExportSeeds.LocalRoot(temp.Path), d1.GetProperty("repository").GetString());
        using JsonDocument summary = JsonDocument.Parse(Entry(temp.Path, "telemetry/summary.json"));
        Assert.Contains(
            summary.RootElement.GetProperty("byBranch").EnumerateArray(),
            row => row.GetProperty("repository").GetString() == ExportSeeds.LocalRoot(temp.Path));
    }

    [Fact]
    public async Task EP4_TheSameInputsWriteTheSameBytesAndAReplayReproducesThem()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        ManualTimeProvider clock = new(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5);
        CliHarness cli = new(temp.Path, new FakeCliPlatform(), clock);

        await cli.RunAsync("export", "--all-repos", "--bundle");
        (byte[] Html, byte[] Zip) first = Outputs(temp.Path);
        await cli.RunAsync("export", "--all-repos", "--bundle", "--force");
        (byte[] Html, byte[] Zip) second = Outputs(temp.Path);
        using JsonDocument manifest = JsonDocument.Parse(Entry(temp.Path, "manifest.json"));
        string generatedAt = manifest.RootElement.GetProperty("generatedAtUtc").GetString()!;
        clock.Advance(TimeSpan.FromHours(1));
        NormalizedGenAiSpan later = TestSpans.Chat(
            startTime: ExportSeeds.GeneratedAt.AddMinutes(30),
            requestModel: "claude-opus-5.5",
            conversationId: "conv-later",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d",
            SpanId = "4d00000000000001",
        };
        await store.RecordAsync([new LedgerEntry(later, JsonRateCardCalculator.LoadEmbedded().Calculate(later), null)]);
        CliRun replay = await cli.RunAsync("export", "--all-repos", "--bundle", "--force", "--generated-at", generatedAt);
        (byte[] Html, byte[] Zip) replayed = Outputs(temp.Path);

        Assert.Equal("2026-11-21T21:30:00.0000000+00:00", generatedAt);
        Assert.Equal(0, replay.ExitCode);
        Assert.Equal(first.Html, second.Html);
        Assert.Equal(first.Zip, second.Zip);
        Assert.Equal(first.Html, replayed.Html);
        Assert.Equal(first.Zip, replayed.Zip);
    }

    [Fact]
    public async Task EP5_TheCultureDoesntChangeAByte()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        (byte[] Html, byte[] Zip) invariant;
        (byte[] Html, byte[] Zip) german;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            await cli.RunAsync("export", "--all-repos", "--bundle");
            invariant = Outputs(temp.Path);
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            await cli.RunAsync("export", "--all-repos", "--bundle", "--force");
            german = Outputs(temp.Path);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }

        Assert.Equal(invariant.Html, german.Html);
        Assert.Equal(invariant.Zip, german.Zip);
    }

    [Fact]
    public void EP6_TheRepositoryAddsNoPackagesOrOtherTooling()
    {
        string root = RepositoryRoot();
        string[] projects =
        [
            .. FilesUnder(Path.Combine(root, "src")).Concat(FilesUnder(Path.Combine(root, "tests")))
                .Where(file => file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)),
        ];
        string[] packages =
        [
            .. projects
                .SelectMany(project => Regex.Matches(File.ReadAllText(project), "PackageReference Include=\"([^\"]+)\"").Select(match => match.Groups[1].Value))
                .Distinct()
                .Order(StringComparer.Ordinal),
        ];
        string[] files =
        [
            .. new[] { "src", "tests", "docs", ".github" }.SelectMany(folder => FilesUnder(Path.Combine(root, folder))),
            .. Directory.GetFiles(root),
        ];

        Assert.Equal(6, projects.Length);
        Assert.Equal(
            [
                "Microsoft.AspNetCore.TestHost", "Microsoft.Data.Sqlite", "Microsoft.NET.Test.Sdk", "ModelContextProtocol",
                "System.CommandLine", "coverlet.collector", "xunit", "xunit.runner.visualstudio",
            ],
            packages);
        Assert.DoesNotContain(files, file =>
            Path.GetFileName(file) is "package.json" or "requirements.txt" || file.EndsWith(".py", StringComparison.OrdinalIgnoreCase));
    }

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

    private static byte[] Entry(string root, string name) =>
        ExportFiles.ReadZip(Path.Combine(root, Bundle)).Single(entry => entry.Name == name).Bytes;

    private static (byte[] Html, byte[] Zip) Outputs(string root) =>
        (File.ReadAllBytes(Path.Combine(root, Report)), File.ReadAllBytes(Path.Combine(root, Bundle)));

    // The report, then each bundle entry, as text.
    private static string[] Texts(string root) =>
    [
        File.ReadAllText(Path.Combine(root, Report)),
        .. ExportFiles.ReadZip(Path.Combine(root, Bundle)).Select(entry => Encoding.UTF8.GetString(entry.Bytes)),
    ];

    // The files under folder, recursively, skipping bin and obj folders.
    private static IEnumerable<string> FilesUnder(string folder) =>
        !Directory.Exists(folder)
            ? []
            : Directory.GetFiles(folder).Concat(Directory.GetDirectories(folder)
                .Where(directory => Path.GetFileName(directory) is not ("bin" or "obj"))
                .SelectMany(FilesUnder));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CopilotLedger.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
```

### Tests

- **EP1** Canaries everywhere export must not look: a session folder's `events.jsonl` and `workspace.yaml` (whose `cwd` is a repository under `CANARY-PATH-5e2b`); an OTLP request through the real decoder, normalizer, session locator, and Git resolver, with content attributes on its chat span and its tool call; and `daemon.log`, which `IngestFailureLog` writes with a `SqliteException` whose message is `CANARY-ERRMSG-5e2b` (it keeps only a `SqliteException`'s message), checked to hold exactly that line before the export runs. `export --all-repos --bundle` shows 1 request in repository `repo`, and no canary is in the report, any entry of the ZIP, standard output, or standard error.
- **EP2** A branch `<script>alert(1)</script>` and an agent `"></svg><img src=x onerror=alert(1)>` appear only HTML-encoded; the page has exactly two `</script>`; `ledger-data` parses and holds the raw branch, written `\u003Cscript\u003E…`; every JSON entry of the ZIP parses, and `summary.json` holds the raw agent name.
- **EP3** By default, no part of a local root is in any file, entry, or output, `localPathsIncluded` is false, and no `repositoryRoot` is written; with `--include-local-paths`, D1's `repositoryRoot`, the by-branch key, and the page show the root, `localPathsIncluded` is true, and standard output doesn't change.
- **EP4** Two runs at the same clock write the same report and ZIP bytes; after the clock moves on and a later request is recorded, `--generated-at <generatedAtUtc> --force` writes the same bytes again.
- **EP5** Under `InvariantCulture` and under `de-DE`, the same report and ZIP bytes.
- **EP6** The `.csproj` files under `src` and `tests` reference exactly the eight packages they did before, and no `package.json`, `*.py`, or `requirements.txt` is in `src`, `tests`, `docs`, or `.github` (skipping `bin` and `obj`) or among the repository root's own files.

### Acceptance criteria

- No content, local path, or failure message reaches an export; ledger text is always encoded; the same inputs write the same bytes in any culture.
- `dotnet test CopilotLedger.slnx` passes **1374** tests (1368 + 6).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 11 specification: The Reference and the Allowlist

### Files

| File | Change |
|---|---|
| `docs/cli.md` | New "Export a report" after "Pull request receipts", with what the report shows, the bundle, and "What export includes" (the allowlist, between `<!-- export-allowlist -->` and `<!-- /export-allowlist -->`); a sentence under "Privacy"; and a troubleshooting entry for `Invalid --time-zone value` |
| `tests/CopilotLedger.Tests/Cli/ExportAllowlistTests.cs` | New: EL1–EL3 |

### The allowlist rule

One row per JSON object: `| <files> | <object> | <fields> |`, where `<files>` is one or more of `manifest.json`, `report.html` (its `ledger-data` block), and the telemetry files, when the object is the same in each; `<object>` is `.` for a file's top level, a property name for a nested object, and `name[]` for an array's objects; and `<fields>` is the object's property names in order. `repositoryRoot` is written only with `--include-local-paths`.

### Code (copy exactly)

Edit 1 of 3 in `docs/cli.md`, replace:

````markdown
`export-pr` needs a branch with at least one commit and a GitHub remote named `origin`, and it refuses a receipt over 65,536 bytes, the most the action posts.

## MCP server

`gh ledger mcp` runs an MCP server on standard input and output, so Copilot can read the ledger and see the circuit breaker. Register it with Copilot CLI using the gh-ledger binary's path, which for the extension is shown under [Get gh-ledger](#get-gh-ledger). In PowerShell on Windows:
````

with:

````markdown
`export-pr` needs a branch with at least one commit and a GitHub remote named `origin`, and it refuses a receipt over 65,536 bytes, the most the action posts.

## Export a report

`gh ledger export` writes one HTML file that shows the spend of a repository, a branch, a pull request, or a Copilot session: summary cards, a timeline of requests and tool calls, cost by model and agent, cache efficiency, the costliest prompt chains, the spend tables, and circuit-breaker trips and budgets. It opens in any browser, offline and without a server. `--bundle` also writes a ZIP with the report, a manifest of SHA-256 hashes, and the data as JSON. For a walkthrough, see [Attach a cost report to a pull request](guide.md#attach-a-cost-report-to-a-pull-request).

```text
gh ledger export [--format html] [--out <path>] [--bundle] [--repo <key> | --all-repos] [--branch <name>] [--pr <n>]
                 [--conversation <id>] [--since <period>] [--time-zone <IANA id>] [--include-local-paths]
                 [--max-timeline-spans <n>] [--generated-at <UTC time>] [--force]
```

| Option | Default | Description |
|---|---|---|
| `--format` | `html` | The report format; `html` is the only one. |
| `--out` | `copilot-ledger-report.html` | The report file, relative to the current directory. It must end in `.html` or `.htm`, and its folder must exist. |
| `--bundle` | | Also write a ZIP with the same name and `.zip`, beside the report. |
| `--repo`, `--all-repos` | The current repository | One repository key, or every repository. Outside a Git repository, every repository. |
| `--branch` | | One branch, or `(none)`. |
| `--pr` | | Requests recorded with this pull request number, as `status --cache --by pr` groups them. Only branches that track `refs/pull/<n>/head` have one, so `--branch` is usually what a pull request's report needs. |
| `--conversation` | | One Copilot session ID. |
| `--since` | `30d` | `today`, `all`, `1d` through `3650d`, or a `yyyy-MM-dd` date, counted at the generation time in the export's time zone. |
| `--time-zone` | This computer's | An IANA time zone ID, such as `America/New_York`, for dates, times, and night shading. On Windows, IANA IDs need Windows 10 version 1903 or later, and Windows IDs such as `Eastern Standard Time` also work. |
| `--include-local-paths` | | Show the local roots of repositories without a GitHub remote. Without it, such a repository shows as its folder name. |
| `--max-timeline-spans` | `20000` | The most requests, and the most tool calls, that the timeline draws: the latest ones. 0 to 100,000. |
| `--generated-at` | Now | The generation time to record, in UTC, such as `2026-11-21T21:30:00Z`, to reproduce an earlier export. |
| `--force` | | Replace existing files. |

The export reads the ledger once, so the report and the bundle agree. It covers the requests that start from the first day of `--since` up to the generation time, and the tool calls of their prompt chains in the same window; breaker trips are those of the same prompt chains, and budgets those of the requests' branches, with their all-time spend as `budget list` shows it. Standard output names the files, the number of requests, the scope, and the period, and never a folder:

```text
Wrote copilot-ledger-report.html: 8 requests in contoso/webapp, branch feature/search (last 30 days).
Wrote copilot-ledger-report.zip: report.html, manifest.json, and 4 telemetry files.
```

When the timeline leaves requests or tool calls out, a third line says how many it draws.

### What the report shows

- **Summary:** requests, GitHub AI credits, Foundry BYOK, API equivalent, local savings, cache hit ratio, prompt chains, breaker trips, and budgets. Each card's tooltip holds the exact amount.
- **Coverage:** whether the requests are complete, whether the tool calls are, and notes on what's missing: imported requests that live spans recorded too (each counted once), imported requests that live spans of their sessions replaced without matching them (see [Overlap with live spans](#overlap-with-live-spans)), imports that deferred requests or didn't finish (counted across the whole ledger, because imports aren't kept per repository), requests older than the 7 days of tool history, unpriced requests, and requests without cache counters. Imports that were replaced without a match, deferred, or not finished may hide requests, so they make the requests incomplete. These limits are also in the bundle's `coverage` object.
- **Timeline:** a row per Copilot session, a bar per request colored by model, a tick per tool call (red when it failed, with no duration, which the ledger doesn't keep), and nights shaded from 18:00 to 06:00 in the export's time zone. Scroll or pinch to zoom, drag to pan, or drag across the overview. When there are more than `--max-timeline-spans` requests or tool calls, it draws the latest ones; the totals and tables still include them all.
- **Cost by model and agent:** billed spend (GitHub AI credits, then Foundry BYOK), the API-equivalent view, and local savings, each on its own scale. The comparison views are never added to billed spend.
- **Cache efficiency, prompt chains, spend tables, and the circuit breaker:** the reports' numbers, with sortable columns.

`Unpriced` and `n/a` are never shown as `$0.00` or `0.0%`, and a hatched mark flags requests without a price. The page works without JavaScript: every chart has a table, and the script only adds zooming, the view switch, and sorting. It loads nothing: its Content Security Policy is `default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:`. GitHub accepts `.html` and `.zip` attachments up to 25 MB, and at the default timeline cap a report stays near 6 MB at most; lower `--max-timeline-spans` for a smaller one.

### The bundle

The ZIP holds exactly six files: `manifest.json`, `report.html` (the same bytes as the report beside it), and `telemetry/prompt-chains.json`, `telemetry/spans.json`, `telemetry/summary.json`, and `telemetry/tool-calls.json`. `spans.json` has every request, however many the timeline draws, and `tool-calls.json` every tool call. Money is an exact decimal number, as in every gh-ledger JSON: parse it as a decimal, not a float.

```json
{
  "schemaVersion": 1,
  "tool": "gh-ledger",
  "toolVersion": "0.4.1",
  "generatedAtUtc": "2026-11-21T21:30:00.0000000+00:00",
  "ratesVersions": ["2026-11-23"],
  "apiRateCards": [{ "ratesVersion": "2026-11-23", "sha256": "<64 hexadecimal digits>" }],
  "filter": { "repo": "contoso/webapp", "allRepositories": false, "branch": "feature/search", "pr": null, "conversation": null, "since": "30d", "period": "last 30 days", "fromUtc": "…", "toUtc": "…" },
  "timeZone": { "requested": null, "id": "…", "nightStartLocal": "18:00", "nightEndLocal": "06:00", "nightBandsUtc": [] },
  "options": { "maxTimelineSpans": 20000, "includeLocalPaths": false },
  "privacy": { "contentFree": true, "localPathsIncluded": false, "excluded": ["prompts", "responses", "tool arguments", "tool results", "file contents", "remote URLs"] },
  "files": [{ "path": "report.html", "bytes": 123456, "sha256": "<64 hexadecimal digits>" }],
  "coverage": { "requestsComplete": true, "warnings": [], "…": "…" }
}
```

`files` lists the other five files with their exact sizes and SHA-256 hashes; the manifest doesn't list itself. The hashes show that the files weren't changed after they were written, not who wrote them. The same gh-ledger build on the same operating system writes the same bytes again from an unchanged ledger: repeat the export's filters with `--generated-at` set to `generatedAtUtc` and `--time-zone` set to `timeZone.requested`, or to `timeZone.id`. A Windows ID works only on Windows, so pass an IANA `--time-zone` when a report may be reproduced elsewhere. A request recorded after the export but started before it changes the result.

### What export includes

These are the only fields export writes. In the table, `.` is a file's top level, a name is a nested object, and `name[]` is each object of an array. `report.html` means its `ledger-data` block. `repositoryRoot` is written only with `--include-local-paths`.

<!-- export-allowlist -->
| File | Object | Fields |
|---|---|---|
| `manifest.json` | `.` | `schemaVersion`, `tool`, `toolVersion`, `generatedAtUtc`, `ratesVersions`, `apiRateCards`, `filter`, `timeZone`, `options`, `privacy`, `files`, `coverage` |
| `manifest.json` | `apiRateCards[]` | `ratesVersion`, `sha256` |
| `manifest.json` | `options` | `maxTimelineSpans`, `includeLocalPaths` |
| `manifest.json` | `privacy` | `contentFree`, `localPathsIncluded`, `excluded` |
| `manifest.json` | `files[]` | `path`, `bytes`, `sha256` |
| `report.html` | `.` | `schemaVersion`, `generatedAtUtc`, `toolVersion`, `filter`, `timeZone`, `timeline`, `coverage` |
| `report.html` | `timeline` | `fromUtc`, `toUtc`, `width`, `ticks` |
| `report.html` | `timeline.ticks[]` | `x`, `level`, `label` |
| `manifest.json`, `report.html` | `filter` | `repo`, `allRepositories`, `branch`, `pr`, `conversation`, `since`, `period`, `fromUtc`, `toUtc` |
| `manifest.json`, `report.html` | `timeZone` | `requested`, `id`, `nightStartLocal`, `nightEndLocal`, `nightBandsUtc` |
| `manifest.json`, `report.html` | `timeZone.nightBandsUtc[]` | `fromUtc`, `toUtc` |
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage` | `requestsComplete`, `warnings`, `spans`, `spansBySource`, `supersededMatchedImportSpans`, `supersededUnmatchedImportSpans`, `ledgerAmbiguousImportRecords`, `ledgerIncompleteImportLogs`, `unpricedSpans`, `apiEquivalentUnpricedSpans`, `apiEquivalentEstimatedSpans`, `cacheReportedSpans`, `cacheAbsentSpans`, `cacheLegacyUnknownSpans`, `toolHistory`, `timeline` |
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage.spansBySource[]` | `source`, `spans` |
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage.toolHistory` | `complete`, `retentionDays`, `retainedFromUtc`, `spansBeforeRetention`, `importedToolCallsWithoutDetail` |
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage.timeline` | `truncated`, `maxSpans`, `spans`, `spansDrawn`, `toolCalls`, `toolCallsDrawn` |
| `telemetry/summary.json` | `.` | `schemaVersion`, `totals`, `byModel`, `byAgent`, `byBranch`, `byChannelModel`, `cacheByModel`, `cacheByAgent`, `breakerTrips`, `budgets`, `coverage` |
| `telemetry/summary.json` | `totals` | `spans`, `unpricedSpans`, `inputTokens`, `outputTokens`, `cacheReadInputTokens`, `cacheWriteInputTokens`, `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, `apiEquivalentEstimatedSpans`, `cacheReportedSpans`, `cacheUnreportedSpans`, `cacheHitRatio`, `promptChains` |
| `telemetry/summary.json` | `byModel[]` | `model`, `spans`, `unpricedSpans`, `inputTokens`, `outputTokens`, `cacheReadInputTokens`, `cacheWriteInputTokens`, `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, `apiEquivalentEstimatedSpans`, `cacheReportedSpans`, `cacheUnreportedSpans`, `cacheHitRatio` |
| `telemetry/summary.json` | `byAgent[]` | `agent`, `spans`, `unpricedSpans`, `inputTokens`, `outputTokens`, `cacheReadInputTokens`, `cacheWriteInputTokens`, `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, `apiEquivalentEstimatedSpans`, `cacheReportedSpans`, `cacheUnreportedSpans`, `cacheHitRatio` |
| `telemetry/summary.json` | `byBranch[]` | `repository`, `branch`, `spans`, `unpricedSpans`, `inputTokens`, `outputTokens`, `cacheReadInputTokens`, `cacheWriteInputTokens`, `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, `apiEquivalentEstimatedSpans`, `cacheReportedSpans`, `cacheUnreportedSpans`, `cacheHitRatio` |
| `telemetry/summary.json` | `byChannelModel[]` | `channel`, `model`, `spans`, `unpricedSpans`, `inputTokens`, `outputTokens`, `cacheReadInputTokens`, `cacheWriteInputTokens`, `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, `apiEquivalentEstimatedSpans`, `cacheReportedSpans`, `cacheUnreportedSpans`, `cacheHitRatio` |
| `telemetry/summary.json` | `cacheByModel[]` | `model`, `spans`, `cacheReportedSpans`, `cacheUnreportedSpans`, `inputTokens`, `outputTokens`, `maxInputTokens`, `averageInputTokensPerTurn`, `reportedInputTokens`, `reportedCacheReadInputTokens`, `reportedCacheWriteInputTokens`, `uncachedInputTokens`, `estimatedUncachedInputTokens`, `cacheHitRatio` |
| `telemetry/summary.json` | `cacheByAgent[]` | `agent`, `spans`, `cacheReportedSpans`, `cacheUnreportedSpans`, `inputTokens`, `outputTokens`, `maxInputTokens`, `averageInputTokensPerTurn`, `reportedInputTokens`, `reportedCacheReadInputTokens`, `reportedCacheWriteInputTokens`, `uncachedInputTokens`, `estimatedUncachedInputTokens`, `cacheHitRatio` |
| `telemetry/summary.json` | `breakerTrips[]` | `rule`, `traceId`, `conversationId`, `repository`, `repositoryRoot`, `branch`, `trippedAt`, `open`, `resetAt`, `windowCostUsd`, `windowMinutes`, `failureCount` |
| `telemetry/summary.json` | `budgets[]` | `repository`, `branch`, `budgetAiCredits`, `spentAiCredits`, `exceeded` |
| `telemetry/spans.json` | `.` | `schemaVersion`, `spans` |
| `telemetry/spans.json` | `spans[]` | `traceId`, `spanId`, `parentSpanId`, `operationName`, `conversationId`, `agent`, `provider`, `requestModel`, `responseModel`, `model`, `startTime`, `endTime`, `inputTokens`, `cacheReadInputTokens`, `cacheWriteInputTokens`, `uncachedInputTokens`, `outputTokens`, `cacheReadState`, `cacheWriteState`, `billingChannel`, `pricingStatus`, `ratesVersion`, `costUsd`, `aiCredits`, `savedUsd`, `apiEquivalentUsd`, `apiEquivalentStatus`, `apiRatesVersion`, `apiRateCardSha256`, `repository`, `repositoryRoot`, `branch`, `headCommit`, `pullRequest`, `ingestSource` |
| `telemetry/tool-calls.json` | `.` | `schemaVersion`, `toolCalls` |
| `telemetry/tool-calls.json` | `toolCalls[]` | `traceId`, `spanId`, `conversationId`, `agent`, `toolName`, `startTime`, `exitCode`, `status`, `failed`, `ingestSource` |
| `telemetry/prompt-chains.json` | `.` | `schemaVersion`, `limit`, `total`, `chains` |
| `telemetry/prompt-chains.json` | `chains[]` | `rank`, `traceId`, `conversationId`, `startTime`, `endTime`, `repository`, `branch`, `agents`, `models`, `spans`, `unpricedSpans`, `inputTokens`, `outputTokens`, `cacheReadInputTokens`, `cacheWriteInputTokens`, `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`, `apiEquivalentUsd`, `apiEquivalentUnpricedSpans`, `apiEquivalentEstimatedSpans` |
<!-- /export-allowlist -->

- A request's `repository` is its `owner/repo` slug, its root's folder name (the root with `--include-local-paths`), or `(none)`; every other `repository` is the same as the reports' key, with a local root shown the same way. `aiCredits` of a request is its cost in AI credits when GitHub billed it, and 0 otherwise. `costUsd` is 0 for an unpriced request, and `pricingStatus` says why.
- Tool calls have a start and no end, so the export shows no duration. A tool call's `failed` is true for an exit code other than 0 or an error status.
- **Never written:** prompts, responses, system instructions, tool arguments and results, file contents, error messages, working directories, data folders, and remote URLs. The ledger doesn't store any of them, and export reads nothing but the ledger.

## MCP server

`gh ledger mcp` runs an MCP server on standard input and output, so Copilot can read the ledger and see the circuit breaker. Register it with Copilot CLI using the gh-ledger binary's path, which for the extension is shown under [Get gh-ledger](#get-gh-ledger). In PowerShell on Windows:
````

Edit 2 of 3 in `docs/cli.md`, replace:

```markdown
## Privacy

CopilotLedger stores no prompt or response content, working directories, or remote URLs. Repositories are stored as `owner/repo` slugs or local roots, and credentials are stripped. `gh ledger import` keeps only the fields in [What import reads and keeps](#what-import-reads-and-keeps), and never reads prompts, responses, tool arguments or results, or compaction summaries.

## Troubleshooting
```

with:

```markdown
## Privacy

CopilotLedger stores no prompt or response content, working directories, or remote URLs. Repositories are stored as `owner/repo` slugs or local roots, and credentials are stripped. `gh ledger import` keeps only the fields in [What import reads and keeps](#what-import-reads-and-keeps), and never reads prompts, responses, tool arguments or results, or compaction summaries. `gh ledger export` writes only the fields in [What export includes](#what-export-includes), and shows local repository roots only with `--include-local-paths`.

## Troubleshooting
```

Edit 3 of 3 in `docs/cli.md`, replace:

```markdown
- **Move the data directory.** Set `COPILOT_LEDGER_HOME` before running gh-ledger. The daemon started at sign-in uses it only if your sign-in session sets it too, for example as a Windows user environment variable.
- **A console window flashes at sign-in.** Windows 11 24H2 and later start gh-ledger without a window; earlier versions show it for about a second.
```

with:

```markdown
- **Move the data directory.** Set `COPILOT_LEDGER_HOME` before running gh-ledger. The daemon started at sign-in uses it only if your sign-in session sets it too, for example as a Windows user environment variable.
- **A console window flashes at sign-in.** Windows 11 24H2 and later start gh-ledger without a window; earlier versions show it for about a second.
- **`gh ledger export` says `Invalid --time-zone value` on Windows.** Windows before Windows 10 version 1903 can't look up IANA time zone IDs. Pass a Windows ID, such as `Eastern Standard Time`, instead.
```

New file `tests/CopilotLedger.Tests/Cli/ExportAllowlistTests.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// Export writes exactly the fields that docs/cli.md lists between its export-allowlist markers, in that order.
public sealed partial class ExportAllowlistTests
{
    private const string Bundle = "copilot-ledger-report.zip";

    [Fact]
    public async Task EL1_EveryObjectWrittenIsDocumentedAndEveryRowIsUsed()
    {
        Dictionary<(string File, string Path), string[]> documented = Documented();
        HashSet<(string File, string Path)> used = [];

        foreach (bool includeLocalPaths in new[] { false, true })
        {
            using TempDirectory temp = new();
            await ExportSeeds.CreateAsync(temp.Path);
            foreach ((string file, JsonDocument document) in await ExportAsync(temp.Path, includeLocalPaths))
            {
                using (document)
                {
                    foreach ((string path, string[] names) in Objects(document.RootElement, "."))
                    {
                        Assert.True(documented.TryGetValue((file, path), out string[]? fields), $"{file} {path} isn't documented.");
                        Assert.Equal(includeLocalPaths ? fields : fields.Where(field => field != "repositoryRoot"), names);
                        used.Add((file, path));
                    }
                }
            }
        }

        Assert.Equal(documented.Keys.Order(), used.Order());
    }

    [Fact]
    public async Task EL2_SpanRecordsHaveTheDocumentedFieldsInOrder()
    {
        string[] documented = Documented()[("telemetry/spans.json", "spans[]")];

        foreach (bool includeLocalPaths in new[] { false, true })
        {
            using TempDirectory temp = new();
            await ExportSeeds.CreateAsync(temp.Path);
            IReadOnlyList<(string File, JsonDocument Document)> files = await ExportAsync(temp.Path, includeLocalPaths);
            JsonElement first = files.Single(file => file.File == "telemetry/spans.json").Document.RootElement.GetProperty("spans")[0];
            string[] names = [.. first.EnumerateObject().Select(property => property.Name)];
            Assert.Equal(includeLocalPaths ? documented : documented.Where(field => field != "repositoryRoot"), names);
            Assert.Equal(includeLocalPaths, names.Contains("repositoryRoot"));
            foreach ((_, JsonDocument document) in files)
            {
                document.Dispose();
            }
        }

        Assert.Equal("repository", documented[Array.IndexOf(documented, "repositoryRoot") - 1]);
    }

    [Fact]
    public async Task EL3_TheManifestAndItsExampleMatchTheAllowlist()
    {
        Dictionary<(string File, string Path), string[]> documented = Documented();
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        IReadOnlyList<(string File, JsonDocument Document)> files = await ExportAsync(temp.Path, false);
        JsonElement manifest = files.Single(file => file.File == "manifest.json").Document.RootElement;

        Assert.Equal(documented[("manifest.json", ".")], manifest.EnumerateObject().Select(property => property.Name));
        foreach (string path in new[] { "filter", "timeZone", "options", "privacy", "coverage", "coverage.toolHistory", "coverage.timeline" })
        {
            JsonElement value = path.Split('.').Aggregate(manifest, (element, name) => element.GetProperty(name));
            Assert.Equal(documented[("manifest.json", path)], value.EnumerateObject().Select(property => property.Name));
        }

        string cli = File.ReadAllText(RepositoryFile("docs", "cli.md")).ReplaceLineEndings("\n");
        string example = ManifestExample().Match(cli[cli.IndexOf("### The bundle", StringComparison.Ordinal)..]).Groups[1].Value;
        using JsonDocument parsed = JsonDocument.Parse(example);
        Assert.Equal(documented[("manifest.json", ".")], parsed.RootElement.EnumerateObject().Select(property => property.Name));
        foreach ((_, JsonDocument document) in files)
        {
            document.Dispose();
        }
    }

    // Exports the fixture with a bundle and returns each JSON document: the bundle's files and the report's ledger-data block.
    private static async Task<IReadOnlyList<(string File, JsonDocument Document)>> ExportAsync(string root, bool includeLocalPaths)
    {
        CliHarness cli = new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));
        CliRun run = await cli.RunAsync(includeLocalPaths
            ? ["export", "--all-repos", "--bundle", "--include-local-paths"]
            : ["export", "--all-repos", "--bundle"]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        IReadOnlyList<(string Name, byte[] Bytes)> entries = ExportFiles.ReadZip(Path.Combine(root, Bundle));
        return
        [
            .. entries.Select(entry => entry.Name == "report.html"
                ? (entry.Name, ExportFiles.LedgerData(System.Text.Encoding.UTF8.GetString(entry.Bytes)))
                : (entry.Name, JsonDocument.Parse(entry.Bytes))),
        ];
    }

    // Each object in element and its property names, with its path: "." for the top level, names joined by "." for nested
    // objects, and "[]" after an array's name for its objects.
    private static IEnumerable<(string Path, string[] Names)> Objects(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return (path, [.. element.EnumerateObject().Select(property => property.Name)]);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                foreach ((string, string[]) nested in Objects(property.Value, path == "." ? property.Name : $"{path}.{property.Name}"))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach ((string, string[]) nested in Objects(item, path + "[]"))
                {
                    yield return nested;
                }
            }
        }
    }

    // The allowlist: each (file, object) and its fields in order. A row may name several files.
    private static Dictionary<(string File, string Path), string[]> Documented()
    {
        string[] lines = File.ReadAllLines(RepositoryFile("docs", "cli.md"));
        int start = Array.IndexOf(lines, "<!-- export-allowlist -->");
        int end = Array.IndexOf(lines, "<!-- /export-allowlist -->");
        Assert.True(start >= 0 && end > start + 2);
        Assert.Equal("| File | Object | Fields |", lines[start + 1]);
        Dictionary<(string, string), string[]> rows = [];
        foreach (string line in lines[(start + 3)..end])
        {
            string[] cells = line.Trim('|', ' ').Split(" | ");
            Assert.Equal(3, cells.Length);
            string path = Assert.Single(Ticked(cells[1]));
            foreach (string file in Ticked(cells[0]))
            {
                Assert.True(rows.TryAdd((file, path), [.. Ticked(cells[2])]), $"{file} {path} is documented twice.");
            }
        }

        return rows;
    }

    private static IEnumerable<string> Ticked(string cell) =>
        Code().Matches(cell).Select(match => match.Groups[1].Value);

    // A file of the repository, found from the test's output directory.
    private static string RepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CopilotLedger.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex Code();

    [GeneratedRegex("```json\n(.*?)\n```", RegexOptions.Singleline)]
    private static partial Regex ManifestExample();
}
```

### Tests

- **EL1** For the fixture, with and without `--include-local-paths`, the property paths of every JSON file and of `ledger-data` equal the documented ones (less `repositoryRoot` without the option); every documented row is used, and no (file, object) pair is documented twice.
- **EL2** A span record's property names, in order, are the documented list, with `repositoryRoot` after `repository` only with the option.
- **EL3** The manifest's top level and each nested object, and the documented JSON example's names, equal the allowlist.

### Acceptance criteria

- `docs/cli.md` documents every field export writes, and the tests prove that it writes no other.
- `dotnet test CopilotLedger.slnx` passes **1377** tests (1374 + 3).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 12 specification: The Walkthrough, README, and CI

### Files

| File | Change |
|---|---|
| `docs/guide.md` | New "Attach a cost report to a pull request" after "Post a receipt on a pull request", in the contents, with the sample `export` |
| `README.md` | A feature line and an export sentence in "Privacy" |
| `.github/workflows/ci.yml` | The smoke step exports with `--bundle` and `--time-zone America/New_York` on both operating systems (stored with CRLF; keep it) |
| `tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs` | `["export"] = GuideState.Month` |

### The guide's sample

```shell
gh ledger export --branch feature/search --bundle
```

```text
Wrote copilot-ledger-report.html: 8 requests in contoso/webapp, branch feature/search (last 30 days).
Wrote copilot-ledger-report.zip: report.html, manifest.json, and 4 telemetry files.
```

The CI step passes `--generated-at 2030-01-01T00:00:00Z`, because the window ends at the generation time and the smoke span starts at 2026-11-21T00:00:00Z, which a runner's clock may not have reached. It checks both output lines, the CSP, the requested time zone in the page, and that the ZIP isn't empty, which proves that the published binary resolves an IANA ID on Linux and Windows.

### Code (copy exactly)

Edit 1 of 1 in `.github/workflows/ci.yml`, replace:

```yaml
          grep -q '"spans": 1,' <<< "$spend"
          grep -q '"aiCredits": 50,' <<< "$spend"
          ./artifacts/gh-ledger/gh-ledger status
          ./artifacts/gh-ledger/gh-ledger breaker status
```

with:

```yaml
          grep -q '"spans": 1,' <<< "$spend"
          grep -q '"aiCredits": 50,' <<< "$spend"
          mkdir -p artifacts/export
          exported=$(./artifacts/gh-ledger/gh-ledger export --all-repos --since all --time-zone America/New_York --generated-at 2030-01-01T00:00:00Z --bundle --out artifacts/export/report.html | tr -d '\r')
          echo "$exported"
          grep -q '^Wrote report.html: 1 request in all repositories (all time)\.$' <<< "$exported"
          grep -q '^Wrote report.zip: report.html, manifest.json, and 4 telemetry files\.$' <<< "$exported"
          grep -q "default-src 'none'" artifacts/export/report.html
          grep -q '"requested": "America/New_York"' artifacts/export/report.html
          test -s artifacts/export/report.zip
          ./artifacts/gh-ledger/gh-ledger status
          ./artifacts/gh-ledger/gh-ledger breaker status
```

Edit 1 of 2 in `README.md`, replace:

```markdown
- API-equivalent USD: what the same requests would cost at each model's API list prices, or at prices you supply, beside what Copilot billed
- `gh ledger import`: backfill the requests in your Copilot CLI and GitHub Copilot app session logs, and in Codex's, from before you installed CopilotLedger

## Requirements
```

with:

```markdown
- API-equivalent USD: what the same requests would cost at each model's API list prices, or at prices you supply, beside what Copilot billed
- `gh ledger import`: backfill the requests in your Copilot CLI and GitHub Copilot app session logs, and in Codex's, from before you installed CopilotLedger
- `gh ledger export`: a self-contained, offline HTML cost report with a timeline and charts, and an optional bundle of its data with SHA-256 hashes, to attach to a pull request or audit ticket

## Requirements
```

Edit 2 of 2 in `README.md`, replace:

````markdown
```

The [user guide](docs/guide.md) shows the everyday tasks with sample output: backfilling history, reports, budgets, the circuit breaker, pull request receipts, Foundry BYOK and local models, MCP, updates, and uninstalling.

## Privacy

The ledger stays on your machine, and CopilotLedger never stores prompt or response content. `gh ledger import` reads Copilot's and Codex's session logs in place and keeps only what pricing, attribution, and later imports need: session IDs; models and providers; token counts and times; agent names; the repository root, branch, commit, and `owner/repo` slug that a session recorded; tool names, outcomes, exit codes, and counts; SHA-256 hashes of request and tool call IDs; and each log's size, last-write time, and SHA-256 hash. It never reads prompts, responses, tool arguments or results, or compaction summaries, and it doesn't read Claude Code's logs. See [What import reads and keeps](docs/cli.md#what-import-reads-and-keeps).

## Documentation
````

with:

````markdown
```

The [user guide](docs/guide.md) shows the everyday tasks with sample output: backfilling history, reports, budgets, the circuit breaker, pull request receipts and cost reports, Foundry BYOK and local models, MCP, updates, and uninstalling.

## Privacy

The ledger stays on your machine, and CopilotLedger never stores prompt or response content. `gh ledger import` reads Copilot's and Codex's session logs in place and keeps only what pricing, attribution, and later imports need: session IDs; models and providers; token counts and times; agent names; the repository root, branch, commit, and `owner/repo` slug that a session recorded; tool names, outcomes, exit codes, and counts; SHA-256 hashes of request and tool call IDs; and each log's size, last-write time, and SHA-256 hash. It never reads prompts, responses, tool arguments or results, or compaction summaries, and it doesn't read Claude Code's logs. See [What import reads and keeps](docs/cli.md#what-import-reads-and-keeps). `gh ledger export` writes only the fields in [What export includes](docs/cli.md#what-export-includes): never prompts, responses, tool arguments or results, or file contents, and local repository roots only with `--include-local-paths`.

## Documentation
````

Edit 1 of 2 in `docs/guide.md`, replace:

```markdown
- [Catch a runaway loop](#catch-a-runaway-loop)
- [Post a receipt on a pull request](#post-a-receipt-on-a-pull-request)
- [Price Foundry BYOK and local models](#price-foundry-byok-and-local-models)
- [Ask Copilot about spend](#ask-copilot-about-spend)
```

with:

```markdown
- [Catch a runaway loop](#catch-a-runaway-loop)
- [Post a receipt on a pull request](#post-a-receipt-on-a-pull-request)
- [Attach a cost report to a pull request](#attach-a-cost-report-to-a-pull-request)
- [Price Foundry BYOK and local models](#price-foundry-byok-and-local-models)
- [Ask Copilot about spend](#ask-copilot-about-spend)
```

Edit 2 of 2 in `docs/guide.md`, replace:

````markdown
Before you merge, run `gh ledger export-pr` again and push, so the receipt includes the latest spend; the action updates the same comment. See the [action's README](https://github.com/swigerb/copilot-ledger-action) for its inputs and the checks it makes.

## Price Foundry BYOK and local models

**Goal:** see what your Microsoft Foundry BYOK models cost, and what local models save you.
````

with:

````markdown
Before you merge, run `gh ledger export-pr` again and push, so the receipt includes the latest spend; the action updates the same comment. See the [action's README](https://github.com/swigerb/copilot-ledger-action) for its inputs and the checks it makes.

## Attach a cost report to a pull request

**Goal:** give reviewers a report of a branch's AI spend that opens anywhere, with the data behind it.

In the repository, export the branch's report and its bundle:

<!-- sample: export -->
```shell
gh ledger export --branch feature/search --bundle
```

```text
Wrote copilot-ledger-report.html: 8 requests in contoso/webapp, branch feature/search (last 30 days).
Wrote copilot-ledger-report.zip: report.html, manifest.json, and 4 telemetry files.
```
<!-- /sample -->

Open `copilot-ledger-report.html` in any browser. It works offline and without a server, and every chart has a table, so it reads well without JavaScript too. Attach both files to the pull request, or to an audit ticket. The ZIP holds the report, the data as JSON, and `manifest.json`, which lists each file's size and SHA-256 hash, so a reviewer can check that the files weren't changed after you wrote them.

- `--time-zone America/New_York` shows dates, times, and nights in another time zone than this computer's.
- `--since all` covers the branch's whole history instead of the last 30 days.
- `--include-local-paths` shows the local roots of repositories without a GitHub remote, which otherwise show as their folder names.
- `--generated-at` with the manifest's `generatedAtUtc` writes the same files again from an unchanged ledger.

See [Export a report](cli.md#export-a-report) for every option and the bundle's format.

## Price Foundry BYOK and local models

**Goal:** see what your Microsoft Foundry BYOK models cost, and what local models save you.
````

Edit 1 of 1 in `tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs`, replace:

```csharp
        ["breaker-reset"] = GuideState.Month,
        ["export-pr"] = GuideState.Month,
        ["rates-validate"] = GuideState.Month,
        ["daemon-stop"] = GuideState.Month,
```

with:

```csharp
        ["breaker-reset"] = GuideState.Month,
        ["export-pr"] = GuideState.Month,
        ["export"] = GuideState.Month,
        ["rates-validate"] = GuideState.Month,
        ["daemon-stop"] = GuideState.Month,
```

### Tests

- **GS1** (changed) The guide's samples include `export`.
- **GS2** (1 more case) `export` prints exactly the sample, with an empty standard error.

### Acceptance criteria

- The walkthrough's sample runs as written; CI exercises the published binary's export.
- `dotnet test CopilotLedger.slnx` passes **1378** tests (1377 + 1).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 13 specification: A Repository Path Shows Only Its Folder Name

Review findings R1 and R6. `ExportRecords.FolderName` returned its whole input when the input ended with `/` or `\`, and when it was a root such as `C:\`, `C:`, or `/`. So a `--repo` given as a path with a trailing separator, as shells complete folder names, put the absolute path in standard output, the page's header, and `manifest.filter.repo`, even when it selected nothing. The same function names the folders of the ledger's local roots.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/ExportRecords.cs` | `FolderName` removes trailing separators and names a root `(root folder)`; `LooksLikeLocalPath` also takes `file:` URIs |
| `docs/cli.md` | `--include-local-paths` names `(root folder)`; the replay paragraph says how to reproduce an export of a repository without a remote (R6) |
| `tests/CopilotLedger.Tests/Cli/ExportPrivacyTests.cs` | New: EP7 |

### Rules

1. `FolderName(path)` removes every trailing `/` and `\`, then takes what follows the last `/` or `\` left. When that's empty (`/`), ends with `:` (`C:\`, `C:`, `file:///`), or the trimmed path is a share's root (two separators, a server, a separator, and a share, as in `\\server\share` and `//server/share`), the result is `(root folder)`. The result never holds a separator.
2. `LooksLikeLocalPath(key)` is true when the key starts with `/` or `\` (a Unix path or a share, with either slash), with a letter and `:` (a drive, with or without a separator after it), or with `file:` in any case. A repository slug never does: it's `owner/repo` of `[A-Za-z0-9._-]`.
3. Nothing else changes: a slug or `(none)` is shown as it is, `--include-local-paths` still shows roots in the files, and standard output still shows a local root by its folder name.

### Code (copy exactly)

Edit 1 of 2 in `docs/cli.md`, replace:

```markdown
| `--since` | `30d` | `today`, `all`, `1d` through `3650d`, or a `yyyy-MM-dd` date, counted at the generation time in the export's time zone. |
| `--time-zone` | This computer's | An IANA time zone ID, such as `America/New_York`, for dates, times, and night shading. On Windows, IANA IDs need Windows 10 version 1903 or later, and Windows IDs such as `Eastern Standard Time` also work. |
| `--include-local-paths` | | Show the local roots of repositories without a GitHub remote. Without it, such a repository shows as its folder name. |
| `--max-timeline-spans` | `20000` | The most requests, and the most tool calls, that the timeline draws: the latest ones. 0 to 100,000. |
| `--generated-at` | Now | The generation time to record, in UTC, such as `2026-11-21T21:30:00Z`, to reproduce an earlier export. |
```

with:

```markdown
| `--since` | `30d` | `today`, `all`, `1d` through `3650d`, or a `yyyy-MM-dd` date, counted at the generation time in the export's time zone. |
| `--time-zone` | This computer's | An IANA time zone ID, such as `America/New_York`, for dates, times, and night shading. On Windows, IANA IDs need Windows 10 version 1903 or later, and Windows IDs such as `Eastern Standard Time` also work. |
| `--include-local-paths` | | Show the local roots of repositories without a GitHub remote. Without it, such a repository shows as its folder name, or as `(root folder)` when its root is a drive or a share, so two folders with the same name look the same. |
| `--max-timeline-spans` | `20000` | The most requests, and the most tool calls, that the timeline draws: the latest ones. 0 to 100,000. |
| `--generated-at` | Now | The generation time to record, in UTC, such as `2026-11-21T21:30:00Z`, to reproduce an earlier export. |
```

Edit 2 of 2 in `docs/cli.md`, replace:

````markdown
```

`files` lists the other five files with their exact sizes and SHA-256 hashes; the manifest doesn't list itself. The hashes show that the files weren't changed after they were written, not who wrote them. The same gh-ledger build on the same operating system writes the same bytes again from an unchanged ledger: repeat the export's filters with `--generated-at` set to `generatedAtUtc` and `--time-zone` set to `timeZone.requested`, or to `timeZone.id`. A Windows ID works only on Windows, so pass an IANA `--time-zone` when a report may be reproduced elsewhere. A request recorded after the export but started before it changes the result.

### What export includes
````

with:

````markdown
```

`files` lists the other five files with their exact sizes and SHA-256 hashes; the manifest doesn't list itself. The hashes show that the files weren't changed after they were written, not who wrote them. The same gh-ledger build on the same operating system writes the same bytes again from an unchanged ledger: repeat the export's filters with `--generated-at` set to `generatedAtUtc` and `--time-zone` set to `timeZone.requested`, or to `timeZone.id`. A Windows ID works only on Windows, so pass an IANA `--time-zone` when a report may be reproduced elsewhere. A request recorded after the export but started before it changes the result. `filter.repo` of a repository without a GitHub remote is its folder name, which `--repo` doesn't select (a slug has a `/`, and a folder name never does): to reproduce such an export, pass the repository's full root to `--repo`, or export with `--include-local-paths`, which records the root.

### What export includes
````

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportRecords.cs`, replace:

```csharp
        !includeLocalPaths && (roots.Contains(key) || LooksLikeLocalPath(key)) ? FolderName(key) : key;

    // The last segment after / or \; the path itself when it has none.
    public static string FolderName(string path)
    {
        int separator = path.LastIndexOfAny(['/', '\\']);
        return separator < 0 || separator == path.Length - 1 ? path : path[(separator + 1)..];
    }

    // Starts with / or \, or with a drive letter and a colon.
    public static bool LooksLikeLocalPath(string key) =>
        key.StartsWith('/') || key.StartsWith('\\') || (key.Length >= 2 && char.IsAsciiLetter(key[0]) && key[1] == ':');

    // "<repository, or all repositories>[, branch <b>][, pull request #<n>][, conversation <id>]". A local root shows as
```

with:

```csharp
        !includeLocalPaths && (roots.Contains(key) || LooksLikeLocalPath(key)) ? FolderName(key) : key;

    // A local path's last segment, once trailing / and \ are removed, so it's never a path; (root folder) for a root, such as
    // C:\, /, or \\server\share.
    public static string FolderName(string path)
    {
        string trimmed = path.TrimEnd('/', '\\');
        string name = trimmed[(trimmed.LastIndexOfAny(['/', '\\']) + 1)..];
        return name.Length == 0 || name.EndsWith(':') || IsShareRoot(trimmed) ? "(root folder)" : name;
    }

    // Starts with / or \, with a drive letter and a colon, or with file:.
    public static bool LooksLikeLocalPath(string key) =>
        key.StartsWith('/') || key.StartsWith('\\') || (key.Length >= 2 && char.IsAsciiLetter(key[0]) && key[1] == ':')
        || key.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

    // \\server\share or //server/share, without trailing separators.
    private static bool IsShareRoot(string trimmed) =>
        trimmed.Length > 2 && trimmed[0] is '/' or '\\' && trimmed[1] is '/' or '\\' && trimmed[2..].Split(['/', '\\']).Length == 2;

    // "<repository, or all repositories>[, branch <b>][, pull request #<n>][, conversation <id>]". A local root shows as
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/ExportPrivacyTests.cs`, replace:

```csharp
    }

    [Fact]
    public async Task EP4_TheSameInputsWriteTheSameBytesAndAReplayReproducesThem()
```

with:

```csharp
    }

    // A --repo that's a path shows as its last folder's name, however it ends, or as (root folder); nothing else of it
    // reaches a file or the console.
    [Theory]
    [InlineData("C:/Users/Alice/private-9f3e/project/", "project")]
    [InlineData(@"C:\Users\Alice\private-9f3e\project\\", "project")]
    [InlineData("/home/alice/private-9f3e/project/", "project")]
    [InlineData(@"\\fileserver-9f3e\share\project\", "project")]
    [InlineData("//fileserver-9f3e/share/project/", "project")]
    [InlineData("file:///C:/Users/Alice/private-9f3e/project/", "project")]
    [InlineData(@"\\fileserver-9f3e\private-9f3e\", "(root folder)")]
    [InlineData(@"C:\", "(root folder)")]
    [InlineData("C:", "(root folder)")]
    [InlineData("/", "(root folder)")]
    public async Task EP7_ARepositoryGivenAsAPathShowsOnlyItsFolderName(string repository, string shown)
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        CliRun run = await Harness(temp.Path).RunAsync("export", "--repo", repository, "--bundle");

        using JsonDocument manifest = JsonDocument.Parse(Entry(temp.Path, "manifest.json"));
        Assert.Equal(
            (0, $"Wrote copilot-ledger-report.html: 0 requests in {shown} (last 30 days).\nWrote copilot-ledger-report.zip: report.html, manifest.json, and 4 telemetry files.\n", string.Empty),
            (run.ExitCode, run.Out, run.Error));
        Assert.Equal(shown, manifest.RootElement.GetProperty("filter").GetProperty("repo").GetString());
        Assert.All(Texts(temp.Path), text => Assert.DoesNotContain("9f3e", text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EP4_TheSameInputsWriteTheSameBytesAndAReplayReproducesThem()
```

### Tests

- **EP7** (10 cases) `export --repo <path> --bundle` shows `project` for `C:/Users/Alice/private-9f3e/project/`, `C:\Users\Alice\private-9f3e\project\\`, `/home/alice/private-9f3e/project/`, `\\fileserver-9f3e\share\project\`, `//fileserver-9f3e/share/project/`, and `file:///C:/Users/Alice/private-9f3e/project/`, and `(root folder)` for `\\fileserver-9f3e\private-9f3e\`, `C:\`, `C:`, and `/`. Standard output is exactly `Wrote copilot-ledger-report.html: 0 requests in <shown> (last 30 days).` and the bundle's line, standard error is empty, `manifest.filter.repo` is the shown name, and `9f3e` is in neither the report nor any entry of the ZIP.

### Acceptance criteria

- No part of a path given to `--repo`, or of a local root, reaches a file or the console without `--include-local-paths`, however the path ends.
- `dotnet test CopilotLedger.slnx` passes **1388** tests (1378 + 10).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 14 specification: A Failed Write Names No Path and Leaves Both Files As They Were

Review findings R2 and R3. A write that failed printed the exception's message, which names the temporary file's absolute path: `--out missing/report.html` printed the working directory and the process ID. And with `--bundle --force`, the ZIP was moved into place before the report and never put back, so a report that couldn't be replaced (on Windows, a read-only one) left the new ZIP beside the old report, and the old ZIP was lost.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/ExportCommands.cs` | `WriteFailure`; a replaced ZIP waits at `BackupPath` until the report is in place, and `RestoreZip` puts it back |
| `docs/cli.md` | `--force` says that a report that can't be replaced leaves the ZIP as it was |
| `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs` | EC3 compares the whole message; new: EC12 |

### Rules

1. A failed write prints `Could not write <shown>: <reason>`, where `<shown>` is `--out` as given, or its `.zip`. `<reason>` is `its folder doesn't exist.` for a `DirectoryNotFoundException`, `access is denied.` for an `UnauthorizedAccessException`, and otherwise `the file system reported error 0x<HRESULT>.`, the exception's `HResult` as 8 uppercase hexadecimal digits. No exception message is printed.
2. Both files are written to temporary files first, as before. Then, with `--bundle`, an existing ZIP (there's one only with `--force`) is moved to `<zip>.<process ID>.bak`, the new ZIP is moved into place, and the report is moved into place, replacing an existing one with `--force`.
3. If a step fails, the export exits with 1 after putting the ZIP back as it was: the `.bak` file is moved back over the ZIP, or, when there's none, a ZIP that the export moved into place is deleted. If the `.bak` file can't be moved back, it's kept, and a second line says `The replaced <zip shown> is kept as <its file name>.`
4. The temporary files are always deleted, and so is the `.bak` file once the report is in place.

### Code (copy exactly)

Edit 1 of 1 in `docs/cli.md`, replace:

```markdown
| `--max-timeline-spans` | `20000` | The most requests, and the most tool calls, that the timeline draws: the latest ones. 0 to 100,000. |
| `--generated-at` | Now | The generation time to record, in UTC, such as `2026-11-21T21:30:00Z`, to reproduce an earlier export. |
| `--force` | | Replace existing files. |

The export reads the ledger once, so the report and the bundle agree. It covers the requests that start from the first day of `--since` up to the generation time, and the tool calls of their prompt chains in the same window; breaker trips are those of the same prompt chains, and budgets those of the requests' branches, with their all-time spend as `budget list` shows it. Standard output names the files, the number of requests, the scope, and the period, and never a folder:
```

with:

```markdown
| `--max-timeline-spans` | `20000` | The most requests, and the most tool calls, that the timeline draws: the latest ones. 0 to 100,000. |
| `--generated-at` | Now | The generation time to record, in UTC, such as `2026-11-21T21:30:00Z`, to reproduce an earlier export. |
| `--force` | | Replace existing files. If the report can't be replaced, the ZIP is put back as it was. |

The export reads the ledger once, so the report and the bundle agree. It covers the requests that start from the first day of `--since` up to the generation time, and the tool calls of their prompt chains in the same window; breaker trips are those of the same prompt chains, and budgets those of the requests' branches, with their all-time spend as `budget list` shows it. Standard output names the files, the number of requests, the scope, and the period, and never a folder:
```

Edit 1 of 5 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        string htmlTemp = TemporaryPath(htmlPath);
        string? zipTemp = zipPath is null ? null : TemporaryPath(zipPath);
        string writing = output;
        ExportReportModel model;
        try
        {
            // Both files are written from one snapshot, then moved into place: the ZIP first, then the report.
            await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync(cancellationToken))
            {
```

with:

```csharp
        string htmlTemp = TemporaryPath(htmlPath);
        string? zipTemp = zipPath is null ? null : TemporaryPath(zipPath);
        string? zipBackup = null;
        bool zipMoved = false;
        string writing = output;
        ExportReportModel model;
        try
        {
            // Both files are written from one snapshot, then moved into place: the ZIP first, then the report. A ZIP that
            // --force replaces waits beside it until the report is in place, so a failed export leaves both as they were.
            await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync(cancellationToken))
            {
```

Edit 2 of 5 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
            {
                writing = zipShown;
                File.Move(zipTemp, zipPath!, overwrite: force);
            }

            writing = output;
```

with:

```csharp
            {
                writing = zipShown;
                if (File.Exists(zipPath))
                {
                    string backup = BackupPath(zipPath!);
                    File.Move(zipPath!, backup);
                    zipBackup = backup;
                }

                File.Move(zipTemp, zipPath!);
                zipMoved = true;
            }

            writing = output;
```

Edit 3 of 5 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine($"Could not write {writing}: {ex.Message}");
            return 1;
        }
```

with:

```csharp
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine($"Could not write {writing}: {WriteFailure(ex)}");
            if (zipPath is not null && !RestoreZip(zipPath, zipBackup, zipMoved))
            {
                context.Error.WriteLine($"The replaced {zipShown} is kept as {Path.GetFileName(zipBackup)}.");
                zipBackup = null;
            }

            return 1;
        }
```

Edit 4 of 5 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
            {
                DeleteIfPresent(zipTemp);
            }
        }
```

with:

```csharp
            {
                DeleteIfPresent(zipTemp);
            }

            if (zipBackup is not null)
            {
                DeleteIfPresent(zipBackup);
            }
        }
```

Edit 5 of 5 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        $"{target}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.tmp";

    private static void DeleteIfPresent(string path)
    {
```

with:

```csharp
        $"{target}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.tmp";

    // Where a ZIP that --force replaces waits until the report is in place.
    private static string BackupPath(string target) =>
        $"{target}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.bak";

    // Why a file couldn't be written, without the path that the exception's message names.
    private static string WriteFailure(Exception ex) => ex switch
    {
        DirectoryNotFoundException => "its folder doesn't exist.",
        UnauthorizedAccessException => "access is denied.",
        _ => $"the file system reported error 0x{ex.HResult.ToString("X8", CultureInfo.InvariantCulture)}.",
    };

    // Puts back the ZIP that --force replaced, or deletes the new one when there was none, so a failed export leaves no ZIP
    // that doesn't match the report beside it. False when the replaced ZIP couldn't be put back.
    private static bool RestoreZip(string zipPath, string? backup, bool moved)
    {
        if (backup is null)
        {
            if (moved)
            {
                DeleteIfPresent(zipPath);
            }

            return true;
        }

        try
        {
            File.Move(backup, zipPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void DeleteIfPresent(string path)
    {
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
        CliRun missing = await cli.RunAsync("export", "--all-repos", "--out", "missing/report.html");

        Assert.Equal((1, "out.html is a directory.\n"), (directory.ExitCode, directory.Error));
        Assert.Equal(1, missing.ExitCode);
        Assert.StartsWith("Could not write missing/report.html: ", missing.Error);
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp", SearchOption.AllDirectories));
    }
```

with:

```csharp
        CliRun missing = await cli.RunAsync("export", "--all-repos", "--out", "missing/report.html");

        Assert.Equal((1, "out.html is a directory.\n"), (directory.ExitCode, directory.Error));
        Assert.Equal((1, "Could not write missing/report.html: its folder doesn't exist.\n"), (missing.ExitCode, missing.Error));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp", SearchOption.AllDirectories));
    }
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
```

with:

```csharp
    }

    // Only Windows refuses to replace a read-only file or to move one that's open; on Linux, the folder's permissions decide.
    [Fact]
    public async Task EC12_AFailedReplaceLeavesBothFilesAsTheyWere()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        string report = Path.Combine(temp.Path, Report);
        string bundle = Path.ChangeExtension(report, ".zip");
        File.WriteAllText(report, "old report");
        File.WriteAllText(bundle, "old bundle");

        File.SetAttributes(report, FileAttributes.ReadOnly);
        CliRun readOnly = await cli.RunAsync("export", "--all-repos", "--bundle", "--force");
        File.SetAttributes(report, FileAttributes.Normal);
        CliRun open;
        using (new FileStream(bundle, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            open = await cli.RunAsync("export", "--all-repos", "--bundle", "--force");
        }

        Assert.Equal((1, "Could not write copilot-ledger-report.html: access is denied.\n"), (readOnly.ExitCode, readOnly.Error));
        Assert.Equal(
            (1, "Could not write copilot-ledger-report.zip: the file system reported error 0x80070020.\n"),
            (open.ExitCode, open.Error));
        Assert.Equal(("old report", "old bundle"), (File.ReadAllText(report), File.ReadAllText(bundle)));
        Assert.Equal([report, bundle], Directory.GetFiles(temp.Path).Order(StringComparer.Ordinal));
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
```

### Tests

- **EC3** (changed) `--out missing/report.html` prints exactly `Could not write missing/report.html: its folder doesn't exist.`
- **EC12** On Windows (elsewhere it returns at once: whether a file can be replaced there depends on its folder, not on the file): with a read-only `copilot-ledger-report.html` and an old ZIP, `export --all-repos --bundle --force` exits with 1 and prints `Could not write copilot-ledger-report.html: access is denied.`; with the ZIP open without sharing, it exits with 1 and prints `Could not write copilot-ledger-report.zip: the file system reported error 0x80070020.` Both times the report and the ZIP keep their old contents, and the folder holds only those two files.

### Acceptance criteria

- No path reaches standard error when a write fails, and a failed export leaves the report and the ZIP as they were.
- `dotnet test CopilotLedger.slnx` passes **1389** tests (1388 + 1).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 15 specification: Tool History Counts Every Missing Tool Call and Every Deleted One

Review findings A1 (R4) and R5. `importedToolCallsWithoutDetail` counted an imported request's `imported_tool_call_count` only when the request owned no link. But import links only the tool calls that started within the 7 days before the import and that completed (`LedgerImporter` and `CopilotSessionLogSource.ToolComplete`), so a request with 3 tool calls and 1 link counted as complete, and `toolHistory.complete` could be true. And tool history was the 7 days before `generatedAt`, while the daemon deletes `span_links` rows older than 7 days by its own clock, when it starts and every 6 hours. So an export with a past `--generated-at`, made after the daemon had deleted tool calls of that week, called requests complete that had lost theirs.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs` | Each imported request's missing tool calls; `ReadEarliestToolCallStartAsync` |
| `src/CopilotLedger.Storage/ExportCoverageRow.cs` | `ImportedToolCallsWithoutDetail`'s comment |
| `src/CopilotLedger.Storage/LedgerExportSnapshot.cs` | `GetEarliestToolCallStartAsync` |
| `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs` | Tool history starts no earlier than the earliest tool call kept |
| `src/CopilotLedger.Cli/Export/ExportReportModel.cs` | `ExportCoverage.ToolHistoryFromEarliestToolCall` |
| `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs` | The note's second wording |
| `docs/cli.md` | The coverage bullet says where tool history starts |
| `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs` | New: XR11, XR12 |
| `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs` | EC8 gives its old request a tool call; new: EC13 |

### Rules

1. An imported request lacks the detail of `MAX(imported_tool_call_count - <its execute_tool links>, 0)` tool calls, where its links are the `span_links` rows whose `owner_span_row_id` is its row. `importedToolCallsWithoutDetail` sums that over the selected requests whose `imported_tool_call_count` is above 0.
2. `GetEarliestToolCallStartAsync` returns the smallest `start_time_unix_ms` of the ledger's `execute_tool` links, of any filter or owner, or null when there's none. The daemon deletes every link that starts before its cutoff, so no tool call before this one is left, and a request that started before it may have lost its tool calls.
3. Tool history starts at `generatedAt` less 7 days, or at the earliest tool call kept when that's later (it can be after `generatedAt`). `coverage.toolHistory.retainedFromUtc` is that start, and `spansBeforeRetention` counts the selected requests that started before it. When the ledger keeps no tool call, it's `generatedAt` less 7 days, because the ledger can't tell deleted tool calls from none.
4. The note is `Tool calls are kept for 7 days, so the <n requests> before <local time> may be missing their tool calls.` when tool history starts 7 days before `generatedAt`, and `The earliest tool call the ledger keeps started at <local time>, so the <n requests> before it may be missing their tool calls.` when it starts at the earliest tool call kept (`its` for 1 request).
5. Both rules read only the snapshot, so an unchanged ledger still writes the same bytes. A replay after the daemon has deleted tool calls reports more requests that may be missing theirs, which is true.

### Code (copy exactly)

Edit 1 of 1 in `docs/cli.md`, replace:

```markdown
### What the report shows

- **Summary:** requests, GitHub AI credits, Foundry BYOK, API equivalent, local savings, cache hit ratio, prompt chains, breaker trips, and budgets. Each card's tooltip holds the exact amount.
- **Coverage:** whether the requests are complete, whether the tool calls are, and notes on what's missing: imported requests that live spans recorded too (each counted once), imported requests that live spans of their sessions replaced without matching them (see [Overlap with live spans](#overlap-with-live-spans)), imports that deferred requests or didn't finish (counted across the whole ledger, because imports aren't kept per repository), requests older than the 7 days of tool history, unpriced requests, and requests without cache counters. Imports that were replaced without a match, deferred, or not finished may hide requests, so they make the requests incomplete. These limits are also in the bundle's `coverage` object.
- **Timeline:** a row per Copilot session, a bar per request colored by model, a tick per tool call (red when it failed, with no duration, which the ledger doesn't keep), and nights shaded from 18:00 to 06:00 in the export's time zone. Scroll or pinch to zoom, drag to pan, or drag across the overview. When there are more than `--max-timeline-spans` requests or tool calls, it draws the latest ones; the totals and tables still include them all.
- **Cost by model and agent:** billed spend (GitHub AI credits, then Foundry BYOK), the API-equivalent view, and local savings, each on its own scale. The comparison views are never added to billed spend.
```

with:

```markdown
### What the report shows

- **Summary:** requests, GitHub AI credits, Foundry BYOK, API equivalent, local savings, cache hit ratio, prompt chains, breaker trips, and budgets. Each card's tooltip holds the exact amount.
- **Coverage:** whether the requests are complete, whether the tool calls are, and notes on what's missing: imported requests that live spans recorded too (each counted once), imported requests that live spans of their sessions replaced without matching them (see [Overlap with live spans](#overlap-with-live-spans)), imports that deferred requests or didn't finish (counted across the whole ledger, because imports aren't kept per repository), requests older than the tool history, unpriced requests, and requests without cache counters. Imports that were replaced without a match, deferred, or not finished may hide requests, so they make the requests incomplete. Tool history is the 7 days before the generation time, but it starts no earlier than the earliest tool call the ledger keeps, because the daemon deletes older tool calls by its own clock; so an export with a past `--generated-at` doesn't count on tool calls that were deleted since. These limits are also in the bundle's `coverage` object.
- **Timeline:** a row per Copilot session, a bar per request colored by model, a tick per tool call (red when it failed, with no duration, which the ledger doesn't keep), and nights shaded from 18:00 to 06:00 in the export's time zone. Scroll or pinch to zoom, drag to pan, or drag across the overview. When there are more than `--max-timeline-spans` requests or tool calls, it draws the latest ones; the totals and tables still include them all.
- **Cost by model and agent:** billed spend (GitHub AI credits, then Foundry BYOK), the API-equivalent view, and local savings, each on its own scale. The comparison views are never added to billed spend.
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
            await snapshot.GetRollupAsync(LedgerGrouping.ChannelModel, filter, cancellationToken);
        LedgerRollupRow totals = ReportCommands.Sum(byChannelModel);
        DateTimeOffset toolHistoryFrom = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
        IReadOnlyList<LedgerRollupRow> byModel = await snapshot.GetRollupAsync(LedgerGrouping.Model, filter, cancellationToken);
```

with:

```csharp
            await snapshot.GetRollupAsync(LedgerGrouping.ChannelModel, filter, cancellationToken);
        LedgerRollupRow totals = ReportCommands.Sum(byChannelModel);

        // Tool history is the retention before generatedAt, but starts no earlier than the earliest tool call the ledger
        // keeps: the daemon deletes tool calls by its own clock, so a past --generated-at can't count on the retention.
        DateTimeOffset retained = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        DateTimeOffset? earliestToolCall = await snapshot.GetEarliestToolCallStartAsync(cancellationToken);
        DateTimeOffset toolHistoryFrom = earliestToolCall is DateTimeOffset earliest && earliest > retained ? earliest : retained;
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
        IReadOnlyList<LedgerRollupRow> byModel = await snapshot.GetRollupAsync(LedgerGrouping.Model, filter, cancellationToken);
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
                ToolHistoryRetentionDays = LedgerImporter.ToolLinkRetention.Days,
                ToolHistoryFromUtc = toolHistoryFrom,
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
```

with:

```csharp
                ToolHistoryRetentionDays = LedgerImporter.ToolLinkRetention.Days,
                ToolHistoryFromUtc = toolHistoryFrom,
                ToolHistoryFromEarliestToolCall = toolHistoryFrom > retained,
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
    public required long CacheLegacyUnknownSpans { get; init; }
    public required int ToolHistoryRetentionDays { get; init; }        // LedgerImporter.ToolLinkRetention.Days (7)
    public required DateTimeOffset ToolHistoryFromUtc { get; init; }    // GeneratedAt less the retention
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
```

with:

```csharp
    public required long CacheLegacyUnknownSpans { get; init; }
    public required int ToolHistoryRetentionDays { get; init; }        // LedgerImporter.ToolLinkRetention.Days (7)
    public required DateTimeOffset ToolHistoryFromUtc { get; init; }    // GeneratedAt less the retention, or the earliest tool call kept
    public required bool ToolHistoryFromEarliestToolCall { get; init; } // ToolHistoryFromUtc is the earliest tool call kept, which is later
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
        if (before > 0)
        {
            yield return $"Tool calls are kept for {coverage.ToolHistoryRetentionDays.ToString(CultureInfo.InvariantCulture)} days, so the {ExportRecords.Plural(before, "request", "requests")} before {LedgerFormat.LocalTime(coverage.ToolHistoryFromUtc, zone)} may be missing {(before == 1 ? "its" : "their")} tool calls.";
        }

        long withoutDetail = coverage.ImportedToolCallsWithoutDetail;
```

with:

```csharp
        if (before > 0)
        {
            string requests = ExportRecords.Plural(before, "request", "requests");
            string their = before == 1 ? "its" : "their";
            string from = LedgerFormat.LocalTime(coverage.ToolHistoryFromUtc, zone);
            yield return coverage.ToolHistoryFromEarliestToolCall
                ? $"The earliest tool call the ledger keeps started at {from}, so the {requests} before it may be missing {their} tool calls."
                : $"Tool calls are kept for {coverage.ToolHistoryRetentionDays.ToString(CultureInfo.InvariantCulture)} days, so the {requests} before {from} may be missing {their} tool calls.";
        }

        long withoutDetail = coverage.ImportedToolCallsWithoutDetail;
```

Edit 1 of 1 in `src/CopilotLedger.Storage/ExportCoverageRow.cs`, replace:

```csharp
    public required long LedgerIncompleteImportLogs { get; init; }   // import_sessions with complete = 0, the whole ledger
    public required long SpansBeforeToolHistory { get; init; }       // selected spans that start before toolHistoryFrom
    public required long ImportedToolCallsWithoutDetail { get; init; }
    public required long CacheReportedSpans { get; init; }
    public required long CacheAbsentSpans { get; init; }
```

with:

```csharp
    public required long LedgerIncompleteImportLogs { get; init; }   // import_sessions with complete = 0, the whole ledger
    public required long SpansBeforeToolHistory { get; init; }       // selected spans that start before toolHistoryFrom
    public required long ImportedToolCallsWithoutDetail { get; init; } // tool calls of imported requests beyond the links they own
    public required long CacheReportedSpans { get; init; }
    public required long CacheAbsentSpans { get; init; }
```

Edit 1 of 1 in `src/CopilotLedger.Storage/LedgerExportSnapshot.cs`, replace:

```csharp
        SqliteLedgerStore.CountExportToolCallsAsync(_transaction, filter, cancellationToken);

    // What the filter's spans cover. Spans that start before toolHistoryFrom may have lost their tool calls to retention.
    public Task<ExportCoverageRow> GetCoverageAsync(
```

with:

```csharp
        SqliteLedgerStore.CountExportToolCallsAsync(_transaction, filter, cancellationToken);

    // The start of the earliest tool call the ledger keeps, of any filter, or null when it keeps none. The daemon deletes
    // older ones, so no tool call before it is left.
    public Task<DateTimeOffset?> GetEarliestToolCallStartAsync(CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadEarliestToolCallStartAsync(_transaction, cancellationToken);

    // What the filter's spans cover. Spans that start before toolHistoryFrom may have lost their tool calls to retention.
    public Task<ExportCoverageRow> GetCoverageAsync(
```

Edit 1 of 2 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
    }

    // A superseded row that a live span of the same call replaced (the same canonical usage ID) counts when that live row is
    // selected: it's the same request, counted once. One that live spans of its session replaced without matching it may be a
    // call that no live span covers, so it counts when the filter selects it by its own columns, as gh ledger import reports
    // it. import_sessions has no repository, so its two counts are the whole ledger's.
    internal static async Task<ExportCoverageRow> ReadExportCoverageAsync(
        SqliteTransaction snapshot,
```

with:

```csharp
    }

    // The start of the earliest execute_tool link in the whole ledger, whatever its owner, or null when there's none.
    internal static async Task<DateTimeOffset?> ReadEarliestToolCallStartAsync(
        SqliteTransaction snapshot,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = "SELECT MIN(start_time_unix_ms) FROM span_links WHERE operation_name = 'execute_tool';";
        return await command.ExecuteScalarAsync(cancellationToken) is long start
            ? DateTimeOffset.FromUnixTimeMilliseconds(start)
            : null;
    }

    // A superseded row that a live span of the same call replaced (the same canonical usage ID) counts when that live row is
    // selected: it's the same request, counted once. One that live spans of its session replaced without matching it may be a
    // call that no live span covers, so it counts when the filter selects it by its own columns, as gh ledger import reports
    // it. An imported request lacks the detail of each tool call it made beyond the execute_tool links it owns: import keeps
    // the links of only the tool calls that started within the retention, and that completed. import_sessions has no
    // repository, so its two counts are the whole ledger's.
    internal static async Task<ExportCoverageRow> ReadExportCoverageAsync(
        SqliteTransaction snapshot,
```

Edit 2 of 2 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
            SELECT COALESCE(SUM(CASE WHEN start_time_unix_ms < @tool_history_from THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN imported_tool_call_count > 0
                                      AND NOT EXISTS (SELECT 1 FROM span_links WHERE span_links.owner_span_row_id = spans.id)
                                     THEN imported_tool_call_count ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN NOT {ReportedSql}
```

with:

```csharp
            SELECT COALESCE(SUM(CASE WHEN start_time_unix_ms < @tool_history_from THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN imported_tool_call_count > 0
                                     THEN MAX(imported_tool_call_count
                                              - (SELECT COUNT(*) FROM span_links
                                                  WHERE span_links.owner_span_row_id = spans.id
                                                    AND span_links.operation_name = 'execute_tool'), 0)
                                     ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN NOT {ReportedSql}
```

Edit 1 of 4 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
using System.Xml.Linq;
using CopilotLedger.Core.Import;
```

with:

```csharp
using System.Text.Json;
using System.Xml.Linq;
using CopilotLedger.Core.Import;
```

Edit 2 of 4 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
        XDocument fixture = await ExportAsync(cli, temp.Path, "--all-repos");
        await store.RecordAsync([Entry(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero))]);
        XDocument older = await ExportAsync(cli, temp.Path, "--all-repos", "--force");
        await store.RecordImportedAsync([], Checkpoint(ambiguous: 2));
```

with:

```csharp
        XDocument fixture = await ExportAsync(cli, temp.Path, "--all-repos");
        await store.RecordAsync([Entry(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero))]);
        await store.RecordSpanLinksAsync([ToolCall("9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a", new DateTimeOffset(2026, 11, 10, 12, 0, 30, TimeSpan.Zero))]);
        XDocument older = await ExportAsync(cli, temp.Path, "--all-repos", "--force");
        await store.RecordImportedAsync([], Checkpoint(ambiguous: 2));
```

Edit 3 of 4 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
```

with:

```csharp
    }

    // The daemon deleted the tool calls before 11-21, so the two requests of 11-20 may have lost theirs, though they're
    // within 7 days of the generation time.
    [Fact]
    public async Task EC13_ToolHistoryStartsNoEarlierThanTheEarliestToolCallKept()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await store.RecordSpanLinksAsync([ToolCall(ExportSeeds.E, new DateTimeOffset(2026, 11, 21, 13, 0, 0, TimeSpan.Zero))]);
        await store.PruneSpanLinksAsync(new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero));

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos", "--generated-at", "2026-11-21T21:30:00Z");

        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        JsonElement history = data.RootElement.GetProperty("coverage").GetProperty("toolHistory");
        Assert.Equal(
            ("2026-11-21T13:00:00.0000000+00:00", 2, false),
            (history.GetProperty("retainedFromUtc").GetString(), history.GetProperty("spansBeforeRetention").GetInt32(), history.GetProperty("complete").GetBoolean()));
        Assert.Equal("Tool calls: partial; see the notes.", ExportFiles.ById(page, "coverage-tool-calls").Value);
        Assert.Contains(
            "The earliest tool call the ledger keeps started at 2026-11-21 08:00, so the 2 requests before it may be missing their tool calls.",
            Notes(page));
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
```

Edit 4 of 4 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
    }

    private static ImportSessionCheckpoint Checkpoint(long ambiguous) => new()
    {
```

with:

```csharp
    }

    // A bash call in trace at start.
    private static GenAiSpanLink ToolCall(string trace, DateTimeOffset start) => new()
    {
        TraceId = trace,
        SpanId = trace[..2] + "00000000000099",
        OperationName = "execute_tool",
        ConversationId = "conv-x",
        StartTime = start,
        ToolName = "bash",
        ToolExitCode = 0,
    };

    private static ImportSessionCheckpoint Checkpoint(long ambiguous) => new()
    {
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs`, replace:

```csharp
    }

    [Fact]
    public async Task XR5_ProvenanceListsTheRateCardsThatPricedTheSelection()
    {
```

with:

```csharp
    }

    [Fact]
    public async Task XR11_ImportedToolCallsWithoutDetailAreWhatEachRequestLacks()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        DateTimeOffset toolHistoryFrom = new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);
        string i1 = ImportEntries.Copilot("k1", "conv-i", null).Span.TraceId;
        List<long> withoutDetail = [];

        // I1 owns the link of one tool call: it lacks none of 1, and 2 of 3.
        foreach (int count in new[] { 1, 3 })
        {
            await LedgerSql.ExecuteAsync(
                store.DatabasePath,
                "UPDATE spans SET imported_tool_call_count = @p0 WHERE trace_id = @p1;",
                count,
                i1);
            await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
            withoutDetail.Add((await snapshot.GetCoverageAsync(LedgerFilter.All, toolHistoryFrom)).ImportedToolCallsWithoutDetail);
        }

        Assert.Equal([0L, 2L], withoutDetail);
    }

    [Fact]
    public async Task XR12_TheEarliestToolCallIsTheWholeLedgers()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore empty = new(Path.Combine(temp.Path, "empty.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await empty.InitializeAsync();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        List<DateTimeOffset?> earliest = [];

        foreach (SqliteLedgerStore ledger in new[] { empty, store })
        {
            await using LedgerExportSnapshot snapshot = await ledger.OpenExportSnapshotAsync();
            earliest.Add(await snapshot.GetEarliestToolCallStartAsync());
        }

        // Once I1's, I2's, T1, and T2 are deleted, T5 (A's invoke_agent link, at 14:00:50) is the earliest link, but T3 the
        // earliest tool call.
        await store.PruneSpanLinksAsync(new DateTimeOffset(2026, 11, 21, 14, 0, 45, TimeSpan.Zero));
        await using (LedgerExportSnapshot pruned = await store.OpenExportSnapshotAsync())
        {
            earliest.Add(await pruned.GetEarliestToolCallStartAsync());
        }

        Assert.Equal([null, ImportEntries.Start, new DateTimeOffset(2026, 11, 21, 15, 0, 10, TimeSpan.Zero)], earliest);
    }

    [Fact]
    public async Task XR5_ProvenanceListsTheRateCardsThatPricedTheSelection()
    {
```

### Tests

- **XR11** I1 owns the link of one tool call: with `imported_tool_call_count` 1 it lacks no detail, and with 3 it lacks 2.
- **XR12** The earliest tool call is null in an empty ledger and `2026-11-20T12:00:00Z` (I1's and I2's) in `ExportSeeds`; once the links before 14:00:45 on 11-21 are deleted, it's T3's, at 15:00:10, though T5, an invoke_agent link at 14:00:50, is earlier.
- **EC8** (changed) The request of 11-10 has a tool call at 12:00:30, so tool history still starts 7 days before `generatedAt`, and the note is unchanged.
- **EC13** With a tool call at 13:00 on 11-21 and every link before 11-21 deleted, an export at `2026-11-21T21:30:00Z` has `retainedFromUtc` `2026-11-21T13:00:00.0000000+00:00`, 2 requests before it (I1 and J1), `complete` false, `Tool calls: partial; see the notes.`, and the note `The earliest tool call the ledger keeps started at 2026-11-21 08:00, so the 2 requests before it may be missing their tool calls.`

### Acceptance criteria

- Tool history is complete only when no selected request may have lost a tool call to retention, to the daemon's deletions, or to an import that kept no detail; an unchanged ledger still replays byte for byte.
- `dotnet test CopilotLedger.slnx` passes **1392** tests (1389 + 3).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 16 specification: Pages Seek the Start-Time Indexes

Review finding C2, from the coordinator's review. `AfterCursorSql`, `(@after_start IS NULL OR (start_time_unix_ms, trace_id, span_id) > (…))`, together with `FilterSql`'s `@from IS NULL OR …`, left SQLite no index to seek. So every 1,000-row page of `spans.json` scanned and sorted the whole table, and every page of `tool-calls.json` built the list of every selected trace again (the plans and times are under "Review fixes"). The bundle reads each file twice, once to hash it and once to write it. The rows don't change, only how fast they come.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs` | `AfterCursorSql` with a plain lower bound, and a first cursor at the period's start; `ExportToolCallWhereSql` with a correlated `EXISTS` |
| `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs` | New: XR13 |

### Rules

1. A page reads the rows with `start_time_unix_ms >= @after_start AND (start_time_unix_ms, trace_id, span_id) > (@after_start, @after_trace, @after_span)`, in that order, up to its limit. The plain bound lets SQLite seek `ix_spans_start_time`, or `ix_span_links_start` for tool calls, and stop after the limit.
2. Without a cursor, `@after_start` is the filter's `From` in Unix milliseconds, or `long.MinValue`, and `@after_trace` and `@after_span` are empty, which sorts before every ID, so the first page starts at the period's first row.
3. A tool call belongs to the selection when `EXISTS (SELECT 1 FROM spans AS chain WHERE chain.trace_id = span_links.trace_id AND <FilterSql>)`: a lookup in `ix_spans_trace` for each link, instead of an `IN` list that every page built again. The tool-call count uses the same condition.
4. The snapshot stays read-only, with no temporary table.

### Code (copy exactly)

Edit 1 of 3 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
        """;

    // The execute_tool links that count, start in the filter's period, and belong to a prompt chain with a selected span.
    // Unqualified columns are span_links' outside the subquery and spans' inside it.
    private const string ExportToolCallWhereSql = $"""
        operation_name = 'execute_tool'
```

with:

```csharp
        """;

    // The execute_tool links that count, start in the filter's period, and belong to a prompt chain with a selected span: a
    // correlated EXISTS, which finds each link's trace through ix_spans_trace, rather than an IN list of every selected trace
    // that each page would build again. Unqualified columns are span_links' outside the subquery and spans' inside it.
    private const string ExportToolCallWhereSql = $"""
        operation_name = 'execute_tool'
```

Edit 2 of 3 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
          AND (@from IS NULL OR start_time_unix_ms >= @from)
          AND (@to IS NULL OR start_time_unix_ms < @to)
          AND trace_id IN (SELECT trace_id FROM spans WHERE {FilterSql})
        """;

    // Rows after the cursor in (start time, trace ID, span ID) order, or every row when there's no cursor.
    private const string AfterCursorSql =
        "(@after_start IS NULL OR (start_time_unix_ms, trace_id, span_id) > (@after_start, @after_trace, @after_span))";

    internal static Task<IReadOnlyList<ExportSpanRow>> ReadExportSpansAsync(
```

with:

```csharp
          AND (@from IS NULL OR start_time_unix_ms >= @from)
          AND (@to IS NULL OR start_time_unix_ms < @to)
          AND EXISTS (SELECT 1 FROM spans AS chain WHERE chain.trace_id = span_links.trace_id AND {FilterSql})
        """;

    // Rows after the cursor in (start time, trace ID, span ID) order. The plain lower bound on start_time_unix_ms lets every
    // page seek ix_spans_start_time or ix_span_links_start instead of scanning and sorting the whole table; the first page's
    // cursor is the period's start, or the earliest time, with empty IDs, which sorts before every row (ReadExportRowsAsync).
    private const string AfterCursorSql = """
        start_time_unix_ms >= @after_start
          AND (start_time_unix_ms, trace_id, span_id) > (@after_start, @after_trace, @after_span)
        """;

    internal static Task<IReadOnlyList<ExportSpanRow>> ReadExportSpansAsync(
```

Edit 3 of 3 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
        command.CommandText = sql;
        AddFilterParameters(command, filter);
        command.Parameters.AddWithValue("@after_start", OrDbNull(after?.StartTimeUnixMs));
        command.Parameters.AddWithValue("@after_trace", OrDbNull(after?.TraceId));
        command.Parameters.AddWithValue("@after_span", OrDbNull(after?.SpanId));
        command.Parameters.AddWithValue("@limit", limit);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
```

with:

```csharp
        command.CommandText = sql;
        AddFilterParameters(command, filter);
        command.Parameters.AddWithValue("@after_start", after?.StartTimeUnixMs ?? filter.From?.ToUnixTimeMilliseconds() ?? long.MinValue);
        command.Parameters.AddWithValue("@after_trace", after?.TraceId ?? string.Empty);
        command.Parameters.AddWithValue("@after_span", after?.SpanId ?? string.Empty);
        command.Parameters.AddWithValue("@limit", limit);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs`, replace:

```csharp
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Import;
```

with:

```csharp
using System.Globalization;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Import;
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs`, replace:

```csharp
    }

    [Fact]
    public async Task XR5_ProvenanceListsTheRateCardsThatPricedTheSelection()
```

with:

```csharp
    }

    // Pages seek past rows that start in the same millisecond, from the period's first, and skip or repeat none.
    [Fact]
    public async Task XR13_PagesSplitRowsThatStartTogether()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        DateTimeOffset start = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        string[] traces = [.. "12345".Select(digit => new string(digit, 32))];
        await store.RecordAsync(
        [
            .. traces.SelectMany(trace => new[] { Request(trace, 2, start), Request(trace, 1, start) }),
            Request(traces[0], 3, start.AddMilliseconds(-1)),
            Request(traces[4], 3, start.AddSeconds(1)),
        ]);
        await store.RecordSpanLinksAsync(
        [
            .. traces.Take(3).SelectMany(trace => new[] { ToolCall(trace, 2, start.AddSeconds(30)), ToolCall(trace, 1, start.AddSeconds(30)) }),
            ToolCall(new string('9', 32), 1, start.AddSeconds(30)),
        ]);
        LedgerFilter filter = LedgerFilter.All with { From = start, To = start.AddHours(1) };
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        List<string> spans = [];
        ExportCursor? after = null;
        IReadOnlyList<ExportSpanRow> page;
        while ((page = await snapshot.GetSpansAsync(filter, after, 3)).Count > 0)
        {
            spans.AddRange(page.Select(row => $"{row.TraceId[0]}{row.SpanId[^1]}"));
            after = new ExportCursor(page[^1].StartTime.ToUnixTimeMilliseconds(), page[^1].TraceId, page[^1].SpanId);
        }

        List<string> toolCalls = [];
        after = null;
        IReadOnlyList<ExportToolCallRow> calls;
        while ((calls = await snapshot.GetToolCallsAsync(filter, after, 4)).Count > 0)
        {
            toolCalls.AddRange(calls.Select(row => $"{row.TraceId[0]}{row.SpanId[^1]}"));
            after = new ExportCursor(calls[^1].StartTime.ToUnixTimeMilliseconds(), calls[^1].TraceId, calls[^1].SpanId);
        }

        Assert.Equal(["11", "12", "21", "22", "31", "32", "41", "42", "51", "52", "53"], spans);
        Assert.Equal(["11", "12", "21", "22", "31", "32"], toolCalls);
    }

    [Fact]
    public async Task XR5_ProvenanceListsTheRateCardsThatPricedTheSelection()
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs`, replace:

```csharp
        Assert.Equal(trips, (await snapshot.GetBreakerTripsAsync(filter)).Count);
    }
}
```

with:

```csharp
        Assert.Equal(trips, (await snapshot.GetBreakerTripsAsync(filter)).Count);
    }

    // A claude-haiku-4.5 request, the index-th of trace, at start.
    private static LedgerEntry Request(string trace, int index, DateTimeOffset start)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: start,
            requestModel: "claude-haiku-4.5",
            conversationId: "conv-" + trace[0],
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = trace,
            SpanId = ExportSeeds.SpanId(trace, index),
        };
        return new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null);
    }

    // A bash call, the index-th of trace, at start.
    private static GenAiSpanLink ToolCall(string trace, int index, DateTimeOffset start) => new()
    {
        TraceId = trace,
        SpanId = "7" + trace[0] + "0000000000000" + index.ToString(CultureInfo.InvariantCulture),
        OperationName = "execute_tool",
        ConversationId = "conv-" + trace[0],
        StartTime = start,
        ToolName = "bash",
        ToolExitCode = 0,
    };
}
```

### Tests

- **XR13** Ten requests that start in the same millisecond, one a millisecond before the period (left out), and one a second after the ten, read 3 at a time, come back once each, in (start, trace, span) order; so do six tool calls that start together in three of the traces, read 4 at a time, without the tool call of a trace that has no request. XR13 passes before this task too, because the old queries were slow, not wrong. A test can't check a query plan without the store's SQL, which is private, so the plans were checked by the probe under "Review fixes".

### Acceptance criteria

- Pages seek the start-time indexes and return the same rows as before.
- `dotnet test CopilotLedger.slnx` passes **1393** tests (1392 + 1).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 17 specification: The Timeline Draws Every Request, Labels Only Real Times, and Lets the Page Scroll

Review findings C1, R7 (C3), R8, and A2:

- **C1.** A bar was `Math.Max(X(end) - X(start), 0)` wide, to three decimals, so a request without a duration (every Codex import, and Copilot imports without one) or shorter than about 1.2 s on a 30-day axis was `width="0"`, which SVG never draws, though the note says every request is drawn.
- **R7 (C3).** `ExportTime.ToUtc` moves a skipped local time to the gap's end, so local 02:00 on 2026-03-08 in New York became the instant of 03:00 with the label `02:00`, and the real 03:00 was dropped as its duplicate. The comment said the opposite.
- **R8.** Ticks were as fine as the whole range needed (6 hours over 30 days), but the script zoomed down to 0.5 units (about 22 minutes there), where a view can hold no tick and so no label.
- **A2.** The wheel always zoomed, and `#timeline-plot` had `touch-action: none`, so a timeline of many rows, up to 200 of 16 units each, trapped the page's scrolling under the pointer and under a finger.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/TimelineLayout.cs` | `MinimumBarWidth`; `Ticks` rewritten; `Step`, `AxisLevel`, and `MinWidth` |
| `src/CopilotLedger.Cli/Export/ExportReportModel.cs` | `ExportTimeline.MinWidth` and `AxisLevel` |
| `src/CopilotLedger.Cli/Export/ExportSvg.cs` | The axis draws lines down to `AxisLevel` |
| `src/CopilotLedger.Cli/Export/ExportJson.cs` | `timeline.minWidth` in `ledger-data` |
| `src/CopilotLedger.Cli/Export/Assets/report.js` | Every zoom stops at `minWidth`; the wheel zooms only with Ctrl or Command |
| `src/CopilotLedger.Cli/Export/Assets/report.css` | `#timeline-plot` has `touch-action: pan-y` |
| `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs` | The help text |
| `docs/cli.md` | The Timeline bullet; `minWidth` in the allowlist |
| `tests/CopilotLedger.Tests/Cli/ExportTimelineTests.cs` | TL7's ticks; new: TL11, TL12, TL13 |
| `tests/CopilotLedger.Tests/Cli/ExportAssetTests.cs` | New: EA4 |

### Rules

1. A bar is at least 0.001 units wide (`MinimumBarWidth`), and its non-scaling 1-pixel stroke shows it at any zoom.
2. Ticks are local boundaries from t0 to t1, in time order, as fine as `Step`: 10 minutes within 7 days, an hour within 62, 6 hours within 366, and else a day, thinned as before beyond 2,000 days. A boundary that a transition skips is no tick, except midnight, whose tick is then the day's first instant, and wins over an hour's tick at that instant. A boundary that a transition repeats is a tick at each of its instants, with the same label.
3. The axis draws a line only for ticks down to `AxisLevel`, the levels it drew before: 10-minute marks within a day, hours within 7 days, 6-hour marks within 62, and else days. The finer ticks are in `ledger-data`, for the script's labels.
4. `ledger-data`'s `timeline.minWidth`, after `width`, is twice the widest gap between neighboring ticks, or between an end of the axis and its nearest tick, and at least 0.5. The script's every zoom (wheel, pinch, keys, and the overview) stops at it, so every view holds at least two ticks to label.
5. The wheel zooms only with Ctrl or Command held, or in a trackpad pinch, which browsers send as a wheel with `ctrlKey`; otherwise the page scrolls. `#timeline-plot` has `touch-action: pan-y`, so a vertical swipe scrolls the page and a sideways drag or a pinch still reaches the script. The help text and `docs/cli.md` say so.

### Code (copy exactly)

Edit 1 of 2 in `docs/cli.md`, replace:

```markdown
- **Summary:** requests, GitHub AI credits, Foundry BYOK, API equivalent, local savings, cache hit ratio, prompt chains, breaker trips, and budgets. Each card's tooltip holds the exact amount.
- **Coverage:** whether the requests are complete, whether the tool calls are, and notes on what's missing: imported requests that live spans recorded too (each counted once), imported requests that live spans of their sessions replaced without matching them (see [Overlap with live spans](#overlap-with-live-spans)), imports that deferred requests or didn't finish (counted across the whole ledger, because imports aren't kept per repository), requests older than the tool history, unpriced requests, and requests without cache counters. Imports that were replaced without a match, deferred, or not finished may hide requests, so they make the requests incomplete. Tool history is the 7 days before the generation time, but it starts no earlier than the earliest tool call the ledger keeps, because the daemon deletes older tool calls by its own clock; so an export with a past `--generated-at` doesn't count on tool calls that were deleted since. These limits are also in the bundle's `coverage` object.
- **Timeline:** a row per Copilot session, a bar per request colored by model, a tick per tool call (red when it failed, with no duration, which the ledger doesn't keep), and nights shaded from 18:00 to 06:00 in the export's time zone. Scroll or pinch to zoom, drag to pan, or drag across the overview. When there are more than `--max-timeline-spans` requests or tool calls, it draws the latest ones; the totals and tables still include them all.
- **Cost by model and agent:** billed spend (GitHub AI credits, then Foundry BYOK), the API-equivalent view, and local savings, each on its own scale. The comparison views are never added to billed spend.
- **Cache efficiency, prompt chains, spend tables, and the circuit breaker:** the reports' numbers, with sortable columns.
```

with:

```markdown
- **Summary:** requests, GitHub AI credits, Foundry BYOK, API equivalent, local savings, cache hit ratio, prompt chains, breaker trips, and budgets. Each card's tooltip holds the exact amount.
- **Coverage:** whether the requests are complete, whether the tool calls are, and notes on what's missing: imported requests that live spans recorded too (each counted once), imported requests that live spans of their sessions replaced without matching them (see [Overlap with live spans](#overlap-with-live-spans)), imports that deferred requests or didn't finish (counted across the whole ledger, because imports aren't kept per repository), requests older than the tool history, unpriced requests, and requests without cache counters. Imports that were replaced without a match, deferred, or not finished may hide requests, so they make the requests incomplete. Tool history is the 7 days before the generation time, but it starts no earlier than the earliest tool call the ledger keeps, because the daemon deletes older tool calls by its own clock; so an export with a past `--generated-at` doesn't count on tool calls that were deleted since. These limits are also in the bundle's `coverage` object.
- **Timeline:** a row per Copilot session, a bar per request colored by model, a tick per tool call (red when it failed, with no duration, which the ledger doesn't keep), and nights shaded from 18:00 to 06:00 in the export's time zone. Hold Ctrl or Command and scroll, or pinch, to zoom, as far as the axis can still label two times; drag sideways to pan; or drag across the overview. When there are more than `--max-timeline-spans` requests or tool calls, it draws the latest ones; the totals and tables still include them all.
- **Cost by model and agent:** billed spend (GitHub AI credits, then Foundry BYOK), the API-equivalent view, and local savings, each on its own scale. The comparison views are never added to billed spend.
- **Cache efficiency, prompt chains, spend tables, and the circuit breaker:** the reports' numbers, with sortable columns.
```

Edit 2 of 2 in `docs/cli.md`, replace:

```markdown
| `manifest.json` | `files[]` | `path`, `bytes`, `sha256` |
| `report.html` | `.` | `schemaVersion`, `generatedAtUtc`, `toolVersion`, `filter`, `timeZone`, `timeline`, `coverage` |
| `report.html` | `timeline` | `fromUtc`, `toUtc`, `width`, `ticks` |
| `report.html` | `timeline.ticks[]` | `x`, `level`, `label` |
| `manifest.json`, `report.html` | `filter` | `repo`, `allRepositories`, `branch`, `pr`, `conversation`, `since`, `period`, `fromUtc`, `toUtc` |
```

with:

```markdown
| `manifest.json` | `files[]` | `path`, `bytes`, `sha256` |
| `report.html` | `.` | `schemaVersion`, `generatedAtUtc`, `toolVersion`, `filter`, `timeZone`, `timeline`, `coverage` |
| `report.html` | `timeline` | `fromUtc`, `toUtc`, `width`, `minWidth`, `ticks` |
| `report.html` | `timeline.ticks[]` | `x`, `level`, `label` |
| `manifest.json`, `report.html` | `filter` | `repo`, `allRepositories`, `branch`, `pr`, `conversation`, `since`, `period`, `fromUtc`, `toUtc` |
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/Assets/report.css`, replace:

```
.timeline-viewport { min-width: 0; }
.timeline-viewport > svg { display: block; width: 100%; }
#timeline-plot { touch-action: none; cursor: grab; }
#timeline-overview { touch-action: none; cursor: crosshair; margin-top: 4px; }
.axis-labels { position: relative; height: 18px; font-size: 11px; color: var(--muted); }
```

with:

```
.timeline-viewport { min-width: 0; }
.timeline-viewport > svg { display: block; width: 100%; }
#timeline-plot { touch-action: pan-y; cursor: grab; }
#timeline-overview { touch-action: none; cursor: crosshair; margin-top: 4px; }
.axis-labels { position: relative; height: 18px; font-size: 11px; color: var(--muted); }
```

Edit 1 of 5 in `src/CopilotLedger.Cli/Export/Assets/report.js`, replace:

```
    }

    var width = timeline.width;
    var height = Number(plot.getAttribute('height'));
    var ticks = Array.isArray(timeline.ticks) ? timeline.ticks : [];
```

with:

```
    }

    var width = timeline.width;
    var minWidth = timeline.minWidth;
    var height = Number(plot.getAttribute('height'));
    var ticks = Array.isArray(timeline.ticks) ? timeline.ticks : [];
```

Edit 2 of 5 in `src/CopilotLedger.Cli/Export/Assets/report.js`, replace:

```
    }

    function apply() {
      view.w = clamp(view.w, 0.5, width);
      view.x = clamp(view.x, 0, width - view.w);
      plot.setAttribute('viewBox', view.x + ' 0 ' + view.w + ' ' + height);
```

with:

```
    }

    function apply() {
      view.w = clamp(view.w, minWidth, width);
      view.x = clamp(view.x, 0, width - view.w);
      plot.setAttribute('viewBox', view.x + ' 0 ' + view.w + ' ' + height);
```

Edit 3 of 5 in `src/CopilotLedger.Cli/Export/Assets/report.js`, replace:

```
    function zoom(factor, unit) {
      var ratio = (unit - view.x) / view.w;
      view.w = clamp(view.w * factor, 0.5, width);
      view.x = unit - ratio * view.w;
      apply();
```

with:

```
    function zoom(factor, unit) {
      var ratio = (unit - view.x) / view.w;
      view.w = clamp(view.w * factor, minWidth, width);
      view.x = unit - ratio * view.w;
      apply();
```

Edit 4 of 5 in `src/CopilotLedger.Cli/Export/Assets/report.js`, replace:

```
    }

    viewport.addEventListener('wheel', function (event) {
      event.preventDefault();
      zoom(event.deltaY < 0 ? 1 / 1.25 : 1.25, unitAt(event.clientX));
```

with:

```
    }

    // Ctrl or Command with the wheel zooms, as does a trackpad pinch, which browsers send as a wheel with ctrlKey set. A plain
    // scroll is left to the page: a timeline of many rows is taller than the window, and would otherwise trap it.
    viewport.addEventListener('wheel', function (event) {
      if (!(event.ctrlKey || event.metaKey) || event.deltaY === 0) {
        return;
      }

      event.preventDefault();
      zoom(event.deltaY < 0 ? 1 / 1.25 : 1.25, unitAt(event.clientX));
```

Edit 5 of 5 in `src/CopilotLedger.Cli/Export/Assets/report.js`, replace:

```
        var box = plot.getBoundingClientRect();
        var middle = (positions[0] + positions[1]) / 2;
        view.w = clamp(pinch.w * pinch.distance / distance, 0.5, width);
        view.x = pinch.unit - (middle - box.left) / Math.max(box.width, 1) * view.w;
        apply();
```

with:

```
        var box = plot.getBoundingClientRect();
        var middle = (positions[0] + positions[1]) / 2;
        view.w = clamp(pinch.w * pinch.distance / distance, minWidth, width);
        view.x = pinch.unit - (middle - box.left) / Math.max(box.width, 1) * view.w;
        apply();
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportJson.cs`, replace:

```csharp
                LedgerFormat.WriteTime(writer, "toUtc", DateTimeOffset.FromUnixTimeMilliseconds(timeline.ToUnixMs));
                writer.WriteNumber("width", TimelineLayout.Width);
                writer.WriteStartArray("ticks");
                foreach (ExportAxisTick tick in timeline.Ticks)
```

with:

```csharp
                LedgerFormat.WriteTime(writer, "toUtc", DateTimeOffset.FromUnixTimeMilliseconds(timeline.ToUnixMs));
                writer.WriteNumber("width", TimelineLayout.Width);
                writer.WritePropertyName("minWidth");
                writer.WriteRawValue(Number(timeline.MinWidth));
                writer.WriteStartArray("ticks");
                foreach (ExportAxisTick tick in timeline.Ticks)
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
    public required IReadOnlyList<UtcBand> NightBands { get; init; }
    public required IReadOnlyList<ExportAxisTick> Ticks { get; init; }
    public required IReadOnlyList<string> Models { get; init; }                   // the models of colors m0-m6: the model rollup's first 7
    public required bool OtherModels { get; init; }                               // some drawn request has color m7
```

with:

```csharp
    public required IReadOnlyList<UtcBand> NightBands { get; init; }
    public required IReadOnlyList<ExportAxisTick> Ticks { get; init; }
    public required decimal MinWidth { get; init; }                               // the narrowest view the script zooms to
    public required int AxisLevel { get; init; }                                  // the finest tick level the axis draws a line for
    public required IReadOnlyList<string> Models { get; init; }                   // the models of colors m0-m6: the model rollup's first 7
    public required bool OtherModels { get; init; }                               // some drawn request has color m7
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportSvg.cs`, replace:

```csharp
        Line(html, "</svg>");
        Line(html, "<svg id=\"timeline-axis\" class=\"timeline-axis\" aria-hidden=\"true\" viewBox=\"0 0 1000 6\" preserveAspectRatio=\"none\" height=\"6\">");
        foreach (ExportAxisTick tick in timeline.Ticks)
        {
            Line(html, $"<line class=\"tick\" x1=\"{ExportJson.Number(tick.X)}\" y1=\"0\" x2=\"{ExportJson.Number(tick.X)}\" y2=\"6\" />");
```

with:

```csharp
        Line(html, "</svg>");
        Line(html, "<svg id=\"timeline-axis\" class=\"timeline-axis\" aria-hidden=\"true\" viewBox=\"0 0 1000 6\" preserveAspectRatio=\"none\" height=\"6\">");
        foreach (ExportAxisTick tick in timeline.Ticks.Where(tick => tick.Level <= timeline.AxisLevel))
        {
            Line(html, $"<line class=\"tick\" x1=\"{ExportJson.Number(tick.X)}\" y1=\"0\" x2=\"{ExportJson.Number(tick.X)}\" y2=\"6\" />");
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
            Line(
                html,
                "<p id=\"timeline-help\">Scroll or pinch to zoom and drag to pan, or focus the timeline and use the arrow keys, + and -, and 0. Drag across the overview to choose a range.</p></div>");
            Line(html, "<figure class=\"chart\" id=\"timeline-figure\">");
            ExportSvg.Timeline(html, timeline, zone);
```

with:

```csharp
            Line(
                html,
                "<p id=\"timeline-help\">Hold Ctrl or Command and scroll, or pinch, to zoom; drag sideways to pan; or focus the timeline and use the arrow keys, + and -, and 0. Drag across the overview to choose a range.</p></div>");
            Line(html, "<figure class=\"chart\" id=\"timeline-figure\">");
            ExportSvg.Timeline(html, timeline, zone);
```

Edit 1 of 7 in `src/CopilotLedger.Cli/Export/TimelineLayout.cs`, replace:

```csharp
    private const long DayMs = 86_400_000;

    public static readonly ExportTimeline Empty = new()
    {
```

with:

```csharp
    private const long DayMs = 86_400_000;

    // The narrowest bar: SVG draws no rectangle 0 wide, and a request without a duration still shows as its 1-pixel stroke.
    private const decimal MinimumBarWidth = 0.001m;

    public static readonly ExportTimeline Empty = new()
    {
```

Edit 2 of 7 in `src/CopilotLedger.Cli/Export/TimelineLayout.cs`, replace:

```csharp
        NightBands = [],
        Ticks = [],
        Models = [],
        OtherModels = false,
```

with:

```csharp
        NightBands = [],
        Ticks = [],
        MinWidth = Width,
        AxisLevel = 0,
        Models = [],
        OtherModels = false,
```

Edit 3 of 7 in `src/CopilotLedger.Cli/Export/TimelineLayout.cs`, replace:

```csharp
                decimal end = X(span.EndTime.ToUnixTimeMilliseconds());
                int color = span.Model is null ? -1 : colors.IndexOf(span.Model);
                return new ExportTimelineSpan(span, GroupOf(span.ConversationId).Row, start, Math.Max(end - start, 0m), color < 0 ? 7 : color);
            }),
        ];
```

with:

```csharp
                decimal end = X(span.EndTime.ToUnixTimeMilliseconds());
                int color = span.Model is null ? -1 : colors.IndexOf(span.Model);
                return new ExportTimelineSpan(span, GroupOf(span.ConversationId).Row, start, Math.Max(end - start, MinimumBarWidth), color < 0 ? 7 : color);
            }),
        ];
```

Edit 4 of 7 in `src/CopilotLedger.Cli/Export/TimelineLayout.cs`, replace:

```csharp
        }

        return new ExportTimeline
        {
```

with:

```csharp
        }

        IReadOnlyList<ExportAxisTick> axis = Ticks(t0, t1, zone, X);
        return new ExportTimeline
        {
```

Edit 5 of 7 in `src/CopilotLedger.Cli/Export/TimelineLayout.cs`, replace:

```csharp
                DateTimeOffset.FromUnixTimeMilliseconds(t0),
                DateTimeOffset.FromUnixTimeMilliseconds(t1)),
            Ticks = Ticks(t0, t1, zone, X),
            Models = colors,
            OtherModels = bars.Any(bar => bar.Color == 7),
```

with:

```csharp
                DateTimeOffset.FromUnixTimeMilliseconds(t0),
                DateTimeOffset.FromUnixTimeMilliseconds(t1)),
            Ticks = axis,
            MinWidth = MinWidth(axis),
            AxisLevel = AxisLevel(t1 - t0),
            Models = colors,
            OtherModels = bars.Any(bar => bar.Color == 7),
```

Edit 6 of 7 in `src/CopilotLedger.Cli/Export/TimelineLayout.cs`, replace:

```csharp
    }

    // Local boundaries from t0 to t1 as ticks: level 0 at midnight, 1 at 06:00, 12:00, and 18:00 within 62 days, 2 at other
    // whole hours within 7 days, and 3 at other 10-minute marks within a day. A boundary that a transition skips maps to the
    // instant of the previous one and is dropped.
    private static IReadOnlyList<ExportAxisTick> Ticks(long t0, long t1, TimeZoneInfo zone, Func<long, decimal> x)
    {
        long range = t1 - t0;
        TimeSpan step = range <= DayMs
            ? TimeSpan.FromMinutes(10)
            : range <= 7 * DayMs
                ? TimeSpan.FromHours(1)
                : range <= 62 * DayMs ? TimeSpan.FromHours(6) : TimeSpan.FromDays(1);
        DateTime end = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(t1), zone).DateTime;
        List<ExportAxisTick> ticks = [];
        long previous = long.MinValue;
        for (DateTime local = ExportTime.LocalDate(DateTimeOffset.FromUnixTimeMilliseconds(t0), zone).ToDateTime(TimeOnly.MinValue);
             local <= end;
             local += step)
        {
            long instant = ExportTime.ToUtc(local, zone).ToUnixTimeMilliseconds();
            if (instant < t0 || instant > t1 || instant == previous)
            {
                continue;
            }

            previous = instant;
            int level = local.TimeOfDay == TimeSpan.Zero ? 0 : local.Minute != 0 ? 3 : local.Hour % 6 == 0 ? 1 : 2;
            ticks.Add(new ExportAxisTick(
                x(instant),
                level,
                local.ToString(level == 0 ? "yyyy-MM-dd" : "HH:mm", CultureInfo.InvariantCulture)));
        }

        int days = ticks.Count(tick => tick.Level == 0);
```

with:

```csharp
    }

    // Local boundaries from t0 to t1 as ticks, in time order: level 0 at midnight, 1 at 06:00, 12:00, and 18:00, 2 at other
    // whole hours, and 3 at other 10-minute marks, down to Step's. A boundary that a transition skips isn't a tick, except
    // midnight, whose tick is then the day's first instant; one that a transition repeats is a tick each time it occurs.
    private static IReadOnlyList<ExportAxisTick> Ticks(long t0, long t1, TimeZoneInfo zone, Func<long, decimal> x)
    {
        TimeSpan step = Step(t1 - t0);
        DateTime end = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(t1), zone).DateTime;
        List<(long Instant, int Level, string Label)> boundaries = [];
        for (DateTime local = ExportTime.LocalDate(DateTimeOffset.FromUnixTimeMilliseconds(t0), zone).ToDateTime(TimeOnly.MinValue);
             local <= end;
             local += step)
        {
            if (zone.IsInvalidTime(local) && local.TimeOfDay != TimeSpan.Zero)
            {
                continue;
            }

            int level = local.TimeOfDay == TimeSpan.Zero ? 0 : local.Minute != 0 ? 3 : local.Hour % 6 == 0 ? 1 : 2;
            string label = local.ToString(level == 0 ? "yyyy-MM-dd" : "HH:mm", CultureInfo.InvariantCulture);
            long[] instants = zone.IsAmbiguousTime(local)
                ? [.. zone.GetAmbiguousTimeOffsets(local).Select(offset => new DateTimeOffset(local, offset).ToUnixTimeMilliseconds())]
                : [ExportTime.ToUtc(local, zone).ToUnixTimeMilliseconds()];
            boundaries.AddRange(instants.Where(instant => instant >= t0 && instant <= t1).Select(instant => (instant, level, label)));
        }

        List<ExportAxisTick> ticks =
        [
            .. boundaries
                .OrderBy(boundary => boundary.Instant)
                .ThenBy(boundary => boundary.Level)
                .DistinctBy(boundary => boundary.Instant)
                .Select(boundary => new ExportAxisTick(x(boundary.Instant), boundary.Level, boundary.Label)),
        ];

        int days = ticks.Count(tick => tick.Level == 0);
```

Edit 7 of 7 in `src/CopilotLedger.Cli/Export/TimelineLayout.cs`, replace:

```csharp
        }

        return ticks;
    }

    private static ExportTimelineRow Row(
```

with:

```csharp
        }

        return ticks;
    }

    // The spacing of a range's finest ticks: 10 minutes within 7 days, an hour within 62, 6 hours within 366, else a day. They
    // are finer than the axis's lines (AxisLevel), so the script has times to label wherever it zooms.
    private static TimeSpan Step(long range) =>
        range <= 7 * DayMs ? TimeSpan.FromMinutes(10)
        : range <= 62 * DayMs ? TimeSpan.FromHours(1)
        : range <= 366 * DayMs ? TimeSpan.FromHours(6)
        : TimeSpan.FromDays(1);

    // The finest level the axis draws a line for: 10-minute marks within a day, hours within 7 days, 6-hour marks within 62,
    // else days.
    private static int AxisLevel(long range) =>
        range <= DayMs ? 3 : range <= 7 * DayMs ? 2 : range <= 62 * DayMs ? 1 : 0;

    // The narrowest view the script zooms to: twice the widest gap between two neighboring ticks, or between an end of the
    // axis and its nearest tick, so every view holds at least two ticks to label; at least half a unit.
    private static decimal MinWidth(IReadOnlyList<ExportAxisTick> ticks)
    {
        decimal widest = ticks.Count == 0 ? Width : Math.Max(ticks[0].X, Width - ticks[^1].X);
        for (int index = 1; index < ticks.Count; index++)
        {
            widest = Math.Max(widest, ticks[index].X - ticks[index - 1].X);
        }

        return Math.Max(2 * widest, 0.5m);
    }

    private static ExportTimelineRow Row(
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/ExportAssetTests.cs`, replace:

```csharp
    }

    private static async Task<string> ExportAsync(string root)
    {
```

with:

```csharp
    }

    [Fact]
    public async Task EA4_APlainScrollOverTheTimelineScrollsThePage()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        string html = await ExportAsync(temp.Path);

        XDocument page = ExportFiles.Parse(html);
        string wheel = html[html.IndexOf("addEventListener('wheel'", StringComparison.Ordinal)..];
        wheel = wheel[..wheel.IndexOf("}, { passive: false });", StringComparison.Ordinal)];
        int guard = wheel.IndexOf("if (!(event.ctrlKey || event.metaKey) || event.deltaY === 0) {", StringComparison.Ordinal);
        Assert.InRange(guard, 0, wheel.IndexOf("return;", StringComparison.Ordinal));
        Assert.True(wheel.IndexOf("return;", StringComparison.Ordinal) < wheel.IndexOf("event.preventDefault();", StringComparison.Ordinal));
        Assert.Contains("#timeline-plot { touch-action: pan-y; cursor: grab; }", html, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "touch-action: none"));
        Assert.StartsWith("Hold Ctrl or Command and scroll, or pinch, to zoom; drag sideways to pan;", ExportFiles.ById(page, "timeline-help").Value);
    }

    private static async Task<string> ExportAsync(string root)
    {
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Cli/ExportTimelineTests.cs`, replace:

```csharp
        using JsonDocument data = ExportFiles.LedgerData(html);

        JsonElement[] ticks = [.. data.RootElement.GetProperty("timeline").GetProperty("ticks").EnumerateArray()];
        Assert.Equal(30, ticks.Length);
        Assert.Equal([0, 1, 2], ticks.Select(tick => tick.GetProperty("level").GetInt32()).Distinct().Order());
        Assert.Equal(("0", 2, "07:00"), (ticks[0].GetProperty("x").GetRawText(), ticks[0].GetProperty("level").GetInt32(), ticks[0].GetProperty("label").GetString()));
        Assert.Equal(
```

with:

```csharp
        using JsonDocument data = ExportFiles.LedgerData(html);

        JsonElement[] ticks = [.. data.RootElement.GetProperty("timeline").GetProperty("ticks").EnumerateArray()];
        Assert.Equal(175, ticks.Length);
        Assert.Equal([0, 1, 2, 3], ticks.Select(tick => tick.GetProperty("level").GetInt32()).Distinct().Order());
        Assert.Equal(("0", 2, "07:00"), (ticks[0].GetProperty("x").GetRawText(), ticks[0].GetProperty("level").GetInt32(), ticks[0].GetProperty("label").GetString()));
        Assert.Equal(
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Cli/ExportTimelineTests.cs`, replace:

```csharp
    }

    private static CliHarness Harness(string root)
    {
```

with:

```csharp
    }

    // SVG draws no rectangle 0 wide, so a request without a duration, such as a Codex import, or one too short for a
    // thousandth of a 4-week axis, is a bar 0.001 wide, which its 1-pixel stroke shows.
    [Fact]
    public async Task TL11_RequestsWithoutADurationAreStillDrawn()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        ImportedLedgerEntry imported = ImportEntries.Copilot("k1", "conv-i", null);
        DateTimeOffset start = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        await store.RecordImportedAsync([imported with { Span = imported.Span with { StartTime = start, EndTime = start } }], null);
        LedgerEntry live = Entry("conv-live", 1, new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        await store.RecordAsync([live with { Span = live.Span with { EndTime = live.Span.StartTime.AddSeconds(1) } }]);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        Assert.Equal(
            ["0|0.001", "1000|0.001"],
            Elements(ExportFiles.ById(page, "timeline-plot"), "rect", "span").Select(rect =>
                $"{(string)rect.Attribute("x")!}|{(string)rect.Attribute("width")!}"));
    }

    // A time that a transition skips is no tick, so 03:00 isn't shown as 02:00; a time that it repeats is a tick each time.
    [Theory]
    [InlineData("2026-03-08T05:00:00Z", "2026-03-08T10:00:00Z", "2026-03-08|01:00|03:00|04:00|05:00|06:00")]
    [InlineData("2026-11-01T04:00:00Z", "2026-11-01T08:00:00Z", "2026-11-01|01:00|01:00|02:00|03:00")]
    public async Task TL12_AxisLabelsAreLocalTimesThatExist(string first, string last, string labels)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        await store.RecordAsync(
        [
            Entry("conv-dst", 1, DateTimeOffset.Parse(first, CultureInfo.InvariantCulture)),
            Entry("conv-dst", 2, DateTimeOffset.Parse(last, CultureInfo.InvariantCulture)),
        ]);

        XDocument page = await ExportAsync(
            Harness(temp.Path),
            temp.Path,
            "--all-repos", "--since", "all", "--time-zone", "Test/New_York", "--generated-at", "2026-12-01T00:00:00Z");

        Assert.Equal(labels.Split('|'), ExportFiles.ById(page, "timeline-axis-labels").Elements("span").Select(span => span.Value));
    }

    // Over 4 weeks the ticks go down to hours, though the axis draws lines only every 6 hours, and zooming stops at twice the
    // widest gap between ticks, so every view has two times to label.
    [Fact]
    public async Task TL13_ZoomingStopsWhereTheAxisStillHasTwoLabels()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        await store.RecordAsync(
        [
            Entry("conv-month", 1, new DateTimeOffset(2026, 10, 23, 5, 0, 0, TimeSpan.Zero)),
            Entry("conv-month", 2, new DateTimeOffset(2026, 11, 21, 17, 0, 0, TimeSpan.Zero)),
        ]);

        await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        using JsonDocument data = ExportFiles.LedgerData(html);
        JsonElement timeline = data.RootElement.GetProperty("timeline");
        JsonElement[] ticks = [.. timeline.GetProperty("ticks").EnumerateArray()];
        decimal[] xs = [.. ticks.Select(tick => tick.GetProperty("x").GetDecimal())];
        decimal widest = new[] { xs[0], 1000m - xs[^1] }.Concat(xs.Zip(xs.Skip(1), (left, right) => right - left)).Max();
        Assert.Equal(709, ticks.Length);
        Assert.Equal([0, 1, 2], ticks.Select(tick => tick.GetProperty("level").GetInt32()).Distinct().Order());
        Assert.Equal(2.826m, timeline.GetProperty("minWidth").GetDecimal());
        Assert.Equal(2 * widest, timeline.GetProperty("minWidth").GetDecimal());
        Assert.Equal(
            ticks.Count(tick => tick.GetProperty("level").GetInt32() <= 1),
            ExportFiles.ById(ExportFiles.Parse(html), "timeline-axis").Elements("line").Count());
        Assert.Equal(3, Regex.Matches(html, ", minWidth, width\\);").Count);
        Assert.DoesNotContain("0.5, width)", html, StringComparison.Ordinal);
    }

    private static CliHarness Harness(string root)
    {
```

### Tests

- **TL7** (changed) The fixture's 29 hours have 175 ticks, at levels 0–3; the labels don't change.
- **TL11** A zero-duration imported request on 10-25 and a 1-second request on 11-21 are bars `x="0" width="0.001"` and `x="1000" width="0.001"`.
- **TL12** (2 cases) In `Test/New_York`, the axis labels for 2026-03-08 05:00–10:00 UTC are `2026-03-08`, `01:00`, `03:00`, `04:00`, `05:00`, and `06:00`, and those for 2026-11-01 04:00–08:00 UTC are `2026-11-01`, `01:00`, `01:00`, `02:00`, and `03:00`.
- **TL13** Over 29.5 days, `ledger-data` has 709 ticks at levels 0–2 and `minWidth` 2.826, twice the widest gap; the axis draws a line for each tick at levels 0 and 1; and the script clamps to `minWidth` three times and never to 0.5.
- **EA4** The wheel listener returns before `preventDefault` unless Ctrl or Command is held; `#timeline-plot` has `touch-action: pan-y`, and only the overview keeps `none`; and the help text starts `Hold Ctrl or Command and scroll, or pinch, to zoom; drag sideways to pan;`.

### Acceptance criteria

- Every drawn request is a visible bar, no axis label is a local time that doesn't exist, every zoom has labels, and a plain scroll over the timeline scrolls the page.
- `dotnet test CopilotLedger.slnx` passes **1398** tests (1393 + 5).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 18 specification: An Encoded Path Shows Only Its Folder Name

Review finding N1. Task 13 took a `file:` URI's last segment as it was written, so `file:///C:%5CUsers%5CAlice%5Cprivate%5Crepo`, whose separators are percent-encoded, became `C:%5CUsers%5CAlice%5Cprivate%5Crepo` in standard output, the page, and `manifest.filter.repo`.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/ExportRecords.cs` | `Unescape`, which `FolderName` and `LooksLikeLocalPath` apply first |
| `tests/CopilotLedger.Tests/Cli/ExportPrivacyTests.cs` | EP7 gains 5 cases |

### Rules

1. `Unescape(text)` applies `Uri.UnescapeDataString` until the text stops changing, so a byte encoded twice (`%255C`) is decoded as well. `Uri.UnescapeDataString` leaves an invalid escape as it is.
2. `LooksLikeLocalPath` judges the decoded key, so `%2Fhome%2F…` is a path, and `FolderName` takes the decoded path's last segment by Task 13's rules. A slug doesn't change: `owner/repo` holds no `%`.

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportRecords.cs`, replace:

```csharp
        !includeLocalPaths && (roots.Contains(key) || LooksLikeLocalPath(key)) ? FolderName(key) : key;

    // A local path's last segment, once trailing / and \ are removed, so it's never a path; (root folder) for a root, such as
    // C:\, /, or \\server\share.
    public static string FolderName(string path)
    {
        string trimmed = path.TrimEnd('/', '\\');
        string name = trimmed[(trimmed.LastIndexOfAny(['/', '\\']) + 1)..];
        return name.Length == 0 || name.EndsWith(':') || IsShareRoot(trimmed) ? "(root folder)" : name;
    }

    // Starts with / or \, with a drive letter and a colon, or with file:.
    public static bool LooksLikeLocalPath(string key) =>
        key.StartsWith('/') || key.StartsWith('\\') || (key.Length >= 2 && char.IsAsciiLetter(key[0]) && key[1] == ':')
        || key.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

    // \\server\share or //server/share, without trailing separators.
```

with:

```csharp
        !includeLocalPaths && (roots.Contains(key) || LooksLikeLocalPath(key)) ? FolderName(key) : key;

    // A local path's last segment, once its percent-encoded bytes are decoded and trailing / and \ removed, so it's never a
    // path; (root folder) for a root, such as C:\, /, or \\server\share.
    public static string FolderName(string path)
    {
        string trimmed = Unescape(path).TrimEnd('/', '\\');
        string name = trimmed[(trimmed.LastIndexOfAny(['/', '\\']) + 1)..];
        return name.Length == 0 || name.EndsWith(':') || IsShareRoot(trimmed) ? "(root folder)" : name;
    }

    // Starts with / or \, with a drive letter and a colon, or with file:, once its percent-encoded bytes are decoded.
    public static bool LooksLikeLocalPath(string key)
    {
        string path = Unescape(key);
        return path.StartsWith('/') || path.StartsWith('\\') || (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
            || path.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
    }

    // text with its percent-encoded bytes decoded until none is left, however deep the encoding, as a file: URI can hold
    // separators: file:///C:%5CUsers%5C…
    private static string Unescape(string text)
    {
        string decoded = Uri.UnescapeDataString(text);
        while (decoded != text)
        {
            text = decoded;
            decoded = Uri.UnescapeDataString(text);
        }

        return text;
    }

    // \\server\share or //server/share, without trailing separators.
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Cli/ExportPrivacyTests.cs`, replace:

```csharp
    }

    // A --repo that's a path shows as its last folder's name, however it ends, or as (root folder); nothing else of it
    // reaches a file or the console.
    [Theory]
    [InlineData("C:/Users/Alice/private-9f3e/project/", "project")]
```

with:

```csharp
    }

    // A --repo that's a path shows as its last folder's name, however it ends or is encoded, or as (root folder); nothing else
    // of it reaches a file or the console.
    [Theory]
    [InlineData("C:/Users/Alice/private-9f3e/project/", "project")]
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Cli/ExportPrivacyTests.cs`, replace:

```csharp
    [InlineData("//fileserver-9f3e/share/project/", "project")]
    [InlineData("file:///C:/Users/Alice/private-9f3e/project/", "project")]
    [InlineData(@"\\fileserver-9f3e\private-9f3e\", "(root folder)")]
    [InlineData(@"C:\", "(root folder)")]
```

with:

```csharp
    [InlineData("//fileserver-9f3e/share/project/", "project")]
    [InlineData("file:///C:/Users/Alice/private-9f3e/project/", "project")]
    [InlineData("file:///C:%5CUsers%5CAlice%5Cprivate-9f3e%5Cproject", "project")]
    [InlineData("FILE:///home/alice%2Fprivate-9f3e%2Fproject%2f", "project")]
    [InlineData("file:///C:/Users/Alice/private-9f3e%5cproject%5C", "project")]
    [InlineData("C:%255CUsers%255CAlice%255Cprivate-9f3e%255Cproject", "project")]
    [InlineData("%2Fhome%2Falice%2Fprivate-9f3e%2Fproject", "project")]
    [InlineData(@"\\fileserver-9f3e\private-9f3e\", "(root folder)")]
    [InlineData(@"C:\", "(root folder)")]
```

### Tests

- **EP7** (5 more cases) `file:///C:%5CUsers%5CAlice%5Cprivate-9f3e%5Cproject`, `FILE:///home/alice%2Fprivate-9f3e%2Fproject%2f`, `file:///C:/Users/Alice/private-9f3e%5cproject%5C`, `C:%255CUsers%255CAlice%255Cprivate-9f3e%255Cproject`, and `%2Fhome%2Falice%2Fprivate-9f3e%2Fproject` each show `project`, and `9f3e` reaches no file.

### Acceptance criteria

- No encoding of a path given to `--repo` reaches a file or the console without `--include-local-paths`.
- `dotnet test CopilotLedger.slnx` passes **1403** tests (1398 + 5).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 19 specification: The Report and the Bundle Are Moved Into Place by One Rule

Review findings N2, N3, and D1:

- **N2.** Task 14 moved aside any ZIP that existed at the move, with or without `--force`, so a ZIP that another program wrote during an export without `--force` was replaced, and its backup deleted.
- **N3.** When the report couldn't be moved into place and the new ZIP couldn't be deleted, the rollback swallowed the failure.
- **D1.** Windows renames a read-only file but won't delete it, so `--force` moved a read-only ZIP aside, replaced it, exited 0, and left the old ZIP behind, still read-only, as `<zip>.<process ID>.bak`. Before Task 14, `File.Move(…, overwrite: true)` had refused it.

And review round 3's N6 and A4:

- **N6.** EC15 as first written wrote the other program's ZIP just before the export moved its own ZIP into place, after the decision to move a ZIP aside, so it passed even when that decision ignored `--force`. No platform call came between the checks and that decision for a test to act at.
- **A4.** No test made a report appear during an export without `--force`, so a report move that overwrote would have passed.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/ICliPlatform.cs` | `MoveFile`, `DeleteFile`, and `WriteFileAsync` |
| `src/CopilotLedger.Cli/SystemCliPlatform.cs` | They call `File.Move`, `File.Delete`, and `File.WriteAllBytesAsync` |
| `src/CopilotLedger.Cli/Export/ExportCommands.cs` | `CanWrite` refuses read-only files; the rules below; `RestoreZip` returns what's left |
| `docs/cli.md` | `--force` replaces only files that aren't read-only |
| `tests/CopilotLedger.Tests/TestSupport/FakeCliPlatform.cs` | `MoveFile`, `DeleteFile`, `WriteFileAsync`, and `FileOperationHandler` |
| `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs` | New: EC14 (2 cases), EC15 (2 cases), EC16, EC17 |

### Rules

1. `CanWrite` refuses, before anything is read or written: a directory (`<shown> is a directory.`), an existing file without `--force` (`<shown> already exists. Use --force to replace it.`), and, even with `--force`, a read-only file (`Could not write <shown>: access is denied.`). Read-only is `FileAttributes.ReadOnly`, which on Unix means that this user can read the file but not write it.
2. Export moves its two files into place, and deletes them, through `ICliPlatform.MoveFile` and `DeleteFile`, and writes its report's temporary file through `WriteFileAsync`, which `SystemCliPlatform` implements with `File.Move`, `File.Delete`, and `File.WriteAllBytesAsync`. That write comes after `CanWrite` and before the decision to move a ZIP aside, so a test can act between the two. `ExportBundleWriter` still writes the bundle's temporary file, and temporary files are still deleted with `File.Delete`.
3. Without `--force`, no move replaces a file. A file that appeared at either path since `CanWrite` is refused with `<shown> already exists. Use --force to replace it.`
4. With `--force`, an existing ZIP is moved to `<zip>.<process ID>.bak` first. Then the new ZIP, and the report, are moved into place, the report replacing an existing one.
5. If a step fails, the export exits with 1 after `Could not write <shown>: <reason>`, or after the `already exists` line, and puts the ZIP back as it was: the backup is moved over the new ZIP, or, with no backup, a new ZIP is deleted. If that fails, a second line says what's left: `<zip> was written, but <report> wasn't, so they don't match.`, followed by ` The replaced <zip> is kept as <backup's file name>.` when there's a backup; or only `The replaced <zip> is kept as <backup's file name>.` when the new ZIP never moved.
6. After the report is in place, the backup is deleted. If it can't be, the export exits with 1, writes nothing to standard output, and says on standard error `<report> and <zip> were written, but the replaced <zip> is kept as <backup's file name>: it couldn't be deleted.`
7. Every line names the files as `--out` gives them, and no other folder.

### Code (copy exactly)

Edit 1 of 1 in `docs/cli.md`, replace:

```markdown
| `--max-timeline-spans` | `20000` | The most requests, and the most tool calls, that the timeline draws: the latest ones. 0 to 100,000. |
| `--generated-at` | Now | The generation time to record, in UTC, such as `2026-11-21T21:30:00Z`, to reproduce an earlier export. |
| `--force` | | Replace existing files. If the report can't be replaced, the ZIP is put back as it was. |

The export reads the ledger once, so the report and the bundle agree. It covers the requests that start from the first day of `--since` up to the generation time, and the tool calls of their prompt chains in the same window; breaker trips are those of the same prompt chains, and budgets those of the requests' branches, with their all-time spend as `budget list` shows it. Standard output names the files, the number of requests, the scope, and the period, and never a folder:
```

with:

```markdown
| `--max-timeline-spans` | `20000` | The most requests, and the most tool calls, that the timeline draws: the latest ones. 0 to 100,000. |
| `--generated-at` | Now | The generation time to record, in UTC, such as `2026-11-21T21:30:00Z`, to reproduce an earlier export. |
| `--force` | | Replace existing files that aren't read-only. If the report can't be replaced, the ZIP is put back as it was. |

The export reads the ledger once, so the report and the bundle agree. It covers the requests that start from the first day of `--since` up to the generation time, and the tool calls of their prompt chains in the same window; breaker trips are those of the same prompt chains, and budgets those of the requests' branches, with their all-time spend as `budget list` shows it. Standard output names the files, the number of requests, the scope, and the period, and never a folder:
```

Edit 1 of 6 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        try
        {
            // Both files are written from one snapshot, then moved into place: the ZIP first, then the report. A ZIP that
            // --force replaces waits beside it until the report is in place, so a failed export leaves both as they were.
            await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync(cancellationToken))
            {
                model = await ExportModelBuilder.BuildAsync(snapshot, inputs, cancellationToken);
                byte[] html = HtmlReportWriter.Write(model);
                await File.WriteAllBytesAsync(htmlTemp, html, cancellationToken);
                if (zipTemp is not null)
                {
```

with:

```csharp
        try
        {
            // Both files are written from one snapshot, then moved into place: the ZIP first, then the report. Without --force,
            // no move replaces a file, so one that appeared since CanWrite is refused. With it, an existing ZIP waits beside the
            // new one until the report is in place, so a failed export leaves both as they were.
            await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync(cancellationToken))
            {
                model = await ExportModelBuilder.BuildAsync(snapshot, inputs, cancellationToken);
                byte[] html = HtmlReportWriter.Write(model);
                await context.Platform.WriteFileAsync(htmlTemp, html, cancellationToken);
                if (zipTemp is not null)
                {
```

Edit 2 of 6 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
            {
                writing = zipShown;
                if (File.Exists(zipPath))
                {
                    string backup = BackupPath(zipPath!);
                    File.Move(zipPath!, backup);
                    zipBackup = backup;
                }

                File.Move(zipTemp, zipPath!);
                zipMoved = true;
            }

            writing = output;
            File.Move(htmlTemp, htmlPath, overwrite: force);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine($"Could not write {writing}: {WriteFailure(ex)}");
            if (zipPath is not null && !RestoreZip(zipPath, zipBackup, zipMoved))
            {
                context.Error.WriteLine($"The replaced {zipShown} is kept as {Path.GetFileName(zipBackup)}.");
                zipBackup = null;
            }

            return 1;
```

with:

```csharp
            {
                writing = zipShown;
                if (force && File.Exists(zipPath))
                {
                    string backup = BackupPath(zipPath!);
                    context.Platform.MoveFile(zipPath!, backup, overwrite: false);
                    zipBackup = backup;
                }

                context.Platform.MoveFile(zipTemp, zipPath!, overwrite: false);
                zipMoved = true;
            }

            writing = output;
            context.Platform.MoveFile(htmlTemp, htmlPath, overwrite: force);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine(!force && ex is IOException && File.Exists(writing == output ? htmlPath : zipPath)
                ? $"{writing} already exists. Use --force to replace it."
                : $"Could not write {writing}: {WriteFailure(ex)}");
            if (zipPath is not null && RestoreZip(context.Platform, zipPath, zipShown, output, zipBackup, zipMoved) is string left)
            {
                context.Error.WriteLine(left);
            }

            return 1;
```

Edit 3 of 6 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
                DeleteIfPresent(zipTemp);
            }

            if (zipBackup is not null)
            {
                DeleteIfPresent(zipBackup);
            }
        }
```

with:

```csharp
                DeleteIfPresent(zipTemp);
            }
        }

        // The report is in place, so the replaced ZIP goes; an export that has to leave it behind doesn't succeed.
        if (zipBackup is not null)
        {
            try
            {
                context.Platform.DeleteFile(zipBackup);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                context.Error.WriteLine(
                    $"{output} and {zipShown} were written, but the replaced {zipShown} is kept as {Path.GetFileName(zipBackup)}: it couldn't be deleted.");
                return 1;
            }
        }
```

Edit 4 of 6 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
    }

    // False, after writing why, when path is a directory, or a file that --force doesn't allow replacing.
    private static bool CanWrite(CliContext context, string path, string shown, bool force)
    {
```

with:

```csharp
    }

    // False, after writing why, when path is a directory, a file that --force doesn't allow replacing, or a read-only file,
    // which even --force doesn't replace.
    private static bool CanWrite(CliContext context, string path, string shown, bool force)
    {
```

Edit 5 of 6 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
        }

        return true;
    }

    // The temporary file beside target that a file is written to before it's moved into place.
```

with:

```csharp
        }

        if (File.Exists(path) && IsReadOnly(path))
        {
            context.Error.WriteLine($"Could not write {shown}: access is denied.");
            return false;
        }

        return true;
    }

    // Whether path is read-only (on Unix, a file that this user can read but not write); false when it can't be examined.
    private static bool IsReadOnly(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // The temporary file beside target that a file is written to before it's moved into place.
```

Edit 6 of 6 in `src/CopilotLedger.Cli/Export/ExportCommands.cs`, replace:

```csharp
    };

    // Puts back the ZIP that --force replaced, or deletes the new one when there was none, so a failed export leaves no ZIP
    // that doesn't match the report beside it. False when the replaced ZIP couldn't be put back.
    private static bool RestoreZip(string zipPath, string? backup, bool moved)
    {
        if (backup is null)
        {
            if (moved)
            {
                DeleteIfPresent(zipPath);
            }

            return true;
        }

        try
        {
            File.Move(backup, zipPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
```

with:

```csharp
    };

    // Puts the ZIP back as it was before the export: the one --force replaced, or none. Returns a line for standard error when
    // it can't, so what's left is said, or null.
    private static string? RestoreZip(
        ICliPlatform platform,
        string zipPath,
        string zipShown,
        string output,
        string? backup,
        bool moved)
    {
        try
        {
            if (backup is not null)
            {
                platform.MoveFile(backup, zipPath, overwrite: true);
            }
            else if (moved)
            {
                platform.DeleteFile(zipPath);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            string kept = backup is null ? string.Empty : $"The replaced {zipShown} is kept as {Path.GetFileName(backup)}.";
            return moved ? $"{zipShown} was written, but {output} wasn't, so they don't match. {kept}".TrimEnd() : kept;
        }
    }
```

Edit 1 of 1 in `src/CopilotLedger.Cli/ICliPlatform.cs`, replace:

```csharp
    void SetStartupCommand(string name, string? command);                  // Windows: sets that value (REG_SZ), or deletes it when command is null; other platforms throw PlatformNotSupportedException
    bool FileExists(string path);
    string? FindOnPath(string fileName);                                   // full path of an executable on PATH, or null
    TimeZoneInfo? FindTimeZone(string id);                                 // a time zone by IANA ID (on Windows also by Windows ID), or null
```

with:

```csharp
    void SetStartupCommand(string name, string? command);                  // Windows: sets that value (REG_SZ), or deletes it when command is null; other platforms throw PlatformNotSupportedException
    bool FileExists(string path);
    void MoveFile(string source, string destination, bool overwrite);      // File.Move; export moves its files into place through it
    void DeleteFile(string path);                                          // File.Delete; nothing when the file doesn't exist
    Task WriteFileAsync(string path, byte[] bytes, CancellationToken cancellationToken); // File.WriteAllBytesAsync; export writes its report's temporary file through it
    string? FindOnPath(string fileName);                                   // full path of an executable on PATH, or null
    TimeZoneInfo? FindTimeZone(string id);                                 // a time zone by IANA ID (on Windows also by Windows ID), or null
```

Edit 1 of 1 in `src/CopilotLedger.Cli/SystemCliPlatform.cs`, replace:

```csharp
    }

    public bool FileExists(string path) => File.Exists(path);

    public string? FindOnPath(string fileName)
```

with:

```csharp
    }

    public bool FileExists(string path) => File.Exists(path);

    public void MoveFile(string source, string destination, bool overwrite) => File.Move(source, destination, overwrite);

    public void DeleteFile(string path) => File.Delete(path);

    public Task WriteFileAsync(string path, byte[] bytes, CancellationToken cancellationToken) =>
        File.WriteAllBytesAsync(path, bytes, cancellationToken);

    public string? FindOnPath(string fileName)
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
using System.Text.Json;
using System.Xml.Linq;
```

with:

```csharp
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
```

with:

```csharp
    }

    // Even with --force, a read-only report or bundle stops the export before it writes anything, on every OS.
    [Theory]
    [InlineData("copilot-ledger-report.html")]
    [InlineData("copilot-ledger-report.zip")]
    public async Task EC14_AReadOnlyFileIsNotReplacedEvenWithForce(string readOnly)
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        string report = Path.Combine(temp.Path, Report);
        string bundle = Path.ChangeExtension(report, ".zip");
        File.WriteAllText(report, "old report");
        File.WriteAllText(bundle, "old bundle");

        File.SetAttributes(Path.Combine(temp.Path, readOnly), FileAttributes.ReadOnly);
        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--bundle", "--force");
        File.SetAttributes(Path.Combine(temp.Path, readOnly), FileAttributes.Normal);

        Assert.Equal((1, $"Could not write {readOnly}: access is denied.\n"), (run.ExitCode, run.Error));
        Assert.Equal(("old report", "old bundle"), (File.ReadAllText(report), File.ReadAllText(bundle)));
        Assert.Equal([report, bundle], Directory.GetFiles(temp.Path).Order(StringComparer.Ordinal));
    }

    // Without --force, a report or bundle that another program writes during the export is refused and kept: the fake platform
    // writes it after the checks, as the export writes its report's temporary file, so before the export decides whether to
    // move a bundle aside.
    [Theory]
    [InlineData("copilot-ledger-report.zip")]
    [InlineData("copilot-ledger-report.html")]
    public async Task EC15_AFileThatAppearsDuringTheExportIsKeptWithoutForce(string name)
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        string competing = Path.Combine(temp.Path, name);
        string reportTemp = $"{Path.Combine(temp.Path, Report)}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.tmp";
        FakeCliPlatform platform = new()
        {
            FileOperationHandler = (operation, path) =>
            {
                if (operation == "write" && path == reportTemp)
                {
                    File.WriteAllText(competing, "another program's file");
                }
            },
        };
        CliHarness cli = new(temp.Path, platform, new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

        CliRun run = await cli.RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal((1, $"{name} already exists. Use --force to replace it.\n"), (run.ExitCode, run.Error));
        Assert.Equal("another program's file"u8.ToArray(), File.ReadAllBytes(competing));
        Assert.Equal([competing], Directory.GetFiles(temp.Path));
    }

    // When the report can't be moved into place and the new bundle can't be deleted, standard error says that they don't
    // match: the fake platform fails both.
    [Fact]
    public async Task EC16_ABundleThatCantBeTakenBackIsReported()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        string bundle = Path.Combine(temp.Path, "copilot-ledger-report.zip");
        FakeCliPlatform platform = new()
        {
            FileOperationHandler = (operation, path) =>
            {
                if (operation == "move" && path == Path.Combine(temp.Path, Report))
                {
                    throw new UnauthorizedAccessException();
                }

                if (operation == "delete" && path == bundle)
                {
                    throw new IOException();
                }
            },
        };
        CliHarness cli = new(temp.Path, platform, new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

        CliRun run = await cli.RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal(
            (1, "Could not write copilot-ledger-report.html: access is denied.\ncopilot-ledger-report.zip was written, but copilot-ledger-report.html wasn't, so they don't match.\n"),
            (run.ExitCode, run.Error));
        Assert.Equal([bundle], Directory.GetFiles(temp.Path));
    }

    // An export that can't delete the bundle it replaced doesn't succeed: the fake platform fails the deletion.
    [Fact]
    public async Task EC17_AReplacedBundleThatCantBeDeletedIsReported()
    {
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);
        string bundle = Path.Combine(temp.Path, "copilot-ledger-report.zip");
        string backup = $"{bundle}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.bak";
        File.WriteAllText(bundle, "old bundle");
        FakeCliPlatform platform = new()
        {
            FileOperationHandler = (operation, path) =>
            {
                if (operation == "delete" && path == backup)
                {
                    throw new IOException();
                }
            },
        };
        CliHarness cli = new(temp.Path, platform, new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

        CliRun run = await cli.RunAsync("export", "--all-repos", "--bundle", "--force");

        Assert.Equal(
            (1, string.Empty, $"copilot-ledger-report.html and copilot-ledger-report.zip were written, but the replaced copilot-ledger-report.zip is kept as {Path.GetFileName(backup)}: it couldn't be deleted.\n"),
            (run.ExitCode, run.Out, run.Error));
        Assert.Equal("old bundle", File.ReadAllText(backup));
        Assert.Equal(
            [Path.Combine(temp.Path, Report), bundle, backup],
            Directory.GetFiles(temp.Path).Order(StringComparer.Ordinal));
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/TestSupport/FakeCliPlatform.cs`, replace:

```csharp
    public Dictionary<string, TimeZoneInfo> TimeZones { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ExistingFiles { get; } = new(StringComparer.Ordinal);
    public List<(string FileName, IReadOnlyList<string> Arguments)> ProcessRuns { get; } = [];
    public Func<string, IReadOnlyList<string>, CancellationToken, Task<ProcessRunResult>>? RunHandler { get; set; }
```

with:

```csharp
    public Dictionary<string, TimeZoneInfo> TimeZones { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ExistingFiles { get; } = new(StringComparer.Ordinal);

    // Runs before each WriteFileAsync, with "write" and the file, before each MoveFile, with "move" and the destination, and
    // before each DeleteFile, with "delete" and the file; a test may create a file or throw. The three act on real files, which
    // tests keep in their TempDirectory.
    public Action<string, string>? FileOperationHandler { get; set; }
    public List<(string FileName, IReadOnlyList<string> Arguments)> ProcessRuns { get; } = [];
    public Func<string, IReadOnlyList<string>, CancellationToken, Task<ProcessRunResult>>? RunHandler { get; set; }
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/TestSupport/FakeCliPlatform.cs`, replace:

```csharp
    public bool FileExists(string path) => ExistingFiles.Contains(path);

    public string? FindOnPath(string fileName) =>
        PathExecutables.GetValueOrDefault(fileName);
```

with:

```csharp
    public bool FileExists(string path) => ExistingFiles.Contains(path);

    public void MoveFile(string source, string destination, bool overwrite)
    {
        FileOperationHandler?.Invoke("move", destination);
        File.Move(source, destination, overwrite);
    }

    public void DeleteFile(string path)
    {
        FileOperationHandler?.Invoke("delete", path);
        File.Delete(path);
    }

    public Task WriteFileAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        FileOperationHandler?.Invoke("write", path);
        return File.WriteAllBytesAsync(path, bytes, cancellationToken);
    }

    public string? FindOnPath(string fileName) =>
        PathExecutables.GetValueOrDefault(fileName);
```

### Tests

- **EC14** (2 cases) With `--force`, an old report and ZIP, and a read-only report, or a read-only ZIP: exit code 1, `Could not write <that file>: access is denied.`, both files unchanged, and no other file in the folder, on every OS.
- **EC15** (2 cases) Without `--force`, the fake platform writes the ZIP, or the report, as the export writes its report's temporary file: exit code 1, `<that file> already exists. Use --force to replace it.`, the other program's file kept byte for byte, and no other file in the folder. The ZIP case fails when the decision to move a ZIP aside ignores `--force`; the report case fails when the report's move overwrites.
- **EC16** The fake platform fails the report's move and the new ZIP's deletion: exit code 1, `Could not write copilot-ledger-report.html: access is denied.`, and `copilot-ledger-report.zip was written, but copilot-ledger-report.html wasn't, so they don't match.`
- **EC17** With `--force` over an old ZIP, the fake platform fails the backup's deletion: exit code 1, nothing on standard output, `copilot-ledger-report.html and copilot-ledger-report.zip were written, but the replaced copilot-ledger-report.zip is kept as copilot-ledger-report.zip.<process ID>.bak: it couldn't be deleted.`, and the backup holds the old ZIP.

### Acceptance criteria

- An export never replaces a file without `--force`, or a read-only file with it; never exits 0 with a backup left behind; and says, without a path, what's left when it can't undo a step.
- `dotnet test CopilotLedger.slnx` passes **1409** tests (1403 + 6).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 20 specification: Tool History Starts at the Earliest Link Kept, and Without Links There Is None

Review finding N4. Task 15 started tool history at the earliest tool call the ledger kept, and, when it kept none, at 7 days before `generatedAt`. So after the daemon had deleted every link, an export with a past `--generated-at` could still call tool history complete.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs` | `ReadEarliestLinkStartAsync` replaces `ReadEarliestToolCallStartAsync` |
| `src/CopilotLedger.Storage/LedgerExportSnapshot.cs` | `GetEarliestLinkStartAsync` replaces `GetEarliestToolCallStartAsync` |
| `src/CopilotLedger.Cli/Export/ExportReportModel.cs` | `ExportToolHistoryStart`, and `ExportCoverage.ToolHistoryStart` instead of `ToolHistoryFromEarliestToolCall` |
| `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs` | The rule below |
| `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs` | The note's three wordings |
| `docs/cli.md` | Where tool history starts |
| `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs` | XR12: the earliest link of any kind |
| `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs` | EC13's note and name; new: EC18 |

### Rules

1. `GetEarliestLinkStartAsync` returns the smallest `start_time_unix_ms` in `span_links`, of any operation, filter, or owner, or null. The daemon deletes links of every kind by the same rule, so an agent invocation's link bounds what it deleted as well as a tool call's.
2. Tool history (`ExportToolHistoryStart`) starts at the earliest link when that's later than 7 days before `generatedAt` (`EarliestLink`), at 7 days before `generatedAt` otherwise (`Retention`), and at `generatedAt` when the ledger keeps no link (`NoLinks`). In that last case every selected request starts before it, so `toolHistory.complete` is false. `retainedFromUtc` is the start.
3. The notes are `Tool calls are kept for 7 days, so the <n requests> before <local time> may be missing their tool calls.` (`Retention`), `The ledger's tool history starts at <local time>, so the <n requests> before it may be missing their tool calls.` (`EarliestLink`), and `The ledger keeps no tool history, so the <n requests> may be missing their tool calls.` (`NoLinks`), with `its` for 1 request.

### Code (copy exactly)

Edit 1 of 1 in `docs/cli.md`, replace:

```markdown
### What the report shows

- **Summary:** requests, GitHub AI credits, Foundry BYOK, API equivalent, local savings, cache hit ratio, prompt chains, breaker trips, and budgets. Each card's tooltip holds the exact amount.
- **Coverage:** whether the requests are complete, whether the tool calls are, and notes on what's missing: imported requests that live spans recorded too (each counted once), imported requests that live spans of their sessions replaced without matching them (see [Overlap with live spans](#overlap-with-live-spans)), imports that deferred requests or didn't finish (counted across the whole ledger, because imports aren't kept per repository), requests older than the tool history, unpriced requests, and requests without cache counters. Imports that were replaced without a match, deferred, or not finished may hide requests, so they make the requests incomplete. Tool history is the 7 days before the generation time, but it starts no earlier than the earliest tool call the ledger keeps, because the daemon deletes older tool calls by its own clock; so an export with a past `--generated-at` doesn't count on tool calls that were deleted since. These limits are also in the bundle's `coverage` object.
- **Timeline:** a row per Copilot session, a bar per request colored by model, a tick per tool call (red when it failed, with no duration, which the ledger doesn't keep), and nights shaded from 18:00 to 06:00 in the export's time zone. Hold Ctrl or Command and scroll, or pinch, to zoom, as far as the axis can still label two times; drag sideways to pan; or drag across the overview. When there are more than `--max-timeline-spans` requests or tool calls, it draws the latest ones; the totals and tables still include them all.
- **Cost by model and agent:** billed spend (GitHub AI credits, then Foundry BYOK), the API-equivalent view, and local savings, each on its own scale. The comparison views are never added to billed spend.
```

with:

```markdown
### What the report shows

- **Summary:** requests, GitHub AI credits, Foundry BYOK, API equivalent, local savings, cache hit ratio, prompt chains, breaker trips, and budgets. Each card's tooltip holds the exact amount.
- **Coverage:** whether the requests are complete, whether the tool calls are, and notes on what's missing: imported requests that live spans recorded too (each counted once), imported requests that live spans of their sessions replaced without matching them (see [Overlap with live spans](#overlap-with-live-spans)), imports that deferred requests or didn't finish (counted across the whole ledger, because imports aren't kept per repository), requests older than the tool history, unpriced requests, and requests without cache counters. Imports that were replaced without a match, deferred, or not finished may hide requests, so they make the requests incomplete. Tool history is the 7 days before the generation time, but it starts no earlier than the earliest tool call or agent link the ledger keeps, because the daemon deletes older ones by its own clock; so an export with a past `--generated-at` doesn't count on tool calls that were deleted since. A ledger that keeps none has no tool history: every request may be missing its tool calls, and `retainedFromUtc` is the generation time. These limits are also in the bundle's `coverage` object.
- **Timeline:** a row per Copilot session, a bar per request colored by model, a tick per tool call (red when it failed, with no duration, which the ledger doesn't keep), and nights shaded from 18:00 to 06:00 in the export's time zone. Hold Ctrl or Command and scroll, or pinch, to zoom, as far as the axis can still label two times; drag sideways to pan; or drag across the overview. When there are more than `--max-timeline-spans` requests or tool calls, it draws the latest ones; the totals and tables still include them all.
- **Cost by model and agent:** billed spend (GitHub AI credits, then Foundry BYOK), the API-equivalent view, and local savings, each on its own scale. The comparison views are never added to billed spend.
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
        LedgerRollupRow totals = ReportCommands.Sum(byChannelModel);

        // Tool history is the retention before generatedAt, but starts no earlier than the earliest tool call the ledger
        // keeps: the daemon deletes tool calls by its own clock, so a past --generated-at can't count on the retention.
        DateTimeOffset retained = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        DateTimeOffset? earliestToolCall = await snapshot.GetEarliestToolCallStartAsync(cancellationToken);
        DateTimeOffset toolHistoryFrom = earliestToolCall is DateTimeOffset earliest && earliest > retained ? earliest : retained;
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
        IReadOnlyList<LedgerRollupRow> byModel = await snapshot.GetRollupAsync(LedgerGrouping.Model, filter, cancellationToken);
```

with:

```csharp
        LedgerRollupRow totals = ReportCommands.Sum(byChannelModel);

        // Tool history is the retention before generatedAt, but starts no earlier than the earliest span link the ledger
        // keeps: the daemon deletes links, of tool calls and agent invocations alike, by its own clock, so a past
        // --generated-at can't count on the retention. A ledger that keeps no link keeps no tool history.
        DateTimeOffset retained = inputs.GeneratedAt - LedgerImporter.ToolLinkRetention;
        DateTimeOffset? earliestLink = await snapshot.GetEarliestLinkStartAsync(cancellationToken);
        (DateTimeOffset toolHistoryFrom, ExportToolHistoryStart toolHistoryStart) = earliestLink switch
        {
            null => (inputs.GeneratedAt, ExportToolHistoryStart.NoLinks),
            DateTimeOffset earliest when earliest > retained => (earliest, ExportToolHistoryStart.EarliestLink),
            _ => (retained, ExportToolHistoryStart.Retention),
        };
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
        IReadOnlyList<LedgerRollupRow> byModel = await snapshot.GetRollupAsync(LedgerGrouping.Model, filter, cancellationToken);
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
                ToolHistoryRetentionDays = LedgerImporter.ToolLinkRetention.Days,
                ToolHistoryFromUtc = toolHistoryFrom,
                ToolHistoryFromEarliestToolCall = toolHistoryFrom > retained,
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
```

with:

```csharp
                ToolHistoryRetentionDays = LedgerImporter.ToolLinkRetention.Days,
                ToolHistoryFromUtc = toolHistoryFrom,
                ToolHistoryStart = toolHistoryStart,
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
```

Edit 1 of 2 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
}

internal sealed record ExportCoverage
{
```

with:

```csharp
}

// Where tool history starts: at the retention before GeneratedAt, at the earliest span link the ledger keeps when that's
// later, or at GeneratedAt when the ledger keeps no link.
internal enum ExportToolHistoryStart
{
    Retention,
    EarliestLink,
    NoLinks,
}

internal sealed record ExportCoverage
{
```

Edit 2 of 2 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
    public required long CacheLegacyUnknownSpans { get; init; }
    public required int ToolHistoryRetentionDays { get; init; }        // LedgerImporter.ToolLinkRetention.Days (7)
    public required DateTimeOffset ToolHistoryFromUtc { get; init; }    // GeneratedAt less the retention, or the earliest tool call kept
    public required bool ToolHistoryFromEarliestToolCall { get; init; } // ToolHistoryFromUtc is the earliest tool call kept, which is later
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
```

with:

```csharp
    public required long CacheLegacyUnknownSpans { get; init; }
    public required int ToolHistoryRetentionDays { get; init; }        // LedgerImporter.ToolLinkRetention.Days (7)
    public required DateTimeOffset ToolHistoryFromUtc { get; init; }    // where ToolHistoryStart says tool history starts
    public required ExportToolHistoryStart ToolHistoryStart { get; init; }
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
            string their = before == 1 ? "its" : "their";
            string from = LedgerFormat.LocalTime(coverage.ToolHistoryFromUtc, zone);
            yield return coverage.ToolHistoryFromEarliestToolCall
                ? $"The earliest tool call the ledger keeps started at {from}, so the {requests} before it may be missing {their} tool calls."
                : $"Tool calls are kept for {coverage.ToolHistoryRetentionDays.ToString(CultureInfo.InvariantCulture)} days, so the {requests} before {from} may be missing {their} tool calls.";
        }

        long withoutDetail = coverage.ImportedToolCallsWithoutDetail;
```

with:

```csharp
            string their = before == 1 ? "its" : "their";
            string from = LedgerFormat.LocalTime(coverage.ToolHistoryFromUtc, zone);
            yield return coverage.ToolHistoryStart switch
            {
                ExportToolHistoryStart.NoLinks => $"The ledger keeps no tool history, so the {requests} may be missing {their} tool calls.",
                ExportToolHistoryStart.EarliestLink => $"The ledger's tool history starts at {from}, so the {requests} before it may be missing {their} tool calls.",
                _ => $"Tool calls are kept for {coverage.ToolHistoryRetentionDays.ToString(CultureInfo.InvariantCulture)} days, so the {requests} before {from} may be missing {their} tool calls.",
            };
        }

        long withoutDetail = coverage.ImportedToolCallsWithoutDetail;
```

Edit 1 of 1 in `src/CopilotLedger.Storage/LedgerExportSnapshot.cs`, replace:

```csharp
        SqliteLedgerStore.CountExportToolCallsAsync(_transaction, filter, cancellationToken);

    // The start of the earliest tool call the ledger keeps, of any filter, or null when it keeps none. The daemon deletes
    // older ones, so no tool call before it is left.
    public Task<DateTimeOffset?> GetEarliestToolCallStartAsync(CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadEarliestToolCallStartAsync(_transaction, cancellationToken);

    // What the filter's spans cover. Spans that start before toolHistoryFrom may have lost their tool calls to retention.
```

with:

```csharp
        SqliteLedgerStore.CountExportToolCallsAsync(_transaction, filter, cancellationToken);

    // The start of the earliest span link the ledger keeps, a tool call's or an agent invocation's, of any filter, or null when
    // it keeps none. The daemon deletes older links of every kind, so no tool call before it is left.
    public Task<DateTimeOffset?> GetEarliestLinkStartAsync(CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadEarliestLinkStartAsync(_transaction, cancellationToken);

    // What the filter's spans cover. Spans that start before toolHistoryFrom may have lost their tool calls to retention.
```

Edit 1 of 2 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
    }

    // The start of the earliest execute_tool link in the whole ledger, whatever its owner, or null when there's none.
    internal static async Task<DateTimeOffset?> ReadEarliestToolCallStartAsync(
        SqliteTransaction snapshot,
        CancellationToken cancellationToken)
```

with:

```csharp
    }

    // The start of the earliest span link in the whole ledger, of any operation or owner, or null when there's none.
    internal static async Task<DateTimeOffset?> ReadEarliestLinkStartAsync(
        SqliteTransaction snapshot,
        CancellationToken cancellationToken)
```

Edit 2 of 2 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = "SELECT MIN(start_time_unix_ms) FROM span_links WHERE operation_name = 'execute_tool';";
        return await command.ExecuteScalarAsync(cancellationToken) is long start
            ? DateTimeOffset.FromUnixTimeMilliseconds(start)
```

with:

```csharp
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = "SELECT MIN(start_time_unix_ms) FROM span_links;";
        return await command.ExecuteScalarAsync(cancellationToken) is long start
            ? DateTimeOffset.FromUnixTimeMilliseconds(start)
```

Edit 1 of 3 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
    // within 7 days of the generation time.
    [Fact]
    public async Task EC13_ToolHistoryStartsNoEarlierThanTheEarliestToolCallKept()
    {
        using TempDirectory temp = new();
```

with:

```csharp
    // within 7 days of the generation time.
    [Fact]
    public async Task EC13_ToolHistoryStartsNoEarlierThanTheEarliestLinkKept()
    {
        using TempDirectory temp = new();
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
        Assert.Equal("Tool calls: partial; see the notes.", ExportFiles.ById(page, "coverage-tool-calls").Value);
        Assert.Contains(
            "The earliest tool call the ledger keeps started at 2026-11-21 08:00, so the 2 requests before it may be missing their tool calls.",
            Notes(page));
    }
```

with:

```csharp
        Assert.Equal("Tool calls: partial; see the notes.", ExportFiles.ById(page, "coverage-tool-calls").Value);
        Assert.Contains(
            "The ledger's tool history starts at 2026-11-21 08:00, so the 2 requests before it may be missing their tool calls.",
            Notes(page));
    }
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
```

with:

```csharp
    }

    // With every link deleted, the ledger keeps no tool history, so no request's tool calls count as kept, even at a past
    // --generated-at.
    [Fact]
    public async Task EC18_ALedgerWithoutLinksKeepsNoToolHistory()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await store.PruneSpanLinksAsync(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos", "--generated-at", "2026-11-21T21:30:00Z");

        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        JsonElement history = data.RootElement.GetProperty("coverage").GetProperty("toolHistory");
        Assert.Equal(
            ("2026-11-21T21:30:00.0000000+00:00", 8, false),
            (history.GetProperty("retainedFromUtc").GetString(), history.GetProperty("spansBeforeRetention").GetInt32(), history.GetProperty("complete").GetBoolean()));
        Assert.Contains("The ledger keeps no tool history, so the 8 requests may be missing their tool calls.", Notes(page));
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
```

Edit 1 of 2 in `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs`, replace:

```csharp
    }

    [Fact]
    public async Task XR12_TheEarliestToolCallIsTheWholeLedgers()
    {
        using TempDirectory temp = new();
```

with:

```csharp
    }

    [Fact]
    public async Task XR12_TheEarliestLinkIsTheWholeLedgers()
    {
        using TempDirectory temp = new();
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs`, replace:

```csharp
        {
            await using LedgerExportSnapshot snapshot = await ledger.OpenExportSnapshotAsync();
            earliest.Add(await snapshot.GetEarliestToolCallStartAsync());
        }

        // Once I1's, I2's, T1, and T2 are deleted, T5 (A's invoke_agent link, at 14:00:50) is the earliest link, but T3 the
        // earliest tool call.
        await store.PruneSpanLinksAsync(new DateTimeOffset(2026, 11, 21, 14, 0, 45, TimeSpan.Zero));
        await using (LedgerExportSnapshot pruned = await store.OpenExportSnapshotAsync())
        {
            earliest.Add(await pruned.GetEarliestToolCallStartAsync());
        }

        Assert.Equal([null, ImportEntries.Start, new DateTimeOffset(2026, 11, 21, 15, 0, 10, TimeSpan.Zero)], earliest);
    }

    // Pages seek past rows that start in the same millisecond, from the period's first, and skip or repeat none.
```

with:

```csharp
        {
            await using LedgerExportSnapshot snapshot = await ledger.OpenExportSnapshotAsync();
            earliest.Add(await snapshot.GetEarliestLinkStartAsync());
        }

        // Once I1's, I2's, T1, and T2 are deleted, the earliest link is T5, A's invoke_agent link at 14:00:50, though the
        // earliest tool call is T3's: the daemon deletes both kinds by the same rule.
        await store.PruneSpanLinksAsync(new DateTimeOffset(2026, 11, 21, 14, 0, 45, TimeSpan.Zero));
        await using (LedgerExportSnapshot pruned = await store.OpenExportSnapshotAsync())
        {
            earliest.Add(await pruned.GetEarliestLinkStartAsync());
        }

        Assert.Equal([null, ImportEntries.Start, new DateTimeOffset(2026, 11, 21, 14, 0, 50, TimeSpan.Zero)], earliest);
    }

    // Pages seek past rows that start in the same millisecond, from the period's first, and skip or repeat none.
```

### Tests

- **XR12** (changed) The earliest link is null in an empty ledger, and I1's and I2's 11-20 12:00 in `ExportSeeds`. Once the links before 14:00:45 on 11-21 are deleted, it's T5's 14:00:50, an invoke_agent link.
- **EC13** (changed) Renamed `EC13_ToolHistoryStartsNoEarlierThanTheEarliestLinkKept`. Its note is `The ledger's tool history starts at 2026-11-21 08:00, so the 2 requests before it may be missing their tool calls.`
- **EC18** With every link deleted, an export at `2026-11-21T21:30:00Z` has `retainedFromUtc` `2026-11-21T21:30:00.0000000+00:00`, 8 requests before it, and `complete` false, and the note `The ledger keeps no tool history, so the 8 requests may be missing their tool calls.`

### Acceptance criteria

- At any `--generated-at`, tool history is never called complete for a request whose tool calls the daemon may have deleted.
- `dotnet test CopilotLedger.slnx` passes **1410** tests (1409 + 1).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 21 specification: The Coverage Query Counts Each Request's Tool Links Once

Review finding D2, from the coordinator's review and the architect's own. Task 15's `(SELECT COUNT(*) FROM span_links WHERE span_links.owner_span_row_id = spans.id AND span_links.operation_name = 'execute_tool')` ran once for each selected imported request with tool calls. `owner_span_row_id` has no index, and `span_links` is `WITHOUT ROWID`, so each run scanned the whole table (`SCAN spans`, `CORRELATED SCALAR SUBQUERY 1`, `SCAN span_links`).

Measured on 2026-11-23 through `GetCoverageAsync`, with 10,000 imported requests and 30,007 links it took 33.2 s, and with 30,000 and 50,007, 178.8 s. Task 3's `NOT EXISTS` form, which Task 15 replaced, took 1.7 s and 44.7 s. The query runs on every export. The counts don't change.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs` | The coverage query joins each owner's link count |
| `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs` | New: XR14 |

### Rules

1. The coverage query adds `LEFT JOIN (SELECT owner_span_row_id, COUNT(*) AS links FROM span_links WHERE operation_name = 'execute_tool' AND owner_span_row_id IS NOT NULL GROUP BY owner_span_row_id) AS owned ON owned.owner_span_row_id = spans.id`. A request then lacks `MAX(imported_tool_call_count - COALESCE(owned.links, 0), 0)`.
2. SQLite counts once and finds each request's count through an automatic index: `MATERIALIZE owned`, then `SEARCH owned USING AUTOMATIC COVERING INDEX (owner_span_row_id=?)`. On the ledgers above, the whole coverage query takes 65 ms and 133 ms.
3. A selected request is effective, so every link it owns counts, and `EffectiveLinkSql` doesn't change the counts. No index is added, because that needs a migration.

### Code (copy exactly)

Edit 1 of 3 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
    // call that no live span covers, so it counts when the filter selects it by its own columns, as gh ledger import reports
    // it. An imported request lacks the detail of each tool call it made beyond the execute_tool links it owns: import keeps
    // the links of only the tool calls that started within the retention, and that completed. import_sessions has no
    // repository, so its two counts are the whole ledger's.
    internal static async Task<ExportCoverageRow> ReadExportCoverageAsync(
        SqliteTransaction snapshot,
```

with:

```csharp
    // call that no live span covers, so it counts when the filter selects it by its own columns, as gh ledger import reports
    // it. An imported request lacks the detail of each tool call it made beyond the execute_tool links it owns: import keeps
    // the links of only the tool calls that started within the retention, and that completed. Each owner's links are counted
    // once, in a grouped join, because owner_span_row_id has no index to look them up by. import_sessions has no repository,
    // so its two counts are the whole ledger's.
    internal static async Task<ExportCoverageRow> ReadExportCoverageAsync(
        SqliteTransaction snapshot,
```

Edit 2 of 3 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
            SELECT COALESCE(SUM(CASE WHEN start_time_unix_ms < @tool_history_from THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN imported_tool_call_count > 0
                                     THEN MAX(imported_tool_call_count
                                              - (SELECT COUNT(*) FROM span_links
                                                  WHERE span_links.owner_span_row_id = spans.id
                                                    AND span_links.operation_name = 'execute_tool'), 0)
                                     ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END), 0),
```

with:

```csharp
            SELECT COALESCE(SUM(CASE WHEN start_time_unix_ms < @tool_history_from THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN imported_tool_call_count > 0
                                     THEN MAX(imported_tool_call_count - COALESCE(owned.links, 0), 0)
                                     ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END), 0),
```

Edit 3 of 3 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
                                      AND (cache_read_state = 'legacy_unknown' OR cache_write_state = 'legacy_unknown')
                                     THEN 1 ELSE 0 END), 0)
            FROM spans WHERE {FilterSql};

            SELECT COALESCE(SUM(records_ambiguous), 0), COALESCE(SUM(CASE WHEN complete = 0 THEN 1 ELSE 0 END), 0)
```

with:

```csharp
                                      AND (cache_read_state = 'legacy_unknown' OR cache_write_state = 'legacy_unknown')
                                     THEN 1 ELSE 0 END), 0)
            FROM spans
            LEFT JOIN (SELECT owner_span_row_id, COUNT(*) AS links FROM span_links
                       WHERE operation_name = 'execute_tool' AND owner_span_row_id IS NOT NULL
                       GROUP BY owner_span_row_id) AS owned ON owned.owner_span_row_id = spans.id
            WHERE {FilterSql};

            SELECT COALESCE(SUM(records_ambiguous), 0), COALESCE(SUM(CASE WHEN complete = 0 THEN 1 ELSE 0 END), 0)
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Storage/ExportRowTests.cs`, replace:

```csharp
    }

    // Pages seek past rows that start in the same millisecond, from the period's first, and skip or repeat none.
    [Fact]
```

with:

```csharp
    }

    // An imported request lacks the tool calls it reported beyond its own execute_tool links: all 3 with none, 2 of 3 with one
    // (an invoke_agent link it owns doesn't count), and none of 1 with two.
    [Fact]
    public async Task XR14_EachImportedRequestLacksWhatItsOwnToolLinksDontCover()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        ImportedLedgerEntry none = ImportEntries.Copilot("k1", "conv-1", null);
        ImportedLedgerEntry some = ImportEntries.Copilot("k2", "conv-2", null);
        ImportedLedgerEntry more = ImportEntries.Copilot("k3", "conv-3", null);
        await store.RecordImportedAsync(
        [
            none with { ToolCallCount = 3 },
            some with { ToolCallCount = 3, ToolLinks = [ImportEntries.ToolLink(some, "call-1", 0)] },
            more with { ToolCallCount = 1, ToolLinks = [ImportEntries.ToolLink(more, "call-2", 0), ImportEntries.ToolLink(more, "call-3", 1)] },
        ],
            null);
        await LedgerSql.ExecuteAsync(
            store.DatabasePath,
            """
            INSERT INTO span_links (trace_id, span_id, operation_name, start_time_unix_ms, recorded_at_unix_ms, owner_span_row_id)
            SELECT trace_id, '7a00000000000001', 'invoke_agent', start_time_unix_ms, 0, id FROM spans WHERE trace_id = @p0;
            """,
            some.Span.TraceId);

        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        ExportCoverageRow coverage = await snapshot.GetCoverageAsync(LedgerFilter.All, ImportEntries.Start.AddDays(-7));

        Assert.Equal(5, coverage.ImportedToolCallsWithoutDetail);
    }

    // Pages seek past rows that start in the same millisecond, from the period's first, and skip or repeat none.
    [Fact]
```

### Tests

- **XR14** Imported requests that reported 3 tool calls and own no link, 3 with one execute_tool link and an invoke_agent link, and 1 with two execute_tool links lack 3, 2, and 0, so 5 in all. XR14 passes before this task too: XR4, XR11, and XR14 pin counts that the old and the new query give alike.

### Acceptance criteria

- The coverage query reads `span_links` once, and its counts don't change.
- `dotnet test CopilotLedger.slnx` passes **1411** tests (1410 + 1).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 22 specification: A Range That Ends in a Repeated Hour Keeps Its Ticks

Review finding N5. Task 17's tick loop walked local times only as far as the local time of the range's end. So when a range ended inside the hour that fall-back repeats (New York, 2026-11-01 05:01–06:01:02 UTC), the 10-minute marks of the hour's first pass were never reached. One tick was left, and `minWidth` came to about 1966, more than the axis, so the script's clamp had a floor above its ceiling.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Export/TimelineLayout.cs` | `Ticks` walks whole local days around the range; `MinWidth` is at most `Width` |
| `src/CopilotLedger.Cli/Export/Assets/report.js` | `minWidth` is at most `width` |
| `tests/CopilotLedger.Tests/Cli/ExportTimelineTests.cs` | New: TL14 |

### Rules

1. `Ticks` walks local boundaries from the midnight before t0's local date to the midnight after t1's, and keeps each boundary's instants that fall in [t0, t1], as before.
2. `MinWidth` is at most 1000, and the script uses `Math.min(timeline.minWidth, width)`.

### Code (copy exactly)

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/Assets/report.js`, replace:

```
    }

    var width = timeline.width;
    var minWidth = timeline.minWidth;
    var height = Number(plot.getAttribute('height'));
    var ticks = Array.isArray(timeline.ticks) ? timeline.ticks : [];
```

with:

```
    }

    var width = timeline.width;
    var minWidth = Math.min(timeline.minWidth, width);
    var height = Number(plot.getAttribute('height'));
    var ticks = Array.isArray(timeline.ticks) ? timeline.ticks : [];
```

Edit 1 of 3 in `src/CopilotLedger.Cli/Export/TimelineLayout.cs`, replace:

```csharp
    {
        TimeSpan step = Step(t1 - t0);
        DateTime end = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(t1), zone).DateTime;
        List<(long Instant, int Level, string Label)> boundaries = [];
        for (DateTime local = ExportTime.LocalDate(DateTimeOffset.FromUnixTimeMilliseconds(t0), zone).ToDateTime(TimeOnly.MinValue);
             local <= end;
             local += step)
        {
```

with:

```csharp
    {
        TimeSpan step = Step(t1 - t0);
        List<(long Instant, int Level, string Label)> boundaries = [];

        // Whole local days around the range, so a time that a transition repeats is reached at both of its instants, whatever
        // local time the range ends at; instants outside [t0, t1] are dropped.
        DateTime last = ExportTime.LocalDate(DateTimeOffset.FromUnixTimeMilliseconds(t1), zone).AddDays(1).ToDateTime(TimeOnly.MinValue);
        for (DateTime local = ExportTime.LocalDate(DateTimeOffset.FromUnixTimeMilliseconds(t0), zone).AddDays(-1).ToDateTime(TimeOnly.MinValue);
             local <= last;
             local += step)
        {
```

Edit 2 of 3 in `src/CopilotLedger.Cli/Export/TimelineLayout.cs`, replace:

```csharp
        range <= DayMs ? 3 : range <= 7 * DayMs ? 2 : range <= 62 * DayMs ? 1 : 0;

    // The narrowest view the script zooms to: twice the widest gap between two neighboring ticks, or between an end of the
    // axis and its nearest tick, so every view holds at least two ticks to label; at least half a unit.
    private static decimal MinWidth(IReadOnlyList<ExportAxisTick> ticks)
    {
```

with:

```csharp
        range <= DayMs ? 3 : range <= 7 * DayMs ? 2 : range <= 62 * DayMs ? 1 : 0;

    // The narrowest view the script zooms to: twice the widest gap between two neighboring ticks, or between an end of the
    // axis and its nearest tick, so every view holds at least two ticks to label; at least half a unit, at most the axis.
    private static decimal MinWidth(IReadOnlyList<ExportAxisTick> ticks)
    {
```

Edit 3 of 3 in `src/CopilotLedger.Cli/Export/TimelineLayout.cs`, replace:

```csharp
        }

        return Math.Max(2 * widest, 0.5m);
    }

    private static ExportTimelineRow Row(
```

with:

```csharp
        }

        return Math.Min(Math.Max(2 * widest, 0.5m), Width);
    }

    private static ExportTimelineRow Row(
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/ExportTimelineTests.cs`, replace:

```csharp
    }

    private static CliHarness Harness(string root)
    {
```

with:

```csharp
    }

    // A range that ends inside the hour that fall-back repeats has every tick: the 10-minute marks of the hour's first pass,
    // and 01:00 of its second; and zooming never needs more than the whole axis.
    [Fact]
    public async Task TL14_ARangeThatEndsInARepeatedHourHasAllItsTicks()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        await store.RecordAsync(
        [
            Entry("conv-dst", 1, new DateTimeOffset(2026, 11, 1, 5, 1, 0, TimeSpan.Zero)),
            Entry("conv-dst", 2, new DateTimeOffset(2026, 11, 1, 6, 1, 0, TimeSpan.Zero)),
        ]);

        await ExportAsync(
            Harness(temp.Path),
            temp.Path,
            "--all-repos", "--since", "all", "--time-zone", "Test/New_York", "--generated-at", "2026-12-01T00:00:00Z");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        using JsonDocument data = ExportFiles.LedgerData(html);
        JsonElement timeline = data.RootElement.GetProperty("timeline");
        Assert.Equal(
            ["01:10", "01:20", "01:30", "01:40", "01:50", "01:00"],
            timeline.GetProperty("ticks").EnumerateArray().Select(tick => tick.GetProperty("label").GetString()));
        Assert.Equal(333.148m, timeline.GetProperty("minWidth").GetDecimal());
        Assert.Contains("var minWidth = Math.min(timeline.minWidth, width);", html, StringComparison.Ordinal);
    }

    private static CliHarness Harness(string root)
    {
```

### Tests

- **TL14** In `Test/New_York`, the range 2026-11-01 05:01–06:01:02 UTC has the ticks `01:10`, `01:20`, `01:30`, `01:40`, and `01:50` (the hour's first pass), and `01:00` (its second), and `minWidth` 333.148. The script holds `var minWidth = Math.min(timeline.minWidth, width);`.

### Acceptance criteria

- A range has every tick that falls in it, whatever local time it ends at, and `minWidth` never exceeds the axis.
- `dotnet test CopilotLedger.slnx` passes **1412** tests (1411 + 1).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Task 23 specification: Imported Requests Without Tool Data Leave Tool History Incomplete

Review finding E1, which the coordinator added after Sprint 10's session found it. A Codex rollout records no tool calls, so a request imported from one owns no `span_links` row, and its `imported_tool_call_count` is NULL. `importedToolCallsWithoutDetail` sums only the deficits of requests whose count is above 0, so a Codex request added nothing to it, and an export of recent Codex imports said "Tool calls: complete for this window." (`toolHistory.complete: true`). That breaks #64's rule never to call incomplete data complete, and #63's requirement that export label missing tool coverage.

The figure can't count every imported row without a count. Copilot's importer records a compaction (`session.compaction_complete`) without a count and never gives it links: a compaction is the billed call that summarizes the conversation, and it makes no tool calls. Nothing stored tells a compaction from a usage record: both are `chat` spans, and `import_record_aliases` keeps only a hash of the record's kind. Copilot logs count each usage record's tool calls (`data.usage.numToolCalls`, on every usage record of Copilot 1.0.103–1.0.114 in Sprint 8's census), and store a reported 0 as 0. So a Copilot row without a count is a compaction, and Copilot rows keep their coverage. Counting every imported row without a count would have called every export with a Copilot compaction incomplete, and ExportSeeds' I1 too.

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/ExportCoverageRow.cs` | New: `ImportedRequestsWithoutToolData` |
| `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs` | The coverage query counts them, in the same scan as the other per-request counts |
| `src/CopilotLedger.Cli/Export/ExportReportModel.cs` | `ExportCoverage.ImportedRequestsWithoutToolData`; `ToolHistoryComplete`; the warning |
| `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs` | Copies the figure |
| `src/CopilotLedger.Cli/Export/ExportJson.cs` | `coverage.toolHistory.importedRequestsWithoutToolData` |
| `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs` | The coverage note |
| `docs/cli.md` | The coverage bullet and the allowlist row |
| `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs` | New: EC19, EC20 |

### Rules

1. `importedRequestsWithoutToolData` counts the selected effective requests whose `ingest_source` is neither `otlp` nor `import:copilot` and whose `imported_tool_call_count` is NULL: today, every Codex request (`import:codex`). A known count, 0 included, is tool data; what a request with a count lacks stays in `importedToolCallsWithoutDetail`.
2. Live spans never count (the schema keeps their count NULL), and Copilot rows never do.
3. `toolHistory.complete` is false when the figure is above 0, and `coverage.warnings` then holds `imported-requests-without-tool-data`, which sorts between `import-superseded-unmatched` and `imported-tool-calls-without-detail`.
4. `importedRequestsWithoutToolData` follows `importedToolCallsWithoutDetail` in `coverage.toolHistory`, which `ExportJson.WriteCoverage` writes to `manifest.json`, `telemetry/summary.json`, and `ledger-data` alike; the allowlist row in `docs/cli.md` lists it in that place.
5. The note follows the without-detail note: `1 imported request has no tool-call data in its session log, so its tool calls are unknown.`, or `<N> imported requests have no tool-call data in their session logs, so their tool calls are unknown.`, with N formatted by `LedgerFormat.Count`. With it, the page says `Tool calls: partial; see the notes.`
6. The query plan doesn't change: the new column is one more `CASE` in the scan that Task 21 joined to the grouped link counts.

### Code (copy exactly)

Edit 1 of 2 in `docs/cli.md`, replace:

```markdown
### What the report shows

- **Summary:** requests, GitHub AI credits, Foundry BYOK, API equivalent, local savings, cache hit ratio, prompt chains, breaker trips, and budgets. Each card's tooltip holds the exact amount.
- **Coverage:** whether the requests are complete, whether the tool calls are, and notes on what's missing: imported requests that live spans recorded too (each counted once), imported requests that live spans of their sessions replaced without matching them (see [Overlap with live spans](#overlap-with-live-spans)), imports that deferred requests or didn't finish (counted across the whole ledger, because imports aren't kept per repository), requests older than the tool history, unpriced requests, and requests without cache counters. Imports that were replaced without a match, deferred, or not finished may hide requests, so they make the requests incomplete. Tool history is the 7 days before the generation time, but it starts no earlier than the earliest tool call or agent link the ledger keeps, because the daemon deletes older ones by its own clock; so an export with a past `--generated-at` doesn't count on tool calls that were deleted since. A ledger that keeps none has no tool history: every request may be missing its tool calls, and `retainedFromUtc` is the generation time. These limits are also in the bundle's `coverage` object.
- **Timeline:** a row per Copilot session, a bar per request colored by model, a tick per tool call (red when it failed, with no duration, which the ledger doesn't keep), and nights shaded from 18:00 to 06:00 in the export's time zone. Hold Ctrl or Command and scroll, or pinch, to zoom, as far as the axis can still label two times; drag sideways to pan; or drag across the overview. When there are more than `--max-timeline-spans` requests or tool calls, it draws the latest ones; the totals and tables still include them all.
- **Cost by model and agent:** billed spend (GitHub AI credits, then Foundry BYOK), the API-equivalent view, and local savings, each on its own scale. The comparison views are never added to billed spend.
```

with:

```markdown
### What the report shows

- **Summary:** requests, GitHub AI credits, Foundry BYOK, API equivalent, local savings, cache hit ratio, prompt chains, breaker trips, and budgets. Each card's tooltip holds the exact amount.
- **Coverage:** whether the requests are complete, whether the tool calls are, and notes on what's missing: imported requests that live spans recorded too (each counted once), imported requests that live spans of their sessions replaced without matching them (see [Overlap with live spans](#overlap-with-live-spans)), imports that deferred requests or didn't finish (counted across the whole ledger, because imports aren't kept per repository), requests older than the tool history, imported requests whose session logs hold no tool-call data (Codex rollouts hold none; Copilot logs count each request's tool calls, and a compaction makes none), unpriced requests, and requests without cache counters. Imports that were replaced without a match, deferred, or not finished may hide requests, so they make the requests incomplete; imported requests without tool-call data make the tool calls incomplete. Tool history is the 7 days before the generation time, but it starts no earlier than the earliest tool call or agent link the ledger keeps, because the daemon deletes older ones by its own clock; so an export with a past `--generated-at` doesn't count on tool calls that were deleted since. A ledger that keeps none has no tool history: every request may be missing its tool calls, and `retainedFromUtc` is the generation time. These limits are also in the bundle's `coverage` object.
- **Timeline:** a row per Copilot session, a bar per request colored by model, a tick per tool call (red when it failed, with no duration, which the ledger doesn't keep), and nights shaded from 18:00 to 06:00 in the export's time zone. Hold Ctrl or Command and scroll, or pinch, to zoom, as far as the axis can still label two times; drag sideways to pan; or drag across the overview. When there are more than `--max-timeline-spans` requests or tool calls, it draws the latest ones; the totals and tables still include them all.
- **Cost by model and agent:** billed spend (GitHub AI credits, then Foundry BYOK), the API-equivalent view, and local savings, each on its own scale. The comparison views are never added to billed spend.
```

Edit 2 of 2 in `docs/cli.md`, replace:

```markdown
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage` | `requestsComplete`, `warnings`, `spans`, `spansBySource`, `supersededMatchedImportSpans`, `supersededUnmatchedImportSpans`, `ledgerAmbiguousImportRecords`, `ledgerIncompleteImportLogs`, `unpricedSpans`, `apiEquivalentUnpricedSpans`, `apiEquivalentEstimatedSpans`, `cacheReportedSpans`, `cacheAbsentSpans`, `cacheLegacyUnknownSpans`, `toolHistory`, `timeline` |
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage.spansBySource[]` | `source`, `spans` |
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage.toolHistory` | `complete`, `retentionDays`, `retainedFromUtc`, `spansBeforeRetention`, `importedToolCallsWithoutDetail` |
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage.timeline` | `truncated`, `maxSpans`, `spans`, `spansDrawn`, `toolCalls`, `toolCallsDrawn` |
| `telemetry/summary.json` | `.` | `schemaVersion`, `totals`, `byModel`, `byAgent`, `byBranch`, `byChannelModel`, `cacheByModel`, `cacheByAgent`, `breakerTrips`, `budgets`, `coverage` |
```

with:

```markdown
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage` | `requestsComplete`, `warnings`, `spans`, `spansBySource`, `supersededMatchedImportSpans`, `supersededUnmatchedImportSpans`, `ledgerAmbiguousImportRecords`, `ledgerIncompleteImportLogs`, `unpricedSpans`, `apiEquivalentUnpricedSpans`, `apiEquivalentEstimatedSpans`, `cacheReportedSpans`, `cacheAbsentSpans`, `cacheLegacyUnknownSpans`, `toolHistory`, `timeline` |
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage.spansBySource[]` | `source`, `spans` |
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage.toolHistory` | `complete`, `retentionDays`, `retainedFromUtc`, `spansBeforeRetention`, `importedToolCallsWithoutDetail`, `importedRequestsWithoutToolData` |
| `manifest.json`, `report.html`, `telemetry/summary.json` | `coverage.timeline` | `truncated`, `maxSpans`, `spans`, `spansDrawn`, `toolCalls`, `toolCallsDrawn` |
| `telemetry/summary.json` | `.` | `schemaVersion`, `totals`, `byModel`, `byAgent`, `byBranch`, `byChannelModel`, `cacheByModel`, `cacheByAgent`, `breakerTrips`, `budgets`, `coverage` |
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportJson.cs`, replace:

```csharp
        writer.WriteNumber("spansBeforeRetention", coverage.SpansBeforeToolHistory);
        writer.WriteNumber("importedToolCallsWithoutDetail", coverage.ImportedToolCallsWithoutDetail);
        writer.WriteEndObject();
        writer.WriteStartObject("timeline");
```

with:

```csharp
        writer.WriteNumber("spansBeforeRetention", coverage.SpansBeforeToolHistory);
        writer.WriteNumber("importedToolCallsWithoutDetail", coverage.ImportedToolCallsWithoutDetail);
        writer.WriteNumber("importedRequestsWithoutToolData", coverage.ImportedRequestsWithoutToolData);
        writer.WriteEndObject();
        writer.WriteStartObject("timeline");
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/ExportModelBuilder.cs`, replace:

```csharp
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
                ToolCalls = await snapshot.CountToolCallsAsync(filter, cancellationToken),
                MaxTimelineSpans = inputs.MaxTimelineSpans,
```

with:

```csharp
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
                ImportedRequestsWithoutToolData = coverage.ImportedRequestsWithoutToolData,
                ToolCalls = await snapshot.CountToolCallsAsync(filter, cancellationToken),
                MaxTimelineSpans = inputs.MaxTimelineSpans,
```

Edit 1 of 3 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
    public required long ToolCalls { get; init; }                       // CountToolCallsAsync
    public required int MaxTimelineSpans { get; init; }
```

with:

```csharp
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
    public required long ImportedRequestsWithoutToolData { get; init; } // imported requests whose logs record no tool calls, as Codex's
    public required long ToolCalls { get; init; }                       // CountToolCallsAsync
    public required int MaxTimelineSpans { get; init; }
```

Edit 2 of 3 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
    public bool RequestsComplete =>
        LedgerAmbiguousImportRecords == 0 && LedgerIncompleteImportLogs == 0 && SupersededUnmatchedImportSpans == 0;
    public bool ToolHistoryComplete => SpansBeforeToolHistory == 0 && ImportedToolCallsWithoutDetail == 0;

    // The limitations that apply, ordinal.
```

with:

```csharp
    public bool RequestsComplete =>
        LedgerAmbiguousImportRecords == 0 && LedgerIncompleteImportLogs == 0 && SupersededUnmatchedImportSpans == 0;
    public bool ToolHistoryComplete =>
        SpansBeforeToolHistory == 0 && ImportedToolCallsWithoutDetail == 0 && ImportedRequestsWithoutToolData == 0;

    // The limitations that apply, ordinal.
```

Edit 3 of 3 in `src/CopilotLedger.Cli/Export/ExportReportModel.cs`, replace:

```csharp
            (LedgerIncompleteImportLogs > 0, "import-incomplete"),
            (SupersededUnmatchedImportSpans > 0, "import-superseded-unmatched"),
            (ImportedToolCallsWithoutDetail > 0, "imported-tool-calls-without-detail"),
            (TimelineTruncated, "timeline-truncated"),
```

with:

```csharp
            (LedgerIncompleteImportLogs > 0, "import-incomplete"),
            (SupersededUnmatchedImportSpans > 0, "import-superseded-unmatched"),
            (ImportedRequestsWithoutToolData > 0, "imported-requests-without-tool-data"),
            (ImportedToolCallsWithoutDetail > 0, "imported-tool-calls-without-detail"),
            (TimelineTruncated, "timeline-truncated"),
```

Edit 1 of 1 in `src/CopilotLedger.Cli/Export/HtmlReportWriter.cs`, replace:

```csharp
                ? "1 tool call of an imported request is counted without detail."
                : $"{LedgerFormat.Count(withoutDetail)} tool calls of imported requests are counted without detail.";
        }

        long unpriced = coverage.UnpricedSpans;
```

with:

```csharp
                ? "1 tool call of an imported request is counted without detail."
                : $"{LedgerFormat.Count(withoutDetail)} tool calls of imported requests are counted without detail.";
        }

        long withoutToolData = coverage.ImportedRequestsWithoutToolData;
        if (withoutToolData > 0)
        {
            yield return withoutToolData == 1
                ? "1 imported request has no tool-call data in its session log, so its tool calls are unknown."
                : $"{LedgerFormat.Count(withoutToolData)} imported requests have no tool-call data in their session logs, so their tool calls are unknown.";
        }

        long unpriced = coverage.UnpricedSpans;
```

Edit 1 of 1 in `src/CopilotLedger.Storage/ExportCoverageRow.cs`, replace:

```csharp
    public required long SpansBeforeToolHistory { get; init; }       // selected spans that start before toolHistoryFrom
    public required long ImportedToolCallsWithoutDetail { get; init; } // tool calls of imported requests beyond the links they own
    public required long CacheReportedSpans { get; init; }
    public required long CacheAbsentSpans { get; init; }
```

with:

```csharp
    public required long SpansBeforeToolHistory { get; init; }       // selected spans that start before toolHistoryFrom
    public required long ImportedToolCallsWithoutDetail { get; init; } // tool calls of imported requests beyond the links they own
    public required long ImportedRequestsWithoutToolData { get; init; } // selected imported requests, other than Copilot's, without a tool-call count
    public required long CacheReportedSpans { get; init; }
    public required long CacheAbsentSpans { get; init; }
```

Edit 1 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
    // it. An imported request lacks the detail of each tool call it made beyond the execute_tool links it owns: import keeps
    // the links of only the tool calls that started within the retention, and that completed. Each owner's links are counted
    // once, in a grouped join, because owner_span_row_id has no index to look them up by. import_sessions has no repository,
    // so its two counts are the whole ledger's.
    internal static async Task<ExportCoverageRow> ReadExportCoverageAsync(
        SqliteTransaction snapshot,
```

with:

```csharp
    // it. An imported request lacks the detail of each tool call it made beyond the execute_tool links it owns: import keeps
    // the links of only the tool calls that started within the retention, and that completed. Each owner's links are counted
    // once, in a grouped join, because owner_span_row_id has no index to look them up by. An imported request whose log gives
    // no tool-call count has no tool data at all, as every Codex request (a rollout records no tool calls); a known 0 is data.
    // Copilot rows are left out: Copilot logs count each usage record's tool calls, so a Copilot row without a count is a
    // compaction, which makes none. import_sessions has no repository, so its two counts are the whole ledger's.
    internal static async Task<ExportCoverageRow> ReadExportCoverageAsync(
        SqliteTransaction snapshot,
```

Edit 2 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
                                     THEN MAX(imported_tool_call_count - COALESCE(owned.links, 0), 0)
                                     ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN NOT {ReportedSql}
```

with:

```csharp
                                     THEN MAX(imported_tool_call_count - COALESCE(owned.links, 0), 0)
                                     ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN ingest_source NOT IN ('otlp', 'import:copilot') AND imported_tool_call_count IS NULL
                                     THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN NOT {ReportedSql}
```

Edit 3 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        (long before, long withoutDetail, long reported, long absent, long legacy) =
            (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4));
        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
```

with:

```csharp
        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        (long before, long withoutDetail, long withoutToolData, long reported, long absent, long legacy) =
            (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5));
        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
```

Edit 4 of 4 in `src/CopilotLedger.Storage/SqliteLedgerStore.Export.cs`, replace:

```csharp
            SpansBeforeToolHistory = before,
            ImportedToolCallsWithoutDetail = withoutDetail,
            CacheReportedSpans = reported,
            CacheAbsentSpans = absent,
```

with:

```csharp
            SpansBeforeToolHistory = before,
            ImportedToolCallsWithoutDetail = withoutDetail,
            ImportedRequestsWithoutToolData = withoutToolData,
            CacheReportedSpans = reported,
            CacheAbsentSpans = absent,
```

Edit 1 of 1 in `tests/CopilotLedger.Tests/Cli/ExportCommandTests.cs`, replace:

```csharp
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
```

with:

```csharp
    }

    // A Codex rollout records no tool calls, so a recent request imported from one leaves tool history incomplete, and the page,
    // the manifest, and the summary say so.
    [Fact]
    public async Task EC19_ACodexImportLeavesToolHistoryIncomplete()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await store.RecordImportedAsync([ImportEntries.Copilot("k7", "conv-codex", null) with { Source = ImportSourceKind.Codex }], null);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos", "--bundle");

        IReadOnlyList<(string Name, byte[] Bytes)> entries = ExportFiles.ReadZip(Path.Combine(temp.Path, "copilot-ledger-report.zip"));
        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        using JsonDocument manifest = JsonDocument.Parse(entries.Single(entry => entry.Name == "manifest.json").Bytes);
        using JsonDocument summary = JsonDocument.Parse(entries.Single(entry => entry.Name == "telemetry/summary.json").Bytes);
        JsonDocument[] documents = [data, manifest, summary];
        foreach (JsonElement coverage in documents.Select(document => document.RootElement.GetProperty("coverage")))
        {
            JsonElement history = coverage.GetProperty("toolHistory");
            Assert.Equal(
                (false, 1, 0, 0),
                (history.GetProperty("complete").GetBoolean(),
                 history.GetProperty("importedRequestsWithoutToolData").GetInt32(),
                 history.GetProperty("spansBeforeRetention").GetInt32(),
                 history.GetProperty("importedToolCallsWithoutDetail").GetInt32()));
            Assert.Contains(
                "imported-requests-without-tool-data",
                coverage.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()));
        }

        Assert.Equal("Tool calls: partial; see the notes.", ExportFiles.ById(page, "coverage-tool-calls").Value);
        Assert.Contains("1 imported request has no tool-call data in its session log, so its tool calls are unknown.", Notes(page));
    }

    // A Copilot import that reported 0 tool calls has tool data, and a Copilot compaction, whose row has no count, makes no
    // tool calls, so tool history stays complete.
    [Fact]
    public async Task EC20_CopilotImportsKeepToolHistoryComplete()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        const string compaction = "33333333-3333-4333-8333-333333333333";
        await store.RecordImportedAsync(
        [
            ImportEntries.Copilot("k8", "conv-zero", null) with { ToolCallCount = 0 },
            ImportEntries.Copilot(compaction, "conv-zero", compaction, ImportedRecordKind.Compaction),
        ],
            null);

        XDocument page = await ExportAsync(Harness(temp.Path), temp.Path, "--all-repos");

        using JsonDocument data = ExportFiles.LedgerData(ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
        JsonElement history = data.RootElement.GetProperty("coverage").GetProperty("toolHistory");
        Assert.Equal((true, 0), (history.GetProperty("complete").GetBoolean(), history.GetProperty("importedRequestsWithoutToolData").GetInt32()));
        Assert.Equal("Tool calls: complete for this window.", ExportFiles.ById(page, "coverage-tool-calls").Value);
        Assert.Equal(
            2L,
            await LedgerSql.ScalarAsync(
                Path.Combine(temp.Path, "data", "ledger.db"),
                "SELECT COUNT(*) FROM spans WHERE conversation_id = 'conv-zero' AND accounting_state = 'effective';"));
    }

    // A harness at ExportSeeds.GeneratedAt in ManualTimeProvider.UtcMinus5, with the test time zones.
    private static CliHarness Harness(string root)
```

### Tests

- **EC19** ExportSeeds plus one Codex request imported at 2026-11-20 12:00 UTC (`ImportEntries.Copilot("k7", "conv-codex", null) with { Source = ImportSourceKind.Codex }`), exported with `--all-repos --bundle`. In `ledger-data`, `manifest.json`, and `telemetry/summary.json`, `coverage.toolHistory` has `complete` false, `importedRequestsWithoutToolData` 1, `spansBeforeRetention` 0, and `importedToolCallsWithoutDetail` 0, and `coverage.warnings` holds `imported-requests-without-tool-data`. The page says `Tool calls: partial; see the notes.` and has the note `1 imported request has no tool-call data in its session log, so its tool calls are unknown.`
- **EC20** ExportSeeds plus two Copilot requests of `conv-zero`: a usage record that reported 0 tool calls (`ToolCallCount = 0`), and a compaction, which has no count (`ImportedRecordKind.Compaction`, service request ID `33333333-3333-4333-8333-333333333333`). Both are effective; `complete` is true, `importedRequestsWithoutToolData` is 0, and the page says `Tool calls: complete for this window.`

Both fail without the fix, because `importedRequestsWithoutToolData` is missing. With the figure but the old `ToolHistoryComplete`, EC19 fails (`complete` true). With a figure that counts every imported row without a count, EC20 fails (2: the compaction and ExportSeeds' I1, a Copilot usage record that the test helper gives no count), and so does EC19 (2).

### Acceptance criteria

- An export whose requests include Codex imports says, in the page, the manifest, and the summary, that tool calls are partial, and why.
- Live spans and Copilot imports, compactions included, keep their coverage.
- `dotnet test CopilotLedger.slnx` passes **1414** tests (1412 + 2).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## Test specifications

Every test is hermetic: it uses `TempDirectory` for files and databases, `ManualTimeProvider` with fixed times, `FakeCliPlatform` with the custom zones of `TestZones`, and `CliHarness` for the CLI. Ledgers are synthetic, written by `ExportSeeds` or the test itself. No test starts a process, runs `git` or a browser, uses the network, Node, Python, or Docker, or reads `~/.copilot`, `~/.codex`, `~/.claude`, or `~/.copilot-ledger`; ET1 and ET2 read only the operating system's time zone data. Money is `decimal` or integer pico-USD, never `double`, and expected text is compared exactly.

| Task | New tests (cases) | Changed tests | Total after |
|---|---|---|---:|
| 0 | — (`main` at `7620e55`) | — | 1,269 |
| 1 | ET1–ET7, ET8 (12) | — | 1,288 |
| 2 | XS1 (5), XS2–XS4 | — | 1,296 |
| 3 | XR1–XR8, XR9 (4), XR10 | — | 1,309 |
| 4 | EC1 (12), EC2–EC11 | — | 1,331 |
| 5 | EH1–EH7 | — | 1,338 |
| 6 | TL1–TL10 | — | 1,348 |
| 7 | CH1–CH8 | — | 1,356 |
| 8 | EA1–EA3 | — | 1,359 |
| 9 | EB1–EB9 | — | 1,368 |
| 10 | EP1–EP6 | — | 1,374 |
| 11 | EL1–EL3 | — | 1,377 |
| 12 | GS2 (1 more case) | GS1 (the new sample ID) | 1,378 |
| 13 | EP7 (10) | — | 1,388 |
| 14 | EC12 | EC3 (the whole message) | 1,389 |
| 15 | XR11, XR12, EC13 | EC8 (a tool call for the old request) | 1,392 |
| 16 | XR13 | — | 1,393 |
| 17 | TL11, TL12 (2), TL13, EA4 | TL7 (175 ticks, at levels 0–3) | 1,398 |
| 18 | EP7 (5 more cases) | — | 1,403 |
| 19 | EC14 (2), EC15 (2), EC16, EC17 | — | 1,409 |
| 20 | EC18 | XR12 (the earliest link of any kind), EC13 (its note and name) | 1,410 |
| 21 | XR14 | — | 1,411 |
| 22 | TL14 | — | 1,412 |
| 23 | EC19, EC20 | — | 1,414 |

Exact values used across tasks:

| Value | What | Where |
|---|---|---|
| `2026-11-21T21:30:00Z` | `ExportSeeds.GeneratedAt`, 16:30 at `ManualTimeProvider.UtcMinus5` | Tasks 3–12 |
| `0.6595`, `65.95` | The fixture's GitHub USD and AI credits (A1 0.448, A2 0.15, D1 0.06, I1 and J1 0.00075 each) | EC6, EB3 |
| `0.315` | B1, Foundry BYOK, priced with `RateFixtures.ByokTestRatesJson` | EC6, EB3 |
| `0.1725` | C1, the cloud spend a local model avoided | EC6, CH2, EB3 |
| `0.508` | The API equivalent of A1 and D1 (6 requests unpriced, D1 estimated) | EC6, CH2, EB3 |
| `91.3` | The `feature/ledger` budget's all-time spend: A1, A2, and B1 | XR7, EH4 |
| `896.535` | A1's bar on the 1000-unit timeline | TL1 |
| `2026-11-21T21:30:00.0000000+00:00` | `generatedAtUtc` as the manifest and `ledger-data` write it | TL5, EB5, EP4 |
| `(root folder)` | How a drive, share, or file-system root shows without `--include-local-paths` | EP7 |
| `2026-11-21T13:00:00.0000000+00:00` | EC13's tool call, the earliest one kept once the links before 11-21 are deleted | EC13 |
| `2.826` | `minWidth` over 29.5 days: twice the widest gap between hourly ticks | TL13 |
| `333.148` | `minWidth` over TL14's hour: twice a 10-minute gap | TL14 |

## Final step: the pull request (orchestrator, not a builder task)

After Task 23, the orchestrator:

1. Runs `dotnet build CopilotLedger.slnx` (0 warnings, 0 errors) and `dotnet test CopilotLedger.slnx` (1414 passed, 0 failed, 0 skipped), and checks with `git --no-pager diff --stat origin/main` that the branch changes only this plan, `.github/plans/archive/sprint-8.md`, and the files the tasks list.
2. Exports a report from a temporary `COPILOT_LEDGER_HOME` with a few recorded spans and opens it in a browser with and without JavaScript: zoom (Ctrl or Command with the wheel, and a pinch), pan, a plain scroll over the timeline, the overview's range selection, reset, the lens switch, sorting, keyboard focus, the dark scheme, and reduced motion. The browser's console shows no Content Security Policy violation. Never point it at a real home directory.
3. Pushes `swigerb-sprint-9-ledger-export-html-report` and opens one pull request titled exactly "Sprint 9: `gh ledger export`: self-contained offline HTML report & metadata-only bundle", whose body says `Closes #64`, lists the twenty-three task commits, and has `## Changes`, `## Deviations` (D1–D19 and P1–P15 from "Design decisions", each with a one-line rationale), `## Review fixes` (the findings of both "Review fixes" sections, each with its task), and `## Verification`.
4. Waits for CI on `ubuntu-latest` and `windows-latest`, including the smoke test of the published binary's export.
5. Asks `ledger-architect` to review the branch.

This sprint doesn't bump the version, tag, or release. When the sprint closes, `ledger-architect` archives this plan with its amendments.

## Deferred to later sprints

- **A hash-based Content Security Policy** (D19): `script-src 'sha256-…'` and `style-src 'sha256-…'` instead of `'unsafe-inline'`.
- **The audit findings section** (#65): its content, a bundle entry, and a manifest field; the model and the page already have the place.
- **Coverage notes in reports, receipts, and MCP**: Sprint 8's D26 still holds for them; only export and import report coverage.
- **IANA IDs on Windows 10 1703–1809**, through `icuin.dll`, which has the same function under a versioned name.
- **Every prompt chain in `prompt-chains.json`**, configurable night hours, and PDF or PNG output (out of scope in #64).
- **Byte identity of the ZIP container across operating systems** (A3); the entries' bytes are already identical.
- **Failed tool calls and the sixth model color share #d55e00.** Ticks and bars differ in shape and the timeline's table lists failures, but a distinct color would read faster.
- **Telling same-named local repositories apart** (R6): two roots with the same folder name show the same name; a short, stable suffix after the name could tell them apart without showing either path.
- **A record of the daemon's deletions** (R5): with the cutoff of its last deletion of tool calls in the ledger, tool history wouldn't start at the earliest tool call kept, which a quiet spell without tool calls moves later. It needs a schema change.
- **Repository slugs recorded before Sprint 8's strict `owner/repo` rule** are shown as stored, by reports and by export; one from a remote URL with a query or fragment may hold it. A migration could clear the slugs that aren't strict.
- **Axis lines at every zoom**: the axis draws lines only down to the level it labels unzoomed, so the finer labels the script shows when zoomed in have no line under them.
- **A crash between moving the replaced ZIP aside and moving the new one in** leaves only `<zip>.<process ID>.bak`; copying the old ZIP aside, rather than moving it, would close that window at the cost of a copy.
- **Requests at the very ends of the axis**: a request without a duration at either end shows only half of its 1-pixel stroke; a little padding around the range would show all of it.
- **A Copilot usage record without `numToolCalls`** would be taken for a compaction, and its tool calls for complete. No observed Copilot version writes one (Sprint 8's census); telling the two apart needs the record's kind stored, which is a schema change.
