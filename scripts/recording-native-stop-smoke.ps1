param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [switch]$AudioBoth,
    [switch]$Unattended
)
$ErrorActionPreference = 'Stop'
if ($AudioBoth -and $Unattended) { throw 'Audio requires interactive consent.' }
$exe = (Resolve-Path -LiteralPath $Executable).Path
if (-not $Unattended) {
    if ($AudioBoth) {
        Write-Host 'Teste isolado #106: grava uma janela ficticia, audio do PC e microfone.'
        Write-Host 'Use somente sons e fala ficticios. Os MP4 ficam locais; nada e enviado.'
    } else { Write-Host 'Teste isolado #106: somente janela ficticia, sem audio.' }
    Write-Host 'O teste encerra somente os proprios processos auxiliares, nunca outra instancia do app.'
    [void](Read-Host 'Pressione Enter quando estiver pronto; feche esta janela para cancelar')
}
$root = [IO.Path]::GetFullPath((Join-Path $OutputDirectory ('native-stop-' + [Guid]::NewGuid().ToString('N'))))
[void](New-Item -ItemType Directory -Path $root)
$results = @()
$exitCode = 0
try {
    $sources = @('silent')
    if ($AudioBoth) { $sources += 'both' }
    foreach ($source in $sources) {
        foreach ($mode in @('late-stop', 'emergency', 'normal')) {
            $caseDirectory = Join-Path $root ($source + '-' + $mode)
            [void](New-Item -ItemType Directory -Path $caseDirectory)
            $arguments = '--recording-native-stop-smoke "' + $caseDirectory + '" ' + $mode
            if ($source -eq 'both') { $arguments += ' --audio-both' }
            Write-Host ('Executando: ' + $source + ' / ' + $mode)
            $child = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $caseDirectory 'stderr.txt')
            # Keep a process handle before exit; Windows PowerShell 5.1 can otherwise
            # lose ExitCode after Start-Process has detached its temporary handle.
            $null = $child.Handle
            $pidOwned = $child.Id
            $startedOwned = $child.StartTime.ToUniversalTime().Ticks
            try {
                if (-not $child.WaitForExit(70000)) { throw 'Tempo limite do processo de teste.' }
                $child.Refresh()
                $childExit = $child.ExitCode
                $report = Get-Content -LiteralPath (Join-Path $caseDirectory 'native-stop.json') -Raw | ConvertFrom-Json
                if ($report.status -eq 'INCOMPLETE') {
                    $results += [pscustomobject]@{ source = $source; mode = $mode; status = 'INCOMPLETE' }
                    $exitCode = 2
                    break
                }
                if ($mode -eq 'emergency') {
                    if ($childExit -eq 0 -or $report.status -ne 'READY_FOR_SELF_TERMINATION' -or
                        $report.pid -ne $pidOwned -or $report.startedTicks -ne $startedOwned -or
                        $report.uncertain -ne $true -or $report.nativeFileLocked -ne $true) {
                        throw 'Encerramento emergencial ou identidade do processo nao confirmado.'
                    }
                } elseif ($childExit -ne 0 -or $report.status -ne 'PASS' -or $report.nativeDisposed -ne $true) {
                    throw ('Cenario nao aprovado: ' + $source + ' / ' + $mode)
                }
                if ($report.mode -ne $mode -or $report.audio -ne ($source -eq 'both') -or
                    [string]::IsNullOrWhiteSpace($report.partial) -or
                    [IO.Path]::GetFileName($report.partial) -ne $report.partial -or $report.bytes -le 32) {
                    throw 'Relatorio nativo incompleto ou inconsistente.'
                }
                $filePath = Join-Path $caseDirectory $report.partial
                $file = [IO.File]::Open($filePath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                try {
                    $bytes = $file.Length
                    if ($bytes -lt $report.bytes) { throw 'Bytes do arquivo parcial nao preservados.' }
                } finally { $file.Dispose() }
                if ($mode -eq 'emergency') {
                    $markerPath = Join-Path $caseDirectory ('fixture-data/SlashDeskPilotData/RecordingRecovery-v1/' + ([Guid]$report.recordingId).ToString('N') + '.json')
                    $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
                    if ($marker.Schema -ne 1 -or $marker.Id -ne $report.recordingId -or
                        $marker.Pid -ne $pidOwned -or $marker.StartedUtcTicks -ne $startedOwned -or
                        $marker.StopUnconfirmed -ne $true -or $marker.PartialPath -ne $filePath) {
                        throw 'Metadados privados de recuperacao nao correspondem a sessao encerrada.'
                    }
                }
                $results += [pscustomobject]@{ source = $source; mode = $mode; status = 'PASS';
                    bytes = $bytes; processExited = $true; fileReleased = $true }
            } finally {
                # Only the Process object started above, with its original lifetime.
                if (-not $child.HasExited -and $child.Id -eq $pidOwned -and
                    $child.StartTime.ToUniversalTime().Ticks -eq $startedOwned) {
                    $child.Kill(); [void]$child.WaitForExit(10000)
                }
                $child.Dispose()
            }
        }
    }
    $expected = 3 * $sources.Count
    if ($exitCode -eq 0 -and ($results.Count -ne $expected -or @($results | Where-Object status -ne 'PASS').Count -ne 0)) {
        throw 'Matriz incompleta.'
    }
} catch {
    $exitCode = 1
    $_.ToString() | Set-Content -LiteralPath (Join-Path $root 'failure.txt') -Encoding UTF8
    Write-Host ('FALHA: ' + $_.Exception.Message)
} finally {
    $status = 'PASS'
    if ($exitCode -eq 1) { $status = 'FAIL' } elseif ($exitCode -eq 2) { $status = 'INCOMPLETE' }
    [pscustomobject]@{ status = $status; audioOptIn = [bool]$AudioBoth;
        executableSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant();
        injection = 'Stop held before forwarding to real native backend; no driver fault';
        hardwareIndicatorReview = $(if ($AudioBoth) { 'PENDING USER' } else { 'NOT TESTED' });
        results = $results } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'summary.json') -Encoding UTF8
}
Write-Host ('Resultado: ' + $status + ' / ' + $root)
exit $exitCode
