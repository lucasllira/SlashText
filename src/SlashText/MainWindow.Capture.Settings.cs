using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SlashText.Models;
using SlashText.Services;
using Forms = System.Windows.Forms;

namespace SlashText;

public partial class MainWindow
{
    private static string SelectedTag(ComboBox box, string fallback) =>
        box.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;

    private void InitializeRecordingPresetControls()
    {
        AddPresetItems(RecordingQualityBox, RecordingPresetCatalog.Mp4Quality,
            item => item.Name, item => item.Value);
        AddPresetItems(GifFpsBox, RecordingPresetCatalog.GifFps,
            item => $"{item.Value} FPS — {item.Name}", item => item.Value.ToString());
        AddPresetItems(GifQualityBox, RecordingPresetCatalog.GifQuality,
            item => item.Name, item => item.Value.ToString());
        PopulateCompactRecordingControls();
    }

    private void PopulateCompactRecordingControls()
    {
        if (CaptureCompactFpsBox is null || CaptureCompactQualityBox is null)
        {
            return;
        }
        _updatingCaptureRecordingCompact = true;
        try
        {
            CaptureCompactFpsBox.Items.Clear();
            CaptureCompactQualityBox.Items.Clear();
            if (_captureMediaMode == CaptureMediaMode.Gif)
            {
                CaptureCompactHintText.Text = UiText.Get("Paleta adaptativa · prévia antes de salvar");
                CaptureCompactTargetBox.Items.Clear();
                CaptureCompactTargetBox.Items.Add(new ComboBoxItem { Content = UiText.Get("Região"), Tag = "Region" });
                CaptureCompactTargetBox.SelectedIndex = 0;
                CaptureCompactTargetBox.IsEnabled = false;
                AddPresetItems(CaptureCompactFpsBox, RecordingPresetCatalog.GifFps,
                    item => $"{item.Value}", item => item.Value.ToString());
                AddPresetItems(CaptureCompactQualityBox, RecordingPresetCatalog.GifQuality,
                    item => item.Name, item => item.Value.ToString());
                SelectComboByTag(CaptureCompactFpsBox, _settings.Capture.Recording.GifFps.ToString());
                SelectComboByTag(CaptureCompactQualityBox, _settings.Capture.Recording.GifQuality.ToString());
            }
            else
            {
                CaptureCompactHintText.Text = UiText.Get("H.264 local · pausa e retomada");
                CaptureCompactTargetBox.Items.Clear();
                CaptureCompactTargetBox.Items.Add(new ComboBoxItem { Content = UiText.Get("Monitor ativo"), Tag = "Monitor" });
                CaptureCompactTargetBox.Items.Add(new ComboBoxItem { Content = UiText.Get("Região"), Tag = "Region" });
                CaptureCompactTargetBox.Items.Add(new ComboBoxItem { Content = UiText.Get("Janela"), Tag = "Window" });
                CaptureCompactTargetBox.IsEnabled = true;
                foreach (var fps in new[] { 24, 30, 60 })
                {
                    CaptureCompactFpsBox.Items.Add(new ComboBoxItem { Content = fps.ToString(), Tag = fps.ToString() });
                }
                AddPresetItems(CaptureCompactQualityBox, RecordingPresetCatalog.Mp4Quality,
                    item => item.Name, item => item.Value);
                SelectComboByTag(CaptureCompactTargetBox, SelectedTag(RecordingTargetBox, "Monitor"));
                SelectComboByTag(CaptureCompactFpsBox, SelectedTag(RecordingFpsBox, _settings.Capture.Recording.VideoFps.ToString()));
                SelectComboByTag(CaptureCompactQualityBox, SelectedTag(RecordingQualityBox, _settings.Capture.Recording.VideoQuality));
            }
            CaptureCompactCursorCheckBox.IsChecked = RecordingCursorCheckBox.IsChecked == true;
        }
        finally
        {
            _updatingCaptureRecordingCompact = false;
        }
    }

