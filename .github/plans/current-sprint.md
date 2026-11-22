# Sprint 5: Audit Fixes, Action Repository, Docs & User Guide

| | |
|---|---|
| Architect | `ledger-architect` (Claude Opus 5.5) |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET 10 (`net10.0`), SDK 10.0.x, zero Docker |
| Plan written | 2026-11-21, against merged Sprint 4 (`main` at `3a0a514`, released as v0.4.0) |
| Status | Current sprint. Sprint 4 is complete and archived at `.github/plans/archive/sprint-4.md`. |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification section, runs the verification commands, changes the line to `- [x]`, and stops. Tasks 1–5 are sequential, and each builds on the ones before it.

Task R is different. It runs in the other repository, swigerb/copilot-ledger-action, in parallel with Tasks 1–4, and it has no checkbox here, so a builder in this repository never picks it up. The orchestrator assigns it to a builder that works in that repository. Task 5 starts only after Task R's pull request merges, because Task 5 points this repository at the merged action.

Every task ships with its tests, listed under "Test specifications". A gh-ledger task is done only when `dotnet test` runs the whole suite, including its new cases, with 0 failed and 0 skipped. Task R's verification runs the action's PowerShell tests.

Most new code and docs in this plan are marked **copy exactly**. The architect built every task in scratch copies of both repositories: the code compiled with `TreatWarningsAsErrors`, `dotnet test` passed 671 tests after Task 5, and the action's PowerShell tests passed all 18 cases. Copy each block character for character, including every comment line, `using` directive, blank line, and line of Markdown. Don't add argument checks (`ThrowIfNull` and the like), cancellation tokens, `ConfigureAwait`, logging, caching, or fast paths that the code doesn't show, and don't tidy code or reword docs. Write every new file with `\n` line breaks and one `\n` at the end. An edited file keeps its byte order mark and its ending as they are. Of the files this plan edits, only the test project file has a byte order mark, and it has no line break at its end.

Each test case lists every assertion it makes, most of them as code. Write all of them, in the order given. Don't drop one because it looks redundant, and don't turn an exact comparison into `Contains` or `StartsWith`. Never normalize the product's output in a test (no `ReplaceLineEndings`, `Replace("\r\n", …)`, or `Trim` on an actual value); `CliHarness` already turns `\r\n` into `\n` in `Out` and `Error`.

The final section, "Final step: end-to-end check and releases", isn't a builder task. It has no checkbox, and `ledger-builder` never runs it.

## Tasks

- [x] Task 1: Audit Fixes & Version 0.4.1 (CopilotLedger.Otlp, Core, Cli & tests). The daemon logs why it answers 503, at most once a minute (A1). Test reads and writes of fresh files wait out antivirus locks (A2). Dead members are removed (A5), the fake platform loses its default home directory (A10), and once a trip is recorded, the breaker finishes its flag file even if the request is cancelled (A11). The version becomes 0.4.1.
- [ ] Task 2: Workflow Hardening (CI & Release workflows). Pin every action to a full commit SHA (A7), pass matrix values to `run` scripts through the environment (A6), and make the Release workflow check that a changed `rates.json` comes with a later `ratesVersion` (A4). CI's smoke test also sends a real OTLP trace to the published binary and checks what it recorded.
- [ ] Task 3: User Guide with Tested Samples (docs & tests). Rewrite `docs/guide.md` as short scenarios, each with a goal, the exact commands, and sample output (A8). Add `GuideSamplesTests`, which runs every sample's command against a deterministic example ledger and checks that the output is exactly what the guide shows.
- [ ] Task 4: Documentation Sweep (README & docs). Rewrite `README.md`, `docs/cli.md`, and `docs/releasing.md`, and edit `docs/configuration.md`, so the docs are accurate and concise, say each thing once, and follow one naming rule for `gh ledger` and `gh-ledger`. Covers A3 (the extension's binary path for MCP), A4 (the `ratesVersion` release step), A9 (the data files table), and how to update pinned actions.
- [ ] Task 5: Use the Action Repository (action/, workflows & docs). After Task R merges, delete `action/` and `docs/action.md`, drop CI's action-test step, and point `receipt.yml` at `swigerb/copilot-ledger-action`, pinned to the merged commit.

### Parallel task in swigerb/copilot-ledger-action

- Task R: The Action Repository. Fill swigerb/copilot-ledger-action with a Marketplace-ready root `action.yml`, `receipt.ps1`, `tests/receipt.Tests.ps1`, a `README.md` that documents the action, and a CI workflow that runs the tests on `ubuntu-latest` and `windows-latest`. It runs in that repository, in parallel with Tasks 1–4. It has no checkbox, so `ledger-builder` in this repository never picks it up.

A12, the Sprint 1 archive's missing amendments section, isn't a builder task. The architect added it in this plan's pull request, along with the Sprint 4 archive.
## Starting point

- Sprint 4 is complete. Tasks 1–5 merged in PRs #41, #42, #43, #45, and #46, all on the first pass and byte-exact with the plan (`main` at `3a0a514`). `dotnet test CopilotLedger.slnx` passes 649 tests (0 failed, 0 skipped), the action's PowerShell tests pass 18 cases, and CI is green on `ubuntu-latest` and `windows-latest` (run 44922053265). Sprint 4's plan is archived at `.github/plans/archive/sprint-4.md`; its amendments section records none.
- gh-ledger v0.4.0 is released: tag `v0.4.0` at `3a0a514`, published 2026-11-21 by the Release workflow run for the tag push (44938942680), after a manual dry run (44938792097), with all eight assets. So `gh extension install swigerb/gh-ledger` works, `rates update` finds a `rates.json`, and `swigerb/gh-ledger/action@v0.4.0` keeps working, because tags don't move. The docs stop showing it.
- The architect's audit of `3a0a514` found A1–A12 and no blockers, and the orchestrator accepted all twelve. Tasks 1–4 fix A1–A11; this plan's pull request fixes A12.
- swigerb/copilot-ledger-action exists. It's public, and its default branch `main` is at `c18a67b` ("Initial commit"), with GitHub's `README.md` stub and an MIT `LICENSE` ("Copyright (c) 2026 Brian Swiger").
- Merged behavior this plan depends on (read from the code at `3a0a514`):
  - `OtlpReceiverApp.Build(options, ingestor, configureWebHost)` maps `/v1/traces`, `/v1/metrics`, and `/v1/logs` to `HandleAsync`. When `IngestAsync` throws anything but a cancellation of the request, a bare `catch` answers 503 with `Retry-After: 5` and `The ledger is unavailable; retry later.`, and nothing is logged (A1).
  - `DaemonHost.RunAsync` writes log lines as `{TimeProvider.GetUtcNow():O} {message}` through its private `WriteLog(writer, context, message)`. It builds the receiver after the ingestor, then runs `EnqueuePendingAsync` and `StartAsync`, then writes `daemon.json` and logs `gh-ledger <version> daemon listening on http://localhost:<port> (pid <pid>); ledger <path>`.
  - `CircuitBreakerMonitor.EvaluateAsync` calls `CircuitFlagFile.SyncAsync(_store, trip.RepositoryRoot)` without its `cancellationToken`, and nothing says whether that's deliberate (A11). `SyncAsync` deletes its temporary file only when the write or move throws `IOException` or `UnauthorizedAccessException`.
  - Nothing in `src/` or `tests/` calls `JsonRateCardCalculator.LoadDefault()`, `LedgerPaths.DefaultDatabasePath`, or `LedgerPaths.DefaultRatesOverlayPath` (A5). `LedgerPaths.DataDirectory` is used.
  - `FakeCliPlatform.HomeDirectory` defaults to `/home/test`, so a macOS or Linux `install` test that forgot to set it would write under a real `/home/test` (A10). Every current test that needs it sets it.
  - `FileRetry` has `ReadAllTextAsync` and `ReadAllBytesAsync`, which retry an `IOException` every 25 ms for up to 10 seconds. 25 test reads of files the product just wrote call `File.Read*` directly, and `GitFixture.Create` doesn't wait out a lock on the `.git` files it writes (A2).
  - The version, `0.4.0`, is written in the Cli project's `<Version>`, the `GhLedgerCli` comment and fallback, CI's smoke test, DM1, and `ReportCommandTests.StatusText`.
  - As the installed extension, `install` prints `gh ledger runs this GitHub CLI extension, so no alias is needed.`, `status` shows `(GitHub CLI extension)`, and `rates update` names `gh ledger` commands. Every other message says `gh-ledger <command>`.
  - `agents` and `top-prompts` report every repository unless `--repo` is given; only `branch` defaults to the current repository. The current `docs/guide.md` says "Run reports from the repository you want to inspect", which misleads.
  - `CliHarness` runs commands in-process with `FakeCliPlatform`, and has `RunAsync`, `RunInAsync`, `RunWithHttpAsync`, `StartInProcessDaemonAsync`, `CreateDaemonClient`, `InProcessDaemon`, and `DaemonOutput`. `DaemonCommands.StartAsync` and `StopAsync` measure their timeouts with `context.TimeProvider`, so under a `ManualTimeProvider` that nobody advances they wait until `daemon.json` appears or the daemon's lock is released, however long that takes.
  - The three workflows use `actions/*@vN` tags. `ci.yml` writes `${{ matrix.rid }}` into a `run` script (A6), and its smoke test starts the published binary's daemon but sends it no telemetry. `receipt.yml` checks out the repository and runs `./action`.
- Sprint 4's public contracts are fixed. Sprint 5 changes only these existing artifacts, each called out in its task:
  - Task 1: `OtlpReceiverApp.Build` (one optional parameter) and `HandleAsync`, `DaemonHost`, `CircuitBreakerMonitor`, `CircuitFlagFile.SyncAsync`, `JsonRateCardCalculator` and `LedgerPaths` (removals), the Cli project file and `GhLedgerCli` (version), `.github/workflows/ci.yml` (version), `FileRetry`, `GitFixture`, `FakeCliPlatform`, and the tests its section lists;
  - Task 2: `.github/workflows/ci.yml`, `release.yml`, and `receipt.yml`;
  - Task 3: `docs/guide.md`, `FileRetry`, and the test project file;
  - Task 4: `README.md`, `docs/cli.md`, `docs/configuration.md`, and `docs/releasing.md`;
  - Task 5: `.github/workflows/ci.yml` and `receipt.yml`, a comment in `ReceiptMarkdown`, and `docs/releasing.md`. It deletes `action/` and `docs/action.md`.
- Removing `LoadDefault()`, `DefaultDatabasePath`, and `DefaultRatesOverlayPath` removes public API that nothing uses. The orchestrator chose removal over `[Obsolete]`.
- The test project uses xUnit v2 (`xunit` 2.9.3), and tests use only public APIs, with no `InternalsVisibleTo`.
## Verified facts (retrieved 2026-11-21)

### GitHub Marketplace and action metadata

Sources: GitHub Docs, [Publishing actions in GitHub Marketplace](https://docs.github.com/en/actions/how-tos/create-and-publish-actions/publish-in-github-marketplace), [Metadata syntax reference](https://docs.github.com/en/actions/reference/workflows-and-actions/metadata-syntax#branding), and [Managing custom actions](https://docs.github.com/en/actions/how-tos/create-and-publish-actions/manage-custom-actions); a GitHub Marketplace search.

- Marketplace publishes an action without review when its repository is public and has a single action metadata file (`action.yml` or `action.yaml`) at the root. Metadata files in subfolders are allowed but aren't listed. The `name` must be unique: it can't match an action already in Marketplace, a GitHub user or organization (unless that owner publishes it), or a Marketplace category, and GitHub reserves the names of its features.
- To list a release, the repository owner accepts the GitHub Marketplace Developer Agreement (until then the checkbox is disabled), selects **Publish this Action to the GitHub Marketplace** on the release, and picks a primary category. Publishing requires two-factor authentication. An existing release has the same checkbox when it's edited.
- `branding.color` is one of `white`, `black`, `yellow`, `blue`, `green`, `orange`, `red`, `purple`, or `gray-dark`. `branding.icon` names a Feather v4.28.0 icon; `dollar-sign` is on the supported list.
- Searching Marketplace for "CopilotLedger" found no actions, and `https://github.com/marketplace/actions/copilotledger-pr-receipt` and `https://github.com/marketplace/actions/copilot-ledger-pr-receipt` returned 404.
- GitHub's release management guidance for actions is semantic version tags plus a major version tag, such as `v1`, that the maintainer moves to each compatible release. Users reference the major tag, or a full commit SHA for the most stability.

### Pinning actions

Sources: GitHub Docs, [Secure use reference](https://docs.github.com/en/actions/reference/security/secure-use#using-third-party-actions) and [Locking action dependencies in a repository's workflows](https://docs.github.com/en/actions/how-tos/secure-your-work/lock-workflow-dependencies); `gh release view` and `gh api repos/<owner>/<repo>/commits/<tag>` with gh 2.103.3.

- Pinning an action to a full-length commit SHA means a backdoor would need a SHA-1 collision. A tag "can be moved or deleted if a bad actor gains access to the repository storing the action".
- The same page now calls a workflow dependency lockfile "the strongest protection available", which "supersedes SHA pinning for repositories that adopt it". The lockfile is generated by the `github/gh-actions-lock` gh extension, which also rewrites `uses:` lines, and GitHub recommends letting Dependabot keep it current. This plan pins SHAs, as the orchestrator decided; lockfiles are deferred.
- The latest release of each action this repository uses, and the commit its tag points to. Each action's major tag (`v7`, `v6`, `v7`, `v8`) points to the same commit.

  | Action | Release | Commit |
  |---|---|---|
  | `actions/checkout` | v7.0.1 | `3d3c42e5aac5ba805825da76410c181273ba90b1` |
  | `actions/setup-dotnet` | v6.1.0 | `0f4a354dd5547a28c70b7853dbd95d5b67886e73` |
  | `actions/upload-artifact` | v7.0.2 | `cf430e030ddbb5b0abf93d22962f4752f3646cd9` |
  | `actions/download-artifact` | v8.0.2 | `9000827ccba6bdab643e8b6fd33ac0654aef8333` |

- `gh release view --repo <owner>/<repo> --json tagName --jq .tagName` prints the latest release's tag, and `gh api repos/<owner>/<repo>/commits/<tag> --jq .sha` prints the commit the tag points to, following an annotated tag to its commit. Run together, they reproduced all four commits above; `docs/releasing.md` documents them for updating pins.

### Runner images and GitHub CLI

Sources: actions/runner-images [`Ubuntu2404-Readme.md`](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md) (image 20261116.376.1), [`Ubuntu2604-Readme.md`](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2604-Readme.md) (image 20261116.202.1), and [issue #14748](https://github.com/actions/runner-images/issues/14748); `gh release download --help` (gh 2.103.3).

- `ubuntu-latest` moves from Ubuntu 24.04 to Ubuntu 26.04 during November 2026. Both images have jq (1.7.1 and 1.8.1), GitHub CLI 2.104.0, PowerShell 7.6.10, curl, and .NET SDK 10.0.x, so Task 2's workflows run on either.
- Without a tag argument, `gh release download` downloads assets from the latest release and requires `--pattern` or `--archive`. It fails when no asset matches.
- The latest gh-ledger release is v0.4.0. Its `rates.json` asset is the embedded `rates.json` at `3a0a514`, whose `ratesVersion` is `2026-11-20`.

### Upgrading the extension while gh-ledger runs

Sources: cli/cli v2.103.3 [`pkg/cmd/extension/manager.go`](https://github.com/cli/cli/blob/v2.103.3/pkg/cmd/extension/manager.go) and [`http.go`](https://github.com/cli/cli/blob/v2.103.3/pkg/cmd/extension/http.go); the MCP specification, [Transports](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports#stdio); `copilot mcp --help` (Copilot CLI 1.0.112); a probe on Windows 11 ARM64.

- `gh extension upgrade` of a binary extension calls `installBin`, which downloads the new asset straight to `<data>/extensions/gh-ledger/gh-ledger[.exe]` with `downloadAsset`, opening that file with `O_CREATE|O_WRONLY|O_TRUNC` (`http.go`, line 95). `gh extension remove` calls `os.RemoveAll` on the extension's directory.
- In MCP's stdio transport, "the client launches the MCP server as a subprocess" and ends it by closing its standard input. So each Copilot session that loads the `ledger` server keeps a `gh-ledger mcp` process running until the session ends.
- **Probe.** While a `gh-ledger mcp` process ran (started with redirected standard input, in a temporary `COPILOT_LEDGER_HOME`), opening its executable for writing failed with "The process cannot access the file … because it is being used by another process." After its standard input was closed, the server exited with code 0, and the same open succeeded. Sprint 4 found the same for the daemon. So on Windows, `gh extension upgrade` and `gh extension remove` fail while the daemon or any Copilot session's MCP server runs the extension's binary.
- `copilot mcp` has the subcommands `list`, `get`, `add`, `remove`, `enable`, and `disable`. `copilot mcp remove <name>` removes a server from the user configuration, `~/.copilot/mcp-config.json`. `copilot mcp add <name> -- <command> [args...]` adds a local (stdio) server, as the docs show.

### GitHub Markdown

Source: GitHub's REST API, `POST /markdown` with `"mode": "gfm"`.

- Task 3's guide shows the sample receipt quoted with `> ` on every line. GitHub rendered it as a `<blockquote>` with the receipt's heading and list, then a collapsed `<details>` holding the table and the JSON code block, the same structure as the pull request comment. The receipt's first line, the `<!-- copilot-ledger-receipt -->` marker, renders as nothing.

### gh-ledger probes

Probes ran on Windows 11 ARM64 with .NET SDK 10.0.404, PowerShell 7.6, and the MSYS `sh` (bash 5.3), `curl`, `grep`, and `tr` that ship with the Copilot app's Git.

- **The CI ingestion smoke (Task 2).** A `win-arm64` single-file publish of the scratch code, with a temporary `COPILOT_LEDGER_HOME`, ran Task 2's whole bash smoke step under MSYS `sh` on port 54318 (with a stand-in for `sleep`, which that `sh` lacks). It exited 0. The POST returned `{}`, and `branch --all-repos --since all --json` listed one row, `(none)` / `(none)`, with `"spans": 1,` and `"aiCredits": 50,`. 50 AI credits is `(100,000 × 4 + 5,000 × 20) / 1,000,000 = 0.50` USD at `claude-opus-5.5`'s $4 input and $20 output per 1M tokens, below the breaker's 150-credit velocity rule: `breaker status` printed `Circuit breaker: closed`, `daemon.log` had no `ALERT` line, and no `alert-toast.xml` was written. The first version of the trace, 600 credits on one prompt, tripped the breaker and showed a toast on the reviewer's machine. `1795219200000000000` ns is 2026-11-21T00:00:00Z.
- **The Release workflow's rates check (Task 2).** The `rates` job's script ran locally under MSYS `sh`, downloading the real v0.4.0 `rates.json` with `gh`. An unchanged file printed `rates.json is unchanged since the latest release (2026-11-20).` and exited 0. A changed file with `ratesVersion` `2026-11-21` printed `rates.json changed since the latest release; ratesVersion moves from 2026-11-20 to 2026-11-21.` and exited 0. A changed file that kept `2026-11-20` printed `::error::rates.json changed since the latest release, but its ratesVersion 2026-11-20 isn't later than 2026-11-20. Set ratesVersion to the date of the change.` and exited 1. Against a repository with no release (swigerb/copilot-ledger-action), `gh` printed `release not found`, and the job printed `Skipping the check: no rates.json was downloaded from the latest release.` and exited 0. `jq` isn't installed on the architect's machine, so a PowerShell stand-in replaced it (see "Assumptions").
- **A1.** After `DROP TABLE spans;` on a running daemon's ledger, every `POST /v1/traces` fails inside `RecordAsync` with `SqliteException: SQLite Error 1: 'no such table: spans'.` and gets 503 (DM7 runs exactly this). At `3a0a514` the receiver's bare `catch` writes nothing, so `daemon.log` gives no sign that spans aren't being recorded.
- **The guide's stand-in daemon (Task 3).** An earlier `GuideWorld` whose stand-in daemon deleted `daemon.json` and `daemon.stop` with `File.Delete` before releasing its lock hung 2 of 11 test runs. The blame collector's hang dump named `GS2_CommandSamplesShowTheirOutput(id: "daemon-stop")`: under the manual clock, `daemon stop` waits until the lock is released, and a delete that throws (an antivirus scan of the just-written `daemon.stop`, the issue #30 pattern) left the lock held forever. With Task 3's `GuideWorld`, which deletes through `FileRetry.Delete`, releases the lock in `finally`, and gives each start its own lock and stop token, 30 of 30 runs passed.
- **Line endings.** All 168 files in the index at `3a0a514` are stored with LF line endings, and the repository has no `.gitattributes`.

### Assumptions (not verified)

- **The rates check on a runner.** The `rates` job's `jq -S .` hasn't run: the local check used a PowerShell stand-in (`ConvertFrom-Json`, then `ConvertTo-Json -Compress`). jq 1.7 and later keep number literals as written, so two files that differ only in how a number is written count as changed, which only asks for a later `ratesVersion`. The orchestrator's manual Release run in Task 2's verification runs the job for real.
- **The ingestion smoke on the runners.** It ran under bash on Windows ARM64 only; `ubuntu-latest` and `windows-latest` first run it in Task 2's CI.
- **Upgrading on macOS and Linux while gh-ledger runs** wasn't tested (carried from Sprint 4). The docs give every platform the same steps: stop the daemon and close the Copilot sessions and apps that use the MCP server first. That's needed on Windows and harmless elsewhere.
- **The new tests on Linux.** DM7 and `GuideSamplesTests` ran on Windows. They use only the fake platform, in-memory hosting, and temporary directories, like the Sprint 4 tests that pass on `ubuntu-latest`, and `GuideWorld` builds every shown path from Windows-style strings that don't depend on the OS. They first run on Linux in Task 1's and Task 3's CI.
- **The Marketplace name** is checked again when the release is published. "CopilotLedger PR Receipt" was free on 2026-11-21.
- **`@v1` before it exists.** The docs show `uses: swigerb/copilot-ledger-action@v1` from Task 3 on, but `v1` exists only after the orchestrator's release (see "Final step"). Until then, the action repository's README and gh-ledger's docs describe a version that can't be used yet.
## Design decisions

### Logging ingestion failures (Task 1, A1)

- **The receiver reports and the daemon logs.** `OtlpReceiverApp.Build` gains an optional fourth parameter, `Action<Exception>? ingestFailed`. `HandleAsync` calls it with each exception that turns a request into a 503, just before it writes the response; a cancelled request still returns silently, as before. The Otlp project stays free of logging, and existing callers compile unchanged.
- **At most once a minute.** The daemon passes `IngestFailureLog.Report`, which writes `Warning: could not record spans: <exception type>: <message>` through the daemon's `WriteLog` for the first failure, then at most once every minute, measured with `context.TimeProvider`. A ledger that stays unavailable (a full disk, or a locked or damaged database) gets one line a minute while Copilot keeps retrying, instead of one line per request. A line that can't be written (`IOException` or `ObjectDisposedException`) is dropped, so logging can never turn a 503 into a crash.
- **A clock that steps back.** The interval uses the wall clock, the same clock as the log's timestamps. When the clock is earlier than the last line's time, for example after a time sync, the next failure is written at once instead of being held back until the old time plus a minute. Rejected: `TimeProvider.GetTimestamp`, which is monotonic, because the tests' `ManualTimeProvider` doesn't drive it and the log lines show wall-clock time anyway.
- **Nothing from the request is logged**, only the exception's type and message. The ingestor never holds prompt or response content.
- **Rejected:** `ILogger` (the daemon has no logging pipeline; its log is `WriteLog`), and logging inside the Otlp project, which has no log writer.

### Test hygiene (Task 1, A2, A5, A10, A11)

- **Fresh files.** Every test read of a file the product just wrote goes through `FileRetry`: 25 lines in five test files. `GitFixture.Create` reads back the three `.git` files it wrote through a new synchronous `FileRetry.ReadAllText`, because `GitFixture` is synchronous and its callers are many. This is the issue #30 pattern: Windows antivirus briefly locks new files.
- **Dead members are removed** (A5): `JsonRateCardCalculator.LoadDefault()`, `LedgerPaths.DefaultDatabasePath`, and `LedgerPaths.DefaultRatesOverlayPath`. They'd bypass `COPILOT_LEDGER_HOME` and `rates.downloaded.json` if anyone used them.
- **No default home** (A10). `FakeCliPlatform.HomeDirectory` throws `InvalidOperationException` until a test sets it, so a test that forgets can't write a profile or sign-in item outside its `TempDirectory`. All 649 existing tests still pass, so none relied on the default.
- **The flag file always finishes** (A11, the orchestrator's decision). `CircuitBreakerMonitor` calls `CircuitFlagFile.SyncAsync` with `CancellationToken.None` on purpose, and a comment says why. By then the trip is committed and its alert published. If a client disconnect cancelled the sync, the flag file would stay unwritten, with nothing logged and no retry. `SyncAsync` now deletes its temporary file whatever the write or move throws, not only `IOException` and `UnauthorizedAccessException`, so a cancelled `breaker reset` or `breaker config --flag-file` leaves no `CIRCUIT_OPEN.<guid>.tmp` behind.

### Workflows (Task 2, A4, A6, A7)

- **SHA pins, no Dependabot.** Every `uses:` of another repository's action names a full commit SHA with its version in a comment, for example `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1`. Nothing updates them automatically; Dependabot would open pull requests on its own, which the user didn't ask for. `docs/releasing.md` explains how to update a pin. GitHub's newer workflow lockfiles are deferred.
- **Matrix values reach scripts only through `env`** (A6). CI's publish step reads `$RID`, as the Release workflow already does.
- **`ratesVersion` check** (A4). A new `rates` job compares `src/CopilotLedger.Core/Pricing/rates.json` with the latest release's `rates.json` asset, both canonicalized with `jq -S .`. Unchanged passes. Changed passes only when the new `ratesVersion` sorts after the old one; for `yyyy-MM-dd` dates, string order is date order. If nothing can be downloaded (no release yet, or a network error), the job prints a message and passes: missing a check only delays `rates update` users until the next release, while failing would block every release on a download hiccup. The job runs on manual runs too, so the dry run in `docs/releasing.md` checks it, and the `release` job needs it.
- **A scripted end-to-end check in CI.** The smoke test now posts a real OTLP/HTTP JSON trace to the published binary's daemon over a real socket and checks `branch --all-repos --since all --json`: one span, 50 AI credits. 50 credits stays below the breaker's 150-credit rule, so the smoke trips nothing and shows no notification, on the runner or for anyone who runs it locally. It complements Task 3's `GuideSamplesTests`, which run in-process. Generating the guide's samples from the published binary instead was rejected: it would need a real `install` on the runner (user environment, sign-in items), and real clocks and process IDs, so the output couldn't be exact.

### The user guide (Task 3, A8)

- **Scenarios.** `docs/guide.md` keeps its path, so links stay valid. It has eleven short scenarios: set up and check; see spend; the most expensive prompts; agents; a budget and its alert; a runaway loop; a pull request receipt; Foundry BYOK and local models; asking Copilot through MCP; updating rates; upgrading and uninstalling. Each has a **Goal** line, the exact commands, and their output.
- **One example.** Every sample comes from the same world: gh-ledger as the GitHub CLI extension on Windows, the most common setup and the one that needs no Git for Windows, in `contoso/webapp` on `feature/search`, at 16:30 on 2026-11-21 (UTC-5), with a month of seeded spend, one breaker trip, a budget, and a Foundry price overlay. The data directory shows as `C:\Users\you\.copilot-ledger`.
- **Samples can't drift.** `GuideSamplesTests` parses every `<!-- sample: <id> -->` … `<!-- /sample -->` block, runs its `gh ledger` command in `GuideWorld`, and compares the whole output with the guide. GS1 fails if the guide gains a sample that no test checks, or loses one. The overlay sample is the input that the example prices with. The receipt sample is quoted, so GitHub renders it the way the comment looks, and it's checked against `export-pr --stdout`. The MCP sample is `get_branch_ledger`'s text, with its one-line JSON indented. The budget alert sample is the real daemon's `ALERT` line.
- **A stand-in daemon.** `install` and `daemon start` call `FakeCliPlatform.StartDetachedHandler`, which `GuideWorld` sets to a stand-in. It holds `daemon.lock`, writes `daemon.json` with process 4242 and the clock's time, and on `daemon stop` deletes its files and releases the lock, so samples show a fixed process ID and start time. Each start owns its lock and stop token, deletes through `FileRetry.Delete`, and releases the lock in `finally`, so an antivirus lock can't hang `daemon stop`. The budget alert uses the real daemon, in-process, because the `ALERT` line comes from it.
- **The version is in the samples** (both `status` samples and the receipt), so `docs/releasing.md` lists the guide among the places a version bump changes, and `GuideSamplesTests` fails until it's updated.
- **Not shown:** output that gh-ledger doesn't print (`gh extension install`, `upgrade`, and `remove`), and `rates init`, whose example world already has an overlay.
- **gh-ledger's messages still say `gh-ledger <command>`** under the extension (deferred). The guide says once, in its introduction, to type those commands as `gh ledger <command>`.
- **One starting state.** After the setup section, every sample starts from the same `Month` state, and the introduction says so. That's why the overlay's rates version, BYOK spend, and the open breaker trip appear before the sections that explain them.

### The documentation (Task 4, A3, A4, A9)

- **One job per document, each fact in one place.** `README.md`: what CopilotLedger is, the quick start, and links. `docs/guide.md`: the scenarios. `docs/cli.md`: the reference for every command and option, plus troubleshooting. `docs/configuration.md`: pricing, the overlay, and the data files. `docs/releasing.md`: maintainers. The action's README in its own repository documents the action. Other documents link instead of repeating.
- **One naming rule.** Commands are written `gh ledger <command>`. `gh-ledger` names the program: the binary, `--help`, and its messages. `docs/cli.md` says once that a binary built from source without the `gh ledger` alias runs by its path, as `<folder>/gh-ledger <command>` (it isn't on `PATH`), and that a `gh-ledger <command>` in a message is typed `gh ledger <command>` under the extension.
- **Fixes folded in:** the MCP registration with the extension's binary path for each OS (A3); the `ratesVersion` release step, with every sample and test that shows the old date, and how to update pinned actions (A4, A7); `daemon.stop` and `alert-toast.xml` in the files table (A9); the report scopes (only `branch` defaults to the current repository); `daemon stop` ending the process after 15 seconds; restarting the daemon after an overlay change; the shell profile `install` edits; `daemon.log` at sign-in; building from source into a permanent folder and running `install` from there; and the new `could not record spans` warning in troubleshooting.
- **Upgrading and removing the extension.** The daemon and every Copilot session that uses the `ledger` MCP server run the extension's binary, and on Windows gh can't rewrite or delete it until they stop. So the guide, `docs/cli.md`, and troubleshooting say to stop the daemon and close the Copilot sessions and apps that use the MCP server before `gh extension upgrade` or `remove`. The steps are the same on every platform: only Windows was verified, and the steps do no harm elsewhere. `uninstall` doesn't touch an MCP registration (registering it in `install` is deferred), so the docs add `copilot mcp remove ledger`, and removing the VS Code entry.

### The action repository (Task R and Task 5)

- **Content.** A root `action.yml`, as Marketplace requires; `receipt.ps1` and `tests/receipt.Tests.ps1`, identical to gh-ledger's `action/` files except for comment lines that named `docs/action.md` or the `action/` directory, so Sprint 4's 18 cases and its review carry over; a `README.md` that takes over `docs/action.md`; a CI workflow that runs the tests on `ubuntu-latest` and `windows-latest`; and the existing `LICENSE`.
- **Marketplace-ready.** `name: CopilotLedger PR Receipt` (free on 2026-11-21), a one-sentence `description`, `author: swigerb`, and `branding` with the `dollar-sign` icon in `green`. Listing it is the user's step in the browser, with two-factor authentication and the Developer Agreement.
- **Versions.** Releases are `v1.0.0`, `v1.0.1`, and so on, plus a `v1` tag that the maintainer moves to each v1.x release. Docs show `uses: swigerb/copilot-ledger-action@v1`. The action's README shows how to pin a commit SHA instead.
- **gh-ledger's side (Task 5).** `action/` and `docs/action.md` go, CI stops running the action's tests (the action's own CI runs them), and `receipt.yml` uses the action pinned to the commit Task R merged, which the orchestrator tags `v1.0.0`. `swigerb/gh-ledger/action@v0.4.0` keeps working, because tags don't move, but no document shows it.
- **Ordering.** Task R runs in parallel. Task 5 is last and waits for Task R's merge, so this repository never points at an action that doesn't exist.

### Archives (A12)

- This plan's pull request moves the Sprint 4 plan to `.github/plans/archive/sprint-4.md`, with an amendments section that records none. It also adds the missing amendments section to `.github/plans/archive/sprint-1.md`: the CI workflow added in Task 1, and the `Calculate` contract that a later sprint found didn't hold.
## Task 1 specification: Audit Fixes & Version 0.4.1

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Otlp/Receiver/OtlpReceiverApp.cs` | `Build` takes `ingestFailed`; `HandleAsync` reports each 503's exception (A1) |
| `src/CopilotLedger.Cli/Daemon/IngestFailureLog.cs` | New, copy exactly (A1) |
| `src/CopilotLedger.Cli/Daemon/DaemonHost.cs` | Passes `IngestFailureLog.Report` to `Build` (A1) |
| `src/CopilotLedger.Otlp/Breaker/CircuitBreakerMonitor.cs` | Runs the flag-file sync with `CancellationToken.None`, and says why (A11) |
| `src/CopilotLedger.Otlp/Breaker/CircuitFlagFile.cs` | `SyncAsync` deletes its temporary file whatever it throws (A11) |
| `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs` | Remove `LoadDefault()` (A5) |
| `src/CopilotLedger.Core/LedgerPaths.cs` | Remove `DefaultDatabasePath` and `DefaultRatesOverlayPath` (A5) |
| `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`, `src/CopilotLedger.Cli/GhLedgerCli.cs`, `.github/workflows/ci.yml` | Version 0.4.1 |
| `tests/CopilotLedger.Tests/TestSupport/FileRetry.cs` | Add `ReadAllText` (A2) |
| `tests/CopilotLedger.Tests/TestSupport/GitFixture.cs` | Read back the files it writes (A2) |
| `tests/CopilotLedger.Tests/TestSupport/FakeCliPlatform.cs` | `HomeDirectory` throws until set (A10) |
| `tests/CopilotLedger.Tests/Otlp/OtlpReceiverTests.cs` | RC8 checks the callback (Task 1 tests) |
| `tests/CopilotLedger.Tests/Cli/DaemonCommandTests.cs` | DM1's version; new DM7 (Task 1 tests) |
| `tests/CopilotLedger.Tests/Otlp/CircuitBreakerMonitorTests.cs` | New BM12 (Task 1 tests), plus two of the A2 reads below |
| `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs` | `StatusText`'s version (Task 1 tests) |
| `AlertDaemonTests.cs`, `BreakerCommandTests.cs`, `InstallCommandTests.cs`, `RatesCommandTests.cs` (in `tests/CopilotLedger.Tests/Cli/`) and `CircuitBreakerMonitorTests.cs` (in `tests/CopilotLedger.Tests/Otlp/`) | 25 reads of fresh files go through `FileRetry` (A2, Task 1 tests) |

Make each edit below exactly as shown. Every "replace" block occurs exactly once in its file at `3a0a514`.

### Report each 503's cause (A1, copy exactly)

Edit 1 of 4 in `src/CopilotLedger.Otlp/Receiver/OtlpReceiverApp.cs`, replace:

```csharp
{
    // Serves the OTLP endpoints on localhost (127.0.0.1 and ::1) at options.Port.
    // configureWebHost runs after the defaults; tests pass builder => builder.UseTestServer().
    public static WebApplication Build(
        OtlpReceiverOptions options,
        ILedgerIngestor ingestor,
        Action<IWebHostBuilder>? configureWebHost = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ingestor);
```

with:

```csharp
{
    // Serves the OTLP endpoints on localhost (127.0.0.1 and ::1) at options.Port.
    // configureWebHost runs after the defaults; tests pass builder => builder.UseTestServer().
    // ingestFailed receives each exception from IngestAsync that turns a request into a 503, so the caller can log it.
    public static WebApplication Build(
        OtlpReceiverOptions options,
        ILedgerIngestor ingestor,
        Action<IWebHostBuilder>? configureWebHost = null,
        Action<Exception>? ingestFailed = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ingestor);
```

Edit 2 of 4 in `src/CopilotLedger.Otlp/Receiver/OtlpReceiverApp.cs`, replace:

```csharp
        configureWebHost?.Invoke(builder.WebHost);
        WebApplication app = builder.Build();

        app.MapPost("/v1/traces", context => HandleAsync(context, options, ingestor, traces: true));
        app.MapPost("/v1/metrics", context => HandleAsync(context, options, ingestor, traces: false));
        app.MapPost("/v1/logs", context => HandleAsync(context, options, ingestor, traces: false));
        return app;
    }
```

with:

```csharp
        configureWebHost?.Invoke(builder.WebHost);
        WebApplication app = builder.Build();

        app.MapPost("/v1/traces", context => HandleAsync(context, options, ingestor, ingestFailed, traces: true));
        app.MapPost("/v1/metrics", context => HandleAsync(context, options, ingestor, ingestFailed, traces: false));
        app.MapPost("/v1/logs", context => HandleAsync(context, options, ingestor, ingestFailed, traces: false));
        return app;
    }
```

Edit 3 of 4 in `src/CopilotLedger.Otlp/Receiver/OtlpReceiverApp.cs`, replace:

```csharp
        HttpContext context,
        OtlpReceiverOptions options,
        ILedgerIngestor ingestor,
        bool traces)
    {
        OtlpEncoding encoding = OtlpHttp.EncodingFromContentType(
```

with:

```csharp
        HttpContext context,
        OtlpReceiverOptions options,
        ILedgerIngestor ingestor,
        Action<Exception>? ingestFailed,
        bool traces)
    {
        OtlpEncoding encoding = OtlpHttp.EncodingFromContentType(
```

Edit 4 of 4 in `src/CopilotLedger.Otlp/Receiver/OtlpReceiverApp.cs`, replace:

```csharp
        {
            return;
        }
        catch
        {
            context.Response.Headers.RetryAfter = "5";
            await OtlpHttp.WriteErrorAsync(
                context.Response,
```

with:

```csharp
        {
            return;
        }
        catch (Exception ex)
        {
            ingestFailed?.Invoke(ex);
            context.Response.Headers.RetryAfter = "5";
            await OtlpHttp.WriteErrorAsync(
                context.Response,
```

### `IngestFailureLog.cs` (copy exactly)

New file `src/CopilotLedger.Cli/Daemon/IngestFailureLog.cs`:

```csharp
namespace CopilotLedger.Cli.Daemon;

// Writes "Warning: could not record spans: <type>: <message>" for the first ingestion failure, then at most once a
// minute, so a ledger that stays unavailable can't flood daemon.log. After the clock steps back, the next failure is
// written at once. A line that can't be written is dropped.
internal sealed class IngestFailureLog(Action<string> write, TimeProvider timeProvider)
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private DateTimeOffset? _lastWritten;

    public void Report(Exception exception)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (_lastWritten is DateTimeOffset last && now >= last && now - last < Interval)
            {
                return;
            }

            _lastWritten = now;
        }

        try
        {
            write($"Warning: could not record spans: {exception.GetType().Name}: {exception.Message}");
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }
    }
}
```

### Log through it (A1, copy exactly)

In `src/CopilotLedger.Cli/Daemon/DaemonHost.cs`, replace:

```csharp
                    context.TimeProvider),
                new CopilotSessionWorkspaceLocator(context.CopilotHome, context.TimeProvider),
                monitor);
            app = OtlpReceiverApp.Build(
                new OtlpReceiverOptions { Port = port },
                ingestor,
                context.ConfigureWebHost);

            if (!OperatingSystem.IsWindows())
            {
```

with:

```csharp
                    context.TimeProvider),
                new CopilotSessionWorkspaceLocator(context.CopilotHome, context.TimeProvider),
                monitor);
            IngestFailureLog ingestFailures = new(
                message => WriteLog(writer, context, message),
                context.TimeProvider);
            app = OtlpReceiverApp.Build(
                new OtlpReceiverOptions { Port = port },
                ingestor,
                context.ConfigureWebHost,
                ingestFailures.Report);

            if (!OperatingSystem.IsWindows())
            {
```

### Finish the flag file (A11, copy exactly)

In `src/CopilotLedger.Otlp/Breaker/CircuitBreakerMonitor.cs`, replace:

```csharp
        try
        {
            await CircuitFlagFile.SyncAsync(
                _store,
                trip.RepositoryRoot);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
```

with:

```csharp
        try
        {
            // The trip is committed and published, so an aborted request mustn't leave its flag file unwritten.
            await CircuitFlagFile.SyncAsync(
                _store,
                trip.RepositoryRoot,
                CancellationToken.None);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
```

Edit 1 of 2 in `src/CopilotLedger.Otlp/Breaker/CircuitFlagFile.cs`, replace:

```csharp
    }

    // Makes the flag file match the ledger. Throws IOException or UnauthorizedAccessException from the file system, and
    // whatever the store throws. Does nothing when repositoryRoot isn't an existing directory.
    public static async Task SyncAsync(
        SqliteLedgerStore store,
        string repositoryRoot,
```

with:

```csharp
    }

    // Makes the flag file match the ledger. Throws IOException or UnauthorizedAccessException from the file system, and
    // whatever the store throws, including OperationCanceledException. Whatever it throws, it first tries to delete the
    // temporary file it was writing. Does nothing when repositoryRoot isn't an existing directory.
    public static async Task SyncAsync(
        SqliteLedgerStore store,
        string repositoryRoot,
```

Edit 2 of 2 in `src/CopilotLedger.Otlp/Breaker/CircuitFlagFile.cs`, replace:

```csharp
                    await File.WriteAllTextAsync(temp, desired, Utf8, cancellationToken);
                    File.Move(temp, path, overwrite: true);
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException)
                {
                    try
                    {
```

with:

```csharp
                    await File.WriteAllTextAsync(temp, desired, Utf8, cancellationToken);
                    File.Move(temp, path, overwrite: true);
                }
                catch
                {
                    try
                    {
```

### Remove dead members (A5, copy exactly)

In `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs`, replace:

```csharp
        }
    }

    public static JsonRateCardCalculator LoadDefault()
    {
        return File.Exists(LedgerPaths.DefaultRatesOverlayPath)
            ? LoadEmbeddedWithOverlay(LedgerPaths.DefaultRatesOverlayPath)
            : LoadEmbedded();
    }

    // baseJson is a complete rates document, such as a downloaded rates.json; its errors have no prefix. When overlayPath
    // isn't null, that overlay is merged as LoadEmbeddedWithOverlay merges it, and its errors start with overlayPath.
    public static JsonRateCardCalculator LoadWithOverlay(string baseJson, string? overlayPath)
```

with:

```csharp
        }
    }

    // baseJson is a complete rates document, such as a downloaded rates.json; its errors have no prefix. When overlayPath
    // isn't null, that overlay is merged as LoadEmbeddedWithOverlay merges it, and its errors start with overlayPath.
    public static JsonRateCardCalculator LoadWithOverlay(string baseJson, string? overlayPath)
```

In `src/CopilotLedger.Core/LedgerPaths.cs`, replace:

```csharp
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot-ledger");

    public static string DefaultDatabasePath { get; } =
        Path.Combine(DataDirectory, "ledger.db");

    public static string DefaultRatesOverlayPath { get; } =
        Path.Combine(DataDirectory, "rates.json");
}
```

with:

```csharp
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot-ledger");
}
```

### Version 0.4.1 (copy exactly)

In `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`, replace:

```xml
    <AssemblyName>gh-ledger</AssemblyName>
    <RootNamespace>CopilotLedger.Cli</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Version>0.4.0</Version>
    <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
    <InvariantGlobalization>true</InvariantGlobalization>
    <EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
```

with:

```xml
    <AssemblyName>gh-ledger</AssemblyName>
    <RootNamespace>CopilotLedger.Cli</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Version>0.4.1</Version>
    <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
    <InvariantGlobalization>true</InvariantGlobalization>
    <EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
```

In `src/CopilotLedger.Cli/GhLedgerCli.cs`, replace:

```csharp
public static class GhLedgerCli
{
    // AssemblyInformationalVersionAttribute: "0.4.0"
    public static string Version { get; } =
        typeof(GhLedgerCli).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "0.4.0";

    // Builds a new RootCommand, parses args, and invokes it with
    // new InvocationConfiguration { Output = context.Out, Error = context.Error, ProcessTerminationTimeout = null }.
```

with:

```csharp
public static class GhLedgerCli
{
    // AssemblyInformationalVersionAttribute: "0.4.1"
    public static string Version { get; } =
        typeof(GhLedgerCli).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "0.4.1";

    // Builds a new RootCommand, parses args, and invokes it with
    // new InvocationConfiguration { Output = context.Out, Error = context.Error, ProcessTerminationTimeout = null }.
```

In `.github/workflows/ci.yml`, replace:

```yaml
        env:
          COPILOT_LEDGER_HOME: ${{ runner.temp }}/copilot-ledger
        run: |
          test "$(./artifacts/gh-ledger/gh-ledger --version | tr -d '\r')" = "0.4.0"
          ./artifacts/gh-ledger/gh-ledger rates validate
          started=$(./artifacts/gh-ledger/gh-ledger daemon start | tr -d '\r')
          echo "$started"
```

with:

```yaml
        env:
          COPILOT_LEDGER_HOME: ${{ runner.temp }}/copilot-ledger
        run: |
          test "$(./artifacts/gh-ledger/gh-ledger --version | tr -d '\r')" = "0.4.1"
          ./artifacts/gh-ledger/gh-ledger rates validate
          started=$(./artifacts/gh-ledger/gh-ledger daemon start | tr -d '\r')
          echo "$started"
```

DM1 and `ReportCommandTests.StatusText` change with it (see "Task 1 tests").

### Test support (A2 and A10, copy exactly)

Add `ReadAllText` at the end of `FileRetry`:

In `tests/CopilotLedger.Tests/TestSupport/FileRetry.cs`, replace:

```csharp
            }
        }
    }
}
```

with:

```csharp
            }
        }
    }

    // Like ReadAllTextAsync, for synchronous helpers such as GitFixture.Create.
    public static string ReadAllText(string path)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(25);
            }
        }
    }
}
```

In `tests/CopilotLedger.Tests/TestSupport/GitFixture.cs`, replace:

```csharp
        File.WriteAllText(
            Path.Combine(git, "config"),
            "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n");
        return repo;
    }
