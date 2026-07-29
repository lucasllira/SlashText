$ErrorActionPreference = 'Stop'

$xamlPath = 'src/SlashText/MainWindow.xaml'
$codePath = 'src/SlashText/MainWindow.xaml.cs'
$editorPath = 'src/SlashText/Views/CaptureEditorWindow.cs'

$xaml = Get-Content $xamlPath -Raw
$code = Get-Content $codePath -Raw
$editor = Get-Content $editorPath -Raw

[xml]$null = $xaml

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

Write-Host "UI integrity smoke: OK ($($handlers.Count) handlers)"
