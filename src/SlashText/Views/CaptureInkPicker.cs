using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SlashText.Design;

namespace SlashText.Views;

/// <summary>Compact color strip; precise RGB fields are disclosed on demand.</summary>
public static class CaptureInkPicker
{
    private static readonly (string Name, string Hex)[] Colors =
    [
        ("Preto", "000000"), ("Branco", "FFFFFF"), ("Vermelho", "E84E60"),
        ("Laranja", "FF5F00"), ("Amarelo", "FFD800"), ("Verde", "008758"),
        ("Turquesa", "00A6BF"), ("Azul", "004CDF"), ("Violeta", "6600CC"),
        ("Rosa escuro", "AF0F64"), ("Cinza", "A6A6A6"), ("Cinza claro", "D4D4D4"),
        ("Cinza médio", "838383"), ("Cinza escuro", "595959"), ("Âmbar", "FFAE00"),
        ("Amarelo vivo", "FFEA00"), ("Lima", "AAE31B"), ("Verde vivo", "00D900"),
        ("Índigo", "4400B3"), ("Roxo", "800080"), ("Pêssego", "F7D7C4"),
        ("Bege", "BF966A"), ("Marrom", "90552F"), ("Marrom escuro", "643C2B"),
        ("Rosa claro", "FB83FF"), ("Laranja claro", "FFC481"), ("Amarelo claro", "FFFF89"),
        ("Verde claro", "A2F7B6"), ("Azul claro", "7FD7FD"), ("Lilás", "B9A7FB")
    ];