```

with:

```csharp
        File.WriteAllText(
            Path.Combine(git, "config"),
            "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n");

        // Reading the files back waits out a brief antivirus lock, so the resolver sees them when the test runs.
        _ = FileRetry.ReadAllText(Path.Combine(git, "HEAD"));
        _ = FileRetry.ReadAllText(branch);
        _ = FileRetry.ReadAllText(Path.Combine(git, "config"));
        return repo;
    }
```

In `tests/CopilotLedger.Tests/TestSupport/FakeCliPlatform.cs`, replace:

```csharp
public sealed class FakeCliPlatform : ICliPlatform
{
    public bool IsWindows { get; set; }
    public bool IsMacOS { get; set; }
    public string HomeDirectory { get; set; } = "/home/test";
    public string? ProcessPath { get; set; } = "/opt/gh-ledger/gh-ledger";
    public string? ShellPath { get; set; }
    public Dictionary<string, string> ProcessVariables { get; } =
```

with:

```csharp
public sealed class FakeCliPlatform : ICliPlatform
{
    private string? _homeDirectory;

    public bool IsWindows { get; set; }
    public bool IsMacOS { get; set; }

    // Throws until a test sets it, so install can't write a profile or sign-in item outside the test's TempDirectory.
    public string HomeDirectory
    {
        get => _homeDirectory
            ?? throw new InvalidOperationException(
                "Set FakeCliPlatform.HomeDirectory to a directory under the test's TempDirectory.");
        set => _homeDirectory = value;
    }

