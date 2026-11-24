#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Binary,
    [ValidateRange(2, 1000000)] [int] $Records = 20000,
    [string] $WorkRoot,
    [switch] $KeepFiles
)

$ErrorActionPreference = 'Stop'

$binaryPath = [IO.Path]::GetFullPath($Binary)
if (-not [IO.File]::Exists($binaryPath)) {
    throw "Binary $binaryPath doesn't exist."
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([IO.Path]::GetTempPath()) ("gh-ledger-import-benchmark-" + (Get-Date -Format yyyyMMdd-HHmmss))
}
$WorkRoot = [IO.Path]::GetFullPath($WorkRoot)

if ([IO.Directory]::Exists($WorkRoot) -and
    [IO.Directory]::GetFileSystemEntries($WorkRoot).Length -ne 0) {
    throw "WorkRoot $WorkRoot isn't empty."
}

$copilotHome = Join-Path $WorkRoot 'copilot-home'
$ledgerHome = Join-Path $WorkRoot 'ledger-home'
$sessionDirectory = Join-Path $copilotHome 'session-state/6f1c2d3e-4b5a-4c7d-8e9f-0a1b2c3d4e5f'
$eventsPath = Join-Path $sessionDirectory 'events.jsonl'
[IO.Directory]::CreateDirectory($sessionDirectory) | Out-Null
[IO.Directory]::CreateDirectory($ledgerHome) | Out-Null

