# Sprint 4: PR Receipts, GitHub Action & gh Extension Release

| | |
|---|---|
| Architect | `ledger-architect` (Claude Opus 5.5) |
| Builder | `ledger-builder` (MAI Code 2) |
| Target | .NET 10 (`net10.0`), SDK 10.0.x, zero Docker |
| Plan written | 2026-11-21, against merged Sprint 3 (`main` at `33a2881`) |
| Status | Current sprint. Sprint 3 is complete and archived at `.github/plans/archive/sprint-3.md`. |

## How to use this plan

`ledger-builder` implements one task per run: the first unchecked `Task N:` line below. It follows that task's specification section, runs the verification commands, changes the line to `- [x]`, and stops. Tasks are sequential, and each builds on the ones before it.

Every task ships with its tests, listed under "Test specifications". A task is done only when `dotnet test` runs the whole suite, including its new cases, with 0 failed and 0 skipped. Task 3 also adds PowerShell tests for the action, and its verification runs them.

Most new code in this plan is marked **copy exactly**. The architect built every task in a scratch copy of `main`, and that code compiled with `TreatWarningsAsErrors` and passed the tests below: 649 tests in `dotnet test`, plus the action's 18 PowerShell cases. Copy it character for character, including every comment line, `using` directive, and blank line. Don't add argument checks (`ThrowIfNull` and the like), cancellation tokens, `ConfigureAwait`, logging, caching, or fast paths that the code doesn't show, and don't "tidy" it.

Each test case lists every assertion it makes; most are given as code. Write all of them, in the order given; don't drop one because it looks redundant, and don't turn an exact comparison into `Contains` or `StartsWith`. Never normalize the product's output in a test (no `ReplaceLineEndings`, `Replace("\r\n", …)`, or `Trim` on an actual value); `CliHarness` already turns `\r\n` into `\n` in `Out` and `Error`.

The final section, "Final step: architect audit", isn't a builder task. It has no checkbox, and `ledger-builder` never runs it.

## Tasks

- [x] Task 1: Detached Daemon Start (CopilotLedger.Cli & CI). `daemon start` gives the daemon its own null standard handles and, on Windows, lets it inherit no other handle, so `x=$(gh-ledger daemon start)` and PowerShell captures return at once instead of waiting for the daemon to exit; a daemon binary that can't run is reported on standard error. Tests cover Windows command-line quoting and that failure, and CI captures `daemon start` in bash and PowerShell on Ubuntu and Windows.
- [x] Task 2: PR Receipts: `export-pr` (CopilotLedger.Storage & Cli). Add a channel-and-model rollup, a prompt count, and a breaker-trip count to the store, and `gh-ledger export-pr`, which writes the branch's receipt to `.github/copilot-ledger/receipts/<branch>-<hash>.md`: collapsible Markdown with GitHub AI Credits, Foundry BYOK, local offload, breaker trips, and the most expensive prompt, plus its JSON data, at most 65,536 bytes. Tests compare whole receipts and reconcile them with `branch --json` in decimal.
- [x] Task 3: The Receipt Action (action/ & CI). Add `action/action.yml`, a composite action that runs `action/receipt.ps1` on GitHub's PowerShell 7 to find the receipt a pull request commits, validate it, and post or update one comment, with only `contents: read` and `pull-requests: write`. PowerShell tests replay the GitHub REST API offline, CI runs them, and this repository uses the action on its own pull requests.
- [x] Task 4: gh Extension Release, Version 0.4.0 & Quickstart (CopilotLedger.Cli, workflows & docs). Add a tag-triggered release workflow that publishes the six binaries under GitHub CLI's precompiled-extension names, `rates.json`, and checksums. When `gh-ledger` runs as the installed extension, `install` skips the alias and `status` says so and names `gh ledger` commands. Bump the version to 0.4.0 and rewrite the README quickstart around `gh extension install`, `export-pr`, and the action.
- [x] Task 5: `rates update` (CopilotLedger.Core & Cli). `gh-ledger rates update` downloads `rates.json` from the latest release through the system proxy, validates it, and saves it as `rates.downloaded.json` when it's newer than the rates in use. The daemon, `status`, and `rates validate` use the newer of the built-in and downloaded rates, with the overlay on top. Tests use a fake `HttpMessageHandler`, never the network.

## Starting point