    public string? ProcessPath { get; set; } = "/opt/gh-ledger/gh-ledger";
    public string? ShellPath { get; set; }
    public Dictionary<string, string> ProcessVariables { get; } =
```

### Acceptance criteria

- `dotnet build CopilotLedger.slnx` reports 0 warnings and 0 errors.
- `dotnet test CopilotLedger.slnx` passes 651 tests: the 649 from before plus DM7 and BM12. 0 failed, 0 skipped.
- Each file in the table matches this section and "Task 1 tests" exactly, and no other file changes.
- `src/`, `tests/`, and `.github/workflows/` contain no `0.4.0`, `LoadDefault`, `DefaultDatabasePath`, or `DefaultRatesOverlayPath`.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```
## Task 2 specification: Workflow Hardening

### Files

| File | Change |
|---|---|
| `.github/workflows/ci.yml` | Replace, copy exactly |
| `.github/workflows/release.yml` | Replace, copy exactly |
| `.github/workflows/receipt.yml` | Replace, copy exactly |

What changes, for review:

- **All three:** each remote `uses:` names a full commit SHA with its version in a comment, from the table in "Verified facts" (A7). `receipt.yml`'s local `uses: ./action` stays until Task 5.
- **`ci.yml`:** the publish step reads the runtime identifier from `$RID`, set in `env` (A6). The bash smoke step, after its `daemon start` check, posts one OTLP/HTTP JSON trace to the daemon, expects the response `{}`, and checks that `branch --all-repos --since all --json` shows 1 span and 50 AI credits. Its other lines, the PowerShell capture step, and the "Test the receipt action" step (which Task 5 removes) don't change.
- **`release.yml`:** a new `rates` job checks `ratesVersion` (A4), and the `release` job needs it.
- **`receipt.yml`:** only the pin. Task 5 replaces this file.

### The smoke test's trace

The trace has one `chat` span for `claude-opus-5.5`, 100,000 input and 5,000 output tokens, starting at 2026-11-21T00:00:00Z. It has no resource attributes, so the span is recorded under repository `(none)` and branch `(none)`, and `--all-repos --since all` finds it whatever the runner's date and directory. It costs `(100,000 × 4 + 5,000 × 20) / 1,000,000 = 0.50` USD, or 50 AI credits: below the breaker's 150-credit rule, so the smoke test trips no breaker and shows no desktop notification, on the runner or on a developer's machine. `branch --json` writes indented JSON, so the checks match the whole `"spans": 1,` and `"aiCredits": 50,` properties.

### `.github/workflows/ci.yml` (copy exactly)

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build-test:
    name: build-test (${{ matrix.os }})
    strategy:
      fail-fast: false
      matrix:
        include:
          - os: ubuntu-latest
            rid: linux-x64
          - os: windows-latest
            rid: win-x64
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-dotnet@0f4a354dd5547a28c70b7853dbd95d5b67886e73 # v6.1.0
        with:
          dotnet-version: 10.0.x
      - run: dotnet build CopilotLedger.slnx
      - run: dotnet test CopilotLedger.slnx
      - name: Test the receipt action
        shell: pwsh
        run: pwsh -NoProfile -File action/tests/receipt.Tests.ps1
      - name: Publish single-file gh-ledger
        shell: bash
        env:
          RID: ${{ matrix.rid }}
        run: dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r "$RID" -o artifacts/gh-ledger
      - name: Smoke-test the published binary
        shell: bash
        timeout-minutes: 5
        env:
          COPILOT_LEDGER_HOME: ${{ runner.temp }}/copilot-ledger
        run: |
          test "$(./artifacts/gh-ledger/gh-ledger --version | tr -d '\r')" = "0.4.1"
          ./artifacts/gh-ledger/gh-ledger rates validate
          started=$(./artifacts/gh-ledger/gh-ledger daemon start | tr -d '\r')
          echo "$started"
          grep -Eq '^gh-ledger daemon started \(pid [0-9]+\) on http://localhost:4318\.$' <<< "$started"
          trace='{"resourceSpans":[{"resource":{"attributes":[]},"scopeSpans":[{"spans":[{"traceId":"0af7651916cd43dd8448eb211c80319c","spanId":"b7ad6b7169203331","name":"chat claude-opus-5.5","kind":3,"startTimeUnixNano":"1795219200000000000","endTimeUnixNano":"1795219202000000000","attributes":[{"key":"gen_ai.operation.name","value":{"stringValue":"chat"}},{"key":"gen_ai.request.model","value":{"stringValue":"claude-opus-5.5"}},{"key":"gen_ai.usage.input_tokens","value":{"intValue":"100000"}},{"key":"gen_ai.usage.output_tokens","value":{"intValue":"5000"}}]}]}]}]}'
          test "$(curl -sS -H 'Content-Type: application/json' --data "$trace" http://localhost:4318/v1/traces)" = "{}"
          spend=$(./artifacts/gh-ledger/gh-ledger branch --all-repos --since all --json | tr -d '\r')
          echo "$spend"
          grep -q '"spans": 1,' <<< "$spend"
          grep -q '"aiCredits": 50,' <<< "$spend"
          ./artifacts/gh-ledger/gh-ledger status
          ./artifacts/gh-ledger/gh-ledger breaker status
          ./artifacts/gh-ledger/gh-ledger budget list
          request='{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientCapabilities":{}}}}'
          response=$( { echo "$request"; sleep 10; } | ./artifacts/gh-ledger/gh-ledger mcp )
          echo "$response"
          grep -q '"get_branch_ledger"' <<< "$response"
          ./artifacts/gh-ledger/gh-ledger daemon stop
      - name: Smoke-test daemon start under PowerShell capture
        shell: pwsh
        timeout-minutes: 5
        env:
          COPILOT_LEDGER_HOME: ${{ runner.temp }}/copilot-ledger-pwsh
        run: |
          $started = & ./artifacts/gh-ledger/gh-ledger daemon start --port 4319
          $started
          if ($started -cnotmatch '^gh-ledger daemon started \(pid \d+\) on http://localhost:4319\.$') { exit 1 }
          & ./artifacts/gh-ledger/gh-ledger daemon stop
```

### `.github/workflows/release.yml` (copy exactly)

```yaml
name: Release

on:
  push:
    tags: ['v*']
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: release-${{ github.ref }}
  cancel-in-progress: false

jobs:
  build:
    name: build (${{ matrix.rid }})
    strategy:
      fail-fast: true
      matrix:
        include:
          - os: windows-latest
            rid: win-x64
            asset: gh-ledger-windows-amd64.exe
            smoke: true
          - os: windows-latest
            rid: win-arm64
            asset: gh-ledger-windows-arm64.exe
            smoke: false
          - os: ubuntu-latest
            rid: linux-x64
            asset: gh-ledger-linux-amd64
            smoke: true
          - os: ubuntu-latest
            rid: linux-arm64
            asset: gh-ledger-linux-arm64
            smoke: false
          - os: macos-latest
            rid: osx-x64
            asset: gh-ledger-darwin-amd64
            smoke: false
          - os: macos-latest
            rid: osx-arm64
            asset: gh-ledger-darwin-arm64
            smoke: true
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-dotnet@0f4a354dd5547a28c70b7853dbd95d5b67886e73 # v6.1.0
        with:
          dotnet-version: 10.0.x
      - name: Check the version
        shell: bash
        run: |
          version=$(dotnet msbuild src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -getProperty:Version | tr -d '\r')
          echo "Project version: $version"
          if [ "$GITHUB_REF_TYPE" = "tag" ] && [ "v$version" != "$GITHUB_REF_NAME" ]; then
            echo "::error::Tag $GITHUB_REF_NAME doesn't match the project version $version."
            exit 1
          fi
      - name: Publish
        shell: bash
        env:
          RID: ${{ matrix.rid }}
        run: dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r "$RID" -o publish
      - name: Name the asset
        shell: bash
        env:
          ASSET: ${{ matrix.asset }}
        run: |
          mkdir dist
          if [ -f publish/gh-ledger.exe ]; then
            cp publish/gh-ledger.exe "dist/$ASSET"
          else
            cp publish/gh-ledger "dist/$ASSET"
          fi
      - name: Smoke-test the asset
        if: matrix.smoke
        shell: bash
        env:
          ASSET: ${{ matrix.asset }}
        run: |
          version=$(dotnet msbuild src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -getProperty:Version | tr -d '\r')
          test "$("./dist/$ASSET" --version | tr -d '\r')" = "$version"
      - uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: ${{ matrix.asset }}
          path: dist/${{ matrix.asset }}
          if-no-files-found: error

  rates:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - name: Check ratesVersion against the latest release
        shell: bash
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          if ! gh release download --repo "$GITHUB_REPOSITORY" --pattern rates.json --dir latest; then
            echo "Skipping the check: no rates.json was downloaded from the latest release."
            exit 0
          fi
          current=src/CopilotLedger.Core/Pricing/rates.json
          old=$(jq -r .ratesVersion latest/rates.json)
          new=$(jq -r .ratesVersion "$current")
          if cmp -s <(jq -S . latest/rates.json) <(jq -S . "$current"); then
            echo "rates.json is unchanged since the latest release ($old)."
          elif [[ "$new" > "$old" ]]; then
            echo "rates.json changed since the latest release; ratesVersion moves from $old to $new."
          else
            echo "::error::rates.json changed since the latest release, but its ratesVersion $new isn't later than $old. Set ratesVersion to the date of the change."
            exit 1
          fi

