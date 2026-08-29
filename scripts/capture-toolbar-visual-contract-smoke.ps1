$ErrorActionPreference = 'Stop'

$required = @(
    'docs/capture-toolbar-visual-contract.md',
    'src/SlashText/Styles/CaptureToolbarVisualContract.xaml',
    'src/SlashText/Views/CaptureToolbarPreviewWindow.xaml',
    'src/SlashText/Views/CaptureToolbarPreviewWindow.xaml.cs'
)
foreach ($path in $required) {
    if (-not (Test-Path $path)) {
        throw "Visual Contract ausente: $path"
    }
}

[xml](Get-Content 'src/SlashText/Styles/CaptureToolbarVisualContract.xaml' -Raw) | Out-Null
[xml](Get-Content 'src/SlashText/Views/CaptureToolbarPreviewWindow.xaml' -Raw) | Out-Null

$resources = Get-Content 'src/SlashText/Styles/CaptureToolbarVisualContract.xaml' -Raw
foreach ($token in @(
    'Preview.Canvas',
    'Preview.Toolbar',
    'Preview.Accent',
    'PreviewIcon.Number',
    'PreviewIcon.Emoji'
)) {
    if (-not $resources.Contains($token)) {
        throw "Token obrigatório ausente: $token"
    }
}

$preview = Get-Content 'src/SlashText/Views/CaptureToolbarPreviewWindow.xaml' -Raw
foreach ($state in @('DefaultState', 'CaptureState', 'ShapesState', 'EmojiState')) {
    if (-not $preview.Contains($state)) {
        throw "Estado obrigatório ausente no preview: $state"
    }
}
foreach ($visualFix in @(
    'FontFamily="Segoe UI Emoji"',
    'Adaptativo · referência 1440 × 900',
    'Foreground="#FFFF375F"'
)) {
    if (-not $preview.Contains($visualFix)) {
        throw "Correção visual obrigatória ausente: $visualFix"
    }
}

$app = Get-Content 'src/SlashText/App.xaml.cs' -Raw
if (-not $app.Contains('--capture-toolbar-preview') -or
    -not $app.Contains('CaptureToolbarPreviewWindow')) {
    throw 'O argumento isolado do preview não está registrado.'
}

'Capture toolbar Visual Contract smoke: OK'