    private void CaptureCompactRecording_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingCaptureRecordingCompact || RecordingCursorCheckBox is null)
        {
            return;
        }
        if (_captureMediaMode == CaptureMediaMode.Video && ReferenceEquals(sender, CaptureCompactTargetBox))
        {
            _captureLauncherMode = SelectedTag(CaptureCompactTargetBox, "Region") switch
            {
                "Monitor" => CaptureLauncherMode.ActiveMonitor,
                "Window" => CaptureLauncherMode.Window,
                _ => CaptureLauncherMode.Region
            };
            SyncVideoTargetControls();
            UpdateCaptureSelectionSummary();
        }
        RecordingCursorCheckBox.IsChecked = CaptureCompactCursorCheckBox.IsChecked == true;
        if (_captureMediaMode == CaptureMediaMode.Gif)
        {
            SelectComboByTag(GifFpsBox, SelectedTag(CaptureCompactFpsBox, "10"));
            SelectComboByTag(GifQualityBox, SelectedTag(CaptureCompactQualityBox, "128"));
        }
        else
        {
            SelectComboByTag(RecordingTargetBox, SelectedTag(CaptureCompactTargetBox, "Monitor"));
            SelectComboByTag(RecordingFpsBox, SelectedTag(CaptureCompactFpsBox, "30"));
            SelectComboByTag(RecordingQualityBox, SelectedTag(CaptureCompactQualityBox, "Alta"));
        }
    }

    private static void AddPresetItems<T>(
        ComboBox box,
        IReadOnlyList<RecordingPreset<T>> presets,
        Func<RecordingPreset<T>, string> content,
        Func<RecordingPreset<T>, string> tag)
    {
        box.Items.Clear();
        foreach (var preset in presets)
        {
            box.Items.Add(new ComboBoxItem
            {
                Content = content(preset),
                Tag = tag(preset),
                ToolTip = preset.Description
            });
        }
    }

    private void RecordingPreset_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePresetDescription(RecordingQualityBox, RecordingQualityDescriptionText);
        UpdatePresetDescription(GifFpsBox, GifFpsDescriptionText);
        UpdatePresetDescription(GifQualityBox, GifQualityDescriptionText);
    }

    private static void UpdatePresetDescription(ComboBox box, TextBlock description)
    {
        var text = box.SelectedItem is ComboBoxItem { ToolTip: string value }
            ? value
            : string.Empty;
        description.Text = text;
        box.ToolTip = text;
    }

    private static void SelectComboByTag(ComboBox box, string value)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string tag &&
                tag.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = 0;
    }

    private void LoadCaptureSettings()
    {
        var capture = _settings.Capture ??= new CaptureSettings();
        capture.Recording ??= new RecordingSettings();
        RecordingPresetCatalog.Normalize(capture.Recording);
        CaptureMonitorShortcutBox.Text = capture.ActiveMonitorShortcut;
        CaptureRegionShortcutBox.Text = capture.RegionShortcut;
        CaptureWindowShortcutBox.Text = capture.WindowShortcut;
        CaptureScrollingShortcutBox.Text = capture.ScrollingShortcut;
        CaptureMonitorShortcutText.Text = DisplayShortcut(capture.ActiveMonitorShortcut);
        CaptureRegionShortcutText.Text = DisplayShortcut(capture.RegionShortcut);
        CaptureWindowShortcutText.Text = DisplayShortcut(capture.WindowShortcut);
        CaptureScrollingShortcutText.Text = DisplayShortcut(capture.ScrollingShortcut);
        CaptureDirectoryBox.Text = capture.OutputDirectoryTemplate;
        CaptureFileNameBox.Text = capture.FileNameTemplate;
        SelectComboByTag(CaptureFormatBox, capture.ImageFormat);
        CaptureQualityBox.Text = capture.JpegQuality.ToString();
        CaptureClipboardCheckBox.IsChecked = capture.CopyToClipboard;
        CaptureAutoSaveCheckBox.IsChecked = capture.SaveAutomatically;
        CaptureCursorCheckBox.IsChecked = capture.IncludeCursor;
        CaptureHideSlashDeskCheckBox.IsChecked = capture.HideSlashDeskDuringCapture;
        SelectComboByTag(
            CaptureEditorCheckBox,
            capture.OpenEditorForMonitorAndWindow ? "Editor" : "Direct");
        SelectComboByTag(CaptureDelayBox, capture.DelaySeconds.ToString());
        SelectComboByTag(CaptureRetentionBox, capture.HistoryRetentionDays.ToString());
        SelectComboByTag(RecordingTargetBox, _captureMediaMode == CaptureMediaMode.Video ? SelectedVideoTargetKind.ToString() : "Monitor");
        SelectComboByTag(RecordingFpsBox, capture.Recording.VideoFps.ToString());
        SelectComboByTag(RecordingQualityBox, capture.Recording.VideoQuality);
        RecordingCursorCheckBox.IsChecked = capture.Recording.IncludeCursor;
        SelectComboByTag(GifFpsBox, capture.Recording.GifFps.ToString());
        SelectComboByTag(GifQualityBox, capture.Recording.GifQuality.ToString());
        RecordingPreset_OnChanged(this, null!);
        SelectComboByTag(CaptureHistoryFilterBox, "all");
        CaptureQualityBox.IsEnabled =
            capture.ImageFormat.Equals("JPEG", StringComparison.OrdinalIgnoreCase);
        CapturePostModeText.Text = capture.OpenEditorForMonitorAndWindow
            ? UiText.Get("Editor: revisar antes de concluir")
            : UiText.Get("Direta: copiar e salvar");
        PopulateCompactRecordingControls();
    }

    private static string DisplayShortcut(string value) =>
        value.Replace("+", " + ", StringComparison.Ordinal);

    private void BrowseCaptureDestination_OnClick(object sender, RoutedEventArgs e)
    {
        var expandedTemplate =
            Environment.ExpandEnvironmentVariables(CaptureDirectoryBox.Text.Trim());
        var currentDirectory = CaptureService.ResolveDirectoryTemplate(
            expandedTemplate,
            DateTimeOffset.Now);
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = UiText.Get("Selecione a pasta onde as capturas serão salvas."),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(currentDirectory)
                ? currentDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            CaptureDirectoryBox.Text = dialog.SelectedPath;
        }
    }

    private async void SaveCaptureSettings_OnClick(object sender, RoutedEventArgs e)
    {
        using var lease = TryReserveCaptureCommand();
        if (lease is null) return;
        if (!await PrepareCapturePreferencesAsync(UiText.Get("Configuração de captura"))) return;
        ConfigureCaptureShortcuts();
        ShowCapturePreferencesSavedStatus();
    }

    private bool TryReadCaptureSettings(out string error)
    {
        error = string.Empty;
        var shortcuts = new[]
        {
            CaptureMonitorShortcutBox.Text.Trim(),
            CaptureRegionShortcutBox.Text.Trim(),
            CaptureWindowShortcutBox.Text.Trim(),
            CaptureScrollingShortcutBox.Text.Trim()
        };
        if (shortcuts.Any(item => !GlobalCaptureShortcutService.IsValid(item)))
        {
            error = UiText.Get("Clique no campo e pressione uma tecla, roda ou botão do mouse. ") +
                    UiText.Get("A roda sempre exige Ctrl, Alt, Shift ou Win; cliques esquerdo e direito não são aceitos.");
            return false;
        }
        if (shortcuts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != shortcuts.Length)
        {
            error = UiText.Get("Cada ação precisa ter um atalho diferente.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(CaptureDirectoryBox.Text) ||
            string.IsNullOrWhiteSpace(CaptureFileNameBox.Text))
        {
            error = UiText.Get("Informe a pasta e o modelo de nome do arquivo.");
            return false;
        }
        var openEditor = SelectedTag(CaptureEditorCheckBox, "Direct")
            .Equals("Editor", StringComparison.OrdinalIgnoreCase);
        var copyToClipboard = CaptureClipboardCheckBox.IsChecked == true;
        var saveAutomatically = CaptureAutoSaveCheckBox.IsChecked == true;
        if (!openEditor && !copyToClipboard && !saveAutomatically)
        {
            error = UiText.Get("No modo Captura direta, ative Copiar após capturar, ") +
                    UiText.Get("Salvar automaticamente ou ambas as opções.");
            return false;
        }

        var gifFps = ParseSelectedInt(GifFpsBox, 10);
        var gifQuality = ParseSelectedInt(GifQualityBox, 128);
        if (!RecordingPresetCatalog.GifFps.Any(item => item.Value == gifFps) ||
            !RecordingPresetCatalog.GifQuality.Any(item => item.Value == gifQuality))
        {
            error = UiText.Get("Selecione um preset disponível de FPS e qualidade do GIF.");
            return false;
        }

        var legacyGifDuration = _settings.Capture.Recording.GifDurationSeconds;
        var legacyGifWidth = _settings.Capture.Recording.GifWidth;

        var quality = int.TryParse(CaptureQualityBox.Text, out var parsedQuality)
            ? Math.Clamp(parsedQuality, 1, 100)
            : 90;
        _settings.Capture = new CaptureSettings
        {
            ActiveMonitorShortcut = shortcuts[0],
            RegionShortcut = shortcuts[1],
            WindowShortcut = shortcuts[2],
            ScrollingShortcut = shortcuts[3],
            OutputDirectoryTemplate = CaptureDirectoryBox.Text.Trim(),
            FileNameTemplate = CaptureFileNameBox.Text.Trim(),
            ImageFormat = SelectedTag(CaptureFormatBox, "PNG"),
            JpegQuality = quality,
            CopyToClipboard = copyToClipboard,
            SaveAutomatically = saveAutomatically,
            HideSlashDeskDuringCapture = CaptureHideSlashDeskCheckBox.IsChecked == true,
            IncludeCursor = CaptureCursorCheckBox.IsChecked == true,
            OpenEditorForMonitorAndWindow = openEditor,
            DelaySeconds = ParseSelectedInt(CaptureDelayBox, 0),
            HistoryRetentionDays = ParseSelectedInt(CaptureRetentionBox, 90),
            Ocr = _settings.Capture.Ocr ?? new(),
            Recording = new RecordingSettings
            {
                Audio = (_settings.Capture.Recording.Audio ?? new()).Copy(),
                VideoFps = ParseSelectedInt(RecordingFpsBox, 30),
                VideoQuality = SelectedTag(RecordingQualityBox, "Alta"),
                IncludeCursor = RecordingCursorCheckBox.IsChecked == true,
                GifFps = gifFps,
                GifDurationSeconds = legacyGifDuration,
                GifWidth = legacyGifWidth,
                GifQuality = gifQuality
            }
        };
        CaptureQualityBox.Text = quality.ToString();
        return true;
    }

    private void ConfigureCaptureShortcuts()
    {
        if (CapturePreparationForEvidence is { } evidence)
        {
            evidence("shortcuts");
            return;
        }
        var capture = _settings.Capture;
        var errors = _captureShortcuts.Configure(
            this,
            capture.ActiveMonitorShortcut,
            capture.RegionShortcut,
            capture.WindowShortcut,
            capture.ScrollingShortcut);
        CaptureShortcutStatusText.Text = errors.Count == 0
            ? UiText.Get("● Atalhos ativos")
            : string.Join(Environment.NewLine, errors);
        CaptureShortcutStatusText.Foreground = errors.Count == 0
            ? (Brush)FindResource("SuccessBrush")
            : (Brush)FindResource("MutedBrush");
    }
}
