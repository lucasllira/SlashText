$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$baselinePath = Join-Path $PSScriptRoot 'ui-inventory-2.9.1.json'
$xamlPath = Join-Path $root 'src/SlashText/MainWindow.xaml'
$codePath = Join-Path $root 'src/SlashText/MainWindow.xaml.cs'
$viewsPath = Join-Path $root 'src/SlashText/Views'

$baseline = Get-Content $baselinePath -Raw | ConvertFrom-Json
$xaml = Get-Content $xamlPath -Raw
$code = Get-Content $codePath -Raw

$currentControls = [regex]::Matches(
    $xaml,
    'x:Name="([A-Za-z_][A-Za-z0-9_]*)"'
) | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

$currentHandlers = [regex]::Matches(
    $xaml,
    '(?:Click|Loaded|Closing|Closed|Activated|SizeChanged|StateChanged|TextChanged|SelectionChanged|ValueChanged|Checked|Unchecked|LostFocus|MouseLeftButtonDown)="([A-Za-z_][A-Za-z0-9_]*)"'
) | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

$missingControls = @($baseline.namedControls | Where-Object { $_ -notin $currentControls })
$missingHandlers = @($baseline.handlers | Where-Object { $_ -notin $currentHandlers })
$missingViews = @($baseline.requiredViews | Where-Object {
    -not (Test-Path (Join-Path $viewsPath $_))
})

foreach ($handler in $currentHandlers) {
    if (-not $code.Contains("$handler(")) {
        throw "Handler XAML sem implementação: $handler"
    }
}

if ($missingControls.Count -gt 0) {
    throw "Controles da linha de base removidos: $($missingControls -join ', ')"
}
if ($missingHandlers.Count -gt 0) {
    throw "Handlers da linha de base removidos: $($missingHandlers -join ', ')"
}
if ($missingViews.Count -gt 0) {
    throw "Janelas/componentes da linha de base removidos: $($missingViews -join ', ')"
}
if ($currentControls.Count -lt [int]$baseline.namedControlCount) {
    throw "Quantidade de controles caiu de $($baseline.namedControlCount) para $($currentControls.Count)."
}
if ($currentHandlers.Count -lt [int]$baseline.handlerCount) {
    throw "Quantidade de handlers caiu de $($baseline.handlerCount) para $($currentHandlers.Count)."
}

$requiredMarkers = @(
    'ShowView(ShortcutsView',
    'ShowView(QuickAccentView',
    'ShowView(CaptureView',
    'ShowView(StatisticsView',
    'ShowView(SettingsView',
    'ShowView(AboutView',
    'StartGifRecording_OnClick',
    'StartMp4Recording_OnClick',
    'ImportSnippets_OnClick',
    'CreateBackup_OnClick',
    'RestoreBackup_OnClick',
    'CheckUpdates_OnClick',
    'OpenHistoryItem_OnClick',
    'CopyHistoryItem_OnClick',
    'EditHistoryItem_OnClick',
    'DeleteHistoryItem_OnClick'
)
foreach ($marker in $requiredMarkers) {
    if (-not $code.Contains($marker)) {
        throw "Marcador funcional removido: $marker"
    }
}

Write-Host "UI inventory comparison: OK ($($currentControls.Count) controls, $($currentHandlers.Count) handlers, $($baseline.requiredViews.Count) auxiliary views)"