- Sprint 3 is complete. Tasks 1–5 merged in PRs #26, #27, #28, #29, and #33, with fix #31 (issue #30) and PR #34, which added `LICENSE`, `README.md`, and `docs/guide.md` at the user's request (`main` at `33a2881`). `dotnet test CopilotLedger.slnx` passes 577 tests (0 failed, 0 skipped), and CI is green on `ubuntu-latest` and `windows-latest`. Sprint 3's plan, with its amendments, is archived at `.github/plans/archive/sprint-3.md`.
- The repository is now `swigerb/gh-ledger`; GitHub redirects the old name. Product URLs added in this sprint use the new name. Existing identifiers that contain `copilot-ledger` (the data directory, the LaunchAgent label, test fixtures' `swigerb/copilot-ledger` slug) don't change.
- Merged Sprint 3 behavior that this plan depends on (read from the code at `33a2881`):
  - `ICliPlatform.StartDetached(fileName, arguments, workingDirectory)` returns the new process ID. `SystemCliPlatform` implements it with `Process.Start` (`UseShellExecute = false`, `CreateNoWindow = true`, no redirection). `DaemonCommands.StartAsync` calls it with `[daemon, run, --port, <port>, --log-file, <data>/daemon.log]` and the data directory, then polls `daemon.json` until its `ProcessId` equals the returned ID. Tests replace it with `FakeCliPlatform.StartDetachedHandler`, which `CliHarness` sets to an in-process daemon.
  - `DaemonHost.RunAsync` loads rates once, before it opens the store: `LoadEmbeddedWithOverlay(<data>/rates.json)` when that file exists, else `LoadEmbedded()`. On `InvalidDataException` it logs `Invalid rates overlay: <message>` and returns 1. It logs `{TimeProvider.GetUtcNow():O} {message}` lines to `--log-file`, or to `context.Out` without one.
  - `ReportCommands.StatusAsync` loads rates the same way, inline. Its text output is the `gh-ledger <version>` line, then `Daemon:`, `Ledger:`, `Rates:`, and `Copilot:` lines, then a `Warning: <text>` line per OTel warning, a blank line, and the period table. The daemon line says `not running; start it with: gh-ledger daemon start`, and the Copilot line says `OTel export not enabled; run: gh-ledger install` when export is off. `WriteStatusJson` writes `version`, `daemon`, `databasePath`, `ratesVersion`, `ratesError`, `copilotOtelEnabled`, `warnings`, and `periods`.
  - `RatesCommands` has `rates init [--force]` and `rates validate [--file <path>]`. Without `--file` and without an overlay, `validate` prints `No rates overlay at <path>; using the built-in rates (<version>).`; with a valid overlay it prints `<path> is valid. Rates version: <version>.`; with an invalid one it writes `Invalid rates overlay: <message>` to `Error` and returns 1.
  - `JsonRateCardCalculator` exposes `LoadEmbedded()`, `LoadEmbeddedWithOverlay(path)`, `Parse(json, overlayJson?)`, and `RatesVersion`. Errors are `InvalidDataException`s: a base document's have no prefix, and an overlay's start with its path (`Parse` uses `overlay`). Invalid JSON reads `line <L>, column <C>: invalid JSON`. A merged version is `<base>+<overlay label or "user">`. The embedded `rates.json` has `"ratesVersion": "2026-11-20"`.
  - `InstallCommands.InstallAsync` checks that the executable is named `gh-ledger`, prints OTel warnings, sets the Windows user variable or the profile block, runs `gh alias set ledger '<exe>' "$@" --shell --clobber` when `gh` is on `PATH`, prints the `sh.exe` warning on Windows when `FindSh` finds none, registers the sign-in item (`"<exe>" daemon start` on Windows and Linux, `<exe> daemon run --log-file …` in the macOS plist), writes `install.json`, and starts the daemon. `TryReadManifestAsync` and `ManifestPath` are private.
  - `SqliteLedgerStore.GetRollupAsync(grouping, filter)` groups by `LedgerGrouping` (`Repository`, `Branch`, `Model`, `Agent`) and orders rows by `github_usd_pico + byok_usd_pico DESC, saved_usd_pico DESC`, then the keys ascending. A `LedgerFilter`'s `Repository` and `Branch` match `COALESCE(repo_slug, repo_root, '(none)')` and `COALESCE(branch_name, '(none)')`. `GetTopPromptChainsAsync(filter, limit)` returns prompt chains (one per `trace_id`) ordered the same way. `breaker_trips` has `repository` and `branch` columns that hold the same keys, and `reset_at_unix_ms` is set when a trip is reset.
  - `LedgerFormat` (internal) has `Credits` (2 places, `MidpointRounding.AwayFromZero`, `N2`), `Usd` (`$` + `Credits`), `Count` (`N0`), `WriteMoney` (`0.############`), `WriteTime` (`O`, UTC), and `WriteTotals` (10 properties: `spans`, `unpricedSpans`, `inputTokens`, `outputTokens`, `cacheReadInputTokens`, `cacheWriteInputTokens`, `aiCredits`, `aiCreditsUsd`, `foundryByokUsd`, `localSavedUsd`).
  - `branch` resolves the repository key as `RepositorySlug ?? RepositoryRoot` from `new FileSystemGitContextResolver().Resolve(context.WorkingDirectory)`, which reads `.git` and never throws. `GitWorkspaceContext` has `RepositoryRoot`, `BranchName` (null when detached), `HeadCommitSha` (null on an unborn branch), and `RepositorySlug` (null without a GitHub-style `origin` URL).
  - `CliHarness` runs commands in-process with `FakeCliPlatform`; `RunAsync`, `RunInAsync(workingDirectory, …)`, `StartInProcessDaemonAsync`, `CreateDaemonClient`, `InProcessDaemon`, and `DaemonOutput` exist. `FakeCliPlatform.ProcessPath` defaults to `/opt/gh-ledger/gh-ledger`; `FileExists` answers from `ExistingFiles`. `TempDirectory.Dispose` calls `SqliteConnection.ClearAllPools()` first. `FileRetry.ReadAllTextAsync` retries reads that Windows reports in use. `GitFixture.Create(root)` makes `<root>/repo` on branch `feature/ledger` at `Sha1` with origin `https://github.com/swigerb/copilot-ledger.git`.
  - `GhLedgerCli.Version` is `0.3.0`. The CI smoke test checks it, starts the daemon, runs `status`, `breaker status`, `budget list`, and one MCP `tools/list`, and stops the daemon.
- Sprint 3's public contracts are fixed. Sprint 4 adds types and members, and it changes only these existing artifacts, each called out in its task:
  - Task 1: `SystemCliPlatform.StartDetached` (plus one new method), the `StartDetached` comment in `ICliPlatform`, `.github/workflows/ci.yml`, and `docs/cli.md`;
  - Task 2: `LedgerGrouping` (one member), `LedgerRollupRow` (one property), `SqliteLedgerStore` (one rollup arm and two methods), `GhLedgerCli` (registration), `FileRetry` (one method), and `docs/cli.md`;
  - Task 3: `.github/workflows/ci.yml` and `docs/cli.md`;
  - Task 4: `InstallCommands`, `ReportCommands`, the Cli project file and `GhLedgerCli` (version), `.github/workflows/ci.yml`, `README.md`, `docs/guide.md`, `docs/cli.md`, and the tests DM1 and `ReportCommandTests.StatusText`;
  - Task 5: `JsonRateCardCalculator` (one method), `CliContext` (one property), `RatesCommands`, `DaemonHost`, `ReportCommands`, `CliHarness` (one method), `README.md`, `docs/cli.md`, and `docs/configuration.md`.
- The test project uses xUnit v2 (`xunit` 2.9.3), and tests use only public APIs, with no `InternalsVisibleTo`. That's why `WindowsCommandLine` and `SystemCliPlatform.UnixDetachedStartInfo` (Task 1) and `ReceiptMarkdown` (Task 2) are public.
- When an acceptance criterion says `src/` contains no `double` or `float`, it means the C# keywords and the `GetDouble` and `TryGetDouble` calls.

## Verified facts (retrieved 2026-11-21)

### Detached daemon start

Sources: [.NET 10 `Process.Windows.cs`](https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs); [`ProcessStartInfo.cs`](https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/ProcessStartInfo.cs); [UpdateProcThreadAttribute](https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute); POSIX.1-2024 [`echo`](https://pubs.opengroup.org/onlinepubs/9799919799/utilities/echo.html); Ubuntu 24.04 [dash(1)](https://manpages.ubuntu.com/manpages/noble/man1/dash.1.html). Probes ran on Windows 11 ARM64 (build 28000), PowerShell 7.6, and the minimal MSYS `sh` (bash 5.3) bundled with the Copilot app's Git.

- On Windows, .NET 10's `Process.Start` calls `CreateProcess` with `bInheritHandles = true` (commented `// handle inheritance flag`) and no handle list, so the child inherits every inheritable handle in the parent. `ProcessStartInfo` has no option to limit that. When a shell captures `gh-ledger daemon start`, gh-ledger's standard output is the capture pipe, the daemon inherits it, and the reader waits for the daemon to exit.
- `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` gives `CreateProcess` the list of handles to inherit. The handles must be inheritable, and `bInheritHandles` must be `TRUE`.
- **Reproduced with the merged binary.** A `win-arm64` single-file publish of `main` at `33a2881`, run as `$x = & gh-ledger daemon start --port 54318` in a child `pwsh` with a temporary `COPILOT_LEDGER_HOME`: the capture hadn't returned after 20 seconds, and it returned only after `gh-ledger daemon stop`.
- **Fixed with Task 1's code.** The same capture with Task 1's `SystemCliPlatform` returned in 6.8 seconds (the time the daemon takes to write `daemon.json` on that machine), and `x=$(gh-ledger daemon start --port 54319)` under MSYS `sh` returned in 1.2 seconds with `gh-ledger daemon started (pid 18228) on http://localhost:54319.`. A standalone probe of the same launcher took 0.2 seconds under a PowerShell capture and 0.3 seconds under `sh`, against 8.4 and 6.4 seconds with `Process.Start`. The child saw redirected standard handles: reading standard input returned end-of-file, and writing to standard output and error didn't throw.
- `WindowsCommandLine.Build` was checked through real child processes started by `WindowsDetachedProcess.Start`: one child got WC1's 13 vectors as its arguments and another WC2's six daemon arguments, and each child's `args` matched them exactly.
- POSIX leaves `echo`'s output implementation-defined when an operand contains a backslash. dash, "the standard command interpreter" on Ubuntu, interprets backslash sequences in `echo`; `\c` means "subsequent output is suppressed". So Task 1's script prints the path with `printf '... %s\n'`.
- **Task 1's `/bin/sh` check, probed under MSYS `sh` (bash 5.3).** For a file without execute permission, the script printed `gh-ledger: cannot execute <path>` on standard error and exited 126; a path containing `\cut` was printed whole. For an executable script, it exec'd the script with its arguments intact (`daemon`, `run`, `a b`), and what the script wrote to standard output and error went to the null device. On Windows, `CreateProcessW` for a missing file fails, and `WindowsDetachedProcess.Start` throws a `Win32Exception` whose `NativeErrorCode` is 2 (SD1).
- The analyzers in this repository report CA1416 when `WindowsDetachedProcess.Start` is called without an `OperatingSystem.IsWindows()` check, and nothing for the `DllImport` declarations (SYSLIB1054 is a suggestion).
- While the daemon runs on Windows, its executable can't be opened for writing ("being used by another process") or deleted ("Access … is denied"). That affects `gh extension upgrade` and `gh extension remove` (below).

### GitHub CLI extensions

Sources: GitHub Docs, [Creating GitHub CLI extensions](https://docs.github.com/en/github-cli/github-cli/creating-github-cli-extensions); cli/cli v2.103.3 [`pkg/cmd/extension/manager.go`](https://github.com/cli/cli/blob/v2.103.3/pkg/cmd/extension/manager.go), [`http.go`](https://github.com/cli/cli/blob/trunk/pkg/cmd/extension/http.go), [`pkg/cmd/root/root.go`](https://github.com/cli/cli/blob/v2.103.3/pkg/cmd/root/root.go), [`pkg/cmd/alias/shared/validations.go`](https://github.com/cli/cli/blob/v2.103.3/pkg/cmd/alias/shared/validations.go), and [`pkg/cmd/release/create/create.go`](https://github.com/cli/cli/blob/v2.103.3/pkg/cmd/release/create/create.go); go-gh [`pkg/config/config.go`](https://github.com/cli/go-gh/blob/trunk/pkg/config/config.go); a probe with gh 2.103.3.

- An extension repository's name starts with `gh-`, and the rest is the command name, so `swigerb/gh-ledger` provides `gh ledger`.
- A precompiled extension's release assets end in `<os>-<arch>`, plus `.exe` on Windows; for example `gh-whoami-windows-amd64.exe`. gh picks the first asset of the latest release whose name ends with its own `runtime.GOOS-runtime.GOARCH` and extension. It treats the repository as precompiled when any asset ends with a known platform (`windows-amd64.exe`, `windows-arm64.exe`, `linux-amd64`, `linux-arm64`, `darwin-amd64`, `darwin-arm64`, among others), so other assets such as `rates.json` are ignored.
- On an Apple silicon Mac without a `darwin-arm64` asset, gh falls back to `darwin-amd64` only if Rosetta is installed. No other platform falls back. gh ad-hoc signs a `darwin-arm64` asset with `codesign` after downloading it.
- gh downloads the asset to `<data>/extensions/gh-ledger/gh-ledger` (`gh-ledger.exe` on Windows), with mode 0755, and writes `manifest.yml` next to it (`owner`, `name`, `host`, `tag`, `ispinned`, `path`). It lists a directory with a `manifest.yml` as a binary extension. `<data>` is `$XDG_DATA_HOME/gh` when `XDG_DATA_HOME` is set, else `%LOCALAPPDATA%\GitHub CLI` on Windows, else `~/.local/share/gh`. A probe that installed `github/gh-models` with `XDG_DATA_HOME`, `XDG_STATE_HOME`, and `GH_CONFIG_DIR` pointed at a temporary directory produced exactly `gh\extensions\gh-models\gh-models.exe` and `gh\extensions\gh-models\manifest.yml`.
- gh runs a binary extension directly on every OS; only script extensions go through `sh.exe` on Windows. It sets `GH_EXTENSION=1` and `GH_PATH` for the child (this plan doesn't rely on them, because the sign-in item runs the binary without gh).
- gh registers extension commands before aliases, and an alias whose name is already a command isn't registered. `gh alias set ledger …` fails while the `ledger` extension is installed: the probe printed `X Could not create alias models: already a gh command or extension` and exited 1.
- `gh extension upgrade` rewrites the binary in place (`O_TRUNC`), and `gh extension remove` deletes the directory, so both fail on Windows while the daemon runs from that binary.
- `gh release create <tag> <files>` creates a draft, uploads the files, then publishes it, and deletes the draft if an upload fails. `--verify-tag` aborts when the tag doesn't exist on the remote.

### Releases and downloads

Sources: GitHub Docs, [Linking to releases](https://docs.github.com/en/repositories/releasing-projects-on-github/linking-to-releases); probes with a `net10.0` `HttpClient` (`SocketsHttpHandler { DefaultProxyCredentials = CredentialCache.DefaultCredentials }`) through this machine's corporate proxy.

- `https://github.com/<owner>/<repo>/releases/latest/download/<asset>` downloads that asset of the latest release. "Latest" is the newest release that isn't a draft or prerelease.
- `https://github.com/jqlang/jq/releases/latest/download/sha256sum.txt` answered 200 (`application/octet-stream`, 2,302 bytes) after redirecting to `release-assets.githubusercontent.com`. `https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json` answered 404 with a 21-byte JSON body, because the repository has no release yet. `swigerb/gh-ledger` is public.
- Current major versions of the actions this plan uses: `actions/checkout@v7` (v7.0.1), `actions/setup-dotnet@v6` (v6.1.0), `actions/upload-artifact@v7` (v7.0.2), and `actions/download-artifact@v8` (v8.0.2). Both artifact actions run on `node24`. `upload-artifact` stores a single file under its own name, and `download-artifact` with `merge-multiple: true` puts every artifact's files in one directory.
- `dotnet msbuild <project> -getProperty:Version` prints only the value (`0.4.0`).
- `dotnet publish -c Release -r <rid>` of the Cli project writes `gh-ledger` (`gh-ledger.exe` on Windows) plus `.pdb` files; the SQLite native library is inside the single file.

### GitHub REST API and Actions

Sources: GitHub Docs, [Permissions required for GitHub Apps](https://docs.github.com/en/rest/authentication/permissions-required-for-github-apps), [REST API endpoints for issue comments](https://docs.github.com/en/rest/issues/comments), [API versions](https://docs.github.com/en/rest/about-the-rest-api/api-versions) and [breaking changes](https://docs.github.com/en/rest/about-the-rest-api/breaking-changes), [Events that trigger workflows](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows), [Metadata syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/metadata-syntax), [Workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax), [Variables](https://docs.github.com/en/actions/reference/workflows-and-actions/variables); runner-images READMEs for Ubuntu 24.04, macOS 15 Arm64, and Windows Server 2025; read-only API calls against this repository.

- Installation tokens, including `GITHUB_TOKEN`, need: **Contents: read** for `GET /repos/{owner}/{repo}/contents/{path}`; **Pull requests: read** for `GET /repos/{owner}/{repo}/pulls/{pull_number}/files`; and **Issues** or **Pull requests** (read to list, write to create or update) for `GET`/`POST /repos/{owner}/{repo}/issues/{issue_number}/comments` and `PATCH /repos/{owner}/{repo}/issues/comments/{comment_id}`.
- Issue comments list in ascending ID order, with `per_page` up to 100. A comment has `id`, `body`, `html_url`, and `user` (`login`, or null).
- Requests without `X-GitHub-Api-Version` use `2022-11-28`. The newest version, `2026-03-10`, changed none of the endpoints above except directory listings in "Get repository content", which this plan doesn't use. A version stays supported for at least 24 months after its successor ships.
- A live call to `GET /repos/swigerb/gh-ledger/pulls/34/files` returned objects with `filename` and `status` (`added`). `GET /repos/swigerb/gh-ledger/contents/docs/guide.md?ref=<head sha>` returned `size` 8271 and `encoding` `base64`, with `content` split into lines; `Convert.FromBase64String` decoded it to 8,271 UTF-8 characters.
- "The `GITHUB_TOKEN` has read-only permissions in pull requests from forked repositories." A pull request's `head.repo.full_name` equals the repository for a branch in the same repository.
- A composite action sets `runs.using: composite`; a `run` step needs `shell`; `GITHUB_ACTION_PATH` is the action's directory; and steps can read the `inputs` and `github` contexts. For `shell: pwsh` the runner prepends `$ErrorActionPreference = 'stop'` and appends `if ((Test-Path -LiteralPath variable:\LASTEXITCODE)) { exit $LASTEXITCODE }`. `GITHUB_API_URL` is the API URL, and `GITHUB_OUTPUT` is the step's output file.
- GitHub-hosted runners include PowerShell 7.6.10 (`pwsh`), GitHub CLI, and jq on `ubuntu-latest`, `macos-latest`, and `windows-latest`.
- A workflow can use an action from its own repository with `uses: ./path` after `actions/checkout`. (The newer `$/path` reference needs no checkout; this plan uses the familiar form.)
- For `workflow_dispatch`, `GITHUB_REF` is the "branch or tag that received dispatch", so `github.ref_type` is `tag` for a manual run started from a tag; `github.event_name` tells it apart from a pushed tag (`push`). A manual run is possible only when the workflow file exists on the default branch.

### GitHub Markdown

Source: GitHub's Markdown API (`POST /markdown`, `mode: gfm`, `context: swigerb/gh-ledger`), which renders as comments do and posts nothing.

- In plain text, `@octocat` becomes a mention link and `#1` an issue link. Inside a code span, neither does, and `<b>`, backticks, and URLs stay literal: `` `` feat/@octocat-#1-<b>x</b>`tick`-https://example.com `` `` rendered as one `<code>` element.
- Task 2's whole sample receipt rendered as intended: a level-3 heading, a five-item list, a `<details>` block whose `<summary>` is `Details and data`, a right-aligned table, the footer paragraphs, and a highlighted JSON block.
- In a table cell, `` `a\|b` `` renders as the code `a|b`.

### .NET

Sources: [HttpClient.DefaultProxy](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient.defaultproxy), [SocketsHttpHandler.DefaultProxyCredentials](https://learn.microsoft.com/dotnet/api/system.net.http.socketshttphandler.defaultproxycredentials); probes.

- `HttpClient.DefaultProxy` reads `HTTP_PROXY`, `HTTPS_PROXY`, `ALL_PROXY`, and `NO_PROXY`; without them it uses the user's proxy settings on Windows and the system's on macOS, and no proxy on Linux. `SocketsHttpHandler.DefaultProxyCredentials` supplies credentials to that default proxy.
- `HttpClient` treats a `TaskCanceledException` from its handler, when the caller's token isn't canceled, as a timeout and throws `TaskCanceledException`.
- In PowerShell 7, `catch [System.Text.Json.JsonException]` and `catch [System.Text.DecoderFallbackException]` catch those exceptions from .NET method calls. A function named like a cmdlet (`Invoke-WebRequest`) that's defined in a calling script takes precedence over the cmdlet in a script that script runs with `&`.
- `Write-Output` inside a function whose output is assigned to a variable goes into that variable; `Write-Host` doesn't, and `*>&1` captures it.
- **PowerShell compares strings with culture rules.** In PowerShell 7.6.10, each of these was `True`: `"feature/ledger$([char] 0x200B)" -ceq 'feature/ledger'` (a zero-width space), `"$([char] 0xFEFF)<!-- copilot-ledger-receipt -->" -ceq '<!-- copilot-ledger-receipt -->'` (a byte order mark), `"fea$([char] 0xAD)ture" -ceq 'feature'` (a soft hyphen), and `"$([char] 0xE9)" -ceq "e$([char] 0x301)"` (precomposed and combining forms). `[string]::Equals(a, b, [System.StringComparison]::Ordinal)` returned `False` for the first two, and `[System.Array]::IndexOf` on a `string[]` compares ordinally. PowerShell passes `$null` to a .NET `string` parameter as `""`: `[string]::Equals($null, '', [System.StringComparison]::Ordinal)` is `True`.

### Assumptions (not verified)

- **The comment size limit.** The REST docs don't state a body limit. GitHub's API rejects longer comments with `Body is too long (maximum is 65536 characters)` (reported in [community discussion #27190](https://github.com/orgs/community/discussions/27190)). The action and `export-pr` reject receipts over 65,536 bytes of UTF-8, which is never more characters than that.
- **macOS binaries.** The release workflow builds `osx-x64` and `osx-arm64` on `macos-latest`, where the .NET SDK signs the app host, and smoke-tests `osx-arm64` there. A manual run of the workflow (Task 4's verification) checks this before the first tag.
- **Linux arm64 and Windows arm64 binaries** are cross-published and not run in CI.
- **Task 1's `/bin/sh` check under dash.** The script ran only under MSYS `sh` (bash). SD1's Linux branch, which runs it through `/bin/sh` (dash on `ubuntu-latest`), first runs in Task 1's CI.
- **Locked binaries on Linux and macOS.** Whether `gh extension upgrade` can replace a running binary there wasn't tested; the docs tell everyone to stop the daemon first.

## Design decisions

### Daemon handles (Task 1)

- **Windows:** `WindowsDetachedProcess.Start` opens one inheritable handle to the NUL device (`File.OpenHandle(@"\\.\NUL", …, FileShare.ReadWrite | FileShare.Inheritable)`), uses it as the daemon's standard input, output, and error (`STARTF_USESTDHANDLES`), and passes it as the only entry of `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` to `CreateProcessW` with `EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW`. The daemon inherits nothing else, whatever gh-ledger itself inherited. `CREATE_NO_WINDOW` keeps the current behavior: a console without a window, so Windows still sends the daemon shutdown events.
- **Rejected for Windows:** clearing the inherit flag on gh-ledger's own standard handles (it misses inheritable handles gh-ledger got from its parent but doesn't use as standard handles); redirecting the three streams to pipes (`bInheritHandles` stays `TRUE`, so everything else still leaks); and `UseShellExecute = true` (no handle control, and it can show the "Open File – Security Warning" prompt for a downloaded binary).
- **Linux and macOS:** start `/bin/sh` with `-c`, this script, the gh-ledger path (the script's `$0`), and `daemon run …`:

  ```shell
  [ -x "$0" ] || { printf 'gh-ledger: cannot execute %s\n' "$0" >&2; exit 126; }; exec "$0" "$@" </dev/null >/dev/null 2>&1
  ```

  The redirections replace the inherited standard descriptors before the daemon runs, and `exec` keeps the process ID, so `daemon start`'s check of `daemon.json` still matches. .NET opens its own descriptors close-on-exec.
- **A daemon binary that can't run fails loudly.** A failed `exec` would report to the null device, because the redirections come first; `Process.Start` used to throw instead. So the script first checks that the file is executable; if not, it writes `gh-ledger: cannot execute <path>` to the standard error it inherited (the caller's) and exits 126, the shell's code for a command that can't be executed. `sh` exits at once, so no caller's pipe stays open, and `daemon start` then reports that the daemon didn't start, as before. It prints with `printf`, because dash's `echo` interprets backslashes. On Windows, `CreateProcessW` fails synchronously and `WindowsDetachedProcess.Start` throws a `Win32Exception`, as `Process.Start` did.
- The command line is built by `WindowsCommandLine.Build`, which quotes like .NET's `ArgumentList` (the C runtime's rules) and is unit-tested on every OS.

### The receipt and how it reaches the pull request (Task 2)

- **The handoff is a committed file.** The ledger lives on the developer's machine, and a CI runner has none, so the receipt must travel with the pull request. `export-pr` writes `.github/copilot-ledger/receipts/<name>-<hash>.md` in the repository; the developer commits it on the branch; the action reads it from the pull request. Each step is observable: the file is in the diff, the action logs what it found, and the comment shows the same Markdown. Rejected: the pull request body (manual and easy to clobber), Git notes (gh-ledger never runs `git`, and checkouts don't fetch notes), workflow artifacts (they need a workflow run on the developer's side), and any upload (no cloud hosting).
- **Per-branch file names** keep concurrent pull requests from conflicting. The name is the branch with every character outside `[A-Za-z0-9._-]` replaced by `-`, cut to 64 characters, plus the first 8 hex digits of the branch's SHA-256, so `feat/x` and `feat-x`, or `Feat/X` and `feat/x` on a case-insensitive file system, never collide, and Windows device names such as `con` can't occur. The action matches receipts by the branch inside them, not by file name, so it never recomputes the name.
- **The Markdown file is the comment.** The action posts it unchanged, so it does no formatting or money math. The JSON data travels in a fenced `json` block inside `<details>`. JSON lines can never close the fence: every line starts with `{`, `}`, `]`, or spaces and `"`.
- **User text is shown in code spans** (branch, repository, models), so GitHub never turns it into mentions, issue links, or HTML. Fixed text needs no escaping.
- **Exact reconciliation.** The receipt's totals are the branch's row from `GetRollupAsync(LedgerGrouping.Branch, { Repository, Branch })`: the same query, keys, and filter as `gh-ledger branch --since all`. EP4 compares all 10 fields of `export-pr --json` and `branch --since all --json`, decimals by value.
- **All recorded spend on the branch.** A receipt covers every span on the branch, with no `--since`, and says so through its check command. If a branch name is reused, the receipt includes the earlier spend (deferred: `--since`).
- **Terms.** A *turn* is one model request: one stored inference span. *Max single-prompt burn* is the costliest prompt chain's billed spend, `(AiCreditsUsd + FoundryByokUsd) / 0.01`, in AI credits: the same measure that orders `top-prompts` and that the breaker's velocity rule counts, with BYOK at $0.01 per credit. *Breaker trips* counts the branch's trips, open or reset.
- **At most 65,536 bytes.** The action rejects a larger receipt, and GitHub's comment limit is 65,536 characters, so `export-pr` refuses one too, with or without `--stdout`, and the developer finds out before pushing instead of from a failed job. `--json` still writes the data.
- **The repository must have a GitHub `origin`.** Without a slug, the ledger key is a local path, which mustn't be committed or posted, so `export-pr` refuses.

### Signed receipts and a pre-push hook (deferred)

- **Signing** can't prove the spend is right: the developer controls the ledger that produced it, so a signature would only show who produced the receipt, and the pushed (optionally signed) commit already shows that. A real key scheme would also need key distribution, which needs hosting. Deferred.
- **A pre-push hook** runs after the commits being pushed are fixed, so a receipt it writes can't be in that push; it would always lag one push behind. A pre-commit hook would rewrite the receipt in every commit and need gh-ledger to edit `.git/hooks` or `core.hooksPath`, which conflicts with hook managers such as Husky. An explicit `export-pr` lets the developer decide when spend is published. Deferred.

### The action (Task 3)

- **Composite action, PowerShell 7, no dependencies.** `action/action.yml` runs `action/receipt.ps1` with `shell: pwsh`. It calls the REST API with `Invoke-WebRequest` and parses responses with `System.Text.Json`, so strings stay exact: no jq number coercion, and no `ConvertFrom-Json` turning a date-like branch name into a `DateTime`. It uses no third-party or `actions/*` action. PowerShell 7.6 is on every GitHub-hosted runner, and the tests run offline by defining a function named `Invoke-WebRequest`.
- **Permissions: `contents: read` and `pull-requests: write`.** This is a deliberate deviation from "only `pull-requests: write`": reading the receipt file at the pull request's head needs Contents read, by GitHub's permission table. The example workflow requests both in its `permissions` block, so it works whichever default token permissions the repository uses, and the token gets nothing else.
- **Finding the receipt:** list the pull request's changed files (paged, 100 per page, up to 30 pages), keep those under `receipts-directory` that end in `.md` and weren't removed, fetch each at the head SHA, and pick the one whose `branch` equals the pull request's head branch, compared ordinally: case-sensitively, with every character counted.
- **Validating it:** at most 65,536 bytes, valid UTF-8, the marker `<!-- copilot-ledger-receipt -->` as its first line (with nothing before it, not even a byte order mark), a `json` block, `"schemaVersion": 1`, and a string `branch`. A file that fails is an error that fails the job and names the file; when no receipt is for the head branch, the error lists the branches it found. No receipt is a notice, not a failure.
- **One comment:** the oldest comment by `comment-author` (default `github-actions[bot]`, compared case-insensitively, as GitHub logins are) whose body starts with the marker is updated; with none, a comment is created; when the body is already identical (ignoring `\r`), nothing is sent. Others' comments are never edited. The example workflow's `concurrency` group per pull request prevents two runs from both creating a comment.
- **Exact comparisons.** `receipt.ps1` compares strings only with `[string]::Equals` and an ordinal `StringComparison`, or ordinal `StartsWith` and `EndsWith`, never with `-eq`, `-ceq`, `-like`, or `-match`. PowerShell's operators use culture rules, which ignore zero-width characters and a byte order mark, so a receipt for `feature/ledger` plus a zero-width space would be posted on `feature/ledger`, and a receipt starting with a byte order mark would pass the marker check and then never match its own comment, adding a comment on every run.
- **Events:** only `pull_request`. Fork pull requests are skipped with a notice, because their token is read-only. `pull_request_target` fails with an error: it runs with a write token on untrusted input, and posting a fork author's Markdown as the bot isn't worth that risk.
- **Location:** `action/` in this repository, used as `swigerb/gh-ledger/action@<ref>`, per the user's layout. A separate repository for a Marketplace listing is deferred.

### The gh extension (Task 4)

- **Release assets** follow gh's convention: `gh-ledger-windows-amd64.exe`, `gh-ledger-windows-arm64.exe`, `gh-ledger-linux-amd64`, `gh-ledger-linux-arm64`, `gh-ledger-darwin-amd64`, and `gh-ledger-darwin-arm64`, plus `rates.json` and `gh-ledger-checksums.txt` (SHA-256), which gh ignores. Each OS's binaries are built on that OS's runner. The three that the runners can run (`win-x64`, `linux-x64`, and `osx-arm64`) are smoke-tested: `--version` equals the project version. A tag `v*` must equal `v` plus the project version. The release job runs only when a tag is pushed (`github.event_name == 'push' && github.ref_type == 'tag'`), so a manual (`workflow_dispatch`) run builds and smoke-tests without releasing, even when it's started from a tag. `run` scripts read matrix values from environment variables, never from `${{ }}`.
- **Detecting the extension:** gh-ledger is running as the installed extension when its path is `…/extensions/gh-ledger/gh-ledger` (or `gh-ledger.exe`) and `manifest.yml` exists in that directory, the layout gh creates. It doesn't depend on `GH_EXTENSION`, which the sign-in item doesn't set.
- **`install` as the extension** skips the alias (gh would refuse it, and the extension already provides `gh ledger`) and the `sh.exe` warning (binary extensions don't use `sh`). The sign-in item already uses the running executable, which is the extension's binary; `gh extension upgrade` replaces it in place, so the item stays valid. An alias set by an earlier standalone install is left recorded, and `uninstall` removes it as before.
- **`status` as the extension** shows `gh-ledger <version> (GitHub CLI extension)`, names `gh ledger daemon start` and `gh ledger install` in its hints, and reports `"ghExtension": true`. In either mode it warns when `install.json` names a different executable, for example after switching from a downloaded binary to the extension: `CopilotLedger was installed from <path>; run <command> install to use this copy instead.`
- Other messages that name `gh-ledger` (breaker, MCP, alerts, sign-in warnings) don't change this sprint (deferred).
- **Upgrading:** Windows locks the running daemon's binary, so the docs say to run `gh ledger daemon stop` before `gh extension upgrade ledger` or `gh extension remove ledger`, and `gh ledger uninstall` before removing the extension.

### `rates update` (Task 5)

- **Source:** `https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json`, which Task 4's workflow attaches to every release. A release asset is the `rates.json` that passed CI with that release's code, it changes only when a release is published, and it doesn't depend on where the file sits in the source tree. A raw URL on `main` would serve unreleased rates, possibly in a schema older binaries can't read, and would break if the file moved.
- **Storage and precedence:** a newer file is saved as `<data>/rates.downloaded.json` (temporary file, then `File.Move`). The base rates are the downloaded file when it's valid and its `ratesVersion` date is later than the built-in one, otherwise the built-in rates; the user's overlay (`<data>/rates.json`) is merged on top as before. So upgrading gh-ledger to newer built-in rates makes an old download inert, and a broken download falls back to the built-in rates with a warning instead of stopping the daemon.
- **Versions are dates.** A downloaded `ratesVersion` must be `yyyy-MM-dd`; comparison is by date. Equal means up to date, older is reported and ignored, and only newer is saved.
- **The overlay must still merge.** If the newer rates conflict with the user's overlay (for example, they add a model the overlay also adds), nothing is saved, and the error says why.
- **HTTP:** `SocketsHttpHandler { DefaultProxyCredentials = CredentialCache.DefaultCredentials }`, so the system proxy, including an authenticating corporate proxy, works. A 30-second timeout, a 1 MiB response cap, and the user agent `gh-ledger/<version>`. The body is read as bytes, so the content type and charset don't matter. No new packages.
- **The daemon reads rates at start,** so after an update with the daemon running, `rates update` says to restart it.

## Task 1 specification: Detached Daemon Start

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/WindowsCommandLine.cs` | New, copy exactly |
| `src/CopilotLedger.Cli/WindowsDetachedProcess.cs` | New, copy exactly |
| `src/CopilotLedger.Cli/SystemCliPlatform.cs` | Replace `StartDetached`; add `UnixDetachedStartInfo` |
| `src/CopilotLedger.Cli/ICliPlatform.cs` | Replace the comment on `StartDetached` |
| `.github/workflows/ci.yml` | Capture `daemon start` in the smoke test; add a PowerShell step |
| `docs/cli.md` | One sentence in "The daemon" |
| `tests/CopilotLedger.Tests/Cli/WindowsCommandLineTests.cs` | New (Task 1 tests) |
| `tests/CopilotLedger.Tests/Cli/StartDetachedTests.cs` | New (Task 1 tests) |

No project file changes: `DllImport` needs neither `AllowUnsafeBlocks` nor a package.

### `WindowsCommandLine.cs` (copy exactly)

```csharp
using System.Text;

namespace CopilotLedger.Cli;

// Builds a CreateProcess command line that the C runtime and CommandLineToArgvW split back into fileName and arguments.
// fileName is always quoted (Windows paths can't contain '"'). Each argument is written as is when it is non-empty and has
// no space, tab, newline, vertical tab, or '"'; otherwise it is quoted, a '"' becomes \", and backslashes are doubled
// when they come right before a '"' or the closing quote.
public static class WindowsCommandLine
{
    public static string Build(string fileName, IReadOnlyList<string> arguments)
    {
        StringBuilder builder = new();
        builder.Append('"').Append(fileName).Append('"');
        foreach (string argument in arguments)
        {
            builder.Append(' ');
            if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0)
            {
                builder.Append(argument);
                continue;
            }

            builder.Append('"');
            int backslashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', (backslashes * 2) + 1);
                }
                else
                {
                    builder.Append('\\', backslashes);
                }

                backslashes = 0;
                builder.Append(character);
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');
        }

        return builder.ToString();
    }
}
```

### `WindowsDetachedProcess.cs` (copy exactly)

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace CopilotLedger.Cli;

// Starts a process that inherits exactly one handle: an inheritable handle to the NUL device, used as its standard
// input, output, and error. Process.Start passes bInheritHandles = TRUE without a handle list, so the child would
// inherit every inheritable handle in this process, including the caller's output pipe, and a caller that reads that
// pipe to the end (x=$(gh-ledger daemon start)) would wait until the daemon exits.
[SupportedOSPlatform("windows")]
internal static class WindowsDetachedProcess
{
    private const int StartfUseStdHandles = 0x00000100;
    private const uint CreateNoWindow = 0x08000000;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const int ErrorInsufficientBuffer = 122;
    private static readonly IntPtr ProcThreadAttributeHandleList = 0x00020002;

    public static int Start(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        using SafeFileHandle nul = File.OpenHandle(
            @"\\.\NUL",
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Inheritable);
        IntPtr size = IntPtr.Zero;
        if (InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size)
            || Marshal.GetLastPInvokeError() != ErrorInsufficientBuffer)
        {
            throw new Win32Exception();
        }

        IntPtr attributeList = Marshal.AllocHGlobal(size);
        IntPtr handleList = Marshal.AllocHGlobal(IntPtr.Size);
        IntPtr commandLine = Marshal.StringToHGlobalUni(
            WindowsCommandLine.Build(fileName, arguments));
        bool initialized = false;
        try
        {
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
            {
                throw new Win32Exception();
            }

            initialized = true;
            Marshal.WriteIntPtr(handleList, nul.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ProcThreadAttributeHandleList,
                    handleList,
                    IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception();
            }

            StartupInfoEx startupInfo = new()
            {
                StartupInfo = new StartupInfo
                {
                    Size = Marshal.SizeOf<StartupInfoEx>(),
                    Flags = StartfUseStdHandles,
                    StandardInput = nul.DangerousGetHandle(),
                    StandardOutput = nul.DangerousGetHandle(),
                    StandardError = nul.DangerousGetHandle(),
                },
                AttributeList = attributeList,
            };
            if (!CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,
                    CreateNoWindow | ExtendedStartupInfoPresent,
                    IntPtr.Zero,
                    workingDirectory,
                    ref startupInfo,
                    out ProcessInformation process))
            {
                throw new Win32Exception();
            }

            CloseHandle(process.Thread);
            CloseHandle(process.Process);
            return process.ProcessId;
        }
        finally
        {
            if (initialized)
            {
                DeleteProcThreadAttributeList(attributeList);
            }

            Marshal.FreeHGlobal(commandLine);
            Marshal.FreeHGlobal(handleList);
            Marshal.FreeHGlobal(attributeList);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        int attributeCount,
        int flags,
        ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        uint flags,
        IntPtr attribute,
        IntPtr value,
        IntPtr size,
        IntPtr previousValue,
        IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(
        string? applicationName,
        IntPtr commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Size;
        public IntPtr Reserved2;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public int ProcessId;
        public int ThreadId;
    }
}
```

### `SystemCliPlatform` (copy exactly)

Replace the whole `StartDetached` method with these two methods. `UnixDetachedStartInfo` is public so that SD1 can run the same `/bin/sh` command with standard error redirected.

```csharp
    public int StartDetached(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsDetachedProcess.Start(fileName, arguments, workingDirectory);
        }

        using Process process = new() { StartInfo = UnixDetachedStartInfo(fileName, arguments, workingDirectory) };
        process.Start();
        return process.Id;
    }

    // /bin/sh first checks that fileName can be executed: if not, it says so on the caller's standard error and exits
    // with 126, because a failed exec would report to the null device. Otherwise exec keeps the process id, and the
    // redirections replace the inherited standard handles before the daemon runs. printf, not echo: dash's echo
    // interprets backslashes in the path.
    public static ProcessStartInfo UnixDetachedStartInfo(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        ProcessStartInfo startInfo = new("/bin/sh")
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(
            """[ -x "$0" ] || { printf 'gh-ledger: cannot execute %s\n' "$0" >&2; exit 126; }; exec "$0" "$@" </dev/null >/dev/null 2>&1""");
        startInfo.ArgumentList.Add(fileName);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
```

The `if (OperatingSystem.IsWindows())` check is what satisfies CA1416 for the Windows-only class; keep it in that form. The script is a one-line C# raw string literal (`"""…"""`), so its `\n` reaches `printf` as a backslash and an `n`.

### `ICliPlatform` (copy exactly)

Replace the `StartDetached` line with:

```csharp
    int StartDetached(string fileName, IReadOnlyList<string> arguments, string workingDirectory);   // returns the new process id; its standard input, output, and error are the null device; on Windows it inherits no other handle
```

### CI

In `.github/workflows/ci.yml`, step "Smoke-test the published binary":

1. Add `timeout-minutes: 5` after `shell: bash`.
2. Replace the line `./artifacts/gh-ledger/gh-ledger daemon start` with these three lines, at the same indentation:

   ```shell
   started=$(./artifacts/gh-ledger/gh-ledger daemon start | tr -d '\r')
   echo "$started"
   grep -Eq '^gh-ledger daemon started \(pid [0-9]+\) on http://localhost:4318\.$' <<< "$started"
   ```

Then add this step after it, at the end of the job:

```yaml
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

Before this task, both captures wait for the daemon to exit, so they run into the 5-minute limit; with it, they return in seconds. The PowerShell step uses its own data directory and port, so it can't collide with the bash step.

### `docs/cli.md`

In "## The daemon", append this sentence to the first paragraph:

```markdown
`daemon start` gives the daemon the null device as its standard input, output, and error, so a script can capture its output, for example `started=$(gh-ledger daemon start)`, and go on while the daemon runs.
```

### Acceptance criteria

- `StartDetached` calls `WindowsDetachedProcess.Start` on Windows and starts `UnixDetachedStartInfo`'s `/bin/sh` command elsewhere. Apart from those two methods, nothing in `SystemCliPlatform` changes.
- On Linux and macOS, a daemon binary that can't be executed makes `sh` write `gh-ledger: cannot execute <path>` to the caller's standard error and exit 126; on Windows, `StartDetached` throws a `Win32Exception` (SD1).
- `WindowsCommandLine` is public; `WindowsDetachedProcess` is internal and marked `[SupportedOSPlatform("windows")]`.
- `dotnet build` reports no warnings, and `dotnet test` passes 592 tests (577 + 15).
- CI passes on `ubuntu-latest` and `windows-latest`, including both capture checks.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r <rid> -o artifacts/gh-ledger
```

Then, with `COPILOT_LEDGER_HOME` set to a new temporary directory, run `$x = & ./artifacts/gh-ledger/gh-ledger daemon start --port 54318` in PowerShell (or `x=$(./artifacts/gh-ledger/gh-ledger daemon start --port 54318)` in bash). It must return within 15 seconds, and `$x` must be the `gh-ledger daemon started …` line. Then run `./artifacts/gh-ledger/gh-ledger daemon stop`. Report the passed, failed, and skipped counts.

## Task 2 specification: PR Receipts: `export-pr`

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Storage/LedgerGrouping.cs` | Add `ChannelModel` |
| `src/CopilotLedger.Storage/LedgerRollupRow.cs` | Add `Channel` |
| `src/CopilotLedger.Storage/SqliteLedgerStore.cs` | Channel-and-model rollup, `GetPromptCountAsync`, `CountBreakerTripsAsync` |
| `src/CopilotLedger.Cli/Receipts/PullRequestReceipt.cs` | New, copy exactly |
| `src/CopilotLedger.Cli/Receipts/ReceiptMarkdown.cs` | New, copy exactly |
| `src/CopilotLedger.Cli/Receipts/ReceiptCommands.cs` | New, copy exactly |
| `src/CopilotLedger.Cli/GhLedgerCli.cs` | Register `export-pr` |
| `docs/cli.md` | New section "Pull request receipts" |
| `tests/CopilotLedger.Tests/TestSupport/ReceiptSeeds.cs` | New (Task 2 tests) |
| `tests/CopilotLedger.Tests/TestSupport/FileRetry.cs` | Add `ReadAllBytesAsync` (Task 2 tests) |
| `tests/CopilotLedger.Tests/Storage/ReceiptQueryTests.cs` | New (Task 2 tests) |
| `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs` | New (Task 2 tests) |

### Storage contracts (copy exactly)

`LedgerGrouping` gets a fifth member, after `Agent`:

```csharp
    ChannelModel,
```

`LedgerRollupRow` gets `using CopilotLedger.Core.Pricing;` at the top (before `namespace`, followed by a blank line) and this property directly after `Agent`:

```csharp
    public BillingChannel? Channel { get; init; }
```

`Channel` is set only for `ChannelModel` rows; `Model` is set for `Model` and `ChannelModel` rows.

### Store changes (copy exactly)

1. In `GetRollupSql`, add this arm directly after the `LedgerGrouping.Agent` arm:

   ```csharp
               LedgerGrouping.ChannelModel =>
                   ("""
                    billing_channel AS channel,
                             COALESCE(model_id, '(none)') AS model
                    """,
                    "channel, model"),
   ```

   The rest of the SQL doesn't change, so rows are ordered by billed spend descending, then savings descending, then `channel` and `model` ascending (by their stored strings: `foundry_byok`, `github_ai_credits`, `local`, `unpriced`).
2. In `GetRollupAsync`, replace the `Model = …` and `Agent = …` initializers with:

   ```csharp
                   Model = grouping is LedgerGrouping.Model or LedgerGrouping.ChannelModel
                       ? reader.GetString(reader.GetOrdinal("model"))
                       : null,
                   Agent = grouping == LedgerGrouping.Agent
                       ? reader.GetString(reader.GetOrdinal("agent"))
                       : null,
                   Channel = grouping == LedgerGrouping.ChannelModel
                       ? ToBillingChannel(reader.GetString(reader.GetOrdinal("channel")))
                       : null,
   ```
3. Add this method directly before `private static string ToDatabaseString(PricingStatus status)`:

   ```csharp
       private static BillingChannel ToBillingChannel(string value) => value switch
       {
           "github_ai_credits" => BillingChannel.GitHubAiCredits,
           "foundry_byok" => BillingChannel.FoundryByok,
           "local" => BillingChannel.Local,
           "unpriced" => BillingChannel.Unpriced,
           _ => throw new InvalidDataException($"Unknown billing channel '{value}'."),
       };
   ```

   An unknown stored value is a damaged ledger, so it throws `InvalidDataException`, as `ReadTripsAsync` does for an unknown rule.
4. Add this method directly after `GetTopPromptChainsAsync` (before `OpenConnectionAsync`):

   ```csharp
       // Distinct trace_id values (prompts) among the spans that match the filter.
       public async Task<long> GetPromptCountAsync(
           LedgerFilter filter,
           CancellationToken cancellationToken = default)
       {
           EnsureInitialized();
           await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
           await using SqliteCommand command = connection.CreateCommand();
           command.CommandText = """
               SELECT COUNT(DISTINCT trace_id)
               FROM spans
               WHERE (@from IS NULL OR start_time_unix_ms >= @from)
                 AND (@to IS NULL OR start_time_unix_ms < @to)
                 AND (@repository IS NULL OR COALESCE(repo_slug, repo_root, '(none)') = @repository)
                 AND (@branch IS NULL OR COALESCE(branch_name, '(none)') = @branch);
               """;
           command.Parameters.AddWithValue("@from", OrDbNull(filter.From?.ToUnixTimeMilliseconds()));
           command.Parameters.AddWithValue("@to", OrDbNull(filter.To?.ToUnixTimeMilliseconds()));
           command.Parameters.AddWithValue("@repository", OrDbNull(filter.Repository));
           command.Parameters.AddWithValue("@branch", OrDbNull(filter.Branch));
           return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
       }
   ```
5. Add this method directly after `GetUnnotifiedBreakerTripsAsync`:

   ```csharp
       // Trips, open or reset, whose repository and branch keys equal the arguments (ordinal).
       public async Task<long> CountBreakerTripsAsync(
           string repository,
           string branch,
           CancellationToken cancellationToken = default)
       {
           EnsureInitialized();
           await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
           await using SqliteCommand command = connection.CreateCommand();
           command.CommandText = """
               SELECT COUNT(*) FROM breaker_trips WHERE repository = @repository AND branch = @branch;
               """;
           command.Parameters.AddWithValue("@repository", repository);
           command.Parameters.AddWithValue("@branch", branch);
           return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
       }
   ```

### The receipt

`export-pr` resolves Git with `new FileSystemGitContextResolver().Resolve(context.WorkingDirectory)`, as `branch` does, and fails without writing anything, exit code 1 and nothing on `Out`, in this order:

| Condition | `Error` |
|---|---|
| No repository | `<working directory> isn't in a Git repository.` |
| `BranchName` is null | `HEAD is detached. Check out the pull request's branch, then run export-pr again.` |
| `HeadCommitSha` is null | `<branch> has no commits yet.` |
| `RepositorySlug` is null | `export-pr needs a GitHub remote named origin, and <repository root> has none.` |

The receipt's data, with the filter `{ Repository = slug, Branch = branch }`:

| Field | Source |
|---|---|
| `Totals` | The single row of `GetRollupAsync(LedgerGrouping.Branch, filter)`, or all zeros without one |
| `Models` | `GetRollupAsync(LedgerGrouping.ChannelModel, filter)`, in its order |
| `Prompts` | `GetPromptCountAsync(filter)` |
| `MaxPromptCredits` | `(AiCreditsUsd + FoundryByokUsd) / AiCreditMath.UsdPerAiCredit` of `GetTopPromptChainsAsync(filter, 1)`'s row, or 0 |
| `BreakerTrips` | `CountBreakerTripsAsync(slug, branch)` |
| `GeneratedAt` | `context.TimeProvider.GetUtcNow()` |

The Markdown, built by `ReceiptMarkdown.ToMarkdown` with `\n` line breaks on every OS:

1. The marker line `<!-- copilot-ledger-receipt -->`, then `### CopilotLedger receipt for <branch>` and a blank line. Every branch, repository, model, and commit is shown with `ReceiptMarkdown.Code`.
2. A list:
   - `**GitHub AI Credits:**` the branch's credits and USD, `across <n> turn(s)` (`, <m> unpriced` when some of those turns are unpriced), and each GitHub model's credits; or `none` without GitHub rows.
   - `**Foundry BYOK:**` the BYOK USD the same way, or `none`.
   - `**Local offload:**` input plus output tokens, the turns, `($0.00 billed • <saved> cloud spend avoided)`, and each local model's tokens; or `none`.
   - `**Not priced:**` the turns and models of the `unpriced` channel, only when it has rows.
   - `**Circuit breaker:**` `<n> trip(s) • max single-prompt burn: <credits> credits`.
3. `<details>` with `<summary>Details and data</summary>`: a table (Billing, Model, Turns, Unpriced, Input tokens, Output tokens, Cost, Cloud spend avoided) with one row per `Models` row, or `No spans are recorded for this branch.`; a footer with the repository, prompts, the first 12 characters of the commit, and `generated <yyyy-MM-dd HH:mm> UTC by gh-ledger <version>`; the check command; and the JSON from `ToJson` in a fenced `json` block.

Money and counts use `LedgerFormat` (`Credits`, `Usd`, `Count`), so they round half away from zero to cents, exactly like the CLI tables. EP1 shows a whole receipt.

A receipt whose Markdown is more than `ReceiptMarkdown.MaximumBytes` (65,536) bytes of UTF-8 is refused before anything is written, with `--stdout` or without: exit code 1, nothing on `Out`, and `The receipt for <branch> is <size> bytes; a receipt can be at most 65536 bytes.` on `Error`. The action rejects such a receipt, and GitHub's comment limit is 65,536 characters. `--json` isn't limited.

### `PullRequestReceipt.cs` (copy exactly)

```csharp
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Receipts;

internal sealed record PullRequestReceipt
{
    public required string Repository { get; init; }                       // GitWorkspaceContext.RepositorySlug
    public required string Branch { get; init; }                           // GitWorkspaceContext.BranchName
    public required string Commit { get; init; }                           // GitWorkspaceContext.HeadCommitSha
    public required DateTimeOffset GeneratedAt { get; init; }              // context.TimeProvider.GetUtcNow()
    public required LedgerRollupRow Totals { get; init; }                  // the branch's Branch rollup row; all zero without spans
    public required IReadOnlyList<LedgerRollupRow> Models { get; init; }   // ChannelModel rollup rows, in GetRollupAsync order
    public required long Prompts { get; init; }                            // GetPromptCountAsync
    public required decimal MaxPromptCredits { get; init; }                // costliest prompt chain: (AiCreditsUsd + FoundryByokUsd) / 0.01m; 0 without spans
    public required long BreakerTrips { get; init; }                       // CountBreakerTripsAsync
}
```

### `ReceiptMarkdown.cs` (copy exactly)

````csharp
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Receipts;

// Renders a receipt as the Markdown file that export-pr writes and the action posts. Every line ends with "\n" on every OS.
public static class ReceiptMarkdown
{
    public const string Marker = "<!-- copilot-ledger-receipt -->";
    public const string ReceiptsDirectory = ".github/copilot-ledger/receipts";
    public const int MaximumBytes = 65536;   // as in action/receipt.ps1; keeps a receipt within GitHub's 65,536-character comment limit

    internal static string ToJson(PullRequestReceipt receipt)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions
            {
                Indented = true,
                NewLine = "\n",
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("repository", receipt.Repository);
            writer.WriteString("branch", receipt.Branch);
            writer.WriteString("commit", receipt.Commit);
            LedgerFormat.WriteTime(writer, "generatedAt", receipt.GeneratedAt);
            writer.WriteString("generator", $"gh-ledger {GhLedgerCli.Version}");
            writer.WritePropertyName("totals");
            writer.WriteStartObject();
            LedgerFormat.WriteTotals(writer, receipt.Totals);
            writer.WriteEndObject();
            writer.WriteNumber("prompts", receipt.Prompts);
            LedgerFormat.WriteMoney(writer, "maxPromptCredits", receipt.MaxPromptCredits);
            writer.WriteNumber("breakerTrips", receipt.BreakerTrips);
            writer.WriteStartArray("models");
            foreach (LedgerRollupRow row in receipt.Models)
            {
                writer.WriteStartObject();
                writer.WriteString("channel", ChannelId(row.Channel!.Value));
                writer.WriteString("model", row.Model);
                LedgerFormat.WriteTotals(writer, row);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static string ToMarkdown(PullRequestReceipt receipt)
    {
        LedgerRollupRow totals = receipt.Totals;
        LedgerRollupRow[] github = Rows(receipt, BillingChannel.GitHubAiCredits);
        LedgerRollupRow[] byok = Rows(receipt, BillingChannel.FoundryByok);
        LedgerRollupRow[] local = Rows(receipt, BillingChannel.Local);
        LedgerRollupRow[] unpriced = Rows(receipt, BillingChannel.Unpriced);
        StringBuilder text = new();
        text.Append(Marker).Append('\n');
        text.Append("### CopilotLedger receipt for ").Append(Code(receipt.Branch)).Append("\n\n");

        text.Append("- **GitHub AI Credits:** ");
        if (github.Length == 0)
        {
            text.Append("none\n");
        }
        else
        {
            text.Append(LedgerFormat.Credits(totals.AiCredits)).Append(" credits (")
                .Append(LedgerFormat.Usd(totals.AiCreditsUsd)).Append(") across ")
                .Append(Turns(github)).Append(": ")
                .Append(string.Join(", ", github.Select(row => $"{Code(row.Model!)} {LedgerFormat.Credits(row.AiCredits)}")))
                .Append('\n');
        }

        text.Append("- **Foundry BYOK:** ");
        if (byok.Length == 0)
        {
            text.Append("none\n");
        }
        else
        {
            text.Append(LedgerFormat.Usd(totals.FoundryByokUsd)).Append(" across ")
                .Append(Turns(byok)).Append(": ")
                .Append(string.Join(", ", byok.Select(row => $"{Code(row.Model!)} {LedgerFormat.Usd(row.FoundryByokUsd)}")))
                .Append('\n');
        }

        text.Append("- **Local offload:** ");
        if (local.Length == 0)
        {
            text.Append("none\n");
        }
        else
        {
            text.Append(LedgerFormat.Count(local.Sum(row => row.InputTokens + row.OutputTokens))).Append(" tokens across ")
                .Append(Turns(local)).Append(" ($0.00 billed • ")
                .Append(LedgerFormat.Usd(totals.LocalSavedUsd)).Append(" cloud spend avoided): ")
                .Append(string.Join(", ", local.Select(row => $"{Code(row.Model!)} {LedgerFormat.Count(row.InputTokens + row.OutputTokens)}")))
                .Append('\n');
        }

        if (unpriced.Length > 0)
        {
            text.Append("- **Not priced:** ").Append(Plural(unpriced.Sum(row => row.SpanCount), "turn", "turns")).Append(": ")
                .Append(string.Join(", ", unpriced.Select(row => Code(row.Model!))))
                .Append('\n');
        }

        text.Append("- **Circuit breaker:** ").Append(Plural(receipt.BreakerTrips, "trip", "trips"))
            .Append(" • max single-prompt burn: ").Append(LedgerFormat.Credits(receipt.MaxPromptCredits))
            .Append(" credits\n\n");

        text.Append("<details>\n<summary>Details and data</summary>\n\n");
        if (receipt.Models.Count == 0)
        {
            text.Append("No spans are recorded for this branch.\n\n");
        }
        else
        {
            text.Append("| Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | Cloud spend avoided |\n");
            text.Append("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |\n");
            foreach (LedgerRollupRow row in receipt.Models)
            {
                text.Append("| ").Append(ChannelLabel(row.Channel!.Value))
                    .Append(" | ").Append(Code(row.Model!).Replace("|", "\\|", StringComparison.Ordinal))
                    .Append(" | ").Append(LedgerFormat.Count(row.SpanCount))
                    .Append(" | ").Append(LedgerFormat.Count(row.UnpricedSpanCount))
                    .Append(" | ").Append(LedgerFormat.Count(row.InputTokens))
                    .Append(" | ").Append(LedgerFormat.Count(row.OutputTokens))
                    .Append(" | ").Append(Cost(row))
                    .Append(" | ").Append(LedgerFormat.Usd(row.LocalSavedUsd))
                    .Append(" |\n");
            }

            text.Append('\n');
        }

        text.Append("Repository ").Append(Code(receipt.Repository))
            .Append(" • ").Append(Plural(receipt.Prompts, "prompt", "prompts"))
            .Append(" • commit ").Append(Code(receipt.Commit[..Math.Min(12, receipt.Commit.Length)]))
            .Append(" • generated ")
            .Append(receipt.GeneratedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            .Append(" UTC by gh-ledger ").Append(GhLedgerCli.Version).Append("\n\n");
        text.Append("To check these totals, run ").Append(Code("gh ledger branch --since all --json"))
            .Append(" in the repository and compare the ").Append(Code(receipt.Branch)).Append(" row.\n\n");
        text.Append("```json\n").Append(ToJson(receipt)).Append("\n```\n\n</details>\n");
        return text.ToString();
    }

    // <name>-<hash>.md. name is branch with every character other than A-Z, a-z, 0-9, '.', '_', and '-' replaced by '-',
    // cut to its first 64 characters. hash is the first 8 lowercase hex digits of the SHA-256 of branch's UTF-8 bytes,
    // so branches that differ only in replaced characters or letter case still get different files.
    public static string FileName(string branch)
    {
        StringBuilder name = new(branch.Length);
        foreach (char character in branch)
        {
            name.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '-');
        }

        if (name.Length > 64)
        {
            name.Length = 64;
        }

        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(branch)))[..8];
        return $"{name}-{hash}.md";
    }

    // A CommonMark code span that shows value literally. Control characters, U+2028, and U+2029 become U+FFFD; the fence is
    // one backtick longer than the longest run of backticks in value; and a space pads both ends when value starts or ends
    // with a backtick or a space. GitHub doesn't turn @mentions, #references, or URLs inside a code span into links.
    // value is never empty.
    public static string Code(string value)
    {
        StringBuilder content = new(value.Length);
        int run = 0;
        int longestRun = 0;
        foreach (char character in value)
        {
            char shown = char.IsControl(character) || character is '\u2028' or '\u2029'
                ? '\uFFFD'
                : character;
            run = shown == '`' ? run + 1 : 0;
            longestRun = Math.Max(longestRun, run);
            content.Append(shown);
        }

        string fence = new('`', longestRun + 1);
        bool pad = content[0] is '`' or ' ' || content[content.Length - 1] is '`' or ' ';
        return pad ? $"{fence} {content} {fence}" : $"{fence}{content}{fence}";
    }

    private static string ChannelId(BillingChannel channel) => channel switch
    {
        BillingChannel.GitHubAiCredits => "github_ai_credits",
        BillingChannel.FoundryByok => "foundry_byok",
        BillingChannel.Local => "local",
        BillingChannel.Unpriced => "unpriced",
        _ => throw new UnreachableException(),
    };

    private static string ChannelLabel(BillingChannel channel) => channel switch
    {
        BillingChannel.GitHubAiCredits => "GitHub AI Credits",
        BillingChannel.FoundryByok => "Foundry BYOK",
        BillingChannel.Local => "Local",
        BillingChannel.Unpriced => "Not priced",
        _ => throw new UnreachableException(),
    };

    private static string Cost(LedgerRollupRow row) => row.Channel switch
    {
        BillingChannel.GitHubAiCredits =>
            $"{LedgerFormat.Credits(row.AiCredits)} credits ({LedgerFormat.Usd(row.AiCreditsUsd)})",
        BillingChannel.FoundryByok => LedgerFormat.Usd(row.FoundryByokUsd),
        BillingChannel.Local or BillingChannel.Unpriced => "$0.00",
        _ => throw new UnreachableException(),
    };

    private static LedgerRollupRow[] Rows(PullRequestReceipt receipt, BillingChannel channel) =>
        receipt.Models.Where(row => row.Channel == channel).ToArray();

    private static string Turns(IReadOnlyList<LedgerRollupRow> rows)
    {
        long unpriced = rows.Sum(row => row.UnpricedSpanCount);
        string turns = Plural(rows.Sum(row => row.SpanCount), "turn", "turns");
        return unpriced == 0 ? turns : $"{turns}, {LedgerFormat.Count(unpriced)} unpriced";
    }

    private static string Plural(long count, string singular, string plural) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? singular : plural)}";
}
````

### `ReceiptCommands.cs` (copy exactly)

```csharp
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Receipts;

internal static class ReceiptCommands
{
    private static readonly LedgerRollupRow NoSpend = new()
    {
        SpanCount = 0,
        UnpricedSpanCount = 0,
        InputTokens = 0,
        OutputTokens = 0,
        CacheReadInputTokens = 0,
        CacheWriteInputTokens = 0,
        AiCredits = 0m,
        AiCreditsUsd = 0m,
        FoundryByokUsd = 0m,
        LocalSavedUsd = 0m,
    };

    public static Command Create(CliContext context)
    {
        Command command = new("export-pr", "Write the current branch's AI spend receipt for its pull request.");
        Option<bool> stdout = new("--stdout")
        {
            Description = "Write the Markdown receipt to standard output instead of the receipt file.",
        };
        Option<bool> json = new("--json")
        {
            Description = "Write the receipt's JSON data to standard output instead of the receipt file.",
        };
        command.Add(stdout);
        command.Add(json);
        command.Validators.Add(result =>
        {
            if (result.GetValue(stdout) && result.GetValue(json))
            {
                result.AddError("Use either --stdout or --json, not both.");
            }
        });
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                ExportAsync(
                    context,
                    result.GetValue(stdout),
                    result.GetValue(json),
                    cancellationToken));
        return command;
    }

    private static async Task<int> ExportAsync(
        CliContext context,
        bool stdout,
        bool json,
        CancellationToken cancellationToken)
    {
        GitWorkspaceContext? git =
            new FileSystemGitContextResolver().Resolve(context.WorkingDirectory);
        if (git is null)
        {
            context.Error.WriteLine($"{context.WorkingDirectory} isn't in a Git repository.");
            return 1;
        }

        if (git.BranchName is null)
        {
            context.Error.WriteLine(
                "HEAD is detached. Check out the pull request's branch, then run export-pr again.");
            return 1;
        }

        if (git.HeadCommitSha is null)
        {
            context.Error.WriteLine($"{git.BranchName} has no commits yet.");
            return 1;
        }

        if (git.RepositorySlug is null)
        {
            context.Error.WriteLine(
                $"export-pr needs a GitHub remote named origin, and {git.RepositoryRoot} has none.");
            return 1;
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        LedgerFilter filter = new()
        {
            Repository = git.RepositorySlug,
            Branch = git.BranchName,
        };
        IReadOnlyList<LedgerRollupRow> branchRows = await store.GetRollupAsync(
            LedgerGrouping.Branch,
            filter,
            cancellationToken);
        IReadOnlyList<PromptChainRow> costliest = await store.GetTopPromptChainsAsync(
            filter,
            1,
            cancellationToken);
        PullRequestReceipt receipt = new()
        {
            Repository = git.RepositorySlug,
            Branch = git.BranchName,
            Commit = git.HeadCommitSha,
            GeneratedAt = context.TimeProvider.GetUtcNow(),
            Totals = branchRows.Count == 0 ? NoSpend : branchRows[0],
            Models = await store.GetRollupAsync(
                LedgerGrouping.ChannelModel,
                filter,
                cancellationToken),
            Prompts = await store.GetPromptCountAsync(filter, cancellationToken),
            MaxPromptCredits = costliest.Count == 0
                ? 0m
                : (costliest[0].AiCreditsUsd + costliest[0].FoundryByokUsd) / AiCreditMath.UsdPerAiCredit,
            BreakerTrips = await store.CountBreakerTripsAsync(
                git.RepositorySlug,
                git.BranchName,
                cancellationToken),
        };

        if (json)
        {
            context.Out.Write(ReceiptMarkdown.ToJson(receipt) + "\n");
            return 0;
        }

        string markdown = ReceiptMarkdown.ToMarkdown(receipt);
        int size = Encoding.UTF8.GetByteCount(markdown);
        if (size > ReceiptMarkdown.MaximumBytes)
        {
            context.Error.WriteLine(
                $"The receipt for {git.BranchName} is {size} bytes; a receipt can be at most {ReceiptMarkdown.MaximumBytes} bytes.");
            return 1;
        }

        if (stdout)
        {
            context.Out.Write(markdown);
            return 0;
        }

        string fileName = ReceiptMarkdown.FileName(git.BranchName);
        string path = Path.Combine(
            git.RepositoryRoot,
            ".github",
            "copilot-ledger",
            "receipts",
            fileName);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(
                path,
                markdown,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine($"Could not write {path}: {ex.Message}");
            return 1;
        }

        context.Out.WriteLine($"Wrote {ReceiptMarkdown.ReceiptsDirectory}/{fileName} for {git.BranchName}.");
        context.Out.WriteLine(
            "Commit it on the branch and push; the CopilotLedger action posts it on the pull request.");
        return 0;
    }
}
```

### `GhLedgerCli`

Add `using CopilotLedger.Cli.Receipts;` after `using CopilotLedger.Cli.Rates;`, and add this line directly before `root.Add(McpCommand.Create(context));`:

```csharp
        root.Add(ReceiptCommands.Create(context));
```

### `docs/cli.md`

Add this section after "## Budgets":

```markdown
## Pull request receipts

`gh-ledger export-pr` writes the current branch's receipt for its pull request to `.github/copilot-ledger/receipts/<branch>-<hash>.md` in the repository root, where `<branch>` is the branch name with characters other than letters, digits, `.`, `_`, and `-` replaced by `-`. Commit that file on the branch and push it.

The receipt covers all spend recorded on the branch. It lists GitHub AI Credits by model, Foundry BYOK spend, local-model tokens and the cloud spend they avoided, requests that couldn't be priced, the number of circuit-breaker trips, and the most expensive prompt in AI credits. A collapsible section holds a table by billing channel and model, and the receipt's data as JSON. Its totals equal the branch's row in `gh-ledger branch --since all --json`.

- `--stdout` writes the Markdown to standard output instead of the file.
- `--json` writes the JSON data to standard output instead of the file.

A receipt can be at most 65,536 bytes, the most the CopilotLedger action posts; `export-pr` refuses a larger one, with or without `--stdout`. `export-pr` needs a branch with at least one commit and a GitHub remote named `origin`. A turn is one model request. Branch and model names appear in code spans, so they never become mentions or links.
```

### Acceptance criteria

- `GetRollupAsync` returns the same rows as before for the four existing groupings, with `Channel` null.
- `export-pr` writes only the receipt file, with UTF-8 without a byte order mark and `\n` line breaks, and creates its directories. `--stdout` and `--json` write no file. A receipt over 65,536 bytes is refused, with or without `--stdout`, and writes nothing.
- No `double` or `float` in `src/`. Money flows only through `decimal` and `LedgerFormat`.
- `dotnet build` reports no warnings, and `dotnet test` passes 621 tests (592 + 29).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

Report the passed, failed, and skipped counts.

## Task 3 specification: The Receipt Action

### Files

| File | Change |
|---|---|
| `action/action.yml` | New, copy exactly |
| `action/receipt.ps1` | New, copy exactly |
| `action/tests/receipt.Tests.ps1` | New, copy exactly (Task 3 tests) |
| `.github/workflows/receipt.yml` | New, copy exactly: this repository uses the action |
| `.github/workflows/ci.yml` | Run the action's tests |
| `docs/action.md` | New, copy exactly |
| `docs/cli.md` | One sentence in "Pull request receipts" |

Write the three `action/` files and the workflow with `\n` line breaks. `receipt.ps1` is run by `pwsh` explicitly, so it needs no executable bit.

### `action/action.yml` (copy exactly)

```yaml
name: CopilotLedger PR receipt
description: Posts the CopilotLedger AI spend receipt committed in a pull request as one comment, and updates it on later pushes.
author: swigerb

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

Every value from the event reaches the script through `env`, never by `${{ }}` inside `run`, so a branch name can't inject script.

### `action/receipt.ps1` (copy exactly)

````powershell
#Requires -Version 7.4
# Posts the CopilotLedger receipt that a pull request commits as one comment, and updates that comment on later runs.
# action.yml passes the inputs and pull request fields as environment variables. See docs/action.md.
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

How it behaves, in order:

1. Any event but `pull_request`: `::error::The CopilotLedger receipt action runs on pull_request events, not <event>.`, exit 1, no API calls.
2. A head repository other than this one (a fork, or a deleted fork): `::notice::Skipped the CopilotLedger receipt: …`, outputs `result=skipped` and an empty `comment-url`, exit 0, no API calls.
3. List the changed files and keep the candidates. None: a `::notice::` naming `gh ledger export-pr` and the directory, `result=no-receipt`, exit 0.
4. Read and validate every candidate (errors name the file and exit 1), then require exactly one whose `branch` is the head branch, compared ordinally. With none, the error lists the branches it found.
5. Find the comment (the oldest by `comment-author`, compared ignoring case, whose body starts with the marker), then create it, leave it, or update it, and write `CopilotLedger receipt <result>: <url>` and both outputs.

Every string comparison is `[string]::Equals` with `[System.StringComparison]::Ordinal` (`OrdinalIgnoreCase` for logins) or an ordinal `StartsWith` or `EndsWith`. Don't use `-eq`, `-ceq`, `-ne`, `-cne`, `-like`, or `-match` on strings: they compare with culture rules, which ignore zero-width characters and a byte order mark (see "Verified facts"). The login check keeps `$null -ne $login`, because PowerShell passes `$null` to `[string]::Equals` as `""`.

Messages go through `Write-Host`, because `Write-Output` inside a function whose result is assigned would be swallowed. Workflow-command text escapes `%`, `\r`, and `\n`. `Set-StrictMode -Version 3.0` is deliberate.

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
      - uses: actions/checkout@v7
      - uses: ./action
```

On pull requests in this repository without a receipt, the action leaves a notice and succeeds, which checks the action's real API calls on every pull request.

### CI

In `.github/workflows/ci.yml`, add this step directly after `- run: dotnet test CopilotLedger.slnx`:

```yaml
      - name: Test the receipt action
        shell: pwsh
        run: pwsh -NoProfile -File action/tests/receipt.Tests.ps1
```

### `docs/action.md` (copy exactly)

````markdown
# Posting PR receipts with the CopilotLedger action

The CopilotLedger action posts a branch's AI spend receipt on its pull request. The receipt comes from your machine: CopilotLedger's ledger is local, and the action only reads the receipt file the pull request commits. It needs no cloud service and no secrets.

## How it works

1. On your machine, check out the pull request's branch and run `gh ledger export-pr`. It writes the branch's receipt to `.github/copilot-ledger/receipts/<branch>-<hash>.md`.
2. Commit that file on the branch and push.
3. On each push, the action finds the receipt among the pull request's changed files, checks it, and posts it as one comment. Later pushes update that comment instead of adding new ones.

The receipt is Markdown with its data in a JSON block. It shows GitHub AI Credits, Foundry BYOK spend, local-model tokens with the cloud spend they avoided, circuit-breaker trips, and the most expensive prompt. Its totals equal the branch's row in `gh ledger branch --since all --json`.

Receipts stay in the repository after the pull request merges, as a record of what each branch spent.

## Add the workflow

Create `.github/workflows/copilot-ledger-receipt.yml`:

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
      - uses: swigerb/gh-ledger/action@v0.4.0
```

The action reads the receipt with `contents: read` and comments with `pull-requests: write`, using the workflow's `GITHUB_TOKEN`.

## Inputs and outputs

| Input | Default | Meaning |
|---|---|---|
| `github-token` | `${{ github.token }}` | Token that reads the pull request and writes the comment |
| `receipts-directory` | `.github/copilot-ledger/receipts` | Where receipts are committed; change it only if you move them |
| `comment-author` | `github-actions[bot]` | The login that owns the receipt comment. Change it only when `github-token` belongs to another account. |

| Output | Meaning |
|---|---|
| `result` | `created`, `updated`, `unchanged`, `no-receipt`, or `skipped` |
| `comment-url` | The receipt comment's URL; empty for `no-receipt` and `skipped` |

## What the action checks

- It runs only for `pull_request` events. Pull requests from forks get a read-only token, so the action skips them with a notice.
- A receipt must be among the pull request's changed files, under the receipts directory, and end in `.md`. With none, the action leaves a notice and succeeds.
- A receipt must start with `<!-- copilot-ledger-receipt -->` (with no byte order mark before it), be at most 65,536 bytes of UTF-8, and hold a JSON block with `"schemaVersion": 1` and a `branch` exactly equal to the pull request's branch. Otherwise the action fails with an error that names the file or, when no receipt is for the pull request's branch, lists the branches of the receipts it found.
- It updates the oldest comment by `comment-author` (compared case-insensitively, as GitHub logins are) that starts with `<!-- copilot-ledger-receipt -->`. It never edits anyone else's comment.

The action is a composite action that runs PowerShell 7, which GitHub-hosted runners include.
````

### `docs/cli.md`

At the end of the first paragraph of "## Pull request receipts", after "Commit that file on the branch and push it.", add:

```markdown
The CopilotLedger action then posts it on the pull request; see [Posting PR receipts with the CopilotLedger action](action.md).
```

### Acceptance criteria

- `pwsh -NoProfile -File action/tests/receipt.Tests.ps1` runs AC1–AC18, prints `All receipt action tests passed.`, and exits 0 on Windows and Linux.
- `receipt.ps1` uses none of `-eq`, `-ceq`, `-ne`, `-cne`, `-like`, `-clike`, `-match`, or `-cmatch` on a string; the remaining `-eq`, `-ne`, `-lt`, `-le`, and `-gt` compare numbers, enum values, or `$null`.
- The action uses no other action and no network access other than `GITHUB_API_URL`. The workflow grants only `contents: read` and `pull-requests: write`.
- `dotnet test` still passes 621 tests.
- On this task's pull request, the `Receipt` workflow succeeds with a `No CopilotLedger receipt changed in this pull request.` notice.

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
pwsh -NoProfile -File action/tests/receipt.Tests.ps1
```

Report the .NET passed, failed, and skipped counts and the PowerShell result line.

## Task 4 specification: gh Extension Release, Version 0.4.0 & Quickstart

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Cli/Install/GhExtension.cs` | New, copy exactly |
| `src/CopilotLedger.Cli/Install/InstallCommands.cs` | Skip the alias as the extension; make two helpers internal |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | `status` as the extension and the install-path warning |
| `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj` | `<Version>0.4.0</Version>` |
| `src/CopilotLedger.Cli/GhLedgerCli.cs` | Version comment and fallback `0.4.0` |
| `.github/workflows/release.yml` | New, copy exactly |
| `.github/workflows/ci.yml` | Smoke test expects `0.4.0` |
| `README.md` | Quickstart around the extension, `export-pr`, and the action |
| `docs/guide.md` | Install with gh; show spend on pull requests; uninstall |
| `docs/cli.md` | Getting gh-ledger, install, uninstall, and troubleshooting |
| `docs/releasing.md` | New, copy exactly |
| `tests/CopilotLedger.Tests/Cli/InstallCommandTests.cs` | IN17–IN19 |
| `tests/CopilotLedger.Tests/Cli/ReportCommandTests.cs` | RP12, RP13, and `StatusText` |
| `tests/CopilotLedger.Tests/Cli/DaemonCommandTests.cs` | DM1 expects `0.4.0` |

### `GhExtension.cs` (copy exactly)

```csharp
namespace CopilotLedger.Cli.Install;

// `gh extension install swigerb/gh-ledger` saves the release asset for the platform as
// <gh data directory>/extensions/gh-ledger/gh-ledger (gh-ledger.exe on Windows) next to a manifest.yml, and runs it for
// `gh ledger ...`. Paths are split on both '/' and '\' so tests can use either style on any OS.
internal static class GhExtension
{
    public static bool IsInstalled(ICliPlatform platform)
    {
        string? executable = platform.ProcessPath;
        if (executable is null)
        {
            return false;
        }

        int separator = LastSeparator(executable);
        if (separator < 0)
        {
            return false;
        }

        string name = executable[(separator + 1)..];
        string directory = executable[..separator];
        int parentSeparator = LastSeparator(directory);
        if (parentSeparator < 0)
        {
            return false;
        }

        string parent = directory[..parentSeparator];
        return (name.Equals("gh-ledger", StringComparison.OrdinalIgnoreCase)
                || name.Equals("gh-ledger.exe", StringComparison.OrdinalIgnoreCase))
            && directory[(parentSeparator + 1)..].Equals("gh-ledger", StringComparison.OrdinalIgnoreCase)
            && parent[(LastSeparator(parent) + 1)..].Equals("extensions", StringComparison.OrdinalIgnoreCase)
            && platform.FileExists(directory + executable[separator] + "manifest.yml");
    }

    // How to run gh-ledger in messages: "gh ledger" for the extension, else "gh-ledger".
    public static string CommandName(ICliPlatform platform) =>
        IsInstalled(platform) ? "gh ledger" : "gh-ledger";

    private static int LastSeparator(string path) =>
        Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
}
```

### `InstallCommands` (copy exactly)

1. Replace these lines in `InstallAsync`:

   ```csharp
           string? gh = context.Platform.FindOnPath("gh");
           if (gh is null)
           {
   ```

   with:

   ```csharp
           bool extension = GhExtension.IsInstalled(context.Platform);
           string? gh = context.Platform.FindOnPath("gh");
           if (extension)
           {
               context.Out.WriteLine(
                   "gh ledger runs this GitHub CLI extension, so no alias is needed.");
           }
           else if (gh is null)
           {
   ```

   (`UninstallAsync` has a similar `FindOnPath("gh")` line; don't change it.)
2. Replace `if (gh is not null && context.Platform.IsWindows && FindSh(context) is null)` with:

   ```csharp
           if (!extension && gh is not null && context.Platform.IsWindows && FindSh(context) is null)
   ```
3. Change `private static async Task<InstallManifest?> TryReadManifestAsync(` to `internal static async Task<InstallManifest?> TryReadManifestAsync(`, and `private static string ManifestPath(CliContext context) =>` to `internal static string ManifestPath(CliContext context) =>`.

Nothing else in `install` or `uninstall` changes. As the extension, the manifest keeps `GhAliasSet` from the previous manifest (false on a first install).

### `ReportCommands` (copy exactly)

1. In `StatusAsync`, replace:

   ```csharp
           IReadOnlyList<string> warnings = CopilotOtelEnvironment.GetWarnings(lookup);
           bool enabled = IsOtelEnabled(lookup);
   ```

   with:

   ```csharp
           List<string> warnings = [.. CopilotOtelEnvironment.GetWarnings(lookup)];
           bool enabled = IsOtelEnabled(lookup);
           bool extension = GhExtension.IsInstalled(context.Platform);
           string command = GhExtension.CommandName(context.Platform);
           InstallManifest? manifest = await InstallCommands.TryReadManifestAsync(
               InstallCommands.ManifestPath(context),
               cancellationToken);
           if (manifest is not null
               && context.Platform.ProcessPath is string executable
               && !string.Equals(
                   manifest.ExecutablePath,
                   executable,
                   context.Platform.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
           {
               warnings.Add(
                   $"CopilotLedger was installed from {manifest.ExecutablePath}; run {command} install to use this copy instead.");
           }
   ```
2. Pass `extension` as a new last argument to `WriteStatusJson`, and replace the first two text lines and the Copilot line:

   ```csharp
           context.Out.WriteLine(extension
               ? $"gh-ledger {GhLedgerCli.Version} (GitHub CLI extension)"
               : $"gh-ledger {GhLedgerCli.Version}");
           context.Out.WriteLine($"Daemon:  {DaemonLine(running, state, context, command)}");
   ```

   ```csharp
           context.Out.WriteLine(
               $"Copilot: {(enabled
                   ? CopilotEnabledLine(lookup)
                   : $"OTel export not enabled; run: {command} install")}");
   ```
3. `WriteStatusJson` gets a last parameter `bool extension`, and writes `writer.WriteBoolean("ghExtension", extension);` directly after `writer.WriteString("version", GhLedgerCli.Version);`.
4. `DaemonLine` gets a last parameter `string command`, and its not-running text becomes `$"not running; start it with: {command} daemon start"`.

The new warning is printed with the others (`Warning: <text>`) and is in the JSON `warnings` array. Without `install.json`, or when it names this executable, nothing changes.

### Version 0.4.0

- `CopilotLedger.Cli.csproj`: `<Version>0.4.0</Version>`.
- `GhLedgerCli.cs`: the comment becomes `// AssemblyInformationalVersionAttribute: "0.4.0"` and the fallback `?? "0.4.0";`.
- `ci.yml`: the smoke test's version check compares with `"0.4.0"`.
- Tests: in `DaemonCommandTests.DM1`, `Assert.Equal("0.4.0", root.GetProperty("version").GetString());`; in `ReportCommandTests.StatusText`, the first line is `gh-ledger 0.4.0`.

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
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
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
      - uses: actions/upload-artifact@v7
        with:
          name: ${{ matrix.asset }}
          path: dist/${{ matrix.asset }}
          if-no-files-found: error

  release:
    needs: build
    if: github.event_name == 'push' && github.ref_type == 'tag'
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v7
      - uses: actions/download-artifact@v8
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

### `docs/releasing.md` (copy exactly)

````markdown
# Releasing gh-ledger

Releases are GitHub Releases of this repository. They make `gh extension install swigerb/gh-ledger` work, and `gh ledger rates update` downloads their `rates.json`.

## Cut a release

1. Change the version everywhere it's written, and merge that change:
   - `<Version>` in `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`;
   - in `src/CopilotLedger.Cli/GhLedgerCli.cs`, the `AssemblyInformationalVersionAttribute` comment and the fallback after `??`;
   - the version that the smoke test in `.github/workflows/ci.yml` compares with;
   - the tests that expect it: `DaemonCommandTests.DM1` and `ReportCommandTests.StatusText`.
2. Optional: run the Release workflow on `main` from the Actions tab. A manual run builds all six binaries, smoke-tests the three that GitHub's runners can run (`win-x64`, `linux-x64`, and `osx-arm64`), and uploads the six as workflow artifacts. It never creates a release, even when it's started from a tag.
3. Tag the merged commit and push the tag:

   ```shell
   git tag v0.4.0
   git push origin v0.4.0
   ```

When a `v*` tag is pushed, the Release workflow checks that the tag equals `v` plus the project version, builds a single-file binary for each platform, smoke-tests the same three, and creates the release with these assets:

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
````

### `README.md`

Replace the sections from "## Features" through "## Documentation" (keep the title, the two opening paragraphs, and "## License") with:

````markdown
## Features

- Reports for branches, agents, and prompt traces
- Pull request receipts: commit a branch's spend receipt, and a GitHub Action posts it on the pull request
- A circuit breaker for spend velocity and retry churn
- Soft AI-credit budgets for each branch
- An MCP server that lets Copilot agents inspect the ledger
- Attribution for Foundry BYOK and local models
- Local SQLite storage and JSON output for scripts

## Requirements

- GitHub CLI (`gh`) to install CopilotLedger as an extension, or the .NET 10 SDK to build it from source
- Windows (x64 or Arm64), Linux (x64 or Arm64), or macOS (Intel or Apple silicon)
- Projects in Git repositories, for per-branch attribution. CopilotLedger reads
  `.git` directly and doesn't run `git`.
- Copilot CLI, the GitHub Copilot app, or VS Code with Copilot

## How it works

CopilotLedger runs an OpenTelemetry HTTP receiver on your machine. Copilot sends
usage spans to that receiver, and CopilotLedger stores usage and cost in a
local SQLite ledger.

Spend is available through text reports, JSON reports, pull request receipts,
and the MCP server. Prompt and response content are not stored.

## Quick start

Install the GitHub CLI extension, then turn on Copilot's telemetry export and
start the daemon:

```shell
gh extension install swigerb/gh-ledger
gh ledger install
```

`install` also registers the daemon to start when you sign in: a Windows Run
value, a macOS LaunchAgent, or an XDG autostart entry. The extension doesn't
need Git for Windows.

Restart the GitHub Copilot app, VS Code, and your open terminals. Send one
Copilot prompt, then check the receiver and export configuration:

```shell
gh ledger status
```

See branch spend:

```shell
gh ledger branch
```

Run `gh ledger --help` for every command. The daemon runs from the extension's
binary, so stop it before you upgrade:

```shell
gh ledger daemon stop
gh extension upgrade ledger
gh ledger daemon start
```

### Show spend on pull requests

On a pull request's branch, write the branch's receipt and commit it:

```shell
gh ledger export-pr
git add .github/copilot-ledger/receipts
git commit -m "Add CopilotLedger receipt"
git push
```

Then add `.github/workflows/copilot-ledger-receipt.yml` to the repository:

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
      - uses: swigerb/gh-ledger/action@v0.4.0
```

The action posts the receipt as one comment and updates it on later pushes.
See [Posting PR receipts](docs/action.md).

### Build from source

Build a single-file, self-contained binary for your platform:

```shell
dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release \
  -r <rid> -o artifacts/gh-ledger
```

Use `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, or
`osx-arm64`. Move the binary to a permanent folder, then run
`/path/to/gh-ledger install`, which records that location and sets a
`gh ledger` alias. On Windows, the alias needs Git for Windows (`sh.exe`);
without it, run `gh-ledger` directly.

## Documentation

- [Getting the most from CopilotLedger](docs/guide.md)
- [CLI command reference](docs/cli.md)
- [Pricing and rates configuration](docs/configuration.md)
- [Posting PR receipts with the CopilotLedger action](docs/action.md)
- [Releasing gh-ledger](docs/releasing.md)
````

### `docs/guide.md`

1. In the opening paragraph, replace the sentence that starts "Commands are shown as" through the end of the paragraph with:

   ```markdown
   Commands are shown as `gh-ledger <command>`. With the GitHub CLI extension, or after `install` sets the alias, run them as `gh ledger <command>`.
   ```
2. Replace the subsections "### Build the binary" and "### Install it", everything from "### Build the binary" up to "### Verify it", with:

   ````markdown
   ### Install it

   Install the GitHub CLI extension and run its `install` command:

   ```shell
   gh extension install swigerb/gh-ledger
   gh ledger install
   ```

   To build from source instead, publish a single-file binary for your platform, move it to a permanent folder, and run its `install`:

   ```shell
   dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release \
     -r <rid> -o artifacts/gh-ledger
   /path/to/gh-ledger install
   ```

   Replace `<rid>` with `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64`.

   `install` sets `COPILOT_OTEL_ENABLED=true` for your user, starts the daemon, and records its changes in `install.json`. For a binary you built, it also sets the `gh ledger` alias; the extension doesn't need one. Use `--dry-run` to print the changes first.

   It also registers the daemon to start when you sign in:

   - Windows uses `CopilotLedger` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`.
   - macOS uses `~/Library/LaunchAgents/io.github.swigerb.copilot-ledger.plist`.
   - Linux uses `copilot-ledger.desktop` in `$XDG_CONFIG_HOME/autostart`, or `~/.config/autostart` by default.

   Restart the GitHub Copilot app, VS Code, and open terminals after the install.
   ````
3. Add this section directly before "## Stop runaway loops":

   ````markdown
   ## Show spend on pull requests

   Give reviewers the AI spend behind a pull request. On the pull request's branch, write its receipt and commit it:

   ```shell
   gh-ledger export-pr
   git add .github/copilot-ledger/receipts
   git commit -m "Add CopilotLedger receipt"
   git push
   ```

   The receipt lists GitHub AI Credits by model, Foundry BYOK spend, local-model tokens with the cloud spend they avoided, circuit-breaker trips, and the most expensive prompt, with the data as JSON. Its totals match `gh-ledger branch --since all`. Run `export-pr` again before you push more work, so the receipt stays current.

   Add the CopilotLedger action to the repository once, and it posts the receipt on the pull request and updates that comment on later pushes. See [Posting PR receipts](action.md) for the workflow.
   ````
4. In "## Uninstall", append this paragraph:

   ```markdown
   If you installed the GitHub CLI extension, remove it after `uninstall`, with `gh extension remove ledger`. On Windows, the daemon must be stopped first, because Windows can't replace or delete a running program; `uninstall` stops it.
   ```

### `docs/cli.md`

1. Replace the whole "## Build the binary" section, everything from that heading up to "## Install", with the text below. That's the heading, the paragraph before the code block, the code block, and the paragraph after it that starts "Use `win-x64`". The replacement:

   ````markdown
   ## Get gh-ledger

   Install the GitHub CLI extension:

   ```shell
   gh extension install swigerb/gh-ledger
   ```

   It downloads the release binary for your platform, which then runs as `gh ledger <command>`. Or publish one single-file, self-contained binary for your runtime identifier:

   ```shell
   dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r <rid> -o artifacts/gh-ledger
   ```

   Use `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64`. Keep the binary in a permanent folder, because `install` points the sign-in item and the `gh ledger` alias at it.
   ````
2. In "## Install", replace the first paragraph's words "sets the `gh ledger` alias," with "sets the `gh ledger` alias (unless gh-ledger is running as the GitHub CLI extension, which provides `gh ledger` itself),".
3. In "## Install", replace the paragraph "On Windows, `gh ledger` needs `sh.exe` from Git for Windows. The `gh-ledger` executable works without it." with:

   ```markdown
   On Windows, the `gh ledger` alias needs `sh.exe` from Git for Windows; the extension and the `gh-ledger` executable don't.

   As the extension, `status` shows `(GitHub CLI extension)` after the version and names `gh ledger` commands. `status` warns when `install.json` names a different executable, for example after switching from a downloaded binary to the extension; run `install` again from the copy you want the sign-in item to start.

   The sign-in item and the daemon run the extension's binary, and Windows can't replace a running program. Stop the daemon before you upgrade: `gh ledger daemon stop`, `gh extension upgrade ledger`, then `gh ledger daemon start`.
   ```
4. In "## Uninstall", append this sentence to its paragraph:

   ```markdown
   If you installed the GitHub CLI extension, run `gh extension remove ledger` afterward.
   ```
5. In "## Troubleshooting", add this item after "Port 4318 in use":

   ```markdown
   - **`gh extension upgrade` or `remove` fails on Windows.** The daemon is running from the extension's binary. Run `gh ledger daemon stop` first.
   ```

### Acceptance criteria

- As the extension (`…/extensions/gh-ledger/gh-ledger[.exe]` with `manifest.yml` beside it), `install` runs no `gh` command, prints the no-alias line, never prints the `sh.exe` warning, and registers the extension's path; otherwise `install` is unchanged.
- `status` text and JSON are unchanged for a standalone binary without `install.json` (apart from the version and `"ghExtension": false`).
- `release.yml` publishes the six assets, `rates.json`, and `gh-ledger-checksums.txt` only when a `v*` tag whose version matches is pushed. A manual run (`workflow_dispatch`) builds and smoke-tests without releasing, even when it's started from a tag. No `run` script contains `${{ }}`. Don't push a tag or run a release.
- `dotnet build` reports no warnings, and `dotnet test` passes 628 tests (621 + 7).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
pwsh -NoProfile -File action/tests/receipt.Tests.ps1
```

Report the counts. After this task merges, the orchestrator runs the Release workflow manually on `main` (`gh workflow run release.yml -R swigerb/gh-ledger --ref main`) and checks that all six builds and the three smoke tests pass, before anyone tags `v0.4.0`.

## Task 5 specification: `rates update`

### Files

| File | Change |
|---|---|
| `src/CopilotLedger.Core/Pricing/JsonRateCardCalculator.cs` | Add `LoadWithOverlay` |
| `src/CopilotLedger.Cli/CliContext.cs` | Add `CreateHttpHandler` |
| `src/CopilotLedger.Cli/Rates/RateCardFiles.cs` | New, copy exactly |
| `src/CopilotLedger.Cli/Rates/RatesUpdate.cs` | New, copy exactly |
| `src/CopilotLedger.Cli/Rates/RatesCommands.cs` | `rates update`; `validate` uses the downloaded base |
| `src/CopilotLedger.Cli/Daemon/DaemonHost.cs` | Load rates through `RateCardFiles` |
| `src/CopilotLedger.Cli/Reporting/ReportCommands.cs` | `status` shows the base and its warning |
| `README.md`, `docs/cli.md`, `docs/configuration.md` | Document `rates update` |
| `tests/CopilotLedger.Tests/TestSupport/CliHarness.cs` | Add `RunWithHttpAsync` |
| `tests/CopilotLedger.Tests/TestSupport/FakeHttpHandler.cs` | New (Task 5 tests) |
| `tests/CopilotLedger.Tests/TestSupport/PublishedRates.cs` | New (Task 5 tests) |
| `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs` | New (Task 5 tests) |
| `tests/CopilotLedger.Tests/Pricing/LoadWithOverlayTests.cs` | New (Task 5 tests) |

### `JsonRateCardCalculator.LoadWithOverlay` (copy exactly)

Add directly after `LoadDefault()`:

```csharp
    // baseJson is a complete rates document, such as a downloaded rates.json; its errors have no prefix. When overlayPath
    // isn't null, that overlay is merged as LoadEmbeddedWithOverlay merges it, and its errors start with overlayPath.
    public static JsonRateCardCalculator LoadWithOverlay(string baseJson, string? overlayPath)
    {
        ArgumentNullException.ThrowIfNull(baseJson);
        if (overlayPath is null)
        {
            return Create(baseJson, null, null);
        }

        string overlayJson;
        try
        {
            overlayJson = File.ReadAllText(overlayPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"{overlayPath}: could not read rates overlay.", ex);
        }

        return Create(baseJson, overlayJson, overlayPath);
    }
```

The existing load methods don't change.

### `CliContext` (copy exactly)

Add directly after `ConfigureWebHost`:

```csharp
    public Func<HttpMessageHandler>? CreateHttpHandler { get; init; }                   // rates update; tests: a fake handler; null: RatesUpdate.CreateDefaultHandler()
```

### `RateCardFiles.cs` (copy exactly)

```csharp
using System.Globalization;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Rates;

// DownloadedJson: the text of rates.downloaded.json when it's the base, else null (the built-in rates are the base).
// Version: the base's ratesVersion. Warning: why an existing rates.downloaded.json was ignored, or null.
internal sealed record RatesBase(string? DownloadedJson, string Version, string? Warning);

internal static class RateCardFiles
{
    public static string DownloadedPath(CliContext context) =>
        Path.Combine(context.DataDirectory, "rates.downloaded.json");

    // The downloaded rates when rates.downloaded.json is valid and its ratesVersion date is later than the built-in one;
    // otherwise the built-in rates. A downloaded file that isn't later is ignored without a warning.
    public static RatesBase LoadBase(CliContext context)
    {
        string builtIn = JsonRateCardCalculator.LoadEmbedded().RatesVersion;
        string path = DownloadedPath(context);
        if (!File.Exists(path))
        {
            return new RatesBase(null, builtIn, null);
        }

        string json;
        string version;
        try
        {
            json = File.ReadAllText(path);
            version = JsonRateCardCalculator.Parse(json).RatesVersion;
        }
        catch (Exception ex) when (
            ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new RatesBase(null, builtIn, $"ignored {path}: {ex.Message}");
        }

        if (!TryParseVersion(version, out DateOnly downloaded))
        {
            return new RatesBase(
                null,
                builtIn,
                $"ignored {path}: ratesVersion {version} isn't a yyyy-MM-dd date.");
        }

        return downloaded > ParseVersion(builtIn)
            ? new RatesBase(json, version, null)
            : new RatesBase(null, builtIn, null);
    }

    // The base rates with the overlay at overlayPath merged on top; null overlayPath means no overlay.
    // Throws InvalidDataException when the overlay is invalid; the message starts with overlayPath.
    public static JsonRateCardCalculator Load(RatesBase rates, string? overlayPath)
    {
        if (rates.DownloadedJson is not null)
        {
            return JsonRateCardCalculator.LoadWithOverlay(rates.DownloadedJson, overlayPath);
        }

        return overlayPath is null
            ? JsonRateCardCalculator.LoadEmbedded()
            : JsonRateCardCalculator.LoadEmbeddedWithOverlay(overlayPath);
    }

    // context.RatesOverlayPath when that file exists, else null.
    public static string? OverlayPath(CliContext context) =>
        File.Exists(context.RatesOverlayPath) ? context.RatesOverlayPath : null;

    public static bool TryParseVersion(string version, out DateOnly date) =>
        DateOnly.TryParseExact(
            version,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    public static DateOnly ParseVersion(string version) =>
        DateOnly.ParseExact(version, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
```

### `RatesUpdate.cs` (copy exactly)

```csharp
using System.Net;
using System.Text;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Install;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Rates;

internal static class RatesUpdate
{
    public const string DownloadUrl =
        "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json";

    private const int MaximumBytes = 1_048_576;

    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);

    // Uses the system proxy (HttpClient.DefaultProxy) and signs in to it with the current user's credentials.
    public static HttpMessageHandler CreateDefaultHandler() =>
        new SocketsHttpHandler
        {
            DefaultProxyCredentials = CredentialCache.DefaultCredentials,
        };

    public static async Task<int> RunAsync(CliContext context, CancellationToken cancellationToken)
    {
        byte[] bytes;
        using (HttpClient client = new(
            context.CreateHttpHandler?.Invoke() ?? CreateDefaultHandler(),
            disposeHandler: true))
        {
            client.Timeout = DownloadTimeout;
            client.MaxResponseContentBufferSize = MaximumBytes;
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"gh-ledger/{GhLedgerCli.Version}");
            try
            {
                using HttpResponseMessage response = await client.GetAsync(
                    DownloadUrl,
                    cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    context.Error.WriteLine(
                        $"Could not download rates from {DownloadUrl}: HTTP {(int)response.StatusCode}.");
                    return 1;
                }

                bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                context.Error.WriteLine($"Could not download rates from {DownloadUrl}: {ex.Message}");
                return 1;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                context.Error.WriteLine(
                    $"Could not download rates from {DownloadUrl}: no response within 30 seconds.");
                return 1;
            }
        }

        string json;
        try
        {
            json = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            context.Error.WriteLine("The downloaded rates are invalid: the file isn't UTF-8.");
            return 1;
        }

        string version;
        try
        {
            version = JsonRateCardCalculator.Parse(json).RatesVersion;
        }
        catch (InvalidDataException ex)
        {
            context.Error.WriteLine($"The downloaded rates are invalid: {ex.Message}");
            return 1;
        }

        if (!RateCardFiles.TryParseVersion(version, out DateOnly published))
        {
            context.Error.WriteLine(
                $"The downloaded rates are invalid: ratesVersion {version} isn't a yyyy-MM-dd date.");
            return 1;
        }

        RatesBase current = RateCardFiles.LoadBase(context);
        if (current.Warning is not null)
        {
            context.Out.WriteLine($"Warning: {current.Warning}");
        }

        DateOnly inUse = RateCardFiles.ParseVersion(current.Version);
        if (published < inUse)
        {
            context.Out.WriteLine(
                $"The published rates ({version}) are older than the rates in use ({current.Version}); nothing changed.");
            return 0;
        }

        if (published == inUse)
        {
            context.Out.WriteLine($"Rates are up to date ({current.Version}).");
            return 0;
        }

        string? overlay = RateCardFiles.OverlayPath(context);
        if (overlay is not null)
        {
            try
            {
                JsonRateCardCalculator.LoadWithOverlay(json, overlay);
            }
            catch (InvalidDataException ex)
            {
                context.Error.WriteLine(
                    $"The downloaded rates ({version}) don't work with your rates overlay: {ex.Message}");
                return 1;
            }
        }

        string path = RateCardFiles.DownloadedPath(context);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(context.DataDirectory);
            await File.WriteAllTextAsync(temp, json, new UTF8Encoding(false), cancellationToken);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DaemonFiles.DeleteIfExists(temp);
            context.Error.WriteLine($"Could not write {path}: {ex.Message}");
            return 1;
        }

        context.Out.WriteLine($"Updated rates from {current.Version} to {version}.");
        if (DaemonFiles.IsHeld(context))
        {
            string command = GhExtension.CommandName(context.Platform);
            context.Out.WriteLine(
                $"Restart the daemon to price new spans with them: {command} daemon stop, then {command} daemon start.");
        }

        return 0;
    }
}
```

The messages:

| Case | Stream | Text | Exit |
|---|---|---|---|
| `HttpRequestException` (offline, DNS, proxy, TLS, body over 1 MiB) | Error | `Could not download rates from <url>: <message>` | 1 |
| No response within 30 seconds | Error | `Could not download rates from <url>: no response within 30 seconds.` | 1 |
| Not a success status | Error | `Could not download rates from <url>: HTTP <code>.` | 1 |
| Not UTF-8 | Error | `The downloaded rates are invalid: the file isn't UTF-8.` | 1 |
| Rejected by the rates reader | Error | `The downloaded rates are invalid: <message>` | 1 |
| `ratesVersion` isn't `yyyy-MM-dd` | Error | `The downloaded rates are invalid: ratesVersion <version> isn't a yyyy-MM-dd date.` | 1 |
| The current download was ignored | Out | `Warning: ignored <path>: <reason>`, then one of the next rows | |
| Older than the rates in use | Out | `The published rates (<new>) are older than the rates in use (<current>); nothing changed.` | 0 |
| Same date | Out | `Rates are up to date (<current>).` | 0 |
| The overlay doesn't merge | Error | `The downloaded rates (<new>) don't work with your rates overlay: <overlay message>` | 1 |
| Can't write the file | Error | `Could not write <path>: <message>` | 1 |
| Saved | Out | `Updated rates from <current> to <new>.` | 0 |
| Saved while the daemon runs | Out | also `Restart the daemon to price new spans with them: <command> daemon stop, then <command> daemon start.` | 0 |

`<url>` is `RatesUpdate.DownloadUrl`, and `<command>` is `GhExtension.CommandName`. A cancellation of the command's own token isn't caught.

### `RatesCommands` (copy exactly)

1. The `rates` command's description becomes `"Create, validate, and update rates."`.
2. Directly after `rates.Add(validate);`, add a blank line and:

   ```csharp
           Command update = new("update", "Download the latest published rates.");
           update.SetAction(
               (ParseResult _, CancellationToken cancellationToken) =>
                   RatesUpdate.RunAsync(context, cancellationToken));
           rates.Add(update);
   ```
3. In `Validate`, directly after the `string path = …;` statement, add:

   ```csharp
           RatesBase rates = RateCardFiles.LoadBase(context);
           if (rates.Warning is not null)
           {
               context.Out.WriteLine($"Warning: {rates.Warning}");
           }

   ```

   Replace the no-overlay message with:

   ```csharp
               context.Out.WriteLine(
                   $"No rates overlay at {path}; using the {(rates.DownloadedJson is null ? "built-in" : "downloaded")} rates ({rates.Version}).");
   ```

   and replace the `JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbeddedWithOverlay(path);` statement with:

   ```csharp
               JsonRateCardCalculator calculator = RateCardFiles.Load(rates, path);
   ```

Without a downloaded file, every existing `rates validate` message is unchanged.

### `DaemonHost` (copy exactly)

Add `using CopilotLedger.Cli.Rates;` after `using CopilotLedger.Cli.Alerts;`. Replace the rates block after `DaemonFiles.DeleteIfExists(DaemonFiles.StopPath(context));` with:

```csharp
            RatesBase rates = RateCardFiles.LoadBase(context);
            if (rates.Warning is not null)
            {
                WriteLog(writer, context, $"Warning: {rates.Warning}");
            }

            JsonRateCardCalculator calculator;
            try
            {
                calculator = RateCardFiles.Load(rates, RateCardFiles.OverlayPath(context));
            }
            catch (InvalidDataException ex)
            {
                WriteLog(writer, context, $"Invalid rates overlay: {ex.Message}");
                return 1;
            }
```

### `ReportCommands` (copy exactly)

Add `using CopilotLedger.Cli.Rates;` after `using CopilotLedger.Cli.Install;`. In `StatusAsync`:

1. Replace the rates block (from `string? ratesVersion;` through the end of its `catch`) with:

   ```csharp
           RatesBase rates = RateCardFiles.LoadBase(context);
           string? ratesVersion;
           string? ratesError = null;
           try
           {
               ratesVersion = RateCardFiles.Load(rates, RateCardFiles.OverlayPath(context)).RatesVersion;
           }
           catch (InvalidDataException ex)
           {
               ratesVersion = null;
               ratesError = ex.Message;
           }
   ```
2. Directly after Task 4's install-path warning block, add:

   ```csharp

           if (rates.Warning is not null)
           {
               warnings.Add(rates.Warning);
           }
   ```
3. Pass `rates.DownloadedJson is not null` as a new last argument to `WriteStatusJson`.
4. Replace the `Rates:` line with:

   ```csharp
           context.Out.WriteLine(
               $"Rates:   {(ratesError is not null ? $"invalid overlay: {ratesError}" : rates.DownloadedJson is null ? ratesVersion : $"{ratesVersion} (downloaded)")}");
   ```
5. `WriteStatusJson` gets a last parameter `bool downloadedRates` and writes `writer.WriteString("ratesSource", downloadedRates ? "downloaded" : "built-in");` directly before `if (ratesError is null)`.

### `CliHarness` (copy exactly)

Add directly after `RunInAsync`:

```csharp
    public Task<CliRun> RunWithHttpAsync(HttpMessageHandler handler, params string[] args) =>
        InvokeAsync(Context with { CreateHttpHandler = () => handler }, args);
```

### Docs

`README.md`: in "## Features", replace `- Attribution for Foundry BYOK and local models` with:

```markdown
- Attribution for Foundry BYOK and local models, and `rates update` for newer prices between releases
```

`docs/cli.md`: replace the "## Rates" paragraph with:

```markdown
Run `gh-ledger rates init` to create a starter overlay and `gh-ledger rates validate` to validate it. See [configuration.md](configuration.md) for the rates format and pricing channels.

`gh-ledger rates update` downloads `rates.json` from the latest gh-ledger release and keeps it when it's newer than the rates in use and your overlay still applies to it. It uses your system proxy. Restart the daemon afterward; it reads rates when it starts.
```

`docs/configuration.md`:

1. In the file table, add this row after the `rates.json` row:

   ```markdown
   | `~/.copilot-ledger/rates.downloaded.json` | Newer built-in rates saved by `gh-ledger rates update` |
   ```
2. Add this section directly before "## The rates overlay: `~/.copilot-ledger/rates.json`":

   ```markdown
   ## Newer rates between releases: `gh-ledger rates update`

   Each gh-ledger release attaches the `rates.json` it was built with. `gh-ledger rates update` downloads the latest release's copy from `https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json`, through your system proxy, and checks it with the same rules as the built-in rates.

   Rates versions are dates. When the download is newer than the rates in use, `rates update` saves it as `rates.downloaded.json`; when it's the same or older, nothing changes. If your overlay doesn't merge with the newer rates, nothing is saved, and the error names the problem in your overlay.

   CopilotLedger's base rates are `rates.downloaded.json` when that file is valid and newer than the built-in rates, and the built-in rates otherwise. So after you upgrade gh-ledger to newer built-in rates, an older download is ignored. A download that can't be read is ignored with a warning in `gh-ledger status`, `gh-ledger rates validate`, and the daemon log. Your overlay applies on top of either. `gh-ledger status` shows `(downloaded)` after the rates version when the download is in use.

   The daemon reads rates when it starts, so restart it after an update. Spans already recorded keep the prices they were recorded with.
   ```

### Acceptance criteria

- Tests never touch the network: every `rates update` test passes a `FakeHttpHandler`.
- Without `rates.downloaded.json`, `status`, `rates validate`, and the daemon behave exactly as before, apart from `status --json`'s new `ratesSource`.
- No new package references. No `double` or `float` in `src/`.
- `docs/configuration.md` keeps exactly three `<!-- rates-error-example -->` blocks.
- `dotnet build` reports no warnings, and `dotnet test` passes 649 tests (628 + 21).

### Verify

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
pwsh -NoProfile -File action/tests/receipt.Tests.ps1
```

Report the counts. Don't run `rates update` against the real URL in tests; running it by hand is fine, and before the first release it prints `Could not download rates from https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json: HTTP 404.`

## Test specifications

Shared rules for every task, which extend Sprint 3's:

- **Money.** Assertions use `decimal`. Read JSON money with `GetDecimal()`, never `GetDouble`.
- **Public API only.** No `InternalsVisibleTo`. CLI behavior is tested through `GhLedgerCli.InvokeAsync` (`CliHarness`). The public helpers `WindowsCommandLine`, `SystemCliPlatform.UnixDetachedStartInfo`, and `ReceiptMarkdown` are tested directly.
- **Isolation.** Each test that writes files gets its own `TempDirectory`, declared first (`using TempDirectory temp = new();`) so it's disposed last, after the harness and any store. Build every path with `Path.Combine`, except the fake platform's paths, which are plain strings because the fake never touches the disk for them. Nothing touches the network, the real registry, `~/.copilot-ledger`, `~/.config`, gh, or shell profiles: HTTP goes through `FakeHttpHandler`, processes through `FakeCliPlatform`, and daemons through `TestServer`. No test opens a socket or runs `git`. Only SD1 starts a process: on Linux and macOS, `/bin/sh` with a file it can't execute, which exits at once.
- **SQLite pools.** Keep every database under the test's `TempDirectory`; `TempDirectory.Dispose` calls `SqliteConnection.ClearAllPools()` before deleting it. These tests never reopen a database with a changed schema, so they need no other pool handling.
- **Files the product just wrote.** Read them with `FileRetry.ReadAllTextAsync` or `FileRetry.ReadAllBytesAsync`, which retry while Windows reports the file in use (antivirus scans of new files caused issue #30).
- **Files the test just wrote.** When a test writes a file that the product then reads, it calls `await FileRetry.ReadAllTextAsync(path);` right after the write, before running the product, so a brief antivirus lock can't change the product's output. RP13, RU4–RU9, EP5, EP6, LW2, and LW3 do this.
- **Text.** `CliRun.Out` and `CliRun.Error` already have `\r\n` replaced with `\n`; never normalize them again, and never normalize a file the product wrote. Write multi-line expected text either as string literals that each end in `\n`, joined with `+`, or as a raw string literal followed by `.ReplaceLineEndings("\n")` *before* any value is substituted into it (checkouts use `core.autocrlf=true`, so a raw literal takes the file's line endings). Never call `ReplaceLineEndings` on interpolated text.
- **Exact output.** When a case compares `Out` or `Error`, it compares the whole text. A line check compares a whole line (`Split('\n')[index]`), and `StartsWith` checks include the trailing `\n`. Every CLI run asserts its exit code.
- **Clocks.** Receipt tests use `new ManualTimeProvider(2026-11-21T15:00:00Z, ManualTimeProvider.UtcMinus5)`. RU9 uses `ManualTimeProvider(2026-11-21T15:00:00Z)` so the daemon's log line is exact.
- **Outside a repository.** EP6's `no-repository` case expects `<root>/work` not to be in a Git repository. Like `ReportCommandTests` and Sprint 3's BC3, it assumes no `.git` above the system temp directory, which holds on developer machines and GitHub's runners.
- **The action's tests** are PowerShell: `pwsh -NoProfile -File action/tests/receipt.Tests.ps1`. They define a function named `Invoke-WebRequest` that answers from a table and fails the case on any request it doesn't expect, so they never reach the network.

### Task 1 tests

`tests/CopilotLedger.Tests/Cli/WindowsCommandLineTests.cs` (copy exactly). WC1 checks each argument rule; WC2 checks the daemon's whole command line and an empty argument list. WC1's 13 vectors and WC2's six daemon arguments are the ones that round-tripped through a real child process (see "Verified facts").

```csharp
using CopilotLedger.Cli;

namespace CopilotLedger.Tests.Cli;

public sealed class WindowsCommandLineTests
{
    private const string Executable = @"C:\Program Files\gh-ledger\gh-ledger.exe";

    public static TheoryData<string, string> Arguments => new()
    {
        { "daemon", "daemon" },
        { "4318", "4318" },
        { @"C:\Users\O'Brien Smith\data\daemon.log", "\"C:\\Users\\O'Brien Smith\\data\\daemon.log\"" },
        { "", "\"\"" },
        { "a\"b", "\"a\\\"b\"" },
        { @"C:\a b\", "\"C:\\a b\\\\\"" },
        { "a\\\\\"b", "\"a\\\\\\\\\\\"b\"" },
        { "tab\there", "\"tab\there\"" },
        { @"back\slash", @"back\slash" },
        { @"end\", @"end\" },
        { "\"quoted\"", "\"\\\"quoted\\\"\"" },
        { "\u00FCn\u00EFc\u00F6d\u00E9 \u2713", "\"\u00FCn\u00EFc\u00F6d\u00E9 \u2713\"" },
        { @"\\server\share\x y\", "\"\\\\server\\share\\x y\\\\\"" },
    };

    [Theory]
    [MemberData(nameof(Arguments))]
    public void WC1_EachArgumentIsQuotedForTheCRuntime(string argument, string written)
    {
        Assert.Equal($"\"{Executable}\" {written}", WindowsCommandLine.Build(Executable, [argument]));
    }

    [Fact]
    public void WC2_DaemonCommandLineIsExact()
    {
        Assert.Equal(
            "\"C:\\Program Files\\gh-ledger\\gh-ledger.exe\" daemon run --port 4318 --log-file \"C:\\Users\\O'Brien Smith\\data\\daemon.log\"",
            WindowsCommandLine.Build(
                Executable,
                ["daemon", "run", "--port", "4318", "--log-file", @"C:\Users\O'Brien Smith\data\daemon.log"]));
        Assert.Equal("\"C:\\gh-ledger.exe\"", WindowsCommandLine.Build(@"C:\gh-ledger.exe", []));
    }
}
```

`tests/CopilotLedger.Tests/Cli/StartDetachedTests.cs` (copy exactly). **SD1** checks that a daemon binary that can't run fails loudly. On Windows, `StartDetached` with a missing file throws a `Win32Exception` whose `NativeErrorCode` is 2 (`ERROR_FILE_NOT_FOUND`), and no process starts. On Linux and macOS, it runs `UnixDetachedStartInfo` for a file without execute permission, named `gh-ledger\cut`, with standard error redirected: the exit code is 126, and standard error is exactly `gh-ledger: cannot execute <path>` and `\n`, with the whole path. So each CI runner covers its own branch, and `ubuntu-latest` runs the script under dash.

```csharp
using System.ComponentModel;
using System.Diagnostics;
using CopilotLedger.Cli;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class StartDetachedTests
{
    // On Windows, CreateProcess fails at once for a missing file. Elsewhere, /bin/sh reports a file it can't execute on
    // the caller's standard error, which the test redirects to read it. The backslash in the file name checks that the
    // path is printed as is: dash's echo would stop at "\c".
    [Fact]
    public async Task SD1_StartDetachedFailsLoudlyWhenTheFileCantRun()
    {
        using TempDirectory temp = new();
        if (OperatingSystem.IsWindows())
        {
            Win32Exception exception = Assert.Throws<Win32Exception>(() => new SystemCliPlatform().StartDetached(
                Path.Combine(temp.Path, "gh-ledger.exe"),
                ["daemon", "run"],
                temp.Path));
            Assert.Equal(2, exception.NativeErrorCode);
            return;
        }

        string file = Path.Combine(temp.Path, @"gh-ledger\cut");
        await File.WriteAllTextAsync(file, "#!/bin/sh\n");
        ProcessStartInfo startInfo = SystemCliPlatform.UnixDetachedStartInfo(file, ["daemon", "run"], temp.Path);
        startInfo.RedirectStandardError = true;
        using Process process = Process.Start(startInfo)!;
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(126, process.ExitCode);
        Assert.Equal($"gh-ledger: cannot execute {file}\n", error);
    }
}
```

The capture behavior itself is checked by the two CI steps in Task 1, on both operating systems, because unit tests don't start the daemon.

### Task 2 tests

#### Seed: `tests/CopilotLedger.Tests/TestSupport/ReceiptSeeds.cs` (copy exactly)

The seed records eight spans and three breaker trips. On `swigerb/copilot-ledger` `feature/ledger` (`GitFixture.Feature`):

| Trace | Span | Model, endpoint | Tokens (input, output, cache read, cache write) | Priced at |
|---|---|---|---|---|
| `a…a` | 1 | `claude-opus-5.5`, none | 1,200,000, 50,000, 800,000, 100,000 | 300,000 × $4 + 800,000 × $0.20 + 100,000 × $5 + 50,000 × $20 per 1M = $2.86 = 286 credits |
| `a…a` | 2 | `gpt-6.1-sol`, `contoso.openai.azure.com` | 100,000, 10,000 | BYOK fixture: 100,000 × $1.75 + 10,000 × $14 per 1M = $0.315 |
| `b…b` | 1 | `mai-code-2`, none | 1,000,000, 100,000, 400,000 | 600,000 × $0.50 + 400,000 × $0.025 + 100,000 × $2.50 per 1M = $0.56 = 56 credits |
| `b…b` | 2 | `qwen3.8-27b-cuda-gpu:1`, `127.0.0.1` | 400,000, 12,000 | local; saved at `mai-code-2`: 400,000 × $0.50 + 12,000 × $2.50 per 1M = $0.23 |
| `c…c` | 1 | `gpt-unknown`, none | 1,000, 100 | `unknown_model` (GitHub channel, unpriced) |
| `c…c` | 2 | `mystery-1`, `llm.example.com` | 2,000, 200 | `unrecognized_endpoint` (unpriced channel) |

Plus `d…d` (`claude-opus-5.5`, 10,000 and 1,000 tokens = 6 credits) on `main`, and `e…e` (`mai-code-2`, 10,000 and 1,000 tokens = 0.75 credits) on `contoso/api` `feature/ledger`. Trips: trace `a…a` on `feature/ledger` (open), trace `b…b` on `feature/ledger` (reset), and trace `d…d` on `main`. So `feature/ledger` has 6 spans (2 unpriced), 342 credits, $3.42, $0.315 BYOK, $0.23 saved, 3 prompts, 2 trips, and its costliest prompt is trace `a…a`: ($2.86 + $0.315) / $0.01 = 317.5 credits.

```csharp
using System.Globalization;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.TestSupport;

public static class ReceiptSeeds
{
    // Records eight spans and three breaker trips. Six spans in three traces are on swigerb/copilot-ledger feature/ledger
    // (GitFixture.Feature(root)); one is on its main branch, and one is on contoso/api feature/ledger. Two trips are on
    // feature/ledger (one of them reset) and one is on main. Returns the number of spans recorded (8).
    public static async Task<int> SeedAsync(SqliteLedgerStore store, string root)
    {
        JsonRateCardCalculator embedded = JsonRateCardCalculator.LoadEmbedded();
        JsonRateCardCalculator byok = JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson);
        GitWorkspaceContext feature = GitFixture.Feature(root);
        GitWorkspaceContext main = feature with { BranchName = "main" };
        GitWorkspaceContext api = new(
            Path.Combine(root, "api"),
            Path.Combine(root, "api", ".git"),
            "feature/ledger",
            GitFixture.Sha1,
            "https://github.com/contoso/api.git",
            "contoso/api");

        NormalizedGenAiSpan opus = Span('a', 1, "13:00", "claude-opus-5.5", null, 1_200_000, 50_000, 800_000, 100_000);
        NormalizedGenAiSpan foundry = Span('a', 2, "13:01", "gpt-6.1-sol", "contoso.openai.azure.com", 100_000, 10_000);
        NormalizedGenAiSpan mai = Span('b', 1, "13:10", "mai-code-2", null, 1_000_000, 100_000, 400_000);
        NormalizedGenAiSpan qwen = Span('b', 2, "13:11", "qwen3.8-27b-cuda-gpu:1", "127.0.0.1", 400_000, 12_000);
        NormalizedGenAiSpan unknown = Span('c', 1, "13:20", "gpt-unknown", null, 1_000, 100);
        NormalizedGenAiSpan mystery = Span('c', 2, "13:21", "mystery-1", "llm.example.com", 2_000, 200);
        NormalizedGenAiSpan onMain = Span('d', 1, "13:30", "claude-opus-5.5", null, 10_000, 1_000);
        NormalizedGenAiSpan onApi = Span('e', 1, "13:40", "mai-code-2", null, 10_000, 1_000);

        int recorded = await store.RecordAsync(
        [
            new LedgerEntry(opus, embedded.Calculate(opus), feature),
            new LedgerEntry(foundry, byok.Calculate(foundry), feature),
            new LedgerEntry(mai, embedded.Calculate(mai), feature),
            new LedgerEntry(qwen, embedded.Calculate(qwen), feature),
            new LedgerEntry(unknown, embedded.Calculate(unknown), feature),
            new LedgerEntry(mystery, embedded.Calculate(mystery), feature),
            new LedgerEntry(onMain, embedded.Calculate(onMain), main),
            new LedgerEntry(onApi, embedded.Calculate(onApi), api),
        ]);

        await store.RecordBreakerTripAsync(Trip(new string('a', 32), "feature/ledger"));
        long? reset = await store.RecordBreakerTripAsync(Trip(new string('b', 32), "feature/ledger"));
        await store.ResetBreakerTripsAsync(new BreakerTripFilter { Id = reset });
        await store.RecordBreakerTripAsync(Trip(new string('d', 32), "main"));
        return recorded;
    }

    private static NormalizedGenAiSpan Span(
        char trace,
        int index,
        string time,
        string model,
        string? serverAddress,
        long inputTokens,
        long outputTokens,
        long cacheReadInputTokens = 0,
        long cacheWriteInputTokens = 0) =>
        TestSpans.Chat(
            startTime: DateTimeOffset.Parse($"2026-11-21T{time}:00Z", CultureInfo.InvariantCulture),
            requestModel: model,
            serverAddress: serverAddress,
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            cacheReadInputTokens: cacheReadInputTokens,
            cacheWriteInputTokens: cacheWriteInputTokens) with
        {
            TraceId = new string(trace, 32),
            SpanId = new string(trace, 15) + index.ToString(CultureInfo.InvariantCulture),
        };

    private static NewBreakerTrip Trip(string traceId, string branch) => new()
    {
        Rule = BreakerRule.CreditVelocity,
        TraceId = traceId,
        Repository = "swigerb/copilot-ledger",
        Branch = branch,
        TrippedAt = DateTimeOffset.Parse("2026-11-21T14:00:00Z", CultureInfo.InvariantCulture),
        Detail = "180.00 AI credits in 5 minutes",
        WindowCostUsd = 1.8m,
        WindowMinutes = 5,
    };
}
```

#### `FileRetry.ReadAllBytesAsync` (copy exactly)

Add to `tests/CopilotLedger.Tests/TestSupport/FileRetry.cs`, after `ReadAllTextAsync`, with a blank line before it:

```csharp
    // Like ReadAllTextAsync, for the file's bytes.
    public static async Task<byte[]> ReadAllBytesAsync(string path)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            try
            {
                return await File.ReadAllBytesAsync(path);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(25);
            }
        }
    }
```

#### Store: `tests/CopilotLedger.Tests/Storage/ReceiptQueryTests.cs` (copy exactly)

- **RQ1** checks the six channel-and-model rows across all repositories, in order, with every count and amount. `claude-opus-5.5` adds `main`'s 6 credits (292), and `mai-code-2` adds `contoso/api`'s 0.75 credits (56.75, $0.5675).
- **RQ2** checks that the four existing groupings leave `Channel` null.
- **RQ3** checks prompt counts: 5 in all, 3 on `feature/ledger`, 1 in `contoso/api`, 2 between 13:10 and 13:30, and 0 on an unknown branch.
- **RQ4** checks trip counts: 2 on `feature/ledger` (one reset), 1 on `main`, 0 in `contoso/api`, and 0 for `Feature/Ledger` (keys are case-sensitive).

```csharp
using System.Globalization;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

public sealed class ReceiptQueryTests
{
    [Fact]
    public async Task RQ1_ChannelModelRollupGroupsByChannelAndModel()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp.Path);

        IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
            LedgerGrouping.ChannelModel,
            LedgerFilter.All);

        Assert.Equal(6, rows.Count);
        AssertRow(rows[0], BillingChannel.GitHubAiCredits, "claude-opus-5.5", 2, 0, 1_210_000, 51_000, 800_000, 100_000, 292m, 2.92m, 0m, 0m);
        AssertRow(rows[1], BillingChannel.GitHubAiCredits, "mai-code-2", 2, 0, 1_010_000, 101_000, 400_000, 0, 56.75m, 0.5675m, 0m, 0m);
        AssertRow(rows[2], BillingChannel.FoundryByok, "gpt-6.1-sol", 1, 0, 100_000, 10_000, 0, 0, 0m, 0m, 0.315m, 0m);
        AssertRow(rows[3], BillingChannel.Local, "qwen3.8-27b", 1, 0, 400_000, 12_000, 0, 0, 0m, 0m, 0m, 0.23m);
        AssertRow(rows[4], BillingChannel.GitHubAiCredits, "gpt-unknown", 1, 1, 1_000, 100, 0, 0, 0m, 0m, 0m, 0m);
        AssertRow(rows[5], BillingChannel.Unpriced, "mystery-1", 1, 1, 2_000, 200, 0, 0, 0m, 0m, 0m, 0m);
        Assert.All(rows, row =>
        {
            Assert.Null(row.Repository);
            Assert.Null(row.Branch);
            Assert.Null(row.Agent);
        });
    }

    [Fact]
    public async Task RQ2_ChannelIsSetOnlyForChannelModelRows()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp.Path);

        foreach (LedgerGrouping grouping in new[] { LedgerGrouping.Repository, LedgerGrouping.Branch, LedgerGrouping.Model, LedgerGrouping.Agent })
        {
            IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(grouping, LedgerFilter.All);
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Null(row.Channel));
        }
    }

    [Fact]
    public async Task RQ3_PromptCountsDistinctTracesThatMatchTheFilter()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp.Path);

        Assert.Equal(5L, await store.GetPromptCountAsync(LedgerFilter.All));
        Assert.Equal(3L, await store.GetPromptCountAsync(new LedgerFilter { Repository = "swigerb/copilot-ledger", Branch = "feature/ledger" }));
        Assert.Equal(1L, await store.GetPromptCountAsync(new LedgerFilter { Repository = "contoso/api" }));
        Assert.Equal(2L, await store.GetPromptCountAsync(new LedgerFilter { From = DateTimeOffset.Parse("2026-11-21T13:10:00Z", CultureInfo.InvariantCulture), To = DateTimeOffset.Parse("2026-11-21T13:30:00Z", CultureInfo.InvariantCulture) }));
        Assert.Equal(0L, await store.GetPromptCountAsync(new LedgerFilter { Branch = "nope" }));
    }

    [Fact]
    public async Task RQ4_CountBreakerTripsCountsOpenAndResetTripsForOneBranch()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp.Path);

        Assert.Equal(2L, await store.CountBreakerTripsAsync("swigerb/copilot-ledger", "feature/ledger"));
        Assert.Equal(1L, await store.CountBreakerTripsAsync("swigerb/copilot-ledger", "main"));
        Assert.Equal(0L, await store.CountBreakerTripsAsync("contoso/api", "feature/ledger"));
        Assert.Equal(0L, await store.CountBreakerTripsAsync("swigerb/copilot-ledger", "Feature/Ledger"));
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(string root)
    {
        SqliteLedgerStore store = new(Path.Combine(root, "ledger.db"));
        await store.InitializeAsync();
        Assert.Equal(8, await ReceiptSeeds.SeedAsync(store, root));
        return store;
    }

    private static void AssertRow(
        LedgerRollupRow row,
        BillingChannel channel,
        string model,
        long spans,
        long unpriced,
        long input,
        long output,
        long cacheRead,
        long cacheWrite,
        decimal credits,
        decimal githubUsd,
        decimal byokUsd,
        decimal savedUsd)
    {
        Assert.Equal(channel, row.Channel);
        Assert.Equal(model, row.Model);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(unpriced, row.UnpricedSpanCount);
        Assert.Equal(input, row.InputTokens);
        Assert.Equal(output, row.OutputTokens);
        Assert.Equal(cacheRead, row.CacheReadInputTokens);
        Assert.Equal(cacheWrite, row.CacheWriteInputTokens);
        Assert.Equal(credits, row.AiCredits);
        Assert.Equal(githubUsd, row.AiCreditsUsd);
        Assert.Equal(byokUsd, row.FoundryByokUsd);
        Assert.Equal(savedUsd, row.LocalSavedUsd);
    }
}
```

#### Command: `tests/CopilotLedger.Tests/Cli/ReceiptCommandTests.cs` (copy exactly)

- **EP1** runs `export-pr` from `<root>/repo/src` and compares `Out`, `Error`, the only file in the receipts directory, its first byte (no byte order mark), and its whole text with `FeatureReceipt`.
- **EP2** compares `--stdout` with the same text and checks that no `.github` directory was created.
- **EP3** compares `--json` with the JSON block inside that text, plus `\n`.
- **EP4** reconciles `export-pr --json` with `branch --since all --json` field by field, checks the four amounts, and checks that the models' credits add up to the total.
- **EP5** points `HEAD` at `fix/@octocat-#7`, a branch with no spend, and compares the whole empty receipt; the mention and the issue reference stay inside code spans.
- **EP6** covers the four Git problems; each exits 1 with exactly its error and writes nothing.
- **EP7** rejects `--stdout --json` together.
- **EP8** and **EP9** check `ReceiptMarkdown.FileName` and `ReceiptMarkdown.Code` vectors. The hashes are the first 8 hex digits of SHA-256 of the branch's UTF-8 bytes, computed independently in PowerShell.
- **EP10** records one span on `feature/ledger` whose model name is 22,000 `m` characters. The name appears three times (the summary line, the table, and the JSON), so the receipt is 1,765 + 3 × 21,999 = 67,762 bytes; 1,765 is that receipt's size with a one-character name, and sizes measured at 100 and 1,000 characters (2,062 and 4,762) confirm three bytes per character. `export-pr` and `export-pr --stdout` each exit 1 with nothing on `Out` and exactly `The receipt for feature/ledger is 67762 bytes; a receipt can be at most 65536 bytes.` and `\n` on `Error`. `export-pr --json` exits 0 with nothing on `Error`. No `.github` directory is created.

