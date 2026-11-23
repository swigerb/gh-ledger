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
| `gh ledger status [--json]` | The daemon, ledger, rates, Copilot's export setting, warnings, and totals and cache hit ratios for today and the last 7 and 30 days |
| `gh ledger branch [--repo <key> \| --all-repos] [--since <period>] [--json]` | Spend by branch in the current repository, or in another or every repository; outside a Git repository, every repository |
| `gh ledger agents [--repo <key>] [--since <period>] [--json]` | Spend by agent in every repository, or in one with `--repo` |
| `gh ledger top-prompts [--repo <key>] [--since <period>] [--limit <n>] [--json]` | The most expensive prompt chains in every repository, or in one with `--repo`; `--limit` is 1 to 100 and defaults to 10 |
| `gh ledger status --cache ...` | Prompt-cache efficiency; see [Prompt-cache efficiency](#prompt-cache-efficiency) |

- A repository key is an `owner/repo` slug, a local repository root for a repository whose `origin` has no `owner/repo` path, or `(none)` for spend outside any repository.
- `--since` takes `today`, `all`, `1d` through `3650d`, or a `yyyy-MM-dd` date; the default is `30d`.
- Tables round money to cents; JSON keeps exact amounts.
- Spend is split into GitHub AI credits (`AI credits` and `GitHub USD`), Foundry BYOK (`BYOK USD`), and local models, whose `Local saved` is the cloud spend they avoided. `Unpriced` counts requests without a price; see [configuration.md](configuration.md).
- An agent shows as `(none)` when Copilot reports no agent name, as its default agent does. Built-in subagents such as `explore` report their own names, and a custom agent reports the `name` in its `.agent.md` file, or the file's name without `.agent.md`.
- `status --json` adds a `cache` object to each period, and each row of `branch --json` and `agents --json` adds `cacheReportedSpans`, `cacheUnreportedSpans`, and `cacheHitRatio`. See [Prompt-cache efficiency](#prompt-cache-efficiency).

## Prompt-cache efficiency

Copilot reports how many of each request's input tokens the model read from its prompt cache and how many it wrote to it. Cache reads cost much less than uncached input, so a falling hit ratio, or a context that grows on every request, raises spend without changing the result. For a walkthrough, see [Find context bloat](guide.md#find-context-bloat).

| Command | Reports |
|---|---|
| `gh ledger status --cache [--by model\|branch\|pr\|agent] [--repo <key>] [--branch <name>] [--pr <n>] [--since <period>] [--json]` | Cache efficiency by model (the default), branch, pull request, or agent, in every repository or in one with `--repo`; `--branch` and `--pr` keep one branch or pull request |
| `gh ledger status --cache --turns (--trace <traceId> \| --conversation <id>) [--json]` | Each model request of one prompt chain, or of one Copilot session, in start order |
| `gh ledger status --cache --bloat [--repo <key>] [--since <period>] [--limit <n>] [--json]` | The prompt chains with the most uncached input and the most input growth; `--limit` is 1 to 100 and defaults to 10 |

- `Input` counts all input tokens, including those read from and written to the cache. `Uncached` is the rest: input minus cache reads and cache writes.
- Copilot sends a cache-read or cache-write count only when it isn't zero, so when a request sends one, the other is 0. A request that sends neither, as many Foundry BYOK and local endpoints do, has unknown cache use. Requests recorded by earlier versions of gh-ledger count as unknown, unless they recorded a cache read or write.
- `Cache reported` counts the requests with known cache use. `Hit %` is cache reads divided by input over those requests, rounded to one decimal place. It shows `n/a` when no request in the row reported cache counters; a real zero shows as `0.0%`. The cache columns are totals over the same requests, so they show `n/a` too, and `*` marks a total that leaves some of the row's requests out. A footnote counts the requests left out.
- A request that missed the cache and wrote nothing to it also sends neither count, so it's left out as unknown, not counted as a miss. For a model whose provider reports cache reads but never cache writes, as some providers with automatic prompt caching do, every miss is like that: its `Hit %` covers only the requests that read from the cache, so it's higher than the model's real hit ratio. When a model's `Cache reported` is less than its `Spans`, its `Hit %` is probably too high.
- `--by pr` groups requests by pull request when the branch's Git config tracks `refs/pull/<n>/head`, as `gh pr checkout` sets up for some pull requests from forks. Other requests show as `(no PR) <branch>`. gh-ledger reads only the local Git config and never asks GitHub.
- A request, or turn, is one model call. `--turns` lists each request with its cache use and `Growth`, the change in input from the previous request of the same agent invocation and model, so a subagent's requests are compared only with each other. `-` marks the first request of an invocation, and `n/a` a request whose agent invocation wasn't recorded.
- `--bloat` ranks prompt chains whose requests all reported cache counters by uncached input, and prompt chains by their largest input growth within one agent invocation and model. Chains with requests that reported no cache counters are ranked separately by estimated uncached input, which counts all input of those requests as uncached, as pricing does. Ties go to the lower trace ID.
- Without `--cache`, `status` takes only `--json`. With `--turns`, give `--trace` or `--conversation`, and optionally `--json`. `--by`, `--branch`, and `--pr` can't be used with `--bloat`, and `--limit` needs `--bloat`.

In JSON, `cacheHitRatio` is the exact ratio or `null`, and token counts that are unknown are `null`:

- `status --json` periods: `cache` has `cacheReportedSpans`, `cacheUnreportedSpans`, `reportedInputTokens`, `reportedCacheReadInputTokens`, and `cacheHitRatio`.
- `status --cache --json` rows: the group (`model`; `agent`; `repository` and `branch`; or `repository`, `pullRequest`, and `branch`, where `pullRequest` is `null` for `(no PR)` rows), then `spans`, `cacheReportedSpans`, `cacheUnreportedSpans`, `inputTokens`, `outputTokens`, `maxInputTokens`, `averageInputTokensPerTurn`, `reportedInputTokens`, `reportedCacheReadInputTokens`, `reportedCacheWriteInputTokens`, `uncachedInputTokens` (observed, over the requests that reported cache counters), `estimatedUncachedInputTokens` (over all requests, counting unknown cache use as uncached), and `cacheHitRatio`.
- `status --cache --turns --json` turns: IDs, time, Git context, agent, model, tokens, `cacheReadState` and `cacheWriteState` (`reported`, `absent`, or `legacy_unknown`), the observed cache and uncached tokens, `cacheHitRatio`, and `inputDeltaFromPreviousTurn`.
- `status --cache --bloat --json`: `byUncachedInput`, `byInputGrowth`, and `byEstimatedUncachedInput`, and `spansWithoutAgent`, the requests left out of input growth.

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

The server has five tools. All but `get_cache_efficiency` use the current repository and branch unless you pass `repository` and `branch`:

- `get_branch_ledger`: the branch's spend, top model, budget, circuit-breaker state, and prompt-cache hit ratio.
- `get_top_expensive_prompts`: the branch's most expensive prompt chains, with output tokens and failed tool calls; `limit` (1 to 20, default 5) and `orderBy` (`cost`, `outputTokens`, or `failedToolCalls`).
- `get_agent_breakdown`: the branch's spend and prompt-cache hit ratio by agent.
- `get_cache_efficiency`: prompt-cache efficiency in the current repository, or in `repository`, like `status --cache`. `by` groups rows by `model` (the default), `branch`, `pr`, or `agent`; `branch` and `pullRequest` keep one branch or pull request; `since` takes the same periods as `--since`; and `limit` (1 to 50, default 10) keeps the rows with the most input.
- `set_branch_budget`: sets the branch's budget in AI credits (`aiCredits`).

The first four are read-only. `set_branch_budget` changes only the budget, so Copilot asks before running it; in prompt mode, allow it with `--allow-tool 'ledger(set_branch_budget)'`. While the breaker is open for the branch or the session, or the branch is over budget, every result starts with a `WARNING:` line; for `get_cache_efficiency`, the branch is the current one. No tool can reset the breaker.

## Rates

- `gh ledger rates init [--force]` creates a starter rates overlay, `rates.json` in the data directory.
- `gh ledger rates validate [--file <path>]` checks the overlay, or another file, with the same rules the daemon uses.
- `gh ledger rates update` downloads `rates.v2.json` from the latest gh-ledger release, or its `rates.json` if it has none, and keeps it when it's newer than the rates in use, has API list prices (`schemaVersion` 2), and your overlay still applies. It uses your system proxy. Restart the daemon afterward; it reads rates when it starts.

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
