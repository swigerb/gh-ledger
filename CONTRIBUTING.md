# Contributing to gh-ledger

Thanks for helping. gh-ledger is a small, local-first GitHub CLI extension, and a few rules keep it that way.

## Before you start

- To report a bug or suggest a feature, [open an issue](https://github.com/swigerb/gh-ledger/issues/new/choose). For a larger change, open an issue first, so we can agree on the approach before you write code.
- Report security problems privately, as [SECURITY.md](SECURITY.md) describes.

## Branches

- `main` is the default branch, and releases are tagged on it. It changes only through pull requests that pass CI on Ubuntu and Windows.
- `dev` is where work lands. Open pull requests against `dev`, not the default branch, `main`.
- A release merges `dev` into `main` through a pull request, then tags `main`; see [Releasing](docs/releasing.md).
- Branches in this repository other than `main` and `dev` are deleted when their pull request merges.

## Build and test

Install the .NET 10 SDK, then run from the repository root:

```shell
dotnet build CopilotLedger.slnx
dotnet test CopilotLedger.slnx
```

Both must pass, and the build treats warnings as errors. The tests use only temporary folders and local SQLite files: they need no network, Docker, or `git`, and never touch your own `~/.copilot-ledger`.

To try a change by hand without touching your own ledger, [publish a binary](docs/cli.md#get-gh-ledger), set `COPILOT_LEDGER_HOME` to a temporary folder, and run the daemon on another port with `daemon run --port <port>`.

## Ground rules

[`.github/copilot-instructions.md`](.github/copilot-instructions.md) has the full rules. These matter most:

- **Local only.** gh-ledger is a single .NET 10 binary that needs no Docker, cloud service, or other runtime, such as Python or Node.
- **No content.** gh-ledger never stores, logs, or exports prompts, responses, tool arguments or results, or file contents.
- **Exact money.** Money is `decimal`, never `float` or `double`, and the ledger stores it as integer pico-USD.
- **Small changes with tests.** Change only what your pull request needs, and add tests for the behavior you add or fix.
- **Tested docs.** Tests check the docs against the code: `GuideSamplesTests` compares every sample in `docs/guide.md` with real output, and other tests check the tables in `docs/cli.md` and the error examples in `docs/configuration.md`. Update the docs in the same pull request.
- **Fictional examples.** Screenshots, samples, and test data use made-up repositories, such as `contoso/webapp`, never real spend.

Maintainers plan larger changes in `.github/plans/`, with the agents in `.github/agents/`.
