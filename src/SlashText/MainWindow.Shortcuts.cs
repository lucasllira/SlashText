using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
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
    private string? _shortcutCategoryIconOverride;
    private bool _syncShortcutFormatting;
    private Popup? _shortcutCategoryPopup;
    private static readonly (string Kind, string Label)[] ShortcutCategoryIconChoices =
    [
        ("FolderOpen", "Geral"), ("Briefcase", "Trabalho"), ("Mail", "Mensagens"), ("BookOpen", "Estudos"),
        ("CodeXml", "Código"), ("Star", "Favoritos"), ("Keyboard", "Comandos"), ("ScrollText", "Documentos")
    ];

    private string[] ReadShortcutDraft() =>
    [
        NameBox.Text, TriggerBox.Text, CategoryBox.Text, FormatBox.SelectedIndex.ToString(),
        RichTextMarkdownConverter.Save(ContentEditor,
            FormatBox.SelectedIndex == 1 ? SnippetFormat.Markdown : SnippetFormat.Plain),
        CurrentShortcutCategoryIcon
    ];

    private bool HasUnsavedShortcutDraft => !_loadingShortcutDraft && _shortcutDraftBaseline is not null &&
        !_shortcutDraftBaseline.SequenceEqual(ReadShortcutDraft(), StringComparer.Ordinal);

    private void ResetShortcutDraftBaseline()
    {
        _shortcutCategoryIconOverride = null;
        RefreshShortcutCategoryIcon();
        _shortcutDraftBaseline = ReadShortcutDraft();
        RefreshShortcutDraftState();
        RefreshShortcutFormatting();
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
        if (ShortcutCategoryIconButton is not null) ShortcutCategoryIconButton.IsEnabled = _snippetStorageAvailable && !_shortcutOperationInProgress;
        if (dirty && StatusText is not null) StatusText.Text = "Alterações não salvas";
    }

    private bool CanDiscardShortcutDraft()
    {
        if (_shortcutOperationInProgress) return false;
        if (!HasUnsavedShortcutDraft) return true;
        if (_shortcutDiscardDecisionForEvidence is bool decision) return decision;
        var dialog = new ShortcutConfirmationWindow("Descartar alterações?", "Este atalho tem alterações que ainda não foram salvas.",
            "Para continuar editando, escolha Cancelar e depois use Salvar.", "Descartar alterações") { Owner = this };
        LabMotion.SetReduced(dialog, LabMotion.GetReduced(this)); dialog.EnableBackdrop();
        return ShowCaptureDialog(dialog);
    }

    private Button CreateCategoryButton(string? category, string label, int count)
    {
        var selected = string.Equals(category, _selectedCategory, StringComparison.CurrentCultureIgnoreCase);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new LabIcon { Kind = SavedShortcutCategoryIcon(category), Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) }; row.Children.Add(icon);
        var name = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        name.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1) });
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

    private string SavedShortcutCategoryIcon(string? category)
    {
        if (category is null || _settings.ShortcutCategoryIcons is null) return "FolderOpen";
        var found = _settings.ShortcutCategoryIcons.FirstOrDefault(pair => string.Equals(pair.Key, category.Trim(), StringComparison.OrdinalIgnoreCase)).Value;
        return ShortcutCategoryIconChoices.Any(item => item.Kind == found) ? found! : "FolderOpen";
    }
    private string CurrentShortcutCategoryIcon => _shortcutCategoryIconOverride ?? SavedShortcutCategoryIcon(CategoryBox.Text);
    private void RefreshShortcutCategoryIcon()
    {
        if (ShortcutCategoryIcon is not null && CategoryBox is not null) ShortcutCategoryIcon.Kind = CurrentShortcutCategoryIcon;
    }
    private void SetShortcutCategoryIcon(string kind)
    {
        if (!ShortcutCategoryIconChoices.Any(item => item.Kind == kind)) return;
        _shortcutCategoryIconOverride = kind; RefreshShortcutCategoryIcon(); RefreshShortcutDraftState();
    }
    private async Task<bool> SaveShortcutCategoryIconAsync(string category, string kind)
    {
        if (_shortcutCategoryIconOverride is null || kind == SavedShortcutCategoryIcon(category)) return true;
        var previous = _settings.ShortcutCategoryIcons;
        var next = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in previous ?? new Dictionary<string, string>()) next[pair.Key] = pair.Value;
        next[category] = kind; _settings.ShortcutCategoryIcons = next;
        try { await _settingsStore.SaveAsync(_settings); return true; }
        catch (Exception exception)
        {
            _settings.ShortcutCategoryIcons = previous ?? new Dictionary<string, string>();
            // Content has already been saved. Report the cosmetic preference failure separately.
            AppDiagnosticLog.Write("shortcuts.category_icon.save_failed", ("exceptionType", exception.GetType().Name));
            return false;
        }
    }
    private void ChooseShortcutCategoryIcon_OnClick(object sender, RoutedEventArgs e)
    {
        if (_shortcutCategoryPopup?.IsOpen == true) { _shortcutCategoryPopup.IsOpen = false; return; }
        var panel = new StackPanel { Margin = new Thickness(14) };
        var title = new TextBlock { Text = "Ícone da categoria", FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) };
        title.SetResourceReference(TextBlock.ForegroundProperty, "Lab.text"); panel.Children.Add(title);
        var hint = new TextBlock { Text = "O mesmo ícone identifica todos os atalhos desta categoria. Use Salvar para aplicar.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Lab.muted"); panel.Children.Add(hint);
        var choices = new UniformGrid { Columns = 2 };
        var border = new Border { Child = panel, Width = 302, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1) };
        border.SetResourceReference(Border.BackgroundProperty, "Lab.panel"); border.SetResourceReference(Border.BorderBrushProperty, "Lab.line-strong");
        var popup = new Popup { Child = border, PlacementTarget = ShortcutCategoryIconButton, Placement = PlacementMode.Bottom, VerticalOffset = 6, AllowsTransparency = true, StaysOpen = false };
        _shortcutCategoryPopup = popup;
        foreach (var item in ShortcutCategoryIconChoices)
        {
            var button = new Button { Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(9, 8, 9, 8), HorizontalContentAlignment = HorizontalAlignment.Left };
            button.SetResourceReference(StyleProperty, "Lab.Shortcuts.Item");
            if (item.Kind == CurrentShortcutCategoryIcon) { button.SetResourceReference(BackgroundProperty, "Lab.tint"); button.SetResourceReference(ForegroundProperty, "Lab.accent-text"); }
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = new LabIcon { Kind = item.Kind, Width = 18, Height = 18, Margin = new Thickness(0, 0, 8, 0) }; row.Children.Add(icon);
            var label = new TextBlock { Text = item.Label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            label.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { Source = button }); row.Children.Add(label);
            button.Content = row; AutomationProperties.SetName(button, "Ícone " + item.Label);
            button.Click += (_, _) => { SetShortcutCategoryIcon(item.Kind); popup.IsOpen = false; };
            choices.Children.Add(button);
        }
        panel.Children.Add(choices); LabMotion.SetEntrance(border, "Popup"); LabMotion.SetReduced(border, LabMotion.GetReduced(this));
        border.PreviewKeyDown += (_, args) => { if (args.Key == Key.Escape) { args.Handled = true; popup.IsOpen = false; } };
        popup.Opened += (_, _) => { LabMotion.PlayEntrance(border); border.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); };
        popup.Closed += (_, _) => ShortcutCategoryIconButton.Focus(); popup.IsOpen = true;
    }

    private void InitializeShortcutFormatting()
    {
        _syncShortcutFormatting = true;
        try
        {
            var preferred = new[] { "Segoe UI", "Arial", "Calibri", "Verdana", "Georgia", "Consolas" };
            var installed = Fonts.SystemFontFamilies.Select(font => font.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            FontFamilyBox.Items.Clear();
            foreach (var font in preferred.Where(font => installed.Contains(font, StringComparer.OrdinalIgnoreCase))
                .Concat(installed.Except(preferred, StringComparer.OrdinalIgnoreCase).OrderBy(font => font)))
                FontFamilyBox.Items.Add(new ComboBoxItem { Content = font, Tag = font });
        }
        finally { _syncShortcutFormatting = false; }
        RefreshShortcutFormatting();
    }
    private void ShortcutContent_OnSelectionChanged(object sender, RoutedEventArgs e) => RefreshShortcutFormatting();
    private void RefreshShortcutFormatting()
    {
        if (_syncShortcutFormatting || ContentEditor is null || FontFamilyBox is null || ShortcutBoldButton is null) return;
        _syncShortcutFormatting = true;
        try
        {
            var selection = ContentEditor.Selection;
            var font = selection.GetPropertyValue(TextElement.FontFamilyProperty) as FontFamily;
            FontFamilyBox.SelectedItem = FontFamilyBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag as string, font?.Source, StringComparison.OrdinalIgnoreCase));
            var size = selection.GetPropertyValue(TextElement.FontSizeProperty);
            if (size is double fontSize)
            {
                var sizeItem = FontSizeBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => double.TryParse(item.Tag as string, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && Math.Abs(n - fontSize) < .05);
                if (sizeItem is null)
                {
                    sizeItem = new ComboBoxItem { Content = (fontSize * .75).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), Tag = fontSize.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                    FontSizeBox.Items.Add(sizeItem);
                }
                FontSizeBox.SelectedItem = sizeItem;
            }
            else FontSizeBox.SelectedItem = null;
            ShortcutBoldButton.Tag = selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight weight && weight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight() ? "Selected" : null;
            ShortcutItalicButton.Tag = Equals(selection.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic) ? "Selected" : null;
            ShortcutUnderlineButton.Tag = selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection decorations && decorations.Any(d => d.Location == TextDecorationLocation.Underline) ? "Selected" : null;
            var alignment = selection.GetPropertyValue(Paragraph.TextAlignmentProperty);
            foreach (var (button, value) in new[] { (ShortcutAlignLeftButton, TextAlignment.Left), (ShortcutAlignCenterButton, TextAlignment.Center), (ShortcutAlignRightButton, TextAlignment.Right), (ShortcutAlignJustifyButton, TextAlignment.Justify) })
                button.Tag = Equals(alignment, value) ? "Selected" : null;
            ShortcutUndoButton.IsEnabled = ContentEditor.CanUndo; ShortcutRedoButton.IsEnabled = ContentEditor.CanRedo;
        }
        finally { _syncShortcutFormatting = false; }
    }
    private void ShortcutUndo_OnClick(object sender, RoutedEventArgs e) { ContentEditor.Undo(); ContentEditor.Focus(); RefreshShortcutFormatting(); }
    private void ShortcutRedo_OnClick(object sender, RoutedEventArgs e) { ContentEditor.Redo(); ContentEditor.Focus(); RefreshShortcutFormatting(); }
    private void ShortcutJustify_OnClick(object sender, RoutedEventArgs e) => EditingCommands.AlignJustify.Execute(null, ContentEditor);

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
        guide.EnableBackdrop();
        ShowCaptureDialog(guide);
        if (guide.RequestedTarget is not { } name || FindName(name) is not FrameworkElement target) return;
        // Help never changes format, panel visibility or the current draft to reveal a target.
        if (!target.IsVisible) target = name == "ShortcutVariablesPanel" ? ShortcutVariablesToggleButton : FormatBox;
        target.BringIntoView();
        Dispatcher.BeginInvoke(new Action(() => { if (!target.IsVisible) return; target.Focus(); _shortcutsHelpHighlight = ScreenHelpHighlighter.Show(target); }), DispatcherPriority.Loaded);
    }
}
