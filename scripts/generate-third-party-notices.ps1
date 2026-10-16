param(
    [Parameter(Mandatory)][string]$AssetsFile,
    [Parameter(Mandatory)][string]$RuntimePacksFile,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)
$catalog = Get-Content (Join-Path $Root 'src/SlashText/Assets/ThirdParty/catalog.json') -Raw | ConvertFrom-Json
$assets = Get-Content -LiteralPath $AssetsFile -Raw | ConvertFrom-Json -AsHashtable
$documents = [Collections.Generic.List[object]]::new()
$payloads = [ordered]@{}
$packages = [Collections.Generic.List[object]]::new()
function Canonical-Bytes([string]$Path) {
    [Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($Path).Replace("`r`n", "`n").Replace("`r", "`n"))
}
function Hash([byte[]]$Bytes) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant() }
function Add-Document([string]$Id, [string]$Name, [string]$Version, [string]$License, [string]$Source, [byte[]]$Bytes) {
    if ($Id -cnotmatch '\A[A-Za-z0-9_-]+\z' -or $payloads.Contains($Id + '.txt') -or $Bytes.Length -lt 20) { throw 'Invalid or duplicate notice document.' }
    $entry = $Id + '.txt'
    $payloads[$entry] = $Bytes
    $documents.Add([ordered]@{ id=$Id; name=$Name; version=$Version; license=$License; source=$Source; entry=$entry; sha256=(Hash $Bytes); length=$Bytes.Length })
}
foreach ($item in $catalog.documents) {
    $relative = if ($item.PSObject.Properties['sourcePath']) { $item.sourcePath } else { 'src/SlashText/Assets/ThirdParty/' + $item.file }
    $path = [IO.Path]::GetFullPath((Join-Path $Root $relative))
    if (-not $path.StartsWith($Root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'License source escapes repository.' }
    $bytes = Canonical-Bytes $path
    if ((Hash $bytes) -cne $item.sha256) { throw "Reviewed notice changed: $($item.id). Review and update catalog hashes explicitly." }
    if ($item.package) {
        $resolved = @($assets.libraries.Keys | Where-Object { $_.Split('/')[0] -ieq $item.package })
        if ($resolved.Count -ne 1 -or $resolved[0].Split('/')[1] -cne $item.version) { throw "Notice version does not match resolved package: $($item.package)" }
        $packages.Add([ordered]@{name=$item.package;version=$item.version;document=$item.id})
        if ($item.package -eq 'ScreenRecorderLib') {
            $lib = $assets.libraries[$resolved[0]]
            $packageRoot = @($assets.packageFolders.Keys | ForEach-Object { Join-Path $_ $lib.path } | Where-Object { Test-Path $_ })[0]
            if ((Hash (Canonical-Bytes (Join-Path $packageRoot 'LICENSE'))) -cne $item.sha256) { throw 'Recorder license differs from the resolved NuGet package.' }
        }
    }
    Add-Document $item.id $item.name $item.version $item.license $item.source $bytes
}
foreach ($line in Get-Content -LiteralPath $RuntimePacksFile) {
    if (-not $line.Trim()) { continue }
    $fields = $line.Split('|')
    if ($fields.Count -ne 3) { throw 'Invalid resolved runtime pack metadata.' }
    $name, $version, $path = $fields
    if ($name -notin @('Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64')) { continue }
    if ($version -notin $catalog.validatedDesktopRuntimeVersions) { throw "Review upstream runtime notices for $name/$version before changing the validated runtime versions." }
    $files = @(Get-ChildItem -LiteralPath $path -File | Where-Object { $_.Name -imatch '^(LICENSE(?:\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' })
    if (-not @($files | Where-Object { $_.Name -imatch '^LICENSE' }).Count) { throw 'Resolved runtime license missing.' }
    if ($name -eq 'Microsoft.NETCore.App.Runtime.win-x64' -and -not @($files | Where-Object { $_.Name -imatch '^THIRD-PARTY' }).Count) { throw 'Runtime transitive notices missing.' }
    $runtimeDocumentIds = [Collections.Generic.List[string]]::new()
    foreach ($file in $files) {
        $id = ($name + '-' + $file.Name) -replace '[^A-Za-z0-9_-]', '_'
        Add-Document $id "$name / $($file.Name)" $version 'see upstream license and notices' "https://www.nuget.org/packages/$name/$version" (Canonical-Bytes $file.FullName)
        $runtimeDocumentIds.Add($id)
    }
    $packages.Add([ordered]@{name=$name;version=$version;documents=@($runtimeDocumentIds)})
}
$manifest = [ordered]@{schemaVersion=1;normalization=$catalog.normalization;packages=@($packages);documents=@($documents);nativeProvenance='OCR native source commits/backports are not attested. Codec versions from verified x64 exports/strings; liblzma version unavailable. Supplemental upstream COPYING is not a binary version assertion.'}
New-Item -Path $OutputDirectory -ItemType Directory -Force | Out-Null
$memory = [IO.MemoryStream]::new()
$zip = [IO.Compression.ZipArchive]::new($memory, [IO.Compression.ZipArchiveMode]::Create, $true)
try {
    foreach ($name in $payloads.Keys) {
        $entry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(2000,1,1,0,0,0,[TimeSpan]::Zero)
        $stream = $entry.Open()
        try { $stream.Write($payloads[$name]) } finally { $stream.Dispose() }
    }
} finally { $zip.Dispose() }
function Write-Changed([string]$Path, [byte[]]$Bytes) {
    if (-not (Test-Path -LiteralPath $Path) -or (Hash ([IO.File]::ReadAllBytes($Path))) -cne (Hash $Bytes)) { [IO.File]::WriteAllBytes($Path, $Bytes) }
}
Write-Changed (Join-Path $OutputDirectory 'notices.zip') $memory.ToArray()
$memory.Dispose()
Write-Changed (Join-Path $OutputDirectory 'catalog.json') ([Text.Encoding]::UTF8.GetBytes(($manifest | ConvertTo-Json -Depth 8)))
Write-Output "Third-party notices: $($documents.Count) verified documents, $($packages.Count) resolved package/runtime origins; no network."
