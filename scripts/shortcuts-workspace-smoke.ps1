$ErrorActionPreference = 'Stop'
$xaml = Get-Content 'src/SlashText/MainWindow.xaml' -Raw
$styles = Get-Content 'src/SlashText/Styles/VisualLab/Shortcuts.xaml' -Raw
$app = Get-Content 'src/SlashText/App.xaml' -Raw
foreach ($dictionary in Get-ChildItem src/SlashText/Styles -Recurse -Filter '*.xaml') {
    $dictionaryKeys = [regex]::Matches((Get-Content $dictionary.FullName -Raw), 'x:Key="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    $duplicates = $dictionaryKeys | Group-Object | Where-Object Count -gt 1
    if ($duplicates) { throw "Chaves duplicadas em $($dictionary.Name): $($duplicates.Name -join ', ')" }
}
$source = (Get-ChildItem src/SlashText/Styles -Recurse -Filter '*.xaml' |
    ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$source += "`n" + $app
$keys = [regex]::Matches($source, 'x:Key="([^"]+)"') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($match in [regex]::Matches($xaml + $styles, '\{StaticResource ([^{}]+)\}')) {
    $key = $match.Groups[1].Value
    if ($key -notin $keys) { throw "Recurso estático de Atalhos não definido: $key" }
}
if (-not $app.Contains('Source="Styles/VisualLab/Shortcuts.xaml"')) {
    throw 'O dicionário de Atalhos não está carregado.'
}
[xml](Get-Content 'src/SlashText/Styles/VisualLab/Shortcuts.xaml' -Raw) | Out-Null
Write-Host 'Shortcuts resource dictionary smoke: OK'
