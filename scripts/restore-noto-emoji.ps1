param([string]$SourceDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$folder = Join-Path $root 'src/SlashText/Assets/NotoEmoji'
$manifestPath = Join-Path $folder 'catalog.json'
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$destination = Join-Path $folder 'Full'
$stamp = Join-Path $destination '.verified'
$manifestHash = (Get-FileHash $manifestPath -Algorithm SHA256).Hash
if (-not $SourceDirectory -and (Test-Path $stamp) -and (Get-Content $stamp -Raw).Trim() -eq $manifestHash -and
    @(Get-ChildItem $destination -Filter *.png).Count -eq $manifest.Count) { $SourceDirectory = $destination }

function Git-Checked([string[]]$Arguments) {
    & git @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "git failed: $($Arguments -join ' ')" }
}
if (-not $SourceDirectory) {
    $cache = Join-Path ([IO.Path]::GetTempPath()) "slashdesk-noto-$($manifest.Commit)"
    if (-not (Test-Path (Join-Path $cache '.git'))) {
        New-Item $cache -ItemType Directory -Force | Out-Null
        Git-Checked @('init', $cache)
        Git-Checked @('-C', $cache, 'remote', 'add', 'origin', 'https://github.com/googlefonts/noto-emoji.git')
        Git-Checked @('-C', $cache, 'fetch', '--depth=1', '--filter=blob:none', 'origin', $manifest.Commit)
    }
    Git-Checked @('-C', $cache, 'sparse-checkout', 'set', '--cone', '2D/png/128', 'third_party/region-flags/png')
    Git-Checked @('-C', $cache, 'checkout', '--detach', $manifest.Commit)
    $SourceDirectory = $cache
}
New-Item $destination -ItemType Directory -Force | Out-Null
Remove-Item $stamp -ErrorAction SilentlyContinue
foreach ($item in $manifest.Items) {
    if ($item.AssetName -notmatch '^[A-Za-z0-9_-]+\.png$' -or
        $item.SourcePath -notmatch '^(2D/png/128|third_party/region-flags/png)/[A-Za-z0-9_-]+\.png$') { throw 'Invalid asset path' }
    $source = if ($SourceDirectory -eq $destination) { Join-Path $destination $item.AssetName }
              else { Join-Path $SourceDirectory $item.SourcePath }
    $bytes = [IO.File]::ReadAllBytes($source)
    $prefix = [Text.Encoding]::ASCII.GetBytes("blob $($bytes.Length)`0")
    $sha = [Security.Cryptography.SHA1]::Create()
    try {
        $inputBytes = New-Object byte[] ($prefix.Length + $bytes.Length)
        [Array]::Copy($prefix, 0, $inputBytes, 0, $prefix.Length)
        [Array]::Copy($bytes, 0, $inputBytes, $prefix.Length, $bytes.Length)
        $hash = [BitConverter]::ToString($sha.ComputeHash($inputBytes)).Replace('-', '').ToLowerInvariant()
        if ($hash -ne $item.BlobSha) { throw "Noto asset hash mismatch: $($item.AssetName)" }
    } finally { $sha.Dispose() }
    if ([IO.Path]::GetFullPath($SourceDirectory) -ne [IO.Path]::GetFullPath($destination)) {
        Copy-Item $source (Join-Path $destination $item.AssetName) -Force
    }
}
# Remove stale assets from older snapshots; no obsolete catalog entries can leak in.
$names = @{}; foreach ($item in $manifest.Items) { $names[$item.AssetName] = $true }
Get-ChildItem $destination -Filter *.png | Where-Object { -not $names.ContainsKey($_.Name) } | Remove-Item
Set-Content $stamp -Value $manifestHash -Encoding ascii
"Verified $($manifest.Count) offline Noto PNGs from $($manifest.Commit)."
