using Microsoft.Win32;
using SlashText.Design;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SlashText.Views;

public partial class DesignGalleryWindow : Window
{
    private string _theme = "Light";
    private bool _ready;
    private readonly string? _smokeOutput;
    public DesignGalleryWindow(string? smokeOutput = null)
    {
        _smokeOutput = smokeOutput;
        InitializeComponent();
        InvalidField.SetBinding(TextBox.TextProperty, new Binding(nameof(Example.Value))
        { Source = new Example(), Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        foreach (var name in new[] { "Keyboard", "Camera", "Languages", "BarChart3", "Settings", "Info", "Monitor", "ScanLine", "AppWindow", "ScrollText", "Film", "Video", "PenLine", "Palette", "Save", "FolderOpen", "Undo2", "Redo2" })
        {
            var icon = new LabIcon { Kind = name, Width = 24, Height = 24, Margin = new Thickness(0,0,18,12), ToolTip = name };
            icon.SetResourceReference(LabIcon.ForegroundProperty, "Lab.text"); IconsPanel.Children.Add(icon);
        }
    }
    private async void GalleryLoaded(object sender, RoutedEventArgs e)
    {
        _ready = true;
        SystemEvents.UserPreferenceChanged += WindowsPreferenceChanged;
        SystemParameters.StaticPropertyChanged += AnimationPreferenceChanged;
        ApplyTheme(false); ValidateField(InvalidField, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
        if (_smokeOutput is null) return;
        try { await RunSmoke(); Application.Current.Shutdown(0); }
        catch (Exception exception)
        {
            Directory.CreateDirectory(_smokeOutput); File.WriteAllText(Path.Combine(_smokeOutput, "failure.txt"), exception.ToString());
            Application.Current.Shutdown(1);
        }
    }
    private void GalleryClosed(object? sender, EventArgs e)
    {
        _ready = false;
        SystemEvents.UserPreferenceChanged -= WindowsPreferenceChanged;
        SystemParameters.StaticPropertyChanged -= AnimationPreferenceChanged;
    }
    private void WindowsPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_ready && _theme == "System") Dispatcher.BeginInvoke(() => { if (_ready) ApplyTheme(true); });
    }
    private void AnimationPreferenceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_ready || e.PropertyName != nameof(SystemParameters.ClientAreaAnimation)) return;
        Dispatcher.BeginInvoke(() => { if (_ready) ApplyMotionPreference(); });
    }
    private void ThemeChanged(object sender, RoutedEventArgs e)
    {
        _theme = (string)((RadioButton)sender).Tag;
        if (_ready) ApplyTheme(true);
    }
    private void ApplyTheme(bool animate)
    {
        var dark = _theme == "Dark" || _theme == "System" && Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int light && light == 0;
        LabPalette.Apply(Resources, dark, animate && LabMotion.Allowed(this));
        // Popup HWND inherits resources, but its motion preference is explicitly propagated.
        LabMotion.SetReduced(PopupContent, LabMotion.GetReduced(this));
    }
    private void ApplyMotionPreference()
    {
        var reduced = ReducedSwitch.IsChecked == true || !SystemParameters.ClientAreaAnimation;
        LabMotion.SetReduced(this, reduced); LabMotion.SetReduced(PopupContent, reduced);
        if (reduced) ApplyTheme(false); // cancel any in-flight color transition as well
    }
    private void MotionChanged(object sender, RoutedEventArgs e) { if (_ready) ApplyMotionPreference(); }
    private void TabChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        Status.Text = $"Aba selecionada: {((RadioButton)sender).Content}. Demonstração do componente.";
    }
    private void ActionClicked(object sender, RoutedEventArgs e) => Status.Text = $"{((Button)sender).Content}: clique recebido.";
    private void ReplayClicked(object sender, RoutedEventArgs e) => LabMotion.PlayEntrance(PageSurface);
    private void PopupClicked(object sender, RoutedEventArgs e) => OptionsPopup.IsOpen = true;
    private void PopupOpened(object? sender, EventArgs e) { LabMotion.PlayEntrance(PopupContent); ClosePopupButton.Focus(); }
    private void PopupClosed(object? sender, EventArgs e) => PopupButton.Focus();
    private void ClosePopupClicked(object sender, RoutedEventArgs e) => OptionsPopup.IsOpen = false;
    private void PopupKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { OptionsPopup.IsOpen = false; e.Handled = true; } }
    private void ValidateField(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        var expression = InvalidField.GetBindingExpression(TextBox.TextProperty);
        if (expression is null) return;
        var empty = string.IsNullOrWhiteSpace(InvalidField.Text);
        if (empty) Validation.MarkInvalid(expression, new ValidationError(new RequiredRule(), expression, "Campo obrigatório.", null));
        else Validation.ClearInvalid(expression);
        ValidationMessage.Text = empty ? "Campo obrigatório. Digite para corrigir." : "Campo válido.";
        ValidationMessage.SetResourceReference(TextBlock.ForegroundProperty, empty ? "Lab.error" : "Lab.green");
    }
    private async Task RunSmoke()
    {
        Directory.CreateDirectory(_smokeOutput!);
        ReducedSwitch.IsChecked = true;
        ApplyMotionPreference();
        foreach (var theme in new[] { "Light", "Dark" })
        {
            (theme == "Dark" ? DarkTheme : LightTheme).IsChecked = true;
            _theme = theme; ApplyTheme(false);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            UpdateLayout();
            Require(Validation.GetHasError(InvalidField), "Invalid field must expose Validation.HasError");
            InvalidField.Text = "Validado"; Require(!Validation.GetHasError(InvalidField), "Error must clear after input"); InvalidField.Text = "";
            EnabledSwitch.IsChecked = false; EnabledSwitch.IsChecked = true;
            EnabledSwitch.ApplyTemplate();
            var thumb = (FrameworkElement)EnabledSwitch.Template.FindName("Thumb", EnabledSwitch);
            Require(((TranslateTransform)thumb.RenderTransform).X == 18, "Switch must reach checked position in reduced motion");
            var expected = theme == "Dark" ? "#FFF3F3F3" : "#FF202024";
            Require(ValidField.Foreground.ToString() == expected, "TextBox theme did not update");
            FormatCombo.IsDropDownOpen = true;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            FormatCombo.UpdateLayout();
            var dropdown = (Border)FormatCombo.Template.FindName("PopupSurface", FormatCombo);
            Require(dropdown.Background.ToString() == (theme == "Dark" ? "#FF242424" : "#FFFFFFFF"), "ComboBox popup theme");
            FormatCombo.IsDropDownOpen = false;
            OptionsPopup.IsOpen = true;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Require(PopupContent.Background.ToString() == (theme == "Dark" ? "#FF242424" : "#FFFFFFFF"), "Detached popup theme");
            OptionsPopup.IsOpen = false;
            Require(Descendants(PrimaryButton).OfType<TextBlock>().Any(t => t.Text == "Ação principal" && t.Foreground.ToString() == (theme == "Dark" ? "#FF082126" : "#FFFFFFFF")), "Primary label must inherit on-accent color: " + string.Join(",", Descendants(PrimaryButton).OfType<TextBlock>().Select(t => t.Text + "=" + t.Foreground)));
            Require(Descendants(NormalButton).OfType<TextBlock>().Any(t => t.Text == "Ação secundária" && t.Foreground.ToString() == expected), "Neutral label must follow theme");
            Require(Descendants(FormatCombo).OfType<TextBlock>().Any(t => t.Text == "PNG — imagem" && t.Foreground.ToString() == expected), "Combo selection label must follow theme");
            // Detach from HWND to avoid the runner desktop clipping the offscreen snapshot.
            Content = null;
            UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Require(VisualTreeHelper.GetParent(GalleryRoot) is null, "Offscreen gallery must be detached from HWND content presenter");
            GalleryRoot.Resources = Resources;
            foreach (var size in new[] { new Size(1440,900), new Size(980,680) })
            {
                GalleryRoot.Width = size.Width; GalleryRoot.Height = size.Height;
                GalleryRoot.Measure(size); GalleryRoot.Arrange(new Rect(size)); GalleryRoot.UpdateLayout();
                GalleryScroller.ScrollToTop(); GalleryRoot.UpdateLayout();
                Require(Math.Abs(GalleryRoot.ActualWidth - size.Width) < .1 && Math.Abs(GalleryRoot.ActualHeight - size.Height) < .1,
                    $"Offscreen layout differs from requested size: {GalleryRoot.ActualWidth}x{GalleryRoot.ActualHeight}, expected {size}");
                foreach (var scale in new[] { 1d, 1.25d, 1.5d, 2d })
                {
                    var bitmap = new RenderTargetBitmap((int)(size.Width*scale), (int)(size.Height*scale), 96*scale, 96*scale, PixelFormats.Pbgra32);
                    bitmap.Render(GalleryRoot);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(_smokeOutput!, $"gallery-{theme}-{size.Width}x{size.Height}-{scale*100:0}.png")); png.Save(file);
                }
            }
            GalleryRoot.ClearValue(WidthProperty); GalleryRoot.ClearValue(HeightProperty);
            Content = GalleryRoot;
            GalleryRoot.Resources = new ResourceDictionary();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
        // Exercise real clocks and interruption as well as the reduced-motion path.
        GalleryRoot.ClearValue(WidthProperty); GalleryRoot.ClearValue(HeightProperty);
        ReducedSwitch.IsChecked = false; ApplyMotionPreference();
        LabMotion.PlayEntrance(PageSurface); LabMotion.PlayEntrance(PopupContent);
        EnabledSwitch.IsChecked = false; EnabledSwitch.IsChecked = true;
        await Task.Delay(250);
        Require(PageSurface.Opacity == 1, "Entrance must settle at full opacity");
        ReducedSwitch.IsChecked = true;
        Require(!LabMotion.Allowed(PageSurface), "Reduced motion must propagate");
        File.WriteAllText(Path.Combine(_smokeOutput!, "result.txt"),
            "PASS: themes, validation, switch interruption/rest position, ComboBox and Popup resources, entrance clocks, reduced motion.\n" +
            "16 offscreen PNGs: 1440x900 and 980x680 DIP at 100/125/150/200%. This is NOT a physical mixed-monitor test.\n" +
            "Manual pending: hover/pressed/focus, Windows theme event, actual DPI/monitors, fonts, visual approval.\n");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Example { public string Value { get; set; } = ""; }
    private sealed class RequiredRule : ValidationRule
    { public override ValidationResult Validate(object value, CultureInfo cultureInfo) => string.IsNullOrWhiteSpace(value?.ToString()) ? new ValidationResult(false, "Campo obrigatório.") : ValidationResult.ValidResult; }
}
