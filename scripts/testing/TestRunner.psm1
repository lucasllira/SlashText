# Process boundary is intentional: legacy scripts may call exit or change location.
Set-StrictMode -Version Latest

function Invoke-TestSteps {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]]$Steps,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string]$OutputDirectory,
        [string]$CancelFile,
        [hashtable]$Metadata = @{}
    )
    $ErrorActionPreference = 'Stop'
    $root = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $root) { throw 'Use a new evidence directory; previous results are never overwritten.' }
    $ids = @($Steps | ForEach-Object { $_.id })
    if (@($ids | Sort-Object -Unique).Count -ne $ids.Count -or @($ids | Where-Object { $_ -notmatch '^[a-z0-9-]+$' }).Count) {
        throw 'Test identifiers must be unique, simple directory names.'
    }
    [void][IO.Directory]::CreateDirectory($root)
    $results = [Collections.Generic.List[object]]::new()
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $halt = $false
    $active = $null
    try {
        foreach ($step in $Steps) {
            $entry = [ordered]@{ id = $step.id; status = 'NOT_RUN'; durationSeconds = 0; exitCode = $null; reason = $null }
            $results.Add($entry)
            if ($halt) { continue }
            if ($CancelFile -and (Test-Path -LiteralPath $CancelFile)) {
                $entry.status = 'CANCELLED'; $entry.reason = 'Cancellation marker present.'; $halt = $true; continue
            }
            if ($step.Contains('unavailableReason')) {
                $entry.status = 'UNAVAILABLE'; $entry.reason = $step.unavailableReason; continue
            }
            # Check at execution time: a preceding build may create this EXE.
            if ($step.Contains('requiredFileVersion')) {
                $candidate = Get-Item -LiteralPath $step.versionedFile -ErrorAction SilentlyContinue
                if (-not $candidate -or $candidate.VersionInfo.FileVersion -ne $step.requiredFileVersion) {
                    $entry.status = 'UNAVAILABLE'
                    $entry.reason = 'Fixture EXE FileVersion differs from this checkout. Older/unknown versions are not launched with helper switches.'
                    continue
                }
            }
            $directory = Join-Path $root $step.id
            [void][IO.Directory]::CreateDirectory($directory)
            $timer = [Diagnostics.Stopwatch]::StartNew()
            $stdout = $null; $stderr = $null; $stdoutCopy = $null; $stderrCopy = $null; $started = $false
            try {
                $command = Get-Command $step.tool -CommandType Application -ErrorAction SilentlyContinue
                if (-not $command) {
                    $entry.status = 'UNAVAILABLE'; $entry.reason = 'Required executable not found.'; continue
                }
                $entry.tool = [IO.Path]::GetFileName($command.Source)
                $entry.toolSha256 = (Get-FileHash -LiteralPath $command.Source -Algorithm SHA256).Hash.ToLowerInvariant()
                $companion = [IO.Path]::ChangeExtension($command.Source, '.dll')
                if (Test-Path -LiteralPath $companion -PathType Leaf) {
                    $entry.managedAssemblySha256 = (Get-FileHash -LiteralPath $companion -Algorithm SHA256).Hash.ToLowerInvariant()
                }
                $info = [Diagnostics.ProcessStartInfo]::new()
                $info.FileName = $command.Source
                $info.WorkingDirectory = $WorkingDirectory
                $info.UseShellExecute = $false; $info.CreateNoWindow = $true
                $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
                foreach ($argument in $step.arguments) {
                    [void]$info.ArgumentList.Add(([string]$argument).Replace('{output}', $directory))
                }
                $stdout = [IO.File]::Create((Join-Path $directory 'stdout.log'))
                $stderr = [IO.File]::Create((Join-Path $directory 'stderr.log'))
                $active = [Diagnostics.Process]::new(); $active.StartInfo = $info
                if (-not $active.Start()) { throw 'Could not start owned test process.' }
                $started = $true
                $stdoutCopy = $active.StandardOutput.BaseStream.CopyToAsync($stdout)
                $stderrCopy = $active.StandardError.BaseStream.CopyToAsync($stderr)
                while (-not $active.WaitForExit(100)) {
                    if ($CancelFile -and (Test-Path -LiteralPath $CancelFile)) {
                        $entry.status = 'CANCELLED'; $entry.reason = 'Cancellation requested.'; break
                    }
                    if ($timer.Elapsed.TotalSeconds -ge $step.timeoutSeconds) {
                        $entry.status = 'FAIL'; $entry.reason = 'Test timeout.'; break
                    }
                }
                if ($entry.status -in @('CANCELLED','FAIL')) {
                    if (-not $active.HasExited) { $active.Kill($true) }
                    [void]$active.WaitForExit(10000)
                    $halt = $true
                } else {
                    $entry.exitCode = $active.ExitCode
                    $entry.status = switch ($active.ExitCode) { 0 { 'PASS' } 130 { 'CANCELLED' } default { 'FAIL' } }
                    if ($step.Contains('unavailableExitCodes') -and $active.ExitCode -in $step.unavailableExitCodes) { $entry.status = 'UNAVAILABLE' }
                    if ($entry.status -ne 'PASS') { $entry.reason = 'Child exit code; see step logs.' }
                }
                if (-not [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($stdoutCopy,$stderrCopy)).Wait(5000)) {
                    throw 'Test output streams did not close after process exit.'
                }
                $stdout.Dispose(); $stdout = $null; $stderr.Dispose(); $stderr = $null
                if ($entry.status -eq 'PASS') {
                    foreach ($expected in $step.expectedFiles) {
                        $path = [IO.Path]::GetFullPath((Join-Path $directory $expected))
                        if (-not $path.StartsWith($directory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
                            -not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
                            $entry.status = 'FAIL'; $entry.reason = 'Required evidence missing or empty.'; break
                        }
                    }
                    if ($entry.status -eq 'PASS' -and $step.Contains('unavailablePattern') -and
                        (Select-String -LiteralPath (Join-Path $directory 'stdout.log') -Pattern $step.unavailablePattern -Quiet)) {
                        $entry.status = 'UNAVAILABLE'; $entry.reason = 'A required subtest reported unavailable.'
                    }
                    if ($entry.status -eq 'PASS' -and $step.Contains('unavailableJsonProperty')) {
                        $items = @(Get-Content -LiteralPath (Join-Path $directory $step.expectedFiles[0]) -Raw | ConvertFrom-Json)
                        if (@($items | Where-Object { $_.PSObject.Properties[$step.unavailableJsonProperty] }).Count) {
                            $entry.status = 'UNAVAILABLE'; $entry.reason = 'Native fixture reported an unavailable source.'
                        }
                    }
                    if ($entry.status -eq 'PASS' -and $step.Contains('manualReview') -and $step.manualReview) {
                        $entry.status = 'REVIEW_REQUIRED'; $entry.reason = 'Automatic stage finished; physical review remains pending.'
                    }
                }
                if ($entry.status -in @('FAIL','CANCELLED')) { $halt = $true }
            } catch {
                $entry.status = 'FAIL'; $entry.reason = $_.Exception.Message; $halt = $true
            } finally {
                if ($active) {
                    if ($started -and -not $active.HasExited) { $active.Kill($true); [void]$active.WaitForExit(10000) }
                    if ($stdoutCopy) { try { [void]$stdoutCopy.Wait(1000) } catch {} }
                    if ($stderrCopy) { try { [void]$stderrCopy.Wait(1000) } catch {} }
                    $active.Dispose(); $active = $null
                }
                if ($stdout) { $stdout.Dispose() }; if ($stderr) { $stderr.Dispose() }
                $entry.durationSeconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3)
                Write-Host ("{0}: {1} ({2}s)" -f $entry.id, $entry.status, $entry.durationSeconds)
            }
        }
    } finally {
        if ($active -and -not $active.HasExited) { $active.Kill($true) }
        $statuses = @($results | ForEach-Object { $_.status })
        $status = if ('CANCELLED' -in $statuses) { 'CANCELLED' } elseif ('FAIL' -in $statuses) { 'FAIL' }
            elseif ($results.Count -ne $Steps.Count -or $Steps.Count -eq 0 -or @($statuses | Where-Object { $_ -ne 'PASS' }).Count) { 'INCOMPLETE' } else { 'PASS' }
        $code = switch ($status) { PASS { 0 } CANCELLED { 130 } FAIL { 1 } default { 2 } }
        $report = [ordered]@{ schemaVersion = 1; status = $status; exitCode = $code; startedUtc = [DateTime]::UtcNow.Subtract($clock.Elapsed).ToString('o')
            durationSeconds = [Math]::Round($clock.Elapsed.TotalSeconds,3); metadata = $Metadata; planned = $Steps.Count; results = @($results.ToArray()) }
        $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $root 'summary.json') -Encoding utf8
        @("# Test run: $status", '', "Duration: $($report.durationSeconds)s", '', '| Step | Status | Seconds |', '|---|---|---|') +
            @($results | ForEach-Object { "| $($_.id) | $($_.status) | $($_.durationSeconds) |" }) |
            Set-Content -LiteralPath (Join-Path $root 'summary.md') -Encoding utf8
    }
    return [pscustomobject]$report
}
Export-ModuleMember -Function Invoke-TestSteps
