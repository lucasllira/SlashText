using System.IO;
using System.Windows;
using System.Windows.Controls;
using SlashText.Models;
using SlashText.Services;
using Forms = System.Windows.Forms;

namespace SlashText.Views;

public partial class CaptureRuleDialog : Window
{
    private readonly RecordingSettings _recording;

    public CaptureSettings Result { get; private set; }

    public CaptureRuleDialog(CaptureSettings settings)
    {
        InitializeComponent();
        _recording = settings.Recording ?? new RecordingSettings();
        Result = settings;
        DirectoryBox.Text = settings.OutputDirectoryTemplate;
        FileNameBox.Text = settings.FileNameTemplate;
        QualityBox.Text = settings.JpegQuality.ToString();
        CopyCheckBox.IsChecked = settings.CopyToClipboard;
        SaveCheckBox.IsChecked = settings.SaveAutomatically;
        CursorCheckBox.IsChecked = settings.IncludeCursor;
        HideCheckBox.IsChecked = settings.HideSlashDeskDuringCapture;
        SelectByTag(AfterCaptureBox, settings.OpenEditorForMonitorAndWindow ? "Editor" : "Direct");
        SelectByTag(FormatBox, settings.ImageFormat);
        SelectByTag(DelayBox, settings.DelaySeconds.ToString());
        SelectByTag(RetentionBox, settings.HistoryRetentionDays.ToString());
        UpdateQualityState();
        SourceInitialized += (_, _) => ThemeService.ApplyToWindow(this);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
            }
        };
    }

    private void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        var expanded = Environment.ExpandEnvironmentVariables(DirectoryBox.Text.Trim());
        var resolved = CaptureService.ResolveDirectoryTemplate(expanded, DateTimeOffset.Now);
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Selecione a pasta onde as capturas serão salvas.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(resolved)
                ? resolved
                : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            DirectoryBox.Text = dialog.SelectedPath;
        }
    }

    private void Format_OnChanged(object sender, SelectionChangedEventArgs e) => UpdateQualityState();

    private void UpdateQualityState()
    {
        if (QualityBox is not null && FormatBox is not null)
        {
            QualityBox.IsEnabled = SelectedTag(FormatBox, "PNG") == "JPEG";
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DirectoryBox.Text) || string.IsNullOrWhiteSpace(FileNameBox.Text))
        {
            MessageBox.Show("Informe a pasta e o modelo de nome do arquivo.", "Regra de captura",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var editor = SelectedTag(AfterCaptureBox, "Direct") == "Editor";
        var copy = CopyCheckBox.IsChecked == true;
        var save = SaveCheckBox.IsChecked == true;
        if (!editor && !copy && !save)
        {
            MessageBox.Show("No modo direto, ative Copiar após capturar, Salvar automaticamente ou ambas.",
                "Regra de captura", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var quality = int.TryParse(QualityBox.Text, out var parsed) ? Math.Clamp(parsed, 1, 100) : 90;
        Result = new CaptureSettings
        {
            ActiveMonitorShortcut = Result.ActiveMonitorShortcut,
            RegionShortcut = Result.RegionShortcut,
            WindowShortcut = Result.WindowShortcut,
            ScrollingShortcut = Result.ScrollingShortcut,
            OutputDirectoryTemplate = DirectoryBox.Text.Trim(),
            FileNameTemplate = FileNameBox.Text.Trim(),
            ImageFormat = SelectedTag(FormatBox, "PNG"),
            JpegQuality = quality,
            CopyToClipboard = copy,
            SaveAutomatically = save,
            HideSlashDeskDuringCapture = HideCheckBox.IsChecked == true,
            IncludeCursor = CursorCheckBox.IsChecked == true,
            OpenEditorForMonitorAndWindow = editor,
            DelaySeconds = SelectedInt(DelayBox, 0),
            HistoryRetentionDays = SelectedInt(RetentionBox, 90),
            Recording = _recording
        };
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string SelectedTag(ComboBox box, string fallback) =>
        box.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;

    private static int SelectedInt(ComboBox box, int fallback) =>
        int.TryParse(SelectedTag(box, fallback.ToString()), out var value) ? value : fallback;

    private static void SelectByTag(ComboBox box, string value)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string tag && tag.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = 0;
    }
}
