using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using SlashText.Design;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText;

public partial class MainWindow
{
    private string[]? _shortcutDraftBaseline;
    private bool _loadingShortcutDraft;
    private bool _shortcutOperationInProgress;
    private bool _shortcutVariablesVisible = true;
    private double _shortcutVariablesWidth = 280;
    private ScreenHelpHighlighter? _shortcutsHelpHighlight;

    private string[] ReadShortcutDraft() =>
    [
        NameBox.Text, TriggerBox.Text, CategoryBox.Text, FormatBox.SelectedIndex.ToString(),
        RichTextMarkdownConverter.Save(ContentEditor,
            FormatBox.SelectedIndex == 1 ? SnippetFormat.Markdown : SnippetFormat.Plain)
    ];

    private bool HasUnsavedShortcutDraft => !_loadingShortcutDraft && _shortcutDraftBaseline is not null &&
        !_shortcutDraftBaseline.SequenceEqual(ReadShortcutDraft(), StringComparer.Ordinal);

    private void ResetShortcutDraftBaseline()
    {
        _shortcutDraftBaseline = ReadShortcutDraft();
        RefreshShortcutDraftState();
    }

    private void RefreshShortcutDraftState()
    {
        if (_loadingShortcutDraft || ContentEditor is null || ShortcutSaveStateText is null || FormatBox is null || ShortcutSaveButton is null || ShortcutDeleteButton is null || ShortcutImportButton is null) return;
        var dirty = HasUnsavedShortcutDraft;
        ShortcutSaveStateText.Text = !_snippetStorageAvailable ? "Atalhos em modo protegido" : dirty ? "Alterações não salvas" : "Tudo salvo";
        var color = !_snippetStorageAvailable ? "Lab.error" : dirty ? "Lab.muted" : "Lab.green";
        ShortcutSaveStateText.SetResourceReference(TextBlock.ForegroundProperty, color);
        ShortcutSaveStateIcon.Kind = dirty || !_snippetStorageAvailable ? "CircleHelp" : "Check";
        ShortcutSaveStateIcon.SetResourceReference(LabIcon.ForegroundProperty, color);
        ShortcutSaveButton.IsEnabled = _snippetStorageAvailable && !_shortcutOperationInProgress;
        ShortcutDeleteButton.IsEnabled = _snippetStorageAvailable && _selected is not null && !_shortcutOperationInProgress;
        ShortcutImportButton.IsEnabled = _snippetStorageAvailable && !_shortcutOperationInProgress;
        if (dirty && StatusText is not null) StatusText.Text = "Alterações não salvas";
    }