    public static bool TryParseHex(string text, out int argb)
    {
        var value = text.Trim().TrimStart('#');
        if (value.Length == 6 && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        { argb = unchecked((int)0xFF000000) | rgb; return true; }
        argb = 0; return false;
    }

    internal static Border CreateContent(CaptureWorkbenchEditor editor) => CreateContent(
        () => editor.InkArgb, editor.SetColor, () => editor.InkThickness, editor.SetThickness);

    internal static Border CreateContent(Func<int> getInk, Action<int> setInk, Func<float> getThickness, Action<float> setThickness)
    {
        var panel = new StackPanel { Margin = new Thickness(14) };
        var border = new Border { Width = 700, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = panel };
        border.SetResourceReference(Border.BackgroundProperty, "Lab.raised");
        border.SetResourceReference(Border.BorderBrushProperty, "Lab.line-strong");
        border.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "Lab.text");
        TextBlock Label(string text)
        {
            var label = new TextBlock { Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Lab.text"); return label;
        }
        var row = new Grid { MinHeight = 44 };
        foreach (var width in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto })
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        void Place(UIElement control, int column) { Grid.SetColumn(control, column); row.Children.Add(control); }
        var label = Label("Cores"); label.Margin = new Thickness(0, 0, 10, 0); Place(label, 0);
        var colors = new StackPanel { Name = "InkPalette", Orientation = Orientation.Horizontal };
        var scroll = new ScrollViewer { Content = colors, Height = 44, Margin = new Thickness(0, 0, 4, 0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Place(scroll, 2);
        var rings = new System.Collections.Generic.List<(int Argb, Border Ring, Button Button)>();
        var preview = new System.Windows.Shapes.Path { Name = "InkPreview", Width = 286, Height = 54,
            Data = Geometry.Parse("M 8,34 C 65,0 94,14 138,28 S 226,50 278,18"),
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Stretch = Stretch.Fill };
        border.SizeChanged += (_, _) =>
        {
            preview.Visibility = border.ActualWidth < 460 ? Visibility.Collapsed : Visibility.Visible;
            preview.Width = border.ActualWidth < 580 ? 180 : 286;
        };
        var sizeValue = Label(""); sizeValue.Name = "InkSizeValue"; sizeValue.Width = 44; sizeValue.TextAlignment = TextAlignment.Right;
        var rgbPanel = new StackPanel { Name = "InkRgbPanel", Visibility = Visibility.Collapsed, Margin = new Thickness(0, 12, 0, 0) };
        var fields = new WrapPanel();
        TextBox Field(string caption, string name, double width, int maxLength)
        {
            var group = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 6) };
            var title = Label(caption); title.Margin = new Thickness(0, 0, 6, 0); group.Children.Add(title);
            var field = new TextBox { Name = name, Width = width, MaxLength = maxLength };
            field.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Field");
            System.Windows.Automation.AutomationProperties.SetName(field, caption == "Hex" ? "Cor hexadecimal" : $"Componente {caption}, de 0 a 255");
            group.Children.Add(field); fields.Children.Add(group); return field;
        }
        var r = Field("R", "InkRed", 62, 3); var g = Field("G", "InkGreen", 62, 3); var b = Field("B", "InkBlue", 62, 3);
        var hex = Field("Hex", "InkHex", 110, 7);
        var apply = new Button { Name = "ApplyInkHex", Content = "Aplicar", Margin = new Thickness(0, 0, 0, 6) };
        apply.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Button"); fields.Children.Add(apply);
        rgbPanel.Children.Add(fields);
        var error = Label("Use R, G e B de 0 a 255 ou uma cor #RRGGBB."); error.FontSize = 11;
        error.TextWrapping = TextWrapping.Wrap; error.Visibility = Visibility.Collapsed;
        error.SetResourceReference(TextBlock.ForegroundProperty, "Lab.error"); rgbPanel.Children.Add(error);
        bool syncing = false, hexEdited = false;
        void SetFields(int argb)
        {
            var c = System.Drawing.Color.FromArgb(argb);
            syncing = true;
            r.Text = c.R.ToString(); g.Text = c.G.ToString(); b.Text = c.B.ToString();
            hex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}"; syncing = false;
        }
        bool ReadRgb(out int argb)
        {
            argb = 0;
            if (!byte.TryParse(r.Text, out var red) || !byte.TryParse(g.Text, out var green) || !byte.TryParse(b.Text, out var blue)) return false;
            argb = System.Drawing.Color.FromArgb(red, green, blue).ToArgb(); return true;
        }
        void Refresh()
        {
            var c = System.Drawing.Color.FromArgb(getInk());
            preview.Stroke = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)); preview.StrokeThickness = getThickness();
            sizeValue.Text = $"{getThickness():0} px"; SetFields(getInk()); error.Visibility = Visibility.Collapsed;
            foreach (var item in rings)
            {
                if (item.Argb == getInk()) item.Ring.SetResourceReference(Border.BorderBrushProperty, "Lab.accent");
                else item.Ring.BorderBrush = Brushes.Transparent;
                System.Windows.Automation.AutomationProperties.SetItemStatus(item.Button, item.Argb == getInk() ? "Selecionada" : "");
            }
        }
        foreach (var item in Colors)
        {
            TryParseHex(item.Hex, out var argb); var c = System.Drawing.Color.FromArgb(argb);
            var dot = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)), BorderThickness = new Thickness(1) };
            dot.SetResourceReference(Border.BorderBrushProperty, "Lab.line-strong");
            var ring = new Border { Width = 32, Height = 32, Padding = new Thickness(2), BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(16), BorderBrush = Brushes.Transparent, Child = dot };
            var button = new Button { Content = ring, Width = 38, Height = 40, Padding = new Thickness(1), Margin = new Thickness(0, 2, 0, 2),
                ToolTip = $"{item.Name} · #{item.Hex}", Tag = argb };
            button.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Pilot.ToolButton");
            System.Windows.Automation.AutomationProperties.SetName(button, item.Name);
            button.Click += (_, _) => { setInk(argb); Refresh(); };
            rings.Add((argb, ring, button)); colors.Children.Add(button);
        }
        Button Arrow(bool forward)
        {
            var icon = new LabIcon { Kind = "ArrowLeft", Width = 14, Height = 14, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(forward ? 180 : 0) };
            icon.SetResourceReference(LabIcon.ForegroundProperty, "Lab.text");
            var button = new Button { Content = icon, Width = 30, Height = 40, Padding = new Thickness(4), Margin = new Thickness(0, 0, 4, 0), ToolTip = forward ? "Próximas cores" : "Cores anteriores" };
            button.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Pilot.ToolButton");
            System.Windows.Automation.AutomationProperties.SetName(button, button.ToolTip.ToString());
            button.Click += (_, _) => scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset + (forward ? 228 : -228)); return button;
        }
        var previous = Arrow(false); var next = Arrow(true); Place(previous, 1); Place(next, 3);
        scroll.ScrollChanged += (_, _) => { previous.IsEnabled = scroll.HorizontalOffset > 0; next.IsEnabled = scroll.HorizontalOffset < scroll.ScrollableWidth; };
        var rgb = new Button { Name = "OpenInkRgb", Content = "RGB", Padding = new Thickness(12, 6, 12, 6) };
        rgb.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Button"); rgb.ToolTip = "Definir uma cor específica por RGB ou hexadecimal";
        rgb.Click += (_, _) => { rgbPanel.Visibility = rgbPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; if (rgbPanel.IsVisible) r.Focus(); };
        Place(rgb, 4); panel.Children.Add(row); panel.Children.Add(rgbPanel);
        hex.TextChanged += (_, _) =>
        {
            if (syncing) return; hexEdited = true;
            if (TryParseHex(hex.Text, out var argb)) SetFields(argb);
        };
        foreach (var field in new[] { r, g, b }) field.TextChanged += (_, _) =>
        { if (syncing) return; hexEdited = false; if (ReadRgb(out var argb)) SetFields(argb); };
        void Apply()
        {
            int argb;
            if (!(hexEdited ? TryParseHex(hex.Text, out argb) : ReadRgb(out argb)))
            { error.Visibility = Visibility.Visible; return; }
            setInk(argb); Refresh();
        }
        apply.Click += (_, _) => Apply();
        fields.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { Apply(); e.Handled = true; } };
        var sizeRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var width in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto })
            sizeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        var sizeLabel = Label("Espessura"); sizeLabel.Margin = new Thickness(0, 0, 10, 0); sizeRow.Children.Add(sizeLabel);
        var slider = new Slider { Name = "InkThickness", Minimum = 1, Maximum = 24, Value = getThickness(),
            TickFrequency = 1, IsSnapToTickEnabled = true, SmallChange = 1, LargeChange = 4, MinWidth = 70 };
        slider.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Pilot.InkSlider");
        System.Windows.Automation.AutomationProperties.SetName(slider, "Espessura do traço em pixels");
        slider.ValueChanged += (_, _) => { setThickness((float)slider.Value); Refresh(); };
        Grid.SetColumn(slider, 1); sizeRow.Children.Add(slider);
        Grid.SetColumn(sizeValue, 2); sizeValue.Margin = new Thickness(10, 0, 10, 0); sizeRow.Children.Add(sizeValue);
        Grid.SetColumn(preview, 3); sizeRow.Children.Add(preview); panel.Children.Add(sizeRow);
        var hint = Label("Aplica às próximas anotações."); hint.FontSize = 11; hint.Margin = new Thickness(0, 4, 0, 0);
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Lab.muted"); panel.Children.Add(hint);
        Refresh(); LabMotion.SetEntrance(border, "Popup"); return border;
    }

    public static void Show(Button anchor, CaptureWorkbenchEditor editor)
    {
        var content = CreateContent(editor); content.MaxWidth = Math.Max(320, SystemParameters.WorkArea.Width - 48);
        var scroll = new ScrollViewer { Content = content, MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 48),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom,
            StaysOpen = false, AllowsTransparency = true, VerticalOffset = 6, Child = scroll };
        LabMotion.SetReduced(content, LabMotion.GetReduced(editor));
        content.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { popup.IsOpen = false; e.Handled = true; } };
        popup.Opened += (_, _) => { LabMotion.PlayEntrance(content); content.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); };
        popup.Closed += (_, _) => anchor.Focus(); popup.IsOpen = true;
    }
}
