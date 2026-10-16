param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][ValidateSet('Portable', 'Installed')][string]$Layout,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh candidate evidence directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
[xml]$project = Get-Content (Join-Path $PSScriptRoot '../src/SlashText/SlashText.csproj') -Raw
$version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
$results = [Collections.Generic.List[object]]::new()
function Invoke-CandidateProbe([string]$Name, [string[]]$Arguments, [int]$Timeout = 120) {
    $child = Start-Process -FilePath $exe -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    $null = $child.Handle
    if (-not $child.WaitForExit($Timeout * 1000)) {
        $child.Kill(); throw "Candidate probe timed out: $Name"
    }
    if ($child.ExitCode -ne 0) { throw "Candidate probe failed: $Name (exit $($child.ExitCode))" }
    $results.Add([ordered]@{ name = $Name; status = 'PASS'; exitCode = $child.ExitCode })
}
try {
    if ($info.FileVersion -cne "$version.0" -or $info.ProductVersion.Split('+')[0] -cne $version) {
        throw 'Published executable identity differs from the candidate source version.'
    }
    # Real startup probes may perform migration. Only a fresh, disposable CI account
    # is allowed; do not use an installed personal profile as a local fallback.
    if ($env:GITHUB_ACTIONS -eq 'true') {
        $expectedData = if ($Layout -eq 'Portable') {
            Join-Path (Split-Path $exe -Parent) 'SlashDeskData'
        } else { Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'SlashDesk' }
        if (Test-Path -LiteralPath $expectedData) { throw 'Candidate startup requires a fresh CI data profile.' }
        Invoke-CandidateProbe 'real-isolated-startup' @('--portable-smoke')
        if (-not (Test-Path -LiteralPath (Join-Path $expectedData 'snippets.md'))) {
            throw 'Startup did not select the expected production distribution data directory.'
        }
        if (Test-Path -LiteralPath (Join-Path (Split-Path $exe -Parent) 'SlashDeskPilotData')) {
            throw 'Candidate unexpectedly selected a pilot data directory.'
        }
    } else {
        $results.Add([ordered]@{ name = 'real-isolated-startup'; status = 'UNAVAILABLE'; reason = 'Requires fresh disposable CI account; no personal profile accessed.' })
    }
    foreach ($probe in @(
        @('about', '--about-smoke'),
        @('languages', '--languages-smoke'),
        @('notices', '--third-party-notices-smoke'),
        @('ocr', '--capture-ocr-native-smoke')
    )) {
        $destination = Join-Path $output $probe[0]
        Invoke-CandidateProbe $probe[0] @($probe[1], ('"{0}"' -f $destination)) 180
    }
    $ocr = Get-Content (Join-Path $output 'ocr/result.json') -Raw | ConvertFrom-Json
    if ($ocr.status -ne 'PASS' -or $ocr.results.Count -ne 6) { throw 'Native OCR evidence incomplete.' }
    $status = if (@($results | Where-Object status -eq 'UNAVAILABLE').Count) { 'PASS_WITH_LIMITATIONS' } else { 'PASS' }
    [ordered]@{
        status = $status; layout = $Layout; version = $version
        sourceCommit = (& git -C (Join-Path $PSScriptRoot '..') rev-parse HEAD).Trim()
        executableSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
        probes = $results
        limitations = 'Synthetic fixtures; no hooks or personal clipboard/screen/audio. Hosted CI does not prove clean Windows without VC++ or physical DPI/audio.'
    } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'summary.json') -Encoding utf8
    Write-Output "Candidate $Layout package: $status"
} catch {
    [ordered]@{ status = 'FAIL'; layout = $Layout; error = $_.Exception.Message; probes = $results } |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'summary.json') -Encoding utf8
    throw
}