    private bool CanDiscardShortcutDraft()
    {
        if (_shortcutOperationInProgress) return false;
        if (!HasUnsavedShortcutDraft) return true;
        if (_shortcutDiscardDecisionForEvidence is bool decision) return decision;
        return MessageBox.Show(this,
            "Há alterações não salvas neste atalho. Deseja descartá-las?\n\nEscolha Não para continuar editando e usar Salvar.",
            "Alterações no atalho", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private Button CreateCategoryButton(string? category, string label, int count)
    {
        var selected = string.Equals(category, _selectedCategory, StringComparison.CurrentCultureIgnoreCase);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new LabIcon { Kind = "FolderOpen", Size = 16, Margin = new Thickness(0, 0, 9, 0) }; row.Children.Add(icon);
        var name = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(name, 1); row.Children.Add(name);
        var number = new TextBlock { Text = count.ToString(), FontSize = 11, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        number.SetResourceReference(TextBlock.ForegroundProperty, "Lab.muted"); Grid.SetColumn(number, 2); row.Children.Add(number);
        var button = new Button { Content = row, Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 3), ToolTip = $"{label} · {count} atalhos" };
        button.SetResourceReference(StyleProperty, "Lab.Shortcuts.Item");
        button.SetResourceReference(BackgroundProperty, selected ? "Lab.tint" : "Lab.shell");
        button.SetResourceReference(ForegroundProperty, selected ? "Lab.accent-text" : "Lab.text");
        AutomationProperties.SetName(button, $"Categoria {label}, {count} atalhos" + (selected ? ", selecionada" : ""));
        button.Click += (_, _) => { _selectedCategory = category; RefreshNavigation(); };
        return button;
    }

    private Button CreateSnippetButton(Snippet snippet)
    {
        var selected = ReferenceEquals(snippet, _selected);
        var content = new StackPanel();
        var command = new TextBlock { Text = snippet.Trigger, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        command.SetResourceReference(TextBlock.ForegroundProperty, "Lab.accent-text"); content.Children.Add(command);
        var name = new TextBlock { Text = snippet.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 5, 0, 0) };
        name.SetResourceReference(TextBlock.ForegroundProperty, "Lab.text"); content.Children.Add(name);
        var excerpt = snippet.Content.Replace('\r', ' ').Replace('\n', ' ');
        var preview = new TextBlock { Text = excerpt, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 5, 0, 0) };
        preview.SetResourceReference(TextBlock.ForegroundProperty, "Lab.muted"); content.Children.Add(preview);
        if (!snippet.Enabled || snippet.HasLegacyIncompatibleTrigger)
        {
            var state = new TextBlock { Text = snippet.HasLegacyIncompatibleTrigger ? "Comando legado · revise para ativar" : "Pausado", FontSize = 11, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
            state.SetResourceReference(TextBlock.ForegroundProperty, "Lab.muted"); content.Children.Add(state);
        }
        var button = new Button { Content = content, Tag = selected ? "Selected" : null, ToolTip = $"{snippet.Name}\n{snippet.Trigger}\nCategoria: {snippet.Category}" };
        button.SetResourceReference(StyleProperty, "Lab.Shortcuts.Item");
        AutomationProperties.SetName(button, $"{snippet.Name}, comando {snippet.Trigger}, categoria {snippet.Category}" + (selected ? ", selecionado" : ""));
        button.Click += (_, _) => SelectSnippet(snippet);
        return button;
    }

    private void ShortcutContent_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        e.Handled = true;
        ContentEditor.MoveFocus(new TraversalRequest(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next));
    }

    private void ClearShortcutSearch_OnClick(object sender, RoutedEventArgs e) { SearchBox.Clear(); SearchBox.Focus(); }

    private void ToggleShortcutVariables_OnClick(object sender, RoutedEventArgs e) => SetShortcutVariablesVisible(!_shortcutVariablesVisible);

    private void SetShortcutVariablesVisible(bool visible)
    {
        if (!visible && _shortcutVariablesVisible) _shortcutVariablesWidth = ShortcutRightColumn.ActualWidth;
        _shortcutVariablesVisible = visible;
        ShortcutVariablesPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ShortcutRightDivider.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible) { ShortcutRightColumn.MinWidth = ShortcutRightMinimum; ShortcutRightColumn.Width = new GridLength(Math.Max(ShortcutRightMinimum, _shortcutVariablesWidth)); }
        NormalizeShortcutColumns(ActualWidth > 0 ? ActualWidth : Width);
        ShortcutVariablesToggleButton.ToolTip = visible ? "Ocultar variáveis" : "Mostrar variáveis";
        if (visible) LabMotion.PlayEntrance(ShortcutVariablesPanel);
    }

    private void OpenShortcutsHelp_OnClick(object sender, RoutedEventArgs e)
    {
        _shortcutsHelpHighlight?.Remove(); _shortcutsHelpHighlight = null;
        var guide = new ScreenHelpWindow(ShortcutsHelpContent.Create()) { Owner = this };
        LabMotion.SetReduced(guide, LabMotion.GetReduced(this));
        ShowCaptureDialog(guide);
        if (guide.RequestedTarget is not { } name || FindName(name) is not FrameworkElement target) return;
        // Help never changes format, panel visibility or the current draft to reveal a target.
        if (!target.IsVisible) target = name == "ShortcutVariablesPanel" ? ShortcutVariablesToggleButton : FormatBox;
        target.BringIntoView();
        Dispatcher.BeginInvoke(new Action(() => { if (!target.IsVisible) return; target.Focus(); _shortcutsHelpHighlight = ScreenHelpHighlighter.Show(target); }), DispatcherPriority.Loaded);
    }
}
