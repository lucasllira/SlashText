using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SlashText.Design;
using SlashText.Services;

namespace SlashText.Views;

/// <summary>Contextual controls for the same image document, not a second editor.</summary>
public sealed class CaptureEditorContextPanel : UserControl
{
    private readonly WrapPanel _panel = new() { Margin = new Thickness(14, 9, 14, 9), VerticalAlignment = VerticalAlignment.Center };
    private CaptureWorkbenchEditor? _editor;
    private string? _shown;
    private Button? _applyCrop;
    public CaptureEditorContextPanel()
    {
        SetResourceReference(BackgroundProperty, "Lab.input");
        SetResourceReference(ForegroundProperty, "Lab.text");
        Content = _panel;
        MinHeight = 52;
    }
    public void Attach(CaptureWorkbenchEditor editor) { _editor = editor; Refresh(); }
    public void Refresh()
    {
        if (_editor is null) return;
        if (_applyCrop is not null) _applyCrop.IsEnabled = _editor.HasPendingCrop;
        var shown = _editor.IsCropTool ? "crop" : _editor.SelectedTool?.ToString() ?? "navigate";
        if (_shown == shown) return;
        _shown = shown; _applyCrop = null; _panel.Children.Clear();
        AddLabel(shown switch
        {
            "crop" => "Recortar", "navigate" => "Navegar", "Text" => "Texto", "Stamp" => "Emotes",
            "Blur" => "Desfocar", "Pixelate" => "Pixelizar", "Ellipse" => "Elipse", "Number" => "Número",
            "Rectangle" => "Retângulo", "Pencil" => "Caneta", "Highlighter" => "Marca-texto", "Line" => "Linha", _ => "Seta"
        }, emphasis: true);
        if (shown == "Text")
        {
            AddLabel("Fonte");
            var preferred = new[] { "Segoe UI", "Arial", "Verdana", "Georgia", "Consolas" };
            var installed = Fonts.SystemFontFamilies.Select(f => f.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var fonts = preferred.Where(p => installed.Contains(p, StringComparer.OrdinalIgnoreCase))
                .Concat(installed.Except(preferred, StringComparer.OrdinalIgnoreCase).OrderBy(f => f)).ToArray();
            var fontBox = Combo(fonts, _editor.TextFontFamily, value => _editor.TextFontFamily = value, 155);
            AddLabel("Tamanho");
            Combo(new[] { "12", "16", "20", "24", "32", "40", "48", "64", "80", "96", "120" },
                _editor.TextSize.ToString(CultureInfo.InvariantCulture), value => _editor.TextSize = float.Parse(value, CultureInfo.InvariantCulture), 68);
            var input = new TextBox { Text = _editor.AnnotationText, MinWidth = 210, MaxLength = 120, Margin = new Thickness(0, 0, 10, 0) };
            input.SetResourceReference(StyleProperty, "Lab.Field");
            input.FontFamily = new FontFamily(_editor.TextFontFamily);
            fontBox.SelectionChanged += (_, _) => input.FontFamily = new FontFamily(_editor.TextFontFamily);
            input.TextChanged += (_, _) => _editor.AnnotationText = input.Text;
            _panel.Children.Add(input);
            Toggle("Negrito", _editor.TextBold, value => _editor.TextBold = value);
            Toggle("Itálico", _editor.TextItalic, value => _editor.TextItalic = value);
            Combo(new[] { "Esquerda", "Centro", "Direita" }, _editor.TextAlignment switch { "Center" => "Centro", "Right" => "Direita", _ => "Esquerda" },
                value => _editor.TextAlignment = value switch { "Centro" => "Center", "Direita" => "Right", _ => "Left" }, 116);
        }
        else if (shown == "Stamp")
        {
            var strip = new StackPanel { Orientation = Orientation.Horizontal };
            var scroller = new ScrollViewer
            {
                Content = strip, MaxWidth = 810, Height = 45, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 10, 0)
            };
            var buttons = new Dictionary<string, Button>();
            foreach (var item in NotoEmojiCatalog.Items)
            {
                var button = new Button
                {
                    Content = new Image { Source = NotoEmojiCatalog.CreateImageSource(item.Value), Width = 26, Height = 26 },
                    ToolTip = item.Name, Width = 34, Height = 34, Padding = new Thickness(3), Margin = new Thickness(0, 0, 3, 0),
                    Tag = item.Value == _editor.SelectedStamp ? "Selected" : null
                };
                System.Windows.Automation.AutomationProperties.SetName(button, item.Name);
                button.SetResourceReference(StyleProperty, "Lab.Pilot.ToolButton");
                button.Click += (_, _) => { _editor.SelectedStamp = item.Value; foreach (var pair in buttons) pair.Value.Tag = pair.Key == item.Value ? "Selected" : null; };
                buttons[item.Value] = button; strip.Children.Add(button);
            }
            Button NavigationButton(string label, bool forward)
            {
                var icon = new LabIcon { Kind = "ArrowLeft", Width = 16, Height = 16,
                    RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(forward ? 180 : 0) };
                icon.SetResourceReference(LabIcon.ForegroundProperty, "Lab.text");
                var button = new Button { Content = icon, ToolTip = label, Width = 34, Height = 34,
                    Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(6) };
                button.SetResourceReference(StyleProperty, "Lab.Pilot.ToolButton");
                System.Windows.Automation.AutomationProperties.SetName(button, label);
                button.Click += (_, _) => scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset + (forward ? 300 : -300));
                return button;
            }
            scroller.SizeChanged += (_, _) => scroller.MaxWidth = Math.Max(150, Math.Min(810, ActualWidth - 350));
            _panel.Children.Add(NavigationButton("Emotes anteriores", false));
            _panel.Children.Add(scroller);
            _panel.Children.Add(NavigationButton("Próximos emotes", true));
            AddButton("Ver todos", "Smile", () =>
            {
                var selected = CaptureEmojiPicker.Show(Window.GetWindow(this));
                if (selected is null) return;
                _editor.SelectedStamp = selected;
                foreach (var pair in buttons) pair.Value.Tag = pair.Key == selected ? "Selected" : null;
                buttons[selected].BringIntoView();
            });
            Combo(new[] { "32", "48", "64", "96", "128" }, _editor.StampSize.ToString(CultureInfo.InvariantCulture), value => _editor.StampSize = float.Parse(value, CultureInfo.InvariantCulture), 70);
        }
        else if (shown == "crop")
        {
            AddLabel("Arraste a área e confirme.");
            AddButton("Cancelar seleção", "X", () => _editor.CancelCrop());
            _applyCrop = AddButton("Aplicar recorte", "Check", () =>
            {
                if (!_editor.ApplyCrop()) MessageBox.Show("Selecione ao menos 8 × 8 pixels.", "Recorte", MessageBoxButton.OK, MessageBoxImage.Information);
            }, primary: true);
            _applyCrop.IsEnabled = _editor.HasPendingCrop;
        }
        else if (shown is "Blur" or "Pixelate")
        {
            AddLabel("Intensidade");
            var value = new TextBlock { Text = _editor.PrivacyStrength.ToString(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 16, 0) };
            var slider = new Slider { Minimum = 6, Maximum = 40, Value = _editor.PrivacyStrength, Width = 140, TickFrequency = 1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
            slider.ValueChanged += (_, _) => { _editor.PrivacyStrength = (int)slider.Value; value.Text = _editor.PrivacyStrength.ToString(); };
            _panel.Children.Add(slider); _panel.Children.Add(value);
            AddLabel("Efeito real na prévia e no arquivo. Confira antes de compartilhar.");
        }
        else if (shown is "Rectangle" or "Ellipse")
        {
            Toggle("Contorno", _editor.ShapeOutline, value => _editor.ShapeOutline = value);
            Toggle("Preenchimento", _editor.ShapeFill.HasValue, value => _editor.ShapeFill = value ? _editor.InkArgb : null);
            AddLabel("Opacidade");
            Combo(new[] { "25%", "50%", "75%", "100%" }, ((int)(_editor.AnnotationOpacity * 100)) + "%", value => _editor.AnnotationOpacity = int.Parse(value.TrimEnd('%')) / 100f, 80);
        }
        else AddLabel(shown == "navigate" ? "Arraste para navegar · Ctrl+roda para zoom" : "Arraste na imagem para aplicar a ferramenta");
        LabMotion.SetEntrance(_panel, "Page");
        if (_panel.IsLoaded) LabMotion.PlayEntrance(_panel);
    }

