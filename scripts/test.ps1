#Requires -Version 7.2
[CmdletBinding()]
param(
    [ValidateSet('Quick','Full','Hardware')][string]$Profile = 'Quick',
    [string[]]$Task = @(),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../test-results'),
    [string]$DotnetPath = 'dotnet',
    [switch]$SkipBuild,
    [string]$AppExecutable,
    [string]$TestExecutable,
    [string]$CancelFile,
    [switch]$AllowHardware,
    [switch]$IncludeAudio,
    [switch]$List
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'testing/TestRunner.psm1') -Force
$repo = Split-Path $PSScriptRoot -Parent
$pwsh = Join-Path $PSHOME 'pwsh.exe'
$steps = [Collections.Generic.List[object]]::new()
function Add-Step([string]$Id,[string]$Tool,[string[]]$Arguments,[int]$Timeout=120,[string[]]$Expected=@()) {
    $steps.Add([ordered]@{ id=$Id; tool=$Tool; arguments=$Arguments; timeoutSeconds=$Timeout; expectedFiles=$Expected })
}
$contracts = [ordered]@{
    'actions-policy'='actions-policy-smoke.ps1'; 'actions-regression'='actions-policy-regression.ps1'
    'languages'='languages-catalog-smoke.ps1'; 'ui-inventory'='compare-ui-inventory.ps1'; 'ui-integrity'='ui-integrity-smoke.ps1'
    'capture-contract'='capture-pilot-smoke.ps1'; 'toolbar-contract'='capture-toolbar-visual-contract-smoke.ps1'
    'visual-foundation'='visual-foundation-smoke.ps1'; 'workspace-contract'='shortcuts-workspace-smoke.ps1'
    'release-contract'='release-workflow-smoke.ps1'; 'notices-regression'='third-party-notices-regression.ps1'
}
if ($Profile -in @('Quick','Full')) {
    foreach ($entry in $contracts.GetEnumerator()) {
        Add-Step $entry.Key $pwsh @('-NoProfile','-File',(Join-Path $PSScriptRoot $entry.Value))
    }
}
if ($Profile -eq 'Full') {
    if (-not $SkipBuild) {
        Add-Step 'restore' $DotnetPath @('restore','SlashText.sln','-p:Platform=x64') 600
        Add-Step 'build' $DotnetPath @('build','SlashText.sln','-c','Release','--no-restore','-p:Platform=x64') 600
    }
    if (-not $AppExecutable) { $AppExecutable=Join-Path $repo 'src/SlashText/bin/x64/Release/net10.0-windows/SlashDesk.exe' }
    if (-not $TestExecutable) { $TestExecutable=Join-Path $repo 'tests/SlashText.SmokeTests/bin/x64/Release/net10.0-windows/SlashText.SmokeTests.exe' }
    Add-Step 'domain' $TestExecutable @() 300
    $steps[$steps.Count-1].unavailablePattern='^Clipboard native: UNAVAILABLE'
    Add-Step 'gif-timeline' $TestExecutable @('--gif-timeline-smoke','{output}') 120 @('result.txt')
    $modes=[ordered]@{
        'workspace-ui'='--shortcuts-workspace-smoke'; 'accent-ui'='--quick-accent-smoke'; 'statistics-ui'='--statistics-smoke'
        'settings-ui'='--settings-smoke'; 'about-ui'='--about-smoke'; 'auxiliary-ui'='--auxiliary-smoke'
        'languages-ui'='--languages-smoke'; 'accessible-names'='--accessibility-names-smoke'; 'notices-ui'='--third-party-notices-smoke'
        'usage-failure-ui'='--usage-write-ui-smoke'; 'preferences-failure-ui'='--capture-preference-ui-smoke'
        'recording-reservation-ui'='--recording-operation-ui-smoke'; 'recording-targets-ui'='--recording-targets-smoke'
        'recording-audio-ui'='--recording-audio-smoke'; 'protected-storage-ui'='--protected-storage-smoke'
        'legacy-backups-ui'='--legacy-backups-smoke'; 'restore-recovery-ui'='--restore-recovery-smoke'
        'virtualized-list-ui'='--shortcut-list-smoke'; 'ocr-model-trust'='--capture-ocr-native-smoke'
    }
    foreach ($mode in $modes.GetEnumerator()) {
        $expected = if ($mode.Key -eq 'ocr-model-trust') { 'result.json' } else { 'result.txt' }
        Add-Step $mode.Key $AppExecutable @($mode.Value,'{output}') 180 @($expected)
    }
}
if ($Profile -eq 'Hardware') {
    if (-not $AllowHardware) {
        $steps.Add([ordered]@{id='hardware-consent';unavailableReason='Hardware requires explicit -AllowHardware. No capture or audio was started.'})
    } elseif (-not $AppExecutable) {
        $steps.Add([ordered]@{id='hardware-executable';unavailableReason='Specify the exact tested EXE with -AppExecutable.'})
    } else {
        Add-Step 'native-stop' $pwsh @('-NoProfile','-File',(Join-Path $PSScriptRoot 'recording-native-stop-smoke.ps1'),'-Executable',$AppExecutable,'-OutputDirectory','{output}','-Unattended') 240
        $steps[$steps.Count-1].unavailableExitCodes=@(2)
        if ($IncludeAudio) {
            Add-Step 'native-audio' $AppExecutable @('--recording-audio-smoke','{output}','--native') 240 @('native.json')
            $steps[$steps.Count-1].manualReview=$true
            $steps[$steps.Count-1].unavailableJsonProperty='Skipped'
        }
    }
} elseif ($AllowHardware -or $IncludeAudio) { throw 'Hardware switches are valid only with -Profile Hardware.' }
if ($Task.Count) {
    $unknown=@($Task | Where-Object { $_ -notin @($steps | ForEach-Object {$_.id}) })
    if ($unknown.Count -or @($Task | Sort-Object -Unique).Count -ne $Task.Count) { throw 'Unknown or repeated task. Inspect -List first.' }
    if ($Profile -eq 'Hardware') { throw 'Hardware tasks cannot bypass the consent plan.' }
    $selected=@($steps | Where-Object { $_.id -in $Task })
} else { $selected=@($steps.ToArray()) }
if ($List) { $selected | ForEach-Object {$_.id}; exit 0 }
$run=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ($Profile.ToLowerInvariant()+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
foreach ($step in $selected) {
    # The recovery fixture intentionally uses the product's MAX_PATH guard even
    # with a fake backend. Include its complete private working-file suffix.
    if ($step.id -eq 'recording-audio-ui') {
        $fixturePath=Join-Path $run 'recording-audio-ui/operation-recovery/pt-BR-Light/inert-media/.recovery-operation-fixture.00000000000000000000000000000000.recording.mp4'
        if ($fixturePath.Length -ge 260) {
            $step.unavailableReason='Evidence path makes the inert recording recovery fixture exceed MAX_PATH. Select a shorter -OutputDirectory; the product guard is preserved.'
        }
    }
}
$source=& git -C $repo rev-parse HEAD 2>$null
$dirty = [bool](& git -C $repo status --porcelain 2>$null)
$metadata=@{ profile=$Profile; selectedSubset=[bool]$Task.Count; sourceCommit=$source; sourceDirty=$dirty; powershellVersion=$PSVersionTable.PSVersion.ToString(); hardwareOptIn=[bool]$AllowHardware; audioOptIn=[bool]($AllowHardware -and $IncludeAudio)
    coverage='Quick=source contracts; Full=build/domain/presented UI/offline OCR; Hardware=owned fixture window with explicit opt-in. Packaging/security scans/provenance publication remain in dedicated CI.' }
Write-Host "Evidence: $run"
$report=Invoke-TestSteps -Steps $selected -WorkingDirectory $repo -OutputDirectory $run -CancelFile $CancelFile -Metadata $metadata
Write-Host "Result: $($report.status); $run"
exit $report.exitCode
