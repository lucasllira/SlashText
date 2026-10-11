using System.Windows;
using System.Windows.Controls;

namespace SlashText;

public partial class MainWindow
{
    internal void ConfigureLanguageEvidence(string view, double width)
    {
        _settings.Language = UiText.Language;
        SyncSettingsControls();
        switch (view)
        {
            case "Shortcuts": ShowView(ShortcutsView, ShortcutsTabButton); UpdateResponsiveLayout(width); break;
            case "Capture": LoadCaptureSettings(); ShowView(CaptureView, CaptureTabButton); break;
            case "Accent": LoadQuickAccentControls(); ShowView(QuickAccentView, QuickAccentTabButton); UpdateQuickAccentLayout(width); break;
            case "Statistics": RefreshStatistics(); ShowView(StatisticsView, StatisticsTabButton); UpdateStatisticsLayout(width); break;
            case "About": ShowView(AboutView, AboutTabButton); UpdateAboutLayout(width); break;
            default: ShowView(SettingsView, SettingsTabButton); UpdateSettingsLayout(width); break;
        }
    }

    private async void LanguageBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _settingsOperationInProgress || _shortcutOperationInProgress || LanguageBox.SelectedItem is not ComboBoxItem { Tag: string language }) return;
        await SaveLanguagePreferenceAsync(language);
    }

    internal async Task<bool> SaveLanguagePreferenceAsync(string language)
    {
        if (_settingsOperationInProgress || _shortcutOperationInProgress) return false;
        var previous = _settings.Language;
        _settingsOperationInProgress = true;
        SettingsCardsGrid.IsEnabled = false;
        try
        {
            _settings.Language = UiText.Normalize(language);
            await _settingsStore.SaveAsync(_settings);
            LanguageRestartText.Text = UiText.Get("Idioma salvo. Reabra o SlashDesk para aplicar.");
            SettingsOperationText.Text = UiText.Get("Preferência salva.");
            return true;
        }
        catch (Exception exception)
        {
            _settings.Language = previous;
            SyncSettingsControls();
            LanguageRestartText.Text = UiText.Get("Não foi possível salvar o idioma. A preferência anterior foi mantida.");
            SettingsOperationText.Text = UiText.Get("Não foi possível salvar a preferência.");
            ShowSettingsDialog(exception.Message, UiText.Get("Não foi possível salvar a configuração"), image: MessageBoxImage.Warning);
            return false;
        }
        finally { SettingsCardsGrid.IsEnabled = true; _settingsOperationInProgress = false; }
    }
}
