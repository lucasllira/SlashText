$ErrorActionPreference='Stop'
$generator=Join-Path $PSScriptRoot 'generate-third-party-notices.ps1'
$root=Join-Path ([IO.Path]::GetTempPath()) "SlashDesk noticegen tests $([guid]::NewGuid().ToString('N'))"
$count=0
function Check([bool]$value,[string]$label){if(-not $value){throw $label};$script:count++}
try {
    $docs=Join-Path $root 'src/SlashText/Assets/ThirdParty'
    $cache=Join-Path $root 'cache/screenrecorderlib/6.6.0'
    New-Item $docs,$cache -ItemType Directory -Force | Out-Null
    $text="Copyright example author.`nPermission is hereby granted for this inert license fixture.`n"
    $bytes=[Text.Encoding]::UTF8.GetBytes($text)
    $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    $file=Join-Path $docs 'Recorder.txt';[IO.File]::WriteAllBytes($file,$bytes)
    [IO.File]::WriteAllBytes((Join-Path $cache 'LICENSE'),$bytes)
    $catalog=[ordered]@{schemaVersion=1;normalization='UTF-8/LF';validatedDesktopRuntimeVersions=@('10.0.12','10.0.13');documents=@([ordered]@{id='Recorder';name='Recorder';version='6.6.0';license='MIT';file='Recorder.txt';sha256=$hash;source='https://example.invalid/source';package='ScreenRecorderLib'})}
    $catalogPath=Join-Path $docs 'catalog.json';$catalog | ConvertTo-Json -Depth 6 | Set-Content $catalogPath
    $assetsPath=Join-Path $root 'assets.json'
    $assets=[ordered]@{libraries=@{'ScreenRecorderLib/6.6.0'=@{path='screenrecorderlib/6.6.0'}};packageFolders=@{(Join-Path $root 'cache')=@{}}}
    $assets | ConvertTo-Json -Depth 6 | Set-Content $assetsPath
    $packs=Join-Path $root 'runtime-packs.txt';Set-Content $packs ''
    $output=Join-Path $root 'generated'
    function Generate { & $generator -Root $root -AssetsFile $assetsPath -RuntimePacksFile $packs -OutputDirectory $output | Out-Null }
    function Rejected([string]$label){$failed=$false;try{Generate}catch{$failed=$true};Check $failed $label}
    Generate
    $manifest=Get-Content (Join-Path $output 'catalog.json') -Raw | ConvertFrom-Json
    Check ($manifest.documents.Count -eq 1 -and $manifest.documents[0].sha256 -ceq $hash) 'Valid notice manifest does not match source.'
    $archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $output 'notices.zip'))
    try {
        $reader=[IO.StreamReader]::new($archive.Entries[0].Open());try{$body=$reader.ReadToEnd()}finally{$reader.Dispose()}
        Check ($body -ceq $text) 'Complete original license was not retained.'
    } finally {$archive.Dispose()}
    Set-Content $file 'tampered license';Rejected 'Modified source notice accepted.';[IO.File]::WriteAllBytes($file,$bytes)
    Rename-Item -LiteralPath $file -NewName 'Recorder-unavailable.txt';Rejected 'Missing source notice accepted.';Rename-Item (Join-Path $docs 'Recorder-unavailable.txt') 'Recorder.txt'
    $assets.libraries=@{'ScreenRecorderLib/7.0.1'=@{path='screenrecorderlib/6.6.0'}};$assets | ConvertTo-Json -Depth 6 | Set-Content $assetsPath
    Rejected 'Changed package version accepted with stale license review.'
    $assets.libraries=@{'ScreenRecorderLib/6.6.0'=@{path='screenrecorderlib/6.6.0'}};$assets | ConvertTo-Json -Depth 6 | Set-Content $assetsPath
    Set-Content (Join-Path $cache 'LICENSE') 'different NuGet license';Rejected 'NuGet recorder license mismatch accepted.';[IO.File]::WriteAllBytes((Join-Path $cache 'LICENSE'),$bytes)
    Set-Content $packs "Microsoft.NETCore.App.Runtime.win-x64|10.0.14|$cache";Rejected 'Unreviewed runtime notice version accepted.'
    Set-Content $packs "Microsoft.NETCore.App.Runtime.win-x64|10.0.12|$cache";Rejected 'Runtime without transitive notices accepted.'
    [IO.File]::WriteAllBytes((Join-Path $cache 'THIRD-PARTY-NOTICES.TXT'),$bytes)
    foreach($version in @('10.0.12','10.0.13')) {
        Set-Content $packs "Microsoft.NETCore.App.Runtime.win-x64|$version|$cache";Generate
        $runtimeManifest=Get-Content (Join-Path $output 'catalog.json') -Raw | ConvertFrom-Json
        $runtimeDocs=@($runtimeManifest.documents | Where-Object version -eq $version)
        Check ($runtimeDocs.Count -eq 2 -and @($runtimeDocs.name | Select-Object -Unique).Count -eq 2 -and @($runtimeDocs | Where-Object source -ne "https://www.nuget.org/packages/Microsoft.NETCore.App.Runtime.win-x64/$version").Count -eq 0) 'Reviewed runtime notices lost identity or exact resolved version.'
    }
    Set-Content $packs '';Generate
    Write-Output "Third-party notice generation: PASS ($count cases; original text retained; modified/missing/stale package/runtime notices rejected; no network)"
} finally {
    $resolved=[IO.Path]::GetFullPath($root)
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    if($resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -like 'SlashDesk noticegen tests *') {Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue}
}
