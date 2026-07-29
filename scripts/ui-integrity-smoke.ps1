$ErrorActionPreference = 'Stop'

$xamlPath = 'src/SlashText/MainWindow.xaml'
$codePath = 'src/SlashText/MainWindow.xaml.cs'
$editorPath = 'src/SlashText/Views/CaptureEditorWindow.cs'
$resourcesPath = 'src/SlashText/App.xaml'
$themePath = 'src/SlashText/Services/ThemeService.cs'

$xaml = Get-Content $xamlPath -Raw
$code = Get-Content $codePath -Raw
$editor = Get-Content $editorPath -Raw
$resources = Get-Content $resourcesPath -Raw
$theme = Get-Content $themePath -Raw

[xml]$null = $xaml
[xml]$null = $resources

$handlers = [regex]::Matches(
    $xaml,
    '(?:Click|Loaded|Closing|Closed|Activated|SizeChanged|StateChanged|TextChanged|SelectionChanged|Checked|Unchecked|LostFocus)="([A-Za-z_][A-Za-z0-9_]*)"'
) | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

foreach ($handler in $handlers) {
    if (-not $code.Contains("$handler(")) {
        throw "Handler XAML ausente: $handler"
    }
}

$recorderCount = ([regex]::Matches($xaml, '<views:ShortcutRecorderBox')).Count
if ($recorderCount -ne 3) {
    throw "Esperados 3 gravadores de atalho; encontrados: $recorderCount"
}

foreach ($tool in @(
    'Arrow',
    'Highlighter',
    'Rectangle',
    'Ellipse',
    'Pencil',
    'Text',
    'Number'
)) {
    if (-not $editor.Contains("CaptureAnnotationKind.$tool")) {
        throw "Ferramenta ausente no editor: $tool"
    }
}

if (-not $code.Contains('openEditor: action == CaptureShortcutAction.Region')) {
    throw 'O fluxo de região não abre o editor.'
}

foreach ($resource in @(
    'PanelBrush',
    'ChromeBrush',
    'AccentSubtleBrush',
    'NavButton',
    'StatusPill',
    'FeatureTile'
)) {
    if (-not $resources.Contains("`"$resource`"")) {
        throw "Recurso do design system ausente: $resource"
    }
}

foreach ($brush in @(
    'CanvasBrush',
    'SurfaceBrush',
    'PanelBrush',
    'ChromeBrush',
    'InputBrush',
    'InkBrush',
    'MutedBrush',
    'DividerBrush',
    'AccentBrush'
)) {
    if (-not $theme.Contains("Set(`"$brush`"")) {
        throw "Tema não atualiza o recurso: $brush"
    }
}

if (-not $code.Contains('button.Tag = selected ? "Selected" : null')) {
    throw 'A navegação não mantém estado visual selecionado.'
}

if (-not $editor.Contains('UpdateToolSelection()')) {
    throw 'O editor não informa visualmente a ferramenta selecionada.'
}

Write-Host "UI integrity smoke: OK ($($handlers.Count) handlers)"
