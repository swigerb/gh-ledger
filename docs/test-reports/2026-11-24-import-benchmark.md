# Import speed benchmark

## What was measured

The benchmark generated one Copilot 1.0.114-shaped session containing 20,000 `session.usage_record` events. Immediately after record 10,000, it generated two 9 MiB lines, of types `model.messages_snapshot` and `model.model_call_success`, which import never reads.

Only generated data was read. The benchmark used temporary `COPILOT_HOME` and `COPILOT_LEDGER_HOME` directories.

## Environment

- OS: Microsoft Windows 10.0.26300
- Processors: 16
- .NET SDK: 10.0.115
- Before commit: `0f96a83` with the Task 1 benchmark script (`30dd55e`)
- After commit parent: `1a765adf730e43e55a87891eb235cba7fa1a6838`
- Same machine as Task 1: yes

## Commands

```powershell
dotnet publish src/CopilotLedger.Cli/CopilotLedger.Cli.csproj -c Release -r win-x64 -o "$env:TEMP\gh-ledger-bench-after"
pwsh -NoProfile -File tests/benchmarks/Measure-ImportSpeed.ps1 -Binary "$env:TEMP\gh-ledger-bench-after\gh-ledger.exe"
```

## Before

| Run | Seconds | Exit | Scanned | Skipped unchanged | Recorded | Duplicate | Malformed lines | Skipped events |
|---|---|---|---|---|---|---|---|---|
| 1 first | 141.76 | 2 | 1 | 0 | 20000 | 0 | 2 | 0 |
| 2 unchanged | 288.57 | 2 | 1 | 0 | 0 | 20000 | 2 | 0 |
| 3 changed bytes | 287.98 | 2 | 1 | 0 | 0 | 20000 | 2 | 0 |

## After

| Run | Seconds | Exit | Scanned | Skipped unchanged | Recorded | Duplicate | Malformed lines | Skipped events |
|---|---|---|---|---|---|---|---|---|
| 1 first | 6.36 | 0 | 1 | 0 | 20000 | 0 | 0 | 2 |
| 2 unchanged | 0.23 | 0 | 1 | 1 | 0 | 0 | 0 | 0 |
| 3 changed bytes | 1.92 | 0 | 1 | 0 | 0 | 20000 | 0 | 2 |

## Comparison

| Run | Before (s) | After (s) | Change |
|---|---:|---:|---:|
| 1 first | 141.76 | 6.36 | -95.51% |
| 2 unchanged | 288.57 | 0.23 | -99.92% |
| 3 changed bytes | 287.98 | 1.92 | -99.33% |

## Target results

- **Met:** after run 1 exited 0, recorded 20,000, had 0 malformed lines and 2 skipped events, and took 6.36 seconds, at most 20% of the 141.76-second before timing.
- **Met:** after run 2 exited 0, skipped 1 unchanged session, and recorded and duplicated 0 records.
- **Met:** after run 3 exited 0, duplicated 20,000, recorded 0, had 0 malformed lines, and took 1.92 seconds, at most 10 seconds.
