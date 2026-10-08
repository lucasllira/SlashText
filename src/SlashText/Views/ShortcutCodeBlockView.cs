using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using SlashText.Design;
using SlashText.Services;

namespace SlashText.Views;

/// <summary>The raw text/language are dependency properties, including for WPF undo/XAML snapshots.</summary>
public sealed class ShortcutCodeBlockView : FrameworkElement
{
    public static readonly DependencyProperty CodeTextProperty = DependencyProperty.Register(nameof(CodeText), typeof(string), typeof(ShortcutCodeBlockView), new FrameworkPropertyMetadata("", Changed));
    public static readonly DependencyProperty CodeLanguageProperty = DependencyProperty.Register(nameof(CodeLanguage), typeof(string), typeof(ShortcutCodeBlockView), new FrameworkPropertyMetadata("text", Changed));
    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(nameof(IsEditable), typeof(bool), typeof(ShortcutCodeBlockView), new FrameworkPropertyMetadata(true, Changed));
    public static readonly RoutedEvent EditRequestedEvent = EventManager.RegisterRoutedEvent("EditRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ShortcutCodeBlockView));
    public string CodeText { get => (string)GetValue(CodeTextProperty); set => SetValue(CodeTextProperty, value); }
    public string CodeLanguage { get => (string)GetValue(CodeLanguageProperty); set => SetValue(CodeLanguageProperty, value); }
    public bool IsEditable { get => (bool)GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }
    internal CodeBlockContent CodeContent => new(CodeLanguage, CodeText);
    private readonly Border _root;
    private readonly TextEditor _editor;
    private readonly TextBlock _title;
    private readonly TextBlock _count;
    private readonly Button _edit;
    private readonly Button _collapse;
    private bool _expanded = true;

    public ShortcutCodeBlockView()
    {
        var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition());
        var header = new DockPanel { Margin = new Thickness(10, 5, 10, 5), LastChildFill = true };
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        _collapse = Tool("Recolher código", "ChevronUp", () => SetExpanded(!_expanded)); actions.Children.Add(_collapse);
        actions.Children.Add(Tool("Copiar código", "Copy", CopyCode));
        _edit = Tool("Editar bloco de código", "Pencil", () => RaiseEvent(new RoutedEventArgs(EditRequestedEvent, this))); actions.Children.Add(_edit);
        var identity = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var icon = new LabIcon { Kind = "CodeXml", Width = 17, Height = 17, Margin = new Thickness(0, 0, 8, 0) }; icon.SetResourceReference(LabIcon.ForegroundProperty, "Lab.accent-text"); identity.Children.Add(icon);
        _title = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }; _title.SetResourceReference(TextBlock.ForegroundProperty, "Lab.text"); identity.Children.Add(_title);
        _count = new TextBlock { FontSize = 11, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; _count.SetResourceReference(TextBlock.ForegroundProperty, "Lab.muted"); identity.Children.Add(_count); header.Children.Add(identity);
        var headerSurface = new Border { Child = header, BorderThickness = new Thickness(0, 0, 0, 1) }; headerSurface.SetResourceReference(Border.BackgroundProperty, "Lab.raised"); headerSurface.SetResourceReference(Border.BorderBrushProperty, "Lab.line"); layout.Children.Add(headerSurface);
        _editor = ShortcutCodeEditor.Create(readOnly: true); _editor.MinHeight = 70; _editor.MaxHeight = 230; Grid.SetRow(_editor, 1); layout.Children.Add(_editor);
        ShortcutCodeEditor.BindLanguage(_editor, () => CodeLanguage);
        _root = new Border { Child = layout, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), ClipToBounds = true };
        _root.SetResourceReference(Border.BorderBrushProperty, "Lab.line"); _root.SetResourceReference(Border.BackgroundProperty, "Lab.input");
        AddVisualChild(_root); AddLogicalChild(_root); AutomationProperties.SetName(this, "Bloco de código"); Refresh();
    }
    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => index == 0 ? _root : throw new ArgumentOutOfRangeException(nameof(index));
    protected override IEnumerator LogicalChildren => new[] { _root }.GetEnumerator();
    protected override Size MeasureOverride(Size availableSize) { _root.Measure(availableSize); return _root.DesiredSize; }
    protected override Size ArrangeOverride(Size finalSize) { _root.Arrange(new Rect(finalSize)); return finalSize; }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((ShortcutCodeBlockView)sender).Refresh();
    private void Refresh()
    {
        if (_editor is null) return;
        _editor.Text = CodeText; _title.Text = ShortcutCodeEditor.Label(CodeLanguage);
        _count.Text = $"{CodeText.Count(c => c == '\n') + 1} linhas"; _edit.Visibility = IsEditable ? Visibility.Visible : Visibility.Collapsed;
        _editor.Height = Math.Clamp((CodeText.Count(c => c == '\n') + 1) * 19 + 24, 70, 230);
        AutomationProperties.SetName(this, "Bloco de código " + _title.Text); InvalidateMeasure();
    }
    internal void SetExpanded(bool expanded)
    {
        _expanded = expanded; _editor.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        ((LabIcon)_collapse.Content).Kind = expanded ? "ChevronUp" : "ChevronDown";
        _collapse.ToolTip = expanded ? "Recolher código" : "Expandir código";
        AutomationProperties.SetName(_collapse, _collapse.ToolTip.ToString());
        if (expanded) { LabMotion.SetEntrance(_editor, "Page"); LabMotion.PlayEntrance(_editor); }
        InvalidateMeasure();
    }
    private void CopyCode()
    {
        if (CodeText.Length == 0) { _count.Text = "Bloco vazio"; return; }
        try { Clipboard.SetText(CodeText); _count.Text = "Copiado"; }
        catch (System.Runtime.InteropServices.ExternalException) { _count.Text = "Tente copiar novamente"; }
    }
    private static Button Tool(string label, string kind, Action action)
    {
        var button = new Button { Content = new LabIcon { Kind = kind, Width = 15, Height = 15 }, ToolTip = label, Width = 30, Height = 30, Padding = new Thickness(5), Margin = new Thickness(2, 0, 0, 0) };
        button.SetResourceReference(StyleProperty, "Lab.Shortcuts.Ghost"); AutomationProperties.SetName(button, label); button.Click += (_, _) => action(); return button;
    }
}