    public void ShowMoreTools(Button anchor)
    {
        if (_editor is null || !_editor.HasImage) return;
        var panel = new StackPanel();
        var border = new Border { Padding = new Thickness(7), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Child = panel };
        border.SetResourceReference(Border.BackgroundProperty, "Lab.raised");
        border.SetResourceReference(Border.BorderBrushProperty, "Lab.line-strong");
        var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Child = border, VerticalOffset = 6 };
        LabMotion.SetReduced(border, LabMotion.GetReduced(this));
        border.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { popup.IsOpen = false; e.Handled = true; } };
        popup.Closed += (_, _) => anchor.Focus();
        popup.Opened += (_, _) => { LabMotion.PlayEntrance(border); if (panel.Children.OfType<Button>().FirstOrDefault() is { } first) first.Focus(); };
        foreach (var (label, icon, tool) in new[]
        {
            ("Elipse", "Circle", CaptureAnnotationKind.Ellipse), ("Linha", "Minus", CaptureAnnotationKind.Line),
            ("Número", "Hash", CaptureAnnotationKind.Number), ("Desfocar", "ScanLine", CaptureAnnotationKind.Blur),
            ("Pixelizar", "Grid2X2", CaptureAnnotationKind.Pixelate)
        })
        {
            var button = MakeButton(label, icon, () => { popup.IsOpen = false; _editor.SelectTool(tool); });
            button.HorizontalContentAlignment = HorizontalAlignment.Left; button.MinWidth = 180; panel.Children.Add(button);
        }
        panel.Children.Add(MakeButton("Recortar", "Crop", () => { popup.IsOpen = false; _editor.SelectCrop(); }));
        panel.Children.Add(MakeButton("Redimensionar", "Scaling", () => { popup.IsOpen = false; ShowResize(); }));
        LabMotion.SetEntrance(border, "Popup"); popup.IsOpen = true;
    }

    private void ShowResize()
    {
        if (_editor?.Document is not { } document) return;
        var size = document.Dimensions;
        var dialog = new Window { Title = "Redimensionar imagem", Owner = Window.GetWindow(this), Width = 460, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        dialog.SetResourceReference(Window.BackgroundProperty, "Lab.raised"); dialog.SetResourceReference(Window.ForegroundProperty, "Lab.text");
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Redimensionar imagem", FontSize = 20, FontWeight = FontWeights.SemiBold });
        var width = new TextBox { Text = size.Width.ToString(), Margin = new Thickness(0, 6, 0, 12) };
        var height = new TextBox { Text = size.Height.ToString(), Margin = new Thickness(0, 6, 0, 12) };
        width.SetResourceReference(StyleProperty, "Lab.Field"); height.SetResourceReference(StyleProperty, "Lab.Field");
        panel.Children.Add(new TextBlock { Text = "Largura (px)", Margin = new Thickness(0, 18, 0, 0) }); panel.Children.Add(width);
        panel.Children.Add(new TextBlock { Text = "Altura (px)" }); panel.Children.Add(height);
        var ratio = new CheckBox { Content = "Manter proporção", IsChecked = true, Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(ratio);
        bool syncing = false;
        width.TextChanged += (_, _) => { if (syncing || ratio.IsChecked != true || !int.TryParse(width.Text, out var w)) return; syncing = true; height.Text = Math.Max(1, Math.Round((double)w * size.Height / size.Width)).ToString(); syncing = false; };
        height.TextChanged += (_, _) => { if (syncing || ratio.IsChecked != true || !int.TryParse(height.Text, out var h)) return; syncing = true; width.Text = Math.Max(1, Math.Round((double)h * size.Width / size.Height)).ToString(); syncing = false; };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }; error.SetResourceReference(TextBlock.ForegroundProperty, "Lab.error"); panel.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = MakeButton("Cancelar", "X", () => dialog.DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
        actions.Children.Add(MakeButton("Aplicar tamanho", "Check", () =>
        {
            if (!int.TryParse(width.Text, out var w) || !int.TryParse(height.Text, out var h) || !CaptureEditorDocument.ValidSize(w, h)) { error.Text = "Use 8–32.000 px por lado, até 64 megapixels."; return; }
            _editor.ResizeImage(w, h); dialog.DialogResult = true;
        }, primary: true));
        panel.Children.Add(actions); dialog.Content = panel; LabMotion.SetEntrance(panel, "Popup");
        dialog.SourceInitialized += (_, _) => ThemeService.ApplyToWindow(dialog); dialog.ShowDialog();
    }

    private void AddLabel(string text, bool emphasis = false)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, emphasis ? 18 : 8, 0), FontSize = 12 };
        label.SetResourceReference(TextBlock.ForegroundProperty, emphasis ? "Lab.accent-text" : "Lab.muted"); _panel.Children.Add(label);
    }
    private ComboBox Combo(IEnumerable<string> choices, string selected, Action<string> change, double width)
    {
        var combo = new ComboBox { ItemsSource = choices.ToArray(), SelectedItem = selected, Width = width, Margin = new Thickness(0, 0, 12, 0) };
        combo.SetResourceReference(StyleProperty, "Lab.Combo"); combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string value) change(value); }; _panel.Children.Add(combo);
        return combo;
    }
    private void Toggle(string text, bool state, Action<bool> change)
    {
        var control = new CheckBox { Content = text, IsChecked = state, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        control.SetResourceReference(Control.ForegroundProperty, "Lab.text");
        control.Checked += (_, _) => change(true); control.Unchecked += (_, _) => change(false); _panel.Children.Add(control);
    }
    private Button AddButton(string text, string icon, Action click, bool primary = false) { var button = MakeButton(text, icon, click, primary); _panel.Children.Add(button); return button; }
    private static Button MakeButton(string text, string icon, Action click, bool primary = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new LabIcon { Kind = icon, Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0) });
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1) });
        content.Children.Add(label);
        var button = new Button { Content = content, Margin = new Thickness(0, 0, 8, 0) };
        button.SetResourceReference(StyleProperty, primary ? "Lab.Pilot.PrimaryButton" : "Lab.Button"); button.Click += (_, _) => click(); return button;
    }
}
