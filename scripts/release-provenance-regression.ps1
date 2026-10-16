$ErrorActionPreference = 'Stop'
$verifier = Join-Path $PSScriptRoot 'verify-release-provenance.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) "SlashDesk provenance tests $([guid]::NewGuid().ToString('N'))"
$savedExitCode = $global:LASTEXITCODE
$count = 0
function Assert-Case([bool]$Value, [string]$Label) {
    if (-not $Value) { throw $Label }
    $script:count++
}
try {
    New-Item -Path $fixture -ItemType Directory | Out-Null
    $file = Join-Path $fixture 'SlashDesk.exe'
    $bundle = Join-Path $fixture 'bundle.json'
    Set-Content -LiteralPath $file 'inert fixture, never executed'
    Set-Content -LiteralPath $bundle '{}'
    $commit = '1234567890123456789012345678901234567890'
    $calls = [Collections.Generic.List[object]]::new()
    $verifyExit = 0
    function gh {
        $calls.Add(@($args))
        $global:LASTEXITCODE = $verifyExit
        if ($verifyExit -eq 0) { '[]' }
    }
    & $verifier -Artifact $file -Tag 'v3.3.0' -Commit $commit -Bundle $bundle | Out-Null
    Assert-Case ($calls.Count -eq 1) 'Expected one cryptographic verifier call.'
    $arguments = $calls[0]
    foreach ($pair in @(
        @('--repo', 'lucasllira/SlashText'),
        @('--cert-identity', 'https://github.com/lucasllira/SlashText/.github/workflows/release.yml@refs/tags/v3.3.0'),
        @('--source-ref', 'refs/tags/v3.3.0'), @('--source-digest', $commit), @('--signer-digest', $commit),
        @('--predicate-type', 'https://slsa.dev/provenance/v1'), @('--bundle', $bundle)
    )) {
        $index = [array]::IndexOf($arguments, $pair[0])
        Assert-Case ($index -ge 0 -and $arguments[$index + 1] -ceq $pair[1]) "Missing fixed policy: $($pair[0])"
    }
    Assert-Case ($arguments -contains '--deny-self-hosted-runners') 'Hosted-runner constraint missing.'
    foreach ($exit in @(1, 2, 4)) {
        $verifyExit = $exit
        $failed = $false
        try { & $verifier -Artifact $file -Tag 'v3.3.0' -Commit $commit -Bundle $bundle | Out-Null } catch { $failed = $true }
        Assert-Case $failed 'Failed/missing/invalid provenance must fail closed.'
    }
    $verifyExit = 0
    foreach ($tag in @('3.3.0', 'v03.3.0', 'v3.3.0-01', 'v3.3.0-rc.01', "v3.3.0`n", 'v3.3.0+build', 'refs/heads/main')) {
        $before = $calls.Count
        $failed = $false
        try { & $verifier -Artifact $file -Tag $tag -Commit $commit | Out-Null } catch { $failed = $true }
        Assert-Case ($failed -and $calls.Count -eq $before) 'Invalid identity reached verification.'
    }
    foreach ($digest in @('abc', ('A' * 40), ($commit + "`n"), '')) {
        $before = $calls.Count
        $failed = $false
        try { & $verifier -Artifact $file -Tag 'v3.3.0' -Commit $digest | Out-Null } catch { $failed = $true }
        Assert-Case ($failed -and $calls.Count -eq $before) 'Invalid commit reached verification.'
    }
    foreach ($name in @('SlashDesk-3.3.0-portable-win-x64.zip', 'SlashDesk-3.3.0-installed-staging-win-x64.zip', 'SlashDesk-3.3.0-rc.1-portable-win-x64.zip')) {
        $path = Join-Path $fixture $name
        Set-Content -LiteralPath $path 'inert archive fixture'
        $tag = if ($name.Contains('rc.1')) { 'v3.3.0-rc.1' } else { 'v3.3.0' }
        $before = $calls.Count
        & $verifier -Artifact $path -Tag $tag -Commit $commit | Out-Null
        Assert-Case ($calls.Count -eq $before + 1 -and $calls[-1] -notcontains '--bundle') 'Valid named artifact/API lookup rejected.'
    }
    $wrongFile = Join-Path $fixture 'SlashDesk-3.2.0-portable-win-x64.zip'
    Set-Content -LiteralPath $wrongFile 'inert historical fixture'
    foreach ($path in @($fixture, (Join-Path $fixture 'missing.exe'), $wrongFile)) {
        $failed = $false
        try { & $verifier -Artifact $path -Tag 'v3.3.0' -Commit $commit | Out-Null } catch { $failed = $true }
        Assert-Case $failed 'Missing or mismatched artifact accepted.'
    }
    $failed = $false
    try { & $verifier -Artifact $file -Tag 'v3.3.0' -Commit $commit -Bundle (Join-Path $fixture 'missing.json') | Out-Null } catch { $failed = $true }
    Assert-Case $failed 'Missing bundle must not fall back to hash-only verification.'
    $workflow = Get-Content (Join-Path $PSScriptRoot '../.github/workflows/release.yml') -Raw
    $previous = -1
    foreach ($step in @('Download validated release artifacts', 'Prepare exact attestation subjects', 'Attest validated release artifacts', 'Verify provenance before publication', 'Create GitHub Release')) {
        $index = $workflow.IndexOf("      - name: $step")
        Assert-Case ($index -gt $previous) "Provenance publication order changed: $step"
        $previous = $index
    }
    Assert-Case ($workflow.Contains('release-assets/*.zip') -and $workflow.Contains('attestation-subjects/SlashDesk.exe')) 'Both archives and portable EXE must be attested.'
    Assert-Case ($workflow.Contains('"release-assets/SlashDesk-$env:RELEASE_VERSION-provenance.json",')) 'Offline bundle must be published.'
    Write-Output "Release provenance regression: PASS ($count assertions; gh mocked, cryptographic checks delegated to gh)"
} finally {
    $global:LASTEXITCODE = $savedExitCode
    $resolved = [IO.Path]::GetFullPath($fixture)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -like 'SlashDesk provenance tests *') {
        Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
    }
}
