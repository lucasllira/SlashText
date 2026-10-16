param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path $Executable).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item $output -ItemType Directory -Force | Out-Null
$process = Start-Process $exe -ArgumentList "--gif-timeline-smoke `"$output`"" -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $output 'stdout.txt') -RedirectStandardError (Join-Path $output 'stderr.txt')
if (-not $process.WaitForExit(90000)) { $process.Kill(); throw 'GIF timeline smoke timeout' }
Get-Content (Join-Path $output 'stdout.txt')
if ($process.ExitCode -ne 0) {
    Get-Content (Join-Path $output 'failure.txt') -ErrorAction SilentlyContinue
    Get-Content (Join-Path $output 'stderr.txt') -ErrorAction SilentlyContinue
    throw "GIF timeline smoke failed: $($process.ExitCode)"
}
Get-Content (Join-Path $output 'result.txt')
