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
- API-equivalent USD: what the same requests would cost at each model's API list prices, or at prices you supply, beside what Copilot billed
- `gh ledger import`: backfill the requests in your Copilot CLI and GitHub Copilot app session logs, and in Codex's, from before you installed CopilotLedger
- `gh ledger export`: a self-contained, offline HTML cost report with a timeline and charts, and an optional bundle of its data with SHA-256 hashes, to attach to a pull request or audit ticket

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

The [user guide](docs/guide.md) shows the everyday tasks with sample output: backfilling history, reports, budgets, the circuit breaker, pull request receipts and cost reports, Foundry BYOK and local models, MCP, updates, and uninstalling.

## Privacy

The ledger stays on your machine, and CopilotLedger never stores prompt or response content. `gh ledger import` reads Copilot's and Codex's session logs in place and keeps only what pricing, attribution, and later imports need: session IDs; models and providers; token counts and times; agent names; the repository root, branch, commit, and `owner/repo` slug that a session recorded; tool names, outcomes, exit codes, and counts; SHA-256 hashes of request and tool call IDs; and each log's size, last-write time, and SHA-256 hash. It never reads prompts, responses, tool arguments or results, or compaction summaries, and it doesn't read Claude Code's logs. See [What import reads and keeps](docs/cli.md#what-import-reads-and-keeps). `gh ledger export` writes only the fields in [What export includes](docs/cli.md#what-export-includes): never prompts, responses, tool arguments or results, or file contents, and local repository roots only with `--include-local-paths`.

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
