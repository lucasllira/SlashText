$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$docs = Join-Path $root 'docs/design/3.3.0'
$manifest = Get-Content (Join-Path $docs 'contract-manifest.json') -Raw | ConvertFrom-Json
foreach ($file in $manifest.files.PSObject.Properties) {
    $actual = (Get-FileHash (Join-Path $docs "contract/$($file.Name)") -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $file.Value) { throw "Frozen design contract changed: $($file.Name)" }
}
$styles = Join-Path $root 'src/SlashText/Styles/VisualLab'
foreach ($file in Get-ChildItem $styles -Filter '*.xaml') {
    [xml]$xml = Get-Content $file.FullName -Raw
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace('p', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
    $ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
    foreach ($style in $xml.SelectNodes('/p:ResourceDictionary/p:Style', $ns)) {
        if (-not $style.GetAttribute('Key', 'http://schemas.microsoft.com/winfx/2006/xaml').StartsWith('Lab.')) {
            throw "Unscoped style in $($file.Name)"
        }
    }
}
$tokens = Get-Content (Join-Path $docs 'contract/tokens.json') -Raw | ConvertFrom-Json
foreach ($theme in @('light', 'black')) {
    [xml]$palette = Get-Content (Join-Path $styles "$theme.xaml") -Raw
    foreach ($token in $tokens.themes.$theme.PSObject.Properties) {
        $brush = $palette.ResourceDictionary.SolidColorBrush | Where-Object { $_.Key -eq "Lab.$($token.Name)" }
        $color = $token.Value
        if ($color.Length -eq 4) { $color = '#' + (($color.Substring(1).ToCharArray() | ForEach-Object { "$_$_" }) -join '') }
        if ($brush.Color -ne $color) { throw "Palette differs from frozen contract: $theme $($token.Name)" }
    }
}
'PASS: frozen contract hashes, scoped styles, palette parity.'