$invariant = [Globalization.CultureInfo]::InvariantCulture
$baseTime = [DateTime]::Parse('2026-11-20T12:00:00Z', $invariant, [Globalization.DateTimeStyles]::AdjustToUniversal)
$eventNumber = 0
$previousId = $null
$utf8NoBom = [Text.UTF8Encoding]::new($false)
$writer = [IO.StreamWriter]::new($eventsPath, $false, $utf8NoBom)
$writer.NewLine = "`n"
try {
    function Write-Event {
        param(
            [IO.StreamWriter] $EventWriter,
            [string] $Type,
            [string] $Data
        )

        $script:eventNumber++
        $id = '00000000-0000-4000-8000-{0:x12}' -f $script:eventNumber
        $timestamp = $script:baseTime.AddSeconds($script:eventNumber).ToString('yyyy-MM-ddTHH:mm:ss.fffZ', $script:invariant)
        $parent = if ($null -eq $script:previousId) { 'null' } else { "`"$script:previousId`"" }
        $EventWriter.WriteLine("{`"type`":`"$Type`",`"data`":$Data,`"id`":`"$id`",`"timestamp`":`"$timestamp`",`"parentId`":$parent}")
        $script:previousId = $id
    }

    $rootForJson = $WorkRoot.Replace('\', '/')
    $escapedRoot = ConvertTo-Json -InputObject $rootForJson -Compress
    $startData = "{" +
        '"sessionId":"6f1c2d3e-4b5a-4c7d-8e9f-0a1b2c3d4e5f","version":1,"producer":"copilot-agent",' +
        '"copilotVersion":"1.0.114","startTime":"2026-11-20T12:00:00.000Z","context":{"cwd":' + $escapedRoot + '},"alreadyInUse":false}'
    Write-Event -EventWriter $writer -Type 'session.start' -Data $startData

    for ($i = 1; $i -le $Records; $i++) {
        $usageId = 'u-{0:d8}' -f $i
        $requestId = '10000000-0000-4000-8000-{0:x12}' -f $i
        $usageData = "{" +
            '"usage":{"model":"claude-haiku-4.5","provider":{"id":"copilot","kind":"copilot","label":"GitHub Copilot"},' +
            '"modelDisplayName":"claude-haiku-4.5","inputTokens":1000,"outputTokens":100,"cacheReadTokens":0,"cacheWriteTokens":0,' +
            '"reasoningTokens":0,"duration":1000,"initiator":"user","isByok":false,"apiCallId":"' + $usageId +
            '","serviceRequestId":"' + $requestId +
            '","accounting":{"sourceSessionId":"6f1c2d3e-4b5a-4c7d-8e9f-0a1b2c3d4e5f","sequence":' + $i +
            ',"usageId":"' + $usageId + '"},"cacheDetailsReported":true,"numToolCalls":0}}'
        Write-Event -EventWriter $writer -Type 'session.usage_record' -Data $usageData

        if ($i -eq [int]($Records / 2)) {
            $filler = 'x' * 9437184
            Write-Event -EventWriter $writer -Type 'model.messages_snapshot' -Data ('{"messages":"' + $filler + '"}')
            Write-Event -EventWriter $writer -Type 'model.model_call_success' -Data ('{"response":"' + $filler + '"}')
            $filler = $null
        }
    }
} finally {
    $writer.Dispose()
}

$logBytes = (Get-Item -LiteralPath $eventsPath).Length
$originalCopilotHome = [Environment]::GetEnvironmentVariable('COPILOT_HOME', 'Process')
$originalLedgerHome = [Environment]::GetEnvironmentVariable('COPILOT_LEDGER_HOME', 'Process')
$runs = @()

try {
    [Environment]::SetEnvironmentVariable('COPILOT_HOME', $copilotHome, 'Process')
    [Environment]::SetEnvironmentVariable('COPILOT_LEDGER_HOME', $ledgerHome, 'Process')

    for ($run = 1; $run -le 3; $run++) {
        if ($run -eq 3) {
            [IO.File]::AppendAllText($eventsPath, "`n")
        }

        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        $stdout = & $binaryPath import --all --json
        $stopwatch.Stop()
        $exitCode = $LASTEXITCODE
        $json = ($stdout | Out-String) | ConvertFrom-Json
        $source = @($json.sources | Where-Object source -eq 'copilot')
        if ($source.Count -ne 1) {
            throw "Run $run found $($source.Count) copilot sources, not 1."
        }
        $scanned = [int]$source[0].sessionsScanned
        if ($scanned -ne 1) {
            throw "Run $run scanned $scanned sessions, not 1."
        }

        $runs += [pscustomobject]@{
            Name              = @('1 first', '2 unchanged', '3 changed bytes')[$run - 1]
            Seconds           = $stopwatch.Elapsed.TotalSeconds
            Exit              = $exitCode
            Scanned           = $scanned
            SkippedUnchanged  = [int]$source[0].sessionsSkippedUnchanged
            Recorded          = [int]$source[0].recorded
            Duplicate         = [int]$source[0].duplicate
            MalformedLines    = [int]$source[0].malformedLines
            SkippedEvents     = [int]$source[0].skippedEvents
        }
    }

    Write-Output "Binary: $binaryPath"
    Write-Output "Version: $(& $binaryPath --version)"
    Write-Output "OS: $([Runtime.InteropServices.RuntimeInformation]::OSDescription)"
    Write-Output "Processors: $([Environment]::ProcessorCount)"
    Write-Output "Records: $Records"
    Write-Output "Log bytes: $logBytes"
    Write-Output ''
    Write-Output '| Run | Seconds | Exit | Scanned | Skipped unchanged | Recorded | Duplicate | Malformed lines | Skipped events |'
    Write-Output '|---|---|---|---|---|---|---|---|---|'
    foreach ($result in $runs) {
        $seconds = $result.Seconds.ToString('F2', $invariant)
        Write-Output "| $($result.Name) | $seconds | $($result.Exit) | $($result.Scanned) | $($result.SkippedUnchanged) | $($result.Recorded) | $($result.Duplicate) | $($result.MalformedLines) | $($result.SkippedEvents) |"
    }
} finally {
    [Environment]::SetEnvironmentVariable('COPILOT_HOME', $originalCopilotHome, 'Process')
    [Environment]::SetEnvironmentVariable('COPILOT_LEDGER_HOME', $originalLedgerHome, 'Process')
    if (-not $KeepFiles) {
        [IO.Directory]::Delete($WorkRoot, $true)
    }
}
