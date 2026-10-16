param(
    [Parameter(Mandatory)][string]$Artifact,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$Commit,
    [string]$Bundle
)

$ErrorActionPreference = 'Stop'
$tagMatch = [regex]::Match($Tag, '\Av(?<version>(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?<suffix>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?)\z')
if (-not $tagMatch.Success -or $Commit -cnotmatch '\A[0-9a-f]{40}\z') {
    throw 'Supply the expected SemVer tag and full lowercase source commit from a trusted release decision.'
}
foreach ($part in $tagMatch.Groups['suffix'].Value.Split('.')) {
    if ($part -cmatch '\A[0-9]+\z' -and $part.Length -gt 1 -and $part[0] -eq '0') { throw 'Invalid numeric prerelease identifier.' }
}
$file = Get-Item -LiteralPath $Artifact -ErrorAction Stop
if ($file.PSIsContainer) { throw 'Artifact must be a file.' }
$version = $tagMatch.Groups['version'].Value
if ($file.Name -cnotin @('SlashDesk.exe', "SlashDesk-$version-portable-win-x64.zip", "SlashDesk-$version-installed-staging-win-x64.zip")) {
    throw 'Unexpected release artifact name.'
}
$arguments = @(
    'attestation', 'verify', $file.FullName,
    '--repo', 'lucasllira/SlashText',
    '--cert-identity', "https://github.com/lucasllira/SlashText/.github/workflows/release.yml@refs/tags/$Tag",
    '--source-ref', "refs/tags/$Tag",
    '--source-digest', $Commit,
    '--signer-digest', $Commit,
    '--deny-self-hosted-runners',
    '--predicate-type', 'https://slsa.dev/provenance/v1',
    '--format', 'json'
)
if ($Bundle) {
    $bundleFile = Get-Item -LiteralPath $Bundle -ErrorAction Stop
    if ($bundleFile.PSIsContainer) { throw 'Bundle must be a file.' }
    $arguments += @('--bundle', $bundleFile.FullName)
}
# gh performs cryptographic chain, transparency/timestamp, subject digest and
# certificate identity verification. Never replace it with parsing bundle JSON.
Get-Command gh -ErrorAction Stop | Out-Null
& gh @arguments
if ($LASTEXITCODE -ne 0) { throw 'Release provenance verification failed. Do not install or publish this artifact.' }
