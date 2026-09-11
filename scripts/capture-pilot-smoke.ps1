$ErrorActionPreference = 'Stop'

$xaml = Get-Content 'src/SlashText/MainWindow.xaml' -Raw
$code = Get-Content 'src/SlashText/MainWindow.xaml.cs' -Raw
$app = Get-Content 'src/SlashText/App.xaml' -Raw
$styles = Get-Content 'src/SlashText/Styles/VisualLab/CapturePilot.xaml' -Raw
$ruleDialog = Get-Content 'src/SlashText/Views/CaptureRuleDialog.xaml' -Raw
$ruleCode = Get-Content 'src/SlashText/Views/CaptureRuleDialog.xaml.cs' -Raw
$shortcutDialog = Get-Content 'src/SlashText/Views/CaptureShortcutDialog.xaml' -Raw
$shortcutCode = Get-Content 'src/SlashText/Views/CaptureShortcutDialog.xaml.cs' -Raw

[xml]$null = $xaml
[xml]$null = $app
[xml]$null = $styles
[xml]$null = $ruleDialog
[xml]$null = $shortcutDialog

$orderedTabs = @(
    'x:Name="ShortcutsTabButton"',
    'x:Name="CaptureTabButton"',
    'x:Name="QuickAccentTabButton"',
    'x:Name="StatisticsTabButton"',
    'x:Name="SettingsTabButton"',
    'x:Name="AboutTabButton"'
)
$lastIndex = -1
foreach ($tab in $orderedTabs) {
    $index = $xaml.IndexOf($tab, [StringComparison]::Ordinal)
    if ($index -le $lastIndex) {
        throw "Ordem de navegação do contrato 3.3.0 divergente em: $tab"
    }
    $lastIndex = $index
}

foreach ($surface in @(
    'Text="Capture a ideia inteira."',
    'x:Name="CaptureNewButton"',
    'x:Name="CaptureImageMediaButton"',
    'x:Name="CaptureVideoMediaButton"',
    'x:Name="CaptureGifMediaButton"',
    'x:Name="CaptureMonitorModeButton"',
    'x:Name="CaptureRegionModeButton"',
    'x:Name="CaptureWindowModeButton"',
    'x:Name="CaptureScrollingModeButton"',
    'x:Name="CaptureGifModeButton"',
    'x:Name="CaptureRecordingCard"',
    'x:Name="CaptureVideoConfigurationPanel"',
    'x:Name="CaptureGifConfigurationPanel"',
    'x:Name="CaptureRuleCard"',
    'x:Name="CaptureHistoryPanel"',
    'x:Name="CaptureWorkbenchZoomBox"',
    'x:Name="CapturePostModeText"',
    'Text="Personalizar atalho"',
    'Click="OpenCaptureImage_OnClick"',
    'Click="CopyCapturePreview_OnClick"',
    'Click="SaveCapturePreview_OnClick"',
    'Click="CompleteCapturePreview_OnClick"'
)) {
    if (-not $xaml.Contains($surface)) {
        throw "Superfície funcional do piloto ausente: $surface"
    }
}

foreach ($behavior in @(
    'CaptureLauncherMode_OnClick',
    'CaptureMediaMode_OnClick',
    'StartSelectedCapture_OnClick',
    'CaptureActiveMonitor_OnClick(sender, e)',
    'CaptureRegion_OnClick(sender, e)',
    'CaptureWindow_OnClick(sender, e)',
    'CaptureScrolling_OnClick(sender, e)',
    'StartMp4Recording_OnClick(sender, e)',
    'StartGifRecording_OnClick(sender, e)',
    'TryReadCaptureSettings(out var error)',
    'new CaptureRuleDialog(_settings.Capture)',
    'new CaptureShortcutDialog(_settings.Capture)',
    'LoadCaptureWorkbenchImage(',
    'BuildCaptureHistoryCard(item)'
)) {
    if (-not $code.Contains($behavior)) {
        throw "Comando real do piloto ausente: $behavior"
    }
}

foreach ($style in @(
    'Lab.Pilot.ShellHeader',
    'Lab.Pilot.NavigationButton',
    'Lab.Pilot.CommandBar',
    'Lab.Pilot.Segment',
    'Lab.Pilot.ModeCard',
    'Lab.Pilot.ToolButton',
    'Lab.Pilot.Card'
)) {
    if (-not $styles.Contains("x:Key=`"$style`"")) {
        throw "Estilo opt-in do piloto ausente: $style"
    }
}

if (-not $app.Contains('Source="Styles/VisualLab/CapturePilot.xaml"')) {
    throw 'O aplicativo não carrega o estilo do piloto de Captura.'
}

foreach ($modalContract in @(
    @{ Content = $ruleDialog; Token = 'x:Class="SlashText.Views.CaptureRuleDialog"' },
    @{ Content = $ruleDialog; Token = 'Text="Regra de captura"' },
    @{ Content = $ruleDialog; Token = 'Text="Salvar regra"' },
    @{ Content = $ruleCode; Token = 'DialogResult = true' },
    @{ Content = $shortcutDialog; Token = 'x:Class="SlashText.Views.CaptureShortcutDialog"' },
    @{ Content = $shortcutDialog; Token = 'x:Name="ScrollingBox"' },
    @{ Content = $shortcutCode; Token = 'GlobalCaptureShortcutService.IsValid' }
)) {
    if (-not $modalContract.Content.Contains($modalContract.Token)) {
        throw "Contrato modal ausente: $($modalContract.Token)"
    }
}

if ($xaml.Contains('CaptureRuleCard.BringIntoView()') -or
    $code.Contains('CaptureRuleCard.BringIntoView()')) {
    throw 'Regra de captura ainda navega para o formulário antigo.'
}

foreach ($shortcutText in @(
    'x:Name="CaptureMonitorShortcutText"',
    'x:Name="CaptureRegionShortcutText"',
    'x:Name="CaptureWindowShortcutText"',
    'x:Name="CaptureScrollingShortcutText"'
)) {
    if (-not $xaml.Contains($shortcutText)) {
        throw "Atalho compacto ausente: $shortcutText"
    }
}

if ([regex]::IsMatch($xaml, '#[0-9A-Fa-f]{6,8}') -or
    [regex]::IsMatch($styles, '#[0-9A-Fa-f]{6,8}')) {
    throw 'O piloto deve usar apenas tokens semânticos, sem cores fixas.'
}

'PASS: shell, ordem das abas, superfície de Captura, comandos reais e tokens do piloto 3.3.0.'