  release:
    needs: [build, rates]
    if: github.event_name == 'push' && github.ref_type == 'tag'
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2
        with:
          path: dist
          merge-multiple: true
      - name: Add the rates and checksums
        run: |
          cp src/CopilotLedger.Core/Pricing/rates.json dist/rates.json
          cd dist
          sha256sum gh-ledger-* rates.json > gh-ledger-checksums.txt
          cat gh-ledger-checksums.txt
      - name: Create the release
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          prerelease=()
          case "$GITHUB_REF_NAME" in
            *-*) prerelease=(--prerelease) ;;
          esac
          gh release create "$GITHUB_REF_NAME" dist/* --verify-tag --title "gh-ledger $GITHUB_REF_NAME" --generate-notes "${prerelease[@]}"
```

### `.github/workflows/receipt.yml` (copy exactly)

```yaml
name: Receipt

on:
  pull_request:
    types: [opened, synchronize, reopened]

permissions:
  contents: read
  pull-requests: write

concurrency:
  group: receipt-${{ github.event.pull_request.number }}
  cancel-in-progress: true

jobs:
  receipt:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: ./action
```

### Acceptance criteria

- The three files match the blocks above exactly.
- Every remote `uses:` names a 40-character commit SHA followed by `# v<version>`; the only other `uses:` is `receipt.yml`'s `./action`. No `run:` script contains `${{`.
- `dotnet build CopilotLedger.slnx` reports 0 warnings, and `dotnet test CopilotLedger.slnx` passes 651 tests (0 failed, 0 skipped).
- CI on the pull request passes on `ubuntu-latest` and `windows-latest`, including the new smoke lines, and the Receipt workflow runs with the pinned checkout.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

Then the orchestrator runs the Release workflow from the task's branch (`gh workflow run release.yml --ref <branch> -R swigerb/gh-ledger`) and confirms that all six builds and the three smoke tests pass, that the `rates` job prints `rates.json is unchanged since the latest release (2026-11-20).`, and that the `release` job is skipped.
## Task 3 specification: User Guide with Tested Samples

### Files

| File | Change |
|---|---|
| `docs/guide.md` | Replace, copy exactly |
| `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs` | New, copy exactly |
| `tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs` | New, copy exactly (see "Task 3 tests") |
| `tests/CopilotLedger.Tests/TestSupport/FileRetry.cs` | Add `Delete` |
| `tests/CopilotLedger.Tests/CopilotLedger.Tests.csproj` | Copy `docs/guide.md` into the test output |

### The example world

`GuideWorld` builds the setting that every sample comes from:

- **gh-ledger as the extension on Windows.** A `FakeCliPlatform` with `IsWindows`, `ProcessPath` `C:\Users\you\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe`, and that directory's `manifest.yml` in `ExistingFiles`, so gh-ledger detects the extension. `RunningProcesses` holds 4242, the stand-in daemon's process ID.
- **The time.** A `ManualTimeProvider` at UTC-5 (`ManualTimeProvider.UtcMinus5`); "now" is 2026-11-21T21:30:00Z, 16:30 local time.
- **The repository.** `<root>/webapp`, on branch `feature/search` at commit `4f9c2e1d8b7a6c5d4e3f2a1b0c9d8e7f6a5b4c3d`, with `origin` `https://github.com/contoso/webapp.git`, so its key is `contoso/webapp`. Commands run there.
- **Three states.** `Fresh`: nothing installed and no ledger. `FirstPrompt`: installed at 16:20 local time, with one prompt at 16:25. `Month`: installed at 08:02, with a month of spend (below), the guide's overlay saved as `rates.json`, one breaker trip that has already been notified, and a 750-credit budget on `feature/search`.
- **The stand-in daemon** answers `install` and `daemon start` (see "Design decisions"). The budget alert sample uses the real daemon, in-process.
- **`Display`** shows the test's data directory as `C:\Users\you\.copilot-ledger`.

The `Month` state's spans, priced with the built-in rates and the guide's overlay. Rates are USD per 1M tokens: `claude-opus-5.5` $4 input, $0.20 cached input, $20 output; `mai-code-2` (promotional until 2027-05-17) $0.50, $0.025, $2.50; the overlay's `gpt-6.1-sol` $1.75 and $14.00. Local savings use `mai-code-2`'s rates. Each span ends 2 seconds after it starts.

| Trace | Start (UTC) | Branch | Model | Agent | Input | Cached | Output | Cost |
|---|---|---|---|---|---:|---:|---:|---|
| `…00a1` | 2026-11-21 14:05 | `feature/search` | `claude-opus-5.5` | | 400,000 | 250,000 | 15,000 | 95.00 credits |
| `…00a1` | 2026-11-21 14:07 | `feature/search` | `mai-code-2` | `explore` | 300,000 | 0 | 15,000 | 18.75 credits |
| `…00b2` | 16:20, 16:21:30, 16:23, 16:24:30 | `feature/search` | `claude-opus-5.5` | | 500,000 each | 300,000 each | 20,000 each | 126.00 credits each |
| `…00c3` | 2026-11-21 19:10 | `feature/search` | `gpt-6.1-sol` at `contoso.openai.azure.com` | | 200,000 | 0 | 10,000 | $0.49 BYOK |
| `…00d4` | 2026-11-21 20:40 | `feature/search` | `qwen3.8-27b` at `127.0.0.1` | | 300,000 | 0 | 9,000 | $0.00; saves $0.1725 |
| `…00e5` | 2026-11-19 15:00 | `fix/login-timeout` | `claude-opus-5.5` | `code-review` | 200,000 | 0 | 20,000 | 120.00 credits |
| `…00f6` | 2026-11-18 18:00 | `main` | `mai-code-2` | | 800,000 | 500,000 | 40,000 | 26.25 credits |
| `…00a7` | 2026-11-03 15:30 | `main` | `claude-opus-5.5` | `explore` | 300,000 | 0 | 12,000 | 144.00 credits |

Recomputed in decimal, these give every number in the samples:

- `feature/search`: 95 + 18.75 + 4 × 126 = 617.75 credits ($6.1775, shown as $6.18), $0.49 BYOK, and $0.1725 saved (shown as $0.17). Billed, with BYOK at $0.01 per credit: 617.75 + 49 = 666.75, which the budget samples show.
- `main`: 26.25 + 144 = 170.25. `fix/login-timeout`: 120.00. Today (from 05:00Z): the 8 `feature/search` spans, 617.75; the last 7 days add `…00e5` and `…00f6`, 764.00; the last 30 days add `…00a7`, 908.00.
- Agents: `(none)` 95 + 504 + 26.25 = 625.25 over 8 spans; `explore` 18.75 + 144 = 162.75; `code-review` 120.00.
- Prompts by cost: `…00b2` 504.00, `…00a7` 144.00, `…00e5` 120.00, then `…00a1` 113.75.
- The breaker: `…00b2`'s first two spans make 252.00 credits within 5 minutes, over the 150-credit limit, so it trips when the second ends, at 16:21:32Z (11:21 local).
- The receipt: `claude-opus-5.5` 95 + 504 = 599.00 over 5 turns, `mai-code-2` 18.75, 4 prompts, 309,000 local tokens. `get_branch_ledger`: 3,200,000 input, 129,000 output, and 1,450,000 cached tokens.
- `FirstPrompt`'s prompt: `claude-opus-5.5`, 30,000 input and 1,500 output tokens, 15.00 credits. The budget alert's span: 250,000 input tokens of `claude-opus-5.5`, 100.00 credits, which takes the branch to 766.75.

### How samples are marked

A sample starts with a line `<!-- sample: <id> -->` and ends with a line `<!-- /sample -->`. Between them is one of:

- a `shell` block with one `gh ledger` command, a blank line, and a `text` block with its output;
- a single `text` or `json` block;
- the receipt, with every line quoted (`> ` before each line, `>` alone for an empty one).

`GuideSamplesTests` reads these from `docs/guide.md` in the test output. When you change the guide, keep every marker line, and don't put anything else between the markers.

### `docs/guide.md` (copy exactly)

````markdown
# CopilotLedger user guide

CopilotLedger records what GitHub Copilot spends and shows it by repository, branch, model, agent, and prompt. This guide walks through everyday tasks, one scenario at a time. For every command and option, see the [command reference](cli.md).

The samples come from one example: gh-ledger installed as the GitHub CLI extension on Windows, used in the `contoso/webapp` repository on the `feature/search` branch at 16:30 on 2026-11-21, local time (UTC-5). After setup, every sample starts from the same state: a month of spend, a rates overlay with Foundry BYOK prices, a budget for `feature/search`, and an open circuit-breaker trip, so early samples show things that later sections explain. A test runs every sample, so each one is exactly what gh-ledger prints. Your numbers will differ. gh-ledger's messages name its commands `gh-ledger <command>`; with the extension, run them as `gh ledger <command>`.

- [Set up CopilotLedger](#set-up-copilotledger)
- [See what you spent](#see-what-you-spent)
- [Find the most expensive prompts](#find-the-most-expensive-prompts)
- [Compare agents](#compare-agents)
- [Set a budget for a branch](#set-a-budget-for-a-branch)
- [Catch a runaway loop](#catch-a-runaway-loop)
- [Post a receipt on a pull request](#post-a-receipt-on-a-pull-request)
- [Price Foundry BYOK and local models](#price-foundry-byok-and-local-models)
- [Ask Copilot about spend](#ask-copilot-about-spend)
- [Update rates](#update-rates)
- [Upgrade or uninstall](#upgrade-or-uninstall)

## Set up CopilotLedger

**Goal:** record what Copilot spends from now on.

```shell
gh extension install swigerb/gh-ledger
```

<!-- sample: install -->
```shell
gh ledger install
```

```text
Set COPILOT_OTEL_ENABLED=true for your user account.
gh ledger runs this GitHub CLI extension, so no alias is needed.
gh-ledger will start when you sign in.
gh-ledger daemon started (pid 4242) on http://localhost:4318.
CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.
```
<!-- /sample -->

On macOS and Linux, `install` sets the variable in your shell profile instead. Restart the apps and terminals as it says, send Copilot a prompt, and check that the prompt was recorded:

<!-- sample: status-first -->
```shell
gh ledger status
```

```text
gh-ledger 0.4.1 (GitHub CLI extension)
Daemon:  running (pid 4242) on http://localhost:4318 since 2026-11-21 16:20
Ledger:  C:\Users\you\.copilot-ledger\ledger.db
Rates:   2026-11-20
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
Today             1         0       15.00       $0.15     $0.00        $0.00
Last 7 days       1         0       15.00       $0.15     $0.00        $0.00
Last 30 days      1         0       15.00       $0.15     $0.00        $0.00
```
<!-- /sample -->

If no spans appear, fix any warnings that `status` shows, and see [Troubleshooting](cli.md#troubleshooting).

## See what you spent

**Goal:** see today's spend, and each branch's spend.

<!-- sample: status -->
```shell
gh ledger status
```

```text
gh-ledger 0.4.1 (GitHub CLI extension)
Daemon:  running (pid 4242) on http://localhost:4318 since 2026-11-21 08:02
Ledger:  C:\Users\you\.copilot-ledger\ledger.db
Rates:   2026-11-20+contoso-2026-11
Copilot: OTel export enabled (COPILOT_OTEL_ENABLED=true)

Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
Today             8         0      617.75       $6.18     $0.49        $0.17
Last 7 days      10         0      764.00       $7.64     $0.49        $0.17
Last 30 days     11         0      908.00       $9.08     $0.49        $0.17
```
<!-- /sample -->

<!-- sample: branch -->
```shell
gh ledger branch
```

```text
Branches in contoso/webapp (last 30 days)

Branch             Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
feature/search         8         0      617.75       $6.18     $0.49        $0.17
main                   2         0      170.25       $1.70     $0.00        $0.00
fix/login-timeout      1         0      120.00       $1.20     $0.00        $0.00
```
<!-- /sample -->

- `AI credits` and `GitHub USD` are what GitHub bills; 1 AI credit is $0.01. `BYOK USD` is what Microsoft Foundry bills you directly, and `Local saved` is what local models saved you. `Unpriced` counts requests that have no price.
- `branch` covers the current repository. Add `--all-repos` for every repository, `--since` for another period, such as `today`, `7d`, `all`, or a date, and `--json` for exact amounts.

## Find the most expensive prompts

**Goal:** find the prompts that cost the most.

<!-- sample: top-prompts -->
```shell
gh ledger top-prompts --limit 3
```

```text
Top 3 prompts by cost in all repositories (last 30 days)

#  Started           Repository      Agents       Models           Spans  AI credits  GitHub USD  BYOK USD  Local saved  Trace
1  2026-11-21 11:20  contoso/webapp  -            claude-opus-5.5      4      504.00       $5.04     $0.00        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2
2  2026-11-03 10:30  contoso/webapp  explore      claude-opus-5.5      1      144.00       $1.44     $0.00        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00a7
3  2026-11-19 10:00  contoso/webapp  code-review  claude-opus-5.5      1      120.00       $1.20     $0.00        $0.00  0f3c9a7e5d1b4f2a8c6e0d9b7a5c00e5
```
<!-- /sample -->

Each row is one prompt you sent, with every model request Copilot made for it (`Spans`). `Agents` lists the named agents that worked on it, or `-` when only Copilot's default agent did. `top-prompts` and `agents` cover every repository unless you add `--repo`, for example `--repo contoso/webapp`.

## Compare agents

**Goal:** see how much each agent spends.

<!-- sample: agents -->
```shell
gh ledger agents
```

```text
Agents in all repositories (last 30 days)

Agent        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  Local saved
(none)           8         0      625.25       $6.25     $0.49        $0.17
explore          2         0      162.75       $1.63     $0.00        $0.00
code-review      1         0      120.00       $1.20     $0.00        $0.00
```
<!-- /sample -->

`(none)` is Copilot's default agent, which reports no name. Subagents such as `explore`, and your custom agents, report their own names.

## Set a budget for a branch

**Goal:** get an alert when a branch spends more than you planned.

<!-- sample: budget-set -->
```shell
gh ledger budget set 750
```

```text
Budget for contoso/webapp @ feature/search: 750.00 AI credits. Spent so far: 666.75.
```
<!-- /sample -->

<!-- sample: budget-list -->
```shell
gh ledger budget list
```

```text
contoso/webapp @ feature/search: 666.75 of 750.00 AI credits
```
<!-- /sample -->

Budgets count Foundry BYOK spend at $0.01 per AI credit, so the branch's 617.75 GitHub AI credits and $0.49 of BYOK spend make 666.75. When the branch passes its budget, the daemon shows a desktop notification once and writes this line to `daemon.log`:

<!-- sample: budget-alert -->
```text
2026-11-21T21:30:00.0000000+00:00 ALERT CopilotLedger branch budget exceeded: contoso/webapp @ feature/search: 766.75 of 750.00 AI credits spent. Run: gh-ledger budget list
```
<!-- /sample -->

A budget doesn't block anything. Set it again to re-arm the alert, or remove it with `gh ledger budget clear`.

## Catch a runaway loop

**Goal:** notice when an agent burns credits in a loop.

The daemon trips the circuit breaker when one prompt spends more than 150 AI credits in 5 minutes, or makes 5 failed tool calls in a row. A trip shows a desktop notification and writes an `ALERT` line to `daemon.log`; it doesn't stop the agent. To see what tripped:

<!-- sample: breaker-status -->
```shell
gh ledger breaker status
```

```text
Circuit breaker: open (1 trip)
Rules: more than 150.00 AI credits in 5 minutes on one prompt; 5 failed tool calls in a row on one prompt
Flag file: off. Notifications: on.

Trip 1 at 2026-11-21 11:21: contoso/webapp @ feature/search: 252.00 AI credits in 5 minutes on one prompt
  rule credit_velocity, trace 0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2, session 7d4e1c2a-5b3f-4e8d-9a6c-0000000000b2
Reset a trip with: gh-ledger breaker reset --id <id>
```
<!-- /sample -->

When you've dealt with the loop, close the branch's open trips:

<!-- sample: breaker-reset -->
```shell
gh ledger breaker reset
```

```text
Reset 1 trip.
```
<!-- /sample -->

To change the limits, or to have the daemon write a flag file that a wrapper script can watch, see [Circuit breaker](cli.md#circuit-breaker).

## Post a receipt on a pull request

**Goal:** show a branch's AI spend on its pull request.

Add this workflow to the repository as `.github/workflows/copilot-ledger-receipt.yml`:

```yaml
name: CopilotLedger receipt

on:
  pull_request:
    types: [opened, synchronize, reopened]

permissions:
  contents: read
  pull-requests: write

concurrency:
  group: copilot-ledger-receipt-${{ github.event.pull_request.number }}
  cancel-in-progress: true

jobs:
  receipt:
    runs-on: ubuntu-latest
    steps:
      - uses: swigerb/copilot-ledger-action@v1
```

On the pull request's branch, write the branch's receipt, then commit and push it:

<!-- sample: export-pr -->
```shell
gh ledger export-pr
```

```text
Wrote .github/copilot-ledger/receipts/feature-search-38e7a3a2.md for feature/search.
Commit it on the branch and push; the CopilotLedger action posts it on the pull request.
```
<!-- /sample -->

```shell
git add .github/copilot-ledger/receipts
git commit -m "Add CopilotLedger receipt"
git push
```

The action posts the receipt as a comment on the pull request:

<!-- sample: receipt -->
> <!-- copilot-ledger-receipt -->
> ### CopilotLedger receipt for `feature/search`
>
> - **GitHub AI Credits:** 617.75 credits ($6.18) across 6 turns: `claude-opus-5.5` 599.00, `mai-code-2` 18.75
> - **Foundry BYOK:** $0.49 across 1 turn: `gpt-6.1-sol` $0.49
> - **Local offload:** 309,000 tokens across 1 turn ($0.00 billed • $0.17 cloud spend avoided): `qwen3.8-27b` 309,000
> - **Circuit breaker:** 1 trip • max single-prompt burn: 504.00 credits
>
> <details>
> <summary>Details and data</summary>
>
> | Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | Cloud spend avoided |
> | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
> | GitHub AI Credits | `claude-opus-5.5` | 5 | 0 | 2,400,000 | 95,000 | 599.00 credits ($5.99) | $0.00 |
> | Foundry BYOK | `gpt-6.1-sol` | 1 | 0 | 200,000 | 10,000 | $0.49 | $0.00 |
> | GitHub AI Credits | `mai-code-2` | 1 | 0 | 300,000 | 15,000 | 18.75 credits ($0.19) | $0.00 |
> | Local | `qwen3.8-27b` | 1 | 0 | 300,000 | 9,000 | $0.00 | $0.17 |
>
> Repository `contoso/webapp` • 4 prompts • commit `4f9c2e1d8b7a` • generated 2026-11-21 21:30 UTC by gh-ledger 0.4.1
>
> To check these totals, run `gh ledger branch --since all --json` in the repository and compare the `feature/search` row.
>
> ```json
> {
>   "schemaVersion": 1,
>   "repository": "contoso/webapp",
>   "branch": "feature/search",
>   "commit": "4f9c2e1d8b7a6c5d4e3f2a1b0c9d8e7f6a5b4c3d",
>   "generatedAt": "2026-11-21T21:30:00.0000000+00:00",
>   "generator": "gh-ledger 0.4.1",
>   "totals": {
>     "spans": 8,
>     "unpricedSpans": 0,
>     "inputTokens": 3200000,
>     "outputTokens": 129000,
>     "cacheReadInputTokens": 1450000,
>     "cacheWriteInputTokens": 0,
>     "aiCredits": 617.75,
>     "aiCreditsUsd": 6.1775,
>     "foundryByokUsd": 0.49,
>     "localSavedUsd": 0.1725
>   },
>   "prompts": 4,
>   "maxPromptCredits": 504,
>   "breakerTrips": 1,
>   "models": [
>     {
>       "channel": "github_ai_credits",
>       "model": "claude-opus-5.5",
>       "spans": 5,
>       "unpricedSpans": 0,
>       "inputTokens": 2400000,
>       "outputTokens": 95000,
>       "cacheReadInputTokens": 1450000,
>       "cacheWriteInputTokens": 0,
>       "aiCredits": 599,
>       "aiCreditsUsd": 5.99,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0
>     },
>     {
>       "channel": "foundry_byok",
>       "model": "gpt-6.1-sol",
>       "spans": 1,
>       "unpricedSpans": 0,
>       "inputTokens": 200000,
>       "outputTokens": 10000,
>       "cacheReadInputTokens": 0,
>       "cacheWriteInputTokens": 0,
>       "aiCredits": 0,
>       "aiCreditsUsd": 0,
>       "foundryByokUsd": 0.49,
>       "localSavedUsd": 0
>     },
>     {
>       "channel": "github_ai_credits",
>       "model": "mai-code-2",
>       "spans": 1,
>       "unpricedSpans": 0,
>       "inputTokens": 300000,
>       "outputTokens": 15000,
>       "cacheReadInputTokens": 0,
>       "cacheWriteInputTokens": 0,
>       "aiCredits": 18.75,
>       "aiCreditsUsd": 0.1875,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0
>     },
>     {
>       "channel": "local",
>       "model": "qwen3.8-27b",
>       "spans": 1,
>       "unpricedSpans": 0,
>       "inputTokens": 300000,
>       "outputTokens": 9000,
>       "cacheReadInputTokens": 0,
>       "cacheWriteInputTokens": 0,
>       "aiCredits": 0,
>       "aiCreditsUsd": 0,
>       "foundryByokUsd": 0,
>       "localSavedUsd": 0.1725
>     }
>   ]
> }
> ```
>
> </details>
<!-- /sample -->

Before you merge, run `gh ledger export-pr` again and push, so the receipt includes the latest spend; the action updates the same comment. See the [action's README](https://github.com/swigerb/copilot-ledger-action) for its inputs and the checks it makes.

## Price Foundry BYOK and local models

**Goal:** see what your Microsoft Foundry BYOK models cost, and what local models save you.

CopilotLedger records requests to local runtimes, such as Foundry Local and Ollama, at $0.00 and estimates what they saved. Foundry bills BYOK requests to you directly, not through GitHub, so CopilotLedger records them unpriced until you add your prices. Create a rates overlay:

```shell
gh ledger rates init
```

Then edit `rates.json` in your data directory, `C:\Users\you\.copilot-ledger` here or `~/.copilot-ledger` on macOS and Linux, and list your models with the prices from your Azure pricing or invoice:

<!-- sample: rates-overlay -->
```json
{
  "schemaVersion": 1,
  "ratesVersion": "contoso-2026-11",
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
<!-- /sample -->

List your deployment names as `aliases`, because Foundry requests often report them instead of the model name. Check the file:

<!-- sample: rates-validate -->
```shell
gh ledger rates validate
```

```text
C:\Users\you\.copilot-ledger\rates.json is valid. Rates version: 2026-11-20+contoso-2026-11.
```
<!-- /sample -->

Restart the daemon, with `gh ledger daemon stop` and then `gh ledger daemon start`, so it prices new requests with your rates. BYOK spend then shows under `BYOK USD`, as in [See what you spent](#see-what-you-spent). For custom endpoints and local model names, see [configuration.md](configuration.md).

## Ask Copilot about spend

**Goal:** let Copilot check the branch's spend, budget, and circuit breaker while it works.

Register gh-ledger's MCP server with Copilot CLI. In PowerShell on Windows:

```powershell
copilot mcp add ledger -- "$env:LOCALAPPDATA\GitHub CLI\extensions\gh-ledger\gh-ledger.exe" mcp
```

On macOS and Linux:

```shell
copilot mcp add ledger -- ~/.local/share/gh/extensions/gh-ledger/gh-ledger mcp
```

Then ask Copilot, for example, "How much has this branch cost so far?" Copilot calls the `get_branch_ledger` tool. While the breaker is open or the branch is over its budget, the result starts with a `WARNING:` line; the data follows as JSON, indented here:

<!-- sample: mcp -->
```text
WARNING: CopilotLedger circuit breaker is open for contoso/webapp @ feature/search: 252.00 AI credits in 5 minutes on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1
{
  "repository": "contoso/webapp",
  "branch": "feature/search",
  "spans": 8,
  "unpricedSpans": 0,
  "inputTokens": 3200000,
  "outputTokens": 129000,
  "cacheReadInputTokens": 1450000,
  "cacheWriteInputTokens": 0,
  "aiCredits": 617.75,
  "aiCreditsUsd": 6.1775,
  "foundryByokUsd": 0.49,
  "localSavedUsd": 0.1725,
  "billedAiCredits": 666.75,
  "topModel": "claude-opus-5.5",
  "budget": {
    "aiCredits": 750,
    "spentAiCredits": 666.75,
    "exceeded": false
  },
  "circuitBreaker": {
    "state": "open",
    "openTrips": [
      {
        "id": 1,
        "rule": "credit_velocity",
        "repository": "contoso/webapp",
        "branch": "feature/search",
        "traceId": "0f3c9a7e5d1b4f2a8c6e0d9b7a5c00b2",
        "conversationId": "7d4e1c2a-5b3f-4e8d-9a6c-0000000000b2",
        "currentSession": false,
        "trippedAt": "2026-11-21T16:21:32.0000000+00:00",
        "detail": "252.00 AI credits in 5 minutes",
        "resetCommand": "gh-ledger breaker reset --id 1"
      }
    ]
  },
  "warnings": [
    "WARNING: CopilotLedger circuit breaker is open for contoso/webapp @ feature/search: 252.00 AI credits in 5 minutes on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id 1"
  ]
}
```
<!-- /sample -->

The warning tells Copilot to stop and ask you before it continues. No tool can reset the breaker, and Copilot asks before it changes a budget. For the other tools, and for VS Code, see [MCP server](cli.md#mcp-server).

## Update rates

**Goal:** price new requests with GitHub's latest prices before the next gh-ledger release.

<!-- sample: rates-update -->
```shell
gh ledger rates update
```

```text
Updated rates from 2026-11-20 to 2026-12-15.
Restart the daemon to price new spans with them: gh ledger daemon stop, then gh ledger daemon start.
```
<!-- /sample -->

Restart the daemon as it says. Requests already recorded keep the prices they were recorded with, and your overlay applies on top of the new rates.

## Upgrade or uninstall

**Goal:** move to a new gh-ledger release, or remove CopilotLedger.

The daemon runs the extension's binary, and so does every Copilot session that uses the `ledger` MCP server. Windows can't replace that file while either runs it, so before you upgrade, close the Copilot CLI sessions, the GitHub Copilot app, and VS Code if they use the MCP server, and stop the daemon:

<!-- sample: daemon-stop -->
```shell
gh ledger daemon stop
```

```text
gh-ledger daemon stopped.
```
<!-- /sample -->

```shell
gh extension upgrade ledger
```

Then start the daemon again, and reopen your Copilot apps:

<!-- sample: daemon-start -->
```shell
gh ledger daemon start
```

```text
gh-ledger daemon started (pid 4242) on http://localhost:4318.
```
<!-- /sample -->

To remove CopilotLedger, undo `install`:

<!-- sample: uninstall -->
```shell
gh ledger uninstall
```

```text
gh-ledger daemon stopped.
gh-ledger will no longer start when you sign in.
Removed COPILOT_OTEL_ENABLED from your user account.
CopilotLedger is uninstalled. Your ledger data is still in C:\Users\you\.copilot-ledger.
```
<!-- /sample -->

If you registered the MCP server, remove it, and remove the `ledger` entry from VS Code's `mcp.json` if you added one:

```shell
copilot mcp remove ledger
```

Then close the Copilot sessions and apps that still run it, and remove the extension:

```shell
gh extension remove ledger
```

Your ledger stays in your data directory; delete the directory to remove it too.
````

### `GuideWorld.cs` (copy exactly)

New file `tests/CopilotLedger.Tests/TestSupport/GuideWorld.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Otlp.Breaker;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.TestSupport;

public enum GuideState
{
    Fresh,          // nothing installed and no ledger yet
    FirstPrompt,    // installed at 16:20, the daemon running, one prompt at 16:25
    Month,          // installed, the daemon running since 08:02, a month of spend, one breaker trip, a budget, and an overlay
}

// The setup behind the samples in docs/guide.md: gh-ledger as the GitHub CLI extension on Windows, run in the
// contoso/webapp repository on branch feature/search at 16:30 on 2026-11-21, UTC-5. The daemon is a stand-in that
// writes daemon.json with process 4242, holds daemon.lock, and exits when daemon stop writes daemon.stop.
public sealed class GuideWorld : IAsyncDisposable
{
    public const string ExtensionPath = @"C:\Users\you\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe";
    public const string ShownDataDirectory = @"C:\Users\you\.copilot-ledger";
    public const string Slug = "contoso/webapp";
    public const string Commit = "4f9c2e1d8b7a6c5d4e3f2a1b0c9d8e7f6a5b4c3d";
    public const int DaemonProcessId = 4242;

    public static readonly DateTimeOffset Now = Utc("2026-11-21T21:30:00Z");

    private readonly ManualTimeProvider _clock;
    private readonly List<(CancellationTokenSource Stop, Task Run)> _daemons = [];

    private GuideWorld(string root, ManualTimeProvider clock)
    {
        _clock = clock;
        Repository = Path.Combine(root, "webapp");
        Platform = new FakeCliPlatform
        {
            IsWindows = true,
            ProcessPath = ExtensionPath,
        };
        Platform.ExistingFiles.Add(
            @"C:\Users\you\AppData\Local\GitHub CLI\extensions\gh-ledger\manifest.yml");
        Platform.RunningProcesses.Add(DaemonProcessId);
        Harness = new CliHarness(root, Platform, clock);
        Platform.StartDetachedHandler = (_, _, _) =>
        {
            StartDaemon();
            return DaemonProcessId;
        };
    }

    public CliHarness Harness { get; }
    public FakeCliPlatform Platform { get; }
    public string Repository { get; }
    public ManualTimeProvider Clock => _clock;

    // Creates the repository and the state; overlay is the rates overlay the Month state saves and prices with.
    public static async Task<GuideWorld> CreateAsync(string root, GuideState state, string overlay)
    {
        DateTimeOffset start = state switch
        {
            GuideState.Fresh => Now,
            GuideState.FirstPrompt => Utc("2026-11-21T21:20:00Z"),
            _ => Utc("2026-11-21T13:02:00Z"),
        };
        GuideWorld world = new(root, new ManualTimeProvider(start, ManualTimeProvider.UtcMinus5));
        world.CreateRepository();
        if (state != GuideState.Fresh)
        {
            CliRun install = await world.Harness.RunAsync("install");
            Assert.Equal(0, install.ExitCode);
        }

        world._clock.Advance(Now - start);
        if (state != GuideState.Fresh)
        {
            await world.SeedAsync(state, overlay);
        }

        return world;
    }

    // Runs "gh ledger <arguments>" in the repository.
    public Task<CliRun> RunAsync(string command)
    {
        Assert.StartsWith("gh ledger ", command);
        return Harness.RunInAsync(Repository, command["gh ledger ".Length..].Split(' '));
    }

    // Shows the test's data directory as C:\Users\you\.copilot-ledger.
    public string Display(string text)
    {
        string data = Harness.Context.DataDirectory;
        return text
            .Replace(data + Path.DirectorySeparatorChar, ShownDataDirectory + @"\", StringComparison.Ordinal)
            .Replace(data, ShownDataDirectory, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        foreach ((CancellationTokenSource stop, Task run) in _daemons)
        {
            await stop.CancelAsync();
            await run;
            stop.Dispose();
        }

        await Harness.DisposeAsync();
    }

    private static DateTimeOffset Utc(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private void CreateRepository()
    {
        string git = Path.Combine(Repository, ".git");
        string branch = Path.Combine(git, "refs", "heads", "feature", "search");
        Directory.CreateDirectory(Path.GetDirectoryName(branch)!);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/feature/search\n");
        File.WriteAllText(branch, Commit + "\n");
        File.WriteAllText(
            Path.Combine(git, "config"),
            "[remote \"origin\"]\n\turl = https://github.com/contoso/webapp.git\n");
        _ = FileRetry.ReadAllText(Path.Combine(git, "HEAD"));
        _ = FileRetry.ReadAllText(branch);
        _ = FileRetry.ReadAllText(Path.Combine(git, "config"));
    }

    // Each start holds its own lock until daemon stop writes daemon.stop or the world is disposed, then removes
    // daemon.json and daemon.stop and releases the lock, as the real daemon does.
    private void StartDaemon()
    {
        string data = Harness.Context.DataDirectory;
        FileStream daemonLock = new(
            Path.Combine(data, "daemon.lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        File.WriteAllText(
            Path.Combine(data, "daemon.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                processId = DaemonProcessId,
                startedAt = _clock.GetUtcNow(),
                endpoint = "http://localhost:4318",
                version = GhLedgerCli.Version,
                databasePath = Harness.Context.DatabasePath,
            }));
        _ = FileRetry.ReadAllText(Path.Combine(data, "daemon.json"));
        CancellationTokenSource stop = new();
        _daemons.Add((stop, RunDaemonAsync(data, daemonLock, stop.Token)));
    }

    private static async Task RunDaemonAsync(
        string data,
        FileStream daemonLock,
        CancellationToken cancellationToken)
    {
        string stopPath = Path.Combine(data, "daemon.stop");
        while (!cancellationToken.IsCancellationRequested && !File.Exists(stopPath))
        {
            await Task.Delay(25, CancellationToken.None);
        }

        try
        {
            FileRetry.Delete(Path.Combine(data, "daemon.json"));
            FileRetry.Delete(stopPath);
        }
        finally
        {
            await daemonLock.DisposeAsync();
        }
    }

    private async Task SeedAsync(GuideState state, string overlay)
    {
        SqliteLedgerStore store = new(Harness.Context.DatabasePath, _clock);
        await store.InitializeAsync();
        if (state == GuideState.FirstPrompt)
        {
            JsonRateCardCalculator builtIn = JsonRateCardCalculator.LoadEmbedded();
            NormalizedGenAiSpan first = Chat("1f", "c1", "2026-11-21T21:25:00Z", "claude-opus-5.5", null, null, 30_000, 1_500);
            await store.RecordAsync([new LedgerEntry(first, builtIn.Calculate(first), Context("feature/search"))]);
            return;
        }

        string overlayPath = Path.Combine(Harness.Context.DataDirectory, "rates.json");
        await File.WriteAllTextAsync(overlayPath, overlay);
        _ = await FileRetry.ReadAllTextAsync(overlayPath);
        JsonRateCardCalculator rates = JsonRateCardCalculator.LoadEmbeddedWithOverlay(overlayPath);
        (NormalizedGenAiSpan Span, string Branch)[] spans =
        [
            (Chat("a1", "c1", "2026-11-21T14:05:00Z", "claude-opus-5.5", null, null, 400_000, 15_000, 250_000), "feature/search"),
            (Chat("a1", "c2", "2026-11-21T14:07:00Z", "mai-code-2", null, "explore", 300_000, 15_000), "feature/search"),
            (Chat("b2", "c1", "2026-11-21T16:20:00Z", "claude-opus-5.5", null, null, 500_000, 20_000, 300_000), "feature/search"),
            (Chat("b2", "c2", "2026-11-21T16:21:30Z", "claude-opus-5.5", null, null, 500_000, 20_000, 300_000), "feature/search"),
            (Chat("b2", "c3", "2026-11-21T16:23:00Z", "claude-opus-5.5", null, null, 500_000, 20_000, 300_000), "feature/search"),
            (Chat("b2", "c4", "2026-11-21T16:24:30Z", "claude-opus-5.5", null, null, 500_000, 20_000, 300_000), "feature/search"),
            (Chat("c3", "c1", "2026-11-21T19:10:00Z", "gpt-6.1-sol", "contoso.openai.azure.com", null, 200_000, 10_000), "feature/search"),
            (Chat("d4", "c1", "2026-11-21T20:40:00Z", "qwen3.8-27b", "127.0.0.1", null, 300_000, 9_000), "feature/search"),
            (Chat("e5", "c1", "2026-11-19T15:00:00Z", "claude-opus-5.5", null, "code-review", 200_000, 20_000), "fix/login-timeout"),
            (Chat("f6", "c1", "2026-11-18T18:00:00Z", "mai-code-2", null, null, 800_000, 40_000, 500_000), "main"),
            (Chat("a7", "c1", "2026-11-03T15:30:00Z", "claude-opus-5.5", null, "explore", 300_000, 12_000), "main"),
        ];
        await store.RecordAsync(
            spans.Select(item => new LedgerEntry(item.Span, rates.Calculate(item.Span), Context(item.Branch))).ToArray());
        await new CircuitBreakerMonitor(store, new RecordingAlertSink()).EvaluateAsync(
            spans.Select(item => item.Span.TraceId));
        foreach (BreakerTrip trip in await store.GetOpenBreakerTripsAsync(BreakerTripFilter.All))
        {
            Assert.True(await store.MarkBreakerTripNotifiedAsync(trip.Id));
        }

        await store.SetBranchBudgetAsync(Slug, "feature/search", 750m);
    }

    private GitWorkspaceContext Context(string branch) => new(
        Repository,
        Path.Combine(Repository, ".git"),
        branch,
        Commit,
        "https://github.com/contoso/webapp.git",
        Slug);

    // trace and span are the last characters of 32- and 16-character IDs.
    private static NormalizedGenAiSpan Chat(
        string trace,
        string span,
        string start,
        string model,
        string? serverAddress,
        string? agent,
        long inputTokens,
        long outputTokens,
        long cacheReadInputTokens = 0) =>
        TestSpans.Chat(
            startTime: Utc(start),
            requestModel: model,
            serverAddress: serverAddress,
            agentName: agent,
            conversationId: "7d4e1c2a-5b3f-4e8d-9a6c-" + trace.PadLeft(12, '0'),
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            cacheReadInputTokens: cacheReadInputTokens) with
        {
            TraceId = "0f3c9a7e5d1b4f2a8c6e0d9b7a5c" + trace.PadLeft(4, '0'),
            SpanId = "5e8a1c3f7b9d" + trace.PadLeft(2, '0') + span,
        };
}
```

### `FileRetry.Delete` (copy exactly)

Add `Delete` at the end of `FileRetry`, after `ReadAllText`:

In `tests/CopilotLedger.Tests/TestSupport/FileRetry.cs`, replace:

```csharp
            }
        }
    }
}
```

with:

```csharp
            }
        }
    }

    // Deletes the file, retrying every 25 ms for up to 10 seconds while Windows reports it in use
    // or pending deletion.
    public static void Delete(string path)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (Exception ex) when (
                (ex is IOException or UnauthorizedAccessException) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(25);
            }
        }
    }
}
```

### Test project (copy exactly)

In `tests/CopilotLedger.Tests/CopilotLedger.Tests.csproj`, replace:

```xml
  <ItemGroup>
    <None Include="..\..\docs\configuration.md" Link="docs\configuration.md" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <ItemGroup>
```

with:

```xml
  <ItemGroup>
    <None Include="..\..\docs\configuration.md" Link="docs\configuration.md" CopyToOutputDirectory="PreserveNewest" />
    <None Include="..\..\docs\guide.md" Link="docs\guide.md" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <ItemGroup>
```

The test project file starts with a UTF-8 byte order mark and has no line break after `</Project>`. Keep both as they are.

### Acceptance criteria

- The files match this section and "Task 3 tests" exactly.
- `dotnet build CopilotLedger.slnx` reports 0 warnings, and `dotnet test CopilotLedger.slnx` passes 671 tests: the 651 from before plus GS1, GS2's 14 cases, and GS3–GS7. 0 failed, 0 skipped.
- Five runs of the guide tests in a row each pass all 20.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
dotnet test CopilotLedger.slnx --filter "FullyQualifiedName~GuideSamplesTests"
```

Run the last command five times. Each run passes 20 tests.
## Task 4 specification: Documentation Sweep

### Files

| File | Change |
|---|---|
| `README.md` | Replace, copy exactly |
| `docs/cli.md` | Replace, copy exactly |
| `docs/configuration.md` | Three edits, copy exactly |
| `docs/releasing.md` | Replace, copy exactly |

`docs/action.md` stays until Task 5 deletes it. After this task, no document links to it.

What changes, for review:

- **`README.md`** is the front door: what CopilotLedger does, its features and requirements, a quick start (`gh extension install`, `gh ledger install`, `status`, `branch`), and links. The workflow YAML and the upgrade steps move to the guide, the action's README, and `docs/cli.md`; building from source points to `docs/cli.md` and adds the build and test commands.
- **`docs/cli.md`** is the reference. It states the naming rule once; gives the extension's binary path on each OS, and for a binary built from source, moving it to a permanent folder and running `<folder>/gh-ledger install`; merges install, uninstall, and upgrading into one section (the shell profile `install` edits, when it sets the alias, every warning, the `install.json` mismatch, removing the MCP registration after `uninstall`, and why the daemon and Copilot's MCP servers must stop before `gh extension upgrade` or `remove`); covers the daemon's options, its null standard handles, and `daemon stop` ending the process after 15 seconds; lists the reports in a table that says which ones default to the current repository; covers the breaker, budgets, and receipts (`uses: swigerb/copilot-ledger-action@v1`); registers the MCP server with the extension's binary path (A3); and adds the `could not record spans` warning to troubleshooting.
- **`docs/configuration.md`** names commands `gh ledger`, adds `daemon.stop` and `alert-toast.xml` to the files table (A9), says that `daemon.log` is also written when the daemon starts at sign-in, and says to restart the daemon after changing the overlay.
- **`docs/releasing.md`** adds the guide's samples to the places that hold the version, adds a `ratesVersion` step that lists every sample and test showing the old date (A4), says the dry run checks `ratesVersion`, and explains how to update pinned actions (A7).

### `README.md` (copy exactly)

````markdown
# CopilotLedger

CopilotLedger shows what GitHub Copilot costs you. Through Copilot's OpenTelemetry export, it records the GitHub AI credits that Copilot CLI, the GitHub Copilot app, and VS Code spend, and attributes them to repository, branch, model, agent, and prompt. It also tracks Microsoft Foundry BYOK spend and what local models save you.

It runs on your machine as the `gh ledger` GitHub CLI extension, with no Docker or cloud service, and it never stores prompt or response content.

## Features

- Reports by branch, agent, and prompt, as tables or JSON
- A circuit breaker that alerts you when one prompt spends too fast or an agent keeps failing
- Soft AI-credit budgets for branches
- Pull request receipts, posted by the [CopilotLedger PR Receipt action](https://github.com/swigerb/copilot-ledger-action)
- An MCP server, so Copilot can check spend, budgets, and the circuit breaker
- Foundry BYOK prices, local-model savings, and `rates update` for new prices between releases

## Requirements

- The GitHub CLI (`gh`), or the .NET 10 SDK to build from source
- Windows, macOS, or Linux, on x64 or Arm64
- Copilot CLI, the GitHub Copilot app, or VS Code with Copilot
- Git repositories, for per-branch attribution. CopilotLedger reads `.git` directly and never runs `git`.

## Quick start

```shell
gh extension install swigerb/gh-ledger
gh ledger install
```

`install` turns on Copilot's telemetry export, starts the daemon, and registers it to start when you sign in. Restart the GitHub Copilot app, VS Code, and open terminals, send Copilot a prompt, and check what it recorded:

```shell
gh ledger status
gh ledger branch
```

The [user guide](docs/guide.md) shows the everyday tasks with sample output: reports, budgets, the circuit breaker, pull request receipts, Foundry BYOK and local models, MCP, updates, and uninstalling.

## Documentation

- [User guide](docs/guide.md): scenarios with sample output
- [Command reference](docs/cli.md): every command and option, and troubleshooting
- [Configuration](docs/configuration.md): pricing, the rates overlay, and data files
- [CopilotLedger PR Receipt action](https://github.com/swigerb/copilot-ledger-action): posting receipts on pull requests
- [Releasing](docs/releasing.md): for maintainers

## Build from source

To publish your own binary instead of the extension, see [Get gh-ledger](docs/cli.md#get-gh-ledger). To build and run the tests:

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

## License

MIT. See [LICENSE](LICENSE).
````

### `docs/cli.md` (copy exactly)

````markdown
# gh-ledger command reference

gh-ledger runs a local OpenTelemetry receiver at `http://localhost:4318`, records GitHub Copilot's usage in a SQLite ledger on your machine, and reports AI spend by repository, branch, model, agent, and prompt. It needs no Docker and no cloud service. For walkthroughs with sample output, see the [user guide](guide.md).

**Command names.** This page writes commands as `gh ledger <command>`, which runs gh-ledger as a GitHub CLI extension. If you built gh-ledger yourself and have no `gh ledger` alias, run the binary by its path instead, as `<folder>/gh-ledger <command>`. `gh-ledger` is the program's own name, so `--help` and gh-ledger's messages say `gh-ledger`; with the extension, run the commands they show as `gh ledger`.

## Get gh-ledger

Install the GitHub CLI extension:

```shell
gh extension install swigerb/gh-ledger
```

gh downloads the release binary for your platform to:

- Windows: `%LOCALAPPDATA%\GitHub CLI\extensions\gh-ledger\gh-ledger.exe`
- macOS and Linux: `~/.local/share/gh/extensions/gh-ledger/gh-ledger`

When `XDG_DATA_HOME` is set, gh uses `$XDG_DATA_HOME/gh/extensions/gh-ledger/` instead. The path stays the same when you upgrade, so the sign-in item and an MCP registration keep working.

To build from source instead, publish one single-file, self-contained binary for your runtime identifier:

```shell
dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r <rid> -o artifacts/gh-ledger
```

Use `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64`. Move `gh-ledger` (`gh-ledger.exe` on Windows) from `artifacts/gh-ledger` to a permanent folder, then run `<folder>/gh-ledger install`. `install` points the sign-in item and the `gh ledger` alias at that copy; it doesn't add the folder to `PATH`.

## Install and uninstall

`gh ledger install` makes these changes and records them in `install.json`:

- It sets `COPILOT_OTEL_ENABLED=true`: as a user environment variable on Windows, or in a marked block in your shell profile elsewhere (`.zshrc` for zsh, `.bash_profile` for bash on macOS, `.bashrc` for bash on Linux, and `.profile` otherwise).
- For a binary you built, it sets the `gh ledger` alias when `gh` is on `PATH`. The extension doesn't need one. On Windows, the alias needs `sh.exe` from Git for Windows.
- It registers the daemon to start when you sign in: the `CopilotLedger` value under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` on Windows, `~/Library/LaunchAgents/io.github.swigerb.copilot-ledger.plist` on macOS, or `copilot-ledger.desktop` in `$XDG_CONFIG_HOME/autostart` (default `~/.config/autostart`) on Linux.
- It starts the daemon.

It changes nothing else; in particular, it doesn't edit `PATH` or other OpenTelemetry variables. Use `--dry-run` to print the changes without making them. Afterward, restart the GitHub Copilot app, VS Code, and open terminals so they see `COPILOT_OTEL_ENABLED`.

`install` and `status` warn about settings that keep Copilot's telemetry from reaching CopilotLedger:

- `OTEL_EXPORTER_OTLP_ENDPOINT` set to any endpoint other than `http://localhost:4318`.
- `COPILOT_OTEL_FILE_EXPORTER_PATH`, which makes Copilot write telemetry to a file instead.
- `COPILOT_OTEL_EXPORTER_TYPE` set to anything other than `otlp-http`.
- Any of `OTEL_EXPORTER_OTLP_CERTIFICATE`, `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE`, `OTEL_EXPORTER_OTLP_CLIENT_KEY`, `OTEL_EXPORTER_OTLP_TRACES_CERTIFICATE`, `OTEL_EXPORTER_OTLP_TRACES_CLIENT_CERTIFICATE`, or `OTEL_EXPORTER_OTLP_TRACES_CLIENT_KEY`, which turn off export to `http://` endpoints.
- `OTEL_EXPORTER_OTLP_PROTOCOL` or `OTEL_EXPORTER_OTLP_TRACES_PROTOCOL` set to `grpc`; CopilotLedger accepts only `http/protobuf` and `http/json`.

`install` also warns when `COPILOT_LEDGER_HOME` is set, because the daemon started at sign-in uses it only if your sign-in session sets it too. `status` warns when `install.json` names a different executable, for example after you switch from a binary you built to the extension; run `install` again from the copy you want to keep.

`gh ledger uninstall` stops the daemon, removes the sign-in item, and reverses the environment variable, profile block, and alias changes recorded in `install.json`, restoring the variable's previous value. `--dry-run` prints the changes without making them. Your ledger stays, and so does an MCP registration: remove it with `copilot mcp remove ledger`, and remove the `ledger` entry from VS Code's `mcp.json` if you added one. To remove the extension too, run `gh extension remove ledger` afterward.

**Upgrading or removing the extension.** The daemon runs the extension's binary, and so does each Copilot session that uses the `ledger` MCP server: Copilot CLI, the GitHub Copilot app, and VS Code start `gh-ledger mcp` and keep it running until the session ends. On Windows, gh can't replace or delete the binary while any of them runs it, so `gh extension upgrade ledger` and `gh extension remove ledger` fail. Before either, stop the daemon and close the Copilot sessions and apps that use the MCP server. To upgrade:

```shell
gh ledger daemon stop
gh extension upgrade ledger
gh ledger daemon start
```

## The daemon

- `gh ledger daemon run [--port <port>] [--log-file <path>]` runs the receiver in the foreground. It logs to standard output, or appends to `--log-file`.
- `gh ledger daemon start [--port <port>]` starts it as a detached process that logs to `daemon.log`. The daemon gets the null device as its standard input, output, and error, so a script can capture the command's output, for example `started=$(gh ledger daemon start)`.
- `gh ledger daemon stop` asks the daemon to stop, waits up to 15 seconds, and then ends its process if it's still running.

The port defaults to 4318. Only one daemon runs at a time. Copilot's telemetry is lost while no daemon is running.

## Reports

| Command | Reports |
|---|---|
| `gh ledger status [--json]` | The daemon, ledger, rates, Copilot's export setting, warnings, and totals for today and the last 7 and 30 days |
| `gh ledger branch [--repo <key> \| --all-repos] [--since <period>] [--json]` | Spend by branch in the current repository, or in another or every repository; outside a Git repository, every repository |
| `gh ledger agents [--repo <key>] [--since <period>] [--json]` | Spend by agent in every repository, or in one with `--repo` |
| `gh ledger top-prompts [--repo <key>] [--since <period>] [--limit <n>] [--json]` | The most expensive prompt chains in every repository, or in one with `--repo`; `--limit` is 1 to 100 and defaults to 10 |

- A repository key is an `owner/repo` slug, a local repository root for a repository whose `origin` has no `owner/repo` path, or `(none)` for spend outside any repository.
- `--since` takes `today`, `all`, `1d` through `3650d`, or a `yyyy-MM-dd` date; the default is `30d`.
- Tables round money to cents; JSON keeps exact amounts.
- Spend is split into GitHub AI credits (`AI credits` and `GitHub USD`), Foundry BYOK (`BYOK USD`), and local models, whose `Local saved` is the cloud spend they avoided. `Unpriced` counts requests without a price; see [configuration.md](configuration.md).
- An agent shows as `(none)` when Copilot reports no agent name, as its default agent does. Built-in subagents such as `explore` report their own names, and a custom agent reports the `name` in its `.agent.md` file, or the file's name without `.agent.md`.

## Circuit breaker

The daemon checks every prompt against two rules and trips the breaker when one is broken:

- **Credit velocity:** one prompt's billed spend, GitHub AI credits plus Foundry BYOK at $0.01 per credit, exceeds 150 AI credits within 5 minutes.
- **Retry churn:** one prompt makes 5 failed tool calls in a row, with no successful command and no new commit between them. A failed call is a shell command with a non-zero exit code or a tool call that Copilot reports as an error. Copilot doesn't export the commands themselves, so any failing command counts. VS Code's Local agent harness may not report exit codes, so this rule may not trip there.

A trip writes an `ALERT` line to `daemon.log` and shows a desktop notification: a toast on Windows, a notification on macOS, and `notify-send` on Linux when it's installed. It doesn't stop the agent. Each rule trips at most once per prompt, and a trip stays open until you reset it.

- `gh ledger breaker status [--json]` shows the rules and the open trips.
- `gh ledger breaker reset` closes the current branch's open trips. Use `--id <id>` for one trip, `--repo <key> --branch <name>` for another branch, or `--all`.
- `gh ledger breaker config` shows the rules and changes them with `--velocity-credits <credits>` (1 to 1,000,000, with up to 2 decimal places), `--velocity-minutes <minutes>` (1 to 1,440), and `--churn-failures <count>` (2 to 100). `--notifications off` keeps the `ALERT` line but stops desktop notifications.

With `--flag-file on`, the daemon also writes `.copilot-ledger/CIRCUIT_OPEN` in the repository root while a trip is open there, so a wrapper script can stop an agent. The file holds the open trips as JSON, a `.copilot-ledger/.gitignore` keeps it out of commits, and `breaker reset` removes it. Trips outside a local repository get no file. If the daemon logs that it couldn't update the file, run `gh ledger breaker config --flag-file on` again.

## Budgets

A budget is a soft cap in AI credits for one branch. When the branch's billed spend exceeds it, the daemon alerts once, the same way as a breaker trip; nothing is blocked. Foundry BYOK spend counts at $0.01 per credit.

- `gh ledger budget set <credits> [--repo <key> --branch <name>]` sets the current branch's budget, or another branch's. Credits are more than 0 and at most 1,000,000, with up to 2 decimal places. Setting a budget again re-arms its alert.
- `gh ledger budget clear [--repo <key> --branch <name>]` removes it.
- `gh ledger budget list [--json]` lists every budget with its spend.

## Pull request receipts

`gh ledger export-pr` writes the current branch's AI spend receipt to `.github/copilot-ledger/receipts/<branch>-<hash>.md` in the repository, where `<branch>` is the branch name with characters other than letters, digits, `.`, `_`, and `-` replaced by `-`. Commit the file on the branch and push it; the [CopilotLedger PR Receipt action](https://github.com/swigerb/copilot-ledger-action), `uses: swigerb/copilot-ledger-action@v1`, posts it on the pull request.

The receipt covers all spend recorded on the branch: GitHub AI credits by model, Foundry BYOK spend, local-model tokens and the cloud spend they avoided, unpriced requests, circuit-breaker trips, and the most expensive prompt, with the data as JSON. A turn is one model request. Its totals equal the branch's row in `gh ledger branch --since all --json`. Branch and model names appear in code spans, so they never become mentions or links.

- `--stdout` writes the Markdown to standard output instead of the file.
- `--json` writes the JSON data to standard output instead of the file.

`export-pr` needs a branch with at least one commit and a GitHub remote named `origin`, and it refuses a receipt over 65,536 bytes, the most the action posts.

## MCP server

`gh ledger mcp` runs an MCP server on standard input and output, so Copilot can read the ledger and see the circuit breaker. Register it with Copilot CLI using the gh-ledger binary's path, which for the extension is shown under [Get gh-ledger](#get-gh-ledger). In PowerShell on Windows:

```powershell
copilot mcp add ledger -- "$env:LOCALAPPDATA\GitHub CLI\extensions\gh-ledger\gh-ledger.exe" mcp
```

On macOS and Linux:

```shell
copilot mcp add ledger -- ~/.local/share/gh/extensions/gh-ledger/gh-ledger mcp
```

That adds `ledger` to `~/.copilot/mcp-config.json`, which VS Code's Copilot agent harness also reads. The GitHub Copilot app runs the same agent runtime; if it doesn't list the server, add it in the app's MCP settings with the same command. For VS Code's Local agent harness, add the server to `mcp.json` under `servers`, with the binary's path as `"command"` and `["mcp"]` as `"args"`.

The server has four tools. Each uses the current repository and branch unless you pass `repository` and `branch`:

- `get_branch_ledger`: the branch's spend, top model, budget, and circuit-breaker state.
- `get_top_expensive_prompts`: the branch's most expensive prompt chains, with output tokens and failed tool calls; `limit` (1 to 20, default 5) and `orderBy` (`cost`, `outputTokens`, or `failedToolCalls`).
- `get_agent_breakdown`: the branch's spend by agent.
- `set_branch_budget`: sets the branch's budget in AI credits (`aiCredits`).

The first three are read-only. `set_branch_budget` changes only the budget, so Copilot asks before running it; in prompt mode, allow it with `--allow-tool 'ledger(set_branch_budget)'`. While the breaker is open for the branch or the session, or the branch is over budget, every result starts with a `WARNING:` line. No tool can reset the breaker.

## Rates

- `gh ledger rates init [--force]` creates a starter rates overlay, `rates.json` in the data directory.
- `gh ledger rates validate [--file <path>]` checks the overlay, or another file, with the same rules the daemon uses.
- `gh ledger rates update` downloads `rates.json` from the latest gh-ledger release and keeps it when it's newer than the rates in use and your overlay still applies. It uses your system proxy. Restart the daemon afterward; it reads rates when it starts.

See [configuration.md](configuration.md) for pricing, the overlay format, and where files are kept.

## Privacy

CopilotLedger stores no prompt or response content, working directories, or remote URLs. Repositories are stored as `owner/repo` slugs or local roots, and credentials are stripped.

## Troubleshooting

- **Nothing is recorded.** Run `gh ledger status`, address its warnings, and restart Copilot.
- **`daemon.log` says `Warning: could not record spans`.** The ledger couldn't be written, for example because the disk is full or the database is damaged, and Copilot is told to retry. The warning repeats at most once a minute; fix the cause shown after it.
- **Port 4318 is in use.** Stop the other program or the old daemon, then run `gh ledger daemon start`.
- **`gh extension upgrade` or `remove` fails on Windows.** A program is running the extension's binary: the daemon, or the MCP server of a Copilot session. Run `gh ledger daemon stop`, close the Copilot CLI sessions, the GitHub Copilot app, and VS Code, then try again.
- **Apps started from the desktop record nothing.** On macOS and Linux, apps started outside a terminal may not read your shell profile, so they don't see `COPILOT_OTEL_ENABLED`. Start them from a terminal.
- **Move the data directory.** Set `COPILOT_LEDGER_HOME` before running gh-ledger. The daemon started at sign-in uses it only if your sign-in session sets it too, for example as a Windows user environment variable.
- **A console window flashes at sign-in.** Windows 11 24H2 and later start gh-ledger without a window; earlier versions show it for about a second.
````

### `docs/configuration.md` (copy exactly)

Edit 1 of 3 in `docs/configuration.md`, replace:

```markdown
| `~/.copilot-ledger/` | Data directory (`%USERPROFILE%\.copilot-ledger\` on Windows) |
| `~/.copilot-ledger/ledger.db` | The local SQLite ledger (WAL mode) |
| `~/.copilot-ledger/rates.json` | Optional rates overlay, which you create |
| `~/.copilot-ledger/rates.downloaded.json` | Newer built-in rates saved by `gh-ledger rates update` |
| `~/.copilot-ledger/daemon.json` | Written while the daemon runs: process ID, endpoint, start time, and database path |
| `~/.copilot-ledger/daemon.lock` | Held while the daemon runs, so only one daemon runs at a time |
| `~/.copilot-ledger/daemon.log` | The daemon's log when it's started with `gh-ledger daemon start` |
| `~/.copilot-ledger/install.json` | What `gh-ledger install` changed, so `gh-ledger uninstall` can undo it |

Set the `COPILOT_LEDGER_HOME` environment variable to keep these files in another directory.
```

with:

```markdown
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
```

Edit 2 of 3 in `docs/configuration.md`, replace:

```markdown
Until you add prices, BYOK requests are still counted and attributed to repository, branch, model, and agent, with status `byok_unpriced` and $0.00 cost.

## Newer rates between releases: `gh-ledger rates update`

Each gh-ledger release attaches the `rates.json` it was built with. `gh-ledger rates update` downloads the latest release's copy from `https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json`, through your system proxy, and checks it with the same rules as the built-in rates.

Rates versions are dates. When the download is newer than the rates in use, `rates update` saves it as `rates.downloaded.json`; when it's the same or older, nothing changes. If your overlay doesn't merge with the newer rates, nothing is saved, and the error names the problem in your overlay.

CopilotLedger's base rates are `rates.downloaded.json` when that file is valid and newer than the built-in rates, and the built-in rates otherwise. So after you upgrade gh-ledger to newer built-in rates, an older download is ignored. A download that can't be read is ignored with a warning in `gh-ledger status`, `gh-ledger rates validate`, and the daemon log. Your overlay applies on top of either. `gh-ledger status` shows `(downloaded)` after the rates version when the download is in use.

The daemon reads rates when it starts, so restart it after an update. Spans already recorded keep the prices they were recorded with.
```

with:

```markdown
Until you add prices, BYOK requests are still counted and attributed to repository, branch, model, and agent, with status `byok_unpriced` and $0.00 cost.

## Newer rates between releases: `gh ledger rates update`

Each gh-ledger release attaches the `rates.json` it was built with. `gh ledger rates update` downloads the latest release's copy from `https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json`, through your system proxy, and checks it with the same rules as the built-in rates.

Rates versions are dates. When the download is newer than the rates in use, `rates update` saves it as `rates.downloaded.json`; when it's the same or older, nothing changes. If your overlay doesn't merge with the newer rates, nothing is saved, and the error names the problem in your overlay.

CopilotLedger's base rates are `rates.downloaded.json` when that file is valid and newer than the built-in rates, and the built-in rates otherwise. So after you upgrade gh-ledger to newer built-in rates, an older download is ignored. A download that can't be read is ignored with a warning in `gh ledger status`, `gh ledger rates validate`, and the daemon log. Your overlay applies on top of either. `gh ledger status` shows `(downloaded)` after the rates version when the download is in use.

The daemon reads rates when it starts, so restart it after an update. Spans already recorded keep the prices they were recorded with.
```

Edit 3 of 3 in `docs/configuration.md`, replace:

```markdown
To change pricing, create `~/.copilot-ledger/rates.json`. CopilotLedger merges it over the built-in rates when it loads them, so you only write what you want to change.

Run `gh-ledger rates init` to create a starter file, and `gh-ledger rates validate` to check it against the same rules the daemon uses.

- Every section is optional except `"schemaVersion": 1`.
- A model with a new `id` is added. A model with the same `id` as a built-in one replaces it.
```

with:

```markdown
To change pricing, create `~/.copilot-ledger/rates.json`. CopilotLedger merges it over the built-in rates when it loads them, so you only write what you want to change.

Run `gh ledger rates init` to create a starter file, and `gh ledger rates validate` to check it against the same rules the daemon uses. The daemon reads the file when it starts, so restart it after a change: `gh ledger daemon stop`, then `gh ledger daemon start`.

- Every section is optional except `"schemaVersion": 1`.
- A model with a new `id` is added. A model with the same `id` as a built-in one replaces it.
```

The three `rates-error-example` blocks don't change; `RateOverlayTests.DocsExamples_MatchRealMessages` reads them.

### `docs/releasing.md` (copy exactly)

````markdown
# Releasing gh-ledger

Releases are GitHub Releases of this repository. They make `gh extension install swigerb/gh-ledger` work, and `gh ledger rates update` downloads their `rates.json`. The CopilotLedger PR Receipt action is released from [its own repository](https://github.com/swigerb/copilot-ledger-action).

## Cut a release

1. Change the version everywhere it's written, and merge that change:
   - `<Version>` in `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`;
   - in `src/CopilotLedger.Cli/GhLedgerCli.cs`, the `AssemblyInformationalVersionAttribute` comment and the fallback after `??`;
   - the version that the smoke test in `.github/workflows/ci.yml` compares with;
   - the tests that expect it: `DaemonCommandTests.DM1` and `ReportCommandTests.StatusText`;
   - the samples in `docs/guide.md` that show it: both `status` samples and the receipt. `GuideSamplesTests` fails until they match.
2. If `src/CopilotLedger.Core/Pricing/rates.json` changed since the latest release, set its `ratesVersion` to the date of the change (`yyyy-MM-dd`), later than the latest release's. `gh ledger rates update` only takes rates with a later version, and the Release workflow fails when `rates.json` changed but its `ratesVersion` didn't move forward. In the same change, replace the old date wherever it's shown:
   - the samples in `docs/guide.md` that show it: `status-first`, `status`, `rates-validate`, and `rates-update`. The `rates-update` sample and `GuideSamplesTests.GS4` download version `2026-12-15`; if that isn't later than the new date, change it in both;
   - the tests that expect it: in `ReportCommandTests` (including `StatusText`), `RatesCommandTests`, `RatesUpdateTests`, `RateOverlayTests`, and `JsonRateCardCalculatorTests`, and in `TestSupport/PublishedRates.cs`. `RatesUpdateTests` downloads versions before, equal to, and after the old date; keep each in the same place relative to the new one.

   `GuideSamplesTests` and the other tests fail until they match.
3. Optional: run the Release workflow on `main` from the Actions tab. A manual run builds all six binaries, smoke-tests the three that GitHub's runners can run (`win-x64`, `linux-x64`, and `osx-arm64`), checks `ratesVersion`, and uploads the six binaries as workflow artifacts. It never creates a release, even when it's started from a tag.
4. Tag the merged commit and push the tag:

   ```shell
   git tag v0.4.1
   git push origin v0.4.1
   ```

When a `v*` tag is pushed, the Release workflow checks that the tag equals `v` plus the project version, builds a single-file binary for each platform, smoke-tests the same three, checks `ratesVersion` against the latest release's `rates.json`, and creates the release with these assets:

| Asset | Platform |
|---|---|
| `gh-ledger-windows-amd64.exe` | Windows x64 |
| `gh-ledger-windows-arm64.exe` | Windows Arm64 |
| `gh-ledger-linux-amd64` | Linux x64 |
| `gh-ledger-linux-arm64` | Linux Arm64 |
| `gh-ledger-darwin-amd64` | macOS x64 |
| `gh-ledger-darwin-arm64` | macOS Apple silicon |
| `rates.json` | The built-in rates, for `gh ledger rates update` |
| `gh-ledger-checksums.txt` | SHA-256 checksums of the files above |

GitHub CLI picks a release asset by the `<os>-<arch>` suffix of its name, so keep these names. A tag with a hyphen, such as `v0.5.0-rc.1`, makes a prerelease, which `gh extension install` and `rates update` ignore because they use the latest release.

## Update the pinned actions

Every workflow pins each action to a full commit SHA, with its version in a comment, so a moved tag can't change what runs:

```yaml
- uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
```

Nothing updates the pins automatically. To update one, look up the action's latest release and the commit its tag points to:

```shell
gh release view --repo actions/checkout --json tagName --jq .tagName
gh api repos/actions/checkout/commits/v7.0.1 --jq .sha
```

Read the release notes for breaking changes, then replace the SHA and the comment in every workflow that uses the action, in this repository and in swigerb/copilot-ledger-action.
````

### Acceptance criteria

- The four files match this section exactly.
- Every relative link in `README.md` and `docs/*.md` names a file that exists, and every `#anchor` names a heading in it. GitHub makes a heading's anchor by lowercasing it, dropping punctuation other than `-` and `_`, and turning spaces into `-`.
- `dotnet build CopilotLedger.slnx` reports 0 warnings, and `dotnet test CopilotLedger.slnx` passes 671 tests (0 failed, 0 skipped).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```
## Task 5 specification: Use the Action Repository

**Prerequisite.** Task R's pull request has merged in swigerb/copilot-ledger-action. The orchestrator gives the builder the merge commit's full 40-character SHA, the commit it tags `v1.0.0`. Don't start this task without that SHA.

### Files

| File | Change |
|---|---|
| `action/action.yml`, `action/receipt.ps1`, `action/tests/receipt.Tests.ps1` | Delete |
| `docs/action.md` | Delete |
| `.github/workflows/ci.yml` | Remove the step that runs the action's tests |
| `.github/workflows/receipt.yml` | Replace, copy exactly, with the SHA |
| `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs` | One comment |
| `docs/releasing.md` | One paragraph added |

### `.github/workflows/ci.yml` (copy exactly)

In `.github/workflows/ci.yml`, replace:

```yaml
          dotnet-version: 10.0.x
      - run: dotnet build CopilotLedger.slnx
      - run: dotnet test CopilotLedger.slnx
      - name: Test the receipt action
        shell: pwsh
        run: pwsh -NoProfile -File action/tests/receipt.Tests.ps1
      - name: Publish single-file gh-ledger
        shell: bash
        env:
```

with:

```yaml
          dotnet-version: 10.0.x
      - run: dotnet build CopilotLedger.slnx
      - run: dotnet test CopilotLedger.slnx
      - name: Publish single-file gh-ledger
        shell: bash
        env:
```

The action's own CI runs its tests now.

### `.github/workflows/receipt.yml` (copy exactly)

Replace `<commit SHA>` with the SHA the orchestrator gave, and change nothing else:

```yaml
name: Receipt

on:
  pull_request:
    types: [opened, synchronize, reopened]

permissions:
  contents: read
  pull-requests: write

concurrency:
  group: receipt-${{ github.event.pull_request.number }}
  cancel-in-progress: true

jobs:
  receipt:
    runs-on: ubuntu-latest
    steps:
      - uses: swigerb/copilot-ledger-action@<commit SHA> # v1.0.0
```

The checkout step goes: the action reads the receipt through the REST API, so only the local `./action` needed a checkout.

### `ReceiptMarkdown` (copy exactly)

In `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs`, replace:

```csharp
{
    public const string Marker = "<!-- copilot-ledger-receipt -->";
    public const string ReceiptsDirectory = ".github/copilot-ledger/receipts";
    public const int MaximumBytes = 65536;   // as in action/receipt.ps1; keeps a receipt within GitHub's 65,536-character comment limit

    internal static string ToJson(PullRequestReceipt receipt)
    {
```

with:

```csharp
{
    public const string Marker = "<!-- copilot-ledger-receipt -->";
    public const string ReceiptsDirectory = ".github/copilot-ledger/receipts";
    public const int MaximumBytes = 65536;   // as in receipt.ps1 in swigerb/copilot-ledger-action; keeps a receipt within GitHub's 65,536-character comment limit

    internal static string ToJson(PullRequestReceipt receipt)
    {
```

### `docs/releasing.md` (copy exactly)

In `docs/releasing.md`, replace:

````markdown
```

Read the release notes for breaking changes, then replace the SHA and the comment in every workflow that uses the action, in this repository and in swigerb/copilot-ledger-action.
````

with:

````markdown
```

Read the release notes for breaking changes, then replace the SHA and the comment in every workflow that uses the action, in this repository and in swigerb/copilot-ledger-action.

`receipt.yml` pins swigerb/copilot-ledger-action to the commit of one of its releases in the same way. After each release of the action, look up the commit its tag points to, as above, and update the pin.
````

### Acceptance criteria

- `action/` and `docs/action.md` no longer exist.
- Outside `.github/plans/`, no file mentions `action/receipt.ps1`, `action/tests`, `docs/action.md`, `uses: ./action`, or `gh-ledger/action`.
- `receipt.yml` matches the block above with the orchestrator's SHA, and no `ci.yml` step runs `receipt.Tests.ps1`.
- `dotnet build CopilotLedger.slnx` reports 0 warnings, and `dotnet test CopilotLedger.slnx` passes 671 tests (0 failed, 0 skipped).
- On the pull request, the Receipt workflow downloads `swigerb/copilot-ledger-action` at that SHA and finishes. A pull request without a receipt gets a notice, not a failure.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```
## Task R specification: The Action Repository

**Where.** This task runs in swigerb/copilot-ledger-action, on a branch from `main` (`c18a67b`), with a pull request to that repository's `main`. It changes nothing in gh-ledger. Its builder reads this plan from gh-ledger's `main`. There's no .NET in this repository, so the `dotnet` commands in the builder's instructions don't apply; the verification below replaces them.

### Files

| File | Change |
|---|---|
| `action.yml` | New, copy exactly |
| `receipt.ps1` | New, copy exactly |
| `tests/receipt.Tests.ps1` | New, copy exactly (see "Task R tests") |
| `README.md` | Replace GitHub's stub, copy exactly |
| `.github/workflows/ci.yml` | New, copy exactly |
| `LICENSE` | Unchanged |

How they relate to gh-ledger's `action/` at `3a0a514`, for review:

- `action.yml` has the same inputs, outputs, and step. Its `description` is new and shorter, and `author` and `branding` are added for Marketplace.
- `receipt.ps1` is identical except line 3, which points to `README.md` instead of `docs/action.md`.
- `tests/receipt.Tests.ps1` is identical except lines 2 and 3, which drop the `action/` prefix. Its `$ScriptPath = Join-Path $PSScriptRoot '..' 'receipt.ps1'` already finds `receipt.ps1` at the repository root.
- `README.md` replaces gh-ledger's `docs/action.md`: how it works, usage (`uses: swigerb/copilot-ledger-action@v1`), inputs, outputs, the checks, versions, development, and releasing.

### `action.yml` (copy exactly)

```yaml
name: CopilotLedger PR Receipt
description: Posts the AI spend receipt from gh ledger export-pr as one pull request comment, updated on later pushes.
author: swigerb
branding:
  icon: dollar-sign
  color: green

inputs:
  github-token:
    description: Token that reads the pull request and writes the comment. Needs contents read and pull-requests write.
    default: ${{ github.token }}
  receipts-directory:
    description: Repository directory where receipts are committed. Change it only if you move them.
    default: .github/copilot-ledger/receipts
  comment-author:
    description: Login that owns the receipt comment. Change it only when github-token belongs to another account.
    default: 'github-actions[bot]'

outputs:
  result:
    description: created, updated, unchanged, no-receipt, or skipped.
    value: ${{ steps.receipt.outputs.result }}
  comment-url:
    description: URL of the receipt comment; empty for no-receipt and skipped.
    value: ${{ steps.receipt.outputs.comment-url }}

runs:
  using: composite
  steps:
    - id: receipt
      shell: pwsh
      env:
        RECEIPT_TOKEN: ${{ inputs.github-token }}
        RECEIPTS_DIRECTORY: ${{ inputs.receipts-directory }}
        COMMENT_AUTHOR: ${{ inputs.comment-author }}
        EVENT_NAME: ${{ github.event_name }}
        PR_NUMBER: ${{ github.event.pull_request.number }}
        HEAD_REF: ${{ github.event.pull_request.head.ref }}
        HEAD_SHA: ${{ github.event.pull_request.head.sha }}
        HEAD_REPOSITORY: ${{ github.event.pull_request.head.repo.full_name }}
      run: '& (Join-Path $env:GITHUB_ACTION_PATH receipt.ps1)'
```

### `receipt.ps1` (copy exactly)

````powershell
#Requires -Version 7.4
# Posts the CopilotLedger receipt that a pull request commits as one comment, and updates that comment on later runs.
# action.yml passes the inputs and pull request fields as environment variables. See README.md.
# Strings are compared with [string]::Equals and an ordinal StringComparison, never -eq or -ceq: PowerShell compares
# strings with culture rules, which ignore zero-width characters and a byte order mark.

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$Marker = '<!-- copilot-ledger-receipt -->'
$MaximumBytes = 65536
$PageSize = 100
$MaximumPages = 30

function ConvertTo-CommandValue([string] $Value) {
    return $Value.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
}

function Write-Notice([string] $Message) {
    Write-Host "::notice::$(ConvertTo-CommandValue $Message)"
}

function Stop-WithError([string] $Message) {
    Write-Host "::error::$(ConvertTo-CommandValue $Message)"
    exit 1
}

function Set-ActionOutput([string] $Name, [string] $Value) {
    [System.IO.File]::AppendAllText(
        $env:GITHUB_OUTPUT,
        "$Name=$Value`n",
        [System.Text.UTF8Encoding]::new($false))
}

function Invoke-GitHubApi([string] $Method, [string] $Path, [string] $Body = '') {
    $parameters = @{
        Uri = "$($env:GITHUB_API_URL.TrimEnd('/'))/$Path"
        Method = $Method
        Headers = @{
            Authorization = "Bearer $($env:RECEIPT_TOKEN)"
            Accept = 'application/vnd.github+json'
            'X-GitHub-Api-Version' = '2026-03-10'
        }
    }
    if ($Body.Length -gt 0) {
        $parameters.Body = [System.Text.Encoding]::UTF8.GetBytes($Body)
        $parameters.ContentType = 'application/json'
    }

    $response = Invoke-WebRequest @parameters
    return [System.Text.Json.JsonDocument]::Parse([string] $response.Content).RootElement.Clone()
}

function Get-StringProperty([System.Text.Json.JsonElement] $Element, [string] $Name) {
    $value = [System.Text.Json.JsonElement]::new()
    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object -and
        $Element.TryGetProperty($Name, [ref] $value) -and
        $value.ValueKind -eq [System.Text.Json.JsonValueKind]::String) {
        return $value.GetString()
    }

    return $null
}

function Get-Pages([string] $Path) {
    $items = [System.Collections.Generic.List[System.Text.Json.JsonElement]]::new()
    for ($page = 1; $page -le $MaximumPages; $page++) {
        $separator = if ($Path.Contains('?')) { '&' } else { '?' }
        $array = Invoke-GitHubApi 'GET' "$Path${separator}per_page=$PageSize&page=$page"
        $count = 0
        foreach ($item in $array.EnumerateArray()) {
            $items.Add($item)
            $count++
        }

        if ($count -lt $PageSize) {
            break
        }
    }

    return , $items
}

function Read-Receipt([string] $Repository, [string] $Path, [string] $Sha) {
    $encodedPath = ($Path.Split('/') | ForEach-Object { [System.Uri]::EscapeDataString($_) }) -join '/'
    $item = Invoke-GitHubApi 'GET' "repos/$Repository/contents/$($encodedPath)?ref=$Sha"
    $size = $item.GetProperty('size').GetInt64()
    if ($size -gt $MaximumBytes) {
        Stop-WithError "$Path is $size bytes; a receipt can be at most $MaximumBytes bytes."
    }

    $bytes = [System.Convert]::FromBase64String((Get-StringProperty $item 'content'))
    try {
        $text = [System.Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    }
    catch [System.Text.DecoderFallbackException] {
        Stop-WithError "$Path is not valid UTF-8."
    }

    [string[]] $lines = @($text.Split("`n") | ForEach-Object { $_.TrimEnd("`r") })
    if (-not [string]::Equals($lines[0], $Marker, [System.StringComparison]::Ordinal)) {
        Stop-WithError "$Path doesn't start with $Marker, so it isn't a CopilotLedger receipt."
    }

    $start = [System.Array]::IndexOf($lines, '```json')
    $end = if ($start -lt 0) { -1 } else { [System.Array]::IndexOf($lines, '```', $start + 1) }
    if ($start -lt 0 -or $end -le $start + 1) {
        Stop-WithError "$Path has no JSON data block."
    }

    try {
        $data = [System.Text.Json.JsonDocument]::Parse(($lines[($start + 1)..($end - 1)] -join "`n")).RootElement.Clone()
    }
    catch [System.Text.Json.JsonException] {
        Stop-WithError "$Path has invalid JSON data: $($_.Exception.Message)"
    }

    $version = [System.Text.Json.JsonElement]::new()
    if ($data.ValueKind -ne [System.Text.Json.JsonValueKind]::Object -or
        -not $data.TryGetProperty('schemaVersion', [ref] $version) -or
        $version.ValueKind -ne [System.Text.Json.JsonValueKind]::Number -or
        -not [string]::Equals($version.GetRawText(), '1', [System.StringComparison]::Ordinal)) {
        Stop-WithError "$Path has an unsupported schemaVersion; this action reads schemaVersion 1."
    }

    $branch = Get-StringProperty $data 'branch'
    if ($null -eq $branch) {
        Stop-WithError "$Path has no branch."
    }

    return [pscustomobject]@{ Path = $Path; Branch = $branch; Text = $text }
}

if (-not [string]::Equals($env:EVENT_NAME, 'pull_request', [System.StringComparison]::Ordinal)) {
    Stop-WithError "The CopilotLedger receipt action runs on pull_request events, not $($env:EVENT_NAME)."
}

$repository = $env:GITHUB_REPOSITORY
if (-not [string]::Equals($env:HEAD_REPOSITORY, $repository, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Notice 'Skipped the CopilotLedger receipt: workflows for pull requests from forks get a read-only token, so they can''t comment.'
    Set-ActionOutput 'result' 'skipped'
    Set-ActionOutput 'comment-url' ''
    exit 0
}

$directory = $env:RECEIPTS_DIRECTORY.Trim('/') + '/'
$candidates = [System.Collections.Generic.List[string]]::new()
foreach ($file in (Get-Pages "repos/$repository/pulls/$($env:PR_NUMBER)/files")) {
    $name = Get-StringProperty $file 'filename'
    if ($null -ne $name -and
        -not [string]::Equals((Get-StringProperty $file 'status'), 'removed', [System.StringComparison]::Ordinal) -and
        $name.StartsWith($directory, [System.StringComparison]::Ordinal) -and
        $name.EndsWith('.md', [System.StringComparison]::Ordinal)) {
        $candidates.Add($name)
    }
}

if ($candidates.Count -eq 0) {
    Write-Notice "No CopilotLedger receipt changed in this pull request. Run gh ledger export-pr on the branch, then commit the file it writes under $directory."
    Set-ActionOutput 'result' 'no-receipt'
    Set-ActionOutput 'comment-url' ''
    exit 0
}

$receipts = @($candidates | ForEach-Object { Read-Receipt $repository $_ $env:HEAD_SHA })
$matching = @($receipts | Where-Object { [string]::Equals($_.Branch, $env:HEAD_REF, [System.StringComparison]::Ordinal) })
if ($matching.Count -eq 0) {
    $found = ($receipts | ForEach-Object { $_.Branch }) -join ', '
    Stop-WithError "No receipt for $($env:HEAD_REF) changed in this pull request; found receipts for: $found."
}

if ($matching.Count -gt 1) {
    $paths = ($matching | ForEach-Object { $_.Path }) -join ', '
    Stop-WithError "More than one receipt for $($env:HEAD_REF) changed in this pull request: $paths."
}

$receipt = $matching[0]
$existing = $null
foreach ($comment in (Get-Pages "repos/$repository/issues/$($env:PR_NUMBER)/comments")) {
    $user = [System.Text.Json.JsonElement]::new()
    $login = if ($comment.TryGetProperty('user', [ref] $user)) { Get-StringProperty $user 'login' } else { $null }
    $body = Get-StringProperty $comment 'body'
    if ($null -ne $login -and
        [string]::Equals($login, $env:COMMENT_AUTHOR, [System.StringComparison]::OrdinalIgnoreCase) -and
        $null -ne $body -and
        $body.StartsWith($Marker, [System.StringComparison]::Ordinal)) {
        $existing = $comment
        break
    }
}

$payload = '{"body":' + (ConvertTo-Json -InputObject $receipt.Text -Compress) + '}'
if ($null -eq $existing) {
    $created = Invoke-GitHubApi 'POST' "repos/$repository/issues/$($env:PR_NUMBER)/comments" $payload
    $result = 'created'
    $url = Get-StringProperty $created 'html_url'
}
elseif ([string]::Equals(
        (Get-StringProperty $existing 'body').Replace("`r`n", "`n"),
        $receipt.Text.Replace("`r`n", "`n"),
        [System.StringComparison]::Ordinal)) {
    $result = 'unchanged'
    $url = Get-StringProperty $existing 'html_url'
}
else {
    $id = $existing.GetProperty('id').GetInt64()
    $updated = Invoke-GitHubApi 'PATCH' "repos/$repository/issues/comments/$id" $payload
    $result = 'updated'
    $url = Get-StringProperty $updated 'html_url'
}

Write-Host "CopilotLedger receipt ${result}: $url"
Set-ActionOutput 'result' $result
Set-ActionOutput 'comment-url' $url
exit 0
````

### `README.md` (copy exactly)

````markdown
# CopilotLedger PR Receipt

Posts a branch's AI spend receipt on its pull request as one comment, and updates that comment on later pushes. The receipt comes from [gh-ledger](https://github.com/swigerb/gh-ledger), which records GitHub Copilot usage on your own machine, so the action needs no cloud service and no secrets.

## How it works

1. On your machine, check out the pull request's branch and run `gh ledger export-pr`. It writes the branch's receipt to `.github/copilot-ledger/receipts/<branch>-<hash>.md`.
2. Commit that file and push.
3. On each push, the action finds the receipt among the pull request's changed files, checks it, and posts it as one comment. Later pushes update that comment instead of adding new ones.

The receipt shows GitHub AI Credits by model, Foundry BYOK spend, local-model tokens with the cloud spend they avoided, circuit-breaker trips, and the most expensive prompt, with its data as JSON. Its totals equal the branch's row in `gh ledger branch --since all --json`. Receipts stay in the repository after the pull request merges, as a record of what each branch spent.

## Usage

Add `.github/workflows/copilot-ledger-receipt.yml` to your repository:

```yaml
name: CopilotLedger receipt

on:
  pull_request:
    types: [opened, synchronize, reopened]

permissions:
  contents: read
  pull-requests: write

concurrency:
  group: copilot-ledger-receipt-${{ github.event.pull_request.number }}
  cancel-in-progress: true

jobs:
  receipt:
    runs-on: ubuntu-latest
    steps:
      - uses: swigerb/copilot-ledger-action@v1
```

The workflow's `GITHUB_TOKEN` reads the receipt with `contents: read` and comments with `pull-requests: write`, and needs nothing else. The `concurrency` group keeps two runs for the same pull request from both adding a comment.

## Inputs

| Input | Default | Meaning |
|---|---|---|
| `github-token` | `${{ github.token }}` | Token that reads the pull request and writes the comment |
| `receipts-directory` | `.github/copilot-ledger/receipts` | Where receipts are committed; change it only if you move them |
| `comment-author` | `github-actions[bot]` | The login that owns the receipt comment; change it only when `github-token` belongs to another account |

## Outputs

| Output | Meaning |
|---|---|
| `result` | `created`, `updated`, `unchanged`, `no-receipt`, or `skipped` |
| `comment-url` | The receipt comment's URL; empty for `no-receipt` and `skipped` |

## What the action checks

- It runs only for `pull_request` events. Pull requests from forks get a read-only token, so the action skips them with a notice. Other events, including `pull_request_target`, fail.
- A receipt must be among the pull request's changed files, under `receipts-directory`, and end in `.md`. With none, the action leaves a notice and succeeds.
- A receipt must start with `<!-- copilot-ledger-receipt -->` (with no byte order mark before it), be at most 65,536 bytes of UTF-8, and hold a JSON block with `"schemaVersion": 1` and a `branch` exactly equal to the pull request's branch. gh-ledger 0.4.0 and later write such receipts. Otherwise the action fails with an error that names the file or, when no receipt is for the pull request's branch, lists the branches of the receipts it found.
- It updates the oldest comment by `comment-author` (compared case-insensitively, as GitHub logins are) that starts with `<!-- copilot-ledger-receipt -->`. It never edits anyone else's comment.

## Versions

`@v1` follows the latest v1.x release. To pin an exact release, use its full commit SHA with the version as a comment, for example `uses: swigerb/copilot-ledger-action@<commit SHA> # v1.0.0`.

## Development

The action is a composite action: `action.yml` runs `receipt.ps1` with PowerShell 7, which GitHub-hosted runners include. The tests replay the GitHub REST API offline:

```shell
pwsh -NoProfile -File tests/receipt.Tests.ps1
```

## Releasing

1. Merge the change to `main` with CI passing.
2. Create the release, for example `gh release create v1.0.1 --repo swigerb/copilot-ledger-action --target main --title v1.0.1 --generate-notes`.
3. In the browser, edit the release, select **Publish this Action to the GitHub Marketplace**, choose a category, and select **Update release**. Publishing needs two-factor authentication, and the first time, accepting the GitHub Marketplace Developer Agreement.
4. Move the major tag to the release: `git fetch --tags origin`, then `git tag -f v1 v1.0.1` and `git push -f origin v1`.

## License

MIT. See [LICENSE](LICENSE).
````

### `.github/workflows/ci.yml` (copy exactly)

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  test:
    name: test (${{ matrix.os }})
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - name: Test the receipt action
        shell: pwsh
        run: pwsh -NoProfile -File tests/receipt.Tests.ps1
```

### Acceptance criteria

- The five files match this section and "Task R tests" exactly, `LICENSE` is unchanged, and the repository has no other files.
- `pwsh -NoProfile -File tests/receipt.Tests.ps1` prints `All receipt action tests passed.` and exits 0.
- CI on the pull request passes on `ubuntu-latest` and `windows-latest`.

### Verify

```shell
pwsh -NoProfile -File tests/receipt.Tests.ps1
```
## Test specifications

Shared rules for every gh-ledger task, which extend Sprint 4's:

- **Money.** Assertions use `decimal`. Read JSON money with `GetDecimal()`, never `GetDouble`.
- **Public API only.** No `InternalsVisibleTo`. CLI behavior is tested through `GhLedgerCli.InvokeAsync` (`CliHarness`), and the MCP server through the SDK's `McpClient` over in-memory pipes.
- **Isolation.** Each test that writes files gets its own `TempDirectory`, declared first (`using TempDirectory temp = new();`) so it's disposed last. Nothing touches the network, the real registry, `~/.copilot-ledger`, `~/.config`, gh, or shell profiles: HTTP goes through `FakeHttpHandler`, processes through `FakeCliPlatform`, and daemons through `TestServer` or `GuideWorld`'s stand-in. No test opens a socket or runs `git`.
- **SQLite pools.** Keep every database under the test's `TempDirectory`; `TempDirectory.Dispose` calls `SqliteConnection.ClearAllPools()` before deleting it. DM7 drops a table on a database that no pooled connection reopens with an older schema, so it needs nothing more.
- **Fresh files.** Read a file the product just wrote with `FileRetry.ReadAllTextAsync`, `ReadAllBytesAsync`, or, in synchronous helpers, `ReadAllText`. After a test writes a file for the product to read, read it back through `FileRetry` before running the product (`GitFixture` and `GuideWorld` do). Delete a file that was just written with `FileRetry.Delete`. Windows antivirus briefly locks new files (issue #30).
- **Clocks and waits.** `DaemonCommands.StartAsync` and `StopAsync` measure their timeouts with the context's clock, so under a `ManualTimeProvider` they wait until the daemon writes `daemon.json` or releases its lock. A stand-in daemon must therefore always release its lock, even when its cleanup fails; `GuideWorld`'s does so in `finally`.
- **Text.** `CliRun.Out` and `CliRun.Error` already have `\r\n` replaced with `\n`; never normalize them again. `GuideSamplesTests` normalizes the guide's text, which is the expected value, when it reads the file.
- **Exact output.** When a case compares `Out` or `Error`, it compares the whole text, and every CLI run asserts its exit code.

### Task 1 tests

**RC8** (`OtlpReceiverTests`, copy exactly). It builds the receiver with the new callback and checks that the ingestor's exception reaches it, once, as the same object:

Edit 1 of 2 in `tests/CopilotLedger.Tests/Otlp/OtlpReceiverTests.cs`, replace:

```csharp
    [Fact]
    public async Task RC8_IngestFailure_ReturnsRetryable503()
    {
        RecordingIngestor ingestor = new() { Exception = new InvalidOperationException() };
        await using var app = Build(ingestor);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.SendAsync(
```

with:

```csharp
    [Fact]
    public async Task RC8_IngestFailure_ReturnsRetryable503()
    {
        InvalidOperationException failure = new();
        RecordingIngestor ingestor = new() { Exception = failure };
        List<Exception> reported = [];
        await using var app = OtlpReceiverApp.Build(
            new OtlpReceiverOptions(),
            ingestor,
            builder => builder.UseTestServer(),
            reported.Add);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.SendAsync(
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Otlp/OtlpReceiverTests.cs`, replace:

```csharp
        Assert.Equal(
            "The ledger is unavailable; retry later.",
            await ReadJsonErrorAsync(response));
    }

    [Fact]
```

with:

```csharp
        Assert.Equal(
            "The ledger is unavailable; retry later.",
            await ReadJsonErrorAsync(response));
        Assert.Same(failure, Assert.Single(reported));
    }

    [Fact]
```

**DM1 and DM7** (`DaemonCommandTests`, copy exactly). DM1 expects version `0.4.1`. DM7 starts the in-process daemon with a manual clock at 2026-11-21T15:00:00Z and drops the `spans` table, so every ingestion fails. It posts the same Copilot trace three times, advancing the clock one minute before the third, then steps the clock back two minutes, to 14:59, and posts a fourth time. Then it stops the daemon. It asserts:

- all four responses are 503, and `daemon stop` and the daemon both exit 0;
- the log has exactly three `could not record spans` lines, in order: the first failure at 15:00, the third at 15:01 (the second, at 15:00, is suppressed), and the fourth at 14:59, written at once because the clock stepped back;
- the log has 6 lines in all, counting the empty string after its last line break: `daemon listening`, the three warnings, and `daemon stopped`. The test doesn't assert the `daemon listening` line's position, because the daemon writes it just after `daemon.json`, so in rare runs it could follow the first warning.

Edit 1 of 3 in `tests/CopilotLedger.Tests/Cli/DaemonCommandTests.cs`, replace:

```csharp
using System.Text.Json;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;
```

with:

```csharp
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Cli;
```

Edit 2 of 3 in `tests/CopilotLedger.Tests/Cli/DaemonCommandTests.cs`, replace:

```csharp
        JsonElement root = state.RootElement;
        Assert.Equal(Environment.ProcessId, root.GetProperty("processId").GetInt32());
        Assert.Equal("http://localhost:4318", root.GetProperty("endpoint").GetString());
        Assert.Equal("0.4.0", root.GetProperty("version").GetString());
        Assert.Equal(
            Path.Combine(temp.Path, "data", "ledger.db"),
            root.GetProperty("databasePath").GetString());
```

with:

```csharp
        JsonElement root = state.RootElement;
        Assert.Equal(Environment.ProcessId, root.GetProperty("processId").GetInt32());
        Assert.Equal("http://localhost:4318", root.GetProperty("endpoint").GetString());
        Assert.Equal("0.4.1", root.GetProperty("version").GetString());
        Assert.Equal(
            Path.Combine(temp.Path, "data", "ledger.db"),
            root.GetProperty("databasePath").GetString());
```

Edit 3 of 3 in `tests/CopilotLedger.Tests/Cli/DaemonCommandTests.cs`, replace:

```csharp
        Assert.False(File.Exists(stopPath));
        Assert.False(File.Exists(statePath));
    }
}
```

with:

```csharp
        Assert.False(File.Exists(stopPath));
        Assert.False(File.Exists(statePath));
    }

    [Fact]
    public async Task DM7_IngestFailuresAreLoggedAtMostOnceAMinute()
    {
        using var temp = new TempDirectory();
        ManualTimeProvider clock = new(
            DateTimeOffset.Parse("2026-11-21T15:00:00Z", CultureInfo.InvariantCulture));
        await using var harness = new CliHarness(temp.Path, timeProvider: clock);
        await harness.StartInProcessDaemonAsync();

        // Without the spans table, every request fails in RecordAsync, as it would with a broken or full ledger.
        await using (SqliteConnection connection = new(
            $"Data Source={Path.Combine(temp.Path, "data", "ledger.db")}"))
        {
            await connection.OpenAsync();
            await using SqliteCommand drop = connection.CreateCommand();
            drop.CommandText = "DROP TABLE spans;";
            await drop.ExecuteNonQueryAsync();
        }

        using HttpClient client = harness.CreateDaemonClient();
        HttpStatusCode first = await PostTraceAsync(client);
        HttpStatusCode second = await PostTraceAsync(client);
        clock.Advance(TimeSpan.FromMinutes(1));
        HttpStatusCode third = await PostTraceAsync(client);

        // After the clock steps back, the next failure is logged at once.
        clock.Advance(TimeSpan.FromMinutes(-2));
        HttpStatusCode fourth = await PostTraceAsync(client);
        CliRun stop = await harness.RunAsync("daemon", "stop");
        CliRun daemon = await harness.InProcessDaemon!;

        Assert.Equal(
            [
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.ServiceUnavailable,
            ],
            [first, second, third, fourth]);
        Assert.Equal(0, stop.ExitCode);
        Assert.Equal(0, daemon.ExitCode);
        Assert.Equal(
            [
                "2026-11-21T15:00:00.0000000+00:00 Warning: could not record spans: SqliteException: SQLite Error 1: 'no such table: spans'.",
                "2026-11-21T15:01:00.0000000+00:00 Warning: could not record spans: SqliteException: SQLite Error 1: 'no such table: spans'.",
                "2026-11-21T14:59:00.0000000+00:00 Warning: could not record spans: SqliteException: SQLite Error 1: 'no such table: spans'.",
            ],
            harness.DaemonOutput.Split('\n').Where(
                line => line.Contains(" Warning: could not record spans: ", StringComparison.Ordinal)));

        // The other lines are "daemon listening", "daemon stopped", and the empty string after the last line break.
        Assert.Equal(6, harness.DaemonOutput.Split('\n').Length);
    }

    private static async Task<HttpStatusCode> PostTraceAsync(HttpClient client)
    {
        using ByteArrayContent content = new(Encoding.UTF8.GetBytes(OtlpFixtures.CopilotChatTraceJson));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using HttpResponseMessage response = await client.PostAsync("/v1/traces", content);
        return response.StatusCode;
    }
}
```

**BM12** (`CircuitBreakerMonitorTests`, copy exactly). It records trace H's spans without a monitor, so no trip is recorded yet. Then it runs a `CircuitBreakerMonitor` whose sink cancels the evaluation's token when the trip is published, the moment a client disconnect would hurt most. It asserts:

- `EvaluateAsync` throws an `OperationCanceledException`, from the budget check that runs after the trip;
- the trip is recorded as in BM1 (`AssertTripH`), the one alert is a trip, and nothing was warned;
- the flag file exists and equals `CircuitFlagFile.Render` of the open trips, and `.copilot-ledger` holds no `*.tmp` file.

With the token passed to `SyncAsync` instead of `CancellationToken.None`, BM12 fails: the sync throws before it writes the flag file. Add it after BM11, and add `CancelingAlertSink` after `Harness`:

Edit 1 of 2 in `tests/CopilotLedger.Tests/Otlp/CircuitBreakerMonitorTests.cs`, replace:

```csharp
        Assert.Equal(NOW, exceeded.AlertedAt);
    }

    private static void AssertTripH(BreakerTrip trip, string repo)
    {
        Assert.Equal(1, trip.Id);
```

with:

```csharp
        Assert.Equal(NOW, exceeded.AlertedAt);
    }

    [Fact]
    public async Task BM12_CancellationAfterATripStillWritesTheFlagFile()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(
            temp,
            BreakerSettings.Default with { FlagFile = true },
            monitor: false);
        for (int request = 1; request <= 6; request++)
        {
            await harness.Ingestor.IngestAsync(TraceHRequest(request));
        }

        // The sink cancels the token as the trip is published, as a client that disconnects right then would.
        using CancellationTokenSource aborted = new();
        CancelingAlertSink sink = new(aborted);
        CircuitBreakerMonitor breaker = new(harness.Store, sink);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => breaker.EvaluateAsync([TraceH], aborted.Token));

        IReadOnlyList<BreakerTrip> trips =
            await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All);
        AssertTripH(Assert.Single(trips), harness.Repo);
        Assert.Equal(BreakerAlertKind.Trip, Assert.Single(sink.Alerts).Kind);
        Assert.Empty(sink.Warnings);
        string directory = Path.Combine(harness.Repo, ".copilot-ledger");
        Assert.Equal(
            CircuitFlagFile.Render(harness.Repo, trips),
            await FileRetry.ReadAllTextAsync(Path.Combine(directory, "CIRCUIT_OPEN")));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    private static void AssertTripH(BreakerTrip trip, string repo)
    {
        Assert.Equal(1, trip.Id);
```

Edit 2 of 2 in `tests/CopilotLedger.Tests/Otlp/CircuitBreakerMonitorTests.cs`, replace:

```csharp
            return new Harness(databasePath, store, ingestor, sink, repo);
        }
    }
}
```

with:

```csharp
            return new Harness(databasePath, store, ingestor, sink, repo);
        }
    }

    // Records alerts and warnings, and cancels source when an alert is published.
    private sealed class CancelingAlertSink(CancellationTokenSource source) : IBreakerAlertSink
    {
        public List<BreakerAlert> Alerts { get; } = [];
        public List<string> Warnings { get; } = [];

        public void Publish(BreakerAlert alert)
        {
            Alerts.Add(alert);
            source.Cancel();
        }

        public void Warn(string message) => Warnings.Add(message);
    }
}
```

**`ReportCommandTests.StatusText`** (copy exactly):

In `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs`, replace:

```csharp
    private static string StatusText(string root) =>
        Normalize($"""
        gh-ledger 0.4.0
        Daemon:  not running; start it with: gh-ledger daemon start
        Ledger:  {Path.Combine(root, "data", "ledger.db")}
        Rates:   2026-11-20
```

with:

```csharp
    private static string StatusText(string root) =>
        Normalize($"""
        gh-ledger 0.4.1
        Daemon:  not running; start it with: gh-ledger daemon start
        Ledger:  {Path.Combine(root, "data", "ledger.db")}
        Rates:   2026-11-20
```

**Fresh-file reads (A2).** On each line below (line numbers at `3a0a514`), the text after the line's indentation is exactly the "Replace" cell; change it to the "With" cell and keep the indentation. Nothing else in these files changes except BM12's additions, which come after these lines, and every enclosing test is already `async`.

| File | Line | Replace | With |
|---|---|---|---|
| `AlertDaemonTests.cs` | 74 | `File.ReadAllText(` | `await FileRetry.ReadAllTextAsync(` |
| `BreakerCommandTests.cs` | 266 | `Assert.Equal(expected, File.ReadAllText(flag));` | `Assert.Equal(expected, await FileRetry.ReadAllTextAsync(flag));` |
| `BreakerCommandTests.cs` | 273 | `Assert.Equal(expected, File.ReadAllText(flag));` | `Assert.Equal(expected, await FileRetry.ReadAllTextAsync(flag));` |
| `InstallCommandTests.cs` | 51 | `await File.ReadAllTextAsync(ManifestPath(temp.Path)));` | `await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));` |
| `InstallCommandTests.cs` | 110 | `await File.ReadAllTextAsync(ManifestPath(temp.Path)));` | `await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));` |
| `InstallCommandTests.cs` | 149 | `await File.ReadAllTextAsync(ManifestPath(temp.Path)));` | `await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));` |
| `InstallCommandTests.cs` | 171 | `await File.ReadAllTextAsync(ManifestPath(temp.Path))))` | `await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path))))` |
| `InstallCommandTests.cs` | 201 | `string afterFirst = await File.ReadAllTextAsync(profile);` | `string afterFirst = await FileRetry.ReadAllTextAsync(profile);` |
| `InstallCommandTests.cs` | 208 | `Assert.Equal(afterFirst, await File.ReadAllTextAsync(profile));` | `Assert.Equal(afterFirst, await FileRetry.ReadAllTextAsync(profile));` |
| `InstallCommandTests.cs` | 219 | `await File.ReadAllTextAsync(ManifestPath(temp.Path)));` | `await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));` |
| `InstallCommandTests.cs` | 252 | `(await File.ReadAllTextAsync(` | `(await FileRetry.ReadAllTextAsync(` |
| `InstallCommandTests.cs` | 287 | `await File.ReadAllTextAsync(ManifestPath(temp.Path)))` | `await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)))` |
| `InstallCommandTests.cs` | 415 | `(await File.ReadAllTextAsync(` | `(await FileRetry.ReadAllTextAsync(` |
| `InstallCommandTests.cs` | 464 | `await File.ReadAllTextAsync(autostart));` | `await FileRetry.ReadAllTextAsync(autostart));` |
| `InstallCommandTests.cs` | 466 | `await File.ReadAllTextAsync(ManifestPath(temp.Path))))` | `await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path))))` |
| `InstallCommandTests.cs` | 557 | `await File.ReadAllTextAsync(agent));` | `await FileRetry.ReadAllTextAsync(agent));` |
| `InstallCommandTests.cs` | 559 | `await File.ReadAllTextAsync(ManifestPath(temp.Path))))` | `await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path))))` |
| `InstallCommandTests.cs` | 622 | `await File.ReadAllTextAsync(ManifestPath(temp.Path)));` | `await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));` |
| `InstallCommandTests.cs` | 727 | `await File.ReadAllTextAsync(ManifestPath(temp.Path)));` | `await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));` |
| `RatesCommandTests.cs` | 36 | `File.ReadAllBytes(path));` | `await FileRetry.ReadAllBytesAsync(path));` |
| `RatesCommandTests.cs` | 37 | `string original = await File.ReadAllTextAsync(path);` | `string original = await FileRetry.ReadAllTextAsync(path);` |
| `RatesCommandTests.cs` | 44 | `Assert.Equal(original, await File.ReadAllTextAsync(path));` | `Assert.Equal(original, await FileRetry.ReadAllTextAsync(path));` |
| `RatesCommandTests.cs` | 47 | `Assert.Equal(original, await File.ReadAllTextAsync(path));` | `Assert.Equal(original, await FileRetry.ReadAllTextAsync(path));` |
| `CircuitBreakerMonitorTests.cs` | 243 | `File.ReadAllText(flag));` | `await FileRetry.ReadAllTextAsync(flag));` |
| `CircuitBreakerMonitorTests.cs` | 246 | `File.ReadAllText(` | `await FileRetry.ReadAllTextAsync(` |

A5 and A10 get no new tests. The build shows that nothing used the removed members. A10 changes only test support, and the suite passing with the throwing getter shows that no test relied on the default. The wider cleanup in `SyncAsync` has no test: nothing public can make the write or move throw anything but `IOException` or `UnauthorizedAccessException` at a known moment.

### Task 2 tests

No .NET tests. CI's new smoke lines are the test, on `ubuntu-latest` and `windows-latest`, and the orchestrator's manual Release run tests the `rates` job.

### Task 3 tests

`tests/CopilotLedger.Tests/Docs/GuideSamplesTests.cs` (copy exactly). Each case builds a fresh `GuideWorld` in its own `TempDirectory`, with the guide's `rates-overlay` sample as the overlay.

- **GS1** compares the set of sample IDs in the guide with the IDs the tests check: the 14 command samples plus `daemon-start`, `rates-update`, `budget-alert`, `receipt`, `mcp`, and `rates-overlay`.
- **GS2** (14 cases) runs each command sample in its state, asserts exit code 0 and an empty `Error`, and compares `Out`, with the data directory shown as `C:\Users\you\.copilot-ledger`, to the sample's text plus `\n`.
- **GS3** stops the stand-in daemon, asserts exit code 0, then runs `gh ledger daemon start` and compares it as GS2 does.
- **GS4** runs `rates update` with a `FakeHttpHandler` that serves the built-in rates with `ratesVersion` `2026-12-15`, after asserting that the sample's command is `gh ledger rates update`, and compares as GS2 does.
- **GS5** stops the stand-in, starts the real daemon in-process, posts the trace from `ChatTrace` (which must get 200), stops the daemon, and asserts both exit codes. The log must have exactly four elements after splitting on `\n`. The first two, in either order, are the `daemon listening` line (checked with `StartsWith` up to `(pid `, because the process ID varies) and the sample's `ALERT` line exactly: `StartInProcessDaemonAsync` waits only for `daemon.json`, which the daemon writes just before it logs `listening`, so the alert can come first. Then come `daemon stopped`, because shutdown drains the alert queue before logging it, and the empty string.
- **GS6** compares `gh ledger export-pr --stdout` with the quoted receipt, unquoted, plus `\n`.
- **GS7** runs `gh ledger mcp` over pipes, calls `get_branch_ledger` with no arguments, checks that the server exits 0 and wrote nothing to `Out` or `Error`, and compares the tool's text, with its last line (the JSON) indented, to the sample.

````csharp
using System.Globalization;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using CopilotLedger.Cli;
using CopilotLedger.Tests.TestSupport;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CopilotLedger.Tests.Docs;

// Checks that each sample in docs/guide.md is exactly what gh-ledger prints in the GuideWorld example.
public sealed partial class GuideSamplesTests
{
    // The samples that show a command's output, and the state each command runs in.
    private static readonly Dictionary<string, GuideState> Commands = new(StringComparer.Ordinal)
    {
        ["install"] = GuideState.Fresh,
        ["status-first"] = GuideState.FirstPrompt,
        ["status"] = GuideState.Month,
        ["branch"] = GuideState.Month,
        ["top-prompts"] = GuideState.Month,
        ["agents"] = GuideState.Month,
        ["budget-set"] = GuideState.Month,
        ["budget-list"] = GuideState.Month,
        ["breaker-status"] = GuideState.Month,
        ["breaker-reset"] = GuideState.Month,
        ["export-pr"] = GuideState.Month,
        ["rates-validate"] = GuideState.Month,
        ["daemon-stop"] = GuideState.Month,
        ["uninstall"] = GuideState.Month,
    };

    // The samples that GS3 to GS7 check, and the overlay that the Month state uses.
    private static readonly string[] OtherSamples =
        ["daemon-start", "rates-update", "budget-alert", "receipt", "mcp", "rates-overlay"];

    private static readonly Lazy<IReadOnlyDictionary<string, GuideSample>> Samples = new(ReadSamples);

    public static TheoryData<string> CommandIds => new(Commands.Keys);

    private static string Overlay => Samples.Value["rates-overlay"].Text + "\n";

    [Fact]
    public void GS1_TheGuideHasExactlyTheCheckedSamples()
    {
        Assert.Equal(
            Commands.Keys.Concat(OtherSamples).Order(StringComparer.Ordinal),
            Samples.Value.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(CommandIds))]
    public async Task GS2_CommandSamplesShowTheirOutput(string id)
    {
        GuideSample sample = Samples.Value[id];
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, Commands[id], Overlay);
        CliRun run = await world.RunAsync(sample.Command!);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(sample.Text + "\n", world.Display(run.Out));
    }

    [Fact]
    public async Task GS3_DaemonStartSampleRestartsTheDaemon()
    {
        GuideSample sample = Samples.Value["daemon-start"];
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        Assert.Equal(0, (await world.RunAsync("gh ledger daemon stop")).ExitCode);
        CliRun run = await world.RunAsync(sample.Command!);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(sample.Text + "\n", world.Display(run.Out));
    }

    [Fact]
    public async Task GS4_RatesUpdateSampleDownloadsNewerRates()
    {
        GuideSample sample = Samples.Value["rates-update"];
        Assert.Equal("gh ledger rates update", sample.Command);
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        FakeHttpHandler handler = new(
            _ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-15"))));
        CliRun run = await world.Harness.RunWithHttpAsync(handler, "rates", "update");
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(sample.Text + "\n", world.Display(run.Out));
    }

    [Fact]
    public async Task GS5_BudgetAlertSampleIsTheDaemonsAlertLine()
    {
        GuideSample sample = Samples.Value["budget-alert"];
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        Assert.Equal(0, (await world.RunAsync("gh ledger daemon stop")).ExitCode);
        await world.Harness.StartInProcessDaemonAsync();
        using HttpClient client = world.Harness.CreateDaemonClient();
        using ByteArrayContent content = new(Encoding.UTF8.GetBytes(ChatTrace(world.Repository)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using HttpResponseMessage response = await client.PostAsync("/v1/traces", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await world.Harness.RunAsync("daemon", "stop")).ExitCode);
        Assert.Equal(0, (await world.Harness.InProcessDaemon!).ExitCode);
        string[] lines = world.Harness.DaemonOutput.Split('\n');
        Assert.Equal(4, lines.Length);

        // The daemon logs "listening" just after it writes daemon.json, so the alert can come before it.
        Assert.Single(
            lines[..2],
            line => line.StartsWith(
                $"2026-11-21T21:30:00.0000000+00:00 gh-ledger {GhLedgerCli.Version} daemon listening on http://localhost:4318 (pid ",
                StringComparison.Ordinal));
        Assert.Single(lines[..2], line => line == sample.Text);
        Assert.Equal("2026-11-21T21:30:00.0000000+00:00 gh-ledger daemon stopped", lines[2]);
        Assert.Equal(string.Empty, lines[3]);
    }

    [Fact]
    public async Task GS6_ReceiptSampleIsTheBranchReceipt()
    {
        GuideSample sample = Samples.Value["receipt"];
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        CliRun run = await world.RunAsync("gh ledger export-pr --stdout");
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(sample.Text + "\n", run.Out);
    }

    [Fact]
    public async Task GS7_McpSampleIsTheBranchLedgerTool()
    {
        GuideSample sample = Samples.Value["mcp"];
        using TempDirectory temp = new();
        await using GuideWorld world = await GuideWorld.CreateAsync(temp.Path, GuideState.Month, Overlay);
        Pipe toServer = new();
        Pipe toClient = new();
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        Task<int> command = GhLedgerCli.InvokeAsync(
            ["mcp"],
            world.Harness.Context with
            {
                StandardInput = toServer.Reader.AsStream(),
                StandardOutput = toClient.Writer.AsStream(),
                WorkingDirectory = world.Repository,
                Out = output,
                Error = error,
            });
        await using McpClient client = await McpClient.CreateAsync(
            new StreamClientTransport(
                toServer.Writer.AsStream(),
                toClient.Reader.AsStream()),
            new McpClientOptions
            {
                ClientInfo = new Implementation
                {
                    Name = "guide-tests",
                    Version = "1.0",
                },
            });
        CallToolResult result = await client.CallToolAsync(
            "get_branch_ledger",
            new Dictionary<string, object?>());
        string text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        await client.DisposeAsync();
        await toServer.Writer.CompleteAsync();
        Assert.Equal(0, await command.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Empty(output.ToString());
        Assert.Empty(error.ToString());
        await toClient.Reader.CompleteAsync();
        int json = text.LastIndexOf('\n') + 1;
        Assert.Equal(sample.Text, text[..json] + Indent(text[json..]));
    }

    // A sample is "<!-- sample: <id> -->", then a shell block holding one gh ledger command and a text block holding
    // its output, or a single text or json block, or a quoted receipt, then "<!-- /sample -->".
    private static IReadOnlyDictionary<string, GuideSample> ReadSamples()
    {
        string guide = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "guide.md"))
            .ReplaceLineEndings("\n");
        Dictionary<string, GuideSample> samples = new(StringComparer.Ordinal);
        foreach (Match match in SampleRegex().Matches(guide))
        {
            string body = match.Groups["body"].Value;
            Match command = CommandSampleRegex().Match(body);
            Match block = BlockSampleRegex().Match(body);
            GuideSample sample = command.Success
                ? new GuideSample(command.Groups["command"].Value, command.Groups["text"].Value)
                : block.Success
                    ? new GuideSample(null, block.Groups["text"].Value)
                    : new GuideSample(null, Unquote(body));
            samples.Add(match.Groups["id"].Value, sample);
        }

        return samples;
    }

    // Removes the "> " or ">" that starts each line of a quoted sample.
    private static string Unquote(string body) =>
        string.Join(
            '\n',
            body.Split('\n').Select(line =>
                line == ">"
                    ? string.Empty
                    : line.StartsWith("> ", StringComparison.Ordinal)
                        ? line[2..]
                        : throw new InvalidDataException($"A quoted sample has an unquoted line: {line}")));

    // get_branch_ledger returns its JSON on one line; the guide shows it indented.
    private static string Indent(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(
            document.RootElement,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                NewLine = "\n",
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
    }

    // One chat span in the repository at 14:50 local time: 250,000 input tokens of claude-opus-5.5, 100.00 AI credits.
    private static string ChatTrace(string repository) => """
        {"resourceSpans":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"github-copilot"}},{"key":"process.working_directory","value":{"stringValue":REPOSITORY}}]},"scopeSpans":[{"spans":[{"traceId":"0f3c9a7e5d1b4f2a8c6e0d9b7a5c00f1","spanId":"5e8a1c3f7b9df1c1","name":"chat claude-opus-5.5","kind":3,"startTimeUnixNano":"1795290600000000000","endTimeUnixNano":"1795290600000000000","attributes":[{"key":"gen_ai.operation.name","value":{"stringValue":"chat"}},{"key":"gen_ai.request.model","value":{"stringValue":"claude-opus-5.5"}},{"key":"gen_ai.usage.input_tokens","value":{"intValue":"250000"}},{"key":"gen_ai.usage.output_tokens","value":{"intValue":"0"}}]}]}]}]}
        """.Replace("REPOSITORY", JsonSerializer.Serialize(repository), StringComparison.Ordinal);

    [GeneratedRegex(@"^<!-- sample: (?<id>[a-z-]+) -->\n(?<body>.*?)\n<!-- /sample -->$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex SampleRegex();

    [GeneratedRegex(@"\A```shell\n(?<command>gh ledger [^\n]+)\n```\n\n```text\n(?<text>.*)\n```\z", RegexOptions.Singleline)]
    private static partial Regex CommandSampleRegex();

    [GeneratedRegex(@"\A```(?:text|json)\n(?<text>.*)\n```\z", RegexOptions.Singleline)]
    private static partial Regex BlockSampleRegex();

    private sealed record GuideSample(string? Command, string Text);
}
````

### Task 4 tests

None new. `RateOverlayTests.DocsExamples_MatchRealMessages` (which reads `docs/configuration.md`) and `GuideSamplesTests` keep passing.

### Task 5 tests

None new. The Receipt workflow's run on the pull request shows that the pinned action resolves.

### Task R tests

`tests/receipt.Tests.ps1` (copy exactly). Its 18 cases, AC1–AC18, are Sprint 4's (see "Task 3 tests" in `.github/plans/archive/sprint-4.md`); only the two header comment lines differ. They fake the GitHub REST API with a function named `Invoke-WebRequest`, so they never reach the network.

````powershell
#Requires -Version 7.4
# Runs receipt.ps1 against a fake GitHub REST API. No network.
# Usage: pwsh -NoProfile -File tests/receipt.Tests.ps1

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$ScriptPath = Join-Path $PSScriptRoot '..' 'receipt.ps1'
$Api = 'https://api.github.test'
$Repository = 'octo/widgets'
$Sha = '0123456789abcdef0123456789abcdef01234567'
$Directory = '.github/copilot-ledger/receipts'
$ReceiptPath = "$Directory/feature-ledger-0a1b2c3d.md"
$FilesUri = "GET $Api/repos/$Repository/pulls/7/files?per_page=100&page=1"
$ContentUri = "GET $Api/repos/$Repository/contents/.github/copilot-ledger/receipts/feature-ledger-0a1b2c3d.md?ref=$Sha"
$CommentsUri = "GET $Api/repos/$Repository/issues/7/comments?per_page=100&page=1"
$CreateUri = "POST $Api/repos/$Repository/issues/7/comments"

$ReceiptText = @'
<!-- copilot-ledger-receipt -->
### CopilotLedger receipt for `feature/ledger` — 56.00 AI credits

<details>
<summary>Data</summary>

```json
{
  "schemaVersion": 1,
  "branch": "feature/ledger"
}
```

</details>
'@.ReplaceLineEndings("`n") + "`n"

$Fake = @{ Requests = $null; Bodies = $null; Headers = $null; Responses = $null }
$Failures = [System.Collections.Generic.List[string]]::new()

# Replaces the cmdlet for receipt.ps1, which runs in a child scope of this script.
function Invoke-WebRequest {
    param(
        [string] $Uri,
        [string] $Method,
        [hashtable] $Headers,
        [byte[]] $Body,
        [string] $ContentType)
    $key = "$Method $Uri"
    $Fake.Requests.Add($key)
    $Fake.Headers = $Headers
    if ($null -ne $Body) {
        $Fake.Bodies.Add("$ContentType $([System.Text.Encoding]::UTF8.GetString($Body))")
    }

    if (-not $Fake.Responses.ContainsKey($key)) {
        throw "Unexpected request: $key"
    }

    return [pscustomobject]@{ Content = $Fake.Responses[$key] }
}

function ConvertTo-CompactJson($Value) {
    return ConvertTo-Json -InputObject $Value -Compress -Depth 5
}

function New-Files([object[]] $Files) {
    return ConvertTo-CompactJson @($Files | ForEach-Object { [ordered]@{ filename = $_[0]; status = $_[1] } })
}

function New-Content([string] $Text, [long] $Size = -1) {
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($Text)
    return ConvertTo-CompactJson ([ordered]@{
        size = if ($Size -ge 0) { $Size } else { $bytes.Length }
        encoding = 'base64'
        content = [System.Convert]::ToBase64String($bytes, [System.Base64FormattingOptions]::InsertLineBreaks)
    })
}

function New-Comment([long] $Id, [string] $Login, [string] $Body) {
    return [ordered]@{
        id = $Id
        user = [ordered]@{ login = $Login }
        body = $Body
        html_url = "https://github.test/octo/widgets/pull/7#issuecomment-$Id"
    }
}

function Invoke-Receipt([hashtable] $Responses, [hashtable] $Environment = @{}) {
    $Fake.Requests = [System.Collections.Generic.List[string]]::new()
    $Fake.Bodies = [System.Collections.Generic.List[string]]::new()
    $Fake.Headers = $null
    $Fake.Responses = $Responses
    $outputFile = [System.IO.Path]::GetTempFileName()
    $values = @{
        GITHUB_API_URL = $Api
        GITHUB_REPOSITORY = $Repository
        GITHUB_OUTPUT = $outputFile
        RECEIPT_TOKEN = 'test-token'
        RECEIPTS_DIRECTORY = $Directory
        COMMENT_AUTHOR = 'github-actions[bot]'
        EVENT_NAME = 'pull_request'
        PR_NUMBER = '7'
        HEAD_REF = 'feature/ledger'
        HEAD_SHA = $Sha
        HEAD_REPOSITORY = $Repository
    }
    foreach ($name in $Environment.Keys) {
        $values[$name] = $Environment[$name]
    }

    foreach ($name in $values.Keys) {
        [System.Environment]::SetEnvironmentVariable($name, $values[$name])
    }

    $global:LASTEXITCODE = 0
    try {
        [string[]] $lines = @(& $ScriptPath *>&1 | ForEach-Object { "$_" })
    }
    catch {
        # An unexpected request fails this case, not the whole run.
        [string[]] $lines = @("exception: $($_.Exception.Message)")
        $global:LASTEXITCODE = 99
    }

    $run = [pscustomobject]@{
        ExitCode = $global:LASTEXITCODE
        Lines = $lines
        Outputs = [System.IO.File]::ReadAllText($outputFile)
        Requests = [string[]] $Fake.Requests.ToArray()
        Bodies = [string[]] $Fake.Bodies.ToArray()
        Headers = $Fake.Headers
    }
    Remove-Item -LiteralPath $outputFile
    return $run
}

function Assert-Equal($Expected, $Actual, [string] $Message) {
    $expectedJson = ConvertTo-Json -InputObject $Expected -Compress -Depth 5 -EscapeHandling EscapeNonAscii
    $actualJson = ConvertTo-Json -InputObject $Actual -Compress -Depth 5 -EscapeHandling EscapeNonAscii
    if (-not [string]::Equals($expectedJson, $actualJson, [System.StringComparison]::Ordinal)) {
        $Failures.Add("$Message`n  expected: $expectedJson`n  actual:   $actualJson")
    }
}

function Get-PostedBody([string] $Request) {
    $json = $Request.Substring('application/json '.Length)
    return [System.Text.Json.JsonDocument]::Parse($json).RootElement.GetProperty('body').GetString()
}

# AC1: a new receipt is posted as a new comment.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(@('src/app.cs', 'modified'), @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText
    $CommentsUri = '[]'
    $CreateUri = ConvertTo-CompactJson (New-Comment 100 'github-actions[bot]' $ReceiptText)
}
Assert-Equal 0 $run.ExitCode 'AC1 exit code'
Assert-Equal @('CopilotLedger receipt created: https://github.test/octo/widgets/pull/7#issuecomment-100') $run.Lines 'AC1 log'
Assert-Equal "result=created`ncomment-url=https://github.test/octo/widgets/pull/7#issuecomment-100`n" $run.Outputs 'AC1 outputs'
Assert-Equal @($FilesUri, $ContentUri, $CommentsUri, $CreateUri) $run.Requests 'AC1 requests'
Assert-Equal 1 $run.Bodies.Count 'AC1 body count'
Assert-Equal $ReceiptText (Get-PostedBody $run.Bodies[0]) 'AC1 posted body'
Assert-Equal 'Bearer test-token' $run.Headers['Authorization'] 'AC1 authorization header'
Assert-Equal 'application/vnd.github+json' $run.Headers['Accept'] 'AC1 accept header'
Assert-Equal '2026-03-10' $run.Headers['X-GitHub-Api-Version'] 'AC1 API version header'

# AC2: the bot's earlier receipt comment is updated in place.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'modified'))
    $ContentUri = New-Content $ReceiptText
    $CommentsUri = ConvertTo-CompactJson @(
        (New-Comment 54 'octocat' 'Looks good'),
        (New-Comment 55 'github-actions[bot]' "<!-- copilot-ledger-receipt -->`nold"))
    "PATCH $Api/repos/$Repository/issues/comments/55" = ConvertTo-CompactJson (New-Comment 55 'github-actions[bot]' $ReceiptText)
}
Assert-Equal 0 $run.ExitCode 'AC2 exit code'
Assert-Equal @('CopilotLedger receipt updated: https://github.test/octo/widgets/pull/7#issuecomment-55') $run.Lines 'AC2 log'
Assert-Equal "result=updated`ncomment-url=https://github.test/octo/widgets/pull/7#issuecomment-55`n" $run.Outputs 'AC2 outputs'
Assert-Equal @($FilesUri, $ContentUri, $CommentsUri, "PATCH $Api/repos/$Repository/issues/comments/55") $run.Requests 'AC2 requests'
Assert-Equal $ReceiptText (Get-PostedBody $run.Bodies[0]) 'AC2 posted body'

# AC3: an identical comment (stored with CRLF line endings) isn't edited.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText
    $CommentsUri = ConvertTo-CompactJson @(, (New-Comment 56 'github-actions[bot]' $ReceiptText.Replace("`n", "`r`n")))
}
Assert-Equal 0 $run.ExitCode 'AC3 exit code'
Assert-Equal @('CopilotLedger receipt unchanged: https://github.test/octo/widgets/pull/7#issuecomment-56') $run.Lines 'AC3 log'
Assert-Equal "result=unchanged`ncomment-url=https://github.test/octo/widgets/pull/7#issuecomment-56`n" $run.Outputs 'AC3 outputs'
Assert-Equal @($FilesUri, $ContentUri, $CommentsUri) $run.Requests 'AC3 requests'

# AC4: a marker comment by someone else isn't touched; the action adds its own.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText
    $CommentsUri = ConvertTo-CompactJson @(, (New-Comment 57 'octocat' $ReceiptText))
    $CreateUri = ConvertTo-CompactJson (New-Comment 101 'github-actions[bot]' $ReceiptText)
}
Assert-Equal 0 $run.ExitCode 'AC4 exit code'
Assert-Equal "result=created`ncomment-url=https://github.test/octo/widgets/pull/7#issuecomment-101`n" $run.Outputs 'AC4 outputs'
Assert-Equal @($FilesUri, $ContentUri, $CommentsUri, $CreateUri) $run.Requests 'AC4 requests'

# AC5: no receipt in the pull request (a removed receipt doesn't count).
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(@('src/app.cs', 'modified'), @($ReceiptPath, 'removed'), @("$Directory/notes.txt", 'added'))
}
Assert-Equal 0 $run.ExitCode 'AC5 exit code'
Assert-Equal @('::notice::No CopilotLedger receipt changed in this pull request. Run gh ledger export-pr on the branch, then commit the file it writes under .github/copilot-ledger/receipts/.') $run.Lines 'AC5 log'
Assert-Equal "result=no-receipt`ncomment-url=`n" $run.Outputs 'AC5 outputs'
Assert-Equal @(, $FilesUri) $run.Requests 'AC5 requests'

# AC6: pull requests from forks are skipped without any request.
$run = Invoke-Receipt @{} @{ HEAD_REPOSITORY = 'fork-owner/widgets' }
Assert-Equal 0 $run.ExitCode 'AC6 exit code'
Assert-Equal @('::notice::Skipped the CopilotLedger receipt: workflows for pull requests from forks get a read-only token, so they can''t comment.') $run.Lines 'AC6 log'
Assert-Equal "result=skipped`ncomment-url=`n" $run.Outputs 'AC6 outputs'
Assert-Equal @() $run.Requests 'AC6 requests'

# AC7: other events fail without any request.
$run = Invoke-Receipt @{} @{ EVENT_NAME = 'pull_request_target' }
Assert-Equal 1 $run.ExitCode 'AC7 exit code'
Assert-Equal @('::error::The CopilotLedger receipt action runs on pull_request events, not pull_request_target.') $run.Lines 'AC7 log'
Assert-Equal '' $run.Outputs 'AC7 outputs'
Assert-Equal @() $run.Requests 'AC7 requests'

# AC8: a receipt for another branch fails.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText
} @{ HEAD_REF = 'feature/other' }
Assert-Equal 1 $run.ExitCode 'AC8 exit code'
Assert-Equal @('::error::No receipt for feature/other changed in this pull request; found receipts for: feature/ledger.') $run.Lines 'AC8 log'
Assert-Equal @($FilesUri, $ContentUri) $run.Requests 'AC8 requests'

# AC9: a file without the marker fails.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content ("# Notes`n" + $ReceiptText)
}
Assert-Equal 1 $run.ExitCode 'AC9 exit code'
Assert-Equal @("::error::$ReceiptPath doesn't start with <!-- copilot-ledger-receipt -->, so it isn't a CopilotLedger receipt.") $run.Lines 'AC9 log'

# AC10: a receipt over 65,536 bytes fails before it's decoded.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText 65537
}
Assert-Equal 1 $run.ExitCode 'AC10 exit code'
Assert-Equal @("::error::$ReceiptPath is 65537 bytes; a receipt can be at most 65536 bytes.") $run.Lines 'AC10 log'

# AC11: invalid JSON in the data block fails.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText.Replace('"schemaVersion": 1,', '"schemaVersion": 1')
}
Assert-Equal 1 $run.ExitCode 'AC11 exit code'
Assert-Equal 1 $run.Lines.Count 'AC11 log line count'
Assert-Equal $true ($run.Lines[0].StartsWith("::error::$ReceiptPath has invalid JSON data: ", [System.StringComparison]::Ordinal)) 'AC11 log'

# AC12: an unsupported schema version fails.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText.Replace('"schemaVersion": 1,', '"schemaVersion": 2,')
}
Assert-Equal 1 $run.ExitCode 'AC12 exit code'
Assert-Equal @("::error::$ReceiptPath has an unsupported schemaVersion; this action reads schemaVersion 1.") $run.Lines 'AC12 log'

# AC13: pages are read until one has fewer than 100 items.
$page1 = @(1..100 | ForEach-Object { , @("src/file$_.cs", 'modified') })
$run = Invoke-Receipt @{
    $FilesUri = New-Files $page1
    "GET $Api/repos/$Repository/pulls/7/files?per_page=100&page=2" = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText
    $CommentsUri = '[]'
    $CreateUri = ConvertTo-CompactJson (New-Comment 102 'github-actions[bot]' $ReceiptText)
}
Assert-Equal 0 $run.ExitCode 'AC13 exit code'
Assert-Equal @($FilesUri, "GET $Api/repos/$Repository/pulls/7/files?per_page=100&page=2", $ContentUri, $CommentsUri, $CreateUri) $run.Requests 'AC13 requests'

# AC14: a receipts-directory input with surrounding slashes still matches.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText
    $CommentsUri = '[]'
    $CreateUri = ConvertTo-CompactJson (New-Comment 103 'github-actions[bot]' $ReceiptText)
} @{ RECEIPTS_DIRECTORY = "/$Directory/" }
Assert-Equal 0 $run.ExitCode 'AC14 exit code'
Assert-Equal "result=created`ncomment-url=https://github.test/octo/widgets/pull/7#issuecomment-103`n" $run.Outputs 'AC14 outputs'

# AC15: branch names are compared case-sensitively.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText
} @{ HEAD_REF = 'Feature/Ledger' }
Assert-Equal 1 $run.ExitCode 'AC15 exit code'
Assert-Equal @('::error::No receipt for Feature/Ledger changed in this pull request; found receipts for: feature/ledger.') $run.Lines 'AC15 log'

# AC16: a branch that differs only by a zero-width space doesn't match.
$zeroWidthBranch = "feature/ledger$([char] 0x200B)"
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content $ReceiptText.Replace('"branch": "feature/ledger"', "`"branch`": `"$zeroWidthBranch`"")
}
Assert-Equal 1 $run.ExitCode 'AC16 exit code'
Assert-Equal @("::error::No receipt for feature/ledger changed in this pull request; found receipts for: $zeroWidthBranch.") $run.Lines 'AC16 log'
Assert-Equal @($FilesUri, $ContentUri) $run.Requests 'AC16 requests'

# AC17: a receipt that starts with a byte order mark fails the marker check.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'added'))
    $ContentUri = New-Content ("$([char] 0xFEFF)" + $ReceiptText)
}
Assert-Equal 1 $run.ExitCode 'AC17 exit code'
Assert-Equal @("::error::$ReceiptPath doesn't start with <!-- copilot-ledger-receipt -->, so it isn't a CopilotLedger receipt.") $run.Lines 'AC17 log'
Assert-Equal @($FilesUri, $ContentUri) $run.Requests 'AC17 requests'

# AC18: comment-author matches the comment's login case-insensitively, as GitHub logins do.
$run = Invoke-Receipt @{
    $FilesUri = New-Files @(, @($ReceiptPath, 'modified'))
    $ContentUri = New-Content $ReceiptText
    $CommentsUri = ConvertTo-CompactJson @(, (New-Comment 58 'github-actions[bot]' "<!-- copilot-ledger-receipt -->`nold"))
    "PATCH $Api/repos/$Repository/issues/comments/58" = ConvertTo-CompactJson (New-Comment 58 'github-actions[bot]' $ReceiptText)
} @{ COMMENT_AUTHOR = 'GitHub-Actions[bot]' }
Assert-Equal 0 $run.ExitCode 'AC18 exit code'
Assert-Equal "result=updated`ncomment-url=https://github.test/octo/widgets/pull/7#issuecomment-58`n" $run.Outputs 'AC18 outputs'
Assert-Equal @($FilesUri, $ContentUri, $CommentsUri, "PATCH $Api/repos/$Repository/issues/comments/58") $run.Requests 'AC18 requests'

if ($Failures.Count -gt 0) {
    $Failures | ForEach-Object { Write-Host "FAIL: $_" }
    Write-Host "$($Failures.Count) assertion(s) failed."
    exit 1
}

Write-Host 'All receipt action tests passed.'
exit 0
````
## Final step: end-to-end check and releases (orchestrator, not a builder task)

After Task 5 merges and CI on `main` is green, the orchestrator:

1. **Runs the end-to-end check**, with `COPILOT_LEDGER_HOME` pointed at a temporary directory:
   - a real Copilot CLI request into a running daemon;
   - the reports, the circuit breaker, a budget, and the MCP server;
   - `export-pr`, and the action on a real draft pull request, from `swigerb/copilot-ledger-action` at the commit `receipt.yml` pins;
   - `rates update`;
   - `gh extension install swigerb/gh-ledger`.
2. **Releases the action first**, because the docs say `@v1`. It creates `v1.0.0` at the commit Task 5 pinned, with `gh release create v1.0.0 --repo swigerb/copilot-ledger-action --target <commit> --title v1.0.0 --generate-notes`. Then, from a clone, it creates the major tag at the same commit: `git fetch --tags origin`, `git tag v1 v1.0.0`, and `git push origin v1`. The user lists the release on Marketplace in the browser, as the action's README describes. If the end-to-end check needs a fix in the action, the fixed commit becomes `v1.0.0`, and `receipt.yml`'s pin moves to it in a small pull request.
3. **Releases gh-ledger v0.4.1**, following `docs/releasing.md`: a manual Release run on `main` (the dry run, which also checks `ratesVersion`), then the tag. Afterward, with the daemon stopped and the Copilot sessions that use the MCP server closed, `gh extension upgrade ledger` takes an installed 0.4.0 to 0.4.1 (`gh ledger status` shows `gh-ledger 0.4.1 (GitHub CLI extension)`), and `gh ledger rates update` prints `Rates are up to date (2026-11-20).`, because the rates didn't change.

When the sprint closes, `ledger-architect` archives this plan with its amendments.

## Deferred to later sprints

- **`gh ledger` in every message** (carried). Under the extension, breaker, alert, MCP, sign-in, `rates init`, and port messages still say `gh-ledger <command>`, and the guide's samples show it. Using `GhExtension.CommandName` in each would change many tests and the guide's samples. Recommended next.
- **Workflow dependency lockfiles.** GitHub now recommends `gh actions-lock` lockfiles over SHA pins, kept current by Dependabot. Adopting them needs the user's decision on Dependabot.
- **The guide on macOS and Linux.** The samples show Windows. A second `GuideWorld` platform could produce the setup scenario's `install` and `status` output for a shell profile.
- **Reconciliation with `nano_aiu`** (carried). Store each `chat` span's `github.copilot.nano_aiu` and report drift per model, never the root `invoke_agent` total. Protobuf carries it as an IEEE `double`, so it needs a decimal-only design.
- **Breaker state in `status`** (carried).
- **MCP registration in `install`** (carried). `install` could run `copilot mcp add` or edit `~/.copilot/mcp-config.json`, with `uninstall` undoing it; until then, the docs give `copilot mcp remove ledger`.
- **`COPILOT_LEDGER_HOME` in sign-in items** (carried). Pass the data directory to the sign-in item instead of warning.
- **Headless Linux** (carried). A `systemd --user` unit for machines without a desktop session.
- **A tool gate** (carried). A user-level `preToolUse` hook that denies tools while the breaker is open; it needs the hook contract verified live.
- **Webhooks** (carried) as another alert channel.
- **Smarter retry churn** (carried). Telling test and build commands apart needs the command text, which Copilot exports only with content capture.
- **Signed receipts and a pre-push hook** (carried). A signature can't vouch for a local ledger, and a pre-push hook can't add to the push it runs in; see `.github/plans/archive/sprint-4.md`.
- **`export-pr --since`** (carried), for branch names that are reused.
- **Upgrading without stopping everything by hand** (carried). For example, a `gh ledger upgrade` command that stops the daemon, upgrades, and restarts it, instead of documenting `daemon stop` first. Copilot's MCP servers would still hold the binary on Windows, so it could only report which processes are left.
- **Build provenance** (carried). `actions/attest-build-provenance` for the release assets, verifiable with `gh attestation verify`.
- **Arm64 smoke tests** (carried). Run the `linux-arm64` and `win-arm64` binaries on Arm64 runners, and `osx-x64` under Rosetta.
- **Checking for newer rates automatically** (carried), for example a `status` hint when the latest release's rates are newer.
- **Receipts for fork pull requests** (carried). They'd need `pull_request_target` or a second workflow triggered by `workflow_run`, with care for untrusted input.
- **Carried from Sprint 2:** a custom port in `install`, rates follow-ups, BYOK, local, and VS Code Local captures, file-exporter ingestion, Native AOT, and overflow-safe rollups, as described in `.github/plans/archive/sprint-2.md`.
