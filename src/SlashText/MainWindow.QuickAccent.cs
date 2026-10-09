using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SlashText.Design;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText;

public partial class MainWindow
{
    private bool _updatingQuickAccentControls;
    private readonly SemaphoreSlim _quickAccentSaveGate = new(1, 1);
    private ScreenHelpHighlighter? _quickAccentHelpHighlight;
    private string _quickAccentPreviewCharacters = string.Empty;

    private void LoadQuickAccentControls()
    {
        _updatingQuickAccentControls = true;
        try
        {
            QuickAccentEnabledCheckBox.IsChecked = _settings.QuickAccentEnabled;
            SelectComboByTag(QuickAccentActivationBox, _settings.QuickAccentActivationKey);
            SelectComboByTag(QuickAccentPositionBox, _settings.QuickAccentToolbarPosition);
            QuickAccentUnicodeCheckBox.IsChecked = _settings.QuickAccentShowUnicode;
            QuickAccentSortCheckBox.IsChecked = _settings.QuickAccentSortByUsage;
            QuickAccentDelaySlider.Value = Math.Clamp(_settings.QuickAccentInputDelayMs, 0, 2000);
            QuickAccentDelayBox.Text = _settings.QuickAccentInputDelayMs.ToString();
            QuickAccentDelayErrorText.Visibility = Visibility.Collapsed;
            QuickAccentDelayBox.SetResourceReference(Control.BorderBrushProperty, "Lab.line");
            QuickAccentExcludedAppsBox.Text = _settings.QuickAccentExcludedApps;
            ApplyQuickAccentCharacterSetSelection(_settings.QuickAccentCharacterSets);
        }
        finally { _updatingQuickAccentControls = false; }
        ApplyQuickAccentSettings();
    }