````csharp
using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Cli.Receipts;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class ReceiptCommandTests
{
    private const string FeatureFile = ".github/copilot-ledger/receipts/feature-ledger-0a69c755.md";

    private const string FeatureReceipt = """
        <!-- copilot-ledger-receipt -->
        ### CopilotLedger receipt for `feature/ledger`

        - **GitHub AI Credits:** 342.00 credits ($3.42) across 3 turns, 1 unpriced: `claude-opus-5.5` 286.00, `mai-code-2` 56.00, `gpt-unknown` 0.00
        - **Foundry BYOK:** $0.32 across 1 turn: `gpt-6.1-sol` $0.32
        - **Local offload:** 412,000 tokens across 1 turn ($0.00 billed • $0.23 cloud spend avoided): `qwen3.8-27b` 412,000
        - **Not priced:** 1 turn: `mystery-1`
        - **Circuit breaker:** 2 trips • max single-prompt burn: 317.50 credits

        <details>
        <summary>Details and data</summary>

        | Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | Cloud spend avoided |
        | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
        | GitHub AI Credits | `claude-opus-5.5` | 1 | 0 | 1,200,000 | 50,000 | 286.00 credits ($2.86) | $0.00 |
        | GitHub AI Credits | `mai-code-2` | 1 | 0 | 1,000,000 | 100,000 | 56.00 credits ($0.56) | $0.00 |
        | Foundry BYOK | `gpt-6.1-sol` | 1 | 0 | 100,000 | 10,000 | $0.32 | $0.00 |
        | Local | `qwen3.8-27b` | 1 | 0 | 400,000 | 12,000 | $0.00 | $0.23 |
        | GitHub AI Credits | `gpt-unknown` | 1 | 1 | 1,000 | 100 | 0.00 credits ($0.00) | $0.00 |
        | Not priced | `mystery-1` | 1 | 1 | 2,000 | 200 | $0.00 | $0.00 |

        Repository `swigerb/copilot-ledger` • 3 prompts • commit `0123456789ab` • generated 2026-11-21 15:00 UTC by gh-ledger <version>

        To check these totals, run `gh ledger branch --since all --json` in the repository and compare the `feature/ledger` row.

        ```json
        {
          "schemaVersion": 1,
          "repository": "swigerb/copilot-ledger",
          "branch": "feature/ledger",
          "commit": "0123456789abcdef0123456789abcdef01234567",
          "generatedAt": "2026-11-21T15:00:00.0000000+00:00",
          "generator": "gh-ledger <version>",
          "totals": {
            "spans": 6,
            "unpricedSpans": 2,
            "inputTokens": 2703000,
            "outputTokens": 172300,
            "cacheReadInputTokens": 1200000,
            "cacheWriteInputTokens": 100000,
            "aiCredits": 342,
            "aiCreditsUsd": 3.42,
            "foundryByokUsd": 0.315,
            "localSavedUsd": 0.23
          },
          "prompts": 3,
          "maxPromptCredits": 317.5,
          "breakerTrips": 2,
          "models": [
            {
              "channel": "github_ai_credits",
              "model": "claude-opus-5.5",
              "spans": 1,
              "unpricedSpans": 0,
              "inputTokens": 1200000,
              "outputTokens": 50000,
              "cacheReadInputTokens": 800000,
              "cacheWriteInputTokens": 100000,
              "aiCredits": 286,
              "aiCreditsUsd": 2.86,
              "foundryByokUsd": 0,
              "localSavedUsd": 0
            },
            {
              "channel": "github_ai_credits",
              "model": "mai-code-2",
              "spans": 1,
              "unpricedSpans": 0,
              "inputTokens": 1000000,
              "outputTokens": 100000,
              "cacheReadInputTokens": 400000,
              "cacheWriteInputTokens": 0,
              "aiCredits": 56,
              "aiCreditsUsd": 0.56,
              "foundryByokUsd": 0,
              "localSavedUsd": 0
            },
            {
              "channel": "foundry_byok",
              "model": "gpt-6.1-sol",
              "spans": 1,
              "unpricedSpans": 0,
              "inputTokens": 100000,
              "outputTokens": 10000,
              "cacheReadInputTokens": 0,
              "cacheWriteInputTokens": 0,
              "aiCredits": 0,
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0.315,
              "localSavedUsd": 0
            },
            {
              "channel": "local",
              "model": "qwen3.8-27b",
              "spans": 1,
              "unpricedSpans": 0,
              "inputTokens": 400000,
              "outputTokens": 12000,
              "cacheReadInputTokens": 0,
              "cacheWriteInputTokens": 0,
              "aiCredits": 0,
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0.23
            },
            {
              "channel": "github_ai_credits",
              "model": "gpt-unknown",
              "spans": 1,
              "unpricedSpans": 1,
              "inputTokens": 1000,
              "outputTokens": 100,
              "cacheReadInputTokens": 0,
              "cacheWriteInputTokens": 0,
              "aiCredits": 0,
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0
            },
            {
              "channel": "unpriced",
              "model": "mystery-1",
              "spans": 1,
              "unpricedSpans": 1,
              "inputTokens": 2000,
              "outputTokens": 200,
              "cacheReadInputTokens": 0,
              "cacheWriteInputTokens": 0,
              "aiCredits": 0,
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0
            }
          ]
        }
        ```

        </details>

        """;

    private const string EmptyReceipt = """
        <!-- copilot-ledger-receipt -->
        ### CopilotLedger receipt for `fix/@octocat-#7`

        - **GitHub AI Credits:** none
        - **Foundry BYOK:** none
        - **Local offload:** none
        - **Circuit breaker:** 0 trips • max single-prompt burn: 0.00 credits

        <details>
        <summary>Details and data</summary>

        No spans are recorded for this branch.

        Repository `swigerb/copilot-ledger` • 0 prompts • commit `0123456789ab` • generated 2026-11-21 15:00 UTC by gh-ledger <version>

        To check these totals, run `gh ledger branch --since all --json` in the repository and compare the `fix/@octocat-#7` row.

        ```json
        {
          "schemaVersion": 1,
          "repository": "swigerb/copilot-ledger",
          "branch": "fix/@octocat-#7",
          "commit": "0123456789abcdef0123456789abcdef01234567",
          "generatedAt": "2026-11-21T15:00:00.0000000+00:00",
          "generator": "gh-ledger <version>",
          "totals": {
            "spans": 0,
            "unpricedSpans": 0,
            "inputTokens": 0,
            "outputTokens": 0,
            "cacheReadInputTokens": 0,
            "cacheWriteInputTokens": 0,
            "aiCredits": 0,
            "aiCreditsUsd": 0,
            "foundryByokUsd": 0,
            "localSavedUsd": 0
          },
          "prompts": 0,
          "maxPromptCredits": 0,
          "breakerTrips": 0,
          "models": []
        }
        ```

        </details>

        """;

    private static ManualTimeProvider Time => new(
        DateTimeOffset.Parse("2026-11-21T15:00:00Z"),
        ManualTimeProvider.UtcMinus5);

    [Fact]
    public async Task EP1_ExportWritesTheExactReceiptFile()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun run = await harness.RunInAsync(Path.Combine(repo, "src"), "export-pr");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            $"Wrote {FeatureFile} for feature/ledger.\n" +
            "Commit it on the branch and push; the CopilotLedger action posts it on the pull request.\n",
            run.Out);
        Assert.Equal(string.Empty, run.Error);
        string[] files = Directory.GetFiles(Path.Combine(repo, ".github", "copilot-ledger", "receipts"));
        Assert.Equal([Path.Combine(repo, ".github", "copilot-ledger", "receipts", "feature-ledger-0a69c755.md")], files);
        byte[] bytes = await FileRetry.ReadAllBytesAsync(files[0]);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal(Expected(FeatureReceipt), System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task EP2_StdoutWritesTheMarkdownAndNoFile()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun run = await harness.RunInAsync(repo, "export-pr", "--stdout");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(Expected(FeatureReceipt), run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.False(Directory.Exists(Path.Combine(repo, ".github")));
    }

    [Fact]
    public async Task EP3_JsonIsTheReceiptDataBlock()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun run = await harness.RunInAsync(repo, "export-pr", "--json");

        Assert.Equal(0, run.ExitCode);
        string receipt = Expected(FeatureReceipt);
        int start = receipt.IndexOf("```json\n", StringComparison.Ordinal) + "```json\n".Length;
        int end = receipt.IndexOf("\n```\n", start, StringComparison.Ordinal);
        Assert.Equal(receipt[start..end] + "\n", run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.False(Directory.Exists(Path.Combine(repo, ".github")));
    }

    [Fact]
    public async Task EP4_TotalsReconcileWithBranchJson()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun receipt = await harness.RunInAsync(repo, "export-pr", "--json");
        CliRun branch = await harness.RunInAsync(repo, "branch", "--since", "all", "--json");

        Assert.Equal(0, receipt.ExitCode);
        Assert.Equal(0, branch.ExitCode);
        using JsonDocument receiptJson = JsonDocument.Parse(receipt.Out);
        using JsonDocument branchJson = JsonDocument.Parse(branch.Out);
        JsonElement totals = receiptJson.RootElement.GetProperty("totals");
        JsonElement row = Assert.Single(
            branchJson.RootElement.GetProperty("rows").EnumerateArray(),
            item => item.GetProperty("branch").GetString() == "feature/ledger");
        Assert.Equal("swigerb/copilot-ledger", row.GetProperty("repository").GetString());
        foreach (string name in new[] { "spans", "unpricedSpans", "inputTokens", "outputTokens", "cacheReadInputTokens", "cacheWriteInputTokens" })
        {
            Assert.Equal(row.GetProperty(name).GetInt64(), totals.GetProperty(name).GetInt64());
        }

        foreach (string name in new[] { "aiCredits", "aiCreditsUsd", "foundryByokUsd", "localSavedUsd" })
        {
            Assert.Equal(row.GetProperty(name).GetDecimal(), totals.GetProperty(name).GetDecimal());
        }

        Assert.Equal(342m, totals.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(3.42m, totals.GetProperty("aiCreditsUsd").GetDecimal());
        Assert.Equal(0.315m, totals.GetProperty("foundryByokUsd").GetDecimal());
        Assert.Equal(0.23m, totals.GetProperty("localSavedUsd").GetDecimal());
        decimal modelCredits = receiptJson.RootElement.GetProperty("models").EnumerateArray()
            .Sum(item => item.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(342m, modelCredits);
    }

    [Fact]
    public async Task EP5_BranchWithoutSpendGetsAnEmptyReceipt()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");
        string git = Path.Combine(repo, ".git");
        Directory.CreateDirectory(Path.Combine(git, "refs", "heads", "fix"));
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/fix/@octocat-#7\n");
        File.WriteAllText(Path.Combine(git, "refs", "heads", "fix", "@octocat-#7"), GitFixture.Sha1 + "\n");
        await FileRetry.ReadAllTextAsync(Path.Combine(git, "HEAD"));
        await FileRetry.ReadAllTextAsync(Path.Combine(git, "refs", "heads", "fix", "@octocat-#7"));

        CliRun run = await harness.RunInAsync(repo, "export-pr");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            "Wrote .github/copilot-ledger/receipts/fix--octocat--7-98e51822.md for fix/@octocat-#7.\n" +
            "Commit it on the branch and push; the CopilotLedger action posts it on the pull request.\n",
            run.Out);
        Assert.Equal(
            Expected(EmptyReceipt),
            await FileRetry.ReadAllTextAsync(Path.Combine(repo, ".github", "copilot-ledger", "receipts", "fix--octocat--7-98e51822.md")));
    }

    public static TheoryData<string, string> GitProblems => new()
    {
        { "no-repository", "<work> isn't in a Git repository.\n" },
        { "detached", "HEAD is detached. Check out the pull request's branch, then run export-pr again.\n" },
        { "unborn", "feature/ledger has no commits yet.\n" },
        { "no-origin", "export-pr needs a GitHub remote named origin, and <repo> has none.\n" },
    };

    // Assumes no directory above the temp directory has a .git entry.
    [Theory]
    [MemberData(nameof(GitProblems))]
    public async Task EP6_GitProblemsFailWithoutWriting(string problem, string error)
    {
        using TempDirectory temp = new();
        string work = Path.Combine(temp.Path, "work");
        Directory.CreateDirectory(work);
        string repo = GitFixture.Create(temp.Path);
        string git = Path.Combine(repo, ".git");
        switch (problem)
        {
            case "detached":
                File.WriteAllText(Path.Combine(git, "HEAD"), GitFixture.Sha1 + "\n");
                await FileRetry.ReadAllTextAsync(Path.Combine(git, "HEAD"));
                break;
            case "unborn":
                File.Delete(Path.Combine(git, "refs", "heads", "feature", "ledger"));
                break;
            case "no-origin":
                File.WriteAllText(Path.Combine(git, "config"), "[core]\n\tbare = false\n");
                await FileRetry.ReadAllTextAsync(Path.Combine(git, "config"));
                break;
        }

        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);

        CliRun run = await harness.RunInAsync(problem == "no-repository" ? work : repo, "export-pr");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal(error.Replace("<work>", work).Replace("<repo>", repo), run.Error);
        Assert.False(Directory.Exists(Path.Combine(repo, ".github")));
    }

    [Fact]
    public async Task EP7_StdoutAndJsonTogetherFail()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunInAsync(
            Path.Combine(temp.Path, "repo"),
            "export-pr",
            "--stdout",
            "--json");

        Assert.Equal(1, run.ExitCode);
        Assert.StartsWith("Use either --stdout or --json, not both.\n", run.Error);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "repo", ".github")));
    }

    public static TheoryData<string, string> FileNames => new()
    {
        { "feature/ledger", "feature-ledger-0a69c755.md" },
        { "fix/@octocat-#7", "fix--octocat--7-98e51822.md" },
        { "Feature/Ledger", "Feature-Ledger-a4b486e4.md" },
        { "release/v1.2_rc-1", "release-v1.2_rc-1-7a79d7e7.md" },
        { "topic/\u00FCn\u00EFcode", "topic--n-code-ae54b3cf.md" },
        { new string('a', 70), new string('a', 64) + "-6bd5e503.md" },
        { "con", "con-1143da2b.md" },
    };

    [Theory]
    [MemberData(nameof(FileNames))]
    public void EP8_ReceiptFileNames(string branch, string fileName)
    {
        Assert.Equal(fileName, ReceiptMarkdown.FileName(branch));
    }

    public static TheoryData<string, string> CodeSpans => new()
    {
        { "feature/ledger", "`feature/ledger`" },
        { "@octocat", "`@octocat`" },
        { "a`b", "``a`b``" },
        { "`tick", "`` `tick ``" },
        { " lead", "`  lead `" },
        { "a``b```c", "````a``b```c````" },
        { "x\ny\u2028z", "`x\uFFFDy\uFFFDz`" },
    };

    [Theory]
    [MemberData(nameof(CodeSpans))]
    public void EP9_CodeSpansShowValuesLiterally(string value, string span)
    {
        Assert.Equal(span, ReceiptMarkdown.Code(value));
    }

    // The model's 22,000-character name appears three times: in the summary, the table, and the JSON data.
    [Fact]
    public async Task EP10_ReceiptsOverTheSizeLimitAreRefused()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: new string('m', 22_000),
            inputTokens: 1_000,
            outputTokens: 100);
        await store.RecordAsync(
        [
            new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), GitFixture.Feature(temp.Path)),
        ]);
        string repo = Path.Combine(temp.Path, "repo");
        const string Error =
            "The receipt for feature/ledger is 67762 bytes; a receipt can be at most 65536 bytes.\n";

        CliRun file = await harness.RunInAsync(repo, "export-pr");
        CliRun stdout = await harness.RunInAsync(repo, "export-pr", "--stdout");
        CliRun json = await harness.RunInAsync(repo, "export-pr", "--json");

        Assert.Equal(1, file.ExitCode);
        Assert.Equal(string.Empty, file.Out);
        Assert.Equal(Error, file.Error);
        Assert.Equal(1, stdout.ExitCode);
        Assert.Equal(string.Empty, stdout.Out);
        Assert.Equal(Error, stdout.Error);
        Assert.Equal(0, json.ExitCode);
        Assert.Equal(string.Empty, json.Error);
        Assert.False(Directory.Exists(Path.Combine(repo, ".github")));
    }

    private static string Expected(string template) =>
        template.ReplaceLineEndings("\n").Replace("<version>", GhLedgerCli.Version, StringComparison.Ordinal);

    private static async Task<CliHarness> SeededHarnessAsync(string root)
    {
        GitFixture.Create(root);
        CliHarness harness = new(root, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(Path.Combine(root, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        await ReceiptSeeds.SeedAsync(store, root);
        return harness;
    }
}
````

### Task 3 tests

`action/tests/receipt.Tests.ps1` (copy exactly). It fakes the GitHub API with a function named `Invoke-WebRequest`, runs `receipt.ps1` once per case with the action's environment variables, and checks the exit code, every log line, the `GITHUB_OUTPUT` file, every request in order, the posted body, and the request headers. An unexpected request fails only its own case: `Invoke-Receipt` catches the exception, records it as the only log line, and sets exit code 99. `Assert-Equal` compares JSON forms ordinally, and the JSON escapes non-ASCII characters, so a failure shows a zero-width space or byte order mark as `\u200b` or `\ufeff`. The architect's mutation checks each failed exactly one case: comparing branches with `OrdinalIgnoreCase` failed AC15, comparing them with `-ceq` failed AC16, checking the marker with `-cne` failed AC17, and comparing logins with `-ceq` failed AC18.

| Case | Setup | Expected |
|---|---|---|
| AC1 | An added receipt, no comments | `POST` with the receipt as the body; `result=created`; the three headers |
| AC2 | The bot's older receipt comment after someone else's comment | `PATCH` comment 55; `result=updated` |
| AC3 | The bot's comment with the same body in `\r\n` form | No write; `result=unchanged` |
| AC4 | A marker comment by `octocat` | Left alone; a new comment; `result=created` |
| AC5 | Only a removed receipt and a non-`.md` file | Notice; `result=no-receipt`; one request |
| AC6 | A fork's pull request | Notice; `result=skipped`; no requests |
| AC7 | A `pull_request_target` event | Error; exit 1; no outputs; no requests |
| AC8 | A receipt for another branch | Error naming both branches |
| AC9 | A file without the marker | Error naming the file |
| AC10 | `size` 65,537 | Error before decoding |
| AC11 | Invalid JSON in the block | Error starting with the file and `has invalid JSON data:` |
| AC12 | `"schemaVersion": 2` | Error |
| AC13 | 100 files on page 1, the receipt on page 2 | Both pages read, then the usual requests |
| AC14 | `receipts-directory` `/.github/copilot-ledger/receipts/` | Still found |
| AC15 | Head branch `Feature/Ledger` | Error: branch names are case-sensitive |
| AC16 | The receipt's branch is `feature/ledger` plus U+200B (zero-width space) | Error listing that branch; no comment requests |
| AC17 | The receipt starts with U+FEFF (byte order mark) | The marker error naming the file; no comment requests |
| AC18 | `comment-author` `GitHub-Actions[bot]`; the bot's comment is by `github-actions[bot]` | `PATCH` that comment; `result=updated` |

````powershell
#Requires -Version 7.4
# Runs action/receipt.ps1 against a fake GitHub REST API. No network.
# Usage: pwsh -NoProfile -File action/tests/receipt.Tests.ps1

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

### Task 4 tests

1. In `DaemonCommandTests.DM1`, the version assertion becomes `Assert.Equal("0.4.0", root.GetProperty("version").GetString());`.
2. In `ReportCommandTests.StatusText`, the first line becomes `gh-ledger 0.4.0`.
3. Add to `InstallCommandTests`, after the `WindowsPlatform()` helper (copy exactly):

```csharp
    private const string WindowsExtension =
        @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe";

    [Fact]
    public async Task IN17_WindowsExtensionInstallSkipsTheAlias()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        platform.ProcessPath = WindowsExtension;
        platform.ExistingFiles.Add(
            @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\manifest.yml");
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            "Set COPILOT_OTEL_ENABLED=true for your user account.\n" +
            "gh ledger runs this GitHub CLI extension, so no alias is needed.\n" +
            "gh-ledger will start when you sign in.\n" +
            $"gh-ledger daemon started (pid {Environment.ProcessId}) on http://localhost:4318.\n" +
            "CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n",
            run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Empty(platform.ProcessRuns);
        Assert.Equal(
            [("CopilotLedger", $"\"{WindowsExtension}\" daemon start")],
            platform.StartupCommandWrites);
        DetachedStart start = Assert.Single(platform.DetachedStarts);
        Assert.Equal(WindowsExtension, start.FileName);
        using JsonDocument manifest = JsonDocument.Parse(
            await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));
        Assert.Equal(
            WindowsExtension,
            manifest.RootElement.GetProperty("executablePath").GetString());
        Assert.False(manifest.RootElement.GetProperty("ghAliasSet").GetBoolean());
    }

    [Fact]
    public async Task IN18_LinuxExtensionInstallStartsTheExtensionAtSignIn()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = UnixPlatform(temp.Path);
        platform.ProcessPath = "/home/me/.local/share/gh/extensions/gh-ledger/gh-ledger";
        platform.ExistingFiles.Add("/home/me/.local/share/gh/extensions/gh-ledger/manifest.yml");
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            $"Added COPILOT_OTEL_ENABLED=true to {Path.Combine(temp.Path, "home", ".zshrc")}.\n" +
            "gh ledger runs this GitHub CLI extension, so no alias is needed.\n" +
            "gh-ledger will start when you sign in.\n" +
            $"gh-ledger daemon started (pid {Environment.ProcessId}) on http://localhost:4318.\n" +
            "CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n",
            run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Empty(platform.ProcessRuns);
        Assert.Equal(
            """
            [Desktop Entry]
            Type=Application
            Name=CopilotLedger
            Comment=Starts the CopilotLedger daemon
            Exec="/home/me/.local/share/gh/extensions/gh-ledger/gh-ledger" daemon start
            Terminal=false
            """.ReplaceLineEndings("\n") + "\n",
            await FileRetry.ReadAllTextAsync(Path.Combine(
                temp.Path,
                "home",
                ".config",
                "autostart",
                "copilot-ledger.desktop")));
    }

    public static TheoryData<string, string?> NotExtensions => new()
    {
        { @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe", null },
        { @"C:\Users\me\AppData\Local\GitHub CLI\other\gh-ledger\gh-ledger.exe", @"C:\Users\me\AppData\Local\GitHub CLI\other\gh-ledger\manifest.yml" },
        { @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-tools\gh-ledger.exe", @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-tools\manifest.yml" },
    };

    [Theory]
    [MemberData(nameof(NotExtensions))]
    public async Task IN19_OnlyTheGhExtensionLayoutSkipsTheAlias(string executable, string? existingManifest)
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        platform.ProcessPath = executable;
        if (existingManifest is not null)
        {
            platform.ExistingFiles.Add(existingManifest);
        }

        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        (string gh, IReadOnlyList<string> arguments) = Assert.Single(platform.ProcessRuns);
        Assert.Equal(@"C:\gh\gh.exe", gh);
        Assert.Equal(
            ["alias", "set", "ledger", $"'{executable.Replace('\\', '/')}' \"$@\"", "--shell", "--clobber"],
            arguments);
        Assert.DoesNotContain("no alias is needed", run.Out);
    }
```

   - **IN17**: as the Windows extension, the whole `Out`, no `gh` call, the Run value and daemon use the extension's path, and the manifest records it with `ghAliasSet` false.
   - **IN18**: as the Linux extension, the whole `Out`, no `gh` call, and the exact desktop entry.
   - **IN19**: without `manifest.yml`, in a directory not named `extensions`, or in an extension directory not named `gh-ledger`, `install` sets the alias as before.
4. Add to `ReportCommandTests`, after RP11 (copy exactly):

```csharp
    [Fact]
    public async Task RP12_StatusAsGhExtensionNamesGhLedgerCommands()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(
            temp.Path,
            platform =>
            {
                platform.ProcessPath = "/home/me/.local/share/gh/extensions/gh-ledger/gh-ledger";
                platform.ExistingFiles.Add("/home/me/.local/share/gh/extensions/gh-ledger/manifest.yml");
            });

        CliRun text = await harness.RunAsync("status");
        CliRun json = await harness.RunAsync("status", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            StatusText(temp.Path)
                .Replace($"gh-ledger {GhLedgerCli.Version}\n", $"gh-ledger {GhLedgerCli.Version} (GitHub CLI extension)\n", StringComparison.Ordinal)
                .Replace("start it with: gh-ledger daemon start", "start it with: gh ledger daemon start", StringComparison.Ordinal)
                .Replace("run: gh-ledger install", "run: gh ledger install", StringComparison.Ordinal),
            text.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.True(document.RootElement.GetProperty("ghExtension").GetBoolean());
        Assert.Empty(document.RootElement.GetProperty("warnings").EnumerateArray());
    }

    [Fact]
    public async Task RP13_StatusWarnsWhenInstalledFromAnotherExecutable()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string manifest = Path.Combine(temp.Path, "data", "install.json");
        await File.WriteAllTextAsync(
            manifest,
            """
            {"schemaVersion":1,"installedAt":"2026-11-20T00:00:00+00:00","executablePath":"/opt/old/gh-ledger","windowsUserVariableSet":false,"windowsUserVariablePreviousValue":null,"profileFiles":[],"ghAliasSet":false}
            """);
        await FileRetry.ReadAllTextAsync(manifest);
        const string Warning =
            "CopilotLedger was installed from /opt/old/gh-ledger; run gh-ledger install to use this copy instead.";

        CliRun text = await harness.RunAsync("status");
        CliRun json = await harness.RunAsync("status", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            StatusText(temp.Path).Replace(
                "run: gh-ledger install\n",
                $"run: gh-ledger install\nWarning: {Warning}\n",
                StringComparison.Ordinal),
            text.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.False(document.RootElement.GetProperty("ghExtension").GetBoolean());
        Assert.Equal(
            [Warning],
            document.RootElement.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()));
    }
```

   - **RP12**: as the extension, `status` is the usual text with the three changed lines, and `ghExtension` is true with no warnings.
   - **RP13**: an `install.json` that names `/opt/old/gh-ledger` (read back with `FileRetry` before `status` runs) adds one warning line after the Copilot line and one entry to `warnings`.

### Task 5 tests

#### Support (copy exactly)

`tests/CopilotLedger.Tests/TestSupport/FakeHttpHandler.cs`:

```csharp
namespace CopilotLedger.Tests.TestSupport;

public sealed record HttpCall(string Method, string Uri, string UserAgent);

// Answers every request with respond(request). An exception that respond throws becomes the faulted result of SendAsync.
public sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpCall> Calls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Calls.Add(new HttpCall(
            request.Method.Method,
            request.RequestUri!.AbsoluteUri,
            request.Headers.UserAgent.ToString()));
        try
        {
            return Task.FromResult(respond(request));
        }
        catch (Exception ex)
        {
            return Task.FromException<HttpResponseMessage>(ex);
        }
    }

    // 200 with the bytes and no Content-Type, like any body the product reads as bytes.
    public static HttpResponseMessage Bytes(byte[] body) =>
        new(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
}
```

`tests/CopilotLedger.Tests/TestSupport/PublishedRates.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Tests.TestSupport;

public static class PublishedRates
{
    public const string Url = "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json";

    // The embedded rates.json text, exactly as built into CopilotLedger.Core.
    public static string Embedded()
    {
        using Stream stream = typeof(JsonRateCardCalculator).Assembly
            .GetManifestResourceStream(JsonRateCardCalculator.EmbeddedResourceName)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    // The embedded rates with "ratesVersion": "2026-11-20" replaced by ratesVersion.
    public static string WithVersion(string ratesVersion) =>
        Embedded().Replace(
            "\"ratesVersion\": \"2026-11-20\"",
            $"\"ratesVersion\": \"{ratesVersion}\"",
            StringComparison.Ordinal);

    // WithVersion(ratesVersion) with claude-opus-5.5's inputPer1M set to 8 and outputPer1M to 40 (twice the built-in rates).
    public static string DoubledOpus(string ratesVersion)
    {
        JsonNode document = JsonNode.Parse(WithVersion(ratesVersion))!;
        JsonNode tier = document["githubAiCredits"]!["models"]!.AsArray()
            .Single(model => model!["id"]!.GetValue<string>() == "claude-opus-5.5")!["windows"]![0]!["tiers"]![0]!;
        tier["inputPer1M"] = 8m;
        tier["outputPer1M"] = 40m;
        return document.ToJsonString();
    }

    public static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    // WithVersion(ratesVersion) plus a GitHub model gpt-new, alias gpt-preview, at $1 input, $0.10 cached input, and
    // $4 output per 1M tokens.
    public static string WithGptNew(string ratesVersion)
    {
        JsonNode document = JsonNode.Parse(WithVersion(ratesVersion))!;
        document["githubAiCredits"]!["models"]!.AsArray().Add(JsonNode.Parse(
            """
            { "id": "gpt-new", "aliases": ["gpt-preview"], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 1, "cachedInputPer1M": 0.1, "cacheWritePer1M": null, "outputPer1M": 4 } ] } ] }
            """));
        return document.ToJsonString();
    }

    // An overlay that adds the GitHub model gpt-preview. It's valid with the built-in rates and conflicts with WithGptNew.
    public const string GptPreviewOverlay = """
        { "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "gpt-preview", "aliases": [], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 1, "cachedInputPer1M": 0.1, "cacheWritePer1M": null, "outputPer1M": 4 } ] } ] } ] } }
        """;
}
```

Responses use `ByteArrayContent` without a content type, never `StringContent`, because the product must not depend on a charset.

#### `tests/CopilotLedger.Tests/Cli/RatesUpdateTests.cs` (copy exactly)

- **RU1**: newer rates are saved byte for byte from one `GET` with the user agent `gh-ledger/<version>`, nothing else is written, and `rates validate`, `status`, and `status --json` report the downloaded rates.
- **RU2**: equal and older rates aren't saved.
- **RU3**: offline, 404, timeout, invalid UTF-8, invalid JSON, an incomplete document, and a non-date version each give exactly one error line and save nothing.
- **RU4**: rates that conflict with the overlay aren't saved, and the error carries the overlay's message.
- **RU5**: a download replaces an older download and is up to date against an equal one.
- **RU6**: an unreadable download is ignored with the same warning in `rates validate`, `status --json`, and `rates update`, which then replaces it.
- **RU7**: a download dated before the built-in rates is ignored without a warning.
- **RU8**: with the daemon running on downloaded rates that double `claude-opus-5.5`'s prices, `rates update` adds the restart line, and trace V's three chats are priced at the downloaded rates: 3 × (100,000 × $8 + 10,000 × $40) per 1M = $3.60, or 360 AI credits, instead of the built-in 180.
- **RU9**: the daemon logs the ignored download as its first line, still starts and stops, and exits with 0.
- **RU10**: the built-in `ratesVersion` is a `yyyy-MM-dd` date.

```csharp
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class RatesUpdateTests
{
    [Fact]
    public async Task RU1_NewerPublishedRatesAreSavedAndUsed()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string published = PublishedRates.WithVersion("2026-12-01");
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(published)));

        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("Updated rates from 2026-11-20 to 2026-12-01.\n", run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(
            [new HttpCall("GET", PublishedRates.Url, $"gh-ledger/{GhLedgerCli.Version}")],
            handler.Calls);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Assert.Equal(published, await FileRetry.ReadAllTextAsync(downloaded));
        Assert.Equal([downloaded], Directory.GetFiles(Path.Combine(temp.Path, "data")));

        CliRun validate = await harness.RunAsync("rates", "validate");
        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the downloaded rates (2026-12-01).\n",
            validate.Out);
        CliRun status = await harness.RunAsync("status");
        Assert.Equal(0, status.ExitCode);
        Assert.Equal("Rates:   2026-12-01 (downloaded)", status.Out.Split('\n')[3]);
        CliRun json = await harness.RunAsync("status", "--json");
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Equal("2026-12-01", document.RootElement.GetProperty("ratesVersion").GetString());
        Assert.Equal("downloaded", document.RootElement.GetProperty("ratesSource").GetString());
    }

    public static TheoryData<string, string> NotNewer => new()
    {
        { "2026-11-20", "Rates are up to date (2026-11-20).\n" },
        { "2026-10-01", "The published rates (2026-10-01) are older than the rates in use (2026-11-20); nothing changed.\n" },
    };

    [Theory]
    [MemberData(nameof(NotNewer))]
    public async Task RU2_RatesThatAreNotNewerAreNotSaved(string version, string output)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Bytes(
            PublishedRates.Utf8(PublishedRates.WithVersion(version))));

        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(output, run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.False(File.Exists(Path.Combine(temp.Path, "data", "rates.downloaded.json")));
    }

    public static TheoryData<string, string> Failures => new()
    {
        { "offline", $"Could not download rates from {PublishedRates.Url}: No such host is known. (github.com:443)\n" },
        { "404", $"Could not download rates from {PublishedRates.Url}: HTTP 404.\n" },
        { "timeout", $"Could not download rates from {PublishedRates.Url}: no response within 30 seconds.\n" },
        { "not-utf8", "The downloaded rates are invalid: the file isn't UTF-8.\n" },
        { "not-json", "The downloaded rates are invalid: line 1, column 2: invalid JSON\n" },
        { "incomplete", "The downloaded rates are invalid: source: is required\n" },
        { "not-a-date", "The downloaded rates are invalid: ratesVersion 2026-12 isn't a yyyy-MM-dd date.\n" },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task RU3_FailuresReportExactErrorsAndSaveNothing(string failure, string error)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        FakeHttpHandler handler = new(_ => failure switch
        {
            "offline" => throw new HttpRequestException("No such host is known. (github.com:443)"),
            "404" => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new ByteArrayContent(PublishedRates.Utf8("{\"error\":\"Not Found\"}")),
            },
            "timeout" => throw new TaskCanceledException(),
            "not-utf8" => FakeHttpHandler.Bytes([0xC3, 0x28]),
            "not-json" => FakeHttpHandler.Bytes(PublishedRates.Utf8("{")),
            "incomplete" => FakeHttpHandler.Bytes(PublishedRates.Utf8("{ \"schemaVersion\": 1, \"ratesVersion\": \"2026-12-01\" }")),
            "not-a-date" => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12"))),
            _ => throw new InvalidOperationException(failure),
        });

        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal(error, run.Error);
        Assert.False(File.Exists(Path.Combine(temp.Path, "data", "rates.downloaded.json")));
    }

    [Fact]
    public async Task RU4_OverlayConflictKeepsTheCurrentRates()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string overlay = Path.Combine(temp.Path, "data", "rates.json");
        Directory.CreateDirectory(Path.GetDirectoryName(overlay)!);
        await File.WriteAllTextAsync(overlay, PublishedRates.GptPreviewOverlay);
        await FileRetry.ReadAllTextAsync(overlay);
        Assert.Equal(0, (await harness.RunAsync("rates", "validate")).ExitCode);
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Bytes(
            PublishedRates.Utf8(PublishedRates.WithGptNew("2026-12-01"))));

        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal(
            $"The downloaded rates (2026-12-01) don't work with your rates overlay: {overlay}: githubAiCredits.models[0].id: duplicate model identity in the merged rates: gpt-preview (built-in model gpt-new)\n",
            run.Error);
        Assert.False(File.Exists(Path.Combine(temp.Path, "data", "rates.downloaded.json")));
    }

    [Fact]
    public async Task RU5_LaterRatesReplaceEarlierDownloads()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.WithVersion("2026-12-01"));
        await FileRetry.ReadAllTextAsync(downloaded);

        CliRun same = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-01")))),
            "rates",
            "update");
        CliRun later = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-15")))),
            "rates",
            "update");

        Assert.Equal(0, same.ExitCode);
        Assert.Equal("Rates are up to date (2026-12-01).\n", same.Out);
        Assert.Equal(0, later.ExitCode);
        Assert.Equal("Updated rates from 2026-12-01 to 2026-12-15.\n", later.Out);
        Assert.Equal(PublishedRates.WithVersion("2026-12-15"), await FileRetry.ReadAllTextAsync(downloaded));
    }

    [Fact]
    public async Task RU6_UnusableDownloadsAreIgnoredWithAWarning()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, "not json");
        await FileRetry.ReadAllTextAsync(downloaded);
        string warning = $"Warning: ignored {downloaded}: line 1, column 2: invalid JSON\n";

        CliRun validate = await harness.RunAsync("rates", "validate");
        CliRun status = await harness.RunAsync("status", "--json");
        CliRun update = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-01")))),
            "rates",
            "update");

        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            warning + $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the built-in rates (2026-11-20).\n",
            validate.Out);
        Assert.Equal(0, status.ExitCode);
        using JsonDocument document = JsonDocument.Parse(status.Out);
        Assert.Equal("2026-11-20", document.RootElement.GetProperty("ratesVersion").GetString());
        Assert.Equal("built-in", document.RootElement.GetProperty("ratesSource").GetString());
        Assert.Equal(
            [warning[9..^1]],
            document.RootElement.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(0, update.ExitCode);
        Assert.Equal(warning + "Updated rates from 2026-11-20 to 2026-12-01.\n", update.Out);
    }

    [Fact]
    public async Task RU7_DownloadsNotLaterThanTheBuiltInRatesAreIgnoredSilently()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.WithVersion("2026-11-01"));
        await FileRetry.ReadAllTextAsync(downloaded);

        CliRun validate = await harness.RunAsync("rates", "validate");

        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the built-in rates (2026-11-20).\n",
            validate.Out);
    }

    [Fact]
    public async Task RU8_RunningDaemonGetsARestartHintAndPricesWithDownloadedRates()
    {
        using TempDirectory temp = new();
        string repo = GitFixture.Create(temp.Path);
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.DoubledOpus("2026-12-01"));
        await FileRetry.ReadAllTextAsync(downloaded);
        await harness.StartInProcessDaemonAsync();

        CliRun update = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-15")))),
            "rates",
            "update");
        using HttpClient client = harness.CreateDaemonClient();
        using StringContent content = new(
            BreakerTraces.TraceVJson(repo),
            new MediaTypeHeaderValue("application/json"));
        using HttpResponseMessage response = await client.PostAsync("/v1/traces", content);
        CliRun branch = await harness.RunInAsync(repo, "branch", "--since", "all", "--json");

        Assert.Equal(0, update.ExitCode);
        Assert.Equal(
            "Updated rates from 2026-12-01 to 2026-12-15.\n" +
            "Restart the daemon to price new spans with them: gh-ledger daemon stop, then gh-ledger daemon start.\n",
            update.Out);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(0, branch.ExitCode);
        using JsonDocument document = JsonDocument.Parse(branch.Out);
        JsonElement row = Assert.Single(document.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal(3, row.GetProperty("spans").GetInt64());
        Assert.Equal(360m, row.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(3.6m, row.GetProperty("aiCreditsUsd").GetDecimal());
    }

    [Fact]
    public async Task RU9_DaemonLogsIgnoredDownloadsAndStillStarts()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(
            temp.Path,
            timeProvider: new ManualTimeProvider(
                DateTimeOffset.Parse("2026-11-21T15:00:00Z", CultureInfo.InvariantCulture)));
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, "not json");
        await FileRetry.ReadAllTextAsync(downloaded);

        await harness.StartInProcessDaemonAsync();
        CliRun stop = await harness.RunAsync("daemon", "stop");
        Assert.Equal(0, (await harness.InProcessDaemon!).ExitCode);

        Assert.Equal(0, stop.ExitCode);
        Assert.StartsWith(
            $"2026-11-21T15:00:00.0000000+00:00 Warning: ignored {downloaded}: line 1, column 2: invalid JSON\n",
            harness.DaemonOutput);
    }

    [Fact]
    public void RU10_BuiltInRatesVersionIsADate()
    {
        Assert.True(DateOnly.TryParseExact(
            JsonRateCardCalculator.LoadEmbedded().RatesVersion,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _));
    }
}
```

#### `tests/CopilotLedger.Tests/Pricing/LoadWithOverlayTests.cs` (copy exactly)

- **LW1**: a base without an overlay keeps its version.
- **LW2**: an overlay without a label gives `<base>+user`.
- **LW3**: an invalid or missing overlay's error starts with its path.
- **LW4**: a base's error has no prefix.

```csharp
using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class LoadWithOverlayTests
{
    [Fact]
    public void LW1_BaseWithoutOverlayKeepsItsVersion()
    {
        JsonRateCardCalculator calculator =
            JsonRateCardCalculator.LoadWithOverlay(PublishedRates.WithVersion("2026-12-01"), null);

        Assert.Equal("2026-12-01", calculator.RatesVersion);
    }

    [Fact]
    public async Task LW2_OverlayIsMergedOnTheGivenBase()
    {
        using TempDirectory temp = new();
        string overlay = Path.Combine(temp.Path, "rates.json");
        File.WriteAllText(overlay, PublishedRates.GptPreviewOverlay);
        await FileRetry.ReadAllTextAsync(overlay);

        JsonRateCardCalculator calculator =
            JsonRateCardCalculator.LoadWithOverlay(PublishedRates.WithVersion("2026-12-01"), overlay);

        Assert.Equal("2026-12-01+user", calculator.RatesVersion);
    }

    [Fact]
    public async Task LW3_OverlayErrorsStartWithTheOverlayPath()
    {
        using TempDirectory temp = new();
        string overlay = Path.Combine(temp.Path, "rates.json");
        File.WriteAllText(overlay, "{");
        await FileRetry.ReadAllTextAsync(overlay);

        InvalidDataException invalid = Assert.Throws<InvalidDataException>(() =>
            JsonRateCardCalculator.LoadWithOverlay(PublishedRates.WithVersion("2026-12-01"), overlay));
        InvalidDataException missing = Assert.Throws<InvalidDataException>(() =>
            JsonRateCardCalculator.LoadWithOverlay(
                PublishedRates.WithVersion("2026-12-01"),
                Path.Combine(temp.Path, "missing.json")));

        Assert.Equal($"{overlay}: line 1, column 2: invalid JSON", invalid.Message);
        Assert.Equal(
            $"{Path.Combine(temp.Path, "missing.json")}: could not read rates overlay.",
            missing.Message);
    }

    [Fact]
    public void LW4_BaseErrorsHaveNoPrefix()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            JsonRateCardCalculator.LoadWithOverlay("{", null));

        Assert.Equal("line 1, column 2: invalid JSON", exception.Message);
    }
}
```

## Final step: architect audit (not a builder task)

After Task 5 merges, `ledger-architect` audits the whole repository at the merge commit, as the user's plan offers ("optional final PR/diff audit across the full repository"). It changes no code and opens no pull request. It sends the orchestrator one findings report; the orchestrator decides what to fix and how.

**Scope:** every file in `src/`, `tests/`, `action/`, `.github/workflows/`, `docs/`, and `README.md`, and the four sprint plans with their amendments.

**Checks:**

1. Build and test from a clean clone: `dotnet build CopilotLedger.slnx`, `dotnet test CopilotLedger.slnx`, and `pwsh -NoProfile -File action/tests/receipt.Tests.ps1`. Record the counts and the latest CI run on `main`.
2. **Money:** no `double`, `float`, `GetDouble`, `TryGetDouble`, or `Math` double overloads in `src/`; no SQLite `REAL` columns or `SUM` over `TEXT`; rounding only for display, with an explicit `MidpointRounding`; costs follow `tokens × rate / 1,000,000`.
3. **Domain invariants:** only inference spans are priced; `invoke_agent` totals are never priced or added; cache-read and cache-write tokens are subtracted from input; both cache-write attribute names are accepted.
4. **Privacy:** no prompt or response content, working directory, or credential is stored, logged, written to a receipt, or posted; remote URLs are stripped; receipts and comments contain only slugs, branch and model names, counts, and amounts.
5. **No `git` and no Docker:** no product code or test starts `git`; no container files or services.
6. **Hermetic tests:** no network, real home directory, registry, user environment, or sockets; every database under a `TempDirectory`; reads of just-written files go through `FileRetry`.
7. **Contracts:** each sprint's public contracts and copy-exactly code match the plans and their recorded amendments.
8. **Docs:** `README.md`, `docs/guide.md`, `docs/cli.md`, `docs/configuration.md`, `docs/action.md`, and `docs/releasing.md` match `--help`, the messages in code, and the file layout; every command they show exists with those options.
9. **Robustness:** exception filters name their types; a default `switch` arm can't be swallowed by a broad `catch`; files are written atomically where a reader may race; cancellation reaches long operations; Windows and Unix paths both work.
10. **Security:** workflow permissions are the least needed; no `${{ }}` expression is interpolated into a `run` script; the action never edits others' comments; release assets are checksummed.
11. **Deferred items:** each still has a rationale, and none is half-built.

**Report** (sent to the orchestrator, not committed): a table of findings, each with an ID (`A1`, `A2`, …), severity (blocker, major, minor, or note), the file and line, what's wrong with evidence (a command and its output, or a quote), and the suggested fix. It ends with the counts from check 1 and the list of checks that found nothing. Findings the orchestrator accepts become tasks in the next sprint's plan.

## Deferred to later sprints

- **Reconciliation with `nano_aiu`** (carried). Store each `chat` span's `github.copilot.nano_aiu` and report drift per model, never the root `invoke_agent` total. Protobuf carries it as an IEEE `double`, so it needs a decimal-only design.
- **Breaker state in `status`** (carried). `status` still doesn't show open trips, so its tested output changes only where this sprint says.
- **MCP registration in `install`** (carried). `install` could run `copilot mcp add` or edit `~/.copilot/mcp-config.json`, with `uninstall` undoing it.
- **`COPILOT_LEDGER_HOME` in sign-in items** (carried). Pass the data directory to the sign-in item instead of warning.
- **Headless Linux** (carried). A `systemd --user` unit for machines without a desktop session.
- **A tool gate** (carried). A user-level `preToolUse` hook that denies tools while the breaker is open; it needs the hook contract verified live.
- **Webhooks** (carried) as another alert channel.
- **Smarter retry churn** (carried). Telling test and build commands apart needs the command text, which Copilot exports only with content capture.
- **A Marketplace action** (carried). Marketplace needs a root `action.yml`, so it needs a separate `swigerb/copilot-ledger-action` repository: the user's decision.
- **Signed receipts and a pre-push hook.** Deferred with the rationale in "Design decisions": a signature can't vouch for a local ledger, and a pre-push hook can't add to the push it runs in.
- **`export-pr --since`**, for branch names that are reused.
- **`gh ledger` in every message.** Breaker, MCP, alert, and sign-in messages still say `gh-ledger <command>`; only `status` and `rates update` name the command the way it was run. Changing the rest touches many Sprint 3 tests.
- **Restarting the daemon around `gh extension upgrade`**, for example a `gh ledger upgrade` command, instead of documenting `daemon stop` first.
- **Build provenance.** `actions/attest-build-provenance` (v4) for the release assets, verifiable with `gh attestation verify`.
- **Arm64 smoke tests.** Run the `linux-arm64` and `win-arm64` binaries on Arm64 runners, and `osx-x64` under Rosetta.
- **Checking for newer rates automatically**, for example a `status` hint when the latest release's rates are newer.
- **Receipts for fork pull requests.** They'd need `pull_request_target` or a second workflow triggered by `workflow_run`, with care for untrusted input.
- **Carried from Sprint 2:** a custom port in `install`, rates follow-ups, BYOK, local, and VS Code Local captures, file-exporter ingestion, Native AOT, and overflow-safe rollups, as described in `.github/plans/archive/sprint-2.md`.

## Amendments during implementation

Recorded by `ledger-architect` on 2026-11-21, after all five tasks merged.

None. Tasks 1–5 merged in PRs #41, #42, #43, #45, and #46, each on the first pass. For each pull request, the orchestrator rebuilt every changed file from the previous `main` plus this plan's copy-exactly blocks and listed edits, and every file matched byte for byte.
