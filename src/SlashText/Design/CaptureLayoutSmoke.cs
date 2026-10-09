using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

internal static class CaptureLayoutSmoke
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var theme in new[] { "Light", "Dark", "System" })
        {
            ThemeService.Apply(theme);
            using var bitmap = new System.Drawing.Bitmap(900, 500);
            var overlay = new RegionCaptureWindow(bitmap, pilotVisuals: true);
            Window? toolbarHost = null;
            try
            {
                overlay.SetSelectionForEvidence(new Rect(40, 40, 750, 350));
                var toolbar = overlay.ToolbarForEvidence; toolbar.Visibility = Visibility.Visible; LabMotion.SetReduced(toolbar, true);
                toolbarHost = new Window { Content = toolbar, Width = 900, SizeToContent = SizeToContent.Height,
                    WindowStyle = WindowStyle.None, ShowInTaskbar = false };
                toolbarHost.Show(); await SettleAsync();
                var textButton = Descendants(toolbar).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Extrair texto da região");
                Require(textButton.Content is StackPanel textContent && textContent.Children.OfType<TextBlock>().Any(t => t.Text == "Extrair texto"),
                    "OCR contains the actual text label, beyond its automation name");
                foreach (var compact in new[] { false, true })
                {
                    await SettleAsync();
                    overlay.SetDensityForEvidence(compact);
                    overlay.FitToolbarForEvidence(compact ? 530 : 900);
                    CheckDistribution(overlay);
                    Require(textButton.Visibility == Visibility.Visible, "OCR stays visible in both densities");
                    Save(toolbar, output, $"{theme}-toolbar-{(compact ? "compact" : "normal")}", compact ? 530 : 900);
                    var menu = overlay.OverflowForEvidence();
                    var menuSurface = new Border { Child = menu, CornerRadius = new CornerRadius(10), Padding = new Thickness(8) };
                    menuSurface.SetResourceReference(Border.BackgroundProperty, "Lab.panel");
                    Save(menuSurface, output, $"{theme}-more-{(compact ? "compact" : "normal")}", 310);
                }
                overlay.SetDensityForEvidence(false);
                foreach (var privacy in new[] { CaptureAnnotationKind.Blur, CaptureAnnotationKind.Pixelate })
                {
                    var label = privacy == CaptureAnnotationKind.Blur ? "Desfocar" : "Pixelizar";
                    overlay.OverflowForEvidence().Children.OfType<Button>().Single(b => AutomationProperties.GetName(b) == label)
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await SettleAsync(); overlay.SetDensityForEvidence(false); overlay.FitToolbarForEvidence(900);
                    CheckDistribution(overlay);
                    Require(overlay.ToolForEvidence(privacy).Visibility == Visibility.Visible, "Active privacy tool appears in toolbar");
                    Save(toolbar, output, $"{theme}-toolbar-{privacy}", 900);
                }
            }
            finally { toolbarHost?.Close(); overlay.Close(); }

            foreach (var maximized in new[] { false, true })
            {
                var ownerRoot = new Grid(); ownerRoot.SetResourceReference(Panel.BackgroundProperty, "Lab.bg");
                var owner = new Window { Content = ownerRoot, Width = 1080, Height = 900, ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen };
                owner.Show(); if (maximized) owner.WindowState = WindowState.Maximized;
                await SettleAsync();
                // Lower bounds reproduce the same defect even on a small CI desktop.
                var settings = new CaptureSettings(); var before = System.Text.Json.JsonSerializer.Serialize(settings);
                var dialog = new CaptureRuleDialog(settings) { Owner = owner, MaxHeight = 400, MaxWidth = 600 };
                LabMotion.SetReduced(dialog, true); dialog.Show(); await SettleAsync();
                var background = (FrameworkElement)dialog.Content;
                Require(Math.Abs(background.ActualHeight - ownerRoot.ActualHeight) < 2 && Math.Abs(background.ActualWidth - ownerRoot.ActualWidth) < 2,
                    "Backdrop covers the entire owner client area, without height/width caps");
                var center = dialog.Surface.TransformToAncestor(background).Transform(new Point(dialog.Surface.ActualWidth / 2, dialog.Surface.ActualHeight / 2));
                Require(Math.Abs(center.X - background.ActualWidth / 2) < 2 && Math.Abs(center.Y - background.ActualHeight / 2) < 2,
                    "Settings surface is centered in normal and maximized owners");
                Save(background, output, $"{theme}-settings-{(maximized ? "maximized" : "normal")}", background.ActualWidth, preserveLayout: true);
                Require(!ModalBackdrop.DismissAt(dialog.Surface, new Point(40, 40), dialog.Close), "Inside click keeps settings open");
                Require(ModalBackdrop.DismissAt(dialog.Surface, new Point(-10, 40), dialog.Close), "Outside click dismisses settings");
                Require(System.Text.Json.JsonSerializer.Serialize(settings) == before, "Backdrop dismissal preserves settings");
                owner.Close();
            }
        }
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: labeled OCR, toolbar/menu partition without duplicates, active privacy tools, normal/compact rendering, full backdrop and centered settings in normal/maximized owners, safe dismissal, all themes.");
    }

    private static void CheckDistribution(RegionCaptureWindow overlay)
    {
        var menu = overlay.OverflowForEvidence();
        var labels = menu.Children.OfType<Button>().Select(b => AutomationProperties.GetName(b)).ToArray();
        foreach (var (label, tool) in new[] {
            ("Caneta", CaptureAnnotationKind.Pencil), ("Marca-texto", CaptureAnnotationKind.Highlighter),
            ("Formas, setas e números", CaptureAnnotationKind.Rectangle), ("Texto", CaptureAnnotationKind.Text),
            ("Emotes e meus emojis", CaptureAnnotationKind.Stamp), ("Desfocar", CaptureAnnotationKind.Blur), ("Pixelizar", CaptureAnnotationKind.Pixelate) })
            Require((overlay.ToolForEvidence(tool).Visibility == Visibility.Visible ? 1 : 0) + labels.Count(value => value == label) == 1,
                "Tool is reachable exactly once: " + label);
        Require(!labels.Contains("Selecionar / mover") && labels.Count(value => value == "Refazer seleção · R") == 1 && labels.Count(value => value == "Limpar marcações") == 1,
            "Selection stays in toolbar; reset/clear are only menu actions");
        Require(!menu.Children.OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Limpar marcações").IsEnabled,
            "Clear stays disabled with no annotations");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
        if (parent is ContentControl { Content: DependencyObject content } && VisualTreeHelper.GetChildrenCount(parent) == 0)
        { yield return content; foreach (var child in Descendants(content)) yield return child; }
    }

    private static Task SettleAsync() => System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
    private static void Save(FrameworkElement visual, string output, string name, double width, bool preserveLayout = false)
    {
        double height;
        if (preserveLayout) { width = visual.ActualWidth; height = visual.ActualHeight; }
        else
        {
            visual.InvalidateMeasure(); visual.Measure(new Size(width, double.PositiveInfinity));
            height = visual.DesiredSize.Height; visual.Arrange(new Rect(0, 0, width, height)); visual.UpdateLayout();
        }
        Require(width > 0 && height > 0, $"Rendered surface has size: {name}, {visual.Visibility}, {visual.DesiredSize}, loaded={visual.IsLoaded}");
        if (name.Contains("-toolbar-"))
            foreach (var button in Descendants(visual).OfType<System.Windows.Controls.Primitives.ButtonBase>().Where(b => b.IsVisible))
            {
                var bounds = button.TransformToAncestor(visual).TransformBounds(new Rect(button.RenderSize));
                Require(bounds.Left >= -1 && bounds.Right <= visual.ActualWidth + 1, "Toolbar command is not clipped: " + AutomationProperties.GetName(button));
            }
        var render = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        // Capture the element itself, without its centering offset in the evidence host.
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen()) context.DrawRectangle(new VisualBrush(visual), null, new Rect(0, 0, width, height));
        render.Render(drawing); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(render));
        using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
