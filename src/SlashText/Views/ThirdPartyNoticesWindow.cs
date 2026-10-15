using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlashText.Design;
using SlashText.Services;

namespace SlashText.Views;

internal sealed class ThirdPartyNoticesWindow : Window
{
    internal ComboBox Components { get; }
    internal TextBox LicenseText { get; }
    internal Border Surface { get; }
    internal void EnableBackdrop() => ModalBackdrop.Attach(this, Surface, Close);
    internal ThirdPartyNoticesWindow()
    {
        var notices = ThirdPartyNotices.Load();
        Title = UiText.Get("Avisos de terceiros");
        Width = Math.Min(920, SystemParameters.WorkArea.Width - 24); Height = Math.Min(720, SystemParameters.WorkArea.Height - 24);
        MinWidth = 420; MinHeight = 360; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(FontFamilyProperty, "Lab.Font");
        var root = new Grid { Margin = new Thickness(24) };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star) }) root.RowDefinitions.Add(new RowDefinition { Height = height });
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        header.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        var close = new Button { Content = new LabIcon { Kind = "X", Width = 18, Height = 18 }, Width = 36, Height = 36, Padding = new Thickness(8), Margin = new Thickness(14, 0, 0, 0) };
        close.SetResourceReference(StyleProperty, "Lab.Button"); AutomationProperties.SetName(close, UiText.Get("Fechar"));
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        var titles = new StackPanel();
        var title = new TextBlock { Text = Title, FontSize = 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap }; title.SetResourceReference(TextBlock.ForegroundProperty, "Lab.text"); titles.Children.Add(title);
        var subtitle = new TextBlock { Text = UiText.Get("Licenças e atribuições incluídas no aplicativo. Consulta disponível sem internet."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) }; subtitle.SetResourceReference(TextBlock.ForegroundProperty, "Lab.muted"); titles.Children.Add(subtitle);
        header.Children.Add(titles); root.Children.Add(header);
        Components = new ComboBox { ItemsSource = notices.Documents, DisplayMemberPath = nameof(ThirdPartyNotice.DisplayName), Margin = new Thickness(0, 0, 0, 14), MinHeight = 38 };
        Components.SetResourceReference(StyleProperty, "Lab.Combo");
        AutomationProperties.SetName(Components, UiText.Get("Componente")); Grid.SetRow(Components, 1); root.Children.Add(Components);
        LicenseText = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, FontFamily = new FontFamily("Consolas"), FontSize = 13, Padding = new Thickness(12), IsReadOnlyCaretVisible = true };
        LicenseText.SetResourceReference(Control.BackgroundProperty, "Lab.bg"); LicenseText.SetResourceReference(Control.ForegroundProperty, "Lab.text"); LicenseText.SetResourceReference(Control.BorderBrushProperty, "Lab.line");
        AutomationProperties.SetName(LicenseText, UiText.Get("Texto da licença")); Grid.SetRow(LicenseText, 2); root.Children.Add(LicenseText);
        Components.SelectionChanged += (_, _) =>
        {
            if (Components.SelectedItem is ThirdPartyNotice selected)
            { LicenseText.Text = $"{selected.Name}\n{selected.Version}\n{selected.Source}\nSHA-256: {selected.Sha256}\n\n" + notices.Text(selected); LicenseText.ScrollToHome(); }
        };
        Components.SelectedIndex = 0;
        Surface = new Border { Child = root, Margin = new Thickness(8), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1) };
        Surface.SetResourceReference(Border.BackgroundProperty, "Lab.panel"); Surface.SetResourceReference(Border.BorderBrushProperty, "Lab.line-strong"); Content = Surface;
        SourceInitialized += (_, _) => ThemeService.ApplyToWindow(this); Loaded += (_, _) => Components.Focus();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        LabMotion.SetEntrance(Surface, "Popup");
    }
}
