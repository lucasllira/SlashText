param([Parameter(Mandatory)][string]$Executable,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$exe=(Resolve-Path -LiteralPath $Executable).Path
$output=[IO.Path]::GetFullPath($OutputDirectory)
$process=Start-Process $exe -ArgumentList "--accessibility-names-smoke `"$output`"" -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(90000)) { $process.Kill(); throw 'Accessibility name smoke timed out.' }
if($process.ExitCode -ne 0) {
    Get-Content (Join-Path $output 'failure.txt') -ErrorAction SilentlyContinue
    throw 'Accessibility name smoke failed.'
}
Get-Content (Join-Path $output 'result.txt')
