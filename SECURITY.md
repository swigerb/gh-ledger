# Security policy

## Supported versions

Only the latest release is supported. To upgrade, follow [the upgrade steps](docs/guide.md#upgrade-or-uninstall).

## Report a vulnerability

Report vulnerabilities privately through [GitHub's private vulnerability reporting](https://github.com/swigerb/gh-ledger/security/advisories/new), not in a public issue. Include your gh-ledger version (`gh ledger --version`), your operating system, and how to reproduce the problem. Don't include prompts, responses, or your ledger.

## What's in scope

gh-ledger runs on your machine, so these matter most:

- the daemon's OpenTelemetry receiver, which listens only on localhost;
- `gh ledger import`, which reads Copilot's and Codex's session logs;
- everything that writes or sends data: reports, exports, receipts, `audit --local-llm`, and the MCP server, none of which may include prompt or response content;
- the release binaries and their build provenance attestations.
