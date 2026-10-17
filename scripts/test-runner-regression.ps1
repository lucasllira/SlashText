#Requires -Version 7.2
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../test-results'))
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'testing/TestRunner.psm1') -Force
$root=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ('orchestrator-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[void][IO.Directory]::CreateDirectory($root)
$stub=Join-Path $root 'inert fixture ç.ps1'
@'
param($Mode,$Value,$Output,$Cancel)
switch($Mode) {
 'pass' { [IO.File]::WriteAllText((Join-Path $Output 'result.txt'),$Value); Write-Output $Value }
 'failure' { [Console]::Error.WriteLine('intentional inert failure'); exit 7 }
 'unavailable' { exit 2 }
 'marker' { Write-Output 'Clipboard native: UNAVAILABLE (inert)' }
 'cancel' { [IO.File]::WriteAllText($Cancel,'cancel'); Start-Sleep -Seconds 10 }
 'timeout' { [IO.File]::WriteAllText((Join-Path $Output 'owned-pid.txt'),[string]$PID); Start-Sleep -Seconds 10 }
 'skip' { [IO.File]::WriteAllText((Join-Path $Output 'result.txt'),'[{"Skipped":"no fixture device"}]') }
 'volume' { [Console]::Out.Write(('o'*262144)); [Console]::Error.Write(('e'*262144)) }
 'prepare' { Copy-Item -LiteralPath $Value -Destination (Join-Path $Output 'fixture.exe') }
}
'@ | Set-Content -LiteralPath $stub -Encoding utf8
$pwsh=Join-Path $PSHOME 'pwsh.exe'
$checks=[Collections.Generic.List[string]]::new()
function Check($Ok,$Label) { if(-not $Ok) { throw $Label }; $checks.Add($Label) }
function Step($Id,$Mode,$Expected=@()) {
    [ordered]@{id=$Id;tool=$pwsh;arguments=@('-NoProfile','-File',$stub,'-Mode',$Mode,'-Value','literal " ç $() ` value','-Output','{output}');timeoutSeconds=20;expectedFiles=$Expected}
}
function Run($Name,$Steps,$Cancel='') {
    Invoke-TestSteps -Steps $Steps -WorkingDirectory (Split-Path $PSScriptRoot -Parent) -OutputDirectory (Join-Path $root $Name) -CancelFile $Cancel
}
$r=Run 'success with spaces ç' @((Step 'first' 'pass' @('result.txt')),(Step 'second' 'pass' @('result.txt')))
Check ($r.status -eq 'PASS' -and $r.exitCode -eq 0 -and $r.results.Count -eq 2) 'two independent child processes pass'
Check ((Get-Content -LiteralPath (Join-Path $root 'success with spaces ç/first/result.txt') -Raw) -ceq 'literal " ç $() ` value') 'arguments remain literal across spaces, Unicode, quotes and shell metacharacters'
Check (Test-Path -LiteralPath (Join-Path $root 'success with spaces ç/summary.md')) 'human summary exists'
$r=Run 'failure' @((Step 'failed' 'failure'),(Step 'never-started' 'pass'))
Check ($r.status -eq 'FAIL' -and $r.exitCode -eq 1 -and $r.results[0].exitCode -eq 7 -and $r.results[1].status -eq 'NOT_RUN') 'failure retains child code and stops dependent steps'
Check ((Get-Content (Join-Path $root 'failure/failed/stderr.log') -Raw).Contains('intentional inert failure')) 'stderr retained'
$r=Run 'missing-tool' @([ordered]@{id='missing';tool=(Join-Path $root 'not-installed.exe');arguments=@();timeoutSeconds=1;expectedFiles=@()})
Check ($r.status -eq 'INCOMPLETE' -and $r.exitCode -eq 2 -and $r.results[0].status -eq 'UNAVAILABLE') 'missing prerequisite is incomplete, never pass'
$step=Step 'unavailable' 'unavailable';$step.unavailableExitCodes=@(2)
$r=Run 'exit-two' @($step)
Check ($r.status -eq 'INCOMPLETE') 'exit two means unavailable'
$r=Run 'unexpected-exit-two' @((Step 'unexpected' 'unavailable'))
Check ($r.status -eq 'FAIL') 'undeclared nonzero exit is a failure, not an environmental excuse'
$r=Run 'pipe-volume' @((Step 'volume' 'volume'))
Check ($r.status -eq 'PASS' -and (Get-Item (Join-Path $root 'pipe-volume/volume/stdout.log')).Length -eq 262144 -and (Get-Item (Join-Path $root 'pipe-volume/volume/stderr.log')).Length -eq 262144) 'both redirected streams drain without pipe deadlock or lost bytes'
$step=Step 'marker' 'marker';$step.unavailablePattern='^Clipboard native: UNAVAILABLE'
$r=Run 'subtest-unavailable' @($step)
Check ($r.status -eq 'INCOMPLETE') 'embedded clipboard unavailability cannot be hidden by parent exit zero'
$step=Step 'skip' 'skip' @('result.txt');$step.unavailableJsonProperty='Skipped'
$r=Run 'native-source-unavailable' @($step)
Check ($r.results[0].status -eq 'UNAVAILABLE') 'native source skip cannot pass'
$r=Run 'missing-evidence' @((Step 'missing-evidence' 'marker' @('result.txt')))
Check ($r.status -eq 'FAIL') 'zero exit without required evidence fails'
$step=Step 'timeout' 'timeout';$step.timeoutSeconds=1
$r=Run 'timeout' @($step)
Check ($r.status -eq 'FAIL' -and $r.results[0].reason -eq 'Test timeout.') 'timeout is a failure'
$ownedPid=[int](Get-Content (Join-Path $root 'timeout/timeout/owned-pid.txt'))
Check (-not (Get-Process -Id $ownedPid -ErrorAction SilentlyContinue)) 'timed-out owned process exited'
$cancel=Join-Path $root 'cancel.flag';$step=Step 'cancel' 'cancel';$step.arguments+=@('-Cancel',$cancel)
$r=Run 'cancelled' @($step,(Step 'never-started' 'pass')) $cancel
Check ($r.status -eq 'CANCELLED' -and $r.exitCode -eq 130 -and $r.results[1].status -eq 'NOT_RUN') 'mid-run cancellation stops only owned process and following steps'
$r=Run 'cancel-before-start' @((Step 'never-started' 'pass')) $cancel
Check ($r.status -eq 'CANCELLED' -and -not(Test-Path (Join-Path $root 'cancel-before-start/never-started'))) 'existing cancellation marker prevents startup'
$step=Step 'manual' 'pass' @('result.txt');$step.manualReview=$true
$r=Run 'manual-review' @($step)
Check ($r.status -eq 'INCOMPLETE' -and $r.results[0].status -eq 'REVIEW_REQUIRED') 'automatic success does not claim physical review'
$refused=$false;try { Run 'success with spaces ç' @((Step 'first' 'pass')) | Out-Null }catch{$refused=$true}
Check $refused 'existing evidence never overwritten'
$refused=$false;try { Run 'duplicate' @((Step 'same' 'pass'),(Step 'same' 'pass')) | Out-Null }catch{$refused=$true}
Check $refused 'duplicate tasks rejected'
$refused=$false;try { Run 'escape' @((Step '../escape' 'pass')) | Out-Null }catch{$refused=$true}
Check $refused 'step path traversal rejected'
$r=Run 'consent' @([ordered]@{id='consent';unavailableReason='No hardware opt-in.'})
Check ($r.status -eq 'INCOMPLETE') 'unapproved hardware plan contains no executable'
$entry=Join-Path $PSScriptRoot 'test.ps1'
$full=@(& $pwsh -NoProfile -File $entry -Profile Full -List)
Check ($LASTEXITCODE -eq 0 -and 'domain' -in $full -and 'settings-ui' -in $full -and 'ocr-model-trust' -in $full -and 'native-stop' -notin $full -and 'native-audio' -notin $full) 'full plan contains real domain/UI tests and no physical recording'
$gate=@(& $pwsh -NoProfile -File $entry -Profile Hardware -List)
Check ($LASTEXITCODE -eq 0 -and $gate.Count -eq 1 -and $gate[0] -eq 'hardware-consent') 'public hardware entry point refuses missing opt-in before launch'
$hardware=@(& $pwsh -NoProfile -File $entry -Profile Hardware -AllowHardware -AppExecutable 'inert-not-launched.exe' -List)
Check ($LASTEXITCODE -eq 0 -and 'native-stop' -in $hardware -and 'native-audio' -notin $hardware) 'hardware opt-in does not implicitly consent to microphone or computer audio'
$hardware=@(& $pwsh -NoProfile -File $entry -Profile Hardware -AllowHardware -IncludeAudio -AppExecutable 'inert-not-launched.exe' -List)
Check ($LASTEXITCODE -eq 0 -and 'native-audio' -in $hardware) 'audio requires a second explicit switch'
$gateOutput=Join-Path $root 'public-gate'
& $pwsh -NoProfile -File $entry -Profile Hardware -OutputDirectory $gateOutput | Out-Null
$gateReport=Get-ChildItem -LiteralPath $gateOutput -Recurse -Filter summary.json | Select-Object -First 1 | Get-Content -Raw | ConvertFrom-Json
Check ($LASTEXITCODE -eq 2 -and $gateReport.status -eq 'INCOMPLETE' -and $gateReport.results[0].status -eq 'UNAVAILABLE') 'public gate returns incomplete evidence and exit two, without hardware'
$longOutput=Join-Path $root ('long-fixture-'+('x'*60))
& $pwsh -NoProfile -File $entry -Profile Full -SkipBuild -Task recording-audio-ui -AppExecutable 'inert-not-launched.exe' -OutputDirectory $longOutput | Out-Null
$longReport=Get-ChildItem -LiteralPath $longOutput -Recurse -Filter summary.json | Select-Object -First 1 | Get-Content -Raw | ConvertFrom-Json
Check ($LASTEXITCODE -eq 2 -and $longReport.results[0].status -eq 'UNAVAILABLE' -and $longReport.results[0].reason.Contains('MAX_PATH')) 'recovery fixture path budget is checked before launch without weakening product guard'
$unknownOutput=Join-Path $root 'unknown-version'
& $pwsh -NoProfile -File $entry -Profile Full -SkipBuild -Task about-ui -AppExecutable $pwsh -OutputDirectory $unknownOutput | Out-Null
$unknownReport=Get-ChildItem -LiteralPath $unknownOutput -Recurse -Filter summary.json | Select-Object -First 1 | Get-Content -Raw | ConvertFrom-Json
Check ($LASTEXITCODE -eq 2 -and $unknownReport.results[0].status -eq 'UNAVAILABLE' -and $unknownReport.results[0].reason.Contains('FileVersion')) 'unknown product version is never launched with helper switches that could open normal startup'
$prepare=Step 'build-fixture' 'prepare';$prepare.arguments=@('-NoProfile','-File',$stub,'-Mode','prepare','-Value',$pwsh,'-Output','{output}')
$version=Step 'versioned' 'pass';$version.versionedFile=Join-Path $root 'version-after-build/build-fixture/fixture.exe';$version.requiredFileVersion=(Get-Item $pwsh).VersionInfo.FileVersion
$r=Run 'version-after-build' @($prepare,$version)
Check ($r.status -eq 'PASS') 'version preflight runs after the build step creates a fresh binary'
@{status='PASS';checks=$checks.Count;results=@($checks.ToArray())} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'regression.json') -Encoding utf8
Write-Output "Test orchestration regression: PASS ($($checks.Count) checks; inert child processes only); $root"
# Negative CLI cases deliberately leave LASTEXITCODE=2. The GHA pwsh wrapper
# propagates it even after all assertions pass unless this script owns its exit.
exit 0
