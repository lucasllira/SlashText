param([string]$FixtureDirectory)
$ErrorActionPreference = 'Stop'
$savedExitCode = $global:LASTEXITCODE
$owned = -not $FixtureDirectory
if ($owned) { $FixtureDirectory = Join-Path ([IO.Path]::GetTempPath()) "SlashDesk crypto tests $([guid]::NewGuid().ToString('N'))" }
New-Item -Path $FixtureDirectory -ItemType Directory -Force | Out-Null
$root = [IO.Path]::GetFullPath($FixtureDirectory)
$count = 0
$source = 'https://raw.githubusercontent.com/cli/cli/55dbb4dc6b7edb10b48e3d7fc5bccd32318d1b55/pkg/cmd/attestation/test/data'
$artifactName = 'github_provenance_demo-0.0.12-py3-none-any.whl'
$bundleName = 'github_provenance_demo-0.0.12-py3-none-any-bundle.jsonl'
$expected = @{
    $artifactName = 'ae57936def59bc4c75edd3a837d89bcefc6d3a5e31d55a6fa7a71624f92c3c3b'
    $bundleName = '4f8c096e38a0eee242574ab100d16701928605409225e59784a3636f742bb27e'
}
function Check-Verification([string[]]$Arguments, [bool]$Success, [string]$Label) {
    $output = & gh @Arguments 2>&1 | Out-String
    $code = $LASTEXITCODE
    if (($code -eq 0) -ne $Success) { throw "Cryptographic case failed: $Label (exit $code). $output" }
    $script:count++
}
try {
    Get-Command gh -ErrorAction Stop | Out-Null
    foreach ($name in $expected.Keys) {
        $path = Join-Path $root $name
        if (-not (Test-Path -LiteralPath $path)) { Invoke-WebRequest -Uri "$source/$name" -OutFile $path }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expected[$name]) {
            throw 'Pinned inert CLI fixture hash mismatch.'
        }
    }
    $artifact = Join-Path $root $artifactName
    $bundle = Join-Path $root $bundleName
    $commit = 'a6c23b9806c593664f68637c8f9d45dfcf98b2db'
    $policy = @('attestation', 'verify', $artifact, '--bundle', $bundle, '--repo', 'actions/attest-demo',
        '--cert-identity', 'https://github.com/actions/attest-demo/.github/workflows/build-python.yml@refs/heads/main',
        '--source-ref', 'refs/heads/main', '--source-digest', $commit, '--signer-digest', $commit,
        '--deny-self-hosted-runners', '--predicate-type', 'https://slsa.dev/provenance/v1', '--format', 'json')
    Check-Verification $policy $true 'valid official SLSA fixture, public trusted roots, no authentication needed'
    foreach ($pair in @(
        @('--repo', 'lucasllira/SlashText'), @('--cert-identity', 'https://github.com/actions/attest-demo/.github/workflows/other.yml@refs/heads/main'),
        @('--source-ref', 'refs/tags/v3.3.0'), @('--source-digest', ('0' * 40)), @('--signer-digest', ('0' * 40)),
        @('--predicate-type', 'https://invalid.example/predicate'),
        @('--cert-identity', 'https://github.com/actions/attest-demo/.github/workflows/build-python.yml@refs/tags/v3.3.0')
    )) {
        $arguments = $policy.Clone()
        $arguments[[array]::IndexOf($arguments, $pair[0]) + 1] = $pair[1]
        Check-Verification $arguments $false "wrong policy $($pair[0])"
    }
    $changedArtifact = Join-Path $root 'modified.whl'
    $bytes = [IO.File]::ReadAllBytes($artifact)
    $bytes[-1] = $bytes[-1] -bxor 1
    [IO.File]::WriteAllBytes($changedArtifact, $bytes)
    $arguments = $policy.Clone(); $arguments[2] = $changedArtifact
    Check-Verification $arguments $false 'modified artifact, original valid proof'
    $changedBundle = Join-Path $root 'modified-bundle.json'
    $data = Get-Content -LiteralPath $bundle -Raw | ConvertFrom-Json
    $signature = [Convert]::FromBase64String($data.dsseEnvelope.signatures[0].sig)
    $signature[-1] = $signature[-1] -bxor 1
    $data.dsseEnvelope.signatures[0].sig = [Convert]::ToBase64String($signature)
    $data | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath $changedBundle
    $arguments = $policy.Clone(); $arguments[[array]::IndexOf($arguments, '--bundle') + 1] = $changedBundle
    Check-Verification $arguments $false 'modified signature, original artifact'
    Set-Content -LiteralPath $changedBundle '{}'
    Check-Verification $arguments $false 'missing verification material'
    # Exercise the actual consumer entrypoint with a valid proof from another producer.
    $renamed = Join-Path $root 'SlashDesk.exe'
    Copy-Item -LiteralPath $artifact -Destination $renamed -Force
    $failed = $false
    try { & "$PSScriptRoot/verify-release-provenance.ps1" -Artifact $renamed -Tag 'v3.3.0' -Commit $commit -Bundle $bundle 2>&1 | Out-Null } catch { $failed = $true }
    if (-not $failed) { throw 'Actual consumer accepted a valid signature from another producer.' }
    $count++
    Write-Output "Release provenance crypto: PASS ($count real gh verifications; pinned inert official fixtures; no artifact execution)"
} finally {
    $global:LASTEXITCODE = $savedExitCode
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($owned -and $root.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $root -Leaf) -like 'SlashDesk crypto tests *') {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}
