# Releasing gh-ledger

Releases are GitHub Releases of this repository: `gh extension install swigerb/gh-ledger` installs their binaries, and `gh ledger rates update` downloads their `rates.v2.json`. The PR receipt action is released from [its own repository](https://github.com/swigerb/copilot-ledger-action).

## Cut a release

1. Change the version everywhere it's written, and merge that change:
   - `<Version>` in `src/CopilotLedger.Cli/CopilotLedger.Cli.csproj`;
   - in `src/CopilotLedger.Cli/GhLedgerCli.cs`, the `AssemblyInformationalVersionAttribute` comment and the fallback after `??`;
   - the version that the smoke test in `.github/workflows/ci.yml` compares with;
   - the tests that expect it: `DaemonCommandTests.DM1`, `ReportCommandTests.StatusText`, `ExportCommandTests.EC5`, and `ExportBundleTests.EB2`;
   - the samples in `docs/guide.md` that show it: `status-first`, `status`, and the receipt, which shows it twice. `GuideSamplesTests` fails until they match;
   - the manifest sample under "The bundle" in `docs/cli.md`, as `"toolVersion"`;
   - the tag in step 4.

   `VersionPlacesTests` fails until the comment, the fallback, the smoke test, the manifest sample, and the tag show the new `<Version>`, and while any other line names it. Everything else reads the version as it runs. Leave the versions that the docs and tests name on purpose: "gh-ledger 0.4.1 and earlier" in the table of assets below; the prerelease tag in the example after that table; the comment on `GuideSamplesTests.HaikuOverlay`; the comments in `ModelResolutionTests` and `RatesUpdateTests`; the earlier daemon's `daemon.json` in `ImportCommandTests`; and the mentions to leave that `VersionPlacesTests` lists.
2. If `src/CopilotLedger.Core/Pricing/rates.json` changed since the latest release, set its `ratesVersion` to the date of the change (`yyyy-MM-dd`), later than the latest release's. `gh ledger rates update` only takes rates with a later version, and the Release workflow fails when `rates.json` changed but its `ratesVersion` didn't move forward. In the same change, replace the old date wherever it's shown; the tests fail until every place matches:
   - the samples in `docs/guide.md`: `status-first`, `status`, `rates-validate`, `rates-update`, and `audit`. The `rates-update` sample and `GuideSamplesTests.GS4` download version `2026-12-15`; if that isn't later than the new date, change it in both;
   - the tests that expect it, such as `ReportCommandTests` (including `StatusText`), `RatesCommandTests`, `RatesUpdateTests`, `RateOverlayTests`, `JsonRateCardCalculatorTests`, the audit and export tests, and `TestSupport/PublishedRates.cs` and `TestSupport/AuditOutputs.cs`. `RatesUpdateTests` downloads versions before, equal to, and after the old date; keep each in the same place relative to the new one.
3. Optional: run the Release workflow on `main` from the Actions tab. A manual run builds all six binaries, smoke-tests the three that GitHub's runners can run (`win-x64`, `linux-x64`, and `osx-arm64`), checks `ratesVersion`, and uploads the binaries as workflow artifacts. It never creates a release or an attestation: only the release job, which runs for a pushed tag, has the `id-token: write` and `attestations: write` permissions.
4. Tag the merged commit and push the tag:

   ```shell
   git tag v0.7.0
   git push origin v0.7.0
   ```

For a pushed `v*` tag, the Release workflow checks that the tag is `v` plus the project version, builds and smoke-tests the binaries, checks `ratesVersion` against the latest release's rates, attests the six binaries, `rates.json`, and `rates.v2.json` with `actions/attest-build-provenance`, and creates the release with these assets:

| Asset | Platform |
|---|---|
| `gh-ledger-windows-amd64.exe` | Windows x64 |
| `gh-ledger-windows-arm64.exe` | Windows Arm64 |
| `gh-ledger-linux-amd64` | Linux x64 |
| `gh-ledger-linux-arm64` | Linux Arm64 |
| `gh-ledger-darwin-amd64` | macOS x64 |
| `gh-ledger-darwin-arm64` | macOS Apple silicon |
| `rates.v2.json` | The built-in rates, for `gh ledger rates update` |
| `rates.json` | The built-in rates without `apiEquivalent`, as `schemaVersion` 1, for `gh ledger rates update` in gh-ledger 0.4.1 and earlier, which refuse `schemaVersion` 2 |
| `gh-ledger-checksums.txt` | SHA-256 checksums of the files above |

GitHub CLI picks a release asset by the `<os>-<arch>` suffix of its name, so keep these names. A tag with a hyphen, such as `v0.5.0-rc.1`, makes a prerelease, which `gh extension install` and `rates update` ignore because they use the latest release.

## Verify a release

Each release after v0.5.0 has a build provenance attestation for its binaries, `rates.json`, and `rates.v2.json`: a signed statement, stored by GitHub and recorded in Sigstore's public log, that this repository's Release workflow built the file with that SHA-256 digest from the tagged commit. To check a release's assets:

```shell
gh release download <tag> --repo swigerb/gh-ledger --dir gh-ledger-<tag>
gh attestation verify gh-ledger-<tag>/gh-ledger-linux-amd64 --repo swigerb/gh-ledger --signer-workflow swigerb/gh-ledger/.github/workflows/release.yml --source-ref refs/tags/<tag>
```

Repeat the second command for each asset. `gh-ledger-checksums.txt` isn't attested: it's computed from the attested files. The Windows binaries aren't signed with Authenticode.

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

`receipt.yml` pins swigerb/copilot-ledger-action to the commit of one of its releases in the same way. After each release of the action, look up the commit its tag points to, as above, and update the pin.