    private async void QuickAccentSettings_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_initialized && !_updatingQuickAccentControls) await SaveQuickAccentSettingsAsync();
    }

    private async void QuickAccentDelaySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QuickAccentDelayBox is null || _updatingQuickAccentControls) return;
        QuickAccentDelayBox.Text = ((int)Math.Round(e.NewValue)).ToString();
        if (_initialized) await SaveQuickAccentSettingsAsync();
    }

    private async void QuickAccentDelay_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !_initialized) return;
        e.Handled = true;
        await SaveQuickAccentSettingsAsync();
    }

    private async Task SaveQuickAccentSettingsAsync()
    {
        await _quickAccentSaveGate.WaitAsync();
        try
        {
            // Invalid delay stays visible for correction; unrelated switches still save,
            // especially disabling the feature. Never replace a valid delay with a default.
            var validDelay = int.TryParse(QuickAccentDelayBox.Text, out var delay) && delay is >= 0 and <= 2000;
            QuickAccentDelayErrorText.Text = "Informe um número inteiro entre 0 e 2.000 ms.";
            QuickAccentDelayErrorText.Visibility = validDelay ? Visibility.Collapsed : Visibility.Visible;
            QuickAccentDelayBox.SetResourceReference(Control.BorderBrushProperty, validDelay ? "Lab.line" : "Lab.error");
            if (validDelay)
            {
                _settings.QuickAccentInputDelayMs = delay;
                _updatingQuickAccentControls = true;
                try { QuickAccentDelaySlider.Value = delay; }
                finally { _updatingQuickAccentControls = false; }
            }
            _settings.QuickAccentEnabled = QuickAccentEnabledCheckBox.IsChecked == true;
            _settings.QuickAccentActivationKey = SelectedTag(QuickAccentActivationBox, "Space");
            _settings.QuickAccentToolbarPosition = SelectedTag(QuickAccentPositionBox, "BottomCenter");
            _settings.QuickAccentShowUnicode = QuickAccentUnicodeCheckBox.IsChecked == true;
            _settings.QuickAccentSortByUsage = QuickAccentSortCheckBox.IsChecked == true;
            _settings.QuickAccentExcludedApps = QuickAccentExcludedAppsBox.Text;
            _settings.QuickAccentCharacterSets = SelectedQuickAccentCharacterSets();
            ApplyQuickAccentSettings();
            await _settingsStore.SaveAsync(_settings);
        }
        catch (Exception exception)
        {
            AppDiagnosticLog.Write("quick-accent.settings.save_failed", ("exceptionType", exception.GetType().Name));
            QuickAccentDelayErrorText.Text = "Não foi possível salvar as preferências. Tente novamente.";
            QuickAccentDelayErrorText.Visibility = Visibility.Visible;
        }
        finally { _quickAccentSaveGate.Release(); }
    }

    private Button[] QuickAccentPreviewButtons() =>
    [ QuickAccentPreviewChoice0, QuickAccentPreviewChoice1, QuickAccentPreviewChoice2, QuickAccentPreviewChoice3,
      QuickAccentPreviewChoice4, QuickAccentPreviewChoice5, QuickAccentPreviewChoice6, QuickAccentPreviewChoice7 ];

    private void QuickAccentPreviewLetter_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        _quickAccentPreviewIndex = 0;
        UpdateQuickAccentPreviewSelection();
    }

    private void QuickAccentPreviewChoice_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Kept as a selection-only route for the existing UI inventory. Button.Click
        // handles keyboard and mouse insertion once, entirely inside the local test box.
        if (sender is Button { Tag: string value } && int.TryParse(value, out var index))
        { _quickAccentPreviewIndex = index; UpdateQuickAccentPreviewSelection(); }
    }

    private void QuickAccentPreviewChoice_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value, IsEnabled: true } || !int.TryParse(value, out var index)
            || index < 0 || index >= _quickAccentPreviewCharacters.Length) return;
        _quickAccentPreviewIndex = index;
        QuickAccentTestBox.SelectedText = _quickAccentPreviewCharacters[index].ToString();
        QuickAccentTestBox.CaretIndex = QuickAccentTestBox.SelectionStart + QuickAccentTestBox.SelectionLength;
        QuickAccentTestBox.SelectionLength = 0;
        UpdateQuickAccentPreviewSelection();
        QuickAccentTestBox.Focus();
    }

    private void QuickAccentClearTest_OnClick(object sender, RoutedEventArgs e)
    { QuickAccentTestBox.Clear(); QuickAccentTestBox.Focus(); }

    private void UpdateQuickAccentPreviewSelection()
    {
        if (QuickAccentPreviewChoice7 is null || QuickAccentTestBox is null) return;
        var letter = SelectedTag(QuickAccentPreviewLetterBox, "A")[0];
        _quickAccentPreviewCharacters = _quickAccentService.GetPreviewChoices(char.ToLowerInvariant(letter));
        _quickAccentPreviewIndex = Math.Clamp(_quickAccentPreviewIndex, 0, Math.Max(0, _quickAccentPreviewCharacters.Length - 1));
        var enabled = _settings.QuickAccentEnabled;
        QuickAccentStateText.Text = enabled ? "Ativo" : "Inativo";
        QuickAccentStateText.SetResourceReference(TextBlock.ForegroundProperty, enabled ? "Lab.accent-text" : "Lab.muted");
        QuickAccentStateBadge.SetResourceReference(Border.BackgroundProperty, enabled ? "Lab.tint" : "Lab.raised");
        QuickAccentPreviewActivationText.Text = " + " + (_settings.QuickAccentActivationKey switch { "Left" => "←", "Right" => "→", _ => "Espaço" });
        QuickAccentTestBox.IsEnabled = QuickAccentClearTestButton.IsEnabled = enabled;
        QuickAccentPreviewEmptyText.Visibility = _quickAccentPreviewCharacters.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var buttons = QuickAccentPreviewButtons();
        for (var index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            button.Visibility = index < _quickAccentPreviewCharacters.Length ? Visibility.Visible : Visibility.Collapsed;
            button.IsEnabled = enabled;
            if (index >= _quickAccentPreviewCharacters.Length) continue;
            var character = _quickAccentPreviewCharacters[index];
            var selected = index == _quickAccentPreviewIndex;
            var content = new StackPanel();
            var characterText = new TextBlock { Text = character.ToString(), HorizontalAlignment = HorizontalAlignment.Center };
            characterText.SetResourceReference(TextBlock.ForegroundProperty, selected ? "Lab.accent-text" : "Lab.text");
            content.Children.Add(characterText);
            if (_settings.QuickAccentShowUnicode)
            {
                var unicodeText = new TextBlock { Text = $"U+{(int)character:X4}", FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
                unicodeText.SetResourceReference(TextBlock.ForegroundProperty, selected ? "Lab.accent-text" : "Lab.muted");
                content.Children.Add(unicodeText);
            }
            button.Content = content;
            System.Windows.Automation.AutomationProperties.SetName(button, $"Inserir {character}, U+{(int)character:X4}, no campo de teste");
            button.ToolTip = $"Inserir {character} no teste · U+{(int)character:X4}";
            button.SetResourceReference(Control.BackgroundProperty, selected ? "Lab.tint" : "Lab.input");
            button.SetResourceReference(Control.BorderBrushProperty, selected ? "Lab.accent" : "Lab.line");
            button.SetResourceReference(Control.ForegroundProperty, selected ? "Lab.accent-text" : "Lab.text");
        }
    }

    private void UpdateQuickAccentLayout(double width)
    {
        if (QuickAccentCards is null) return;
        var narrow = width < 1100;
        QuickAccentCards.ColumnDefinitions[1].Width = new GridLength(narrow ? 0 : 18);
        QuickAccentCards.ColumnDefinitions[2].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(QuickAccentSetsPanel, narrow ? 0 : 2);
        Grid.SetRow(QuickAccentSetsPanel, narrow ? 1 : 0);
        QuickAccentSetsPanel.Margin = new Thickness(0, narrow ? 18 : 0, 0, 0);
    }

    private void OpenQuickAccentHelp_OnClick(object sender, RoutedEventArgs e)
    {
        _quickAccentHelpHighlight?.Remove();
        var guide = new ScreenHelpWindow(QuickAccentHelpContent.Create()) { Owner = this };
        LabMotion.SetReduced(guide, LabMotion.GetReduced(this));
        guide.EnableBackdrop(); ShowCaptureDialog(guide);
        if (guide.RequestedTarget is not { } name || FindName(name) is not FrameworkElement target) return;
        target.BringIntoView();
        Dispatcher.BeginInvoke(new Action(() =>
        { if (target.IsVisible) { target.Focus(); _quickAccentHelpHighlight = ScreenHelpHighlighter.Show(target); } }), DispatcherPriority.Loaded);
    }

    internal void PrepareQuickAccentEvidence(AppSettings settings, double width)
    {
        _settings = settings; LoadQuickAccentControls();
        ShowView(QuickAccentView, QuickAccentTabButton); UpdateQuickAccentLayout(width);
        _initialized = true; // Exercise real change handlers; this fixture never runs Loaded/startup hooks.
    }
    internal Task SaveQuickAccentForEvidence() => SaveQuickAccentSettingsAsync();
    internal bool QuickAccentHookRunningForEvidence => _quickAccentService.IsRunning;
    internal string QuickAccentChoicesForEvidence => _quickAccentPreviewCharacters;
}
