$ErrorActionPreference = 'Stop'

$xamlPath = 'src/SlashText/MainWindow.xaml'
$codePath = 'src/SlashText/MainWindow.xaml.cs'
$editorPath = 'src/SlashText/Views/CaptureEditorWindow.cs'
$regionPath = 'src/SlashText/Views/RegionCaptureWindow.cs'
$variablePath = 'src/SlashText/Views/VariableInputWindow.cs'
$keyboardPath = 'src/SlashText/Services/KeyboardHookService.cs'
$resourcesPath = 'src/SlashText/App.xaml'
$themePath = 'src/SlashText/Services/ThemeService.cs'

$xaml = Get-Content $xamlPath -Raw
$code = Get-Content $codePath -Raw
$editor = Get-Content $editorPath -Raw
$region = Get-Content $regionPath -Raw
$variable = Get-Content $variablePath -Raw
$keyboard = Get-Content $keyboardPath -Raw
$resources = Get-Content $resourcesPath -Raw
$theme = Get-Content $themePath -Raw

[xml]$null = $xaml
[xml]$null = $resources

$handlers = [regex]::Matches(
    $xaml,
    '(?:Click|Loaded|Closing|Closed|Activated|SizeChanged|StateChanged|TextChanged|SelectionChanged|ValueChanged|Checked|Unchecked|LostFocus|MouseLeftButtonDown)="([A-Za-z_][A-Za-z0-9_]*)"'
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

foreach ($captureElement in @(
    'CaptureVirtualDesktop()',
    'UpdateShade(',
    'PositionHandles()',
    'PositionToolbar()',
    'PreferredTool',
    'Selecionar novamente'
)) {
    if (-not $region.Contains($captureElement)) {
        throw "Seleção de região sem o elemento Snipping Tool: $captureElement"
    }
}

if ($variable.Contains('window.SourceInitialized +=') -or
    -not $variable.Contains('new WindowInteropHelper(window).Owner = targetWindow;')) {
    throw 'O proprietário do diálogo de variáveis não é definido antes de ShowDialog.'
}

if (-not $keyboard.Contains('ToUnicodeNoStateChange') -or
    -not $keyboard.Contains('ToUnicodeNoStateChange,')) {
    throw 'A tradução de teclado ainda pode alterar o estado de teclas mortas ABNT.'
}

foreach ($control in @(
    'QuickAccentPreviewChoice0',
    'QuickAccentDelaySlider',
    'CapturePreviewImage',
    'CaptureTotalText',
    'CaptureRegionTotalText',
    'AverageCharactersText'
)) {
    if (-not $xaml.Contains("x:Name=`"$control`"")) {
        throw "Controle do novo layout ausente: $control"
    }
}

$recorder = Get-Content 'src/SlashText/Views/ShortcutRecorderBox.cs' -Raw
if (-not $recorder.Contains('PreviewKeyUp += OnPreviewKeyUp') -or
    -not $recorder.Contains('Key.Snapshot')) {
    throw 'O gravador não trata Print Screen na liberação da tecla.'
}

$quickAccent = Get-Content 'src/SlashText/Services/QuickAccentService.cs' -Raw
if (-not $quickAccent.Contains('_activationDown')) {
    throw 'O Acento Rápido não bloqueia o auto-repeat da tecla de ativação.'
}

Write-Host "UI integrity smoke: OK ($($handlers.Count) handlers)"
