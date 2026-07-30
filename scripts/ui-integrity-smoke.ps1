$ErrorActionPreference = 'Stop'

$xamlPath = 'src/SlashText/MainWindow.xaml'
$codePath = 'src/SlashText/MainWindow.xaml.cs'
$editorPath = 'src/SlashText/Views/CaptureEditorWindow.cs'
$regionPath = 'src/SlashText/Views/RegionCaptureWindow.cs'
$variablePath = 'src/SlashText/Views/VariableInputWindow.cs'
$keyboardPath = 'src/SlashText/Services/KeyboardHookService.cs'
$resourcesPath = 'src/SlashText/App.xaml'
$themePath = 'src/SlashText/Services/ThemeService.cs'
$importPath = 'src/SlashText/Services/SnippetImportService.cs'
$backupPath = 'src/SlashText/Services/BackupService.cs'

$xaml = Get-Content $xamlPath -Raw
$code = Get-Content $codePath -Raw
$editor = Get-Content $editorPath -Raw
$region = Get-Content $regionPath -Raw
$variable = Get-Content $variablePath -Raw
$keyboard = Get-Content $keyboardPath -Raw
$resources = Get-Content $resourcesPath -Raw
$theme = Get-Content $themePath -Raw
$import = Get-Content $importPath -Raw
$backup = Get-Content $backupPath -Raw

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

if (-not $code.Contains('SelectAndEditRegion(null)') -or
    -not $code.Contains('ProcessEditedRegionAsync(')) {
    throw 'O fluxo de região não edita e processa no mesmo overlay.'
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

foreach ($shellElement in @(
    '<RowDefinition Height="64"/>',
    '<RowDefinition Height="49"/>',
    'BorderThickness="0,1,0,1"',
    'Produtividade local para Windows',
    'SelectionIndicator'
)) {
    $source = if ($shellElement -eq 'SelectionIndicator') {
        $resources
    } else {
        $xaml
    }
    if (-not $source.Contains($shellElement)) {
        throw "Shell visual novo ausente: $shellElement"
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
    'CaptureVirtualDesktopBitmap()',
    'UpdateShade(',
    'PositionHandles()',
    'PositionToolbar()',
    'Selecionar novamente',
    'EditedBitmap',
    'AddInlineTextEditor(',
    'Undo()',
    'Redo()'
)) {
    if (-not $region.Contains($captureElement)) {
        throw "Seleção de região sem o elemento Snipping Tool: $captureElement"
    }
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
    if (-not $region.Contains("CaptureAnnotationKind.$tool")) {
        throw "Ferramenta ausente durante a seleção: $tool"
    }
}

if ($xaml.Contains('Á  Acento Rápido') -or
    -not $xaml.Contains('Text="Acento Rápido"')) {
    throw 'Nome da aba Acento Rápido incorreto.'
}

foreach ($label in @(
    'Seta',
    'Marca-texto',
    'Retângulo',
    'Elipse',
    'Lápis',
    'Texto',
    'Número',
    'Capturar'
)) {
    if (-not $region.Contains("`"$label`"")) {
        throw "Barra de captura sem rótulo legível: $label"
    }
}

foreach ($themeElement in @(
    'ThemeService.IsDark',
    '_isDark ? "#F2121922" : "#F8FFFFFF"',
    '_isDark ? "#FA121922" : "#FCF8FAFC"',
    '_isDark ? "#F5F8FA" : "#25313D"',
    '_isDark ? "#1E2834" : "#FFFFFF"'
)) {
    if (-not $region.Contains($themeElement)) {
        throw "Overlay de região sem variante clara/escura: $themeElement"
    }
}

if ($region.Contains(
        'Background = new SolidColorBrush(Color.FromArgb(250, 18, 25, 34))')) {
    throw 'A barra de captura ainda força o tema escuro.'
}

if (-not $region.Contains('_annotationLayer.MouseLeftButtonDown += OnAnnotationMouseDown') -or
    -not $region.Contains('BeginAnnotation(canvasPoint)')) {
    throw 'A camada selecionada não recebe a edição inline diretamente.'
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
    'AverageCharactersText',
    'ImportSourceBox',
    'BackupSummaryText'
)) {
    if (-not $xaml.Contains("x:Name=`"$control`"")) {
        throw "Controle do novo layout ausente: $control"
    }
}

foreach ($source in @(
    'SnippetImportSource.SlashDesk',
    'SnippetImportSource.TextBlaze',
    'SnippetImportSource.Espanso',
    'ConvertTextBlazeVariables',
    'EspansoReplacePattern'
)) {
    if (-not $import.Contains($source)) {
        throw "Importador ausente ou incompleto: $source"
    }
}

foreach ($backupFeature in @(
    'CreateManualSnapshot()',
    'RestoreSnapshot(',
    'CreateSnapshot(',
    'capture-history.json'
)) {
    if (-not $backup.Contains($backupFeature)) {
        throw "Backup ausente ou incompleto: $backupFeature"
    }
}

foreach ($handler in @(
    'ImportSnippets_OnClick',
    'CreateBackup_OnClick',
    'RestoreBackup_OnClick',
    'OpenBackupFolder_OnClick'
)) {
    if (-not $code.Contains("$handler(")) {
        throw "Fluxo de dados ausente: $handler"
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
